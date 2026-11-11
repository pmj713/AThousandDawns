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
    // 갈림길 보상: 은자 · 수련 · 잡화 · 비급 (처음부터), 기물 길은 갈림길마다 꼭 하나
    public class ForkRewardTests : InputTestFixture
    {
        RoomRun _run;
        PlayerController _player;

        public override void Setup()
        {
            base.Setup();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{System.Guid.NewGuid():N}.json");
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
            _player = Object.FindFirstObjectByType<PlayerController>();
        }

        [UnityTest]
        public IEnumerator 갈림길마다_기물_길이_있고_비도면_비급_길이_없음()
        {
            yield return Load();
            var seen = new System.Collections.Generic.HashSet<RoomRun.RewardKind>();
            for (int i = 0; i < 40; i++)
            {
                _run.BeginFork(1);
                Assert.IsTrue(_run.ForkOptions.Any(p => p.Reward == RoomRun.RewardKind.Item), "기물 길은 꼭");
                Assert.AreEqual(_run.ForkOptions.Count, _run.ForkOptions.Select(p => p.Reward).Distinct().Count(), "같은 보상은 한 번만");
                foreach (var p in _run.ForkOptions) seen.Add(p.Reward);
            }
            foreach (var kind in new[] { RoomRun.RewardKind.Silver, RoomRun.RewardKind.Training, RoomRun.RewardKind.Supply, RoomRun.RewardKind.Heal })
                Assert.IsTrue(seen.Contains(kind), $"{kind}도 나와요 (해금 없이)");
            Assert.IsFalse(seen.Contains(RoomRun.RewardKind.Forge), "대장간은 회귀록 해금 뒤");
            Assert.IsFalse(seen.Contains(RoomRun.RewardKind.Scroll), "비급 길은 없어요 (고수 보상으로만)");

            _player.SetWeapon(WeaponKind.Dagger);
            for (int i = 0; i < 40; i++)
            {
                _run.BeginFork(1);
                Assert.IsFalse(_run.ForkOptions.Any(p => p.Reward == RoomRun.RewardKind.Scroll), "비도는 비급 길 없음");
            }
        }

        [UnityTest]
        public IEnumerator 은자_수련_잡화_보상()
        {
            yield return Load();
            _run.ChooseFork(0);
            int silver = _run.Silver;
            _run.GrantForkRewardForTest(RoomRun.RewardKind.Silver, false);
            Assert.AreEqual(silver + 80, _run.Silver);
            _run.GrantForkRewardForTest(RoomRun.RewardKind.Silver, true);
            Assert.AreEqual(silver + 200, _run.Silver, "짙으면 120");

            int xp = _player.Level.Xp;
            int level = _player.Level.Level;
            _run.GrantForkRewardForTest(RoomRun.RewardKind.Training, false);
            Assert.IsTrue(_player.Level.Level > level || _player.Level.Xp == xp + 25, "수련치 25");

            Assert.AreEqual(0, _player.Potions, "판 시작은 0개");
            _run.GrantForkRewardForTest(RoomRun.RewardKind.Supply, true);
            Assert.AreEqual(2, _player.Potions, "짙으면 금창약 2");
            silver = _run.Silver;
            while (_player.Potions < _player.PotionMax) _player.AddPotion();
            _run.GrantForkRewardForTest(RoomRun.RewardKind.Supply, false);
            Assert.AreEqual(silver + 40, _run.Silver, "가득하면 은자로");
        }

        [UnityTest]
        public IEnumerator 비급_길은_서고에서_고르기()
        {
            yield return Load();
            _run.ChooseFork(0);
            Assert.AreEqual(SkillKind.Spin, _player.Skill);
            _run.GrantForkRewardForTest(RoomRun.RewardKind.Scroll, false);
            Assert.AreEqual("scroll", _run.CurrentEvent.Id);
            Assert.AreEqual(2, _run.CurrentEvent.Choices.Count, "한 권 + 그냥 두기");
            _run.CloseEvent();

            _run.GrantForkRewardForTest(RoomRun.RewardKind.Scroll, true);
            Assert.AreEqual(3, _run.CurrentEvent.Choices.Count, "짙으면 두 권 + 그냥 두기");
            _run.ChooseEvent(0);
            _run.CloseEvent();
            Assert.AreNotEqual(SkillKind.Spin, _player.Skill, "배웠어요");
        }
    }
}
