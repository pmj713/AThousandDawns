using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 진전 (기연 방에서 낮은 확률): 그 판 동안 절초 모양이 바뀌어요. 판을 새로 시작하면 사라져요.
    //   검 (만검귀종): 일검 · 검진 · 귀원      비도 (만천화우): 폭우 · 산화 · 회류
    // 지금 무기와 맞지 않는 진전이면 보통 절초예요. 절초 각인 (키워드 4단계)은 그대로 붙어요.
    public sealed partial class PlayerController
    {
        public UltimateForm UltimateForm { get; set; }

        // 절초 경지 0 하단전 · 1 중단전 · 2 상단전 (판 안에서 지역 보스를 넘을 때마다, RoomRun이 정해요)
        public int UltimateStage { get; set; }
        public string UltimateStageName => UltimateStage >= 2 ? "상단전" : UltimateStage == 1 ? "중단전" : "하단전";

        static string StageKey(string key, int stage) => stage == 0 ? key : $"{key}_s{stage}";

        public UltimateForm ActiveUltimateForm =>
            UltimateForm != UltimateForm.None && UltimateForms.ForDagger(UltimateForm) == UsesDagger ? UltimateForm : UltimateForm.None;

        static float J(string key, float fallback) => GameConfig.GetOptional(key, fallback);

        // StartUltimate에서: 무기·진전에 맞춰 검(비도) 수·피해·범위를 정하고 내리기 시작해요
        void BeginUltimateRain(HitDamageInput hit)
        {
            var form = ActiveUltimateForm;
            // 경지 (무기 시트 51~66행): 하단전 → 중단전 (검 12자루 + 검흔 · 비도 40발 5초) → 상단전 (검 16 · 비도 50 + 마무리 100)
            int stage = Mathf.Clamp(UltimateStage, 0, 2);
            if (UsesDagger) // 만천화우: 진짜 비도를 내 둘레로 던져요 (PlayerController.Dagger.cs)
            {
                int knives = Mathf.RoundToInt(D(StageKey("knife.ultimate_count", stage), stage == 0 ? 24f : stage == 1 ? 40f : 50f));
                float range = D("knife.ultimate_range", 10f);
                if (form == UltimateForm.Storm) // 폭우: 더 많이, 더 멀리, 한 발은 약하게
                {
                    knives = Mathf.RoundToInt(knives * J("jinjeon.storm_count", 1.5f));
                    hit.SpecialMultiplier *= J("jinjeon.storm_damage", 0.7f);
                    range *= J("jinjeon.storm_area", 1.3f);
                }
                float finale = stage >= 2 ? J("ultimate.finale_damage", 100f) / Mathf.Max(1f, D("knife.ultimate_damage", 6f)) : 0f;
                BeginDaggerRain(hit, knives, D(StageKey("knife.ultimate_time", stage), stage == 0 ? 3f : 5f), range, form, finale);
                return;
            }
            int count = stage == 0 ? _ultimateSwordCount : Mathf.RoundToInt(J(StageKey("ultimate.sword_count", stage), stage == 1 ? 12f : 16f));
            float time = _ultimateCastTime;
            Vector2 area = _ultimateArea;
            swordRain.ScarRatio = stage >= 1 ? J("ultimate.scar_ratio", 0.25f) : 0f; // 검흔: 0.5초마다 한 자루의 25% × 4번 (한 적에 한 번)
            swordRain.FinaleRatio = stage >= 2 ? J("ultimate.finale_damage", 100f) / Mathf.Max(1f, J("ultimate.sword_damage", 18f)) : 0f;
            if (form == UltimateForm.OneSword) // 큰 검 한 자루 = 원래 검 모두 × 배율
            {
                hit.SpecialMultiplier *= count * J("jinjeon.one_sword_ratio", 1.2f);
                count = 1;
            }
            swordRain.Form = form;
            swordRain.Owner = transform;
            swordRain.OneSwordRadius = J("jinjeon.one_sword_radius", 3f);
            swordRain.OrbitTime = J("jinjeon.formation_time", 3f);
            swordRain.OrbitRatio = J("jinjeon.formation_ratio", 0.5f);
            swordRain.OrbitInterval = J("jinjeon.formation_interval", 0.3f);
            swordRain.EchoDelay = J("jinjeon.echo_delay", 1f);
            swordRain.EchoRatio = J("jinjeon.echo_ratio", 0.6f);
            swordRain.Begin(transform.position, area, count, time, _ultimateFallTime, hit, _rules);
        }

        // 기연 방에서 고를 진전: 지금 무기의 3가지 중 지금 것이 아닌 하나
        public UltimateForm RollUltimateForm(System.Random rng)
        {
            var forms = UsesDagger ? UltimateForms.Dagger : UltimateForms.Sword;
            UltimateForm pick;
            do pick = forms[rng.Next(forms.Length)];
            while (pick == UltimateForm);
            return pick;
        }
    }
}
