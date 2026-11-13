using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    // 플레이 테스트에서 나온 문제들: 던진 적을 쓰러뜨리면 핏덩이도 사라짐 · 상인 대사가 고른 뒤 바뀜 · 레벨이 올라도 경맥도가 저절로 열리지 않음 · 보상 방은 싸움 없이
    public class PlaytestFixTests : InputTestFixture
    {
        string _savePath;
        RoomRun _run;
        PlayerController _player;
        EnemyDirector _director;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 12345;
        }

        public override void TearDown()
        {
            MetaSave.PathOverride = null;
            RoomRun.SeedOverride = 0;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            _run.ChooseFork(0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 던진_혈폭자를_쓰러뜨리면_떨어지던_핏덩이도_사라짐()
        {
            yield return Load();
            var bomber = _director.Spawn(EnemyKind.BloodBomber, _player.transform.position + new Vector3(5f, 0f, 0f));
            yield return null;
            float hp = _player.Health;
            BloodBurst.Create(_director, _player.transform.position, 1.5f, 0.5f, 20f, bomber.Damageable, 0f);
            bomber.Damageable.ApplyHit(99999, false);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(hp, _player.Health, "주인이 쓰러지면 터지지 않아요");

            BloodBurst.Create(_director, _player.transform.position, 1.5f, 0.3f, 20f, null, 0f); // 죽음의 폭발 (주인 없음)은 그대로
            yield return new WaitForSeconds(0.8f);
            Assert.Less(_player.Health, hp);
        }

        [UnityTest]
        public IEnumerator 떠돌이_상인을_거절하면_대사가_바뀜()
        {
            yield return Load();
            _run.OpenEventForTest("peddler");
            string greeting = _run.CurrentEvent.Text;
            int reject = _run.CurrentEvent.Choices.FindIndex(c => c.Label == "거절한다");
            Assert.GreaterOrEqual(reject, 0);
            _run.ChooseEvent(reject);
            Assert.AreNotEqual(greeting, _run.CurrentEvent.Text, "처음 대사가 그대로 남지 않아요");
            StringAssert.Contains("떠났", _run.CurrentEvent.Result);
        }

        [UnityTest]
        public IEnumerator 레벨이_올라도_경맥도는_저절로_열리지_않음()
        {
            yield return Load();
            var meridian = Object.FindFirstObjectByType<MeridianScreen>();
            _player.Level.Add(500);
            Assert.Greater(_player.Level.Pending, 0);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(meridian.IsOpen, "화면 아래 안내만 깜빡여요");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 보상_방은_작고_싸움_없이_가운데_보상을_상호작용으로()
        {
            yield return Load();
            var reward = _run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Reward);
            _run.Enter(reward, 0);
            yield return null;
            Assert.IsFalse(_run.IsFighting);
            Assert.AreEqual(0, _director.Enemies.Count, "적이 나오지 않아요");
            Assert.AreEqual(RoomRun.RoomSizeKind.Small, _run.RoomSize);
            Assert.IsTrue(reward.Pouch);
            var at = _run.PouchObject.transform.position;
            Assert.Less(new Vector2(at.x - _run.RoomCenter.x, at.z - _run.RoomCenter.z).magnitude, 0.1f, "방 가운데");
            Assert.IsFalse(reward.Cleared);
            _run.OpenPouch();
            Assert.IsTrue(reward.Cleared, "받으면 비운 방");
            Assert.IsFalse(reward.Pouch);
        }
    }
}
