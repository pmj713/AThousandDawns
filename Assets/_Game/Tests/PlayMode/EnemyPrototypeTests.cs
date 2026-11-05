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

        // 넓은 맵: 일반 적 8종 한 마리씩 + 결투장의 녹림왕. 플레이어가 시작 자리에 있으면 아무도 깨어나지 않아요.
        [UnityTest]
        public IEnumerator 넓은_맵에_적_9마리가_가만히_있음()
        {
            PrototypeArena.AutoSpawn = true;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            Assert.AreEqual(9, _director.Enemies.Count);
            Assert.IsNotNull(arena.Boss);

            yield return new WaitForSeconds(1.5f);
            foreach (var e in _director.Enemies) Assert.IsFalse(e.IsAwake, $"{e.DisplayName}은(는) 아직 자고 있어야 해요");
        }

        // 일반 적 8종을 플레이어 둘레에 한꺼번에 깨워서 8초 동안 풀어 둬요. 오류 로그가 하나라도 나오면 테스트가 실패해요.
        [UnityTest]
        public IEnumerator 여덟_종이_오류_없이_싸우고_동시_공격_수를_지킴()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, -3f));
            var spots = new[]
            {
                (EnemyKind.Grunt, new Vector3(-5f, 0f, 1f)), (EnemyKind.Boar, new Vector3(5f, 0f, 1f)),
                (EnemyKind.Archer, new Vector3(0f, 0f, 4f)), (EnemyKind.Hound, new Vector3(-6f, 0f, -6f)),
                (EnemyKind.Shield, new Vector3(6f, 0f, -6f)), (EnemyKind.Horn, new Vector3(8f, 0f, 0f)),
                (EnemyKind.Bomber, new Vector3(-3f, 0f, -8f)), (EnemyKind.Snake, new Vector3(3f, 0f, -8f)),
            };
            foreach (var (kind, position) in spots) _director.Spawn(kind, position);

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
            PlacePlayer(new Vector3(0f, 0f, -12.5f)); // 남쪽 벽 바로 앞 (북쪽 가운데는 결투장 입구라 비어 있음)
            var boar = (BoarBrain)_director.Spawn(EnemyKind.Boar, new Vector3(0f, 0f, -6f));

            yield return WaitUntil(() => boar.IsWallStunned, 6f);
            Assert.IsTrue(boar.IsWallStunned, "벽에 부딪혀 기절해야 해요");
            Assert.AreEqual(85f, _player.Health, "멧돼지 공격 15");
            Assert.Less(boar.transform.position.z, _player.transform.position.z + 0.5f, "플레이어를 뚫고 지나가요");
        }

        // 결투장에 들어가면 녹림왕이 깨어나 입구가 막히고, 체력 50% 아래로 떨어지면 1.5초 뒤 2페이즈 + 불타는 죽림.
        [UnityTest]
        public IEnumerator 녹림왕_결투장_입구가_막히고_2페이즈로_넘어감()
        {
            yield return LoadScene();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var boss = arena.SpawnBoss();
            PlacePlayer(new Vector3(0f, 0f, 13f));

            yield return WaitUntil(() => boss.IsAwake, 2f);
            Assert.IsTrue(boss.IsAwake);
            yield return null;
            var gate = GameObject.Find("BossGate (결계)");
            Assert.IsNotNull(gate, "결계가 켜져 있어야 해요");

            yield return WaitUntil(() => _player.Health < _player.MaxHealth, 8f);
            Assert.Less(_player.Health, _player.MaxHealth, "녹림왕 공격이 들어와야 해요");

            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            Assert.IsTrue(boss.IsTransitioning);
            Assert.IsFalse(boss.Damageable.Targetable, "전환 중엔 무적");
            yield return WaitUntil(() => boss.Phase == 2, 3f);
            Assert.AreEqual(2, boss.Phase);
            Assert.IsNotNull(boss.Grove, "2페이즈에 불타는 죽림");
        }
    }
}
