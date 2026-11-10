using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 2D 배경 시험 씬: 바닥은 3D 판에 2D 재질, 대나무·바위·목책·등불은 카메라를 향해 세운 2D 스프라이트.
    // 그림은 VARCO에서 뽑아 초록 배경을 뺀 것 (Art/Env/BurningBamboo).
    public static partial class PrototypeSceneBuilder
    {
        public const string Env2DScenePath = "Assets/_Game/Scenes/Test_Env2D.unity";
        const string Env2DArtFolder = "Assets/_Game/Art/Env/BurningBamboo";
        const string Env2DMaterialFolder = "Assets/_Game/Art/Env/BurningBamboo/Materials";

        // 바닥은 방(CombatSize 20×12)보다 넓게 깔아 벽 너머 숲 밑동까지 덮어요
        const float Env2DFloorWidth = 32f;
        const float Env2DFloorDepth = 24f;
        const float Env2DFloorTileSize = 4f; // 바닥 그림 한 장이 덮는 칸 수
        const float CameraPitch = 55f;

        [MenuItem("AThousandDawns/2D 배경 시험 씬 만들기")]
        public static void BuildEnv2DFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEnv2D();
        }

        // 배치 모드: -executeMethod AThousandDawns.EditorTools.PrototypeSceneBuilder.BuildEnv2DBatch
        public static void BuildEnv2DBatch()
        {
            BuildEnv2D();
            EditorApplication.Exit(0);
        }

        public static void BuildEnv2D()
        {
            string[] sprites = { "Bamboo_A", "Bamboo_B", "Bamboo_C", "Rock_A", "Palisade_A", "LanternPost_A", "Bush_A", "Bush_B" };
            foreach (var name in sprites) ImportAsSprite($"{Env2DArtFolder}/{name}.png");
            ImportAsSprite($"{Env2DArtFolder}/Source/Backdrop_Ink.png");
            ImportAsTiledTexture($"{Env2DArtFolder}/Floor_Ground.png");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetupEnvCameraAndLight();

            BuildEnv2DFloor();
            BuildEnv2DRoom();

            BuildInkCharacters();

            EnsureFolder("Assets/_Game/Scenes");
            EditorSceneManager.SaveScene(scene, Env2DScenePath);
            AssetDatabase.SaveAssets();
        }

        // 시험판과 같은 고정 쿼터뷰 (방 가운데를 봐요), 한지색 배경
        static void SetupEnvCameraAndLight()
        {
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cam = Camera.main;
            cam.transform.SetPositionAndRotation(CameraOffset, Quaternion.Euler(CameraPitch, 0f, 0f));
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            ApplyPaperAtmosphere(cam);
        }

        const string InkPaperMaterialPath = "Assets/_Game/Art/PostFX/InkPaperScreen.mat";

        // 한지색 배경 + 원근 안개 + 화면 전체 한지 질감 후처리 (카메라 위치는 그대로)
        static void ApplyPaperAtmosphere(Camera cam)
        {
            ColorUtility.TryParseHtmlString("#E9E4D8", out Color paper);
            cam.backgroundColor = paper;

            // 화면 전체 수묵 질감: 이 카메라에만 붙는 후처리 (재질 값은 인스펙터에서 조절해도 씬을 다시 만들 때 남아요)
            var paperMat = AssetDatabase.LoadAssetAtPath<Material>(InkPaperMaterialPath);
            if (paperMat == null)
            {
                EnsureFolder("Assets/_Game/Art/PostFX");
                paperMat = new Material(Shader.Find("AThousandDawns/InkPaperScreen"));
                AssetDatabase.CreateAsset(paperMat, InkPaperMaterialPath);
            }
            var fx = cam.GetComponent<AThousandDawns.Art.InkPaperPostFX>();
            if (fx == null) fx = cam.gameObject.AddComponent<AThousandDawns.Art.InkPaperPostFX>();
            SetRef(fx, "material", paperMat);

            // 수묵화처럼 먼 것일수록 한지색 안개로 옅어져요. 방 가운데까지(카메라에서 약 16)는 거의 그대로, 벽 너머 숲부터 옅어져요
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = paper;
            RenderSettings.fogStartDistance = 17f;
            RenderSettings.fogEndDistance = 40f;
        }

        // Prototype_Run의 층 방(CombatSize 20×12, 네 변 가운데 폭 3 문)과 같은 자리에 2D 그림을 둬요.
        // 상자 벽 자리는 숲·목책으로, 대나무 기둥 6개는 대나무 그림으로. 문 앞 십자 길은 비워 둬요.
        static void BuildEnv2DRoom()
        {
            float hx = CombatSize.x * 0.5f, hz = CombatSize.y * 0.5f;
            const float doorHalf = 1.75f; // 문 폭 3 + 복도 벽 두께

            var room = new GameObject("Room_Floor (2D)").transform;
            var north = new GameObject("North (palisade + forest)").transform;
            var west = new GameObject("West (forest)").transform;
            var east = new GameObject("East (forest)").transform;
            var south = new GameObject("South (rocks)").transform;
            var pillars = new GameObject("Bamboo Pillars").transform;
            var bushes = new GameObject("Bushes (청죽사)").transform;
            foreach (var t in new[] { north, west, east, south, pillars, bushes }) t.SetParent(room, false);

            // 북쪽 너머 먼 풍경
            Billboard(room, "Backdrop_Ink", $"{Env2DArtFolder}/Source/Backdrop_Ink.png", new Vector3(0f, 0f, hz + 5f), 22f);

            // 북쪽 벽: 목책 세 칸씩, 문 양옆 등불, 그 뒤로 대나무 두 줄
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                    Billboard(north, "Palisade_A", $"{Env2DArtFolder}/Palisade_A.png",
                        new Vector3(side * (doorHalf + 1f + i * 1.9f), 0f, hz + 0.3f), 2.6f, (i + side) % 2 == 0);
                Billboard(north, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png",
                    new Vector3(side * (doorHalf + 0.2f), 0f, hz + 0.15f), 3.6f, side > 0);
            }
            BambooRow(north, -hx - 2f, hx + 2f, hz + 1.3f, 1.4f, true, doorHalf, 0);
            BambooRow(north, -hx - 1.3f, hx + 1.3f, hz + 2.4f, 1.6f, true, 0f, 3);

            // 동·서 벽: 대나무 두 줄, 문 자리(z = 0)는 비워 둬요
            BambooRow(west, -hz, hz + 0.5f, -hx - 0.6f, 1.3f, false, doorHalf, 5);
            BambooRow(west, -hz - 1f, hz + 1f, -hx - 1.7f, 1.5f, false, doorHalf, 8);
            BambooRow(east, -hz, hz + 0.5f, hx + 0.6f, 1.3f, false, doorHalf, 11);
            BambooRow(east, -hz - 1f, hz + 1f, hx + 1.7f, 1.5f, false, doorHalf, 14);

            // 남쪽 벽: 카메라 쪽이라 키 큰 그림은 방을 가려요. 낮은 바위만 둬요
            float[] rockX = { -8.6f, -6.2f, -3.4f, 3.2f, 5.8f, 8.4f };
            for (int i = 0; i < rockX.Length; i++)
                Billboard(south, "Rock_A", $"{Env2DArtFolder}/Rock_A.png",
                    new Vector3(rockX[i], 0f, -hz - 0.4f), 1.2f + (i * 7 % 4) * 0.15f, i % 2 == 1);

            // 대나무 기둥 6개 (Prototype_Run과 같은 자리)
            Vector3[] pillarSpots =
            {
                new Vector3(-5.5f, 0f, 3f), new Vector3(5.5f, 0f, -3f), new Vector3(-7f, 0f, -3.5f),
                new Vector3(7f, 0f, 3.5f), new Vector3(3.5f, 0f, 3f), new Vector3(-3.5f, 0f, -3f),
            };
            for (int i = 0; i < pillarSpots.Length; i++)
            {
                string name = Env2DBamboos[i % Env2DBamboos.Length];
                Billboard(pillars, name, $"{Env2DArtFolder}/{name}.png", pillarSpots[i], 7f, i % 2 == 0);
            }

            // 청죽사 풀숲 4곳 (Prototype_Run과 같은 자리). 시험판 풀숲(지름 1.5)보다 조금 넓게 그려요
            Vector3[] bushSpots =
            {
                new Vector3(-7.5f, 0f, 4.3f), new Vector3(7.5f, 0f, -4.3f), new Vector3(-3f, 0f, 4.3f), new Vector3(3f, 0f, -4.3f),
            };
            for (int i = 0; i < bushSpots.Length; i++)
            {
                string name = i % 2 == 0 ? "Bush_A" : "Bush_B";
                Billboard(bushes, name, $"{Env2DArtFolder}/{name}.png", bushSpots[i], 1.6f, i >= 2);
            }

            // 방 안쪽 바위 장식 두 개 (길·기둥과 겹치지 않는 자리)
            Billboard(room, "Rock_A", $"{Env2DArtFolder}/Rock_A.png", new Vector3(-8.3f, 0f, 1.6f), 1.6f);
            Billboard(room, "Rock_A", $"{Env2DArtFolder}/Rock_A.png", new Vector3(8.4f, 0f, -1.9f), 1.4f, true);
        }

        const string InkShaderName = "AThousandDawns/InkCharacter";
        const string InkMaterialFolder = "Assets/_Game/Art/Characters/Materials";

        // 수묵 셰이더 시험: 플레이어(붉은 포인트)·졸개·멧돼지 자리를 수묵으로, 옆에 원래 Lit 캡슐을 비교용으로 둬요.
        static void BuildInkCharacters()
        {
            var group = new GameObject("Characters (ink test)").transform;

            // 플레이어는 WASD로 걸려 볼 수 있어요 (카메라가 따라와요).
            // 맨몸 기본 바디 파츠 + 머리카락 모델에 수묵 셰이더를 입혀 세워요. 모델이 없으면 예전 캡슐로 세워요
            GameObject player;
            var playerAnimator = SetupPlayerRig();   // 한 번만 만들어 두 모델이 같이 써요
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerBodyModel) != null)
            {
                player = new GameObject("Player (Ink, WASD)");
                player.transform.SetParent(group, false);
                player.transform.localPosition = new Vector3(0f, 0f, -3f);
                PlayerModel(player.transform, GetInkMaterial("Ink_PlayerSkin", PlayerSkinColor, 0.35f),
                    GetInkMaterial("Ink_PlayerHair", PlayerHairColor, 0.1f), playerAnimator);
                var controller = player.AddComponent<CharacterController>();
                controller.height = 1.9f;
                controller.radius = 0.35f;
                controller.center = new Vector3(0f, 0.95f, 0f);
            }
            else
            {
                var playerMat = GetInkMaterial("Ink_Player", PlayerColor, 0.75f);
                player = InkPrimitive(group, "Player (Ink, WASD)", PrimitiveType.Capsule, new Vector3(0f, 1f, -3f), Vector3.one, playerMat);
                InkPrimitive(player.transform, "Facing", PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0.45f), Vector3.one * 0.3f, playerMat);
                player.AddComponent<CharacterController>();
            }
            player.AddComponent<AThousandDawns.Art.ArtTestWalker>();
            InkPrimitive(group, "Grunt (Ink)", PrimitiveType.Capsule, new Vector3(-2.2f, 1f, -1.2f), Vector3.one,
                GetInkMaterial("Ink_Grunt", GruntColor, 0.1f));
            InkPrimitive(group, "Boar (Ink)", PrimitiveType.Sphere, new Vector3(2.4f, 0.55f, -1.2f), new Vector3(1.3f, 1.1f, 1.9f),
                GetInkMaterial("Ink_Boar", BoarColor, 0.1f));

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerBodyModel) != null)
            {
                var plain = new GameObject("Player (Plain Lit, 비교용)");
                plain.transform.SetParent(group, false);
                plain.transform.localPosition = new Vector3(1.4f, 0f, -3.6f);
                PlayerModel(plain.transform, GetMaterial("PlayerSkin", PlayerSkinColor, false), GetMaterial("PlayerHair", PlayerHairColor, false), playerAnimator);
            }
            else
            {
                InkPrimitive(group, "Player (Plain Lit, 비교용)", PrimitiveType.Capsule, new Vector3(1.4f, 1f, -3.6f), Vector3.one,
                    GetMaterial("Player", PlayerColor, false));
            }
        }

        const string PlayerPartsFolder = "Assets/_Game/Art/Characters/Player/Parts";
        const string PlayerBodyModel = PlayerPartsFolder + "/Player_BaseBody_All.fbx";
        const string PlayerHairModel = PlayerPartsFolder + "/Player_Hair.fbx";
        const string PlayerSkinColor = "#D8B89A";
        const string PlayerHairColor = "#1E1C1C";
        const string PlayerSwordColor = "#2E343B";
        // 옷 (컨셉 B안): 먹색 도포·바지, 흰 속소매·깃, 붉은 허리띠·머리끈. Blender clothes.py가 이름 앞에 Cloth_Dark/Light/Red를 붙여요
        const string ClothDarkColor = "#26262A";
        const string ClothLightColor = "#D9D5CB";
        const string ClothRedColor = "#A3241E";

        // 플레이어 모델을 발밑 기준으로 parent 아래에 세워요. 파츠 기준점이 모두 발밑이라 그대로 맞물려요.
        // 뼈대·동작이 든 Player_Rigged.fbx가 있으면 그걸(애니메이터 연결), 없으면 맨몸 파츠 + 머리카락을 따로 세워요
        static void PlayerModel(Transform parent, Material skin, Material hair, RuntimeAnimatorController controller)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(parent, false);
            // 오른손 칼(Player_Sword): 수묵이면 먹색 수묵 재질, 비교용 Lit이면 같은 색 Lit
            var sword = skin != null && skin.shader != null && skin.shader.name == InkShaderName
                ? GetInkMaterial("Ink_PlayerSword", PlayerSwordColor, 0.1f)
                : GetMaterial("PlayerSword", PlayerSwordColor, false);
            bool ink = skin != null && skin.shader != null && skin.shader.name == InkShaderName;
            Material Cloth(string name, string hex, float sat) => ink ? GetInkMaterial($"Ink_Player{name}", hex, sat) : GetMaterial($"Player{name}", hex, false);
            var clothDark = Cloth("ClothDark", ClothDarkColor, 0.1f);
            var clothLight = Cloth("ClothLight", ClothLightColor, 0.1f);
            var clothRed = Cloth("ClothRed", ClothRedColor, 0.85f);
            // Blender에서 -Y를 보던 모델이 Unity에서는 -Z(뒤)를 보고 들어와서, 앞(+Z)을 보게 돌려요
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var paths = controller != null ? new[] { PlayerRiggedModel } : new[] { PlayerBodyModel, PlayerHairModel };
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, model);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mat = r.name.Contains("Cloth_Dark") ? clothDark : r.name.Contains("Cloth_Light") ? clothLight : r.name.Contains("Cloth_Red") ? clothRed
                        : r.name.Contains("Sword") ? sword : r.name.Contains("Hair") || path == PlayerHairModel ? hair : skin;
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
                if (controller != null)
                {
                    var animator = go.GetComponent<Animator>();
                    if (animator == null) animator = go.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                }
            }
        }

        static GameObject InkPrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Material GetInkMaterial(string name, string hex, float saturation)
        {
            EnsureFolder(InkMaterialFolder);
            string path = $"{InkMaterialFolder}/{name}.mat";
            var shader = Shader.Find(InkShaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                ColorUtility.TryParseHtmlString(hex, out Color color);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Saturation", saturation);
            }
            else if (shader != null) mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static readonly string[] Env2DBamboos = { "Bamboo_A", "Bamboo_B", "Bamboo_C" };

        // from~to 구간에 대나무를 한 줄로 세워요. alongX면 x를 따라 (z = line), 아니면 z를 따라 (x = line).
        // 가운데 |값| < gap 은 문 자리라 비워요. seed로 높이·좌우·흔들림을 다르게 해요.
        static void BambooRow(Transform parent, float from, float to, float line, float step, bool alongX, float gap, int seed)
        {
            int i = 0;
            for (float t = from; t <= to + 0.01f; t += step, i++)
            {
                if (Mathf.Abs(t) < gap) continue;
                int h = (seed + i) * 2654435 & 0xFFFF;
                float jitter = ((h & 0xFF) / 255f - 0.5f) * step * 0.5f;
                float lineJitter = (((h >> 8) & 0xFF) / 255f - 0.5f) * 0.5f;
                float along = t + jitter;
                if (Mathf.Abs(along) < gap) along = Mathf.Sign(along) * gap;
                var foot = alongX ? new Vector3(along, 0f, line + lineJitter) : new Vector3(line + lineJitter, 0f, along);
                string name = Env2DBamboos[(seed + i) % Env2DBamboos.Length];
                float height = 8f + (h % 9) * 0.2f;
                Billboard(parent, name, $"{Env2DArtFolder}/{name}.png", foot, height, (h & 1) == 1);
            }
        }

        static void BuildEnv2DFloor()
        {
            EnsureFolder(Env2DMaterialFolder);
            string path = $"{Env2DMaterialFolder}/Floor_Ground.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Env2DArtFolder}/Floor_Ground.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetTextureScale("_BaseMap", new Vector2(Env2DFloorWidth / Env2DFloorTileSize, Env2DFloorDepth / Env2DFloorTileSize));
            EditorUtility.SetDirty(mat);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Floor (2D texture)";
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            floor.transform.localScale = new Vector3(Env2DFloorWidth, Env2DFloorDepth, 1f);
            floor.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // 3D 캐릭터처럼 바닥에 수직으로 세워요. 그래야 원근 기울기와 앞뒤 가림이 캐릭터와 같아요.
        // 수직 판은 55° 카메라에서 세로가 cos55°(≈0.57)만큼 납작해 보여요. 그림은 이미 위에서 본 각도로
        // 그려져 있으니 가로를 cos55°만큼 줄여 화면에서 그림 비율이 그대로 보이게 해요.
        // height는 월드 키 (플레이어 캡슐 = 2). 화면에서는 같은 키의 3D 물체와 같은 크기로 보여요.
        static void Billboard(Transform parent, string name, string spritePath, Vector3 foot, float height, bool flip = false)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(foot, Quaternion.identity);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flip;
            float scale = height / sprite.bounds.size.y;
            float foreshorten = Mathf.Cos(CameraPitch * Mathf.Deg2Rad);
            go.transform.localScale = new Vector3(scale * foreshorten, scale, 1f);
        }

        static void ImportAsSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { AssetDatabase.ImportAsset(path); importer = (TextureImporter)AssetImporter.GetAtPath(path); }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        static void ImportAsTiledTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { AssetDatabase.ImportAsset(path); importer = (TextureImporter)AssetImporter.GetAtPath(path); }
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
