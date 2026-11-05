using System.Collections;
using System.Linq;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AThousandDawns.Tests
{
    // 시험판 씬을 실제로 띄워서 입력 → 이동 → 평타 → 피해까지 한 번에 확인해요.
    public class PrototypeSceneTests : InputTestFixture
    {
        Keyboard _keyboard;
        Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTest]
        public IEnumerator 시험판_씬에서_이동하고_목인장을_때림()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;

            Assert.IsNotNull(GameConfig.Balance, "GameConfig에 BalanceConfig가 연결되지 않았어요.");
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player, "Player가 없어요.");

            Vector3 start = player.transform.position;
            Press(_keyboard.dKey);
            yield return new WaitForSeconds(0.5f);
            Release(_keyboard.dKey);
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(player.transform.position.x, start.x + 1.5f, "D를 0.5초 누르면 오른쪽으로 움직여야 해요 (초당 6칸).");

            var dummies = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).OrderBy(d => d.transform.position.x).ToArray();
            Assert.AreEqual(2, dummies.Length);

            yield return HitOnce(player, dummies[0]);
            Assert.AreEqual(10, dummies[0].Total, "강인함 0 목인장: 평타 1타 = 10");

            yield return HitOnce(player, dummies[1]);
            Assert.AreEqual(5, dummies[1].Total, "녹림왕 목인장: (10 − 5) × 0.9 = 4.5 → 5");
        }

        IEnumerator HitOnce(PlayerController player, TrainingDummy dummy)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = dummy.transform.position + new Vector3(0f, 0f, -1.2f);
            controller.enabled = true;
            yield return new WaitForSeconds(1.2f); // 카메라가 따라와 멈출 때까지

            Vector3 screen = Camera.main.WorldToScreenPoint(dummy.transform.position);
            Set(_mouse.position, new Vector2(screen.x, screen.y));
            yield return null;

            Press(_mouse.leftButton);
            yield return new WaitForSeconds(0.15f); // 1타 판정(0.08초)은 지나고, 2타로 이어지기 전에 뗌
            Release(_mouse.leftButton);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
