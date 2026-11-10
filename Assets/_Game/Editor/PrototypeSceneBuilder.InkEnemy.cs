using System.Linq;
using AThousandDawns.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판 적 모델: VARCO로 만들고 자동 리깅한 모델(Blender prepare_grunt.py가 정리)을 Humanoid로 가져와,
    // 플레이어 동작(대기·달리기·1타 내려찍기)을 옮겨 입혀요. 적은 실행 중에 생겨서 InkEnemyModels가 생길 때 갈아 끼워요.
    public static partial class PrototypeSceneBuilder
    {
        const string GruntFolder = "Assets/_Game/Art/Characters/Grunt";
        const string GruntModel = GruntFolder + "/Grunt_Rigged.fbx";
        const string GruntPrefab = GruntFolder + "/Grunt_Ink.prefab";
        const string GruntControllerPath = GruntFolder + "/Grunt.controller";

        static void DressEnemyModelsInk()
        {
            var entries = new System.Collections.Generic.List<AThousandDawns.Art.InkEnemyModels.Entry>();
            var grunt = SetupGrunt();
            if (grunt.prefab != null)
                entries.Add(new AThousandDawns.Art.InkEnemyModels.Entry
                {
                    kind = EnemyKind.Grunt, model = grunt.prefab, controller = grunt.controller, scale = 1f, yaw = 180f,
                });
            if (entries.Count == 0) return;
            var models = new GameObject("Ink Enemy Models").AddComponent<AThousandDawns.Art.InkEnemyModels>();
            models.SetEntries(entries.ToArray());
        }

        static (GameObject prefab, RuntimeAnimatorController controller) SetupGrunt()
        {
            if (!(AssetImporter.GetAtPath(GruntModel) is ModelImporter importer)) return (null, null);
            if (importer.animationType != ModelImporterAnimationType.Human || !importer.bakeAxisConversion)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            if (AssetDatabase.LoadAllAssetsAtPath(GruntModel).OfType<Avatar>().All(a => !a.isHuman))
                Debug.LogWarning($"{GruntModel}의 Humanoid 아바타가 만들어지지 않았어요");

            // 동작: 플레이어 동작을 그대로 옮겨 입혀요 (둘 다 Humanoid).
            // 플레이어 설정(SetupPlayerRig)은 Player.controller를 지우고 다시 만들어서, 이미 세운 플레이어 애니메이터 연결이 끊겨요.
            // 그래서 플레이어 모델이 아직 Humanoid로 안 가져와졌을 때만 불러요
            if (AssetImporter.GetAtPath(PlayerRiggedModel) is ModelImporter playerImporter
                && playerImporter.animationType != ModelImporterAnimationType.Human)
                SetupPlayerRig();
            var clips = AssetDatabase.LoadAllAssetsAtPath(PlayerRiggedModel).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            if (!clips.TryGetValue("Idle", out var idle) || !clips.TryGetValue("Run", out var run)) return (null, null);
            var attackClip = clips.TryGetValue("IKAttack1", out var ik) ? ik : clips.TryGetValue("Attack1", out var a1) ? a1 : null;

            AssetDatabase.DeleteAsset(GruntControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(GruntControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = controller.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sRun = sm.AddState("Run"); sRun.motion = run; sRun.speed = 0.75f;   // 졸개 걸음 3.5칸/초
            sm.defaultState = sIdle;
            var t = sIdle.AddTransition(sRun); t.hasExitTime = false; t.duration = 0.1f; t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t = sRun.AddTransition(sIdle); t.hasExitTime = false; t.duration = 0.12f; t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            if (attackClip != null)
            {
                // 예고 동안 첫 자세(치켜듦)에서 멈췄다가 판정 순간 제 속도로 한 번 내려찍어요 (InkEnemyAnimator가 AttackSpeed를 0/1로 바꿔요)
                var sAtk = sm.AddState("Attack"); sAtk.motion = attackClip;
                sAtk.speedParameterActive = true; sAtk.speedParameter = "AttackSpeed";
                // 공격 상태로는 InkEnemyAnimator가 직접 넘겨요 (트리거로 넘기면 여러 번 걸려 거듭 휘둘러요)
                var back = sAtk.AddTransition(sIdle); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.15f;
            }

            // 재질: 수묵 캐릭터 셰이더 + VARCO 색 텍스처. 채도는 지역 색 양(_InkColorAmount)을 따라요
            var body = GetInkMaterial("Ink_Grunt_Body", "#FFFFFF", 0.45f);
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{GruntFolder}/Grunt_Albedo.png"));
            body.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(body);
            var dao = GetInkMaterial("Ink_Grunt_Dao", "#3A3F44", 0.1f);

            // 프리팹: 모델 + 재질 + 애니메이터
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GruntModel);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.name.Contains("Dao") ? dao : body;
                r.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            }
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, GruntPrefab);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return (prefab, controller);
        }
    }
}
