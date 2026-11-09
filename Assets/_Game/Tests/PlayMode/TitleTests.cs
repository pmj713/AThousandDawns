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
    // 시작 화면: 저장이 없으면 시작하기 → 프롤로그 → 첫 판, 있으면 이어하기 → 마을.
    public class TitleTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
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

        static IEnumerator LoadTitle()
        {
            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;
        }

        static IEnumerator WaitForRun()
        {
            for (int i = 0; i < 60 && Object.FindFirstObjectByType<RoomRun>() == null; i++) yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator 저장이_없으면_시작하기_프롤로그_뒤_첫_판()
        {
            yield return LoadTitle();
            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsFalse(title.HasSave);
            Assert.AreEqual("시작하기", title.ItemLabel(TitleScreen.Item.Continue));
            Assert.AreEqual(0, title.Selected, "처음부터 첫 버튼이 선택돼 있어요");

            title.Choose(TitleScreen.Item.Continue);
            Assert.AreEqual(TitleScreen.View.Prologue, title.Current);
            for (int i = 0; i < TitleScreen.Prologue.Length; i++) title.NextPage();
            yield return WaitForRun();
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsNotNull(run, "프롤로그가 끝나면 판 구조 씬");
            Assert.IsTrue(run.IsForking, "첫 판은 바로 죽림");
        }

        [UnityTest]
        public IEnumerator 저장이_있으면_이어하기로_마을()
        {
            new MetaSave { runs = 3, memory = 42 }.Save();
            yield return LoadTitle();
            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsTrue(title.HasSave);
            Assert.AreEqual("이어하기", title.ItemLabel(TitleScreen.Item.Continue));
            StringAssert.Contains("제3회 회귀", title.ProgressText);

            title.Choose(TitleScreen.Item.Settings);
            Assert.AreEqual(TitleScreen.View.Settings, title.Current);
            title.Choose(TitleScreen.Item.Continue);
            yield return WaitForRun();
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsNotNull(run);
            Assert.IsTrue(run.InVillage, "회차가 있으면 마을부터");
        }
    }
}
