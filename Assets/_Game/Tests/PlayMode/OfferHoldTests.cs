using System;
using System.Collections;
using System.Linq;
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
    // 기물 고르기 · 이벤트를 Esc로 접으면 넘기지 않고 남아요 (다시 열면 같은 기물 · 같은 창). 포기하면 은자를 받아요
    public class OfferHoldTests : InputTestFixture
    {
        RoomRun _run;
        PrototypeArena _arena;

        public override void Setup()
        {
            base.Setup();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 4242;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            Time.timeScale = 1f;
            if (MetaSave.PathOverride != null && System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            base.TearDown();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _arena = Object.FindFirstObjectByType<PrototypeArena>();
            _run.ChooseFork(0);
        }

        [UnityTest]
        public IEnumerator 접은_기물은_같은_기물로_다시_열리고_포기하면_은자()
        {
            yield return Load();
            _arena.GrantOffer();
            float until = Time.realtimeSinceStartup + 2f;
            while (!_arena.IsChoosing && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(_arena.IsChoosing);
            var ids = _arena.Offer.Select(d => d.id).ToArray();

            _arena.HoldOffer();
            yield return null;
            yield return null;
            Assert.IsFalse(_arena.IsChoosing, "접으면 저절로 다시 열리지 않아요");
            Assert.IsTrue(_arena.HasHeldOffer);
            Assert.AreEqual(1f, Time.timeScale, "접으면 다시 움직여요");

            _arena.ReopenHeldOffer();
            Assert.IsTrue(_arena.IsChoosing);
            CollectionAssert.AreEqual(ids, _arena.Offer.Select(d => d.id).ToArray(), "같은 기물 그대로");

            int silver = _run.Silver;
            _arena.Forfeit();
            yield return null;
            Assert.IsFalse(_arena.IsChoosing);
            Assert.IsFalse(_arena.HasHeldOffer);
            Assert.Greater(_run.Silver, silver, "포기하면 은자");
        }

        [UnityTest]
        public IEnumerator 이벤트는_Esc로_접었다가_같은_창으로_다시()
        {
            yield return Load();
            var room = _run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Combat && !r.Optional);
            room.Kind = FloorRoomKind.Event;
            room.EventId = "peddler";
            _run.Enter(room, 0);
            yield return null;
            Assert.IsTrue(_run.IsInEvent, "이벤트 방에 들어가면 창이 떠요");
            var first = _run.CurrentEvent;
            var keyboard = InputSystem.GetDevice<Keyboard>();
            Press(keyboard.escapeKey);
            yield return null;
            Release(keyboard.escapeKey);
            yield return null;
            Assert.IsFalse(_run.IsInEvent, "Esc로 접어요");
            Assert.IsFalse(room.Cleared, "고르지 않았으니 그대로");
            _run.Enter(room, 0);
            yield return null;
            Assert.IsTrue(_run.IsInEvent);
            Assert.AreSame(first, _run.CurrentEvent, "같은 창 그대로 (진열 · 선택지가 바뀌지 않아요)");
        }
    }
}
