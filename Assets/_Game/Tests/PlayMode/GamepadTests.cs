using System;
using System.Collections;
using System.Linq;
using AThousandDawns.Core;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AThousandDawns.Tests
{
    // 게임패드로 창 고르기 (십자키 · A 고르기 · B 닫기/상호작용) · 회귀 기록 (판이 끝날 때 쌓여요)
    public class GamepadTests : InputTestFixture
    {
        Gamepad _pad;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _pad = InputSystem.AddDevice<Gamepad>();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            RoomRun.SeedOverride = 31337;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            if (System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            Time.timeScale = 1f;
            base.TearDown();
        }

        IEnumerator Tap(ButtonControl button)
        {
            Press(button);
            yield return null;
            Release(button);
            yield return null;
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 갈림길_이벤트_기물_카드를_패드로_고름()
        {
            yield return Load();
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var player = Object.FindFirstObjectByType<PlayerController>();

            // 갈림길: 좌우 · A
            Assert.IsTrue(run.IsForking);
            yield return Tap(_pad.dpad.right);
            Assert.AreEqual(1, run.ForkCursor, "오른쪽으로 한 칸");
            Assert.IsTrue(Pad.Active, "패드를 쓰면 커서가 보여요");
            yield return Tap(_pad.buttonSouth);
            Assert.IsFalse(run.IsForking, "A로 골라요");

            // 이벤트: 잠긴 선택지는 건너뛰고 A
            run.OpenEventForTest("doctor");
            var view = run.CurrentEvent;
            int first = view.Choices.FindIndex(c => c.Locked == null);
            Assert.AreEqual(first, view.Cursor, "처음 고를 수 있는 선택지에서 시작");
            yield return Tap(_pad.dpad.down);
            Assert.IsNull(view.Choices[view.Cursor].Locked, "잠긴 선택지는 건너뛰어요");
            yield return Tap(_pad.buttonSouth);
            Assert.IsTrue(!run.IsInEvent || run.CurrentEvent != view || view.Result != null, "A로 골라요");
            if (run.IsInEvent && run.CurrentEvent.Result != null) yield return Tap(_pad.buttonSouth);
            if (run.IsInEvent) run.CloseEvent();

            // 기물 카드: 좌우 · A
            int before = player.Items.Items.Count;
            arena.GrantOffer();
            yield return null;
            yield return null;
            Assert.IsTrue(arena.IsChoosing);
            yield return Tap(_pad.dpad.right);
            Assert.AreEqual(1, arena.OfferCursor);
            yield return Tap(_pad.buttonSouth);
            Assert.IsFalse(arena.IsChoosing, "A로 골라요");
            Assert.AreEqual(before + 1, player.Items.Items.Count);
        }

        [UnityTest]
        public IEnumerator 상호작용은_패드_B()
        {
            yield return Load();
            Press(_pad.buttonEast);
            yield return null;
            Assert.IsTrue(KeyBindings.Pressed(Bind.Interact), "B = 상호작용");
            Release(_pad.buttonEast);
            Press(_pad.selectButton);
            yield return null;
            Assert.IsTrue(KeyBindings.Pressed(Bind.Meridian), "Select = 경맥도");
            Release(_pad.selectButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 판이_끝나면_회귀_기록이_쌓임()
        {
            yield return Load();
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var def = GameConfig.Items.Items.First(d => d.grade == AThousandDawns.Items.ItemGrade.Uncommon);
            player.Items.Add(def);
            run.ChooseFork(0);
            run.ShowResult(false);
            var meta = MetaSave.Load();
            Assert.AreEqual(1, meta.recentRuns.Count);
            Assert.IsFalse(meta.recentRuns[0].victory);
            Assert.AreEqual(1, meta.recentRuns[0].run, "제1회");
            Assert.AreEqual(1, meta.deathCauses.Sum(n => n.count), "쓰러뜨린 것 1");
            Assert.IsTrue(meta.itemCounts.Any(n => n.name == def.name), "끝까지 지닌 기물");
            Assert.AreEqual(1, meta.skillCounts.Sum(n => n.count), "쓰던 초식");

            var panel = new HintBookPanel { Tab = 2 };
            panel.Refresh();
            Assert.IsTrue(panel.RecordTab, "회귀 기록 탭");
        }
    }
}
