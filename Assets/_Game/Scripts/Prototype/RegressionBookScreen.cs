using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 회귀록 화면 (시험판 IMGUI): 매화나무 가지에 봉우리가 달려 있어요.
    // 봉우리를 누르면 오른쪽에 설명·비용·조건 → "개화"를 누르면 기억을 쓰고 꽃이 피어 바로 적용 (다음 판부터).
    // 결과 화면에서 B로 열고 B·Esc로 닫아요. M으로 면벽수련 (심득을 심법 6종에 나눠 넣기, 언제든 다시 나눔).
    public sealed class RegressionBookScreen : MonoBehaviour
    {
        static readonly Color Paper = new Color(0.97f, 0.9f, 0.9f);            // 연분홍 한지
        static readonly Color Ink = new Color(0.16f, 0.1f, 0.1f);
        static readonly Color Branch = new Color(0.16f, 0.12f, 0.12f);          // 매화 가지 (열린 길): 짙은 먹
        static readonly Color BranchDim = new Color(0.55f, 0.48f, 0.48f, 0.55f); // 아직 닫힌 길: 옅은 먹
        static readonly Color Bud = new Color(0.86f, 0.3f, 0.42f);              // 피울 수 있는 꽃봉오리
        static readonly Color BudPoor = new Color(0.92f, 0.66f, 0.72f);         // 열렸지만 기억이 모자람
        static readonly Color Sepal = new Color(0.45f, 0.18f, 0.2f);            // 꽃받침
        static readonly Color Petal = new Color(0.97f, 0.58f, 0.68f);           // 핀 매화 꽃잎
        static readonly Color PetalEdge = new Color(0.9f, 0.4f, 0.52f);
        static readonly Color Glow = new Color(0.98f, 0.6f, 0.7f, 0.22f);       // 꽃 둘레 번짐
        static readonly Color BloomCore = new Color(0.98f, 0.82f, 0.4f);        // 꽃술
        static readonly Color Locked = new Color(0.48f, 0.34f, 0.32f);          // 닫힌 눈 (가지에 붙은 작은 눈)
        static readonly Color Selected = new Color(0.2f, 0.12f, 0.12f);

        // 기준 1920×1080 화면 좌표 (오른쪽 520은 설명 칸).
        // 매화나무 수형 (손그림 BookArt/Tree와 같은 1920×1080 자리): 살짝 휘며 시계 방향으로 기운 줄기에서 큰 가지가 양쪽으로 휘어 나가고,
        // 가지에서 다시 곁가지가 나요. 해금 순서대로 가지를 따라 봉우리가 달려요 (좌표는 그림 속 가지 선에 맞췄어요).
        //   위: 경맥·무기 · 왼쪽: 주머니 · 가운데 큰 가지: 면벽수련 → 심득, 거기서 위로 견문, 아래로 생존 · 아래: 유품
        // "@"로 시작하는 자리는 봉우리가 아니라 가지가 줄기에 붙는 곳이에요 (그림이 없을 때 코드로 그리는 용도).
        static readonly Dictionary<string, Vector2> Layout = new Dictionary<string, Vector2>
        {
            [RegressionBook.Root] = new Vector2(130, 1000),
            ["@relic"] = new Vector2(240, 875), ["@med"] = new Vector2(330, 680), ["@bag"] = new Vector2(390, 470),
            ["@top"] = new Vector2(510, 260), ["@tip"] = new Vector2(570, 120),
            ["tongmaek"] = new Vector2(657, 217), ["jeomhyeol"] = new Vector2(790, 238), ["hanok"] = new Vector2(930, 236),
            ["forge"] = new Vector2(1059, 196), ["flying_dagger"] = new Vector2(1193, 150),
            ["meditation"] = new Vector2(470, 650), ["insight1"] = new Vector2(630, 600), ["insight2"] = new Vector2(790, 574),
            ["insight3"] = new Vector2(948, 546), ["insight4"] = new Vector2(1112, 510), ["insight5"] = new Vector2(1259, 445),
            ["potion_kit"] = new Vector2(561, 755), ["inn_stock"] = new Vector2(694, 801),
            ["item_pack1"] = new Vector2(725, 485), ["item_pack2"] = new Vector2(822, 402),
            ["bag"] = new Vector2(302, 428), ["bag_plus1"] = new Vector2(217, 354), ["bag_plus2"] = new Vector2(168, 276),
            ["relic1"] = new Vector2(380, 880), ["relic2"] = new Vector2(530, 910), ["relic3"] = new Vector2(680, 903), ["relic4"] = new Vector2(831, 874),
        };

        // 가지마다 지나는 봉우리 (그림이 없을 때만 코드로 그려요. 줄기가 먼저, 굵게 시작해 가늘어짐)
        static readonly (string[] path, float start, float end)[] Twigs =
        {
            (new[] { RegressionBook.Root, "@relic", "@med", "@bag", "@top", "@tip" }, 28f, 8f),
            (new[] { "@top", "tongmaek", "jeomhyeol", "hanok", "forge", "flying_dagger" }, 12f, 4f),
            (new[] { "@med", "meditation", "insight1", "insight2", "insight3", "insight4", "insight5" }, 14f, 4f),
            (new[] { "insight1", "item_pack1", "item_pack2" }, 8f, 3f),
            (new[] { "meditation", "potion_kit", "inn_stock" }, 8f, 3f),
            (new[] { "@bag", "bag", "bag_plus1", "bag_plus2" }, 10f, 3f),
            (new[] { "@relic", "relic1", "relic2", "relic3", "relic4" }, 12f, 3f),
        };

        static Texture2D _circle;

        MetaSave _meta;
        bool _meditation; // 면벽수련 화면
        string _selected, _notice;
        float _noticeTimer;
        [System.NonSerialized] GUIStyle _title, _label, _small, _node, _button;

        public bool IsOpen { get; private set; }
        public MetaSave Meta => _meta;

        public void Open()
        {
            _meta = MetaSave.Load();
            _selected = null;
            _meditation = false;
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
            TickPad();
            if (!IsOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Close();
            if (keyboard.mKey.wasPressedThisFrame && MeditationOpen) _meditation = !_meditation;
        }

        // ── 게임패드: 회귀록은 십자키로 봉우리 순서대로 (→↓ 다음 · ←↑ 이전) · A 피우기 · Y 면벽수련 · B 닫기
        //    면벽수련은 위아래 심법 · 좌우 − / + · X 모두 되돌리기
        public int PadRow { get; private set; }
        public bool PadActive => IsOpen && AThousandDawns.Core.Pad.Active;
        public string SelectedNode => _selected; // 테스트용
        public string Notice => _notice;

        void TickPad()
        {
            var _ = AThousandDawns.Core.Pad.Active;
            if (AThousandDawns.Core.Pad.Back) { Close(); return; }
            if (AThousandDawns.Core.Pad.North && MeditationOpen) { _meditation = !_meditation; return; }
            if (_meditation)
            {
                PadRow = AThousandDawns.Core.Pad.Step(PadRow, Meditation.Arts.Length);
                int add = AThousandDawns.Core.Pad.Left ? -1 : AThousandDawns.Core.Pad.Right ? 1 : 0;
                if (add != 0 && Meditation.Add(_meta, (Meditation.Art)PadRow, add)) _meta.Save();
                if (AThousandDawns.Core.Pad.West) { Meditation.Reset(_meta); _meta.Save(); }
                return;
            }
            var dir = new Vector2(AThousandDawns.Core.Pad.Left ? -1 : AThousandDawns.Core.Pad.Right ? 1 : 0, AThousandDawns.Core.Pad.Up ? -1 : AThousandDawns.Core.Pad.Down ? 1 : 0);
            if (dir != Vector2.zero) MoveSelection(dir);
            if (AThousandDawns.Core.Pad.Confirm && _selected != null && !BloomSelected())
            {
                var node = RegressionBook.Find(_selected);
                _notice = node != null ? RegressionBook.Blocker(_meta, node) : null;
                _noticeTimer = 2f;
            }
        }

        // 봉우리 순서대로 (가지마다 해금 순서): 오른쪽 · 아래 다음, 왼쪽 · 위 이전. 처음엔 첫 봉우리
        // (가지가 비스듬히 뻗어 있어 방향으로 찾으면 엉뚱한 봉우리로 뛰어서, 순서로 옮겨요)
        void MoveSelection(Vector2 dir)
        {
            var nodes = RegressionBook.Nodes;
            int at = System.Array.FindIndex(nodes, n => n.Id == _selected);
            int step = dir.x > 0f || dir.y > 0f ? 1 : -1;
            at = at < 0 ? 0 : (at + step + nodes.Length) % nodes.Length;
            _selected = nodes[at].Id;
        }

        void OnGUI()
        {
            if (!IsOpen || _meta == null) return;
            UiScale.Apply();
            EnsureStyles();
            GUI.depth = -10;
            Fill(new Rect(0, 0, UiScale.Width, UiScale.Height), Paper); // 한지 (불투명: 뒤의 결과 화면이 비치지 않게)

            if (MeditationOpen && GUI.Button(new Rect(UiScale.Width - 360, 50, 300, 50), _meditation ? "회귀록 (M)" : "면벽수련 (M)", _button)) _meditation = !_meditation;
            if (_meditation)
            {
                DrawMeditation();
                return;
            }
            GUI.Label(new Rect(60, 40, 900, 50), "회귀록", _title);
            GUI.Label(new Rect(60, 100, 900, 30), $"가진 회귀의 기억 <b>{_meta.memory}</b>   ·   {_meta.runs}번 회귀했어요", _label);
            GUI.Label(new Rect(60, 132, 900, 26), "봉우리를 누르고, 개화를 눌러 피워요 · B·Esc 닫기", _small);
            DrawLegend(new Rect(60, 170, 620, 30));

            // 손그림 가지 (VARCO, Layout과 같은 1920×1080 자리에 그려 둠). 없으면 코드로 그려요
            if (Art.Tree != null) GUI.DrawTexture(new Rect(0, 0, 1920, 1080), Art.Tree);
            else DrawTwigs();

            // 뿌리 (굵은 밑동)
            var root = Layout[RegressionBook.Root];
            if (Art.Tree == null) { Circle(root + new Vector2(-14, 10), 30f, Branch); Circle(root, 28f, Branch); } // 손그림이면 밑동이 그림에 있어요
            GUI.Label(new Rect(root.x - 80, root.y + 22, 160, 26), "<b>뿌리 · 회귀</b>", _node);

            foreach (var node in RegressionBook.Nodes)
            {
                if (!Layout.TryGetValue(node.Id, out var p)) continue;
                bool bloomed = RegressionBook.Bloomed(_meta, node.Id);
                string blocker = RegressionBook.Blocker(_meta, node);
                bool openable = !bloomed && RegressionBook.Bloomed(_meta, node.Prereq) && (node.Ready || blocker == null);
                float size = bloomed ? 54f : openable ? 30f : 16f;
                if (_selected == node.Id)
                {
                    Circle(p, size + 18f, Selected);
                    Circle(p, size + 10f, Paper);
                    if (PadActive) { float s = size + 30f; AThousandDawns.Fx.InkPadUi.Frame(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), 2f); } // 게임패드로 고른 봉우리: 먹 붓 테두리
                }
                if (bloomed) Blossom(p, size);
                else if (openable) BudShape(p, size, blocker == null ? Bud : BudPoor);
                else LockedBud(p, size); // 닫힌 눈: 가지에 붙은 작은 갈색 눈
                string name = bloomed ? $"<b>{node.Name}</b>" : openable ? $"<b>{node.Name}</b> <color=#8a2a3a>{RegressionBook.Cost(node)}</color>" : $"<color=#8a8278>{node.Name}</color>";
                var labelRect = new Rect(p.x - 100, p.y + size * 0.5f + 4, 200, 26);
                if (Art.Tree != null) // 손그림 가지 위에서도 읽히게 이름 뒤에 옅은 한지
                {
                    float tw = _node.CalcSize(new GUIContent(name)).x + 10f;
                    Fill(new Rect(p.x - tw * 0.5f, labelRect.y + 1, tw, 22), new Color(Paper.r, Paper.g, Paper.b, 0.8f));
                }
                GUI.Label(labelRect, name, _node);
                float hit = Mathf.Max(size, 40f) + 12f;
                if (GUI.Button(new Rect(p.x - hit * 0.5f, p.y - hit * 0.5f, hit, hit), GUIContent.none, GUIStyle.none)) _selected = node.Id;
            }

            DrawDetail();
            if (_noticeTimer > 0f && _notice != null) GUI.Label(new Rect(0, UiScale.Height - 80, UiScale.Width, 40), _notice, _title);
            else if (PadActive) AThousandDawns.Fx.InkPadUi.Hints(UiScale.Width * 0.5f, UiScale.Height - 60f, 34f, ("A", "피우기"), ("Y", "면벽수련"), ("B", "닫기")); // 붉은 낙관 안내
        }

        void DrawDetail()
        {
            var node = _selected != null ? RegressionBook.Find(_selected) : null;
            var pack = PackItems(node);
            var panel = pack != null ? new Rect(UiScale.Width - 660, 100, 610, 690) : new Rect(UiScale.Width - 520, 180, 470, 360);
            Fill(panel, new Color(1f, 1f, 1f, 0.85f));
            if (node == null)
            {
                GUI.Label(new Rect(panel.x + 20, panel.y + 20, panel.width - 40, 60), "봉우리를 눌러 보세요.", _label);
                return;
            }
            int cost = RegressionBook.Cost(node);
            bool bloomed = RegressionBook.Bloomed(_meta, node.Id);
            string blocker = RegressionBook.Blocker(_meta, node);
            float y = panel.y + 18;
            GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 40), $"<b>{node.Name}</b>", _title);
            y += 48;
            GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 26), $"{node.Branch} 가지 · 비용 기억 {cost}", _label);
            y += 32;
            if (pack != null) // 새 기물 묶음: 풀리는 기물 6종을 미리 보여 줘요
            {
                GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 26), "새 기물 6종이 보상 · 객잔 · 이벤트에 나와요", _small);
                y += 30;
                foreach (var def in pack)
                {
                    string head = $"<b>{def.name}</b>  {PlayerKeywords.Label(def.keyword)} · {ItemDefinition.GradeLabel(def.grade)} · 기 {def.minQi}–{def.maxQi}";
                    bool icon = ItemIcons.Draw(new Rect(panel.x + 16, y + 4, 58, 58), def);
                    float left = icon ? 84 : 20;
                    GUI.Label(new Rect(panel.x + left, y, panel.width - left - 20, 66), $"{head}\n{def.effectText}", _small);
                    y += 70;
                }
                y += 4;
            }
            else
            {
                GUI.Label(new Rect(panel.x + 20, y, panel.width - 40, 90), node.Effect, _small);
                y += 96;
            }
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

        // 새 기물 묶음 봉우리면 그 묶음의 기물 (기물 에셋 순서), 아니면 null
        public static List<ItemDefinition> PackItems(BookNode node)
        {
            string[] ids = node?.Id == "item_pack1" ? ItemDatabase.Pack1 : node?.Id == "item_pack2" ? ItemDatabase.Pack2 : null;
            var db = GameConfig.Items;
            if (ids == null || db == null) return null;
            var list = new List<ItemDefinition>();
            foreach (var id in ids)
            {
                var def = db.Find(id);
                if (def != null) list.Add(def);
            }
            return list.Count > 0 ? list : null;
        }

        bool MeditationOpen => _meta != null && RegressionBook.Bloomed(_meta, "meditation");

        public bool IsMeditationShown => IsOpen && _meditation;
        public void ShowMeditation(bool on) => _meditation = on && MeditationOpen;

        // 면벽수련: 심법마다 − / + 와 단계 (5·10·20). 바꾸면 바로 저장, 다음 판부터 적용.
        void DrawMeditation()
        {
            int total = Meditation.TotalPoints(_meta), spent = Meditation.Spent(_meta);
            GUI.Label(new Rect(60, 40, 900, 50), "면벽수련", _title);
            GUI.Label(new Rect(60, 100, 1100, 30), $"심득 <b>{total - spent}</b> 남음 / {total}  ·  회귀록 면벽수련·심득 봉우리로 늘어요 · 언제든 다시 나눌 수 있어요", _label);
            if (GUI.Button(new Rect(60, 140, 200, 40), "모두 되돌리기", _button))
            {
                Meditation.Reset(_meta);
                _meta.Save();
            }
            float y = 200;
            for (int i = 0; i < Meditation.Arts.Length; i++)
            {
                var art = (Meditation.Art)i;
                var info = Meditation.Arts[i];
                int p = Meditation.Points(_meta, art);
                var row = new Rect(60, y, UiScale.Width - 120, 120);
                Fill(row, new Color(1f, 1f, 1f, 0.55f));
                if (PadActive && i == PadRow) AThousandDawns.Fx.InkPadUi.Frame(row, 4f); // 게임패드로 고른 심법: 먹 붓 테두리
                GUI.Label(new Rect(row.x + 20, row.y + 10, 260, 40), $"<b>{info.Name}</b> {info.Theme}", _title);
                GUI.Label(new Rect(row.x + 20, row.y + 60, 300, 30), $"포인트당 {info.PerPoint}", _small);
                if (GUI.Button(new Rect(row.x + 330, row.y + 30, 56, 56), "−", _button) && Meditation.Add(_meta, art, -1)) _meta.Save();
                GUI.Label(new Rect(row.x + 392, row.y + 30, 70, 56), $"<b>{p}</b>", _title);
                if (GUI.Button(new Rect(row.x + 460, row.y + 30, 56, 56), "+", _button) && Meditation.Add(_meta, art, 1)) _meta.Save();
                float mx = row.x + 560, mw = (row.xMax - mx - 20) / 3f;
                for (int s = 0; s < 3; s++)
                {
                    bool reached = Meditation.Reached(_meta, art, s);
                    bool ready = info.Ready[s];
                    var r = new Rect(mx + s * mw, row.y + 14, mw - 12, 92);
                    Fill(r, reached ? (ready ? new Color(0.96f, 0.62f, 0.7f, 0.85f) : new Color(0.75f, 0.7f, 0.7f, 0.85f)) : new Color(0.85f, 0.82f, 0.76f, 0.9f));
                    string text = $"<b>{Meditation.Steps[s]}</b>  {info.Milestones[s]}" + (ready ? "" : "\n<color=#7a6a6a>준비 중</color>");
                    GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12), text, _small);
                }
                y += 132;
            }
            if (PadActive) AThousandDawns.Fx.InkPadUi.Hints(UiScale.Width * 0.5f, UiScale.Height - 60f, 34f, ("↑↓", "고르기"), ("←→", "− +"), ("X", "되돌리기"), ("Y", "회귀록"), ("B", "닫기")); // 붉은 낙관 안내
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // 가지마다 봉우리를 잇는 부드러운 곡선 (캣멀롬). 굵기는 시작에서 끝으로 가늘어지고, 선행이 안 핀 구간은 흐리게.
        void DrawTwigs()
        {
            foreach (var (path, start, end) in Twigs)
            {
                var pts = new List<Vector2>();
                foreach (var id in path) pts.Add(Layout[id]);
                int segs = pts.Count - 1;
                for (int i = 0; i < segs; i++)
                {
                    Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(segs, i + 2)];
                    bool open = path[i].StartsWith("@") || RegressionBook.Bloomed(_meta, path[i]); // 줄기에 붙는 자리는 뿌리처럼 늘 열림
                    var color = open ? Branch : BranchDim;
                    float len = Vector2.Distance(p1, p2);
                    int steps = Mathf.Max(2, Mathf.CeilToInt(len / 2.5f));
                    for (int s = 0; s <= steps; s++)
                    {
                        float t = s / (float)steps;
                        float along = (i + t) / segs;
                        Circle(CatmullRom(p0, p1, p2, p3, t), Mathf.Lerp(start, end, along), color);
                    }
                    // 마디에서 삐죽 나온 짧은 잔가지 (그림 맛)
                    if (i % 2 == 0)
                    {
                        Vector2 mid = CatmullRom(p0, p1, p2, p3, 0.55f), dir = (p2 - p1).normalized;
                        Vector2 side = new Vector2(dir.y, -dir.x) * (i % 4 == 0 ? 1f : -1f);
                        Line(mid, mid + (dir * 0.6f + side) * 26f, color, Mathf.Max(2.5f, Mathf.Lerp(start, end, (i + 0.55f) / segs) * 0.35f));
                    }
                }
            }
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        // 핀 매화: 은은한 번짐 + 꽃잎 다섯 장 + 꽃술
        static void Blossom(Vector2 c, float size)
        {
            Circle(c, size * 2.1f, Glow);
            if (Painted(c, size * 1.25f, Art.Flower, Color.white)) return; // 손그림 매화 (없으면 아래 도형)
            float r = size * 0.3f;
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f - 90f) * Mathf.Deg2Rad;
                var petal = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Circle(petal, size * 0.56f, PetalEdge);
                Circle(petal, size * 0.48f, Petal);
            }
            Circle(c, size * 0.3f, BloomCore);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Circle(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * 0.2f, 3.5f, new Color(0.7f, 0.28f, 0.2f));
            }
        }

        // 꽃봉오리: 둥근 봉오리 + 아래 꽃받침
        static void BudShape(Vector2 c, float size, Color color)
        {
            // 손그림 봉오리: 기억이 모자라면 옅게 (없으면 아래 도형)
            if (Painted(c, size * 1.5f, Art.Bud, color == Bud ? Color.white : new Color(1f, 1f, 1f, 0.45f))) return;
            Circle(c + new Vector2(0, size * 0.28f), size * 0.7f, Sepal);
            Circle(c, size, color);
            Circle(c + new Vector2(-size * 0.18f, -size * 0.18f), size * 0.3f, new Color(1f, 1f, 1f, 0.35f)); // 윤기
        }

        // 가지: 회전 없이 점을 촘촘히 찍어요 (화면 비율 행렬과 회전이 섞이면 자리가 틀어져요)
        static void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / (width * 0.4f)));
            for (int i = 0; i <= steps; i++) Circle(Vector2.Lerp(a, b, i / (float)steps), width, c);
        }

        // 손그림 (Art/UI/Book/Resources/BookArt): 매화·봉오리·닫힌 눈. 없으면 null이라 원래 도형으로 그려요
        static class Art
        {
            static bool _loaded;
            static Texture2D _tree, _flower, _bud, _dormant;
            public static Texture2D Flower { get { Load(); return _flower; } }
            public static Texture2D Bud { get { Load(); return _bud; } }
            public static Texture2D Dormant { get { Load(); return _dormant; } }
            public static Texture2D Tree { get { Load(); return _tree; } } // 줄기·가지 한 장 (투명 바탕, 1920×1080 기준)

            static void Load()
            {
                if (_loaded) return;
                _loaded = true;
                _flower = Resources.Load<Texture2D>("BookArt/Flower");
                _bud = Resources.Load<Texture2D>("BookArt/Bud");
                _dormant = Resources.Load<Texture2D>("BookArt/DormantBud");
                _tree = Resources.Load<Texture2D>("BookArt/Tree");
            }
        }

        // 손그림 한 장을 가운데 맞춰 그려요. 그림이 없으면 false
        static bool Painted(Vector2 center, float size, Texture2D tex, Color tint)
        {
            if (tex == null) return false;
            float w = size * tex.width / Mathf.Max(tex.width, tex.height), h = size * tex.height / Mathf.Max(tex.width, tex.height);
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h), tex);
            GUI.color = old;
            return true;
        }

        // 닫힌 눈: 손그림 겨울눈 (없으면 갈색 점)
        static void LockedBud(Vector2 c, float size)
        {
            if (!Painted(c, size * 1.6f, Art.Dormant, Color.white)) Circle(c, size, Locked);
        }

        static void Circle(Vector2 center, float size, Color c)
        {
            if (_circle == null) _circle = MakeCircle(64);
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), _circle);
            GUI.color = old;
        }

        static Texture2D MakeCircle(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
            }
            tex.Apply();
            return tex;
        }

        // 범례: 닫힘 · 피울 수 있음 · 기억 모자람 · 핌
        void DrawLegend(Rect area)
        {
            float x = area.x;
            var p0 = new Vector2(x + 12, area.y + 14);
            LockedBud(p0, 12f);
            GUI.Label(new Rect(x + 28, area.y, 140, 28), "닫힘 · 준비 중", _small);
            x += 150;
            BudShape(new Vector2(x + 12, area.y + 12), 18f, Bud);
            GUI.Label(new Rect(x + 28, area.y, 140, 28), "피울 수 있음", _small);
            x += 150;
            BudShape(new Vector2(x + 12, area.y + 12), 18f, BudPoor);
            GUI.Label(new Rect(x + 28, area.y, 140, 28), "기억 모자람", _small);
            x += 150;
            Blossom(new Vector2(x + 12, area.y + 14), 24f);
            GUI.Label(new Rect(x + 32, area.y, 140, 28), "핌", _small);
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
            _node = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.UpperCenter, richText = true };
            _node.normal.textColor = Ink;
            _button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        }
    }
}
