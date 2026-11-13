using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 대장간 융합 검 효과 (무기 탭 95~109행). 홈 하나 = 기본, 두 홈이 같은 키워드 = 강화판 (▶).
    //   열양: 평타로 화상 중인 적을 맞히면 화상 시간 +1초 (최대 8초) ▶ 화상 피해 간격 0.5 → 0.4초
    //   빙백: 빙결된 적을 3타로 깨면 빙결 면역 −3초, 2칸 안의 적에게 둔화 2중첩 ▶ −5초, 3중첩
    //   뇌정: 감전 최대 중첩인 적을 3타로 맞히면 3타 피해 150%의 낙뢰 ▶ 낙뢰 3번
    //   강기: 평타 범위 +30% ▶ +60%
    //   경공: 대시 공격이 적중하면 대시 1회 바로 회복 ▶ 2회
    //   금강: 3타 적중마다 최대 체력 2% 보호막 (융합 보호막 최대 10%) ▶ 3% (최대 15%)
    public sealed partial class PlayerController
    {
        // 검 융합 효과 (비도를 들면 꺼지고 비도 융합 효과가 대신 붙어요: PlayerController.ForgeDagger)
        int Forge(Keyword keyword) => _items != null && !UsesDagger ? _items.ForgeLevel(keyword) : 0;
        int DaggerForge(Keyword keyword) => _items != null && UsesDagger ? _items.ForgeLevel(keyword) : 0;
        public float ForgeRangeForTest => ForgeRangeMultiplier; // 테스트용

        static float F(string key, float fallback) => GameConfig.GetOptional(key, fallback);

        // 강기
        float ForgeRangeMultiplier => Forge(Keyword.Ganggi) switch
        {
            0 => 1f,
            1 => 1f + F("forge.ganggi_range", 0.3f),
            _ => 1f + F("forge.ganggi_range_strong", 0.6f),
        };

        // 열양 강화판
        float ForgeBurnTick => Forge(Keyword.Yeolyang) >= 2 ? F("forge.yeolyang_tick_strong", 0.4f) : 0f;

        void ApplyForgeOnHit(Damageable target, HitDamageInput hit, HitTag tag)
        {
            if (target == null || target.IsDead || (tag != HitTag.Basic && tag != HitTag.Finisher)) return;
            var status = StatusEffects.For(target);

            // 열양: 평타가 화상 중인 적을 맞히면 화상 시간 +
            if (Forge(Keyword.Yeolyang) > 0)
                status.ExtendBurn(F("forge.yeolyang_extend", 1f), F("forge.yeolyang_cap", 8f));

            if (tag != HitTag.Finisher) return;

            // 빙백: 3타로 빙결을 깸
            int frost = Forge(Keyword.Bingbaek);
            if (frost > 0 && status.BreakFreeze(frost >= 2 ? F("forge.bingbaek_immunity_cut_strong", 5f) : F("forge.bingbaek_immunity_cut", 3f)))
            {
                int stacks = Mathf.RoundToInt(frost >= 2 ? F("forge.bingbaek_slow_strong", 3f) : F("forge.bingbaek_slow", 2f));
                float radius = F("forge.bingbaek_radius", 2f);
                foreach (var d in Damageable.All())
                {
                    if (d == target || d.IsDead || !d.Targetable) continue;
                    if ((d.transform.position - target.transform.position).sqrMagnitude > radius * radius) continue;
                    StatusEffects.For(d).AddSlow(0f, false, stacks);
                }
            }

            // 뇌정: 감전 최대 중첩이면 낙뢰
            int thunder = Forge(Keyword.Noejeong);
            if (thunder > 0 && status.ShockStacks >= status.ShockMax)
            {
                float trigger = DamageFormula.BeforeToughness(hit);
                float ratio = F("forge.noejeong_ratio", 1.5f);
                int strikes = thunder >= 2 ? Mathf.RoundToInt(F("forge.noejeong_strikes_strong", 3f)) : 1;
                float perStack = DischargePerStack(_keywords.Count(Keyword.Noejeong));
                for (int i = 0; i < strikes && !target.IsDead; i++) Lightning.Strike(target, trigger, ratio, perStack);
            }
        }

        // 금강: 3타가 하나라도 맞으면 보호막
        void ForgeAfterFinisher(int hits)
        {
            int level = Forge(Keyword.Geumgang);
            if (level <= 0 || hits <= 0) return;
            float ratio = level >= 2 ? F("forge.geumgang_shield_strong", 0.03f) : F("forge.geumgang_shield", 0.02f);
            float capRatio = level >= 2 ? F("forge.geumgang_cap_strong", 0.15f) : F("forge.geumgang_cap", 0.1f);
            int cap = Mathf.Min(ShieldCap, Mathf.RoundToInt(_maxHealth * capRatio));
            _shields.Add(Mathf.Max(1, Mathf.RoundToInt(_maxHealth * ratio)), F("forge.geumgang_duration", 0f), cap);
        }

        // 경공: 대시 공격이 맞으면 대시 회복
        void ForgeAfterDashAttack(int hits)
        {
            int level = Forge(Keyword.Gyeonggong);
            if (level <= 0 || hits <= 0) return;
            _dash.Refill(level >= 2 ? Mathf.RoundToInt(F("forge.gyeonggong_refill_strong", 2f)) : Mathf.RoundToInt(F("forge.gyeonggong_refill", 1f)));
        }
    }
}
