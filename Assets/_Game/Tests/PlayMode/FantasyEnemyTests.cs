using System.Collections;
using System.Linq;
using AThousandDawns.Core;
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
            if (kind == EnemyKind.GoblinShaman) director.Spawn(EnemyKind.Goblin, player.transform.position + new Vector3(2f, 0f, 4f)); // 치유할 동료
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
            EnemyBrain.FireboltShot += blinked; // 화염구: 불화살을 쏴요
            Time.timeScale = 2f;
            float end = Time.time + 14f;
            while (Time.time < end && !used) { player.Heal(9999f); yield return null; }
            EnemyBrain.Dashed -= dashed;
            EnemyBrain.Blinked -= blinked;
            EnemyBrain.FireboltShot -= blinked;
            EnemyBrain.InfectionOverride = 0;
            Assert.IsTrue(used, $"{trait}을(를) 써요");
            if (trait == EliteTrait.Fireball) Assert.AreEqual(0, BloodBurst.Live, "발밑 폭발은 없어요");
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

        // 다시 맞춰지는 동안은 무너뜨린 타격의 경직이 끝나도 걷지 않고, 밀려나지도 않아요
        [UnityTest]
        public IEnumerator 스켈레톤은_다시_맞춰지는_동안_제자리()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var skeleton = (SkeletonBrain)director.Spawn(EnemyKind.Skeleton, player.transform.position + new Vector3(0f, 0f, 3f));
            yield return null;
            skeleton.Damageable.ApplyHit(99999); // 경직을 일으키는 보통 타격
            Assert.IsTrue(skeleton.IsReassembling);
            Vector3 at = skeleton.transform.position;
            skeleton.Displace(new Vector3(1f, 0f, 0f));
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = at + new Vector3(2.5f, 0f, 0f); // 옆으로 옮겨서, 움직이면 돌아서 다가오게
            cc.enabled = true;
            float until = Time.realtimeSinceStartup + 4f;
            while (skeleton.IsReassembling && Time.realtimeSinceStartup < until)
            {
                Vector3 moved = skeleton.transform.position - at;
                moved.y = 0f;
                Assert.Less(moved.magnitude, 0.05f, "다시 맞춰지는 동안은 움직이지 않아요");
                yield return null;
            }
            Assert.IsFalse(skeleton.IsReassembling, "다시 맞춰져요");
        }

        [UnityTest]
        public IEnumerator 고블린_족장과_오우거가_오류_없이_싸움([Values(EnemyKind.GoblinChief, EnemyKind.Ogre, EnemyKind.DarkElf, EnemyKind.DeathKnight)] EnemyKind kind)
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var boss = director.Spawn(kind, player.transform.position + new Vector3(0f, 0f, 3f));
            ((IMasterBrain)boss).SetZone(player.transform.position, new Vector2(30f, 20f));
            Assert.AreEqual(kind == EnemyKind.Ogre || kind == EnemyKind.DeathKnight ? EnemyRank.Boss : EnemyRank.Master, boss.Rank);
            Assert.AreEqual(EnemyFaction.Fantasy, boss.Faction);
            Time.timeScale = 2f;
            float end = Time.time + 16f;
            while (Time.time < end && player.Health >= player.MaxHealth) yield return null;
            Assert.Less(player.Health, player.MaxHealth, $"{boss.DisplayName}: 공격이 들어와야 해요");
        }

        // 적끼리 싸울 때 멀리 있는 플레이어는 맞지 않아요 (슬라임이 무협 적을 덮칠 때 플레이어 체력이 줄던 것)
        [UnityTest]
        public IEnumerator 적끼리_싸울_때_멀리_있는_플레이어는_안_맞아요()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 far = player.transform.position + new Vector3(0f, 0f, 14f);
            director.Spawn(EnemyKind.Slime, far);
            director.Spawn(EnemyKind.Grunt, far + new Vector3(1.5f, 0f, 0f));
            int rivalHits = 0;
            System.Action<EnemyBrain, EnemyBrain, Vector3> on = (a, b, at) => rivalHits++;
            EnemyBrain.RivalHit += on;
            float hp = player.Health;
            Time.timeScale = 2f;
            float end = Time.time + 10f;
            while (Time.time < end && rivalHits < 3) yield return null;
            Time.timeScale = 1f;
            EnemyBrain.RivalHit -= on;
            Assert.Greater(rivalHits, 0, "서로 싸워요");
            Assert.AreEqual(hp, player.Health, "멀리 있는 플레이어는 그대로");
        }

        // 오크가 방패수에게 내려찍을 때 충격파 길 밖의 플레이어는 맞지 않아요 (판정이 노리는 적 기준이라 멀리 있는 플레이어가 맞던 것)
        [UnityTest]
        public IEnumerator 오크가_방패수에게_내려찍어도_길_밖의_플레이어는_안_맞아요()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 at = player.transform.position + new Vector3(0f, 0f, 8f); // 충격파는 옆 (방패수 쪽)으로
            var orc = (OrcBrain)director.Spawn(EnemyKind.Orc, at);
            director.Spawn(EnemyKind.Shield, at + new Vector3(2.5f, 0f, 0f));
            bool slammed = false;
            System.Action<EnemyBrain, EnemyBrain, Vector3> on = (a, b, p) => slammed |= a == orc;
            EnemyBrain.RivalHit += on;
            float hp = player.Health;
            Time.timeScale = 2f;
            float end = Time.time + 12f;
            while (Time.time < end && !slammed) yield return null;
            float until = Time.time + 2f;
            while (Time.time < until && orc.IsSlamming) yield return null; // 충격파가 끝날 때까지
            Time.timeScale = 1f;
            EnemyBrain.RivalHit -= on;
            Assert.IsTrue(slammed, "오크가 방패수에게 내려찍어요");
            Assert.AreEqual(hp, player.Health, "충격파 길 밖의 플레이어는 그대로");
        }

        // 오크: 돌진 대신 땅 내려찍기. 충격파가 일직선으로 뻗어 나가며 앞의 플레이어를 한 번 맞혀요. 오크는 그 자리에 서 있어요
        [UnityTest]
        public IEnumerator 오크는_땅을_내려찍어_일직선_충격파를_보내요()
        {
            EnemyBrain.RivalryEnabled = false;
            try
            {
                yield return Load();
                var director = Object.FindFirstObjectByType<EnemyDirector>();
                var player = Object.FindFirstObjectByType<PlayerController>();
                var orc = (OrcBrain)director.Spawn(EnemyKind.Orc, player.transform.position + new Vector3(0f, 0f, 3.5f));
                Assert.AreEqual("땅 내려찍기", orc.AttackName);
                int waves = 0;
                float length = 0f, speed = 0f;
                Vector3 from = Vector3.zero;
                System.Action<OrcBrain, Vector3, Vector3, float, float, float> on = (o, f, d, l, w, s) =>
                {
                    if (o != orc) return;
                    waves++;
                    from = f;
                    length = l;
                    speed = s;
                };
                OrcBrain.Shockwave += on;
                int hits = 0;
                System.Action<int> hurt = d => hits++;
                player.Hurt += hurt;
                Time.timeScale = 2f;
                float end = Time.time + 10f;
                while (Time.time < end && waves == 0) yield return null;
                Assert.AreEqual(1, waves, "충격파 신호");
                Vector3 orcAt = orc.transform.position;
                float until = Time.time + 2f;
                while (Time.time < until && orc.IsSlamming) yield return null;
                Time.timeScale = 1f;
                OrcBrain.Shockwave -= on;
                player.Hurt -= hurt;
                Assert.Greater(length, 3f, "앞으로 길게 뻗어요");
                Assert.AreEqual(GameConfig.GetOptional("orc.slam_speed", 12f), speed, 0.01f);
                Assert.AreEqual(1, hits, "끝이 지나갈 때 한 번만 맞아요");
                Assert.Less((orc.transform.position - orcAt).magnitude, 0.05f, "내려찍는 동안 오크는 제자리");
                Assert.Less(Vector3.Distance(from, orcAt), 0.05f, "발밑에서 시작해요");
            }
            finally { EnemyBrain.RivalryEnabled = true; }
        }

        // 고블린 주술사: 광폭화 대신 둘레 다친 동료를 치유해요 (다치지 않은 동료 · 자신은 빼요)
        [UnityTest]
        public IEnumerator 고블린_주술사는_다친_동료를_치유해요()
        {
            EnemyBrain.RivalryEnabled = false;
            try
            {
                yield return Load();
                var director = Object.FindFirstObjectByType<EnemyDirector>();
                var player = Object.FindFirstObjectByType<PlayerController>();
                Vector3 at = player.transform.position + new Vector3(0f, 0f, 7f);
                var shaman = (GoblinShamanBrain)director.Spawn(EnemyKind.GoblinShaman, at);
                var hurtOrc = director.Spawn(EnemyKind.Orc, at + new Vector3(2f, 0f, 0f));
                var fullOrc = director.Spawn(EnemyKind.Orc, at + new Vector3(-2f, 0f, 0f));
                hurtOrc.Damageable.ApplyHit(Mathf.RoundToInt(hurtOrc.Damageable.MaxHealth * 0.5f), false);
                shaman.Damageable.ApplyHit(1, false);
                float hp = hurtOrc.Damageable.Health;
                var healed = new System.Collections.Generic.List<EnemyBrain>();
                System.Action<GoblinShamanBrain, EnemyBrain> on = (s, t) => { if (s == shaman) healed.Add(t); };
                GoblinShamanBrain.Healed += on;
                Time.timeScale = 2f;
                float end = Time.time + 14f;
                while (Time.time < end && healed.Count == 0) { player.Heal(9999f); yield return null; }
                Time.timeScale = 1f;
                GoblinShamanBrain.Healed -= on;
                CollectionAssert.Contains(healed, hurtOrc, "다친 동료를 치유해요");
                CollectionAssert.DoesNotContain(healed, fullOrc, "다치지 않은 동료는 건너뛰어요");
                CollectionAssert.DoesNotContain(healed, shaman, "자신은 빼요");
                Assert.AreEqual(hp + hurtOrc.Damageable.MaxHealth * GameConfig.GetOptional("goblinshaman.heal_ratio", 0.15f), hurtOrc.Damageable.Health, 0.6f);
                Assert.IsFalse(hurtOrc.IsHastened, "광폭화는 없어요");
            }
            finally { EnemyBrain.RivalryEnabled = true; }
        }

        // 고블린 주술사 저주 장판: 느려지지 않고, 안에 서 있는 동안 플레이어가 저주 (모든 피해 −20%)에 걸려요
        [UnityTest]
        public IEnumerator 고블린_주술사_장판_안에서는_저주에_걸려요()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var shaman = (GoblinShamanBrain)director.Spawn(EnemyKind.GoblinShaman, player.transform.position + new Vector3(0f, 0f, 6f));
            bool placed = false;
            System.Action<GoblinShamanBrain, Vector3, float> on = (s, at, r) => placed |= s == shaman;
            GoblinShamanBrain.CursePlaced += on;
            Time.timeScale = 2f;
            float end = Time.time + 14f;
            while (Time.time < end && !placed) { player.Heal(9999f); yield return null; }
            for (int i = 0; i < 3; i++) { player.Heal(9999f); yield return null; }
            Time.timeScale = 1f;
            GoblinShamanBrain.CursePlaced -= on;
            Assert.IsTrue(placed, "장판이 깔려요");
            Assert.IsTrue(player.IsCursed, "안에 서 있으면 저주");
            Assert.AreEqual(0f, player.SlowRatio, 0.001f, "느려지지 않아요");
            shaman.Damageable.ApplyHit(99999); // 쓰러져도 장판이 남아 있는 동안은 저주가 이어져요
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(player.IsCursed);
        }

        // 화염구 정예: 발밑 폭발 대신 불화살 한 발. 맞으면 화상 1중첩
        [UnityTest]
        public IEnumerator 화염구_정예의_불화살에_맞으면_화상()
        {
            EnemyBrain.InfectionOverride = 1;
            EnemyBrain.RivalryEnabled = false;
            try
            {
                yield return Load();
                var director = Object.FindFirstObjectByType<EnemyDirector>();
                var player = Object.FindFirstObjectByType<PlayerController>();
                var elite = director.Spawn(EnemyKind.Grunt, player.transform.position + new Vector3(0f, 0f, 9f)); // 사거리 (10칸) 안
                elite.MakeElite(EliteTrait.Fireball);
                EnemyBrain.InfectionOverride = 0;
                bool shot = false;
                System.Action<EnemyBrain, Vector3, Vector3> on = (e, f, d) => shot |= e == elite;
                EnemyBrain.FireboltShot += on;
                float end = Time.time + 12f;
                while (Time.time < end && !shot) { player.Heal(9999f); yield return null; }
                EnemyBrain.FireboltShot -= on;
                Assert.IsTrue(shot, "불화살을 쏴요");
                Assert.IsNotNull(Object.FindFirstObjectByType<EnemyBrain.Firebolt>(), "투사체가 날아가요");
                int burnBefore = player.BurnStacks;
                float until = Time.time + 2f;
                while (Time.time < until && player.BurnStacks <= burnBefore && Object.FindFirstObjectByType<EnemyBrain.Firebolt>() != null) yield return null;
                Assert.Greater(player.BurnStacks, burnBefore, "맞으면 화상 1중첩");
                Assert.AreEqual(0, BloodBurst.Live, "발밑 폭발은 없어요");
            }
            finally
            {
                EnemyBrain.InfectionOverride = 0;
                EnemyBrain.RivalryEnabled = true;
            }
        }

        // 판타지 전용 정예 특성:마법 방벽 · 저주 · 분노는 판타지 적에게만 (차원 감염이 켜져 있을 때)
        [Test]
        public void 판타지_특성은_판타지_적에게만()
        {
            EnemyBrain.InfectionOverride = 1;
            foreach (var t in new[] { EliteTrait.Barrier, EliteTrait.Curse, EliteTrait.Frenzy })
            {
                Assert.IsTrue(EnemyBrain.Allows(EnemyKind.Goblin, t), $"{t}: 판타지 적");
                Assert.IsFalse(EnemyBrain.Allows(EnemyKind.Grunt, t), $"{t}: 무협 적은 아니에요");
            }
            EnemyBrain.InfectionOverride = 0;
            Assert.IsFalse(EnemyBrain.Allows(EnemyKind.Goblin, EliteTrait.Barrier), "꺼져 있으면 없어요");
        }

        [UnityTest]
        public IEnumerator 마법_방벽은_깨져도_다시_둘러요()
        {
            EnemyBrain.InfectionOverride = 1;
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var elite = director.Spawn(EnemyKind.Goblin, player.transform.position + new Vector3(0f, 0f, 9f));
            int raised = 0;
            System.Action<EnemyBrain> on = e => { if (e == elite) raised++; };
            EnemyBrain.BarrierRaised += on;
            elite.MakeElite(EliteTrait.Barrier);
            EnemyBrain.InfectionOverride = 0;
            int ward = elite.Damageable.Ward;
            Assert.Greater(ward, 0, "가호를 둘러요");
            float hp = elite.Damageable.Health;
            elite.Damageable.ApplyHit(ward);
            Assert.AreEqual(0, elite.Damageable.Ward);
            Assert.AreEqual(hp, elite.Damageable.Health, "방벽이 먼저 막아요");
            Time.timeScale = 3f;
            float end = Time.time + 12f;
            while (Time.time < end && elite.Damageable.Ward == 0) { player.Heal(9999f); yield return null; }
            Time.timeScale = 1f;
            EnemyBrain.BarrierRaised -= on;
            Assert.Greater(elite.Damageable.Ward, 0, "잠시 뒤 다시 둘러요");
            Assert.AreEqual(2, raised);
        }

        [UnityTest]
        public IEnumerator 저주_정예에게_맞으면_잠시_모든_피해가_줄어요()
        {
            EnemyBrain.InfectionOverride = 1;
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var elite = director.Spawn(EnemyKind.Goblin, player.transform.position + new Vector3(0f, 0f, 9f));
            elite.MakeElite(EliteTrait.Curse);
            EnemyBrain.InfectionOverride = 0;
            float before = player.AttackIncreaseTotal;
            Assert.IsTrue(player.TryReceiveHit(1f, elite.transform.position, 0f, elite.Damageable));
            Assert.IsTrue(player.IsCursed);
            Assert.AreEqual(before - GameConfig.GetOptional("elite.curse_attack", 0.2f), player.AttackIncreaseTotal, 0.001f);
            elite.Damageable.ApplyHit(99999); // 다시 다가와 저주를 새로 걸지 않게
            float until = Time.realtimeSinceStartup + 10f;
            while (player.IsCursed && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(player.IsCursed, "잠시 뒤 풀려요");
        }

        [UnityTest]
        public IEnumerator 분노_정예는_곁의_적이_쓰러지면_강해져요()
        {
            EnemyBrain.InfectionOverride = 1;
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var elite = director.Spawn(EnemyKind.Goblin, player.transform.position + new Vector3(0f, 0f, 9f));
            elite.MakeElite(EliteTrait.Frenzy);
            EnemyBrain.InfectionOverride = 0;
            var near = director.Spawn(EnemyKind.Goblin, elite.transform.position + new Vector3(2f, 0f, 0f));
            var far = director.Spawn(EnemyKind.Goblin, elite.transform.position + new Vector3(12f, 0f, 0f));
            far.Damageable.ApplyHit(99999);
            Assert.AreEqual(0, elite.FrenzyStacks, "멀면 그대로");
            near.Damageable.ApplyHit(99999);
            Assert.AreEqual(1, elite.FrenzyStacks, "곁의 적이 쓰러지면 한 겹");
            yield return null;
        }

        // 이계 기물: 키워드마다 다섯 개 (혼종 세트를 여러 조합으로)
        [Test]
        public void 이계_기물은_키워드마다_다섯_개()
        {
            var db = GameConfig.Items;
            foreach (var k in new[] { AThousandDawns.Player.Keyword.Gyeonggong, AThousandDawns.Player.Keyword.Bingbaek, AThousandDawns.Player.Keyword.Noejeong, AThousandDawns.Player.Keyword.Geumgang, AThousandDawns.Player.Keyword.Yeolyang, AThousandDawns.Player.Keyword.Ganggi })
                Assert.AreEqual(5, db.Items.Count(d => d.otherworld && d.keyword == k), k.ToString());
        }

        // 이계 기물은 따로 칸 없이 무협 기물과 한 목록에서 섞여 나와요 (보상 · 객잔 · 이벤트)
        [Test]
        public void 보상에는_무협_기물과_이계_기물이_섞여_나와요()
        {
            var rng = new System.Random(7);
            int otherworld = 0, wuxia = 0;
            for (int i = 0; i < 200; i++)
                foreach (var d in AThousandDawns.Items.ItemOffer.Roll(GameConfig.Items.Available, id => false, 3, new[] { 0.4f, 0.3f, 0.2f, 0.1f }, () => (float)rng.NextDouble()))
                    if (d.otherworld) otherworld++; else wuxia++;
            Assert.Greater(otherworld, 0, "이계 기물도 나와요");
            Assert.Greater(wuxia, otherworld, "무협 기물이 더 많아요 (목록 비율대로)");
        }

        // 판타지 보스(오우거 · 데스나이트)는 무너진 왕좌의 방에서 (가운데 300, 0, −160)
        [UnityTest]
        public IEnumerator 판타지_보스는_무너진_왕좌의_방에서([Values(EnemyKind.Ogre, EnemyKind.DeathKnight, EnemyKind.Nokrim)] EnemyKind kind)
        {
            RoomRun.BossOverride = kind;
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
                yield return null;
                var run = Object.FindFirstObjectByType<RoomRun>();
                var arena = Object.FindFirstObjectByType<PrototypeArena>();
                var player = Object.FindFirstObjectByType<PlayerController>();
                run.SetRegionForTest(0);
                run.ChooseFork(0);
                run.EnterRest();
                var cc = player.GetComponent<CharacterController>();
                cc.enabled = false;
                player.transform.position = new Vector3(60f, 0f, 6.6f);
                cc.enabled = true;
                float until = Time.realtimeSinceStartup + 5f;
                while (arena.Boss == null && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsNotNull(arena.Boss);
                Assert.AreEqual(kind, arena.Boss.Kind);
                Vector3 hall = new Vector3(300f, 0f, -160f);
                Vector3 b = arena.Boss.transform.position;
                float d = Vector3.Distance(new Vector3(b.x, 0f, b.z), hall);
                if (kind == EnemyKind.Nokrim) Assert.Greater(d, 30f, "무협 보스는 원래 보스 방");
                else
                {
                    Assert.Less(d, 6f, "판타지 보스는 무너진 왕좌의 방");
                    Assert.Less(Vector3.Distance(new Vector3(player.transform.position.x, 0f, player.transform.position.z), hall), 12f, "플레이어도 그 방으로");
                }
            }
            finally
            {
                RoomRun.BossOverride = null;
            }
        }

        [UnityTest]
        public IEnumerator 새_이계_기물도_효과가_들어가요()
        {
            yield return Load();
            var player = Object.FindFirstObjectByType<PlayerController>();
            float before = player.SkillCooldown;
            var hat = GameConfig.Items.Find("wizard_hat");
            player.Items.Add(hat);
            Assert.AreEqual(before, player.SkillCooldown, 0.0001f, "마법사 고깔모자 (빙백): 늘 줄이지 않고 둔화 · 빙결된 적을 맞힐 때 돌려받아요");
            Assert.AreEqual(Keyword.Bingbaek, hat.keyword);
            float attack = player.AttackIncreaseTotal;
            player.Items.Add(GameConfig.Items.Find("knight_helm"));
            Assert.AreEqual(attack, player.AttackIncreaseTotal, 0.0001f, "기사 투구는 모든 피해가 아니라 정예 · 고수 · 보스에게만");
        }

        // 판타지 졸개 셋 (하피 · 늑대인간 · 가고일): 오류 없이 다가와 공격이 들어와요
        [UnityTest]
        public IEnumerator 하피_늑대인간_가고일이_오류_없이_싸움([Values(EnemyKind.Harpy, EnemyKind.Werewolf, EnemyKind.Gargoyle)] EnemyKind kind)
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var enemy = director.Spawn(kind, player.transform.position + new Vector3(0f, 0f, 5f));
            Assert.AreEqual(EnemyFaction.Fantasy, enemy.Faction);
            Assert.IsTrue(RoomBudget.IsFantasy(kind), "어느 지역에서나 섞여요");
            Time.timeScale = 2f;
            float end = Time.time + 15f;
            while (Time.time < end && player.Health >= player.MaxHealth) yield return null;
            Time.timeScale = 1f;
            Assert.Less(player.Health, player.MaxHealth, $"{enemy.DisplayName}: 공격이 들어와야 해요");
        }

        [UnityTest]
        public IEnumerator 늑대인간은_체력_절반_아래에서_광폭화()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var wolf = (WerewolfBrain)director.Spawn(EnemyKind.Werewolf, player.transform.position + new Vector3(0f, 0f, 6f));
            int enraged = 0;
            System.Action<WerewolfBrain> on = w => enraged++;
            WerewolfBrain.Enraged += on;
            wolf.Damageable.ApplyHit(Mathf.CeilToInt(wolf.Damageable.MaxHealth * 0.3f));
            yield return null;
            Assert.IsFalse(wolf.IsEnraged, "아직");
            wolf.Damageable.ApplyHit(Mathf.CeilToInt(wolf.Damageable.MaxHealth * 0.3f));
            yield return null;
            wolf.Damageable.ApplyHit(1);
            yield return null;
            WerewolfBrain.Enraged -= on;
            Assert.IsTrue(wolf.IsEnraged, "절반 아래");
            Assert.AreEqual(1, enraged, "한 번만");
        }

        [UnityTest]
        public IEnumerator 가고일은_할퀸_뒤_석화해_피해를_덜_받아요()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var gargoyle = (GargoyleBrain)director.Spawn(EnemyKind.Gargoyle, player.transform.position + new Vector3(0f, 0f, 2.5f));
            Assert.AreEqual(1f, gargoyle.Damageable.ResolveSituational(player.transform.position, AThousandDawns.Combat.HitTag.Basic), "평소엔 그대로");
            bool stoned = false;
            System.Action<GargoyleBrain, bool> on = (g, s) => stoned |= s;
            GargoyleBrain.StoneChanged += on;
            float end = Time.time + 10f;
            while (Time.time < end && !gargoyle.IsStone)
            {
                player.Heal(9999f);
                yield return null;
            }
            GargoyleBrain.StoneChanged -= on;
            Assert.IsTrue(gargoyle.IsStone, "할퀸 뒤 석화");
            Assert.IsTrue(stoned);
            Assert.AreEqual(GameConfig.GetOptional("gargoyle.stone_ratio", 0.3f), gargoyle.Damageable.ResolveSituational(player.transform.position, AThousandDawns.Combat.HitTag.Basic), 0.001f, "석화 중 피해 30%");
            Vector3 at = gargoyle.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector3.Distance(at, gargoyle.transform.position), 0.05f, "굳어 있어요");
        }

        // 휘두르기 궤적 신호: 고수의 부채꼴 근접 판정마다 한 번
        [UnityTest]
        public IEnumerator 고수가_휘두르면_궤적_신호가_와요()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var master = director.Spawn(EnemyKind.IronFist, player.transform.position + new Vector3(0f, 0f, 2f));
            ((IMasterBrain)master).SetZone(player.transform.position, new Vector2(30f, 20f));
            int swings = 0;
            float radius = 0f;
            System.Action<EnemyBrain, Vector3, Vector3, float, float> on = (e, at, dir, r, a) => { if (e == master) { swings++; radius = r; } };
            EnemyBrain.Swung += on;
            float end = Time.time + 12f;
            while (Time.time < end && swings == 0)
            {
                player.Heal(9999f);
                yield return null;
            }
            EnemyBrain.Swung -= on;
            Assert.Greater(swings, 0, "주먹 2연타마다 신호");
            Assert.Greater(radius, 0f);
        }

        // 데스나이트: 중간 거리에서 망령 기병 돌격 (줄 셋), 2페이즈에 들어서면 스켈레톤을 일으켜요
        [UnityTest]
        public IEnumerator 데스나이트는_망령_기병을_달리게_하고_2페이즈에_스켈레톤을_일으켜요()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var boss = director.Spawn(EnemyKind.DeathKnight, player.transform.position + new Vector3(0f, 0f, 4.5f));
            ((IMasterBrain)boss).SetZone(player.transform.position, new Vector2(30f, 20f));
            Assert.AreEqual("데스나이트", boss.DisplayName);
            int waves = 0, raised = 0;
            System.Action<DeathKnightBrain, Vector3> onWave = (b, d) => waves++;
            System.Action<DeathKnightBrain> onRaise = b => raised++;
            DeathKnightBrain.CurseWave += onWave;
            DeathKnightBrain.Raise += onRaise;
            Time.timeScale = 2f;
            float end = Time.time + 20f;
            var cc = player.GetComponent<CharacterController>();
            while (Time.time < end && waves == 0)
            {
                player.Heal(9999f);
                Vector3 away = player.transform.position - boss.transform.position;
                away.y = 0f;
                if (away.magnitude < 4f && away.sqrMagnitude > 1e-4f) cc.Move(away.normalized * 6f * Time.deltaTime); // 중간 거리 (4~5칸)
                yield return null;
            }
            Assert.Greater(waves, 0, "중간 거리에서 망령 기병 돌격");
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            yield return new WaitUntil(() => raised > 0 || Time.time > end + 10f);
            yield return null;
            DeathKnightBrain.CurseWave -= onWave;
            DeathKnightBrain.Raise -= onRaise;
            Time.timeScale = 1f;
            Assert.AreEqual(1, raised, "2페이즈에 죽은 자의 부름");
            Assert.GreaterOrEqual(director.Enemies.Count(e => e.Kind == EnemyKind.Skeleton), 3, "스켈레톤 셋");
        }

        // 데스나이트: 가까이 붙어 있어도 망령 기병 3갈래를 섞어 쓰고, 2페이즈엔 맞은편 3갈래까지 6줄 (X자)
        [UnityTest]
        public IEnumerator 데스나이트는_가까이서도_망령_기병_2페이즈는_X자()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var boss = (DeathKnightBrain)director.Spawn(EnemyKind.DeathKnight, player.transform.position + new Vector3(0f, 0f, 2f));
            ((IMasterBrain)boss).SetZone(player.transform.position, new Vector2(30f, 20f));
            var dirs = new System.Collections.Generic.List<Vector3>();
            System.Action<DeathKnightBrain, Vector3> onWave = (b, d) => { if (b == boss) dirs.Add(d); };
            DeathKnightBrain.CurseWave += onWave;
            Time.timeScale = 2f;
            float end = Time.time + 12f;
            int most = 0;
            while (Time.time < end && dirs.Count == 0) { player.Heal(9999f); yield return null; }
            Assert.Greater(dirs.Count, 0, "붙어 있어도 망령 기병");
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            dirs.Clear();
            end = Time.time + 20f;
            while (Time.time < end && dirs.Count < 2) { player.Heal(9999f); most = Mathf.Max(most, boss.LanesPending); yield return null; }
            yield return null;
            most = Mathf.Max(most, boss.LanesPending);
            DeathKnightBrain.CurseWave -= onWave;
            Time.timeScale = 1f;
            Assert.AreEqual(2, dirs.Count, "2페이즈는 두 묶음");
            Assert.AreEqual(6, most, "3갈래 × 2");
            Assert.Less(Vector3.Dot(dirs[0], dirs[1]), 0.1f, "맞은편에서 엇갈려요 (X자)");
        }

        // 다크 엘프 검사: 쌍검 난무 · 그림자 숨기 · 독 단검 (흡혈 없음)
        [UnityTest]
        public IEnumerator 다크_엘프_검사는_흡혈_없이_그림자_검술을_써요()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var elf = director.Spawn(EnemyKind.DarkElf, player.transform.position + new Vector3(0f, 0f, 2f));
            ((IMasterBrain)elf).SetZone(player.transform.position, new Vector2(30f, 20f));
            Assert.AreEqual(EnemyRank.Master, elf.Rank);
            Assert.AreEqual("독 단검", elf.VolleyName);
            elf.Damageable.ApplyHit(Mathf.CeilToInt(elf.Damageable.MaxHealth * 0.3f));
            float hurt = elf.Damageable.Health;
            var names = new System.Collections.Generic.HashSet<string>();
            Time.timeScale = 2f;
            float end = Time.time + 12f;
            while (Time.time < end && player.Health >= player.MaxHealth)
            {
                if (elf.AttackName != null) names.Add(elf.AttackName);
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.Less(player.Health, player.MaxHealth, "공격이 들어와요");
            Assert.IsTrue(names.Overlaps(new[] { "쌍검 난무", "그림자 숨기", "그림자 기습", "독 단검" }), string.Join(",", names));
            Assert.LessOrEqual(elf.Damageable.Health, hurt + 0.01f, "흡혈이 없어요");
        }

        // 보스 패턴 개편: 철권 마웅 · 족장 · 오우거 · 데스나이트는 근접 · 먼 거리 수가 서로 겹치지 않아요
        [UnityTest]
        public IEnumerator 철권_계열_보스는_저마다_다른_근접과_먼_거리_수([Values(EnemyKind.IronFist, EnemyKind.GoblinChief, EnemyKind.Ogre, EnemyKind.DeathKnight)] EnemyKind kind)
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var boss = director.Spawn(kind, player.transform.position + new Vector3(0f, 0f, 1.8f));
            ((IMasterBrain)boss).SetZone(player.transform.position, new Vector2(30f, 20f));
            var names = new System.Collections.Generic.HashSet<string>();
            bool grabbed = false, leapt = false;
            System.Action<IronFistBrain, Vector3, Vector3> onGrab = (b, a, d) => grabbed |= b == boss;
            float descendAt = -1f, landAt = -1f;
            bool descendedThere = false;
            System.Action<IronFistBrain, Vector3, Vector3> onLeap = (b, a, c) => { if (b == boss && !leapt) landAt = Time.time; leapt |= b == boss; };
            System.Action<IronFistBrain, Vector3, Vector3, float> onDescend = (b, a, c, t) =>
            {
                if (b != boss || leapt) return;
                descendAt = Time.time;
                Vector3 off = b.transform.position - c;
                off.y = 0f;
                descendedThere = off.magnitude < 0.05f;
            };
            IronFistBrain.Grabbed += onGrab;
            IronFistBrain.FarLeaped += onLeap;
            IronFistBrain.FarLeapDescending += onDescend;
            Time.timeScale = 2f;
            var cc = player.GetComponent<CharacterController>();
            float end = Time.time + 25f, farFrom = Time.time + 8f, nextJump = farFrom;
            Vector3 home = player.transform.position;
            while (Time.time < end && !(names.Count > 0 && (kind == EnemyKind.IronFist ? grabbed : leapt)))
            {
                player.Heal(9999f);
                if (boss.AttackName != null) names.Add(boss.AttackName);
                if (Time.time > nextJump && boss.AttackPhase == EnemyAttackTimeline.Phase.Ready) // 처음엔 붙어 있다가 나중엔 멀리 떨어져요 (먼 거리 수, 벽 구석에 몰리지 않게 옮겨요)
                {
                    nextJump = Time.time + 3f;
                    Vector3 side = boss.transform.position.z > home.z ? Vector3.back : Vector3.forward;
                    cc.enabled = false;
                    player.transform.position = director.FindFreeSpot(boss.transform.position + side * 8f, 0.5f);
                    cc.enabled = true;
                }
                yield return null;
            }
            IronFistBrain.Grabbed -= onGrab;
            IronFistBrain.FarLeaped -= onLeap;
            IronFistBrain.FarLeapDescending -= onDescend;
            Time.timeScale = 1f;
            if (kind != EnemyKind.IronFist && leapt)
            {
                // 몸은 판정 leap_air초 전에 내려앉을 자리로 옮겨 내려오고, 땅에 닿는 순간(FarLeaped) 판정 · 소리가 나요
                float air = GameConfig.GetOptional($"{kind.ToString().ToLowerInvariant()}.leap_air", 0.3f);
                Assert.Greater(descendAt, 0f, "내려앉기 신호가 먼저 와요");
                Assert.IsTrue(descendedThere, "내려앉기 시작할 때 이미 그 자리에 있어요");
                Assert.That(landAt - descendAt, Is.InRange(air * 0.5f, air + 0.2f), "내려앉기 시작 → 땅에 닿기까지 leap_air초");
            }
            string melee = kind switch { EnemyKind.IronFist => "주먹 2연타", EnemyKind.GoblinChief => "철퇴 휘돌리기", EnemyKind.Ogre => "몽둥이 내려찍기", _ => "대검 내려베기" };
            Assert.IsTrue(names.Contains(melee) || names.Contains("잡아 던지기"), string.Join(",", names));
            if (kind == EnemyKind.IronFist) Assert.IsTrue(grabbed, "붙어 있으면 잡아 던지기");
            else Assert.IsTrue(leapt, "멀어지면 뛰어 와요");
        }

        // 다크 엘프 쌍검 난무에 맞으면 독 (체력이 조금씩 줄지만 쓰러지지 않아요)
        [UnityTest]
        public IEnumerator 다크_엘프_쌍검_난무에_맞으면_독()
        {
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var elf = (DarkElfBrain)director.Spawn(EnemyKind.DarkElf, player.transform.position + new Vector3(0f, 0f, 1.6f));
            Time.timeScale = 2f;
            float end = Time.time + 10f;
            while (Time.time < end && !elf.PlayerPoisoned)
            {
                player.Heal(9999f);
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.IsTrue(elf.PlayerPoisoned, "쌍검 난무나 그림자 기습에 맞으면 독");
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
            System.Action<IronFistBrain, Vector3, Vector3> onOwnLeap = (e, a, b) => { leapt |= e == boss; volley |= e == boss; }; // 오우거는 이제 제 땅울림 도약이 먼 거리 대답이에요 (보스 공통기는 시트에서 꺼짐)
            IronFistBrain.FarLeaped += onOwnLeap;
            EnemyBrain.BossLeapStarted += onLeap;
            EnemyBrain.BossVolley += onVolley;
            Time.timeScale = 2f;
            float end = Time.time + 35f; // 오우거는 돌진 · 바위 던지기가 길어서 넉넉히
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
            IronFistBrain.FarLeaped -= onOwnLeap;
            EnemyBrain.BossVolley -= onVolley;
            EnemyBrain.BossKitOverride = 0;
            Assert.IsTrue(leapt, "멀면 도약해 와요");
            Assert.IsTrue(volley, "멀리서 버티면 원거리 견제기");
        }

        // 고수도 약한 판 공통기 (master_kit): 멀면 도약해 오고, 멀리서 버티면 견제 2발
        [UnityTest]
        public IEnumerator 고수도_멀리_있으면_도약해_오고_견제기를_써요()
        {
            EnemyBrain.BossKitOverride = 1;
            yield return Load();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var master = director.Spawn(EnemyKind.Qingzhu, player.transform.position + new Vector3(0f, 0f, 11f));
            Assert.AreEqual(EnemyRank.Master, master.Rank);
            Vector3 zone = player.transform.position + new Vector3(0f, 0f, 5f);
            ((IMasterBrain)master).SetZone(zone, new Vector2(40f, 30f));
            Assert.AreEqual("죽엽비도", master.VolleyName);
            Assert.AreEqual("청죽 경공", master.LeapName);
            bool leapt = false, volley = false;
            System.Action<EnemyBrain, Vector3, Vector3> onLeap = (e, a, b) => leapt |= e == master;
            System.Action<EnemyBrain, Vector3> onVolley = (e, d) => volley |= e == master;
            EnemyBrain.BossLeapStarted += onLeap;
            EnemyBrain.BossVolley += onVolley;
            Time.timeScale = 2f;
            float end = Time.time + 30f;
            var cc = player.GetComponent<CharacterController>();
            while (Time.time < end && !(leapt && volley))
            {
                player.Heal(9999f);
                Vector3 away = player.transform.position - master.transform.position;
                away.y = 0f;
                // 청죽객은 금방 따라붙어요: 붙으면 경기장 반대쪽으로 거리를 벌려요 (멀리서 비도로 버티는 플레이어 흉내)
                if (away.magnitude < 3f && !master.IsLeaping)
                {
                    Vector3 side = master.transform.position - zone;
                    side.y = 0f;
                    cc.enabled = false;
                    player.transform.position = zone - (side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.forward) * 6f;
                    cc.enabled = true;
                }
                else if (away.magnitude < 10f && away.sqrMagnitude > 1e-4f) cc.Move(away.normalized * 6f * Time.deltaTime);
                yield return null;
            }
            EnemyBrain.BossLeapStarted -= onLeap;
            EnemyBrain.BossVolley -= onVolley;
            EnemyBrain.BossKitOverride = 0;
            Time.timeScale = 1f;
            Assert.IsTrue(leapt, "멀면 도약해 와요");
            Assert.IsTrue(volley, "멀리서 버티면 견제기");
            Assert.AreEqual(2, Mathf.RoundToInt(GameConfig.GetOptional("master_kit.volley_count", 2f)), "고수는 2발");
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
