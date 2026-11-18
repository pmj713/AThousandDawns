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
    // 철권 마웅 (보스 · 고수끼리 겹치지 않게):
    //   주먹 2연타: 가까울 때. 예고 0.5, 앞 부채꼴 1.5칸 70° × 2번 (0.35초 간격), 한 번에 0.5칸 전진, ×0.7, 빈틈 0.7
    //   잡아 던지기: 7초마다 붙어 있으면. 예고 0.6, 앞 부채꼴 1.6칸 80°에 잡히면 ×1.0 + 4칸 내던짐. 2페이즈는 예고 ×0.7
    //   돌진: 멀 때. 예고 0.8, 벽까지 직선 초당 13칸 + 넉백, ×1.0. 벽에 부딪히면 2.5초 기절 (2페이즈 1.5초)
    //   권풍: 중간 거리에서 5초마다 크고 느린 장풍 한 발 (관통, ×0.8)
    // 이어받는 판타지 고수 · 보스는 근접 · 특수 · 먼 거리 자리를 저마다 바꿔 끼워요 (고블린 족장 · 오우거 · 데스나이트).
    public class IronFistBrain : EnemyBrain, IMasterBrain
    {
        public override string AttackName => _pattern switch
        {
            Pattern.Jab => JabName, Pattern.Quake => QuakeName, Pattern.Charge => ChargeName, Pattern.Leap => FarLeapName, _ => null,
        };
        protected virtual string JabName => "주먹 2연타";
        protected virtual string QuakeName => "잡아 던지기";
        protected virtual string ChargeName => "돌진";
        protected virtual string FarLeapName => "도약";

        // ── 자리 바꿔 끼우기 (이어받는 쪽)
        protected enum MeleeKind { Jab, Sweep, Overhead, TwinRect }
        protected enum SpecialKind { Grab, None }
        protected enum FarKind { Charge, Leap }
        protected virtual MeleeKind Melee => MeleeKind.Jab;
        protected virtual SpecialKind Special => SpecialKind.Grab;
        protected virtual FarKind Far => FarKind.Charge;

        enum Pattern { None, Jab, Quake, Charge, Leap }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat, _jabsDone;
        float _closeDistance, _farDistance, _decisionPause, _transitionTime, _phase2Ratio;
        float _jabWindup, _jabRecovery, _jabRatio, _jabRadius, _jabAngle, _jabGap, _jabLunge;
        float _meleeLength, _meleeWidth;
        float _grabWindup, _grabRecovery, _grabRatio, _grabRadius, _grabAngle, _grabThrow, _grabInterval, _grabRange;
        float _chargeWindup, _chargeRatio, _chargeSpeed, _chargeWidth, _chargeKnockback, _chargeMax, _stun, _p2Stun;
        float _leapWindup, _leapRadius, _leapRatio, _leapRecovery, _leapInterval, _leapTimer, _leapAir;
        float _waveInterval, _waveTimer, _waveSpeed, _waveRadius, _waveRatio, _waveRange;
        float _rockRatio, _rockRadius, _rockFall;
        float _decisionTimer, _transitionTimer, _grabTimer, _activeTimer, _lungeLeft, _traveled, _plannedLength;
        bool _hitPlayer, _leapDescending;
        Vector3 _leapTo, _leapFrom;
        Vector3 _zoneCenter;
        Vector2 _zoneSize;
        bool _hasZone;
        readonly List<(Vector3 at, float timer, Telegraph marker)> _rocks = new List<(Vector3, float, Telegraph)>();

        const float MaxChargeStep = 0.25f;

        public static event System.Action<IronFistBrain, Vector3, Vector3> Grabbed;  // 잡아 던짐 (보스, 잡은 자리, 던진 방향)
        public static event System.Action<IronFistBrain, Vector3> FistWave;         // 권풍을 쏨 (보스, 방향)
        public static event System.Action<IronFistBrain, Vector3, Vector3> FarLeaped; // 먼 거리 도약이 땅에 닿음 = 판정·소리 (보스, 떠난 자리, 내려앉은 자리)
        public static event System.Action<IronFistBrain, Vector3, Vector3, float> FarLeapDescending; // 내려앉기 시작 (보스, 떠난 자리, 내려앉을 자리, 땅에 닿기까지 초) (아트: 이 시간 동안 내려와 판정 순간 땅에 닿아요)

        public override EnemyKind Kind => EnemyKind.IronFist;
        protected override string Key => "ironfist";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Master;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1f;
        protected override bool RadialTelegraph => _pattern == Pattern.Leap;
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Jab => MeleeHitCount > 1 ? _jabGap * (MeleeHitCount - 1) + 0.1f : 0.1f,
            Pattern.Charge => float.PositiveInfinity, // 벽에 닿으면 EndActive
            _ => 0.1f,
        };

        int MeleeHitCount => Melee == MeleeKind.Jab || Melee == MeleeKind.TwinRect ? 2 : 1;

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public bool IsWallStunned { get; private set; }
        public bool IsCharging => _pattern == Pattern.Charge && Attack.Current == EnemyAttackTimeline.Phase.Active;

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
            _meleeLength = Tuning("melee_length", 3.5f);
            _meleeWidth = Tuning("melee_width", 1.3f);
            _grabWindup = Tuning("grab_windup", 0.6f);
            _grabRecovery = Tuning("grab_recovery", 0.9f);
            _grabRatio = Tuning("grab_ratio", 1f);
            _grabRadius = Tuning("grab_radius", 1.6f);
            _grabAngle = Tuning("grab_angle", 80f);
            _grabThrow = Tuning("grab_throw", 4f);
            _grabInterval = Tuning("grab_interval", 7f);
            _grabRange = Tuning("grab_range", 2f);
            _chargeWindup = Tuning("charge_windup", 0.8f);
            _chargeRatio = Tuning("charge_ratio", 1f);
            _chargeSpeed = Tuning("charge_speed", 13f);
            _chargeWidth = Tuning("charge_width", 1.2f);
            _chargeKnockback = Tuning("charge_knockback", 1.5f);
            _chargeMax = Tuning("charge_max", 16f);
            _stun = Tuning("wall_stun", 2.5f);
            _p2Stun = Tuning("p2_wall_stun", 1.5f);
            _leapWindup = Tuning("leap_windup", 0.9f);
            _leapRadius = Tuning("leap_radius", 2.2f);
            _leapRatio = Tuning("leap_ratio", 1f);
            _leapRecovery = Tuning("leap_recovery", 1f);
            _leapInterval = Tuning("leap_interval", 6f);
            _leapAir = Tuning("leap_air", 0.3f);
            _waveInterval = Tuning("wave_interval", 5f);
            _waveSpeed = Tuning("wave_speed", 7f);
            _waveRadius = Tuning("wave_radius", 0.9f);
            _waveRatio = Tuning("wave_ratio", 0.8f);
            _waveRange = Tuning("wave_range", 12f);
            _rockRatio = Tuning("rock_ratio", 0.8f);
            _rockRadius = Tuning("rock_radius", 1f);
            _rockFall = Tuning("rock_fall", 0.8f);
            _grabTimer = _grabInterval * 0.5f;
            _waveTimer = _waveInterval * 0.4f;
            ReadMoreTuning();
        }

        protected virtual void ReadMoreTuning() { }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_grabTimer > 0f) _grabTimer -= dt;
            if (_leapTimer > 0f) _leapTimer -= dt;
            if (_waveTimer > 0f) _waveTimer -= dt;
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
            TickRocks(dt);
            TickMore(dt);
        }

        protected virtual void TickMore(float dt) { }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (Special == SpecialKind.Grab && _grabTimer <= 0f && distance <= _grabRange) next = Pattern.Quake;
            else if (distance <= _closeDistance) next = Pattern.Jab;
            else if (distance >= _farDistance && (Far == FarKind.Charge || _leapTimer <= 0f)) next = Far == FarKind.Charge ? Pattern.Charge : Pattern.Leap;
            else { TryMidRange(); return false; } // 중간 거리면 다가가며 저마다의 원거리 수 (권풍 · 바위 · 망령 기병 · 덫)
            if (next == _lastPattern && _repeat >= _maxRepeat)
            {
                if (next == Pattern.Quake) next = Pattern.Jab;
                else return Rest(); // 같은 수만 연속이면 한 번 쉬어요 (쉰 뒤엔 다시 셀 수 있게)
            }

            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            float p2 = Phase >= 2 ? WindupP2 : 1f;
            switch (next)
            {
                case Pattern.Jab: WindupTime = _jabWindup * p2; RecoveryTime = _jabRecovery; break;
                case Pattern.Quake:
                    WindupTime = _grabWindup * (Phase >= 2 ? 0.7f : 1f); RecoveryTime = _grabRecovery;
                    _grabTimer = _grabInterval;
                    break;
                case Pattern.Charge: WindupTime = _chargeWindup; RecoveryTime = Phase == 1 ? _stun : _p2Stun; break;
                case Pattern.Leap:
                    WindupTime = _leapWindup * p2; RecoveryTime = _leapRecovery;
                    _leapTimer = _leapInterval;
                    _leapDescending = false;
                    _leapTo = Director.FindFreeSpot(Player.transform.position, BodyRadius + PlayerRadius + 0.2f);
                    _leapTo = Clamp(_leapTo);
                    break;
            }
            return true;
        }

        bool Rest()
        {
            _repeat = 0;
            _lastPattern = Pattern.None;
            _decisionTimer = _decisionPause * 2f;
            return false;
        }

        // 2페이즈 예고 배율 (오우거 분노처럼 이어받는 쪽이 바꿔요)
        protected virtual float WindupP2 => 1f;

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsTransitioning) return;
            Vector3 to = ToPlayer();
            FaceToward(to);
            if (to.magnitude > _closeDistance * 0.8f) Move(to.normalized * MoveSpeed * MoveP2, dt);
        }

        protected virtual float MoveP2 => 1f;

        // ── 예고

        protected override List<Vector2> TelegraphShape()
        {
            switch (_pattern)
            {
                case Pattern.Jab:
                    return Melee switch
                    {
                        MeleeKind.Sweep => TelegraphShapes.Lunge(_jabRadius, _jabAngle, 0f),
                        MeleeKind.Overhead or MeleeKind.TwinRect => TelegraphShapes.Rect(_meleeWidth, _meleeLength),
                        _ => TelegraphShapes.Lunge(_jabRadius, _jabAngle, _jabLunge * 2f),
                    };
                case Pattern.Quake: return TelegraphShapes.Lunge(_grabRadius, _grabAngle, 0f);
                case Pattern.Charge:
                    _plannedLength = PlannedCharge();
                    return TelegraphShapes.Rect(_chargeWidth, _plannedLength);
                case Pattern.Leap: return TelegraphShapes.Circle(_leapRadius);
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
            if (_pattern == Pattern.Leap && Telegraph != null) Telegraph.SetPose(_leapTo, Vector3.forward); // 내려앉을 자리에
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
                    MeleeStrike();
                    break;
                case Pattern.Quake:
                    Grab();
                    break;
                case Pattern.Charge:
                    _traveled = 0f;
                    break;
                case Pattern.Leap:
                    LandLeap();
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
                    if (_jabsDone < MeleeHitCount && _activeTimer >= _jabGap * _jabsDone) MeleeStrike();
                    break;
                case Pattern.Charge:
                    TickCharge(dt);
                    break;
            }
        }

        // 근접 평타: 철권 주먹 2연타 · 족장 휘돌리기 (넓은 부채꼴 한 번) · 오우거 내려찍기 (좁고 긴 한 방) · 데스나이트 올려베기 → 내려베기
        void MeleeStrike()
        {
            _jabsDone++;
            switch (Melee)
            {
                case MeleeKind.Jab:
                    _lungeLeft = _jabLunge;
                    if (SwingHit(transform.position, AimDirection, _jabRadius, _jabAngle)) HitPlayer(_jabRatio, HitKnockback);
                    break;
                case MeleeKind.Sweep:
                    if (SwingHit(transform.position, AimDirection, _jabRadius, _jabAngle)) HitPlayer(_jabRatio, Tuning("sweep_knockback", 2f));
                    break;
                case MeleeKind.Overhead:
                    Director.SpawnBurst(transform.position + AimDirection * (_meleeLength * 0.6f), _meleeWidth, BurstKind.Quake);
                    if (InFrontRect(_meleeWidth, _meleeLength)) HitPlayer(_jabRatio, HitKnockback);
                    break;
                case MeleeKind.TwinRect:
                    if (InFrontRect(_meleeWidth, _meleeLength)) HitPlayer(_jabRatio, HitKnockback);
                    break;
            }
        }

        bool InFrontRect(float width, float length)
        {
            Vector3 rel = Player.transform.position - transform.position;
            rel.y = 0f;
            float along = Vector3.Dot(rel, AimDirection);
            float side = Mathf.Abs(Vector3.Dot(rel, new Vector3(AimDirection.z, 0f, -AimDirection.x)));
            return along >= -PlayerRadius && along <= length + PlayerRadius && side <= width * 0.5f + PlayerRadius;
        }

        // 잡아 던지기: 앞 부채꼴에 잡히면 내던져요
        void Grab()
        {
            if (!SwingHit(transform.position, AimDirection, _grabRadius, _grabAngle)) return;
            if (!HitPlayer(_grabRatio, _grabThrow)) return;
            Grabbed?.Invoke(this, Player.transform.position, AimDirection);
        }

        // 먼 거리 도약은 예고가 leap_air초 남았을 때 내려앉을 자리로 몸을 옮겨요 (아트는 그동안 공중에서 내려와요).
        //   그래야 몸이 땅에 닿는 순간 = 판정 · 내려찍는 소리 · 흙먼지가 한꺼번에 나요 (예전엔 소리가 먼저 나고 몸이 0.3초 뒤에 닿았어요)
        protected override void TickWindup(float dt, bool tracking)
        {
            base.TickWindup(dt, tracking);
            if (_pattern == Pattern.Leap && !_leapDescending && Attack.WindupLeft <= _leapAir) BeginLeapDescent(Attack.WindupLeft);
        }

        void BeginLeapDescent(float untilLanding)
        {
            _leapDescending = true;
            _leapFrom = transform.position;
            Teleport(_leapTo);
            FarLeapDescending?.Invoke(this, _leapFrom, _leapTo, untilLanding / Mathf.Max(0.1f, Status.SpeedMultiplier)); // 둔화면 실제 시간으로는 더 길어요
        }

        // 먼 거리 도약: 내려앉을 자리 예고 원 → 그 자리로 옮겨 내려찍기 (족장 덮치기 · 오우거 땅울림 · 데스나이트 어둠 걸음)
        void LandLeap()
        {
            if (!_leapDescending) BeginLeapDescent(_leapAir); // 예고가 너무 짧아 미리 못 옮겼으면 지금 (아트는 예전처럼 잠깐 뒤에 닿아요)
            _leapDescending = false;
            Director.SpawnBurst(_leapTo, _leapRadius, BurstKind.Quake); // 내려찍는 소리도 여기서 (EnemyDirector.SpawnBurst → Sfx Explosion)
            Vector3 d = Player.transform.position - _leapTo;
            d.y = 0f;
            if (d.magnitude <= _leapRadius + PlayerRadius) HitPlayer(_leapRatio, Tuning("leap_knockback", 1.5f));
            FarLeaped?.Invoke(this, _leapFrom, _leapTo);
            OnFarLeapLanded(_leapTo);
        }

        protected virtual void OnFarLeapLanded(Vector3 at) { }

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
                Attack.EndActive(Phase == 1 ? _stun : _p2Stun);
            }
            else Attack.EndActive(_jabRecovery);
        }

        // 철권 마웅 권풍: 중간 거리에서 크고 느린 장풍 한 발 (관통)
        protected virtual void TryMidRange()
        {
            if (_waveTimer > 0f || Attack.IsAttacking) return;
            _waveTimer = _waveInterval;
            Vector3 to = ToPlayer();
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : Facing;
            FaceToward(dir);
            Director.SpawnArrow(transform.position + Vector3.up * 1.2f + dir * (BodyRadius + 0.4f), dir, _waveSpeed, _waveRange, _waveRadius, AttackPower * _waveRatio, Body);
            FistWave?.Invoke(this, dir);
        }

        // 오우거 바위 던지기: 플레이어 둘레에 돌 3개 (예고 원 뒤 떨어짐)
        protected void DropRocks()
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
                Director.SpawnBurst(at, _rockRadius, AThousandDawns.Combat.BurstKind.Quake);
                Vector3 d = Player.transform.position - at;
                d.y = 0f;
                if (d.magnitude <= _rockRadius + PlayerRadius) Player.TryReceiveHit(AttackPower * _rockRatio, at, -1f, Body);
            }
        }

        protected Vector3 Clamp(Vector3 p)
        {
            if (!_hasZone) return p;
            float hx = _zoneSize.x * 0.5f - 0.8f, hz = _zoneSize.y * 0.5f - 0.8f;
            return new Vector3(Mathf.Clamp(p.x, _zoneCenter.x - hx, _zoneCenter.x + hx), 0f, Mathf.Clamp(p.z, _zoneCenter.z - hz, _zoneCenter.z + hz));
        }

        protected Vector3 ZoneCenter => _hasZone ? _zoneCenter : transform.position;
        protected Vector2 ZoneSize => _hasZone ? _zoneSize : new Vector2(20f, 14f);

        protected bool HitPlayer(float ratio, float knockback) => Player.TryReceiveHit(AttackPower * ratio, transform.position, knockback, Body);

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
            OnEnterPhase2();
        }

        // 이어받는 판타지 고수 · 보스가 덧붙여요
        protected virtual void OnEnterPhase2() { }

        protected override void OnDied()
        {
            foreach (var r in _rocks) if (r.marker != null) Destroy(r.marker.gameObject);
            _rocks.Clear();
            OnDiedMore();
        }

        protected virtual void OnDiedMore() { }
    }
}
