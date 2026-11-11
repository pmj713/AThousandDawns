using System;
using System.Collections;
using System.IO;
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
    // 비도: 던지면 꽂히거나 떨어지고, 회수로 거둬들이며 꽂힌 적에게 한 번에 피해. 마을 무기 거치대에서 바꿔요.
    public class DaggerTests : InputTestFixture
    {
        PlayerController _player;
        EnemyDirector _director;
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            PrototypeArena.AutoSpawn = false;
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            MetaSave.PathOverride = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        IEnumerator LoadCombat()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            _player.SetWeapon(WeaponKind.Dagger);
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            // 게임 시간과 실제 시간이 둘 다 지나야 포기해요 (느린 프레임은 deltaTime이 잘려 게임 시간이 늦게 흘러요). 실제 시간 5배가 상한.
            float end = Time.time + timeout, realEnd = Time.realtimeSinceStartup + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && (Time.time < end || Time.realtimeSinceStartup < realEnd) && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        [UnityTest]
        public IEnumerator 던지면_꽂히고_회수하면_한_번에_피해()
        {
            yield return LoadCombat();
            Assert.AreEqual("회수 (비도 0)", _player.SkillName, "비도를 들면 기본 초식은 회수");
            Assert.IsFalse(_player.CanRecall, "남은 비도가 없으면 회수 못 써요");
            var boar = _director.Spawn(EnemyKind.Boar, _player.transform.position + _player.Facing * 3f);
            var target = boar.Damageable;
            yield return null;
            float hp = target.Health;

            for (int i = 0; i < 3; i++) _player.ThrowDaggers(1, HitDamageInput.Of(8f), HitTag.Basic, 0f);
            yield return WaitUntil(() => _player.StuckDaggers(target) == 3, 2f);
            Assert.AreEqual(3, _player.StuckDaggers(target), "꽂혀요");
            Assert.Less(target.Health, hp, "맞았어요");
            Assert.AreEqual(3, _player.DaggerCount);
            Assert.IsTrue(_player.CanRecall);

            float before = target.Health;
            Assert.AreEqual(1, _player.RecallDaggers(), "같은 적은 한 번으로 합쳐요");
            Assert.Less(target.Health, before, "회수 피해");
            Assert.AreEqual(0, _player.DaggerCount, "전부 돌아와요");
        }

        [UnityTest]
        public IEnumerator 방패_정면에_맞으면_피해가_줄고_적_앞에_떨어짐()
        {
            yield return LoadCombat();
            var shield = _director.Spawn(EnemyKind.Shield, _player.transform.position + _player.Facing * 3f);
            yield return new WaitForSeconds(0.3f); // 방패를 들고 나를 바라볼 때까지
            var target = shield.Damageable;
            float hp = target.Health;
            _player.ThrowDaggers(1, HitDamageInput.Of(8f), HitTag.Basic, 0f);
            yield return WaitUntil(() => _player.DaggerCount == 1, 2f);
            Assert.AreEqual(1, _player.DaggerCount, "떨어진 비도");
            Assert.AreEqual(0, _player.StuckDaggers(target), "방패 정면엔 꽂히지 않아요");
            Assert.Greater(hp - target.Health, 0f, "피해는 들어가요");
            Assert.Less(hp - target.Health, 8f, "줄어든 피해");
        }

        [UnityTest]
        public IEnumerator 회수하면_비도가_날아와_닿으면_사라짐()
        {
            yield return LoadCombat();
            _player.ThrowDaggers(1, HitDamageInput.Of(8f), HitTag.Basic, 0f);
            yield return WaitUntil(() => _player.DaggerCount == 1, 2f);
            _player.RecallDaggers();
            Assert.AreEqual(0, _player.DaggerCount, "남은 비도로는 안 셈");
            Assert.IsNotNull(GameObject.Find("비도"), "날아오는 중");
            yield return WaitUntil(() => GameObject.Find("비도") == null, 2f);
            Assert.IsNull(GameObject.Find("비도"), "나에게 닿으면 사라져요");
        }

        [UnityTest]
        public IEnumerator 꽂힌_적이_바로_사라지면_비도는_바닥에_떨어짐()
        {
            yield return LoadCombat();
            var bomber = _director.Spawn(EnemyKind.Bomber, _player.transform.position + _player.Facing * 3f);
            var target = bomber.Damageable;
            yield return null;
            _player.ThrowDaggers(1, HitDamageInput.Of(1f), HitTag.Basic, 0f);
            yield return WaitUntil(() => _player.StuckDaggers(target) == 1, 2f);
            Object.Destroy(bomber.gameObject); // 자폭: 쓰러지지 않고 바로 사라져요
            yield return null;
            yield return null;
            var dagger = GameObject.Find("비도");
            Assert.IsNotNull(dagger);
            Assert.Less(dagger.transform.position.y, 0.2f, "공중에 남지 않고 바닥에");
            Assert.AreEqual(1, _player.DaggerCount, "회수할 수 있어요");
        }

        [UnityTest]
        public IEnumerator 비도를_계속_던져도_캐릭터가_떠오르지_않음()
        {
            yield return LoadCombat();
            for (int i = 0; i < 20; i++)
            {
                _player.ThrowDaggers(3, HitDamageInput.Of(1f), HitTag.Finisher, 0f);
                yield return null;
                Assert.Less(Mathf.Abs(_player.transform.position.y), 0.05f, "바닥 높이 그대로");
            }
        }

        [UnityTest]
        public IEnumerator 빗나가면_바닥에_떨어지고_방을_옮기면_사라짐()
        {
            yield return LoadCombat();
            _player.ThrowDaggers(3, HitDamageInput.Of(8f), HitTag.Finisher, 0f); // 부채꼴 3발, 앞에 적 없음
            Assert.AreEqual(0, _player.DaggerCount, "날아가는 중엔 안 셈");
            yield return WaitUntil(() => _player.DaggerCount == 3, 2f);
            Assert.AreEqual(3, _player.DaggerCount, "사거리 끝에 떨어져요");
            _player.ClearDaggers();
            Assert.AreEqual(0, _player.DaggerCount);
        }

        [UnityTest]
        public IEnumerator 마을_무기_거치대에서_비도로_바꾸고_다음_판에도_기억()
        {
            RoomRun.TutorialEnabled = false;
            var meta = new MetaSave { runs = 1, memory = 0 };
            meta.bloomed.AddRange(new[] { "tongmaek", "jeomhyeol", "hanok", "forge", "flying_dagger" });
            meta.Save();
            PrototypeArena.AutoSpawn = true;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsTrue(run.InVillage);
            Assert.IsFalse(player.UsesDagger, "처음엔 검");
            run.UseSpot(RoomRun.VillageSpot.Rack);
            Assert.IsTrue(player.UsesDagger, "비도로 바꿨어요");
            Assert.AreEqual(1, MetaSave.Load().weapon, "기억해요");

            run.Depart();
            for (int i = 0; i < 30 && (run != null); i++) yield return null;
            yield return null;
            player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsTrue(player.UsesDagger, "출발해도 비도");
        }
        // 비도는 비급을 배워도 초식이 회수 그대로, 비급은 회수에 효과를 덧붙여요
        IEnumerator RecallWith(SkillKind skill, float distance, Func<EnemyBrain, IEnumerator> check)
        {
            yield return LoadCombat();
            _player.LearnSkill(skill);
            var boar = _director.Spawn(EnemyKind.Boar, _player.transform.position + _player.Facing * distance);
            boar.Damageable.ScaleMaxHealth(10f); // 회수 피해에 죽지 않게
            yield return null;
            for (int i = 0; i < 3; i++) _player.ThrowDaggers(1, HitDamageInput.Of(1f), HitTag.Basic, 0f);
            yield return WaitUntil(() => _player.StuckDaggers(boar.Damageable) == 3, 2f);
            Assert.AreEqual($"회수 · {PlayerController.SkillLabel(skill)} (비도 3)", _player.SkillName);
            Press(Mouse.current.rightButton);
            yield return null;
            Release(Mouse.current.rightButton);
            yield return WaitUntil(() => _player.DaggerCount == 0, 2f);
            Assert.AreEqual(0, _player.DaggerCount, "회수는 그대로");
            yield return check(boar);
        }

        [UnityTest]
        public IEnumerator 비도_금강장은_돌아온_비도만큼_보호막()
        {
            yield return RecallWith(SkillKind.Palm, 3f, boar =>
            {
                Assert.Greater(_player.Shields.Total, 0, "보호막");
                return Done();
            });
        }

        [UnityTest]
        public IEnumerator 비도_죽림섬은_꽂힌_적에게_순간이동()
        {
            yield return RecallWith(SkillKind.BambooFlash, 4f, boar =>
            {
                Assert.Less(Vector3.Distance(_player.transform.position, boar.transform.position), 2.5f, "적 곁으로 순간이동");
                return Done();
            });
        }

        [UnityTest]
        public IEnumerator 비도_붕산권은_회수와_함께_충격파()
        {
            yield return RecallWith(SkillKind.Quake, 1.5f, boar =>
            {
                Assert.Greater(Vector3.Distance(_player.transform.position, boar.transform.position), 2.5f, "밀려나요");
                return Done();
            });
        }

        // 비도 + 탁랑참: 회수 대신 앞쪽에 비도가 모여 소용돌이 → 둘레 적을 끌어당겨 베고, 끝나면 손으로 돌아와요
        [UnityTest]
        public IEnumerator 비도_탁랑참은_탁류_소용돌이()
        {
            yield return LoadCombat();
            _player.LearnSkill(SkillKind.Takrang);
            var boar = _director.Spawn(EnemyKind.Boar, _player.transform.position + _player.Facing * 3f);
            boar.Damageable.ScaleMaxHealth(10f);
            yield return null;
            for (int i = 0; i < 3; i++) _player.ThrowDaggers(1, HitDamageInput.Of(1f), HitTag.Basic, 0f);
            yield return WaitUntil(() => _player.StuckDaggers(boar.Damageable) == 3, 2f);
            Assert.AreEqual("탁류 소용돌이 (비도 3)", _player.SkillName);

            Press(Mouse.current.rightButton);
            yield return null;
            Release(Mouse.current.rightButton);
            yield return WaitUntil(() => _player.IsVortexing, 1f);
            Assert.IsTrue(_player.IsVortexing, "소용돌이가 일어나요");
            Assert.AreEqual(0, _player.StuckDaggers(boar.Damageable), "꽂힌 비도도 빠져나가 모여요");
            float hp = boar.Damageable.Health;
            var boarCc = boar.GetComponent<CharacterController>();
            float pinEnd = Time.time + 1f;
            while (Time.time < pinEnd) // 멧돼지가 돌진해 나가지 않게 소용돌이 가운데에 붙잡아 둬요
            {
                boarCc.enabled = false;
                boar.transform.position = new Vector3(_player.VortexCenter.x, 0f, _player.VortexCenter.z);
                boarCc.enabled = true;
                yield return null;
            }
            Assert.Less(boar.Damageable.Health, hp, "소용돌이 속 적을 베요");
            yield return WaitUntil(() => !_player.IsVortexing && GameObject.Find("비도") == null, 3f);
            Assert.IsNull(GameObject.Find("비도"), "끝나면 손으로 돌아와요");
        }

        static IEnumerator Done() { yield break; }
    }
}
