using System.Collections;
using System.IO;
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
    // 기물 도감: 판에서 처음 얻은 기물이 meta.json에 남고, 무공 요결 창의 도감 탭에 보여요.
    public class CodexTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{System.Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 4242;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            MetaSave.PathOverride = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator 처음_얻은_기물은_도감에_남고_회귀해도_그대로()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.ChooseFork(0);

            var defs = GameConfig.Items.Items.Take(2).ToList();
            foreach (var owned in player.Items.Items.ToList()) player.Items.Remove(owned);
            int before = MetaSave.Load().seenItems.Count;

            var first = player.Items.Add(defs[0]);
            Assert.IsTrue(MetaSave.Load().seenItems.Contains(defs[0].id), "바로 저장돼요");
            Assert.AreEqual(1, run.Record.NewItems);

            player.Items.Remove(first);
            player.Items.Add(defs[0]); // 다시 얻어도 한 번만
            Assert.AreEqual(1, run.Record.NewItems);
            player.Items.Add(defs[1]);
            Assert.AreEqual(2, run.Record.NewItems);
            Assert.AreEqual(before + 2, MetaSave.Load().seenItems.Count);

            var panel = new HintBookPanel();
            panel.Refresh();
            Assert.AreEqual(GameConfig.Items.Items.Count, panel.CodexItems.Count, "모든 기물이 목록에 (못 만난 건 ???)");
            Assert.AreEqual(before + 2, panel.SeenCount);
            Assert.IsTrue(panel.Seen(defs[0]));
            Assert.IsFalse(panel.Seen(GameConfig.Items.Items.Last()), "아직 못 만난 기물");
            Assert.IsTrue(HintBook.All.Any(e => e.Id == "codex"), "요결 항목");
        }
    }
}
