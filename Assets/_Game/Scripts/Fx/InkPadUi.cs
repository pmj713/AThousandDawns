using UnityEngine;

namespace AThousandDawns.Fx
{
    // 패드 커서와 회귀 기록용 수묵 IMGUI 조각.
    //   Frame: 고른 칸 둘레에 먹 붓 테두리 (네 변을 붓 획으로, 숨쉬듯 짙어졌다 옅어져요)
    //   Button: 붉은 둥근 낙관 위에 버튼 글자 (A · B …) + 오른쪽에 설명
    //   Bar: 순위 막대 — 먹 바탕 획 위에 비율만큼 짙은 먹 획
    //   Result: 최근 판 표시 — 승리는 금빛 낙관, 패배는 먹 점
    // 그림: HudArt (HpStroke · Blot), PadArt (PadSeal · GoldSeal). 없으면 단색으로 대신해요.
    public static class InkPadUi
    {
        static readonly Color Ink = new Color(0.1f, 0.09f, 0.08f);
        static readonly Color Paper = new Color(0.94f, 0.91f, 0.84f);

        static Texture2D _stroke, _blot, _seal, _gold;
        static GUIStyle _letter, _label;
        static bool _loaded;

        public static void Frame(Rect r, float pad = 6f)
        {
            if (Event.current.type != EventType.Repaint) return;
            Load();
            var old = GUI.color;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            var o = new Rect(r.x - pad, r.y - pad, r.width + pad * 2f, r.height + pad * 2f);
            const float t = 9f;
            Tint(Ink, 0.9f * pulse);
            Stroke(new Vector2(o.center.x, o.y), o.width + 8f, t, 0f);        // 위
            Stroke(new Vector2(o.center.x, o.yMax), o.width + 8f, t, 180f);   // 아래 (붓이 반대로)
            Stroke(new Vector2(o.x, o.center.y), o.height + 8f, t, -90f);     // 왼쪽
            Stroke(new Vector2(o.xMax, o.center.y), o.height + 8f, t, 90f);   // 오른쪽
            GUI.color = old;
        }

        public static float Button(Vector2 at, string button, string text, float size = 30f)
        {
            Load();
            Styles();
            var old = GUI.color;
            var seal = new Rect(at.x, at.y, size, size);
            if (_seal != null) { GUI.color = Color.white; GUI.DrawTexture(seal, _seal, ScaleMode.ScaleToFit); }
            else { Tint(new Color(0.75f, 0.15f, 0.1f), 1f); GUI.DrawTexture(seal, Texture2D.whiteTexture); }
            GUI.color = Paper;
            _letter.fontSize = Mathf.RoundToInt(size * 0.55f);
            GUI.Label(seal, button, _letter);
            float w = 0f;
            if (!string.IsNullOrEmpty(text))
            {
                GUI.color = old;
                _label.fontSize = Mathf.RoundToInt(size * 0.6f);
                w = _label.CalcSize(new GUIContent(text)).x;
                GUI.Label(new Rect(seal.xMax + 6f, at.y, w + 4f, size), text, _label);
            }
            GUI.color = old;
            return size + (w > 0f ? 6f + w + 18f : 8f);
        }

        public static void Bar(Rect r, float ratio, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Load();
            var old = GUI.color;
            ratio = Mathf.Clamp01(ratio);
            Tint(Ink, 0.18f);
            Part(r, 1f);
            Tint(color, 0.9f);
            Part(new Rect(r.x, r.y + r.height * 0.12f, r.width, r.height * 0.76f), ratio);
            GUI.color = old;
        }

        public static void Result(Rect r, bool victory)
        {
            if (Event.current.type != EventType.Repaint) return;
            Load();
            var old = GUI.color;
            if (victory)
            {
                GUI.color = Color.white;
                if (_gold != null) GUI.DrawTexture(r, _gold, ScaleMode.ScaleToFit);
                else { Tint(new Color(0.85f, 0.65f, 0.25f), 1f); GUI.DrawTexture(r, Texture2D.whiteTexture); }
            }
            else
            {
                Tint(Ink, 0.85f);
                var d = new Rect(r.center.x - r.width * 0.35f, r.center.y - r.height * 0.35f, r.width * 0.7f, r.height * 0.7f);
                if (_blot != null) GUI.DrawTexture(d, _blot, ScaleMode.ScaleToFit);
                else GUI.DrawTexture(d, Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        static void Part(Rect r, float ratio)
        {
            if (ratio <= 0f) return;
            var part = new Rect(r.x, r.y, r.width * ratio, r.height);
            if (_stroke != null) GUI.DrawTextureWithTexCoords(part, _stroke, new Rect(0f, 0f, ratio, 1f));
            else GUI.DrawTexture(part, Texture2D.whiteTexture);
        }

        static void Stroke(Vector2 center, float length, float thick, float angle)
        {
            var m = GUI.matrix;
            if (angle != 0f) GUIUtility.RotateAroundPivot(angle, center);
            var r = new Rect(center.x - length * 0.5f, center.y - thick * 0.5f, length, thick);
            if (_stroke != null) GUI.DrawTexture(r, _stroke, ScaleMode.StretchToFill);
            else GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.matrix = m;
        }

        static void Tint(Color c, float a) => GUI.color = new Color(c.r, c.g, c.b, a);

        static void Styles()
        {
            if (_letter != null) return;
            _letter = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _letter.normal.textColor = Color.white;
            _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            _label.normal.textColor = Color.white;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _stroke = Resources.Load<Texture2D>("HudArt/HpStroke");
            _blot = Resources.Load<Texture2D>("HudArt/Blot");
            _seal = Resources.Load<Texture2D>("PadArt/PadSeal");
            _gold = Resources.Load<Texture2D>("PadArt/GoldSeal");
        }
    }
}
