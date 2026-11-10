using System;
using System.Collections;
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
    // 마교 살수 (패턴·경직 저항)와 추적도 (살수가 쫓아옴, 결투, 처치 보상, 객잔 절반).
    public class AssassinTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{System.Guid.NewGuid():N}.json"); // 진짜 저장을 건드리지 않게 (회차 0이라 마을 없이 시작)
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 777;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            if (MetaSave.PathOverride != null && System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            // 게임 시간과 실제 시간이 둘 다 지나야 포기해요 (느린 프레임은 deltaTime이 잘려 게임 시간이 늦게 흘러요). 실제 시간 5배가 상한.
            float end = Time.time + timeout, realEnd = Time.realtimeSinceStartup + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && (Time.time < end || Time.realtimeSinceStartup < realEnd) && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        IEnumerator LoadSandbox()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 살수는_3번_맞을_때마다_1번만_경직()
        {
            yield return LoadSandbox();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var assassin = director.Spawn(EnemyKind.Assassin, new Vector3(0f, 0f, 6f));
            Assert.AreEqual(160f, assassin.Damageable.MaxHealth);
            Assert.AreEqual(EnemyRank.Assassin, assassin.Rank);
            yield return null;

            assassin.Damageable.ApplyHit(1);
            Assert.IsFalse(assassin.IsStunned, "1번째");
            assassin.Damageable.ApplyHit(1);
            Assert.IsFalse(assassin.IsStunned, "2번째");
            assassin.Damageable.ApplyHit(1);
            Assert.IsTrue(assassin.IsStunned, "3번째에 경직");
        }

        // 10초 동안 풀어 두면 은신 습격(사라짐)이 한 번은 나오고, 플레이어는 맞아요. 오류 로그가 나오면 실패.
        [UnityTest]
        public IEnumerator 살수가_사라졌다_나타나_공격하고_오류가_없음()
        {
            yield return LoadSandbox();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var assassin = director.Spawn(EnemyKind.Assassin, new Vector3(0f, 0f, 3f));
            bool vanished = false;
            float end = Time.time + 10f;
            while (Time.time < end && !player.IsDead)
            {
                vanished |= assassin.IsHidden;
                yield return null;
            }
            Assert.IsTrue(vanished, "은신 습격 (6초마다)");
            Assert.Less(player.Health, player.MaxHealth, "공격이 들어와야 해요");
            Assert.IsFalse(assassin.IsHidden || !assassin.Damageable.Targetable && !assassin.IsDead && assassin.AttackPhase == EnemyAttackTimeline.Phase.Ready, "숨은 채로 남지 않아요");
        }

        [UnityTest]
        public IEnumerator 연막은_체력_50퍼센트_아래에서()
        {
            yield return LoadSandbox();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var assassin = (AssassinBrain)director.Spawn(EnemyKind.Assassin, new Vector3(0f, 0f, 6f));
            assassin.Damageable.ApplyHit(90); // 160 → 70
            yield return WaitUntil(() => assassin.IsSmoking, 6f);
            Assert.IsTrue(assassin.IsSmoking);
            Assert.IsNotNull(GameObject.Find("Smoke (연막)"));
        }

        // 추적도 5로 층에 들어가면 살수 1명. 방 2개를 지나면 시작 방에 나타나고, 시작 방으로 돌아가면 결투.
        [UnityTest]
        public IEnumerator 추적도_5면_살수가_쫓아와_결투하고_쓰러뜨리면_추적도_감소()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            run.SetTracking(5);
            run.ChooseFork(0);
            Assert.AreEqual(1, run.Hunters.Count, "추적도 5 → 살수 1명");
            Assert.IsNull(run.Hunters[0].Room, "아직 나타나지 않음");

            var map = run.Map;
            var fights = map.Rooms.Where(r => r.HasFight).Take(2).ToList();
            foreach (var room in fights)
            {
                run.Enter(room, 0);
                yield return WaitUntil(() =>
                {
                    for (int i = director.Enemies.Count - 1; i >= 0; i--) director.Enemies[i].Damageable.ApplyHit(99999);
                    return !run.IsFighting;
                }, 20f);
                Assert.IsFalse(run.IsFighting);
                if (arena.IsChoosing) arena.Choose(0);
            }
            Assert.AreEqual(7, run.Tracking, "전투 방 2개 → +2");
            Assert.AreEqual(map.Start, run.Hunters[0].Room, "방 2개를 지나면 시작 방에 나타나요");

            run.Enter(map.Start, 0);
            Assert.IsTrue(run.IsDuel, "비운 방(시작 방)에서 따라잡히면 결투");
            Assert.IsTrue(run.IsFighting, "문이 잠겨요");
            yield return WaitUntil(() => director.Enemies.Any(e => e.Kind == EnemyKind.Assassin), 3f);
            var assassin = director.Enemies.First(e => e.Kind == EnemyKind.Assassin);
            assassin.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => !run.IsFighting, 2f);
            Assert.IsFalse(run.IsDuel);
            Assert.AreEqual(5, run.Tracking, "살수 처치 −2");
            yield return WaitUntil(() => arena.IsChoosing, 2f);
            Assert.IsTrue(arena.IsChoosing, "살수 보상 기물");
        }

        [UnityTest]
        public IEnumerator 추적도_10이면_둘째_살수는_관문에서_기다리고_객잔에서_절반()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            run.SetTracking(10);
            run.ChooseFork(0);
            Assert.AreEqual(2, run.Hunters.Count);
            var gate = run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Gate);
            Assert.IsTrue(run.Hunters[1].AtGate);
            Assert.AreEqual(gate, run.Hunters[1].Room);

            run.Enter(gate, 0);
            Assert.IsTrue(run.IsDuel, "관문 방에 들어가면 결투");
            Assert.IsFalse(run.AtGate, "결투 중엔 못 나가요");
            yield return WaitUntil(() => director.Enemies.Any(e => e.Kind == EnemyKind.Assassin), 3f);
            director.Enemies.First(e => e.Kind == EnemyKind.Assassin).Damageable.ApplyHit(99999);
            yield return WaitUntil(() => !run.IsFighting, 2f);
            Assert.AreEqual(8, run.Tracking);

            run.SetTracking(9);
            run.EnterInn();
            Assert.AreEqual(4, run.Tracking, "객잔에 들르면 절반 (내림)");
        }
    }
}
