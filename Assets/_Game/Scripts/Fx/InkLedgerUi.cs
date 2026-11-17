using System.Text.RegularExpressions;
using AThousandDawns.Core;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 사당 장부 화면 (유품록 relic_book · 업보 karma · 사당 메뉴 relic_book_menu) — 영혼수련처럼 한지 바탕의 수묵 장부 (4차 플레이 테스트).
    //   왼쪽: 제목 붓글씨 · 그림 (먹 테두리) · 설명 글. 오른쪽: 한지 줄마다 고른 것(■)은 붉은 낙관, 아닌 것(□)은 빈 먹 동그라미(원상).
    //   줄 글: 첫 줄은 이름 (짙은 먹, 크게), '\n' 뒤는 효과 설명 (한 단계 옅은 먹, 읽히는 크기). 줄 바탕은 옅은 한지 + 아래 가는 먹선뿐 — 글자를 덮는 것은 없어요.
    //   오른쪽 위: ✕ 낙관 닫기 (run.CloseEvent, Esc도 그대로). 맨 아래 "뒤로 가기" 줄은 게임 쪽이 처리해요.
    //   유품을 고르면(RoomRun.RelicToggled true) 그 줄의 낙관이 쾅 찍히고, 빼면 낙관이 옅어지며 빈 동그라미로 돌아가요.
    // 사용: PrototypeHud.DrawEvent 맨 앞에서 if (InkLedgerUi.Draw(run, ev)) return;
    public static class InkLedgerUi
    {
        static readonly Color Ink = InkUi.Ink;
        static readonly Color SubInk = InkUi.SubInk;
        static readonly Color Muted = InkUi.Muted;
        static readonly Color Vermilion = InkUi.Accent;
        static readonly Regex SizeTag = new Regex("</?size[^>]*>");

        static GUIStyle _title, _text, _row, _rowLocked, _desc, _num, _close;
        static Texture2D _seal, _stroke, _enso, _blot;
        static bool _loaded;
        static string _toggledName;  // 방금 고르거나 뺀 유품 이름
        static bool _toggledOn;
        static float _toggledAt = -10f;

        public static bool Handles(string id) => id != null && (id.StartsWith("relic_book") || id == "karma");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            RoomRun.RelicToggled -= OnRelic;
            RoomRun.RelicToggled += OnRelic;
        }

        static void OnRelic(string itemId, bool picked)
        {
            var def = GameConfig.Items != null ? GameConfig.Items.Find(itemId) : null;
            _toggledName = def != null ? def.name : null;
            _toggledOn = picked;
            _toggledAt = Time.unscaledTime;
        }

        public static bool Draw(RoomRun run, RoomRun.EventView ev)
        {
            if (run == null || ev == null || !Handles(ev.Id)) return false;
            Load();
            Styles();
            float W = UiScale.Width, H = UiScale.Height;
            InkMeridianArt.PaperBackdrop(W, H);
            var old = GUI.color;
            var mouse = Event.current.mousePosition;

            // 왼쪽: 제목 · 그림 · 글
            const float left = 90f, colW = 600f;
            GUI.color = Color.white;
            GUI.Label(new Rect(left, 60f, colW, 70f), ev.Title, _title);
            float y = 150f;
            var art = Items.ItemIcons.EventArt(ev.Art ?? ev.Id) ?? Items.ItemIcons.EventArt("relic_book");
            if (art != null)
            {
                var r = new Rect(left, y, colW, colW * 9f / 16f);
                GUI.DrawTexture(r, art, ScaleMode.ScaleAndCrop);
                InkPadUi.Frame(r, 2f);
                y = r.yMax + 24f;
            }
            GUI.Label(new Rect(left, y, colW, H - y - 80f), ev.Text, _text);

            // 오른쪽 위: ✕ 낙관 닫기
            float rx = left + colW + 70f, rw = W - rx - 80f;
            var close = new Rect(W - 80f - 68f, 52f, 68f, 68f);
            bool closeOver = close.Contains(mouse);
            if (_blot != null)
            {
                GUI.color = closeOver ? new Color(0.82f, 0.18f, 0.12f, 1f) : new Color(Vermilion.r, Vermilion.g, Vermilion.b, 0.92f);
                GUI.DrawTexture(Grow(close, closeOver ? 8f : 0f), _blot, ScaleMode.StretchToFill);
            }
            GUI.color = Color.white;
            GUI.Label(close, "✕", _close);
            if (GUI.Button(close, GUIContent.none, GUIStyle.none)) { run.CloseEvent(); GUI.color = old; return true; }

            // 오른쪽: 장부 줄
            const float gap = 6f, listTop = 150f;
            if (ev.Result != null)
            {
                var rr = new Rect(rx, listTop, rw, 100f);
                RowBack(rr, false, false);
                GUI.color = Color.white;
                GUI.Label(new Rect(rr.x + 24f, rr.y, rr.width - 48f, rr.height), $"{ev.Result}\n<size=19><color=#5a4f47>[1] 닫기</color></size>", _row);
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) run.CloseEvent();
                GUI.color = old;
                return true;
            }
            int n = ev.Choices.Count;
            float rowH = Mathf.Min(80f, (H - listTop - 60f) / Mathf.Max(1, n) - gap);
            for (int i = 0; i < n; i++)
            {
                var c = ev.Choices[i];
                var rr = new Rect(rx, listTop + i * (rowH + gap), rw, rowH);
                bool locked = c.Locked != null;
                bool over = rr.Contains(mouse) && !locked;
                bool cursor = Pad.Active && i == ev.Cursor;
                RowBack(rr, over || cursor, locked);

                string label = c.Label ?? "", desc = null;
                int nl = label.IndexOf('\n');
                if (nl >= 0) { desc = SizeTag.Replace(label.Substring(nl + 1), "").Trim(); label = label.Substring(0, nl); }
                bool picked = label.StartsWith("■"), box = picked || label.StartsWith("□");
                if (box) label = label.Substring(1).TrimStart();

                var mark = new Rect(rr.x + 16f, rr.y + (rowH - 46f) * 0.5f, 46f, 46f);
                if (box) DrawMark(mark, picked, label);
                else { GUI.color = Muted; GUI.Label(mark, (i + 1).ToString(), _num); }
                GUI.color = Color.white;
                float tx = mark.xMax + 18f, tw = rr.xMax - tx - 20f;
                if (locked) label = $"{label}  <size=18>· {c.Locked}</size>";
                if (string.IsNullOrEmpty(desc))
                    GUI.Label(new Rect(tx, rr.y, tw, rowH), label, locked ? _rowLocked : _row);
                else
                {
                    GUI.Label(new Rect(tx, rr.y + 4f, tw, rowH * 0.54f), label, locked ? _rowLocked : _row);
                    GUI.Label(new Rect(tx, rr.y + rowH * 0.5f, tw, rowH * 0.48f), desc, _desc);
                }
                GUI.enabled = !locked;
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) run.ChooseEvent(i);
                GUI.enabled = true;
            }
            GUI.color = old;
            return true;
        }

        // 줄 바탕: 옅은 한지 + 아래 가는 먹선 (마우스·패드: 붉은 기운 + 왼쪽 붉은 붓 띠). 글자 뒤에만 그려요.
        static void RowBack(Rect r, bool hot, bool locked)
        {
            var old = GUI.color;
            GUI.color = hot ? new Color(1f, 0.95f, 0.9f, 0.95f) : locked ? new Color(0.9f, 0.88f, 0.84f, 0.6f) : new Color(1f, 0.99f, 0.96f, 0.75f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.45f);
            var line = new Rect(r.x, r.yMax - 2f, r.width, 3f);
            if (_stroke != null) GUI.DrawTexture(line, _stroke, ScaleMode.StretchToFill);
            else GUI.DrawTexture(line, Texture2D.whiteTexture);
            if (hot)
            {
                GUI.color = Vermilion;
                GUI.DrawTexture(new Rect(r.x, r.y + 6f, 5f, r.height - 12f), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        // 고른 것: 붉은 낙관 (방금 고른 줄은 쾅 찍혀요) · 아닌 것: 빈 먹 동그라미 (방금 뺀 줄은 낙관이 옅어지며 사라져요)
        static void DrawMark(Rect r, bool picked, string label)
        {
            float age = Time.unscaledTime - _toggledAt;
            bool fresh = age < 0.6f && _toggledName != null && label.Contains(_toggledName);
            var old = GUI.color;
            if (_enso != null)
            {
                GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.6f);
                GUI.DrawTexture(r, _enso, ScaleMode.ScaleToFit);
            }
            if (picked && _seal != null)
            {
                float k = fresh && _toggledOn ? Mathf.Clamp01(age / 0.12f) : 1f;
                float s = Mathf.Lerp(1.8f, 1f, k * k);
                var sr = new Rect(r.center.x - r.width * 0.5f * s, r.center.y - r.height * 0.5f * s, r.width * s, r.height * s);
                GUI.color = new Color(1f, 1f, 1f, k);
                GUI.DrawTexture(sr, _seal, ScaleMode.ScaleToFit);
                if (fresh && _toggledOn && age > 0.12f && age < 0.45f) // 인주가 튀어요
                {
                    float sp = (age - 0.12f) / 0.33f;
                    GUI.color = new Color(Vermilion.r, Vermilion.g, Vermilion.b, 0.4f * (1f - sp));
                    float g = r.width * (0.6f + sp);
                    GUI.DrawTexture(new Rect(r.center.x - g * 0.5f, r.center.y - g * 0.5f, g, g), _seal, ScaleMode.ScaleToFit);
                }
            }
            else if (fresh && !_toggledOn && _seal != null) // 방금 뺐어요: 낙관만 옅어져요
            {
                GUI.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(age / 0.5f));
                GUI.DrawTexture(r, _seal, ScaleMode.ScaleToFit);
            }
            GUI.color = old;
        }

        static Rect Grow(Rect r, float d) => new Rect(r.x - d * 0.5f, r.y - d * 0.5f, r.width + d, r.height + d);

        static void Styles() // UI 규칙 (InkUi): 제목 Title · 줄 Body · 설명 Label · 닫기 Heading
        {
            if (_title != null) return;
            _title = InkUi.Text(InkUi.Title, Ink, TextAnchor.UpperLeft, bold: true);
            _text = InkUi.Text(InkUi.Body, Ink, TextAnchor.UpperLeft, wrap: true);
            _row = new GUIStyle(InkUi.Text(InkUi.Body, Ink)) { clipping = TextClipping.Clip };
            _rowLocked = new GUIStyle(InkUi.Text(InkUi.Body, Muted)) { clipping = TextClipping.Clip };
            _desc = new GUIStyle(InkUi.Text(InkUi.Label, SubInk)) { clipping = TextClipping.Clip };
            _num = InkUi.Text(InkUi.Body, Color.white, TextAnchor.MiddleCenter, bold: true);
            _close = InkUi.Text(InkUi.Heading, InkUi.PaperText, TextAnchor.MiddleCenter, bold: true);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _seal = Resources.Load<Texture2D>("TitleArt/Seal");
            _stroke = Resources.Load<Texture2D>("HudArt/HpStroke");
            _enso = Resources.Load<Texture2D>("HudArt/Enso");
            _blot = Resources.Load<Texture2D>("HudArt/Blot");
        }
    }
}
