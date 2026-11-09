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
    // 고수 청죽객: 2층 관문 뒤 결투장, 50%에서 2페이즈, 쓰러뜨리면 비급 전수 / 흡성 / 방면 → 3층 갈림길.
    public class MasterTests : InputTestFixture
    {
        Mouse _mouse;
        RoomRun _run;
        PlayerController _player;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 2024;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        // 1층 → 2층 → 2층 관문 → 고수. 쓰러뜨리고 선택 화면까지.
        IEnumerator BeatMaster()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _player = Object.FindFirstObjectByType<PlayerController>();
            _run.ChooseFork(0);
            _run.LeaveFloor();
            Assert.AreEqual(2, _run.Floor, "1층 관문 → 2층 갈림길 (고수는 2층 관문)");
            _run.ChooseFork(0);
            _run.LeaveFloor();
            Assert.AreEqual(RoomRun.Phase.Master, _run.CurrentPhase, "2층 관문 → 고수 결투장");
            var master = _run.Master;
            Assert.IsNotNull(master);
            Assert.AreEqual(1100f, master.Damageable.MaxHealth);
            Assert.AreEqual(EnemyRank.Master, master.Rank);

            master.Damageable.ApplyHit(Mathf.CeilToInt(master.Damageable.MaxHealth * 0.55f));
            Assert.IsTrue(master.IsTransitioning, "50%에서 전환 (무적)");
            Assert.IsFalse(master.Damageable.Targetable);
            yield return WaitUntil(() => master.Phase == 2 && !master.IsTransitioning, 4f);
            Assert.AreEqual(2, master.Phase);
            master.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => _run.IsInEvent, 3f);
            Assert.IsTrue(_run.IsInEvent, "쓰러뜨리면 셋 중 하나");
            Assert.AreEqual(3, _run.CurrentEvent.Choices.Count);
        }

        [UnityTest]
        public IEnumerator 비급_전수를_고르면_초식이_죽림섬으로_바뀌고_3층_갈림길()
        {
            yield return BeatMaster();
            Assert.IsTrue(_run.ChooseEvent(0));
            Assert.AreEqual(SkillKind.BambooFlash, _player.Skill);
            _run.CloseEvent();
            Assert.IsTrue(_run.IsForking);
            Assert.AreEqual(3, _run.Floor);
            Assert.IsTrue(_run.MasterDone);
        }

        [UnityTest]
        public IEnumerator 흡성은_공력_1_오르고_기물_최대_기_1_내려감()
        {
            yield return BeatMaster();
            int power = _player.Gongryeok;
            Assert.IsTrue(_run.ChooseEvent(1));
            Assert.AreEqual(power + 1, _player.Gongryeok);
            Assert.AreEqual(-1, _player.Items.GlobalMaxQiBonus);
            _run.CloseEvent();
        }

        [UnityTest]
        public IEnumerator 방면은_추적도_0_은자_50()
        {
            yield return BeatMaster();
            _run.SetTracking(7);
            int silver = _run.Silver;
            Assert.IsTrue(_run.ChooseEvent(2));
            Assert.AreEqual(0, _run.Tracking);
            Assert.AreEqual(silver + 50, _run.Silver);
            _run.CloseEvent();
        }

        // 결투장 밖(샌드박스)에서 8초 풀어 둬요. 오류 로그가 나오면 실패, 플레이어는 맞아야 해요.
        [UnityTest]
        public IEnumerator 청죽객이_오류_없이_싸움()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var master = (QingzhuBrain)director.Spawn(EnemyKind.Qingzhu, new Vector3(0f, 0f, 1f));
            master.SetZone(new Vector3(0f, 0f, -2f), new Vector2(30f, 20f));
            float end = Time.time + 12f;
            while (Time.time < end && !player.IsDead) yield return null;
            Assert.Less(player.Health, player.MaxHealth, "공격이 들어와야 해요");
        }

        [UnityTest]
        public IEnumerator 죽림섬은_가까운_적에게_순간이동하며_벰()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            player.LearnSkill(SkillKind.BambooFlash);
            Assert.AreEqual("죽림섬", player.SkillName);
            var grunt = director.Spawn(EnemyKind.Boar, player.transform.position + new Vector3(0f, 0f, 3f));
            Vector3 start = player.transform.position;
            yield return null;

            Press(_mouse.rightButton);
            yield return null;
            Release(_mouse.rightButton);
            yield return new WaitForSeconds(1f);
            Assert.Less(grunt.Damageable.Health, grunt.Damageable.MaxHealth, "베였어요");
            Assert.Greater(Vector3.Distance(start, player.transform.position), 1f, "순간이동했어요");
            Assert.Greater(player.SkillCooldownLeft, 0f, "쿨타임");
        }
    }
}
