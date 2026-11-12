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
    // 업보: 천마를 격파하면 다음 단계가 열리고, 마을 사당에서 골라요. 단계는 쌓이고, 단계마다 얻는 기억 +10%.
    public class KarmaTests : InputTestFixture
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

        static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 사당에서_열린_단계까지_업보를_고름()
        {
            new MetaSave { runs = 1, finalBossKills = 1, karmaMax = 2, tutorialFloorDone = true }.Save();
            yield return Load();
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsTrue(run.InVillage);

            run.UseSpot(RoomRun.VillageSpot.Shrine);
            Assert.AreEqual("relic_book_menu", run.CurrentEvent.Id, "업보가 열리면 사당은 메뉴부터");
            int Index(string text) => run.CurrentEvent.Choices.FindIndex(c => c.Label.Contains(text));
            Assert.IsTrue(run.ChooseEvent(Index("업보")));
            Assert.AreEqual("karma", run.CurrentEvent.Id);
            Assert.AreEqual(4, run.CurrentEvent.Choices.Count, "0·1·2단계 + 닫기 (3단계는 아직 안 열림)");
            Assert.IsTrue(run.ChooseEvent(Index("2단계")));
            Assert.AreEqual(2, run.Karma);
            Assert.AreEqual(2, MetaSave.Load().karma);
            StringAssert.StartsWith("■", run.CurrentEvent.Choices[Index("2단계")].Label);
            Assert.IsTrue(run.ChooseEvent(Index("닫는다")));
            Assert.IsFalse(run.IsInEvent);
        }

        [UnityTest]
        public IEnumerator 업보_6단계_추적도_체력_공격_특성_은자()
        {
            new MetaSave { runs = 1, finalBossKills = 6, karma = 6, karmaMax = 6, tutorialFloorDone = true }.Save();
            RoomRun.SkipVillage = true; // 마을을 나선 것처럼 바로 판 시작
            yield return Load();
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.AreEqual(6, run.Karma);
            Assert.AreEqual(3, run.Tracking, "3단계: 추적도 3으로 시작");

            Vector3 at = player.transform.position + new Vector3(0f, 0f, 8f);
            var plain = director.Spawn(EnemyKind.Grunt, at);
            var karma = director.Spawn(EnemyKind.Grunt, at + Vector3.right * 2f);
            run.ApplySpawnRulesForTest(karma);
            Assert.AreEqual(plain.Damageable.MaxHealth * 1.15f, karma.Damageable.MaxHealth, 1f, "1단계: 체력 +15%");
            Assert.AreEqual(plain.AttackStat * 1.15f, karma.AttackStat, 0.01f, "4단계: 공격 +15%");

            var master = director.Spawn(EnemyKind.Hyegwang, at + Vector3.left * 3f);
            float baseHealth = master.Damageable.MaxHealth;
            run.ApplySpawnRulesForTest(master);
            Assert.AreEqual(baseHealth * 1.15f * 1.25f, master.Damageable.MaxHealth, 1f, "6단계: 고수 체력 +25% 더");

            var elite = director.Spawn(EnemyKind.Boar, at + Vector3.back * 2f);
            elite.MakeElite(EliteTrait.Blaze);
            run.ApplySpawnRulesForTest(elite);
            Assert.AreEqual(2, elite.Traits.Count, "2단계: 죽림에서도 특성 둘");
        }

        [UnityTest]
        public IEnumerator 열린_가장_높은_단계로_천마를_격파하면_다음_단계와_기억_보너스()
        {
            new MetaSave { runs = 1, finalBossKills = 1, karma = 1, karmaMax = 1, tutorialFloorDone = true }.Save();
            RoomRun.SkipVillage = true;
            yield return Load();
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.AreEqual(1, run.Karma);
            run.SetRegionForTest(3);
            run.ChooseFork(0);
            run.EnterRest();
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(60f, 0f, 6.6f);
            cc.enabled = true;
            yield return WaitUntil(() => arena.Boss != null && arena.Boss.IsAwake, 3f);
            Assert.AreEqual(14175f * 1.15f, arena.Boss.Damageable.MaxHealth, 2f, "업보 1단계: 체력 +15% (보스 +25%는 6단계)");
            arena.Boss.Damageable.ApplyHit(999999);
            yield return WaitUntil(() => run.IsDone, 4f);
            run.ShowResult(true);
            var line = run.Result.Lines.First(l => l.label == "업보 1단계");
            float before = run.Result.Lines.Where(l => l.label != "업보 1단계").Sum(l => l.memory);
            Assert.AreEqual(before * 0.1f, line.memory, 0.01f, "1단계: 기억 +10%");
            Assert.AreEqual(2, MetaSave.Load().karmaMax, "2단계가 열려요");
        }
    }
}
