using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 혈영검마 (천마총 고수, 마교 좌호법): 체력 1100 · 공격 18 (임시, 천마총 배율 전 → 4950 · 36). 잔상을 남기는 검객. 결투장은 마교 수련장.
    // 고수 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 체력 50%에서 1.5초 전환 (무적), 같은 패턴은 연속 2번까지.
    //   혈영 베기: 가까우면 (2.8칸) 예고 0.5 · 앞 부채꼴 반경 2.6칸 120° ×1.0 → 그 자리에 잔상이 남아 0.5초 뒤 같은 베기 ×0.6
    //   혈선: 3~6칸이면 예고 0.6 · 4칸 직선으로 돌진하며 베기 (폭 1.2칸) ×1.0 → 잔상이 0.5초 뒤 같은 길을 다시 베기 ×0.6
    //   혈영보: 5칸 밖에 1.5초 넘게 있으면 예고 0.4 · 잔상을 남기고 플레이어 뒤로 순간이동해 바로 베기 (원거리 대응)
    //   흡혈: 공격이 맞을 때마다 그 공격력의 30%만큼 체력 회복
    // 2페이즈: 잔상이 하나 더 (0.5초 · 1.0초 뒤 두 번), 예고 20% 빨라져요
    public sealed class BloodShadowBrain : EnemyBrain, IMasterBrain
    {
        enum Pattern { None, Slash, Line, Blink }

        struct Echo // 잔상: 잠시 뒤 같은 베기를 한 번 더
        {
            public Pattern Kind;
            public Vector3 From, Dir;
            public float Timer;
            public Telegraph Marker;
        }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat;
        float _closeDistance, _farDistance, _decisionPause, _transitionTime, _phase2Ratio, _phase2Speed;
        float _slashWindup, _slashRadius, _slashAngle, _slashRatio;
        float _lineWindup, _lineMin, _lineMax, _lineLength, _lineWidth, _lineRatio, _lineTime;
        float _blinkDelay, _blinkWindup, _blinkBehind, _farTimer;
        float _echoDelay, _echoRatio, _lifesteal;
        float _decisionTimer, _transitionTimer, _lineLeft;
        Vector3 _lineFrom;
        bool _lineHit;
        readonly List<Echo> _echoes = new List<Echo>();

        // 이펙트 신호 (아트: Scripts/Art/InkBloodShadowFx). 판정과 상관없이 그림만 그려요.
        public enum FxKind { EchoStart, EchoCut, Blink, Drain }
        public struct FxCue { public FxKind Kind; public Vector3 From, To, Dir; public float Time; public bool Line; }
        public static event System.Action<BloodShadowBrain, FxCue> Cue;
        void Fx(FxCue cue) => Cue?.Invoke(this, cue);

        public override EnemyKind Kind => EnemyKind.BloodShadow;
        protected override string Key => "bloodshadow";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Master;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1f;
        protected override float ActiveTime => _pattern == Pattern.Line ? _lineTime + 0.05f : 0.1f;

        public override string AttackName => _pattern switch
        {
            Pattern.Slash => "혈영 베기", Pattern.Line => "혈선", Pattern.Blink => "혈영보", _ => null,
        };

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public int EchoesPending => _echoes.Count;

        public void SetZone(Vector3 center, Vector2 size) { }

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 2.8f);
            _farDistance = Tuning("far_distance", 5f);
            _decisionPause = Tuning("decision_pause", 0.4f);
            _transitionTime = Tuning("transition_time", 1.5f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _phase2Speed = Tuning("p2_windup_ratio", 0.8f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _slashWindup = Tuning("slash_windup", 0.5f);
            _slashRadius = Tuning("slash_radius", 2.6f);
            _slashAngle = Tuning("slash_angle", 120f);
            _slashRatio = Tuning("slash_ratio", 1f);
            _lineWindup = Tuning("line_windup", 0.6f);
            _lineMin = Tuning("line_min", 3f);
            _lineMax = Tuning("line_max", 6f);
            _lineLength = Tuning("line_length", 4f);
            _lineWidth = Tuning("line_width", 1.2f);
            _lineRatio = Tuning("line_ratio", 1f);
            _lineTime = Mathf.Max(0.05f, Tuning("line_time", 0.2f));
            _blinkDelay = Tuning("blink_delay", 1.5f);
            _blinkWindup = Tuning("blink_windup", 0.4f);
            _blinkBehind = Tuning("blink_behind", 1.5f);
            _echoDelay = Tuning("echo_delay", 0.5f);
            _echoRatio = Tuning("echo_ratio", 0.6f);
            _lifesteal = Tuning("lifesteal", 0.3f);
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            _farTimer = ToPlayer().magnitude > _farDistance ? _farTimer + dt : 0f; // 멀리서만 싸우면 혈영보
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
            TickEchoes(dt);
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_farTimer >= _blinkDelay) next = Pattern.Blink;
            else if (distance <= _closeDistance) next = Pattern.Slash;
            else if (distance >= _lineMin && distance <= _lineMax) next = Pattern.Line;
            else return false; // 다가가요
            if (next == _lastPattern && _repeat >= _maxRepeat && next != Pattern.Blink)
                next = next == Pattern.Slash ? (distance >= _lineMin ? Pattern.Line : Pattern.Slash) : Pattern.Slash;
            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            float speed = Phase == 2 ? _phase2Speed : 1f;
            RecoveryTime = Tuning("recovery", 0.6f);
            switch (next)
            {
                case Pattern.Slash: WindupTime = _slashWindup * speed; break;
                case Pattern.Line: WindupTime = _lineWindup * speed; break;
                case Pattern.Blink:
                    WindupTime = _blinkWindup * speed;
                    _farTimer = 0f;
                    break;
            }
            return true;
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsTransitioning) return;
            Vector3 to = ToPlayer();
            FaceToward(to);
            if (to.magnitude > _closeDistance * 0.8f) Move(to.normalized * MoveSpeed, dt);
        }

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Slash => TelegraphShapes.Lunge(_slashRadius, _slashAngle, 0f),
            Pattern.Line => TelegraphShapes.Rect(_lineWidth, _lineLength),
            _ => TelegraphShapes.Circle(BodyRadius + 0.4f), // 혈영보: 몸 둘레에 피 안개
        };

        // ── 판정

        protected override void OnActiveStarted()
        {
            switch (_pattern)
            {
                case Pattern.Slash:
                    Cut(Pattern.Slash, transform.position, AimDirection, _slashRatio);
                    AddEchoes(Pattern.Slash, transform.position, AimDirection);
                    break;
                case Pattern.Line:
                    _lineFrom = transform.position;
                    _lineLeft = _lineLength;
                    _lineHit = false;
                    AddEchoes(Pattern.Line, transform.position, AimDirection);
                    break;
                case Pattern.Blink: // 잔상을 남기고 플레이어 뒤로
                    AddEchoes(Pattern.Slash, transform.position, AimDirection);
                    Vector3 blinkFrom = transform.position;
                    Vector3 behind = Player.transform.position - Player.Facing * _blinkBehind;
                    Teleport(Director.FindFreeSpot(behind, BodyRadius + 0.1f)); // 벽 속이면 가까운 빈자리
                    Vector3 to = ToPlayer();
                    to.y = 0f;
                    if (to.sqrMagnitude > 1e-4f) AimDirection = to.normalized;
                    FaceToward(AimDirection);
                    Director.SpawnBurst(transform.position, 0.8f);
                    Cut(Pattern.Slash, transform.position, AimDirection, _slashRatio);
                    Fx(new FxCue { Kind = FxKind.Blink, From = blinkFrom, To = transform.position, Dir = AimDirection });
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            if (_pattern != Pattern.Line || _lineLeft <= 0f) return;
            float step = Mathf.Min(_lineLeft, _lineLength / _lineTime * dt);
            MoveRaw(AimDirection * step);
            _lineLeft -= step;
            if (!_lineHit && OnLine(_lineFrom, transform.position, Player.transform.position)) _lineHit = Hit(_lineRatio);
        }

        bool OnLine(Vector3 from, Vector3 to, Vector3 p)
        {
            Vector3 d = to - from;
            d.y = 0f;
            float length = d.magnitude;
            Vector3 dir = length > 1e-3f ? d / length : AimDirection;
            Vector3 rel = p - from;
            rel.y = 0f;
            float along = Vector3.Dot(rel, dir);
            float side = Mathf.Abs(Vector3.Dot(rel, new Vector3(dir.z, 0f, -dir.x)));
            return along >= -PlayerRadius && along <= length + PlayerRadius && side <= _lineWidth * 0.5f + PlayerRadius;
        }

        void Cut(Pattern kind, Vector3 from, Vector3 dir, float ratio)
        {
            if (kind == Pattern.Line)
            {
                if (OnLine(from, from + dir * _lineLength, Player.transform.position)) Hit(ratio);
                return;
            }
            if (HitShapes.InSector(from, dir, _slashRadius, _slashAngle, Player.transform.position, PlayerRadius)) Hit(ratio);
        }

        // 맞히면 흡혈
        bool Hit(float ratio)
        {
            if (!Player.TryReceiveHit(AttackPower * ratio, transform.position, HitKnockback, Body)) return false;
            Body.Heal(AttackPower * ratio * _lifesteal);
            Fx(new FxCue { Kind = FxKind.Drain, From = Player.transform.position, To = transform.position });
            return true;
        }

        // ── 잔상

        void AddEchoes(Pattern kind, Vector3 from, Vector3 dir)
        {
            int count = Phase == 2 ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
                marker.Show(kind == Pattern.Line ? TelegraphShapes.Rect(_lineWidth, _lineLength) : TelegraphShapes.Lunge(_slashRadius, _slashAngle, 0f));
                marker.SetPose(from, dir);
                marker.SetProgress(0f);
                _echoes.Add(new Echo { Kind = kind, From = from, Dir = dir, Timer = _echoDelay * (i + 1), Marker = marker });
                Fx(new FxCue { Kind = FxKind.EchoStart, From = from, Dir = dir, Time = _echoDelay * (i + 1), Line = kind == Pattern.Line });
            }
        }

        void TickEchoes(float dt)
        {
            for (int i = _echoes.Count - 1; i >= 0; i--)
            {
                var e = _echoes[i];
                e.Timer -= dt;
                if (e.Marker != null) e.Marker.SetProgress(1f - Mathf.Clamp01(e.Timer / Mathf.Max(0.01f, _echoDelay)));
                if (e.Timer > 0f)
                {
                    _echoes[i] = e;
                    continue;
                }
                if (e.Marker != null) Destroy(e.Marker.gameObject);
                _echoes.RemoveAt(i);
                Director.SpawnBurst(e.From + e.Dir * 1.2f, 0.8f);
                Fx(new FxCue { Kind = FxKind.EchoCut, From = e.From, Dir = e.Dir, Line = e.Kind == Pattern.Line });
                Cut(e.Kind, e.From, e.Dir, _echoRatio);
            }
        }

        protected override void OnAttackFinished() => _decisionTimer = _decisionPause;

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            _pattern = Pattern.None;
            _decisionTimer = _decisionPause;
        }

        // ── 페이즈

        protected override void OnHitTaken()
        {
            if (Phase == 1 && !IsTransitioning && Body.Health <= Body.MaxHealth * _phase2Ratio) StartTransition();
        }

        void StartTransition()
        {
            if (Attack.IsAttacking) CancelAttack();
            _pattern = Pattern.None;
            _transitionTimer = _transitionTime;
            Body.Targetable = false;
        }

        void EnterPhase2()
        {
            Phase = 2;
            Body.Targetable = true;
            _decisionTimer = _decisionPause;
            _repeat = 0;
        }

        protected override void OnDied()
        {
            foreach (var e in _echoes) if (e.Marker != null) Destroy(e.Marker.gameObject);
            _echoes.Clear();
        }
    }
}
