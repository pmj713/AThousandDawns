using System.IO;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Data;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 시험판 전투 씬을 코드로 다시 만들어요. 손으로 고친 씬이 있으면 덮어쓰니 주의.
    public static partial class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Prototype_Combat.unity";
        const string MaterialFolder = "Assets/_Game/Prototype/Materials";

        // 죽림 1층 팔레트 (색감 시트)
        const string FloorColor = "#CFE0C8";
        const string WallColor = "#2F5A44";
        const string BambooColor = "#3E9C74";
        const string PlayerColor = "#C8463C";
        const string DummyColor = "#B8913A";
        const string FlashColor = "#FFF6D8";
        const string UltimateSwordColor = "#DDE3E8"; // 만검귀종 검 (임시)

        // 시험판 적 (임시 색). 예고 색은 색감 시트 #FF9A2E + 흰 테두리, 바탕은 예고 색과 바닥색의 중간
        const string GruntColor = "#6B4E3D";
        const string BoarColor = "#3D2F26";
        const string ArcherColor = "#4A5560";
        const string HoundColor = "#7A6A58";
        const string ShieldColor = "#5A4A3A";
        const string HornColor = "#7B5B3A";
        const string BomberColor = "#8C3B2E";
        const string SnakeColor = "#4F8F3A";
        const string BushColor = "#5E9E5A";
        const string NokrimColor = "#8A3324";
        const string AssassinColor = "#2B2433";   // 마교 살수 (임시, 검보라)
        const string QingzhuColor = "#2E6B57";    // 청죽객 (임시, 짙은 청죽색)
        const string IronFistColor = "#5A4636";   // 철권 마웅 (임시, 무쇠 갈색)
        const string FireColor = "#E25822";       // 불타는 죽림 (임시)
        const string GateColor = "#B7322C";       // 결계 부적 (잠긴 문), 색감 시트
        const string EnemyAccentColor = "#EFE6D2";
        const string ArrowColor = "#1E1C1A";
        const string TelegraphBaseColor = "#E7BD7B";
        const string TelegraphFillColor = "#FF9A2E";
        const string TelegraphOutlineColor = "#FFFFFF";

        static readonly Vector3 CameraOffset = new Vector3(0f, 13.1f, -9.2f); // 거리 16, 55°
        static readonly Vector3 BossZoneCenter = new Vector3(0f, 0f, 16.5f); // 녹림왕 결투장 가운데

        [MenuItem("AThousandDawns/시험판 씬 만들기")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // 배치 모드: -executeMethod AThousandDawns.EditorTools.PrototypeSceneBuilder.BuildBatch
        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        // 시험판 씬 셋: 시작 화면(Title, 처음 씬) · 판 구조(Prototype_Run) · 넓은 맵 샌드박스(Prototype_Combat).
        public static void Build()
        {
            SheetImporter.ImportBalance();
            SheetImporter.ImportItems();
            // 새 씬을 열면 앞에서 불러 둔 에셋 참조가 풀려요. 씬마다 다시 불러요.
            BuildSandbox(LoadBalance(), LoadItems());
            BuildRun(LoadBalance(), LoadItems());
            BuildTitle();
            // 시작 화면이 처음 씬. 그 밖에 따로 더한 씬(수묵 시험판 등)은 뒤에 그대로 둬요.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(RunScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != TitleScenePath && s.path != RunScenePath && s.path != ScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        public const string TitleScenePath = "Assets/_Game/Scenes/Title.unity";

        // 시작 화면: 카메라와 TitleScreen 하나 (그림이 없어서 IMGUI로 다 그려요)
        static void BuildTitle()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xEF, 0xE6, 0xD2, 0xFF);
            new GameObject("TitleScreen").AddComponent<AThousandDawns.Prototype.TitleScreen>();
            EditorSceneManager.SaveScene(scene, TitleScenePath);
            Debug.Log($"[시험판 씬] {TitleScenePath} 를 만들었어요.");
        }

        static BalanceConfig LoadBalance() => AssetDatabase.LoadAssetAtPath<BalanceConfig>(SheetImporter.BalanceAssetPath);
        static AThousandDawns.Items.ItemDatabase LoadItems() => AssetDatabase.LoadAssetAtPath<AThousandDawns.Items.ItemDatabase>(SheetImporter.ItemsAssetPath);

        static void BuildSandbox(BalanceConfig balance, AThousandDawns.Items.ItemDatabase itemDb)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floorMat = GetMaterial("Floor", FloorColor, false);
            var wallMat = GetMaterial("Wall", WallColor, false);
            var bambooMat = GetMaterial("Bamboo", BambooColor, false);
            var playerMat = GetMaterial("Player", PlayerColor, false);
            var dummyMat = GetMaterial("Dummy", DummyColor, false);
            var flashMat = GetMaterial("SwingFlash", FlashColor, true);

            var gate = BuildRoom(floorMat, wallMat, bambooMat, GetMaterial("BossGate", GateColor, false));
            BuildBushes(GetMaterial("Bush", BushColor, false));

            var config = new GameObject("GameConfig").AddComponent<GameConfig>();
            SetRef(config, "balance", balance);
            SetRef(config, "items", itemDb);

            var swordMat = GetMaterial("UltimateSword", UltimateSwordColor, false);
            var player = BuildPlayer(playerMat, wallMat, flashMat, swordMat, new Vector3(0f, 0f, -3f));
            var dummyA = BuildDummy("목인장 (강인함 0)", new Vector3(-2.5f, 0f, 2f), 0f, 0f, false, dummyMat);
            var dummyB = BuildDummy("목인장 (녹림왕: 강인함 5, 방어 10%)", new Vector3(2.5f, 0f, 2f), 5f, 0.1f, true, dummyMat);

            BuildCamera(player.transform);

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var director = BuildDirector(player);
            var arena = new GameObject("PrototypeArena").AddComponent<PrototypeArena>();
            SetRef(arena, "director", director);
            SetRef(arena, "bossGate", gate);
            var arenaSo = new SerializedObject(arena);
            arenaSo.FindProperty("bossZoneCenter").vector3Value = BossZoneCenter;
            arenaSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = new GameObject("PrototypeHud").AddComponent<PrototypeHud>();
            SetRef(hud, "player", player);
            SetRef(hud, "director", director);
            SetRef(hud, "arena", arena);
            var so = new SerializedObject(hud);
            var arr = so.FindProperty("dummies");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = dummyA;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = dummyB;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[시험판 씬] {ScenePath} 를 만들었어요.");
        }

        // 청죽사가 숨는 풀숲 8곳 (대나무·목인장·플레이어 시작 자리·테스트 경로와 떨어진 곳). 충돌체 없음.
        static void BuildBushes(Material mat, Vector3[] spots = null)
        {
            var parent = new GameObject("Bushes").transform;
            spots ??= new[]
            {
                new Vector3(-6.5f, 0f, -2.5f), new Vector3(6.5f, 0f, 2f), new Vector3(-3f, 0f, 4.8f), new Vector3(3.5f, 0f, -4.5f),
                new Vector3(-15f, 0f, 5f), new Vector3(15f, 0f, -9f), new Vector3(-5f, 0f, -11f), new Vector3(13f, 0f, 8f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var bush = new GameObject($"Bush_{i + 1}");
                bush.transform.SetParent(parent, false);
                bush.transform.localPosition = spots[i];

                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Visual";
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(bush.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                visual.transform.localScale = new Vector3(1.5f, 0.7f, 1.5f);
                visual.GetComponent<Renderer>().sharedMaterial = mat;

                SetRef(bush.AddComponent<AmbushBush>(), "visual", visual.transform);
            }
        }

        // 넓은 죽림 (x −20~20, z −14~10) + 북쪽 녹림왕 결투장 (16×12칸, x −8~8, z 10.5~22.5, 몬스터 시트 결투장 크기).
        // 결투장 입구(가운데 3칸)는 녹림왕이 깨어나면 결계로 막혀요. 결계 오브젝트를 돌려줘요.
        static GameObject BuildRoom(Material floorMat, Material wallMat, Material bambooMat, Material gateMat)
        {
            var room = new GameObject("Room").transform;
            Box("Floor_Field", room, new Vector3(0f, -0.1f, -2f), new Vector3(41f, 0.2f, 25f), floorMat);
            Box("Floor_Arena", room, new Vector3(0f, -0.1f, 16.5f), new Vector3(17f, 0.2f, 13f), floorMat);

            Box("Wall_S", room, new Vector3(0f, 0.75f, -14.25f), new Vector3(41f, 1.5f, 0.5f), wallMat);
            Box("Wall_W", room, new Vector3(-20.25f, 0.75f, -2f), new Vector3(0.5f, 1.5f, 25f), wallMat);
            Box("Wall_E", room, new Vector3(20.25f, 0.75f, -2f), new Vector3(0.5f, 1.5f, 25f), wallMat);
            Box("Wall_N_West", room, new Vector3(-11f, 0.75f, 10.25f), new Vector3(19f, 1.5f, 0.5f), wallMat);
            Box("Wall_N_East", room, new Vector3(11f, 0.75f, 10.25f), new Vector3(19f, 1.5f, 0.5f), wallMat);

            // 결투장 테두리는 대나무 숲 (몬스터 시트)
            Box("Arena_W", room, new Vector3(-8.25f, 1.25f, 16.5f), new Vector3(0.5f, 2.5f, 12.5f), bambooMat);
            Box("Arena_E", room, new Vector3(8.25f, 1.25f, 16.5f), new Vector3(0.5f, 2.5f, 12.5f), bambooMat);
            Box("Arena_N", room, new Vector3(0f, 1.25f, 22.75f), new Vector3(17f, 2.5f, 0.5f), bambooMat);

            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "BossGate (결계)";
            gate.transform.SetParent(room, false);
            gate.transform.localPosition = new Vector3(0f, 0.75f, 10.25f);
            gate.transform.localScale = new Vector3(3f, 1.5f, 0.5f);
            gate.GetComponent<Renderer>().sharedMaterial = gateMat;
            gate.SetActive(false);

            // 가운데(시작 자리·테스트 경로 x −2~2)는 비워 둬요
            Vector3[] bamboo =
            {
                new Vector3(-5f, 0f, 3.5f), new Vector3(5.5f, 0f, -3f), new Vector3(-4.5f, 0f, -4f), new Vector3(5f, 0f, 4f),
                new Vector3(-10f, 0f, -12f), new Vector3(-16f, 0f, -4f), new Vector3(-16f, 0f, 8f), new Vector3(-11f, 0f, 6f),
                new Vector3(11f, 0f, -11f), new Vector3(16f, 0f, -2f), new Vector3(16f, 0f, 8f), new Vector3(10f, 0f, 2f),
                new Vector3(-9f, 0f, -6f), new Vector3(9f, 0f, -3f),
            };
            for (int i = 0; i < bamboo.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                b.name = $"Bamboo_{i + 1}";
                b.transform.SetParent(room, false);
                b.transform.localPosition = bamboo[i] + Vector3.up * 1.5f;
                b.transform.localScale = new Vector3(0.6f, 1.5f, 0.6f);
                b.GetComponent<Renderer>().sharedMaterial = bambooMat;
            }
            return gate;
        }

        static PlayerController BuildPlayer(Material bodyMat, Material noseMat, Material flashMat, Material swordMat, Vector3 start)
        {
            var root = new GameObject("Player");
            root.transform.position = start;
            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.6f;
            cc.center = new Vector3(0f, 0.8f, 0f);
            cc.skinWidth = 0.02f;
            cc.minMoveDistance = 0f; // 기본값 0.001이면 프레임이 아주 높을 때 한 프레임 이동이 버려져요

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(visual, false);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(visual, false);
            nose.transform.localPosition = new Vector3(0f, 1.1f, 0.35f);
            nose.transform.localScale = new Vector3(0.15f, 0.15f, 0.3f);
            nose.GetComponent<Renderer>().sharedMaterial = noseMat;

            var flashGo = new GameObject("SwingFlash", typeof(MeshFilter), typeof(MeshRenderer));
            flashGo.GetComponent<MeshRenderer>().sharedMaterial = flashMat;
            flashGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var flash = flashGo.AddComponent<SwingFlash>();

            root.AddComponent<PlayerInputReader>();
            var controller = root.AddComponent<PlayerController>();
            SetRef(controller, "visual", visual);
            SetRef(controller, "swingFlash", flash);
            SetRef(controller, "bodyRenderer", body.GetComponent<Renderer>());
            var rain = root.AddComponent<SwordRain>();
            SetRef(rain, "swordMaterial", swordMat);
            SetRef(controller, "swordRain", rain);
            return controller;
        }

        static EnemyDirector BuildDirector(PlayerController player)
        {
            var director = new GameObject("EnemyDirector").AddComponent<EnemyDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("player").objectReferenceValue = player;
            var v = so.FindProperty("visuals");
            v.FindPropertyRelative("grunt").objectReferenceValue = GetMaterial("Enemy_Grunt", GruntColor, false);
            v.FindPropertyRelative("boar").objectReferenceValue = GetMaterial("Enemy_Boar", BoarColor, false);
            v.FindPropertyRelative("archer").objectReferenceValue = GetMaterial("Enemy_Archer", ArcherColor, false);
            v.FindPropertyRelative("hound").objectReferenceValue = GetMaterial("Enemy_Hound", HoundColor, false);
            v.FindPropertyRelative("shield").objectReferenceValue = GetMaterial("Enemy_Shield", ShieldColor, false);
            v.FindPropertyRelative("horn").objectReferenceValue = GetMaterial("Enemy_Horn", HornColor, false);
            v.FindPropertyRelative("bomber").objectReferenceValue = GetMaterial("Enemy_Bomber", BomberColor, false);
            v.FindPropertyRelative("snake").objectReferenceValue = GetMaterial("Enemy_Snake", SnakeColor, false);
            v.FindPropertyRelative("nokrim").objectReferenceValue = GetMaterial("Enemy_Nokrim", NokrimColor, false);
            v.FindPropertyRelative("assassin").objectReferenceValue = GetMaterial("Enemy_Assassin", AssassinColor, false);
            v.FindPropertyRelative("qingzhu").objectReferenceValue = GetMaterial("Enemy_Qingzhu", QingzhuColor, false);
            v.FindPropertyRelative("ironfist").objectReferenceValue = GetMaterial("Enemy_IronFist", IronFistColor, false);
            v.FindPropertyRelative("fire").objectReferenceValue = GetMaterial("Fire", FireColor, true);
            v.FindPropertyRelative("accent").objectReferenceValue = GetMaterial("Enemy_Accent", EnemyAccentColor, false);
            v.FindPropertyRelative("arrow").objectReferenceValue = GetMaterial("Arrow", ArrowColor, false);
            v.FindPropertyRelative("telegraphBase").objectReferenceValue = GetMaterial("Telegraph_Base", TelegraphBaseColor, true);
            v.FindPropertyRelative("telegraphFill").objectReferenceValue = GetMaterial("Telegraph_Fill", TelegraphFillColor, true);
            v.FindPropertyRelative("telegraphOutline").objectReferenceValue = GetMaterial("Telegraph_Outline", TelegraphOutlineColor, true);
            so.ApplyModifiedPropertiesWithoutUndo();
            return director;
        }

        static TrainingDummy BuildDummy(string label, Vector3 position, float toughness, float defense, bool isBoss, Material mat)
        {
            var root = new GameObject(label);
            root.transform.position = position;
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.radius = 0.4f;
            col.height = 2f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;

            var damageable = root.AddComponent<Damageable>();
            var so = new SerializedObject(damageable);
            so.FindProperty("toughness").floatValue = toughness;
            so.FindProperty("defense").floatValue = defense;
            so.FindProperty("invincible").boolValue = true;
            so.FindProperty("bodyRadius").floatValue = 0.4f;
            so.FindProperty("flashRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 상태이상 (녹림왕 목인장은 보스로: 둔화 효과 50%, 빙결 1초 + 면역 10초)
            var status = root.AddComponent<StatusEffects>();
            var sso = new SerializedObject(status);
            sso.FindProperty("isBoss").boolValue = isBoss;
            sso.ApplyModifiedPropertiesWithoutUndo();

            var dummy = root.AddComponent<TrainingDummy>();
            var dso = new SerializedObject(dummy);
            dso.FindProperty("label").stringValue = label;
            dso.ApplyModifiedPropertiesWithoutUndo();
            return dummy;
        }

        static void BuildCamera(Transform target)
        {
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<CinemachineBrain>() == null) cam.gameObject.AddComponent<CinemachineBrain>();

            var go = new GameObject("CM QuarterView");
            go.transform.SetPositionAndRotation(target.position + CameraOffset, Quaternion.Euler(55f, 0f, 0f));
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = target;
            var lens = vcam.Lens;
            lens.FieldOfView = 40f;
            vcam.Lens = lens;

            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = CameraOffset;
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(0.3f, 0.3f, 0.3f);
            follow.TrackerSettings = tracker;
        }

        static void Box(string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Material GetMaterial(string name, string hex, bool unlit)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null) mat.shader = shader;
            ColorUtility.TryParseHtmlString(hex, out Color color);
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
