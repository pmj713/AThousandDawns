using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // VARCO로 만든 정적 소품 (Art/Env/Rift/Models: 원본 glb는 Source~, 변환은 Tools~/prepare_prop.py — 밑면 0 · 가운데 원점 · 높이 맞춤 · 앞 +Z).
    //   축복 제단(BlessingAltar, 높이 1.9) · 마나 수정(ManaCrystal, 높이 1.5). 재질은 채색 캐릭터 셰이더에 그 소품 색 텍스처
    public static partial class PrototypeSceneBuilder
    {
        const string VarcoPropFolder = "Assets/_Game/Art/Env/Rift/Models";

        static Transform VarcoProp(Transform parent, string name, Vector3 localPos, float yaw, float scale = 1f)
        {
            string path = $"{VarcoPropFolder}/{name}.fbx";
            if (AssetImporter.GetAtPath(path) is ModelImporter importer &&
                (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation || importer.animationType != ModelImporterAnimationType.None || !importer.bakeAxisConversion))
            {
                importer.bakeAxisConversion = true;   // Blender 축 변환을 메시에 굳혀요 (안 하면 루트에 −90° 회전이 남는데, 아래에서 회전을 덮어써 모델이 누웠어요)
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            var mat = FantasyLook(GetInkMaterial($"Ink_Varco_{name}", "#FFFFFF", 1f));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{VarcoPropFolder}/{name}_Albedo.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_OutlineWidth", 2.5f);
            EditorUtility.SetDirty(mat);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);   // 변환한 FBX는 Unity에서 위(+Z)가 앞으로 누워 들어와요 → X축 90°로 세워요
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
            return go.transform;
        }
    }
}
