using System;
using System.Collections;
using System.IO;
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
    // 판 중간 저장: 안전한 순간에 자동 저장, 이어하기는 그 시점부터 (층 지도·방·기물·경맥도·은자·추적도·레벨), 판이 끝나면 지워요.
    public class RunSaveTests : InputTestFixture
    {
        string _savePath;
        RoomRun _run;
        PlayerController _player;
        EnemyDirector _director;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            RoomRun.SeedOverride = 4242;
        }

        public override void TearDown()
        {
            RunSave.Delete();
            MetaSave.PathOverride = null;
            RoomRun.SeedOverride = 0;
            RoomRun.ResumeRun = false;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        IEnumerator Load(bool resume)
        {
            RoomRun.ResumeRun = resume;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _run = Object.FindFirstObjectByType<RoomRun>();
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            // 게임 시간과 실제 시간이 둘 다 지나야 포기해요 (느린 프레임은 deltaTime이 잘려 게임 시간이 늦게 흘러요). 실제 시간 5배가 상한.
            float end = Time.time + timeout, realEnd = Time.realtimeSinceStartup + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && (Time.time < end || Time.realtimeSinceStartup < realEnd) && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        [UnityTest]
        public IEnumerator 갈림길에서_저장되고_이어하면_같은_길()
        {
            yield return Load(false);
            Assert.IsTrue(_run.IsForking);
            yield return WaitUntil(() => RunSave.Exists, 2f);
            Assert.IsTrue(RunSave.Exists, "갈림길에서 저장");
            var options = _run.ForkOptions.Select(p => (p.Reward, p.Dense)).ToArray();

            yield return Load(true);
            Assert.IsTrue(_run.IsForking, "이어하기: 갈림길부터");
            CollectionAssert.AreEqual(options, _run.ForkOptions.Select(p => (p.Reward, p.Dense)).ToArray(), "같은 길");
        }

        [UnityTest]
        public IEnumerator 방을_비우면_저장되고_이어하면_지도_기물_경맥도_은자가_그대로()
        {
            yield return Load(false);
            _run.ChooseFork(0);
            var items = _player.Items;
            items.AddDanjeon(new Vector2Int(2, 1));
            var def = GameConfig.Items.Items.First(d => d.grade == AThousandDawns.Items.ItemGrade.Common);
            var item = items.Add(def);
            items.Place(item, new Vector2Int(3, 1));
            _run.GiveSilver(77);
            _run.SetTracking(4);
            _player.Level.Restore(3, 0, 0);

            var room = _run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Combat && !r.Optional);
            _run.Enter(room, 0);
            yield return WaitUntil(() =>
            {
                for (int i = _director.Enemies.Count - 1; i >= 0; i--) _director.Enemies[i].Damageable.ApplyHit(99999);
                return !_run.IsFighting;
            }, 20f);
            yield return WaitUntil(() =>
            {
                var meridian = Object.FindFirstObjectByType<MeridianScreen>(); // 레벨이 오르면 경맥도가 저절로 열려요 (열려 있으면 저장을 미뤄요)
                if (meridian != null && meridian.IsOpen) meridian.Close();
                var s = RunSave.Load();
                return s != null && s.roomX == room.Cell.x && s.roomY == room.Cell.y;
            }, 5f);
            var saved = RunSave.Load();
            Assert.IsNotNull(saved);
            int silver = _run.Silver, tracking = _run.Tracking, rooms = _run.Map.Rooms.Count, cleared = _run.Map.Rooms.Count(r => r.Cleared);
            Assert.AreEqual(5, tracking, "4 + 방을 비워 +1");

            yield return Load(true);
            Assert.AreEqual(RoomRun.Phase.Floor, _run.CurrentPhase);
            Assert.AreEqual(rooms, _run.Map.Rooms.Count, "같은 층 지도");
            Assert.AreEqual(cleared, _run.Map.Rooms.Count(r => r.Cleared), "비운 방도 그대로");
            Assert.AreEqual(room.Cell, _run.Room.Cell, "그 방부터");
            Assert.AreEqual(silver, _run.Silver);
            Assert.AreEqual(tracking, _run.Tracking);
            Assert.IsTrue(_player.Items.Board.IsDanjeon(new Vector2Int(2, 1)));
            var back = _player.Items.Find(def.id);
            Assert.IsNotNull(back, "기물");
            Assert.IsTrue(back.OnBoard);
            Assert.AreEqual(new Vector2Int(3, 1), back.Cell, "같은 칸");
            Assert.GreaterOrEqual(_player.Level.Level, 3, "레벨도");
            Assert.IsFalse(_run.IsFighting, "비운 방이라 싸움 없음");
        }

        [UnityTest]
        public IEnumerator 판이_끝나면_저장을_지우고_시작_화면은_위치를_보여_줌()
        {
            yield return Load(false);
            yield return WaitUntil(() => RunSave.Exists, 2f);
            Assert.IsTrue(RunSave.Exists);
            Assert.AreEqual("죽림 1층 갈림길", RunSave.Load().WhereText);

            yield return SceneManager.LoadSceneAsync("Title", LoadSceneMode.Single);
            yield return null;
            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsTrue(title.HasRunSave);
            Assert.AreEqual("이어하기", title.ItemLabel(TitleScreen.Item.Continue));
            StringAssert.Contains("죽림 1층 갈림길", title.ProgressText);

            yield return Load(true);
            _run.ShowResult(false);
            Assert.IsFalse(RunSave.Exists, "결과 화면: 판 저장을 지워요");
        }
    }
}
