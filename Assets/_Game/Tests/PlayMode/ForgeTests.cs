using System;
using System.Collections;
using System.IO;
using System.Linq;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
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
    // 대장간: 기물을 녹여 무기 홈에 넣기 (키워드 +1, 같은 키워드 둘이면 강화판)와 검 효과.
    public class ForgeTests : InputTestFixture
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

        static ItemDefinition ItemWith(Keyword k, int skip = 0) => GameConfig.Items.Items.Where(d => d.keyword == k).Skip(skip).First();

        [UnityTest]
        public IEnumerator 녹인_기물은_사라지고_홈마다_키워드_1()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var items = player.Items;
            var a = items.Add(ItemWith(Keyword.Ganggi));
            var b = items.Add(ItemWith(Keyword.Ganggi, 1));

            Assert.IsTrue(items.Fuse(a, 0));
            Assert.IsFalse(items.Has(a.Def.id), "녹인 기물은 사라져요");
            Assert.AreEqual(Keyword.Ganggi, items.ForgeSlot(0));
            Assert.AreEqual(1, items.ForgeLevel(Keyword.Ganggi));

            Assert.IsTrue(items.Fuse(b, 1));
            Assert.AreEqual(2, items.ForgeLevel(Keyword.Ganggi), "같은 키워드 둘 = 강화판");
            Assert.AreEqual(2, player.Keywords.Count(Keyword.Ganggi), "기물 없이 홈 둘만으로 2");

            items.SetForgeSlot(1, Keyword.Mado);
            Assert.AreEqual(Keyword.Ganggi, items.ForgeSlot(1), "보조 키워드는 못 넣어요");
            Assert.IsFalse(PlayerItems.CanFuse(Keyword.Jeongjong));
        }

        [UnityTest]
        public IEnumerator 열양은_화상을_늘리고_빙백은_빙결을_깸()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var grunt = director.Spawn(EnemyKind.Grunt, new Vector3(0f, 0f, 6f));
            var near = director.Spawn(EnemyKind.Grunt, new Vector3(1f, 0f, 6f));
            var status = StatusEffects.For(grunt.Damageable);

            status.AddBurn(0.01f, 0);
            for (int i = 0; i < 10; i++) status.ExtendBurn(1f, 8f);
            status.AddBurn(0.01f, 0); // 새 중첩이면 기본 시간으로 돌아가요
            for (int i = 0; i < 10; i++) status.ExtendBurn(1f, 8f);
            yield return null;
            Assert.Greater(status.BurnStacks, 0);

            Assert.IsTrue(status.TryFreeze());
            Assert.IsTrue(status.BreakFreeze(3f));
            Assert.IsFalse(status.IsFrozen, "깨면 바로 풀려요");
            float normal = GameConfig.Get("freeze.normal_immunity", 3f);
            Assert.AreEqual(Mathf.Max(0f, normal - 3f), status.FreezeImmunityLeft, 0.05f, "면역 −3초");
            Assert.IsFalse(status.BreakFreeze(3f), "빙결이 아니면 못 깨요");
            Assert.AreEqual(0, StatusEffects.For(near.Damageable).SlowStacks);
        }

        [UnityTest]
        public IEnumerator 대장간_길_보상_방에서_녹여_넣기()
        {
            var meta = new MetaSave { memory = 0 };
            meta.bloomed.Add("forge");
            meta.Save();
            RoomRun.SeedOverride = 77;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsTrue(run.ForgeUnlocked);
            player.Items.Add(ItemWith(Keyword.Gyeonggong));

            for (int i = 0; i < 30 && !run.ForkOptions.Any(p => p.Reward == RoomRun.RewardKind.Forge); i++) run.BeginFork(1);
            int pick = Enumerable.Range(0, run.ForkOptions.Count).First(i => run.ForkOptions[i].Reward == RoomRun.RewardKind.Forge);
            bool dense = run.ForkOptions[pick].Dense;
            run.ChooseFork(pick);
            var reward = run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Reward);
            run.Enter(reward, 0);
            yield return WaitUntil(() =>
            {
                for (int i = director.Enemies.Count - 1; i >= 0; i--) director.Enemies[i].Damageable.ApplyHit(99999);
                return !run.IsFighting;
            }, 20f);
            yield return WaitUntil(() => run.IsInEvent, 2f);
            Assert.AreEqual("forge", run.CurrentEvent.Id);
            int index = run.CurrentEvent.Choices.FindIndex(c => c.Label.Contains("#경공"));
            Assert.GreaterOrEqual(index, 0);
            run.ChooseEvent(index);
            Assert.AreEqual("forge_slot", run.CurrentEvent.Id, "다음은 홈 고르기");
            run.ChooseEvent(0);
            Assert.AreEqual("forge_confirm", run.CurrentEvent.Id, "확인 창 (기본 켜기): 녹이기 전에 묻기");
            run.ChooseEvent(0);
            Assert.IsNotNull(run.CurrentEvent.Result);
            run.CloseEvent();
            Assert.AreEqual(Keyword.Gyeonggong, player.Items.ForgeSlot(0));
            Assert.AreEqual(dense, run.IsInEvent, "짙은 길이면 한 번 더");
            if (dense) run.CloseEvent();
        }

        [Test]
        public void 대장간_길은_회귀록에서_연_뒤부터()
        {
            Assert.AreEqual("대장간", RoomRun.RewardLabel(RoomRun.RewardKind.Forge));
            Assert.IsFalse(RegressionBook.ForgeUnlocked(new MetaSave()));
        }
    }
}
