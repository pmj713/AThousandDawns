using System;
using System.Collections;
using System.Linq;
using AThousandDawns.Enemies;
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
    // 이벤트 방 첫 버전 6개: 선택지가 조건대로 잠기고, 결과가 은자·금창약·추적도·혈자리·전투에 반영돼요.
    public class EventTests : InputTestFixture
    {
        RoomRun _run;
        PrototypeArena _arena;
        PlayerController _player;
        EnemyDirector _director;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 31337;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _arena = Object.FindFirstObjectByType<PrototypeArena>();
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            _run.ChooseFork(0);
        }

        int Index(string labelStart) => _run.CurrentEvent.Choices.FindIndex(c => c.Label.StartsWith(labelStart));

        [UnityTest]
        public IEnumerator 떠돌이_의원_은자가_모자라면_잠기고_있으면_금창약()
        {
            yield return Load();
            _run.OpenEventForTest("doctor");
            Assert.IsTrue(_run.IsInEvent);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual("은자가 모자라요", _run.CurrentEvent.Choices[Index("금창약을 산다")].Locked);
            Assert.IsNull(_run.CurrentEvent.Choices.Last().Locked, "지나가기는 늘 고를 수 있어요");
            _run.CloseEvent();

            _run.GiveSilver(100);
            _run.OpenEventForTest("doctor");
            Assert.IsTrue(_run.ChooseEvent(Index("금창약을 산다")));
            Assert.AreEqual(1, _player.Potions);
            Assert.AreEqual(50, _run.Silver);
            Assert.IsNotNull(_run.CurrentEvent.Result);
            _run.CloseEvent();
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 투전판_공력이_모자라면_속임수는_흐리게_기물을_걸면_얻거나_잃음()
        {
            yield return Load();
            _player.Items.Add(AThousandDawns.Core.GameConfig.Items.Items[0]);
            _run.OpenEventForTest("gambler");
            var trick = _run.CurrentEvent.Choices[Index("🔒")];
            StringAssert.StartsWith("공력 4 이상", trick.Locked);
            Assert.IsTrue(_run.ChooseEvent(Index("기물을 건다")));
            Assert.AreEqual("어느 기물을 걸까?", _run.CurrentEvent.Text);
            Assert.IsTrue(_run.ChooseEvent(0));
            int count = _player.Items.Items.Count;
            Assert.IsTrue(count == 0 || count == 2, "지면 잃고, 이기면 같은 등급 하나 더");
            _run.CloseEvent();
        }

        [UnityTest]
        public IEnumerator 붙잡힌_무인_구하면_정예와_싸우고_이기면_희귀_기물()
        {
            yield return Load();
            _run.OpenEventForTest("captive");
            StringAssert.StartsWith("#금강", _run.CurrentEvent.Choices[Index("🔒")].Locked);
            Assert.IsTrue(_run.ChooseEvent(Index("구해 준다")));
            Assert.IsFalse(_run.IsInEvent, "창이 닫히고 전투");
            Assert.IsTrue(_run.IsFighting);
            int elites = 0;
            yield return WaitUntil(() =>
            {
                for (int i = _director.Enemies.Count - 1; i >= 0; i--)
                {
                    if (_director.Enemies[i].IsElite) elites++;
                    _director.Enemies[i].Damageable.ApplyHit(99999);
                }
                return !_run.IsFighting;
            }, 20f);
            Assert.AreEqual(1, elites, "정예 1");
            yield return WaitUntil(() => _arena.IsChoosing, 2f);
            Assert.IsTrue(_arena.IsChoosing);
            Assert.IsTrue(_arena.Offer.All(d => d.grade == AThousandDawns.Items.ItemGrade.Rare), "희귀 기물만");
        }

        [UnityTest]
        public IEnumerator 산신당_공물은_이번_층만_받는_피해_증가()
        {
            yield return Load();
            _run.OpenEventForTest("shrine");
            Assert.IsTrue(_run.ChooseEvent(Index("공물을 챙긴다")));
            Assert.AreEqual(80, _run.Silver);
            Assert.AreEqual(0.15f, _player.RunTakenIncrease, 1e-4f);
            _run.CloseEvent();
            _run.LeaveFloor();
            Assert.AreEqual(0f, _player.RunTakenIncrease, 1e-4f, "층을 떠나면 노여움이 풀려요");
        }

        [UnityTest]
        public IEnumerator 검결과_약방은_추적도와_혈자리_금창약()
        {
            yield return Load();
            _run.OpenEventForTest("scripture");
            Assert.IsTrue(_run.ChooseEvent(Index("깊이 참오한다")));
            Assert.AreEqual(2, _run.Tracking);
            Assert.AreEqual(1, _player.Level.Pending, "혈자리 하나");
            _run.CloseEvent();

            _run.OpenEventForTest("pharmacy");
            Assert.IsTrue(_run.ChooseEvent(Index("샅샅이 뒤진다")));
            Assert.AreEqual(3, _run.Tracking);
            Assert.AreEqual(1, _player.Potions);
            _run.CloseEvent();
        }
    }
}
