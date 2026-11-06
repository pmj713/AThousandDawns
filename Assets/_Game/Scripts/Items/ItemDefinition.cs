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
        public string side;    // 주화입마 부작용 종류
        public float sideValue;
        public string effectText, sideText;

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
