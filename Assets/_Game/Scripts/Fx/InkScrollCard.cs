using System.Text.RegularExpressions;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 기물 고르기 족자 (아트 목록 연출). 카드 자리(그대로의 크기)에 족자가 위에서 말린 채 떨어져 내려오고,
    // 아래 축이 굴러 내려가며 펼쳐져요. 펼쳐진 만큼만 한지와 그 위 글·아이콘이 보여요 (적힌 내용이 드러나요).
    // 사용 (IMGUI, elapsed = 고르기가 열린 뒤 흐른 실제 시간):
    //   if (InkScrollCard.Begin(rect, elapsed, i, out bool opened)) { ...그룹 안 좌표 (0,0)부터 글·아이콘...; InkScrollCard.End(); }
    //   글은 InkScrollCard.Ink(text)로 한지 위에서 읽히는 먹색으로 바꿔 넣어요.
    public static class InkScrollCard
    {
        const float Drop = 0.32f;     // 떨어져 내려오는 시간
        const float Unroll = 0.55f;   // 펼쳐지는 시간
        const float Stagger = 0.12f;  // 카드마다 늦게
        const float DropFrom = 260f;  // 얼마나 위에서 (기준 1080)
        const float RodH = 24f;       // 축 굵기
        const float RodOver = 0.09f;  // 축이 카드 양옆으로 나오는 비율 (손잡이)

        static readonly Color InkText = new Color(0.12f, 0.1f, 0.09f);
        static Texture2D _paper, _rod;
        static bool _loaded;
        static GUIStyle _style, _styleFrom;
        static Rect _card;
        static float _visible;

        // 다 펼쳐질 때까지 걸리는 시간 (카드 수만큼)
        public static float Duration(int count) => Drop + Unroll + Stagger * Mathf.Max(0, count - 1);

        // 족자 바탕과 위 축을 그리고, 펼쳐진 한지 부분으로 그룹을 열어요. 아직 안 보이면 false (End를 부르지 않아요)
        public static bool Begin(Rect card, float elapsed, int index, out bool opened)
        {
            Load();
            float t = elapsed - index * Stagger;
            opened = t >= Drop + Unroll;
            if (t <= 0f) return false;

            // 떨어짐: 말린 족자(축 둘이 겹친 것)가 위에서 내려와 살짝 튀어요
            float drop = Mathf.Clamp01(t / Drop);
            float fall = 1f - BackOut(drop);
            float alpha = Mathf.Clamp01(drop * 2.5f);
            card.y -= fall * DropFrom;
            float unroll = Mathf.Clamp01((t - Drop) / Unroll);
            float open = 1f - Mathf.Pow(1f - unroll, 3f);
            _card = card;
            _visible = card.height * open;

            var old = GUI.color;
            if (_visible > 0.5f)
            {
                // 그림자 (한지 아래로 살짝)
                GUI.color = new Color(0f, 0f, 0f, 0.35f * alpha);
                GUI.DrawTexture(new Rect(card.x + 6f, card.y + 8f, card.width, _visible), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                var shown = new Rect(card.x, card.y, card.width, _visible);
                if (_paper != null) GUI.DrawTextureWithTexCoords(shown, _paper, new Rect(0f, 1f - open, 1f, open));
                else { GUI.color = new Color(0.93f, 0.9f, 0.82f, alpha); GUI.DrawTexture(shown, Texture2D.whiteTexture); }
                // 마우스가 올라가면 붉은 먹이 옅게 번져요
                if (opened && shown.Contains(Event.current.mousePosition))
                {
                    GUI.color = new Color(0.78f, 0.27f, 0.24f, 0.1f);
                    GUI.DrawTexture(shown, Texture2D.whiteTexture);
                }
            }
            GUI.color = new Color(1f, 1f, 1f, alpha);
            DrawRod(card.y);
            GUI.color = old;

            if (_visible <= 0.5f) { DrawBottomRod(alpha); return false; }
            GUI.BeginGroup(new Rect(card.x, card.y, card.width, _visible));
            return true;
        }

        // 그룹을 닫고 아래 축(펼쳐진 끝)을 그 위에 그려요
        public static void End()
        {
            GUI.EndGroup();
            DrawBottomRod(1f);
        }

        // 원래 카드 글꼴·여백을 따르되 바탕 없이 먹색 글 (한지 테두리만큼 여백을 더해요)
        public static GUIStyle Style(GUIStyle from)
        {
            if (_style == null || _styleFrom != from)
            {
                _styleFrom = from;
                _style = new GUIStyle(from);
                _style.normal.background = _style.hover.background = _style.active.background = _style.focused.background = null;
                _style.normal.textColor = _style.hover.textColor = _style.active.textColor = _style.focused.textColor = InkText;
                var p = from.padding;
                _style.padding = new RectOffset(p.left + 10, p.right + 10, p.top + 8, p.bottom + 10);
            }
            return _style;
        }

        static readonly Regex ColorTag = new Regex("<color=#([0-9a-fA-F]{6})>");

        // 어두운 바탕용 밝은 글색(<color=#...>)을 한지 위에서 읽히게 짙게 바꿔요 (색상은 그대로)
        public static string Ink(string text) => ColorTag.Replace(text, m =>
        {
            ColorUtility.TryParseHtmlString("#" + m.Groups[1].Value, out var c);
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var dark = Color.HSVToRGB(h, s < 0.12f ? s : Mathf.Max(s, 0.75f), Mathf.Min(v, s < 0.12f ? 0.3f : 0.55f));
            return $"<color=#{ColorUtility.ToHtmlStringRGB(dark)}>";
        });

        static void DrawBottomRod(float alpha)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            DrawRod(_card.y + _visible);
            GUI.color = old;
        }

        static void DrawRod(float centerY)
        {
            float over = _card.width * RodOver;
            var rect = new Rect(_card.x - over, centerY - RodH * 0.5f, _card.width + over * 2f, RodH);
            if (_rod != null) GUI.DrawTexture(rect, _rod, ScaleMode.StretchToFill);
            else
            {
                var old = GUI.color;
                GUI.color = new Color(0.22f, 0.14f, 0.09f, old.a);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = old;
            }
        }

        static float BackOut(float x)
        {
            const float c1 = 1.2f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _paper = Resources.Load<Texture2D>("FxArt/ScrollPaper");
            _rod = Resources.Load<Texture2D>("FxArt/ScrollRod");
        }
    }
}
