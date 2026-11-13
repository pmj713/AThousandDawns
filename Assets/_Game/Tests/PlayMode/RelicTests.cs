using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    // 유품록: 천마를 격파한 판에 지녔던 기물이 남고, 마을 사당에서 기억 용량만큼 골라 두면 다음 판을 지니고 시작해요.
    public class RelicTests : InputTestFixture
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
            RoomRun.SkipVillage = false;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        static ItemDefinition Def(ItemGrade grade, int skip = 0) =>
            GameConfig.Items.Items.Where(d => d.grade == grade && ItemDatabase.PackOf(d.id) == 0).Skip(skip).First();

        [UnityTest]
        public IEnumerator 천마를_격파한_판에_지닌_기물이_유품록에_남음()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var def = Def(ItemGrade.Rare);
            player.Items.Add(def);
            run.SetRegionForTest(3);
            run.ChooseFork(0);
            run.EnterRest();
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(60f, 0f, 6.6f);
            cc.enabled = true;
            yield return WaitUntil(() => arena.Boss != null && arena.Boss.IsAwake, 3f);
            arena.Boss.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => run.IsDone, 4f);
            run.ShowResult(true);
            var meta = MetaSave.Load();
            CollectionAssert.Contains(meta.relics, def.id, "지닌 기물이 유품으로");
            Assert.AreEqual(1, RegressionBook.RelicCapacity(meta), "첫 천마 격파: 기억 용량 1");
        }

        [UnityTest]
        public IEnumerator 사당에서_고른_유품을_지니고_다음_판을_시작함()
        {
            var common = Def(ItemGrade.Uncommon);
            var legendary = Def(ItemGrade.Legendary);
            var meta = new MetaSave { runs = 1, finalBossKills = 1, tutorialFloorDone = true };
            meta.relics.Add(common.id);
            meta.relics.Add(legendary.id);
            meta.Save();
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsTrue(run.InVillage);

            run.UseSpot(RoomRun.VillageSpot.Shrine);
            Assert.AreEqual("relic_book", run.CurrentEvent.Id);
            int Index(string name) => run.CurrentEvent.Choices.FindIndex(c => c.Label.Contains(name));
            Assert.AreEqual("기억 용량이 모자라요", run.CurrentEvent.Choices[Index(legendary.name)].Locked, "전설은 기억 2");
            Assert.IsTrue(run.ChooseEvent(Index(common.name)));
            StringAssert.StartsWith("■", run.CurrentEvent.Choices[Index(common.name)].Label, "골랐어요");
            CollectionAssert.Contains(MetaSave.Load().relicPicks, common.id);
            Assert.IsTrue(run.ChooseEvent(Index("뒤로 가기"))); // 업보가 없으면 사당 메뉴가 없어서 닫혀요
            Assert.IsFalse(run.IsInEvent);

            var old = run;
            run.Depart();
            for (int i = 0; i < 30 && (old != null || Object.FindFirstObjectByType<RoomRun>() == null); i++) yield return null;
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsTrue(player.Items.Has(common.id), "고른 유품을 지니고 시작");
            Assert.IsFalse(player.Items.Has(legendary.id));
        }

        [Test]
        public void 유품_가지_봉우리마다_기억_용량이_늘어남()
        {
            var meta = new MetaSave { finalBossKills = 0, memory = 9999 };
            Assert.AreEqual(0, RegressionBook.RelicCapacity(meta), "천마 격파 전엔 닫혀 있어요");
            Assert.IsFalse(RegressionBook.Bloom(meta, "relic1"));
            meta.finalBossKills = 1;
            foreach (var id in new[] { "relic1", "relic2", "relic3", "relic4" }) Assert.IsTrue(RegressionBook.Bloom(meta, id), id);
            Assert.AreEqual(5, RegressionBook.RelicCapacity(meta));
            Assert.AreEqual(2, RegressionBook.RelicCost(ItemGrade.Legendary));
            Assert.AreEqual(1, RegressionBook.RelicCost(ItemGrade.Rare));
        }
    }
}
