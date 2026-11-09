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
    // 주머니 (회귀록에서 열기)와 둘 곳이 없을 때 버릴 기물 고르기.
    public class BagTests : InputTestFixture
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
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator 주머니가_없으면_하나를_버리고_받거나_포기()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var items = player.Items;
            items.BagCapacity = 0; // 주머니 없음
            var pool = GameConfig.Items.Items;
            int cells = items.Board.Width * items.Board.Height;
            for (int i = 0; i < cells; i++) Assert.IsNotNull(items.Add(pool[i]));
            Assert.AreEqual(0, items.BagCount);

            Assert.IsNull(items.Add(pool[cells]), "경맥도가 가득하고 주머니가 없으면 바로 못 받아요");
            Assert.AreEqual(pool[cells], items.Pending);
            yield return null;
            Assert.AreEqual(0f, Time.timeScale, "고르는 동안 시간 정지");

            var first = items.Items[0];
            var cell = first.Cell;
            arena.Discard(0);
            Assert.IsNull(items.Pending);
            Assert.IsFalse(items.Has(first.Def.id), "버린 기물은 사라져요");
            Assert.AreEqual(cell, items.Find(pool[cells].id).Cell, "그 자리에 새 기물");
            Assert.AreEqual(cells, items.Items.Count);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);

            Assert.IsNull(items.Add(pool[cells + 1]));
            arena.DeclinePending();
            Assert.IsFalse(items.Has(pool[cells + 1].id), "포기하면 안 받아요");
            Assert.AreEqual(cells, items.Items.Count);
            Assert.IsFalse(items.ToBag(items.Items[0]), "주머니가 없으면 넣을 수 없어요");
        }

        [UnityTest]
        public IEnumerator 회귀록에서_주머니를_열면_판이_4칸으로_시작()
        {
            var meta = new MetaSave { memory = 30 };
            Assert.IsTrue(RegressionBook.Bloom(meta, "bag"));
            meta.Save();
            RoomRun.SeedOverride = 5;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            RoomRun.SeedOverride = 0;
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.AreEqual(4, player.Items.BagCapacity);
        }
    }
}
