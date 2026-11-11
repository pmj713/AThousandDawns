using System;
using System.Collections;
using AThousandDawns.Combat;
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
    // 폐사찰 2층 관문 고수 파계승 혜광: 금강불괴 · 2페이즈 · 꺾으면 고수 보상
    public class HyegwangTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 4242;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
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

        [UnityTest]
        public IEnumerator 폐사찰_관문은_혜광_금강불괴와_2페이즈()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(1);
            run.ChooseFork(0);
            run.EnterMaster();

            var master = run.Master as HyegwangBrain;
            Assert.IsNotNull(master, "폐사찰 고수는 혜광");
            Assert.AreEqual(1980f, master.Damageable.MaxHealth, 1f, "1100 × 1.8");

            yield return WaitUntil(() => master.IsVajra, 12f);
            Assert.IsTrue(master.IsVajra, "가까우면 금강불괴");
            Assert.AreEqual(0f, master.Damageable.ResolveSituational(player.transform.position, HitTag.Basic), "버티는 동안 최소 피해");
            yield return WaitUntil(() => !master.IsVajra, 4f);
            Assert.AreEqual(1f, master.Damageable.ResolveSituational(player.transform.position, HitTag.Basic));

            master.Damageable.ApplyHit(Mathf.CeilToInt(master.Damageable.MaxHealth * 0.55f));
            yield return WaitUntil(() => master.Phase == 2 && !master.IsTransitioning, 4f);
            Assert.AreEqual(2, master.Phase);

            // 2페이즈 금강불괴 강화: 버티는 동안 맞을 때마다 충격파가 커져요 (최대 +2칸)
            yield return WaitUntil(() => { player.Heal(9999f); return master.IsVajra; }, 12f);
            Assert.IsTrue(master.IsVajra, "2페이즈에도 금강불괴");
            float radius = master.VajraRadius;
            for (int i = 0; i < 10; i++) master.Damageable.ApplyHit(1);
            Assert.AreEqual(radius + 2f, master.VajraRadius, 0.01f, "맞을수록 커지고 최대 +2칸");
            yield return WaitUntil(() => !master.IsVajra, 2f);
            Assert.IsFalse(master.IsVajra, "2페이즈는 1초 만에 터져요");
            Assert.AreEqual(radius, master.VajraRadius, 0.01f, "터지면 원래대로");

            master.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => run.IsInEvent, 3f);
            Assert.AreEqual("master", run.CurrentEvent.Id);
            StringAssert.Contains("혜광", run.CurrentEvent.Title);
            Assert.AreEqual(3, run.CurrentEvent.Choices.Count, "비급 · 흡성 · 방면");
            Assert.IsTrue(run.ChooseEvent(0), "비급 전수");
            Assert.AreEqual(SkillKind.Palm, player.Skill, "혜광의 비급은 금강장");
            Assert.AreEqual("금강장", player.SkillName);
        }

        // 멀리서만 싸우면: 3칸 밖이면 염주 세 갈래, 5칸 밖에 2초 넘게 있으면 축지로 순식간에 다가와요
        [UnityTest]
        public IEnumerator 멀리_있으면_염주_세_갈래와_축지()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var master = (HyegwangBrain)director.Spawn(EnemyKind.Hyegwang, player.transform.position + new Vector3(0f, 0f, 4f));
            master.SetZone(Vector3.zero, new Vector2(40f, 24f));

            int beads = 0;
            var keep = player.GetComponent<CharacterController>();
            yield return WaitUntil(() =>
            {
                // 4칸 거리를 지켜요 (첫 공격 대기 동안 다가오니까)
                Vector3 away = player.transform.position - master.transform.position;
                away.y = 0f;
                keep.enabled = false;
                player.transform.position = master.transform.position + (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.back) * 4f;
                keep.enabled = true;
                return (beads = Object.FindObjectsByType<BeadThrow>(FindObjectsSortMode.None).Length) > 0;
            }, 6f);
            Assert.AreEqual(3, beads, "4칸 거리에서도 염주 세 갈래");

            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = master.transform.position + new Vector3(0f, 0f, -9f);
            cc.enabled = true;
            bool rushed = false;
            yield return WaitUntil(() => (rushed |= master.AttackName == "축지") && Vector3.Distance(master.transform.position, player.transform.position) < 3f, 6f);
            Assert.IsTrue(rushed, "9칸 떨어져 있으면 축지");
            Assert.Less(Vector3.Distance(master.transform.position, player.transform.position), 3f, "순식간에 다가와요");
        }
    }
}
