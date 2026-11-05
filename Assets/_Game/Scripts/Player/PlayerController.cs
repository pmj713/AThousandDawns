using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 시험판 플레이어: 이동, 대시(무적), 평타 3연타(누르고 있기), 대시 공격, 피격. 수치는 전부 data_balance에서 읽어요.
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public sealed class PlayerController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Transform visual;
        [SerializeField] SwingFlash swingFlash;
        [SerializeField] Renderer bodyRenderer;

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

        public event Action<int> Hurt;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            ReadTuning();
            Health = _maxHealth;
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
            if (_input.AttackPressed) _combo.Press();

            _dash.Tick(dt);
            if (_invulnerableTimer > 0f) _invulnerableTimer -= dt;

            if (IsStunned)
            {
                float step = Mathf.Min(dt, _stunTimer);
                _controller.Move(_knockbackVelocity * step);
                _stunTimer -= dt;
                return;
            }

            if (IsDashing)
            {
                UpdateDash(dt);
                return;
            }

            if (_sinceDashPress <= _inputBuffer && _combo.CanDashCancel && _dash.TryConsume())
            {
                StartDash();
                UpdateDash(dt);
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
        public bool TryReceiveHit(float enemyAttack, Vector3 source)
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
            _knockbackVelocity = away.normalized * (_hurtKnockback / Mathf.Max(0.01f, _hurtStun));

            _combo.Cancel();
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

        void StartDash()
        {
            Vector2 move = _input.Move;
            _dashDirection = move.sqrMagnitude > 0.01f ? new Vector3(move.x, 0f, move.y).normalized : _facing;
            _facing = _dashDirection;
            _combo.Cancel();
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
            float speed = _moveSpeed * (_combo.IsAttacking ? _attackMoveRatio : 1f);
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

            if (!_combo.IsAttacking && _velocity.sqrMagnitude > 0.01f) _facing = _velocity.normalized;
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
            int count = HitSector(radius, angle, finisher ? _finisherDamage : _basicDamage);
            Gauge = Mathf.Min(_gaugeMax, Gauge + count * (finisher ? _gaugeFinisher : _gaugeBasic));
        }

        void DoDashAttack()
        {
            _hitThisSwing.Clear();
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, _dashAttackRadius, _dashAttackAngle, 0.12f);
            int count = HitSector(_dashAttackRadius, _dashAttackAngle, _dashAttackDamage);
            Gauge = Mathf.Min(_gaugeMax, Gauge + count * _gaugeDashAttack);
        }

        int HitSector(float radius, float angle, float baseDamage)
        {
            int found = Physics.OverlapSphereNonAlloc(transform.position, radius + 1f, _overlap, ~0, QueryTriggerInteraction.Collide);
            int hits = 0;
            for (int i = 0; i < found; i++)
            {
                var target = _overlap[i].GetComponentInParent<Damageable>();
                if (target == null || target.IsDead || !_hitThisSwing.Add(target)) continue;
                if (!HitShapes.InSector(transform.position, _facing, radius, angle, target.transform.position, target.BodyRadius)) continue;

                var hit = HitDamageInput.Of(baseDamage);
                hit.Toughness = target.Toughness;
                hit.Defense = target.Defense;
                target.ApplyHit(DamageFormula.ToEnemy(hit, _rules));
                hits++;
            }
            return hits;
        }
    }
}
