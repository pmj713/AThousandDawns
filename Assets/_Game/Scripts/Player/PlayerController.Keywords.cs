using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 키워드 5종의 단계 효과와 절초 각인 (키워드 탭, 무기 탭 4번·6번).
    //   #강기: 평타를 멀리, 빠르게 · #금강: 막고, 깨지면 되받아친다 · #경공: 피하면서 싸운다
    //   #마도: 주화입마를 감수하고 힘을 얻음 · #정종: 기를 다스려 안정을 지킴
    // #열양·#빙백·#뇌정(상태이상)은 PlayerController.cs에 있어요.
    public sealed partial class PlayerController
    {
        float _ganggiRatio, _ganggiSpeed, _ganggiRange, _ganggiWidth, _ganggiAttackSpeed, _ganggiImprintTime, _ganggiImprintRatio;
        int _ganggiStage4Count;
        float _geumgangRoomShield, _geumgangHitShieldRatio, _geumgangHitShieldTime, _geumgangHitShieldCooldown;
        float _geumgangShockRatio, _geumgangShockRadius, _shieldCapRatio, _geumgangImprintShield, _geumgangImprintTime;
        int _geumgangToughness;
        int _gyeonggongDashBonus, _baseDashCharges;
        float _gyeonggongLackMove, _gyeonggongLackMoveMax, _perfectBonus4, _perfectBonus6, _perfectWindow, _counterRatio;
        float _madoPerDeviated, _madoMax, _madoImprintPower, _madoImprintTaken, _madoImprintTime;
        float _jeongjongPerCirculating, _jeongjongReduction, _jeongjongPure, _jeongjongImprintGauge;

        readonly PlayerShields _shields = new PlayerShields();
        float _dashIframeTimer, _perfectBonusTimer, _perfectBonusRatio, _hitShieldCooldown, _madoPenaltyTimer, _ganggiImprintTimer;
        bool _perfectDodgeUsed, _ultimateEndPending;

        public PlayerShields Shields => _shields;
        public int ShieldCap => Mathf.RoundToInt(_maxHealth * _shieldCapRatio);
        public bool IsDashInvulnerable => _dashIframeTimer > 0f;
        public float PerfectBonusLeft => Mathf.Max(0f, _perfectBonusTimer);
        public float PerfectBonusRatio => _perfectBonusRatio;
        public float MadoPenaltyLeft => Mathf.Max(0f, _madoPenaltyTimer);
        public float GanggiImprintLeft => Mathf.Max(0f, _ganggiImprintTimer);

        int K(Keyword keyword) => _keywords != null ? _keywords.Count(keyword) : 0;
        int Qi(QiState state) => _keywords != null ? _keywords.Qi(state) : 0;

        void ReadKeywordTuning()
        {
            _baseDashCharges = Mathf.RoundToInt(GameConfig.Get("dash.charges", 1f));

            _ganggiRatio = GameConfig.Get("ganggi.ratio", 0.6f);
            _ganggiSpeed = GameConfig.Get("ganggi.speed", 14f);
            _ganggiRange = GameConfig.Get("ganggi.range", 5f);
            _ganggiWidth = GameConfig.Get("ganggi.width", 0.8f);
            _ganggiAttackSpeed = GameConfig.Get("ganggi.stage2_attack_speed", 0.15f);
            _ganggiStage4Count = Mathf.RoundToInt(GameConfig.Get("ganggi.stage4_count", 1f));
            _ganggiImprintTime = GameConfig.Get("imprint.ganggi_duration", 3f);
            _ganggiImprintRatio = GameConfig.Get("imprint.ganggi_ratio", 0.6f);

            _geumgangRoomShield = GameConfig.Get("geumgang.room_shield", 0.1f);
            _geumgangToughness = Mathf.RoundToInt(GameConfig.Get("geumgang.shield_toughness", 2f));
            _geumgangHitShieldRatio = GameConfig.Get("geumgang.hit_shield_ratio", 0.5f);
            _geumgangHitShieldTime = GameConfig.Get("geumgang.hit_shield_duration", 3f);
            _geumgangHitShieldCooldown = GameConfig.Get("geumgang.hit_shield_cooldown", 6f);
            _geumgangShockRatio = GameConfig.Get("geumgang.shockwave_ratio", 2f);
            _geumgangShockRadius = GameConfig.Get("geumgang.shockwave_radius", 2f);
            _shieldCapRatio = GameConfig.Get("shield.max_ratio", 0.3f);
            _geumgangImprintShield = GameConfig.Get("imprint.geumgang_shield", 0.2f);
            _geumgangImprintTime = GameConfig.Get("imprint.geumgang_duration", 5f);

            _gyeonggongDashBonus = Mathf.RoundToInt(GameConfig.Get("gyeonggong.dash_bonus", 1f));
            _gyeonggongLackMove = GameConfig.Get("gyeonggong.lack_move_per", 0.04f);
            _gyeonggongLackMoveMax = GameConfig.Get("gyeonggong.lack_move_max", 0.2f);
            _perfectBonus4 = GameConfig.Get("gyeonggong.perfect_bonus", 0.3f);
            _perfectBonus6 = GameConfig.Get("gyeonggong.perfect_bonus6", 0.6f);
            _perfectWindow = GameConfig.Get("gyeonggong.perfect_window", 1.5f);
            _counterRatio = GameConfig.Get("gyeonggong.counter_ratio", 2f);

            _madoPerDeviated = GameConfig.Get("mado.per_deviated", 0.04f);
            _madoMax = GameConfig.Get("mado.per_deviated_max", 0.2f);
            _madoImprintPower = GameConfig.Get("imprint.mado_power", 1.6f);
            _madoImprintTaken = GameConfig.Get("imprint.mado_taken", 0.3f);
            _madoImprintTime = GameConfig.Get("imprint.mado_duration", 3f);

            _jeongjongPerCirculating = GameConfig.Get("jeongjong.per_circulating", 0.03f);
            _jeongjongReduction = GameConfig.Get("jeongjong.stage4_reduction", 0.15f);
            _jeongjongPure = GameConfig.Get("jeongjong.stage6_bonus", 0.3f);
            _jeongjongImprintGauge = GameConfig.Get("imprint.jeongjong_gauge", 0.3f);
        }

        void InitKeywords()
        {
            _keywords.Changed += OnKeywordsChanged;
            _shields.Broken += OnShieldBroken;
            OnKeywordsChanged();
        }

        // #경공 2단계: 대시 +1회. 기물이 바뀌어도 키워드 개수가 바뀌어 여기로 와요.
        // 천둥 북 부작용: 대시 횟수 −1 (최소 1)
        void OnKeywordsChanged()
        {
            int bonus = K(Keyword.Gyeonggong) >= 2 ? _gyeonggongDashBonus : 0;
            _dash.SetMaxCharges(_baseDashCharges + bonus + ItemDashBonus - Mathf.RoundToInt(Side("dash_count"))); // + 비연 깃털
            RefreshItems();
        }

        void TickKeywordTimers(float dt)
        {
            _shields.Tick(dt);
            if (_dashIframeTimer > 0f) _dashIframeTimer -= dt;
            if (_perfectBonusTimer > 0f) _perfectBonusTimer -= dt;
            if (_hitShieldCooldown > 0f) _hitShieldCooldown -= dt;
            if (_madoPenaltyTimer > 0f) _madoPenaltyTimer -= dt;
            if (_ganggiImprintTimer > 0f) _ganggiImprintTimer -= dt;
        }

        // ── 피해 배율

        // ② 공격 증가 합 ("모든 피해 +%"): #마도 2단계, #정종 2·6단계
        public float AttackIncreaseTotal
        {
            get
            {
                float increase = 0f;
                if (K(Keyword.Mado) >= 2) increase += Mathf.Min(_madoMax, Qi(QiState.Deviated) * _madoPerDeviated);
                if (K(Keyword.Jeongjong) >= 2) increase += Qi(QiState.Circulating) * _jeongjongPerCirculating;
                if (K(Keyword.Jeongjong) >= 6 && Qi(QiState.Lacking) == 0 && Qi(QiState.Deviated) == 0) increase += _jeongjongPure;
                return increase;
            }
        }

        // #정종 4단계: 주화입마 기물이 0개면 받는 피해 −15%
        float DamageReductionTotal => (K(Keyword.Jeongjong) >= 4 && Qi(QiState.Deviated) == 0 ? _jeongjongReduction : 0f) + ItemDamageReduction; // + 쇠사슬 · 금빛 가사

        // #마도 각인: 절초 사용 후 3초간 받는 피해 +30%
        float TakenIncreaseTotal => (_madoPenaltyTimer > 0f ? _madoImprintTaken : 0f) + Side("taken") + RunTakenIncrease; // + 기물 부작용 + 산신의 노여움

        // #금강 2단계: 보호막이 있는 동안 강인함 +2
        float EffectiveToughness => Toughness + (K(Keyword.Geumgang) >= 2 && _shields.Any ? _geumgangToughness : 0) + ItemToughness; // + 거북 등껍질

        // #강기 2단계: 평타 속도 +15%
        float AttackSpeedMultiplier => (1f + (K(Keyword.Ganggi) >= 2 ? _ganggiAttackSpeed : 0f) + ItemAttackSpeedBonus + RunAttackSpeedBonus) * Mathf.Max(0.1f, 1f - Side("attack_speed")); // + 기물 · × 기물 부작용

        // #경공 2단계: 기 부족 기물 1개당 이동속도 +4% (최대 +20%)
        float MoveSpeedBonus => (K(Keyword.Gyeonggong) >= 2 ? Mathf.Min(_gyeonggongLackMoveMax, Qi(QiState.Lacking) * _gyeonggongLackMove) : 0f) + ItemMoveSpeedBonus - Side("move_speed") + RunMoveBonus; // + 기물 − 기물 부작용 + 산신당 축복

        // 플레이어 공격 한 번의 피해 입력. 간파 보너스는 다음 공격 한 번에 쓰여요.
        HitDamageInput PlayerHit(float baseDamage)
        {
            var hit = HitDamageInput.Of(baseDamage);
            hit.AttackIncrease = AttackIncreaseTotal + ConsumeCloakBonus(); // + 흑풍 망토 주화입마 (대시 후 첫 공격)
            if (_perfectBonusTimer > 0f)
            {
                hit.AttackIncrease += _perfectBonusRatio;
                _perfectBonusTimer = 0f;
            }
            return hit;
        }

        // ── #강기

        // 4단계: 평타 3타째에 강기 1발. 각인: 절초가 끝난 뒤 3초간 모든 평타가 강기 발사 (그 평타 피해의 60%)
        void FireGanggiOnSwing(bool finisher, HitDamageInput swing)
        {
            if (_ganggiImprintTimer > 0f)
            {
                FireGanggi(swing, _ganggiImprintRatio);
                return;
            }
            if (!finisher || K(Keyword.Ganggi) < 4) return;
            for (int i = 0; i < _ganggiStage4Count; i++) FireGanggi(swing, _ganggiRatio);
        }

        // 강기 피해 = 발동 평타 피해(강인함 빼기 전) × 60%. 6단계: 모든 적 관통 + 공력 배율
        void FireGanggi(HitDamageInput swing, float ratio)
        {
            float damage = DamageFormula.BeforeToughness(swing) * ratio;
            bool stage6 = K(Keyword.Ganggi) >= 6;
            if (stage6) damage *= DamageFormula.GongryeokMultiplier(Gongryeok, _rules);
            FireGanggiBolt(damage, 1f);
        }

        // 강기 한 발 (혈수 붕대 파편도). 6단계면 모든 적 관통, 아니면 강기 구슬만큼 관통 (주화입마면 맞은 자리에서 터짐)
        void FireGanggiBolt(float damage, float widthScale)
        {
            int pierce = K(Keyword.Ganggi) >= 6 ? GanggiBolt.PierceAll : C("ganggi_bead", 1);
            bool burst = Deviated("ganggi_bead");
            GanggiBolt.Fire(transform.position + Vector3.up + _facing * 0.5f, _facing, _ganggiSpeed, _ganggiRange, _ganggiWidth * widthScale, damage, pierce,
                burst ? V("ganggi_bead", 2) : 0f, burst ? V("ganggi_bead", 3) : 0f);
        }

        // ── #금강 (보호막)

        // 2단계: 방에 들어갈 때마다 보호막 (최대 체력의 10%). 시험판에선 배치가 새로 나올 때, 결투장이 닫힐 때, 단계를 올릴 때.
        public void EnterRoom()
        {
            _tabletUsed = false; // 불괴 호패: 전투(방)마다 1회
            if (K(Keyword.Geumgang) >= 2) _shields.Add(Mathf.RoundToInt(_maxHealth * _geumgangRoomShield), 0f, ShieldCap);
        }

        // 4단계: 피격하면 받은 피해의 50%만큼 보호막, 3초 (6초에 한 번)
        void OnHurtForShield(int damageTaken)
        {
            if (K(Keyword.Geumgang) < 4 || _hitShieldCooldown > 0f || damageTaken <= 0) return;
            _shields.Add(Mathf.Max(1, Mathf.RoundToInt(damageTaken * _geumgangHitShieldRatio)), _geumgangHitShieldTime, ShieldCap);
            _hitShieldCooldown = _geumgangHitShieldCooldown;
        }

        // 플레이어 쪽 폭발 신호 (아트가 종류마다 다른 폭발을 그려요): 금강 충격파 · 잔상 반격 · 강기 구슬 터짐 · 뇌정 방전
        public static event System.Action<Vector3, float, BurstKind> BurstCue;
        public static void CueBurst(Vector3 at, float radius, BurstKind kind) => BurstCue?.Invoke(at, radius, kind);

        // 6단계: 보호막이 깨질 때 충격파 (반경 2칸, 깨진 보호막 최대치의 200%, 파생 피해)
        void OnShieldBroken(int shieldMax)
        {
            if (K(Keyword.Geumgang) < 6) return;
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, _geumgangShockRadius, 360f, 0.15f);
            CueBurst(transform.position, _geumgangShockRadius, BurstKind.Vajra);
            float raw = shieldMax * _geumgangShockRatio;
            foreach (var d in Damageable.All())
            {
                if (d.IsDead || !d.Targetable) continue;
                Vector3 offset = d.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude > _geumgangShockRadius + d.BodyRadius) continue;
                d.ApplyHit(DamageFormula.Proc(raw, 1f, d.Defense), false, PopupStyle.Burst);
            }
        }

        // ── #경공 (간파)

        // 4단계: 대시로 공격을 피하면 간파 (대시 무적 0.2초 중에 적의 공격 판정이 몸에 닿음). 대시 한 번에 한 번.
        //   대시 1회 즉시 충전 + 1.5초 안 다음 공격 피해 +30%. 6단계: 보너스 +60%, 잔상이 그 적에게 반격 (기준 피해의 200%, 거리 제한 없음)
        void TryPerfectDodge(Damageable attacker)
        {
            int count = K(Keyword.Gyeonggong);
            if (count < 4 || _perfectDodgeUsed) return;
            _perfectDodgeUsed = true;
            _dash.Refill(1);
            _perfectBonusRatio = count >= 6 ? _perfectBonus6 : _perfectBonus4;
            _perfectBonusTimer = _perfectWindow;
            Sfx.Play(SfxId.PerfectDodge);
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, 0.9f, 360f, 0.12f); // 잔상이 번쩍
            CueBurst(transform.position, 0.9f, BurstKind.Echo);

            if (count < 6 || attacker == null || attacker.IsDead || !attacker.Targetable) return;
            attacker.ApplyHit(DamageFormula.Proc(BaseDamage * _counterRatio, 1f, attacker.Defense), false, PopupStyle.Burst);
        }

        // ── 절초 시전과 각인

        // 시전 중이면 true (이번 프레임은 여기서 끝). #경공 각인이면 시전 1초 동안 이동·대시 가능 (이동속도 그대로).
        bool TickUltimateCast(float dt)
        {
            if (!IsCastingUltimate) return false;
            _ultimateTimer -= dt;
            if (_ultimateTimer <= 0f)
            {
                OnUltimateCastEnded();
                return false;
            }
            if (K(Keyword.Gyeonggong) < 4) return true;

            if (IsDashing)
            {
                UpdateDash(dt);
                return true;
            }
            if (_sinceDashPress <= _inputBuffer && TryConsumeDash())
            {
                StartDash();
                UpdateDash(dt);
                return true;
            }
            UpdateMovement(dt);
            return true;
        }

        // 각인 (4단계): #금강 보호막 20% 5초 · #마도 3초간 받는 피해 +30% · #정종 기세 30% 돌려받음 · #강기 3초간 모든 평타가 강기
        void OnUltimateCastEnded()
        {
            if (!_ultimateEndPending) return;
            _ultimateEndPending = false;
            if (K(Keyword.Geumgang) >= 4) _shields.Add(Mathf.RoundToInt(_maxHealth * _geumgangImprintShield), _geumgangImprintTime, ShieldCap);
            if (K(Keyword.Mado) >= 4) _madoPenaltyTimer = _madoImprintTime;
            if (K(Keyword.Jeongjong) >= 4) Gauge = Mathf.Min(GaugeCap, Gauge + _gaugeMax * _jeongjongImprintGauge);
            if (K(Keyword.Ganggi) >= 4) _ganggiImprintTimer = _ganggiImprintTime;
            _noDashTimer = Side("no_dash_after_ultimate"); // 뇌정 방울 부작용
        }
    }
}
