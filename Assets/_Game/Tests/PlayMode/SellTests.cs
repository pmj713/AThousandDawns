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

        // 진열대에서 경맥도(E)를 열었다 닫아도 진열대는 멈춘 그대로예요 (닫을 때 플레이어를 다시 켜서 진열대 좌클릭이 평타로 나가던 것)
        [UnityTest]
        public IEnumerator 진열대_위에서_경맥도를_닫아도_좌클릭이_평타로_안_나가요()
        {
            yield return Load();
            _run.EnterInn();
            _run.OpenShop();
            Assert.IsFalse(_player.enabled);
            var meridian = Object.FindFirstObjectByType<MeridianScreen>();
            meridian.TryOpen();
            Assert.IsTrue(meridian.IsOpen, "객잔은 싸우는 중이 아니라 열려요");
            meridian.Close();
            Assert.IsTrue(_run.IsShopping);
            Assert.IsFalse(_player.enabled, "진열대가 열려 있으면 계속 멈춰 있어요");
            Assert.AreEqual(0f, Time.timeScale);

            _player.enabled = true; // 다른 창이 다시 켜도
            yield return null;
            Assert.IsFalse(_player.enabled, "진열대에선 다시 꺼요");

            _run.CloseShop();
            Assert.IsTrue(_player.enabled, "닫으면 다시 움직여요");
            meridian.TryOpen();
            meridian.Close();
            Assert.IsTrue(_player.enabled, "창이 없으면 경맥도를 닫을 때 다시 켜요");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 소문을_들으면_다음_층에_기연_방이_보임()
        {
            yield return Load();
            _run.EnterInn();
            var shop = _run.Shop;
            int rumor = shop.Slots.ToList().FindIndex(s => s.Kind == InnShop.SlotKind.Rumor);
            Assert.GreaterOrEqual(rumor, 0, "소문 듣기 칸은 늘 있어요");
            Assert.AreEqual(200, shop.Slots[rumor].Price);
            _run.GiveSilver(200);
            _run.OpenShop();
            Assert.IsTrue(_run.Buy(rumor));
            Assert.IsTrue(_run.RumorPending);
            Assert.IsTrue(shop.Slots[rumor].Sold, "객잔마다 한 번");
            _run.CloseShop();

            _run.BeginFork(_run.Floor + 1);
            _run.ChooseFork(0);
            Assert.IsNotNull(_run.Map.Hidden, "다음 층에 기연 방이 꼭");
            Assert.IsTrue(_run.Map.Hidden.Revealed, "처음부터 지도에 보여요");
            Assert.IsFalse(_run.RumorPending, "한 번 쓰면 끝");
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
