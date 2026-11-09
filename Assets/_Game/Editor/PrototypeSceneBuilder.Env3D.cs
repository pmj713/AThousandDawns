using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 3D 덩어리 시험 씬: 상자·원기둥으로 대충 만든 구조물에 수묵 트라이플래너 재질(InkTriplanar)을 씌워요.
    // 방 배치는 Prototype_Run 층 방과 같아요. 잎·풀처럼 가는 것(덤불)과 먼 풍경만 2D 그림이에요.
    public static partial class PrototypeSceneBuilder
    {
        public const string Env3DScenePath = "Assets/_Game/Scenes/Test_Env3D.unity";
        const string InkTriplanarShaderName = "AThousandDawns/InkTriplanar";

        [MenuItem("AThousandDawns/3D 덩어리 시험 씬 만들기")]
        public static void BuildEnv3DFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEnv3D();
        }

        // 배치 모드: -executeMethod AThousandDawns.EditorTools.PrototypeSceneBuilder.BuildEnv3DBatch
        public static void BuildEnv3DBatch()
        {
            BuildEnv3D();
            EditorApplication.Exit(0);
        }

        public static void BuildEnv3D()
        {
            foreach (var name in new[] { "Floor_Ground", "Tex_Stone", "Tex_Wood", "Tex_BambooStalk" })
                ImportAsTiledTexture($"{Env2DArtFolder}/{name}.png");
            foreach (var name in new[] { "Bush_A", "Bush_B", "LanternPost_A", "BambooLeaves_A", "BambooLeaves_B" })
                ImportAsSprite($"{Env2DArtFolder}/{name}.png");
            ImportAsSprite($"{Env2DArtFolder}/Source/Backdrop_Ink.png");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetupEnvCameraAndLight();

            var floorMat = GetInkTriplanar("Ink3D_Floor", "Floor_Ground", 4f, 0f);
            var stoneMat = GetInkTriplanar("Ink3D_Stone", "Tex_Stone", 1.5f, 2.5f);
            var woodMat = GetInkTriplanar("Ink3D_Wood", "Tex_Wood", 1f, 2f);
            var bambooMat = GetInkTriplanar("Ink3D_Bamboo", "Tex_BambooStalk", 1.2f, 2f);

            float hx = CombatSize.x * 0.5f, hz = CombatSize.y * 0.5f;
            const float doorHalf = 1.75f;
            var room = new GameObject("Room_Floor (3D blockout)").transform;

            Block(room, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(32f, 0.2f, 24f), Quaternion.identity, floorMat);
            BuildEnv3DWalls(room, hx, hz, stoneMat);
            BuildEnv3DGate(room, hz, woodMat);
            BuildEnv3DPalisade(room, hx, hz, doorHalf, woodMat);
            BuildEnv3DForest(room, hx, hz, doorHalf, bambooMat);

            // 대나무 기둥 6개 (Prototype_Run과 같은 자리): 줄기 두세 개 묶음
            var pillars = new GameObject("Bamboo Pillars (3D)").transform;
            pillars.SetParent(room, false);
            Vector3[] pillarSpots =
            {
                new Vector3(-5.5f, 0f, 3f), new Vector3(5.5f, 0f, -3f), new Vector3(-7f, 0f, -3.5f),
                new Vector3(7f, 0f, 3.5f), new Vector3(3.5f, 0f, 3f), new Vector3(-3.5f, 0f, -3f),
            };
            for (int i = 0; i < pillarSpots.Length; i++) BambooCluster(pillars, pillarSpots[i], 4f, i, bambooMat);

            // 바위: 기울인 상자 몇 개를 겹친 덩어리
            RockCluster(room, new Vector3(-8.3f, 0f, 1.6f), 1.3f, 0, stoneMat);
            RockCluster(room, new Vector3(8.4f, 0f, -1.9f), 1.1f, 1, stoneMat);

            // 가늘고 복잡한 것만 2D 그림: 덤불, 문 옆 등불, 먼 풍경
            var flat = new GameObject("2D Cards").transform;
            flat.SetParent(room, false);
            Vector3[] bushSpots =
            {
                new Vector3(-7.5f, 0f, 4.3f), new Vector3(7.5f, 0f, -4.3f), new Vector3(-3f, 0f, 4.3f), new Vector3(3f, 0f, -4.3f),
            };
            for (int i = 0; i < bushSpots.Length; i++)
                FoliageCards(flat, i % 2 == 0 ? "Bush_A" : "Bush_B", i % 2 == 0 ? "Bush_A" : "Bush_B",
                    bushSpots[i] + Vector3.up * 0.55f, 1.1f, 3, i * 40f);
            Billboard(flat, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png", new Vector3(-2.6f, 0f, hz + 0.9f), 3.6f);
            Billboard(flat, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png", new Vector3(2.6f, 0f, hz + 0.9f), 3.6f, true);
            Billboard(flat, "Backdrop_Ink", $"{Env2DArtFolder}/Source/Backdrop_Ink.png", new Vector3(0f, 0f, hz + 6f), 22f);

            BuildInkCharacters();

            EnsureFolder("Assets/_Game/Scenes");
            EditorSceneManager.SaveScene(scene, Env3DScenePath);
            AssetDatabase.SaveAssets();
        }

        // Prototype_Run의 BuildFourDoorRoom과 같은 돌벽 (높이 1.5, 네 변 가운데 폭 3 문은 열어 둠)
        static void BuildEnv3DWalls(Transform room, float hx, float hz, Material mat)
        {
            var walls = new GameObject("Walls (stone)").transform;
            walls.SetParent(room, false);
            float side = (CombatSize.x + 1f - 3f) * 0.5f;
            float sideZ = (CombatSize.y + 1f - 3f) * 0.5f;
            var q = Quaternion.identity;
            Block(walls, "Wall_N_W", PrimitiveType.Cube, new Vector3(-1.5f - side * 0.5f, 0.75f, hz + 0.25f), new Vector3(side, 1.5f, 0.5f), q, mat);
            Block(walls, "Wall_N_E", PrimitiveType.Cube, new Vector3(1.5f + side * 0.5f, 0.75f, hz + 0.25f), new Vector3(side, 1.5f, 0.5f), q, mat);
            Block(walls, "Wall_S_W", PrimitiveType.Cube, new Vector3(-1.5f - side * 0.5f, 0.75f, -hz - 0.25f), new Vector3(side, 1.5f, 0.5f), q, mat);
            Block(walls, "Wall_S_E", PrimitiveType.Cube, new Vector3(1.5f + side * 0.5f, 0.75f, -hz - 0.25f), new Vector3(side, 1.5f, 0.5f), q, mat);
            Block(walls, "Wall_E_N", PrimitiveType.Cube, new Vector3(hx + 0.25f, 0.75f, 1.5f + sideZ * 0.5f), new Vector3(0.5f, 1.5f, sideZ), q, mat);
            Block(walls, "Wall_E_S", PrimitiveType.Cube, new Vector3(hx + 0.25f, 0.75f, -1.5f - sideZ * 0.5f), new Vector3(0.5f, 1.5f, sideZ), q, mat);
            Block(walls, "Wall_W_N", PrimitiveType.Cube, new Vector3(-hx - 0.25f, 0.75f, 1.5f + sideZ * 0.5f), new Vector3(0.5f, 1.5f, sideZ), q, mat);
            Block(walls, "Wall_W_S", PrimitiveType.Cube, new Vector3(-hx - 0.25f, 0.75f, -1.5f - sideZ * 0.5f), new Vector3(0.5f, 1.5f, sideZ), q, mat);
        }

        // 북쪽 문: 나무 기둥 둘과 가로대 둘 (산채 문루 느낌)
        static void BuildEnv3DGate(Transform room, float hz, Material mat)
        {
            var gate = new GameObject("North Gate (wood)").transform;
            gate.SetParent(room, false);
            for (int s = -1; s <= 1; s += 2)
                Block(gate, "Post", PrimitiveType.Cylinder, new Vector3(s * 1.75f, 1.6f, hz + 0.25f), new Vector3(0.4f, 1.6f, 0.4f), Quaternion.identity, mat);
            Block(gate, "Beam_Top", PrimitiveType.Cube, new Vector3(0f, 3.1f, hz + 0.25f), new Vector3(4.6f, 0.3f, 0.4f), Quaternion.identity, mat);
            Block(gate, "Beam_Low", PrimitiveType.Cube, new Vector3(0f, 2.6f, hz + 0.25f), new Vector3(3.5f, 0.18f, 0.3f), Quaternion.identity, mat);
        }

        // 북쪽 돌벽 뒤 통나무 목책 (문 자리는 비움)
        static void BuildEnv3DPalisade(Transform room, float hx, float hz, float doorHalf, Material mat)
        {
            var fence = new GameObject("Palisade (wood logs)").transform;
            fence.SetParent(room, false);
            int i = 0;
            for (float x = -hx; x <= hx + 0.01f; x += 0.42f, i++)
            {
                if (Mathf.Abs(x) < doorHalf + 0.3f) continue;
                float h = 2.3f + (i * 37 % 7) * 0.08f;
                Block(fence, "Log", PrimitiveType.Cylinder, new Vector3(x, h * 0.5f, hz + 0.75f), new Vector3(0.38f, h * 0.5f, 0.38f),
                    Quaternion.Euler(0f, i * 47f, 0f), mat);
            }
        }

        // 벽 너머 대나무 숲: 줄기 묶음 (동·서·북, 문 자리 비움)
        static void BuildEnv3DForest(Transform room, float hx, float hz, float doorHalf, Material mat)
        {
            var forest = new GameObject("Forest (3D stalks)").transform;
            forest.SetParent(room, false);
            int seed = 0;
            for (float x = -hx - 2f; x <= hx + 2f; x += 1.5f, seed++)
                if (Mathf.Abs(x) > doorHalf + 0.5f) BambooCluster(forest, new Vector3(x, 0f, hz + 1.8f + (seed % 3) * 0.4f), 6f, seed, mat);
            for (float z = -hz; z <= hz + 1f; z += 1.5f, seed++)
            {
                if (Mathf.Abs(z) < doorHalf + 0.5f) continue;
                BambooCluster(forest, new Vector3(-hx - 1.3f - (seed % 3) * 0.4f, 0f, z), 6f, seed, mat);
                BambooCluster(forest, new Vector3(hx + 1.3f + (seed % 3) * 0.4f, 0f, z), 6f, seed + 7, mat);
            }
        }

        // 줄기 2~3개 묶음 + 꼭대기 잎 다발(2D 카드). height는 가장 높은 줄기 키
        static void BambooCluster(Transform parent, Vector3 foot, float height, int seed, Material mat)
        {
            var group = new GameObject("Bamboo").transform;
            group.SetParent(parent, false);
            group.localPosition = foot;
            Vector3[] offsets = { Vector3.zero, new Vector3(0.32f, 0f, 0.18f), new Vector3(-0.22f, 0f, 0.3f) };
            float[] heights = { 1f, 0.8f, 0.65f };
            float[] widths = { 0.3f, 0.24f, 0.2f };
            int count = 2 + seed % 2;
            float tallest = 0f;
            for (int i = 0; i < count; i++)
            {
                float h = height * heights[i] * (0.9f + (seed * 13 + i * 7) % 5 * 0.05f);
                tallest = Mathf.Max(tallest, h);
                Block(group, "Stalk", PrimitiveType.Cylinder, offsets[i] + Vector3.up * h * 0.5f,
                    new Vector3(widths[i], h * 0.5f, widths[i]), Quaternion.Euler(0f, seed * 31f + i * 90f, 0f), mat);
            }

            // 잎 다발: 줄기 꼭대기를 감싸는 엇갈린 카드 세 장
            string leaves = seed % 2 == 0 ? "BambooLeaves_A" : "BambooLeaves_B";
            FoliageCards(group, "Leaves", leaves, new Vector3(0.05f, tallest * 0.8f, 0.1f), tallest * 0.45f, 3, seed * 17f);
        }

        // 그림 카드 count장을 같은 가운데에 세워 180/count 도씩 돌려 꽂아요 (어느 각도에서 봐도 덩어리로 보여요).
        // center는 parent 기준 위치, height는 카드 세로 길이 (가로는 그림 비율대로).
        static void FoliageCards(Transform parent, string name, string textureName, Vector3 center, float height, int count, float yaw)
        {
            var mat = GetFoliageMaterial(textureName);
            var tex = (Texture2D)mat.GetTexture("_BaseMap");
            float width = height * tex.width / tex.height;
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = center;
            for (int k = 0; k < count; k++)
            {
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"Card_{k + 1}";
                Object.DestroyImmediate(card.GetComponent<Collider>());
                card.transform.SetParent(group, false);
                card.transform.localRotation = Quaternion.Euler(0f, yaw + k * 180f / count, 0f);
                card.transform.localScale = new Vector3(k % 2 == 0 ? width : -width, height, 1f);
                card.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        static Material GetFoliageMaterial(string textureName)
        {
            EnsureFolder(Env2DMaterialFolder);
            string path = $"{Env2DMaterialFolder}/Foliage_{textureName}.mat";
            var shader = Shader.Find("AThousandDawns/InkFoliageCard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null) mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Env2DArtFolder}/{textureName}.png"));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 기울인 상자 세 개를 겹쳐 각진 바위 덩어리를 만들어요
        static void RockCluster(Transform parent, Vector3 foot, float size, int seed, Material mat)
        {
            var group = new GameObject("Rock").transform;
            group.SetParent(parent, false);
            group.localPosition = foot;
            Block(group, "Big", PrimitiveType.Cube, new Vector3(0f, size * 0.35f, 0f), new Vector3(size, size * 0.8f, size * 0.9f),
                Quaternion.Euler(8f, 25f + seed * 40f, 12f), mat);
            Block(group, "Mid", PrimitiveType.Cube, new Vector3(size * 0.55f, size * 0.2f, -size * 0.3f), new Vector3(size * 0.6f, size * 0.5f, size * 0.55f),
                Quaternion.Euler(-10f, 60f + seed * 25f, 6f), mat);
            Block(group, "Small", PrimitiveType.Cube, new Vector3(-size * 0.5f, size * 0.12f, -size * 0.35f), new Vector3(size * 0.35f, size * 0.3f, size * 0.4f),
                Quaternion.Euler(15f, -20f + seed * 50f, -8f), mat);
        }

        static void Block(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // 처음 만들 때만 칸 크기·외곽선을 넣어요. 그 뒤 인스펙터에서 바꾼 값은 씬을 다시 만들어도 남아요.
        static Material GetInkTriplanar(string name, string textureName, float tileSize, float outlineWidth)
        {
            EnsureFolder(Env2DMaterialFolder);
            string path = $"{Env2DMaterialFolder}/{name}.mat";
            var shader = Shader.Find(InkTriplanarShaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                mat.SetFloat("_TileSize", tileSize);
                mat.SetFloat("_OutlineWidth", outlineWidth);
            }
            else if (shader != null) mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Env2DArtFolder}/{textureName}.png"));
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
