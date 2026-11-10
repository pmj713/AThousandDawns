using System.Linq;
using AThousandDawns.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판 적 모델: VARCO로 만들고 자동 리깅한 사람형 적(Blender Enemies/Tools~/prepare_enemy.py가 정리)을 Humanoid로 가져와,
    // 플레이어 동작(대기·달리기·1타 내려찍기)을 옮겨 입혀요. 적은 실행 중에 생겨서 InkEnemyModels가 생길 때 갈아 끼워요.
    public static partial class PrototypeSceneBuilder
    {
        const string CharactersFolder = "Assets/_Game/Art/Characters";

        // 사람형 적: 폴더 이름(= 파일 앞 이름), 게임 종류, 이동 속도(칸/초, 달리기 동작 빠르기 맞춤)
        static readonly (string name, EnemyKind kind, float moveSpeed)[] InkEnemies =
        {
            ("Grunt", EnemyKind.Grunt, 3.5f),
            ("Archer", EnemyKind.Archer, 3f),
            ("Shield", EnemyKind.Shield, 2.5f),
            ("Horn", EnemyKind.Horn, 3f),
            ("Bomber", EnemyKind.Bomber, 4.5f),
            ("Assassin", EnemyKind.Assassin, 5f),
        };

        static void DressEnemyModelsInk()
        {
            var entries = new System.Collections.Generic.List<AThousandDawns.Art.InkEnemyModels.Entry>();
            foreach (var (name, kind, moveSpeed) in InkEnemies)
            {
                var made = SetupEnemy(name, moveSpeed);
                if (made.prefab != null)
                    entries.Add(new AThousandDawns.Art.InkEnemyModels.Entry
                    {
                        kind = kind, model = made.prefab, controller = made.controller, scale = 1f, yaw = 180f,
                        // 나팔수·궁수: 예고 동안 나팔 불기·시위 당기기 동작을 그대로 재생
                        windupPlays = kind == EnemyKind.Horn || kind == EnemyKind.Archer,
                        noAttackAnim = kind == EnemyKind.Bomber,   // 화약 산적: 손 들지 않고 그대로 터져요
                    });
            }
            if (entries.Count == 0) return;
            var models = new GameObject("Ink Enemy Models").AddComponent<AThousandDawns.Art.InkEnemyModels>();
            models.SetEntries(entries.ToArray());
        }

        static (GameObject prefab, RuntimeAnimatorController controller) SetupEnemy(string name, float moveSpeed)
        {
            string folder = $"{CharactersFolder}/{name}";
            string modelPath = $"{folder}/{name}_Rigged.fbx";
            string controllerPath = $"{folder}/{name}.controller";
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return (null, null);
            if (importer.animationType != ModelImporterAnimationType.Human || !importer.bakeAxisConversion || !importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                importer.importAnimation = true;   // 고유 동작(나팔수 BlowHorn 등)이 들어 있으면 써요
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            // 고유 동작(BlowHorn·ShootBow)도 플레이어 동작처럼 몸통 방향·위치를 구워 넣어요.
            // 기본값(몸 방향 기준 루트 회전)으로 두면 모델을 돌려 세운 방향과 어긋나 뒤돌아서 쏴요
            var ownClips = importer.defaultClipAnimations;
            if (ownClips.Length > 0 && (importer.clipAnimations.Length != ownClips.Length || importer.clipAnimations.Any(c => !c.lockRootRotation || !c.keepOriginalOrientation)))
            {
                foreach (var clip in ownClips)
                {
                    clip.name = clip.takeName.Split('|').Last();
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = ownClips;
                importer.SaveAndReimport();
            }
            if (AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().All(a => !a.isHuman))
                Debug.LogWarning($"{modelPath}의 Humanoid 아바타가 만들어지지 않았어요");

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
            // 모델에 고유 공격 동작이 있으면 그걸로 (나팔수 BlowHorn·궁수 ShootBow — 예고 동안 그대로 재생해요)
            var own = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.name.Split('|').Last() is "BlowHorn" or "ShootBow");
            if (own != null) attackClip = own;

            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = controller.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sRun = sm.AddState("Run"); sRun.motion = run; sRun.speed = 1.2f * moveSpeed / 6f;   // 플레이어 달리기(6칸/초, 1.2배)에 맞춰
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
            var body = GetInkMaterial($"Ink_{name}_Body", "#FFFFFF", 0.45f);
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_Albedo.png"));
            body.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(body);
            var weapon = GetInkMaterial("Ink_Enemy_Weapon", "#3A3F44", 0.1f);
            var red = GetInkMaterial("Ink_Enemy_Red", "#B3261E", 0.9f);       // 깃발·목도리처럼 알아보는 붉은 천

            // 프리팹: 모델 + 재질 + 애니메이터
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material m;
                int pi = r.name.IndexOf("_Prop_");
                if (pi >= 0)
                {
                    // VARCO 소품: 자기 색 텍스처를 쓰는 수묵 재질 (방패·삿갓·나팔·폭탄)
                    string key = r.name.Substring(pi + 6);
                    m = GetInkMaterial($"Ink_{name}_{key}", "#FFFFFF", 0.6f);
                    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}_Prop_{key}_Albedo.png"));
                    m.SetColor("_BaseColor", Color.white);
                    EditorUtility.SetDirty(m);
                }
                else if (r.name.Contains("_Red_")) m = red;
                else m = r.name.Contains("Weapon") || r.name.Contains("Dao") ? weapon : body;
                r.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            }
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}_Ink.prefab");
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return (prefab, controller);
        }
    }
}
