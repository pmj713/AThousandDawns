using System;
using System.Collections;
using System.IO;
using System.Linq;
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
    // 항아리 (은자 탭 C4: 방당 3)
    public class JarTests : InputTestFixture
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
            RoomRun.SeedOverride = 0;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator 전투_방마다_항아리_3개_깨면_은자_1씩_다시_안_나옴()
        {
            RoomRun.SeedOverride = 5;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            run.ChooseFork(0);
            Assert.AreEqual(0, run.JarsInRoom, "시작 방엔 없어요");

            var room = run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Combat);
            run.Enter(room, -1);
            Assert.AreEqual(3, run.JarsInRoom);
            int silver = run.Silver;
            run.BreakJar(0);
            Assert.AreEqual(silver + 1, run.Silver);
            Assert.AreEqual(2, room.Jars);

            run.Enter(run.Map.Start, -1);
            Assert.AreEqual(0, run.JarsInRoom);
            run.Enter(room, -1);
            Assert.AreEqual(2, run.JarsInRoom, "깬 항아리는 다시 안 나와요");
        }
    }
}
