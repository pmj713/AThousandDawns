using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Items;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 유품록 (마을 사당, 첫 천마 격파 후): 천마를 격파한 판에 지녔던 기물이 유품으로 남아요.
    // 사당에서 기억 용량만큼 골라 두면 다음 판을 그 기물을 지니고 시작해요 (전설은 기억 2).
    // 기억 용량 = 기본 1 + 회귀록 유품 가지 봉우리 (최대 5).
    public sealed partial class RoomRun
    {
        public static event System.Action<string, bool> RelicToggled; // 유품을 고르거나 뺄 때 (기물 id, 골랐는지)
        const int RelicsPerPage = 7;
        int _relicPage;

        // 천마 격파: 지닌 기물을 유품록에 남겨요 (ShowResult에서)
        void RecordRelics(MetaSave meta)
        {
            if (Player.Items == null) return;
            foreach (var item in Player.Items.Items)
                if (item.Def != null && !meta.relics.Contains(item.Def.id)) meta.relics.Add(item.Def.id);
        }

        // 판 시작: 고른 유품을 지니고 시작해요
        void GrantRelics()
        {
            var meta = MetaSave.Load();
            int cap = RegressionBook.RelicCapacity(meta), used = 0, given = 0;
            if (cap <= 0 || Player.Items == null || GameConfig.Items == null) return;
            foreach (var id in meta.relicPicks)
            {
                var def = GameConfig.Items.Find(id);
                if (def == null || !meta.relics.Contains(id)) continue;
                int cost = RegressionBook.RelicCost(def.grade);
                if (used + cost > cap) continue;
                if (Player.Items.Add(def) == null) continue;
                used += cost;
                given++;
            }
            if (given > 0) ShowBanner($"전생의 유품 {given}개를 지니고 떠나요");
        }

        static int RelicsUsed(MetaSave meta)
        {
            int used = 0;
            foreach (var id in meta.relicPicks)
            {
                var def = GameConfig.Items != null ? GameConfig.Items.Find(id) : null;
                if (def != null) used += RegressionBook.RelicCost(def.grade);
            }
            return used;
        }

        // ── 사당: 유품록 (이벤트 창으로 골라요. 고를 때마다 다시 열려요)
        public void OpenRelicBook()
        {
            var meta = MetaSave.Load();
            int cap = RegressionBook.RelicCapacity(meta), used = RelicsUsed(meta);
            var v = new EventView { Id = "relic_book", Title = "유품록" };
            v.Text = meta.relics.Count == 0
                ? "아직 남은 유품이 없어요. 천마를 쓰러뜨린 판에 지녔던 기물이 여기에 남아요."
                : $"전생의 유품을 골라 두면 다음 판을 지니고 시작해요. 기억 용량 {used}/{cap} (전설은 기억 {RegressionBook.RelicCost(ItemGrade.Legendary)})";

            var defs = new List<ItemDefinition>();
            foreach (var id in meta.relics)
            {
                var def = GameConfig.Items != null ? GameConfig.Items.Find(id) : null;
                if (def != null) defs.Add(def);
            }
            int pages = Mathf.Max(1, (defs.Count + RelicsPerPage - 1) / RelicsPerPage);
            _relicPage = Mathf.Clamp(_relicPage, 0, pages - 1);
            for (int i = _relicPage * RelicsPerPage; i < defs.Count && i < (_relicPage + 1) * RelicsPerPage; i++)
            {
                var def = defs[i];
                bool picked = meta.relicPicks.Contains(def.id);
                int cost = RegressionBook.RelicCost(def.grade);
                v.Choices.Add(new EventChoice
                {
                    Label = $"{(picked ? "■" : "□")} <b>{def.name}</b> · {AThousandDawns.Player.PlayerKeywords.Label(def.keyword)} · {ItemDefinition.GradeLabel(def.grade)} · 기 {def.minQi}–{def.maxQi} · 기억 {cost}\n<size=14>{def.effectText}</size>", // 무슨 기물인지 효과까지 (1레벨 기준)
                    Locked = !picked && used + cost > cap ? "기억 용량이 모자라요" : null,
                    Act = () => { ToggleRelic(def.id); OpenRelicBook(); return null; },
                });
            }
            if (pages > 1)
                v.Choices.Add(new EventChoice { Label = $"다음 쪽 ({_relicPage + 1}/{pages})", Act = () => { _relicPage = (_relicPage + 1) % pages; OpenRelicBook(); return null; } });
            v.Choices.Add(new EventChoice { Label = "닫는다", Act = () => null });
            OpenEventView(v, null);
        }

        static void ToggleRelic(string id)
        {
            var meta = MetaSave.Load();
            if (!meta.relicPicks.Remove(id)) meta.relicPicks.Add(id);
            meta.Save();
            RelicToggled?.Invoke(id, meta.relicPicks.Contains(id));
        }
    }
}
