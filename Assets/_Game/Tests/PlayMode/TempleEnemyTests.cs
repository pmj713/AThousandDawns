using System;
using System.Collections;
using System.Linq;
using AThousandDawns.Combat;
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
    // 폐사찰 일반 적 5종: 망령 혼불 · 석상 나한 기절 · 목탁승 망령 호출 · 파계승 금강불괴 · 등불 귀신 감전
    public class TempleEnemyTests : InputTestFixture
    {
        PlayerController _player;
        EnemyDirector _director;

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

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        EnemyBrain Spawn(EnemyKind kind, float distance) =>
            _director.Spawn(kind, _player.transform.position + _player.Facing * distance);

        [UnityTest]
        public IEnumerator 다섯_종이_이름과_체력대로_나옴()
        {
            yield return Load();
            var expect = new (EnemyKind kind, string name, float hp)[]
            {
                (EnemyKind.Wraith, "망령", 25f), (EnemyKind.Arhat, "석상 나한", 112f), (EnemyKind.Moktak, "목탁승", 40f),
                (EnemyKind.Monk, "파계승", 60f), (EnemyKind.Lantern, "등불 귀신", 25f),
            };
            foreach (var (kind, name, hp) in expect)
            {
                var e = _director.Spawn(kind, _player.transform.position + new Vector3(8f, 0f, 0f));
                Assert.AreEqual(kind, e.Kind);
                Assert.AreEqual(name, e.DisplayName);
                Assert.AreEqual(hp, e.Damageable.MaxHealth, 0.01f, $"{name} 체력 (지역 배율 전)");
                e.Damageable.ApplyHit(99999);
            }
            Assert.IsTrue(RoomBudget.Temple.Contains(EnemyKind.Wraith));
            Assert.AreEqual(3, RoomBudget.Cost(EnemyKind.Arhat));
        }

        [UnityTest]
        public IEnumerator 망령_혼불은_따라와서_맞힘()
        {
            yield return Load();
            Spawn(EnemyKind.Wraith, 7f);
            float hp = _player.Health;
            yield return WaitUntil(() => _player.Health < hp, 10f);
            Assert.Less(_player.Health, hp, "혼불에 맞았어요");
        }

        [UnityTest]
        public IEnumerator 석상_나한_돌진에_맞으면_기절()
        {
            yield return Load();
            Spawn(EnemyKind.Arhat, 3.5f);
            float hp = _player.Health;
            yield return WaitUntil(() => _player.Health < hp, 8f);
            Assert.Less(_player.Health, hp, "돌진에 맞았어요");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(_player.IsStunned, "맞은 뒤 0.5초가 지나도 아직 기절 (0.8초)");
        }

        [UnityTest]
        public IEnumerator 목탁승은_망령을_부름()
        {
            yield return Load();
            Spawn(EnemyKind.Moktak, 6f);
            yield return WaitUntil(() => _director.Count(EnemyKind.Wraith) > 0, 8f);
            Assert.AreEqual(2, _director.Count(EnemyKind.Wraith), "망령 2마리");
        }

        [UnityTest]
        public IEnumerator 파계승_금강불괴_동안은_피해가_거의_안_들어감()
        {
            yield return Load();
            var monk = (MonkBrain)Spawn(EnemyKind.Monk, 2.5f);
            Assert.AreEqual(1f, monk.Damageable.ResolveSituational(_player.transform.position, HitTag.Basic));
            yield return WaitUntil(() => monk.IsStanced, 6f);
            Assert.IsTrue(monk.IsStanced, "가까이 있으면 자세를 잡아요");
            Assert.AreEqual(0f, monk.Damageable.ResolveSituational(_player.transform.position, HitTag.Basic), "자세 중엔 최소 피해만");
            yield return WaitUntil(() => !monk.IsStanced, 3f);
            Assert.IsFalse(monk.IsStanced, "2초 뒤 풀려요");
        }

        [UnityTest]
        public IEnumerator 등불_귀신이_터지면_감전()
        {
            yield return Load();
            var lantern = (LanternBrain)Spawn(EnemyKind.Lantern, 3f);
            yield return WaitUntil(() => lantern == null || lantern.Exploded, 8f);
            Assert.Greater(_player.SlowLeft, 0f, "감전: 잠시 느려져요");
            Assert.AreEqual(0.4f, _player.SlowRatio, 0.01f);
        }
    }
}
