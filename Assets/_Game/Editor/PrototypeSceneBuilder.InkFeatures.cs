using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 폐사찰 방 구성 모델 프리팹 (InkRoomFeatures가 기본판 상자 자리에 세워요). 바닥 기준 원점, 충돌체 없음.
    //  - 깨진 돌기둥 둘 (지름 0.8, 높이 2.4): 네모 받침 위 둥근 기둥, 윗부분이 부러져 기운 토막이 얹혔거나 아예 떨어져 나갔고, 세로로 금, 밑동에 이끼·부스러기
    //  - 석불 (1.6 × 1.4, 높이 2.6, 앞 -Z): 연화대 위 결가부좌, 무릎 위에 모은 손, 머리 한쪽이 깨져 떨어진 조각이 앞에 굴러 있고 어깨·연화대에 이끼
    //  - 무너진 담 (길이 3 · X, 두께 0.5, 높이 1.2): 흙담 토막 높이가 들쭉날쭉, 성한 토막엔 기와, 한쪽이 무너져 흙더미와 기왓장
    public static partial class PrototypeSceneBuilder
    {
        const string FeatureFolder = "Assets/_Game/Art/Env/Temple/Features";

        static void DressRoomFeatures(Material stoneMat, Material roofX)
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
            new GameObject("Ink Room Features (방 구성 모델)").AddComponent<AThousandDawns.Art.InkRoomFeatures>().Setup(pillars, statue, wall);
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
    }
}
