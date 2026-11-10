using System.Collections;
using AThousandDawns.Core;
using AThousandDawns.Fx;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AThousandDawns.Tests
{
    // 수묵 전환 연출을 켜면: 갈림길을 고른 뒤 먹이 덮은 다음에 층이 바뀌고, 두 번 골라도 한 번만
    public class InkTransitionTests
    {
        string _savePath;

        [SetUp]
        public void On()
        {
            _savePath = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{System.Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 4242;
            RoomRun.Transitions = true;
        }

        [TearDown]
        public void Off()
        {
            RoomRun.Transitions = false;
            RoomRun.SeedOverride = 0;
            MetaSave.PathOverride = null;
            if (System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator 갈림길은_먹이_덮은_뒤에_바뀜()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsTrue(run.IsForking);
            run.ChooseFork(0, new Vector2(400f, 300f));
            run.ChooseFork(1); // 연출 중에 또 골라도 무시
            Assert.IsTrue(run.IsForking, "먹이 덮이는 동안은 아직 갈림길");
            Assert.IsTrue(InkTransition.IsPlaying);
            float end = Time.realtimeSinceStartup + 3f;
            while (InkTransition.IsPlaying && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(run.IsForking, "덮인 사이에 층이 바뀌었어요");
            Assert.IsFalse(InkTransition.IsPlaying);
        }
    }
}
