using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 설정창 (시작 화면·일시정지 메뉴가 같이 써요). 줄마다 ◀ 값 ▶, 바꾸면 바로 저장해요.
    // 키보드: 위아래 줄 고르기 · 좌우 값 바꾸기 · Esc 닫기 (패드: 십자 · B)
    // 소리: 전체 · 음악 · 효과음 (0~100%, 10씩).
    public sealed class SettingsPanel
    {
        public sealed class Row
        {
            public string Section, Label;
            public Func<string> Value;
            public Action<int> Step; // −1 · +1
            public Bind? Key;        // 조작 키 줄: 누르면 다음에 누른 키로 바꿔요
        }

        public int Selected { get; private set; }
        readonly List<Row> _rows;
        [NonSerialized] GUIStyle _title, _section, _label, _value, _hint, _arrow;

        public SettingsPanel()
        {
            _rows = Rows();
            // 조작 키 줄 (기본값으로 줄 앞에)
            int at = _rows.FindIndex(r => r.Section == "조작 키");
            for (int i = KeyBindings.Count - 1; i >= 0; i--)
            {
                var bind = (Bind)i;
                _rows.Insert(at, new Row { Section = "조작 키", Label = KeyBindings.Names[i], Key = bind, Value = () => KeyBindings.Label(bind), Step = d => { } });
            }
        }

        // 키 바꾸기 대기 중인 줄 (−1이면 아님)
        public int ListeningRow { get; private set; } = -1;
        int _listenFrame;
        Vector2 _scroll;

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
            new Row
            {
                Section = "화면", Label = "화면 비율",
                Value = () => S.aspect switch
                {
                    GameSettings.AspectMode.Wide16x9 => "16:9 (남는 곳은 검은 띠)",
                    GameSettings.AspectMode.Wide16x10 => "16:10 (남는 곳은 검은 띠)",
                    GameSettings.AspectMode.Ultra21x9 => "21:9 (남는 곳은 검은 띠)",
                    _ => "자동 (창에 맞춤)",
                },
                Step = d => S.aspect = Cycle(S.aspect, d),
            },
            new Row { Section = "조작", Label = "조준 방식", Value = () => S.aim == GameSettings.AimMode.Mouse ? "마우스 방향" : "이동 방향", Step = d => S.aim = Cycle(S.aim, d) },
            new Row { Section = "조작", Label = "대시 방향", Value = () => S.dash == GameSettings.DashDir.Move ? "이동 방향" : "조준 방향", Step = d => S.dash = Cycle(S.dash, d) },
            new Row
            {
                Section = "게임플레이", Label = "피해 숫자",
                Value = () => !S.damageNumbers ? "끄기" : S.damageNumbersSum ? "합산 표시 (같은 적에게 짧은 시간 안 피해를 하나로)" : "켜기",
                Step = d =>
                {
                    int state = !S.damageNumbers ? 2 : S.damageNumbersSum ? 1 : 0; // 켜기 → 합산 → 끄기
                    state = ((state + d) % 3 + 3) % 3;
                    S.damageNumbers = state != 2;
                    S.damageNumbersSum = state == 1;
                },
            },
            new Row
            {
                Section = "게임플레이", Label = "적 체력바",
                Value = () => S.enemyBars switch { GameSettings.EnemyBars.Always => "항상", GameSettings.EnemyBars.Damaged => "피격 시만", _ => "끄기" },
                Step = d => S.enemyBars = Cycle(S.enemyBars, d),
            },
            new Row { Section = "게임플레이", Label = "경맥도 기 숫자", Value = () => S.qiNumbers == GameSettings.QiNumbers.Always ? "항상" : "마우스를 올렸을 때만", Step = d => S.qiNumbers = Cycle(S.qiNumbers, d) },
            new Row { Section = "게임플레이", Label = "확인 창", Value = () => S.confirmDialogs ? "켜기 (녹이기 · 단전 · 이혈 전에 묻기)" : "끄기", Step = d => S.confirmDialogs = !S.confirmDialogs },
            new Row { Section = "소리", Label = "전체 음량", Value = () => $"{S.masterVolume}%", Step = d => S.masterVolume = Mathf.Clamp(S.masterVolume + d * 10, 0, 100) },
            new Row { Section = "소리", Label = "음악", Value = () => $"{S.musicVolume}%", Step = d => S.musicVolume = Mathf.Clamp(S.musicVolume + d * 10, 0, 100) },
            new Row { Section = "소리", Label = "효과음", Value = () => $"{S.sfxVolume}%", Step = d => S.sfxVolume = Mathf.Clamp(S.sfxVolume + d * 10, 0, 100) },
            new Row { Section = "접근성", Label = "화면 흔들림", Value = () => $"{S.screenShake}%", Step = d => S.screenShake = Mathf.Clamp(S.screenShake + d * 10, 0, 100) },
            new Row { Section = "조작 키", Label = "기본값으로", Value = () => "좌우를 누르면 모든 키를 처음대로", Step = d => KeyBindings.ResetAll() },
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
            if (_rows[row].Key.HasValue) // 조작 키: 다음에 누른 키로 바꿔요
            {
                ListeningRow = row;
                _listenFrame = Time.frameCount;
                return;
            }
            _rows[row].Step(step);
            S.Save();
        }

        // 키 처리. 닫으라고 하면 true
        public bool TickKeys()
        {
            if (ListeningRow >= 0)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame) { ListeningRow = -1; return false; } // 취소 (창은 안 닫혀요)
                if (Time.frameCount == _listenFrame) return false; // 고를 때 누른 키·클릭은 빼요
                string path = KeyBindings.ListenPressed();
                if (path != null)
                {
                    KeyBindings.Set(_rows[ListeningRow].Key.Value, path);
                    ListeningRow = -1;
                }
                return false;
            }
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

            // 줄이 많아서 스크롤 (고른 줄이 늘 보이게)
            var view = new Rect(area.x + 30f, area.y + 80f, area.width - 60f, area.height - 140f);
            float contentH = 0f, selectedY = 0f;
            string section = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Section != section) { section = _rows[i].Section; contentH += 32f; }
                if (i == Selected) selectedY = contentH;
                contentH += 42f;
            }
            if (selectedY < _scroll.y) _scroll.y = selectedY;
            if (selectedY + 42f > _scroll.y + view.height) _scroll.y = selectedY + 42f - view.height;
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, view.width - 20f, contentH));
            float y = 0f;
            float rowW = view.width - 20f;
            section = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Section != section)
                {
                    section = row.Section;
                    GUI.Label(new Rect(0, y, 300f, 30f), section, _section);
                    y += 32f;
                }
                var rect = new Rect(0, y, rowW, 38f);
                if (rect.Contains(Event.current.mousePosition) && ListeningRow < 0) Selected = i;
                if (Selected == i)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.08f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = old;
                }
                GUI.Label(new Rect(rect.x + 16f, rect.y, 260f, rect.height), row.Label, _label);
                if (row.Key.HasValue)
                {
                    // 조작 키: 누르면 바꾸기 대기
                    var keyRect = new Rect(rect.x + 290f, rect.y + 3f, rect.width - 300f, 32f);
                    string label = ListeningRow == i ? "<color=#ffd27a>바꿀 키를 누르세요 (Esc 취소)</color>" : KeyBindings.Label(row.Key.Value);
                    if (GUI.Button(keyRect, label, _arrow) && ListeningRow < 0) { Selected = i; Change(i, +1); }
                }
                else
                {
                    var left = new Rect(rect.x + 290f, rect.y + 3f, 36f, 32f);
                    var right = new Rect(rect.xMax - 46f, rect.y + 3f, 36f, 32f);
                    if (GUI.Button(left, "◀", _arrow)) Change(i, -1);
                    GUI.Label(new Rect(left.xMax + 6f, rect.y, right.x - left.xMax - 12f, rect.height), row.Value(), _value);
                    if (GUI.Button(right, "▶", _arrow)) Change(i, +1);
                }
                y += 42f;
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(area.x + 30f, area.yMax - 44f, area.width - 60f, 30f), "위아래 고르기 · 좌우 바꾸기 (조작 키는 Enter·클릭 뒤 새 키) · Esc 닫기", _hint);
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
