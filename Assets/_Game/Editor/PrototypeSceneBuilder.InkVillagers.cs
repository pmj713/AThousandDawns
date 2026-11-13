using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 마을 주민 (VARCO 새 모델 없이 있는 사람 모델로): 무기를 빼고 생활 소품을 쥔 모델을 Villagers/<이름>에 따로 만들어 둬요
    // (Enemies/Tools~/prepare_enemy.py villager=…, 적 모델 파일은 그대로). 저마다 반복 동작 "Life" 하나를 되풀이해요.
    //  - 우물가: 두레박 줄을 당기는 사람 (작살꾼 모델) · 장작더미: 도끼로 장작 패는 사람 (수적)
    //  - 사당 앞: 비질하는 늙은 스님 (목탁승) · 객잔 앞 걸상: 걸터앉아 쉬는 선비 (무림맹 고수)
    // 자리는 마을 가운데 기준 (생활 소품과 같은 기준). 충돌 없음, 화면 밖이면 애니메이터가 쉬어요
    public static partial class PrototypeSceneBuilder
    {
        const string VillagersFolder = CharactersFolder + "/Villagers";

        static void BuildVillagers(Transform root, Material wood)
        {
            // (모델, 자리, 바라보는 방향(도), 동작 시작 위치 0~1)
            var villagers = new (string name, Vector3 at, float yaw, float offset)[]
            {
                ("VillagerWell", new Vector3(6.5f, 0f, 3.25f), 180f, 0f),     // 우물 북쪽에서 우물 너머 남쪽(카메라 쪽)을 보며 줄을 당겨요
                ("VillagerChop", new Vector3(6.75f, 0f, 6.2f), -100f, 0.3f),  // 모탕 동쪽에 옆으로 서서 내려찍어요 (휘두름이 보이게)
                ("VillagerSweep", new Vector3(-5.3f, 0f, -6.4f), -100f, 0.6f), // 사당 앞마당을 쓸어요
                ("VillagerSit", new Vector3(-8.35f, 0f, -1.7f), 90f, 0.15f),  // 객잔 앞 걸상에 앉아 마을을 봐요
            };
            foreach (var (name, at, yaw, offset) in villagers)
            {
                var prefab = SetupVillager(name, offset);
                if (prefab == null) { Debug.LogWarning($"[마을 주민] {name} 모델이 없어요 ({VillagersFolder}/{name})"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = $"{name} (마을 주민)";
                go.transform.localPosition = at;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                if (name == "VillagerSit")   // 앉을 걸상 (엉덩이 밑, 등 뒤로 조금)
                {
                    var seat = Quaternion.Euler(0f, yaw, 0f);
                    Block(root, "Bench (걸상)", PrimitiveType.Cube, at + seat * new Vector3(0f, 0.3f, -0.12f), new Vector3(1.3f, 0.08f, 0.42f), seat, wood);
                    for (int s = -1; s <= 1; s += 2)
                        Block(root, "Bench Leg", PrimitiveType.Cube, at + seat * new Vector3(s * 0.5f, 0.14f, -0.12f), new Vector3(0.08f, 0.28f, 0.36f), seat, wood);
                }
            }
        }

        // 주민 모델 하나: Generic으로 가져와 Life를 반복, 수묵 캐릭터 재질, 동작 하나짜리 애니메이터 → 프리팹
        static GameObject SetupVillager(string name, float offset)
        {
            string folder = $"{VillagersFolder}/{name}";
            string modelPath = $"{folder}/{name}_Rigged.fbx";
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return null;
            if (importer.animationType != ModelImporterAnimationType.Generic || !importer.bakeAxisConversion || !importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            var clips = importer.defaultClipAnimations;
            if (clips.Length > 0 && (importer.clipAnimations.Length != clips.Length || importer.clipAnimations.Any(c => c.name == "Life" && !c.loopTime)))
            {
                foreach (var clip in clips)
                {
                    clip.name = clip.takeName.Split('|').Last();
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
                    clip.loopTime = clip.name == "Life" || clip.name == "Idle";
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            var life = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Life");
            if (life == null) return null;

            string controllerPath = $"{folder}/{name}.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var state = controller.layers[0].stateMachine.AddState("Life");
            state.motion = life;
            state.cycleOffset = offset;   // 주민마다 박자가 어긋나게

            var body = GetInkMaterial($"Ink_{name}_Body", "#FFFFFF", 0.45f);
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_Albedo.png"));
            body.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(body);
            var tool = GetInkMaterial("Ink_Villager_Tool", "#6B5A44", 0.35f);   // 나무 자루·싸리 (도끼날도 같은 먹빛)

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = Enumerable.Repeat(r.name.Contains("Weapon") ? tool : body, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;   // 마을이 화면 밖이면 쉬어요
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}_Ink.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
