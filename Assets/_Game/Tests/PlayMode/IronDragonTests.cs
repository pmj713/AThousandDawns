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
    // 장강 수채 보스 수채주 철룡: 사슬 당기기 · 수하 부르기 · 2페이즈 물길
    public class IronDragonTests : InputTestFixture
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

        // 7칸 밖에서만 싸우면 사슬로 끌어당겨요
        [UnityTest]
        public IEnumerator 멀리_있으면_사슬로_끌어당김()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var boss = (IronDragonBrain)director.Spawn(EnemyKind.IronDragon, player.transform.position + new Vector3(0f, 0f, 9f));
            boss.SetZone(player.transform.position, new Vector2(30f, 24f));
            Assert.AreEqual("수채주 철룡", boss.DisplayName);
            Assert.AreEqual(EnemyRank.Boss, boss.Rank);

            bool hooked = false;
            float closest = float.MaxValue;
            var cc = player.GetComponent<CharacterController>();
            yield return WaitUntil(() =>
            {
                player.Heal(9999f);
                hooked |= boss.HookedPlayer;
                if (!hooked && !player.IsInvulnerable) // 끌려오기 전까지는 9칸 거리를 지켜요
                {
                    cc.enabled = false;
                    player.transform.position = boss.transform.position + new Vector3(0f, 0f, -9f);
                    cc.enabled = true;
                }
                if (hooked) closest = Mathf.Min(closest, Vector3.Distance(player.transform.position, boss.transform.position));
                return hooked && closest < 4f;
            }, 15f);
            Assert.IsTrue(hooked, "멀리 있으면 사슬에 걸려요");
            Assert.Less(closest, 4f, "철룡 앞으로 끌려와요");
        }

        [UnityTest]
        public IEnumerator 수하를_부르고_2페이즈엔_물길이_느리게()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var boss = (IronDragonBrain)director.Spawn(EnemyKind.IronDragon, player.transform.position + new Vector3(0f, 0f, 4f));
            boss.SetZone(player.transform.position, new Vector2(24f, 18f));

            yield return WaitUntil(() => { player.Heal(9999f); return director.Count(EnemyKind.FlagBearer) > 0; }, 12f);
            Assert.AreEqual(1, director.Count(EnemyKind.FlagBearer), "깃발수 하나");
            Assert.AreEqual(2, director.Count(EnemyKind.Pirate), "수적 졸개 둘");

            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            Assert.IsTrue(boss.IsTransitioning, "50%에서 전환 (무적)");
            yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);
            Assert.AreEqual(3, boss.WaterCount, "물길 3곳");

            var water = GameObject.Find("물길");
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(water.transform.position.x, 0f, water.transform.position.z);
            cc.enabled = true;
            yield return null;
            yield return null;
            Assert.IsTrue(boss.PlayerInWater);
            Assert.Greater(player.SlowLeft, 0f, "물길 안에선 느려져요");
        }
    }
}
