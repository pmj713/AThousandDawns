using System;
using System.Collections;
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
    // 적 3종을 시험판 씬에 하나씩 불러서 실제로 플레이어를 때리는지 확인해요. 플레이어는 가만히 서 있어요.
    public class EnemyPrototypeTests : InputTestFixture
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
            base.TearDown();
        }

        IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            Assert.IsNotNull(_player);
            Assert.IsNotNull(_director);
            Assert.AreEqual(0, _director.Enemies.Count, "AutoSpawn을 끄면 처음엔 적이 없어야 해요");
        }

        void PlacePlayer(Vector3 position)
        {
            var cc = _player.GetComponent<CharacterController>();
            cc.enabled = false;
            _player.transform.position = position;
            cc.enabled = true;
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator 졸개가_다가와_전진_베기로_8을_줌()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, -3f));
            _director.Spawn(EnemyKind.Grunt, new Vector3(0f, 0f, 0.5f));

            yield return WaitUntil(() => _player.Health < _player.MaxHealth, 6f);
            Assert.AreEqual(92f, _player.Health, "졸개 공격 8, 플레이어 강인함·방어 0");
            Assert.IsTrue(_player.IsInvulnerable, "맞은 뒤 0.6초 무적");
        }

        [UnityTest]
        public IEnumerator 예고_중인_졸개를_쓰러뜨리면_토큰을_돌려놓음()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, -3f));
            var grunt = _director.Spawn(EnemyKind.Grunt, new Vector3(0f, 0f, 0.5f));

            yield return WaitUntil(() => grunt.AttackPhase == EnemyAttackTimeline.Phase.Windup, 5f);
            Assert.AreEqual(EnemyAttackTimeline.Phase.Windup, grunt.AttackPhase);
            Assert.IsTrue(grunt.TelegraphVisible, "예고가 바닥에 보여야 해요");
            Assert.AreEqual(1, _director.MeleeTokens.InUse);

            grunt.Damageable.ApplyHit(30);
            Assert.AreEqual(0, _director.Enemies.Count);
            Assert.AreEqual(0, _director.MeleeTokens.InUse);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(grunt == null, "쓰러진 적은 잠시 뒤 사라져요");
            Assert.AreEqual(100f, _player.Health);
        }

        [UnityTest]
        public IEnumerator 궁수_화살이_가만히_있는_플레이어에게_맞음()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, -3f));
            _director.Spawn(EnemyKind.Archer, new Vector3(0f, 0f, 3f));

            yield return WaitUntil(() => _player.Health < _player.MaxHealth, 6f);
            Assert.AreEqual(92f, _player.Health, "궁수 공격 8");
            Assert.AreEqual(0, Object.FindObjectsByType<Arrow>(FindObjectsSortMode.None).Length, "맞힌 화살은 사라져요");
        }

        // 한 무리(졸개 3, 멧돼지 1, 궁수 1)를 8초 동안 풀어 둬요. 오류 로그가 하나라도 나오면 테스트가 실패해요.
        [UnityTest]
        public IEnumerator 한_무리가_오류_없이_싸우고_동시_공격_수를_지킴()
        {
            PrototypeArena.AutoSpawn = true;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            Assert.AreEqual(5, _director.Enemies.Count);

            int maxMelee = 0, maxRanged = 0;
            float end = Time.time + 8f;
            while (Time.time < end)
            {
                maxMelee = Mathf.Max(maxMelee, _director.MeleeTokens.InUse);
                maxRanged = Mathf.Max(maxRanged, _director.RangedTokens.InUse);
                yield return null;
            }

            Assert.LessOrEqual(maxMelee, 2, "근접 동시 공격 최대 2");
            Assert.LessOrEqual(maxRanged, 1, "원거리 동시 공격 최대 1");
            Assert.Greater(maxMelee, 0, "누군가는 공격했어야 해요");
            Assert.Less(_player.Health, _player.MaxHealth);
        }

        [UnityTest]
        public IEnumerator 멧돼지가_플레이어를_뚫고_벽에_부딪혀_기절()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, 4.5f)); // 북쪽 벽 바로 앞
            var boar = (BoarBrain)_director.Spawn(EnemyKind.Boar, new Vector3(0f, 0f, -2f));

            yield return WaitUntil(() => boar.IsWallStunned, 6f);
            Assert.IsTrue(boar.IsWallStunned, "벽에 부딪혀 기절해야 해요");
            Assert.AreEqual(85f, _player.Health, "멧돼지 공격 15");
            Assert.Greater(boar.transform.position.z, _player.transform.position.z - 0.5f, "플레이어를 뚫고 지나가요");
        }
    }
}
