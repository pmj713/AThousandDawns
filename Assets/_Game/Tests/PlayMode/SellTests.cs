using System.Collections;
using System.Linq;
using AThousandDawns.Core;
using AThousandDawns.Items;
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
    // 매각: 객잔 매각 칸 (판매가 40%) · 떠돌이 상인 (상인이 고른 기물 하나, 60%)
    public class SellTests : InputTestFixture
    {
        RoomRun _run;
        PlayerController _player;

        public override void Setup()
        {
            base.Setup();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{System.Guid.NewGuid():N}.json");
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 4242;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            Time.timeScale = 1f;
            if (MetaSave.PathOverride != null && System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            base.TearDown();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _player = Object.FindFirstObjectByType<PlayerController>();
            _run.ChooseFork(0);
        }

        ItemDefinition Def(ItemGrade grade, int skip = 0) => GameConfig.Items.Items.Where(d => d.grade == grade).Skip(skip).First();

        [Test]
        public void 매각가는_판매가의_40퍼센트()
        {
            Assert.AreEqual(48, InnShop.SellPrice(new ItemDefinition { grade = ItemGrade.Common }, 0.4f));
            Assert.AreEqual(120, InnShop.SellPrice(new ItemDefinition { grade = ItemGrade.Uncommon }, 0.4f));
            Assert.AreEqual(200, InnShop.SellPrice(new ItemDefinition { grade = ItemGrade.Rare }, 0.4f));
            Assert.AreEqual(360, InnShop.SellPrice(new ItemDefinition { grade = ItemGrade.Legendary }, 0.4f));
        }

        [UnityTest]
        public IEnumerator 객잔_매각_칸에서_가진_기물을_팔아요()
        {
            yield return Load();
            _run.EnterInn();
            var shop = _run.Shop;
            int sell = shop.Slots.ToList().FindIndex(s => s.Kind == InnShop.SlotKind.Sell);
            Assert.GreaterOrEqual(sell, 0, "매각 칸은 늘 있어요");
            foreach (var owned in _player.Items.Items.ToList()) _player.Items.Remove(owned);
            Assert.AreEqual("팔 기물이 없어요", shop.Blocker(sell, _player, 0));

            var common = Def(ItemGrade.Common);
            var rare = Def(ItemGrade.Rare);
            _player.Items.Add(common);
            _player.Items.Add(rare);
            _run.OpenShop();
            Assert.IsTrue(_run.Buy(sell));
            Assert.IsTrue(_run.IsSelling, "매각 목록이 열려요");
            Assert.IsFalse(shop.Slots[sell].Sold, "매각 칸은 계속 남아요");

            int silver = _run.Silver;
            int i = _player.Items.Items.ToList().FindIndex(o => o.Def == rare);
            Assert.IsTrue(_run.Sell(i));
            Assert.AreEqual(silver + InnShop.SellPrice(rare), _run.Silver);
            Assert.IsFalse(_player.Items.Has(rare.id));
            Assert.IsTrue(_run.Sell(0));
            Assert.AreEqual(0, _player.Items.Items.Count);
            Assert.IsFalse(_run.IsSelling, "다 팔면 진열대로");
            _run.CloseShop();
        }

        [UnityTest]
        public IEnumerator 떠돌이_상인은_고른_기물_하나를_60퍼센트에()
        {
            yield return Load();
            foreach (var owned in _player.Items.Items.ToList()) _player.Items.Remove(owned);
            _run.OpenEventForTest("peddler");
            Assert.IsNotNull(_run.CurrentEvent.Choices[0].Locked, "팔 기물이 없으면 잠겨요");
            _run.CloseEvent();

            var legend = Def(ItemGrade.Legendary);
            _player.Items.Add(legend);
            int silver = _run.Silver;
            _run.OpenEventForTest("peddler");
            StringAssert.Contains(legend.name, _run.CurrentEvent.Choices[0].Label);
            Assert.IsTrue(_run.ChooseEvent(0));
            _run.CloseEvent();
            Assert.AreEqual(silver + Mathf.RoundToInt(InnShop.ItemPrice(ItemGrade.Legendary) * 0.6f), _run.Silver);
            Assert.IsFalse(_player.Items.Has(legend.id));
        }
    }
}
