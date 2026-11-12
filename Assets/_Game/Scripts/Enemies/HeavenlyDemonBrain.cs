using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 천마 (천마총 최종 보스, 몬스터 시트 67행): 체력 3150 · 공격 25 · 강인함 6 · 방어 20% (천마총 배율 전 → 14175 · 50). 3페이즈.
    // 보스 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 페이즈 전환 2초 (무적), 같은 패턴은 연속 2번까지.
    // 1페이즈 · 천마검
    //   천마일검: 3칸 안이면 예고 0.6 · 앞 부채꼴 반경 3칸 140° ×1.0
    //   십자 검기: 멀면 예고 0.8 · 플레이어 쪽을 향한 십자로 검기 네 갈래 (10칸, 초속 10칸) ×0.7
    //   마기 장판: 10초마다 예고 0.8 · 플레이어 둘레 세 곳에 마기 장판 (반경 1.5칸, 5초, 안에 있으면 0.5초마다 ×0.3 · 이동 −20%)
    //   마기 손아귀: 6칸 밖에 2초 넘게 있으면 예고 0.7 · 직선 10칸으로 붙잡아 끌어와 (×0.6) 곧바로 천마일검 (원거리 대응)
    // 2페이즈 · 천마군림 (체력 66%): 일검이 "천마군림"이 돼요 (220°). 벨 때마다 플레이어 양옆 3칸에 환영이 나타나 0.5초 뒤 같이 벱니다 (×0.6).
    //   흡인: 12초마다 예고 1.0 · 2초 동안 둘레 6칸을 끌어당기다가 반경 2칸 폭발 ×1.2 (대시면 안 끌려요)
    // 3페이즈 · 해체대법 (체력 33%): 방 가장자리부터 마기가 차올라요. 안전 반경이 11칸에서 3초마다 1칸씩 줄어 5칸까지 (밖에 있으면 0.5초마다 ×0.2).
    //   검비: 8초마다 예고 1.0 · 1.5초 동안 플레이어 둘레에 검 12자루가 떨어져요 (예고 0.6초, 반경 1.2칸, ×0.6). 플레이어 절초 만검귀종의 거울판.
    public sealed class HeavenlyDemonBrain : EnemyBrain, IMasterBrain
    {
        enum Pattern { None, Slash, Cross, Miasma, Grasp, Pull, Rain }

        struct Mark
        {
            public Vector3 At, Dir;
            public float Timer, Total, Radius, Ratio, Angle;
            public bool Sector;
            public Telegraph Marker;
        }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat;
        float _decisionPause, _transitionTime, _p2Ratio, _p3Ratio;
        float _slashWindup, _slashRadius, _slashAngle, _slashAngle2, _slashRatio, _closeDistance;
        float _crossWindup, _crossRange, _crossSpeed, _crossRatio;
        float _miasmaWindup, _miasmaInterval, _miasmaTimer, _miasmaRadius, _miasmaLife, _miasmaRatio, _miasmaSlow;
        float _graspFar, _graspDelay, _graspWindup, _graspRange, _graspWidth, _graspRatio, _graspStop, _farTimer;
        bool _grasped;
        float _phantomSide, _phantomDelay, _phantomRatio;
        float _pullWindup, _pullTime, _pullInterval, _pullTimer, _pullRadius, _pullSpeed, _pullBurst, _pullRatio;
        float _rainWindup, _rainInterval, _rainTimer, _rainFall, _rainRadius, _rainRatio, _rainTime;
        int _rainCount, _rainDone;
        float _safeStart, _safeMin, _safeStep, _safeShrink, _safeRatio, _safeTick, _safeRadius, _safeTimer, _safeHurt;
        float _decisionTimer, _transitionTimer, _activeTimer;
        Vector3 _zoneCenter;
        Vector2 _zoneSize = new Vector2(24f, 18f);
        Telegraph _safeMarker;
        readonly List<Mark> _marks = new List<Mark>();

        public override EnemyKind Kind => EnemyKind.HeavenlyDemon;
        protected override string Key => "heavenlydemon";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 2f;
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Pull => _pullTime,
            Pattern.Rain => _rainTime + 0.05f,
            _ => 0.1f,
        };

        public override string AttackName => _pattern switch
        {
            Pattern.Slash => Phase >= 2 ? "천마군림" : "천마일검", Pattern.Cross => "십자 검기", Pattern.Miasma => "마기 장판",
            Pattern.Grasp => "마기 손아귀", Pattern.Pull => "흡인", Pattern.Rain => "검비", _ => null,
        };

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public bool GraspedPlayer => _grasped;
        public float SafeRadius => _safeRadius; // 3페이즈: 마기가 차오르지 않은 안전 반경 (0이면 아직)
        public bool IsPulling => _pattern == Pattern.Pull && Attack.Current == EnemyAttackTimeline.Phase.Active;
        public Vector3 ZoneCenter => _zoneCenter; // 해체대법 안전 반경의 가운데

        // 그림 신호 (아트 InkHeavenlyDemonFx가 받아 그려요. 판정·수치와는 상관없어요)
        public enum FxKind { Slash, Cross, Grasp, Grasped, Mark, MarkFall, PullBurst, Transition }
        public struct FxCue
        {
            public FxKind Kind;
            public Vector3 At, Dir;
            public float Radius, Angle, Time;
            public bool Sector;
            public Transform Target;
        }
        public static event System.Action<HeavenlyDemonBrain, FxCue> Cue;
        void Fx(FxCue c) => Cue?.Invoke(this, c);

        public void SetZone(Vector3 center, Vector2 size)
        {
            _zoneCenter = new Vector3(center.x, 0f, center.z);
            _zoneSize = size;
        }

        protected override void OnInitialized() => _zoneCenter = new Vector3(transform.position.x, 0f, transform.position.z);

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 3f);
            _decisionPause = Tuning("decision_pause", 0.5f);
            _transitionTime = Tuning("transition_time", 2f);
            _p2Ratio = Tuning("phase2_hp_ratio", 0.66f);
            _p3Ratio = Tuning("phase3_hp_ratio", 0.33f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _slashWindup = Tuning("slash_windup", 0.6f);
            _slashRadius = Tuning("slash_radius", 3f);
            _slashAngle = Tuning("slash_angle", 140f);
            _slashAngle2 = Tuning("p2_slash_angle", 220f);
            _slashRatio = Tuning("slash_ratio", 1f);
            _crossWindup = Tuning("cross_windup", 0.8f);
            _crossRange = Tuning("cross_range", 10f);
            _crossSpeed = Tuning("cross_speed", 10f);
            _crossRatio = Tuning("cross_ratio", 0.7f);
            _miasmaWindup = Tuning("miasma_windup", 0.8f);
            _miasmaInterval = Tuning("miasma_interval", 10f);
            _miasmaRadius = Tuning("miasma_radius", 1.5f);
            _miasmaLife = Tuning("miasma_life", 5f);
            _miasmaRatio = Tuning("miasma_ratio", 0.3f);
            _miasmaSlow = Tuning("miasma_slow", 0.2f);
            _graspFar = Tuning("grasp_far", 6f);
            _graspDelay = Tuning("grasp_delay", 2f);
            _graspWindup = Tuning("grasp_windup", 0.7f);
            _graspRange = Tuning("grasp_range", 10f);
            _graspWidth = Tuning("grasp_width", 1.2f);
            _graspRatio = Tuning("grasp_ratio", 0.6f);
            _graspStop = Tuning("grasp_stop", 2f);
            _phantomSide = Tuning("p2_phantom_side", 3f);
            _phantomDelay = Tuning("p2_phantom_delay", 0.5f);
            _phantomRatio = Tuning("p2_phantom_ratio", 0.6f);
            _pullWindup = Tuning("p2_pull_windup", 1f);
            _pullTime = Tuning("p2_pull_time", 2f);
            _pullInterval = Tuning("p2_pull_interval", 12f);
            _pullRadius = Tuning("p2_pull_radius", 6f);
            _pullSpeed = Tuning("p2_pull_speed", 2.5f);
            _pullBurst = Tuning("p2_pull_burst", 2f);
            _pullRatio = Tuning("p2_pull_ratio", 1.2f);
            _rainWindup = Tuning("p3_rain_windup", 1f);
            _rainInterval = Tuning("p3_rain_interval", 8f);
            _rainFall = Tuning("p3_rain_fall", 0.6f);
            _rainRadius = Tuning("p3_rain_radius", 1.2f);
            _rainRatio = Tuning("p3_rain_ratio", 0.6f);
            _rainTime = Tuning("p3_rain_time", 1.5f);
            _rainCount = Mathf.Max(1, Mathf.RoundToInt(Tuning("p3_rain_count", 12f)));
            _safeStart = Tuning("p3_safe_start", 11f);
            _safeMin = Tuning("p3_safe_min", 5f);
            _safeStep = Tuning("p3_safe_step", 1f);
            _safeShrink = Tuning("p3_safe_interval", 3f);
            _safeRatio = Tuning("p3_safe_ratio", 0.2f);
            _safeTick = Tuning("p3_safe_tick", 0.5f);
            _miasmaTimer = _miasmaInterval * 0.5f;
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_miasmaTimer > 0f) _miasmaTimer -= dt;
            if (_pullTimer > 0f) _pullTimer -= dt;
            if (_rainTimer > 0f) _rainTimer -= dt;
            _farTimer = ToPlayer().magnitude > _graspFar ? _farTimer + dt : 0f; // 멀리서만 싸우면 마기 손아귀
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase(Phase + 1);
            }
            TickMarks(dt);
            TickSafeZone(dt);
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_grasped) next = Pattern.Slash; // 끌려온 플레이어에게 바로
            else if (_farTimer >= _graspDelay) next = Pattern.Grasp;
            else if (Phase >= 3 && _rainTimer <= 0f) next = Pattern.Rain;
            else if (Phase >= 2 && _pullTimer <= 0f && distance <= _pullRadius) next = Pattern.Pull;
            else if (_miasmaTimer <= 0f) next = Pattern.Miasma;
            else if (distance <= _closeDistance) next = Pattern.Slash;
            else next = Pattern.Cross;
            if (next == _lastPattern && _repeat >= _maxRepeat && !_grasped && (next == Pattern.Slash || next == Pattern.Cross))
                next = next == Pattern.Slash ? Pattern.Cross : Pattern.Slash;
            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            RecoveryTime = Tuning("recovery", 0.8f);
            switch (next)
            {
                case Pattern.Slash:
                    WindupTime = _grasped ? _slashWindup * 0.5f : _slashWindup;
                    _grasped = false;
                    break;
                case Pattern.Cross: WindupTime = _crossWindup; break;
                case Pattern.Miasma:
                    WindupTime = _miasmaWindup;
                    _miasmaTimer = _miasmaInterval;
                    break;
                case Pattern.Grasp:
                    WindupTime = _graspWindup;
                    RecoveryTime = 0.3f;
                    _farTimer = 0f;
                    break;
                case Pattern.Pull:
                    WindupTime = _pullWindup;
                    _pullTimer = _pullInterval;
                    break;
                case Pattern.Rain:
                    WindupTime = _rainWindup;
                    _rainTimer = _rainInterval;
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

        float SlashAngle => Phase >= 2 ? _slashAngle2 : _slashAngle;

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Slash => TelegraphShapes.Lunge(_slashRadius, SlashAngle, 0f),
            Pattern.Grasp => TelegraphShapes.Rect(_graspWidth, _graspRange),
            Pattern.Pull => TelegraphShapes.Circle(_pullRadius),
            _ => TelegraphShapes.Circle(BodyRadius + 0.4f), // 몸 둘레에 마기가 모여요
        };

        protected override bool RadialTelegraph => _pattern == Pattern.Pull;

        // ── 판정

        protected override void OnActiveStarted()
        {
            _activeTimer = 0f;
            _rainDone = 0;
            switch (_pattern)
            {
                case Pattern.Slash:
                    if (HitShapes.InSector(transform.position, AimDirection, _slashRadius, SlashAngle, Player.transform.position, PlayerRadius))
                        Player.TryReceiveHit(AttackPower * _slashRatio, transform.position, HitKnockback, Body);
                    Fx(new FxCue { Kind = FxKind.Slash, At = transform.position, Dir = AimDirection, Radius = _slashRadius, Angle = SlashAngle });
                    if (Phase >= 2) Phantoms();
                    break;
                case Pattern.Cross:
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, 90f * i, 0f) * AimDirection;
                        var wave = Director.SpawnArrow(transform.position + Vector3.up, dir, _crossSpeed, _crossRange, 0.45f, AttackPower * _crossRatio, Body);
                        if (wave != null) wave.gameObject.name = "천마 검기";
                        if (wave != null) Fx(new FxCue { Kind = FxKind.Cross, At = wave.transform.position, Dir = dir, Target = wave.transform });
                    }
                    break;
                case Pattern.Miasma:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 at = Player.transform.position + Quaternion.Euler(0f, 120f * i + Random.Range(0f, 60f), 0f) * Vector3.forward * (i == 0 ? 0f : 2f);
                        GhostFire.Create(at, _miasmaRadius, _miasmaLife, AttackPower * _miasmaRatio, _miasmaSlow, 0.6f, Player, Body).name = "마기 장판";
                    }
                    break;
                case Pattern.Grasp:
                    Fx(new FxCue { Kind = FxKind.Grasp, At = transform.position, Dir = AimDirection, Radius = _graspRange });
                    Grasp();
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            _activeTimer += dt;
            if (_pattern == Pattern.Pull) TickPull(dt);
            else if (_pattern == Pattern.Rain)
                while (_rainDone < _rainCount && _activeTimer >= _rainDone * (_rainTime / _rainCount)) RainSword();
        }

        // 마기 손아귀: 앞 직선 위에 있으면 붙잡혀 천마 앞으로 끌려와요
        void Grasp()
        {
            Vector3 rel = Player.transform.position - transform.position;
            rel.y = 0f;
            float along = Vector3.Dot(rel, AimDirection);
            float side = Mathf.Abs(Vector3.Dot(rel, new Vector3(AimDirection.z, 0f, -AimDirection.x)));
            if (along < 0f || along > _graspRange + PlayerRadius || side > _graspWidth * 0.5f + PlayerRadius) return;
            if (!Player.TryReceiveHit(AttackPower * _graspRatio, transform.position, 0f, Body)) return;
            Player.PullToward(transform.position, _graspStop);
            _grasped = true;
            Fx(new FxCue { Kind = FxKind.Grasped, At = transform.position, Dir = AimDirection, Target = Player.transform });
        }

        // 2페이즈: 플레이어 양옆 환영이 같이 벱니다
        void Phantoms()
        {
            Vector3 p = Player.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, AimDirection).normalized * _phantomSide;
            foreach (var at in new[] { p + side, p - side })
            {
                Vector3 dir = p - at;
                dir.y = 0f;
                AddMark(at, dir.normalized, _phantomDelay, _slashRadius, _phantomRatio, true, _slashAngle);
            }
        }

        // 흡인: 끌어당기다가 끝에 폭발
        void TickPull(float dt)
        {
            Vector3 to = transform.position - Player.transform.position;
            to.y = 0f;
            if (to.magnitude <= _pullRadius && to.magnitude > BodyRadius + PlayerRadius + 0.2f)
                Player.Drag(to.normalized * Mathf.Min(_pullSpeed * dt, to.magnitude));
            if (_activeTimer < _pullTime - 0.05f) return;
            Director.SpawnBurst(transform.position, _pullBurst);
            Fx(new FxCue { Kind = FxKind.PullBurst, At = transform.position, Radius = _pullBurst });
            if (ToPlayer().magnitude <= _pullBurst + PlayerRadius) Player.TryReceiveHit(AttackPower * _pullRatio, transform.position, -1f, Body);
            _activeTimer = float.NegativeInfinity; // 한 번만
        }

        void RainSword()
        {
            _rainDone++;
            Vector2 r = Random.insideUnitCircle * 3f;
            AddMark(Player.transform.position + new Vector3(r.x, 0f, r.y), Vector3.forward, _rainFall, _rainRadius, _rainRatio, false, 0f);
        }

        // ── 바닥 예고 (환영 베기 · 검비)

        void AddMark(Vector3 at, Vector3 dir, float fall, float radius, float ratio, bool sector, float angle)
        {
            at.y = 0f;
            var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
            marker.Show(sector ? TelegraphShapes.Lunge(radius, angle, 0f) : TelegraphShapes.Circle(radius), !sector);
            marker.SetPose(at, dir);
            marker.SetProgress(0f);
            _marks.Add(new Mark { At = at, Dir = dir, Timer = fall, Total = fall, Radius = radius, Ratio = ratio, Sector = sector, Angle = angle, Marker = marker });
            Fx(new FxCue { Kind = FxKind.Mark, At = at, Dir = dir, Radius = radius, Angle = angle, Time = fall, Sector = sector });
        }

        void TickMarks(float dt)
        {
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                m.Timer -= dt;
                if (m.Marker != null) m.Marker.SetProgress(1f - Mathf.Clamp01(m.Timer / Mathf.Max(0.01f, m.Total)));
                if (m.Timer > 0f)
                {
                    _marks[i] = m;
                    continue;
                }
                if (m.Marker != null) Destroy(m.Marker.gameObject);
                _marks.RemoveAt(i);
                Director.SpawnBurst(m.Sector ? m.At + m.Dir * 1.2f : m.At, m.Sector ? 0.8f : m.Radius);
                Fx(new FxCue { Kind = FxKind.MarkFall, At = m.At, Dir = m.Dir, Radius = m.Radius, Angle = m.Angle, Sector = m.Sector });
                bool hit = m.Sector
                    ? HitShapes.InSector(m.At, m.Dir, m.Radius, m.Angle, Player.transform.position, PlayerRadius)
                    : new Vector2(Player.transform.position.x - m.At.x, Player.transform.position.z - m.At.z).magnitude <= m.Radius + PlayerRadius;
                if (hit) Player.TryReceiveHit(AttackPower * m.Ratio, m.At, -1f, Body);
            }
        }

        // ── 3페이즈: 방 가장자리부터 마기가 차올라 안전 반경이 줄어요

        void TickSafeZone(float dt)
        {
            if (_safeRadius <= 0f || IsDead) return;
            _safeTimer -= dt;
            if (_safeTimer <= 0f && _safeRadius > _safeMin)
            {
                _safeTimer = _safeShrink;
                _safeRadius = Mathf.Max(_safeMin, _safeRadius - _safeStep);
                if (_safeMarker != null) _safeMarker.SetShape(TelegraphShapes.Circle(_safeRadius));
            }
            _safeHurt -= dt;
            Vector3 d = Player.transform.position - _zoneCenter;
            d.y = 0f;
            if (_safeHurt > 0f || d.magnitude <= _safeRadius) return;
            _safeHurt = _safeTick;
            Player.TryReceiveHit(AttackPower * _safeRatio, _zoneCenter, 0f, Body);
        }

        public bool PlayerInMiasma
        {
            get
            {
                if (_safeRadius <= 0f) return false;
                Vector3 d = Player.transform.position - _zoneCenter;
                d.y = 0f;
                return d.magnitude > _safeRadius;
            }
        }

        protected override void OnAttackFinished() => _decisionTimer = _grasped ? 0f : _decisionPause; // 끌려왔으면 쉬지 않고 벱니다

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            _grasped = false;
            _pattern = Pattern.None;
            _decisionTimer = _decisionPause;
        }

        // ── 페이즈

        protected override void OnHitTaken()
        {
            if (IsTransitioning) return;
            if (Phase == 1 && Body.Health <= Body.MaxHealth * _p2Ratio) StartTransition();
            else if (Phase == 2 && Body.Health <= Body.MaxHealth * _p3Ratio) StartTransition();
        }

        void StartTransition()
        {
            if (Attack.IsAttacking) CancelAttack();
            _pattern = Pattern.None;
            _grasped = false;
            _transitionTimer = _transitionTime;
            Body.Targetable = false;
            Director.SpawnBurst(transform.position, 3f);
            Fx(new FxCue { Kind = FxKind.Transition, At = transform.position, Radius = 3f, Time = _transitionTime });
        }

        void EnterPhase(int phase)
        {
            Phase = phase;
            Body.Targetable = true;
            _decisionTimer = _decisionPause;
            _repeat = 0;
            if (phase == 2) _pullTimer = _pullInterval * 0.4f;
            if (phase == 3)
            {
                _rainTimer = _rainInterval * 0.4f;
                _safeRadius = _safeStart;
                _safeTimer = _safeShrink;
                _safeMarker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
                _safeMarker.gameObject.name = "해체대법 안전 반경";
                _safeMarker.Show(TelegraphShapes.Circle(_safeRadius));
                _safeMarker.SetPose(_zoneCenter, Vector3.forward);
                _safeMarker.SetProgress(0f);
            }
        }

        protected override void OnDied()
        {
            foreach (var m in _marks) if (m.Marker != null) Destroy(m.Marker.gameObject);
            _marks.Clear();
            if (_safeMarker != null) Destroy(_safeMarker.gameObject);
            _safeRadius = 0f;
        }
    }
}
