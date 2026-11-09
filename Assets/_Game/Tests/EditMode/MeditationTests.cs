using AThousandDawns.Core;
using AThousandDawns.Prototype;
using NUnit.Framework;
using MArt = AThousandDawns.Core.Meditation.Art;

namespace AThousandDawns.Tests
{
    public class MeditationTests
    {
        static MetaSave WithBuds(params string[] ids)
        {
            var meta = new MetaSave();
            meta.bloomed.AddRange(ids);
            return meta;
        }

        [Test]
        public void 심득은_면벽수련_5에_심득_봉우리마다_5()
        {
            Assert.AreEqual(0, Meditation.TotalPoints(new MetaSave()));
            Assert.AreEqual(5, Meditation.TotalPoints(WithBuds("meditation")));
            Assert.AreEqual(30, Meditation.TotalPoints(WithBuds("meditation", "insight1", "insight2", "insight3", "insight4", "insight5")));
        }

        [Test]
        public void 남은_심득_안에서만_넣고_언제든_되돌림()
        {
            var meta = WithBuds("meditation");
            for (int i = 0; i < 5; i++) Assert.IsTrue(Meditation.Add(meta, MArt.Iron, 1));
            Assert.IsFalse(Meditation.Add(meta, MArt.Qi, 1), "심득 5를 다 썼어요");
            Assert.IsTrue(Meditation.Reached(meta, MArt.Iron, 0), "강 5 단계");
            Assert.IsTrue(Meditation.Active(meta, MArt.Iron, 0));
            Assert.IsTrue(Meditation.Add(meta, MArt.Iron, -1));
            Assert.IsTrue(Meditation.Add(meta, MArt.Qi, 1), "되돌린 만큼 다른 데 넣어요");
            Meditation.Reset(meta);
            Assert.AreEqual(0, Meditation.Spent(meta));
            Assert.IsFalse(Meditation.Add(meta, MArt.Qi, -1), "0 아래로는 안 내려가요");
        }

        [Test]
        public void 준비_중인_단계는_도달해도_켜지지_않음()
        {
            var meta = WithBuds("meditation", "insight1", "insight2", "insight3");
            for (int i = 0; i < 20; i++) Meditation.Add(meta, MArt.Swift, 1);
            Assert.IsTrue(Meditation.Reached(meta, MArt.Swift, 2));
            Assert.IsFalse(Meditation.Active(meta, MArt.Swift, 2), "쾌 20 (대시 중 초식)은 준비 중");
            Assert.IsTrue(Meditation.Active(meta, MArt.Swift, 1));
        }

        [Test]
        public void 심득이_줄면_뒤에서부터_덜어냄()
        {
            var meta = WithBuds("meditation", "insight1");
            for (int i = 0; i < 5; i++) Meditation.Add(meta, MArt.Swift, 1);
            for (int i = 0; i < 5; i++) Meditation.Add(meta, MArt.Qi, 1);
            meta.bloomed.Remove("insight1");
            Meditation.Clamp(meta);
            Assert.AreEqual(5, Meditation.Points(meta, MArt.Swift));
            Assert.AreEqual(0, Meditation.Points(meta, MArt.Qi));
        }

        [Test]
        public void 행운은_일반에서_덜어_고급_희귀_전설로()
        {
            var w = RoomRun.ApplyLuck(new[] { 0.58f, 0.3f, 0.1f, 0.02f }, 10);
            Assert.AreEqual(0.48f, w[0], 1e-4f);
            Assert.AreEqual(0.36f, w[1], 1e-4f);
            Assert.AreEqual(0.13f, w[2], 1e-4f);
            Assert.AreEqual(0.03f, w[3], 1e-4f);
        }
    }
}
