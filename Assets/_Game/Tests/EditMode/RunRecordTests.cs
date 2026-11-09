using AThousandDawns.Enemies;
using AThousandDawns.Prototype;
using NUnit.Framework;

namespace AThousandDawns.Tests
{
    public class RunRecordTests
    {
        // 기억 시트 환산 규칙 그대로
        static readonly RunRecord.Weights Sheet = new RunRecord.Weights
        {
            Floor = 3f, Normal = 0.1f, Elite = 1f, Assassin = 3f, Master = 5f, Boss = 10f, HiddenRoom = 5f, FinalBoss = 30f,
        };

        [Test]
        public void 층과_처치를_기억으로_바꿈()
        {
            var r = new RunRecord { FloorsCleared = 3 };
            for (int i = 0; i < 60; i++) r.AddKill(EnemyRank.Normal);
            for (int i = 0; i < 4; i++) r.AddKill(EnemyRank.Elite);
            r.AddKill(EnemyRank.Assassin);
            r.AddKill(EnemyRank.Master);
            Assert.AreEqual(9 + 6 + 4 + 3 + 5, r.Memory(Sheet), "죽림에서 사망 정도 (시트 추정 20~30)");
        }

        [Test]
        public void 반올림은_0점5에서_올림()
        {
            var r = new RunRecord();
            for (int i = 0; i < 5; i++) r.AddKill(EnemyRank.Normal); // 0.5
            Assert.AreEqual(1, r.Memory(Sheet));
            var none = new RunRecord();
            none.AddKill(EnemyRank.Normal); // 0.1
            Assert.AreEqual(0, none.Memory(Sheet));
        }

        [Test]
        public void 녹림왕을_잡으면_보스_층까지_돌파()
        {
            var r = new RunRecord { FloorsCleared = 4 };
            r.AddKill(EnemyRank.Boss);
            Assert.AreEqual(12 + 10, r.Memory(Sheet));
        }
    }
}
