using System;
using System.Collections;
using System.IO;
using System.Linq;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
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
    // 튜토리얼: 첫 판 1층은 손으로 짠 방 6개, 첫 기물은 기 낮은 일반, 2층 갈림길은 기물 옅은·짙은 길 둘. 상황별 첫 안내는 한 번만.
    public class TutorialTests : InputTestFixture
    {
        string _savePath;
        RoomRun _run;
        EnemyDirector _director;
        PlayerController _player;
        Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.TutorialEnabled = true;
        }

        public override void TearDown()
        {
            RoomRun.TutorialEnabled = false;
            MetaSave.PathOverride = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            _player = Object.FindFirstObjectByType<PlayerController>();
        }

        IEnumerator Clear(FloorRoom room)
        {
            _run.Enter(room, 1);
            float end = Time.realtimeSinceStartup + 20f;
            while (_run.IsFighting && Time.realtimeSinceStartup < end)
            {
                for (int i = _director.Enemies.Count - 1; i >= 0; i--) _director.Enemies[i].Damageable.ApplyHit(99999);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator 첫_판_1층은_손으로_짠_방_여섯()
        {
            yield return Load();
            Assert.IsTrue(_run.InTutorialFloor, "갈림길 없이 바로 입문 죽림");
            Assert.IsFalse(_run.IsForking);
            var rooms = _run.Map.Rooms;
            Assert.AreEqual(6, rooms.Count);
            CollectionAssert.AreEqual(new[] { FloorRoomKind.Start, FloorRoomKind.Combat, FloorRoomKind.Combat, FloorRoomKind.Combat, FloorRoomKind.Reward, FloorRoomKind.Gate },
                rooms.Select(r => r.Kind).ToArray());
            StringAssert.Contains("WASD", KeyHint.Plain(_run.Hint), "첫 방: 이동 안내");

            // 방패 방: 방패 산적 하나
            _run.Enter(rooms[2], 1);
            yield return null;
            StringAssert.Contains("대시 공격", KeyHint.Plain(_run.Hint));
            yield return null;
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(1, _director.Enemies.Count);
            Assert.AreEqual(EnemyKind.Shield, _director.Enemies[0].Kind);
            yield return Clear(rooms[2]);

            // 보상 방: 싸움 없이 기 낮은 일반 기물 하나
            int before = _player.Items.Items.Count;
            _run.Enter(rooms[4], 1);
            yield return null;
            Assert.IsFalse(_run.IsFighting, "보상 방은 싸움 없이");
            Assert.AreEqual(before, _player.Items.Items.Count, "들어가기만 해서는 아직");
            StringAssert.Contains("기물 주머니", KeyHint.Plain(_run.Hint));
            _run.OpenPouch(); // 가운데 주머니 앞에서 상호작용
            Assert.AreEqual(before + 1, _player.Items.Items.Count, "첫 기물");
            var first = _player.Items.Items.Last().Def;
            Assert.AreEqual(ItemGrade.Common, first.grade);
            int lowest = GameConfig.Items.Items.Where(d => d.grade == ItemGrade.Common).Min(d => d.maxQi);
            Assert.AreEqual(lowest, first.maxQi, "어디에 놓아도 운기가 쉬운 기 낮은 기물");
            StringAssert.Contains("경맥도", KeyHint.Plain(_run.Hint));

            // 관문을 나가면 통과 기록, 2층 갈림길은 기물 옅은 길 · 짙은 길
            _run.LeaveFloor();
            Assert.IsTrue(MetaSave.Load().tutorialFloorDone);
            Assert.IsTrue(_run.IsForking);
            Assert.AreEqual(2, _run.ForkOptions.Count);
            Assert.IsTrue(_run.ForkOptions.All(p => p.Reward == RoomRun.RewardKind.Item));
            Assert.IsFalse(_run.ForkOptions[0].Dense);
            Assert.IsTrue(_run.ForkOptions[1].Dense);
        }

        [UnityTest]
        public IEnumerator 졸개가_처음_공격하려_하면_멈추고_Space로_대시하며_다시_움직임()
        {
            yield return Load();
            _run.Enter(_run.Map.Rooms[1], 1);
            float end = Time.realtimeSinceStartup + 15f;
            while (!_run.DodgeFrozen && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(_run.DodgeFrozen, "공격 예고에서 멈춰요");
            Assert.AreEqual(0f, Time.timeScale);
            StringAssert.Contains("Space 대시", KeyHint.Plain(_run.Hint));

            yield return null;
            Assert.IsTrue(_run.DodgeFrozen, "누를 때까지 그대로");
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            yield return null;
            Assert.IsFalse(_run.DodgeFrozen);
            Assert.AreEqual(1f, Time.timeScale, "다시 움직여요");
            Assert.IsTrue(_player.IsDashing || _player.Dash.Charges < _player.Dash.MaxCharges, "대시가 나가요");
            StringAssert.Contains("좌클릭 공격", KeyHint.Plain(_run.Hint), "다음은 평타");

            // 한 번만: 다시 예고가 나와도 안 멈춰요
            end = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < end && !_run.DodgeFrozen) yield return null;
            Assert.IsFalse(_run.DodgeFrozen);
        }

        [UnityTest]
        public IEnumerator 절초를_쓰는_동안에는_회피_안내로_멈추지_않음()
        {
            yield return Load();
            _player.SetWeapon(WeaponKind.Dagger); // 만천화우는 시전 뒤에도 3초 동안 내려요
            _run.Enter(_run.Map.Rooms[1], 1);
            yield return new WaitForSeconds(1f); // 졸개가 나올 때까지
            _player.FillGauge();
            Press(_keyboard.qKey);
            yield return null;
            Release(_keyboard.qKey);
            yield return null;
            Assert.IsTrue(_player.IsCastingUltimate || _player.UltimateRaining, "절초");
            float end = Time.realtimeSinceStartup + 3f;
            while ((_player.IsCastingUltimate || _player.UltimateRaining) && Time.realtimeSinceStartup < end)
            {
                Assert.IsFalse(_run.DodgeFrozen, "절초 중엔 멈추지 않아요");
                yield return null;
            }
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 회피_안내는_입문_2번_방에서만()
        {
            yield return Load();
            _run.Enter(_run.Map.Rooms[2], 1); // 방패 산적 방 (2번 방을 건너뜀)
            float end = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < end)
            {
                Assert.IsFalse(_run.DodgeFrozen, "다른 방에선 멈추지 않아요");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator 한_번_통과하면_다음_판_1층은_무작위()
        {
            var meta = new MetaSave { tutorialFloorDone = true };
            meta.Save();
            yield return Load();
            Assert.IsFalse(_run.InTutorialFloor);
            Assert.IsTrue(_run.IsForking, "원래대로 갈림길부터");
        }

        [UnityTest]
        public IEnumerator 상황별_첫_안내는_한_번만()
        {
            yield return Load();
            _run.Notify("test_hint", "처음");
            _run.Notify("test_hint", "두 번째");
            yield return null;
            Assert.AreEqual("처음", _run.HintNotice);
            Assert.Contains("test_hint", MetaSave.Load().seenHints, "회귀해도 기억해요");

            RoomRun.ResetTutorial();
            Assert.IsFalse(MetaSave.Load().tutorialFloorDone);
            Assert.IsEmpty(MetaSave.Load().seenHints, "설정: 튜토리얼 다시 보기");
        }
    }
}
