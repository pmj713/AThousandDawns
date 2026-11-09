using System;
using System.Collections;
using System.Linq;
using AThousandDawns.Core;
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
    // 이벤트 방 9개 (첫 버전 6개 + 서툰 대장장이 · 표국 행렬 · 검이 꽂힌 무덤): 선택지가 조건대로 잠기고, 결과가 은자·금창약·추적도·혈자리·전투에 반영돼요.
    public class EventTests : InputTestFixture
    {
        RoomRun _run;
        PrototypeArena _arena;
        PlayerController _player;
        EnemyDirector _director;

        public override void Setup()
        {
            base.Setup();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{System.Guid.NewGuid():N}.json"); // 진짜 저장을 건드리지 않게 (회차 0이라 마을 없이 시작)
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 31337;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            Time.timeScale = 1f;
            if (MetaSave.PathOverride != null && System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
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
        public IEnumerator 표국_호위는_웨이브_3개_뒤_은자_150_짐을_털면_100과_추적도()
        {
            yield return Load();
            Assert.AreEqual(3, _run.PlanRoom(1, 3).Count, "웨이브 3개");
            _run.OpenEventForTest("escort");
            Assert.IsTrue(_run.ChooseEvent(Index("호위한다")));
            Assert.IsFalse(_run.IsInEvent, "창이 닫히고 전투");
            Assert.IsTrue(_run.IsFighting);
            yield return WaitUntil(() =>
            {
                for (int i = _director.Enemies.Count - 1; i >= 0; i--) _director.Enemies[i].Damageable.ApplyHit(99999);
                return !_run.IsFighting;
            }, 30f);
            Assert.IsFalse(_run.IsFighting);
            Assert.GreaterOrEqual(_run.Silver, 150 + 18, "방 은자 + 호위 삯 150");

            yield return Load();
            int tracking = _run.Tracking;
            _run.OpenEventForTest("escort");
            Assert.IsTrue(_run.ChooseEvent(Index("짐을 턴다")));
            Assert.AreEqual(100, _run.Silver);
            Assert.AreEqual(tracking + 3, _run.Tracking);
        }

        [UnityTest]
        public IEnumerator 서툰_대장장이는_삯을_받고_등급을_올리거나_망가뜨림()
        {
            yield return Load();
            _run.OpenEventForTest("smith");
            Assert.AreEqual("은자가 모자라요", _run.CurrentEvent.Choices[Index("기물을 맡긴다")].Locked);
            _run.CloseEvent();

            _run.GiveSilver(60 * 20);
            var common = AThousandDawns.Core.GameConfig.Items.Items.Where(d => d.grade == AThousandDawns.Items.ItemGrade.Common).Take(20).ToList();
            int up = 0, broken = 0;
            foreach (var def in common)
            {
                foreach (var owned in _player.Items.Items.ToList()) _player.Items.Remove(owned);
                var item = _player.Items.Add(def);
                var cell = item.Cell;
                _run.OpenEventForTest("smith");
                Assert.IsTrue(_run.ChooseEvent(Index("기물을 맡긴다")));
                Assert.AreEqual("smith_pick", _run.CurrentEvent.Id);
                Assert.IsTrue(_run.ChooseEvent(0));
                _run.CloseEvent();
                if (_player.Items.Items.Count == 0) { broken++; continue; }
                var now = _player.Items.Items[0];
                Assert.AreEqual(AThousandDawns.Items.ItemGrade.Uncommon, now.Def.grade, "한 단계 위");
                Assert.AreEqual(cell, now.Cell, "같은 자리");
                up++;
            }
            Assert.AreEqual(60 * 20 - 60 * common.Count, _run.Silver, "삯 60씩");
            Assert.Greater(up, 0);
            Assert.Greater(broken, 0);
        }

        [UnityTest]
        public IEnumerator 검이_꽂힌_무덤_망령을_이기면_기물_고르기_절하면_수련치()
        {
            yield return Load();
            _run.OpenEventForTest("tomb");
            Assert.IsTrue(_run.ChooseEvent(Index("검을 뽑는다")));
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
            Assert.AreEqual(1, elites, "망령 (정예) 하나");
            yield return WaitUntil(() => _arena.IsChoosing, 2f);
            Assert.IsTrue(_arena.IsChoosing);
            Assert.IsFalse(_arena.Offer.Any(d => d.grade == AThousandDawns.Items.ItemGrade.Common), "일반은 안 나와요");

            yield return Load();
            int xp = _player.Level.Xp, level = _player.Level.Level;
            _run.OpenEventForTest("tomb");
            Assert.IsTrue(_run.ChooseEvent(Index("절하고 간다")));
            Assert.IsTrue(_player.Level.Level > level || _player.Level.Xp == xp + 10, "수련치 +10");
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
