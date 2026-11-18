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

        [UnityTest]
        public IEnumerator 싸우는_중에도_공격과_비도로_깨짐()
        {
            RoomRun.SeedOverride = 5;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<AThousandDawns.Player.PlayerController>();
            run.ChooseFork(0);
            var room = run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Combat);
            run.Enter(room, -1);
            Assert.IsTrue(run.IsFighting);
            Assert.AreEqual(3, run.JarsInRoom);

            // 공격이 닿는 자리 (평타·초식 부채꼴과 같은 판정)
            int silver = run.Silver;
            var jar = run.Jars[0].transform.position;
            Assert.AreEqual(1, AThousandDawns.Combat.Breakable.BreakNear(jar, 0.1f));
            Assert.AreEqual(2, run.JarsInRoom, "싸우는 중에도 깨져요");
            Assert.AreEqual(silver + 1, run.Silver);

            // 비도: 항아리 쪽으로 던지면 깨고 지나가요
            var target = run.Jars.OrderByDescending(j => j.transform.position.z).First().transform.position; // 바라보는 쪽(+z)으로 던질 수 있게 북쪽 항아리
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(target.x, 0f, target.z) - player.Facing * 1.2f; // 방 안 (벽 바깥이면 비도가 벽에 막혀요)
            cc.enabled = true;
            player.SetWeapon(AThousandDawns.Player.WeaponKind.Dagger);
            player.ThrowDaggers(1, AThousandDawns.Combat.HitDamageInput.Of(1f), AThousandDawns.Combat.HitTag.Basic, 0f);
            float end = Time.realtimeSinceStartup + 2f;
            while (run.JarsInRoom == 2 && Time.realtimeSinceStartup < end) yield return null;
            var flown = GameObject.Find("비도");
            Assert.AreEqual(1, run.JarsInRoom, $"비도로 깼어요 (항아리 {target}, 플레이어 {player.transform.position}, 방향 {player.Facing}, 비도 {(flown != null ? flown.transform.position.ToString() : "없음")}, 남은 비도 {player.DaggerCount})");
        }

        // 강기 · 버들잎 표창 · 하피 깃털 (GanggiBolt)도 지나가는 길의 항아리를 깨요 (허리 높이로 날아서 항아리 위로 지나가거나 벽처럼 막혀 사라지던 것)
        [UnityTest]
        public IEnumerator 강기와_깃털도_항아리를_깨고_지나감([Values(false, true)] bool dart)
        {
            RoomRun.SeedOverride = 5;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<AThousandDawns.Player.PlayerController>();
            run.ChooseFork(0);
            var room = run.Map.Rooms.First(r => r.Kind == FloorRoomKind.Combat);
            run.Enter(room, -1);
            Assert.AreEqual(3, run.JarsInRoom);

            var jar = run.Jars[0].transform.position;
            Vector3 inward = player.transform.position - jar; // 방 가운데 쪽에서 쏴요 (벽 바깥이면 벽에 막혀요)
            inward.y = 0f;
            Vector3 origin = new Vector3(jar.x, player.transform.position.y + 1f, jar.z) + inward.normalized * 1.5f;
            AThousandDawns.Player.GanggiBolt.Fire(origin, -inward, 14f, 4f, dart ? 0.35f : 0.8f, 1f, AThousandDawns.Player.GanggiBolt.PierceAll,
                dart: dart, label: dart ? "하피 깃털" : null);
            float end = Time.realtimeSinceStartup + 2f;
            while (run.JarsInRoom == 3 && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(2, run.JarsInRoom, dart ? "깃털로 깼어요" : "강기로 깼어요");
        }
    }
}
