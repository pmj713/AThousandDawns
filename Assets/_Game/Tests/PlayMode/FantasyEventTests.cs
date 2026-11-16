using System;
using System.Collections;
using System.Linq;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
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
    // 판타지 이벤트 4종: 이계 대장간 · 무너진 성당 제단 · 마나 샘 · 봉인된 마검. 판타지가 켜져 있을 때만 이벤트 풀에 섞여요
    public class FantasyEventTests : InputTestFixture
    {
        RoomRun _run;
        PrototypeArena _arena;
        PlayerController _player;
        EnemyDirector _director;

        public override void Setup()
        {
            base.Setup();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 31337;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            RoomRun.ChapelOverride = -1;
            RoomBudget.FantasyOverride = 0;
            Time.timeScale = 1f;
            if (MetaSave.PathOverride != null && System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, realEnd = Time.realtimeSinceStartup + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && (Time.time < end || Time.realtimeSinceStartup < realEnd) && Time.realtimeSinceStartup < hardEnd) yield return null;
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
        public IEnumerator 판타지가_켜져_있을_때만_이벤트_풀에_섞여요()
        {
            yield return Load();
            RoomBudget.FantasyOverride = 0;
            foreach (var id in RoomRun.FantasyEventIds) Assert.IsFalse(_run.EventPoolHas(id), $"{id}: 꺼져 있으면 안 나와요");
            RoomBudget.FantasyOverride = 1;
            foreach (var id in RoomRun.FantasyEventIds) Assert.IsTrue(_run.EventPoolHas(id), $"{id}: 어느 지역에서나");
            foreach (var id in RoomRun.FantasyEventIds)
            {
                _run.OpenEventForTest(id);
                Assert.IsTrue(_run.IsInEvent, id);
                Assert.IsNotEmpty(_run.CurrentEvent.Title);
                Assert.IsTrue(_run.CurrentEvent.Choices.Any(c => c.Locked == null), "대가 없는 선택지도 있어요");
                _run.CloseEvent();
            }
        }

        [UnityTest]
        public IEnumerator 이계_대장간은_같은_키워드_이계_기물로_벼려요()
        {
            yield return Load();
            var db = GameConfig.Items;
            var def = db.Available.First(d => !d.otherworld && db.Available.Any(o => o.otherworld && o.keyword == d.keyword));
            _run.OpenEventForTest("otherworld_smith");
            Assert.AreEqual("은자가 모자라요", _run.CurrentEvent.Choices[Index("기물을 맡긴다")].Locked);
            _run.CloseEvent();

            _player.Items.Add(def);
            int fee = Mathf.RoundToInt(GameConfig.Get("price.forge_fee", 80f));
            _run.GiveSilver(fee + 5);
            int silver = _run.Silver;
            _run.OpenEventForTest("otherworld_smith");
            Assert.IsTrue(_run.ChooseEvent(Index("기물을 맡긴다")));
            Assert.AreEqual("otherworld_smith_pick", _run.CurrentEvent.Id);
            Assert.IsTrue(_run.ChooseEvent(0));
            _run.CloseEvent();
            var now = _player.Items.Items[0];
            Assert.IsTrue(now.Def.otherworld, "이계 기물");
            Assert.AreEqual(def.keyword, now.Def.keyword, "같은 키워드");
            Assert.AreEqual(silver - fee, _run.Silver);
        }

        [UnityTest]
        public IEnumerator 성당_제단_축복은_받는_피해가_줄고_저주는_늘어요()
        {
            yield return Load();
            float bless = GameConfig.GetOptional("event.chapel_bless", 0.2f), curse = GameConfig.GetOptional("event.chapel_curse", 0.15f);
            float before = _player.RunTakenIncrease;
            _run.GiveSilver(200);
            RoomRun.ChapelOverride = 1;
            _run.OpenEventForTest("chapel");
            Assert.IsTrue(_run.ChooseEvent(Index("은자")));
            _run.CloseEvent();
            Assert.AreEqual(before - bless, _player.RunTakenIncrease, 0.001f, "축복");

            yield return Load();
            before = _player.RunTakenIncrease;
            _run.GiveSilver(200);
            RoomRun.ChapelOverride = 0;
            _run.OpenEventForTest("chapel");
            Assert.IsTrue(_run.ChooseEvent(Index("은자")));
            _run.CloseEvent();
            Assert.AreEqual(before + curse, _player.RunTakenIncrease, 0.001f, "저주");
        }

        [UnityTest]
        public IEnumerator 마나_샘은_회복_아니면_이번_층_공력()
        {
            yield return Load();
            int power = _player.Gongryeok;
            _run.OpenEventForTest("mana_spring");
            Assert.IsTrue(_run.ChooseEvent(Index("몸에 붓는다")));
            _run.CloseEvent();
            Assert.AreEqual(power + 1, _player.Gongryeok, "공력 +1");

            yield return Load();
            _player.TryReceiveHit(_player.MaxHealth * 0.6f, _player.transform.position + Vector3.forward, 0f, null);
            float hp = _player.Health;
            _run.OpenEventForTest("mana_spring");
            Assert.IsTrue(_run.ChooseEvent(Index("마신다")));
            _run.CloseEvent();
            Assert.Greater(_player.Health, hp, "회복");
        }

        [UnityTest]
        public IEnumerator 봉인된_마검_뽑으면_이계_정예와_스켈레톤_이기면_기물_고르기()
        {
            yield return Load();
            _run.OpenEventForTest("sealed_blade");
            Assert.IsTrue(_run.ChooseEvent(Index("검을 뽑는다")));
            Assert.IsTrue(_run.IsFighting);
            int orcs = 0, skeletons = 0, elites = 0;
            yield return WaitUntil(() =>
            {
                orcs = Mathf.Max(orcs, _director.Enemies.Count(e => e.Kind == EnemyKind.Orc));
                skeletons = Mathf.Max(skeletons, _director.Enemies.Count(e => e.Kind == EnemyKind.Skeleton));
                elites = Mathf.Max(elites, _director.Enemies.Count(e => e.IsElite));
                for (int i = _director.Enemies.Count - 1; i >= 0; i--) _director.Enemies[i].Damageable.ApplyHit(99999);
                return !_run.IsFighting;
            }, 20f);
            Assert.AreEqual(1, orcs, "이계 정예 오크");
            Assert.AreEqual(1, elites);
            Assert.GreaterOrEqual(skeletons, 2, "스켈레톤 둘");
            yield return WaitUntil(() => _arena.IsChoosing, 2f);
            Assert.IsTrue(_arena.IsChoosing);
            Assert.IsFalse(_arena.Offer.Any(d => d.grade == ItemGrade.Common || d.grade == ItemGrade.Uncommon), "희귀 이상");
        }
    }
}
