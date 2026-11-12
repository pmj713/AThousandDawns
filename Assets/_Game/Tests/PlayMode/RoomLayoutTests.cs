using System;
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
    // 방 구성 변형: 폐사찰은 기둥 회랑 · 불상 · 무너진 벽, 장강 수채는 여울 · 웅덩이 · 물길 (물에서 느려지고 물살에 밀려요)
    public class RoomLayoutTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
        }

        public override void TearDown()
        {
            RoomRun.LayoutOverride = -1;
            if (System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        static readonly Vector2[] Bamboo = { new Vector2(-5.5f, 3f), new Vector2(5.5f, -3f), new Vector2(-7f, -3.5f), new Vector2(7f, 3.5f), new Vector2(3.5f, 3f), new Vector2(-3.5f, -3f) };
        static readonly Vector2[] Bushes = { new Vector2(-7.5f, 4.3f), new Vector2(7.5f, -4.3f), new Vector2(-3f, 4.3f), new Vector2(3f, -4.3f) };

        static bool Overlaps(RoomRun.RoomFeature f, Vector3 origin, Vector2 p, float r) =>
            Mathf.Abs(origin.x + p.x - f.Center.x) < f.Size.x * 0.5f + r && Mathf.Abs(origin.z + p.y - f.Center.z) < f.Size.y * 0.5f + r;

        IEnumerator Load(int region)
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            run.SetRegionForTest(region);
            run.ChooseFork(0);
        }

        [UnityTest]
        public IEnumerator 폐사찰_방_구성은_십자길_가운데_대나무_풀숲을_비켜요()
        {
            yield return Load(1);
            var run = Object.FindFirstObjectByType<RoomRun>();
            var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
            int[] counts = { 0, 6, 2, 4 };
            for (int layout = 0; layout < 4; layout++)
            {
                RoomRun.LayoutOverride = layout;
                run.Enter(room, 0);
                Assert.AreEqual(layout, run.RoomLayout);
                Assert.AreEqual(counts[layout], run.RoomFeatures.Count, RoomRun.TempleLayoutNames[layout]);
                foreach (var f in run.RoomFeatures)
                {
                    Assert.AreNotEqual(RoomRun.FeatureKind.Water, f.Kind);
                    Assert.IsTrue(f.Go.GetComponent<Collider>().enabled, "사람 · 화살을 막아요");
                    Vector3 local = f.Center - RoomCenter(run);
                    bool crossX = Mathf.Abs(local.x) < f.Size.x * 0.5f + 1.5f, crossZ = Mathf.Abs(local.z) < f.Size.y * 0.5f + 1.5f;
                    Assert.IsFalse(crossX || crossZ, $"{RoomRun.TempleLayoutNames[layout]} {f.Kind}: 문 사이 십자 길은 비워요");
                    foreach (var b in Bamboo) Assert.IsFalse(Overlaps(f, RoomCenter(run), b, 0.5f), $"{f.Kind}가 대나무 기둥과 겹쳐요 {b}");
                    foreach (var b in Bushes) Assert.IsFalse(Overlaps(f, RoomCenter(run), b, 0.5f), $"{f.Kind}가 풀숲과 겹쳐요 {b}");
                }
                foreach (var jar in run.Jars)
                    foreach (var f in run.RoomFeatures)
                        Assert.IsFalse(Overlaps(f, Vector3.zero, new Vector2(jar.transform.position.x, jar.transform.position.z), 0.2f), "항아리는 석불 · 담 밖에");
                yield return null;
            }
        }

        // 방 가운데 = 관문 자리 (시작 방에 들어가면 플레이어가 서는 곳)
        static Vector3 RoomCenter(RoomRun run)
        {
            var prop = typeof(RoomRun).GetField("combat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(run);
            return (Vector3)prop.GetType().GetField("center").GetValue(prop);
        }

        [UnityTest]
        public IEnumerator 죽림은_방_구성이_그대로()
        {
            yield return Load(0);
            var run = Object.FindFirstObjectByType<RoomRun>();
            run.Enter(run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null), 0);
            Assert.AreEqual(0, run.RoomFeatures.Count);
            Assert.AreEqual(-1, run.RoomLayout);
        }

        [UnityTest]
        public IEnumerator 장강_물에서는_느려지고_흐르는_물은_밀어요()
        {
            yield return Load(2);
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<AThousandDawns.Enemies.EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var cc = player.GetComponent<CharacterController>();
            var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
            Vector3 c = RoomCenter(run);

            RoomRun.LayoutOverride = 1; // 여울: 가운데 가로 띠, 동쪽으로
            run.Enter(room, 0);
            yield return WaitUntil(() =>
            {
                for (int i = director.Enemies.Count - 1; i >= 0; i--) director.Enemies[i].Damageable.ApplyHit(99999);
                return !run.IsFighting;
            }, 20f);
            Assert.AreEqual(1, run.RoomFeatures.Count);
            Assert.AreEqual(RoomRun.FeatureKind.Water, run.RoomFeatures[0].Kind);
            cc.enabled = false;
            player.transform.position = c + new Vector3(-5f, 0f, 0f);
            cc.enabled = true;
            yield return null;
            float x0 = player.transform.position.x;
            yield return WaitUntil(() => player.transform.position.x > x0 + 0.8f, 3f);
            Assert.IsTrue(run.PlayerInWater);
            Assert.Greater(player.SlowRatio, 0f, "물에서는 느려요");
            Assert.Greater(player.transform.position.x, x0 + 0.8f, "물살 쪽(동쪽)으로 밀려요");

            cc.enabled = false;
            player.transform.position = c + new Vector3(-5f, 0f, 4f);
            cc.enabled = true;
            yield return WaitUntil(() => player.SlowRatio <= 0f, 2f);
            Assert.IsFalse(run.PlayerInWater);
            Assert.AreEqual(0f, player.SlowRatio, "물 밖에서는 그대로");

            RoomRun.LayoutOverride = 2; // 웅덩이: 고인 물은 밀지 않아요
            run.Enter(room, 0);
            var pool = run.RoomFeatures[0];
            cc.enabled = false;
            player.transform.position = pool.Center;
            cc.enabled = true;
            yield return null;
            Vector3 p0 = player.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(run.PlayerInWater);
            Assert.Greater(player.SlowRatio, 0f);
            Assert.Less(Vector3.Distance(p0, player.transform.position), 0.2f, "고인 물은 밀지 않아요");
        }
    }
}
