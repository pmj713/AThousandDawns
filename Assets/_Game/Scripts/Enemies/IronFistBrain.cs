using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고수 (청죽객·철권 마웅): 체력바·페이즈 표시와 판 구조에서 같이 다뤄요.
    public interface IMasterBrain
    {
        int Phase { get; }
        bool IsTransitioning { get; }
        void SetZone(Vector3 center, Vector2 size);
    }

    // 철권 마웅 (죽림 고수, 몬스터 탭 61·107~111·116행): 체력 1200, 공격 20, 강인함 4, 이동 3.5. 결투장은 사방 돌벽.
    // 고수 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 체력 50%에서 1.5초 전환 (무적), 같은 패턴은 연속 2번까지.
    // 주먹 2연타: 가까울 때. 예고 0.5, 앞 부채꼴 1.5칸 70° × 2번 (0.35초 간격), 한 번에 0.5칸 전진, ×0.7, 빈틈 0.7
    // 붕산권: 7초마다. 예고 0.8, 원 반경 3칸 충격파 + 넉백 2칸, ×1.2, 빈틈 1.0. 2페이즈는 2연속
    // 돌진: 멀 때. 예고 0.8, 벽까지 직선 초당 13칸 + 넉백, ×1.0. 벽에 부딪히면 2.5초 기절 (빈틈)
    //   2페이즈: 벽에 부딪히면 낙석 3개 (예고 원 반경 1칸, ×0.8), 기절은 1.5초
    public sealed class IronFistBrain : EnemyBrain, IMasterBrain
    {
        public override string AttackName => _pattern switch { Pattern.Jab => "주먹 2연타", Pattern.Quake => "붕산권", Pattern.Charge => "돌진", _ => null };

        enum Pattern { None, Jab, Quake, Charge }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat, _jabsDone, _quakesDone, _quakeCount;
        float _closeDistance, _farDistance, _decisionPause, _transitionTime, _phase2Ratio;
        float _jabWindup, _jabRecovery, _jabRatio, _jabRadius, _jabAngle, _jabGap, _jabLunge;
        float _quakeWindup, _quakeRecovery, _quakeRatio, _quakeRadius, _quakeKnockback, _quakeInterval, _quakeGap;
        float _chargeWindup, _chargeRatio, _chargeSpeed, _chargeWidth, _chargeKnockback, _chargeMax, _stun, _p2Stun;
        float _rockRatio, _rockRadius, _rockFall;
        float _decisionTimer, _transitionTimer, _quakeTimer, _activeTimer, _lungeLeft, _traveled, _plannedLength;
        bool _hitPlayer;
        Vector3 _zoneCenter;
        Vector2 _zoneSize;
        bool _hasZone;
        readonly List<(Vector3 at, float timer, Telegraph marker)> _rocks = new List<(Vector3, float, Telegraph)>();

        const float MaxChargeStep = 0.25f;

        public override EnemyKind Kind => EnemyKind.IronFist;
        protected override string Key => "ironfist";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Master;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1f;
        protected override bool RadialTelegraph => _pattern == Pattern.Quake;
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Jab => _jabGap + 0.1f,
            Pattern.Quake => (_quakeCount - 1) * _quakeGap + 0.05f,
            Pattern.Charge => float.PositiveInfinity, // 벽에 닿으면 EndActive
            _ => 0.1f,
        };

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public bool IsWallStunned { get; private set; }

        public void SetZone(Vector3 center, Vector2 size)
        {
            _zoneCenter = new Vector3(center.x, 0f, center.z);
            _zoneSize = size;
            _hasZone = true;
        }

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 2.5f);
            _farDistance = Tuning("far_distance", 5f);
            _decisionPause = Tuning("decision_pause", 0.4f);
            _transitionTime = Tuning("transition_time", 1.5f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _jabWindup = Tuning("jab_windup", 0.5f);
            _jabRecovery = Tuning("jab_recovery", 0.7f);
            _jabRatio = Tuning("jab_ratio", 0.7f);
            _jabRadius = Tuning("jab_radius", 1.5f);
            _jabAngle = Tuning("jab_angle", 70f);
            _jabGap = Tuning("jab_gap", 0.35f);
            _jabLunge = Tuning("jab_lunge", 0.5f);
            _quakeWindup = Tuning("quake_windup", 0.8f);
            _quakeRecovery = Tuning("quake_recovery", 1f);
            _quakeRatio = Tuning("quake_ratio", 1.2f);
            _quakeRadius = Tuning("quake_radius", 3f);
            _quakeKnockback = Tuning("quake_knockback", 2f);
            _quakeInterval = Tuning("quake_interval", 7f);
            _quakeGap = Tuning("p2_quake_gap", 0.6f);
            _chargeWindup = Tuning("charge_windup", 0.8f);
            _chargeRatio = Tuning("charge_ratio", 1f);
            _chargeSpeed = Tuning("charge_speed", 13f);
            _chargeWidth = Tuning("charge_width", 1.2f);
            _chargeKnockback = Tuning("charge_knockback", 1.5f);
            _chargeMax = Tuning("charge_max", 16f);
            _stun = Tuning("wall_stun", 2.5f);
            _p2Stun = Tuning("p2_wall_stun", 1.5f);
            _rockRatio = Tuning("rock_ratio", 0.8f);
            _rockRadius = Tuning("rock_radius", 1f);
            _rockFall = Tuning("rock_fall", 0.8f);
            _quakeTimer = _quakeInterval * 0.5f;
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_quakeTimer > 0f) _quakeTimer -= dt;
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
            TickRocks(dt);
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_quakeTimer <= 0f && distance <= _quakeRadius + 1.5f) next = Pattern.Quake;
            else if (distance <= _closeDistance) next = Pattern.Jab;
            else if (distance >= _farDistance) next = Pattern.Charge;
            else return false; // 중간 거리면 다가가요
            if (next == _lastPattern && _repeat >= _maxRepeat)
            {
                if (next == Pattern.Charge) return false;
                next = next == Pattern.Jab ? Pattern.Quake : Pattern.Jab;
            }

            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            switch (next)
            {
                case Pattern.Jab: WindupTime = _jabWindup; RecoveryTime = _jabRecovery; break;
                case Pattern.Quake:
                    WindupTime = _quakeWindup; RecoveryTime = _quakeRecovery;
                    _quakeCount = Phase == 1 ? 1 : 2; // 2페이즈: 붕산권 2연속
                    _quakeTimer = _quakeInterval;
                    break;
                case Pattern.Charge: WindupTime = _chargeWindup; RecoveryTime = Phase == 1 ? _stun : _p2Stun; break;
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

        protected override List<Vector2> TelegraphShape()
        {
            switch (_pattern)
            {
                case Pattern.Jab: return TelegraphShapes.Lunge(_jabRadius, _jabAngle, _jabLunge * 2f);
                case Pattern.Quake: return TelegraphShapes.Circle(_quakeRadius);
                case Pattern.Charge:
                    _plannedLength = PlannedCharge();
                    return TelegraphShapes.Rect(_chargeWidth, _plannedLength);
                default: return TelegraphShapes.Circle(0.5f);
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
                    Telegraph.SetShape(TelegraphShapes.Rect(_chargeWidth, _plannedLength));
                }
            }
            base.UpdateTelegraph();
        }

        float PlannedCharge() =>
            ObstacleProbe.Distance(transform.position + Vector3.up * 0.8f, AimDirection, BodyRadius * 0.8f, _chargeMax);

        // ── 판정

        protected override void OnActiveStarted()
        {
            _activeTimer = 0f;
            _hitPlayer = false;
            IsWallStunned = false;
            switch (_pattern)
            {
                case Pattern.Jab:
                    _jabsDone = 0;
                    Jab();
                    break;
                case Pattern.Quake:
                    _quakesDone = 0;
                    Quake();
                    break;
                case Pattern.Charge:
                    _traveled = 0f;
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            _activeTimer += dt;
            switch (_pattern)
            {
                case Pattern.Jab:
                    if (_lungeLeft > 0f)
                    {
                        float step = Mathf.Min(_lungeLeft, _jabLunge / 0.1f * dt);
                        Vector3 to = ToPlayer();
                        if (!(Vector3.Dot(to, AimDirection) > 0f && to.magnitude < BodyRadius + PlayerRadius + 0.2f)) MoveRaw(AimDirection * step);
                        _lungeLeft -= step;
                    }
                    if (_jabsDone < 2 && _activeTimer >= _jabGap) Jab();
                    break;
                case Pattern.Quake:
                    while (_quakesDone < _quakeCount && _activeTimer >= _quakesDone * _quakeGap) Quake();
                    break;
                case Pattern.Charge:
                    TickCharge(dt);
                    break;
            }
        }

        void Jab()
        {
            _jabsDone++;
            _lungeLeft = _jabLunge;
            if (HitShapes.InSector(transform.position, AimDirection, _jabRadius, _jabAngle, Player.transform.position, PlayerRadius))
                HitPlayer(_jabRatio, HitKnockback);
        }

        void Quake()
        {
            _quakesDone++;
            Director.SpawnBurst(transform.position, _quakeRadius);
            if (ToPlayer().magnitude <= _quakeRadius + PlayerRadius) HitPlayer(_quakeRatio, _quakeKnockback);
        }

        // 벽까지 직선 (MoveRaw는 벽에 막혀요). 지나가며 플레이어를 치고 밀어내요.
        void TickCharge(float dt)
        {
            float total = _chargeSpeed * dt;
            bool hitWall = false;
            while (total > 0f)
            {
                float step = Mathf.Min(total, MaxChargeStep);
                var flags = MoveRaw(AimDirection * step);
                _traveled += step;
                total -= step;
                if (!_hitPlayer && ToPlayer().magnitude <= _chargeWidth * 0.5f + PlayerRadius) _hitPlayer = HitPlayer(_chargeRatio, _chargeKnockback);
                if ((flags & CollisionFlags.Sides) != 0) { hitWall = true; break; }
                if (_traveled >= _chargeMax) break;
            }
            if (!hitWall && _traveled < _chargeMax) return;
            Telegraph.Hide();
            if (hitWall)
            {
                IsWallStunned = true; // 빈틈 (기절)
                if (Phase == 2) DropRocks();
                Attack.EndActive(Phase == 1 ? _stun : _p2Stun);
            }
            else Attack.EndActive(_jabRecovery);
        }

        // 2페이즈 낙석: 플레이어 둘레에 돌 3개 (예고 원 뒤 떨어짐)
        void DropRocks()
        {
            Vector3 p = Player.transform.position;
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = i == 0 ? p : p + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(1.5f, 3f);
                at = Clamp(at);
                var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
                marker.Show(TelegraphShapes.Circle(_rockRadius), true);
                marker.SetPose(at, Vector3.forward);
                marker.SetProgress(0f);
                _rocks.Add((at, _rockFall, marker));
            }
        }

        void TickRocks(float dt)
        {
            for (int i = _rocks.Count - 1; i >= 0; i--)
            {
                var (at, timer, marker) = _rocks[i];
                timer -= dt;
                if (marker != null) marker.SetProgress(1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, _rockFall)));
                if (timer > 0f)
                {
                    _rocks[i] = (at, timer, marker);
                    continue;
                }
                if (marker != null) Destroy(marker.gameObject);
                _rocks.RemoveAt(i);
                Director.SpawnBurst(at, _rockRadius);
                Vector3 d = Player.transform.position - at;
                d.y = 0f;
                if (d.magnitude <= _rockRadius + PlayerRadius) Player.TryReceiveHit(AttackPower * _rockRatio, at, -1f, Body);
            }
        }

        Vector3 Clamp(Vector3 p)
        {
            if (!_hasZone) return p;
            float hx = _zoneSize.x * 0.5f - 0.8f, hz = _zoneSize.y * 0.5f - 0.8f;
            return new Vector3(Mathf.Clamp(p.x, _zoneCenter.x - hx, _zoneCenter.x + hx), 0f, Mathf.Clamp(p.z, _zoneCenter.z - hz, _zoneCenter.z + hz));
        }

        bool HitPlayer(float ratio, float knockback) => Player.TryReceiveHit(AttackPower * ratio, transform.position, knockback, Body);

        protected override void OnAttackFinished()
        {
            IsWallStunned = false;
            _decisionTimer = _decisionPause;
        }

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
            IsWallStunned = false;
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
            foreach (var r in _rocks) if (r.marker != null) Destroy(r.marker.gameObject);
            _rocks.Clear();
        }
    }
}
