using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 혈불 (폐사찰 보스, 몬스터 시트 64행): 체력 1850 · 공격 24 · 강인함 5 · 방어 15% (폐사찰 배율 전). 피 흘리는 거대 불상, 느리게 걸어요.
    // 보스 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 체력 50%에서 2초 전환 (무적), 같은 패턴은 연속 2번까지.
    // 1페이즈
    //   손바닥 내려찍기: 플레이어 자리와 둘레 2곳에 예고 원 (반경 1.6칸, 1초) → 내려찍기 ×1.0
    //   염불: 10초마다. 3초 동안 0.5초 간격으로 방 곳곳에 붉은 장판 4개씩 (예고 0.6초, 반경 1.2칸) ×0.6
    //   목탁승 부르기: 목탁승이 없으면 15초마다 한 명
    // 2페이즈 (불상이 갈라져 팔이 여섯)
    //   팔 휘두르기: 가까울 때 부채꼴 반경 3.5칸 120°를 좌우로 세 번 (0.45초 간격) ×0.8
    //   피의 비: 플레이어 둘레에 핏방울 (예고 0.7초, 반경 1칸)이 0.4초 간격 6번 ×0.5 + 출혈 (플레이어 화상 1중첩)
    //   손바닥 내려찍기 · 목탁승 부르기는 그대로, 염불 대신 피의 비
    public sealed class BloodBuddhaBrain : EnemyBrain, IMasterBrain
    {
        public override string AttackName => _pattern switch
        {
            Pattern.Palm => "손바닥 내려찍기", Pattern.Chant => "염불", Pattern.Arms => "팔 휘두르기", Pattern.BloodRain => "피의 비", _ => null,
        };

        enum Pattern { None, Palm, Chant, Arms, BloodRain, Summon }

        struct Mark
        {
            public Vector3 At;
            public float Timer, Total, Radius, Ratio;
            public bool Bleed;
            public Telegraph Marker;
        }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat, _waveCount, _wavesDone, _swingsDone;
        float _closeDistance, _decisionPause, _transitionTime, _phase2Ratio;
        float _palmWindup, _palmRecovery, _palmRatio, _palmRadius, _palmSpread;
        float _chantWindup, _chantRecovery, _chantRatio, _chantRadius, _chantFall, _chantGap, _chantInterval;
        int _chantWaves, _chantPerWave;
        float _armsWindup, _armsRecovery, _armsRatio, _armsRadius, _armsAngle, _armsGap;
        float _rainWindup, _rainRecovery, _rainRatio, _rainRadius, _rainFall, _rainGap, _bleedDps;
        int _rainDrops;
        float _summonInterval;
        float _decisionTimer, _transitionTimer, _chantTimer, _summonTimer, _activeTimer;
        Vector3 _zoneCenter;
        Vector2 _zoneSize = new Vector2(16f, 12f);
        readonly List<Mark> _marks = new List<Mark>();

        public override EnemyKind Kind => EnemyKind.BloodBuddha;
        protected override string Key => "bloodbuddha";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1.5f;
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Chant => (_chantWaves - 1) * _chantGap + 0.05f,
            Pattern.BloodRain => (_rainDrops - 1) * _rainGap + 0.05f,
            Pattern.Arms => 2f * _armsGap + 0.05f,
            _ => 0.1f,
        };

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;

        public void SetZone(Vector3 center, Vector2 size)
        {
            _zoneCenter = new Vector3(center.x, 0f, center.z);
            _zoneSize = size;
        }

        protected override void OnInitialized() => _zoneCenter = new Vector3(transform.position.x, 0f, transform.position.z);

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 3.5f);
            _decisionPause = Tuning("decision_pause", 0.5f);
            _transitionTime = Tuning("transition_time", 2f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _palmWindup = Tuning("palm_windup", 1f);
            _palmRecovery = Tuning("palm_recovery", 0.8f);
            _palmRatio = Tuning("palm_ratio", 1f);
            _palmRadius = Tuning("palm_radius", 1.6f);
            _palmSpread = Tuning("palm_spread", 2.5f);
            _chantWindup = Tuning("chant_windup", 0.8f);
            _chantRecovery = Tuning("chant_recovery", 1f);
            _chantRatio = Tuning("chant_ratio", 0.6f);
            _chantRadius = Tuning("chant_radius", 1.2f);
            _chantFall = Tuning("chant_fall", 0.6f);
            _chantGap = Tuning("chant_gap", 0.5f);
            _chantWaves = Mathf.RoundToInt(Tuning("chant_waves", 6f));
            _chantPerWave = Mathf.RoundToInt(Tuning("chant_per_wave", 4f));
            _chantInterval = Tuning("chant_interval", 10f);
            _armsWindup = Tuning("arms_windup", 0.6f);
            _armsRecovery = Tuning("arms_recovery", 1f);
            _armsRatio = Tuning("arms_ratio", 0.8f);
            _armsRadius = Tuning("arms_radius", 3.5f);
            _armsAngle = Tuning("arms_angle", 120f);
            _armsGap = Tuning("arms_gap", 0.45f);
            _rainWindup = Tuning("rain_windup", 0.6f);
            _rainRecovery = Tuning("rain_recovery", 0.8f);
            _rainRatio = Tuning("rain_ratio", 0.5f);
            _rainRadius = Tuning("rain_radius", 1f);
            _rainFall = Tuning("rain_fall", 0.7f);
            _rainGap = Tuning("rain_gap", 0.4f);
            _rainDrops = Mathf.RoundToInt(Tuning("rain_drops", 6f));
            _bleedDps = Tuning("bleed_dps", 2f);
            _summonInterval = Tuning("summon_interval", 15f);
            _chantTimer = _chantInterval * 0.5f;
            _summonTimer = _summonInterval * 0.5f;
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_chantTimer > 0f) _chantTimer -= dt;
            if (_summonTimer > 0f) _summonTimer -= dt;
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
            TickMarks(dt);
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_summonTimer <= 0f && Director.Count(EnemyKind.Moktak) == 0) next = Pattern.Summon;
            else if (Phase == 2 && distance <= _closeDistance) next = Pattern.Arms;
            else if (_chantTimer <= 0f) next = Phase == 1 ? Pattern.Chant : Pattern.BloodRain;
            else next = Pattern.Palm;
            if (next == _lastPattern && _repeat >= _maxRepeat && next != Pattern.Summon)
                next = next == Pattern.Palm ? (Phase == 1 ? Pattern.Chant : Pattern.BloodRain) : Pattern.Palm;

            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            switch (next)
            {
                case Pattern.Palm: WindupTime = _palmWindup; RecoveryTime = _palmRecovery; break;
                case Pattern.Chant:
                    WindupTime = _chantWindup; RecoveryTime = _chantRecovery;
                    _chantTimer = _chantInterval;
                    break;
                case Pattern.BloodRain:
                    WindupTime = _rainWindup; RecoveryTime = _rainRecovery;
                    _chantTimer = _chantInterval * 0.7f;
                    break;
                case Pattern.Arms: WindupTime = _armsWindup; RecoveryTime = _armsRecovery; break;
                case Pattern.Summon:
                    WindupTime = 0.8f; RecoveryTime = 0.5f;
                    _summonTimer = _summonInterval;
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

        // 거대 불상은 예고 동안 움직이지 않아요 (팔 휘두르기만 몸을 돌려요)
        protected override void TickWindup(float dt, bool tracking)
        {
            if (_pattern == Pattern.Arms && tracking) FaceToward(ToPlayer());
        }

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Arms => TelegraphShapes.Lunge(_armsRadius, _armsAngle, 0f),
            _ => TelegraphShapes.Circle(BodyRadius + 0.3f), // 몸 둘레에 기운이 모여요 (실제 판정은 바닥 예고 원)
        };

        protected override void OnAttackStarted()
        {
            if (_pattern != Pattern.Palm) return;
            Vector3 p = Player.transform.position;
            AddMark(p, _palmWindup, _palmRadius, _palmRatio, false);
            for (int i = 0; i < 2; i++)
                AddMark(p + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * _palmSpread, _palmWindup, _palmRadius, _palmRatio, false);
        }

        // ── 판정

        protected override void OnActiveStarted()
        {
            _activeTimer = 0f;
            _wavesDone = 0;
            _swingsDone = 0;
            switch (_pattern)
            {
                case Pattern.Chant: _waveCount = _chantWaves; ChantWave(); break;
                case Pattern.BloodRain: _waveCount = _rainDrops; RainDrop(); break;
                case Pattern.Arms: Swing(); break;
                case Pattern.Summon:
                    Director.Spawn(EnemyKind.Moktak, Director.FindFreeSpot(transform.position + Facing * 2f, 1f));
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            _activeTimer += dt;
            switch (_pattern)
            {
                case Pattern.Chant:
                    while (_wavesDone < _waveCount && _activeTimer >= _wavesDone * _chantGap) ChantWave();
                    break;
                case Pattern.BloodRain:
                    while (_wavesDone < _waveCount && _activeTimer >= _wavesDone * _rainGap) RainDrop();
                    break;
                case Pattern.Arms:
                    while (_swingsDone < 3 && _activeTimer >= _swingsDone * _armsGap) Swing();
                    break;
            }
        }

        void ChantWave()
        {
            _wavesDone++;
            for (int i = 0; i < _chantPerWave; i++) AddMark(RandomInZone(), _chantFall, _chantRadius, _chantRatio, false);
        }

        void RainDrop()
        {
            _wavesDone++;
            Vector2 r = Random.insideUnitCircle * 1.5f;
            AddMark(Player.transform.position + new Vector3(r.x, 0f, r.y), _rainFall, _rainRadius, _rainRatio, true);
        }

        // 좌 → 우 → 좌로 부채꼴 (조금씩 돌려 가며)
        void Swing()
        {
            float side = _swingsDone == 1 ? 35f : -35f;
            _swingsDone++;
            Vector3 dir = Quaternion.Euler(0f, side, 0f) * AimDirection;
            if (HitShapes.InSector(transform.position, dir, _armsRadius, _armsAngle, Player.transform.position, PlayerRadius))
                HitPlayer(_armsRatio);
        }

        Vector3 RandomInZone()
        {
            float hx = _zoneSize.x * 0.5f - 1f, hz = _zoneSize.y * 0.5f - 1f;
            return _zoneCenter + new Vector3(Random.Range(-hx, hx), 0f, Random.Range(-hz, hz));
        }

        // ── 바닥 예고 원 (손바닥 · 염불 · 핏방울): 시간이 다 되면 그 자리에 떨어져요

        void AddMark(Vector3 at, float fall, float radius, float ratio, bool bleed)
        {
            at.y = 0f;
            var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
            marker.Show(TelegraphShapes.Circle(radius), true);
            marker.SetPose(at, Vector3.forward);
            marker.SetProgress(0f);
            _marks.Add(new Mark { At = at, Timer = fall, Total = fall, Radius = radius, Ratio = ratio, Bleed = bleed, Marker = marker });
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
                if (d.magnitude > m.Radius + PlayerRadius) continue;
                if (Player.TryReceiveHit(AttackPower * m.Ratio, m.At, -1f, Body) && m.Bleed) Player.AddBurnStack(_bleedDps); // 출혈
            }
        }

        bool HitPlayer(float ratio) => Player.TryReceiveHit(AttackPower * ratio, transform.position, HitKnockback, Body);

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
            Director.SpawnBurst(transform.position, 3f); // 불상이 갈라져요
        }

        void EnterPhase2()
        {
            Phase = 2;
            Body.Targetable = true;
            _decisionTimer = _decisionPause;
            _repeat = 0;
            _chantTimer = _chantInterval * 0.5f;
            var visual = transform.Find("Visual");
            if (visual != null) visual.localScale = Vector3.one * 1.15f; // 갈라져 커진 불상
        }

        protected override void OnDied()
        {
            foreach (var m in _marks) if (m.Marker != null) Destroy(m.Marker.gameObject);
            _marks.Clear();
        }
    }
}
