using System;
using System.Collections;
using System.IO;
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
    // 판 결과 화면: 쓰러지면 기록을 회귀의 기억으로 바꿔 판 밖 저장에 더해요.
    public class ResultTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 99;
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            MetaSave.PathOverride = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator 사망_원인과_그_밖의_기록()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.ChooseFork(0);
            yield return new WaitForSeconds(0.3f);

            var grunt = director.Spawn(EnemyKind.Grunt, player.transform.position + new Vector3(0f, 0f, 3f));
            grunt.Damageable.ApplyHit(7);
            grunt.Damageable.ApplyHit(12);
            Assert.AreEqual(19, run.Record.DamageDealt, "준 피해");
            Assert.AreEqual(12, run.Record.MaxHit, "최대 단일 피해");
            run.SetTracking(6);
            yield return null;
            Assert.AreEqual(6, run.Record.MaxTracking);

            Assert.IsTrue(player.TryReceiveHit(99999f, grunt.transform.position, -1f, grunt.Damageable));
            Assert.Greater(run.Record.DamageTaken, 0, "받은 피해");
            yield return WaitUntil(() => run.IsShowingResult, 4f);
            StringAssert.Contains($"{grunt.DisplayName}에게 쓰러짐", run.Result.Where, "사망 원인");
            Assert.Greater(run.Result.PlaySeconds, 0.2f, "플레이 시간");
            Assert.AreEqual("19", run.Result.Stats[0].value);
        }

        [Test]
        public void 시계_글자()
        {
            Assert.AreEqual("0:05", RunRecord.Clock(5f));
            Assert.AreEqual("12:34", RunRecord.Clock(754f));
            Assert.AreEqual("1:02:03", RunRecord.Clock(3723f));
        }

        [UnityTest]
        public IEnumerator 쓰러지면_결과_화면과_기억이_저장됨()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.ChooseFork(0);
            run.LeaveFloor(); // 1층 돌파
            run.ChooseFork(0);

            for (int i = 0; i < 5; i++) director.Spawn(EnemyKind.Grunt, new Vector3(-6f + i * 3f, 0f, 4f)).Damageable.ApplyHit(99999);
            var elite = director.Spawn(EnemyKind.Grunt, new Vector3(6f, 0f, -4f));
            elite.MakeElite(EliteTrait.IronShirt);
            elite.Damageable.ApplyHit(99999);
            yield return null;
            Assert.AreEqual(5, run.Record.Kills[(int)EnemyRank.Normal]);
            Assert.AreEqual(1, run.Record.Kills[(int)EnemyRank.Elite]);

            player.TryReceiveHit(99999f, player.transform.position + Vector3.forward);
            Assert.IsTrue(player.IsDead);
            yield return WaitUntil(() => run.IsShowingResult, 4f);
            Assert.IsTrue(run.IsShowingResult, "쓰러지고 잠시 뒤 결과 화면");
            Assert.IsFalse(run.Result.Victory);
            StringAssert.Contains("죽림 2층", run.Result.Where);
            Assert.AreEqual(3 + 1 + 1, run.Result.Memory, "층 1 × 3 + 일반 5 × 0.1 (0.5) + 정예 1 → 4.5 → 5");

            var meta = MetaSave.Load();
            Assert.AreEqual(5, meta.memory);
            Assert.AreEqual(1, meta.runs);
            Assert.AreEqual(1, meta.bestFloors);
        }
    }
}
