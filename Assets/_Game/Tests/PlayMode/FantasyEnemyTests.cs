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

        // 판타지 전용 정예 특성: 마법 방벽 · 저주 · 분노는 판타지 적에게만 (차원 감염이 켜져 있을 때)
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

        [UnityTest]
        public IEnumerator 새_이계_기물도_효과가_들어가요()
        {
            yield return Load();
            var player = Object.FindFirstObjectByType<PlayerController>();
            float before = player.AttackIncreaseTotal;
            player.Items.Add(GameConfig.Items.Find("knight_helm"));
            Assert.Greater(player.AttackIncreaseTotal, before, "기사 투구: 모든 피해");
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

        // 데스나이트: 중간 거리에서 저주 검기 셋, 2페이즈에 들어서면 스켈레톤을 일으켜요
        [UnityTest]
        public IEnumerator 데스나이트는_저주_검기를_날리고_2페이즈에_스켈레톤을_일으켜요()
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
            Assert.Greater(waves, 0, "중간 거리에서 저주 검기");
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            yield return new WaitUntil(() => raised > 0 || Time.time > end + 10f);
            yield return null;
            DeathKnightBrain.CurseWave -= onWave;
            DeathKnightBrain.Raise -= onRaise;
            Time.timeScale = 1f;
            Assert.AreEqual(1, raised, "2페이즈에 죽은 자의 부름");
            Assert.GreaterOrEqual(director.Enemies.Count(e => e.Kind == EnemyKind.Skeleton), 3, "스켈레톤 셋");
        }

        // 다크 엘프 검사: 혈영검마 규칙에 이름이 바뀌고 흡혈이 없어요
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
            Assert.IsTrue(names.Overlaps(new[] { "월광 베기", "그림자 질주", "그림자 걸음" }), string.Join(",", names));
            Assert.LessOrEqual(elf.Damageable.Health, hurt + 0.01f, "흡혈이 없어요");
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
