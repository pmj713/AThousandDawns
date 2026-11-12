using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 앱 아이콘 (一朝千回 낙관, Art/UI/AppIcon/AppIcon.png): 에디터가 켜질 때 Player Settings의 기본 아이콘이 비어 있으면 넣어요.
    // ProjectSettings 파일을 손으로 고치지 않고 유니티가 직접 저장하게 해요 (이미 다른 아이콘을 넣었으면 건드리지 않아요)
    [InitializeOnLoad]
    static class AppIconSetup
    {
        const string IconPath = "Assets/_Game/Art/UI/AppIcon/AppIcon.png";

        static AppIconSetup() => EditorApplication.delayCall += Apply;

        [MenuItem("AThousandDawns/Art/Apply App Icon (앱 아이콘 넣기)")]
        static void ApplyFromMenu() => Apply(force: true);

        static void Apply() => Apply(force: false);

        static void Apply(bool force)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) return;
            var current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (!force && current.Length > 0 && current[0] != null) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log($"[앱 아이콘] 기본 아이콘을 {IconPath} 로 넣었어요.");
        }
    }
}
