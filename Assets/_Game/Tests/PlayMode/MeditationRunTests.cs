using System;
using System.Collections;
using System.IO;
using AThousandDawns.Core;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using MArt = AThousandDawns.Core.Meditation.Art;

namespace AThousandDawns.Tests
{
    // 면벽수련이 판 시작에 적용되는지 (강·은·기·쾌)와 강 10·20 (첫 피격 무효, 기사회생).
    public class MeditationRunTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 321;
        }

        public override void TearDown()
        {
            MetaSave.PathOverride = null;
            RoomRun.SeedOverride = 0;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        static MetaSave Meta(params (MArt art, int points)[] spend)
        {
            var meta = new MetaSave();
            meta.bloomed.AddRange(new[] { "meditation", "insight1", "insight2", "insight3", "insight4", "insight5" });
            foreach (var (art, points) in spend)
                for (int i = 0; i < points; i++) Assert.IsTrue(Meditation.Add(meta, art, 1));
            meta.Save();
            return meta;
        }

        [UnityTest]
        public IEnumerator 판_시작에_강_은_기가_적용됨()
        {
            Meta((MArt.Iron, 10), (MArt.Shadow, 10), (MArt.Qi, 10));
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.AreEqual(100f + 20f, player.MaxHealth, "강 10 × 2");
            Assert.AreEqual(player.MaxHealth, player.Health);
            Assert.AreEqual(3f, player.Toughness, "강 5: 강인함 +3");
            Assert.AreEqual(50, run.Silver, "은 5: 시작 은자 50");
            Assert.AreEqual(5f * 0.9f, player.SkillCooldown, 1e-3f, "기 5: 초식 쿨타임 −10%");

            run.ChooseFork(0);
            Assert.IsTrue(player.FloorGuard, "강 10: 층에 들어가면 첫 피격 무효");
            Assert.IsFalse(player.TryReceiveHit(30f, player.transform.position + Vector3.forward), "첫 피격은 막아요");
            Assert.AreEqual(player.MaxHealth, player.Health);
            Assert.IsFalse(player.FloorGuard);
        }

        [UnityTest]
        public IEnumerator 강_20이면_한_번_기사회생()
        {
            Meta((MArt.Iron, 20));
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.ChooseFork(0);
            player.FloorGuard = false;
            Assert.IsTrue(player.ReviveReady);
            player.TryReceiveHit(99999f, player.transform.position + Vector3.forward);
            Assert.IsFalse(player.IsDead, "기사회생");
            Assert.AreEqual(player.MaxHealth * 0.4f, player.Health, 0.5f);
            Assert.IsFalse(player.ReviveReady, "한 판에 한 번");
            yield return new WaitForSeconds(2f);
            Assert.IsFalse(run.IsShowingResult);
        }
    }
}
