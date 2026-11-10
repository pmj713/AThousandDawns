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
    // 대장간 비도 융합 (무기 시트 5번 표): 강기 관통, 뇌정·열양·빙백은 꽂힌 비도에 따라, 금강은 회수 보호막, 경공은 대시로 다시 던지기.
    public class DaggerForgeTests : InputTestFixture
    {
        PlayerController _player;
        EnemyDirector _director;
        Keyboard _keyboard;
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
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

        IEnumerator Load(Keyword? a, Keyword? b = null)
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            _player.SetWeapon(WeaponKind.Dagger);
            _player.Items.SetForgeSlot(0, a);
            _player.Items.SetForgeSlot(1, b);
        }

        Damageable Spawn(float distance)
        {
            var e = _director.Spawn(EnemyKind.Boar, _player.transform.position + _player.Facing * distance);
            return e.Damageable;
        }

        void Throw(int count = 1) { for (int i = 0; i < count; i++) _player.ThrowDaggers(1, HitDamageInput.Of(1f), HitTag.Basic, 0f); }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            // 게임 시간과 실제 시간이 둘 다 지나야 포기해요 (느린 프레임은 deltaTime이 잘려 게임 시간이 늦게 흘러요). 실제 시간 5배가 상한.
            float end = Time.time + timeout, realEnd = Time.realtimeSinceStartup + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && (Time.time < end || Time.realtimeSinceStartup < realEnd) && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        [UnityTest]
        public IEnumerator 강기_관통해_다음_적에게_꽂힘()
        {
            yield return Load(Keyword.Ganggi);
            var near = Spawn(2f);
            var far = Spawn(4.5f);
            yield return null;
            float nearHp = near.Health, farHp = far.Health;
            Throw();
            yield return WaitUntil(() => _player.StuckDaggers(far) == 1, 2f);
            Assert.AreEqual(0, _player.StuckDaggers(near), "첫 적은 뚫고 지나가요");
            Assert.AreEqual(1, _player.StuckDaggers(far), "다음 적에게 꽂혀요");
            Assert.Less(near.Health, nearHp);
            Assert.Less(far.Health, farHp);
        }

        [UnityTest]
        public IEnumerator 뇌정_꽂힐_때마다_감전_강화판은_최대_중첩_증가()
        {
            yield return Load(Keyword.Noejeong, Keyword.Noejeong);
            var target = Spawn(2.5f);
            var status = StatusEffects.For(target);
            int baseMax = status.ShockMax;
            Throw(3);
            yield return WaitUntil(() => _player.StuckDaggers(target) == 3, 2f);
            yield return null;
            Assert.AreEqual(3, status.ShockStacks, "꽂힐 때마다 감전 1중첩");
            Assert.AreEqual(baseMax + 3, status.ShockMax, "강화판: 꽂힌 비도 1개당 최대 중첩 +1");
            _player.RecallDaggers();
            yield return null;
            Assert.AreEqual(baseMax, status.ShockMax, "회수하면 원래대로");
        }

        [UnityTest]
        public IEnumerator 열양_꽂혀_있으면_1초마다_화상()
        {
            yield return Load(Keyword.Yeolyang, Keyword.Yeolyang);
            var target = Spawn(2.5f);
            var status = StatusEffects.For(target);
            Throw(4);
            yield return WaitUntil(() => _player.StuckDaggers(target) == 4, 2f);
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(status.BurnStacks, 0, "1초마다 화상 1중첩");
            Assert.AreEqual(2, status.ExtraBurnMax, "강화판: 비도 2개마다 최대 중첩 +1");
            _player.RecallDaggers();
            yield return null;
            Assert.AreEqual(0, status.ExtraBurnMax, "회수하면 내려가요");
        }

        [UnityTest]
        public IEnumerator 빙백_꽂혀_있으면_둔화_유지_강화판은_회수때_둔화()
        {
            yield return Load(Keyword.Bingbaek, Keyword.Bingbaek);
            var target = Spawn(2.5f);
            var status = StatusEffects.For(target);
            Throw();
            yield return WaitUntil(() => _player.StuckDaggers(target) == 1, 2f);
            yield return null;
            Assert.IsTrue(status.HoldSlow, "꽂혀 있는 동안 둔화가 안 풀려요");
            _player.RecallDaggers();
            yield return null;
            Assert.IsFalse(status.HoldSlow);
            Assert.GreaterOrEqual(status.SlowStacks, 2, "강화판: 회수할 때 둔화 2중첩 (+ 홈 둘로 #빙백 2단계라 초식 적중 둔화 1중첩)");
        }

        [UnityTest]
        public IEnumerator 금강_회수하면_돌아온_비도만큼_보호막()
        {
            yield return Load(Keyword.Geumgang);
            Throw(3); // 앞에 적이 없어 바닥에 떨어져요
            yield return WaitUntil(() => _player.DaggerCount == 3, 2f);
            Assert.AreEqual(0, _player.Shields.Total);
            _player.RecallDaggers();
            Assert.AreEqual(Mathf.RoundToInt(_player.MaxHealth * 0.03f), _player.Shields.Total, 1, "1개당 1% × 3");
        }

        [UnityTest]
        public IEnumerator 경공_대시하면_주변_비도를_다시_던짐()
        {
            yield return Load(Keyword.Gyeonggong);
            var target = Spawn(2.5f);
            Throw();
            yield return WaitUntil(() => _player.StuckDaggers(target) == 1, 2f);
            float hp = target.Health;
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            yield return WaitUntil(() => target.Health < hp, 2f);
            Assert.Less(target.Health, hp, "다시 던진 비도가 맞았어요");
        }

        [UnityTest]
        public IEnumerator 비도를_들면_검_융합_효과는_꺼짐()
        {
            yield return Load(Keyword.Ganggi, Keyword.Ganggi);
            Assert.AreEqual(1f, _player.ForgeRangeForTest, 1e-4f, "비도: 평타 범위 +60% 없음");
            _player.SetWeapon(WeaponKind.Sword);
            Assert.AreEqual(1.6f, _player.ForgeRangeForTest, 1e-4f, "검: 강화판 +60%");
        }
    }
}
