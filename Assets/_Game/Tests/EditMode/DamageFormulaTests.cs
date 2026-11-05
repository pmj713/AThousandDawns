using AThousandDawns.Combat;
using NUnit.Framework;

namespace AThousandDawns.Tests
{
    // 피해 계산기 시트의 "공식 확인용 예시"와 같은 값이 나와야 해요.
    public class DamageFormulaTests
    {
        static readonly DamageRules Rules = DamageRules.Default;

        [Test]
        public void 평타3타째_공격증가60퍼센트_멧돼지는_21()
        {
            var hit = HitDamageInput.Of(15f);
            hit.AttackIncrease = 0.6f;
            hit.Toughness = 3f;
            Assert.AreEqual(21, DamageFormula.ToEnemy(hit, Rules));
        }

        [Test]
        public void 회선참_공력4_녹림왕은_46()
        {
            var hit = HitDamageInput.Of(40f);
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(4, Rules);
            hit.Toughness = 5f;
            hit.Defense = 0.1f;
            Assert.AreEqual(46, DamageFormula.ToEnemy(hit, Rules));
        }

        [Test]
        public void 녹림왕_대도가_플레이어에게_17()
        {
            var p = new PlayerHitInput { EnemyAttack = 22f, TakenIncrease = 0.1f, DamageReduction = 0.15f, Toughness = 2f, Defense = 6f };
            Assert.AreEqual(17, DamageFormula.ToPlayer(p, Rules));
        }

        [Test]
        public void 강인함은_적용전_피해의_20퍼센트_아래로_내리지_않음()
        {
            var hit = HitDamageInput.Of(5f);
            hit.Toughness = 5f;
            Assert.AreEqual(1, DamageFormula.ToEnemy(hit, Rules));

            var big = HitDamageInput.Of(10f);
            big.Toughness = 9f;
            Assert.AreEqual(2, DamageFormula.ToEnemy(big, Rules));
        }

        [Test]
        public void 절초는_강인함을_무시함()
        {
            var hit = HitDamageInput.Of(144f);
            hit.GongryeokMultiplier = DamageFormula.GongryeokMultiplier(3, Rules);
            hit.Toughness = 5f;
            hit.Defense = 0.1f;
            hit.IgnoreToughness = true;
            Assert.AreEqual(168, DamageFormula.ToEnemy(hit, Rules));
        }

        [Test]
        public void 낙뢰는_발동공격의_150퍼센트_강인함무시()
        {
            var trigger = HitDamageInput.Of(15f);
            trigger.AttackIncrease = 0.6f;
            float before = DamageFormula.BeforeToughness(trigger);
            Assert.AreEqual(36, DamageFormula.Proc(before, 1.5f, 0f));
        }

        [Test]
        public void 받는피해_배율은_40퍼센트_아래로_내려가지_않음()
        {
            var p = new PlayerHitInput { EnemyAttack = 20f, DamageReduction = 0.9f };
            Assert.AreEqual(8, DamageFormula.ToPlayer(p, Rules));
        }
    }
}
