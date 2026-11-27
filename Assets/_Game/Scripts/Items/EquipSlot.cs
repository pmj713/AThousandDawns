using UnityEngine;

namespace AThousandDawns.Items
{
    // 장비 부위 (경맥도 대신 장비 칸 10개). 기물마다 끼울 수 있는 부위가 하나예요 (data_items의 slot 열).
    public enum EquipSlot { None, Head, Top, Bottom, Shoes, Earring, Neck, Bracelet, Waist, Charm }

    // 장비 칸 10개: 몸의 혈자리 위에 놓여요. 칸 번호 = 판의 칸 (x, 0).
    //   옷: 머리(백회) · 상의(단중) · 하의(기해) · 신발(용천)
    //   장신구: 귀걸이 둘(청궁 좌·우) · 목걸이(천돌) · 팔찌(내관) · 허리(대맥) · 무기 장신구(노궁)
    // 칸의 기 = 칸마다 정한 기본 기 + (공력 − 시작 공력) + 기혈 (+ 이계 기물이면 뒤틀림). 시트 equip.slot_qi.*
    public static class EquipSlots
    {
        public const int Count = 10;

        public static readonly EquipSlot[] Kinds =
        {
            EquipSlot.Head, EquipSlot.Top, EquipSlot.Bottom, EquipSlot.Shoes,
            EquipSlot.Earring, EquipSlot.Earring, EquipSlot.Neck, EquipSlot.Bracelet, EquipSlot.Waist, EquipSlot.Charm,
        };

        public static readonly string[] Acupoints = { "백회", "단중", "기해", "용천", "청궁 (좌)", "청궁 (우)", "천돌", "내관", "대맥", "노궁" };

        // 시트 키 (equip.slot_qi.{key}) · 기본 기
        static readonly string[] Keys = { "head", "top", "bottom", "shoes", "earring", "earring", "neck", "bracelet", "waist", "charm" };
        static readonly int[] DefaultQi = { 3, 4, 4, 1, 2, 2, 3, 2, 3, 2 };

        public static int BaseQi(int index) =>
            Mathf.RoundToInt(AThousandDawns.Core.GameConfig.GetOptional($"equip.slot_qi.{Keys[index]}", DefaultQi[index]));

        // 인체도 그림(HudArt/BodyChart, 정사각) 위 혈자리 점 (0~1, 왼쪽 위 0,0). 화면에서 본 좌우
        public static readonly Vector2[] Points =
        {
            new Vector2(0.50f, 0.04f), // 백회 (머리)
            new Vector2(0.50f, 0.28f), // 단중 (가슴)
            new Vector2(0.50f, 0.46f), // 기해 (아랫배)
            new Vector2(0.37f, 0.94f), // 용천 (발)
            new Vector2(0.45f, 0.11f), // 청궁 좌
            new Vector2(0.55f, 0.11f), // 청궁 우
            new Vector2(0.50f, 0.19f), // 천돌 (목)
            new Vector2(0.66f, 0.45f), // 내관 (팔목)
            new Vector2(0.57f, 0.43f), // 대맥 (허리 옆)
            new Vector2(0.31f, 0.54f), // 노궁 (손바닥)
        };

        // 칸 자리 (장비 칸 영역 기준 0~1, 가운데): 혈자리가 몰려 있어서 칸은 그림 좌우 두 줄에 놓고 혈자리 점까지 먹선으로 이어요
        //   플레이테스트 11-27: 입는 것(머리·상의·허리·하의·신발)은 오른쪽에 위에서 아래로, 장신구(귀걸이 둘·목걸이·팔찌·무기 장신구)는 왼쪽
        public static readonly Vector2[] Layout =
        {
            new Vector2(0.92f, 0.08f),  // 머리 (오른쪽)
            new Vector2(0.92f, 0.28f),  // 상의 (오른쪽)
            new Vector2(0.92f, 0.68f),  // 하의 (오른쪽)
            new Vector2(0.92f, 0.88f),  // 신발 (오른쪽)
            new Vector2(0.08f, 0.08f),  // 귀걸이 좌 (왼쪽)
            new Vector2(0.08f, 0.28f),  // 귀걸이 우 (왼쪽)
            new Vector2(0.08f, 0.48f),  // 목걸이 (왼쪽)
            new Vector2(0.08f, 0.68f),  // 팔찌 (왼쪽)
            new Vector2(0.92f, 0.48f),  // 허리 (오른쪽)
            new Vector2(0.08f, 0.88f),  // 무기 장신구 (왼쪽)
        };

        // 처음부터 모든 칸이 열려 있어요 (예전엔 보스마다 귀걸이 · 허리 · 무기 장신구가 열렸어요)
        public static bool OpenAtStart(int index) => true;

        public static int[] UnlockAfterBoss(int region) => System.Array.Empty<int>();

        public static string Label(EquipSlot slot) => slot switch
        {
            EquipSlot.Head => "머리",
            EquipSlot.Top => "상의",
            EquipSlot.Bottom => "하의",
            EquipSlot.Shoes => "신발",
            EquipSlot.Earring => "귀걸이",
            EquipSlot.Neck => "목걸이",
            EquipSlot.Bracelet => "팔찌",
            EquipSlot.Waist => "허리",
            EquipSlot.Charm => "무기 장신구",
            _ => "",
        };

        public static EquipSlot Parse(string raw)
        {
            raw = (raw ?? "").Trim();
            for (var s = EquipSlot.Head; s <= EquipSlot.Charm; s++)
                if (Label(s) == raw || s.ToString().Equals(raw, System.StringComparison.OrdinalIgnoreCase)) return s;
            return EquipSlot.None;
        }

        // 이 칸에 이 부위 기물을 끼울 수 있나 (부위가 없는 기물은 어디든)
        public static bool Fits(int index, EquipSlot slot) =>
            index >= 0 && index < Count && (slot == EquipSlot.None || Kinds[index] == slot);
    }
}
