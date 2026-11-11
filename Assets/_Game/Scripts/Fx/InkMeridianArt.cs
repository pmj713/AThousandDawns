using UnityEngine;

namespace AThousandDawns.Fx
{
    // 경맥도 화면 수묵 그림 (아트 목록 UI). MeridianScreen이 단색 사각형 대신 부르는 자리만 맡아요 — 글자·배치·입력은 그쪽 그대로.
    //   Backdrop: 화면 전체 — 먹 번짐 위로 경혈도(사람 몸 경락선)가 옅게 비치는 어두운 바탕.
    //   Cell:     칸 — 기 색으로 물든 먹 번짐 + 옅은 한지색 붓 네모 테두리. 단전 칸은 아래에 붉은 먹이 번져요.
    //   Panel:    주머니·설명 칸 — 짙은 먹 한 획 바탕.
    // 그림: Art/UI/Hud/Resources/HudArt (MeridianBg, InkSwatch, CellFrame, Blot). 없으면 예전처럼 단색.
    public static class InkMeridianArt
    {
        static readonly Color Paper = new Color(0.94f, 0.91f, 0.84f);
        static readonly Color Plum = new Color(0.78f, 0.27f, 0.24f);
        static readonly Color PanelInk = new Color(0.07f, 0.065f, 0.06f);

        static Texture2D _bg, _swatch, _frame, _blot;
        static bool _loaded;

        public static void Backdrop(float width, float height)
        {
            Load();
            var old = GUI.color;
            var screen = new Rect(0f, 0f, width, height);
            if (_bg != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(screen, _bg, ScaleMode.ScaleAndCrop);
                GUI.color = new Color(0f, 0f, 0f, 0.3f); // 글자가 읽히게 한 겹 더 어둡게
            }
            else GUI.color = new Color(0f, 0f, 0f, 0.86f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Cell(Rect rect, Color qiColor, bool danjeon)
        {
            Load();
            if (_swatch == null || _frame == null) { Fill(rect, qiColor); return; }
            var old = GUI.color;
            if (danjeon && _blot != null) // 단전: 붉은 먹이 칸 밖으로 살짝 번져요
            {
                float pulse = 0.55f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.5f);
                GUI.color = new Color(Plum.r, Plum.g, Plum.b, pulse);
                GUI.DrawTexture(Grow(rect, 0.18f), _blot, ScaleMode.StretchToFill);
            }
            GUI.color = new Color(qiColor.r, qiColor.g, qiColor.b, Mathf.Max(0.75f, qiColor.a));
            GUI.DrawTexture(Grow(rect, -0.04f), _swatch, ScaleMode.StretchToFill);
            GUI.color = new Color(Paper.r, Paper.g, Paper.b, danjeon ? 0.75f : 0.32f);
            GUI.DrawTexture(Grow(rect, 0.04f), _frame, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        public static void Panel(Rect rect)
        {
            Load();
            if (_swatch == null) { GUI.Box(rect, GUIContent.none); return; }
            var old = GUI.color;
            GUI.color = new Color(PanelInk.r, PanelInk.g, PanelInk.b, 0.92f);
            GUI.DrawTexture(Grow(rect, 0.03f), _swatch, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        static Rect Grow(Rect r, float ratio)
        {
            float dx = r.width * ratio * 0.5f, dy = r.height * ratio * 0.5f;
            return new Rect(r.x - dx, r.y - dy, r.width + dx * 2f, r.height + dy * 2f);
        }

        static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _bg = Resources.Load<Texture2D>("HudArt/MeridianBg");
            _swatch = Resources.Load<Texture2D>("HudArt/InkSwatch");
            _frame = Resources.Load<Texture2D>("HudArt/CellFrame");
            _blot = Resources.Load<Texture2D>("HudArt/Blot");
        }
    }
}
