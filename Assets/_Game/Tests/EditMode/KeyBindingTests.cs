using System.Linq;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace AThousandDawns.Tests
{
    // 설정 → 조작 키 바꾸기 (겹치면 맞바꿈, 기본값으로) · 피해 숫자 합산 표시
    public class KeyBindingTests
    {
        [SetUp]
        public void Fresh()
        {
            new GameSettings().Save();
            GameSettings.Reload();
        }

        [TearDown]
        public void Restore()
        {
            new GameSettings().Save();
            GameSettings.Reload();
        }

        [Test]
        public void 기본_키와_이름()
        {
            Assert.AreEqual("<Keyboard>/f", KeyBindings.Path(Bind.Interact));
            Assert.AreEqual("좌클릭", KeyBindings.Label(Bind.Attack));
            Assert.AreEqual("Space", KeyBindings.Label(Bind.Dash));
            Assert.AreEqual(KeyBindings.Count, KeyBindings.Names.Length);
        }

        [Test]
        public void 안내_글의_키_표식은_지금_키_이름으로()
        {
            string text = $"{KeyHint.Key(Bind.Meridian)} 경맥도 · {KeyHint.Key(Bind.Attack)} 공격";
            StringAssert.DoesNotContain("E 경맥도", text, "표식 그대로는 글자가 아니에요");
            Assert.AreEqual("E 경맥도 · 좌클릭 공격", KeyHint.Plain(text));
            KeyBindings.Set(Bind.Meridian, "<Keyboard>/m");
            Assert.AreEqual("M 경맥도 · 좌클릭 공격", KeyHint.Plain(text), "키를 바꾸면 따라 바뀌어요");
            Assert.AreEqual("표식 없음", KeyHint.Plain("표식 없음"));
        }

        [Test]
        public void 바꾸면_저장되고_겹치면_맞바꿈()
        {
            KeyBindings.Set(Bind.Interact, "<Keyboard>/g");
            GameSettings.Reload();
            Assert.AreEqual("<Keyboard>/g", KeyBindings.Path(Bind.Interact), "바꾸면 바로 저장");

            KeyBindings.Set(Bind.Interact, "<Keyboard>/e"); // 경맥도가 쓰던 키
            Assert.AreEqual("<Keyboard>/e", KeyBindings.Path(Bind.Interact));
            Assert.AreEqual("<Keyboard>/g", KeyBindings.Path(Bind.Meridian), "경맥도는 상호작용이 쓰던 키로");

            KeyBindings.ResetAll();
            Assert.AreEqual("<Keyboard>/f", KeyBindings.Path(Bind.Interact));
            Assert.AreEqual("<Keyboard>/e", KeyBindings.Path(Bind.Meridian));
        }

        [Test]
        public void 설정창에_조작_키_줄이_있음()
        {
            var panel = new SettingsPanel();
            var keyRows = panel.AllRows.Where(r => r.Key.HasValue).ToList();
            Assert.AreEqual(KeyBindings.Count, keyRows.Count);
            int row = panel.AllRows.ToList().FindIndex(r => r.Key == Bind.Dash);
            panel.Change(row, +1);
            Assert.AreEqual(row, panel.ListeningRow, "누르면 새 키를 기다려요");
        }

        [Test]
        public void 피해_숫자_합산_표시()
        {
            var panel = new SettingsPanel();
            int row = panel.AllRows.ToList().FindIndex(r => r.Label == "피해 숫자");
            panel.Change(row, +1);
            Assert.IsTrue(GameSettings.Current.damageNumbersSum, "켜기 → 합산");
            Assert.IsTrue(GameSettings.Current.damageNumbers);

            int before = DamagePopups.Current.Count;
            var at = new Vector3(100f, 1f, 100f);
            DamagePopups.Spawn(at, 7);
            DamagePopups.Spawn(at + new Vector3(0.2f, 0f, 0f), 5);
            Assert.AreEqual(before + 1, DamagePopups.Current.Count, "같은 자리는 한 숫자로");
            Assert.AreEqual(12, DamagePopups.Current.Last().Amount);

            panel.Change(row, +1);
            Assert.IsFalse(GameSettings.Current.damageNumbers, "합산 → 끄기");
            DamagePopups.Spawn(at, 3);
            Assert.AreEqual(before + 1, DamagePopups.Current.Count, "끄면 안 나와요");
        }
    }
}
