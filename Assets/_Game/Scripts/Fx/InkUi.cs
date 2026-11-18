using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // UI 규칙 한 곳 (플레이테스트 2차: 화면마다 틀 · 글자 크기 · 색 · 버튼이 따로 놀아요) — 모든 IMGUI 화면이 이걸 써요. 기준 해상도 1920×1080 (UiScale).
    //   글자 크기 6단계: Title 42 (화면 제목) · Heading 30 (칸 제목 · 큰 숫자) · Body 24 (본문 · 줄) · Label 20 (라벨 · 설명) · Small 17 (보조) · Tiny 15 (키 · 작은 표시)
    //   색: 글자 Ink · SubInk · Muted (밝은 바탕 위), PaperText (짙은 바탕 위) / 바탕 Paper (한지 창) · Navy (짙은 패널)
    //       강조 Accent 주홍 · Gold 금 · Cobalt 코발트 · Good 초록 · Bad 진홍
    //   틀: Window (밝은 한지 창 + 남빛 붓 테두리) · Panel (짙은 남빛 칸) · Button (남빛 붓 버튼, 마우스를 올리면 주홍, 글자색 고정)
    //   글자 스타일: Text(크기, 색, 정렬, 굵게, 줄바꿈) — 같은 조합은 한 번만 만들어 캐시하고, 마우스를 올려도 색이 바뀌지 않아요 (Steady).
    public static class InkUi
    {
        public const int Title = 42, Heading = 30, Body = 24, Label = 20, Small = 17, Tiny = 15;

        public static readonly Color Ink = new Color(0.1f, 0.09f, 0.12f);
        public static readonly Color SubInk = new Color(0.28f, 0.24f, 0.24f);
        public static readonly Color Muted = new Color(0.5f, 0.47f, 0.46f);
        public static readonly Color PaperText = new Color(0.96f, 0.93f, 0.86f);
        public static readonly Color Paper = new Color(0.97f, 0.94f, 0.88f);
        public static readonly Color Navy = new Color(0.08f, 0.1f, 0.24f);
        public static readonly Color Accent = new Color(0.88f, 0.15f, 0.1f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.15f);
        public static readonly Color Cobalt = new Color(0.2f, 0.45f, 0.95f);
        public static readonly Color Good = new Color(0.25f, 0.7f, 0.35f);
        public static readonly Color Bad = new Color(0.85f, 0.12f, 0.15f);
        // 짙은 바탕(HUD · 어두운 창) 위 글자: 본문 PaperText · 흐림 MutedOnDark · 강조 AccentOnDark
        public static readonly Color MutedOnDark = new Color(0.65f, 0.62f, 0.58f);
        public static readonly Color AccentOnDark = new Color(1f, 0.55f, 0.42f);

        static readonly Dictionary<(int, Color, TextAnchor, bool, bool), GUIStyle> Styles = new Dictionary<(int, Color, TextAnchor, bool, bool), GUIStyle>();
        static readonly Dictionary<int, GUIStyle> Buttons = new Dictionary<int, GUIStyle>();
        static bool _skinDone;

        // 글자 스타일 (OnGUI 안에서 불러요). 서식 태그(<b>, <color>)는 늘 켜져 있어요.
        public static GUIStyle Text(int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false)
        {
            var key = (size, color, align, bold, wrap);
            if (Styles.TryGetValue(key, out var s) && s != null) return s;
            s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = align, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, wordWrap = wrap, richText = true };
            s.normal.textColor = color;
            Styles[key] = Steady(s);
            return s;
        }

        // 버튼 스타일: 남빛 붓 바탕 (마우스 · 누름 = 주홍), 글자는 한지색 고정
        public static GUIStyle ButtonStyle(int size = Label)
        {
            if (Buttons.TryGetValue(size, out var s) && s != null) return s;
            s = new GUIStyle(GUI.skin.button) { fontSize = size, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            InkMeridianArt.Restyle(s);
            Buttons[size] = Steady(s);
            return s;
        }

        public static bool Button(Rect rect, string text, int size = Label) => GUI.Button(rect, text, ButtonStyle(size));

        public static void Window(Rect rect) => InkMeridianArt.PaperPanel(rect);
        public static void Window(Rect rect, Color fill) => InkMeridianArt.PaperPanel(rect, fill);
        public static void Panel(Rect rect) => InkMeridianArt.Panel(rect);

        // 자유 크기를 6단계 중 가장 가까운 것으로 (예전 화면 숫자를 옮길 때)
        public static int Snap(int size) => size <= 15 ? Tiny : size <= 18 ? Small : size <= 21 ? Label : size <= 26 ? Body : size <= 36 ? Heading : size <= 60 ? Title : size;

        // 게임 글자체 (플레이테스트 6차): 고운바탕 Gowun Batang (SIL 오픈 폰트 라이선스 1.1, Art/UI/Fonts/OFL.txt) — 붓맛이 도는 부드러운 바탕체.
        //   Resources/Fonts/GowunBatang-Regular (굵게는 같은 이름의 Bold를 Unity가 찾아 써요).
        static Font _font;
        static bool _fontLoaded;
        public static Font GameFont
        {
            get
            {
                if (!_fontLoaded) { _fontLoaded = true; _font = Resources.Load<Font>("Fonts/GowunBatang-Regular"); }
                return _font;
            }
        }

        // 마우스를 올리거나 누르거나 포커스가 가도 글자색이 바뀌지 않게 (Unity 기본 hover 글자색은 흰색이에요).
        // 처음 부를 때 기본 스킨(button · label · toggle · box)도 같이 고정해요 — 스타일 없이 그리는 GUI.Button(rect, text)도요.
        public static GUIStyle Steady(GUIStyle s)
        {
            if (!_skinDone && GUI.skin != null)
            {
                _skinDone = true;
                var font = GameFont; // 게임 글자체 (고운바탕): 기본 스킨에 한 번 — 글꼴을 따로 안 정한 모든 IMGUI 글자가 이걸 써요
                if (font != null) GUI.skin.font = font;
                InkMeridianArt.LockText(GUI.skin.button);
                InkMeridianArt.LockText(GUI.skin.label);
                InkMeridianArt.LockText(GUI.skin.toggle);
                InkMeridianArt.LockText(GUI.skin.box);
            }
            return InkMeridianArt.LockText(s);
        }
    }
}
