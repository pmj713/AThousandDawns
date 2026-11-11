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
    // 폐사찰 보스 혈불: 바닥 예고 원으로 내려찍고, 목탁승을 부르고, 체력 절반에서 2페이즈
    public class BloodBuddhaTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            PrototypeArena.AutoSpawn = false;
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        [UnityTest]
        public IEnumerator 내려찍고_목탁승을_부르고_2페이즈()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var boss = (BloodBuddhaBrain)director.Spawn(EnemyKind.BloodBuddha, player.transform.position + new Vector3(0f, 0f, 5f));
            boss.SetZone(player.transform.position, new Vector2(16f, 12f));
            Assert.AreEqual("혈불", boss.DisplayName);
            Assert.AreEqual(1850f, boss.Damageable.MaxHealth, 0.01f);
            Assert.AreEqual(EnemyRank.Boss, boss.Rank);

            float hp = player.Health;
            yield return WaitUntil(() => player.Health < hp, 8f);
            Assert.Less(player.Health, hp, "가만히 있으면 예고 원에 맞아요");

            yield return WaitUntil(() => director.Count(EnemyKind.Moktak) > 0, 12f);
            Assert.AreEqual(1, director.Count(EnemyKind.Moktak), "목탁승 한 명");

            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            Assert.IsTrue(boss.IsTransitioning);
            yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);
            Assert.AreEqual(2, boss.Phase);
        }
        // 2페이즈: 6칸 밖에서만 싸우면 혈수로 끌어당겨요
        [UnityTest]
        public IEnumerator 이페이즈_멀리_있으면_혈수로_끌어당김()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var boss = (BloodBuddhaBrain)director.Spawn(EnemyKind.BloodBuddha, player.transform.position + new Vector3(0f, 0f, 8f));
            boss.SetZone(player.transform.position, new Vector2(30f, 24f));
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);

            bool grabbed = false;
            float closest = float.MaxValue;
            var cc = player.GetComponent<CharacterController>();
            yield return WaitUntil(() =>
            {
                grabbed |= boss.GrabbedPlayer;
                if (!grabbed && !player.IsInvulnerable) // 끌려오기 전까지는 8칸 거리를 지켜요
                {
                    cc.enabled = false;
                    player.transform.position = boss.transform.position + new Vector3(0f, 0f, -8f);
                    cc.enabled = true;
                }
                if (grabbed) closest = Mathf.Min(closest, Vector3.Distance(player.transform.position, boss.transform.position));
                return grabbed && closest < 4f;
            }, 12f);
            Assert.IsTrue(grabbed, "멀리 있으면 혈수에 맞아요");
            Assert.Less(closest, 4f, "혈불 앞으로 끌려와요");
        }
    }
}
