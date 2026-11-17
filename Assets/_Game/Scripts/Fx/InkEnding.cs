using AThousandDawns.Core;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 천마 격파 엔딩 (결과 화면 전, 첫 엔딩·두 번째부터의 짧은 엔딩 모두): 검은 화면 위 두루마리 대사.
    //   암전 (EndingTime 0~1.2초): 3D 장면을 검게 덮고, 가장자리엔 옅은 먹 번짐이 천천히 일렁여요. 대사 내내 검은 화면이에요.
    //   대사: 가운데 가로 두루마리가 양쪽 축을 굴리며 펼쳐지고, 줄이 바뀔 때마다 글이 먹처럼 번져 나와요.
    //         "화자: 글"이면 화자 이름은 위에 작은 붉은 글씨로. 제목·부제 문구는 따로 그리지 않아요 (4차 플레이 테스트).
    //   하늘의 틈 (SkyRift): 위쪽 가운데 보랏빛 균열 — 첫 격파는 첫 줄 동안 아물며 금빛 새벽빛이 번지고, 다시 격파는 조금 좁아지기만 해요.
    // 그림: FxArt (ScrollPaper · ScrollRod · InkBlot · InkMist), EliteFx/Blink (균열). 없으면 그 부분은 단색으로 대신해요.
    // 사용: OnGUI에서 InkEnding.Draw(EndingTime, EndingLine, EndingLineIndex, EndingLineCount, EndingFirst) — 기준 1920×1080 (UiScale) 위에서.
    public static class InkEnding
    {
        const float FadeTime = 1.2f;
        const float ScrollW = 1120f, ScrollH = 150f, Unroll = 0.45f;

        static readonly Color Ink = new Color(0.1f, 0.09f, 0.08f);
        static readonly Color Paper = new Color(0.94f, 0.91f, 0.84f);
        static readonly Color Blood = new Color(0.62f, 0.08f, 0.07f);

        static Texture2D _paper, _rod, _blot, _mist, _rift;
        static bool _loaded;
        static GUIStyle _line, _speaker, _hint;
        static int _lastIndex = -1;
        static float _lineStart, _lastTime, _scrollStart = -1f;
        static Matrix4x4 _matrix;

        // 하늘의 틈 (차원 겹침 엔딩): 화면 위쪽 가운데 보랏빛 균열.
        //   첫 격파 (first): 첫 줄 동안 (1.5~6초) 균열이 천천히 아물어 닫히고, 그 자리에서 금빛 새벽빛이 번져 화면 위쪽을 물들여요.
        //   다시 격파: 균열이 조금 좁아지기만 해요 (닫히지 않아요).
        static void SkyRift(float time, bool first, float w, float h, float dark)
        {
            if (dark <= 0.01f) return;
            var center = new Vector2(w * 0.5f, h * 0.2f);
            float close = Smooth((time - 1.5f) / 4.5f);
            float open = first ? 1f - close : Mathf.Lerp(1f, 0.6f, close);
            float pulse = 0.85f + 0.15f * Mathf.Sin(time * 3f);
            if (_mist != null && open > 0.02f) // 균열 둘레 보랏빛 번짐
            {
                Tint(new Color(0.55f, 0.25f, 1f), 0.35f * open * dark * pulse);
                float s = h * 0.5f;
                GUI.DrawTexture(new Rect(center.x - s * 0.5f, center.y - s * 0.5f, s, s), _mist, ScaleMode.StretchToFill);
            }
            if (_rift != null && open > 0.02f)
            {
                float rh = h * 0.34f, rw = rh * _rift.width / Mathf.Max(1f, (float)_rift.height) * open;
                Tint(Color.white, dark * Mathf.Clamp01(open * 3f));
                GUI.DrawTexture(new Rect(center.x - rw * 0.5f, center.y - rh * 0.5f, rw, rh), _rift, ScaleMode.StretchToFill);
            }
            if (first && _mist != null && close > 0f) // 새벽빛: 닫힌 자리에서 금빛이 번져 오래 남아요
            {
                float glow = Smooth((time - 3f) / 4f);
                for (int i = 0; i < 3; i++)
                {
                    float s = Mathf.Lerp(h * 0.2f, h * (0.9f + 0.25f * i), glow);
                    Tint(i == 0 ? new Color(1f, 0.92f, 0.7f) : new Color(1f, 0.72f, 0.38f), (0.4f - 0.1f * i) * glow * dark);
                    GUI.DrawTexture(new Rect(center.x - s * 0.5f, center.y - s * 0.5f, s, s), _mist, ScaleMode.StretchToFill);
                }
            }
        }

        public static void Draw(float time, string line, int index, int count, bool first = true)
        {
            Load();
            Styles();
            if (time < _lastTime) { _lastIndex = -1; _scrollStart = -1f; } // 새 엔딩
            _lastTime = time;
            if (index != _lastIndex) { _lastIndex = index; _lineStart = time; }
            var old = GUI.color;
            _matrix = GUI.matrix;
            float w = UiScale.Width, h = UiScale.Height;
            var screen = new Rect(0f, 0f, w, h);

            // ── 암전: 화면을 검게 덮어요
            float dark = Smooth(time / FadeTime);
            Tint(Color.black, dark);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            if (_blot != null) // 가장자리 먹 번짐 (검붉은 먹과 먹빛이 번갈아, 천천히 일렁여요)
                for (int i = 0; i < 8; i++)
                {
                    float ang = (i * 45f + 22f) * Mathf.Deg2Rad, drift = Mathf.Sin(time * 0.15f + i) * 20f;
                    var c = new Vector2(w * 0.5f + Mathf.Cos(ang) * w * 0.55f + drift, h * 0.5f + Mathf.Sin(ang) * h * 0.6f);
                    float s = 700f + 140f * (i % 3);
                    Tint(i % 2 == 0 ? new Color(0.22f, 0.05f, 0.05f) : new Color(0.12f, 0.11f, 0.1f), 0.35f * dark);
                    GUI.DrawTexture(new Rect(c.x - s * 0.5f, c.y - s * 0.4f, s, s * 0.8f), _blot, ScaleMode.StretchToFill);
                }

            SkyRift(time, first, w, h, dark);

            // ── 두루마리 대사 (암전이 거의 끝나면 펼쳐져요)
            if (!string.IsNullOrEmpty(line) && time > FadeTime * 0.7f)
            {
                if (_scrollStart < 0f) _scrollStart = time;
                DrawScroll(line, time - _scrollStart, time - Mathf.Max(_lineStart, _scrollStart), index, count, w, h);
            }
            GUI.color = old;
        }

        static void DrawScroll(string line, float age, float lineAge, int index, int count, float w, float h)
        {
            float open = Smooth(age / Unroll);
            float cw = Mathf.Max(24f, ScrollW * open), cx = w * 0.5f, y = (h - ScrollH) * 0.5f;
            var paper = new Rect(cx - cw * 0.5f, y, cw, ScrollH);
            Tint(Color.white, 1f);
            if (_paper != null) GUI.DrawTextureWithTexCoords(paper, _paper, new Rect(0.5f - 0.5f * open, 0.4f, open, 0.21f));
            else { Tint(Paper, 1f); GUI.DrawTexture(paper, Texture2D.whiteTexture); }
            Rod(paper.x, paper.center.y);
            Rod(paper.xMax, paper.center.y);
            if (open < 0.95f) return;

            string speaker = null, body = line;
            int colon = line.IndexOf(": ", System.StringComparison.Ordinal);
            if (colon > 0 && colon < 12) { speaker = line.Substring(0, colon); body = line.Substring(colon + 2); }
            float k = Smooth(lineAge / 0.6f); // 먹이 번지듯
            var text = new Rect(paper.x + 70f, paper.y + (speaker != null ? 34f : 18f), paper.width - 140f, paper.height - (speaker != null ? 46f : 36f));
            if (speaker != null)
            {
                Tint(Blood, k);
                GUI.Label(new Rect(text.x, paper.y + 10f, text.width, 28f), speaker, _speaker);
            }
            Tint(Ink, k);
            GUI.Label(new Rect(text.x, text.y + (1f - k) * 6f, text.width, text.height), body, _line);
            if (lineAge > 1.2f) // 넘기기 안내 (깜빡임)
            {
                Tint(Ink, 0.35f + 0.25f * Mathf.Sin(lineAge * 4f));
                GUI.Label(new Rect(paper.xMax - 170f, paper.yMax - 30f, 150f, 24f), count > 1 ? $"{index + 1}/{count}  F ▸" : "F ▸", _hint);
            }
        }

        static void Rod(float x, float cy)
        {
            const float len = ScrollH + 34f, thick = 22f;
            if (_rod != null)
            {
                Tint(Color.white, 1f);
                GUI.matrix = _matrix * Matrix4x4.TRS(new Vector3(x, cy, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one); // RotateAroundPivot은 UiScale 배율에서 축이 어긋나요
                GUI.DrawTexture(new Rect(-len * 0.5f, -thick * 0.5f, len, thick), _rod, ScaleMode.StretchToFill);
                GUI.matrix = _matrix;
            }
            else
            {
                Tint(new Color(0.25f, 0.13f, 0.07f), 1f);
                GUI.DrawTexture(new Rect(x - thick * 0.5f, cy - len * 0.5f, thick, len), Texture2D.whiteTexture);
            }
        }

        // ── 도움

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static void Tint(Color c, float a) => GUI.color = new Color(c.r, c.g, c.b, a);

        static void Styles()
        {
            if (_line != null) return;
            _line = InkUi.Text(InkUi.Heading, Color.white, TextAnchor.MiddleCenter, wrap: true); // UI 규칙 (InkUi): 대사 Heading · 화자 Label · 안내 Small
            _speaker = InkUi.Text(InkUi.Label, Color.white, TextAnchor.MiddleLeft, bold: true);
            _hint = InkUi.Text(InkUi.Small, Color.white, TextAnchor.MiddleRight);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _paper = Resources.Load<Texture2D>("FxArt/ScrollPaper");
            _rod = Resources.Load<Texture2D>("FxArt/ScrollRod");
            _blot = Resources.Load<Texture2D>("FxArt/InkBlot");
            _mist = Resources.Load<Texture2D>("FxArt/InkMist");
            _rift = Resources.Load<Texture2D>("EliteFx/Blink");
        }
    }
}
