using System;
using System.Collections;
using AThousandDawns.Combat;
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
    // 폐사찰 2층 관문 고수 파계승 혜광: 금강불괴 · 2페이즈 · 꺾으면 고수 보상
    public class HyegwangTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 4242;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            MetaSave.PathOverride = null;
            if (System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        [UnityTest]
        public IEnumerator 폐사찰_관문은_혜광_금강불괴와_2페이즈()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(1);
            run.ChooseFork(0);
            run.EnterMaster();

            var master = run.Master as HyegwangBrain;
            Assert.IsNotNull(master, "폐사찰 고수는 혜광");
            Assert.AreEqual(1980f, master.Damageable.MaxHealth, 1f, "1100 × 1.8");

            yield return WaitUntil(() => master.IsVajra, 12f);
            Assert.IsTrue(master.IsVajra, "가까우면 금강불괴");
            Assert.AreEqual(0f, master.Damageable.ResolveSituational(player.transform.position, HitTag.Basic), "버티는 동안 최소 피해");
            yield return WaitUntil(() => !master.IsVajra, 4f);
            Assert.AreEqual(1f, master.Damageable.ResolveSituational(player.transform.position, HitTag.Basic));

            master.Damageable.ApplyHit(Mathf.CeilToInt(master.Damageable.MaxHealth * 0.55f));
            yield return WaitUntil(() => master.Phase == 2 && !master.IsTransitioning, 4f);
            Assert.AreEqual(2, master.Phase);

            master.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => run.IsInEvent, 3f);
            Assert.AreEqual("master", run.CurrentEvent.Id);
            StringAssert.Contains("혜광", run.CurrentEvent.Title);
            Assert.AreEqual(3, run.CurrentEvent.Choices.Count, "비급 · 흡성 · 방면");
        }
    }
}
