using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    public enum EnemyKind { Grunt, Boar, Archer, Hound, Shield, Horn, Bomber, Snake, Nokrim, Assassin, Qingzhu, IronFist, Wraith, Arhat, Moktak, Monk, Lantern, Hyegwang } // 망령부터 폐사찰 (혜광은 고수)

    // 적 등급 (수련치 탭: 일반 1 · 정예 5 · 살수 5 · 고수 15 · 보스 30)
    public enum EnemyRank { Normal, Elite, Assassin, Master, Boss }

    // 일반 적 공통: 첫 공격 대기, 공격 토큰, 공격 간격, 예고 절반 규칙, 경직. 종류별 움직임과 공격은 하위 클래스가 정해요.
    [RequireComponent(typeof(CharacterController), typeof(Damageable))]
    public abstract partial class EnemyBrain : MonoBehaviour
    {
        protected Damageable Body;
        protected StatusEffects Status; // 화상·둔화·빙결·감전
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
        protected virtual bool UsesToken => true;            // 나팔수 소환은 동시 공격 수에 안 들어가요
        protected virtual float TurnSpeed => 0f;             // 초당 회전 각도. 0이면 바로 돌아봐요 (방패 산적 180°)
        protected virtual bool InterruptibleWindup => false; // 예고 중에 맞으면 끊김 (나팔수 소환, 예고 중 경직 규칙의 유일한 예외)
        protected virtual bool RadialTelegraph => false;     // 원형 예고는 가운데부터 차올라요
        protected virtual float HitKnockback => -1f;         // 플레이어를 미는 거리. 음수면 기본 넉백
        protected virtual float DeathDelay => 0.25f;         // 쓰러진 뒤 사라지기까지
        protected virtual bool IsBoss => false;              // 고수·보스: 둔화 효과 50%, 빙결 1초 + 면역 10초
        public virtual EnemyRank Rank => IsElite ? EnemyRank.Elite : EnemyRank.Normal; // 쓰러뜨리면 받는 수련치
        protected virtual bool FreezeBlocked => false;       // 보스 페이즈 전환 연출 중엔 빙결이 안 걸림

        public string DisplayName { get; private set; }
        // 지금 쓰는 공격 이름 (결과 화면 사망 원인: "청죽객의 죽림섬에 쓰러짐"). 없으면 이름만 ("산적 졸개에게 쓰러짐")
        public virtual string AttackName => null;
        public Damageable Damageable => Body;
        public CharacterController BodyCollider => Controller;
        public float Height => Controller != null ? Controller.height : 1.6f;
        public EnemyAttackTimeline.Phase AttackPhase => Attack.Current;
        public bool HasToken => _tokens != null && _tokens.Holds(this);
        public bool IsStunned => _stunTimer > 0f;
        public bool IsDead => _dead;
        public bool IsHidden { get; private set; }

        // 넓은 맵에 놓아 둔 적은 플레이어가 이 거리 안에 오거나 맞기 전까지 가만히 있어요. 0이면 바로 깨어 있어요.
        public float WakeRadius { get; set; }
        public bool IsAwake => _awake;
        public event System.Action<EnemyBrain> Woke;
        bool _awake;
        public bool TelegraphVisible => Telegraph != null && Telegraph.IsVisible;
        public StatusEffects Statuses => Status;

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
            // 몬스터 시트에서 빈 칸인 적이 있는 값 (나팔수 사거리, 화약 산적 빈틈·공격 간격, 보스는 패턴마다 다름)
            WindupTime = GameConfig.GetOptional($"{Key}.windup", 0.6f);
            AttackRange = GameConfig.GetOptional($"{Key}.attack_range", 1.2f);
            RecoveryTime = GameConfig.GetOptional($"{Key}.recovery", 0f);
            AttackInterval = GameConfig.GetOptional($"{Key}.attack_interval", 2f);
            TrackMoveRatio = GameConfig.Get("enemy.track_move_ratio", 0.5f);
            ReadTuning();

            Body.Configure(Tuning("hp", 30f), Tuning("toughness", 0f), GameConfig.GetOptional($"{Key}.defense", 0f), Controller.radius, flashRenderer);
            Body.Damaged += OnDamaged;

            Status = StatusEffects.For(Body);
            Status.IsBoss = IsBoss;
            Status.FreezeBlocked = () => FreezeBlocked;
            Status.Frozen += OnFrozen;

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
            OnInitialized();
        }

        protected float Tuning(string field, float fallback) => GameConfig.Get($"{Key}.{field}", fallback);

        protected virtual void ReadTuning() { }
        protected virtual void TickAlways(float dt) { } // 경직·공격 중에도 매 프레임 (살수 연막·패턴 간격)
        protected virtual void OnInitialized() { }
        protected virtual void OnWoke() { }

        protected virtual bool ShouldWake() => WakeRadius <= 0f || ToPlayer().magnitude <= WakeRadius;

        // 깨어나면 그때부터 첫 공격 대기(1초)를 세요.
        protected void Wake()
        {
            if (_awake) return;
            _awake = true;
            _cooldown = GameConfig.Get("enemy.first_attack_delay", 1f);
            OnWoke();
            Woke?.Invoke(this);
        }

        // 빙결: 모든 행동 정지 (소환·시전 포함). 하던 공격은 취소해요 (보스는 진행 중인 패턴 취소).
        void OnFrozen()
        {
            bool cancelled = Attack.IsAttacking;
            if (cancelled) CancelAttack();
            OnFreezeStarted(cancelled);
        }

        protected virtual void OnFreezeStarted(bool cancelledAttack) { }

        void Update()
        {
            if (!_initialized || _dead || Status.IsFrozen) return;
            // 둔화: 이동과 공격(예고·빈틈 포함)이 함께 느려져요 (1중첩당 10%, 보스는 효과 50%)
            float dt = Time.deltaTime * Status.SpeedMultiplier;
            _reaction.Tick(dt);
            TickElite(Time.deltaTime);
            TickAlways(dt);
            if (_cooldown > 0f) _cooldown -= dt;
            TickStrafeFlip(dt);

            if (Player == null || Player.IsDead)
            {
                if (Attack.IsAttacking) CancelAttack();
                return;
            }

            if (!_awake)
            {
                if (!ShouldWake()) return;
                Wake();
            }

            if (_stunTimer > 0f)
            {
                _stunTimer -= dt;
                return;
            }

            switch (Attack.Current)
            {
                case EnemyAttackTimeline.Phase.Ready:
                    bool canAttack = _cooldown <= 0f && (!UsesToken || _tokens.HasFree || HasToken);
                    TickMovement(dt, canAttack);
                    if (canAttack && WantsToAttack(ToPlayer().magnitude) && (!UsesToken || _tokens.TryAcquire(this))) BeginAttack();
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
            if (TurnSpeed <= 0f && to.sqrMagnitude > 1e-4f) FaceToward(to); // 천천히 도는 적은 지금 보는 방향 그대로
            AimDirection = Facing;
            Attack.Start(WindupTime + Status.WindupExtra, ActiveTime, RecoveryTime); // 눈송이 수정: 둔화 3중첩이면 예고가 길어짐
            OnAttackStarted();
            Telegraph.Show(TelegraphShape(), RadialTelegraph);
            if (Rank != EnemyRank.Normal) Sfx.Play(SfxId.Telegraph, 0.6f); // 예고 소리는 정예·살수·고수·보스만 (졸개가 여럿이면 겹쳐서)
            UpdateTelegraph();
        }

        protected void CancelAttack()
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
        protected virtual void OnAttackInterrupted() { } // InterruptibleWindup인 적의 예고가 끊겼을 때
        protected virtual void OnHitTaken() { }          // 쓰러지지 않고 맞았을 때 (경직 처리 뒤)
        protected virtual void OnDied() { }

        // 예고 앞 절반: 플레이어 쪽으로 방향을 맞추며 이동속도 50%로 따라와요. 뒤 절반은 멈춰서 고정.
        protected virtual void TickWindup(float dt, bool tracking)
        {
            if (!tracking) return;
            Vector3 to = ToPlayer();
            TurnToward(to, dt);
            AimDirection = Facing;
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

        // TurnSpeed만큼만 돌아요 (0이면 바로). 방패 산적은 이 때문에 옆·뒤가 열려요.
        protected void TurnToward(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;
            if (TurnSpeed <= 0f)
            {
                FaceToward(direction);
                return;
            }
            FaceToward(Vector3.RotateTowards(Facing, direction.normalized, TurnSpeed * Mathf.Deg2Rad * dt, 0f));
        }

        // 숨으면 보이지 않고 맞힐 수도 없어요 (청죽사 풀숲). 충돌체는 그대로 둬요.
        protected void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            Body.Targetable = !hidden;
            if (_visual != null) _visual.gameObject.SetActive(!hidden);
        }

        // 기물이 적을 밀거나 끌어요 (금강저 넉백, 잔영 가면). 보스·숨은 적·빙결된 적은 그대로예요. 벽에는 막혀요.
        public void Displace(Vector3 offset)
        {
            if (!_initialized || _dead || IsBoss || IsHidden || Status.IsFrozen) return;
            offset.y = 0f;
            Controller.Move(offset);
        }

        // CharacterController는 위치를 직접 바꾸려면 잠깐 꺼야 해요.
        // 껐다 켜면 Physics.IgnoreCollision이 풀리니까 EnemyDirector.Spawn 전(OnInitialized)에만 써요.
        protected void Teleport(Vector3 position)
        {
            Controller.enabled = false;
            transform.position = new Vector3(position.x, 0f, position.z);
            Controller.enabled = true;
            if (_initialized) Director.RestoreIgnores(this); // 충돌체를 껐다 켜면 서로 무시하던 것이 풀려요
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

        protected bool TryHitPlayer() => Player.TryReceiveHit(AttackPower, transform.position, HitKnockback, Body);

        // 지역 배율 (폐사찰 ×1.3): 나온 직후 한 번
        public void ScaleAttack(float multiplier) => AttackPower *= multiplier;

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
            _sinceHit = 0f; // 회춘: 맞으면 회복이 멈춰요 (화상 같은 지속 피해도)
            if (Body.IsDead)
            {
                Die();
                return;
            }

            Wake(); // 멀리서 맞아도 깨어나요

            // 화상·번개 같은 파생 피해는 경직·늦춰짐·나팔 끊기를 일으키지 않아요 (맞은 것으로는 쳐요)
            if (!Body.LastHitReacts)
            {
                OnHitTaken();
                return;
            }

            if (InterruptibleWindup && !IsIronShirt && Attack.Current == EnemyAttackTimeline.Phase.Windup)
            {
                CancelAttack();
                _stunTimer = _reaction.Hitstun;
                OnAttackInterrupted();
                OnHitTaken();
                return;
            }

            switch (_reaction.OnHit(Attack.Current, SuperArmor || IsIronShirt)) // 철포삼: 경직·늦춰짐 없음
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
            OnHitTaken();
        }

        // 쓰러짐. 화약 산적은 자폭할 때도 이걸 불러요.
        protected void Die()
        {
            if (_dead) return;
            _dead = true;
            CancelAttack();
            Director.Unregister(this);
            Controller.enabled = false;
            if (_visual != null) _visual.localScale = new Vector3(1.1f, 0.3f, 1.1f);
            OnEliteDied();
            OnDied();
            Destroy(gameObject, DeathDelay);
        }

        void OnDestroy()
        {
            if (Body != null) Body.Damaged -= OnDamaged;
            if (_tokens != null) _tokens.Release(this);
            if (Director != null) Director.Unregister(this);
        }
    }
}
