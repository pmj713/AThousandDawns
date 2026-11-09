using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 시험판 플레이어: 이동, 대시(무적), 평타 3연타(누르고 있기), 대시 공격, 초식(회선참), 절초(만검귀종), 피격. 수치는 전부 data_balance에서 읽어요.
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public sealed partial class PlayerController : MonoBehaviour
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

        float _ultimateCastTime, _ultimateSwordDamage, _ultimateFallTime;
        Vector2 _ultimateArea; // 방 하나 크기 (가로 × 세로)
        int _ultimateSwordCount;
        float _ultimateTimer;
        float _sinceUltimatePress = float.PositiveInfinity;
        float _slowTimer, _slowRatio;
        float _burnPerStackRatio, _burnStage4Bonus, _slowStage4Bonus, _dischargePerStack, _dischargeStage4Bonus;
        float _chainRatio, _chainStage6Ratio, _chainRange, _firePatchDuration, _firePatchRadius, _thunderImprintRatio;
        int _burnStage6Extra, _chainTargets, _thunderImprintChains;
        PlayerKeywords _keywords;
        Damageable _firstHit; // 이번 휘두르기에서 처음 맞은 적 (연쇄 번개 시작점)
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
        // 공력: 초식·절초 피해 배율이자 경맥도 모든 단전의 출력 (영약·흡성으로 오름)
        public int Gongryeok
        {
            get => _gongryeok;
            set
            {
                _gongryeok = Mathf.Max(0, value);
                if (_items != null) _items.SetPower(_gongryeok);
            }
        }
        int _gongryeok;

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

        // 플레이어 화상 (상태이상 탭 "플레이어가 걸릴 때"): 1중첩 초당 2 × 지역 공격 배율, 최대 3중첩, 3초.
        // 쌓일 때마다 지속이 새로 시작되고, 0.5초마다 들어와요. 강인함 무시, 방어력 적용, 무적·경직과 상관없는 지속 피해.
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
                float defenseRate = EffectiveDefense / (EffectiveDefense + _rules.PlayerDefenseConstant);
                TakeDirectDamage(Mathf.Max(1, Mathf.RoundToInt(_burnStacks * _burnPerStack * _burnTick * (1f - defenseRate))));
            }
        }

        void TakeDirectDamage(int damage)
        {
            damage = SurviveLethal(_shields.Absorb(damage)); // 불괴 호패
            if (damage <= 0) return;
            Health = Mathf.Max(0f, Health - damage);
            DamagePopups.Spawn(transform.position + Vector3.up * 2f, damage, PopupStyle.Player);
            Hurt?.Invoke(damage);
            if (IsDead && visual != null) visual.localScale = new Vector3(1.2f, 0.25f, 1.2f);
        }
        public float SkillCooldown => _skillCooldown * (1f + Side("skill_cooldown")) * (1f - RunSkillCooldownCut); // 기물 부작용: 초식 쿨타임 +% · 면벽수련 기 5
        public float SkillCooldownLeft => Mathf.Max(0f, _skillCooldownLeft);

        public event Action<int> Hurt;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            ReadTuning();
            Health = _maxHealth;
            if (swordRain == null) swordRain = gameObject.AddComponent<SwordRain>(); // 씬을 다시 만들기 전에도 쓸 수 있게
            swordRain.SwordLanded += OnSwordLanded;
            swordRain.Finished += OnSwordRainFinished;
            if (!TryGetComponent(out _keywords)) _keywords = gameObject.AddComponent<PlayerKeywords>();
            InitKeywords();
            InitItems();
            InitLevel();
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
            _finisherTiming = hits[2];
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
            _ultimateArea = new Vector2(GameConfig.Get("ultimate.range_width", 20f), GameConfig.Get("ultimate.range_depth", 12f));
            _ultimateSwordCount = Mathf.RoundToInt(GameConfig.Get("ultimate.sword_count", 8f));
            _ultimateSwordDamage = GameConfig.Get("ultimate.sword_damage", 18f);
            _ultimateFallTime = GameConfig.Get("ultimate.sword_fall_time", 0.15f);
            Gongryeok = Mathf.RoundToInt(GameConfig.Get("player.gongryeok", 0f));

            _burnMaxStacks = Mathf.RoundToInt(GameConfig.Get("player_burn.max_stacks", 3f));
            _burnDuration = GameConfig.Get("player_burn.duration", 3f);
            _burnTick = Mathf.Max(0.05f, GameConfig.Get("burn.tick", 0.5f));

            // 상태이상과 키워드 단계 (상태이상 탭, 무기 탭 절초 각인)
            _burnPerStackRatio = GameConfig.Get("burn.per_stack", 0.15f);
            _burnStage4Bonus = GameConfig.Get("burn.stage4_bonus", 0.5f);
            _burnStage6Extra = Mathf.RoundToInt(GameConfig.Get("burn.stage6_extra_stacks", 2f));
            _slowStage4Bonus = GameConfig.Get("slow.stage4_bonus", 0.3f);
            _dischargePerStack = GameConfig.Get("discharge.per_stack", 0.4f);
            _dischargeStage4Bonus = GameConfig.Get("discharge.stage4_bonus", 0.5f);
            _chainRatio = GameConfig.Get("chain.ratio", 0.25f);
            _chainStage6Ratio = GameConfig.Get("chain.stage6_ratio", 0.4f);
            _chainTargets = Mathf.RoundToInt(GameConfig.Get("chain.targets", 4f));
            _chainRange = GameConfig.Get("chain.range", 3f);
            _firePatchDuration = GameConfig.Get("imprint.fire_patch_duration", 4f);
            _firePatchRadius = GameConfig.Get("imprint.fire_patch_radius", 1f);
            _thunderImprintChains = Mathf.RoundToInt(GameConfig.Get("imprint.thunder_chains", 4f));
            _thunderImprintRatio = GameConfig.Get("imprint.thunder_ratio", 0.1f);
            ReadKeywordTuning();

            _gaugeMax = GameConfig.Get("ultimate.gauge_max", 100f);
            _gaugeBasic = GameConfig.Get("gauge.basic_hit", 2f);
            _gaugeFinisher = GameConfig.Get("gauge.finisher_hit", 4f);
            _gaugeDashAttack = GameConfig.Get("gauge.dash_attack_hit", 5f);
            _gaugeHurt = GameConfig.Get("gauge.hurt", 8f);

            _maxHealth = GameConfig.Get("player.max_health", 100f);
            _hurtIframe = GameConfig.Get("hurt.iframe", 0.6f);
            _hurtKnockback = GameConfig.Get("hurt.knockback", 0.5f);
            _hurtStun = GameConfig.Get("hurt.stun", 0.15f);
            ReadItemTuning();
            ReadMartialItemTuning();

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
            TryRevive(); // 강 20 (화상처럼 맞지 않고 쓰러져도)
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
            TickKeywordTimers(dt);
            TickItemTimers(dt);
            TickMartialItemTimers(dt);
            TickBurn(dt);
            if (IsDead) return;

            if (IsStunned)
            {
                float step = Mathf.Min(dt, _stunTimer);
                _controller.Move(_knockbackVelocity * step);
                _stunTimer -= dt;
                return;
            }

            // 절초 시전 중: 제자리 고정, 무적 (조작 시트). #경공 각인이면 이동·대시 가능
            if (TickUltimateCast(dt)) return;

            if (IsDashing)
            {
                UpdateDash(dt);
                return;
            }

            if (TickPotion(dt)) return; // 금창약 마시는 중

            // 절초는 기세가 가득이면 평타·초식 중에도 바로 나가요 (몰릴 때 뒤집는 수단)
            if (_sinceUltimatePress <= _inputBuffer && UltimateReady)
            {
                StartUltimate();
                return;
            }

            // 대시로 끊기: 평타는 후딜 중에만, 회선참은 베고 난 뒤에만
            bool canCancel = _combo.CanDashCancel && (!IsCastingSkill || _skillHitDone);
            if (_sinceDashPress <= _inputBuffer && canCancel && TryConsumeDash())
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
                if (Skill == SkillKind.BambooFlash) return; // 죽림섬은 순간이동으로만 움직여요
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

            _combo.Tick(dt * AttackSpeedMultiplier, _input.AttackHeld); // #강기 2단계: 평타 속도 +15%
            UpdateMovement(dt);
        }

        // 적 공격이 닿았을 때. 무적(대시·피격 후)이면 false를 돌려줘요. 화살은 이때 그대로 지나가요.
        // knockback을 주면 기본 넉백(0.5칸) 대신 그만큼 밀려요 (방패 산적 1칸).
        // attacker를 주면 #경공 6단계 간파 반격이 그 적에게 가요.
        public bool TryReceiveHit(float enemyAttack, Vector3 source, float knockback = -1f, Damageable attacker = null)
        {
            if (IsDead) return false;
            if (IsDashInvulnerable)
            {
                TryPerfectDodge(attacker); // 간파 (#경공 4단계)
                return false;
            }
            if (IsInvulnerable) return false;
            if (FloorGuard) // 면벽수련 강 10: 층마다 첫 피격 무효
            {
                FloorGuard = false;
                _invulnerableTimer = Mathf.Max(_invulnerableTimer, _hurtIframe);
                _flashTimer = 0.12f;
                return false;
            }

            var hit = new PlayerHitInput
            {
                EnemyAttack = enemyAttack,
                Toughness = EffectiveToughness,
                Defense = EffectiveDefense,
                TakenIncrease = TakenIncreaseTotal,
                DamageReduction = DamageReductionTotal,
            };
            int damage = DamageFormula.ToPlayer(hit, _rules);
            int dealt = SurviveLethal(_shields.Absorb(damage)); // 피해 공식을 다 계산한 뒤 보호막부터 깎음 (불괴 호패는 그 뒤)
            Health = Mathf.Max(0f, Health - dealt);
            OnHurtForShield(damage);
            OnHurtForItems();
            OnHurtForMartialItems();

            _invulnerableTimer = Mathf.Max(_invulnerableTimer, _hurtIframe);
            _stunTimer = _hurtStun;
            Vector3 away = transform.position - source;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = -_facing;
            _knockbackVelocity = away.normalized * ((knockback >= 0f ? knockback : _hurtKnockback) / Mathf.Max(0.01f, _hurtStun));

            _combo.Cancel();
            CancelPotion(); // 맞으면 금창약도 끊겨요 (소모되지 않음)
            _skillElapsed = -1f; // 맞으면 회선참도 끊겨요 (쿨타임은 그대로 돎)
            _velocity = Vector3.zero;
            _lungeRemaining = 0f;
            _dashAttackTimer = 0f;
            Gauge = Mathf.Min(_gaugeMax, Gauge + _gaugeHurt * GaugeGainMultiplier);

            _flashTimer = 0.12f;
            if (dealt > 0) DamagePopups.Spawn(transform.position + Vector3.up * 2f, dealt, PopupStyle.Player); // 보호막이 다 막으면 숫자 없음
            Hurt?.Invoke(dealt);
            if (TryRevive()) return true;
            // 열화 정예: 맞을 때마다 화상 (화살·폭발처럼 주인이 있는 공격 포함)
            if (attacker != null && attacker.TryGetComponent(out AThousandDawns.Enemies.EnemyBrain brain) && brain.IsBlaze)
                for (int i = Mathf.RoundToInt(GameConfig.Get("elite.blaze_stacks", 1f)); i > 0; i--) AddBurnStack(GameConfig.Get("player_burn.damage", 2f));
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

        // 만검귀종 (하단전): 1초 동안 방 하나 크기(20×12칸, 나를 가운데로) 안 적에게 검 8자루가 고르게 떨어져요. 기세를 모두 써요.
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

            var hit = PlayerHit(_ultimateSwordDamage);
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            if (K(Keyword.Mado) >= 4) hit.SpecialMultiplier *= _madoImprintPower; // #마도 각인: 위력 ×1.6
            _ultimateEndPending = true;
            swordRain.Begin(transform.position, _ultimateArea, _ultimateSwordCount, _ultimateCastTime, _ultimateFallTime, hit, _rules);
        }

        // 시험판용: G 키로 기세를 가득 채워요.
        public void FillGauge() => Gauge = _gaugeMax;

        // 정비실을 나갈 때: 목인장에 쓴 기세를 들어올 때 값으로 되돌리고 회선참 쿨타임을 비워요.
        public void RestoreAfterTraining(float gauge)
        {
            Gauge = Mathf.Clamp(gauge, 0f, _gaugeMax);
            _skillCooldownLeft = 0f;
        }

        // 회선참: 주변 원형(반경 2.2칸)을 한 바퀴 베요. 시전 0.35초, 쿨타임은 누르는 순간부터.
        void StartSkill()
        {
            _sinceSkillPress = float.PositiveInfinity;
            _skillCooldownLeft = SkillCooldown;
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
            OnSkillStartedForItems(); // 연마석 · 수호 노리개
        }

        void TickSkill(float dt)
        {
            _skillElapsed += dt;
            if (Skill == SkillKind.BambooFlash)
            {
                TickBambooFlash();
                return;
            }
            if (Skill == SkillKind.Quake)
            {
                TickQuake();
                return;
            }
            if (!_skillHitDone && _skillElapsed >= _skillHitTime)
            {
                _skillHitDone = true;
                _hitThisSwing.Clear();
                if (swingFlash != null) swingFlash.Show(transform.position, _facing, _skillRadius, 360f, 0.15f);
                var hit = PlayerHit(_skillDamage);
                hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
                int count = HitSector(_skillRadius, 360f, hit, HitTag.Skill);
                Gauge = Mathf.Min(_gaugeMax, Gauge + count * _gaugeSkill * GaugeGainMultiplier);

                // #뇌정 2단계: 초식이 적중하면 처음 맞은 적부터 연쇄 번개 (발동 공격 = 초식 피해, 강인함 빼기 전)
                int thunder = _keywords.Count(Keyword.Noejeong);
                if (count > 0 && thunder >= 2)
                {
                    float trigger = DamageFormula.BeforeToughness(hit);
                    var first = _firstHit;
                    Lightning.Chain(first, transform.position + Vector3.up, trigger, ChainRatio(thunder), _chainTargets, _chainRange, DischargePerStack(thunder));
                    StrikeAfterSkillChain(first, trigger, thunder); // 뇌운 부적
                }
            }
        }

        // ── 키워드 단계 (상태이상 탭). 기준 피해 = 지금 평타 1타 피해 (강인함·방어력 빼기 전)

        float BaseDamage => _basicDamage * (1f + AttackIncreaseTotal); // 공격 증가(②)를 받은 평타 1타
        float BurnPerStackDps(int fire) => BaseDamage * _burnPerStackRatio * (fire >= 4 ? 1f + _burnStage4Bonus : 1f);
        float DischargePerStack(int thunder) => BaseDamage * _dischargePerStack * (thunder >= 4 ? 1f + _dischargeStage4Bonus : 1f);
        float ChainRatio(int thunder) => thunder >= 6 ? _chainStage6Ratio : _chainRatio;

        // 2단계: 평타 3타째·초식·대시 공격이 적중하면 화상 1중첩 (#열양), 둔화 1중첩 (#빙백). 평타 3타째는 감전 1중첩 (#뇌정).
        void ApplyKeywordsOnHit(Damageable target, HitTag tag)
        {
            if (target.IsDead || (tag != HitTag.Finisher && tag != HitTag.Skill && tag != HitTag.DashAttack)) return;
            var status = StatusEffects.For(target);
            int fire = _keywords.Count(Keyword.Yeolyang), frost = _keywords.Count(Keyword.Bingbaek), thunder = _keywords.Count(Keyword.Noejeong);
            if (fire >= 2) status.AddBurn(BurnPerStackDps(fire), fire >= 6 ? _burnStage6Extra : 0);
            if (frost >= 2) status.AddSlow(frost >= 4 ? _slowStage4Bonus : 0f, frost >= 6);
            if (thunder >= 2 && tag == HitTag.Finisher) status.AddShock();
        }

        // 절초 각인 (키워드 4단계, 무기 탭): 검이 떨어질 때마다
        void OnSwordLanded(Damageable target, Vector3 point)
        {
            int fire = _keywords.Count(Keyword.Yeolyang), frost = _keywords.Count(Keyword.Bingbaek);
            if (fire >= 4)
            {
                // #열양: 맞은 적의 화상이 최대 중첩까지 바로 참 + 떨어진 자리에 불길 4초
                if (target != null && !target.IsDead) StatusEffects.For(target).FillBurn(BurnPerStackDps(fire), fire >= 6 ? _burnStage6Extra : 0);
                FirePatch.Create(point, _firePatchRadius, _firePatchDuration, _burnTick);
            }
            // #빙백: 맞은 적이 즉시 빙결 (보스가 빙결 면역이면 대신 둔화 3중첩)
            if (frost >= 4 && target != null && !target.IsDead) StatusEffects.For(target).ImprintFreeze(_slowStage4Bonus);
        }

        // #뇌정 각인: 절초가 끝나는 순간 연쇄 번개 4번 한꺼번에. 번개를 일으키는 피해 = 절초 피해 합 × 10%
        // 뇌정 방울: 절초 연쇄 번개 +2회 (각인이 없어도 그 횟수만큼)
        void OnSwordRainFinished(int totalDamage)
        {
            int thunder = _keywords.Count(Keyword.Noejeong);
            int chains = (thunder >= 4 ? _thunderImprintChains : 0) + BellChains;
            if (chains <= 0 || totalDamage <= 0) return;
            var starts = new List<Damageable>();
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                if (!d.IsDead && d.Targetable && SwordRain.InArea(transform.position, _ultimateArea, d)) starts.Add(d); // 절초 범위(방 하나) 안에서
            if (starts.Count == 0) return;
            starts.Sort((a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
            for (int i = 0; i < chains; i++)
                Lightning.Chain(starts[i % starts.Count], transform.position + Vector3.up * 3f, totalDamage * _thunderImprintRatio, ChainRatio(thunder), _chainTargets, _chainRange, DischargePerStack(thunder));
        }

        public PlayerKeywords Keywords => _keywords;

        // 베는 동안 몸을 한 바퀴 돌려요 (시험판용 표시). 시전이 끝나면 원래 방향.
        void ApplySkillSpin()
        {
            if (Skill == SkillKind.Quake && _skillElapsed < _skillCastTime) return; // 붕산권은 돌지 않아요
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
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, DashIframe);
            _dashIframeTimer = DashIframe; // 간파 판정 시간
            _dashStart = transform.position;
            _perfectDodgeUsed = false;
            _sinceDashPress = float.PositiveInfinity;
            _lungeRemaining = 0f;
            OnDashStartedForItems(); // 버들잎 표창 · 잔영 가면 · 구름 비단
        }

        float DashDistance => _dashDistance * (1f + V("leggings", 1)) * Mathf.Max(0.1f, 1f - Side("dash_distance")); // 각반 · 기물 부작용

        void UpdateDash(float dt)
        {
            float step = Mathf.Min(dt, _dashTimer);
            _controller.Move(_dashDirection * (DashDistance / _dashDuration) * step);
            _dashTimer -= dt;
            _velocity = Vector3.zero;
            ApplyFacing();
            TouchDuringDash(); // 구름 비단
            if (_dashTimer > 0f) return;
            _dashAttackTimer = _dashAttackWindow;
            OnDashEndedForItems();
        }

        void UpdateMovement(float dt)
        {
            Vector2 move = Vector2.ClampMagnitude(_input.Move, 1f);
            bool attacking = _combo.IsAttacking || IsCastingSkill;
            float speed = _moveSpeed * (attacking ? _attackMoveRatio : IsDrinking ? _potionMove : 1f) * (1f - SlowRatio) * (1f + MoveSpeedBonus);
            Vector3 desired = new Vector3(move.x, 0f, move.y) * speed;
            float rate = desired.sqrMagnitude >= _velocity.sqrMagnitude ? _moveSpeed / Mathf.Max(0.001f, _accelTime) : _moveSpeed / Mathf.Max(0.001f, _decelTime);
            _velocity = Vector3.MoveTowards(_velocity, desired, rate * dt);

            Vector3 displacement = _velocity * dt;
            if (_lungeRemaining > 0f)
            {
                float lungeStep = Mathf.Min(_lungeRemaining, _finisherLunge / Mathf.Max(0.01f, _finisherActive) * AttackSpeedMultiplier * dt);
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

        // 3타째와 검보 조각의 4타째는 마무리 타 (3타째 범위·돌진·키워드·기물 효과를 같이 받아요)
        void OnHitFrame(int index)
        {
            bool extra = _combo.IsExtraHit(index);
            bool finisher = index >= _combo.BaseHitCount - 1;
            if (finisher) _lungeRemaining = _finisherLunge;
            float radius = _hitRadius[Mathf.Min(index, _hitRadius.Length - 1)] * BasicRangeMultiplier(finisher) * ForgeRangeMultiplier; // 대장간 강기
            float angle = _hitAngle[Mathf.Min(index, _hitAngle.Length - 1)];
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, radius, angle, _combo.HitCount > 0 ? 0.1f : 0f);
            var hit = PlayerHit(finisher ? _finisherDamage : _basicDamage);
            hit.AttackIncrease += BasicSwingBonus(finisher, extra);
            int count = HitSector(radius, angle, hit, finisher ? HitTag.Finisher : HitTag.Basic);
            Gauge = Mathf.Min(_gaugeMax, Gauge + count * (finisher ? _gaugeFinisher : _gaugeBasic) * GaugeGainMultiplier);
            FireGanggiOnSwing(finisher, hit);
            AfterBasicSwing(count, hit);
            if (finisher) ForgeAfterFinisher(count); // 대장간 금강
        }

        void DoDashAttack()
        {
            _hitThisSwing.Clear();
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, _dashAttackRadius, _dashAttackAngle, 0.12f);
            var hit = PlayerHit(_dashAttackDamage * (1f + RunDashAttackBonus)); // 면벽수련 쾌 10
            hit.AttackIncrease += DashAttackBonus; // 비연 깃털 · 운룡 허리띠
            int count = HitSector(_dashAttackRadius, _dashAttackAngle, hit, HitTag.DashAttack);
            Gauge = Mathf.Min(_gaugeMax, Gauge + count * _gaugeDashAttack * GaugeGainMultiplier);
            LeaveFrostTrail(); // 한철 비녀
            AfterDashAttack(count, hit);
            ForgeAfterDashAttack(count); // 대장간 경공
        }

        // template의 피해·배율을 그대로 쓰고, 대상마다 강인함·방어력·상황 배율(방패 정면 등)만 채워요.
        int HitSector(float radius, float angle, HitDamageInput template, HitTag tag)
        {
            int found = Physics.OverlapSphereNonAlloc(transform.position, radius + 1f, _overlap, ~0, QueryTriggerInteraction.Collide);
            int hits = 0;
            _firstHit = null;
            _swingKills = 0;
            for (int i = 0; i < found; i++)
            {
                var target = _overlap[i].GetComponentInParent<Damageable>();
                if (target == null || target.IsDead || !target.Targetable || _hitThisSwing.Contains(target)) continue;
                if (!HitShapes.InSector(transform.position, _facing, radius, angle, target.transform.position, target.BodyRadius)) continue;
                _hitThisSwing.Add(target);
                HitTarget(target, template, tag);
                hits++;
            }
            TrackFlint(hits); // 부싯돌
            return hits;
        }

        void HitTarget(Damageable target, HitDamageInput template, HitTag tag)
        {
            var hit = template;
            hit.Toughness = target.Toughness;
            hit.Defense = target.Defense;
            hit.Situational *= target.ResolveSituational(transform.position, tag) * UnburnedMultiplier(target);
            target.ApplyHit(DamageFormula.ToEnemy(hit, _rules));
            if (target.IsDead) _swingKills++;
            ApplyItemsOnHit(target, tag);
            ApplyKeywordsOnHit(target, tag);
            ApplyForgeOnHit(target, hit, tag); // 대장간 열양·빙백·뇌정
            if (_firstHit == null) _firstHit = target;
        }
    }
}
