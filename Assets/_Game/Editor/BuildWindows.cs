using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // Windows 실행 파일 빌드 (메뉴 AThousandDawns/Windows 빌드, 배치: -executeMethod AThousandDawns.EditorTools.BuildWindows.Batch).
    // 결과: 프로젝트 폴더 Builds/Windows/AThousandDawns.exe. 씬은 Build Settings 그대로 (시작 화면 · 판 · 샌드박스).
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
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[빌드] {s.result} · 씬 {scenes.Length}개 · 크기 {s.totalSize / (1024f * 1024f):0.0}MB · {s.totalTime.TotalSeconds:0}초 · 오류 {s.totalErrors} · 경고 {s.totalWarnings} · {Path.GetFullPath(OutputPath)}");
            return report;
        }
    }
}
