using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 무공 요결 창 (일시정지 메뉴): 왼쪽 목록 (분류별) · 오른쪽 본문. 위아래 고르기 · Esc 닫기.
    public sealed class HintBookPanel
    {
        List<HintBook.Entry> _entries = new List<HintBook.Entry>();
        Vector2 _scroll;
        [NonSerialized] GUIStyle _title, _group, _item, _itemOn, _body, _hint;

        public int Selected { get; private set; }
        public IReadOnlyList<HintBook.Entry> Entries => _entries;
        public int LockedCount { get; private set; }

        // 열 때마다 저장에서 다시 읽어요 (그 사이 본 안내가 늘었을 수 있어요)
        public void Refresh()
        {
            _entries = HintBook.Unlocked(MetaSave.Load());
            LockedCount = HintBook.All.Length - _entries.Count;
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, _entries.Count - 1));
        }

        public bool TickKeys()
        {
            var k = Keyboard.current;
            var pad = Gamepad.current;
            bool up = (k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            if (_entries.Count > 0)
            {
                if (up) Selected = (Selected + _entries.Count - 1) % _entries.Count;
                if (down) Selected = (Selected + 1) % _entries.Count;
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

            GUI.Label(new Rect(area.x + 30f, area.y + 20f, area.width - 200f, 44f), "무공 요결", _title);
            bool close = GUI.Button(new Rect(area.xMax - 130f, area.y + 22f, 100f, 40f), "닫기");

            // 왼쪽: 목록
            var listArea = new Rect(area.x + 30f, area.y + 80f, 300f, area.height - 140f);
            float rowH = 34f;
            string group = null;
            int groups = 0;
            foreach (var e in _entries) if (e.Group != group) { group = e.Group; groups++; }
            float contentH = _entries.Count * rowH + groups * 30f;
            _scroll = GUI.BeginScrollView(listArea, _scroll, new Rect(0, 0, listArea.width - 20f, contentH));
            float y = 0f;
            group = null;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Group != group)
                {
                    group = e.Group;
                    GUI.Label(new Rect(0, y, listArea.width, 28f), group, _group);
                    y += 30f;
                }
                var row = new Rect(0, y, listArea.width - 24f, rowH - 4f);
                if (Selected == i)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.1f);
                    GUI.DrawTexture(row, Texture2D.whiteTexture);
                    GUI.color = old;
                }
                if (GUI.Button(row, (Selected == i ? "▸ " : "   ") + e.Title, Selected == i ? _itemOn : _item)) Selected = i;
                y += rowH;
            }
            GUI.EndScrollView();

            // 오른쪽: 본문
            var text = new Rect(listArea.xMax + 30f, area.y + 80f, area.xMax - listArea.xMax - 60f, area.height - 140f);
            if (_entries.Count > 0)
            {
                var e = _entries[Mathf.Clamp(Selected, 0, _entries.Count - 1)];
                GUI.Label(new Rect(text.x, text.y, text.width, 36f), e.Title, _title);
                GUI.Label(new Rect(text.x, text.y + 50f, text.width, text.height - 50f), e.Text, _body);
            }
            string locked = LockedCount > 0 ? $"아직 모르는 요결 {LockedCount}개 · 그 상황을 처음 겪으면 펼쳐져요" : "모든 요결을 펼쳤어요";
            GUI.Label(new Rect(area.x + 30f, area.yMax - 44f, area.width - 60f, 30f), $"{locked}   ·   위아래 고르기 · Esc 닫기", _hint);
            return close;
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
            _group = Make(GUI.skin.label, 17, TextAnchor.MiddleLeft, accent, FontStyle.Bold);
            _item = Make(GUI.skin.button, 19, TextAnchor.MiddleLeft, faint);
            _itemOn = Make(GUI.skin.button, 19, TextAnchor.MiddleLeft, text, FontStyle.Bold);
            _body = Make(GUI.skin.label, 21, TextAnchor.UpperLeft, text, FontStyle.Normal, true);
            _hint = Make(GUI.skin.label, 15, TextAnchor.MiddleLeft, faint);
        }
    }
}
