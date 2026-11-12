using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // Windows 실행 파일 빌드 (메뉴 AThousandDawns/Windows 빌드, 배치: -executeMethod AThousandDawns.EditorTools.BuildWindows.Batch).
    // 결과: 프로젝트 폴더 Builds/Windows/AThousandDawns.exe. 씬은 Build Settings에 켜 둔 것 + 수묵 판 씬 (Prototype_Run_Ink).
    public static class BuildWindows
    {
        public const string OutputPath = "Builds/Windows/AThousandDawns.exe";

        [MenuItem("AThousandDawns/Windows 빌드")]
        public static void Menu() => Build();

        public static void Batch()
        {
            var report = Build();
            EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            // 수묵 판 씬은 빌드 목록에 없어도 넣어요 (시작 화면이 있으면 그 씬으로 들어가요, TitleScreen.RunScene)
            const string ink = "Assets/_Game/Scenes/" + AThousandDawns.Prototype.TitleScreen.InkRunScene + ".unity";
            if (File.Exists(ink) && !scenes.Contains(ink)) scenes.Add(ink);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-cleanbuild") >= 0 ? BuildOptions.CleanBuildCache : BuildOptions.None, // -cleanbuild: 빌드 캐시를 비우고 새로
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[빌드] {s.result} · 씬 {scenes.Count}개 ({string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}) · 크기 {s.totalSize / (1024f * 1024f):0.0}MB · {s.totalTime.TotalSeconds:0}초 · 오류 {s.totalErrors} · 경고 {s.totalWarnings} · {Path.GetFullPath(OutputPath)}");
            return report;
        }
    }
}
