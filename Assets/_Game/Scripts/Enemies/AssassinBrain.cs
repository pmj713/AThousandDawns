using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 마교 살수 (몬스터 탭 119~135행, 죽림 기준): 추적도가 쌓이면 층을 쫓아오는 사냥꾼.
    // 항상 경직 저항: 3번 맞을 때마다 1번만 경직.
    // 쌍검 2연격: 가까울 때. 예고 0.4, 앞 부채꼴 1.5칸 90° × 2번 (0.25초 간격), 피해 ×0.7, 빈틈 0.6
    // 표창 3연발: 3초마다. 예고 0.6 (조준선 3개, 옆걸음), 30° 간격, 초당 12칸, 사거리 9칸, 1발 ×0.6, 던지는 순간 0.2초 멈춤, 빈틈 0.4
    // 은신 습격: 6초마다. 1초 동안 사라졌다가 (맞지 않음) 플레이어 뒤에 나타나 부채꼴 1.5칸 베기 ×1.2. 나타나기 0.6초 전 바닥에 그림자. 빈틈 0.8
    // 연막: 체력 50% 이하부터 12초마다. 예고 0.5, 반경 2.5칸 연기 4초. 연기 안에서 위치를 2번 바꾸고, 걷히기 전 다음 공격은 은신 습격.
    //   (반투명해지는 연출은 아직 없어요. 연기는 바닥 원으로만 보여요)
    public sealed class AssassinBrain : EnemyBrain
    {
        enum Pattern { Twin, Shuriken, Ambush, Smoke }

        Pattern _pattern;
        int _hits, _staggerEvery, _slashIndex, _blinksLeft;
        float _twinRange, _twinAngle, _twinGap, _twinRatio, _twinWindup, _twinRecovery;
        float _shurikenWindup, _shurikenRecovery, _shurikenRatio, _shurikenSpread, _shurikenSpeed, _shurikenRange, _shurikenInterval, _shurikenPause, _arrowRadius;
        float _ambushVanish, _ambushShadow, _ambushRecovery, _ambushRatio, _ambushRange, _ambushInterval;
        float _smokeWindup, _smokeRadius, _smokeLife, _smokeHp, _smokeInterval;
        float _shurikenTimer, _ambushTimer, _smokeTimer, _smokeLeft, _blinkTimer, _activeTimer;
        bool _forceAmbush, _shadowShown, _hitPlayer;
        Vector3 _aimPoint, _ambushPoint, _smokeCenter;
        GameObject _smoke;
        readonly Telegraph[] _sideLines = new Telegraph[2];

        public override EnemyKind Kind => EnemyKind.Assassin;
        protected override string Key => "assassin";
        public override EnemyRank Rank => EnemyRank.Assassin;
        protected override bool IsRanged => _pattern == Pattern.Shuriken;
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Twin => _twinGap + 0.05f,
            Pattern.Shuriken => _shurikenPause,
            Pattern.Ambush => 0.1f,
            _ => 0.05f,
        };

        // 3번째 맞을 때만 경직 (_hits는 경직을 일으키는 타격만 세요)
        protected override bool SuperArmor => (_hits + 1) % Mathf.Max(1, _staggerEvery) != 0;

        public bool IsSmoking => _smokeLeft > 0f;
        public Vector3 SmokeCenter => _smokeCenter;

        protected override void ReadTuning()
        {
            _staggerEvery = Mathf.RoundToInt(Tuning("stagger_every", 3f));
            _twinRange = Tuning("twin_range", 1.5f);
            _twinAngle = Tuning("twin_angle", 90f);
            _twinGap = Tuning("twin_gap", 0.25f);
            _twinRatio = Tuning("twin_ratio", 0.7f);
            _twinWindup = Tuning("twin_windup", 0.4f);
            _twinRecovery = Tuning("twin_recovery", 0.6f);
            _shurikenWindup = Tuning("shuriken_windup", 0.6f);
            _shurikenRecovery = Tuning("shuriken_recovery", 0.4f);
            _shurikenRatio = Tuning("shuriken_ratio", 0.6f);
            _shurikenSpread = Tuning("shuriken_spread", 30f);
            _shurikenSpeed = Tuning("shuriken_speed", 12f);
            _shurikenRange = Tuning("shuriken_range", 9f);
            _shurikenInterval = Tuning("shuriken_interval", 3f);
            _shurikenPause = Tuning("shuriken_pause", 0.2f);
            _arrowRadius = Core.GameConfig.Get("archer.arrow_radius", 0.2f);
            _ambushVanish = Tuning("ambush_vanish", 1f);
            _ambushShadow = Tuning("ambush_windup", 0.6f);
            _ambushRecovery = Tuning("ambush_recovery", 0.8f);
            _ambushRatio = Tuning("ambush_ratio", 1.2f);
            _ambushRange = Tuning("ambush_range", 1.5f);
            _ambushInterval = Tuning("ambush_interval", 6f);
            _smokeWindup = Tuning("smoke_windup", 0.5f);
            _smokeRadius = Tuning("smoke_radius", 2.5f);
            _smokeLife = Tuning("smoke_life", 4f);
            _smokeHp = Tuning("smoke_hp", 0.5f);
            _smokeInterval = Tuning("smoke_interval", 12f);
            AttackRange = _twinRange;
            _shurikenTimer = _shurikenInterval * 0.5f;
            _ambushTimer = _ambushInterval;
        }

        protected override void OnInitialized()
        {
            for (int i = 0; i < _sideLines.Length; i++)
                _sideLines[i] = Telegraph.Create(transform, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
        }

        protected override void TickAlways(float dt)
        {
            if (_shurikenTimer > 0f) _shurikenTimer -= dt;
            if (_ambushTimer > 0f) _ambushTimer -= dt;
            if (_smokeTimer > 0f) _smokeTimer -= dt;
            if (_smokeLeft <= 0f) return;
            _smokeLeft -= dt;
            _blinkTimer -= dt;
            // 연기 안에서 위치를 바꿔요 (2번)
            if (_blinksLeft > 0 && _blinkTimer <= 0f && !IsHidden && AttackPhase == EnemyAttackTimeline.Phase.Ready)
            {
                _blinksLeft--;
                _blinkTimer = _smokeLife / 3f;
                Teleport(Director.FindFreeSpot(_smokeCenter, _smokeRadius * Random.Range(0.3f, 0.8f)));
                FaceToward(ToPlayer());
            }
            if (_smokeLeft <= 0f)
            {
                _forceAmbush = false;
                if (_smoke != null) Destroy(_smoke);
            }
        }

        protected override void OnHitTaken()
        {
            if (Body.LastHitReacts) _hits++;
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            bool lowHp = Body.Health <= Body.MaxHealth * _smokeHp;
            if (lowHp && _smokeTimer <= 0f && !IsSmoking) return Choose(Pattern.Smoke);
            if (_forceAmbush || _ambushTimer <= 0f) return Choose(Pattern.Ambush);
            if (_shurikenTimer <= 0f && distance >= 3f && distance <= _shurikenRange && HasClearLine(distance, _arrowRadius, 1f)) return Choose(Pattern.Shuriken);
            if (distance <= _twinRange + 0.4f) return Choose(Pattern.Twin);
            return false;
        }

        bool Choose(Pattern pattern)
        {
            _pattern = pattern;
            switch (pattern)
            {
                case Pattern.Twin: WindupTime = _twinWindup; RecoveryTime = _twinRecovery; break;
                case Pattern.Shuriken: WindupTime = _shurikenWindup; RecoveryTime = _shurikenRecovery; _shurikenTimer = _shurikenInterval; break;
                case Pattern.Ambush: WindupTime = _ambushVanish; RecoveryTime = _ambushRecovery; _ambushTimer = _ambushInterval; _forceAmbush = false; break;
                case Pattern.Smoke: WindupTime = _smokeWindup; RecoveryTime = 0f; _smokeTimer = _smokeInterval; break;
            }
            return true;
        }

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            // 표창이 준비되면 거리를 벌리고, 아니면 붙어요. 공격할 수 없을 땐 옆걸음.
            if (!canAttack) Move(Strafe(dir) * (MoveSpeed * 0.6f), dt);
            else if (_shurikenTimer <= 0f && d < 3.5f) Move(-dir * MoveSpeed + Strafe(dir) * MoveSpeed * 0.5f, dt);
            else Move(dir * MoveSpeed, dt);
        }

        // ── 예고

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Twin => TelegraphShapes.Lunge(_twinRange, _twinAngle, 0f),
            Pattern.Shuriken => TelegraphShapes.Rect(_arrowRadius * 2f, _shurikenRange),
            Pattern.Ambush => TelegraphShapes.Lunge(_ambushRange, _twinAngle, 0f),
            _ => TelegraphShapes.Circle(_smokeRadius),
        };

        protected override bool RadialTelegraph => _pattern == Pattern.Smoke;

        protected override void OnAttackStarted()
        {
            _aimPoint = Player.transform.position;
            _shadowShown = false;
            if (_pattern == Pattern.Ambush) SetHidden(true); // 사라짐
            if (_pattern == Pattern.Shuriken)
                foreach (var line in _sideLines) line.Show(TelegraphShapes.Rect(_arrowRadius * 2f, _shurikenRange));
        }

        protected override void TickWindup(float dt, bool tracking)
        {
            switch (_pattern)
            {
                case Pattern.Shuriken:
                    if (tracking) _aimPoint = Player.transform.position;
                    Move(Strafe(ToPlayer().normalized) * (MoveSpeed * 0.4f), dt);
                    Aim(_aimPoint);
                    break;
                case Pattern.Twin:
                    if (tracking) Aim(Player.transform.position);
                    break;
                case Pattern.Ambush:
                    // 나타나기 0.6초 전에 그림자 (플레이어 뒤, 그 순간 자리에 고정)
                    float elapsed = Attack.WindupProgress * _ambushVanish;
                    if (!_shadowShown && elapsed >= _ambushVanish - _ambushShadow)
                    {
                        _shadowShown = true;
                        Vector3 p = Player.transform.position;
                        Vector3 back = p - transform.position;
                        back.y = 0f;
                        back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.forward;
                        _ambushPoint = Director.FindFreeSpot(p + back * 1.2f, 0.01f);
                        AimDirection = (p - _ambushPoint).sqrMagnitude > 1e-4f ? Flat(p - _ambushPoint).normalized : -back;
                        Telegraph.Show(TelegraphShape(), false);
                    }
                    break;
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        void Aim(Vector3 point)
        {
            Vector3 to = Flat(point - transform.position);
            if (to.sqrMagnitude > 1e-4f) AimDirection = to.normalized;
            FaceToward(AimDirection);
        }

        protected override void UpdateTelegraph()
        {
            if (_pattern == Pattern.Ambush)
            {
                if (_shadowShown) Telegraph.SetPose(_ambushPoint, AimDirection);
                else Telegraph.Hide();
                return;
            }
            if (_pattern == Pattern.Smoke)
            {
                Telegraph.SetPose(transform.position, Vector3.forward);
                return;
            }
            base.UpdateTelegraph();
            if (_pattern != Pattern.Shuriken) return;
            for (int i = 0; i < _sideLines.Length; i++)
            {
                var dir = Quaternion.Euler(0f, i == 0 ? -_shurikenSpread : _shurikenSpread, 0f) * AimDirection;
                _sideLines[i].SetPose(transform.position, dir);
                _sideLines[i].SetProgress(Attack.WindupProgress);
            }
        }

        // ── 판정

        protected override void OnActiveStarted()
        {
            foreach (var line in _sideLines) line.Hide();
            _slashIndex = 0;
            _activeTimer = 0f;
            _hitPlayer = false;
            switch (_pattern)
            {
                case Pattern.Twin: Slash(_twinRange, _twinRatio); break;
                case Pattern.Shuriken:
                    Aim(_aimPoint);
                    Vector3 origin = transform.position + Vector3.up;
                    for (int i = -1; i <= 1; i++)
                        Director.SpawnArrow(origin, Quaternion.Euler(0f, i * _shurikenSpread, 0f) * AimDirection, _shurikenSpeed, _shurikenRange, _arrowRadius, AttackPower * _shurikenRatio, Body);
                    break;
                case Pattern.Ambush:
                    Teleport(_shadowShown ? _ambushPoint : transform.position);
                    SetHidden(false);
                    FaceToward(AimDirection);
                    Slash(_ambushRange, _ambushRatio);
                    break;
                case Pattern.Smoke:
                    _smokeCenter = transform.position;
                    _smokeLeft = _smokeLife;
                    _blinksLeft = 2;
                    _blinkTimer = _smokeLife / 3f;
                    _forceAmbush = true;
                    BuildSmoke();
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            if (_pattern != Pattern.Twin) return;
            _activeTimer += dt;
            if (_slashIndex == 0 && _activeTimer >= _twinGap)
            {
                _slashIndex = 1;
                _hitPlayer = false;
                Slash(_twinRange, _twinRatio);
            }
        }

        void Slash(float range, float ratio)
        {
            if (_hitPlayer) return;
            if (!HitShapes.InSector(transform.position, AimDirection, range, _twinAngle, Player.transform.position, PlayerRadius)) return;
            _hitPlayer = Player.TryReceiveHit(AttackPower * ratio, transform.position, HitKnockback, Body);
        }

        void BuildSmoke()
        {
            if (_smoke != null) Destroy(_smoke);
            _smoke = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _smoke.name = "Smoke (연막)";
            EnemyFactory.StripCollider(_smoke);
            _smoke.transform.position = new Vector3(_smokeCenter.x, 0.04f, _smokeCenter.z);
            _smoke.transform.localScale = new Vector3(_smokeRadius * 2f, 0.03f, _smokeRadius * 2f);
            var renderer = _smoke.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterial.Unlit(ref _smokeMat, new Color(0.35f, 0.35f, 0.38f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material _smokeMat;

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            foreach (var line in _sideLines) line.Hide();
            if (IsHidden) SetHidden(false); // 사라지는 중에 얼면 그 자리에 드러나요
        }

        protected override void OnDied()
        {
            if (_smoke != null) Destroy(_smoke);
        }
    }
}
