using System.Reflection;
using AThousandDawns.Core;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 사당 장부 화면 (유품록 relic_book · 업보 karma · 사당 메뉴 relic_book_menu) — 면벽수련처럼 한지 바탕의 수묵 장부 (4차 플레이 테스트).
    //   왼쪽: 제목 붓글씨 · 그림 (먹 테두리) · 설명 글. 오른쪽: 한지 판 줄마다 고른 것(■)은 붉은 낙관, 아닌 것(□)은 빈 먹 동그라미.
    //   아래 오른쪽: 마우스·패드로 고른 줄의 설명 칸 (기물 설명은 EventChoice.Description이 생기면 그걸 그려요).
    //   유품을 고르거나 빼면(RoomRun.RelicToggled) 그 줄의 낙관이 쾅 찍히거나, 먹 획에 지워지며 사라져요.
    // 사용: PrototypeHud.DrawEvent 맨 앞에서 if (InkLedgerUi.Draw(run, ev)) return;
    public static class InkLedgerUi
    {
        static readonly Color Ink = new Color(0.1f, 0.09f, 0.08f);
        static readonly Color Muted = new Color(0.45f, 0.42f, 0.4f);
        static readonly Color Vermilion = new Color(0.72f, 0.16f, 0.1f);

        static GUIStyle _title, _text, _row, _rowLocked, _small, _num;
        static Texture2D _seal, _stroke;
        static bool _loaded;
        static readonly FieldInfo DescriptionField = typeof(RoomRun.EventChoice).GetField("Description"); // 기획이 넣으면 그려요
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

            // 왼쪽: 제목 · 그림 · 글
            const float left = 90f, colW = 640f;
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
            GUI.Label(new Rect(left, y, colW, H - y - 120f), ev.Text, _text);

            // 오른쪽: 장부 줄
            float rx = left + colW + 80f, rw = W - rx - 90f;
            const float rowH = 66f, gap = 8f;
            float listTop = 150f;
            int hover = -1;
            var mouse = Event.current.mousePosition;
            if (ev.Result != null)
            {
                var rr = new Rect(rx, listTop, rw, rowH * 1.4f);
                InkMeridianArt.PaperPanel(rr);
                GUI.color = Color.white;
                GUI.Label(Shrink(rr, 24f, 10f), $"{ev.Result}\n<size=17><color=#6b6058>[1] 닫기</color></size>", _row);
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) run.CloseEvent();
                GUI.color = old;
                return true;
            }
            for (int i = 0; i < ev.Choices.Count; i++)
            {
                var c = ev.Choices[i];
                var rr = new Rect(rx, listTop + i * (rowH + gap), rw, rowH);
                bool locked = c.Locked != null;
                bool over = rr.Contains(mouse) && !locked;
                if (over || (Pad.Active && i == ev.Cursor)) hover = i;
                InkMeridianArt.PaperPanel(rr, over ? new Color(1f, 0.93f, 0.88f, 0.97f) : locked ? new Color(0.92f, 0.9f, 0.86f, 0.9f) : new Color(1f, 0.99f, 0.96f, 0.97f));
                string label = c.Label ?? "";
                bool picked = label.StartsWith("■"), box = picked || label.StartsWith("□");
                if (box) label = label.Substring(1).TrimStart();
                var mark = new Rect(rr.x + 14f, rr.y + (rowH - 44f) * 0.5f, 44f, 44f);
                if (box) DrawMark(mark, picked, label);
                else { GUI.color = Muted; GUI.Label(mark, (i + 1).ToString(), _num); }
                GUI.color = Color.white;
                var textRect = new Rect(mark.xMax + 16f, rr.y, rr.width - mark.width - 46f, rowH);
                GUI.Label(textRect, locked ? $"{label}  <size=16>· {c.Locked}</size>" : label, locked ? _rowLocked : _row);
                GUI.enabled = !locked;
                if (GUI.Button(rr, GUIContent.none, GUIStyle.none)) run.ChooseEvent(i);
                GUI.enabled = true;
                if (Pad.Active && i == ev.Cursor) InkPadUi.Frame(rr, 4f);
            }

            // 고른 줄 설명 (기물 설명 자리)
            float descTop = listTop + ev.Choices.Count * (rowH + gap) + 12f;
            var desc = new Rect(rx, Mathf.Min(descTop, H - 230f), rw, 170f);
            InkMeridianArt.PaperPanel(desc, new Color(0.98f, 0.96f, 0.9f, 0.9f));
            GUI.color = Color.white;
            string text = hover >= 0 ? Description(ev.Choices[hover]) : null;
            GUI.Label(Shrink(desc, 22f, 14f), string.IsNullOrEmpty(text) ? "<color=#8a8078>줄 위에 올리면 설명이 여기에 나와요</color>" : text, _small);
            GUI.color = old;
            return true;
        }

        static string Description(RoomRun.EventChoice c)
        {
            if (DescriptionField != null && DescriptionField.GetValue(c) is string d && !string.IsNullOrEmpty(d)) return d;
            int nl = c.Label != null ? c.Label.IndexOf('\n') : -1; // 줄바꿈 뒤 글도 설명으로
            return nl > 0 ? c.Label.Substring(nl + 1) : null;
        }

        // 고른 것: 붉은 낙관 (방금 고른 줄은 쾅 찍혀요) · 아닌 것: 빈 먹 동그라미 (방금 뺀 줄은 낙관이 먹 획에 지워져요)
        static void DrawMark(Rect r, bool picked, string label)
        {
            float age = Time.unscaledTime - _toggledAt;
            bool fresh = age < 0.6f && _toggledName != null && label.Contains(_toggledName);
            var old = GUI.color;
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.55f); // 빈 동그라미 (원상)
            if (_stroke != null) Ring(r);
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
            else if (fresh && !_toggledOn && _seal != null) // 방금 뺐어요: 낙관이 옅어지고 먹 획이 긋고 지나가요
            {
                float k = Mathf.Clamp01(age / 0.5f);
                GUI.color = new Color(1f, 1f, 1f, 1f - k);
                GUI.DrawTexture(r, _seal, ScaleMode.ScaleToFit);
                if (_stroke != null)
                {
                    GUI.color = new Color(Ink.r, Ink.g, Ink.b, 0.9f * (1f - k * k));
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(-35f, r.center);
                    float len = r.width * 1.6f * Mathf.Clamp01(age / 0.15f);
                    GUI.DrawTexture(new Rect(r.center.x - r.width * 0.8f, r.center.y - 6f, len, 12f), _stroke, ScaleMode.StretchToFill);
                    GUI.matrix = m;
                }
            }
            GUI.color = old;
        }

        static void Ring(Rect r)
        {
            const int segments = 16;
            var m = GUI.matrix;
            for (int i = 0; i < segments; i++)
            {
                GUIUtility.RotateAroundPivot(360f / segments, r.center);
                GUI.DrawTexture(new Rect(r.center.x - 6f, r.y + 1f, 12f, 4f), _stroke, ScaleMode.StretchToFill);
            }
            GUI.matrix = m;
        }

        static Rect Shrink(Rect r, float x, float y) => new Rect(r.x + x, r.y + y, r.width - x * 2f, r.height - y * 2f);

        static void Styles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, richText = true };
            _title.normal.textColor = Ink;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true, richText = true };
            _text.normal.textColor = Ink;
            _row = new GUIStyle(GUI.skin.label) { fontSize = 23, alignment = TextAnchor.MiddleLeft, wordWrap = true, richText = true };
            _row.normal.textColor = Ink;
            _rowLocked = new GUIStyle(_row);
            _rowLocked.normal.textColor = Muted;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, richText = true };
            _small.normal.textColor = Ink;
            _num = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _num.normal.textColor = Color.white;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _seal = Resources.Load<Texture2D>("TitleArt/Seal");
            _stroke = Resources.Load<Texture2D>("HudArt/HpStroke");
        }
    }
}
