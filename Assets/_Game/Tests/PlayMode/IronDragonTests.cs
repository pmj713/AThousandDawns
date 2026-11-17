using System;
using System.Collections;
using System.Collections.Generic;
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

        // 멀면 닻 충격파: 제자리에서 고리 3개가 퍼지고, 고리 띠에 걸리면 맞아요
        [UnityTest]
        public IEnumerator 멀면_닻_충격파_고리_셋()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var boss = (IronDragonBrain)director.Spawn(EnemyKind.IronDragon, player.transform.position + new Vector3(0f, 0f, 6f));
            boss.SetZone(player.transform.position, new Vector2(30f, 24f));

            int rings = 0, slams = 0, maxActive = 0;
            Vector3 ringAt = Vector3.zero;
            Action<IronDragonBrain, IronDragonBrain.FxCue> onCue = (b, c) =>
            {
                if (c.Kind == IronDragonBrain.FxKind.Ring) { rings++; ringAt = c.At; }
                if (c.Kind == IronDragonBrain.FxKind.Slam) slams++;
            };
            IronDragonBrain.Cue += onCue;
            try
            {
                bool hurt = false, named = false;
                var cc = player.GetComponent<CharacterController>();
                yield return WaitUntil(() =>
                {
                    if (player.Health < player.MaxHealth * 0.5f) player.Heal(9999f);
                    hurt |= rings > 0 && player.Health < player.MaxHealth;
                    named |= boss.AttackName == "닻 충격파";
                    maxActive = Mathf.Max(maxActive, boss.ActiveRings);
                    if (!player.IsInvulnerable) // 6칸 거리를 지켜요 (휘두르기 · 사슬 당기기 밖)
                    {
                        cc.enabled = false;
                        player.transform.position = boss.transform.position + new Vector3(0f, 0f, -6f);
                        cc.enabled = true;
                    }
                    return rings >= 3 && hurt && boss.ActiveRings == 0;
                }, 10f);
                Assert.IsTrue(named, "멀면 닻 충격파");
                Assert.GreaterOrEqual(rings, 3, "고리 셋");
                Assert.AreEqual(3, maxActive, "1페이즈는 고리 3개가 함께 퍼져요");
                Assert.IsTrue(hurt, "고리 띠에 걸리면 맞아요");
                Assert.AreEqual(0, slams, "유성추 내려찍기는 없어졌어요");
                Assert.Less(Vector3.Distance(new Vector3(ringAt.x, 0f, ringAt.z), new Vector3(boss.transform.position.x, 0f, boss.transform.position.z)), 2f, "고리는 철룡 자리 (닻을 꽂은 앞) 에서 퍼져요");
            }
            finally
            {
                IronDragonBrain.Cue -= onCue;
            }
        }

        // 2페이즈 물기둥 줄: 철룡에서 플레이어 쪽 곧은 줄에 물기둥 7개, 1.5칸 간격, 0.18초씩 늦게 솟아요
        [UnityTest]
        public IEnumerator 이페이즈_물기둥_줄()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var boss = (IronDragonBrain)director.Spawn(EnemyKind.IronDragon, player.transform.position + new Vector3(0f, 0f, 4f));
            boss.SetZone(player.transform.position, new Vector2(30f, 24f));
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);

            var geysers = new List<IronDragonBrain.FxCue>();
            Action<IronDragonBrain, IronDragonBrain.FxCue> onCue = (b, c) => { if (c.Kind == IronDragonBrain.FxKind.Geyser) geysers.Add(c); };
            IronDragonBrain.Cue += onCue;
            try
            {
                var cc = player.GetComponent<CharacterController>();
                yield return WaitUntil(() =>
                {
                    player.Heal(9999f);
                    if (!player.IsInvulnerable)
                    {
                        cc.enabled = false;
                        player.transform.position = boss.transform.position + new Vector3(0f, 0f, -5f);
                        cc.enabled = true;
                    }
                    return geysers.Count >= 7;
                }, 15f);
                Assert.GreaterOrEqual(geysers.Count, 7, "물기둥 7개");
                Vector3 dir = (geysers[6].At - geysers[0].At).normalized;
                for (int i = 1; i < 7; i++)
                {
                    Vector3 step = geysers[i].At - geysers[i - 1].At;
                    Assert.AreEqual(1.5f, step.magnitude, 0.05f, "1.5칸 간격");
                    Assert.Greater(Vector3.Dot(step.normalized, dir), 0.99f, "곧은 줄");
                    Assert.AreEqual(0.18f, geysers[i].Time - geysers[i - 1].Time, 0.01f, "차례로 솟아요");
                    Assert.AreEqual(1.1f, geysers[i].Radius, 0.01f);
                }
                Assert.AreEqual(0.8f, geysers[0].Time, 0.01f, "첫 물기둥 예고 0.8초");
            }
            finally
            {
                IronDragonBrain.Cue -= onCue;
            }
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
