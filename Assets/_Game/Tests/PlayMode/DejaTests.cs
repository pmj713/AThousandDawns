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
    // 기시감: 회귀 3회 이상부터. 이야기를 들으면 기억 +5와 이야기 조각 (회귀해도 남음), 천마를 물으면 마지막 보스전 기세 50%.
    public class DejaTests : InputTestFixture
    {
        string _savePath;
        RoomRun _run;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 99;
        }

        public override void TearDown()
        {
            MetaSave.PathOverride = null;
            RoomRun.SeedOverride = 0;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        IEnumerator Load(int runs)
        {
            new MetaSave { runs = runs }.Save();
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _run.ChooseFork(0);
        }

        int Index(string start) => _run.CurrentEvent.Choices.FindIndex(c => c.Label.StartsWith(start));

        [UnityTest]
        public IEnumerator 회귀_3회_전에는_안_나옴()
        {
            yield return Load(2);
            for (int i = 0; i < 40; i++)
            {
                _run.BeginFork(1);
                _run.ChooseFork(0);
                foreach (var r in _run.Map.Rooms) Assert.AreNotEqual("deja", r.EventId, "회귀 2회: 기시감 없음");
            }
        }

        [UnityTest]
        public IEnumerator 이야기를_들으면_조각과_기억_천마를_물으면_보스_기세()
        {
            yield return Load(3);
            _run.OpenEventForTest("deja");
            Assert.AreEqual("기시감", _run.CurrentEvent.Title);
            Assert.IsTrue(_run.ChooseEvent(Index("전생의 이야기를 듣는다")));
            StringAssert.Contains(RoomRun.StoryFragments[0], _run.CurrentEvent.Result);
            Assert.AreEqual(1, MetaSave.Load().storyFragments, "조각은 회귀해도 남아요");
            Assert.AreEqual(1, _run.Record.Deja);
            Assert.AreEqual(5, _run.Record.Memory(RunRecord.Weights.FromConfig()), "기억 +5");
            _run.CloseEvent();

            _run.OpenEventForTest("deja");
            Assert.IsTrue(_run.ChooseEvent(Index("천마에 대해 묻는다")));
            Assert.AreEqual(0.5f, _run.BossGauge, 1e-4f);
            _run.CloseEvent();

            _run.OpenEventForTest("deja");
            _run.ChooseEvent(Index("전생의 이야기를 듣는다"));
            StringAssert.Contains(RoomRun.StoryFragments[1], _run.CurrentEvent.Result, "다음 조각");
            Assert.AreEqual(2, MetaSave.Load().storyFragments);
        }
    }
}
