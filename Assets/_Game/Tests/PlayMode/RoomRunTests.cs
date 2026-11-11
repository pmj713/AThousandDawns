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
    // 판 구조 씬: 갈림길 → 층 탐색 (전투 방을 비우면 은자, 보상 방에서 고른 보상) → 관문 → 다음 갈림길, 마지막엔 정비실 → 녹림왕.
    public class RoomRunTests : InputTestFixture
    {
        RoomRun _run;
        PrototypeArena _arena;
        EnemyDirector _director;
        PlayerController _player;

        public override void Setup()
        {
            base.Setup();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{System.Guid.NewGuid():N}.json"); // 진짜 저장을 건드리지 않게 (회차 0이라 마을 없이 시작)
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            RoomRun.SeedOverride = 12345;
        }

        public override void TearDown()
        {
            RoomRun.SeedOverride = 0;
            Time.timeScale = 1f;
            if (MetaSave.PathOverride != null && System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            base.TearDown();
        }

        // 시간이 멈춰도 (갈림길·기물 고르기) 끝나도록 실제 시간으로 기다려요
        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            // 게임 시간과 실제 시간이 둘 다 지나야 포기해요 (느린 프레임은 deltaTime이 잘려 게임 시간이 늦게 흘러요). 실제 시간 5배가 상한.
            float end = Time.time + timeout, realEnd = Time.realtimeSinceStartup + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && (Time.time < end || Time.realtimeSinceStartup < realEnd) && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _arena = Object.FindFirstObjectByType<PrototypeArena>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
            _player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(_run);
        }

        // 나오는 대로 쓰러뜨려요 (플레이어는 가만히)
        IEnumerator ClearFight()
        {
            yield return WaitUntil(() =>
            {
                for (int i = _director.Enemies.Count - 1; i >= 0; i--)
                    _director.Enemies[i].Damageable.ApplyHit(99999);
                return !_run.IsFighting;
            }, 20f);
            Assert.IsFalse(_run.IsFighting, "웨이브를 다 비워야 해요");
        }

        static int DirectionTo(FloorMap map, FloorRoom from, FloorRoom to) =>
            Enumerable.Range(0, 4).First(d => map.Neighbor(from, d) == to);

        [UnityTest]
        public IEnumerator 갈림길에서_길을_고르면_층_입구에서_시작()
        {
            yield return Load();
            Assert.IsTrue(_run.IsForking, "판을 시작하면 갈림길부터");
            Assert.That(_run.ForkOptions.Count, Is.InRange(2, 3));
            for (int i = 0; i < 20; i++, _run.BeginFork(1))
                Assert.AreEqual(_run.ForkOptions.Count, _run.ForkOptions.Select(p => p.Reward).Distinct().Count(), "같은 보상 종류는 한 갈림길에 한 번만");
            Assert.AreEqual(0f, Time.timeScale, "고르는 동안 시간 정지");
            Assert.AreEqual(1, _run.Floor);

            _run.ChooseFork(0);
            Assert.IsFalse(_run.IsForking);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(RoomRun.Phase.Floor, _run.CurrentPhase);
            Assert.AreEqual(FloorRoomKind.Start, _run.Room.Kind);
            Assert.IsFalse(_run.IsFighting, "시작 방은 싸우지 않아요");
            Assert.Less(Vector3.Distance(_player.transform.position, Vector3.zero), 0.5f, "층 입구는 방 가운데");
        }

        [UnityTest]
        public IEnumerator 전투_방을_비우면_은자_보상_방에서_고른_보상_관문으로_다음_갈림길()
        {
            yield return Load();
            int pick = Enumerable.Range(0, _run.ForkOptions.Count).FirstOrDefault(i => _run.ForkOptions[i].Reward == RoomRun.RewardKind.Item);
            var path = _run.ForkOptions[pick];
            _run.ChooseFork(pick);
            var map = _run.Map;

            // 시작 방 옆 전투 방으로 걸어 들어가요 (통로 끝)
            var start = map.Start;
            int d = Enumerable.Range(0, 4).First(i => map.Neighbor(start, i) != null && map.Neighbor(start, i).HasFight);
            var first = map.Neighbor(start, d);
            var dir = FloorMap.Directions[d];
            var cc = _player.GetComponent<CharacterController>();
            cc.enabled = false;
            _player.transform.position = new Vector3(dir.x * 11.6f, 0f, dir.y * 7.6f);
            cc.enabled = true;
            yield return WaitUntil(() => _run.Room == first, 2f);
            Assert.AreEqual(first, _run.Room, "통로 끝에 서면 옆방으로");
            Assert.IsTrue(_run.IsFighting, "전투 방에 들어가면 문이 잠기고 웨이브");
            Assert.That(_run.WaveCount, Is.InRange(2, 3));

            yield return ClearFight();
            Assert.IsTrue(first.Cleared);
            int silver = (path.Dense ? 27 : 18) + (_run.RoomHasElite ? 15 : 0);
            Assert.AreEqual(silver, _run.Silver, "죽림 전투 방 은자 18 (짙은 길 ×1.5, 정예 +15)");
            Assert.AreEqual(path.Dense, _run.RoomHasElite, "짙은 길이면 방마다 정예 1명");
            if (first.Kind == FloorRoomKind.Reward)
            {
                _run.OpenPouch(); // 기물 주머니
                yield return WaitUntil(() => _arena.IsChoosing, 2f);
                _arena.Choose(0);
            }

            // 보상 방
            var reward = map.Rooms.First(r => r.Kind == FloorRoomKind.Reward);
            if (!reward.Cleared)
            {
                _run.Enter(reward, 0);
                yield return ClearFight();
                Assert.IsTrue(reward.Pouch, "기물 길 보상 방: 비우면 반짝이는 주머니");
                Assert.IsFalse(_arena.IsChoosing, "바로 고르기 창이 뜨지 않아요");
                _run.OpenPouch();
                Assert.IsFalse(reward.Pouch);
                yield return WaitUntil(() => _arena.IsChoosing, 2f);
                Assert.IsTrue(_arena.IsChoosing, "주머니를 열면 기물 3개 중 하나");
                _arena.Choose(0);
            }
            Assert.AreEqual(1, _player.Items.Items.Count);

            // 관문에서 나가면 2층 갈림길
            var gate = map.Rooms.First(r => r.Kind == FloorRoomKind.Gate);
            _run.Enter(gate, 0);
            Assert.IsFalse(_run.IsFighting, "관문 방은 싸우지 않아요");
            Assert.IsFalse(_run.AtGate, "남쪽 입구에 서 있어서 아직 관문 밖");
            cc.enabled = false;
            _player.transform.position = Vector3.zero; // 관문 가운데
            cc.enabled = true;
            Assert.IsTrue(_run.AtGate);
            _run.LeaveFloor();
            Assert.IsTrue(_run.IsForking);
            Assert.AreEqual(2, _run.Floor);
        }

        // 짙은 길: 전투 방마다 정예 1명 (체력 ×3에 짙은 길 +30%), 비우면 은자 18 × 1.5 + 정예 15
        [UnityTest]
        public IEnumerator 짙은_길_전투_방에는_정예가_하나_나옴()
        {
            yield return Load();
            for (int i = 0; i < 30 && !_run.ForkOptions.Any(p => p.Dense); i++) _run.BeginFork(1); // 짙은 길이 나올 때까지 다시 뽑기
            int pick = Enumerable.Range(0, _run.ForkOptions.Count).First(i => _run.ForkOptions[i].Dense);
            _run.ChooseFork(pick);
            var map = _run.Map;
            int d = Enumerable.Range(0, 4).First(i => map.Neighbor(map.Start, i) != null && map.Neighbor(map.Start, i).HasFight);
            _run.Enter(map.Neighbor(map.Start, d), d);
            Assert.IsTrue(_run.RoomHasElite);

            int elites = 0;
            yield return WaitUntil(() =>
            {
                for (int i = _director.Enemies.Count - 1; i >= 0; i--)
                {
                    var e = _director.Enemies[i];
                    if (e.IsElite)
                    {
                        elites++;
                        Assert.AreNotEqual(EliteTrait.None, e.Trait);
                    }
                    e.Damageable.ApplyHit(99999);
                }
                return !_run.IsFighting;
            }, 20f);
            Assert.AreEqual(1, elites, "방마다 정예 1명");
            Assert.AreEqual(27 + 15, _run.Silver);
        }

        // 마지막 층 관문 → 정비실 → 녹림왕 방. 녹림왕을 쓰러뜨리면 기물 하나와 경맥도 6×4.
        [UnityTest]
        public IEnumerator 정비실을_지나_녹림왕을_쓰러뜨리면_경맥도가_넓어짐()
        {
            yield return Load();
            _run.ChooseFork(0);
            _run.EnterRest();
            Assert.AreEqual(RoomRun.Phase.Rest, _run.CurrentPhase);
            _player.FillGauge();

            var cc = _player.GetComponent<CharacterController>();
            cc.enabled = false;
            _player.transform.position = new Vector3(60f, 0f, 6.6f);
            cc.enabled = true;
            yield return WaitUntil(() => _run.CurrentPhase == RoomRun.Phase.Boss, 2f);
            Assert.AreEqual(RoomRun.Phase.Boss, _run.CurrentPhase);
            Assert.AreEqual(0f, _player.Gauge, "정비실에서 채운 기세는 나갈 때 들어올 때 값으로");

            yield return WaitUntil(() => _arena.Boss != null && _arena.Boss.IsAwake, 2f);
            Assert.IsTrue(_arena.Boss.IsAwake, "들어오면 바로 결투");
            var boss = _arena.Boss;
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            yield return WaitUntil(() => _arena.BossInfo.Phase == 2 && !_arena.BossInfo.IsTransitioning, 4f);
            boss.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => _arena.IsChoosing, 3f);
            Assert.IsTrue(_arena.IsChoosing, "녹림왕 보상 기물");
            Assert.IsFalse(_run.IsDone, "죽림 보스는 판 끝이 아니에요");
            Assert.AreEqual(6, _player.Items.Board.Width);
            Assert.AreEqual(50, _run.Silver, "보스 은자 50");

            // 기물을 고르면 폐사찰 1층 갈림길
            _arena.Choose(0);
            yield return WaitUntil(() => _run.IsForking, 2f);
            Assert.AreEqual(1, _run.Region);
            Assert.AreEqual("폐사찰", _run.RegionName);
            Assert.AreEqual(1, _run.Floor);
        }

        // 폐사찰: 적이 지역 배율만큼 세고, 보스를 쓰러뜨리면 판 끝 (승리)
        [UnityTest]
        public IEnumerator 폐사찰은_적이_세고_보스를_쓰러뜨리면_판_끝()
        {
            yield return Load();
            _run.SetRegionForTest(1);
            bool dense = _run.ForkOptions[0].Dense;
            _run.ChooseFork(0);
            Assert.AreEqual("폐사찰", _run.RegionName);
            var room = _run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Combat);
            _run.Enter(room, -1);
            yield return WaitUntil(() => _director.Enemies.Count > 0, 5f);
            var grunt = _director.Enemies.FirstOrDefault(e => e.Kind == EnemyKind.Grunt && !e.IsElite);
            if (grunt != null) Assert.AreEqual(30f * 1.8f * (dense ? 1.3f : 1f), grunt.Damageable.MaxHealth, 0.5f, "졸개 체력 30 × 1.8 (짙은 길이면 × 1.3)");

            _run.EnterRest();
            var cc = _player.GetComponent<CharacterController>();
            cc.enabled = false;
            _player.transform.position = new Vector3(60f, 0f, 6.6f);
            cc.enabled = true;
            yield return WaitUntil(() => _arena.Boss != null && _arena.Boss.IsAwake, 3f);
            var boss = _arena.Boss;
            Assert.IsInstanceOf<BloodBuddhaBrain>(boss, "폐사찰 보스는 혈불");
            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.55f));
            yield return WaitUntil(() => _arena.BossInfo.Phase == 2 && !_arena.BossInfo.IsTransitioning, 4f);
            boss.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => _run.IsDone, 3f);
            Assert.IsTrue(_run.IsDone, "마지막 지역 보스 = 판 끝");
            Assert.AreEqual(5, _player.Items.Board.Height, "경맥도 6×5");
        }
    }
}
