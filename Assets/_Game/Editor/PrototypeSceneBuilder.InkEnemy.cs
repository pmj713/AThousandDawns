using System.Collections.Generic;
using System.Linq;
using AThousandDawns.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Move = AThousandDawns.Art.InkEnemyModels.Move;
using MoveMode = AThousandDawns.Art.InkEnemyModels.MoveMode;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판 적 모델: VARCO로 만들고 자동 리깅한 사람형 적(Blender Enemies/Tools~/prepare_enemy.py가 정리)을 Humanoid로 가져와,
    // 플레이어 동작(대기·달리기·1타 내려찍기)을 옮겨 입혀요. 적은 실행 중에 생겨서 InkEnemyModels가 생길 때 갈아 끼워요.
    // 고수·보스는 패턴마다 다른 동작(플레이어 베기·회전베기·달리기, 모델 고유 동작)을 써요.
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
            ("Nokrim", EnemyKind.Nokrim, 3f),
            ("Qingzhu", EnemyKind.Qingzhu, 5f),
            ("IronFist", EnemyKind.IronFist, 3.5f),
            ("Boar", EnemyKind.Boar, 2.5f),
            ("Hound", EnemyKind.Hound, 5f),
            ("Snake", EnemyKind.Snake, 4f),
        };

        // 패턴 동작: attack(브레인 AttackName, null이면 다른 동작이 이어 부르는 상태) → state.
        // clips가 여럿이면 차례로 이어져요. seconds > 0이면 한 동작이 그 시간에 끝나도록 빠르기를 맞춰요 (연격 간격).
        // speed는 달리기·되풀이 빠르기 배수. after는 빈틈이 시작될 때 이어 재생할 상태
        struct MoveSpec
        {
            public string attack, state, after, ready;
            public string[] clips;
            public MoveMode mode;
            public float seconds, speed;
        }

        // 모델 고유 동작 중 반복하는 것
        static readonly HashSet<string> LoopingOwnClips = new HashSet<string> { "ChargeRun", "ShoulderRun", "Idle", "Run" };

        // 고유 동작만 쓰는 적 (Generic으로 가져와요)
        static readonly HashSet<string> GenericRigs = new HashSet<string> { "Nokrim", "Qingzhu", "IronFist", "Boar", "Hound", "Snake" };   // 동물은 뼈를 직접 심어(prepare_animal.py) 사람 뼈대가 아니에요

        static readonly Dictionary<string, MoveSpec[]> InkEnemyMoves = new Dictionary<string, MoveSpec[]>
        {
            // 녹림왕 (고유 동작): 대도 휘두르기(오른쪽 뒤로 감았다 허리째 가로 베기) · 돌진(낮게 숙인 준비 → 대도를 끌며 달리기) → 끝에서 내려찍기 · 회전베기
            ["Nokrim"] = new[]
            {
                new MoveSpec { attack = "대도 휘두르기", state = "Sweep", clips = new[] { "Sweep" } },
                new MoveSpec { state = "ChargeReady", clips = new[] { "ChargeReady" }, mode = MoveMode.Run },
                new MoveSpec { attack = "돌진", state = "Charge", clips = new[] { "ChargeRun" }, mode = MoveMode.Run, speed = 1.4f, after = "Slam", ready = "ChargeReady" },
                new MoveSpec { state = "Slam", clips = new[] { "Slam" }, mode = MoveMode.Run },   // 양손 내려찍기, 끝 자세로 멈춰 있다가 빈틈이 끝나면 대기로
                // 회전베기(2페이즈): 두 팔을 벌리고 칼끝을 바깥으로 뻗은 자세(Whirl)로 판정 동안 모델째 빙글빙글 (힐윈드처럼)
                new MoveSpec { attack = "회전베기", state = "Spin", clips = new[] { "Whirl" }, mode = MoveMode.Whirl },
            },
            // 멧돼지: 이름 없는 기본 공격(돌진) — 예고 동안 머리를 숙여 버티다가(Ready) 판정 동안 빠르게 달려요
            ["Boar"] = new[]
            {
                new MoveSpec { state = "Ready", clips = new[] { "Ready" }, mode = MoveMode.Run },
                new MoveSpec { attack = "", state = "Charge", clips = new[] { "Run" }, mode = MoveMode.Run, speed = 2.2f, ready = "Ready" },
            },
            // 청죽객: 쾌검 3연격(베기 셋을 0.3초씩) · 죽림섬(순간이동 베기 0.25초씩) · 대나무 베기(내려찍기)
            ["Qingzhu"] = new[]
            {
                // 고유 동작 (Generic): 내려베기 → 되받아 가로베기 → 찌르기 / 좌우 번갈아 가로베기 4번 / 머리 위에서 내려베기
                new MoveSpec { attack = "쾌검 3연격", state = "Combo", clips = new[] { "Combo" } },
                new MoveSpec { attack = "죽림섬", state = "Flash", clips = new[] { "Flash" } },
                new MoveSpec { attack = "대나무 베기", state = "Chop", clips = new[] { "Chop" } },
            },
            // 철권 마웅: 주먹 2연타(고유 동작) · 붕산권(예고부터 치켜들었다 땅 내려치기) · 돌진(달리기)
            ["IronFist"] = new[]
            {
                new MoveSpec { attack = "주먹 2연타", state = "Jab", clips = new[] { "Punch2" } },
                new MoveSpec { attack = "붕산권", state = "Quake", clips = new[] { "Quake" }, mode = MoveMode.Play },
                new MoveSpec { state = "ShoulderReady", clips = new[] { "ShoulderReady" }, mode = MoveMode.Run },
                new MoveSpec { attack = "돌진", state = "Charge", clips = new[] { "ShoulderRun" }, mode = MoveMode.Run, speed = 1.6f, ready = "ShoulderReady" },   // 어깨를 앞세워 들이받으며 달려요
            },
        };

        static void DressEnemyModelsInk()
        {
            var entries = new List<AThousandDawns.Art.InkEnemyModels.Entry>();
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
                        moves = made.moves,
                    });
            }
            if (entries.Count == 0) return;
            var models = new GameObject("Ink Enemy Models").AddComponent<AThousandDawns.Art.InkEnemyModels>();
            models.SetEntries(entries.ToArray());
        }

        static (GameObject prefab, RuntimeAnimatorController controller, Move[] moves) SetupEnemy(string name, float moveSpeed)
        {
            string folder = $"{CharactersFolder}/{name}";
            string modelPath = $"{folder}/{name}_Rigged.fbx";
            string controllerPath = $"{folder}/{name}.controller";
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return (null, null, null);
            // 녹림왕은 Generic: 동작을 모두 Blender에서 만들어서, Humanoid 관절 한계에 잘려 칼이 틀어지지 않고 만든 그대로 움직여요
            bool generic = GenericRigs.Contains(name);
            var rigType = generic ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Human;
            if (importer.animationType != rigType || !importer.bakeAxisConversion || !importer.importAnimation)
            {
                importer.animationType = rigType;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                importer.importAnimation = true;   // 고유 동작(나팔수 BlowHorn 등)이 들어 있으면 써요
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            // 고유 동작(BlowHorn·ShootBow)도 플레이어 동작처럼 몸통 방향·위치를 구워 넣어요.
            // 기본값(몸 방향 기준 루트 회전)으로 두면 모델을 돌려 세운 방향과 어긋나 뒤돌아서 쏴요
            var ownClips = importer.defaultClipAnimations;
            if (ownClips.Length > 0 && (importer.clipAnimations.Length != ownClips.Length || importer.clipAnimations.Any(c => !c.lockRootRotation || !c.keepOriginalOrientation || c.loopTime != LoopingOwnClips.Contains(c.name))))
            {
                foreach (var clip in ownClips)
                {
                    clip.name = clip.takeName.Split('|').Last();
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
                    clip.loopTime = LoopingOwnClips.Contains(clip.name);   // 녹림왕 돌진 달리기는 반복
                }
                importer.clipAnimations = ownClips;
                importer.SaveAndReimport();
            }
            if (!generic && AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().All(a => !a.isHuman))
                Debug.LogWarning($"{modelPath}의 Humanoid 아바타가 만들어지지 않았어요");

            // 동작: 플레이어 동작을 그대로 옮겨 입혀요 (둘 다 Humanoid).
            // 플레이어 설정(SetupPlayerRig)은 Player.controller를 지우고 다시 만들어서, 이미 세운 플레이어 애니메이터 연결이 끊겨요.
            // 그래서 플레이어 모델이 아직 Humanoid로 안 가져와졌을 때만 불러요
            if (AssetImporter.GetAtPath(PlayerRiggedModel) is ModelImporter playerImporter
                && playerImporter.animationType != ModelImporterAnimationType.Human)
                SetupPlayerRig();
            var clips = AssetDatabase.LoadAllAssetsAtPath(PlayerRiggedModel).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            if (!clips.TryGetValue("Idle", out var idle) || !clips.TryGetValue("Run", out var run)) return (null, null, null);
            var attackClip = clips.TryGetValue("IKAttack1", out var ik) ? ik : clips.TryGetValue("Attack1", out var a1) ? a1 : null;
            // 모델 고유 동작 (나팔수 BlowHorn·궁수 ShootBow·철권 Punch2·Quake). 같은 이름이면 플레이어 동작보다 먼저 써요
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                clips[c.name.Split('|').Last()] = c;
            // 녹림왕은 고유 대기(대도를 늘어뜨린 자세, 휘두르기 끝 자세와 같아서 칼이 돌지 않아요)·달리기를 써요
            idle = clips["Idle"];
            run = clips["Run"];
            // 플레이어 동작은 Generic에 못 써요: 기본 공격은 고유 Attack(동물 덤비기) 또는 Slam(녹림왕 부하 부르기 등)
            if (generic) attackClip = new[] { "Attack", "Slam", "Chop", "Punch2" }.Select(n => clips.TryGetValue(n, out var c) ? c : null).FirstOrDefault(c => c != null);
            // 기본 공격에 고유 동작이 있으면 그걸로 (나팔수·궁수 — 예고 동안 그대로 재생해요)
            if (clips.TryGetValue("BlowHorn", out var blow)) attackClip = blow;
            else if (clips.TryGetValue("ShootBow", out var shoot)) attackClip = shoot;

            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = controller.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sRun = sm.AddState("Run"); sRun.motion = run; sRun.speed = generic ? moveSpeed / 3f : 1.2f * moveSpeed / 6f;   // 플레이어 달리기(6칸/초, 1.2배)에 맞춰 (녹림왕 고유 달리기는 3칸/초 기준)
            sm.defaultState = sIdle;
            var t = sIdle.AddTransition(sRun); t.hasExitTime = false; t.duration = 0.1f; t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t = sRun.AddTransition(sIdle); t.hasExitTime = false; t.duration = 0.12f; t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // 공격 상태로는 InkEnemyAnimator가 직접 넘겨요 (트리거로 넘기면 여러 번 걸려 거듭 휘둘러요).
            // 예고 동안 첫 자세(치켜듦)에서 멈췄다가 판정 순간 제 속도로 한 번 내려찍어요 (InkEnemyAnimator가 AttackSpeed를 0/1로 바꿔요)
            void AddMove(string state, AnimationClip[] chain, MoveMode mode, float seconds, float speed)
            {
                AnimatorState prev = null;
                for (int i = 0; i < chain.Length; i++)
                {
                    var s = sm.AddState(i == 0 ? state : $"{state}_{i + 1}");
                    s.motion = chain[i];
                    s.speedParameterActive = true; s.speedParameter = "AttackSpeed";
                    s.speed = seconds > 0f ? chain[i].length / seconds : speed > 0f ? speed : 1f;
                    if (prev != null) { var n = prev.AddTransition(s); n.hasExitTime = true; n.exitTime = 1f; n.duration = 0.05f; }
                    prev = s;
                }
                if (mode == MoveMode.Loop || mode == MoveMode.Whirl) { var again = prev.AddTransition(prev); again.hasExitTime = true; again.exitTime = 1f; again.duration = 0f; }
                else if (mode != MoveMode.Run) { var back = prev.AddTransition(sIdle); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.3f; }   // 천천히 대기로 (칼이 휙 돌지 않게)
            }
            if (attackClip != null) AddMove("Attack", new[] { attackClip }, MoveMode.Swing, 0f, 0f);
            var moves = new List<Move>();
            if (InkEnemyMoves.TryGetValue(name, out var specs))
                foreach (var spec in specs)
                {
                    var chain = spec.clips.Select(c => clips.TryGetValue(c, out var clip) ? clip : null).ToArray();
                    if (chain.Any(c => c == null))
                    {
                        Debug.LogWarning($"{name}: {spec.state} 동작에 필요한 클립({string.Join(",", spec.clips)})이 없어요");
                        continue;
                    }
                    AddMove(spec.state, chain, spec.mode, spec.seconds, spec.speed);
                    if (spec.attack != null) moves.Add(new Move { attack = spec.attack, state = spec.state, mode = spec.mode, after = spec.after, ready = spec.ready });
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
            return (prefab, controller, moves.ToArray());
        }
    }
}
