using System;
using System.Collections;
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
    // 천마총 최종 보스 천마: 3페이즈 · 마기 손아귀 · 해체대법 안전 반경 · 격파하면 천마 격파 (회귀의 기억 · 유품)
    public class HeavenlyDemonTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
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

        IEnumerator Spawn(Action<HeavenlyDemonBrain, PlayerController> use, float distance = 4f)
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var boss = (HeavenlyDemonBrain)director.Spawn(EnemyKind.HeavenlyDemon, player.transform.position + new Vector3(0f, 0f, distance));
            boss.SetZone(player.transform.position, new Vector2(30f, 24f));
            use(boss, player);
        }

        [UnityTest]
        public IEnumerator 세_페이즈와_해체대법_안전_반경()
        {
            HeavenlyDemonBrain boss = null;
            PlayerController player = null;
            yield return Spawn((b, p) => { boss = b; player = p; });
            Assert.AreEqual("천마", boss.DisplayName);
            Assert.AreEqual(EnemyRank.Boss, boss.Rank);
            Assert.AreEqual(1, boss.Phase);

            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.36f));
            Assert.IsTrue(boss.IsTransitioning, "66%에서 전환 (무적)");
            yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);
            Assert.AreEqual(2, boss.Phase, "천마군림");
            Assert.AreEqual(0f, boss.SafeRadius, "마기는 3페이즈부터");

            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.34f));
            Assert.IsTrue(boss.IsTransitioning, "33%에서 전환");
            yield return WaitUntil(() => boss.Phase == 3 && !boss.IsTransitioning, 4f);
            Assert.AreEqual(3, boss.Phase, "해체대법");
            Assert.AreEqual(11f, boss.SafeRadius, 0.01f, "안전 반경 11칸에서 시작");
            yield return WaitUntil(() => boss.SafeRadius < 11f, 4f);
            Assert.Less(boss.SafeRadius, 11f, "3초마다 좁아져요");

            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = player.transform.position + new Vector3(13f, 0f, 0f);
            cc.enabled = true;
            yield return null;
            Assert.IsTrue(boss.PlayerInMiasma, "안전 반경 밖");
            float hp = player.Health;
            yield return WaitUntil(() => player.Health < hp, 2f);
            Assert.Less(player.Health, hp, "마기 속에 있으면 아파요");
        }

        [UnityTest]
        public IEnumerator 멀리_있으면_마기_손아귀로_끌어당김()
        {
            HeavenlyDemonBrain boss = null;
            PlayerController player = null;
            yield return Spawn((b, p) => { boss = b; player = p; }, 9f);
            bool grasped = false;
            float closest = float.MaxValue;
            var cc = player.GetComponent<CharacterController>();
            yield return WaitUntil(() =>
            {
                player.Heal(9999f);
                grasped |= boss.GraspedPlayer;
                if (!grasped && !player.IsInvulnerable)
                {
                    cc.enabled = false;
                    player.transform.position = boss.transform.position + new Vector3(0f, 0f, -9f);
                    cc.enabled = true;
                }
                if (grasped) closest = Mathf.Min(closest, Vector3.Distance(player.transform.position, boss.transform.position));
                return grasped && closest < 4f;
            }, 15f);
            Assert.IsTrue(grasped, "멀리 있으면 붙잡혀요");
            Assert.Less(closest, 4f, "천마 앞으로 끌려와요");
        }

        // 천마총 보스를 쓰러뜨리면 판 끝 · 천마 격파 (결과 화면 · 회귀의 기억 · 유품 가지)
        [UnityTest]
        public IEnumerator 천마를_쓰러뜨리면_천마_격파()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(3);
            run.ChooseFork(0);
            run.EnterRest();
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(60f, 0f, 6.6f);
            cc.enabled = true;
            yield return WaitUntil(() => arena.Boss != null && arena.Boss.IsAwake, 3f);
            Assert.IsInstanceOf<HeavenlyDemonBrain>(arena.Boss, "천마총 보스는 천마");
            Assert.AreEqual(14175f, arena.Boss.Damageable.MaxHealth, 1f, "3150 × 4.5");
            arena.Boss.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => run.IsDone, 4f);
            Assert.IsTrue(run.IsDone, "판 끝");
            run.ShowResult(true);
            Assert.AreEqual("천마 격파", run.Result.Where);
            Assert.AreEqual(1, MetaSave.Load().finalBossKills, "유품 가지가 열려요");
        }
    }
}
