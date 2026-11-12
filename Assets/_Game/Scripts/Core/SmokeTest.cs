using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Core
{
    // 빌드 점검 (실행 파일을 -smoketest로 켜면): 시작 화면 → 판 씬을 차례로 불러 보고, 저장 위치 · 셰이더 · Resources 그림 · 프레임을 적은 뒤 스스로 꺼져요.
    // 결과: persistentDataPath/smoketest.txt (실행 파일 옆 로그에도 [점검] 줄로 남아요). 평소 실행에는 아무 일도 안 해요.
    public sealed class SmokeTest : MonoBehaviour
    {
        static readonly string[] Shaders = { "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Lit", "Sprites/Default" };
        static readonly string[] ArtFolders = { "EventArt", "HintArt", "BookArt", "TitleArt", "HudArt", "PadArt", "FxArt", "ItemIcons" };

        static string RealMeta;

        readonly StringBuilder _log = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-smoketest") < 0) return;
            // 판 씬을 열면 새 판이 시작돼 이어하기 저장을 지워요. 진짜 저장을 건드리지 않게 임시 자리로 돌려요 (진짜 저장 위치는 먼저 적어 둬요)
            RealMeta = MetaSave.FilePath;
            string temp = Path.Combine(Application.temporaryCachePath, "smoketest");
            Directory.CreateDirectory(temp);
            MetaSave.PathOverride = Path.Combine(temp, "meta.json");
            GameSettings.PathOverride = Path.Combine(temp, "settings.json");
            var go = new GameObject("빌드 점검");
            DontDestroyOnLoad(go);
            go.AddComponent<SmokeTest>();
        }

        void Line(string text)
        {
            Debug.Log("[점검] " + text);
            _log.AppendLine(text);
        }

        IEnumerator Start()
        {
            Line($"버전 {Application.version} · Unity {Application.unityVersion} · {SystemInfo.graphicsDeviceType} · {Screen.width}×{Screen.height}");
            Line($"저장 위치 {Application.persistentDataPath}");
            Line($"meta {RealMeta} (있음 {File.Exists(RealMeta)}) · settings 있음 {File.Exists(Path.Combine(Application.persistentDataPath, "settings.json"))} · 점검은 임시 저장 {MetaSave.PathOverride}");
            foreach (var name in Shaders) Line($"셰이더 {name}: {(Shader.Find(name) != null ? "있음" : "없음 ←")}");
            foreach (var folder in ArtFolders) Line($"Resources/{folder}: 그림 {Resources.LoadAll<Texture2D>(folder).Length}장");

            yield return Measure("시작 화면", 3f);
            yield return Load(AThousandDawns.Prototype.TitleScreen.RunScene); // 시작 화면이 들어가는 판 (수묵이 있으면 수묵)
            Line($"설정 {(GameConfig.Balance != null ? "있음" : "없음 ←")} · 기물 {(GameConfig.Items != null ? GameConfig.Items.Items.Count : 0)}개 (판 씬의 GameConfig)");
            yield return Measure("판 (첫 방)", 5f);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-perf") >= 0) yield return Perf();
            Line("끝");
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, "smoketest.txt"), _log.ToString(), Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning(e.Message); }
            Application.Quit();
        }

        // -perf: 천마총 2층 싸우는 방에 적을 더 불러 15초 동안 프레임 · 쓰레기 수거를 재요 (플레이어는 죽지 않게 회복)
        IEnumerator Perf()
        {
            var run = FindFirstObjectByType<AThousandDawns.Prototype.RoomRun>();
            var director = FindFirstObjectByType<AThousandDawns.Enemies.EnemyDirector>();
            var player = FindFirstObjectByType<AThousandDawns.Player.PlayerController>();
            if (run == null || director == null || player == null) { Line("성능: 판을 못 찾았어요 ←"); yield break; }
            run.SetRegionForTest(3);
            run.BeginFork(2);
            run.ChooseFork(0);
            AThousandDawns.Prototype.FloorRoom fight = null;
            foreach (var r in run.Map.Rooms) if (r.HasFight && r.FixedWaves == null) { fight = r; break; }
            if (fight != null) run.Enter(fight, 0);
            yield return new WaitForSeconds(1.5f);
            Vector3 home = player.transform.position;
            var kinds = new[] { AThousandDawns.Enemies.EnemyKind.Cultist, AThousandDawns.Enemies.EnemyKind.BloodArcher, AThousandDawns.Enemies.EnemyKind.DemonGuard, AThousandDawns.Enemies.EnemyKind.BloodBomber };
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                director.Spawn(kinds[i % kinds.Length], home + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 6f);
            }
            int gc0 = GC.CollectionCount(0);
            long heap0 = GC.GetTotalMemory(false);
            var times = new System.Collections.Generic.List<float>(4096);
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 15f)
            {
                player.Heal(9999f);
                yield return null;
                times.Add(Time.unscaledDeltaTime * 1000f);
            }
            times.Sort();
            float avg = 0f;
            foreach (var t in times) avg += t;
            avg /= Mathf.Max(1, times.Count);
            float p95 = times.Count > 0 ? times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.95f))] : 0f;
            float worst = times.Count > 0 ? times[times.Count - 1] : 0f;
            Line($"성능 (천마총 싸움 · 적 {director.Enemies.Count}명 남음): 평균 {1000f / Mathf.Max(0.01f, avg):0} fps ({avg:0.0}ms) · 95% {p95:0.0}ms · 가장 느린 {worst:0}ms · 프레임 {times.Count}");
            Line($"쓰레기 수거 {GC.CollectionCount(0) - gc0}번 · 힙 {(GC.GetTotalMemory(false) - heap0) / 1024} KB 늘어남 · 화면 {Screen.width}×{Screen.height} · {SystemInfo.graphicsDeviceName}");
        }

        IEnumerator Load(string scene)
        {
            float t = Time.realtimeSinceStartup;
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            Line($"{scene} 불러오기 {Time.realtimeSinceStartup - t:0.00}초 {(op == null ? "(실패 ←)" : "")}");
        }

        IEnumerator Measure(string label, float seconds)
        {
            int frames = 0;
            float worst = 0f, start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }
            float elapsed = Time.realtimeSinceStartup - start;
            Line($"{label}: 평균 {frames / Mathf.Max(0.01f, elapsed):0} fps · 가장 느린 프레임 {worst * 1000f:0}ms");
        }
    }
}
