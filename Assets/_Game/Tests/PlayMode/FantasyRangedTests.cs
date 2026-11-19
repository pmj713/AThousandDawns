using System.Collections;
using System.Collections.Generic;
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
    // 겹치던 판타지 일반 적 다섯이 저마다 다른 수를 써요 (플레이테스트 11차):
    //   고블린 단검꾼 단검 던지기 (거리 두기) · 늑대인간 할퀴기 3연타 · 슬라임 덮친 자리 점액 · 고블린 궁수 곡사 · 임프 화염구 (가까이서 터지고 화상)
    public class FantasyRangedTests
    {
        PlayerController _player;
        EnemyDirector _director;

        [TearDown]
        public void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
        }

        IEnumerator Load()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            _player = Object.FindFirstObjectByType<PlayerController>();
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            Vector3 d = a - b;
            d.y = 0f;
            return d.magnitude;
        }

        [UnityTest]
        public IEnumerator 고블린_단검꾼은_물러나서_단검을_던져요()
        {
            yield return Load();
            var rogue = (GoblinRogueBrain)_director.Spawn(EnemyKind.GoblinRogue, _player.transform.position + new Vector3(0f, 0f, 2.5f));
            Assert.AreEqual("단검 던지기", rogue.AttackName);
            float throwDistance = -1f;
            System.Action<GoblinRogueBrain, Vector3, Vector3> on = (b, from, dir) =>
            {
                if (b == rogue && throwDistance < 0f) throwDistance = Flat(_player.transform.position, from);
            };
            GoblinRogueBrain.DaggerThrown += on;
            float hp = _player.Health;
            Time.timeScale = 2f;
            float end = Time.time + 14f;
            while (Time.time < end && (throwDistance < 0f || _player.Health >= hp)) yield return null;
            Time.timeScale = 1f;
            GoblinRogueBrain.DaggerThrown -= on;
            Assert.Greater(throwDistance, 3f, "파고들지 않고 물러나서 던져요");
            Assert.Less(_player.Health, hp, "단검이 날아와 맞아요");
        }

        [UnityTest]
        public IEnumerator 늑대인간은_걸어_나가며_세_번_할퀴어요()
        {
            yield return Load();
            var wolf = (WerewolfBrain)_director.Spawn(EnemyKind.Werewolf, _player.transform.position + new Vector3(0f, 0f, 4f));
            Assert.AreEqual("할퀴기 3연타", wolf.AttackName);
            var index = new List<int>();
            var times = new List<float>();
            System.Action<WerewolfBrain, Vector3, Vector3, int> on = (w, at, dir, i) =>
            {
                if (w != wolf) return;
                index.Add(i);
                times.Add(Time.time);
            };
            WerewolfBrain.Clawed += on;
            Time.timeScale = 2f;
            float end = Time.time + 12f;
            while (Time.time < end && index.Count < 3)
            {
                _player.Heal(9999f);
                yield return null;
            }
            Time.timeScale = 1f;
            WerewolfBrain.Clawed -= on;
            Assert.GreaterOrEqual(index.Count, 3, "세 번 할퀴어요");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, index.GetRange(0, 3), "한 공격에 0 · 1 · 2번째");
            float gap = GameConfig.GetOptional("werewolf.claw_gap", 0.25f);
            Assert.AreEqual(gap, times[1] - times[0], 0.1f, "할퀴기 간격");
            Assert.AreEqual(gap, times[2] - times[1], 0.1f, "할퀴기 간격");
        }

        [UnityTest]
        public IEnumerator 슬라임이_덮친_자리에_점액이_남아요()
        {
            yield return Load();
            var slime = (SlimeBrain)_director.Spawn(EnemyKind.Slime, _player.transform.position + new Vector3(0f, 0f, 4f));
            bool made = false;
            float radius = 0f;
            System.Action<SlimeBrain, Vector3, float, float> on = (s, at, r, life) =>
            {
                if (s != slime) return;
                made = true;
                radius = r;
            };
            SlimeBrain.PuddleMade += on;
            Time.timeScale = 2f;
            float end = Time.time + 12f;
            while (Time.time < end && !made)
            {
                _player.Heal(9999f);
                yield return null;
            }
            Time.timeScale = 1f;
            SlimeBrain.PuddleMade -= on;
            Assert.IsTrue(made, "덮친 뒤 점액");
            Assert.AreEqual(GameConfig.GetOptional("slime.puddle_radius", 1f), radius, 1e-4f);
            Assert.Greater(SlimePuddle.Count, 0, "웅덩이가 남아요");
            Assert.IsNotNull(GameObject.Find("점액"));
        }

        [UnityTest]
        public IEnumerator 점액_웅덩이_안에서는_피해_없이_느려지고_나오면_풀려요()
        {
            yield return Load();
            float hp = _player.Health;
            SlimePuddle.Create(_player.transform.position, 1f, 1.5f, 0.35f, _player, null);
            yield return null;
            yield return null;
            Assert.AreEqual(0.35f, _player.SlowRatio, 1e-4f, "안에 있으면 느려져요");
            Assert.AreEqual(hp, _player.Health, "피해는 없어요");

            var cc = _player.GetComponent<CharacterController>();
            cc.enabled = false;
            _player.transform.position += new Vector3(3f, 0f, 0f);
            cc.enabled = true;
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, _player.SlowRatio, 1e-4f, "나오면 곧 풀려요");
            yield return new WaitForSeconds(1.3f);
            Assert.AreEqual(0, SlimePuddle.Count, "지속이 끝나면 사라져요");
        }

        [UnityTest]
        public IEnumerator 고블린_궁수는_원을_보이고_곡사로_떨어뜨려요()
        {
            yield return Load();
            var archer = (GoblinArcherBrain)_director.Spawn(EnemyKind.GoblinArcher, _player.transform.position + new Vector3(0f, 0f, 6f));
            Assert.AreEqual("곡사", archer.AttackName);
            bool lobbed = false, landed = false, hit = false, arrowSeen = false;
            float flight = 0f, launchedAt = 0f, landedAt = 0f;
            Vector3 target = Vector3.zero, landSpot = Vector3.zero;
            System.Action<GoblinArcherBrain, Vector3, Vector3, float> onLob = (b, from, to, time) =>
            {
                if (b != archer || lobbed) return;
                lobbed = true;
                target = to;
                flight = time;
                launchedAt = Time.time;
                arrowSeen = GameObject.Find("LobbedArrow") != null;
            };
            System.Action<LobbedArrow, Vector3, float, bool> onLand = (a, at, r, h) =>
            {
                if (!lobbed || landed) return;
                landed = true;
                hit = h;
                landSpot = at;
                landedAt = Time.time;
            };
            GoblinArcherBrain.Lobbed += onLob;
            GoblinArcherBrain.LobLanded += onLand;
            Time.timeScale = 2f;
            float end = Time.time + 15f;
            while (Time.time < end && !landed) yield return null;
            Time.timeScale = 1f;
            GoblinArcherBrain.Lobbed -= onLob;
            GoblinArcherBrain.LobLanded -= onLand;
            Assert.IsTrue(lobbed, "곡사로 쏴요");
            Assert.IsTrue(arrowSeen, "화살이 날아가요");
            Assert.IsTrue(landed, "떨어져요");
            Assert.AreEqual(GameConfig.GetOptional("goblinarcher.lob_flight", 1f), flight, 1e-4f);
            Assert.AreEqual(flight, landedAt - launchedAt, 0.15f, "날아가는 시간만큼 뒤에 떨어져요");
            Assert.Less(Flat(target, landSpot), 0.01f, "고른 자리에 떨어져요");
            Assert.LessOrEqual(Flat(target, _player.transform.position), GameConfig.GetOptional("goblinarcher.lob_radius", 1f), "가만히 있으면 발밑");
            Assert.IsTrue(hit, "원 안이면 맞아요");
        }

        [UnityTest]
        public IEnumerator 임프_화염구는_가까이서_터지고_화상을_입혀요()
        {
            yield return Load();
            var imp = (ImpBrain)_director.Spawn(EnemyKind.Imp, _player.transform.position + new Vector3(0f, 0f, 6f));
            Assert.AreEqual("화염구", imp.AttackName);
            bool launched = false, exploded = false, hit = false;
            int burn = 0;
            float blastDistance = 99f;
            System.Action<ImpBrain, ImpFireball, Vector3, Vector3> onLaunch = (b, ball, from, dir) => launched |= b == imp && ball != null;
            System.Action<ImpFireball, Vector3, float, bool> onBoom = (ball, at, r, h) =>
            {
                if (!launched || exploded) return;
                exploded = true;
                hit = h;
                burn = _player.BurnStacks;
                blastDistance = Flat(at, _player.transform.position);
            };
            ImpBrain.FireballLaunched += onLaunch;
            ImpBrain.FireballExploded += onBoom;
            Time.timeScale = 2f;
            float end = Time.time + 15f;
            while (Time.time < end && !exploded) yield return null;
            Time.timeScale = 1f;
            ImpBrain.FireballLaunched -= onLaunch;
            ImpBrain.FireballExploded -= onBoom;
            Assert.IsTrue(launched, "화염구를 던져요");
            Assert.IsTrue(exploded, "터져요");
            Assert.LessOrEqual(blastDistance, GameConfig.GetOptional("imp.fireball_trigger", 1.2f) + 0.2f, "플레이어 곁에서 터져요 (몸에 닿기 전에)");
            Assert.IsTrue(hit, "폭발에 맞아요");
            Assert.GreaterOrEqual(burn, 1, "화상 1중첩");
        }

        // 적끼리 싸울 때 멀리 있는 플레이어는 단검 · 곡사 · 화염구에 맞지 않아요
        [UnityTest]
        public IEnumerator 다른_적을_노린_투사체에_멀리_있는_플레이어는_안_맞아요()
        {
            yield return Load();
            Vector3 far = _player.transform.position + new Vector3(0f, 0f, 14f);
            _director.Spawn(EnemyKind.GoblinRogue, far);
            _director.Spawn(EnemyKind.GoblinArcher, far + new Vector3(-1.5f, 0f, 1f));
            _director.Spawn(EnemyKind.Imp, far + new Vector3(1.5f, 0f, 1f));
            _director.Spawn(EnemyKind.Grunt, far + new Vector3(-1f, 0f, -2f));
            _director.Spawn(EnemyKind.Grunt, far + new Vector3(1.5f, 0f, -2f));
            int rivalHits = 0;
            System.Action<EnemyBrain, EnemyBrain, Vector3> on = (a, b, at) => rivalHits++;
            EnemyBrain.RivalHit += on;
            float hp = _player.Health;
            Time.timeScale = 2f;
            float end = Time.time + 10f;
            while (Time.time < end && rivalHits < 2) yield return null; // 무협 적이 쓰러져 플레이어에게 오기 전에 봐요
            Time.timeScale = 1f;
            EnemyBrain.RivalHit -= on;
            Assert.Greater(rivalHits, 0, "서로 싸워요");
            Assert.AreEqual(hp, _player.Health, "멀리 있는 플레이어는 그대로");
        }
    }
}
