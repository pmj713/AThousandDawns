using System.Collections;
using System.Linq;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AThousandDawns.Tests
{
    // 차원 겹침 판타지 적: 슬라임 (쓰러지면 둘로) · 고블린 넷이 오류 없이 싸우고, 지역 적 목록에 섞여요
    public class FantasyEnemyTests
    {
        [TearDown]
        public void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            RoomBudget.FantasyOverride = 0;
            Time.timeScale = 1f;
        }

        IEnumerator Load()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 고블린_넷과_슬라임이_오류_없이_싸움([Values(EnemyKind.Slime, EnemyKind.Goblin, EnemyKind.GoblinArcher, EnemyKind.GoblinRogue, EnemyKind.GoblinShaman)] EnemyKind kind)
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var enemy = director.Spawn(kind, player.transform.position + new Vector3(0f, 0f, 4f));
            Assert.AreEqual(kind, enemy.Kind);
            if (kind == EnemyKind.GoblinShaman) director.Spawn(EnemyKind.Goblin, player.transform.position + new Vector3(2f, 0f, 4f)); // 광폭화를 걸 동료
            Time.timeScale = 2f;
            float end = Time.time + 16f;
            while (Time.time < end && player.Health >= player.MaxHealth) { yield return null; }
            Assert.Less(player.Health, player.MaxHealth, $"{enemy.DisplayName}: 공격이 들어와야 해요");
        }

        [UnityTest]
        public IEnumerator 슬라임은_쓰러지면_작은_둘로_갈라지고_작은_것은_안_갈라짐()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var slime = director.Spawn(EnemyKind.Slime, player.transform.position + new Vector3(0f, 0f, 5f));
            slime.Damageable.ApplyHit(99999, false);
            yield return null;
            var small = director.Enemies.Where(e => e != null && !e.IsDead && e.Kind == EnemyKind.Slime).ToList();
            Assert.AreEqual(2, small.Count, "작은 슬라임 둘");
            Assert.IsTrue(small.All(e => e.IsSplitChild));
            foreach (var s in small) s.Damageable.ApplyHit(99999, false);
            yield return null;
            Assert.AreEqual(0, director.Enemies.Count(e => e != null && !e.IsDead && e.Kind == EnemyKind.Slime), "작은 것은 다시 안 갈라져요");
        }

        [Test]
        public void 판타지_적은_모든_지역_목록에_섞여요()
        {
            RoomBudget.FantasyOverride = 1;
            for (int r = 0; r < 4; r++)
                foreach (var k in RoomBudget.Fantasy) CollectionAssert.Contains(RoomBudget.ForRegion(r), k);
            RoomBudget.FantasyOverride = 0;
            Assert.IsFalse(RoomBudget.ForRegion(0).Any(RoomBudget.IsFantasy), "끄면 무협 적만");
        }
    }
}
