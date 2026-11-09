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

        const float Env2DFloorWidth = 20f;
        const float Env2DFloorDepth = 14f;
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
            string[] sprites = { "Bamboo_A", "Bamboo_B", "Bamboo_C", "Rock_A", "Palisade_A", "LanternPost_A" };
            foreach (var name in sprites) ImportAsSprite($"{Env2DArtFolder}/{name}.png");
            ImportAsSprite($"{Env2DArtFolder}/Source/Backdrop_Ink.png");
            ImportAsTiledTexture($"{Env2DArtFolder}/Floor_Ground.png");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null) light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cam = Camera.main;
            cam.transform.SetPositionAndRotation(CameraOffset, Quaternion.Euler(CameraPitch, 0f, 0f));
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString("#E9E4D8", out Color paper);
            cam.backgroundColor = paper;

            BuildEnv2DFloor();

            var props = new GameObject("Props (2D)").transform;
            // 뒤쪽 먼 풍경: 바닥 끝 너머에 세운 큰 그림
            Billboard(props, "Backdrop_Ink", $"{Env2DArtFolder}/Source/Backdrop_Ink.png", new Vector3(0f, 0f, Env2DFloorDepth * 0.5f + 0.5f), 11f);

            // 왼쪽과 뒤쪽 가장자리를 따라 대나무 숲
            Vector3[] bambooSpots =
            {
                new Vector3(-9f, 0f, 6f), new Vector3(-7f, 0f, 6.3f), new Vector3(-5.2f, 0f, 5.8f),
                new Vector3(-9.2f, 0f, 3.5f), new Vector3(-8.6f, 0f, 1f), new Vector3(-9.3f, 0f, -1.5f),
                new Vector3(-8.4f, 0f, -4f), new Vector3(8.8f, 0f, 5.6f), new Vector3(9.2f, 0f, 2.8f),
                new Vector3(-3.5f, 0f, 2.5f),
            };
            string[] bamboos = { "Bamboo_A", "Bamboo_B", "Bamboo_C" };
            for (int i = 0; i < bambooSpots.Length; i++)
            {
                string name = bamboos[i % bamboos.Length];
                float height = 5.5f + (i * 37 % 10) * 0.12f;
                Billboard(props, name, $"{Env2DArtFolder}/{name}.png", bambooSpots[i], height, i % 2 == 1);
            }

            // 뒤쪽 산채 입구: 목책 둘과 등불 둘
            Billboard(props, "Palisade_A", $"{Env2DArtFolder}/Palisade_A.png", new Vector3(-1.8f, 0f, 6.2f), 2.2f);
            Billboard(props, "Palisade_A", $"{Env2DArtFolder}/Palisade_A.png", new Vector3(2.6f, 0f, 6.2f), 2.2f, true);
            Billboard(props, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png", new Vector3(0.4f, 0f, 5.9f), 3.4f);
            Billboard(props, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png", new Vector3(5.2f, 0f, 1f), 3.4f, true);

            // 바위 몇 개
            Billboard(props, "Rock_A", $"{Env2DArtFolder}/Rock_A.png", new Vector3(4.5f, 0f, 3.5f), 1.8f);
            Billboard(props, "Rock_A", $"{Env2DArtFolder}/Rock_A.png", new Vector3(-5.5f, 0f, -2.5f), 1.3f, true);
            Billboard(props, "Rock_A", $"{Env2DArtFolder}/Rock_A.png", new Vector3(6.8f, 0f, -4.2f), 1.1f);

            // 크기 비교용 플레이어 자리 (높이 2)
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Scale Reference (Player)";
            player.transform.position = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(player.GetComponent<Collider>());
            player.GetComponent<Renderer>().sharedMaterial = GetMaterial("Player", PlayerColor, false);

            EnsureFolder("Assets/_Game/Scenes");
            EditorSceneManager.SaveScene(scene, Env2DScenePath);
            AssetDatabase.SaveAssets();
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

        // 발밑을 바닥에 붙이고 카메라와 같은 각도로 기울여 세워요. 그림에 이미 위에서 본 각도가 들어 있어요.
        static void Billboard(Transform parent, string name, string spritePath, Vector3 foot, float height, bool flip = false)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(foot, Quaternion.Euler(CameraPitch, 0f, 0f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flip;
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
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
