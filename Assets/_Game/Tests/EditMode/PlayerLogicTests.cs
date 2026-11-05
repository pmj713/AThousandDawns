using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Player;
using NUnit.Framework;
using UnityEngine;

namespace AThousandDawns.Tests
{
    public class PlayerLogicTests
    {
        // 조작 시트의 평타 타이밍: 1·2타 0.08/0.06/0.18, 3타째 0.14/0.08/0.28 → 한 콤보 1.14초
        static ComboTimeline NewCombo() => new ComboTimeline(new[]
        {
            new ComboTimeline.HitTiming(0.08f, 0.06f, 0.18f),
            new ComboTimeline.HitTiming(0.08f, 0.06f, 0.18f),
            new ComboTimeline.HitTiming(0.14f, 0.08f, 0.28f),
        }, 0.25f, 0.12f);

        static void Run(ComboTimeline combo, float seconds, bool held, float dt = 0.01f)
        {
            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++) combo.Tick(dt, held);
        }

        [Test]
        public void 누르고_있으면_한_콤보는_1_14초이고_다시_1타로_이어짐()
        {
            var combo = NewCombo();
            var swings = new List<int>();
            var hits = new List<int>();
            combo.SwingStarted += swings.Add;
            combo.HitFrame += hits.Add;

            Run(combo, 1.13f, true);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, swings);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, hits);

            Run(combo, 0.02f, true);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 0 }, swings);
        }

        [Test]
        public void 첫_판정은_선딜_0_08초_뒤()
        {
            var combo = NewCombo();
            int hitCount = 0;
            combo.HitFrame += _ => hitCount++;
            combo.Press();
            Run(combo, 0.07f, false);
            Assert.AreEqual(0, hitCount);
            Run(combo, 0.02f, false);
            Assert.AreEqual(1, hitCount);
        }

        [Test]
        public void 연결_시간을_놓치면_1타부터()
        {
            var combo = NewCombo();
            combo.Press();
            Run(combo, 0.32f + 0.3f, false); // 1타 끝 + 연결 시간(0.25초) 넘김
            Assert.AreEqual(ComboTimeline.Phase.Idle, combo.CurrentPhase);
            Assert.AreEqual(0, combo.HitIndex);
        }

        [Test]
        public void 연결_시간_안에_누르면_2타()
        {
            var combo = NewCombo();
            int lastSwing = -1;
            combo.SwingStarted += i => lastSwing = i;
            combo.Press();
            Run(combo, 0.4f, false); // 1타 끝, 연결 시간 중
            Assert.AreEqual(ComboTimeline.Phase.ChainWindow, combo.CurrentPhase);
            combo.Press();
            combo.Tick(0.01f, false);
            Assert.AreEqual(1, lastSwing);
        }

        [Test]
        public void 대시로_끊기는_후딜_중에만()
        {
            var combo = NewCombo();
            combo.Press();
            combo.Tick(0.05f, false);
            Assert.AreEqual(ComboTimeline.Phase.Windup, combo.CurrentPhase);
            Assert.IsFalse(combo.CanDashCancel);
            combo.Tick(0.1f, false);
            Assert.AreEqual(ComboTimeline.Phase.Recovery, combo.CurrentPhase);
            Assert.IsTrue(combo.CanDashCancel);
        }

        [Test]
        public void 대시는_하나씩_재충전()
        {
            var dash = new DashCharges(2, 1f);
            Assert.IsTrue(dash.TryConsume());
            Assert.IsTrue(dash.TryConsume());
            Assert.IsFalse(dash.TryConsume());
            dash.Tick(0.99f);
            Assert.AreEqual(0, dash.Charges);
            dash.Tick(0.02f);
            Assert.AreEqual(1, dash.Charges);
            dash.Tick(1f);
            Assert.AreEqual(2, dash.Charges);
        }

        [Test]
        public void 부채꼴_판정()
        {
            Vector3 origin = Vector3.zero;
            Vector3 forward = Vector3.forward;
            Assert.IsTrue(HitShapes.InSector(origin, forward, 1.6f, 100f, new Vector3(0f, 0f, 1.5f)));
            Assert.IsTrue(HitShapes.InSector(origin, forward, 1.6f, 100f, new Vector3(1f, 0f, 1f)));   // 45°
            Assert.IsFalse(HitShapes.InSector(origin, forward, 1.6f, 100f, new Vector3(1.2f, 0f, 0.2f))); // 약 80°
            Assert.IsFalse(HitShapes.InSector(origin, forward, 1.6f, 100f, new Vector3(0f, 0f, -1f)));  // 뒤
            Assert.IsFalse(HitShapes.InSector(origin, forward, 1.6f, 100f, new Vector3(0f, 0f, 2.5f))); // 너무 멂
            Assert.IsTrue(HitShapes.InSector(origin, forward, 1.6f, 100f, new Vector3(0f, 0f, 1.9f), 0.4f)); // 몸이 걸침
        }
    }
}
