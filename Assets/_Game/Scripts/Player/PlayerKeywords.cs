using UnityEngine;

namespace AThousandDawns.Player
{
    // 키워드 8종 (키워드 탭). 주 키워드 6종 + 보조 2종(#마도, #정종).
    public enum Keyword { Yeolyang, Bingbaek, Noejeong, Ganggi, Geumgang, Gyeonggong, Mado, Jeongjong }

    // 기물 상태 (기물 탭): 기 부족 · 운기 · 주화입마
    public enum QiState { Lacking, Circulating, Deviated }

    // 키워드 개수와 기물 상태별 개수. 키워드는 2·4·6개에서 단계 효과, 4개면 절초에 각인돼요.
    // 기물이 주는 개수(PlayerItems가 채움: 기 부족 0, 운기 1, 주화입마 2)에 시험판 키로 더하는 개수를 합쳐요.
    //   시험판 키: 키워드는 0 → 2 → 4 → 6 → 0, 기물 상태는 0 → 1 → … → 5 → 0
    public sealed class PlayerKeywords : MonoBehaviour
    {
        public const int KeywordCount = 8;
        public const int MaxQiCount = 5;

        readonly int[] _extra = new int[KeywordCount];
        readonly int[] _fromItems = new int[KeywordCount];
        readonly int[] _qiExtra = new int[3];
        readonly int[] _qiFromItems = new int[3];

        public event System.Action Changed;

        public int Count(Keyword keyword) => _fromItems[(int)keyword] + _extra[(int)keyword];
        public int FromItems(Keyword keyword) => _fromItems[(int)keyword];

        public void SetCount(Keyword keyword, int count)
        {
            _extra[(int)keyword] = Mathf.Clamp(count, 0, 6);
            Changed?.Invoke();
        }

        // 시험판: 더하는 개수를 0 → 2 → 4 → 6 → 0 (기물이 주는 개수 위에 얹어요). 합계를 돌려줘요.
        public int CycleStage(Keyword keyword)
        {
            int extra = _extra[(int)keyword];
            SetCount(keyword, extra >= 6 ? 0 : (extra / 2 + 1) * 2);
            return Count(keyword);
        }

        public int Qi(QiState state) => _qiFromItems[(int)state] + _qiExtra[(int)state];

        public int CycleQi(QiState state)
        {
            int extra = _qiExtra[(int)state];
            _qiExtra[(int)state] = extra >= MaxQiCount ? 0 : extra + 1;
            Changed?.Invoke();
            return Qi(state);
        }

        // PlayerItems가 기물 상태가 바뀔 때마다 불러요.
        public void SetFromItems(int[] keywordCounts, int[] qiCounts)
        {
            for (int i = 0; i < KeywordCount; i++) _fromItems[i] = keywordCounts[i];
            for (int i = 0; i < 3; i++) _qiFromItems[i] = qiCounts[i];
            Changed?.Invoke();
        }

        public static string Label(Keyword keyword) => keyword switch
        {
            Keyword.Yeolyang => "#열양",
            Keyword.Bingbaek => "#빙백",
            Keyword.Noejeong => "#뇌정",
            Keyword.Ganggi => "#강기",
            Keyword.Geumgang => "#금강",
            Keyword.Gyeonggong => "#경공",
            Keyword.Mado => "#마도",
            _ => "#정종",
        };

        public static string Label(QiState state) => state switch
        {
            QiState.Lacking => "기 부족",
            QiState.Circulating => "운기",
            _ => "주화입마",
        };
    }
}
