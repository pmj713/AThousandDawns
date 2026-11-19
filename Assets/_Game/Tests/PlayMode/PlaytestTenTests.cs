using System;
using System.Collections;
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
    // 플레이테스트 10차: 적대하는 다른 진영 적은 돕지 않음 · 보스·고수가 부른 부하는 수련치 없음 · 보호막 깨지는 소리는 다 깨질 때 한 번
    public class PlaytestTenTests : InputTestFixture
    {
        PlayerController _player;
        EnemyDirector _director;
        bool _rivalry;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            PrototypeArena.AutoSpawn = false;
            _rivalry = EnemyBrain.RivalryEnabled;
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            EnemyBrain.RivalryEnabled = _rivalry;
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

        Vector3 Ahead(float distance, float side = 0f) =>
            _player.transform.position + _player.Facing * distance + Vector3.Cross(Vector3.up, _player.Facing) * side;

        [UnityTest]
        public IEnumerator 적대하는_다른_진영_적에게는_도움을_걸지_않음()
        {
            yield return Load();
            EnemyBrain.RivalryEnabled = true;
            var moktak = _director.Spawn(EnemyKind.Moktak, Ahead(6f));
            var grunt = _director.Spawn(EnemyKind.Grunt, Ahead(4f, -1.5f));
            var goblin = _director.Spawn(EnemyKind.Goblin, Ahead(4f, 1.5f));
            yield return null;
            Assert.AreEqual(EnemyFaction.Wuxia, moktak.Faction);
            Assert.AreEqual(EnemyFaction.Fantasy, goblin.Faction);
            Assert.IsTrue(moktak.CanSupport(grunt), "같은 진영은 도와요");
            Assert.IsFalse(moktak.CanSupport(goblin), "적대하는 다른 진영은 돕지 않아요");
            goblin.Allied = true;
            Assert.IsTrue(moktak.CanSupport(goblin), "협력한 다른 진영은 도와요");
            goblin.Allied = false;
            EnemyBrain.RivalryEnabled = false;
            Assert.IsTrue(moktak.CanSupport(goblin), "적끼리 싸움이 꺼져 있으면 아무도 적대하지 않아요");
            EnemyBrain.RivalryEnabled = true;
            Assert.IsFalse(moktak.CanSupport(moktak), "자기 자신은 빼요");

            // 깃발수 지휘: 같은 진영 수적은 빨라지고 고블린은 그대로
            moktak.Damageable.ApplyHit(99999, false);
            grunt.Damageable.ApplyHit(99999, false);
            _director.Spawn(EnemyKind.FlagBearer, Ahead(6f));
            var pirate = _director.Spawn(EnemyKind.Pirate, Ahead(4f, -1.5f));
            StatusEffects.For(pirate.Damageable).TryFreeze(10f);
            StatusEffects.For(goblin.Damageable).TryFreeze(10f);
            goblin.Damageable.ScaleMaxHealth(50f);
            yield return WaitUntil(() => { _player.Heal(9999f); return pirate.IsHastened; }, 8f);
            Assert.IsTrue(pirate.IsHastened, "같은 진영은 지휘받아요");
            Assert.IsFalse(goblin.IsHastened, "적대하는 고블린은 지휘받지 않아요");
        }

        [UnityTest]
        public IEnumerator 보스가_부른_부하는_쓰러뜨려도_수련치가_없음()
        {
            yield return Load();
            var summon = _director.SpawnSummon(EnemyKind.Grunt, Ahead(4f));
            Assert.IsTrue(summon.NoXp, "SpawnSummon 기본은 보스·고수가 부른 부하");
            int xp = _player.Level.Xp, level = _player.Level.Level;
            summon.Damageable.ApplyHit(99999, false);
            yield return null;
            Assert.AreEqual(level, _player.Level.Level);
            Assert.AreEqual(xp, _player.Level.Xp, "보스가 부른 부하는 수련치 0");

            var called = _director.SpawnSummon(EnemyKind.Grunt, Ahead(4f), givesXp: true); // 나팔수가 부른 도움
            Assert.IsFalse(called.NoXp);
            called.Damageable.ApplyHit(99999, false);
            yield return null;
            Assert.IsTrue(_player.Level.Level > level || _player.Level.Xp > xp, "일반 적이 부른 도움은 수련치를 줘요");
        }

        [Test]
        public void 보호막은_다_깨질_때만_Depleted()
        {
            var shields = new PlayerShields();
            int broken = 0, depleted = 0;
            shields.Broken += _ => broken++;
            shields.Depleted += () => depleted++;
            shields.Add(5, 0f, 100);
            shields.Add(5, 0f, 100);

            Assert.AreEqual(0, shields.Absorb(3), "보호막이 다 막아요");
            Assert.AreEqual(0, broken);
            Assert.AreEqual(0, depleted, "깎이기만 하면 소리 없음");

            Assert.AreEqual(0, shields.Absorb(3));
            Assert.AreEqual(1, broken, "첫 보호막이 깨져요");
            Assert.AreEqual(0, depleted, "아직 보호막이 남아 있으면 소리 없음");

            Assert.AreEqual(6, shields.Absorb(10), "남은 4만 막고 나머지는 지나가요");
            Assert.AreEqual(2, broken);
            Assert.AreEqual(1, depleted, "다 깨진 순간 한 번");

            Assert.AreEqual(5, shields.Absorb(5), "보호막이 없으면 그대로");
            Assert.AreEqual(1, depleted, "없을 때 맞아도 소리 없음");

            shields.Add(5, 1f, 100);
            shields.Tick(2f);
            Assert.IsFalse(shields.Any);
            Assert.AreEqual(1, depleted, "시간이 다 돼 사라지면 깨진 게 아니에요");
        }
    }
}
