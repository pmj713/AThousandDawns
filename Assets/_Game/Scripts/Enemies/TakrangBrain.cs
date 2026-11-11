using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 탁랑객 (장강 수채 고수, 몬스터 시트 65행): 체력 1100 · 공격 18 (장강 배율 전 → 3300 · 29). 탁한 물결을 부리는 검객. 결투장은 강 위 잔교.
    // 고수 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 체력 50%에서 1.5초 전환 (무적), 같은 패턴은 연속 2번까지.
    //   베기: 가까우면 (2.5칸) 예고 0.5 · 앞 부채꼴 반경 2.5칸 120° ×1.0
    //   물결 베기: 멀면 예고 0.7 · 앞으로 검기 세 갈래 (20° 간격, 8칸, 초속 9칸) ×0.8. 2페이즈는 다섯 갈래
    //   탁류 보법: 5칸 밖에 1.5초 넘게 있으면 예고 0.4 · 물 위를 미끄러지듯 플레이어 옆 1.8칸으로 돌아 들어와 (0.25초) 3칸 찌르기 ×1.1 (원거리 대응)
    //   소용돌이: 10초마다 (2페이즈 7초) 예고 1.0 · 1.5초 동안 둘레 4칸 안 플레이어를 초당 2.5칸 끌어당기고 (대시면 안 끌림), 끝에 반경 1.8칸 ×1.2
    // 2페이즈: 예고 20% 빨라져요
    public sealed class TakrangBrain : EnemyBrain, IMasterBrain
    {
        enum Pattern { None, Slash, Waves, Flow, Whirl }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat;
        float _closeDistance, _farDistance, _decisionPause, _transitionTime, _phase2Ratio, _phase2Speed;
        float _slashWindup, _slashRadius, _slashAngle, _slashRatio;
        float _waveWindup, _waveRange, _waveSpeed, _waveRatio, _waveSpread, _waveRadius;
        int _waveCount, _waveCount2;
        float _flowDelay, _flowWindup, _flowTime, _flowSide, _flowReach, _flowRatio, _farTimer;
        float _whirlWindup, _whirlTime, _whirlRadius, _whirlPull, _whirlBurst, _whirlRatio, _whirlInterval, _whirlInterval2, _whirlTimer;
        float _decisionTimer, _transitionTimer, _activeTimer;
        Vector3 _flowTarget;
        bool _flowHitDone;

        public override EnemyKind Kind => EnemyKind.Takrang;
        protected override string Key => "takrang";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Master;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1f;
        protected override bool RadialTelegraph => _pattern == Pattern.Whirl;
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Flow => _flowTime + 0.1f,
            Pattern.Whirl => _whirlTime,
            _ => 0.1f,
        };

        public override string AttackName => _pattern switch
        {
            Pattern.Slash => "베기", Pattern.Waves => "물결 베기", Pattern.Flow => "탁류 보법", Pattern.Whirl => "소용돌이", _ => null,
        };

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public bool IsWhirling => _pattern == Pattern.Whirl && Attack.Current == EnemyAttackTimeline.Phase.Active;

        public void SetZone(Vector3 center, Vector2 size) { }

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 2.5f);
            _farDistance = Tuning("far_distance", 5f);
            _decisionPause = Tuning("decision_pause", 0.4f);
            _transitionTime = Tuning("transition_time", 1.5f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _phase2Speed = Tuning("p2_windup_ratio", 0.8f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _slashWindup = Tuning("slash_windup", 0.5f);
            _slashRadius = Tuning("slash_radius", 2.5f);
            _slashAngle = Tuning("slash_angle", 120f);
            _slashRatio = Tuning("slash_ratio", 1f);
            _waveWindup = Tuning("wave_windup", 0.7f);
            _waveRange = Tuning("wave_range", 8f);
            _waveSpeed = Tuning("wave_speed", 9f);
            _waveRatio = Tuning("wave_ratio", 0.8f);
            _waveSpread = Tuning("wave_spread", 20f);
            _waveRadius = Tuning("wave_radius", 0.4f);
            _waveCount = Mathf.Max(1, Mathf.RoundToInt(Tuning("wave_count", 3f)));
            _waveCount2 = Mathf.Max(1, Mathf.RoundToInt(Tuning("p2_wave_count", 5f)));
            _flowDelay = Tuning("flow_delay", 1.5f);
            _flowWindup = Tuning("flow_windup", 0.4f);
            _flowTime = Mathf.Max(0.05f, Tuning("flow_time", 0.25f));
            _flowSide = Tuning("flow_side", 1.8f);
            _flowReach = Tuning("flow_reach", 3f);
            _flowRatio = Tuning("flow_ratio", 1.1f);
            _whirlWindup = Tuning("whirl_windup", 1f);
            _whirlTime = Tuning("whirl_time", 1.5f);
            _whirlRadius = Tuning("whirl_radius", 4f);
            _whirlPull = Tuning("whirl_pull", 2.5f);
            _whirlBurst = Tuning("whirl_burst", 1.8f);
            _whirlRatio = Tuning("whirl_ratio", 1.2f);
            _whirlInterval = Tuning("whirl_interval", 10f);
            _whirlInterval2 = Tuning("p2_whirl_interval", 7f);
            _whirlTimer = _whirlInterval * 0.5f;
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_whirlTimer > 0f) _whirlTimer -= dt;
            _farTimer = ToPlayer().magnitude > _farDistance ? _farTimer + dt : 0f; // 멀리서만 싸우면 탁류 보법
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_farTimer >= _flowDelay) next = Pattern.Flow;
            else if (_whirlTimer <= 0f && distance <= _whirlRadius) next = Pattern.Whirl;
            else if (distance <= _closeDistance) next = Pattern.Slash;
            else next = Pattern.Waves;
            if (next == _lastPattern && _repeat >= _maxRepeat && next != Pattern.Flow)
            {
                if (next == Pattern.Waves) return false; // 다가가요
                next = next == Pattern.Slash ? Pattern.Waves : Pattern.Slash;
            }
            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            float speed = Phase == 2 ? _phase2Speed : 1f;
            RecoveryTime = Tuning("recovery", 0.6f);
            switch (next)
            {
                case Pattern.Slash: WindupTime = _slashWindup * speed; break;
                case Pattern.Waves: WindupTime = _waveWindup * speed; break;
                case Pattern.Flow:
                    WindupTime = _flowWindup * speed;
                    _farTimer = 0f;
                    break;
                case Pattern.Whirl:
                    WindupTime = _whirlWindup * speed;
                    _whirlTimer = Phase == 1 ? _whirlInterval : _whirlInterval2;
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

        // ── 예고

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Slash => TelegraphShapes.Lunge(_slashRadius, _slashAngle, 0f),
            Pattern.Waves => TelegraphShapes.Lunge(2f, _waveSpread * (WaveCount - 1) + 20f, 0f),
            Pattern.Flow => TelegraphShapes.Rect(0.8f, ToPlayer().magnitude),
            Pattern.Whirl => TelegraphShapes.Circle(_whirlRadius),
            _ => TelegraphShapes.Circle(0.5f),
        };

        int WaveCount => Phase == 1 ? _waveCount : _waveCount2;

        // ── 판정

        protected override void OnActiveStarted()
        {
            _activeTimer = 0f;
            switch (_pattern)
            {
                case Pattern.Slash:
                    if (HitShapes.InSector(transform.position, AimDirection, _slashRadius, _slashAngle, Player.transform.position, PlayerRadius))
                        Player.TryReceiveHit(AttackPower * _slashRatio, transform.position, HitKnockback, Body);
                    break;
                case Pattern.Waves:
                    int n = WaveCount;
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, (i - (n - 1) * 0.5f) * _waveSpread, 0f) * AimDirection;
                        var wave = Director.SpawnArrow(transform.position + Vector3.up, dir, _waveSpeed, _waveRange, _waveRadius, AttackPower * _waveRatio, Body);
                        if (wave != null)
                        {
                            wave.gameObject.name = "물결 검기";
                            wave.transform.localScale = new Vector3(_waveRadius * 3f, 0.15f, 0.4f);
                        }
                    }
                    break;
                case Pattern.Flow: // 플레이어 옆으로 돌아 들어갈 자리
                    Vector3 to = ToPlayer();
                    to.y = 0f;
                    Vector3 side = Vector3.Cross(Vector3.up, to.sqrMagnitude > 1e-4f ? to.normalized : Facing) * (Random.value < 0.5f ? -1f : 1f);
                    _flowTarget = Player.transform.position + side * _flowSide;
                    _flowHitDone = false;
                    break;
                case Pattern.Whirl:
                    Director.SpawnBurst(transform.position, 1f);
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            _activeTimer += dt;
            if (_pattern == Pattern.Flow) TickFlow(dt);
            else if (_pattern == Pattern.Whirl) TickWhirl(dt);
        }

        void TickFlow(float dt)
        {
            if (_flowHitDone) return;
            Vector3 to = _flowTarget - transform.position;
            to.y = 0f;
            float step = (_flowSide + to.magnitude) / _flowTime * dt;
            if (to.magnitude > 0.1f && _activeTimer < _flowTime)
            {
                MoveRaw(to.normalized * Mathf.Min(step, to.magnitude));
                return;
            }
            // 돌아 들어와 찌르기
            _flowHitDone = true;
            Vector3 aim = ToPlayer();
            aim.y = 0f;
            if (aim.sqrMagnitude > 1e-4f) AimDirection = aim.normalized;
            FaceToward(AimDirection);
            Vector3 rel = Player.transform.position - transform.position;
            rel.y = 0f;
            float along = Vector3.Dot(rel, AimDirection);
            float sideDist = Mathf.Abs(Vector3.Dot(rel, new Vector3(AimDirection.z, 0f, -AimDirection.x)));
            if (along >= 0f && along <= _flowReach + PlayerRadius && sideDist <= 0.4f + PlayerRadius)
                Player.TryReceiveHit(AttackPower * _flowRatio, transform.position, HitKnockback, Body);
        }

        // 소용돌이: 끌어당기다가 끝에 터져요
        void TickWhirl(float dt)
        {
            Vector3 to = transform.position - Player.transform.position;
            to.y = 0f;
            if (to.magnitude <= _whirlRadius && to.magnitude > BodyRadius + PlayerRadius + 0.2f)
                Player.Drag(to.normalized * Mathf.Min(_whirlPull * dt, to.magnitude));
            if (_activeTimer < _whirlTime - 0.05f) return;
            if (_pattern != Pattern.Whirl) return;
            Director.SpawnBurst(transform.position, _whirlBurst);
            if (ToPlayer().magnitude <= _whirlBurst + PlayerRadius) Player.TryReceiveHit(AttackPower * _whirlRatio, transform.position, -1f, Body);
            _activeTimer = float.NegativeInfinity; // 한 번만 터져요
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
    }
}
