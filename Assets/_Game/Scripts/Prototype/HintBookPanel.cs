using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 무공 요결 창 (일시정지 메뉴): 왼쪽 목록 (분류별) · 오른쪽 본문. 위아래 고르기 · Esc 닫기.
    // Tab (또는 좌우)으로 "기물 도감" 탭: 한 번이라도 얻은 기물 (meta.json seenItems). 못 만난 기물은 ???.
    public sealed class HintBookPanel
    {
        List<HintBook.Entry> _entries = new List<HintBook.Entry>();
        List<ItemDefinition> _items = new List<ItemDefinition>();
        HashSet<string> _seen = new HashSet<string>();
        int _packs;
        Vector2 _scroll, _codexScroll;
        [NonSerialized] GUIStyle _title, _tab, _tabOn, _group, _item, _itemOn, _body, _hint;

        public int Selected { get; private set; }
        public IReadOnlyList<HintBook.Entry> Entries => _entries;
        public int LockedCount { get; private set; }

        public bool CodexTab { get; set; }
        public int CodexSelected { get; private set; }
        public IReadOnlyList<ItemDefinition> CodexItems => _items;
        public int SeenCount { get; private set; }
        public bool Seen(ItemDefinition def) => def != null && _seen.Contains(def.id);

        // 열 때마다 저장에서 다시 읽어요 (그 사이 본 안내 · 얻은 기물이 늘었을 수 있어요)
        public void Refresh()
        {
            var meta = MetaSave.Load();
            _entries = HintBook.Unlocked(meta);
            LockedCount = HintBook.All.Length - _entries.Count;
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, _entries.Count - 1));

            _seen = new HashSet<string>(meta.seenItems);
            _packs = RegressionBook.ItemPacks(meta);
            _items = new List<ItemDefinition>();
            var db = GameConfig.Items;
            if (db != null)
                foreach (var def in db.Items)
                    if (def != null) _items.Add(def);
            // 키워드 → 등급 → 시트 순서
            var order = new Dictionary<ItemDefinition, int>();
            for (int i = 0; i < _items.Count; i++) order[_items[i]] = i;
            _items.Sort((a, b) => a.keyword != b.keyword ? a.keyword.CompareTo(b.keyword) : a.grade != b.grade ? a.grade.CompareTo(b.grade) : order[a].CompareTo(order[b]));
            SeenCount = 0;
            foreach (var def in _items) if (_seen.Contains(def.id)) SeenCount++;
            CodexSelected = Mathf.Clamp(CodexSelected, 0, Mathf.Max(0, _items.Count - 1));
        }

        public bool TickKeys()
        {
            var k = Keyboard.current;
            var pad = Gamepad.current;
            bool up = (k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool tab = (k != null && (k.tabKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame))
                || (pad != null && (pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame));
            if (tab) CodexTab = !CodexTab;
            int count = CodexTab ? _items.Count : _entries.Count;
            if (count > 0)
            {
                int step = up ? -1 : down ? 1 : 0;
                if (CodexTab) CodexSelected = (CodexSelected + step + count) % count;
                else Selected = (Selected + step + count) % count;
            }
            return (k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
        }

        // 닫기 버튼을 누르면 true
        public bool Draw(Rect area)
        {
            EnsureStyles();
            var old = GUI.color;
            GUI.color = new Color(0.08f, 0.07f, 0.07f, 0.94f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;

            if (GUI.Button(new Rect(area.x + 30f, area.y + 20f, 180f, 44f), "무공 요결", CodexTab ? _tab : _tabOn)) CodexTab = false;
            if (GUI.Button(new Rect(area.x + 230f, area.y + 20f, 320f, 44f), $"기물 도감 {SeenCount}/{_items.Count}", CodexTab ? _tabOn : _tab)) CodexTab = true;
            bool close = GUI.Button(new Rect(area.xMax - 130f, area.y + 22f, 100f, 40f), "닫기");

            var listArea = new Rect(area.x + 30f, area.y + 80f, 300f, area.height - 140f);
            var text = new Rect(listArea.xMax + 30f, area.y + 80f, area.xMax - listArea.xMax - 60f, area.height - 140f);
            string footer;
            if (CodexTab)
            {
                int selected = CodexSelected;
                DrawList(listArea, ref _codexScroll, _items.Count, i => PlayerKeywords.Label(_items[i].keyword), i => Seen(_items[i]) ? _items[i].name : "???", ref selected);
                CodexSelected = selected;
                if (_items.Count > 0) DrawItem(text, _items[Mathf.Clamp(CodexSelected, 0, _items.Count - 1)]);
                footer = SeenCount >= _items.Count && _items.Count > 0 ? "모든 기물을 만났어요" : $"아직 못 만난 기물 {_items.Count - SeenCount}종 · 처음 얻으면 남아요";
            }
            else
            {
                int selected = Selected;
                DrawList(listArea, ref _scroll, _entries.Count, i => _entries[i].Group, i => _entries[i].Title, ref selected);
                Selected = selected;
                if (_entries.Count > 0)
                {
                    var e = _entries[Mathf.Clamp(Selected, 0, _entries.Count - 1)];
                    GUI.Label(new Rect(text.x, text.y, text.width, 36f), e.Title, _title);
                    GUI.Label(new Rect(text.x, text.y + 50f, text.width, text.height - 50f), e.Text, _body);
                }
                footer = LockedCount > 0 ? $"아직 모르는 요결 {LockedCount}개 · 그 상황을 처음 겪으면 펼쳐져요" : "모든 요결을 펼쳤어요";
            }
            GUI.Label(new Rect(area.x + 30f, area.yMax - 44f, area.width - 60f, 30f), $"{footer}   ·   Tab 요결/도감 · 위아래 고르기 · Esc 닫기", _hint);
            return close;
        }

        // 왼쪽 목록: 분류가 바뀔 때마다 분류 이름 줄
        void DrawList(Rect listArea, ref Vector2 scroll, int count, Func<int, string> groupOf, Func<int, string> labelOf, ref int selected)
        {
            var old = GUI.color;
            const float rowH = 34f;
            string group = null;
            int groups = 0;
            for (int i = 0; i < count; i++) if (groupOf(i) != group) { group = groupOf(i); groups++; }
            float contentH = count * rowH + groups * 30f;
            scroll = GUI.BeginScrollView(listArea, scroll, new Rect(0, 0, listArea.width - 20f, contentH));
            float y = 0f;
            group = null;
            for (int i = 0; i < count; i++)
            {
                if (groupOf(i) != group)
                {
                    group = groupOf(i);
                    GUI.Label(new Rect(0, y, listArea.width, 28f), group, _group);
                    y += 30f;
                }
                var row = new Rect(0, y, listArea.width - 24f, rowH - 4f);
                if (selected == i)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.1f);
                    GUI.DrawTexture(row, Texture2D.whiteTexture);
                    GUI.color = old;
                }
                if (GUI.Button(row, (selected == i ? "▸ " : "   ") + labelOf(i), selected == i ? _itemOn : _item)) selected = i;
                y += rowH;
            }
            GUI.EndScrollView();
        }

        // 오른쪽: 만난 기물은 효과까지, 못 만난 기물은 등급과 나오는 곳만
        void DrawItem(Rect text, ItemDefinition def)
        {
            string grade = ItemDefinition.GradeLabel(def.grade);
            DrawCodexIcon(text, def);
            text.width -= 160f; // 글은 아이콘 왼쪽까지
            if (!Seen(def))
            {
                int pack = ItemDatabase.PackOf(def.id);
                string where = pack > _packs ? $"회귀록 \"새 기물 묶음 {pack}\"을 피우면 나와요." : "아직 만나지 못했어요. 기물 보상 · 객잔 · 이벤트에서 나와요.";
                GUI.Label(new Rect(text.x, text.y, text.width, 36f), "???", _title);
                GUI.Label(new Rect(text.x, text.y + 50f, text.width, text.height - 50f), $"{PlayerKeywords.Label(def.keyword)} · {grade}\n\n{where}", _body);
                return;
            }
            string aux = string.IsNullOrEmpty(def.aux) ? "" : $" · #{def.aux}";
            string body = $"{PlayerKeywords.Label(def.keyword)}{aux} · {grade} · 기 {def.minQi}–{def.maxQi}\n\n{def.effectText}";
            if (!string.IsNullOrEmpty(def.sideText)) body += $"\n\n<color=#c98a7a>주화입마: {def.sideText}</color>";
            GUI.Label(new Rect(text.x, text.y, text.width, 36f), def.name, _title);
            GUI.Label(new Rect(text.x, text.y + 50f, text.width, text.height - 50f), body, _body);
        }

        // 본문 오른쪽 위 아이콘: 못 만난 기물은 검은 그림자만
        void DrawCodexIcon(Rect text, ItemDefinition def)
        {
            var tex = ItemIcons.Get(def);
            if (tex == null) return;
            var old = GUI.color;
            if (!Seen(def)) GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(text.xMax - 140f, text.y, 140f, 140f), tex, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        void EnsureStyles()
        {
            if (_item != null) return;
            Color text = new Color(0.94f, 0.91f, 0.85f), faint = new Color(0.65f, 0.62f, 0.58f), accent = new Color(0.9f, 0.55f, 0.45f);
            GUIStyle Make(GUIStyle from, int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal, bool wrap = false)
            {
                var s = new GUIStyle(from) { fontSize = size, alignment = anchor, fontStyle = style, richText = true, wordWrap = wrap };
                s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = color;
                s.normal.background = s.hover.background = s.active.background = null;
                return s;
            }
            _title = Make(GUI.skin.label, 28, TextAnchor.MiddleLeft, text, FontStyle.Bold);
            _tab = Make(GUI.skin.button, 26, TextAnchor.MiddleLeft, faint, FontStyle.Bold);
            _tabOn = Make(GUI.skin.button, 28, TextAnchor.MiddleLeft, text, FontStyle.Bold);
            _group = Make(GUI.skin.label, 17, TextAnchor.MiddleLeft, accent, FontStyle.Bold);
            _item = Make(GUI.skin.button, 19, TextAnchor.MiddleLeft, faint);
            _itemOn = Make(GUI.skin.button, 19, TextAnchor.MiddleLeft, text, FontStyle.Bold);
            _body = Make(GUI.skin.label, 21, TextAnchor.UpperLeft, text, FontStyle.Normal, true);
            _hint = Make(GUI.skin.label, 15, TextAnchor.MiddleLeft, faint);
        }
    }
}
