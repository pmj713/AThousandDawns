using System.Collections;
using System.Linq;
using AThousandDawns.Core;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AThousandDawns.Tests
{
    // 객잔: 비급은 진열하지 않음 (고수 보상으로만) · 이혈 (100, 단전 하나 옮기기)
    public class InnGoodsTests : InputTestFixture
    {
        PlayerController _player;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            PrototypeArena.AutoSpawn = false;
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            base.TearDown();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
        }

        InnShop Stock(int seed) => InnShop.Stock(GameConfig.Items.Items, _player.Items, null, new System.Random(seed), currentSkill: _player.Skill);

        [UnityTest]
        public IEnumerator 비급은_진열하지_않음()
        {
            yield return Load();
            for (int seed = 1; seed <= 200; seed++)
                Assert.IsFalse(Stock(seed).Slots.Any(x => x.Kind == InnShop.SlotKind.Scroll), "비급은 고수 보상으로만");
        }

        [UnityTest]
        public IEnumerator 이혈을_사면_단전을_한_번_옮김()
        {
            yield return Load();
            var items = _player.Items;
            var shop = Stock(1);
            int i = shop.Slots.ToList().FindIndex(x => x.Kind == InnShop.SlotKind.MoveDanjeon);
            Assert.GreaterOrEqual(i, 0, "이혈은 늘 있어요");
            Assert.AreEqual(100, shop.Slots[i].Price);
            foreach (var d in items.Board.Danjeons.ToList()) items.RemoveDanjeon(d);
            Assert.AreEqual("아직 단전이 없어요", shop.Blocker(i, _player, 999));

            var from = new Vector2Int(0, 0);
            var to = new Vector2Int(3, 2);
            Assert.IsTrue(items.AddDanjeon(from));
            Assert.IsFalse(items.MoveDanjeon(from, to), "사기 전엔 못 옮겨요");
            Assert.AreEqual(100, shop.Buy(i, _player, 100));
            Assert.AreEqual(1, items.DanjeonMoves);
            Assert.IsTrue(items.MoveDanjeon(from, to));
            Assert.IsTrue(items.Board.IsDanjeon(to));
            Assert.IsFalse(items.Board.IsDanjeon(from));
            Assert.AreEqual(0, items.DanjeonMoves);
            Assert.IsFalse(items.MoveDanjeon(to, from), "한 번만");
        }
    }
}
