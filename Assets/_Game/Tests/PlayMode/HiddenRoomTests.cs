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
    // 기연 방: 공력으로 감지, 금 간 벽을 부숴 열고, 옥병 (영약 · 원기단) · 낡은 책 · 유품 (기물 · 은자) 가운데 하나.
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
            RoomRun.JinjeonOverride = false;
            MetaSave.PathOverride = null;
            RoomRun.SeedOverride = 0;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            // 게임 시간과 실제 시간이 둘 다 지나야 포기해요 (느린 프레임은 deltaTime이 잘려 게임 시간이 늦게 흘러요). 실제 시간 5배가 상한.
            float end = Time.time + timeout, realEnd = Time.realtimeSinceStartup + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && (Time.time < end || Time.realtimeSinceStartup < realEnd) && Time.realtimeSinceStartup < hardEnd) yield return null;
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
        public IEnumerator 벽을_부수면_열리고_옥병_책_유품_중_하나()
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

            int power = player.Gongryeok, pending = player.Level.Pending;
            run.Enter(hidden, FloorMap.Opposite(dir));
            Assert.IsTrue(run.IsInEvent);
            Assert.AreEqual("hidden", run.CurrentEvent.Id);
            Assert.AreEqual(3, run.CurrentEvent.Choices.Count, "옥병 · 낡은 책 · 유품");
            bool elixir = run.CurrentEvent.Choices[0].Label.Contains("공력 +1"); // 옥병: 영약 (공력) 아니면 원기단 (혈자리)
            StringAssert.Contains("수련치", run.CurrentEvent.Choices[1].Label);
            Assert.IsTrue(run.CurrentEvent.Choices[2].Label.Contains("기물") || run.CurrentEvent.Choices[2].Label.Contains("은자"), "유품: 기물이나 은자");
            run.ChooseEvent(0);
            run.CloseEvent();
            if (elixir) Assert.AreEqual(power + 1, player.Gongryeok);
            else
            {
                Assert.AreEqual(power, player.Gongryeok, "원기단은 공력 대신");
                Assert.AreEqual(pending + 1, player.Level.Pending, "혈자리 하나");
            }
            Assert.IsTrue(hidden.Cleared);
            run.Enter(host, dir);
            run.Enter(hidden, FloorMap.Opposite(dir));
            Assert.IsFalse(run.IsInEvent, "한 번만");

            // 옥병 · 유품에 든 것은 방마다 달라요 (늘 공력만이 아니에요)
            var rng = new System.Random(5);
            var seen = new System.Collections.Generic.HashSet<RoomRun.HiddenReward>();
            for (int i = 0; i < 200; i++)
            {
                var (bottle, relic) = RoomRun.RollHiddenRewards(rng);
                seen.Add(bottle);
                seen.Add(relic);
            }
            Assert.AreEqual(4, seen.Count, "영약 · 원기단 · 기물 · 은자가 모두 나와요");
        }
        [UnityTest]
        public IEnumerator 아주_드물게_진전_검결()
        {
            RoomRun.JinjeonOverride = true;
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
            run.BreakCrack();
            run.Enter(hidden, FloorMap.Opposite(dir));
            Assert.AreEqual(4, run.CurrentEvent.Choices.Count, "옥병 · 낡은 책 · 유품 · 진전");
            StringAssert.Contains("진전", run.CurrentEvent.Choices[3].Label);
            Assert.AreEqual(AThousandDawns.Combat.UltimateForm.None, player.UltimateForm);
            run.ChooseEvent(3);
            run.CloseEvent();
            Assert.AreNotEqual(AThousandDawns.Combat.UltimateForm.None, player.UltimateForm);
            Assert.IsFalse(AThousandDawns.Combat.UltimateForms.ForDagger(player.UltimateForm), "검이면 검 진전");
        }
    }
}
