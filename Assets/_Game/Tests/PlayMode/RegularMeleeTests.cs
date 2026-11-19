using System.Collections;
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
    // 일반 근접 적 넷이 저마다 다른 모양으로 쳐요 (플레이테스트 11차):
    //   졸개 내려찍기 (좁고 긴 직선) · 스켈레톤 방패 밀치기 (짧고 넓은 직선 + 큰 넉백) · 고블린 뛰어 내려치기 (내려앉은 자리 원) · 가고일 날개 내려찍기 (제 둘레 원)
    // 예고 뒤 절반 (방향이 굳은 뒤)에 플레이어를 모양 안 · 밖으로 옮겨서 맞고 안 맞는지 봐요.
    public class RegularMeleeTests
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
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        void PlacePlayer(Vector3 flat)
        {
            var cc = _player.GetComponent<CharacterController>();
            cc.enabled = false;
            _player.transform.position = new Vector3(flat.x, _player.transform.position.y, flat.z);
            cc.enabled = true;
        }

        // 예고가 시작되고 앞 절반 (따라오기)이 끝날 때까지. 그때 적이 노리는 방향 = 적 → 가만히 선 플레이어
        IEnumerator WaitLocked(EnemyBrain enemy, string key)
        {
            float end = Time.time + 8f;
            while (Time.time < end && enemy.AttackPhase != EnemyAttackTimeline.Phase.Windup) yield return null;
            Assert.AreEqual(EnemyAttackTimeline.Phase.Windup, enemy.AttackPhase, $"{enemy.DisplayName}: 예고가 시작돼야 해요");
            Assert.AreEqual(_player.MaxHealth, _player.Health, "예고 전엔 안 맞아요");
            yield return new WaitForSeconds(GameConfig.GetOptional($"{key}.windup", 0.6f) * 0.5f + 0.05f);
            Assert.AreEqual(EnemyAttackTimeline.Phase.Windup, enemy.AttackPhase, "아직 예고 뒤 절반");
        }

        int _strikes, _shape = -1;
        Vector3 _strikeAt;

        // 일반 근접 한 방 신호가 올 때까지 (맞는 판정이 다 끝나게 조금 더)
        IEnumerator WaitStrike(EnemyBrain enemy, int shape)
        {
            _strikes = 0;
            _shape = -1;
            System.Action<EnemyBrain, Vector3, Vector3, float, float, int> on = (e, at, dir, length, width, s) =>
            {
                if (e != enemy) return;
                _strikes++;
                _shape = s;
                _strikeAt = at;
            };
            EnemyBrain.RegularStrike += on;
            float end = Time.time + 2f;
            while (Time.time < end && _strikes == 0) yield return null;
            yield return new WaitForSeconds(0.25f);
            EnemyBrain.RegularStrike -= on;
            Assert.Greater(_strikes, 0, $"{enemy.DisplayName}: 한 방 신호가 와요");
            Assert.AreEqual(shape, _shape, "모양");
        }

        [UnityTest]
        public IEnumerator 졸개_내려찍기는_멀리_곧게_닿고_옆은_비어요([Values(true, false)] bool inside)
        {
            yield return Load();
            var grunt = _director.Spawn(EnemyKind.Grunt, _player.transform.position + new Vector3(0f, 0f, 4f));
            Assert.AreEqual("내려찍기", grunt.AttackName);
            yield return WaitLocked(grunt, "grunt");
            Vector3 dir = Flat(_player.transform.position - grunt.transform.position).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 from = Flat(grunt.transform.position);
            PlacePlayer(inside ? from + dir * 2.3f : from + dir * 1f + side * 1.1f); // 길이 2.2 끝 · 옆 1.1칸 (폭 0.9 밖)
            yield return WaitStrike(grunt, LungeSlashBrain.ShapeLine);
            if (inside) Assert.Less(_player.Health, _player.MaxHealth, "곧게 멀리까지 닿아요");
            else Assert.AreEqual(_player.MaxHealth, _player.Health, "좁아서 옆은 안 맞아요");
        }

        [UnityTest]
        public IEnumerator 스켈레톤_방패_밀치기는_짧고_크게_밀어요([Values(true, false)] bool inside)
        {
            yield return Load();
            var skeleton = _director.Spawn(EnemyKind.Skeleton, _player.transform.position + new Vector3(0f, 0f, 4f));
            Assert.AreEqual("방패 밀치기", skeleton.AttackName);
            yield return WaitLocked(skeleton, "skeleton");
            Vector3 dir = Flat(_player.transform.position - skeleton.transform.position).normalized;
            Vector3 from = Flat(skeleton.transform.position);
            PlacePlayer(from + dir * (inside ? 1.2f : 2.3f)); // 길이 1.6 안 · 밖
            Vector3 before = Flat(_player.transform.position);
            yield return WaitStrike(skeleton, LungeSlashBrain.ShapeLine);
            if (!inside)
            {
                Assert.AreEqual(_player.MaxHealth, _player.Health, "짧아서 멀면 안 맞아요");
                yield break;
            }
            Assert.Less(_player.Health, _player.MaxHealth, "밀쳐 맞아요");
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(Vector3.Distance(before, Flat(_player.transform.position)), 1.3f, "기본 넉백 (0.5칸)보다 크게 밀려나요");
        }

        [UnityTest]
        public IEnumerator 고블린은_뛰어서_내려앉은_자리를_내려쳐요([Values(true, false)] bool inside)
        {
            yield return Load();
            var goblin = _director.Spawn(EnemyKind.Goblin, _player.transform.position + new Vector3(0f, 0f, 4.5f));
            Assert.AreEqual("뛰어 내려치기", goblin.AttackName);
            yield return WaitLocked(goblin, "goblin");
            Vector3 dir = Flat(_player.transform.position - goblin.transform.position).normalized;
            Vector3 from = Flat(goblin.transform.position);
            float d = Flat(_player.transform.position - goblin.transform.position).magnitude;
            if (!inside) PlacePlayer(from + dir * (d + 2f)); // 내려앉을 자리에서 2칸 넘게 물러나요
            yield return WaitStrike(goblin, LungeSlashBrain.ShapeRing);
            Assert.Greater(Vector3.Dot(Flat(goblin.transform.position) - from, dir), 0.5f, "앞으로 뛰어 내려앉아요");
            Assert.Less(Vector3.Distance(Flat(_strikeAt), Flat(goblin.transform.position)), 0.2f, "신호는 내려앉은 자리 (내려앉은 뒤 몸끼리 밀어내는 만큼은 허용)");
            if (inside) Assert.Less(_player.Health, _player.MaxHealth, "내려앉은 자리 둘레에 맞아요");
            else Assert.AreEqual(_player.MaxHealth, _player.Health, "물러나면 안 맞아요");
        }

        [UnityTest]
        public IEnumerator 가고일_날개_내려찍기는_뒤까지_둘레를_쳐요([Values(true, false)] bool inside)
        {
            yield return Load();
            var gargoyle = _director.Spawn(EnemyKind.Gargoyle, _player.transform.position + new Vector3(0f, 0f, 3f));
            Assert.AreEqual("날개 내려찍기", gargoyle.AttackName);
            yield return WaitLocked(gargoyle, "gargoyle");
            Vector3 dir = Flat(_player.transform.position - gargoyle.transform.position).normalized;
            Vector3 from = Flat(gargoyle.transform.position);
            PlacePlayer(inside ? from - dir * 1.2f : from + dir * 2.5f); // 등 뒤 1.2칸 · 앞 2.5칸 (반경 1.6 밖)
            yield return WaitStrike(gargoyle, LungeSlashBrain.ShapeRing);
            if (inside) Assert.Less(_player.Health, _player.MaxHealth, "등 뒤도 맞아요");
            else Assert.AreEqual(_player.MaxHealth, _player.Health, "반경 밖은 안 맞아요");
        }

        // 수적 졸개 두 번 베기: 부채꼴 신호가 칼마다 한 번씩 (둘)
        [UnityTest]
        public IEnumerator 수적_졸개는_칼마다_한_방_신호()
        {
            yield return Load();
            var pirate = _director.Spawn(EnemyKind.Pirate, _player.transform.position + new Vector3(0f, 0f, 3f));
            int count = 0, shape = -1;
            float first = -1f, second = -1f;
            System.Action<EnemyBrain, Vector3, Vector3, float, float, int> on = (e, at, dir, length, width, s) =>
            {
                if (e != pirate) return;
                count++;
                shape = s;
                if (count == 1) first = Time.time;
                else if (count == 2) second = Time.time;
            };
            EnemyBrain.RegularStrike += on;
            float end = Time.time + 8f;
            while (Time.time < end && count < 2)
            {
                _player.Heal(9999f);
                yield return null;
            }
            EnemyBrain.RegularStrike -= on;
            Assert.AreEqual(2, count, "칼 둘");
            Assert.AreEqual(LungeSlashBrain.ShapeSector, shape);
            Assert.Less(second - first, 0.6f, "같은 공격의 둘째 칼");
        }
    }
}
