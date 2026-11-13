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
            RoomRun.RoomSizeOverride = 0; // 방 구성 자리 검사는 작은 방 기준 (방 크기 시험만 따로 바꿔요)
        }

        public override void TearDown()
        {
            RoomRun.LayoutOverride = -1;
            RoomRun.RoomSizeOverride = -1;
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
        static Vector3 RoomCenter(RoomRun run) => run.RoomCenter;

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

        [UnityTest]
        public IEnumerator 천마총_방_구성은_관_마기_기둥_혈지()
        {
            yield return Load(3);
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<AThousandDawns.Enemies.EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
            Vector3 c = RoomCenter(run);
            int[] counts = { 0, 4, 2, 2 };
            for (int layout = 0; layout < 4; layout++)
            {
                RoomRun.LayoutOverride = layout;
                run.Enter(room, 0);
                Assert.AreEqual(counts[layout], run.RoomFeatures.Count, RoomRun.TombLayoutNames[layout]);
                foreach (var f in run.RoomFeatures)
                {
                    if (f.Kind == RoomRun.FeatureKind.BloodPool) continue;
                    Vector3 local = f.Center - c;
                    Assert.IsFalse(Mathf.Abs(local.x) < f.Size.x * 0.5f + 1.5f || Mathf.Abs(local.z) < f.Size.y * 0.5f + 1.5f, $"{f.Kind}: 십자 길은 비워요");
                    foreach (var b in Bamboo) Assert.IsFalse(Overlaps(f, c, b, 0.5f), $"{f.Kind} · 대나무 {b}");
                    foreach (var b in Bushes) Assert.IsFalse(Overlaps(f, c, b, 0.5f), $"{f.Kind} · 풀숲 {b}");
                }
                yield return null;
            }

            // 마기 기둥: 싸우는 동안 때마다 분출
            RoomRun.LayoutOverride = 2;
            var fight = run.Map.Rooms.Where(r => r.HasFight && r.FixedWaves == null && !r.Cleared).Skip(1).First();
            run.Enter(fight, 0);
            int pulses = run.MiasmaPulses;
            yield return WaitUntil(() => { player.Heal(9999f); return run.MiasmaPulses > pulses; }, 6f);
            Assert.Greater(run.MiasmaPulses, pulses, "마기 기둥이 뿜어요");

            // 혈지: 위에 선 적은 회복
            RoomRun.LayoutOverride = 3;
            run.Enter(fight, 0);
            var pool = run.RoomFeatures.First(f => f.Kind == RoomRun.FeatureKind.BloodPool);
            var grunt = director.Spawn(AThousandDawns.Enemies.EnemyKind.Grunt, pool.Center);
            bool healedSignal = false;
            System.Action<AThousandDawns.Enemies.EnemyBrain> onHeal = e => healedSignal |= e == grunt;
            RoomRun.BloodPoolHealed += onHeal;
            try
            {
                grunt.Damageable.ApplyHit(Mathf.CeilToInt(grunt.Damageable.MaxHealth * 0.5f));
                float hurt = grunt.Damageable.Health;
                yield return WaitUntil(() => grunt == null || grunt.Damageable.Health > hurt + 1f || !run.InBloodPool(grunt.transform.position), 3f);
                Assert.IsTrue(healedSignal, "혈지 회복 신호");
                Assert.Greater(grunt.Damageable.Health, hurt, "혈지 위에서 회복해요");
            }
            finally { RoomRun.BloodPoolHealed -= onHeal; }
        }

        // 적 길찾기: 플레이어와 적 사이에 담이 있으면 벽에 비비지 않고 돌아와요
        [UnityTest]
        public IEnumerator 적은_담을_돌아서_다가와요()
        {
            AThousandDawns.Prototype.PrototypeArena.AutoSpawn = false;
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
                yield return null;
                var director = Object.FindFirstObjectByType<AThousandDawns.Enemies.EnemyDirector>();
                var player = Object.FindFirstObjectByType<PlayerController>();
                Vector3 home = player.transform.position;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = home + new Vector3(0f, 0.6f, 3f);
                wall.transform.localScale = new Vector3(4f, 1.2f, 0.5f);
                var grunt = director.Spawn(AThousandDawns.Enemies.EnemyKind.Grunt, home + new Vector3(0f, 0f, 6f));
                bool detoured = false;
                yield return WaitUntil(() =>
                {
                    player.Heal(9999f);
                    detoured |= grunt.IsDetouring;
                    return Vector3.Distance(grunt.transform.position, player.transform.position) < 1.8f;
                }, 8f);
                Assert.IsTrue(detoured, "담 앞에서 돌아가요");
                Assert.Less(Vector3.Distance(grunt.transform.position, player.transform.position), 1.8f, "담을 돌아 플레이어에게 와요");
            }
            finally { AThousandDawns.Prototype.PrototypeArena.AutoSpawn = true; }
        }

        [UnityTest]
        public IEnumerator 죽림은_2층부터_대숲_개울_쓰러진_대나무()
        {
            yield return Load(0);
            var run = Object.FindFirstObjectByType<RoomRun>();
            run.BeginFork(2);
            run.ChooseFork(0);
            var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
            Vector3 c = RoomCenter(run);
            int[] counts = { 0, 6, 1, 4 };
            for (int layout = 0; layout < 4; layout++)
            {
                RoomRun.LayoutOverride = layout;
                run.Enter(room, 0);
                Assert.AreEqual(counts[layout], run.RoomFeatures.Count, RoomRun.BambooLayoutNames[layout]);
                foreach (var f in run.RoomFeatures)
                {
                    if (f.Kind == RoomRun.FeatureKind.Water) continue;
                    Vector3 local = f.Center - c;
                    Assert.IsFalse(Mathf.Abs(local.x) < f.Size.x * 0.5f + 1.5f || Mathf.Abs(local.z) < f.Size.y * 0.5f + 1.5f, $"{f.Kind}: 십자 길은 비워요");
                    foreach (var b in Bamboo) Assert.IsFalse(Overlaps(f, c, b, 0.5f), $"{f.Kind} · 대나무 {b}");
                    foreach (var b in Bushes) Assert.IsFalse(Overlaps(f, c, b, 0.5f), $"{f.Kind} · 풀숲 {b}");
                }
                yield return null;
            }
        }

        static GameObject Wall(Vector3 at, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = at + Vector3.up * size.y * 0.5f;
            wall.transform.localScale = size;
            Physics.SyncTransforms(); // 만든 프레임에 바로 사선 검사가 되게
            return wall;
        }

        static bool ClearLine(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return AThousandDawns.Enemies.ObstacleProbe.Distance(from + Vector3.up * 1f, d.normalized, 0.2f, d.magnitude) >= d.magnitude - 0.3f;
        }

        // 엄폐물 판단: 궁수는 사선이 막히면 쏠 자리로 옮기고, 엄폐물 뒤의 플레이어에게 근접 적 둘은 양쪽으로 갈라져 돌아와요
        [UnityTest]
        public IEnumerator 궁수는_쏠_자리로_옮기고_근접_적은_양쪽으로_돌아와요()
        {
            AThousandDawns.Prototype.PrototypeArena.AutoSpawn = false;
            try
            {
                yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
                yield return null;
                var director = Object.FindFirstObjectByType<AThousandDawns.Enemies.EnemyDirector>();
                var player = Object.FindFirstObjectByType<PlayerController>();
                Vector3 home = player.transform.position;

                var wall = Wall(home + new Vector3(0f, 0f, -2.5f), new Vector3(2.5f, 2f, 0.5f)); // 플레이어 뒤쪽 (앞쪽엔 목인장 둘이 있어요)
                var archer = director.Spawn(AThousandDawns.Enemies.EnemyKind.Archer, home + new Vector3(0f, 0f, -6f));
                Assert.IsFalse(ClearLine(archer.transform.position, player.transform.position), "처음엔 담에 가려요");
                var path = new System.Text.StringBuilder();
                float nextLog = 0f;
                yield return WaitUntil(() =>
                {
                    player.Heal(9999f);
                    if (Time.time >= nextLog) { nextLog = Time.time + 0.5f; path.Append($"({archer.transform.position.x - home.x:0.0},{archer.transform.position.z - home.z:0.0}) "); }
                    return archer == null || ClearLine(archer.transform.position, player.transform.position);
                }, 6f);
                Assert.IsTrue(ClearLine(archer.transform.position, player.transform.position), "궁수가 쏠 자리로 옮겨요 " + path);
                archer.Damageable.ApplyHit(99999);
                Object.Destroy(wall);
                yield return null;

                var cover = Wall(home + new Vector3(0f, 0f, -1.5f), new Vector3(3f, 2f, 0.5f));
                var a = director.Spawn(AThousandDawns.Enemies.EnemyKind.Grunt, home + new Vector3(-0.3f, 0f, -6f));
                var b = director.Spawn(AThousandDawns.Enemies.EnemyKind.Grunt, home + new Vector3(0.3f, 0f, -6f));
                bool aFlank = false, bFlank = false;
                float aMinX = 0f, aMaxX = 0f, bMinX = 0f, bMaxX = 0f;
                yield return WaitUntil(() =>
                {
                    player.Heal(9999f);
                    aFlank |= a.IsFlanking;
                    bFlank |= b.IsFlanking;
                    float ax = a.transform.position.x - home.x, bx = b.transform.position.x - home.x;
                    aMinX = Mathf.Min(aMinX, ax); aMaxX = Mathf.Max(aMaxX, ax);
                    bMinX = Mathf.Min(bMinX, bx); bMaxX = Mathf.Max(bMaxX, bx);
                    return aFlank && bFlank && ((aMinX < -1.4f && bMaxX > 1.4f) || (aMaxX > 1.4f && bMinX < -1.4f));
                }, 6f);
                Assert.IsTrue(aFlank && bFlank, "엄폐물 뒤의 플레이어를 못 보면 돌아와요");
                Assert.IsTrue((aMinX < -1.4f && bMaxX > 1.4f) || (aMaxX > 1.4f && bMinX < -1.4f), "둘은 양쪽으로 갈라져요");
                Object.Destroy(cover);
            }
            finally { AThousandDawns.Prototype.PrototypeArena.AutoSpawn = true; }
        }

        // 방 크기: 작은 방 (20×12) · 중간 방 · 큰 방. 큰 방일수록 적 예산이 크고, 큰 방은 웨이브 하나 더
        [UnityTest]
        public IEnumerator 방_크기_세_가지와_큰_방일수록_많은_적()
        {
            yield return Load(1);
            var run = Object.FindFirstObjectByType<RoomRun>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
            Vector2[] extents = { new Vector2(20f, 12f), new Vector2(26f, 16f), new Vector2(32f, 20f) };
            var centers = new System.Collections.Generic.List<Vector3>();
            for (int size = 0; size < 3; size++)
            {
                RoomRun.RoomSizeOverride = size;
                run.Enter(room, 0);
                Assert.AreEqual((RoomRun.RoomSizeKind)size, run.RoomSize, RoomRun.RoomSizeNames[size]);
                Assert.AreEqual(extents[size], run.RoomExtent, "방 크기");
                Vector3 local = player.transform.position - run.RoomCenter;
                Assert.Less(Mathf.Abs(local.x), extents[size].x * 0.5f, "플레이어가 그 방 안에");
                Assert.Less(Mathf.Abs(local.z), extents[size].y * 0.5f);
                centers.Add(run.RoomCenter);
                if (size == 2) Assert.GreaterOrEqual(run.WaveCount, 3, "큰 방은 웨이브 하나 더");
                yield return null;
            }
            Assert.AreEqual(3, centers.Distinct().Count(), "세 방은 다른 자리");

            // 예산: 같은 층에서 여러 번 짜 보면 큰 방 > 중간 방 > 작은 방
            float Total(float scale) { float sum = 0f; for (int i = 0; i < 30; i++) sum += run.PlanRoom(2, 0, scale).Sum(w => w.Sum(RoomBudget.Cost)); return sum; }
            float s = Total(1f), m = Total(1.4f), l = Total(1.8f);
            Assert.Greater(m, s * 1.2f, "중간 방은 적이 더 많아요");
            Assert.Greater(l, m * 1.15f, "큰 방은 더 많아요");

            // 자연 분포: 층 셋을 지나면 크기가 둘 이상 나와요
            RoomRun.RoomSizeOverride = -1;
            var seen = new System.Collections.Generic.HashSet<RoomRun.RoomSizeKind>();
            for (int floor = 1; floor <= 3; floor++)
            {
                run.BeginFork(floor);
                run.ChooseFork(0);
                foreach (var r in run.Map.Rooms.Where(r => r.HasFight && r.FixedWaves == null))
                {
                    run.Enter(r, 0);
                    seen.Add(run.RoomSize);
                }
            }
            Assert.GreaterOrEqual(seen.Count, 2, "방 크기가 섞여 나와요");
        }

        // 큰 방에서도 방 구성은 늘어난 자리에서 십자 길 · 대나무 기둥 · 풀숲을 비켜요
        [UnityTest]
        public IEnumerator 큰_방의_방_구성도_길과_기둥을_비켜요()
        {
            yield return Load(1);
            var run = Object.FindFirstObjectByType<RoomRun>();
            var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
            RoomRun.RoomSizeOverride = 2;
            for (int layout = 1; layout < 4; layout++)
            {
                RoomRun.LayoutOverride = layout;
                run.Enter(room, 0);
                Vector3 c = run.RoomCenter;
                Vector2 k = new Vector2(run.RoomExtent.x / 20f, run.RoomExtent.y / 12f);
                foreach (var f in run.RoomFeatures)
                {
                    Vector3 local = f.Center - c;
                    Assert.IsFalse(Mathf.Abs(local.x) < f.Size.x * 0.5f + 1.5f || Mathf.Abs(local.z) < f.Size.y * 0.5f + 1.5f, $"{f.Kind}: 십자 길");
                    foreach (var b in Bamboo) Assert.IsFalse(Overlaps(f, c, Vector2.Scale(b, k), 0.5f), $"{f.Kind} · 대나무");
                    foreach (var b in Bushes) Assert.IsFalse(Overlaps(f, c, Vector2.Scale(b, k), 0.5f), $"{f.Kind} · 풀숲");
                }
                yield return null;
            }
        }
    }
}
