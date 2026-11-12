using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 수채주 철룡 (장강 수채 보스, 몬스터 시트 66행): 체력 1900 · 공격 25 · 강인함 6 · 방어 15% (장강 배율 전 → 5700 · 40). 쇠사슬 유성추를 쓰는 수적 두목.
    // 보스 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 체력 50%에서 2초 전환 (무적), 같은 패턴은 연속 2번까지.
    // 1페이즈
    //   유성추 휘두르기: 6칸 안이면 예고 0.9 · 앞 부채꼴 반경 6칸 140° ×1.0 + 넉백 2칸
    //   유성추 내려찍기: 멀면 플레이어 자리에 예고 원 (반경 2칸, 1초) → ×1.2
    //   사슬 당기기: 7칸 밖에 2초 넘게 있으면 예고 0.7 · 직선 10칸 (폭 1.2칸) ×0.6, 맞으면 철룡 2.5칸 앞까지 끌려오고 곧바로 유성추 휘두르기 (예고 절반)
    //   수하 부르기: 15초마다 깃발수가 없으면 깃발수 하나 + 수적 졸개 둘
    // 2페이즈 (배가 부서져요): 방 곳곳 물길 3곳 (반경 2.5칸, 안에 있으면 이동 −30%)
    //   연속 내려찍기: 내려찍기가 0.5초 간격으로 플레이어를 따라 세 번
    //   물기둥: 9초마다 0.4초 간격으로 플레이어 둘레에 물기둥 5개 (예고 0.8초, 반경 1.3칸) ×0.7
    public sealed class IronDragonBrain : EnemyBrain, IMasterBrain
    {
        enum Pattern { None, Swing, Slam, Hook, Summon, Geyser }

        struct Mark
        {
            public Vector3 At;
            public float Timer, Total, Radius, Ratio;
            public Telegraph Marker;
        }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat;
        float _decisionPause, _transitionTime, _phase2Ratio;
        float _swingWindup, _swingRadius, _swingAngle, _swingRatio, _swingKnockback;
        float _slamWindup, _slamRadius, _slamRatio, _slamGap;
        int _slamHits, _slamFollow;
        float _slamFollowTimer;
        float _hookFar, _hookDelay, _hookWindup, _hookRange, _hookWidth, _hookRatio, _hookStop, _farTimer;
        bool _hooked;
        float _summonInterval, _summonTimer;
        float _geyserInterval, _geyserTimer, _geyserWindup, _geyserFall, _geyserRadius, _geyserRatio, _geyserGap;
        int _geyserCount, _geysersDone;
        float _waterRadius, _waterSlow;
        int _waterCount;
        float _decisionTimer, _transitionTimer, _activeTimer;
        Vector3 _zoneCenter;
        Vector2 _zoneSize = new Vector2(24f, 18f);
        readonly List<Mark> _marks = new List<Mark>();
        readonly List<GameObject> _water = new List<GameObject>();

        public override EnemyKind Kind => EnemyKind.IronDragon;
        protected override string Key => "irondragon";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1.5f;
        protected override float ActiveTime => _pattern == Pattern.Geyser ? (_geyserCount - 1) * _geyserGap + 0.05f : 0.1f;

        public override string AttackName => _pattern switch
        {
            Pattern.Swing => "유성추 휘두르기", Pattern.Slam => "유성추 내려찍기", Pattern.Hook => "사슬 당기기",
            Pattern.Summon => "수하 부르기", Pattern.Geyser => "물기둥", _ => null,
        };

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public bool HookedPlayer => _hooked;
        public int WaterCount => _water.Count;

        public void SetZone(Vector3 center, Vector2 size)
        {
            _zoneCenter = new Vector3(center.x, 0f, center.z);
            _zoneSize = size;
        }

        protected override void OnInitialized() => _zoneCenter = new Vector3(transform.position.x, 0f, transform.position.z);

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _decisionPause = Tuning("decision_pause", 0.5f);
            _transitionTime = Tuning("transition_time", 2f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _swingWindup = Tuning("swing_windup", 0.9f);
            _swingRadius = Tuning("swing_radius", 6f);
            _swingAngle = Tuning("swing_angle", 140f);
            _swingRatio = Tuning("swing_ratio", 1f);
            _swingKnockback = Tuning("swing_knockback", 2f);
            _slamWindup = Tuning("slam_windup", 1f);
            _slamRadius = Tuning("slam_radius", 2f);
            _slamRatio = Tuning("slam_ratio", 1.2f);
            _slamGap = Tuning("p2_slam_gap", 0.5f);
            _slamHits = Mathf.Max(1, Mathf.RoundToInt(Tuning("p2_slam_hits", 3f)));
            _hookFar = Tuning("hook_far", 7f);
            _hookDelay = Tuning("hook_delay", 2f);
            _hookWindup = Tuning("hook_windup", 0.7f);
            _hookRange = Tuning("hook_range", 10f);
            _hookWidth = Tuning("hook_width", 1.2f);
            _hookRatio = Tuning("hook_ratio", 0.6f);
            _hookStop = Tuning("hook_stop", 2.5f);
            _summonInterval = Tuning("summon_interval", 15f);
            _geyserInterval = Tuning("p2_geyser_interval", 9f);
            _geyserWindup = Tuning("p2_geyser_windup", 0.6f);
            _geyserFall = Tuning("p2_geyser_fall", 0.8f);
            _geyserRadius = Tuning("p2_geyser_radius", 1.3f);
            _geyserRatio = Tuning("p2_geyser_ratio", 0.7f);
            _geyserGap = Tuning("p2_geyser_gap", 0.4f);
            _geyserCount = Mathf.Max(1, Mathf.RoundToInt(Tuning("p2_geyser_count", 5f)));
            _waterCount = Mathf.RoundToInt(Tuning("p2_water_count", 3f));
            _waterRadius = Tuning("p2_water_radius", 2.5f);
            _waterSlow = Tuning("p2_water_slow", 0.3f);
            _summonTimer = _summonInterval * 0.5f;
            _geyserTimer = _geyserInterval * 0.5f;
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_summonTimer > 0f) _summonTimer -= dt;
            if (_geyserTimer > 0f) _geyserTimer -= dt;
            _farTimer = ToPlayer().magnitude > _hookFar ? _farTimer + dt : 0f; // 멀리서만 싸우면 사슬 당기기
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
            TickSlamFollow(dt);
            TickMarks(dt);
            TickWater();
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_hooked) next = Pattern.Swing; // 끌려온 플레이어에게 바로
            else if (_farTimer >= _hookDelay) next = Pattern.Hook;
            else if (_summonTimer <= 0f && Director.Count(EnemyKind.FlagBearer) == 0) next = Pattern.Summon;
            else if (Phase == 2 && _geyserTimer <= 0f) next = Pattern.Geyser;
            else if (distance <= _swingRadius * 0.85f) next = Pattern.Swing;
            else next = Pattern.Slam;
            if (next == _lastPattern && _repeat >= _maxRepeat && !_hooked && (next == Pattern.Swing || next == Pattern.Slam))
                next = next == Pattern.Swing ? Pattern.Slam : Pattern.Swing;
            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            RecoveryTime = Tuning("recovery", 0.9f);
            switch (next)
            {
                case Pattern.Swing:
                    WindupTime = _hooked ? _swingWindup * 0.5f : _swingWindup; // 끌려오면 예고가 짧아요
                    _hooked = false;
                    break;
                case Pattern.Slam: WindupTime = _slamWindup; break;
                case Pattern.Hook:
                    WindupTime = _hookWindup;
                    RecoveryTime = 0.3f;
                    _farTimer = 0f;
                    break;
                case Pattern.Summon:
                    WindupTime = 0.8f;
                    RecoveryTime = 0.5f;
                    _summonTimer = _summonInterval;
                    break;
                case Pattern.Geyser:
                    WindupTime = _geyserWindup;
                    _geyserTimer = _geyserInterval;
                    break;
            }
            return true;
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (IsTransitioning) return;
            Vector3 to = ToPlayer();
            FaceToward(to);
            if (to.magnitude > _swingRadius * 0.6f) Move(to.normalized * MoveSpeed, dt);
        }

        protected override void TickWindup(float dt, bool tracking)
        {
            if (_pattern == Pattern.Swing && tracking) FaceToward(ToPlayer());
            else if (_pattern == Pattern.Hook && tracking) base.TickWindup(dt, tracking);
        }

        // ── 예고

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Swing => TelegraphShapes.Lunge(_swingRadius, _swingAngle, 0f),
            Pattern.Hook => TelegraphShapes.Rect(_hookWidth, _hookRange),
            _ => TelegraphShapes.Circle(BodyRadius + 0.3f), // 몸 둘레에 기운이 모여요 (실제 판정은 바닥 예고 원)
        };

        protected override void OnAttackStarted()
        {
            if (_pattern != Pattern.Slam) return;
            AddMark(Player.transform.position, _slamWindup, _slamRadius, _slamRatio);
            if (Phase == 2) // 연속 내려찍기: 플레이어를 따라 두 번 더
            {
                _slamFollow = _slamHits - 1;
                _slamFollowTimer = _slamGap;
            }
        }

        // ── 판정

        protected override void OnActiveStarted()
        {
            _activeTimer = 0f;
            _geysersDone = 0;
            switch (_pattern)
            {
                case Pattern.Swing:
                    Director.SpawnBurst(transform.position + AimDirection * (_swingRadius * 0.5f), _swingRadius * 0.4f);
                    if (HitShapes.InSector(transform.position, AimDirection, _swingRadius, _swingAngle, Player.transform.position, PlayerRadius))
                        Player.TryReceiveHit(AttackPower * _swingRatio, transform.position, _swingKnockback, Body);
                    break;
                case Pattern.Hook: Hook(); break;
                case Pattern.Summon:
                    Director.Spawn(EnemyKind.FlagBearer, Director.FindFreeSpot(transform.position + Facing * 2f, 1f));
                    for (int i = 0; i < 2; i++) Director.Spawn(EnemyKind.Pirate, Director.FindFreeSpot(transform.position + Facing * 2.5f, 1.5f));
                    break;
                case Pattern.Geyser: Geyser(); break;
            }
        }

        protected override void TickActive(float dt)
        {
            _activeTimer += dt;
            if (_pattern == Pattern.Geyser)
                while (_geysersDone < _geyserCount && _activeTimer >= _geysersDone * _geyserGap) Geyser();
        }

        // 사슬 당기기: 앞 직선 위에 있으면 맞고 철룡 앞으로 끌려와요
        void Hook()
        {
            Vector3 rel = Player.transform.position - transform.position;
            rel.y = 0f;
            float along = Vector3.Dot(rel, AimDirection);
            float side = Mathf.Abs(Vector3.Dot(rel, new Vector3(AimDirection.z, 0f, -AimDirection.x)));
            if (along < 0f || along > _hookRange + PlayerRadius || side > _hookWidth * 0.5f + PlayerRadius) return;
            if (!Player.TryReceiveHit(AttackPower * _hookRatio, transform.position, 0f, Body)) return;
            Player.PullToward(transform.position, _hookStop);
            _hooked = true;
        }

        void Geyser()
        {
            _geysersDone++;
            Vector2 r = Random.insideUnitCircle * 2.5f;
            AddMark(Player.transform.position + new Vector3(r.x, 0f, r.y), _geyserFall, _geyserRadius, _geyserRatio);
        }

        void TickSlamFollow(float dt)
        {
            if (_slamFollow <= 0) return;
            _slamFollowTimer -= dt;
            if (_slamFollowTimer > 0f) return;
            _slamFollow--;
            _slamFollowTimer = _slamGap;
            AddMark(Player.transform.position, _slamWindup, _slamRadius, _slamRatio);
        }

        // ── 바닥 예고 원 (내려찍기 · 물기둥): 시간이 다 되면 그 자리에 떨어져요

        void AddMark(Vector3 at, float fall, float radius, float ratio)
        {
            at.y = 0f;
            var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
            marker.Show(TelegraphShapes.Circle(radius), true);
            marker.SetPose(at, Vector3.forward);
            marker.SetProgress(0f);
            _marks.Add(new Mark { At = at, Timer = fall, Total = fall, Radius = radius, Ratio = ratio, Marker = marker });
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
                Director.SpawnBurst(m.At, m.Radius);
                Vector3 d = Player.transform.position - m.At;
                d.y = 0f;
                if (d.magnitude <= m.Radius + PlayerRadius) Player.TryReceiveHit(AttackPower * m.Ratio, m.At, -1f, Body);
            }
        }

        // ── 물길 (2페이즈): 안에 있는 동안 느려져요

        void MakeWater()
        {
            float hx = _zoneSize.x * 0.5f - _waterRadius, hz = _zoneSize.y * 0.5f - _waterRadius;
            for (int i = 0; i < _waterCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "물길";
                EnemyFactory.StripCollider(go);
                go.transform.position = _zoneCenter + new Vector3(Random.Range(-hx, hx), 0.02f, Random.Range(-hz, hz));
                go.transform.localScale = new Vector3(_waterRadius * 2f, 0.01f, _waterRadius * 2f);
                var renderer = go.GetComponent<Renderer>();
                renderer.material.color = new Color(0.25f, 0.4f, 0.45f);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _water.Add(go);
            }
        }

        void TickWater()
        {
            if (_water.Count == 0 || Player == null) return;
            foreach (var w in _water)
            {
                if (w == null) continue;
                Vector3 d = Player.transform.position - w.transform.position;
                d.y = 0f;
                if (d.magnitude > _waterRadius) continue;
                Player.ApplySlow(_waterSlow, 0.2f); // 물길 안에 있는 동안
                return;
            }
        }

        public bool PlayerInWater
        {
            get
            {
                foreach (var w in _water)
                {
                    if (w == null) continue;
                    Vector3 d = Player.transform.position - w.transform.position;
                    d.y = 0f;
                    if (d.magnitude <= _waterRadius) return true;
                }
                return false;
            }
        }

        protected override void OnAttackFinished() => _decisionTimer = _hooked ? 0f : _decisionPause; // 끌려왔으면 쉬지 않고 휘둘러요

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            _hooked = false;
            _slamFollow = 0;
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
            _hooked = false;
            _transitionTimer = _transitionTime;
            Body.Targetable = false;
            Director.SpawnBurst(transform.position, 3f); // 배가 부서져요
        }

        void EnterPhase2()
        {
            Phase = 2;
            Body.Targetable = true;
            _decisionTimer = _decisionPause;
            _repeat = 0;
            MakeWater();
        }

        protected override void OnDied()
        {
            foreach (var m in _marks) if (m.Marker != null) Destroy(m.Marker.gameObject);
            _marks.Clear();
            foreach (var w in _water) if (w != null) Destroy(w);
            _water.Clear();
        }
    }
}
