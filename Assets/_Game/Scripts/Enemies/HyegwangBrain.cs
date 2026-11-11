using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 파계승 혜광 (폐사찰 고수, 몬스터 시트 63행): 체력 1100 · 공격 18 (폐사찰 배율 전). 결투장은 사방 돌벽.
    // 고수 공통: 경직·넉백 없음, 빙결 1초 + 패턴 취소, 체력 50%에서 1.5초 전환 (무적), 같은 패턴은 연속 2번까지.
    // 선장 휘두르기: 가까울 때. 예고 0.7, 앞 부채꼴 반경 3칸 150°, ×1.0, 빈틈 0.8
    // 금강불괴: 9초마다 (2페이즈 6초). 3초 동안 서서 버티며 맞아도 1씩만 (원형 예고가 차올라요) → 끝에 반경 3.5칸 충격파 ×1.2 + 넉백 2칸
    // 염주 던지기: 멀 때. 예고 0.6, 염주가 8칸 날아갔다가 돌아와요 (가는 길·오는 길 한 번씩 맞음) ×0.7. 2페이즈는 세 갈래
    // 2페이즈: 예고가 20% 빨라져요
    public sealed class HyegwangBrain : EnemyBrain, IMasterBrain
    {
        static readonly Color Gold = new Color(0.95f, 0.78f, 0.3f);

        public override string AttackName => _pattern switch { Pattern.Sweep => "선장 휘두르기", Pattern.Vajra => "금강불괴", Pattern.Beads => "염주", _ => null };

        enum Pattern { None, Sweep, Vajra, Beads }

        Pattern _pattern, _lastPattern;
        int _repeat, _maxRepeat;
        float _closeDistance, _farDistance, _decisionPause, _transitionTime, _phase2Ratio, _phase2Speed;
        float _sweepWindup, _sweepRecovery, _sweepRatio, _sweepRadius, _sweepAngle;
        float _vajraTime, _vajraRecovery, _vajraRatio, _vajraRadius, _vajraKnockback, _vajraInterval, _vajraInterval2;
        float _beadWindup, _beadRecovery, _beadRatio, _beadRange, _beadSpeed, _beadSpread;
        float _decisionTimer, _transitionTimer, _vajraTimer;

        public override EnemyKind Kind => EnemyKind.Hyegwang;
        protected override string Key => "hyegwang";
        protected override bool UsesToken => false;
        protected override bool SuperArmor => true;
        protected override bool IsBoss => true;
        public override EnemyRank Rank => EnemyRank.Master;
        protected override bool FreezeBlocked => IsTransitioning;
        protected override float DeathDelay => 1f;
        protected override bool RadialTelegraph => _pattern == Pattern.Vajra;
        protected override float ActiveTime => 0.1f;

        public int Phase { get; private set; } = 1;
        public bool IsTransitioning => _transitionTimer > 0f;
        public bool IsVajra => _pattern == Pattern.Vajra && Attack.Current == EnemyAttackTimeline.Phase.Windup;

        public void SetZone(Vector3 center, Vector2 size) { } // 염주는 벽을 지나가요

        protected override void ReadTuning()
        {
            AttackInterval = 0f;
            _closeDistance = Tuning("close_distance", 3f);
            _farDistance = Tuning("far_distance", 5f);
            _decisionPause = Tuning("decision_pause", 0.4f);
            _transitionTime = Tuning("transition_time", 1.5f);
            _phase2Ratio = Tuning("phase2_hp_ratio", 0.5f);
            _phase2Speed = Tuning("p2_windup_ratio", 0.8f);
            _maxRepeat = Mathf.RoundToInt(Tuning("max_repeat", 2f));
            _sweepWindup = Tuning("sweep_windup", 0.7f);
            _sweepRecovery = Tuning("sweep_recovery", 0.8f);
            _sweepRatio = Tuning("sweep_ratio", 1f);
            _sweepRadius = Tuning("sweep_radius", 3f);
            _sweepAngle = Tuning("sweep_angle", 150f);
            _vajraTime = Tuning("vajra_time", 3f);
            _vajraRecovery = Tuning("vajra_recovery", 1f);
            _vajraRatio = Tuning("vajra_ratio", 1.2f);
            _vajraRadius = Tuning("vajra_radius", 3.5f);
            _vajraKnockback = Tuning("vajra_knockback", 2f);
            _vajraInterval = Tuning("vajra_interval", 9f);
            _vajraInterval2 = Tuning("p2_vajra_interval", 6f);
            _beadWindup = Tuning("bead_windup", 0.6f);
            _beadRecovery = Tuning("bead_recovery", 0.6f);
            _beadRatio = Tuning("bead_ratio", 0.7f);
            _beadRange = Tuning("bead_range", 8f);
            _beadSpeed = Tuning("bead_speed", 10f);
            _beadSpread = Tuning("p2_bead_spread", 25f);
            _vajraTimer = _vajraInterval * 0.5f;
        }

        protected override void OnInitialized()
        {
            Body.SituationalResolver = (attacker, tag) => IsVajra ? 0f : 1f; // 금강불괴: 0이어도 최소 1은 들어가요
        }

        protected override void TickAlways(float dt)
        {
            if (!IsAwake) return;
            if (_decisionTimer > 0f) _decisionTimer -= dt;
            if (_vajraTimer > 0f) _vajraTimer -= dt;
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
            if (_vajraTimer <= 0f && distance <= _vajraRadius + 1f) next = Pattern.Vajra;
            else if (distance <= _closeDistance) next = Pattern.Sweep;
            else if (distance >= _farDistance) next = Pattern.Beads;
            else return false; // 중간 거리면 다가가요
            if (next == _lastPattern && _repeat >= _maxRepeat)
            {
                if (next == Pattern.Beads) return false;
                next = next == Pattern.Sweep ? Pattern.Beads : Pattern.Sweep;
            }

            _repeat = next == _lastPattern ? _repeat + 1 : 1;
            _lastPattern = next;
            _pattern = next;
            float speed = Phase == 2 ? _phase2Speed : 1f;
            switch (next)
            {
                case Pattern.Sweep: WindupTime = _sweepWindup * speed; RecoveryTime = _sweepRecovery; break;
                case Pattern.Vajra:
                    WindupTime = _vajraTime; RecoveryTime = _vajraRecovery;
                    _vajraTimer = Phase == 1 ? _vajraInterval : _vajraInterval2;
                    Body.SetTint(Gold);
                    break;
                case Pattern.Beads: WindupTime = _beadWindup * speed; RecoveryTime = _beadRecovery; break;
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

        // 금강불괴는 버티고 서 있어요
        protected override void TickWindup(float dt, bool tracking)
        {
            if (_pattern == Pattern.Vajra)
            {
                FaceToward(ToPlayer());
                return;
            }
            base.TickWindup(dt, tracking);
        }

        // ── 예고

        protected override List<Vector2> TelegraphShape() => _pattern switch
        {
            Pattern.Sweep => TelegraphShapes.Lunge(_sweepRadius, _sweepAngle, 0f),
            Pattern.Vajra => TelegraphShapes.Circle(_vajraRadius),
            Pattern.Beads => TelegraphShapes.Rect(0.6f, _beadRange),
            _ => TelegraphShapes.Circle(0.5f),
        };

        // ── 판정

        protected override void OnActiveStarted()
        {
            switch (_pattern)
            {
                case Pattern.Sweep:
                    if (HitShapes.InSector(transform.position, AimDirection, _sweepRadius, _sweepAngle, Player.transform.position, PlayerRadius))
                        HitPlayer(_sweepRatio, HitKnockback);
                    break;
                case Pattern.Vajra:
                    Body.SetTint(null);
                    Director.SpawnBurst(transform.position, _vajraRadius);
                    if (ToPlayer().magnitude <= _vajraRadius + PlayerRadius) HitPlayer(_vajraRatio, _vajraKnockback);
                    break;
                case Pattern.Beads:
                    int count = Phase == 1 ? 1 : 3;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, (i - (count - 1) * 0.5f) * _beadSpread, 0f) * AimDirection;
                        ThrowBead(dir);
                    }
                    break;
            }
        }

        protected override void TickActive(float dt) { }

        void ThrowBead(Vector3 direction)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "염주";
            EnemyFactory.StripCollider(go);
            go.transform.position = transform.position + Vector3.up;
            go.transform.localScale = Vector3.one * 0.5f;
            var renderer = go.GetComponent<Renderer>();
            renderer.material.color = new Color(0.45f, 0.25f, 0.15f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<BeadThrow>().Launch(Player, transform, direction, _beadSpeed, _beadRange, 0.35f, AttackPower * _beadRatio, Body);
        }

        bool HitPlayer(float ratio, float knockback) => Player.TryReceiveHit(AttackPower * ratio, transform.position, knockback, Body);

        protected override void OnAttackFinished() => _decisionTimer = _decisionPause;

        protected override void OnFreezeStarted(bool cancelledAttack)
        {
            Body.SetTint(null);
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
            Body.SetTint(null);
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

    // 혜광의 염주: 곧게 range만큼 날아갔다가 던진 사람에게 돌아와요. 가는 길·오는 길에 한 번씩 맞을 수 있어요. 벽은 지나가요.
    public sealed class BeadThrow : MonoBehaviour
    {
        PlayerController _target;
        Transform _owner;
        Damageable _ownerBody;
        Vector3 _direction;
        float _speed, _left, _radius, _attack;
        bool _returning, _hitOut, _hitBack;

        public void Launch(PlayerController target, Transform owner, Vector3 direction, float speed, float range, float radius, float attack, Damageable ownerBody)
        {
            _target = target;
            _owner = owner;
            _ownerBody = ownerBody;
            _direction = new Vector3(direction.x, 0f, direction.z).normalized;
            _speed = speed;
            _left = range;
            _radius = radius;
            _attack = attack;
        }

        void Update()
        {
            if (_target == null || _owner == null)
            {
                Destroy(gameObject);
                return;
            }
            float step = _speed * Time.deltaTime;
            if (!_returning)
            {
                transform.position += _direction * step;
                _left -= step;
                if (_left <= 0f) _returning = true;
            }
            else
            {
                Vector3 home = _owner.position + Vector3.up - transform.position;
                if (home.magnitude <= step + 0.3f)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.position += home.normalized * step;
            }

            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > _radius + _target.BodyRadius) return;
            if (_returning ? _hitBack : _hitOut) return;
            _target.TryReceiveHit(_attack, transform.position, -1f, _ownerBody); // 대시 무적이면 그냥 지나가요 (그 길은 끝)
            if (_returning) _hitBack = true;
            else _hitOut = true;
        }
    }
}
