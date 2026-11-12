using AThousandDawns.Core;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 천마 격파 엔딩 (결과 화면 전): 수묵 새벽 전환 + 두루마리 대사 + 마지막 줄 뒤 제목 낙관.
    //   새벽 (EndingTime 0~6초): 3D 장면 위에 얹어요 (장면의 해·주변광은 메인 아트 InkEndingDawn). 처음엔 화면이 검붉게 눌리고
    //       가장자리를 덮은 검붉은 먹구름이 바깥으로 걷히며, 위에서 금빛 번짐이 차올라 따뜻해져요. 가운데(천마)는 비워 둬요.
    //   대사: 아래 가운데 가로 두루마리가 양쪽 축을 굴리며 펼쳐지고, 줄이 바뀔 때마다 글이 먹처럼 번져 나와요.
    //         "화자: 글"이면 화자 이름은 위에 작은 붉은 글씨로.
    //   제목 낙관: "일조천회"가 든 줄이 나오면 1초 뒤 가운데 위로 제목 붓글씨가 번져 나오고, 붉은 낙관이 쾅 찍혀요.
    // 그림: TitleArt (Title · Seal), FxArt (ScrollPaper · ScrollRod · InkBlot · InkMist). 없으면 그 부분은 단색으로 대신해요.
    // 사용: OnGUI에서 InkEnding.Draw(EndingTime, EndingLine, EndingLineIndex, EndingLineCount) — 기준 1920×1080 (UiScale) 위에서.
    public static class InkEnding
    {
        const float DawnStart = 1f, DawnTime = 5f;   // 0~6초 새벽
        const float ScrollW = 1120f, ScrollH = 150f, Unroll = 0.45f;

        static readonly Color Ink = new Color(0.1f, 0.09f, 0.08f);
        static readonly Color Paper = new Color(0.94f, 0.91f, 0.84f);
        static readonly Color Blood = new Color(0.62f, 0.08f, 0.07f);

        static Texture2D _title, _seal, _paper, _rod, _blot, _mist;
        static bool _loaded;
        static GUIStyle _line, _speaker, _hint;
        static int _lastIndex = -1;
        static float _lineStart, _lastTime, _scrollStart = -1f;
        static Matrix4x4 _matrix;

        public static void Draw(float time, string line, int index, int count)
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

            // ── 새벽 전환 (3D 장면 위에 얹어요: 무릎 꿇은 천마와 InkEndingDawn의 새벽빛이 보이게 가운데는 비워 둬요)
            float warm = Smooth((time - DawnStart - 1f) / (DawnTime - 1f));
            float clouds = 1f - Smooth((time - 0.4f) / 3.6f); // 화면 가장자리를 짓누르던 검붉은 먹구름이 바깥으로 걷혀요
            if (clouds > 0f)
            {
                Tint(new Color(0.2f, 0.02f, 0.03f), 0.35f * clouds); // 처음엔 화면 전체가 검붉게 눌려 있어요
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
            }
            if (_blot != null && clouds > 0f)
                for (int i = 0; i < 12; i++)
                {
                    float ang = (i * 30f + 15f) * Mathf.Deg2Rad, spread = 1f + (1f - clouds) * 0.8f;
                    var c = new Vector2(w * 0.5f + Mathf.Cos(ang) * w * 0.52f * spread, h * 0.5f + Mathf.Sin(ang) * h * 0.55f * spread);
                    float s = 760f + 160f * (i % 3);
                    Tint(i % 2 == 0 ? new Color(0.36f, 0.04f, 0.05f) : new Color(0.1f, 0.04f, 0.05f), 0.85f * clouds);
                    GUI.DrawTexture(new Rect(c.x - s * 0.5f, c.y - s * 0.4f, s, s * 0.8f), _blot, ScaleMode.StretchToFill);
                }
            if (_mist != null && warm > 0f) // 새벽: 위쪽에서 금빛 번짐이 차오르고 화면이 따뜻해져요
            {
                for (int i = 0; i < 3; i++)
                {
                    var sun = new Vector2(w * (0.3f + 0.2f * i), h * Mathf.Lerp(0.2f, 0.05f, warm));
                    float s = Mathf.Lerp(500f, 1100f, warm) * (i == 1 ? 1.2f : 0.9f);
                    Tint(new Color(1f, 0.82f, 0.52f), (i == 1 ? 0.4f : 0.25f) * warm);
                    GUI.DrawTexture(new Rect(sun.x - s * 0.5f, sun.y - s * 0.5f, s, s), _mist, ScaleMode.StretchToFill);
                }
                Tint(new Color(1f, 0.9f, 0.72f), 0.08f * warm);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
            }
            Tint(Color.black, 0.28f); // 대사가 잘 보이게 아래를 살짝 어둡게
            GUI.DrawTexture(new Rect(0f, h * 0.72f, w, h * 0.28f), Texture2D.whiteTexture);

            // ── 제목 낙관
            if (!string.IsNullOrEmpty(line) && line.Contains("일조천회")) DrawTitle(time - _lineStart, w, h);

            // ── 두루마리 대사
            if (!string.IsNullOrEmpty(line))
            {
                if (_scrollStart < 0f) _scrollStart = time;
                DrawScroll(line, time - _scrollStart, time - _lineStart, index, count, w, h);
            }
            GUI.color = old;
        }

        static void DrawScroll(string line, float age, float lineAge, int index, int count, float w, float h)
        {
            float open = Smooth(age / Unroll);
            float cw = Mathf.Max(24f, ScrollW * open), cx = w * 0.5f, y = h - ScrollH - 64f;
            var paper = new Rect(cx - cw * 0.5f, y, cw, ScrollH);
            Tint(Color.black, 0.35f); // 그림자
            GUI.DrawTexture(new Rect(paper.x + 6f, paper.y + 8f, paper.width, paper.height), Texture2D.whiteTexture);
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
                GUIUtility.RotateAroundPivot(90f, new Vector2(x, cy));
                GUI.DrawTexture(new Rect(x - len * 0.5f, cy - thick * 0.5f, len, thick), _rod, ScaleMode.StretchToFill);
                GUI.matrix = _matrix;
            }
            else
            {
                Tint(new Color(0.25f, 0.13f, 0.07f), 1f);
                GUI.DrawTexture(new Rect(x - thick * 0.5f, cy - len * 0.5f, thick, len), Texture2D.whiteTexture);
            }
        }

        static void DrawTitle(float age, float w, float h)
        {
            float k = Smooth((age - 1f) / 1.2f);
            if (k <= 0f) return;
            float th = 520f, tw = _title != null ? th * _title.width / (float)_title.height : 150f;
            var center = new Vector2(w * 0.5f, h * 0.36f);
            if (_mist != null) // 붓글씨 뒤 한지빛 번짐
            {
                Tint(Paper, 0.55f * k);
                float s = th * 1.25f;
                GUI.DrawTexture(new Rect(center.x - s * 0.5f, center.y - s * 0.5f, s, s), _mist, ScaleMode.StretchToFill);
            }
            if (_title != null)
            {
                float sc = Mathf.Lerp(1.06f, 1f, k);
                Tint(Color.white, k);
                GUI.DrawTexture(new Rect(center.x - tw * sc * 0.5f, center.y - th * sc * 0.5f, tw * sc, th * sc), _title, ScaleMode.ScaleToFit);
            }
            float hit = Smooth((age - 2.2f) / 0.14f); // 낙관이 쾅
            if (_seal == null || hit <= 0f) return;
            float ss = 96f * Mathf.Lerp(1.9f, 1f, hit);
            var at = new Vector2(center.x + tw * 0.5f + 40f, center.y + th * 0.5f - 50f);
            Tint(Color.white, hit);
            GUIUtility.RotateAroundPivot(-6f, at);
            GUI.DrawTexture(new Rect(at.x - ss * 0.5f, at.y - ss * 0.5f, ss, ss), _seal, ScaleMode.ScaleToFit);
            GUI.matrix = _matrix;
            float splash = (age - 2.36f) / 0.35f; // 인주가 튀어요
            if (_blot != null && splash > 0f && splash < 1f)
            {
                Tint(new Color(0.8f, 0.1f, 0.08f), 0.4f * (1f - splash));
                float bs = 96f * (1f + splash);
                GUI.DrawTexture(new Rect(at.x - bs * 0.5f, at.y - bs * 0.5f, bs, bs), _blot, ScaleMode.StretchToFill);
            }
        }

        // ── 도움

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static void Tint(Color c, float a) => GUI.color = new Color(c.r, c.g, c.b, a);

        static void Styles()
        {
            if (_line != null) return;
            _line = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
            _line.normal.textColor = Color.white;
            _speaker = new GUIStyle(_line) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _hint = new GUIStyle(_line) { fontSize = 16, alignment = TextAnchor.MiddleRight };
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _title = Resources.Load<Texture2D>("TitleArt/Title");
            _seal = Resources.Load<Texture2D>("TitleArt/Seal");
            _paper = Resources.Load<Texture2D>("FxArt/ScrollPaper");
            _rod = Resources.Load<Texture2D>("FxArt/ScrollRod");
            _blot = Resources.Load<Texture2D>("FxArt/InkBlot");
            _mist = Resources.Load<Texture2D>("FxArt/InkMist");
        }
    }
}
