using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 시험판 플레이어: 이동, 대시(무적), 평타 3연타(누르고 있기), 대시 공격, 초식(회선참), 절초(만검귀종), 피격. 수치는 전부 data_balance에서 읽어요.
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public sealed class PlayerController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform visual;
        [SerializeField] SwingFlash swingFlash;
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] SwordRain swordRain;

        CharacterController _controller;
        PlayerInputReader _input;
        ComboTimeline _combo;
        DashCharges _dash;
        DamageRules _rules;

        float _moveSpeed, _accelTime, _decelTime, _attackMoveRatio;
        float _dashDistance, _dashDuration, _dashIframe, _dashAttackWindow, _inputBuffer;
        float _basicDamage, _finisherDamage, _dashAttackDamage, _dashAttackRadius, _dashAttackAngle;
        float[] _hitRadius, _hitAngle;
        float _finisherLunge, _finisherActive;
        float _gaugeMax, _gaugeBasic, _gaugeFinisher, _gaugeDashAttack, _gaugeHurt;
        float _maxHealth, _hurtIframe, _hurtKnockback, _hurtStun;
        float _skillDamage, _skillCooldown, _skillCastTime, _skillHitTime, _skillRadius, _gaugeSkill;

        float _skillElapsed = -1f; // 0 이상이면 회선참 시전 중
        float _skillCooldownLeft;
        bool _skillHitDone;
        float _sinceSkillPress = float.PositiveInfinity;

        float _ultimateCastTime, _ultimateRadius, _ultimateSwordDamage, _ultimateFallTime;
        int _ultimateSwordCount;
        float _ultimateTimer;
        float _sinceUltimatePress = float.PositiveInfinity;
        float _slowTimer, _slowRatio;
        int _burnStacks, _burnMaxStacks;
        float _burnTimer, _burnTickTimer, _burnPerStack, _burnDuration, _burnTick;

        Vector3 _velocity;
        Vector3 _knockbackVelocity;
        float _stunTimer, _flashTimer;
        MaterialPropertyBlock _block;
        Color _bodyColor = Color.white;
        Vector3 _facing = Vector3.forward;
        Vector3 _dashDirection;
        float _dashTimer, _invulnerableTimer, _dashAttackTimer, _lungeRemaining;
        float _sinceDashPress = float.PositiveInfinity;
        float _sinceAttackPress = float.PositiveInfinity;

        readonly Collider[] _overlap = new Collider[32];
        readonly HashSet<Damageable> _hitThisSwing = new HashSet<Damageable>();

        public ComboTimeline Combo => _combo;
        public DashCharges Dash => _dash;
        public float Gauge { get; private set; }
        public float GaugeMax => _gaugeMax;
        public bool IsDashing => _dashTimer > 0f;
        public bool IsInvulnerable => _invulnerableTimer > 0f;
        public bool IsStunned => _stunTimer > 0f;
        public float Health { get; private set; }
        public float MaxHealth => _maxHealth;
        public bool IsDead => Health <= 0f;
        public float BodyRadius => _controller != null ? _controller.radius : 0.3f;
        public CharacterController Body => _controller;

        // 지금은 기물이 없어서 0. 기물이 붙으면 여기로 들어와요.
        public float Toughness { get; set; }
        public float Defense { get; set; }
        public int Gongryeok { get; set; } // 공력: 초식·절초 피해 배율 (영약·흡성으로 오름)

        public bool IsCastingSkill => _skillElapsed >= 0f;
        public bool IsCastingUltimate => _ultimateTimer > 0f;
        public bool UltimateReady => Gauge >= _gaugeMax;
        public Vector3 Facing => _facing;

        // 둔화 (청죽사 물기: 이동 −25%, 2초). 겹치면 더 센 쪽, 더 긴 쪽으로.
        public float SlowRatio => _slowTimer > 0f ? _slowRatio : 0f;
        public float SlowLeft => Mathf.Max(0f, _slowTimer);

        public void ApplySlow(float ratio, float duration)
        {
            _slowRatio = Mathf.Max(SlowRatio, ratio);
            _slowTimer = Mathf.Max(_slowTimer, duration);
        }

        // 화상 (녹림왕 불타는 죽림): 1중첩씩 쌓이고(최대 5), 쌓일 때마다 지속 4초가 새로 시작돼요.
        // 0.5초마다 중첩 × perStackPerSecond × 0.5 만큼 피해. 무적·경직과 상관없는 지속 피해예요.
        public int BurnStacks => _burnTimer > 0f ? _burnStacks : 0;

        public void AddBurnStack(float perStackPerSecond)
        {
            if (IsDead) return;
            _burnStacks = Mathf.Min(_burnMaxStacks, BurnStacks + 1);
            _burnTimer = _burnDuration;
            _burnPerStack = perStackPerSecond;
        }

        void TickBurn(float dt)
        {
            if (_burnTimer <= 0f)
            {
                _burnStacks = 0;
                _burnTickTimer = 0f;
                return;
            }
            _burnTimer -= dt;
            _burnTickTimer += dt;
            while (_burnTickTimer >= _burnTick && !IsDead)
            {
                _burnTickTimer -= _burnTick;
                TakeDirectDamage(Mathf.Max(1, Mathf.RoundToInt(_burnStacks * _burnPerStack * _burnTick)));
            }
        }

        void TakeDirectDamage(int damage)
        {
            Health = Mathf.Max(0f, Health - damage);
            DamagePopups.Spawn(transform.position + Vector3.up * 2f, damage, true);
            Hurt?.Invoke(damage);
            if (IsDead && visual != null) visual.localScale = new Vector3(1.2f, 0.25f, 1.2f);
        }
        public float SkillCooldown => _skillCooldown;
        public float SkillCooldownLeft => Mathf.Max(0f, _skillCooldownLeft);

        public event Action<int> Hurt;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            ReadTuning();
            Health = _maxHealth;
            if (swordRain == null) swordRain = gameObject.AddComponent<SwordRain>(); // 씬을 다시 만들기 전에도 쓸 수 있게
            if (bodyRenderer != null && bodyRenderer.sharedMaterial != null && bodyRenderer.sharedMaterial.HasProperty(BaseColorId))
                _bodyColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        void ReadTuning()
        {
            _moveSpeed = GameConfig.Get("player.move_speed", 6f);
            _accelTime = GameConfig.Get("player.accel_time", 0.06f);
            _decelTime = GameConfig.Get("player.decel_time", 0.04f);
            _attackMoveRatio = GameConfig.Get("player.attack_move_ratio", 0.4f);

            _dashDistance = GameConfig.Get("dash.distance", 4f);
            _dashDuration = GameConfig.Get("dash.duration", 0.18f);
            _dashIframe = GameConfig.Get("dash.iframe", 0.2f);
            _dashAttackWindow = GameConfig.Get("dash.attack_window", 0.15f);
            _dash = new DashCharges(Mathf.RoundToInt(GameConfig.Get("dash.charges", 1f)), GameConfig.Get("dash.recharge", 1f));

            _inputBuffer = GameConfig.Get("combo.input_buffer", 0.12f);
            var hits = new ComboTimeline.HitTiming[3];
            for (int i = 0; i < 3; i++)
            {
                string k = $"combo.{i + 1}.";
                hits[i] = new ComboTimeline.HitTiming(GameConfig.Get(k + "windup", 0.08f), GameConfig.Get(k + "active", 0.06f), GameConfig.Get(k + "recovery", 0.18f));
            }
            _combo = new ComboTimeline(hits, GameConfig.Get("combo.chain_window", 0.25f), _inputBuffer);
            _combo.SwingStarted += OnSwingStarted;
            _combo.HitFrame += OnHitFrame;

            float r1 = GameConfig.Get("combo.1.radius", 1.6f), a1 = GameConfig.Get("combo.1.angle", 100f);
            _hitRadius = new[] { r1, r1, GameConfig.Get("combo.3.radius", 2f) };
            _hitAngle = new[] { a1, a1, GameConfig.Get("combo.3.angle", 160f) };
            _finisherLunge = GameConfig.Get("combo.3.lunge", 0.5f);
            _finisherActive = hits[2].Active;

            _basicDamage = GameConfig.Get("sword.basic_damage", 10f);
            _finisherDamage = GameConfig.Get("sword.finisher_damage", 15f);
            _dashAttackDamage = GameConfig.Get("sword.dash_attack_damage", 20f);
            _dashAttackRadius = GameConfig.Get("sword.dash_attack_radius", 2f);
            _dashAttackAngle = GameConfig.Get("sword.dash_attack_angle", 120f);

            _skillDamage = GameConfig.Get("sword.skill_damage", 40f);
            _skillCooldown = GameConfig.Get("sword.skill_cooldown", 5f);
            _skillCastTime = GameConfig.Get("sword.skill_cast_time", 0.35f);
            _skillHitTime = GameConfig.Get("sword.skill_hit_time", 0.12f);
            _skillRadius = GameConfig.Get("sword.skill_radius", 2.2f);
            _gaugeSkill = GameConfig.Get("gauge.skill_hit", 3f);

            _ultimateCastTime = GameConfig.Get("ultimate.cast_time", 1f);
            _ultimateRadius = GameConfig.Get("ultimate.radius", 3f);
            _ultimateSwordCount = Mathf.RoundToInt(GameConfig.Get("ultimate.sword_count", 8f));
            _ultimateSwordDamage = GameConfig.Get("ultimate.sword_damage", 18f);
            _ultimateFallTime = GameConfig.Get("ultimate.sword_fall_time", 0.15f);
            Gongryeok = Mathf.RoundToInt(GameConfig.Get("player.gongryeok", 0f));

            _burnMaxStacks = Mathf.RoundToInt(GameConfig.Get("burn.max_stacks", 5f));
            _burnDuration = GameConfig.Get("burn.duration", 4f);
            _burnTick = Mathf.Max(0.05f, GameConfig.Get("burn.tick", 0.5f));

            _gaugeMax = GameConfig.Get("ultimate.gauge_max", 100f);
            _gaugeBasic = GameConfig.Get("gauge.basic_hit", 2f);
            _gaugeFinisher = GameConfig.Get("gauge.finisher_hit", 4f);
            _gaugeDashAttack = GameConfig.Get("gauge.dash_attack_hit", 5f);
            _gaugeHurt = GameConfig.Get("gauge.hurt", 8f);

            _maxHealth = GameConfig.Get("player.max_health", 100f);
            _hurtIframe = GameConfig.Get("hurt.iframe", 0.6f);
            _hurtKnockback = GameConfig.Get("hurt.knockback", 0.5f);
            _hurtStun = GameConfig.Get("hurt.stun", 0.15f);

            _rules = new DamageRules(
                GameConfig.Get("damage.toughness_floor_ratio", 0.2f),
                GameConfig.Get("damage.gongryeok_per_point", 0.1f),
                GameConfig.Get("damage.player_multiplier_floor", 0.4f),
                GameConfig.Get("damage.player_defense_constant", 50f));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateFlash(dt);
            if (IsDead) return;

            _sinceDashPress = _input.DashPressed ? 0f : _sinceDashPress + dt;
            _sinceAttackPress = _input.AttackPressed ? 0f : _sinceAttackPress + dt;
            _sinceSkillPress = _input.SkillPressed ? 0f : _sinceSkillPress + dt;
            _sinceUltimatePress = _input.UltimatePressed ? 0f : _sinceUltimatePress + dt;
            if (_input.AttackPressed) _combo.Press();

            _dash.Tick(dt);
            if (_invulnerableTimer > 0f) _invulnerableTimer -= dt;
            if (_skillCooldownLeft > 0f) _skillCooldownLeft -= dt;
            if (_slowTimer > 0f) _slowTimer -= dt;
            TickBurn(dt);
            if (IsDead) return;

            if (IsStunned)
            {
                float step = Mathf.Min(dt, _stunTimer);
                _controller.Move(_knockbackVelocity * step);
                _stunTimer -= dt;
                return;
            }

            // 절초 시전 중: 제자리 고정, 무적 (조작 시트)
            if (IsCastingUltimate)
            {
                _ultimateTimer -= dt;
                return;
            }

            if (IsDashing)
            {
                UpdateDash(dt);
                return;
            }

            // 절초는 기세가 가득이면 평타·초식 중에도 바로 나가요 (몰릴 때 뒤집는 수단)
            if (_sinceUltimatePress <= _inputBuffer && UltimateReady)
            {
                StartUltimate();
                return;
            }

            // 대시로 끊기: 평타는 후딜 중에만, 회선참은 베고 난 뒤에만
            bool canCancel = _combo.CanDashCancel && (!IsCastingSkill || _skillHitDone);
            if (_sinceDashPress <= _inputBuffer && canCancel && _dash.TryConsume())
            {
                StartDash();
                UpdateDash(dt);
                return;
            }

            // 초식은 평타를 쉬고 있거나 평타 후딜 중에 나가요 (대시로 끊을 수 있는 때와 같음)
            if (_sinceSkillPress <= _inputBuffer && _skillCooldownLeft <= 0f && !IsCastingSkill && _combo.CanDashCancel)
                StartSkill();

            if (IsCastingSkill)
            {
                TickSkill(dt);
                UpdateMovement(dt);
                ApplySkillSpin();
                return;
            }

            if (_dashAttackTimer > 0f)
            {
                _dashAttackTimer -= dt;
                if (_input.AttackHeld || _sinceAttackPress <= _inputBuffer)
                {
                    _dashAttackTimer = 0f;
                    _sinceAttackPress = float.PositiveInfinity;
                    _combo.ConsumePress();
                    DoDashAttack();
                }
            }

            _combo.Tick(dt, _input.AttackHeld);
            UpdateMovement(dt);
        }

        // 적 공격이 닿았을 때. 무적(대시·피격 후)이면 false를 돌려줘요. 화살은 이때 그대로 지나가요.
        // knockback을 주면 기본 넉백(0.5칸) 대신 그만큼 밀려요 (방패 산적 1칸).
        public bool TryReceiveHit(float enemyAttack, Vector3 source, float knockback = -1f)
        {
            if (IsDead || IsInvulnerable) return false;

            var hit = new PlayerHitInput { EnemyAttack = enemyAttack, Toughness = Toughness, Defense = Defense };
            int damage = DamageFormula.ToPlayer(hit, _rules);
            Health = Mathf.Max(0f, Health - damage);

            _invulnerableTimer = Mathf.Max(_invulnerableTimer, _hurtIframe);
            _stunTimer = _hurtStun;
            Vector3 away = transform.position - source;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -_facing;
            _knockbackVelocity = away.normalized * ((knockback >= 0f ? knockback : _hurtKnockback) / Mathf.Max(0.01f, _hurtStun));

            _combo.Cancel();
            _skillElapsed = -1f; // 맞으면 회선참도 끊겨요 (쿨타임은 그대로 돎)
            _velocity = Vector3.zero;
            _lungeRemaining = 0f;
            _dashAttackTimer = 0f;
            Gauge = Mathf.Min(_gaugeMax, Gauge + _gaugeHurt);

            _flashTimer = 0.12f;
            DamagePopups.Spawn(transform.position + Vector3.up * 2f, damage, true);
            Hurt?.Invoke(damage);
            if (IsDead && visual != null) visual.localScale = new Vector3(1.2f, 0.25f, 1.2f); // 시험판용 쓰러짐 표시
            return true;
        }

        void UpdateFlash(float dt)
        {
            if (bodyRenderer == null || (_flashTimer <= 0f && _block == null)) return;
            _flashTimer -= dt;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, _flashTimer > 0f ? Color.white : _bodyColor);
            bodyRenderer.SetPropertyBlock(_block);
        }

        // 만검귀종 (하단전): 1초 동안 맵 전체 적에게 검 8자루가 고르게 떨어져요 (ultimate.radius 0 = 맵 전체). 기세를 모두 써요.
        void StartUltimate()
        {
            _sinceUltimatePress = float.PositiveInfinity;
            Gauge = Mathf.Max(0f, Gauge - _gaugeMax);
            _ultimateTimer = _ultimateCastTime;
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, _ultimateCastTime);

            _combo.Cancel();
            _skillElapsed = -1f;
            _velocity = Vector3.zero;
            _lungeRemaining = 0f;
            _dashAttackTimer = 0f;
            ApplyFacing();

            if (swingFlash != null && _ultimateRadius > 0f) swingFlash.Show(transform.position, _facing, _ultimateRadius, 360f, 0.2f); // 0이면 맵 전체라 범위 표시 없음
            var hit = HitDamageInput.Of(_ultimateSwordDamage);
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            swordRain.Begin(transform.position, _ultimateRadius, _ultimateSwordCount, _ultimateCastTime, _ultimateFallTime, hit, _rules);
        }

        // 시험판용: G 키로 기세를 가득 채워요.
        public void FillGauge() => Gauge = _gaugeMax;

        // 회선참: 주변 원형(반경 2.2칸)을 한 바퀴 베요. 시전 0.35초, 쿨타임은 누르는 순간부터.
        void StartSkill()
        {
            _sinceSkillPress = float.PositiveInfinity;
            _skillCooldownLeft = _skillCooldown;
            _skillElapsed = 0f;
            _skillHitDone = false;
            _combo.Cancel();
            _lungeRemaining = 0f;
            _dashAttackTimer = 0f;
            if (_input.TryGetAimPoint(out Vector3 aim))
            {
                Vector3 dir = aim - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) _facing = dir.normalized;
            }
        }

        void TickSkill(float dt)
        {
            _skillElapsed += dt;
            if (!_skillHitDone && _skillElapsed >= _skillHitTime)
            {
                _skillHitDone = true;
                _hitThisSwing.Clear();
                if (swingFlash != null) swingFlash.Show(transform.position, _facing, _skillRadius, 360f, 0.15f);
                var hit = HitDamageInput.Of(_skillDamage);
                hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
                int count = HitSector(_skillRadius, 360f, hit, HitTag.Skill);
                Gauge = Mathf.Min(_gaugeMax, Gauge + count * _gaugeSkill);
            }
        }

        // 베는 동안 몸을 한 바퀴 돌려요 (시험판용 표시). 시전이 끝나면 원래 방향.
        void ApplySkillSpin()
        {
            if (_skillElapsed >= _skillCastTime)
            {
                _skillElapsed = -1f;
                ApplyFacing();
                return;
            }
            if (visual == null) return;
            float t = Mathf.Clamp01(_skillElapsed / Mathf.Max(0.01f, _skillHitTime + 0.08f));
            visual.rotation = Quaternion.LookRotation(_facing) * Quaternion.Euler(0f, 360f * t, 0f);
        }

        void StartDash()
        {
            Vector2 move = _input.Move;
            _dashDirection = move.sqrMagnitude > 0.01f ? new Vector3(move.x, 0f, move.y).normalized : _facing;
            _facing = _dashDirection;
            _combo.Cancel();
            _skillElapsed = -1f;
            _dashTimer = _dashDuration;
            _invulnerableTimer = _dashIframe;
            _sinceDashPress = float.PositiveInfinity;
            _lungeRemaining = 0f;
        }

        void UpdateDash(float dt)
        {
            float step = Mathf.Min(dt, _dashTimer);
            _controller.Move(_dashDirection * (_dashDistance / _dashDuration) * step);
            _dashTimer -= dt;
            _velocity = Vector3.zero;
            ApplyFacing();
            if (_dashTimer <= 0f) _dashAttackTimer = _dashAttackWindow;
        }

        void UpdateMovement(float dt)
        {
            Vector2 move = Vector2.ClampMagnitude(_input.Move, 1f);
            bool attacking = _combo.IsAttacking || IsCastingSkill;
            float speed = _moveSpeed * (attacking ? _attackMoveRatio : 1f) * (1f - SlowRatio);
            Vector3 desired = new Vector3(move.x, 0f, move.y) * speed;
            float rate = desired.sqrMagnitude >= _velocity.sqrMagnitude ? _moveSpeed / Mathf.Max(0.001f, _accelTime) : _moveSpeed / Mathf.Max(0.001f, _decelTime);
            _velocity = Vector3.MoveTowards(_velocity, desired, rate * dt);

            Vector3 displacement = _velocity * dt;
            if (_lungeRemaining > 0f)
            {
                float lungeStep = Mathf.Min(_lungeRemaining, _finisherLunge / Mathf.Max(0.01f, _finisherActive) * dt);
                displacement += _facing * lungeStep;
                _lungeRemaining -= lungeStep;
            }
            _controller.Move(displacement);

            if (!attacking && _velocity.sqrMagnitude > 0.01f) _facing = _velocity.normalized;
            ApplyFacing();
        }

        void ApplyFacing()
        {
            if (visual != null && _facing.sqrMagnitude > 0.0001f) visual.rotation = Quaternion.LookRotation(_facing);
        }

        void OnSwingStarted(int index)
        {
            if (_input.TryGetAimPoint(out Vector3 aim))
            {
                Vector3 dir = aim - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) _facing = dir.normalized;
            }
            ApplyFacing();
            _hitThisSwing.Clear();
        }

        void OnHitFrame(int index)
        {
            bool finisher = index == _combo.HitCount - 1;
            if (finisher) _lungeRemaining = _finisherLunge;
            float radius = _hitRadius[Mathf.Min(index, _hitRadius.Length - 1)];
            float angle = _hitAngle[Mathf.Min(index, _hitAngle.Length - 1)];
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, radius, angle, _combo.HitCount > 0 ? 0.1f : 0f);
            int count = HitSector(radius, angle, HitDamageInput.Of(finisher ? _finisherDamage : _basicDamage), finisher ? HitTag.Finisher : HitTag.Basic);
            Gauge = Mathf.Min(_gaugeMax, Gauge + count * (finisher ? _gaugeFinisher : _gaugeBasic));
        }

        void DoDashAttack()
        {
            _hitThisSwing.Clear();
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, _dashAttackRadius, _dashAttackAngle, 0.12f);
            int count = HitSector(_dashAttackRadius, _dashAttackAngle, HitDamageInput.Of(_dashAttackDamage), HitTag.DashAttack);
            Gauge = Mathf.Min(_gaugeMax, Gauge + count * _gaugeDashAttack);
        }

        // template의 피해·배율을 그대로 쓰고, 대상마다 강인함·방어력·상황 배율(방패 정면 등)만 채워요.
        int HitSector(float radius, float angle, HitDamageInput template, HitTag tag)
        {
            int found = Physics.OverlapSphereNonAlloc(transform.position, radius + 1f, _overlap, ~0, QueryTriggerInteraction.Collide);
            int hits = 0;
            for (int i = 0; i < found; i++)
            {
                var target = _overlap[i].GetComponentInParent<Damageable>();
                if (target == null || target.IsDead || !target.Targetable || !_hitThisSwing.Add(target)) continue;
                if (!HitShapes.InSector(transform.position, _facing, radius, angle, target.transform.position, target.BodyRadius)) continue;

                var hit = template;
                hit.Toughness = target.Toughness;
                hit.Defense = target.Defense;
                hit.Situational *= target.ResolveSituational(transform.position, tag);
                target.ApplyHit(DamageFormula.ToEnemy(hit, _rules));
                hits++;
            }
            return hits;
        }
    }
}
