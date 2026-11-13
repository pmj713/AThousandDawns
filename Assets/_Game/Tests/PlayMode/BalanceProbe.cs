using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
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
    // 밸런스 측정 (평소 테스트에서는 빠져요: --filter AThousandDawns.Tests.BalanceProbe 로 따로 돌려요).
    // 적을 그 지역 배율로 세우고, 몬스터 시트 예상 화력 (기본 DPS 38.7 × 지역 화력 배율)을 매 프레임 넣어 처치 시간을 재요.
    // 플레이어는 가만히 서서 피하지 않아요 (회복으로 버티게 함). 그동안 받은 피해 = 피하지 않을 때의 압박.
    // 결과는 %TEMP%/claude_cc/balance_probe.txt에 줄마다 남아요.
    [Explicit("밸런스 측정용, 따로 돌려요")]
    public class BalanceProbe : InputTestFixture
    {
        static readonly float[] Firepower = { 1f, 1.6f, 2.6f, 4f };     // 몬스터 D15~D18
        static readonly float[] MaxHealth = { 110f, 135f, 155f, 175f };  // 몬스터 E15~E18
        static readonly float[] HealthMult = { 1f, 1.8f, 3f, 4.5f }, AttackMult = { 1f, 1.3f, 1.6f, 2f };
        const float BaseDps = 38.7f;
        const float ChaseSpeed = 6f; // player.move_speed

        static string ReportPath => Path.Combine(Path.GetTempPath(), "claude_cc", "balance_probe.txt"); // 프로젝트 Temp는 에디터가 닫힐 때 지워져요

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            MetaSave.PathOverride = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            if (File.Exists(MetaSave.PathOverride)) File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static void Write(string line)
        {
            Debug.Log("[밸런스] " + line);
            File.AppendAllText(ReportPath, line + "\n", Encoding.UTF8);
        }

        // 한 판 싸움: 처치 시간 (초), 받은 피해 (피하지 않을 때), 플레이어 최대 체력 대비
        IEnumerator Fight(EnemyKind kind, int region, int karma, float distance, float limit, string tag, bool kill = true)
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 home = player.transform.position;
            var enemy = director.Spawn(kind, home + new Vector3(0f, 0f, distance));
            if (enemy is IMasterBrain m) m.SetZone(home + new Vector3(0f, 0f, distance * 0.5f), new Vector2(22f, 16f));
            float h = HealthMult[region] * (karma >= 1 ? 1.15f : 1f) * (karma >= 6 && (enemy.Rank == EnemyRank.Master || enemy.Rank == EnemyRank.Boss) ? 1.25f : 1f);
            float a = AttackMult[region] * (karma >= 4 ? 1.15f : 1f);
            enemy.Damageable.ScaleMaxHealth(h);
            enemy.ScaleAttack(a);
            float hp = enemy.Damageable.MaxHealth;
            float dps = BaseDps * Firepower[region];

            Time.timeScale = 4f;
            float t = 0f, taken = 0f, carry = 0f, last = player.Health, biggest = 0f;
            int hits = 0;
            float realEnd = Time.realtimeSinceStartup + limit * 2f;
            while (enemy != null && !enemy.IsDead && t < limit && Time.realtimeSinceStartup < realEnd)
            {
                yield return null;
                float dt = Time.deltaTime;
                t += dt;
                if (player.Health < last)
                {
                    float d = last - player.Health;
                    taken += d;
                    biggest = Mathf.Max(biggest, d);
                    hits++;
                }
                player.Heal(9999f);
                last = player.Health;
                if (enemy == null || enemy.IsDead) break;
                if (!kill || !enemy.IsAwake) continue; // 일반 적은 때리지 않고 압박만 재요
                carry += dps * dt;
                int dmg = Mathf.FloorToInt(carry);
                if (dmg > 0)
                {
                    Target(director, enemy).Damageable.ApplyHit(dmg, false);
                    carry -= dmg;
                }
            }
            Time.timeScale = 1f;
            bool dead = enemy == null || enemy.IsDead;
            float bars = taken / MaxHealth[region];
            Write($"{tag}\t{kind}\tr{region}\tk{karma}\t체력 {hp:0}\t처치 {(kill ? (dead ? t.ToString("0.0") + "초" : $"못함({t:0}초)") : $"계산 {hp / dps:0.0}초")}\t받은 피해 {taken:0} ({bars:0.00}칸)\t초당 {taken / Mathf.Max(1f, t):0.0}\t최대 한 방 {biggest:0} ({biggest / MaxHealth[region]:0%})\t맞은 수 {hits}");
        }

        // 부하 (목탁승 · 망령 · 수적 등)가 있으면 화력을 부하에 먼저 써요 (실제로도 부하부터 정리해요)
        static EnemyBrain Target(EnemyDirector director, EnemyBrain main)
        {
            foreach (var e in director.Enemies)
                if (e != null && e != main && !e.IsDead && e.Damageable.Targetable) return e;
            return main;
        }

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator 보스와_고수()
        {
            File.WriteAllText(ReportPath, "구분\t적\t지역\t업보\t체력\t처치\t받은 피해 (최대 체력 칸)\t초당\t최대 한 방\t맞은 수\n", Encoding.UTF8);
            var bosses = new[] { (EnemyKind.Nokrim, 0), (EnemyKind.BloodBuddha, 1), (EnemyKind.IronDragon, 2), (EnemyKind.HeavenlyDemon, 3) };
            var masters = new[] { (EnemyKind.Qingzhu, 0), (EnemyKind.IronFist, 0), (EnemyKind.Hyegwang, 1), (EnemyKind.Takrang, 2), (EnemyKind.BloodShadow, 3) };
            foreach (var (k, r) in bosses) yield return Fight(k, r, 0, 4f, 240f, "보스");
            yield return Fight(EnemyKind.HeavenlyDemon, 3, 6, 4f, 300f, "보스");
            foreach (var (k, r) in masters) yield return Fight(k, r, 0, 4f, 120f, "고수");
            yield return Fight(EnemyKind.BloodShadow, 3, 6, 4f, 180f, "고수");
        }

        // 방 하나 전체: 그 지역 2층 싸우는 방을 방 구성마다 (실제 웨이브 · 정예 · 엄폐물 · 물 · 혈지 그대로).
        // 화력은 가장 가까운 적에게 (부르는 적 · 갈라진 적도), 플레이어는 방 가운데 가만히. 방을 비울 때까지 시간과 받은 피해.
        IEnumerator Room(int region, int layout, int karma, bool dense, float limit = 120f, bool moving = false, int seed = -1, int size = 0, bool chase = false)
        {
            new MetaSave { runs = 1, finalBossKills = karma > 0 ? karma : 0, karma = karma, karmaMax = karma, tutorialFloorDone = true }.Save();
            RoomRun.SkipVillage = true;
            RoomRun.SeedOverride = seed > 0 ? seed : 1000 + region * 10 + layout;
            RoomRun.RoomSizeOverride = size; // 0 작은 방 · 1 중간 · 2 큰 방
            RoomRun.LayoutOverride = layout;
            PrototypeArena.AutoSpawn = true;
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(region);
            run.BeginFork(2);
            int pick = 0;
            for (int i = 0; i < run.ForkOptions.Count; i++) if (run.ForkOptions[i].Dense == dense) { pick = i; break; }
            run.ChooseFork(pick);
            var room = run.Map.Rooms.FirstOrDefault(r => r.HasFight && r.FixedWaves == null && r.Kind == FloorRoomKind.Combat);
            if (room == null) { Write($"방\tr{region}\t구성 {layout}\t싸우는 방 없음"); yield break; }
            run.Enter(room, 0);
            Vector3 home = player.transform.position;
            float dps = BaseDps * Firepower[region];
            var cc = player.GetComponent<CharacterController>();
            float angle = 0f;
            Time.timeScale = 4f;
            float t = 0f, taken = 0f, carry = 0f, last = player.Health;
            int hits = 0, spawned = 0;
            var seen = new System.Collections.Generic.HashSet<EnemyBrain>();
            float realEnd = Time.realtimeSinceStartup + limit;
            while (run.IsFighting && t < limit && Time.realtimeSinceStartup < realEnd)
            {
                yield return null;
                t += Time.deltaTime;
                if (player.Health < last) { taken += last - player.Health; hits++; }
                if (moving) // 가운데를 반지름 3칸으로 돌며 움직여요 (멈춰 있지 않은 플레이어)
                {
                    angle += Time.deltaTime * 1.1f;
                    Vector3 want = home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
                    cc.Move(want - player.transform.position);
                }
                player.Heal(9999f);
                last = player.Health;
                if (arena.IsChoosing) arena.Choose(0);
                // 깨어 있는 가장 가까운 적. 없으면 숨어 있는 적 (풀숲 청죽사 · 수귀처럼 다가가야 깨는 적은 실제로는 플레이어가 찾아가요)
                EnemyBrain target = null, hidden = null;
                Vector3 from = chase ? player.transform.position : home; // 따라가면 플레이어에게서 가장 가까운 적
                float best = float.MaxValue, bestHidden = float.MaxValue;
                foreach (var e in director.Enemies)
                {
                    if (e == null || e.IsDead) continue;
                    if (seen.Add(e)) spawned++;
                    float d = (e.transform.position - from).sqrMagnitude;
                    if (e.IsAwake && e.Damageable.Targetable) { if (d < best) { best = d; target = e; } }
                    else if (d < bestHidden) { bestHidden = d; hidden = e; }
                }
                if (target == null) target = hidden;
                if (target == null) continue;
                if (chase) // 가장 가까운 적에게 걸어가 1.8칸 앞에서 싸워요 (벽 쪽 기둥 · 혈지 · 물도 지나가요)
                {
                    Vector3 to = target.transform.position - player.transform.position;
                    to.y = 0f;
                    if (to.magnitude > 1.8f) cc.Move(to.normalized * Mathf.Min(to.magnitude - 1.8f, ChaseSpeed * Time.deltaTime) + Vector3.down * 0.1f);
                }
                carry += dps * Time.deltaTime;
                int dmg = Mathf.FloorToInt(carry);
                if (dmg > 0) { target.Damageable.ApplyHit(dmg, false); carry -= dmg; }
            }
            Time.timeScale = 1f;
            RoomRun.LayoutOverride = -1;
            RoomRun.SeedOverride = 0;
            RoomRun.RoomSizeOverride = -1;
            string[] names = region == 0 ? RoomRun.BambooLayoutNames : region == 1 ? RoomRun.TempleLayoutNames : region == 2 ? RoomRun.RiverLayoutNames : RoomRun.TombLayoutNames;
            float bars = taken / MaxHealth[region];
            Write($"방{(chase ? "(따라감)" : moving ? "(움직임)" : "")}\tr{region}\tk{karma}\t{(dense ? "짙은 길" : "옅은 길")} · {names[Mathf.Clamp(layout, 0, 3)]} · {RoomRun.RoomSizeNames[Mathf.Clamp(size, 0, 2)]} · 시드 {(seed > 0 ? seed : 1000 + region * 10 + layout)}\t적 {spawned} · 웨이브 {run.WaveCount}\t처치 {(run.IsFighting ? $"못함({t:0}초)" : t.ToString("0.0") + "초")}\t받은 피해 {taken:0} ({bars:0.00}칸)\t초당 {taken / Mathf.Max(1f, t):0.0}\t맞은 수 {hits}");
        }

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator 방_전체()
        {
            File.AppendAllText(ReportPath, "\n구분\t지역\t업보\t길 · 구성\t적 · 웨이브\t처치\t받은 피해 (칸)\t초당\t맞은 수\n", Encoding.UTF8);
            for (int r = 0; r < 4; r++)
                for (int layout = 0; layout < 4; layout++)
                    yield return Room(r, layout, 0, false);
            for (int r = 0; r < 4; r++) yield return Room(r, 0, 0, true); // 짙은 길 (정예)
            yield return Room(3, 0, 6, true);                               // 천마총 짙은 길 · 업보 6
        }

        // 같은 방들을 플레이어가 움직이며 (가운데 둘레 반지름 3칸): 다가가야 깨는 적 · 지나간 자리 장판 · 꺾여 오는 혼불이 제대로 들어가요
        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator 방_전체_움직임()
        {
            File.AppendAllText(ReportPath, "\n움직이는 플레이어\n", Encoding.UTF8);
            for (int r = 0; r < 4; r++)
                for (int layout = 0; layout < 4; layout++)
                    yield return Room(r, layout, 0, false, 120f, true);
            for (int r = 0; r < 4; r++) yield return Room(r, 0, 0, true, 120f, true);
        }

        // 방 크기 비교: 지역마다 작은 · 중간 · 큰 방을 같은 시드 셋으로 (움직이는 플레이어, 방 구성 없음)
        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator 방_크기_비교()
        {
            File.AppendAllText(ReportPath, "\n방 크기 비교 (움직이는 플레이어 · 방 구성 없음)\n", Encoding.UTF8);
            int[] seeds = { 501, 502, 503 };
            for (int r = 0; r < 4; r++)
                for (int size = 0; size < 3; size++)
                    foreach (int seed in seeds)
                        yield return Room(r, 0, 0, false, 150f, true, seed, size);
        }

        // 천마총 방 구성 비교: 같은 시드 (같은 적 구성)로 방 구성만 바꿔요. 가만히 · 움직이는 플레이어 둘 다
        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator 천마총_구성_비교()
        {
            File.AppendAllText(ReportPath, "\n천마총 방 구성 비교 (같은 시드 = 같은 적 구성)\n", Encoding.UTF8);
            int[] seeds = { 601, 602, 603 };
            foreach (int seed in seeds)
                for (int layout = 0; layout < 4; layout++)
                {
                    yield return Room(3, layout, 0, false, 120f, false, seed, 0);
                    yield return Room(3, layout, 0, false, 120f, true, seed, 0);
                }
        }

        // 따라가는 플레이어: 가장 가까운 적에게 걸어가 싸워요. 벽 쪽 기믹 (마기 기둥 · 혈지 · 물 · 엄폐물)이 실제로 들어가요.
        // 지역마다 방 구성 넷을 같은 시드 셋으로 (같은 적 구성) 비교해요.
        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator 방_구성_따라감()
        {
            File.AppendAllText(ReportPath, "\n방 구성 비교 (따라가는 플레이어 · 같은 시드 = 같은 적 구성)\n", Encoding.UTF8);
            int[] seeds = { 601, 602, 603 };
            for (int r = 0; r < 4; r++)
                foreach (int seed in seeds)
                    for (int layout = 0; layout < 4; layout++)
                        yield return Room(r, layout, 0, false, 120f, false, seed, 0, true);
        }

        // 할당 측정: 수묵 판 천마총 싸움 (적 12명 추가)에서 프레임당 할당 바이트를, 화면 UI · 아트 · 이펙트를 하나씩 끄며 비교해요.
        // (에디터 위라 절대값보다 끈 것과 켠 것의 차이를 봐요)
        public static bool MeasureArtTypes = false, MeasureGameTypes = true; // 종류별로 하나씩 꺼 볼지 (아트 · 게임 코드)

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator 할당_측정()
        {
            new MetaSave { runs = 1, tutorialFloorDone = true }.Save();
            RoomRun.SkipVillage = true;
            RoomRun.LayoutOverride = 2;
            yield return SceneManager.LoadSceneAsync(TitleScreen.RunScene, LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(3);
            run.BeginFork(2);
            run.ChooseFork(0);
            run.Enter(run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null), 0);
            yield return new WaitForSeconds(1f);
            Vector3 home = player.transform.position;
            var kinds = new[] { EnemyKind.Cultist, EnemyKind.BloodArcher, EnemyKind.DemonGuard, EnemyKind.BloodBomber };
            for (int i = 0; i < 12; i++) director.Spawn(kinds[i % 4], home + new Vector3(Mathf.Cos(i * 0.52f), 0f, Mathf.Sin(i * 0.52f)) * 6f);
            yield return null;

            var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            bool IsUi(MonoBehaviour b) => b is PrototypeHud || b is MeridianScreen || b is RegressionBookScreen;
            bool IsArt(MonoBehaviour b) { string ns = b.GetType().Namespace ?? ""; return ns.StartsWith("AThousandDawns.Art") || ns.StartsWith("AThousandDawns.Fx"); }
            File.AppendAllText(ReportPath, $"\n할당 측정 (컴포넌트 {all.Length}개 · 아트·이펙트 {all.Count(IsArt)}개)\n", Encoding.UTF8);

            IEnumerator Sample(string label)
            {
                for (int i = 0; i < 30; i++) { player.Heal(9999f); yield return null; } // 바뀐 상태가 자리 잡게
                var rec = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                var count = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocation In Frame Count", 1);
                long bytes = 0, allocs = 0;
                int frames = 0;
                for (; frames < 300; frames++)
                {
                    player.Heal(9999f);
                    yield return null;
                    bytes += rec.LastValue;
                    allocs += count.LastValue;
                }
                rec.Dispose();
                count.Dispose();
                Write($"할당\t{label}\t프레임당 {bytes / (float)frames / 1024f:0.0} KB · {allocs / (float)frames:0}번");
            }

            void Toggle(System.Func<MonoBehaviour, bool> which, bool on) { foreach (var b in all) if (b != null && which(b)) b.enabled = on; }

            yield return Sample("모두 켬");
            Toggle(IsUi, false);
            yield return Sample("화면 UI 끔 (HUD · 경맥도 · 회귀록)");
            Toggle(IsUi, true);
            Toggle(IsArt, false);
            yield return Sample("아트 · 이펙트 끔");
            Toggle(IsUi, false);
            yield return Sample("둘 다 끔 (게임 코드만)");
            Toggle(IsUi, true);
            Toggle(IsArt, true);

            // 아트 · 이펙트를 종류별로 하나씩 꺼서 (그 종류만 끈 할당 − 모두 켬)이 큰 순서를 봐요
            var types = MeasureArtTypes ? all.Where(IsArt).Select(b => b.GetType()).Distinct().ToList() : new System.Collections.Generic.List<System.Type>();
            foreach (var type in types)
            {
                Toggle(b => b.GetType() == type, false);
                yield return Sample($"끔: {type.Name} ({all.Count(b => b != null && b.GetType() == type)}개)");
                Toggle(b => b.GetType() == type, true);
            }

            // 게임 코드도 종류별로: UI · 아트를 끈 채 (둘 다 끔 기준)에서 한 종류씩 더 꺼 봐요
            if (MeasureGameTypes)
            {
                bool IsGame(MonoBehaviour b) => !IsUi(b) && !IsArt(b) && (b.GetType().Namespace ?? "").StartsWith("AThousandDawns");
                Toggle(IsUi, false);
                Toggle(IsArt, false);
                yield return Sample("기준: UI · 아트 끔");
                var gameTypes = all.Where(IsGame).Select(b => b.GetType()).Distinct().ToList();
                foreach (var type in gameTypes)
                {
                    Toggle(b => b.GetType() == type, false);
                    yield return Sample($"게임 끔: {type.Name} ({all.Count(b => b != null && b.GetType() == type)}개)");
                    Toggle(b => b.GetType() == type, true);
                }
                Toggle(IsUi, true);
                Toggle(IsArt, true);
            }

            // 컴포넌트를 전부 끄고 남는 할당 = 에디터 · 테스트 러너 · 엔진 몫 (게임과 상관없는 바닥)
            Toggle(b => !(b is PlayerController), false);
            player.enabled = false;
            yield return Sample("전부 끔 (에디터 · 엔진 바닥)");
            Toggle(b => true, true);
            RoomRun.LayoutOverride = -1;
        }

