using System.Collections;
using AThousandDawns.Core;
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
    // 설정에서 바꾼 키가 실제 조작에 쓰여요 (대시 · 경맥도)
    public class KeyRemapTests : InputTestFixture
    {
        Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            PrototypeArena.AutoSpawn = false;
        }

        public override void TearDown()
        {
            KeyBindings.ResetAll();
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator 바꾼_대시_키와_경맥도_키()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            KeyBindings.Set(Bind.Dash, "<Keyboard>/j");
            KeyBindings.Set(Bind.Meridian, "<Keyboard>/m");

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            yield return null;
            Assert.IsFalse(player.IsDashing, "원래 키(Space)로는 대시 안 해요");

            Press(_keyboard.jKey);
            yield return null;
            Release(_keyboard.jKey);
            yield return null;
            Assert.IsTrue(player.IsDashing || player.Dash.Charges < player.Dash.MaxCharges, "바꾼 키(J)로 대시");

            yield return new WaitForSeconds(0.5f);
            var meridian = Object.FindFirstObjectByType<MeridianScreen>();
            Press(_keyboard.mKey);
            yield return null;
            Release(_keyboard.mKey);
            yield return null;
            Assert.IsTrue(meridian.IsOpen, "바꾼 키(M)로 경맥도");
            meridian.Close();
        }
    }
}
