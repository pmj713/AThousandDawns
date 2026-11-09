using System;
using System.Collections;
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
    // 판 구조 씬: 방에 들어가면 문이 잠기고, 웨이브를 다 쓰러뜨리면 문이 열리고 기물 고르기, 문으로 나가면 다음 방.
    public class RoomRunTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 12345;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            Time.timeScale = 1f;
            base.TearDown();
        }

        // 시간이 멈춰도 (기물 고르기) 끝나도록 실제 시간으로 기다려요
        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator 방을_비우면_문이_열리고_기물을_고른_뒤_다음_방()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(run);
            Assert.AreEqual(0, run.StepIndex);
            Assert.AreEqual(1, run.Current.Floor);
            Assert.That(run.WaveCount, Is.InRange(2, 3));
            var door = GameObject.Find("Door (결계)");
            Assert.IsNotNull(door, "들어오면 문이 잠겨요");

            // 나오는 대로 쓰러뜨려요 (플레이어는 입구에 가만히)
            int maxSeen = 0;
            yield return WaitUntil(() =>
            {
                maxSeen = Mathf.Max(maxSeen, director.Enemies.Count);
                for (int i = director.Enemies.Count - 1; i >= 0; i--)
                    director.Enemies[i].Damageable.ApplyHit(99999);
                return run.DoorOpen;
            }, 20f);
            Assert.IsTrue(run.DoorOpen, "웨이브를 다 비우면 문이 열려요");
            Assert.Greater(maxSeen, 0, "적이 나왔어야 해요");
            Assert.IsFalse(door.activeSelf);

            yield return WaitUntil(() => arena.IsChoosing, 2f);
            Assert.IsTrue(arena.IsChoosing, $"방을 비우면 기물 3개 중 하나 (남은 고르기 {arena.PendingOffers}, 체력 {player.Health}, 기물 DB {AThousandDawns.Core.GameConfig.Items != null}, 적 {director.Enemies.Count})");
            arena.Choose(0);
            Assert.AreEqual(1, player.Items.Items.Count);

            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(0f, 0f, 7.6f); // 북쪽 문 너머 통로
            cc.enabled = true;
            yield return WaitUntil(() => run.StepIndex == 1, 2f);
            Assert.AreEqual(1, run.StepIndex);
            Assert.AreEqual(2, run.Current.Room);
            Assert.IsTrue(door.activeSelf, "다음 방에 들어가면 다시 잠겨요");
            Assert.Less(player.transform.position.z, -3f, "남쪽 입구로 옮겨져요");
        }

        // 정비실 → 녹림왕 방. 녹림왕을 쓰러뜨리면 기물 하나와 경맥도 6×4.
        [UnityTest]
        public IEnumerator 정비실을_지나_녹림왕을_쓰러뜨리면_경맥도가_넓어짐()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var player = Object.FindFirstObjectByType<PlayerController>();

            run.Enter(run.Steps.Count - 2);
            Assert.AreEqual(RoomRun.StepKind.Rest, run.Current.Kind);
            Assert.IsTrue(run.DoorOpen, "정비실 문은 처음부터 열려 있어요");
            player.FillGauge();

            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(60f, 0f, 6.6f);
            cc.enabled = true;
            yield return WaitUntil(() => run.Current.Kind == RoomRun.StepKind.Boss, 2f);
            Assert.AreEqual(RoomRun.StepKind.Boss, run.Current.Kind);
            Assert.AreEqual(0f, player.Gauge, "정비실에서 채운 기세는 나갈 때 들어올 때 값으로");

            yield return WaitUntil(() => arena.Boss != null && arena.Boss.IsAwake, 2f);
            Assert.IsTrue(arena.Boss.IsAwake, "들어오면 바로 결투");
            var boss = arena.Boss;
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);
            boss.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => run.IsDone, 3f);
            Assert.IsTrue(run.IsDone);
            Assert.AreEqual(6, player.Items.Board.Width);
            yield return WaitUntil(() => arena.IsChoosing, 2f);
            Assert.IsTrue(arena.IsChoosing, "녹림왕 보상 기물");
        }
    }
}
