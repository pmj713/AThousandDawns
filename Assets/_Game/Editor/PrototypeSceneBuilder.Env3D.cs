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
            ImportInkArt();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetupEnvCameraAndLight();

            var (floorMat, stoneMat, woodMat, bambooMat) = GetInkEnvMaterials();

            float hx = CombatSize.x * 0.5f, hz = CombatSize.y * 0.5f;
            const float doorHalf = 1.75f;
            var room = new GameObject("Room_Floor (3D blockout)").transform;

            Block(room, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(32f, 0.2f, 24f), Quaternion.identity, floorMat);
            BuildEnv3DWalls(room, hx, hz, stoneMat);
            BuildEnv3DGate(room, hz, woodMat);
            BuildEnv3DPalisade(room, hx, hz, doorHalf, woodMat);
            BuildEnv3DForest(room, hx, hz, doorHalf, bambooMat);
            BuildEnv3DRest(room, hz, stoneMat, bambooMat);

            BuildInkCharacters();

            EnsureFolder("Assets/_Game/Scenes");
            EditorSceneManager.SaveScene(scene, Env3DScenePath);
            AssetDatabase.SaveAssets();
        }

        static void ImportInkArt()
        {
            foreach (var name in new[] { "Floor_Ground", "Tex_Stone", "Tex_Wood", "Tex_BambooStalk" })
                ImportAsTiledTexture($"{Env2DArtFolder}/{name}.png");
            foreach (var name in new[] { "Bush_A", "Bush_B", "LanternPost_A", "BambooLeaves_A", "BambooLeaves_B" })
                ImportAsSprite($"{Env2DArtFolder}/{name}.png");
            ImportAsSprite($"{Env2DArtFolder}/Source/Backdrop_Ink.png");
        }

        // 수묵 구조물 재질 넷. 대나무·바닥 값은 코드가 정해요 (씬을 다시 만들면 덮어씀). 메시 캐시도 여기서 비워요
        static (Material floor, Material stone, Material wood, Material bamboo) GetInkEnvMaterials()
        {
            var floorMat = GetInkTriplanar("Ink3D_Floor", "Floor_Ground", 4f, 0f);
            var stoneMat = GetInkTriplanar("Ink3D_Stone", "Tex_Stone", 1.5f, 2.5f);
            var woodMat = GetInkTriplanar("Ink3D_Wood", "Tex_Wood", 1f, 2f);
            var bambooMat = GetInkTriplanar("Ink3D_Bamboo", "Tex_BambooStalk", 1.2f, 2f);
            // 수묵 대나무 그림처럼: 줄기는 푸른 기가 도는 짙은 먹색 한 획, 마디는 하얀 틈(버텍스 색),
            // 바닥은 한지처럼 밝게 띄워 진한 줄기·잎과 대비를 줘요. 먼 숲은 한지색 안개(SetupEnvCameraAndLight)로 옅어져요.
            // 줄기 메시는 발밑이 원점이고 법선이 부드러워서 외곽선은 법선 방향으로 부풀려요.
            // 이 값들은 코드가 정해요 (씬을 다시 만들면 덮어씀).
            bambooMat.SetFloat("_OutlineFromCenter", 0f);
            bambooMat.SetFloat("_OutlineWidth", 1f);
            bambooMat.SetFloat("_VertexInk", 1f);
            bambooMat.SetFloat("_PaperLift", 0f);
            bambooMat.SetFloat("_Saturation", 0f);
            bambooMat.SetFloat("_RimInk", 0.55f);
            bambooMat.SetFloat("_RimPower", 2f);
            bambooMat.SetFloat("_Bands", 3f);
            bambooMat.SetFloat("_BandSoftness", 0.3f);
            bambooMat.SetColor("_BaseColor", new Color(0.48f, 0.48f, 0.48f, 1f));
            bambooMat.SetFloat("_StreakLight", 0.6f);   // 세로로 옅은 마른 붓 결
            bambooMat.SetFloat("_StreakScale", 8f);
            bambooMat.SetFloat("_PatchLight", 0.45f);   // 마디·줄기마다 다른 옅은 얼룩
            bambooMat.SetFloat("_PatchScale", 0.9f);
            bambooMat.SetColor("_InkColor", new Color(0.08f, 0.1f, 0.1f, 1f));
            floorMat.SetFloat("_PaperLift", 0.55f);
            floorMat.SetFloat("_Saturation", 0f);
            s_bambooStalkMesh = null;
            s_bushBlobMeshes.Clear();
            return (floorMat, stoneMat, woodMat, bambooMat);
        }

        // 방 안 소품: 대나무 기둥 6개·바위·덤불·등불·먼 풍경
        static void BuildEnv3DRest(Transform room, float hz, Material stoneMat, Material bambooMat)
        {
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
            for (int i = 0; i < bushSpots.Length; i++) BushClump(flat, bushSpots[i], i);
            Billboard(flat, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png", new Vector3(-2.6f, 0f, hz + 0.9f), 3.6f);
            Billboard(flat, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png", new Vector3(2.6f, 0f, hz + 0.9f), 3.6f, true);
            Billboard(flat, "Backdrop_Ink", $"{Env2DArtFolder}/Source/Backdrop_Ink.png", new Vector3(0f, 0f, hz + 6f), 22f);
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
        static Transform BambooCluster(Transform parent, Vector3 foot, float height, int seed, Material mat)
        {
            var group = new GameObject("Bamboo").transform;
            group.SetParent(parent, false);
            group.localPosition = foot;
            Vector3[] offsets = { Vector3.zero, new Vector3(0.32f, 0f, 0.18f), new Vector3(-0.22f, 0f, 0.3f) };
            float[] heights = { 1f, 0.8f, 0.65f };
            float[] widths = { 0.18f, 0.15f, 0.12f }; // 잎 다발에 비해 가늘게 (수묵 대나무 비율)
            int count = 2 + seed % 2;
            float tallest = 0f;
            string leaves = seed % 2 == 0 ? "BambooLeaves_A" : "BambooLeaves_B";
            for (int i = 0; i < count; i++)
            {
                float h = height * heights[i] * (0.9f + (seed * 13 + i * 7) % 5 * 0.05f);
                tallest = Mathf.Max(tallest, h);
                var stalk = Stalk(group, offsets[i], widths[i], h, seed * 31f + i * 90f, mat);
                StalkSprigs(group, stalk, h, seed * 7 + i * 3, i % 2 == 0 ? "BambooLeaves_B" : "BambooLeaves_A");
            }

            // 잎 다발: 줄기 꼭대기를 감싸는 엇갈린 카드 세 장 + 위에서 내려다볼 때 꼭대기를 덮는 눕힌 카드 한 장
            FoliageCards(group, "Leaves", leaves, new Vector3(0.05f, tallest * 0.8f, 0.1f), tallest * 0.45f, 3, seed * 17f);
            var canopy = new GameObject("Canopy").transform;
            canopy.SetParent(group, false);
            canopy.localPosition = new Vector3(0.05f, tallest * 0.93f, 0.1f);
            canopy.localRotation = Quaternion.Euler(70f, seed * 53f, 0f);
            FoliageCards(canopy, "Card", leaves, Vector3.zero, tallest * 0.4f, 1, 0f);
            return group;
        }

        // 줄기 마디에서 옆으로 삐져나온 작은 잎 가지. 가운데 마디들(꼭대기 잎 다발 아래) 중 몇 곳에만,
        // 마디마다 좌우를 번갈아 달아요. 위치는 줄기 메시의 마디 높이·휨(GetBambooStalkMesh와 같은 값)을 따라가요.
        static void StalkSprigs(Transform group, Transform stalk, float height, int seed, string leaves)
        {
            float size = Mathf.Clamp(height * 0.14f, 0.45f, 0.8f);
            for (int n = 2; n <= BambooNodes - 1; n++)
            {
                if ((seed + n * 5) % 3 == 0) continue; // 마디 셋 중 둘쯤에만
                float y = n / (BambooNodes + 1f);
                float side = (seed + n) % 2 == 0 ? 1f : -1f;
                Vector3 node = stalk.TransformPoint(new Vector3(BambooBend * y * y + side * 0.5f, y, 0f));
                Vector3 outward = stalk.TransformDirection(Vector3.right * side).normalized;
                Vector3 center = group.InverseTransformPoint(node) + outward * size * 0.4f + Vector3.down * size * 0.15f;
                float yaw = stalk.localEulerAngles.y + side * 25f + n * 20f;
                FoliageCards(group, "Sprig", leaves, center, size, 2, yaw);
            }
        }

        // 위로 갈수록 가늘어지다 끝이 뾰족하게 닫히는 줄기 (원기둥의 평평한 윗면이 위에서 보이지 않게)
        static Transform Stalk(Transform parent, Vector3 localFoot, float width, float height, float yaw, Material mat)
        {
            var go = new GameObject("Stalk");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localFoot;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(width, height, width);
            go.AddComponent<MeshFilter>().sharedMesh = GetBambooStalkMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.height = 1f;
            col.radius = 0.5f;
            return go.transform;
        }

        const int BambooNodes = 5;      // 줄기 메시 마디 수 (잎 가지도 이 마디에 붙어요)
        const float BambooBend = 0.6f;  // 꼭대기가 +x로 휘는 양 (메시 단위)
        const string BambooStalkMeshPath = "Assets/_Game/Art/Env/BurningBamboo/Meshes/BambooStalk.asset";

        // 지름 1, 키 1, 발밑이 원점인 줄기 메시. 법선을 부드럽게 이어 붓선 외곽선이 끊기지 않아요 (UV는 트라이플래너라 필요 없음).
        // 수묵 대나무처럼 마디마다 한 획: 버텍스 색 빨강 = 먹, 초록 = 종이색. 마디는 하얗게 끊긴 틈(살짝 볼록),
        // 틈 바로 위는 붓을 눌러 시작한 듯 가장 진하고, 획 가운데로 갈수록 조금 옅어졌다가 다음 마디 앞에서 다시 짙어져요.
        // 위로 갈수록 가늘어지고 +x 쪽으로 조금 휘어요 (줄기마다 돌려 세우니 휘는 방향은 제각각).
        static Mesh s_bambooStalkMesh; // 씬을 만들 때마다 한 번만 다시 만들어요

        static Mesh GetBambooStalkMesh()
        {
            if (s_bambooStalkMesh != null) return s_bambooStalkMesh;
            const int sides = 12;

            // (높이, 굵기 배율, 먹, 종이색) 고리 목록
            var rings = new System.Collections.Generic.List<Vector4> { new Vector4(0f, 1f, 0.7f, 0f) };
            for (int n = 1; n <= BambooNodes; n++)
            {
                float y = n / (BambooNodes + 1f);
                float seg = 1f / (BambooNodes + 1f);
                rings.Add(new Vector4(y - seg * 0.5f, 1f, 0.2f, 0f));   // 획 가운데: 옅게
                // 하얀 틈은 고리 하나로만, 양옆 고리를 바짝 붙여 색이 번지는 폭을 좁혀요 (가는 선으로 보이게)
                rings.Add(new Vector4(y - 0.03f, 1f, 0.75f, 0f));       // 마디 앞: 다시 짙게
                rings.Add(new Vector4(y - 0.005f, 1.03f, 0.6f, 0f));
                rings.Add(new Vector4(y, 1.08f, 0f, 0.9f));             // 마디: 하얀 틈 (가늘게)
                rings.Add(new Vector4(y + 0.005f, 1.03f, 0.85f, 0f));   // 틈 바로 위: 획 시작, 가장 진하게
                rings.Add(new Vector4(y + 0.04f, 1f, 0.65f, 0f));
            }
            rings.Add(new Vector4(0.96f, 0.6f, 0.5f, 0f));

            var verts = new System.Collections.Generic.List<Vector3>();
            var colors = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();
            foreach (var ring in rings)
            {
                float radius = (0.5f - 0.15f * ring.x) * ring.y;
                float sway = BambooBend * ring.x * ring.x;
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    verts.Add(new Vector3(Mathf.Cos(a) * radius + sway, ring.x, Mathf.Sin(a) * radius));
                    colors.Add(new Color(ring.z, ring.w, 0f, 1f));
                }
            }
            int tip = verts.Count;
            verts.Add(new Vector3(BambooBend, 1f, 0f));
            colors.Add(new Color(0.7f, 0f, 0f, 1f));
            int ringCount = rings.Count;

            for (int r = 0; r < ringCount - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a = r * sides + s, c = r * sides + (s + 1) % sides;
                    int b = a + sides, d = c + sides;
                    tris.AddRange(new[] { a, b, d, a, d, c });
                }
            int top = (ringCount - 1) * sides;
            for (int s = 0; s < sides; s++) tris.AddRange(new[] { top + s, tip, top + (s + 1) % sides });

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BambooStalkMeshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "BambooStalk" };
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew)
            {
                EnsureFolder("Assets/_Game/Art/Env/BurningBamboo/Meshes");
                AssetDatabase.CreateAsset(mesh, BambooStalkMeshPath);
            }
            else EditorUtility.SetDirty(mesh);
            s_bambooStalkMesh = mesh;
            return mesh;
        }

        // 풀숲: 비대칭으로 겹친 작은 덩이 2~3개 위에 흩뿌린 작은 카드들 + 가장자리에서 삐져나온 가지 카드들.
        // 카드는 항상 카메라를 정면으로 봐서(InkFoliageCard Billboard) 위에서 내려다봐도 옆면(별 모양)이 안 보여요.
        // 매끈한 공처럼 뭉쳐 보이지 않게 윤곽·간격을 들쭉날쭉하게 해요. 폭 약 2 (시험판 풀숲 1.5보다 조금 큼)
        static Transform BushClump(Transform parent, Vector3 foot, int seed)
        {
            var bush = new GameObject("Bush").transform;
            bush.SetParent(parent, false);
            bush.localPosition = foot;
            bush.localRotation = Quaternion.Euler(0f, seed * 73f, 0f);

            string texture = seed % 2 == 0 ? "Bush_A" : "Bush_B";
            var mat = GetFoliageBlobMaterial(texture);
            var tex = (Texture2D)mat.GetTexture("_BaseMap");
            var leaves = new GameObject("Leaves (camera-facing cards)");
            leaves.transform.SetParent(bush, false);
            leaves.AddComponent<MeshFilter>().sharedMesh = GetBushBlobMesh(seed % 3, (float)tex.width / tex.height);
            leaves.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return bush;
        }

        static Material GetFoliageBlobMaterial(string textureName)
        {
            EnsureFolder(Env2DMaterialFolder);
            string path = $"{Env2DMaterialFolder}/FoliageBlob_{textureName}.mat";
            var shader = Shader.Find("AThousandDawns/InkFoliageCard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null) mat.shader = shader;
            mat.SetFloat("_Billboard", 1f);
            mat.SetFloat("_NormalInk", 0.3f);   // 공처럼 둥근 명암이 되지 않게 약하게
            mat.SetFloat("_BottomInk", 0.15f);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Env2DArtFolder}/{textureName}.png"));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static readonly System.Collections.Generic.Dictionary<int, Mesh> s_bushBlobMeshes = new System.Collections.Generic.Dictionary<int, Mesh>();

        // 덤불 덩어리 메시: 카드마다 꼭짓점 넷을 카드 가운데에 두고, UV1에 모서리 오프셋(카드 크기·기울기 포함)을 넣어요.
        // 법선은 덩어리 중심에서 바깥쪽 (셰이더의 Normal Ink용). variant마다 흩뿌림이 달라요.
        static Mesh GetBushBlobMesh(int variant, float aspect)
        {
            if (s_bushBlobMeshes.TryGetValue(variant, out var cached) && cached != null) return cached;

            var rnd = new System.Random(variant * 97 + 5);
            float Rand(float min, float max) => min + (float)rnd.NextDouble() * (max - min);

            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uv0 = new System.Collections.Generic.List<Vector2>();
            var uv1 = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();

            // 카드 하나: pos는 카드 가운데, h는 세로, tiltDeg는 화면에서 기울기, normal은 셰이더 명암용 바깥 방향
            void AddCard(Vector3 pos, Vector3 normal, float h, float tiltDeg)
            {
                float w = h * aspect * (rnd.NextDouble() < 0.5 ? -1f : 1f);
                float tilt = tiltDeg * Mathf.Deg2Rad;
                float cos = Mathf.Cos(tilt), sin = Mathf.Sin(tilt);
                int start = verts.Count;
                for (int c = 0; c < 4; c++)
                {
                    float u = c % 2, v = c / 2;
                    var corner = new Vector2((u - 0.5f) * w, (v - 0.5f) * h);
                    verts.Add(pos);
                    normals.Add(normal.normalized);
                    uv0.Add(new Vector2(u, v));
                    uv1.Add(new Vector2(corner.x * cos - corner.y * sin, corner.x * sin + corner.y * cos));
                }
                tris.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }

            // 비대칭으로 겹친 작은 덩이 2~3개: 덩이마다 크기·높이가 달라 윤곽이 울퉁불퉁해요
            int lobes = 2 + variant % 2;
            for (int l = 0; l < lobes; l++)
            {
                float a = (l * 360f / lobes + Rand(-35f, 35f)) * Mathf.Deg2Rad;
                var lobeCenter = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Rand(0.2f, 0.45f) + Vector3.up * Rand(0.15f, 0.3f);
                var radii = new Vector3(Rand(0.4f, 0.6f), Rand(0.25f, 0.45f), Rand(0.4f, 0.6f));
                int cards = 9 + rnd.Next(4);
                for (int k = 0; k < cards; k++)
                {
                    // 덩이 위쪽 반구에 흩뿌리되 간격을 일부러 고르지 않게 (빈 틈·겹침이 생겨요)
                    float phi = Rand(0f, Mathf.PI * 2f);
                    float yUnit = Rand(-0.15f, 1f);
                    float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - yUnit * yUnit));
                    var dir = new Vector3(Mathf.Cos(phi) * ring, yUnit, Mathf.Sin(phi) * ring);
                    var pos = lobeCenter + Vector3.Scale(dir * Rand(0.5f, 1.05f), radii);
                    pos.y = Mathf.Max(pos.y, 0.12f);
                    AddCard(pos, dir, Rand(0.35f, 0.55f), Rand(-35f, 35f));
                }
            }

            // 가장자리에서 위로 삐져나온 가지: 윤곽을 거칠게
            int sprigs = 4 + rnd.Next(3);
            for (int k = 0; k < sprigs; k++)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var pos = outward * Rand(0.5f, 0.85f) + Vector3.up * Rand(0.3f, 0.55f);
                float lean = Vector3.Dot(outward, Vector3.right) * 25f; // 화면에서 바깥쪽으로 기울게
                AddCard(pos, outward + Vector3.up, Rand(0.45f, 0.7f), -lean + Rand(-15f, 15f));
            }

            string path = $"Assets/_Game/Art/Env/BurningBamboo/Meshes/BushBlob_{variant}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = $"BushBlob_{variant}" };
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(tris, 0);
            // 꼭짓점이 카드 가운데에 모여 있어 자동 범위가 작아요. 펼쳐진 카드까지 덮게 넓혀 둬요 (화면 끝에서 잘림 방지)
            mesh.bounds = new Bounds(new Vector3(0f, 0.4f, 0f), new Vector3(3.5f, 2f, 3.5f));
            if (isNew)
            {
                EnsureFolder("Assets/_Game/Art/Env/BurningBamboo/Meshes");
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);
            s_bushBlobMeshes[variant] = mesh;
            return mesh;
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
