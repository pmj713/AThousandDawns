using System.Collections.Generic;
using System.Linq;
using AThousandDawns.Enemies;
using AThousandDawns.Prototype;
using NUnit.Framework;

namespace AThousandDawns.Tests
{
    public class RoomBudgetTests
    {
        static int Cost(EnemyKind kind) =>
            kind == EnemyKind.Grunt || kind == EnemyKind.Bomber || kind == EnemyKind.Snake ? 1 : 2;

        static readonly RoomBudget.Limits Limits = new RoomBudget.Limits { Archers = 2, Horns = 1, Snakes = 2 };

        static int Points(List<List<EnemyKind>> waves)
        {
            // 들개는 둘이 한 무리 (점수도 무리 단위)
            int total = 0;
            foreach (var wave in waves)
                foreach (var kind in wave)
                    total += kind == EnemyKind.Hound ? 1 : Cost(kind);
            return total;
        }

        [Test]
        public void 예산을_꽉_채우고_제한을_지킴()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                int budget = 6 + seed % 10;
                var waves = RoomBudget.Plan(budget, 3, RoomBudget.Bamboo, Cost, Limits, new System.Random(seed));
                var all = waves.SelectMany(w => w).ToList();
                Assert.AreEqual(budget, Points(waves), $"시드 {seed}: 졸개(1점)가 늘 남으니 예산을 다 써요");
                Assert.LessOrEqual(all.Count(k => k == EnemyKind.Archer), 2);
                Assert.LessOrEqual(all.Count(k => k == EnemyKind.Horn), 1);
                Assert.LessOrEqual(all.Count(k => k == EnemyKind.Snake), 2);
                foreach (var wave in waves)
                {
                    Assert.Greater(wave.Count, 0, "빈 웨이브 없음");
                    Assert.AreEqual(0, wave.Count(k => k == EnemyKind.Hound) % 2, "들개는 한 웨이브 안에 둘씩");
                }
            }
        }

        [Test]
        public void 같은_시드면_같은_방()
        {
            var a = RoomBudget.Plan(8, 3, RoomBudget.Bamboo, Cost, Limits, new System.Random(42));
            var b = RoomBudget.Plan(8, 3, RoomBudget.Bamboo, Cost, Limits, new System.Random(42));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) CollectionAssert.AreEqual(a[i], b[i]);
        }

        [Test]
        public void 웨이브마다_점수가_고르게()
        {
            var waves = RoomBudget.Plan(12, 3, RoomBudget.Bamboo, Cost, Limits, new System.Random(7));
            Assert.AreEqual(3, waves.Count);
            var points = waves.Select(w => Points(new List<List<EnemyKind>> { w })).ToList();
            Assert.LessOrEqual(points.Max() - points.Min(), 2, "가장 무거운 웨이브와 가벼운 웨이브 차이는 무리 하나 정도");
        }
    }
}
