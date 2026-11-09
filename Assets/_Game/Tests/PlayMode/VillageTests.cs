using System;
using System.Collections;
using System.IO;
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
    // 마을: 첫 판은 바로 죽림, 회차가 있으면 마을의 객잔 마루에서 시작. 매화나무 = 회귀록, 동굴 = 면벽수련, 어귀 = 출발.
    public class VillageTests : InputTestFixture
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
            RoomRun.SkipVillage = false;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
        }

        static void Place(PlayerController player, Vector3 at)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = at;
            cc.enabled = true;
        }

        [UnityTest]
        public IEnumerator 첫_판은_마을_없이_바로_죽림()
        {
            yield return Load();
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsFalse(run.InVillage);
            Assert.IsTrue(run.IsForking);
        }

        [UnityTest]
        public IEnumerator 회차가_있으면_마을에서_시작하고_어귀로_나가면_판이_시작됨()
        {
            var meta = new MetaSave { runs = 1, memory = 100 };
            meta.Save();
            yield return Load();
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsTrue(run.InVillage, "회차 1: 마을에서 시작");
            Assert.AreEqual(RoomRun.Phase.Village, run.CurrentPhase);
            Assert.IsFalse(run.IsForking);

            // 매화나무 앞: 회귀록
            var center = player.transform.position - new Vector3(-7f, 0f, 0f); // 마루 = 가운데 + (−7, 0, 0)
            Place(player, center + RoomRun.TreeOffset + new Vector3(0.5f, 0f, -0.5f));
            yield return null;
            Assert.AreEqual(RoomRun.VillageSpot.Tree, run.NearSpot);
            run.UseSpot(RoomRun.VillageSpot.Tree);
            Assert.IsTrue(run.Book.IsOpen);
            Assert.IsFalse(run.Book.IsMeditationShown);
            run.Book.Close();
            yield return null;
            Assert.IsTrue(player.enabled, "닫으면 다시 움직여요");

            // 동굴: 면벽수련을 피우기 전엔 안 열려요
            run.UseSpot(RoomRun.VillageSpot.Cave);
            Assert.IsFalse(run.Book.IsOpen);

            // 어귀로 나가면 씬을 다시 불러 바로 갈림길
            var old = run;
            run.Depart();
            for (int i = 0; i < 30 && (old != null || Object.FindFirstObjectByType<RoomRun>() == null); i++) yield return null; // 씬이 바뀔 때까지
            yield return null;
            run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsFalse(run.InVillage);
            Assert.IsTrue(run.IsForking, "출발: 죽림 1층 갈림길");
        }

        [UnityTest]
        public IEnumerator 면벽수련을_피웠으면_동굴에서_열림()
        {
            var meta = new MetaSave { runs = 2, memory = 500 };
            Assert.IsTrue(RegressionBook.Bloom(meta, "meditation"));
            meta.Save();
            yield return Load();
            var run = Object.FindFirstObjectByType<RoomRun>();
            Assert.IsTrue(run.InVillage);
            run.UseSpot(RoomRun.VillageSpot.Cave);
            Assert.IsTrue(run.Book.IsOpen);
            Assert.IsTrue(run.Book.IsMeditationShown, "동굴은 면벽수련 화면으로");
        }
    }
}
