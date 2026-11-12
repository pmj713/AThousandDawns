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
        IEnumerator Room(int region, int layout, int karma, bool dense, float limit = 120f, bool moving = false)
        {
            new MetaSave { runs = 1, finalBossKills = karma > 0 ? karma : 0, karma = karma, karmaMax = karma, tutorialFloorDone = true }.Save();
            RoomRun.SkipVillage = true;
            RoomRun.SeedOverride = 1000 + region * 10 + layout;
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
                float best = float.MaxValue, bestHidden = float.MaxValue;
                foreach (var e in director.Enemies)
                {
                    if (e == null || e.IsDead) continue;
                    if (seen.Add(e)) spawned++;
                    float d = (e.transform.position - home).sqrMagnitude;
                    if (e.IsAwake && e.Damageable.Targetable) { if (d < best) { best = d; target = e; } }
                    else if (d < bestHidden) { bestHidden = d; hidden = e; }
                }
                if (target == null) target = hidden;
                if (target == null) continue;
                carry += dps * Time.deltaTime;
                int dmg = Mathf.FloorToInt(carry);
                if (dmg > 0) { target.Damageable.ApplyHit(dmg, false); carry -= dmg; }
            }
            Time.timeScale = 1f;
            RoomRun.LayoutOverride = -1;
            RoomRun.SeedOverride = 0;
            string[] names = region == 0 ? RoomRun.BambooLayoutNames : region == 1 ? RoomRun.TempleLayoutNames : region == 2 ? RoomRun.RiverLayoutNames : RoomRun.TombLayoutNames;
            float bars = taken / MaxHealth[region];
            Write($"방{(moving ? "(움직임)" : "")}\tr{region}\tk{karma}\t{(dense ? "짙은 길" : "옅은 길")} · {names[Mathf.Clamp(layout, 0, 3)]}\t적 {spawned} · 웨이브 {run.WaveCount}\t처치 {(run.IsFighting ? $"못함({t:0}초)" : t.ToString("0.0") + "초")}\t받은 피해 {taken:0} ({bars:0.00}칸)\t초당 {taken / Mathf.Max(1f, t):0.0}\t맞은 수 {hits}");
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
