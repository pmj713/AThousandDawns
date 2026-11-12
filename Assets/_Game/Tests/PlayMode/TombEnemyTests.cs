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
    // 천마총 일반 적 5종: 마교도 광폭 · 혈궁수 3연발 · 혈인 부활 · 천마 근위 반격 · 혈폭자 핏덩이와 늦게 터지기
    public class TombEnemyTests : InputTestFixture
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
            _director.ResetRoom();
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
                (EnemyKind.Cultist, "마교도", 39f), (EnemyKind.BloodArcher, "혈궁수", 25f), (EnemyKind.BloodShaman, "혈인", 40f),
                (EnemyKind.DemonGuard, "천마 근위", 78f), (EnemyKind.BloodBomber, "혈폭자", 25f),
            };
            foreach (var (kind, name, hp) in expect)
            {
                var e = _director.Spawn(kind, _player.transform.position + new Vector3(8f, 0f, 0f));
                Assert.AreEqual(kind, e.Kind);
                Assert.AreEqual(name, e.DisplayName);
                Assert.AreEqual(hp, e.Damageable.MaxHealth, 0.01f, $"{name} 체력 (지역 배율 전)");
                e.Damageable.ApplyHit(99999);
            }
            Assert.AreSame(RoomBudget.Tomb, RoomBudget.ForRegion(3));
            Assert.IsFalse(EnemyBrain.Allows(EnemyKind.DemonGuard, EliteTrait.IronShirt), "방패형 + 철포삼");
        }

        [UnityTest]
        public IEnumerator 마교도는_체력_절반_아래면_광폭()
        {
            yield return Load();
            var cultist = (CultistBrain)Spawn(EnemyKind.Cultist, 6f);
            yield return null;
            cultist.Damageable.ApplyHit(10);
            Assert.IsFalse(cultist.IsFrenzied, "아직 절반 위");
            cultist.Damageable.ApplyHit(Mathf.CeilToInt(cultist.Damageable.MaxHealth * 0.3f));
            Assert.IsTrue(cultist.IsFrenzied, "절반 아래면 광폭");
        }

        [UnityTest]
        public IEnumerator 혈궁수는_세_발을_연달아_쏨()
        {
            yield return Load();
            Spawn(EnemyKind.BloodArcher, 6f);
            var seen = new System.Collections.Generic.HashSet<Arrow>(); // 앞 화살이 맞아 사라져도 세요
            yield return WaitUntil(() =>
            {
                foreach (var a in Object.FindObjectsByType<Arrow>(FindObjectsSortMode.None)) seen.Add(a);
                return seen.Count >= 3;
            }, 6f);
            Assert.GreaterOrEqual(seen.Count, 3, "세 발이 날아가요");
        }

        [UnityTest]
        public IEnumerator 혈인은_쓰러진_적을_되살림()
        {
            yield return Load();
            var shaman = (BloodShamanBrain)Spawn(EnemyKind.BloodShaman, 7f);
            var grunt = Spawn(EnemyKind.Grunt, 5f);
            float maxHp = grunt.Damageable.MaxHealth;
            yield return null;
            grunt.Damageable.ApplyHit(99999);
            yield return null;
            Assert.AreEqual(1, BloodShamanBrain.CorpseCount, "시체를 기억해요");
            EnemyBrain risen = null;
            yield return WaitUntil(() => (risen = _director.Enemies.FirstOrDefault(e => e.Kind == EnemyKind.Grunt && !e.IsDead)) != null, 8f);
            Assert.IsNotNull(risen, "되살아나요");
            Assert.IsTrue(risen.Revived);
            Assert.AreEqual(maxHp * 0.5f, risen.Damageable.MaxHealth, 0.5f, "체력 절반");
            Assert.AreEqual(2, shaman.RevivesLeft);
        }

        [UnityTest]
        public IEnumerator 천마_근위는_정면에서_맞으면_반격()
        {
            yield return Load();
            var guard = (DemonGuardBrain)Spawn(EnemyKind.DemonGuard, 2f);
            yield return new WaitForSeconds(0.3f); // 방패를 들고 나를 바라볼 때까지
            float hp = _player.Health;
            float ratio = guard.Damageable.ResolveSituational(_player.transform.position, HitTag.Basic);
            Assert.Less(ratio, 1f, "정면은 막아요");
            Assert.AreEqual(1, guard.Counters, "곧바로 반격");
            Assert.Less(_player.Health, hp, "반격에 맞아요");
            guard.Damageable.ResolveSituational(_player.transform.position, HitTag.Basic);
            Assert.AreEqual(1, guard.Counters, "반격은 1.2초마다 한 번");
        }

        [UnityTest]
        public IEnumerator 혈폭자는_쓰러지면_늦게_터짐()
        {
            yield return Load();
            var bomber = Spawn(EnemyKind.BloodBomber, 1.5f);
            yield return null;
            float hp = _player.Health;
            bomber.Damageable.ApplyHit(99999);
            yield return null;
            Assert.AreEqual(1, BloodBurst.Live, "붉은 예고 원");
            Assert.AreEqual(hp, _player.Health, "바로는 안 터져요");
            yield return WaitUntil(() => BloodBurst.Live == 0, 2f);
            Assert.Less(_player.Health, hp, "1.2초 뒤 터져요 (가만히 있으면 맞아요)");
        }
    }
}
