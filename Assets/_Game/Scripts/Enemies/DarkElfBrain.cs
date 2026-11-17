using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 다크 엘프 검사 (차원 겹침, 판타지 고수): 고수 공통 규칙 (경직·넉백 없음, 빙결 1초 + 패턴 취소, 50%에서 1.5초 전환, 같은 패턴 연속 2번까지).
    // 혈영검마와 겹치지 않게 저만의 패턴 (잔상 · 혈선 · 순간이동 없음, 흡혈 없음):
    //   쌍검 난무 (가까울 때): 예고 0.5 · 좌우 번갈아 부채꼴 4번 (2페이즈 5번), 한 번에 조금 전진, 맞을 때마다 독 (darkelf.poison_*)
    //   그림자 숨기 (darkelf.hide_interval마다, 또는 멀리서 버티면): 사라졌다가 플레이어 옆에서 나타나 짧은 예고 뒤 그림자 기습 베기
    //   독 단검 (중간 · 먼 거리): darkelf.dagger_interval마다 단검 한 자루를 던져요
    // 독은 체력을 직접 깎지만 쓰러뜨리지는 않아요 (체력 1에서 멈춤). 2페이즈: 예고 ×darkelf.p2_windup_ratio
    public sealed class DarkElfBrain : EnemyBrain, IMasterBrain
    {
        enum Pattern { None, Dance, Hide, Ambush, Dagger }

        public override EnemyKind Kind => EnemyKind.DarkElf;
        protected override string Key => "darkelf";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Master;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1f;
        public override string VolleyName => "독 단검";
        protected override float ActiveTime => _pattern switch
        {
            Pattern.Dance => _danceGap * (DanceHits - 1) + 0.1f,
            Pattern.Hide => _hideTime,
            _ => 0.1f,
        };

        public override string AttackName => _pattern switch
        {
            Pattern.Dance => "쌍검 난무", Pattern.Hide => "그림자 숨기", Pattern.Ambush => "그림자 기습", Pattern.Dagger => "독 단검", _ => null,
        };

        // 아트 신호: 사라짐 (자리) · 나타남 (자리) · 난무 한 타 (자리, 방향, 왼손이면 true)
        public static event System.Action<DarkElfBrain, Vector3> Vanished;
        public static event System.Action<DarkElfBrain, Vector3> Emerged;
        public static event System.Action<DarkElfBrain, Vector3, Vector3, bool> DanceCut;

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat, _cutsDone;
        float _closeDistance, _farDelay, _decisionPause, _transitionTime, _phase2Ratio, _p2Windup;
        float _danceWindup, _danceRadius, _danceAngle, _danceSkew, _danceGap, _danceLunge, _danceRatio, _danceRecovery;
        float _hideInterval, _hideWindup, _hideTime, _hideSide, _ambushWindup, _ambushRadius, _ambushAngle, _ambushRatio;
        float _daggerInterval, _daggerWindup, _daggerMin, _daggerSpeed, _daggerRange, _daggerRatio;
        float _poisonTime, _poisonTick, _poisonRatio;
        float _decisionTimer, _transitionTimer, _hideTimer, _daggerTimer, _farTimer, _activeTimer;
        float _poisonLeft, _poisonTickTimer;
        bool _ambushNext;

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public bool PlayerPoisoned => _poisonLeft > 0f;
        int DanceHits => Phase >= 2 ? Mathf.RoundToInt(Tuning("p2_dance_hits", 5f)) : Mathf.RoundToInt(Tuning("dance_hits", 4f));

        public void SetZone(Vector3 center, Vector2 size) { }

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 2.6f);
            _farDelay = Tuning("far_delay", 2f);
            _decisionPause = Tuning("decision_pause", 0.35f);
            _transitionTime = Tuning("transition_time", 1.5f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _p2Windup = Tuning("p2_windup_ratio", 0.8f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _danceWindup = Tuning("dance_windup", 0.5f);
            _danceRadius = Tuning("dance_radius", 2.2f);
            _danceAngle = Tuning("dance_angle", 100f);
            _danceSkew = Tuning("dance_skew", 25f);
            _danceGap = Tuning("dance_gap", 0.22f);
            _danceLunge = Tuning("dance_lunge", 0.3f);
            _danceRatio = Tuning("dance_ratio", 0.55f);
            _danceRecovery = Tuning("dance_recovery", 0.7f);
            _hideInterval = Tuning("hide_interval", 9f);
            _hideWindup = Tuning("hide_windup", 0.4f);
            _hideTime = Tuning("hide_time", 0.8f);
            _hideSide = Tuning("hide_side", 2f);
            _ambushWindup = Tuning("ambush_windup", 0.4f);
            _ambushRadius = Tuning("ambush_radius", 2.4f);
            _ambushAngle = Tuning("ambush_angle", 120f);
            _ambushRatio = Tuning("ambush_ratio", 1f);
            _daggerInterval = Tuning("dagger_interval", 4f);
            _daggerWindup = Tuning("dagger_windup", 0.45f);
            _daggerMin = Tuning("dagger_min", 4f);
            _daggerSpeed = Tuning("dagger_speed", 14f);
            _daggerRange = Tuning("dagger_range", 14f);
            _daggerRatio = Tuning("dagger_ratio", 0.6f);
            _poisonTime = Tuning("poison_time", 3f);
            _poisonTick = Mathf.Max(0.1f, Tuning("poison_tick", 0.5f));
            _poisonRatio = Tuning("poison_ratio", 0.08f);
            _hideTimer = _hideInterval * 0.5f;
            _daggerTimer = 1f;
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_hideTimer > 0f) _hideTimer -= dt;
            if (_daggerTimer > 0f) _daggerTimer -= dt;
            _farTimer = ToPlayer().magnitude > _daggerMin ? _farTimer + dt : 0f;
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= dt;
                if (_transitionTimer <= 0f) EnterPhase2();
            }
            TickPoison(dt);
        }

        // ── 고르기

        protected override bool WantsToAttack(float distance)
        {
            if (IsTransitioning || _decisionTimer > 0f) return false;
            Pattern next;
            if (_ambushNext) next = Pattern.Ambush;
            else if (_hideTimer <= 0f || _farTimer >= _farDelay * 2f) next = Pattern.Hide;
            else if (distance <= _closeDistance) next = Pattern.Dance;
            else if (distance >= _daggerMin && _daggerTimer <= 0f) next = Pattern.Dagger;
            else return false; // 다가가요
            if (next == Pattern.Dance && _lastPattern == Pattern.Dance && _repeat >= _maxRepeat)
            {
                if (_hideTimer > _hideInterval * 0.5f) { _repeat = 0; _lastPattern = Pattern.None; _decisionTimer = _decisionPause * 2f; return false; } // 한 번 쉬어요 (쉰 뒤엔 다시)
                next = Pattern.Hide;
            }
            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            float p2 = Phase >= 2 ? _p2Windup : 1f;
            RecoveryTime = Tuning("recovery", 0.5f);
            switch (next)
            {
                case Pattern.Dance: WindupTime = _danceWindup * p2; RecoveryTime = _danceRecovery; break;
                case Pattern.Hide:
                    WindupTime = _hideWindup;
                    RecoveryTime = 0f;
                    _hideTimer = _hideInterval;
                    _farTimer = 0f;
                    break;
                case Pattern.Ambush: WindupTime = _ambushWindup * p2; _ambushNext = false; break;
                case Pattern.Dagger: WindupTime = _daggerWindup * p2; _daggerTimer = _daggerInterval; break;
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
            Pattern.Dance => TelegraphShapes.Lunge(_danceRadius, _danceAngle + _danceSkew * 2f, _danceLunge * 2f),
            Pattern.Ambush => TelegraphShapes.Lunge(_ambushRadius, _ambushAngle, 0f),
            Pattern.Dagger => TelegraphShapes.Rect(0.5f, 4f),
            _ => TelegraphShapes.Circle(BodyRadius + 0.4f), // 그림자 숨기: 몸 둘레 보라 연기
        };

        // ── 판정

        protected override void OnActiveStarted()
        {
            _activeTimer = 0f;
            switch (_pattern)
            {
                case Pattern.Dance:
                    _cutsDone = 0;
                    DanceStrike();
                    break;
                case Pattern.Hide:
                    Vanished?.Invoke(this, transform.position);
                    SetHidden(true);
                    break;
                case Pattern.Ambush:
                    if (SwingHit(transform.position, AimDirection, _ambushRadius, _ambushAngle) && Hit(_ambushRatio)) Poison();
                    break;
                case Pattern.Dagger:
                    Vector3 to = ToPlayer();
                    to.y = 0f;
                    Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : AimDirection;
                    Director.SpawnArrow(transform.position + Vector3.up * 1.1f + dir * (BodyRadius + 0.3f), dir, _daggerSpeed, _daggerRange, 0.35f, AttackPower * _daggerRatio, Body);
                    break;
            }
        }

        protected override void TickActive(float dt)
        {
            _activeTimer += dt;
            if (_pattern == Pattern.Dance)
            {
                if (_cutsDone < DanceHits && _activeTimer >= _danceGap * _cutsDone) DanceStrike();
            }
            else if (_pattern == Pattern.Hide && _activeTimer >= _hideTime - 0.02f && IsHidden) Emerge();
        }

        void DanceStrike()
        {
            bool left = _cutsDone % 2 == 0;
            _cutsDone++;
            Vector3 dir = Quaternion.Euler(0f, left ? -_danceSkew : _danceSkew, 0f) * AimDirection;
            Vector3 to = ToPlayer();
            if (!(Vector3.Dot(to, AimDirection) > 0f && to.magnitude < BodyRadius + PlayerRadius + 0.2f)) MoveRaw(AimDirection * _danceLunge);
            DanceCut?.Invoke(this, transform.position, dir, left);
            if (SwingHit(transform.position, dir, _danceRadius, _danceAngle) && Hit(_danceRatio)) Poison();
        }

        // 플레이어 옆 (왼쪽 · 오른쪽 중 빈 곳)에서 나타나 바로 그림자 기습을 예고해요
        void Emerge()
        {
            Vector3 p = Player.transform.position;
            Vector3 side = new Vector3(Player.Facing.z, 0f, -Player.Facing.x) * (Random.value < 0.5f ? 1f : -1f);
            Teleport(Director.FindFreeSpot(p + side * _hideSide, BodyRadius + 0.1f));
            SetHidden(false);
            Vector3 to = ToPlayer();
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f) AimDirection = to.normalized;
            FaceToward(AimDirection);
            Emerged?.Invoke(this, transform.position);
            _ambushNext = true;
            _decisionTimer = 0f;
            Attack.EndActive(0f);
        }

        bool Hit(float ratio) => Player.TryReceiveHit(AttackPower * ratio, transform.position, HitKnockback, Body);

        // ── 독: 맞을 때마다 시간이 새로 차요 (겹치지 않아요)

        void Poison()
        {
            if (_poisonLeft <= 0f) _poisonTickTimer = _poisonTick;
            _poisonLeft = _poisonTime;
        }

        void TickPoison(float dt)
        {
            if (_poisonLeft <= 0f) return;
            _poisonLeft -= dt;
            _poisonTickTimer -= dt;
            if (_poisonTickTimer > 0f) return;
            _poisonTickTimer += _poisonTick;
            Player.TakeEventDamage(AttackPower * _poisonRatio);
        }

        protected override void OnAttackFinished()
        {
            if (!_ambushNext) _decisionTimer = _decisionPause;
        }

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            if (IsHidden) SetHidden(false); // 사라지는 중에 얼면 그 자리에 드러나요
            _ambushNext = false;
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
            if (IsHidden) SetHidden(false);
            _ambushNext = false;
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

        protected override void OnDied() => _poisonLeft = 0f;
    }
}
