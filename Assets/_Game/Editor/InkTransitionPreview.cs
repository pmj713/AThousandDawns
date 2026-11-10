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

        [MenuItem(Menu + "회귀 (첫 회차)", true)]
        [MenuItem(Menu + "회귀 (두 번째부터)", true)]
        [MenuItem(Menu + "마을 출발 (족자)", true)]
        [MenuItem(Menu + "층 사이 (먹 번짐)", true)]
        static bool CanPlay() => Application.isPlaying;

        static void Play(InkTransition.Kind kind, bool first) => InkTransition.Play(kind, () => Debug.Log($"연출 미리보기: {kind} 가운데 (여기서 씬을 다시 불러요)"), first);
    }
}
