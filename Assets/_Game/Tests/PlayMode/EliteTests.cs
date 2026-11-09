using System;
using System.Collections;
using AThousandDawns.Core;
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
    // 정예 (몬스터 탭 정예 첫 버전): 공통 배율과 특성 셋 (철포삼 · 회춘 · 열화).
    public class EliteTests : InputTestFixture
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

        [Test]
        public void 붙이지_않는_조합()
        {
            Assert.IsFalse(EnemyBrain.Allows(EnemyKind.Shield, EliteTrait.IronShirt), "방패형 + 철포삼");
            Assert.IsFalse(EnemyBrain.Allows(EnemyKind.Bomber, EliteTrait.Rejuvenation), "자폭형 + 회춘");
            Assert.IsFalse(EnemyBrain.Allows(EnemyKind.Nokrim, EliteTrait.Blaze), "보스는 정예가 아님");
            Assert.IsTrue(EnemyBrain.Allows(EnemyKind.Shield, EliteTrait.Blaze));
        }

        [UnityTest]
        public IEnumerator 정예는_체력_3배_이름_앞에_특성_수련치_5()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, -3f));
            var grunt = _director.Spawn(EnemyKind.Grunt, new Vector3(0f, 0f, 4f));
            float baseHp = grunt.Damageable.MaxHealth;
            grunt.MakeElite(EliteTrait.Rejuvenation);
            Assert.IsTrue(grunt.IsElite);
            Assert.AreEqual(baseHp * GameConfig.Get("elite.hp_mult", 3f), grunt.Damageable.MaxHealth, 0.01f);
            Assert.AreEqual(grunt.Damageable.MaxHealth, grunt.Damageable.Health, "가득 찬 채로");
            Assert.AreEqual(EnemyRank.Elite, grunt.Rank);
            StringAssert.StartsWith("회춘 · ", grunt.DisplayName);

            int before = _player.Level.Xp;
            grunt.Damageable.ApplyHit(99999);
            yield return null;
            Assert.AreEqual(before + 5, _player.Level.Xp, "정예 처치 수련치 5");
        }

        [UnityTest]
        public IEnumerator 철포삼은_맞아도_경직되지_않고_강인함_2()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, -12f));
            var normal = _director.Spawn(EnemyKind.Grunt, new Vector3(-3f, 0f, 4f));
            var iron = _director.Spawn(EnemyKind.Grunt, new Vector3(3f, 0f, 4f));
            float baseToughness = iron.Damageable.Toughness;
            iron.MakeElite(EliteTrait.IronShirt);
            Assert.AreEqual(baseToughness + 2f, iron.Damageable.Toughness);
            yield return null;

            normal.Damageable.ApplyHit(1);
            iron.Damageable.ApplyHit(1);
            Assert.IsTrue(normal.IsStunned, "일반 졸개는 경직");
            Assert.IsFalse(iron.IsStunned, "철포삼은 경직 없음");
        }

        [UnityTest]
        public IEnumerator 회춘은_3초_안_맞으면_회복하고_화상_중엔_멈춤()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, -12f));
            var grunt = _director.Spawn(EnemyKind.Grunt, new Vector3(0f, 0f, 6f));
            grunt.MakeElite(EliteTrait.Rejuvenation);
            grunt.Damageable.ApplyHit(60);
            float hurt = grunt.Damageable.Health;

            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(hurt, grunt.Damageable.Health, "3초 전에는 회복하지 않아요");
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(grunt.Damageable.Health, hurt, "3초 뒤부터 회복");

            grunt.Statuses.AddBurn(0.01f, 0); // 피해가 거의 없는 화상
            yield return new WaitForSeconds(0.3f);
            float burning = grunt.Damageable.Health;
            yield return new WaitForSeconds(1f);
            Assert.LessOrEqual(grunt.Damageable.Health, burning, "화상 중에는 회복 안 함");
        }

        [UnityTest]
        public IEnumerator 열화는_맞히면_화상_죽으면_불길()
        {
            yield return LoadScene();
            PlacePlayer(new Vector3(0f, 0f, -3f));
            var grunt = _director.Spawn(EnemyKind.Grunt, new Vector3(0f, 0f, 0.5f));
            grunt.MakeElite(EliteTrait.Blaze);
            Assert.AreEqual(0.5f, grunt.Statuses.BurnTakenMultiplier, "자신이 받는 화상 절반");

            yield return WaitUntil(() => _player.Health < _player.MaxHealth, 6f);
            Assert.Less(_player.Health, _player.MaxHealth);
            Assert.GreaterOrEqual(_player.BurnStacks, 1, "맞을 때마다 화상 1중첩");

            Vector3 at = grunt.transform.position;
            grunt.Damageable.ApplyHit(99999);
            yield return null;
            var fire = Object.FindFirstObjectByType<EnemyFirePatch>();
            Assert.IsNotNull(fire, "죽은 자리에 불길");
            Assert.Less(Vector3.Distance(new Vector3(at.x, 0f, at.z), new Vector3(fire.transform.position.x, 0f, fire.transform.position.z)), 0.1f);
        }
    }
}
