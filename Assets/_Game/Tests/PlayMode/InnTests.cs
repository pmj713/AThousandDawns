using System;
using System.Collections;
using System.Linq;
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
    // 객잔 (진열 기물 3 · 금창약 2 · 숙박)과 금창약 (R 0.5초 누르기, 40% 회복, 손 떼면 취소).
    public class InnTests : InputTestFixture
    {
        Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 4242;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator 마지막_층_관문_뒤_객잔에서_사고_정비실로()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.ChooseFork(0);
            for (int guard = 0; run.Floor < run.Floors && guard < 10; guard++)
            {
                run.LeaveFloor();
                if (run.CurrentPhase == RoomRun.Phase.Master) // 2층 관문: 고수를 쓰러뜨리고 흡성 (은자는 그대로)
                {
                    run.Master.Damageable.ApplyHit(Mathf.CeilToInt(run.Master.Damageable.MaxHealth * 0.55f));
                    yield return WaitUntil(() => run.MasterInfo.Phase == 2 && !run.MasterInfo.IsTransitioning, 4f);
                    run.Master.Damageable.ApplyHit(99999);
                    yield return WaitUntil(() => run.IsInEvent, 3f);
                    run.ChooseEvent(1);
                    run.CloseEvent();
                }
                run.ChooseFork(0);
            }
            Assert.AreEqual(run.Floors, run.Floor);
            run.LeaveFloor();
            Assert.AreEqual(RoomRun.Phase.Inn, run.CurrentPhase, "마지막 층 관문 → 객잔");

            var shop = run.Shop;
            Assert.AreEqual(3, shop.Slots.Count(s => s.Kind == InnShop.SlotKind.Item), "기물 3개");
            Assert.AreEqual(2, shop.Slots.Count(s => s.Kind == InnShop.SlotKind.Potion), "금창약 2개");
            Assert.AreEqual(1, shop.Slots.Count(s => s.Kind == InnShop.SlotKind.Lodging));
            foreach (var s in shop.Slots.Where(s => s.Kind == InnShop.SlotKind.Item))
                Assert.AreEqual(InnShop.ItemPrice(s.Item.grade), s.Price);

            // 점소이 앞에서 진열대
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = run.InnCounter;
            cc.enabled = true;
            Assert.IsTrue(run.AtCounter);
            run.OpenShop();
            Assert.IsTrue(run.IsShopping);
            Assert.AreEqual(0f, Time.timeScale);

            int potionIndex = shop.Slots.ToList().FindIndex(s => s.Kind == InnShop.SlotKind.Potion);
            Assert.IsFalse(run.Buy(potionIndex), "은자 0이면 못 사요");
            Assert.AreEqual("은자가 모자라요", run.ShopNotice);

            run.GiveSilver(1000);
            Assert.IsTrue(run.Buy(potionIndex));
            Assert.AreEqual(1, player.Potions);
            Assert.AreEqual(940, run.Silver, "금창약 60");
            Assert.IsFalse(run.Buy(potionIndex), "팔린 칸은 다시 못 사요");

            int lodging = shop.Slots.ToList().FindIndex(s => s.Kind == InnShop.SlotKind.Lodging);
            Assert.IsFalse(run.Buy(lodging), "체력이 가득이면 숙박 안 함");

            int itemIndex = shop.Slots.ToList().FindIndex(s => s.Kind == InnShop.SlotKind.Item && s.Price <= run.Silver);
            int price = shop.Slots[itemIndex].Price;
            Assert.IsTrue(run.Buy(itemIndex));
            Assert.AreEqual(1, player.Items.Items.Count);
            Assert.AreEqual(940 - price, run.Silver);

            run.CloseShop();
            Assert.AreEqual(1f, Time.timeScale);
            cc.enabled = false;
            player.transform.position = new Vector3(180f, 0f, 6.6f); // 북쪽 문 너머
            cc.enabled = true;
            yield return WaitUntil(() => run.CurrentPhase == RoomRun.Phase.Rest, 2f);
            Assert.AreEqual(RoomRun.Phase.Rest, run.CurrentPhase, "객잔 → 정비실");
        }

        [UnityTest]
        public IEnumerator 금창약은_R을_0점5초_누르면_40퍼센트_회복_떼면_취소()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.AreEqual(0, player.Potions, "시작 0개");
            Assert.IsTrue(player.AddPotion());
            Assert.IsTrue(player.AddPotion());
            Assert.IsTrue(player.AddPotion());
            Assert.IsFalse(player.AddPotion(), "최대 3개");

            player.TryReceiveHit(60f, player.transform.position + Vector3.forward);
            yield return new WaitForSeconds(0.8f); // 피격 경직·무적이 끝나게
            float hurt = player.Health;

            // 0.2초만 누르고 떼면 취소, 소모 안 함
            Press(_keyboard.rKey);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(player.IsDrinking);
            Release(_keyboard.rKey);
            yield return null;
            yield return null;
            Assert.IsFalse(player.IsDrinking);
            Assert.AreEqual(3, player.Potions);
            Assert.AreEqual(hurt, player.Health);

            // 끝까지 누르면 40% 회복
            Press(_keyboard.rKey);
            yield return new WaitForSeconds(0.7f);
            Release(_keyboard.rKey);
            Assert.AreEqual(2, player.Potions);
            Assert.AreEqual(Mathf.Min(player.MaxHealth, hurt + player.MaxHealth * 0.4f), player.Health, 0.01f);
        }
    }
}
