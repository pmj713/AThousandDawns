using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 기물 꾸러미 1·2 (회귀록 견문 가지, 12종). 검·비도 어느 쪽이든 효과가 같아요.
    //   꾸러미 1: 재 항아리 · 한기 구슬 · 피뢰침 · 결살 검결 · 철포삼 조끼 · 그림자 보법서
    //   꾸러미 2: 숯 주머니 · 뇌격 장갑 · 서리 갑옷 · 호신 부적 · 일섬 검결 · 천리 신발
    // 한기 구슬은 ElementalMods.SlowExtraChance (RefreshItems)에서, 나머지는 여기서 걸어요.
    public sealed partial class PlayerController
    {
        const float ShadowStepCooldown = 0.3f; // 그림자 보법서: 공격 하나가 여러 프레임 닿아도 한 번만

        bool _shadowReady, _gloveChained, _ilseomReady, _ilseomNow;
        float _shadowCooldown;
        int _ilseomSwings;

        public bool IlseomReady => _ilseomReady;
        public bool ShadowStepReady => _shadowReady;

        void TickPackItemTimers(float dt)
        {
            if (_shadowCooldown > 0f) _shadowCooldown -= dt;
        }

        // ── 대상마다 곱하는 피해 (HitTarget, 맞기 전 상태로 봐요)

        float PackTargetMultiplier(Damageable target, HitTag tag)
        {
            if (_items == null || _items.Items.Count == 0) return 1f;
            float mult = 1f;
            // 결살 검결: 체력이 적은 적에게 평타 피해 +
            if ((tag == HitTag.Basic || tag == HitTag.Finisher) && Has("finishing_edge") && target.MaxHealth > 0f
                && target.Health <= target.MaxHealth * V("finishing_edge", 2))
                mult *= 1f + V("finishing_edge", 1);
            // 숯 주머니: 초식으로 맞힌 적의 화상 1중첩당 피해 +
            if (tag == HitTag.Skill && Has("charcoal_bag") && target.TryGetComponent(out StatusEffects s) && s.BurnStacks > 0)
                mult *= 1f + s.BurnStacks * V("charcoal_bag", 1);
            return mult;
        }

        // ── 적중 (ApplyItemsOnHit에서)

        void ApplyPackOnHit(Damageable target, HitTag tag, StatusEffects status)
        {
            // 피뢰침: 감전이 많이 쌓인 적을 맞히면 연쇄 번개 (0개면 확률 2배)
            if (Has("lightning_rod") && status.ShockStacks >= C("lightning_rod", 3)
                && Roll(V("lightning_rod", 1) * (Pure ? V("lightning_rod", 2) : 1f)))
                ChainFrom(target);

            if (tag != HitTag.DashAttack) return;
            // 뇌격 장갑: 대시 공격 적중 시 감전 + (주화입마면 대시 공격 한 번에 연쇄 번개 한 번)
            status.AddShock(C("thunder_glove", 1));
            if (Deviated("thunder_glove") && !_gloveChained)
            {
                _gloveChained = true;
                ChainFrom(target);
            }
        }

        void ChainFrom(Damageable target)
        {
            int thunder = K(Keyword.Noejeong);
            Lightning.Chain(target, target.transform.position + Vector3.up, BaseDamage, ChainRatio(thunder), _chainTargets, _chainRange, DischargePerStack(thunder));
        }

        void AfterDashAttackForPack() => _gloveChained = false;

        // ── 평타

        // 그림자 보법서 (피한 뒤 다음 평타) · 일섬 검결 (평타 5회마다 다음 평타)
        float PackSwingBonus()
        {
            float bonus = 0f;
            if (_shadowReady)
            {
                _shadowReady = false;
                bonus += V("shadow_step", 1);
            }
            _ilseomNow = false;
            if (!Has("sword_scroll"))
            {
                _ilseomSwings = 0;
                _ilseomReady = false;
                return bonus;
            }
            if (_ilseomReady)
            {
                _ilseomReady = false;
                _ilseomNow = true;
                bonus += V("sword_scroll", 1);
            }
            else if (++_ilseomSwings >= C("sword_scroll", 2))
            {
                _ilseomSwings = 0;
                _ilseomReady = true;
            }
            return bonus;
        }

        // 일섬: 앞으로 길게 베요 (평타를 이미 맞은 적은 빼고). 평타 판정이 끝난 뒤 불러요.
        void FireIlseom(HitDamageInput swing)
        {
            if (!_ilseomNow) return;
            _ilseomNow = false;
            float length = V("sword_scroll", 3), width = V("sword_scroll", 4);
            if (swingFlash != null) swingFlash.ShowRect(transform.position, _facing, length, width, 0.12f);
            HitPath(transform.position, transform.position + _facing * length, width * 0.5f, swing, HitTag.Basic);
        }

        // ── 스탯

        // 철포삼 조끼: 보호막이 있으면 강인함 +
        float PackToughness => _shields.Any ? V("iron_vest", 1) : 0f;

        // 천리 신발
        int PackDashBonus => C("cloud_boots", 2);
        float PackDashAttackBonus => V("cloud_boots", 1);

        // ── 대시로 피함 · 피격

        // 그림자 보법서: 대시 무적 중에 적 공격이 닿음 → 기세 + · 다음 평타 피해 +
        void OnDashDodgedForPack()
        {
            if (!Has("shadow_step") || _shadowCooldown > 0f) return;
            _shadowCooldown = ShadowStepCooldown;
            _shadowReady = true;
            Gauge = Mathf.Min(GaugeCap, Gauge + _gaugeMax * V("shadow_step", 2));
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, 0.9f, 360f, 0.12f);
        }

        // 서리 갑옷: 때린 적에게 둔화 (0개면 빙결). 화살처럼 때린 적을 모르면 없어요.
        void OnHurtByForPack(Damageable attacker)
        {
            if (!Has("frost_armor") || attacker == null || attacker.IsDead) return;
            var status = StatusEffects.For(attacker);
            if (Pure) status.TryFreeze(V("frost_armor", 3));
            else status.AddSlow(SlowBonus(K(Keyword.Bingbaek)), false, C("frost_armor", 1) + C("frost_armor", 2));
        }

        // 철포삼 조끼 주화입마: 보호막이 깨질 때 충격파
        void OnShieldBrokenForPack(int shieldMax)
        {
            if (!Deviated("iron_vest")) return;
            float raw = BaseDamage * (1f + V("iron_vest", 2));
            float radius = V("iron_vest", 3);
            ForEachEnemyNear(transform.position, radius, d => d.ApplyHit(DamageFormula.Proc(raw, 1f, d.Defense), false, PopupStyle.Burst));
            if (swingFlash != null) swingFlash.Show(transform.position, _facing, radius, 360f, 0.15f);
        }

        // ── 방 · 처치

        // 호신 부적: 방을 비울 때마다 깨질 때까지 남는 보호막 + (모든 보호막을 합쳐 최대치까지)
        public void OnRoomCleared()
        {
            if (!Has("guard_talisman") || IsDead) return;
            int cap = Mathf.Min(ShieldCap, Mathf.RoundToInt(_maxHealth * V("guard_talisman", 2)));
            _shields.Add(Mathf.Max(1, Mathf.RoundToInt(_maxHealth * V("guard_talisman", 1))), 0f, cap);
        }

        // 재 항아리: 화상이 많이 쌓인 적이 죽으면 중첩 일부가 주변 적에게 옮겨붙어요
        void OnEnemyDiedForPack(Damageable dead)
        {
            if (!Has("ash_urn") || !dead.TryGetComponent(out StatusEffects deadStatus)) return;
            int stacks = deadStatus.BurnStacks;
            if (stacks < C("ash_urn", 2)) return;
            int spread = Mathf.Max(1, Mathf.FloorToInt(stacks * V("ash_urn", 1)));
            int fire = K(Keyword.Yeolyang);
            float dps = BurnPerStackDps(fire);
            int extra = BurnExtra(fire);
            ForEachEnemyNear(dead.transform.position, V("ash_urn", 3), d =>
            {
                if (d != dead) StatusEffects.For(d).AddBurn(dps, extra, spread);
            });
        }
    }
}
