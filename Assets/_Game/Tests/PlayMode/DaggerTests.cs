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
    }
}
