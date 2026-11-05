using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    public enum EnemyKind { Grunt, Boar, Archer }

    // 일반 적 공통: 첫 공격 대기, 공격 토큰, 공격 간격, 예고 절반 규칙, 경직. 종류별 움직임과 공격은 하위 클래스가 정해요.
    [RequireComponent(typeof(CharacterController), typeof(Damageable))]
    public abstract class EnemyBrain : MonoBehaviour
    {
        protected Damageable Body;
        protected CharacterController Controller;
        protected EnemyDirector Director;
        protected PlayerController Player;
        protected Telegraph Telegraph;
        protected readonly EnemyAttackTimeline Attack = new EnemyAttackTimeline();

        protected float MoveSpeed, AttackPower, AttackRange, WindupTime, RecoveryTime, AttackInterval, TrackMoveRatio;
        protected Vector3 Facing = Vector3.back;
        protected Vector3 AimDirection = Vector3.back;
        protected float StrafeSign = 1f;

        Transform _visual;
        HitReaction _reaction;
        AttackTokenPool _tokens;
        float _cooldown, _stunTimer, _strafeFlipTimer;
        bool _initialized, _dead;

        public abstract EnemyKind Kind { get; }
        protected abstract string Key { get; }
        protected virtual bool IsRanged => false;
        protected abstract float ActiveTime { get; }
        protected virtual bool SuperArmor => false;

        public string DisplayName { get; private set; }
        public Damageable Damageable => Body;
        public CharacterController BodyCollider => Controller;
        public float Height => Controller != null ? Controller.height : 1.6f;
        public EnemyAttackTimeline.Phase AttackPhase => Attack.Current;
        public bool HasToken => _tokens != null && _tokens.Holds(this);
        public bool IsStunned => _stunTimer > 0f;
        public bool IsDead => _dead;
        public bool TelegraphVisible => Telegraph != null && Telegraph.IsVisible;

        protected float PlayerRadius => Player != null ? Player.BodyRadius : 0.3f;
        protected float BodyRadius => Body.BodyRadius;

        public void Init(EnemyDirector director, string displayName, Transform visual, Renderer flashRenderer, Telegraph telegraph)
        {
            Body = GetComponent<Damageable>();
            Controller = GetComponent<CharacterController>();
            Director = director;
            Player = director.Player;
            DisplayName = displayName;
            _visual = visual;
            Telegraph = telegraph;

            MoveSpeed = Tuning("move_speed", 3f);
            AttackPower = Tuning("atk", 8f);
            AttackRange = Tuning("attack_range", 1.2f);
            WindupTime = Tuning("windup", 0.6f);
            RecoveryTime = Tuning("recovery", 0.6f);
            AttackInterval = Tuning("attack_interval", 2f);
            TrackMoveRatio = GameConfig.Get("enemy.track_move_ratio", 0.5f);
            ReadTuning();

            Body.Configure(Tuning("hp", 30f), Tuning("toughness", 0f), 0f, Controller.radius, flashRenderer);
            Body.Damaged += OnDamaged;

            _reaction = new HitReaction(
                GameConfig.Get("enemy.hitstun", 0.2f),
                GameConfig.Get("enemy.hitstun_immunity", 0.4f),
                GameConfig.Get("enemy.windup_delay_on_hit", 0.2f));
            _tokens = IsRanged ? director.RangedTokens : director.MeleeTokens;
            _cooldown = GameConfig.Get("enemy.first_attack_delay", 1f);
            StrafeSign = Random.value < 0.5f ? -1f : 1f;
            _strafeFlipTimer = Random.Range(1.5f, 3f);

            Attack.ActiveStarted += () =>
            {
                Telegraph.Hide();
                OnActiveStarted();
            };
            Attack.Finished += () =>
            {
                _tokens.Release(this);
                OnAttackFinished();
            };

            if (Player != null) FaceToward(ToPlayer());
            _initialized = true;
        }

        protected float Tuning(string field, float fallback) => GameConfig.Get($"{Key}.{field}", fallback);

        protected virtual void ReadTuning() { }

        void Update()
        {
            if (!_initialized || _dead) return;
            float dt = Time.deltaTime;
            _reaction.Tick(dt);
            if (_cooldown > 0f) _cooldown -= dt;
            TickStrafeFlip(dt);

            if (Player == null || Player.IsDead)
            {
                if (Attack.IsAttacking) CancelAttack();
                return;
            }

            if (_stunTimer > 0f)
            {
                _stunTimer -= dt;
                return;
            }

            switch (Attack.Current)
            {
                case EnemyAttackTimeline.Phase.Ready:
                    bool canAttack = _cooldown <= 0f && (_tokens.HasFree || HasToken);
                    TickMovement(dt, canAttack);
                    if (canAttack && WantsToAttack(ToPlayer().magnitude) && _tokens.TryAcquire(this)) BeginAttack();
                    break;
                case EnemyAttackTimeline.Phase.Windup:
                    if (!Attack.IsPaused) TickWindup(dt, Attack.IsTracking);
                    UpdateTelegraph();
                    break;
                case EnemyAttackTimeline.Phase.Active:
                    TickActive(dt);
                    break;
            }

            Attack.Tick(dt);
            if (Attack.Current == EnemyAttackTimeline.Phase.Windup) Telegraph.SetProgress(Attack.WindupProgress);
        }

        void BeginAttack()
        {
            _cooldown = AttackInterval;
            Vector3 to = ToPlayer();
            AimDirection = to.sqrMagnitude > 1e-4f ? to.normalized : Facing;
            FaceToward(AimDirection);
            Attack.Start(WindupTime, ActiveTime, RecoveryTime);
            OnAttackStarted();
            Telegraph.Show(TelegraphShape());
            UpdateTelegraph();
        }

        void CancelAttack()
        {
            Attack.Cancel();
            Telegraph.Hide();
            _tokens.Release(this);
        }

        // ── 하위 클래스가 정하는 부분

        // 공격하지 않을 때 움직임. canAttack이면 토큰과 공격 간격이 준비된 상태예요.
        protected abstract void TickMovement(float dt, bool canAttack);
        protected abstract bool WantsToAttack(float distanceToPlayer);
        protected abstract List<Vector2> TelegraphShape();
        protected abstract void TickActive(float dt);
        protected virtual void OnAttackStarted() { }
        protected virtual void OnActiveStarted() { }
        protected virtual void OnAttackFinished() { }

        // 예고 앞 절반: 플레이어 쪽으로 방향을 맞추며 이동속도 50%로 따라와요. 뒤 절반은 멈춰서 고정.
        protected virtual void TickWindup(float dt, bool tracking)
        {
            if (!tracking) return;
            Vector3 to = ToPlayer();
            if (to.sqrMagnitude > 1e-4f) AimDirection = to.normalized;
            FaceToward(AimDirection);
            if (to.magnitude > TrackStopDistance) Move(AimDirection * (MoveSpeed * TrackMoveRatio), dt);
        }

        protected virtual float TrackStopDistance => AttackRange;

        protected virtual void UpdateTelegraph() => Telegraph.SetPose(transform.position, AimDirection);

        // ── 공용 도우미

        protected Vector3 ToPlayer()
        {
            Vector3 d = Player.transform.position - transform.position;
            d.y = 0f;
            return d;
        }

        protected Vector3 Strafe(Vector3 toPlayerDir) => Vector3.Cross(Vector3.up, toPlayerDir) * StrafeSign;

        protected void FaceToward(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;
            Facing = direction.normalized;
            if (_visual != null) _visual.rotation = Quaternion.LookRotation(Facing);
        }

        // 다른 적과 겹치지 않게 살짝 밀어내면서 움직여요. 벽에 막히면 옆걸음 방향을 바꿔요.
        protected CollisionFlags Move(Vector3 velocity, float dt)
        {
            velocity += Separation() * MoveSpeed;
            velocity.y = 0f;
            var flags = Controller.Move(velocity * dt);
            if ((flags & CollisionFlags.Sides) != 0) StrafeSign = -StrafeSign;
            return flags;
        }

        // 분리 없이 그대로 움직여요 (돌진·전진 베기).
        protected CollisionFlags MoveRaw(Vector3 displacement)
        {
            displacement.y = 0f;
            return Controller.Move(displacement);
        }

        protected bool HasClearLine(float distance, float radius, float height)
        {
            Vector3 to = ToPlayer();
            if (to.sqrMagnitude < 1e-4f) return true;
            float free = ObstacleProbe.Distance(transform.position + Vector3.up * height, to.normalized, radius, distance);
            return free >= distance - 0.05f;
        }

        protected bool TryHitPlayer() => Player.TryReceiveHit(AttackPower, transform.position);

        Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            foreach (var other in Director.Enemies)
            {
                if (other == this || other == null) continue;
                Vector3 d = transform.position - other.transform.position;
                d.y = 0f;
                float min = BodyRadius + other.BodyRadius + 0.3f;
                float dist = d.magnitude;
                if (dist >= min) continue;
                push += dist > 1e-3f ? d / dist * (1f - dist / min) : Random.insideUnitSphere;
            }
            // 플레이어 몸 안으로는 걸어 들어가지 않아요 (돌진·전진 베기는 MoveRaw라 예외)
            Vector3 fromPlayer = -ToPlayer();
            float pd = fromPlayer.magnitude;
            float pmin = BodyRadius + PlayerRadius + 0.2f;
            if (pd < pmin && pd > 1e-3f) push += fromPlayer / pd * (1f - pd / pmin) * 2f;
            push.y = 0f;
            return push;
        }

        void TickStrafeFlip(float dt)
        {
            _strafeFlipTimer -= dt;
            if (_strafeFlipTimer > 0f) return;
            _strafeFlipTimer = Random.Range(1.5f, 3f);
            StrafeSign = -StrafeSign;
        }

        void OnDamaged(Damageable _, int amount)
        {
            if (_dead) return;
            if (Body.IsDead)
            {
                Die();
                return;
            }

            switch (_reaction.OnHit(Attack.Current, SuperArmor))
            {
                case HitResponse.Stun:
                    _stunTimer = _reaction.Hitstun;
                    break;
                case HitResponse.DelayWindup:
                    Attack.Delay(_reaction.WindupDelay);
                    break;
                case HitResponse.ExtendRecovery:
                    Attack.ExtendRecovery(_reaction.Hitstun);
                    break;
            }
        }

        void Die()
        {
            _dead = true;
            CancelAttack();
            Director.Unregister(this);
            Controller.enabled = false;
            if (_visual != null) _visual.localScale = new Vector3(1.1f, 0.3f, 1.1f);
            Destroy(gameObject, 0.25f);
        }

        void OnDestroy()
        {
            if (Body != null) Body.Damaged -= OnDamaged;
            if (_tokens != null) _tokens.Release(this);
            if (Director != null) Director.Unregister(this);
        }
    }
}
