using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 기물 효과와 부작용 (기물 탭 #열양·#빙백·#뇌정 30종, data_items).
    // 상태이상 쪽에 더하는 값은 ElementalMods로 모아 적의 StatusEffects·Lightning이 읽고, 때(적중·피격·처치·절초)마다 생기는 효과는 여기서 걸어요.
    // 부작용은 주화입마인 기물만: 이동속도·공격 속도·받는 피해·초식 쿨타임·대시(회복·거리·횟수)·최대 체력·자신 둔화·기세 감소·절초 뒤 대시 불가·화상 없는 적 피해.
    public sealed partial class PlayerController
    {
        const float GlovesDeviatedRange = 2f;      // 홍염 장갑: 주화입마면 범위 2배 (기물 탭 글)
        const float MirrorDeviatedImmunity = 0.5f; // 한빙 거울: 주화입마면 빙결 면역 시간 절반 (기물 탭 글)

        PlayerItems _items;
        float _baseMaxHealth, _selfSlowRatio, _gaugeDrainInterval, _frostTrailWidth, _strikeRatio;
        float _gaugeDrainTimer, _noDashTimer, _reishiCooldown;
        Damageable _flintTarget;
        int _flintCount;
        Vector3 _dashStart;

        public PlayerItems Items => _items;
        public float NoDashLeft => Mathf.Max(0f, _noDashTimer);

        float V(string id, int index) => _items != null ? _items.Value(id, index) : 0f;
        int C(string id, int index) => _items != null ? _items.Count(id, index) : 0;
        bool Has(string id) => _items != null && _items.Active(id);
        bool Deviated(string id) => _items != null && _items.IsDeviated(id);
        float Side(string kind) => _items != null ? _items.SideTotal(kind) : 0f;
        bool Pure => _items == null || _items.NoDeviated; // #정종 보조 "0개면": 주화입마 기물이 하나도 없을 때
        static bool Roll(float chance) => chance > 0f && UnityEngine.Random.value < chance;

        int BurnExtra(int fire) => fire >= 6 ? _burnStage6Extra : 0;
        float SlowBonus(int frost) => frost >= 4 ? _slowStage4Bonus : 0f;

        void ReadItemTuning()
        {
            _baseMaxHealth = _maxHealth;
            _selfSlowRatio = GameConfig.Get("item.self_slow_ratio", 0.25f);
            _gaugeDrainInterval = GameConfig.Get("item.gauge_drain_interval", 10f);
            _frostTrailWidth = GameConfig.Get("item.frost_trail_width", 1f);
            _strikeRatio = GameConfig.Get("lightning.ratio", 1.5f);
        }

        void InitItems()
        {
            if (!TryGetComponent(out _items)) _items = gameObject.AddComponent<PlayerItems>();
            Damageable.AnyDied += OnAnyEnemyDied;
            StatusEffects.AnyFrozen += OnAnyFrozen;
            RefreshItems();
        }

        void OnDestroy()
        {
            Damageable.AnyDied -= OnAnyEnemyDied;
            StatusEffects.AnyFrozen -= OnAnyFrozen;
            ElementalMods.Current = new ElementalMods();
        }

        // 기물·기·키워드가 바뀔 때마다 (기물이 바뀌면 키워드 개수도 바뀌어 OnKeywordsChanged에서 불러요)
        void RefreshItems()
        {
            if (_items == null) return;
            bool pure = Pure;
            ElementalMods.Current = new ElementalMods
            {
                // #열양
                BurnDurationBonus = pure ? V("censer", 2) : V("censer", 1),
                BurnDamageBonus = V("vermilion_feather", 1),
                BurnMaxBonus = C("vermilion_feather", 2),
                BurnRampPer = V("oil_lamp", 1),
                BurnRampMax = V("oil_lamp", 2),
                BurnRampInterval = Has("oil_lamp") ? V("oil_lamp", 3) : 2f,
                BurnOvercap = C("fire_scale", 1),
                // #빙백
                SlowDurationBonus = V("snow_hood", 1),
                SlowWindupExtra = V("snowflake_crystal", 1) * (pure ? V("snowflake_crystal", 2) : 1f),
                BossSlowBonus = V("ice_beads", 1),
                BossFreezeImmunityReduction = pure ? V("ice_beads", 2) : 0f,
                FreezeDurationBonus = V("icemirror", 1),
                FreezeImmunityMultiplier = Deviated("icemirror") ? MirrorDeviatedImmunity : 1f,
                PatternBan = V("snow_lotus_sachet", 1),
                // #뇌정
                ShockDurationBonus = pure ? V("thunderstone", 2) : V("thunderstone", 1),
                ChainTargetsBonus = C("thunder_drum", 1),
                ChainDamageBonus = pure ? V("thunder_drum", 2) : 0f,
                DischargeMaxBonus = V("thunder_brush", 1),
                DischargeBonus = V("thunder_seal", 1),
                CohesionShockMultiplier = Has("thunder_seal") ? V("thunder_seal", 2) : 1f,
                SplashRatio = V("cloud_flute", 1),
                SplashRadius = V("cloud_flute", 2),
                Reshock = Deviated("hawk_talon") ? C("hawk_talon", 2) : 0,
            };

            // 부작용: 최대 체력 −% · 대시 회복 −%
            _maxHealth = _baseMaxHealth * Mathf.Max(0.1f, 1f - Side("max_hp"));
            Health = Mathf.Min(Health, _maxHealth);
            _dash.RechargeSpeed = Mathf.Max(0.1f, 1f - Side("dash_recharge"));
        }

        void TickItemTimers(float dt)
        {
            if (_noDashTimer > 0f) _noDashTimer -= dt;
            if (_reishiCooldown > 0f) _reishiCooldown -= dt;

            // 뇌정 옥새 부작용: 간격마다 기세 −(최대 비율)
            float drain = Side("gauge_drain");
            if (drain <= 0f)
            {
                _gaugeDrainTimer = 0f;
                return;
            }
            _gaugeDrainTimer += dt;
            if (_gaugeDrainTimer < _gaugeDrainInterval) return;
            _gaugeDrainTimer -= _gaugeDrainInterval;
            Gauge = Mathf.Max(0f, Gauge - _gaugeMax * drain);
        }

        // 뇌정 방울 부작용: 절초 뒤 잠시 대시 불가
        bool TryConsumeDash() => _noDashTimer <= 0f && _dash.TryConsume();

        // ── 적중

        // 키워드 단계 효과보다 먼저 불러요 (한기 호박은 맞기 전에 둔화돼 있던 적만 봐요).
        void ApplyItemsOnHit(Damageable target, HitTag tag)
        {
            if (_items == null || _items.Items.Count == 0 || target.IsDead) return;
            var status = StatusEffects.For(target);
            int fire = K(Keyword.Yeolyang), frost = K(Keyword.Bingbaek);
            switch (tag)
            {
                case HitTag.Basic:
                    // 평타 1·2타 확률: 불씨 반지 화상 · 서리 고리 둔화 · 벽조목 빗 감전
                    if (Roll(V("ember_ring", 1))) status.AddBurn(BurnPerStackDps(fire), BurnExtra(fire));
                    if (Roll(V("frost_ring", 1))) status.AddSlow(SlowBonus(frost), frost >= 6);
                    if (Roll(V("lightningwood_comb", 1))) status.AddShock();
                    break;
                case HitTag.Finisher:
                    // 평타 3타째: 잿불 단검 화상 +1중첩 (주화입마면 +2) · 매 발톱 감전 +1중첩
                    status.AddBurn(BurnPerStackDps(fire), BurnExtra(fire), Deviated("ember_dagger") ? C("ember_dagger", 2) : C("ember_dagger", 1));
                    status.AddShock(C("hawk_talon", 1));
                    break;
                case HitTag.Skill:
                    // 초식 적중: 화염 부채 화상 +1중첩 · 고드름 침 둔화 +1중첩
                    status.AddBurn(BurnPerStackDps(fire), BurnExtra(fire), C("flame_fan", 1));
                    status.AddSlow(SlowBonus(frost), frost >= 6, C("icicle_needle", 1));
                    break;
                case HitTag.DashAttack:
                    // 한기 호박: 대시 공격으로 둔화된 적을 맞히면 빙결 확률
                    if (status.SlowStacks > 0 && Roll(V("cold_amber", 1))) status.TryFreeze();
                    break;
            }
        }

        // 부싯돌: 다른 적을 맞히지 않고 같은 적만 3번 맞히면 화상 +1중첩 (헛친 휘두르기는 세지 않음)
        void TrackFlint(int hits)
        {
            if (hits == 0 || !Has("flint")) return;
            if (hits == 1 && _firstHit == _flintTarget) _flintCount++;
            else
            {
                _flintTarget = hits == 1 ? _firstHit : null;
                _flintCount = hits == 1 ? 1 : 0;
            }
            if (_flintTarget == null || _flintCount < C("flint", 1)) return;
            _flintCount = 0;
            if (_flintTarget.IsDead) return;
            int fire = K(Keyword.Yeolyang);
            StatusEffects.For(_flintTarget).AddBurn(BurnPerStackDps(fire), BurnExtra(fire), C("flint", 2));
        }

        // 주작 깃털 부작용: 화상 없는 적에게 주는 피해 −%
        float UnburnedMultiplier(Damageable target)
        {
            float penalty = Side("unburned_damage");
            if (penalty <= 0f) return 1f;
            bool burning = target.TryGetComponent(out StatusEffects s) && s.BurnStacks > 0;
            return burning ? 1f : Mathf.Max(0f, 1f - penalty);
        }

        // 한철 비녀: 대시 공격 경로(대시를 시작한 곳 → 지금 자리 + 길이)에 서리 길
        void LeaveFrostTrail()
        {
            if (!Has("coldiron_pin")) return;
            int frost = K(Keyword.Bingbaek);
            Vector3 end = transform.position + _facing * V("coldiron_pin", 1);
            FrostTrail.Create(_dashStart, end, _frostTrailWidth, V("coldiron_pin", 2), SlowBonus(frost), frost >= 6);
        }

        // 뇌운 부적: 초식으로 연쇄 번개가 발동하면 첫 대상에게 낙뢰 (발동 공격 × 150%)
        void StrikeAfterSkillChain(Damageable first, float trigger, int thunder)
        {
            int strikes = C("thundercloud_talisman", 1);
            for (int i = 0; i < strikes; i++) Lightning.Strike(first, trigger, _strikeRatio, DischargePerStack(thunder));
        }

        // 뇌정 방울: 절초 사용 시 연쇄 번개 +2회 (주화입마면 2배)
        int BellChains => Has("thunder_bell") ? C("thunder_bell", 1) * (Deviated("thunder_bell") ? C("thunder_bell", 2) : 1) : 0;

        // ── 피격 (보호막에 다 막혀도 맞은 것)

        void OnHurtForItems()
        {
            if (_items == null || _items.Items.Count == 0) return;

            // 홍염 장갑: 주변 적에게 화상 1중첩 (주화입마면 2중첩, 범위 2배)
            if (Has("crimson_gloves"))
            {
                bool deviated = Deviated("crimson_gloves");
                float radius = _items.Value("crimson_gloves", 1, false) * (deviated ? GlovesDeviatedRange : 1f);
                int stacks = deviated ? C("crimson_gloves", 3) : C("crimson_gloves", 2);
                int fire = K(Keyword.Yeolyang);
                float dps = BurnPerStackDps(fire);
                int extra = BurnExtra(fire);
                ForEachEnemyNear(transform.position, radius, d => StatusEffects.For(d).AddBurn(dps, extra, stacks));
            }

            // 설산 영지: 주변 적 빙결 (재사용 대기마다 한 번, 주변에 적이 없으면 아끼기). 반경 = 기본 + 레벨당 증가
            if (Has("snow_reishi") && _reishiCooldown <= 0f)
            {
                float radius = V("snow_reishi", 2) + V("snow_reishi", 1);
                float freeze = V("snow_reishi", 4);
                if (ForEachEnemyNear(transform.position, radius, d => StatusEffects.For(d).TryFreeze(freeze)) > 0)
                {
                    _reishiCooldown = V("snow_reishi", 3);
                    if (swingFlash != null) swingFlash.Show(transform.position, _facing, radius, 360f, 0.15f);
                }
            }

            // 설련 향낭 부작용: 피격 시 자신도 둔화
            float selfSlow = Side("self_slow");
            if (selfSlow > 0f) ApplySlow(_selfSlowRatio, selfSlow);
        }

        // ── 적이 쓰러짐 · 빙결됨

        void OnAnyEnemyDied(Damageable dead)
        {
            if (_items == null || _items.Items.Count == 0 || IsDead) return;

            // 염화 호리병: 화상 중인 적이 죽은 자리에 불길. 안에 있는 적은 간격마다 화상 1중첩 (0개면 불길 크기 +50%)
            if (Has("fire_gourd") && dead.TryGetComponent(out StatusEffects deadStatus) && deadStatus.BurnStacks > 0)
            {
                int fire = K(Keyword.Yeolyang);
                float dps = BurnPerStackDps(fire);
                int extra = BurnExtra(fire);
                float radius = V("fire_gourd", 2) * (Pure ? 1f + V("fire_gourd", 3) : 1f);
                FirePatch.Create(dead.transform.position, radius, V("fire_gourd", 1), V("fire_gourd", 4), s => s.AddBurn(dps, extra));
            }

            // 구리 동전: 처치 시 연쇄 번개 (쓰러진 자리에서 가장 가까운 적부터, 발동 공격 = 기준 피해)
            if (Roll(V("copper_coin", 1)))
            {
                int thunder = K(Keyword.Noejeong);
                Lightning.Chain(dead, dead.transform.position + Vector3.up, BaseDamage, ChainRatio(thunder), _chainTargets, _chainRange, DischargePerStack(thunder));
            }
        }

        // 한기 호박 주화입마: 빙결될 때 주변 적 둔화 2중첩 (퍼진 둔화로는 빙결되지 않음)
        void OnAnyFrozen(StatusEffects frozen)
        {
            if (!Deviated("cold_amber") || frozen.Body == null) return;
            int stacks = C("cold_amber", 3);
            float bonus = SlowBonus(K(Keyword.Bingbaek));
            var body = frozen.Body;
            ForEachEnemyNear(body.transform.position, V("cold_amber", 2), d =>
            {
                if (d != body) StatusEffects.For(d).AddSlow(bonus, false, stacks);
            });
        }

        // 반경 안의 (살아 있고 맞힐 수 있는) 적마다. 몇 명이었는지 돌려줘요.
        static int ForEachEnemyNear(Vector3 center, float radius, System.Action<Damageable> action)
        {
            if (radius <= 0f) return 0;
            int count = 0;
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (d.IsDead || !d.Targetable) continue;
                Vector3 offset = d.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > radius + d.BodyRadius) continue;
                action(d);
                count++;
            }
            return count;
        }
    }
}
