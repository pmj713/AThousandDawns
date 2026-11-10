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
    // 진전: 그 판 동안 절초 모양 (검 일검·검진·귀원 / 비도 폭우·산화·회류)
    public class JinjeonTests : InputTestFixture
    {
        Keyboard _keyboard;
        PlayerController _player;
        EnemyDirector _director;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            PrototypeArena.AutoSpawn = false;
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            base.TearDown();
        }

        IEnumerator Load(WeaponKind weapon, UltimateForm form)
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            _player.SetWeapon(weapon);
            _player.UltimateForm = form;
        }

        Damageable Spawn(Vector3 offset, float health = 50f)
        {
            var enemy = _director.Spawn(EnemyKind.Grunt, _player.transform.position + offset);
            enemy.Damageable.ScaleMaxHealth(health);
            StatusEffects.For(enemy.Damageable).TryFreeze(20f); // 움직이지 않게
            return enemy.Damageable;
        }

        // 절초를 쓰고 다 내릴 때까지 기다려요. 걸린 시간을 돌려줘요.
        IEnumerator Ultimate(float[] took)
        {
            _player.FillGauge();
            Press(_keyboard.qKey);
            yield return null;
            Release(_keyboard.qKey);
            yield return null;
            Assert.IsTrue(_player.IsCastingUltimate || _player.UltimateRaining, "절초");
            float start = Time.time, end = Time.realtimeSinceStartup + 8f;
            while ((_player.IsCastingUltimate || _player.UltimateRaining) && Time.realtimeSinceStartup < end) yield return null;
            took[0] = Time.time - start;
        }

        static float Lost(Damageable d) => d.MaxHealth - d.Health;

        [UnityTest]
        public IEnumerator 일검은_체력이_가장_많은_적에게_큰_검_한_자루()
        {
            yield return Load(WeaponKind.Sword, UltimateForm.OneSword);
            Assert.AreEqual(UltimateForm.OneSword, _player.ActiveUltimateForm);
            var strong = Spawn(new Vector3(6f, 0f, 0f), 80f);
            var weak = Spawn(new Vector3(-6f, 0f, 0f), 40f);
            var took = new float[1];
            yield return Ultimate(took);
            Assert.Greater(Lost(strong), 0f, "체력이 가장 많은 적");
            Assert.AreEqual(0f, Lost(weak), "멀리 있는 적은 안 맞아요");
        }

        [UnityTest]
        public IEnumerator 검진은_3초_동안_둘레를_돌며_벰()
        {
            yield return Load(WeaponKind.Sword, UltimateForm.Formation);
            var near = Spawn(new Vector3(2.2f, 0f, 0f));
            var far = Spawn(new Vector3(8f, 0f, 0f));
            var took = new float[1];
            yield return Ultimate(took);
            Assert.GreaterOrEqual(took[0], 2.5f, "3초 동안 돌아요");
            Assert.Greater(Lost(near), 0f);
            Assert.AreEqual(0f, Lost(far), "둘레 밖은 안 맞아요");
        }

        [UnityTest]
        public IEnumerator 귀원은_한_번_더_터짐()
        {
            yield return Load(WeaponKind.Sword, UltimateForm.None);
            var plain = Spawn(new Vector3(4f, 0f, 0f));
            var took = new float[1];
            yield return Ultimate(took);
            float normal = Lost(plain);
            plain.ApplyHit(999999);

            _player.UltimateForm = UltimateForm.Echo;
            var echo = Spawn(new Vector3(4f, 0f, 0f));
            yield return Ultimate(took);
            Assert.Greater(Lost(echo), normal * 1.3f, "검마다 60% 더");
        }

        [UnityTest]
        public IEnumerator 회귀록_새_기물_묶음은_기물_6종을_미리_보여줌()
        {
            yield return Load(WeaponKind.Sword, UltimateForm.None);
            var pack1 = RegressionBookScreen.PackItems(AThousandDawns.Core.RegressionBook.Find("item_pack1"));
            var pack2 = RegressionBookScreen.PackItems(AThousandDawns.Core.RegressionBook.Find("item_pack2"));
            Assert.AreEqual(6, pack1.Count);
            Assert.AreEqual(6, pack2.Count);
            Assert.AreEqual("재 항아리", pack1[0].name);
            Assert.IsNull(RegressionBookScreen.PackItems(AThousandDawns.Core.RegressionBook.Find("forge")));
        }

        [UnityTest]
        public IEnumerator 비도_진전은_검일_때_안_붙음()
        {
            yield return Load(WeaponKind.Sword, UltimateForm.Storm);
            Assert.AreEqual(UltimateForm.None, _player.ActiveUltimateForm, "검이면 보통 만검귀종");
            _player.SetWeapon(WeaponKind.Dagger);
            Assert.AreEqual(UltimateForm.Storm, _player.ActiveUltimateForm);
            var rng = new System.Random(3);
            for (int i = 0; i < 20; i++)
            {
                var form = _player.RollUltimateForm(rng);
                Assert.IsTrue(UltimateForms.ForDagger(form), "비도면 비도 진전");
                Assert.AreNotEqual(UltimateForm.Storm, form, "지금 진전은 안 나와요");
            }
        }

        [UnityTest]
        public IEnumerator 산화와_회류는_만천화우보다_세다()
        {
            yield return Load(WeaponKind.Dagger, UltimateForm.None);
            var plain = Spawn(new Vector3(3f, 0f, 0f));
            var took = new float[1];
            yield return Ultimate(took);
            float normal = Lost(plain);
            Assert.Greater(normal, 0f);
            plain.ApplyHit(999999);

            foreach (var form in new[] { UltimateForm.Bloom, UltimateForm.Backflow, UltimateForm.Storm })
            {
                _player.UltimateForm = form;
                var target = Spawn(new Vector3(3f, 0f, 0f));
                yield return Ultimate(took);
                Assert.Greater(Lost(target), 0f, $"{form}");
                if (form != UltimateForm.Storm) Assert.Greater(Lost(target), normal, $"{form}: 한 적만 있으면 더 세요");
                target.ApplyHit(999999);
            }
        }
    }
}
