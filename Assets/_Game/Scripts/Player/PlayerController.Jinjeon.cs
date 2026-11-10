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

        public UltimateForm ActiveUltimateForm =>
            UltimateForm != UltimateForm.None && UltimateForms.ForDagger(UltimateForm) == UsesDagger ? UltimateForm : UltimateForm.None;

        static float J(string key, float fallback) => GameConfig.GetOptional(key, fallback);

        // StartUltimate에서: 무기·진전에 맞춰 검(비도) 수·피해·범위를 정하고 내리기 시작해요
        void BeginUltimateRain(HitDamageInput hit)
        {
            var form = ActiveUltimateForm;
            int count = UsesDagger ? Mathf.RoundToInt(D("knife.ultimate_count", 24f)) : _ultimateSwordCount;
            float time = UsesDagger ? D("knife.ultimate_time", 3f) : _ultimateCastTime;
            Vector2 area = _ultimateArea;
            switch (form)
            {
                case UltimateForm.OneSword: // 큰 검 한 자루 = 원래 검 모두 × 배율
                    hit.SpecialMultiplier *= count * J("jinjeon.one_sword_ratio", 1.2f);
                    count = 1;
                    break;
                case UltimateForm.Storm: // 더 많이, 더 넓게, 한 발은 약하게
                    count = Mathf.RoundToInt(count * J("jinjeon.storm_count", 1.5f));
                    hit.SpecialMultiplier *= J("jinjeon.storm_damage", 0.7f);
                    area *= J("jinjeon.storm_area", 1.3f);
                    break;
            }
            swordRain.Form = form;
            swordRain.Owner = transform;
            swordRain.OneSwordRadius = J("jinjeon.one_sword_radius", 3f);
            swordRain.OrbitTime = J("jinjeon.formation_time", 3f);
            swordRain.OrbitRatio = J("jinjeon.formation_ratio", 0.5f);
            swordRain.OrbitInterval = J("jinjeon.formation_interval", 0.3f);
            swordRain.EchoDelay = J("jinjeon.echo_delay", 1f);
            swordRain.EchoRatio = J("jinjeon.echo_ratio", 0.6f);
            swordRain.BloomRatio = J("jinjeon.bloom_ratio", 0.5f);
            swordRain.BackflowSpeed = J("jinjeon.backflow_speed", 20f);
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
