using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // VARCO로 만든 정적 소품 (Art/Env/Rift/Models: 원본 glb는 Source~, 변환은 Tools~/prepare_prop.py — 밑면 0 · 가운데 원점 · 높이 맞춤 · 앞 +Z).
    //   축복 제단(BlessingAltar, 높이 1.9) · 마나 수정(ManaCrystal, 높이 1.5). 재질은 채색 캐릭터 셰이더에 그 소품 색 텍스처
    public static partial class PrototypeSceneBuilder
    {
        const string VarcoPropFolder = "Assets/_Game/Art/Env/Rift/Models";

        const string VillagePropFolder = "Assets/_Game/Art/Env/Village/Models";   // 마을 소품 (무기 거치대 부품 · 화분 · 영혼 수련 동굴 입구)

        const string RoomPropFolder = "Assets/_Game/Art/Env/Rooms/Models";   // 방 소품 (객잔 술독 · 대장간 화로)

        // 비도 (플레이테스트 5차: 회색 상자 대신 VARCO 비도 — 양날 강철 날, 검은 감개 손잡이, 꼭지 고리에 붉은 술, 길이 0.55).
        //   Resources/Models/FlyingDagger.prefab: 칼끝이 +Z, 가운데가 원점 (게임 코드가 LookRotation(날아가는 쪽)으로 돌려요). 충돌체 없음.
        //   모델 변환은 Rift/Tools~/prepare_blade.py (칼끝 → 소품 앞, 그래서 yaw 180이면 +Z)
        const string WeaponPropFolder = "Assets/_Game/Art/Characters/Player/Weapons";
        const string FlyingDaggerPrefabPath = "Assets/_Game/Resources/Models/FlyingDagger.prefab";

        static void BuildFlyingDaggerPrefab()
        {
            BuildBladePrefab("FlyingDagger", FlyingDaggerPrefabPath);
            // 버들잎 표창 기물 (플레이테스트 8차: VARCO 버들잎 날 — 가운데 능선, 검은 감개 손잡이, 고리에 초록 술, 길이 0.4). 비도와 같은 규칙.
            //   아트 2의 InkRuntimeDress가 "버들잎 표창" 루트에 끼워요
            BuildBladePrefab("WillowDart", "Assets/_Game/Resources/Models/WillowDart.prefab");
        }

        static void BuildBladePrefab(string name, string prefabPath)
        {
            var root = new GameObject(name);
            if (VarcoProp(root.transform, name, Vector3.zero, 180f, 1f, WeaponPropFolder) is Transform model)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                EnsureFolder("Assets/_Game/Resources/Models");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            Object.DestroyImmediate(root);
        }

        // 녹림왕 2페이즈 불타는 대나무 (플레이테스트 6차: 초록 원기둥 대신 VARCO 대나무 — 마디 일곱, 위에 잎 달린 잔가지, 꺾인 밑동은 그을림).
        //   Resources/Models/BurningBamboo.prefab: 밑동이 원점, 길이 1로 +Y (InkBossPatternFx가 쓰러지는 길이만큼 통째로 키워요). 충돌체 없음.
        //   모델 변환은 prepare_blade.py (가는 끝 = 꺾인 밑동이 소품 앞 → 세우면 아래)
        const string BurningBambooPrefabPath = "Assets/_Game/Resources/Models/BurningBamboo.prefab";

        static void BuildBurningBambooPrefab()
        {
            var root = new GameObject("BurningBamboo");
            if (VarcoProp(root.transform, "FallenBamboo", Vector3.zero, 0f, 1f, RoomPropFolder) is Transform model)
            {
                model.localRotation = Quaternion.Euler(-90f, 0f, 0f) * model.localRotation;   // 소품 앞(밑동)이 아래로 서요
                model.localPosition = new Vector3(0f, 0.5f, 0f);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                EnsureFolder("Assets/_Game/Resources/Models");
                PrefabUtility.SaveAsPrefabAsset(root, BurningBambooPrefabPath);
            }
            Object.DestroyImmediate(root);
        }

        static Transform VarcoProp(Transform parent, string name, Vector3 localPos, float yaw, float scale = 1f, string folder = VarcoPropFolder)
        {
            string path = $"{folder}/{name}.fbx";
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
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_Albedo.png"));
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
