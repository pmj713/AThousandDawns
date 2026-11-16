using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 이계 세트 효과 (혼종 키워드, 차원 겹침): 같은 키워드의 이계 기물을 장비 칸에 2개 · 3개 끼우면 (기 부족이 아닐 때)
    // 무협 키워드 단계와 따로 혼종 세트 보너스가 붙어요. 1단계 = 2개, 2단계 = 3개 (시트 hybrid.*)
    //   업화 (#열양 + 화염 마법) · 서리 마나 (#빙백 + 냉기 마법) · 뇌문 (#뇌정 + 룬) · 마검 (#강기 + 마검술) · 미스릴 수호 (#금강) · 엘프 발걸음 (#경공)
    public static class HybridSets
    {
        public const int Tier1Count = 2, Tier2Count = 3;

        public static int LevelFor(int count) => count >= Tier2Count ? 2 : count >= Tier1Count ? 1 : 0;

        public static bool Has(Keyword keyword) => keyword <= Keyword.Gyeonggong; // 주 키워드 6종만 (#마도 · #정종은 없음)

        public static string Id(Keyword keyword) => keyword switch
        {
            Keyword.Yeolyang => "blazing",
            Keyword.Bingbaek => "frostmana",
            Keyword.Noejeong => "thunderrune",
            Keyword.Ganggi => "spellblade",
            Keyword.Geumgang => "mithril",
            _ => "elvenstep",
        };

        public static string Name(Keyword keyword) => keyword switch
        {
            Keyword.Yeolyang => "업화",
            Keyword.Bingbaek => "서리 마나",
            Keyword.Noejeong => "뇌문",
            Keyword.Ganggi => "마검",
            Keyword.Geumgang => "미스릴 수호",
            _ => "엘프 발걸음",
        };

        // 세트 수치 (시트 hybrid.{id}.t1 · .t2)
        public static float Value(Keyword keyword, int tier)
        {
            float fallback = (keyword, tier) switch
            {
                (Keyword.Yeolyang, 1) => 0.3f,  // 화상 피해 +
                (Keyword.Yeolyang, 2) => 1f,    // 화상 최대 중첩 +
                (Keyword.Bingbaek, 1) => 1f,    // 둔화 지속 +초
                (Keyword.Bingbaek, 2) => 0.5f,  // 빙결 시간 +초
                (Keyword.Noejeong, 1) => 1f,    // 연쇄 번개 대상 +
                (Keyword.Noejeong, 2) => 0.4f,  // 방전 피해 +
                (Keyword.Ganggi, 1) => 0.1f,    // 공격 속도 +
                (Keyword.Ganggi, 2) => 0.1f,    // 모든 피해 +
                (Keyword.Geumgang, 1) => 0.08f, // 받는 피해 −
                (Keyword.Geumgang, 2) => 2f,    // 강인함 +
                (Keyword.Gyeonggong, 1) => 0.1f,// 이동속도 +
                _ => 1f,                        // 엘프 발걸음 2단계: 대시 +
            };
            return GameConfig.GetOptional($"hybrid.{Id(keyword)}.t{tier}", fallback);
        }

        static string P(float v) => Mathf.RoundToInt(v * 100f) + "%";

        public static string TierText(Keyword keyword, int tier)
        {
            float v = Value(keyword, tier);
            return (keyword, tier) switch
            {
                (Keyword.Yeolyang, 1) => $"화상 피해 +{P(v)}",
                (Keyword.Yeolyang, 2) => $"화상 최대 중첩 +{v:0}",
                (Keyword.Bingbaek, 1) => $"둔화 지속 +{v:0.#}초",
                (Keyword.Bingbaek, 2) => $"빙결 시간 +{v:0.#}초",
                (Keyword.Noejeong, 1) => $"연쇄 번개 대상 +{v:0}",
                (Keyword.Noejeong, 2) => $"방전 피해 +{P(v)}",
                (Keyword.Ganggi, 1) => $"공격 속도 +{P(v)}",
                (Keyword.Ganggi, 2) => $"모든 피해 +{P(v)}",
                (Keyword.Geumgang, 1) => $"받는 피해 −{P(v)}",
                (Keyword.Geumgang, 2) => $"강인함 +{v:0}",
                (Keyword.Gyeonggong, 1) => $"이동속도 +{P(v)}",
                _ => $"대시 +{v:0}회",
            };
        }
    }
}
