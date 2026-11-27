using System;
using AThousandDawns.Player;

namespace AThousandDawns.Items
{
    public enum ItemGrade { Common, Uncommon, Rare, Legendary } // 일반, 고급, 희귀, 전설

    // 기물 한 종류 (시트 data_items 한 줄). 효과는 id로 찾아서 코드에서 적용해요 (PlayerController.Items.cs).
    // v1~v4: 기물마다 뜻이 달라요 (note 칸). scales에 적힌 값만 레벨당 증가와 주화입마 배율을 받아요 (비율·시간·거리).
    [Serializable]
    public sealed class ItemDefinition
    {
        public string id;
        public Keyword keyword;
        public string name;
        public ItemGrade grade;
        public string aux;     // "마도", "정종" 또는 빈 칸
        public int minQi, maxQi;
        public float v1, v2, v3, v4;
        public bool v1Scales, v2Scales, v3Scales, v4Scales;
        public float levelBonus;  // v1이 레벨마다 더 붙는 값 (시트 level_bonus, 플레이테스트 11-27): v1 × 레벨 + levelBonus × 레벨(레벨−1)/2. 0이면 예전처럼 똑같이 늘어요
        public string side;    // 주화입마 부작용 종류
        public float sideValue;
        public string effectText, sideText;
        public EquipSlot slot;   // 장비 부위 (장비 칸 방식). None이면 어느 칸이든
        public bool otherworld;  // 이계 기물 (차원 틈에서 흘러온 판타지 물건): 끼운 혈자리의 기를 뒤틀어 올려요

        public int MaxLevel => maxQi - minQi + 1;
        public bool IsMado => aux == "마도";
        public bool IsJeongjong => aux == "정종";

        public static string GradeLabel(ItemGrade grade) => grade switch
        {
            ItemGrade.Common => "일반",
            ItemGrade.Uncommon => "고급",
            ItemGrade.Rare => "희귀",
            _ => "전설",
        };
    }
}
