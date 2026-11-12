using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 폐사찰 방 구성 모델 프리팹 (InkRoomFeatures가 기본판 상자 자리에 세워요). 바닥 기준 원점, 충돌체 없음.
    //  - 깨진 돌기둥 둘 (지름 0.8, 높이 2.4): 네모 받침 위 둥근 기둥, 윗부분이 부러져 기운 토막이 얹혔거나 아예 떨어져 나갔고, 세로로 금, 밑동에 이끼·부스러기
    //  - 석불 (1.6 × 1.4, 높이 2.6, 앞 -Z): 연화대 위 결가부좌, 무릎 위에 모은 손, 머리 한쪽이 깨져 떨어진 조각이 앞에 굴러 있고 어깨·연화대에 이끼
    //  - 무너진 담 (길이 3 · X, 두께 0.5, 높이 1.2): 흙담 토막 높이가 들쭉날쭉, 성한 토막엔 기와, 한쪽이 무너져 흙더미와 기왓장
    // 천마총 방 구성 모델
    //  - 무너진 관 (길이 2 · X, 폭 1, 높이 1): 검은 돌관 앞벽이 깨져 넘어가고, 뚜껑은 뒤로 밀려나 비스듬히 걸쳤고, 안팎에 뼈
    //  - 마기 기둥 (1 × 1, 높이 2.6): 계단 받침 위 가늘어지는 검은 오벨리스크, 네 면에 붉은 마문 새김, 뾰족한 꼭대기
    //  - 혈지 가장자리 (3.5 · X × 2.5 · Z, 바닥판): 둘레의 깨진 검은 돌과 뼈만. 가운데 핏물 표면은 아트 2가 그려요
    // 죽림 방 구성 모델
    //  - 빽빽한 대숲 둘 (지름 0.7, 높이 3): 흙 둔덕 위 대나무 서너 그루 덤불, 죽순과 마른 잎
    //  - 쓰러진 대나무 (길이 3.5 · X, 폭 0.4, 높이 0.5): 둥치 셋이 엇갈려 쓰러졌고, 한쪽 끝은 부러진 밑동, 잎 다발
    //  - 개울가 (폭 2 · X, 길이 12 · Z): 물가 양쪽 조약돌과 풀 포기, 디딤돌 둘만. 물 표면은 아트 2가 그려요
    // 장강 수채 방 구성 모델 (물 둘레만)
    //  - 물가 토막 셋 (길이 4 · Z, 물 폭 2.6): 젖은 돌·낮은 갈대에 말뚝 / 통나무 / 뱃전 조각. 여울·물길 길이만큼 이어 붙여요
    //  - 웅덩이 둘레 (4 × 3): 젖은 돌·갈대 타원, 반쯤 잠긴 말뚝과 통나무
    public static partial class PrototypeSceneBuilder
    {
        const string FeatureFolder = "Assets/_Game/Art/Env/Temple/Features";

        static void DressRoomFeatures(Material stoneMat, Material bambooMat, Material roofX)
        {
            EnsureFolder(FeatureFolder);
            var moss = GetInkMaterial("Ink_Temple_Moss", "#5E6B3A", 0.5f);
            var earth = GetInkMaterial("Ink_Temple_Earth", "#5E5142", 0.4f);
            var dark = GetInkMaterial("Ink_Crack_Dark", "#141210", 0f);
            var pillars = new[]
            {
                SaveFeature(BrokenPillar(stoneMat, moss, dark, true), "Feature_Pillar_A"),
                SaveFeature(BrokenPillar(stoneMat, moss, dark, false), "Feature_Pillar_B"),
            };
            var statue = SaveFeature(StoneBuddha(stoneMat, moss, dark), "Feature_StoneBuddha");
            var wall = SaveFeature(CollapsedWall(earth, stoneMat, roofX), "Feature_CollapsedWall");
            var features = new GameObject("Ink Room Features (방 구성 모델)").AddComponent<AThousandDawns.Art.InkRoomFeatures>();
            features.Setup(pillars, statue, wall);

            // 천마총 (지역 3)
            var marble = GetInkMaterial("Ink_Tomb_Marble", "#1E1C1F", 0.2f);
            var red = GetInkMaterial("Ink_Tomb_Red", "#8E1A16", 0.8f);
            var blood = GetInkMaterial("Ink_Tomb_Blood", "#4E0B0A", 0.7f);
            var bone = GetInkMaterial("Ink_Tomb_Bone", "#CFC6B0", 0.2f);
            features.SetupTomb(
                SaveFeature(CollapsedCoffin(marble, dark, bone, blood), "Feature_TombCoffin"),
                SaveFeature(MiasmaPillar(marble, red, dark), "Feature_MiasmaPillar"),
                SaveFeature(BloodPoolRim(marble, bone, blood), "Feature_BloodPoolRim"));

            // 죽림 (지역 0)
            var bambooEarth = GetInkMaterial("Ink_Bamboo_Earth", "#4A4334", 0.35f);
            features.SetupBamboo(
                new[]
                {
                    SaveFeature(BambooClump(bambooMat, bambooEarth, 3), "Feature_BambooClump_A"),
                    SaveFeature(BambooClump(bambooMat, bambooEarth, 8), "Feature_BambooClump_B"),
                },
                SaveFeature(FallenBamboo(bambooMat, bambooEarth), "Feature_FallenBamboo"),
                SaveFeature(StreamBank(stoneMat), "Feature_StreamBank"));

            // 장강 수채 (지역 2): 물(Water) 둘레만. 물 표면은 아트 2가 그려요
            var reed = RiverMat("Ink_River_Reed", "#7C8452", 0.45f);
            var wetWood = RiverMat("Ink_River_DarkWood", "#3A2D22", 0.35f);
            var rope = RiverMat("Ink_River_Rope", "#A88F62", 0.45f);
            var wetStone = GetInkMaterial("Ink_River_WetStone", "#3B3E3C", 0.15f);
            features.SetupRiver(
                new[]
                {
                    SaveFeature(RiverBankSegment(stoneMat, wetStone, reed, wetWood, rope, 0), "Feature_RiverBank_A"),
                    SaveFeature(RiverBankSegment(stoneMat, wetStone, reed, wetWood, rope, 1), "Feature_RiverBank_B"),
                    SaveFeature(RiverBankSegment(stoneMat, wetStone, reed, wetWood, rope, 2), "Feature_RiverBank_C"),
                },
                SaveFeature(RiverPoolRim(stoneMat, wetStone, reed, wetWood), "Feature_RiverPoolRim"));
        }

        // 장강 물띠 물가 토막 (길이 4 · Z, 물 폭 2.6 · X): 물가 양쪽(x = ±1.3 언저리)에 젖은 돌·낮은 갈대, 토막마다 하나씩 다른 거리
        //  0: 기운 말뚝 셋과 밧줄, 1: 떠밀려 온 통나무, 2: 부서진 뱃전 조각. 물띠 길이만큼 이어 붙여요 (늘리지 않아요, InkRoomFeatures.RiverSegment)

        static GameObject RiverBankSegment(Material stone, Material wet, Material reed, Material wood, Material rope, int variant)
        {
            var root = new GameObject($"River Bank {(char)('A' + variant)} (장강 물가)");
            var t = root.transform;
            int seed = variant * 17 + 3;
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 6; k++, seed++)
                {
                    float z = -1.75f + k * 0.7f + ((seed * 13) % 5 - 2) * 0.06f;
                    var p = new Vector3(side * (1.3f + ((seed * 7) % 5 - 2) * 0.06f), 0f, z);
                    if (seed % 3 != 1) Rock(t, "Wet Stone", p, 0.18f + (seed * 11 % 4) * 0.06f, seed, seed * 41f, seed % 2 == 0 ? wet : stone, false);
                    if ((seed + variant) % 4 == 0)
                    {
                        // 방 안이라 갈대는 낮게 (카메라를 가리지 않게), 물 바깥쪽으로 비켜서
                        ReedClump(t, p + new Vector3(side * 0.35f, 0f, 0.1f), seed, reed);
                        t.GetChild(t.childCount - 1).localScale *= 0.45f;
                    }
                }
            switch (variant)
            {
                case 0:   // 물가에 박힌 기운 말뚝 셋, 밧줄 한 바퀴
                    for (int k = 0; k < 3; k++)
                    {
                        float h = 0.55f + k % 2 * 0.25f;
                        Block(t, "Piling", PrimitiveType.Cylinder, new Vector3(-1.1f - k * 0.18f, h * 0.5f - 0.05f, 0.6f + k * 0.35f), new Vector3(0.16f, h * 0.5f, 0.16f), Quaternion.Euler(k * 5f - 5f, 0f, 9f - k * 4f), wood);
                    }
                    Block(t, "Rope", PrimitiveType.Cylinder, new Vector3(-1.28f, 0.42f, 0.95f), new Vector3(0.2f, 0.02f, 0.2f), Quaternion.Euler(8f, 0f, 9f), rope);
                    break;
                case 1:   // 물가에 걸린 통나무 (한쪽 끝이 물에 잠겨요)
                    Block(t, "Driftwood", PrimitiveType.Capsule, new Vector3(1.05f, 0.08f, -0.4f), new Vector3(0.22f, 0.85f, 0.22f), Quaternion.Euler(0f, -28f, 86f), wood);
                    Block(t, "Branch", PrimitiveType.Capsule, new Vector3(1.35f, 0.15f, -0.9f), new Vector3(0.07f, 0.28f, 0.07f), Quaternion.Euler(30f, 40f, 60f), wood);
                    break;
                case 2:   // 부서진 뱃전 조각: 휜 널빤지 둘과 늑골
                    Block(t, "Plank", PrimitiveType.Cube, new Vector3(-1.2f, 0.07f, -0.8f), new Vector3(0.22f, 0.05f, 1.3f), Quaternion.Euler(0f, 14f, 12f), wood);
                    Block(t, "Plank", PrimitiveType.Cube, new Vector3(-1.4f, 0.14f, -0.75f), new Vector3(0.2f, 0.05f, 1.1f), Quaternion.Euler(0f, 18f, 30f), wood);
                    Block(t, "Rib", PrimitiveType.Cube, new Vector3(-1.3f, 0.18f, -0.35f), new Vector3(0.45f, 0.06f, 0.07f), Quaternion.Euler(0f, 14f, 40f), wood);
                    break;
            }
            return root;
        }

        // 장강 웅덩이 둘레 (4 · X × 3 · Z): 타원 둘레의 젖은 돌과 갈대, 반쯤 잠긴 말뚝 하나. 가운데는 비워요
        static GameObject RiverPoolRim(Material stone, Material wet, Material reed, Material wood)
        {
            var root = new GameObject("River Pool Rim (장강 웅덩이)");
            var t = root.transform;
            const int n = 16;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                var p = new Vector3(Mathf.Cos(a) * 2.05f, 0f, Mathf.Sin(a) * 1.55f);
                if (k % 5 != 2) Rock(t, "Wet Stone", p, 0.2f + (k * 7 % 4) * 0.06f, k, k * 53f, k % 2 == 0 ? wet : stone, false);
                if (k % 4 == 1)
                {
                    ReedClump(t, p * 1.12f, k + 5, reed);
                    t.GetChild(t.childCount - 1).localScale *= 0.5f;
                }
            }
            Block(t, "Sunk Piling", PrimitiveType.Cylinder, new Vector3(1.2f, 0.2f, -0.6f), new Vector3(0.15f, 0.25f, 0.15f), Quaternion.Euler(12f, 0f, -15f), wood);
            Block(t, "Driftwood", PrimitiveType.Capsule, new Vector3(-1.6f, 0.07f, 1.0f), new Vector3(0.14f, 0.5f, 0.14f), Quaternion.Euler(0f, 50f, 88f), wood);
            return root;
        }

        static GameObject SaveFeature(GameObject root, string name)
        {
            StripColliders(root.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{FeatureFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BrokenPillar(Material stone, Material moss, Material dark, bool capOn)
        {
            var root = new GameObject(capOn ? "Broken Pillar A (부러진 돌기둥)" : "Broken Pillar B (부러진 돌기둥)");
            var t = root.transform;
            Block(t, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.92f, 0.24f, 0.92f), Quaternion.identity, stone);
            Block(t, "Base Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.06f, 0.8f), Quaternion.identity, stone);
            float shaft = capOn ? 1.75f : 1.45f;
            Block(t, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0.36f + shaft * 0.5f, 0f), new Vector3(0.68f, shaft * 0.5f, 0.68f), Quaternion.identity, stone);
            float top = 0.36f + shaft;
            // 부러진 윗면: 비스듬한 토막 둘이 들쭉날쭉
            Block(t, "Jag", PrimitiveType.Cube, new Vector3(0.12f, top + 0.06f, 0.05f), new Vector3(0.38f, 0.2f, 0.44f), Quaternion.Euler(8f, 20f, -14f), stone);
            Block(t, "Jag", PrimitiveType.Cube, new Vector3(-0.15f, top + 0.02f, -0.1f), new Vector3(0.3f, 0.12f, 0.36f), Quaternion.Euler(-6f, 50f, 10f), stone);
            if (capOn)   // 부러진 윗토막이 비스듬히 얹혀 있어요
                Block(t, "Broken Top", PrimitiveType.Cylinder, new Vector3(0.06f, top + 0.3f, 0f), new Vector3(0.64f, 0.18f, 0.64f), Quaternion.Euler(0f, 0f, 12f), stone);
            else         // 떨어져 나간 윗토막은 밑동에 굴러 있어요
                Block(t, "Fallen Top", PrimitiveType.Cylinder, new Vector3(0.75f, 0.3f, 0.35f), new Vector3(0.6f, 0.38f, 0.6f), Quaternion.Euler(0f, 30f, 88f), stone);
            for (int k = 0; k < 3; k++)   // 세로 금
            {
                float a = k * 120f + 20f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Block(t, "Crack", PrimitiveType.Cube, dir * 0.34f + Vector3.up * (0.7f + k * 0.25f), new Vector3(0.03f, 0.6f + k * 0.1f, 0.02f), Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, k * 7f - 7f), dark);
            }
            Block(t, "Moss", PrimitiveType.Sphere, new Vector3(-0.32f, 0.3f, 0.25f), new Vector3(0.45f, 0.18f, 0.35f), Quaternion.identity, moss);
            Block(t, "Rubble", PrimitiveType.Cube, new Vector3(-0.55f, 0.08f, -0.4f), Vector3.one * 0.18f, Quaternion.Euler(15f, 30f, 10f), stone);
            Block(t, "Rubble", PrimitiveType.Cube, new Vector3(0.5f, 0.06f, -0.5f), Vector3.one * 0.13f, Quaternion.Euler(-10f, 70f, 5f), stone);
            return root;
        }

        static GameObject StoneBuddha(Material stone, Material moss, Material dark)
        {
            var root = new GameObject("Stone Buddha (낡은 석불)");
            var t = root.transform;
            Block(t, "Lotus Base", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(1.5f, 0.2f, 1.3f), Quaternion.identity, stone);
            for (int k = 0; k < 10; k++)   // 연잎
            {
                float a = k * 36f * Mathf.Deg2Rad;
                Block(t, "Petal", PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.68f, 0.42f, Mathf.Sin(a) * 0.58f), new Vector3(0.36f, 0.16f, 0.24f), Quaternion.Euler(0f, -k * 36f, 0f), stone);
            }
            Block(t, "Crossed Legs", PrimitiveType.Capsule, new Vector3(0f, 0.68f, -0.1f), new Vector3(0.48f, 0.62f, 0.45f), Quaternion.Euler(0f, 0f, 90f), stone);
            Block(t, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.38f, 0.05f), new Vector3(0.82f, 0.62f, 0.55f), Quaternion.identity, stone);
            Block(t, "Robe Fold", PrimitiveType.Cube, new Vector3(0.12f, 1.35f, -0.22f), new Vector3(0.06f, 0.8f, 0.04f), Quaternion.Euler(0f, 0f, 25f), dark);
            for (int s = -1; s <= 1; s += 2)
            {
                Block(t, "Upper Arm", PrimitiveType.Capsule, new Vector3(s * 0.43f, 1.35f, 0f), new Vector3(0.24f, 0.3f, 0.24f), Quaternion.Euler(-15f, 0f, s * 12f), stone);
                Block(t, "Forearm", PrimitiveType.Capsule, new Vector3(s * 0.22f, 1.0f, -0.25f), new Vector3(0.2f, 0.25f, 0.2f), Quaternion.Euler(70f, 0f, -s * 55f), stone);
            }
            Block(t, "Hands", PrimitiveType.Sphere, new Vector3(0f, 0.95f, -0.32f), new Vector3(0.32f, 0.12f, 0.22f), Quaternion.identity, stone);
            Block(t, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.98f, 0.05f), new Vector3(0.24f, 0.08f, 0.24f), Quaternion.identity, stone);
            // 머리: 오른쪽 위가 깨져 나갔어요 (깨진 면은 어두운 단면), 떨어진 조각은 연화대 앞에
            Block(t, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.25f, 0.05f), new Vector3(0.46f, 0.52f, 0.46f), Quaternion.identity, stone);
            Block(t, "Usnisha", PrimitiveType.Sphere, new Vector3(-0.05f, 2.52f, 0.05f), new Vector3(0.2f, 0.16f, 0.2f), Quaternion.identity, stone);
            Block(t, "Broken Face", PrimitiveType.Cube, new Vector3(0.17f, 2.4f, 0.0f), new Vector3(0.22f, 0.3f, 0.5f), Quaternion.Euler(0f, 0f, 35f), dark);
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Ear", PrimitiveType.Capsule, new Vector3(s * 0.24f, 2.18f, 0.05f), new Vector3(0.08f, 0.16f, 0.1f), Quaternion.identity, stone);
            Block(t, "Fallen Piece", PrimitiveType.Sphere, new Vector3(0.45f, 0.1f, -0.85f), new Vector3(0.3f, 0.2f, 0.26f), Quaternion.Euler(20f, 30f, 40f), stone);
            Block(t, "Halo (깨짐)", PrimitiveType.Cylinder, new Vector3(-0.15f, 2.1f, 0.42f), new Vector3(0.95f, 0.04f, 0.95f), Quaternion.Euler(90f, 0f, 0f), stone);
            // 이끼: 어깨·무릎·연화대
            Block(t, "Moss", PrimitiveType.Sphere, new Vector3(-0.38f, 1.65f, 0.05f), new Vector3(0.32f, 0.12f, 0.3f), Quaternion.identity, moss);
            Block(t, "Moss", PrimitiveType.Sphere, new Vector3(0.42f, 0.75f, -0.2f), new Vector3(0.3f, 0.1f, 0.26f), Quaternion.identity, moss);
            Block(t, "Moss", PrimitiveType.Sphere, new Vector3(-0.55f, 0.42f, -0.35f), new Vector3(0.45f, 0.12f, 0.3f), Quaternion.identity, moss);
            return root;
        }

        static GameObject CollapsedWall(Material earth, Material stone, Material roofX)
        {
            var root = new GameObject("Collapsed Wall (무너진 흙담)");
            var t = root.transform;
            float[] heights = { 1.2f, 1.05f, 0.55f, 0.35f, 0.95f, 1.2f };   // 가운데가 무너졌어요
            const float seg = 0.5f, thick = 0.5f;
            for (int k = 0; k < heights.Length; k++)
            {
                float x = -1.5f + seg * (k + 0.5f), h = heights[k];
                Block(t, "Earth", PrimitiveType.Cube, new Vector3(x, h * 0.5f, 0f), new Vector3(seg + 0.01f, h, thick * (h < 1f ? 0.88f : 1f)), Quaternion.Euler(0f, 0f, h < 1f ? (k % 2 == 0 ? 3f : -3f) : 0f), earth);
                if (h >= 1f)   // 성한 토막엔 기와
                {
                    var cap = new GameObject("Tile Cap");
                    cap.transform.SetParent(t, false);
                    cap.transform.localPosition = new Vector3(x, h + 0.12f, 0f);
                    cap.transform.localRotation = Quaternion.Euler(0f, 0f, k == 1 ? -10f : 0f);   // 무너진 쪽으로 처져요
                    cap.transform.localScale = new Vector3(seg + 0.04f, 0.24f, thick + 0.3f);
                    cap.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
                    cap.AddComponent<MeshRenderer>().sharedMaterial = roofX;
                }
            }
            // 무너진 자리: 앞쪽(-Z)으로 쏟아진 흙더미와 돌·기왓장
            Block(t, "Mound", PrimitiveType.Sphere, new Vector3(0.0f, 0.02f, -0.35f), new Vector3(1.3f, 0.5f, 0.8f), Quaternion.identity, earth);
            for (int k = 0; k < 5; k++)
                Block(t, "Rubble", PrimitiveType.Cube, new Vector3(-0.5f + k * 0.25f, 0.07f, -0.6f - (k % 2) * 0.2f), Vector3.one * (0.12f + k % 3 * 0.05f), Quaternion.Euler(k * 13f, k * 47f, k * 7f), stone);
            for (int k = 0; k < 3; k++)
            {
                var tile = new GameObject("Fallen Tile");
                tile.transform.SetParent(t, false);
                tile.transform.localPosition = new Vector3(-0.3f + k * 0.35f, 0.08f, -0.85f + k % 2 * 0.15f);
                tile.transform.localRotation = Quaternion.Euler(k * 10f - 10f, k * 40f, 15f - k * 12f);
                tile.transform.localScale = new Vector3(0.35f, 0.14f, 0.3f);
                tile.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
                tile.AddComponent<MeshRenderer>().sharedMaterial = roofX;
            }
            return root;
        }

        static GameObject BambooClump(Material bamboo, Material earth, int seed)
        {
            var root = new GameObject("Bamboo Clump (대숲 덤불)");
            var t = root.transform;
            Block(t, "Mound", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.85f, 0.22f, 0.8f), Quaternion.identity, earth);
            // 큰 묶음 하나 + 옆에 낮은 묶음 하나로 서너 그루 (지름 0.7 안쪽)
            BambooCluster(t, new Vector3(-0.08f, 0f, -0.1f), 3f, seed, bamboo, 0.75f);
            BambooCluster(t, new Vector3(0.18f, 0f, -0.2f), 2.1f, seed + 1, bamboo, 0.6f).localScale = new Vector3(0.8f, 1f, 0.8f);
            // 죽순과 마른 잎
            for (int k = 0; k < 2; k++)
                Block(t, "Shoot", PrimitiveType.Capsule, new Vector3(k == 0 ? 0.3f : -0.32f, 0.1f, k == 0 ? 0.2f : -0.25f), new Vector3(0.07f, 0.12f + k * 0.04f, 0.07f), Quaternion.Euler(k * 8f, 0f, k * 6f - 3f), bamboo);
            FoliageCards(t, "Litter", "Bush_A", new Vector3(0.05f, 0.12f, 0.3f), 0.28f, 2, seed * 23f);
            return root;
        }

        static GameObject FallenBamboo(Material bamboo, Material earth)
        {
            var root = new GameObject("Fallen Bamboo (쓰러진 대나무)");
            var t = root.transform;
            // 줄기 메시는 발밑이 원점이고 +Y로 뻗어요. Z로 -90도 눕히면 +X로 뻗어요
            void Log(Vector3 foot, float width, float length, float yaw, float tilt)
            {
                var s = Stalk(t, foot, width, length, 0f, bamboo);
                s.localRotation = Quaternion.Euler(0f, yaw, -90f + tilt);
            }
            Log(new Vector3(-1.7f, 0.09f, 0.08f), 0.18f, 3.4f, 2f, 0f);
            Log(new Vector3(1.7f, 0.08f, -0.1f), 0.16f, 3.2f, 182f, 0f);
            Log(new Vector3(-1.4f, 0.27f, 0f), 0.15f, 2.6f, -4f, -3f);   // 위에 걸친 둥치
            // 부러진 밑동: 왼쪽 끝에 짧게 선 그루터기와 쪼개진 끝, 반대쪽 끝엔 잎 다발
            Stalk(t, new Vector3(-1.62f, 0f, -0.12f), 0.2f, 0.42f, 40f, bamboo);
            Block(t, "Split", PrimitiveType.Cube, new Vector3(-1.62f, 0.44f, -0.12f), new Vector3(0.05f, 0.1f, 0.12f), Quaternion.Euler(0f, 40f, 18f), bamboo);
            FoliageCards(t, "Leaves", "BambooLeaves_A", new Vector3(1.55f, 0.18f, 0.05f), 0.45f, 2, 30f);
            FoliageCards(t, "Leaves", "BambooLeaves_B", new Vector3(0.6f, 0.22f, 0.1f), 0.32f, 2, 80f);
            Block(t, "Dirt", PrimitiveType.Sphere, new Vector3(-1.65f, 0f, -0.1f), new Vector3(0.6f, 0.12f, 0.5f), Quaternion.identity, earth);
            return root;
        }

        static GameObject StreamBank(Material stone)
        {
            var root = new GameObject("Stream Bank (개울가)");
            var t = root.transform;
            // 물가 양쪽(x = ±1)을 따라 조약돌과 풀 포기를 듬성듬성 (가운데 물길은 비워요)
            int seed = 0;
            for (int side = -1; side <= 1; side += 2)
                for (float z = -5.7f; z <= 5.7f; z += 0.55f, seed++)
                {
                    float jitter = ((seed * 37) % 7 - 3) * 0.05f;
                    var p = new Vector3(side * (1.02f + jitter), 0f, z + ((seed * 13) % 5 - 2) * 0.06f);
                    if (seed % 4 != 3)
                        Rock(t, "Pebble", p, 0.14f + (seed * 11 % 5) * 0.04f, seed, seed * 47f, stone, false);
                    if (seed % 3 == 0)
                        FoliageCards(t, "Grass", seed % 2 == 0 ? "Bush_A" : "Bush_B", p + new Vector3(side * 0.28f, 0.12f, 0.1f), 0.3f + (seed % 3) * 0.06f, 2, seed * 29f);
                }
            // 물속 디딤돌 둘 (물 위로 조금 솟게)
            Rock(t, "Stepping Stone", new Vector3(-0.35f, -0.05f, -2.4f), 0.4f, 1, 20f, stone, false);
            Rock(t, "Stepping Stone", new Vector3(0.4f, -0.05f, 3.1f), 0.35f, 2, 70f, stone, false);
            return root;
        }

        static GameObject CollapsedCoffin(Material marble, Material dark, Material bone, Material blood)
        {
            var root = new GameObject("Collapsed Coffin (무너진 관)");
            var t = root.transform;
            Block(t, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(2f, 0.12f, 1f), Quaternion.identity, marble);
            Block(t, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.17f, 0f), new Vector3(1.8f, 0.1f, 0.8f), Quaternion.identity, marble);
            // 관 벽: 뒤(+Z)·왼끝은 성하고, 오른끝은 기울었고, 앞(-Z) 벽은 가운데가 깨져 바깥으로 넘어졌어요
            Block(t, "Back Wall", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.36f), new Vector3(1.8f, 0.6f, 0.1f), Quaternion.identity, marble);
            Block(t, "End Wall", PrimitiveType.Cube, new Vector3(-0.86f, 0.5f, 0f), new Vector3(0.1f, 0.6f, 0.8f), Quaternion.identity, marble);
            Block(t, "End Wall", PrimitiveType.Cube, new Vector3(0.86f, 0.42f, 0f), new Vector3(0.1f, 0.45f, 0.8f), Quaternion.Euler(0f, 0f, -6f), marble);
            Block(t, "Front Wall", PrimitiveType.Cube, new Vector3(-0.55f, 0.5f, -0.36f), new Vector3(0.7f, 0.6f, 0.1f), Quaternion.identity, marble);
            Block(t, "Front Wall Fallen", PrimitiveType.Cube, new Vector3(0.2f, 0.12f, -0.62f), new Vector3(0.75f, 0.08f, 0.5f), Quaternion.Euler(-12f, 8f, 4f), marble);
            Block(t, "Front Wall Stub", PrimitiveType.Cube, new Vector3(0.62f, 0.32f, -0.36f), new Vector3(0.38f, 0.3f, 0.1f), Quaternion.Euler(0f, 0f, 9f), marble);
            Block(t, "Inside", PrimitiveType.Cube, new Vector3(0f, 0.23f, 0f), new Vector3(1.6f, 0.02f, 0.6f), Quaternion.identity, dark);
            // 뚜껑: 뒤로 밀려나 앞쪽은 관 벽에 걸치고 뒤쪽은 바닥에 닿게 기울었고, 금이 갔어요
            Block(t, "Lid", PrimitiveType.Cube, new Vector3(-0.1f, 0.5f, 0.62f), new Vector3(1.9f, 0.12f, 0.9f), Quaternion.Euler(-32f, 10f, 3f), marble);
            Block(t, "Lid Crack", PrimitiveType.Cube, new Vector3(0.35f, 0.57f, 0.6f), new Vector3(0.03f, 0.13f, 0.7f), Quaternion.Euler(-32f, 26f, 3f), dark);
            Block(t, "Lid Shard", PrimitiveType.Cube, new Vector3(1.05f, 0.06f, -0.3f), new Vector3(0.3f, 0.1f, 0.24f), Quaternion.Euler(6f, 35f, -10f), marble);
            Block(t, "Shard", PrimitiveType.Cube, new Vector3(-0.2f, 0.06f, -0.72f), Vector3.one * 0.14f, Quaternion.Euler(20f, 40f, 15f), marble);
            // 안의 뼈: 해골 하나와 갈비·다리뼈, 깨진 틈으로 흘러나온 팔뼈
            Block(t, "Skull", PrimitiveType.Sphere, new Vector3(-0.55f, 0.33f, 0.05f), new Vector3(0.2f, 0.18f, 0.22f), Quaternion.Euler(0f, 30f, 20f), bone);
            for (int k = 0; k < 4; k++)
                Block(t, "Rib", PrimitiveType.Capsule, new Vector3(-0.25f + k * 0.1f, 0.27f, 0.02f), new Vector3(0.04f, 0.14f, 0.04f), Quaternion.Euler(90f, k * 9f, 0f), bone);
            Block(t, "Leg Bone", PrimitiveType.Capsule, new Vector3(0.35f, 0.27f, 0.05f), new Vector3(0.05f, 0.25f, 0.05f), Quaternion.Euler(0f, 0f, 84f), bone);
            Block(t, "Arm Bone", PrimitiveType.Capsule, new Vector3(0.15f, 0.2f, -0.6f), new Vector3(0.04f, 0.18f, 0.04f), Quaternion.Euler(0f, 40f, 88f), bone);
            Block(t, "Arm Bone", PrimitiveType.Capsule, new Vector3(0.45f, 0.04f, -0.8f), new Vector3(0.04f, 0.14f, 0.04f), Quaternion.Euler(0f, -25f, 85f), bone);
            Block(t, "Stain", PrimitiveType.Cylinder, new Vector3(0.2f, 0.005f, -0.75f), new Vector3(0.7f, 0.005f, 0.4f), Quaternion.identity, blood);
            return root;
        }

        static GameObject MiasmaPillar(Material marble, Material red, Material dark)
        {
            var root = new GameObject("Miasma Pillar (마기 기둥)");
            var t = root.transform;
            Block(t, "Step", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(1f, 0.2f, 1f), Quaternion.identity, marble);
            Block(t, "Step", PrimitiveType.Cube, new Vector3(0f, 0.28f, 0f), new Vector3(0.82f, 0.16f, 0.82f), Quaternion.identity, marble);
            Block(t, "Step Crack", PrimitiveType.Cube, new Vector3(0.3f, 0.12f, -0.5f), new Vector3(0.03f, 0.18f, 0.02f), Quaternion.Euler(0f, 0f, 20f), dark);
            // 위로 갈수록 가늘어지는 기둥: 네 토막을 조금씩 좁혀 쌓고, 토막마다 네 면에 붉은 마문을 새겨요
            float y = 0.36f;
            for (int k = 0; k < 4; k++)
            {
                float w = 0.6f - k * 0.07f, h = 0.48f;
                var mid = new Vector3(0f, y + h * 0.5f, 0f);
                Block(t, "Shaft", PrimitiveType.Cube, mid, new Vector3(w, h + 0.01f, w), Quaternion.identity, marble);
                for (int f = 0; f < 4; f++)
                {
                    var rot = Quaternion.Euler(0f, f * 90f, 0f);
                    var c = mid + rot * Vector3.back * (w * 0.5f + 0.005f);
                    var side = rot * Vector3.right;
                    float s = (k + f) % 2 == 0 ? 1f : -1f;
                    Block(t, "Glyph", PrimitiveType.Cube, c, new Vector3(0.05f, h * 0.7f, 0.012f), rot, red);
                    Block(t, "Glyph", PrimitiveType.Cube, c + side * s * 0.08f + Vector3.up * 0.08f, new Vector3(0.14f, 0.04f, 0.012f), rot * Quaternion.Euler(0f, 0f, s * 30f), red);
                    Block(t, "Glyph", PrimitiveType.Cube, c - side * s * 0.07f - Vector3.up * 0.1f, new Vector3(0.1f, 0.035f, 0.012f), rot * Quaternion.Euler(0f, 0f, -s * 20f), red);
                }
                y += h;
            }
            // 뾰족한 꼭대기 (모서리를 세운 상자), 꼭대기 아래엔 붉게 스미는 빛
            Block(t, "Tip", PrimitiveType.Cube, new Vector3(0f, y + 0.08f, 0f), new Vector3(0.25f, 0.25f, 0.25f), Quaternion.Euler(45f, 0f, 35f), marble);
            Block(t, "Tip Glow", PrimitiveType.Sphere, new Vector3(0f, y + 0.01f, 0f), new Vector3(0.36f, 0.05f, 0.36f), Quaternion.identity, red);
            Block(t, "Chip", PrimitiveType.Cube, new Vector3(-0.45f, 0.05f, -0.42f), Vector3.one * 0.1f, Quaternion.Euler(10f, 25f, 30f), marble);
            return root;
        }

        static GameObject BloodPoolRim(Material marble, Material bone, Material blood)
        {
            var root = new GameObject("Blood Pool Rim (혈지 가장자리)");
            var t = root.transform;
            // 타원 둘레(3.5 × 2.5)를 따라 깨진 돌을 둘러요. 몇 군데는 빠졌고 높이가 들쭉날쭉
            const int n = 18;
            for (int k = 0; k < n; k++)
            {
                if (k == 4 || k == 11 || k == 12) continue;   // 무너진 틈
                float a = k * Mathf.PI * 2f / n;
                var pos = new Vector3(Mathf.Cos(a) * 1.72f, 0f, Mathf.Sin(a) * 1.22f);
                float h = 0.14f + (k * 7 % 5) * 0.035f;
                float yaw = -a * Mathf.Rad2Deg + 90f;
                Block(t, "Rim Stone", PrimitiveType.Cube, pos + Vector3.up * h * 0.5f, new Vector3(0.52f, h, 0.24f), Quaternion.Euler(k % 3 * 4f - 4f, yaw, k % 2 * 6f - 3f), marble);
            }
            for (int k = 0; k < 3; k++)   // 틈 자리 바깥으로 굴러떨어진 돌
                Block(t, "Fallen Stone", PrimitiveType.Cube, new Vector3(-1.75f - k * 0.12f, 0.06f, -0.45f - k * 0.22f), Vector3.one * (0.14f + k * 0.03f), Quaternion.Euler(k * 20f, k * 50f, 10f), marble);
            // 가장자리에 걸린 뼈: 반쯤 잠긴 해골 둘, 긴 뼈, 갈비
            Block(t, "Skull", PrimitiveType.Sphere, new Vector3(1.3f, 0.08f, 0.72f), new Vector3(0.2f, 0.17f, 0.22f), Quaternion.Euler(-20f, 40f, 15f), bone);
            Block(t, "Skull", PrimitiveType.Sphere, new Vector3(-1.15f, 0.06f, 0.85f), new Vector3(0.17f, 0.14f, 0.19f), Quaternion.Euler(30f, -60f, 0f), bone);
            Block(t, "Long Bone", PrimitiveType.Capsule, new Vector3(0.6f, 0.05f, -1.2f), new Vector3(0.05f, 0.26f, 0.05f), Quaternion.Euler(0f, 70f, 88f), bone);
            Block(t, "Long Bone", PrimitiveType.Capsule, new Vector3(-0.4f, 0.04f, 1.25f), new Vector3(0.05f, 0.22f, 0.05f), Quaternion.Euler(0f, -20f, 86f), bone);
            Block(t, "Long Bone", PrimitiveType.Capsule, new Vector3(1.68f, 0.05f, -0.3f), new Vector3(0.045f, 0.2f, 0.045f), Quaternion.Euler(0f, 10f, 80f), bone);
            for (int k = 0; k < 3; k++)
                Block(t, "Rib", PrimitiveType.Capsule, new Vector3(-1.5f + k * 0.08f, 0.04f, 0.42f + k * 0.08f), new Vector3(0.035f, 0.13f, 0.035f), Quaternion.Euler(80f, 30f + k * 8f, 0f), bone);
            // 돌 바깥으로 튄 마른 핏자국
            Block(t, "Splash", PrimitiveType.Cylinder, new Vector3(1.95f, 0.004f, 0.3f), new Vector3(0.45f, 0.004f, 0.3f), Quaternion.identity, blood);
            Block(t, "Splash", PrimitiveType.Cylinder, new Vector3(-0.9f, 0.004f, -1.42f), new Vector3(0.35f, 0.004f, 0.22f), Quaternion.Euler(0f, 30f, 0f), blood);
            return root;
        }
    }
}
