using UnityEngine;

namespace AThousandDawns.Player
{
    public enum Keyword { Yeolyang, Bingbaek, Noejeong } // #열양, #빙백, #뇌정 (나머지 키워드는 기물을 만들 때)

    // 키워드 개수 (기물 키워드를 세는 자리). 2·4·6개에서 단계 효과, 4개면 절초에 각인돼요.
    // 지금은 기물이 없어서 시험판에서 F1·F2·F3으로 0 → 2 → 4 → 6 → 0 을 돌려요.
    public sealed class PlayerKeywords : MonoBehaviour
    {
        readonly int[] _counts = new int[3];

        public int Count(Keyword keyword) => _counts[(int)keyword];

        public void SetCount(Keyword keyword, int count) => _counts[(int)keyword] = Mathf.Clamp(count, 0, 6);

        public int CycleStage(Keyword keyword)
        {
            int next = Count(keyword) >= 6 ? 0 : (Count(keyword) / 2 + 1) * 2;
            SetCount(keyword, next);
            return next;
        }

        public static string Label(Keyword keyword) => keyword switch
        {
            Keyword.Yeolyang => "#열양",
            Keyword.Bingbaek => "#빙백",
            _ => "#뇌정",
        };
    }
}
