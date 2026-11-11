using AThousandDawns.Art;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 메뉴 AThousandDawns → 아트 → 수묵 강도 조절: 설정 파일(Art/Resources/InkLook.asset)을 Inspector에 열어요 (없으면 만들어요)
    public static class InkLookMenu
    {
        const string Folder = "Assets/_Game/Art/Resources";
        const string AssetPath = Folder + "/InkLook.asset";

        [MenuItem("AThousandDawns/아트/수묵 강도 조절")]
        static void Open()
        {
            var settings = AssetDatabase.LoadAssetAtPath<InkLookSettings>(AssetPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game/Art", "Resources");
                settings = ScriptableObject.CreateInstance<InkLookSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
            }
            settings.Apply();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        // 에디터를 켜면(스크립트를 다시 불러오면) 설정 값을 씬 화면에도 넣어요
        [InitializeOnLoadMethod]
        static void ApplyInEditor()
        {
            EditorApplication.delayCall += () =>
            {
                var settings = AssetDatabase.LoadAssetAtPath<InkLookSettings>(AssetPath);
                if (settings != null) settings.Apply();
            };
        }
    }
}
