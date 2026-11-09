using System;
using System.Collections;
using System.IO;
using AThousandDawns.Core;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AThousandDawns.Tests
{
    // 일시정지: Esc로 열고 닫기, 다른 창이 열려 있으면 안 열림, 시작 화면으로는 한 번 더 눌러 확인.
    public class PauseTests : InputTestFixture
    {
        string _savePath;
        Keyboard _keyboard;
        RoomRun _run;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
        }

        public override void TearDown()
        {
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
            _run.ChooseFork(0);
            yield return null;
        }

        IEnumerator Esc()
        {
            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Esc로_멈추고_다시_Esc로_계속()
        {
            yield return Load();
            Assert.AreEqual(1f, Time.timeScale);
            yield return Esc();
            Assert.IsTrue(_run.IsPaused);
            Assert.AreEqual(0f, Time.timeScale, "시간이 멈춰요");
            yield return Esc();
            Assert.IsFalse(_run.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 다른_창이_열려_있으면_Esc는_그_창을_닫음()
        {
            yield return Load();
            _run.OpenEventForTest("doctor");
            Assert.IsTrue(_run.IsInEvent);
            yield return Esc();
            Assert.IsFalse(_run.IsPaused, "이벤트 창이 있을 땐 일시정지 안 열림");
        }

        [UnityTest]
        public IEnumerator 설정을_열고_시작_화면으로는_한_번_더_눌러야()
        {
            yield return Load();
            _run.OpenPause();
            _run.ChoosePause(RoomRun.PauseItem.Settings);
            Assert.IsTrue(_run.PauseSettingsOpen);
            _run.SetPauseSettings(false);

            _run.ChoosePause(RoomRun.PauseItem.Title);
            Assert.IsTrue(_run.ConfirmTitle, "한 번 더 누르면");
            Assert.IsTrue(_run.IsPaused);
            _run.ChoosePause(RoomRun.PauseItem.Title);
            for (int i = 0; i < 30 && Object.FindFirstObjectByType<TitleScreen>() == null; i++) yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<TitleScreen>(), "시작 화면");
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
