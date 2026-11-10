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
    // 고수 (청죽객·철권 마웅): 2층 관문 뒤 결투장, 50%에서 2페이즈, 쓰러뜨리면 비급 전수 / 흡성 / 방면 → 3층 갈림길.
    public class MasterTests : InputTestFixture
    {
        Mouse _mouse;
        RoomRun _run;
        PlayerController _player;

        public override void Setup()
        {
            base.Setup();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{System.Guid.NewGuid():N}.json"); // 진짜 저장을 건드리지 않게 (회차 0이라 마을 없이 시작)
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 2024;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            RoomRun.MasterOverride = null;
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

        // 1층 → 2층 → 2층 관문 → 고수. 쓰러뜨리고 선택 화면까지.
        IEnumerator BeatMaster(EnemyKind kind = EnemyKind.Qingzhu)
        {
            RoomRun.MasterOverride = kind;
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
            var info = _run.MasterInfo;
            Assert.IsNotNull(master);
            Assert.AreEqual(kind, master.Kind);
            Assert.AreEqual(kind == EnemyKind.Qingzhu ? 1100f : 1200f, master.Damageable.MaxHealth);
            Assert.AreEqual(EnemyRank.Master, master.Rank);

            master.Damageable.ApplyHit(Mathf.CeilToInt(master.Damageable.MaxHealth * 0.55f));
            Assert.IsTrue(info.IsTransitioning, "50%에서 전환 (무적)");
            Assert.IsFalse(master.Damageable.Targetable);
            yield return WaitUntil(() => info.Phase == 2 && !info.IsTransitioning, 4f);
            Assert.AreEqual(2, info.Phase);
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
        public IEnumerator 철권_마웅의_비급은_붕산권()
        {
            yield return BeatMaster(EnemyKind.IronFist);
            Assert.IsTrue(_run.ChooseEvent(0));
            Assert.AreEqual(SkillKind.Quake, _player.Skill);
            Assert.AreEqual("붕산권", _player.SkillName);
            _run.CloseEvent();
            Assert.AreEqual(3, _run.Floor);
        }

        // 철권 마웅을 멀리 두면 돌진하다 벽에 부딪혀 기절 (빈틈)
        [UnityTest]
        public IEnumerator 철권_마웅은_돌진하다_벽에_부딪히면_기절()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(0f, 0f, -12.5f); // 남쪽 벽 바로 앞
            cc.enabled = true;
            var fist = (IronFistBrain)director.Spawn(EnemyKind.IronFist, new Vector3(0f, 0f, 6f)); // 첫 공격 대기 1초 동안 걸어와도 돌진 거리(5칸) 밖
            fist.SetZone(new Vector3(0f, 0f, -2f), new Vector2(40f, 24f));
            yield return WaitUntil(() => fist.IsWallStunned, 8f);
            Assert.IsTrue(fist.IsWallStunned, "벽에 부딪혀 기절");
            Assert.Less(player.Health, player.MaxHealth, "돌진에 맞아요");
        }

        [UnityTest]
        public IEnumerator 붕산권은_둘레의_적을_치고_밀어냄()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            player.LearnSkill(SkillKind.Quake);
            var grunt = director.Spawn(EnemyKind.Boar, player.transform.position + new Vector3(1.5f, 0f, 0f)); // 체력 80 (충격파 40에 안 죽음)
            yield return null;
            float before = Vector3.Distance(grunt.transform.position, player.transform.position);

            Press(_mouse.rightButton);
            yield return null;
            Release(_mouse.rightButton);
            yield return new WaitForSeconds(0.5f);
            Assert.Less(grunt.Damageable.Health, grunt.Damageable.MaxHealth, "충격파에 맞아요");
            Assert.Greater(Vector3.Distance(grunt.transform.position, player.transform.position), before + 1f, "밀려나요");
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
            var master = (QingzhuBrain)director.Spawn(EnemyKind.Qingzhu, new Vector3(0f, 0f, 1f)); // 청죽객
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
