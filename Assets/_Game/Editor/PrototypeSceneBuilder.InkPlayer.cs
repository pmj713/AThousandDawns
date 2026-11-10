using AThousandDawns.Player;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판: 플레이어 캡슐 대신 리깅 모델을 세우고, 게임의 평타·대시 신호에 동작을 이어요 (InkPlayerAnimator).
    // 모델은 PlayerController가 바라보는 방향으로 돌리는 Visual 아래에 둬요. 판정 범위(부채꼴)·피해는 게임 그대로예요.
    public static partial class PrototypeSceneBuilder
    {
        static void DressPlayerModelInk()
        {
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null) return;
            var controller = SetupPlayerRig();
            if (controller == null) return;   // 리깅 모델이 없으면 캡슐 그대로

            var visual = new SerializedObject(player).FindProperty("visual").objectReferenceValue as Transform;
            if (visual == null) return;

            // 캡슐 몸통·코는 안 보이게 (몸통 렌더러는 남겨 두지만 맞을 때 하얗게 번쩍임은 모델 몸통으로 옮겨요)
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;

            PlayerModel(visual, GetInkMaterial("Ink_PlayerSkin", PlayerSkinColor, 0.35f),
                GetInkMaterial("Ink_PlayerHair", PlayerHairColor, 0.1f), controller);

            Renderer torso = null;
            foreach (var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name.Contains("Torso")) torso = r;
            if (torso != null) SetRef(player, "bodyRenderer", torso);

            if (player.GetComponent<AThousandDawns.Art.InkPlayerAnimator>() == null)
                player.gameObject.AddComponent<AThousandDawns.Art.InkPlayerAnimator>();
        }
    }
}
