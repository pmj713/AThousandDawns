using System.Linq;
using AThousandDawns.Enemies;
using NUnit.Framework;
using UnityEngine;
using Phase = AThousandDawns.Enemies.EnemyAttackTimeline.Phase;

namespace AThousandDawns.Tests
{
    public class EnemyLogicTests
    {
        static void Run(EnemyAttackTimeline t, float seconds, float step = 0.01f)
        {
            for (float s = 0f; s < seconds - 1e-5f; s += step) t.Tick(step);
        }

        [Test]
        public void 졸개_공격은_예고_판정_빈틈_순서로_1_35초()
        {
            var t = new EnemyAttackTimeline();
            int active = 0, finished = 0;
            t.ActiveStarted += () => active++;
            t.Finished += () => finished++;

            t.Start(0.6f, 0.15f, 0.6f);
            Run(t, 0.59f);
            Assert.AreEqual(Phase.Windup, t.Current);
            Run(t, 0.02f);
            Assert.AreEqual(Phase.Active, t.Current);
            Assert.AreEqual(1, active);
            Run(t, 0.15f);
            Assert.AreEqual(Phase.Recovery, t.Current);
            Run(t, 0.6f);
            Assert.AreEqual(Phase.Ready, t.Current);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void 예고_앞_절반만_따라옴()
        {
            var t = new EnemyAttackTimeline();
            t.Start(0.6f, 0.15f, 0.6f);
            Run(t, 0.29f);
            Assert.IsTrue(t.IsTracking);
            Run(t, 0.02f);
            Assert.IsFalse(t.IsTracking);
            Assert.AreEqual(Phase.Windup, t.Current);
        }

        [Test]
        public void 예고_중에_맞으면_끊기지_않고_0_2초_늦춰짐()
        {
            var t = new EnemyAttackTimeline();
            t.Start(0.6f, 0.15f, 0.6f);
            Run(t, 0.3f);
            t.Delay(0.2f);
            Run(t, 0.2f);
            Assert.AreEqual(0.5f, t.WindupProgress, 0.02f, "늦춰지는 동안 예고가 차오르지 않아요");
            Run(t, 0.29f);
            Assert.AreEqual(Phase.Windup, t.Current);
            Run(t, 0.02f);
            Assert.AreEqual(Phase.Active, t.Current);
        }

        [Test]
        public void 멧돼지_돌진은_EndActive까지_이어지고_벽에_부딪히면_기절_시간이_빈틈()
        {
            var t = new EnemyAttackTimeline();
            t.Start(0.8f, float.PositiveInfinity, 1f);
            Run(t, 5f);
            Assert.AreEqual(Phase.Active, t.Current);
            t.EndActive(2f);
            Run(t, 1.99f);
            Assert.AreEqual(Phase.Recovery, t.Current);
            Run(t, 0.02f);
            Assert.AreEqual(Phase.Ready, t.Current);
        }

        [Test]
        public void 빈틈_중에_맞으면_빈틈이_길어짐()
        {
            var t = new EnemyAttackTimeline();
            t.Start(0.1f, 0.1f, 0.5f);
            Run(t, 0.3f); // 빈틈 0.1초 지남
            Assert.AreEqual(Phase.Recovery, t.Current);
            t.ExtendRecovery(0.2f);
            Run(t, 0.59f); // 원래라면 0.4초 뒤 끝, 늘어나서 0.6초 뒤 끝
            Assert.AreEqual(Phase.Recovery, t.Current);
            Run(t, 0.02f);
            Assert.AreEqual(Phase.Ready, t.Current);
        }

        [Test]
        public void 경직_뒤_0_4초_동안은_다시_경직되지_않음()
        {
            var r = new HitReaction(0.2f, 0.4f, 0.2f);
            Assert.AreEqual(HitResponse.Stun, r.OnHit(Phase.Ready, false));
            r.Tick(0.59f);
            Assert.AreEqual(HitResponse.None, r.OnHit(Phase.Ready, false), "경직 0.2초 + 면역 0.4초 안");
            r.Tick(0.02f);
            Assert.AreEqual(HitResponse.Stun, r.OnHit(Phase.Ready, false));
        }

        [Test]
        public void 단계별_맞았을_때_반응()
        {
            Assert.AreEqual(HitResponse.DelayWindup, new HitReaction(0.2f, 0.4f, 0.2f).OnHit(Phase.Windup, false));
            Assert.AreEqual(HitResponse.None, new HitReaction(0.2f, 0.4f, 0.2f).OnHit(Phase.Active, false));
            Assert.AreEqual(HitResponse.ExtendRecovery, new HitReaction(0.2f, 0.4f, 0.2f).OnHit(Phase.Recovery, false));
            Assert.AreEqual(HitResponse.None, new HitReaction(0.2f, 0.4f, 0.2f).OnHit(Phase.Ready, true), "돌진 중인 멧돼지");
        }

        [Test]
        public void 공격_토큰은_정원까지만()
        {
            var pool = new AttackTokenPool(2);
            object a = new object(), b = new object(), c = new object();
            Assert.IsTrue(pool.TryAcquire(a));
            Assert.IsTrue(pool.TryAcquire(b));
            Assert.IsFalse(pool.TryAcquire(c));
            Assert.IsTrue(pool.TryAcquire(a), "이미 가진 적은 그대로");
            Assert.AreEqual(2, pool.InUse);
            pool.Release(a);
            Assert.IsTrue(pool.TryAcquire(c));
        }

        [Test]
        public void 전진_베기_예고는_사거리_2_7칸_폭_1_2칸()
        {
            var shape = TelegraphShapes.Lunge(1.2f, 60f, 1.5f);
            Assert.AreEqual(2.7f, shape.Max(p => p.y), 1e-3f);
            Assert.AreEqual(0f, shape.Min(p => p.y), 1e-3f);
            Assert.AreEqual(0.6f, shape.Max(p => Mathf.Abs(p.x)), 1e-3f);
        }

        [Test]
        public void 예고_다각형은_위에서_봤을_때_시계방향()
        {
            foreach (var shape in new[] { TelegraphShapes.Lunge(1.2f, 60f, 1.5f), TelegraphShapes.Lunge(1.6f, 100f, 0f), TelegraphShapes.Rect(1.2f, 8f) })
            {
                float area2 = 0f;
                for (int i = 0; i < shape.Count; i++)
                {
                    Vector2 a = shape[i], b = shape[(i + 1) % shape.Count];
                    area2 += a.x * b.y - b.x * a.y;
                }
                Assert.Less(area2, 0f, "x 오른쪽·y 앞 평면에서 시계방향이면 넓이 부호가 음수 (유니티 앞면 = 시계방향)");
            }
        }
    }
}
