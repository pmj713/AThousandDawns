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
    // 기연 방: 공력으로 감지, 금 간 벽을 부숴 열고, 영약이나 비급.
    public class HiddenRoomTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.HiddenOverride = true;
        }

        public override void TearDown()
        {
            RoomRun.HiddenOverride = false;
            MetaSave.PathOverride = null;
            RoomRun.SeedOverride = 0;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Load(Action<PlayerController> beforeFork)
        {
            RoomRun.SeedOverride = 31;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            beforeFork?.Invoke(Object.FindFirstObjectByType<PlayerController>());
            run.ChooseFork(0);
        }

        [UnityTest]
        public IEnumerator 공력이_낮으면_지도에_없고_높으면_희미하게()
        {
            yield return Load(p => p.Gongryeok = 1);
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsNotNull(run.Map.Hidden);
            Assert.IsFalse(run.Map.Hidden.Revealed, "공력 1: 못 느껴요");
            Assert.AreEqual(4, run.HiddenSensePower);

            yield return Load(p => p.Gongryeok = 4);
            run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsTrue(run.Map.Hidden.Revealed, "공력 4: 지도에 희미하게");
            Assert.IsFalse(run.Map.Hidden.Found);
        }

        [UnityTest]
        public IEnumerator 벽을_부수면_열리고_영약으로_공력_1()
        {
            yield return Load(null);
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var hidden = run.Map.Hidden;
            int dir = Enumerable.Range(0, 4).First(d => run.Map.Neighbor(hidden, d) != null);
            var host = run.Map.Neighbor(hidden, dir);
            run.Enter(host, -1);
            yield return WaitUntil(() =>
            {
                for (int i = director.Enemies.Count - 1; i >= 0; i--) director.Enemies[i].Damageable.ApplyHit(99999);
                return !run.IsFighting;
            }, 20f);
            if (run.IsInEvent) run.CloseEvent();

            int memoryBefore = run.Record.HiddenRooms;
            run.BreakCrack();
            Assert.IsTrue(hidden.Found);
            Assert.AreEqual(memoryBefore + 1, run.Record.HiddenRooms, "기연 방 발견을 세요");

            int power = player.Gongryeok;
            run.Enter(hidden, FloorMap.Opposite(dir));
            Assert.IsTrue(run.IsInEvent);
            Assert.AreEqual("hidden", run.CurrentEvent.Id);
            run.ChooseEvent(0);
            run.CloseEvent();
            Assert.AreEqual(power + 1, player.Gongryeok);
            Assert.IsTrue(hidden.Cleared);
            run.Enter(host, dir);
            run.Enter(hidden, FloorMap.Opposite(dir));
            Assert.IsFalse(run.IsInEvent, "한 번만");
        }
    }
}
