using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 녹림왕 (죽림 보스, 몬스터 시트 고수·보스 패턴). 경직·넉백 없음. 거리에 따라 패턴을 고르고 같은 패턴은 연속 2번까지.
    // 1페이즈: 대도 휘두르기(가까울 때) · 돌진 → 내려찍기(멀 때) · 부하 부르기(20초마다, 부하가 다 쓰러졌을 때만, 페이즈마다 2번).
    // 체력 50%: 1.5초 전환(무적) → 2페이즈: 불타는 죽림 + 회전베기 3연속, 휘두르기·돌진이 빨라지고 부하 부르기 없음.
    public sealed class NokrimBrain : EnemyBrain
    {
        public override string AttackName => _pattern switch { Pattern.Sweep => "대도 휘두르기", Pattern.Charge => "돌진", Pattern.Spin => "회전베기", _ => null };

        enum Pattern { None, Sweep, Charge, Summon, Spin }

        const float ProbeHeight = 0.8f;
        const float MaxChargeStep = 0.25f;

        float _closeDistance, _farDistance, _decisionPause, _transitionTime, _phase2Ratio;
        int _maxRepeat;
        float _sweepWindup, _sweepRecovery, _sweepRatio, _sweepRadius, _sweepAngle, _sweepLunge, _sweepLungeTime;
        float _p2SweepWindup, _p2SweepRecovery, _p2SlamRecovery;
        float _chargeWindup, _chargeRatio, _chargeDistance, _chargeSpeed, _chargeWidth;
        float _slamRecovery, _slamRatio, _slamRadius;
        float _summonWindup, _summonRecovery, _summonInterval;
        int _summonCount, _summonMaxPerPhase;
        float _spinWindup, _spinRecovery, _spinRatio, _spinRadius, _spinInterval, _spinMoveSpeed;
        int _spinCount;
        float _fireWindup, _fireBand, _fireShrinkInterval, _fireShrinkStep, _fireTick;
        Vector2 _fireMinSafe;

        Vector3 _zoneCenter;
        Vector2 _zoneSize;
        bool _hasZone;

        Pattern _pattern, _lastPattern;
        int _repeat, _summonsThisPhase, _spinsDone;
        float _decisionTimer, _transitionTimer, _summonCooldown;
        float _lungeRemaining, _traveled, _plannedLength, _spinElapsed;
        Vector3 _slamPoint; // 내려찍기 원 자리 (돌진이 끝나는 곳, 월드 좌표)
        const float FootstepLength = 2f; // 돌진 발소리 간격 (칸)
        float _nextFootstep; // 다음 발소리가 날 돌진 거리
        bool _hitPlayer;
        readonly float[] _banTimers = new float[5]; // 패턴마다 다시 나오지 않는 남은 시간 (설련 향낭)
        readonly List<EnemyBrain> _minions = new List<EnemyBrain>();
        BurningGrove _grove;

        public override EnemyKind Kind => EnemyKind.Nokrim;
        protected override string Key => "nokrim";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override bool FreezeBlocked => IsTransitioning; // 페이즈 전환 연출 중에는 빙결이 안 걸림
        protected override bool RadialTelegraph => _pattern == Pattern.Summon || _pattern == Pattern.Spin;
        protected override float DeathDelay => 1f;
        protected override float TrackStopDistance => _pattern == Pattern.Charge ? _farDistance : _sweepRadius * 0.6f;

        protected override float ActiveTime => _pattern switch
        {
            Pattern.Sweep => _sweepLungeTime,
            Pattern.Charge => float.PositiveInfinity, // 돌진이 끝나면 내려찍고 EndActive
            Pattern.Spin => (_spinCount - 1) * _spinInterval + 0.05f,
            _ => 0.1f,
        };

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public BurningGrove Grove => _grove;
        float SlamRecovery => Phase == 1 ? _slamRecovery : _p2SlamRecovery;

        // 결투장 (불타는 죽림의 범위, 깨어나는 조건). 안 정하면 나온 자리를 가운데로 해요.
        public void SetZone(Vector3 center, Vector2 size)
        {
            _zoneCenter = new Vector3(center.x, 0f, center.z);
            _zoneSize = size;
            _hasZone = true;
        }

        protected override void ReadTuning()
        {
            AttackInterval = 0f; // 패턴 사이 쉬는 시간은 decision_pause로
            _closeDistance = Tuning("close_distance", 3.6f);
            _farDistance = Tuning("far_distance", 5f);
            _decisionPause = Tuning("decision_pause", 0.4f);
            _transitionTime = Tuning("transition_time", 1.5f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));

            _sweepWindup = Tuning("sweep_windup", 0.7f);
            _sweepRecovery = Tuning("sweep_recovery", 1f);
            _sweepRatio = Tuning("sweep_ratio", 1f);
            _sweepRadius = Tuning("sweep_radius", 3f);
            _sweepAngle = Tuning("sweep_angle", 150f);
            _sweepLunge = Tuning("sweep_lunge", 1f);
            _sweepLungeTime = Mathf.Max(0.05f, Tuning("sweep_lunge_time", 0.2f));
            _p2SweepWindup = Tuning("p2_sweep_windup", 0.6f);
            _p2SweepRecovery = Tuning("p2_sweep_recovery", 0.8f);
            _p2SlamRecovery = Tuning("p2_slam_recovery", 1.2f);

            _chargeWindup = Tuning("charge_windup", 0.9f);
            _chargeRatio = Tuning("charge_ratio", 0.8f);
            _chargeDistance = Tuning("charge_distance", 8f);
            _chargeSpeed = Tuning("charge_speed", 14f);
            _chargeWidth = Tuning("charge_width", 1.4f);
            _slamRecovery = Tuning("slam_recovery", 1.5f);
            _slamRatio = Tuning("slam_ratio", 1.3f);
            _slamRadius = Tuning("slam_radius", 2f);

            _summonWindup = Tuning("summon_windup", 1.2f);
            _summonRecovery = Tuning("summon_recovery", 0.5f);
            _summonCount = Mathf.RoundToInt(Tuning("summon_count", 3f));
            _summonInterval = Tuning("summon_interval", 20f);
            _summonMaxPerPhase = Mathf.RoundToInt(Tuning("summon_max_per_phase", 2f));

            _spinWindup = Tuning("spin_windup", 0.8f);
            _spinRecovery = Tuning("spin_recovery", 1.5f);
            _spinRatio = Tuning("spin_ratio", 0.6f);
            _spinRadius = Tuning("spin_radius", 2.5f);
            _spinCount = Mathf.Max(1, Mathf.RoundToInt(Tuning("spin_count", 3f)));
            _spinInterval = Tuning("spin_interval", 0.65f);
            _spinMoveSpeed = Tuning("spin_move_speed", 2.5f);

            _fireWindup = Tuning("fire_windup", 2f);
            _fireBand = Tuning("fire_band", 2f);
            _fireShrinkInterval = Mathf.Max(1f, Tuning("fire_shrink_interval", 20f));
            _fireShrinkStep = Tuning("fire_shrink_step", 1f);
            _fireMinSafe = new Vector2(Tuning("fire_min_width", 10f), Tuning("fire_min_depth", 6f));
            _fireTick = Tuning("fire_tick", 0.5f);
        }

        protected override void OnInitialized()
        {
            if (!_hasZone)
            {
                _zoneCenter = transform.position;
                _zoneSize = new Vector2(Tuning("arena_width", 16f), Tuning("arena_depth", 12f));
            }
        }

        // 결투장 안으로 1칸 이상 들어오면 깨어나요 (입구가 막혀요).
        protected override bool ShouldWake()
        {
            if (!_hasZone) return base.ShouldWake();
            Vector3 p = Player.transform.position;
            return Mathf.Abs(p.x - _zoneCenter.x) <= _zoneSize.x * 0.5f - 1f && Mathf.Abs(p.z - _zoneCenter.z) <= _zoneSize.y * 0.5f - 1f;
        }

        protected override void OnWoke() => _summonCooldown = _summonInterval;

        // ── 패턴 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            var next = Choose(distance);
            if (next == Pattern.None) return false;

            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            switch (next)
            {
                case Pattern.Sweep:
                    WindupTime = Phase == 1 ? _sweepWindup : _p2SweepWindup;
                    RecoveryTime = Phase == 1 ? _sweepRecovery : _p2SweepRecovery;
                    break;
                case Pattern.Charge:
                    WindupTime = _chargeWindup;
                    RecoveryTime = SlamRecovery;
                    break;
                case Pattern.Summon:
                    WindupTime = _summonWindup;
                    RecoveryTime = _summonRecovery;
                    break;
                case Pattern.Spin:
                    WindupTime = _spinWindup;
                    RecoveryTime = _spinRecovery;
                    break;
            }
            return true;
        }

        Pattern Choose(float distance)
        {
            if (Phase == 1 && !Banned(Pattern.Summon) && SummonReady()) return Pattern.Summon;

            Pattern want;
            if (distance <= _closeDistance) want = Phase == 2 && Random.value < 0.5f ? Pattern.Spin : Pattern.Sweep;
            else if (distance >= _farDistance) want = Pattern.Charge;
            else return Pattern.None; // 중간 거리면 다가가요

            if (want == _lastPattern && _repeat >= _maxRepeat)
            {
                if (want == Pattern.Charge) return Pattern.None; // 다가가서 다른 패턴으로
                want = Phase == 2 ? (want == Pattern.Spin ? Pattern.Sweep : Pattern.Spin) : Pattern.Charge;
            }
            // 봉인된 패턴이면 2페이즈 근접은 다른 근접 패턴으로, 아니면 잠시 다가가기만
            if (Banned(want) && Phase == 2 && want != Pattern.Charge) want = want == Pattern.Spin ? Pattern.Sweep : Pattern.Spin;
            return Banned(want) ? Pattern.None : want;
        }

        bool SummonReady()
        {
            _minions.RemoveAll(m => m == null || m.IsDead);
            return _summonCooldown <= 0f && _summonsThisPhase < _summonMaxPerPhase && _minions.Count == 0;
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsTransitioning) return;
            Vector3 to = ToPlayer();
            FaceToward(to);
            if (to.magnitude > _closeDistance * 0.8f) Move(to.normalized * MoveSpeed, dt);
        }

        protected override void TickWindup(float dt, bool tracking)
        {
            if (_pattern == Pattern.Summon)
            {
                FaceToward(ToPlayer());
                return;
            }
            base.TickWindup(dt, tracking);
        }

        // ── 예고

        protected override List<Vector2> TelegraphShape()
        {
            switch (_pattern)
            {
                case Pattern.Sweep:
                    return TelegraphShapes.Lunge(_sweepRadius, _sweepAngle, _sweepLunge);
                case Pattern.Charge:
                    _plannedLength = PlannedCharge();
                    return TelegraphShapes.Rect(_chargeWidth, _plannedLength + _chargeWidth * 0.5f);
                case Pattern.Spin:
                    return TelegraphShapes.Circle(_spinRadius);
                default:
                    return TelegraphShapes.Circle(1.5f);
            }
        }

        protected override void UpdateTelegraph()
        {
            if (_pattern == Pattern.Charge && Attack.IsTracking)
            {
                float length = PlannedCharge();
                if (Mathf.Abs(length - _plannedLength) > 0.05f)
                {
                    _plannedLength = length;
                    Telegraph.SetShape(TelegraphShapes.Rect(_chargeWidth, _plannedLength + _chargeWidth * 0.5f));
                }
            }
            base.UpdateTelegraph();
        }

        float PlannedCharge() =>
            ObstacleProbe.Distance(transform.position + Vector3.up * ProbeHeight, AimDirection, BodyRadius * 0.8f, _chargeDistance);

        // ── 판정

        protected override void OnActiveStarted()
        {
            _hitPlayer = false;
            switch (_pattern)
            {
                case Pattern.Sweep:
                    _lungeRemaining = _sweepLunge;
                    CheckSweepHit();
                    break;
                case Pattern.Charge:
                    // 돌진하는 동안 끝자리에 내려찍기 원이 차올라요
                    _traveled = 0f;
                    _nextFootstep = 0f;
                    Telegraph.Show(TelegraphShapes.Circle(_slamRadius), true);
                    _slamPoint = transform.position + AimDirection * _plannedLength;
                    Telegraph.SetPose(_slamPoint, Vector3.forward);
                    break;
                case Pattern.Summon:
                    Sfx.Play(SfxId.BossSummon);
                    for (int i = 0; i < _summonCount; i++)
                        _minions.Add(Director.Spawn(EnemyKind.Grunt, Director.FindFreeSpot(transform.position, 2f)));
                    _summonsThisPhase++;
                    _summonCooldown = _summonInterval;
                    break;
                case Pattern.Spin:
                    _spinElapsed = 0f;
                    _spinsDone = 0;
                    Spin();
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            switch (_pattern)
            {
                case Pattern.Sweep:
                {
                    float step = Mathf.Min(_lungeRemaining, _sweepLunge / _sweepLungeTime * dt);
                    Vector3 to = ToPlayer();
                    if (Vector3.Dot(to, AimDirection) > 0f && to.magnitude < BodyRadius + PlayerRadius + 0.2f) step = 0f;
                    if (step > 0f)
                    {
                        MoveRaw(AimDirection * step);
                        _lungeRemaining -= step;
                    }
                    CheckSweepHit();
                    break;
                }
                case Pattern.Charge:
                    TickCharge(dt);
                    break;
                case Pattern.Spin:
                {
                    // 돌면서 초당 2.5칸으로 다가와요
                    Vector3 to = ToPlayer();
                    if (to.magnitude > BodyRadius + PlayerRadius + 0.2f) Move(to.normalized * _spinMoveSpeed, dt);
                    _spinElapsed += dt;
                    while (_spinsDone < _spinCount && _spinElapsed >= _spinsDone * _spinInterval) Spin();
                    break;
                }
            }
        }

        void TickCharge(float dt)
        {
            float total = _chargeSpeed * dt;
            while (total > 0f)
            {
                float step = Mathf.Min(total, MaxChargeStep, _plannedLength - _traveled);
                if (step <= 0f) break;
                var flags = MoveRaw(AimDirection * step);
                _traveled += step;
                total -= step;
                if (!_hitPlayer && ToPlayer().magnitude <= _chargeWidth * 0.5f + PlayerRadius) _hitPlayer = HitPlayer(_chargeRatio);
                if ((flags & CollisionFlags.Sides) != 0) break;
            }
            // 발소리: 2칸마다 한 번, 네 가지 중 무작위
            while (_traveled >= _nextFootstep)
            {
                Sfx.PlayEach(SfxId.BossCharge, -1);
                _nextFootstep += FootstepLength;
            }
            // 예고는 녹림왕 자식이라 같이 움직이니까, 매 프레임 끝자리에 다시 놓아요
            Telegraph.SetPose(_slamPoint, Vector3.forward);
            Telegraph.SetProgress(_plannedLength > 0.1f ? _traveled / _plannedLength : 1f);
            if (_traveled >= _plannedLength - 1e-3f || total > 0f) Slam();
        }

        // 돌진 끝에서 내려찍기: 원 반경 2칸, 대도가 땅에 박혀 빈틈
        void Slam()
        {
            Telegraph.Hide();
            Director.SpawnBurst(transform.position, _slamRadius);
            if (ToPlayer().magnitude <= _slamRadius + PlayerRadius) HitPlayer(_slamRatio);
            Attack.EndActive(SlamRecovery);
        }

        void Spin()
        {
            _spinsDone++;
            Director.SpawnBurst(transform.position, _spinRadius);
            if (ToPlayer().magnitude <= _spinRadius + PlayerRadius) HitPlayer(_spinRatio);
        }

        void CheckSweepHit()
        {
            if (_hitPlayer) return;
            if (!HitShapes.InSector(transform.position, AimDirection, _sweepRadius, _sweepAngle, Player.transform.position, PlayerRadius)) return;
            _hitPlayer = HitPlayer(_sweepRatio);
        }

        // 피해 = 배율 × 최종 공격
        bool HitPlayer(float ratio) => Player.TryReceiveHit(AttackPower * ratio, transform.position, HitKnockback, Body);

        protected override void OnAttackFinished() => _decisionTimer = _decisionPause;

        // 빙결 1초: 진행 중인 패턴이 취소돼요 (공격 취소는 기본 동작).
        // 설련 향낭: 빙결로 취소된 패턴은 잠시 다시 나오지 않아요.
        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            float ban = ElementalMods.Current.PatternBan;
            if (cancelledAttack && ban > 0f && _pattern != Pattern.None) _banTimers[(int)_pattern] = ban;
            _pattern = Pattern.None;
            _decisionTimer = _decisionPause;
        }

        bool Banned(Pattern pattern) => _banTimers[(int)pattern] > 0f;

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
            Sfx.Play(SfxId.BossPhase);
            Body.Targetable = false; // 전환 연출 동안 무적
        }

        void LateUpdate()
        {
            if (IsDead || !IsAwake) return;
            float dt = Time.deltaTime;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_summonCooldown > 0f) _summonCooldown -= dt;
            for (int i = 0; i < _banTimers.Length; i++)
                if (_banTimers[i] > 0f) _banTimers[i] -= dt;
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
        }

        void EnterPhase2()
        {
            Phase = 2;
            Body.Targetable = true;
            _decisionTimer = _decisionPause;
            _repeat = 0;
            var visuals = Director.Visuals;
            float burnPerStack = GameConfig.Get("player_burn.damage", 2f); // 플레이어 화상 1중첩 초당 피해 (상태이상 탭, 죽림 지역 배율 1)
            _grove = BurningGrove.Create(_zoneCenter, _zoneSize, _fireBand, _fireWindup, _fireShrinkInterval, _fireShrinkStep,
                _fireMinSafe, _fireTick, burnPerStack, Player, visuals.telegraphFill, visuals.fire);
        }

        protected override void OnDied()
        {
            Sfx.Play(SfxId.BossDeath);
            if (_grove != null) Destroy(_grove.gameObject);
        }
    }
}