#if UNITY_EDITOR
        // 할당 위치: 같은 싸움을 프로파일러로 잡아 GC.Alloc이 어느 Update 안에서 (콜스택이 있으면 어느 줄에서) 났는지 큰 순서로 적어요
        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator 할당_위치()
        {
            new MetaSave { runs = 1, tutorialFloorDone = true }.Save();
            RoomRun.SkipVillage = true;
            RoomRun.LayoutOverride = 2;
            yield return SceneManager.LoadSceneAsync(TitleScreen.RunScene, LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(3);
            run.BeginFork(2);
            run.ChooseFork(0);
            run.Enter(run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null), 0);
            yield return new WaitForSeconds(1f);
            Vector3 home = player.transform.position;
            var kinds = new[] { EnemyKind.Cultist, EnemyKind.BloodArcher, EnemyKind.DemonGuard, EnemyKind.BloodBomber };
            for (int i = 0; i < 12; i++) director.Spawn(kinds[i % 4], home + new Vector3(Mathf.Cos(i * 0.52f), 0f, Mathf.Sin(i * 0.52f)) * 6f);
            for (int i = 0; i < 30; i++) { player.Heal(9999f); yield return null; }

            UnityEditorInternal.ProfilerDriver.ClearAllFrames();
            UnityEditorInternal.ProfilerDriver.profileEditor = false;
            UnityEditorInternal.ProfilerDriver.memoryRecordMode = UnityEditorInternal.ProfilerMemoryRecordMode.GCAlloc;
            UnityEditorInternal.ProfilerDriver.enabled = true;
            for (int i = 0; i < 150; i++) { player.Heal(9999f); yield return null; }
            UnityEditorInternal.ProfilerDriver.enabled = false;

            var bytes = new System.Collections.Generic.Dictionary<string, float>();
            var calls = new System.Collections.Generic.Dictionary<string, int>();
            int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, lastFrame = UnityEditorInternal.ProfilerDriver.lastFrameIndex, frames = 0;
            var children = new System.Collections.Generic.List<int>();
            var path = new System.Collections.Generic.List<string>();
            for (int f = Mathf.Max(first, lastFrame - 120); f <= lastFrame; f++)
            {
                using var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.Default, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory, false);
                if (view == null || !view.valid) continue;
                frames++;
                void Walk(int id)
                {
                    var kids = new System.Collections.Generic.List<int>();
                    view.GetItemChildren(id, kids);
                    foreach (int c in kids)
                    {
                        string name = view.GetItemName(c);
                        if (name == "GC.Alloc")
                        {
                            string owner = path.Count > 0 ? path[path.Count - 1] : "";
                            int samples = view.GetItemMergedSamplesCount(c);
                            float perSample = view.GetItemColumnDataAsFloat(c, UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory) / Mathf.Max(1, samples);
                            for (int s = 0; s < samples; s++)
                            {
                                var addrs = new System.Collections.Generic.List<ulong>();
                                view.GetItemMergedSampleCallstack(c, s, addrs);
                                var where = new System.Collections.Generic.List<string>();
                                foreach (var a in addrs)
                                {
                                    var m = view.ResolveMethodInfo(a);
                                    if (m.methodName == null || !m.methodName.Contains("AThousandDawns")) continue;
                                    where.Add($"{m.methodName.Substring(m.methodName.LastIndexOf('.') + 1)}:{m.sourceFileLine}");
                                    if (where.Count >= 3) break;
                                }
                                string key = owner + " | " + (where.Count > 0 ? string.Join(" ← ", where) : $"(콜스택 없음 {addrs.Count})");
                                bytes[key] = (bytes.TryGetValue(key, out var b) ? b : 0f) + perSample;
                                calls[key] = (calls.TryGetValue(key, out var n) ? n : 0) + 1;
                            }
                            continue;
                        }
                        path.Add(name);
                        Walk(c);
                        path.RemoveAt(path.Count - 1);
                    }
                }
                Walk(view.GetRootItemID());
            }
            UnityEditorInternal.ProfilerDriver.memoryRecordMode = UnityEditorInternal.ProfilerMemoryRecordMode.None;
            File.AppendAllText(ReportPath, $"\n할당 위치 (프레임 {frames}개, 프레임당 바이트 · 번)\n", Encoding.UTF8);
            foreach (var kv in bytes.OrderByDescending(kv => kv.Value).Take(40))
                Write($"위치\t{kv.Value / Mathf.Max(1, frames):0}B\t{calls[kv.Key] / (float)Mathf.Max(1, frames):0.0}번\t{kv.Key}");
            RoomRun.LayoutOverride = -1;
        }
#endif

        // 보스 하나만 다시 잴 때 (예: --filter "AThousandDawns.Tests.BalanceProbe.보스_하나")
        public static EnemyKind OneKind = EnemyKind.BloodBuddha;
        public static int OneRegion = 1, OneKarma;

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator 보스_하나()
        {
            yield return Fight(OneKind, OneRegion, OneKarma, 4f, 300f, "보스");
        }

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator 조정_확인() // 최근에 고친 적만 다시 재요 (그때그때 바꿔 써요)
        {
            yield return Fight(EnemyKind.BloodBomber, 3, 0, 3f, 15f, "일반", false);
        }

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator 일반_적()
        {
            for (int r = 0; r < 4; r++)
                foreach (var k in RoomBudget.ForRegion(r))
                    yield return Fight(k, r, 0, 3f, 15f, "일반", false);
        }
    }
}
