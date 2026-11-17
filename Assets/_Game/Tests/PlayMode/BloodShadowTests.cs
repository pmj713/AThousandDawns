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
    // 천마총 고수 혈영검마: 잔상 · 흡혈 · 혈영보 · 2페이즈 · 꺾으면 비급 혈영참 (검 · 비도)
    public class BloodShadowTests : InputTestFixture
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
            RoomRun.SeedOverride = 4545;
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

        IEnumerator LoadCombat()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 천마총_관문은_혈영검마_2페이즈_꺾으면_혈영참()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(3);
            run.ChooseFork(0);
            run.EnterMaster();

            var master = run.Master as BloodShadowBrain;
            Assert.IsNotNull(master, "천마총 고수는 혈영검마");
            Assert.AreEqual(4950f, master.Damageable.MaxHealth, 1f, "1100 × 4.5");

            master.Damageable.ApplyHit(Mathf.CeilToInt(master.Damageable.MaxHealth * 0.55f));
            Assert.IsTrue(master.IsTransitioning, "50%에서 전환 (무적)");
            yield return WaitUntil(() => master.Phase == 2 && !master.IsTransitioning, 4f);
            Assert.AreEqual(2, master.Phase);

            master.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => run.IsInEvent, 3f);
            StringAssert.Contains("혈영검마", run.CurrentEvent.Title);
            Assert.IsTrue(run.ChooseEvent(0), "비급 전수");
            Assert.AreEqual(SkillKind.BloodShadow, player.Skill);
            Assert.AreEqual("혈영참", player.SkillName);
        }

        // 가까우면 혈영 베기 → 잔상이 다시 베고, 맞히면 흡혈. 멀리 있으면 혈영보로 뒤에 붙어요
        [UnityTest]
        public IEnumerator 잔상과_흡혈_혈영보()
        {
            yield return LoadCombat();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var master = (BloodShadowBrain)director.Spawn(EnemyKind.BloodShadow, player.transform.position + new Vector3(0f, 0f, 2f));
            master.Damageable.ApplyHit(200); // 흡혈로 차오르는지 보려고
            float hurt = master.Damageable.Health;

            bool echoed = false;
            yield return WaitUntil(() =>
            {
                player.Heal(9999f);
                echoed |= master.EchoesPending > 0;
                return echoed && master.Damageable.Health > hurt;
            }, 10f);
            Assert.IsTrue(echoed, "베고 나면 잔상이 남아요");
            Assert.Greater(master.Damageable.Health, hurt, "맞히면 흡혈");

            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = master.transform.position + new Vector3(0f, 0f, -9f);
            cc.enabled = true;
            bool blinked = false;
            yield return WaitUntil(() =>
            {
                player.Heal(9999f);
                blinked |= master.AttackName == "혈영보";
                return blinked && Vector3.Distance(master.transform.position, player.transform.position) < 3f;
            }, 12f); // 혈조가 먼저 나오고, 혈조 쿨 동안 멀리 버티면 혈영보
            Assert.IsTrue(blinked, "멀리 있으면 혈영보");
            Assert.Less(Vector3.Distance(master.transform.position, player.transform.position), 3f, "뒤로 순간이동해 붙어요");
        }

        // 4~10칸이면 혈조: 플레이어 쪽 직선으로 피 가시가 차례로 솟아요 (자리마다 아트 신호, 한 줄에 한 번만 맞아요)
        [UnityTest]
        public IEnumerator 혈조는_직선으로_가시가_차례로_솟음()
        {
            yield return LoadCombat();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var master = (BloodShadowBrain)director.Spawn(EnemyKind.BloodShadow, player.transform.position + new Vector3(0f, 0f, 6f)); // 깨어나는 거리 (7칸) 안, 혈조 거리 (4~10칸) 안

            int starts = 0, spikes = 0;
            void OnCue(BloodShadowBrain b, BloodShadowBrain.FxCue c)
            {
                if (b != master) return;
                if (c.Kind == BloodShadowBrain.FxKind.SpikeStart) starts++;
                if (c.Kind == BloodShadowBrain.FxKind.Spike) spikes++;
            }
            BloodShadowBrain.Cue += OnCue;
            try
            {
                bool named = false;
                var cc = player.GetComponent<CharacterController>();
                yield return WaitUntil(() =>
                {
                    named |= master.AttackName == "혈조";
                    if (master.AttackName == null) // 고르기 전까지는 6.5칸을 지켜요 (걸어와 붙으면 혈영 베기라서)
                    {
                        cc.enabled = false;
                        player.transform.position = master.transform.position + new Vector3(0f, 0f, -6.5f);
                        cc.enabled = true;
                    }
                    return starts > 0;
                }, 4f);
                Assert.IsTrue(named, "먼 거리 대응은 혈조");
                Assert.AreEqual(1, starts);
                Assert.Greater(master.SpikesPending, 0, "가시가 한꺼번에가 아니라 차례로 솟아요");
                float hp = player.Health;
                yield return WaitUntil(() => master.SpikesPending == 0, 2f);
                Assert.AreEqual(9, spikes, "9칸 / 1칸마다 한 자리");
                Assert.Less(player.Health, hp, "직선 위에 서 있으면 맞아요");
            }
            finally
            {
                BloodShadowBrain.Cue -= OnCue;
            }
        }

        [UnityTest]
        public IEnumerator 혈영참은_돌진해_베고_잔상이_다시_벰()
        {
            yield return LoadCombat();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            player.LearnSkill(SkillKind.BloodShadow);
            var near = director.Spawn(EnemyKind.Boar, player.transform.position + new Vector3(2f, 0f, 0f));
            near.Damageable.ScaleMaxHealth(10f);
            yield return null;
            Move(_mouse.position, (Vector2)Camera.main.WorldToScreenPoint(near.transform.position));
            yield return null;
            Vector3 start = player.transform.position;
            float hp = near.Damageable.Health;

            Press(_mouse.rightButton);
            yield return null;
            Release(_mouse.rightButton);
            yield return WaitUntil(() => player.ShadowEchoesPending > 0, 1f);
            Assert.AreEqual(1, player.ShadowEchoesPending, "잔상이 남아요");
            Assert.Greater(Vector3.Distance(start, player.transform.position), 1f, "앞으로 돌진해요");
            float afterDash = near.Damageable.Health;
            Assert.Less(afterDash, hp, "돌진 베기");
            yield return WaitUntil(() => player.ShadowEchoesPending == 0, 1f);
            Assert.Less(near.Damageable.Health, afterDash, "잔상이 같은 길을 다시 베요");
        }

        [UnityTest]
        public IEnumerator 비도_혈영참은_비도_자리마다_잔상()
        {
            yield return LoadCombat();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            player.SetWeapon(WeaponKind.Dagger);
            player.LearnSkill(SkillKind.BloodShadow);
            StringAssert.StartsWith("혈영 잔상", player.SkillName);
            for (int i = 0; i < 3; i++) player.ThrowDaggers(1, HitDamageInput.Of(1f), HitTag.Basic, 0f); // 앞에 적이 없어 바닥에 떨어져요
            yield return WaitUntil(() => player.DaggerCount == 3, 2f);

            Press(_mouse.rightButton);
            yield return null;
            Release(_mouse.rightButton);
            yield return WaitUntil(() => player.ShadowEchoesMade > 0, 2f);
            Assert.AreEqual(3, player.ShadowEchoesMade, "비도마다 잔상");
            Assert.AreEqual(0, player.DaggerCount, "비도는 손으로 돌아와요");
        }
    }
}
