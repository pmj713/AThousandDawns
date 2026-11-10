using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 판 결과 화면 (기획 대화 결과 화면안, Shape of Dreams 참고):
    //   위: 결말 · 제N회 회귀 · 사망 원인 ("청죽객의 죽림섬에 쓰러짐") · 플레이 시간
    //   왼쪽: 이번 생의 빌드 (무기와 융합 홈, 초식 · 절초 · 공력 · 레벨, 키워드 단계, 경맥도 축소판)
    //   오른쪽: 기록이 한 줄씩 차오르며 회귀의 기억으로 바뀜, 아래에 기억으로 바꾸지 않는 기록 (준·받은 피해 등)
    //   아래: 얻은 기억 합계, B 회귀록 · Enter 다시 회귀
    public sealed partial class PrototypeHud
    {
        const float ResultLineDelay = 0.22f; // 기록 한 줄이 차오르는 간격

        static readonly Color CellEmpty = new Color(0.16f, 0.15f, 0.17f);
        static readonly Color CellLacking = new Color(0.45f, 0.45f, 0.45f);
        static readonly Color CellCirculating = new Color(0.4f, 0.75f, 0.42f);
        static readonly Color CellDeviated = new Color(0.9f, 0.35f, 0.3f);
        static readonly Color CellPart = new Color(0.35f, 0.42f, 0.6f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.4f);

        void DrawResult()
        {
            if (run == null || !run.IsShowingResult) return;
            if (run.Book != null && run.Book.IsOpen) return; // 회귀록·면벽수련이 덮고 있어요
            EnsureStyles();
            var r = run.Result;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.86f);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;

            float cx = UiScale.Width * 0.5f;
            GUI.Label(new Rect(0, 50f, UiScale.Width, 50f), r.Victory ? "죽림을 돌파했어요" : "쓰러졌어요 · 그날 아침으로 회귀해요", _big);
            GUI.Label(new Rect(0, 104f, UiScale.Width, 30f),
                $"<color=#cfcfcf>제{r.Runs}회 회귀 · {r.Where} · 플레이 시간 {RunRecord.Clock(r.PlaySeconds)}</color>", _bossLabel);

            DrawResultBuild(new Rect(cx - 880f, 170f, 800f, 760f));
            float elapsed = Time.unscaledTime - r.ShownAt;
            DrawResultRecord(new Rect(cx + 60f, 170f, 760f, 760f), r, elapsed);
        }

        // 왼쪽: 이번 생의 빌드
        void DrawResultBuild(Rect area)
        {
            var p = player;
            if (p == null) return;
            Panel(area);
            float x = area.x + 24f, y = area.y + 18f, w = area.width - 48f;
            GUI.Label(new Rect(x, y, w, 34f), "<b>이번 생의 빌드</b>", _itemLine);
            y += 44f;

            var items = p.Items;
            string slots = "";
            if (items != null)
            {
                string Slot(int i) => items.ForgeSlot(i) is Keyword k ? Colored(k) : "<color=#808080>빈 홈</color>";
                bool strong = items.ForgeSlot(0).HasValue && items.ForgeSlot(0) == items.ForgeSlot(1);
                slots = $" · 무기 홈 {Slot(0)} {Slot(1)}{(strong ? " <color=#ffd27a>(강화판)</color>" : "")}";
            }
            Line(ref y, x, w, $"무기 <b>{(p.UsesDagger ? "비도" : "검")}</b>{slots}");
            string skill = p.UsesDagger && p.SkillName.StartsWith("회수") ? "회수" : p.SkillName;
            Line(ref y, x, w, $"초식 <b>{skill}</b> · 절초 <b>{(p.UsesDagger ? "만천화우" : "만검귀종")}</b> · 공력 <b>{p.Gongryeok}</b> · 레벨 <b>{(p.Level != null ? p.Level.Level : 1)}</b>");

            var keywords = p.Keywords;
            if (keywords != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < PlayerKeywords.KeywordCount; i++)
                {
                    int n = keywords.Count((Keyword)i);
                    if (n <= 0) continue;
                    if (sb.Length > 0) sb.Append("  ");
                    sb.Append(Colored((Keyword)i)).Append(' ').Append(n).Append("단계");
                }
                Line(ref y, x, w, sb.Length > 0 ? $"키워드 {sb}" : "키워드 <color=#808080>없음</color>");
            }

            // 경맥도 축소판 (실제 배치 그대로)
            y += 10f;
            GUI.Label(new Rect(x, y, w, 26f), "<color=#cfcfcf>경맥도</color>", _itemSmall);
            y += 30f;
            if (items == null) return;
            var board = items.Board;
            float cell = Mathf.Min(64f, (w - 8f) / Mathf.Max(1, board.Width) - 6f);
            for (int by = 0; by < board.Height; by++)
            for (int bx = 0; bx < board.Width; bx++)
            {
                var c = new Vector2Int(bx, by);
                var rect = new Rect(x + bx * (cell + 6f), y + by * (cell + 6f), cell, cell);
                var item = items.At(c);
                var part = board.PartAt(c);
                Color fill = CellEmpty;
                if (part != PartKind.None) fill = CellPart;
                else if (item != null)
                {
                    var state = PlayerItems.StateOf(item);
                    fill = state == QiState.Lacking ? CellLacking : state == QiState.Deviated ? CellDeviated : CellCirculating;
                }
                var old = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                if (board.IsDanjeon(c))
                {
                    GUI.color = Gold;
                    const float t = 3f;
                    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.x, rect.yMax - t, rect.width, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.x, rect.y, t, rect.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.xMax - t, rect.y, t, rect.height), Texture2D.whiteTexture);
                }
                GUI.color = old;
                string label = item != null ? item.Def.name : part != PartKind.None ? PlayerItems.PartLabel(part) : "";
                if (label.Length > 0) GUI.Label(rect, $"<size=11>{label}</size>", _statusLabel);
            }
            y += board.Height * (cell + 6f) + 6f;
            GUI.Label(new Rect(x, y, w, 24f), "<size=13><color=#9a9a9a>금 테두리 단전 · 초록 운기 · 회색 기 부족 · 빨강 주화입마 · 파랑 부품</color></size>", _itemSmall);
        }

        // 오른쪽: 기록 → 회귀의 기억 (한 줄씩 차오름)
        void DrawResultRecord(Rect area, RoomRun.ResultView r, float elapsed)
        {
            Panel(area);
            float x = area.x + 24f, y = area.y + 18f, w = area.width - 48f;
            GUI.Label(new Rect(x, y, w, 34f), "<b>기록 → 회귀의 기억</b>", _itemLine);
            y += 46f;

            int shown = 0;
            float memory = 0f;
            foreach (var line in r.Lines)
            {
                if (line.count <= 0f) continue;
                if (elapsed < shown * ResultLineDelay) break; // 아직 차오르지 않은 줄
                shown++;
                GUI.Label(new Rect(x, y, w * 0.5f, 30f), line.label, _label);
                GUI.Label(new Rect(x + w * 0.45f, y, w * 0.2f, 30f), $"{line.count:0}", _label);
                GUI.Label(new Rect(x + w * 0.68f, y, w * 0.32f, 30f), $"<color=#ffd27a>기억 +{line.memory:0.#}</color>", _label);
                memory += line.memory;
                y += 32f;
            }
            if (shown == 0) GUI.Label(new Rect(x, y, w, 30f), "<color=#9a9a9a>기억으로 바꿀 기록이 없어요</color>", _label);

            // 기억으로 바꾸지 않는 기록
            y = Mathf.Max(y + 14f, area.y + 380f);
            GUI.Label(new Rect(x, y, w, 26f), "<color=#cfcfcf>그 밖의 기록</color>", _itemSmall);
            y += 30f;
            if (r.Stats != null)
                foreach (var s in r.Stats)
                {
                    GUI.Label(new Rect(x, y, w * 0.5f, 28f), $"<color=#bdbdbd>{s.label}</color>", _label);
                    GUI.Label(new Rect(x + w * 0.45f, y, w * 0.3f, 28f), $"<color=#bdbdbd>{s.value}</color>", _label);
                    y += 30f;
                }

            // 합계: 다 차오른 뒤 확정 값
            bool done = elapsed >= (CountLines(r) + 0.5f) * ResultLineDelay;
            int total = done ? r.Memory : Mathf.RoundToInt(memory);
            float by = area.yMax - 120f;
            GUI.Label(new Rect(x, by, w, 40f), $"<b>회귀의 기억 +{total}</b>   <color=#cfcfcf>(가진 기억 {(done ? r.StoredMemory : r.StoredMemory - r.Memory + total)})</color>", _big);
            GUI.Label(new Rect(x, by + 56f, w, 30f), "<color=#cfcfcf>B 회귀록 · Enter · F5 다시 회귀</color>", _bossLabel);
        }

        static int CountLines(RoomRun.ResultView r)
        {
            int n = 0;
            foreach (var line in r.Lines) if (line.count > 0f) n++;
            return n;
        }

        void Line(ref float y, float x, float w, string text)
        {
            GUI.Label(new Rect(x, y, w, 30f), text, _itemLine);
            y += 34f;
        }

        static void Panel(Rect area)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.05f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
