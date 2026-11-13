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

        // 단계 효과 설명 (경맥도 키워드 목록에 마우스를 올리면). stage = 2 · 4 · 6. 4단계는 절초 각인도 함께예요.
        static string P(string key, float fallback) => Mathf.RoundToInt(AThousandDawns.Core.GameConfig.GetOptional(key, fallback) * 100f) + "%";
        static string N(string key, float fallback) => AThousandDawns.Core.GameConfig.GetOptional(key, fallback).ToString("0.#");

        public static string StageText(Keyword keyword, int stage) => (keyword, stage) switch
        {
            (Keyword.Yeolyang, 2) => "평타 3타째 · 초식 · 대시 공격이 맞히면 화상 1중첩",
            (Keyword.Yeolyang, 4) => $"화상 피해 +{P("burn.stage4_bonus", 0.5f)} · 절초 각인: 검이 떨어진 자리에 불길",
            (Keyword.Yeolyang, 6) => $"화상 최대 중첩 +{N("burn.stage6_extra_stacks", 2f)}",
            (Keyword.Bingbaek, 2) => "평타 3타째 · 초식 · 대시 공격이 맞히면 둔화 1중첩",
            (Keyword.Bingbaek, 4) => $"둔화 효과 +{P("slow.stage4_bonus", 0.3f)} · 절초 각인",
            (Keyword.Bingbaek, 6) => "둔화 3중첩이면 빙결",
            (Keyword.Noejeong, 2) => "평타 3타째에 감전 1중첩 · 초식이 맞히면 연쇄 번개",
            (Keyword.Noejeong, 4) => $"방전 피해 +{P("discharge.stage4_bonus", 0.5f)} · 절초 각인",
            (Keyword.Noejeong, 6) => $"연쇄 번개 1명당 피해 {P("chain.ratio", 0.25f)} → {P("chain.stage6_ratio", 0.4f)}",
            (Keyword.Ganggi, 2) => $"평타 속도 +{P("ganggi.stage2_attack_speed", 0.15f)}",
            (Keyword.Ganggi, 4) => "평타 3타째에 강기 1발 · 절초 각인: 끝난 뒤 3초간 모든 평타가 강기",
            (Keyword.Ganggi, 6) => "강기가 모든 적을 관통",
            (Keyword.Geumgang, 2) => $"방에 들어갈 때마다 보호막 (최대 체력 {P("geumgang.room_shield", 0.1f)}) · 보호막이 있는 동안 강인함 +{N("geumgang.shield_toughness", 2f)}",
            (Keyword.Geumgang, 4) => $"맞으면 받은 피해의 {P("geumgang.hit_shield_ratio", 0.5f)}만큼 보호막 ({N("geumgang.hit_shield_cooldown", 6f)}초에 한 번) · 절초 각인: 보호막",
            (Keyword.Geumgang, 6) => $"보호막이 깨질 때 충격파 (반경 {N("geumgang.shockwave_radius", 2f)}칸)",
            (Keyword.Gyeonggong, 2) => $"대시 +{N("gyeonggong.dash_bonus", 1f)}회 · 기 부족 기물 1개당 이동속도 +{P("gyeonggong.lack_move_per", 0.04f)}",
            (Keyword.Gyeonggong, 4) => $"간파 (대시로 공격을 피함): 대시 1회 충전, 다음 공격 피해 +{P("gyeonggong.perfect_bonus", 0.3f)}",
            (Keyword.Gyeonggong, 6) => $"간파 보너스 +{P("gyeonggong.perfect_bonus6", 0.6f)} · 잔상이 그 적에게 반격",
            (Keyword.Mado, 2) => $"주화입마 기물 1개당 모든 피해 +{P("mado.per_deviated", 0.04f)} (최대 {P("mado.per_deviated_max", 0.2f)})",
            (Keyword.Mado, 4) => $"주화입마 효과 배율 ×1.5 → ×{N("mado.stage4_deviation", 1.8f)} · 절초 각인: 위력 ↑, 3초간 받는 피해 ↑",
            (Keyword.Jeongjong, 2) => $"운기 기물 1개당 피해 +{P("jeongjong.per_circulating", 0.03f)}",
            (Keyword.Jeongjong, 4) => $"주화입마 기물이 없으면 받는 피해 −{P("jeongjong.stage4_reduction", 0.15f)} · 절초 각인: 기세 돌려받음",
            (Keyword.Jeongjong, 6) => $"기 부족 · 주화입마 기물이 없으면 모든 피해 +{P("jeongjong.stage6_bonus", 0.3f)}",
            _ => null,
        };

        public static string Label(QiState state) => state switch
        {
            QiState.Lacking => "기 부족",
            QiState.Circulating => "운기",
            _ => "주화입마",
        };
    }
}
