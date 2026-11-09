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
    // 면벽수련이 판 시작에 적용되는지 (강·은·기·쾌·맥)와 강 10·20 (방 회복, 기사회생), 기 20·맥 20·은 20.
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
            var director = Object.FindFirstObjectByType<AThousandDawns.Enemies.EnemyDirector>();
            var room = System.Linq.Enumerable.First(run.Map.Rooms, r => r.Kind == FloorRoomKind.Combat);
            run.Enter(room, -1);
            Assert.IsTrue(player.TryReceiveHit(40f, player.transform.position + Vector3.forward));
            float hurt = player.Health;
            Assert.Less(hurt, player.MaxHealth);
            float end = Time.realtimeSinceStartup + 20f;
            while (run.IsFighting && Time.realtimeSinceStartup < end)
            {
                for (int i = director.Enemies.Count - 1; i >= 0; i--) director.Enemies[i].Damageable.ApplyHit(99999);
                yield return null;
            }
            Assert.IsFalse(run.IsFighting);
            Assert.AreEqual(Mathf.Min(player.MaxHealth, hurt + player.MaxHealth * 0.04f), player.Health, 0.5f, "강 10: 방을 비우면 체력 4%");
        }

        [UnityTest]
        public IEnumerator 기_20_맥_20_은_20()
        {
            Meta((MArt.Qi, 20), (MArt.Meridian, 10));
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsTrue(player.DoubleGauge);
            Assert.AreEqual(player.GaugeMax * 2f, player.GaugeCap);
            player.FillGauge();
            Assert.AreEqual(player.GaugeMax * 2f, player.Gauge, "기 20: 기세 200%");
            Assert.IsTrue(player.UltimateReady);
            Assert.AreEqual(1, player.Items.StartDanjeons, "맥 10만 찍었으면 단전 1개");

            Meta((MArt.Meridian, 20), (MArt.Shadow, 10));
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            run = Object.FindFirstObjectByType<RoomRun>();
            player = Object.FindFirstObjectByType<PlayerController>();
            Assert.AreEqual(2, player.Items.StartDanjeons, "맥 20: 단전 2개");
            Assert.AreEqual(Mathf.RoundToInt(GameConfig.Get("player.gongryeok", 3f)) - 1, player.Gongryeok, "대신 공력 −1");
            Assert.IsFalse(player.DoubleGauge);
        }

        [UnityTest]
        public IEnumerator 은_20이면_객잔에서_이자()
        {
            Meta((MArt.Shadow, 20));
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.AreEqual(50, run.Silver, "은 5: 시작 은자 50");
            run.EnterInn();
            Assert.AreEqual(60, run.Silver, "은 20: 50의 20% = 10");
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
