using AThousandDawns.Core;
using NUnit.Framework;

namespace AThousandDawns.Tests
{
    public class RegressionBookTests
    {
        static BookNode Node(string id) => RegressionBook.Find(id);

        [Test]
        public void 주머니는_기억_30으로_피우고_4칸_확장마다_2칸()
        {
            var meta = new MetaSave { memory = 29 };
            Assert.AreEqual(0, RegressionBook.BagCapacity(meta), "처음엔 주머니 없음");
            Assert.AreEqual("기억이 모자라요", RegressionBook.Blocker(meta, Node("bag")));
            Assert.IsFalse(RegressionBook.Bloom(meta, "bag"));

            meta.memory = 30;
            Assert.IsTrue(RegressionBook.Bloom(meta, "bag"));
            Assert.AreEqual(0, meta.memory);
            Assert.AreEqual(4, RegressionBook.BagCapacity(meta));
            Assert.AreEqual("피었어요", RegressionBook.Blocker(meta, Node("bag")));

            meta.memory = 1000;
            Assert.IsTrue(RegressionBook.Bloom(meta, "bag_plus1"));
            Assert.IsTrue(RegressionBook.Bloom(meta, "bag_plus2"));
            Assert.AreEqual(8, RegressionBook.BagCapacity(meta));
        }

        [Test]
        public void 선행_봉우리가_피어야_피움()
        {
            var meta = new MetaSave { memory = 1000 };
            StringAssert.StartsWith("먼저", RegressionBook.Blocker(meta, Node("bag_plus1")));
            StringAssert.StartsWith("먼저", RegressionBook.Blocker(meta, Node("potion_kit")), "금창약 지참은 면벽수련 다음");
        }

        [Test]
        public void 콘텐츠가_없는_봉우리와_천마_조건은_잠김()
        {
            var meta = new MetaSave { memory = 1000 };
            Assert.IsNull(RegressionBook.Blocker(meta, Node("tongmaek")), "부품 통맥은 피울 수 있어요");
            Assert.IsNull(RegressionBook.Blocker(meta, Node("meditation")), "면벽수련도 피울 수 있어요");
            Assert.AreEqual("첫 천마 격파 후", RegressionBook.Blocker(meta, Node("relic1")));
            meta.bloomed.AddRange(new[] { "tongmaek", "jeomhyeol", "hanok" });
            Assert.IsNull(RegressionBook.Blocker(meta, Node("forge")), "대장간도 피울 수 있어요");
            meta.bloomed.Add("forge");
            StringAssert.StartsWith("준비 중", RegressionBook.Blocker(meta, Node("flying_dagger")), "비도는 아직 준비 중");
        }
        [Test]
        public void 금창약_지참과_객잔_진열()
        {
            var meta = new MetaSave();
            meta.bloomed.Add("meditation");
            meta.bloomed.Add("potion_kit");
            meta.bloomed.Add("inn_stock");
            Assert.AreEqual(1, RegressionBook.StartPotions(meta));
            Assert.AreEqual(1, RegressionBook.InnStockBonus(meta));
        }
    }
}
