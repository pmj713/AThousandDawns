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
        // 비도는 비급마다 비도 초식이 달라요 (끝나면 비도는 손으로 돌아와요)
        float _beforeCast; // 초식을 쓰기 직전 적 체력

        IEnumerator CastWith(SkillKind skill, float distance, Func<EnemyBrain, IEnumerator> check)
        {
            yield return LoadCombat();
            _player.LearnSkill(skill);
            var boar = _director.Spawn(EnemyKind.Boar, _player.transform.position + _player.Facing * distance);
            boar.Damageable.ScaleMaxHealth(10f); // 초식 피해에 죽지 않게
            yield return null;
            for (int i = 0; i < 3; i++) _player.ThrowDaggers(1, HitDamageInput.Of(1f), HitTag.Basic, 0f);
            yield return WaitUntil(() => _player.StuckDaggers(boar.Damageable) == 3, 2f);
            Assert.AreEqual($"{PlayerController.DaggerSkillLabel(skill)} (비도 3)", _player.SkillName);
            _beforeCast = boar.Damageable.Health;
            Press(Mouse.current.rightButton);
            yield return null;
            Release(Mouse.current.rightButton);
            yield return check(boar);
        }

        [UnityTest]
        public IEnumerator 비도_금강장은_비도_호신륜으로_돌며_투사체를_막음()
        {
            yield return CastWith(SkillKind.Palm, 3f, boar => HaloCheck());
        }

        IEnumerator HaloCheck()
        {
            yield return WaitUntil(() => _player.IsHaloing, 1f);
            Assert.IsTrue(_player.IsHaloing, "비도가 내 둘레를 돌아요");
            Assert.AreEqual(0, _player.DaggerCount, "꽂힌 비도도 빠져나와 돌아요");
            float hp = _player.Health;
            var arrow = _director.SpawnArrow(_player.transform.position + Vector3.up + Vector3.right * 6f, Vector3.left, 10f, 10f, 0.2f, 20f);
            yield return WaitUntil(() => arrow == null, 1.5f);
            Assert.IsTrue(arrow == null, "날아온 화살을 막아요");
            Assert.AreEqual(hp, _player.Health, "화살에 안 맞아요");
            yield return WaitUntil(() => !_player.IsHaloing && GameObject.Find("비도") == null, 5f);
            Assert.IsNull(GameObject.Find("비도"), "끝나면 손으로 돌아와요");
        }

        [UnityTest]
        public IEnumerator 비도_죽림섬은_비도_자리로_순간이동하며_뽑아_벰()
        {
            yield return CastWith(SkillKind.BambooFlash, 4f, boar => FlashCheck(boar));
        }

        IEnumerator FlashCheck(EnemyBrain boar)
        {
            float hp = _beforeCast;
            yield return WaitUntil(() => !_player.IsDaggerFlashing && _player.DaggerCount == 0, 2f);
            Assert.AreEqual(0, _player.DaggerCount, "비도를 하나씩 뽑아 거둬요");
            Assert.Less(Vector3.Distance(_player.transform.position, boar.transform.position), 2.5f, "꽂힌 비도 자리 (적 곁)로 순간이동");
            Assert.Less(boar.Damageable.Health, hp, "지나가며 베요");
        }

        [UnityTest]
        public IEnumerator 비도_붕산권은_둘레_비도가_솟구쳐_찌름()
        {
            yield return CastWith(SkillKind.Quake, 1.5f, boar => BurstCheck(boar));
        }

        IEnumerator BurstCheck(EnemyBrain boar)
        {
            float hp = _beforeCast;
            yield return WaitUntil(() => boar.Damageable.Health < hp, 1f);
            Assert.Less(boar.Damageable.Health, hp, "솟구친 비도와 충격파에 맞아요");
            yield return WaitUntil(() => GameObject.Find("비도") == null, 2f);
            Assert.IsNull(GameObject.Find("비도"), "솟구친 비도는 손으로 돌아와요");
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
