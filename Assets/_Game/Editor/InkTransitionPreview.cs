using AThousandDawns.Fx;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 연출 미리보기: 플레이 중에 메뉴로 수묵 화면 전환을 띄워 봐요 (게임 흐름에 연결하기 전 확인용, 씬은 다시 부르지 않아요)
    public static class InkTransitionPreview
    {
        const string Menu = "AThousandDawns/아트/연출 미리보기/";

        [MenuItem(Menu + "회귀 (첫 회차)")] static void FirstDawn() => Play(InkTransition.Kind.Regression, true);
        [MenuItem(Menu + "회귀 (두 번째부터)")] static void Regression() => Play(InkTransition.Kind.Regression, false);
        [MenuItem(Menu + "마을 출발 (족자)")] static void Depart() => Play(InkTransition.Kind.Depart, false);
        [MenuItem(Menu + "층 사이 (먹 번짐)")] static void Floor() => Play(InkTransition.Kind.Floor, false);

        // 지역 사이 색 바램: 수묵 씬에서 지금 색에서 다음 지역 색으로 한 단계 (다시 누르면 그다음, 끝이면 처음으로)
        static int s_previewRegion;
        [MenuItem(Menu + "지역 사이 (색 바램)")]
        static void RegionDrain()
        {
            var zones = Object.FindFirstObjectByType<AThousandDawns.Art.InkColorZones>();
            if (zones == null) { Debug.LogWarning("수묵 씬(Prototype_Run_Ink)에서 플레이 중일 때만 돼요"); return; }
            s_previewRegion = (s_previewRegion + 1) % 4;
            zones.Drain(zones.RegionAmount(s_previewRegion));
            Debug.Log($"연출 미리보기: 지역 {s_previewRegion} 색 {zones.RegionAmount(s_previewRegion)}");
        }

        [MenuItem(Menu + "회귀 (첫 회차)", true)]
        [MenuItem(Menu + "회귀 (두 번째부터)", true)]
        [MenuItem(Menu + "마을 출발 (족자)", true)]
        [MenuItem(Menu + "층 사이 (먹 번짐)", true)]
        [MenuItem(Menu + "지역 사이 (색 바램)", true)]
        static bool CanPlay() => Application.isPlaying;

        static void Play(InkTransition.Kind kind, bool first) => InkTransition.Play(kind, () => Debug.Log($"연출 미리보기: {kind} 가운데 (여기서 씬을 다시 불러요)"), first);
    }
}
