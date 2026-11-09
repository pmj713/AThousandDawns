using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 회귀록 화면 (시험판 IMGUI): 매화나무 가지에 봉우리가 달려 있어요.
    // 봉우리를 누르면 오른쪽에 설명·비용·조건 → "개화"를 누르면 기억을 쓰고 꽃이 피어 바로 적용 (다음 판부터).
    // 결과 화면에서 B로 열고 B·Esc로 닫아요.
    public sealed class RegressionBookScreen : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.12f, 0.1f, 0.1f);
        static readonly Color Branch = new Color(0.36f, 0.27f, 0.22f);
        static readonly Color Bud = new Color(0.55f, 0.3f, 0.35f);
        static readonly Color Bloom = new Color(0.96f, 0.62f, 0.7f);  // 매화
        static readonly Color Locked = new Color(0.35f, 0.33f, 0.33f);
        static readonly Color Selected = new Color(1f, 0.85f, 0.4f);

        // 기준 1920×1080 화면 좌표. 뿌리는 아래 가운데, 줄기가 위로, 가지가 옆으로.
        static readonly Dictionary<string, Vector2> Layout = new Dictionary<string, Vector2>
        {
            [RegressionBook.Root] = new Vector2(820, 960),
            ["bag"] = new Vector2(960, 880), ["bag_plus1"] = new Vector2(1080, 830), ["bag_plus2"] = new Vector2(1190, 770),
            ["tongmaek"] = new Vector2(660, 880), ["jeomhyeol"] = new Vector2(540, 800), ["hanok"] = new Vector2(450, 700),
            ["forge"] = new Vector2(380, 590), ["flying_dagger"] = new Vector2(330, 470),
            ["meditation"] = new Vector2(820, 800), ["insight1"] = new Vector2(820, 680), ["insight2"] = new Vector2(820, 560),
            ["insight3"] = new Vector2(820, 440), ["insight4"] = new Vector2(820, 320), ["insight5"] = new Vector2(820, 200),
            ["potion_kit"] = new Vector2(960, 730), ["inn_stock"] = new Vector2(1090, 660),
            ["item_pack1"] = new Vector2(680, 600), ["item_pack2"] = new Vector2(570, 510),
            ["relic1"] = new Vector2(960, 990), ["relic2"] = new Vector2(1100, 990), ["relic3"] = new Vector2(1240, 980), ["relic4"] = new Vector2(1380, 960),
        };

        MetaSave _meta;
        string _selected, _notice;
        float _noticeTimer;
        GUIStyle _title, _label, _small, _node, _button;

        public bool IsOpen { get; private set; }
        public MetaSave Meta => _meta;

        public void Open()
        {
            _meta = MetaSave.Load();
            _selected = null;
            IsOpen = true;
        }

        public void Close() => IsOpen = false;

        // 봉우리 고르기 → 개화 (테스트·키보드용)
        public void Select(string id) => _selected = id;

        public bool BloomSelected()
        {
            if (_selected == null || _meta == null) return false;
            if (!RegressionBook.Bloom(_meta, _selected)) return false;
            _meta.Save();
            _notice = $"{RegressionBook.Find(_selected).Name} · 꽃이 피었어요 (다음 판부터)";
            _noticeTimer = 2.5f;
            return true;
        }

        void Update()
        {
            if (_noticeTimer > 0f) _noticeTimer -= Time.unscaledDeltaTime;
            if (!IsOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        void OnGUI()
        {
            if (!IsOpen || _meta == null) return;
            UiScale.Apply();
            EnsureStyles();
            GUI.depth = -10;
            Fill(new Rect(0, 0, UiScale.Width, UiScale.Height), new Color(0.93f, 0.9f, 0.84f, 0.97f)); // 한지

            GUI.Label(new Rect(60, 40, 900, 50), "회귀록", _title);
            GUI.Label(new Rect(60, 100, 900, 30), $"가진 회귀의 기억 <b>{_meta.memory}</b>   ·   {_meta.runs}번 회귀했어요", _label);
            GUI.Label(new Rect(60, 132, 900, 26), "봉우리를 누르고, 개화를 눌러 피워요 · B·Esc 닫기", _small);

            // 가지 (선행 → 봉우리)
            foreach (var node in RegressionBook.Nodes)
                if (Layout.TryGetValue(node.Id, out var to) && Layout.TryGetValue(node.Prereq, out var from))
                    Line(from, to, RegressionBook.Bloomed(_meta, node.Prereq) ? Branch : Locked, 6f);

            // 뿌리
            var root = Layout[RegressionBook.Root];
            Fill(new Rect(root.x - 26, root.y - 14, 52, 28), Branch);
            GUI.Label(new Rect(root.x - 60, root.y + 16, 120, 24), "뿌리", _small);

            foreach (var node in RegressionBook.Nodes)
            {
                if (!Layout.TryGetValue(node.Id, out var p)) continue;
                bool bloomed = RegressionBook.Bloomed(_meta, node.Id);
                string blocker = RegressionBook.Blocker(_meta, node);
                bool openable = !bloomed && RegressionBook.Bloomed(_meta, node.Prereq);
                float size = bloomed ? 40f : 28f;
                var rect = new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);
                if (_selected == node.Id) Fill(new Rect(rect.x - 5, rect.y - 5, rect.width + 10, rect.height + 10), Selected);
                Fill(rect, bloomed ? Bloom : openable ? (blocker == null ? Bud : new Color(0.45f, 0.32f, 0.34f)) : Locked);
                GUI.Label(new Rect(p.x - 80, p.y + size * 0.5f + 2, 160, 22), bloomed ? $"<b>{node.Name}</b>" : node.Name, _node);
                if (GUI.Button(new Rect(rect.x - 6, rect.y - 6, rect.width + 12, rect.height + 12), GUIContent.none, GUIStyle.none)) _selected = node.Id;
            }

            DrawDetail();
            if (_noticeTimer > 0f && _notice != null) GUI.Label(new Rect(0, UiScale.Height - 80, UiScale.Width, 40), _notice, _title);
        }

        void DrawDetail()
        {
            var panel = new Rect(UiScale.Width - 560, 180, 500, 330);
            Fill(panel, new Color(1f, 1f, 1f, 0.6f));
            if (_selected == null)
            {
                GUI.Label(new Rect(panel.x + 20, panel.y + 20, panel.width - 40, 60), "봉우리를 눌러 보세요.", _label);
                return;
            }
            var node = RegressionBook.Find(_selected);
            int cost = RegressionBook.Cost(node);
            bool bloomed = RegressionBook.Bloomed(_meta, node.Id);
            string blocker = RegressionBook.Blocker(_meta, node);
            float y = panel.y + 18;
            GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 40), $"<b>{node.Name}</b>", _title);
            y += 48;
            GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 26), $"{node.Branch} 가지 · 비용 기억 {cost}", _label);
            y += 32;
            GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 90), node.Effect, _small);
            y += 96;
            if (bloomed)
            {
                GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 30), "<color=#b0506a>피었어요 · 적용 중</color>", _label);
                return;
            }
            if (blocker != null) GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 30), $"<color=#7a6a6a>{blocker}</color>", _label);
            y += 40;
            GUI.enabled = blocker == null;
            if (GUI.Button(new Rect(panel.x + 20, y, panel.width - 40, 50), $"개화 · 기억 {cost}", _button)) BloomSelected();
            GUI.enabled = true;
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            var old = GUI.matrix;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(a, b);
            GUIUtility.RotateAroundPivot(angle, a);
            Fill(new Rect(a.x, a.y - width * 0.5f, length, width), c);
            GUI.matrix = old;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleLeft, richText = true };
            _title.normal.textColor = Ink;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true, wordWrap = true };
            _label.normal.textColor = Ink;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = true };
            _small.normal.textColor = new Color(0.3f, 0.26f, 0.24f);
            _node = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter, richText = true };
            _node.normal.textColor = Ink;
            _button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        }
    }
}
