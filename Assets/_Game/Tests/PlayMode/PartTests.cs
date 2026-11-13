using System;
using System.Collections;
using System.IO;
using System.Linq;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
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
    // 부품 놓기 (기물과 칸을 나눠 써요)와 갈림길 부품 길.
    public class PartTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
        }

        public override void TearDown()
        {
            MetaSave.PathOverride = null;
            RoomRun.SeedOverride = 0;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            // 게임 시간과 실제 시간이 둘 다 지나야 포기해요 (느린 프레임은 deltaTime이 잘려 게임 시간이 늦게 흘러요). 실제 시간 5배가 상한.
            float end = Time.time + timeout, realEnd = Time.realtimeSinceStartup + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && (Time.time < end || Time.realtimeSinceStartup < realEnd) && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        [UnityTest]
        public IEnumerator 부품_칸에는_기물을_못_놓고_새_기물은_비켜_감()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var items = Object.FindFirstObjectByType<PlayerController>().Items;
            var part = items.AddPart(PartKind.Hanok);
            Assert.IsTrue(items.PlacePart(part, new Vector2Int(0, 0)));
            var item = items.Add(GameConfig.Items.Items[0]);
            Assert.AreNotEqual(new Vector2Int(0, 0), item.Cell, "새 기물은 부품 칸을 건너뛰어요");
            Assert.AreEqual(new Vector2Int(1, 0), item.Cell);
            Assert.AreEqual(2, item.MaxQiBonus, "한옥 옆이라 최대 기 +2");
            Assert.IsFalse(items.PlacePart(items.AddPart(PartKind.Tongmaek), item.Cell), "기물 칸에는 부품 못 놓아요");
            items.Place(item, new Vector2Int(0, 0));
            Assert.AreEqual(new Vector2Int(1, 0), item.Cell, "부품 칸으로 기물을 못 옮겨요");

            items.RemovePart(part);
            Assert.IsFalse(part.OnBoard);
            Assert.AreEqual(0, item.MaxQiBonus, "한옥을 거두면 보너스도 사라져요");
        }

        [UnityTest]
        public IEnumerator 부품_길의_보상_방에서_부품을_고름()
        {
            var meta = new MetaSave { memory = 40 };
            Assert.IsTrue(RegressionBook.Bloom(meta, "tongmaek"));
            meta.Save();
            RoomRun.SeedOverride = 77;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            CollectionAssert.AreEqual(new[] { PartKind.Tongmaek }, run.UnlockedParts.ToArray(), "회귀록에서 연 부품만");

            for (int i = 0; i < 30 && !run.ForkOptions.Any(p => p.Reward == RoomRun.RewardKind.Part); i++) run.BeginFork(1);
            int pick = Enumerable.Range(0, run.ForkOptions.Count).First(i => run.ForkOptions[i].Reward == RoomRun.RewardKind.Part);
            bool dense = run.ForkOptions[pick].Dense;
            run.ChooseFork(pick);
            var reward = run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Reward);
            run.Enter(reward, 0);
            Assert.IsFalse(run.IsFighting, "보상 방은 싸움 없이");
            run.OpenPouch(); // 가운데 보상 앞에서 상호작용
            yield return WaitUntil(() => run.IsInEvent, 2f);
            Assert.IsTrue(run.IsInEvent, "보상 방: 부품 고르기");
            Assert.AreEqual(1, run.CurrentEvent.Choices.Count, "통맥만 열었어요");
            run.ChooseEvent(0);
            run.CloseEvent();
            if (dense)
            {
                Assert.IsTrue(run.IsInEvent, "짙은 길은 하나 더");
                run.ChooseEvent(0);
                run.CloseEvent();
            }
            Assert.AreEqual(dense ? 2 : 1, player.Items.CountParts(PartKind.Tongmaek));
        }
    }
}
