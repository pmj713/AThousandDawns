using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 설정창 (시작 화면·일시정지 메뉴가 같이 써요). 줄마다 ◀ 값 ▶, 바꾸면 바로 저장해요.
    // 키보드: 위아래 줄 고르기 · 좌우 값 바꾸기 · Esc 닫기 (패드: 십자 · B)
    // 소리(음량)는 다른 작업에서 Rows()에 줄을 더해요.
    public sealed class SettingsPanel
    {
        public sealed class Row
        {
            public string Section, Label;
            public Func<string> Value;
            public Action<int> Step; // −1 · +1
        }

        public int Selected { get; private set; }
        readonly List<Row> _rows;
        [NonSerialized] GUIStyle _title, _section, _label, _value, _hint, _arrow;

        public SettingsPanel() => _rows = Rows();

        public IReadOnlyList<Row> AllRows => _rows;

        static GameSettings S => GameSettings.Current;
        static bool _tutorialReset;

        static T Cycle<T>(T value, int step) where T : Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int i = Array.IndexOf(values, value);
            return values[((i + step) % values.Length + values.Length) % values.Length];
        }

        // 해상도 목록: 모니터가 지원하는 크기 (같은 크기는 하나로), 비율을 같이 보여 줘요
        static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!list.Contains(v)) list.Add(v);
            }
            var now = new Vector2Int(S.width > 0 ? S.width : Screen.width, S.height > 0 ? S.height : Screen.height);
            if (!list.Contains(now)) list.Add(now);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        public static string Aspect(int w, int h)
        {
            int a = w, b = h;
            while (b != 0) (a, b) = (b, a % b);
            int x = w / Mathf.Max(1, a), y = h / Mathf.Max(1, a);
            if (x == 8 && y == 5) return "16:10";
            if (x == 64 && y == 27 || x == 43 && y == 18 || x == 21 && y == 9) return "21:9";
            return $"{x}:{y}";
        }

        static List<Row> Rows() => new List<Row>
        {
            new Row
            {
                Section = "화면", Label = "해상도",
                Value = () =>
                {
                    int w = S.width > 0 ? S.width : Screen.width, h = S.height > 0 ? S.height : Screen.height;
                    return $"{w} × {h}  ({Aspect(w, h)})";
                },
                Step = d =>
                {
                    var list = Resolutions();
                    var now = new Vector2Int(S.width > 0 ? S.width : Screen.width, S.height > 0 ? S.height : Screen.height);
                    int i = Mathf.Max(0, list.IndexOf(now));
                    var next = list[((i + d) % list.Count + list.Count) % list.Count];
                    S.width = next.x;
                    S.height = next.y;
                    S.ApplyDisplay();
                },
            },
            new Row
            {
                Section = "화면", Label = "화면 모드",
                Value = () => S.screenMode switch
                {
                    FullScreenMode.ExclusiveFullScreen => "전체 화면",
                    FullScreenMode.FullScreenWindow => "테두리 없는 창",
                    _ => "창",
                },
                Step = d =>
                {
                    FullScreenMode[] modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
                    int i = Mathf.Max(0, Array.IndexOf(modes, S.screenMode));
                    S.screenMode = modes[((i + d) % modes.Length + modes.Length) % modes.Length];
                    S.ApplyDisplay();
                },
            },
            new Row { Section = "조작", Label = "조준 방식", Value = () => S.aim == GameSettings.AimMode.Mouse ? "마우스 방향" : "이동 방향", Step = d => S.aim = Cycle(S.aim, d) },
            new Row { Section = "조작", Label = "대시 방향", Value = () => S.dash == GameSettings.DashDir.Move ? "이동 방향" : "조준 방향", Step = d => S.dash = Cycle(S.dash, d) },
            new Row { Section = "게임플레이", Label = "피해 숫자", Value = () => S.damageNumbers ? "켜기" : "끄기", Step = d => S.damageNumbers = !S.damageNumbers },
            new Row
            {
                Section = "게임플레이", Label = "적 체력바",
                Value = () => S.enemyBars switch { GameSettings.EnemyBars.Always => "항상", GameSettings.EnemyBars.Damaged => "피격 시만", _ => "끄기" },
                Step = d => S.enemyBars = Cycle(S.enemyBars, d),
            },
            new Row { Section = "게임플레이", Label = "경맥도 기 숫자", Value = () => S.qiNumbers == GameSettings.QiNumbers.Always ? "항상" : "마우스를 올렸을 때만", Step = d => S.qiNumbers = Cycle(S.qiNumbers, d) },
            new Row { Section = "게임플레이", Label = "확인 창", Value = () => S.confirmDialogs ? "켜기 (녹이기 · 단전 · 이혈 전에 묻기)" : "끄기", Step = d => S.confirmDialogs = !S.confirmDialogs },
            new Row { Section = "접근성", Label = "화면 흔들림", Value = () => $"{S.screenShake}%", Step = d => S.screenShake = Mathf.Clamp(S.screenShake + d * 10, 0, 100) },
            new Row
            {
                Section = "기타", Label = "튜토리얼 다시 보기",
                Value = () => _tutorialReset ? "다음 판부터 처음 안내를 다시 봐요" : "좌우를 누르면 첫 안내 기록을 지워요",
                Step = d => { RoomRun.ResetTutorial(); _tutorialReset = true; },
            },
        };

        public void Change(int row, int step)
        {
            if (row < 0 || row >= _rows.Count) return;
            _rows[row].Step(step);
            S.Save();
        }

        // 키 처리. 닫으라고 하면 true
        public bool TickKeys()
        {
            var k = Keyboard.current;
            var pad = Gamepad.current;
            bool Pressed(Func<Keyboard, bool> key, Func<Gamepad, bool> button) => (k != null && key(k)) || (pad != null && button(pad));
            if (Pressed(x => x.upArrowKey.wasPressedThisFrame || x.wKey.wasPressedThisFrame, p => p.dpad.up.wasPressedThisFrame)) Selected = (Selected + _rows.Count - 1) % _rows.Count;
            if (Pressed(x => x.downArrowKey.wasPressedThisFrame || x.sKey.wasPressedThisFrame, p => p.dpad.down.wasPressedThisFrame)) Selected = (Selected + 1) % _rows.Count;
            if (Pressed(x => x.leftArrowKey.wasPressedThisFrame || x.aKey.wasPressedThisFrame, p => p.dpad.left.wasPressedThisFrame)) Change(Selected, -1);
            if (Pressed(x => x.rightArrowKey.wasPressedThisFrame || x.dKey.wasPressedThisFrame || x.enterKey.wasPressedThisFrame, p => p.dpad.right.wasPressedThisFrame || p.buttonSouth.wasPressedThisFrame)) Change(Selected, +1);
            return Pressed(x => x.escapeKey.wasPressedThisFrame, p => p.buttonEast.wasPressedThisFrame);
        }

        // 어두운 판 위에 그려요. 닫기 버튼을 누르면 true
        public bool Draw(Rect area)
        {
            EnsureStyles();
            var old = GUI.color;
            GUI.color = new Color(0.08f, 0.07f, 0.07f, 0.92f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(area.x + 30f, area.y + 20f, area.width - 60f, 44f), "설정", _title);
            float y = area.y + 80f;
            string section = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Section != section)
                {
                    section = row.Section;
                    GUI.Label(new Rect(area.x + 30f, y, 300f, 30f), section, _section);
                    y += 32f;
                }
                var rect = new Rect(area.x + 30f, y, area.width - 60f, 38f);
                if (rect.Contains(Event.current.mousePosition)) Selected = i;
                if (Selected == i)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.08f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = old;
                }
                GUI.Label(new Rect(rect.x + 16f, rect.y, 260f, rect.height), row.Label, _label);
                var left = new Rect(rect.x + 290f, rect.y + 3f, 36f, 32f);
                var right = new Rect(rect.xMax - 46f, rect.y + 3f, 36f, 32f);
                if (GUI.Button(left, "◀", _arrow)) Change(i, -1);
                GUI.Label(new Rect(left.xMax + 6f, rect.y, right.x - left.xMax - 12f, rect.height), row.Value(), _value);
                if (GUI.Button(right, "▶", _arrow)) Change(i, +1);
                y += 42f;
            }
            GUI.Label(new Rect(area.x + 30f, area.yMax - 44f, area.width - 60f, 30f), "위아래 고르기 · 좌우 바꾸기 · Esc 닫기   (소리 설정은 아직 없어요)", _hint);
            return GUI.Button(new Rect(area.xMax - 130f, area.y + 22f, 100f, 40f), "닫기", _arrow);
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            Color text = new Color(0.94f, 0.91f, 0.85f), faint = new Color(0.65f, 0.62f, 0.58f), accent = new Color(0.9f, 0.55f, 0.45f);
            GUIStyle Make(int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal)
            {
                var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = style, richText = true };
                s.normal.textColor = s.hover.textColor = s.active.textColor = color;
                return s;
            }
            _title = Make(30, TextAnchor.MiddleLeft, text, FontStyle.Bold);
            _section = Make(18, TextAnchor.MiddleLeft, accent, FontStyle.Bold);
            _label = Make(20, TextAnchor.MiddleLeft, text);
            _value = Make(20, TextAnchor.MiddleCenter, text, FontStyle.Bold);
            _hint = Make(16, TextAnchor.MiddleLeft, faint);
            _arrow = new GUIStyle(GUI.skin.button) { fontSize = 18 };
        }
    }
}
