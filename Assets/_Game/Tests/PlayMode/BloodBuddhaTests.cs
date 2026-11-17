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
    // 폐사찰 보스 혈불: 바닥 예고 원으로 내려찍고, 목탁승을 부르고, 체력 절반에서 2페이즈 (혈해 웅덩이)
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
        // 2페이즈: 혈해 — 발밑에 피 웅덩이, 혈불은 안에서 회복하고 플레이어는 안에서 깎여요
        [UnityTest]
        public IEnumerator 이페이즈_혈해_웅덩이에서_회복하고_플레이어를_깎음()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var boss = (BloodBuddhaBrain)director.Spawn(EnemyKind.BloodBuddha, player.transform.position + new Vector3(0f, 0f, 5f));
            boss.SetZone(player.transform.position, new Vector2(30f, 24f));
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);
            Assert.AreEqual(2, boss.Phase);

            var cc = player.GetComponent<CharacterController>();
            void Place(Vector3 at)
            {
                cc.enabled = false;
                player.transform.position = at;
                cc.enabled = true;
            }

            bool pullCue = false;
            Action<BloodBuddhaBrain, BloodBuddhaBrain.FxCue> onCue = (b, c) => pullCue |= c.Kind == BloodBuddhaBrain.FxKind.Grab || c.Kind == BloodBuddhaBrain.FxKind.GrabStart;
            BloodBuddhaBrain.Cue += onCue;
            try
            {
                // 혈해가 깔릴 때까지 가까이 (6초 + 예고)
                yield return WaitUntil(() =>
                {
                    player.Heal(9999f);
                    Place(boss.transform.position + new Vector3(0f, 0f, -2f));
                    return boss.PoolCount > 0;
                }, 12f);
                Assert.AreEqual(1, boss.PoolCount, "혈해 웅덩이 하나");
                Assert.AreEqual("혈해", boss.AttackName);
                var pool = GameObject.Find("혈해");
                Assert.IsNotNull(pool);
                Vector3 center = new Vector3(pool.transform.position.x, player.transform.position.y, pool.transform.position.z);

                player.Heal(9999f);
                float bossHp = boss.Damageable.Health, playerHp = player.Health;
                bool bossIn = false, playerIn = false;
                yield return WaitUntil(() =>
                {
                    Place(center + new Vector3(0f, 0f, -1.5f)); // 웅덩이 안 (반경 3칸)
                    bossIn |= boss.BossInPool;
                    playerIn |= boss.PlayerInPool;
                    return false;
                }, 1.5f);
                Assert.IsTrue(playerIn, "플레이어가 웅덩이 안");
                Assert.IsTrue(bossIn, "혈불이 웅덩이 안");
                Assert.Greater(boss.Damageable.Health, bossHp, "혈불은 웅덩이 안에서 회복해요");
                Assert.Less(player.Health, playerHp, "플레이어는 웅덩이 안에서 깎여요");
                Assert.IsFalse(pullCue, "혈수(끌어당기기)는 없어졌어요");

                yield return WaitUntil(() => { player.Heal(9999f); return boss.PoolCount == 0; }, 6f);
                Assert.AreEqual(0, boss.PoolCount, "혈해는 6초 뒤 사라져요");
            }
            finally
            {
                BloodBuddhaBrain.Cue -= onCue;
            }
        }
    }
}
