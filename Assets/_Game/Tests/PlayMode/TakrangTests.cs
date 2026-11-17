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
    // 장강 수채 고수 탁랑객: 물살 찌르기 · 물결 베기 · 탁류 보법 · 2페이즈 · 꺾으면 비급 탁랑참
    public class TakrangTests : InputTestFixture
    {
        string _savePath;
        Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _savePath = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 4343;
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
        public IEnumerator 장강_관문은_탁랑객_2페이즈_꺾으면_탁랑참()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(2);
            run.ChooseFork(0);
            run.EnterMaster();

            var master = run.Master as TakrangBrain;
            Assert.IsNotNull(master, "장강 수채 고수는 탁랑객");
            Assert.AreEqual(3300f, master.Damageable.MaxHealth, 1f, "1100 × 3.0");

            master.Damageable.ApplyHit(Mathf.CeilToInt(master.Damageable.MaxHealth * 0.55f));
            Assert.IsTrue(master.IsTransitioning, "50%에서 전환 (무적)");
            yield return WaitUntil(() => master.Phase == 2 && !master.IsTransitioning, 4f);
            Assert.AreEqual(2, master.Phase);

            master.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => run.IsInEvent, 3f);
            StringAssert.Contains("탁랑객", run.CurrentEvent.Title);
            Assert.IsTrue(run.ChooseEvent(0), "비급 전수");
            Assert.AreEqual(SkillKind.Takrang, player.Skill);
            Assert.AreEqual("탁랑참", player.SkillName);
        }

        // 멀리서만 싸우면 물결 베기, 5칸 밖에 1.5초 넘게 있으면 탁류 보법으로 옆에 붙어요
        [UnityTest]
        public IEnumerator 멀리_있으면_물결_베기와_탁류_보법()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var master = (TakrangBrain)director.Spawn(EnemyKind.Takrang, player.transform.position + new Vector3(0f, 0f, 4f));

            bool waves = false;
            var cc = player.GetComponent<CharacterController>();
            yield return WaitUntil(() =>
            {
                waves |= GameObject.Find("물결 검기") != null;
                Vector3 away = player.transform.position - master.transform.position;
                away.y = 0f;
                cc.enabled = false;
                player.transform.position = master.transform.position + (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.back) * 4f;
                cc.enabled = true;
                return waves;
            }, 6f);
            Assert.IsTrue(waves, "4칸이면 물결 베기");

            cc.enabled = false;
            player.transform.position = master.transform.position + new Vector3(0f, 0f, -9f);
            cc.enabled = true;
            bool flowed = false;
            yield return WaitUntil(() => (flowed |= master.AttackName == "탁류 보법") && Vector3.Distance(master.transform.position, player.transform.position) < 3f, 8f);
            Assert.IsTrue(flowed, "멀리 있으면 탁류 보법");
            Assert.Less(Vector3.Distance(master.transform.position, player.transform.position), 3f, "옆으로 돌아 붙어요");
        }

        // 가까우면 물살 찌르기: 앞 직선으로 찌르고 맞으면 뒤로 2칸 밀려나요
        [UnityTest]
        public IEnumerator 가까우면_물살_찌르기로_밀어냄()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var master = (TakrangBrain)director.Spawn(EnemyKind.Takrang, player.transform.position + new Vector3(0f, 0f, 1.8f));
            var cc = player.GetComponent<CharacterController>();

            // 1.8칸에 붙어 있다가 물살 찌르기 예고가 뜨면 그대로 서 있어요
            yield return WaitUntil(() =>
            {
                if (master.AttackName == "물살 찌르기") return true;
                Vector3 away = player.transform.position - master.transform.position;
                away.y = 0f;
                cc.enabled = false;
                player.transform.position = master.transform.position + (away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.back) * 1.8f;
                cc.enabled = true;
                return false;
            }, 6f);
            Assert.AreEqual("물살 찌르기", master.AttackName, "가까우면 물살 찌르기");

            float hp = player.Health;
            yield return WaitUntil(() => player.Health < hp, 2f);
            Assert.Less(player.Health, hp, "앞 직선 찌르기에 맞아요");
            float hitDistance = Vector3.Distance(master.transform.position, player.transform.position);
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(Vector3.Distance(master.transform.position, player.transform.position), hitDistance + 1.2f, "뒤로 밀려나요");
        }

        [UnityTest]
        public IEnumerator 탁랑참은_앞_직선의_적을_꿰뚫음()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            player.LearnSkill(SkillKind.Takrang);
            var near = director.Spawn(EnemyKind.Boar, player.transform.position + new Vector3(2.5f, 0f, 0f));
            var far = director.Spawn(EnemyKind.Boar, player.transform.position + new Vector3(5.5f, 0f, 0f));
            var behind = director.Spawn(EnemyKind.Boar, player.transform.position + new Vector3(-3f, 0f, 0f));
            yield return null;
            Move(_mouse.position, (Vector2)Camera.main.WorldToScreenPoint(far.transform.position));
            yield return null;

            Press(_mouse.rightButton);
            yield return null;
            Release(_mouse.rightButton);
            yield return new WaitForSeconds(0.5f);
            Assert.Less(near.Damageable.Health, near.Damageable.MaxHealth, "가까운 적");
            Assert.Less(far.Damageable.Health, far.Damageable.MaxHealth, "꿰뚫고 먼 적까지");
            Assert.AreEqual(behind.Damageable.MaxHealth, behind.Damageable.Health, "뒤쪽은 안 맞아요");
        }
    }
}
