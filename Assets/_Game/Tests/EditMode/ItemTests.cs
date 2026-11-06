using System.Collections.Generic;
using System.IO;
using AThousandDawns.EditorTools;
using AThousandDawns.Items;
using AThousandDawns.Player;
using NUnit.Framework;

namespace AThousandDawns.Tests
{
    public class ItemTests
    {
        static OwnedItem Owned(int minQi, int maxQi, int qi) =>
            new OwnedItem { Def = new ItemDefinition { id = "x", minQi = minQi, maxQi = maxQi }, Qi = qi };

        // 기물 탭 공통 규칙: 기 < 최소 → 기 부족 · 최소~최대 → 운기 · 최대 초과 → 주화입마. 레벨 = min(기, 최대) − 최소 + 1
        [TestCase(2, 3, 1, QiState.Lacking, 0)]
        [TestCase(2, 3, 2, QiState.Circulating, 1)]
        [TestCase(2, 3, 3, QiState.Circulating, 2)]
        [TestCase(2, 3, 4, QiState.Deviated, 2)]
        [TestCase(3, 6, 6, QiState.Circulating, 4)]
        public void 기에_따라_상태와_레벨이_정해짐(int minQi, int maxQi, int qi, QiState state, int level)
        {
            var item = Owned(minQi, maxQi, qi);
            Assert.AreEqual(state, PlayerItems.StateOf(item));
            Assert.AreEqual(level, PlayerItems.LevelOf(item));
        }

        [Test]
        public void 프로젝트의_data_items_CSV에_오류가_없고_30종이_있음()
        {
            var errors = new List<string>();
            var items = ItemTable.Read(File.ReadAllText(SheetImporter.ItemsCsvPath), errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(30, items.Count);

            var ring = items.Find(i => i.id == "ember_ring");
            Assert.AreEqual(Keyword.Yeolyang, ring.keyword);
            Assert.AreEqual(ItemGrade.Common, ring.grade);
            Assert.IsTrue(ring.v1Scales);
            Assert.AreEqual("move_speed", ring.side);

            var bell = items.Find(i => i.id == "thunder_bell");
            Assert.IsTrue(bell.IsMado);
            Assert.IsFalse(bell.v1Scales, "횟수는 레벨·주화입마 배율을 받지 않음");
        }

        [Test]
        public void 후보는_가진_기물을_빼고_서로_다르게_뽑음()
        {
            var pool = new List<ItemDefinition>();
            for (int i = 0; i < 6; i++) pool.Add(new ItemDefinition { id = "c" + i, grade = ItemGrade.Common });
            pool.Add(new ItemDefinition { id = "legend", grade = ItemGrade.Legendary });

            var rng = new System.Random(7);
            for (int round = 0; round < 50; round++)
            {
                var offer = ItemOffer.Roll(pool, id => id == "c0", 3, new[] { 0.58f, 0.3f, 0.1f, 0.02f }, () => (float)rng.NextDouble());
                Assert.AreEqual(3, offer.Count);
                var ids = new HashSet<string>();
                foreach (var def in offer)
                {
                    Assert.AreNotEqual("c0", def.id);
                    Assert.IsTrue(ids.Add(def.id), "같은 기물이 두 장");
                }
            }
        }

        [Test]
        public void 남은_기물이_적으면_그만큼만_뽑음()
        {
            var pool = new List<ItemDefinition>
            {
                new ItemDefinition { id = "a", grade = ItemGrade.Rare },
                new ItemDefinition { id = "b", grade = ItemGrade.Common },
            };
            var offer = ItemOffer.Roll(pool, id => id == "a", 3, new[] { 0.58f, 0.3f, 0.1f, 0.02f }, () => 0.99f);
            Assert.AreEqual(1, offer.Count);
            Assert.AreEqual("b", offer[0].id);
        }
    }
}
