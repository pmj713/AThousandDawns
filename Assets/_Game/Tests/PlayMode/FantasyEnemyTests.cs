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
        public IEnumerator 고블린_넷과_슬라임이_오류_없이_싸움([Values(EnemyKind.Slime, EnemyKind.Goblin, EnemyKind.GoblinArcher, EnemyKind.GoblinRogue, EnemyKind.GoblinShaman, EnemyKind.Orc, EnemyKind.Skeleton, EnemyKind.Imp)] EnemyKind kind)
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

        [UnityTest]
        public IEnumerator 무협_적과_판타지_적은_서로_싸우고_협력하면_안_싸움([Values(false, true)] bool allied)
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 far = player.transform.position + new Vector3(0f, 0f, 9f); // 플레이어보다 서로가 훨씬 가까워요
            var grunt = director.Spawn(EnemyKind.Grunt, far);
            var goblin = director.Spawn(EnemyKind.Goblin, far + new Vector3(1.5f, 0f, 0f));
            grunt.Allied = goblin.Allied = allied;
            Assert.AreEqual(EnemyFaction.Wuxia, grunt.Faction);
            Assert.AreEqual(EnemyFaction.Fantasy, goblin.Faction);
            int hits = 0;
            System.Action<EnemyBrain, EnemyBrain, Vector3> count = (a, b, at) => hits++;
            EnemyBrain.RivalHit += count;
            float gruntHp = grunt.Damageable.Health, goblinHp = goblin.Damageable.Health;
            Time.timeScale = 2f;
            float end = Time.time + 8f;
            while (Time.time < end && hits == 0) { player.Heal(9999f); yield return null; }
            EnemyBrain.RivalHit -= count;
            if (allied)
            {
                Assert.AreEqual(0, hits, "협력한 적은 서로 안 때려요");
                Assert.IsNull(grunt.Rival);
            }
            else
            {
                Assert.Greater(hits, 0, "서로를 노려 때려요");
                Assert.IsTrue(grunt.Damageable.Health < gruntHp || goblin.Damageable.Health < goblinHp);
            }
        }

        [Test]
        public void 차원_감염_특성은_판타지_적은_무공_무협_적은_마법만()
        {
            EnemyBrain.InfectionOverride = 1;
            try
            {
                Assert.IsTrue(EnemyBrain.Allows(EnemyKind.Goblin, EliteTrait.Gyeonggong));
                Assert.IsFalse(EnemyBrain.Allows(EnemyKind.Goblin, EliteTrait.Fireball));
                Assert.IsFalse(EnemyBrain.Allows(EnemyKind.Grunt, EliteTrait.Gyeonggong));
                Assert.IsTrue(EnemyBrain.Allows(EnemyKind.Grunt, EliteTrait.Fireball));
                Assert.IsTrue(EnemyBrain.Allows(EnemyKind.Cultist, EliteTrait.Blink));
                EnemyBrain.InfectionOverride = 0;
                Assert.IsFalse(EnemyBrain.Allows(EnemyKind.Grunt, EliteTrait.Fireball), "끄면 안 나와요");
            }
            finally { EnemyBrain.InfectionOverride = 0; }
        }

        [UnityTest]
        public IEnumerator 차원_감염_정예는_경공_화염구_순간이동을_써요([Values(EliteTrait.Gyeonggong, EliteTrait.Fireball, EliteTrait.Blink)] EliteTrait trait)
        {
            EnemyBrain.InfectionOverride = 1;
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var kind = trait == EliteTrait.Gyeonggong ? EnemyKind.Goblin : EnemyKind.Grunt;
            var elite = director.Spawn(kind, player.transform.position + new Vector3(0f, 0f, 8f));
            elite.MakeElite(trait);
            Assert.IsTrue(elite.HasTrait(trait));
            bool used = false;
            System.Action<EnemyBrain, Vector3> dashed = (e, at) => used |= e == elite;
            System.Action<EnemyBrain, Vector3, Vector3> blinked = (e, a, b) => used |= e == elite;
            EnemyBrain.Dashed += dashed;
            EnemyBrain.Blinked += blinked;
            Time.timeScale = 2f;
            float end = Time.time + 14f;
            while (Time.time < end && !used && !(trait == EliteTrait.Fireball && BloodBurst.Live > 0)) { player.Heal(9999f); yield return null; }
            EnemyBrain.Dashed -= dashed;
            EnemyBrain.Blinked -= blinked;
            EnemyBrain.InfectionOverride = 0;
            Assert.IsTrue(used || (trait == EliteTrait.Fireball && BloodBurst.Live > 0), $"{trait}을(를) 써요");
        }

        [UnityTest]
        public IEnumerator 스켈레톤은_한_번_무너졌다_다시_일어나고_두_번째엔_쓰러짐()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var skeleton = (SkeletonBrain)director.Spawn(EnemyKind.Skeleton, player.transform.position + new Vector3(0f, 0f, 6f));
            skeleton.Damageable.ApplyHit(99999, false);
            Assert.IsFalse(skeleton.IsDead, "처음엔 무너지기만 해요");
            Assert.IsTrue(skeleton.IsReassembling);
            Assert.IsFalse(skeleton.Damageable.Targetable, "무너진 동안은 맞지 않아요");
            float until = Time.realtimeSinceStartup + 4f;
            while (skeleton.IsReassembling && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(skeleton.IsReassembling, "다시 맞춰져요");
            Assert.IsTrue(skeleton.Damageable.Targetable);
            Assert.Greater(skeleton.Damageable.Health, skeleton.Damageable.MaxHealth * 0.4f, "체력 절반으로 일어나요");
            skeleton.Damageable.ApplyHit(99999, false);
            Assert.IsTrue(skeleton.IsDead, "두 번째엔 쓰러져요");
        }

        [UnityTest]
        public IEnumerator 고블린_족장과_오우거가_오류_없이_싸움([Values(EnemyKind.GoblinChief, EnemyKind.Ogre)] EnemyKind kind)
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var boss = director.Spawn(kind, player.transform.position + new Vector3(0f, 0f, 3f));
            ((IMasterBrain)boss).SetZone(player.transform.position, new Vector2(30f, 20f));
            Assert.AreEqual(kind == EnemyKind.Ogre ? EnemyRank.Boss : EnemyRank.Master, boss.Rank);
            Assert.AreEqual(EnemyFaction.Fantasy, boss.Faction);
            Time.timeScale = 2f;
            float end = Time.time + 16f;
            while (Time.time < end && player.Health >= player.MaxHealth) yield return null;
            Assert.Less(player.Health, player.MaxHealth, $"{boss.DisplayName}: 공격이 들어와야 해요");
        }

        [UnityTest]
        public IEnumerator 보스는_멀리_있으면_도약해_오고_원거리_견제기를_써요()
        {
            EnemyBrain.BossKitOverride = 1;
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var boss = director.Spawn(EnemyKind.Ogre, player.transform.position + new Vector3(0f, 0f, 11f));
            ((IMasterBrain)boss).SetZone(player.transform.position + new Vector3(0f, 0f, 5f), new Vector2(40f, 30f));
            bool leapt = false, volley = false;
            System.Action<EnemyBrain, Vector3, Vector3> onLeap = (e, a, b) => leapt |= e == boss;
            System.Action<EnemyBrain, Vector3> onVolley = (e, d) => volley |= e == boss;
            EnemyBrain.BossLeapStarted += onLeap;
            EnemyBrain.BossVolley += onVolley;
            Time.timeScale = 2f;
            float end = Time.time + 20f;
            Vector3 home = player.transform.position;
            var cc = player.GetComponent<CharacterController>();
            while (Time.time < end && !(leapt && volley))
            {
                player.Heal(9999f);
                // 플레이어가 보스에게서 계속 멀어져요 (견제기 · 이동기가 나오게)
                Vector3 away = player.transform.position - boss.transform.position;
                away.y = 0f;
                if (away.magnitude < 9f && away.sqrMagnitude > 1e-4f) cc.Move(away.normalized * 6f * Time.deltaTime);
                yield return null;
            }
            EnemyBrain.BossLeapStarted -= onLeap;
            EnemyBrain.BossVolley -= onVolley;
            EnemyBrain.BossKitOverride = 0;
            Assert.IsTrue(leapt, "멀면 도약해 와요");
            Assert.IsTrue(volley, "멀리서 버티면 원거리 견제기");
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
