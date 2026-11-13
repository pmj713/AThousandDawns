using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 마을 생활 소품 (사람 사는 기척). 객잔·사당이 있는 서쪽은 두고, 비어 있던 동쪽 반을 채워요. 남쪽 가장자리는 카메라를 가리지 않게 비워요.
    //  - 우물 (6.5, 2.2): 돌 우물 둘레, 나무 틀과 도르래, 두레박
    //  - 빨랫줄 (북동, z 5.6): 기둥 둘 사이 줄에 무명·쪽빛·황토 빨래 다섯 (InkVillageLife가 바람에 흔들어요)
    //  - 장작더미 (동굴 동쪽, 4.9, 6.6): 쌓은 장작, 도끼가 박힌 모탕, 쪼갠 장작
    //  - 장독대 (9.8, -1.2): 낮은 돌 단 위 크고 작은 장독과 뚜껑
    //  - 닭장 (9.6, -4.6): 짚 지붕 닭장과 낮은 울타리, 닭 셋 (InkVillageLife가 모이를 쪼게 해요)
    // 다른 마을 꾸밈처럼 충돌체는 없어요 (StripColliders)
    public static partial class PrototypeSceneBuilder
    {
        static void BuildVillageLife(Transform deco, Vector3 c, Material stone, Material wood, Material dark, Material roofX)
        {
            var root = new GameObject("Village Life (마을 생활 소품)").transform;
            root.SetParent(deco, false);
            root.position = c;
            var water = GetInkMaterial("Ink_Well_Water", "#1E2A2E", 0.2f);
            var rope = GetInkMaterial("Ink_River_Rope", "#A88F62", 0.45f);
            var straw = GetInkMaterial("Ink_Village_Straw", "#B59A5A", 0.5f);
            var glaze = GetInkMaterial("Ink_Village_JarGlaze", "#4A3022", 0.4f);   // 옹기 잿물
            var cotton = GetInkMaterial("Ink_Village_Cotton", "#EFE9DC", 0f);
            var indigo = GetInkMaterial("Ink_Village_Indigo", "#34476B", 0.7f);
            var ochre = GetInkMaterial("Ink_Village_Ochre", "#B5813E", 0.6f);
            var feather = GetInkMaterial("Ink_Village_Feather", "#F2EEE4", 0f);
            var comb = GetInkMaterial("Ink_Village_Comb", "#C0281E", 0.9f);
            var beak = GetInkMaterial("Ink_Village_Beak", "#D9A13B", 0.7f);

            BuildWell(root, new Vector3(6.5f, 0f, 2.2f), stone, wood, dark, water, rope);
            var cloths = BuildLaundry(root, new Vector3(9.3f, 0f, 5.6f), wood, rope, cotton, indigo, ochre);
            BuildWoodpile(root, new Vector3(4.9f, 0f, 6.6f), wood, dark);
            BuildJarTerrace(root, new Vector3(9.8f, 0f, -1.2f), stone, glaze, dark);
            var chickens = BuildCoop(root, new Vector3(9.6f, 0f, -4.6f), wood, straw, dark, feather, comb, beak);
            root.gameObject.AddComponent<AThousandDawns.Art.InkVillageLife>().Setup(cloths, chickens);
            BuildVillagers(root, wood);   // 우물·장작더미·사당·객잔 앞의 마을 주민 (PrototypeSceneBuilder.InkVillagers.cs)
        }

        static void BuildWell(Transform root, Vector3 at, Material stone, Material wood, Material dark, Material water, Material rope)
        {
            var t = new GameObject("Well (우물)").transform;
            t.SetParent(root, false);
            t.localPosition = at;
            // 둘레: 돌 열둘을 둥글게 쌓고 위에 넓적한 갓돌
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Block(t, "Ring Stone", PrimitiveType.Cube, dir * 0.55f + Vector3.up * 0.3f, new Vector3(0.32f, 0.6f, 0.2f), Quaternion.Euler(0f, a, k % 2 * 3f), stone);
                Block(t, "Rim Stone", PrimitiveType.Cube, dir * 0.55f + Vector3.up * 0.64f, new Vector3(0.34f, 0.08f, 0.28f), Quaternion.Euler(0f, a + 4f, 0f), stone);
            }
            Block(t, "Water", PrimitiveType.Cylinder, Vector3.up * 0.42f, new Vector3(0.9f, 0.01f, 0.9f), Quaternion.identity, water);
            // 나무 틀: 기둥 둘, 가로대, 도르래, 늘어진 줄과 갓돌에 얹은 두레박
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Post", PrimitiveType.Cube, new Vector3(s * 0.72f, 0.85f, 0f), new Vector3(0.12f, 1.7f, 0.12f), Quaternion.Euler(0f, 0f, -s * 3f), wood);
            Block(t, "Beam", PrimitiveType.Cube, new Vector3(0f, 1.66f, 0f), new Vector3(1.65f, 0.12f, 0.14f), Quaternion.identity, wood);
            Block(t, "Pulley", PrimitiveType.Cylinder, new Vector3(0f, 1.52f, 0f), new Vector3(0.18f, 0.04f, 0.18f), Quaternion.Euler(0f, 0f, 90f), dark);
            Block(t, "Rope", PrimitiveType.Cylinder, new Vector3(0.05f, 1.1f, 0f), new Vector3(0.025f, 0.42f, 0.025f), Quaternion.identity, rope);
            Block(t, "Bucket", PrimitiveType.Cylinder, new Vector3(0.45f, 0.82f, -0.42f), new Vector3(0.24f, 0.13f, 0.24f), Quaternion.Euler(0f, 0f, 8f), wood);
            Block(t, "Bucket Band", PrimitiveType.Cylinder, new Vector3(0.45f, 0.86f, -0.42f), new Vector3(0.25f, 0.015f, 0.25f), Quaternion.Euler(0f, 0f, 8f), dark);
            Block(t, "Puddle", PrimitiveType.Cylinder, new Vector3(-0.6f, 0.003f, -0.75f), new Vector3(0.6f, 0.003f, 0.35f), Quaternion.Euler(0f, 20f, 0f), water);
        }

        static Transform[] BuildLaundry(Transform root, Vector3 at, Material wood, Material rope, Material cotton, Material indigo, Material ochre)
        {
            var t = new GameObject("Laundry Line (빨랫줄)").transform;
            t.SetParent(root, false);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, -6f, 0f);
            const float half = 1.7f, line = 1.75f;
            for (int s = -1; s <= 1; s += 2)
            {
                Block(t, "Post", PrimitiveType.Cube, new Vector3(s * half, 0.95f, 0f), new Vector3(0.1f, 1.9f, 0.1f), Quaternion.Euler(0f, 0f, s * 2f), wood);
                Block(t, "Fork", PrimitiveType.Cube, new Vector3(s * half, 1.88f, 0f), new Vector3(0.22f, 0.06f, 0.07f), Quaternion.Euler(0f, 0f, s * 25f), wood);
            }
            Block(t, "Line", PrimitiveType.Cylinder, new Vector3(0f, line, 0f), new Vector3(0.018f, half, 0.018f), Quaternion.Euler(0f, 0f, 90f), rope);
            // 빨래: 줄에 매단 윗변이 회전축 (자식 천은 아래로 늘어져요). 바지·저고리·보자기 크기를 섞어요
            var specs = new (float x, float w, float h, Material m)[]
            {
                (-1.15f, 0.55f, 0.75f, cotton), (-0.5f, 0.45f, 0.95f, indigo), (0.1f, 0.6f, 0.6f, ochre), (0.7f, 0.42f, 0.85f, cotton), (1.25f, 0.4f, 0.55f, indigo),
            };
            var pivots = new List<Transform>();
            foreach (var (x, w, h, m) in specs)
            {
                var p = new GameObject("Cloth").transform;
                p.SetParent(t, false);
                p.localPosition = new Vector3(x, line - 0.02f, 0f);
                Block(p, "Cloth", PrimitiveType.Cube, new Vector3(0f, -h * 0.5f, 0f), new Vector3(w, h, 0.02f), Quaternion.Euler(0f, 0f, (x * 7f) % 4f - 2f), m);
                Block(p, "Fold", PrimitiveType.Cube, new Vector3(0f, -0.03f, -0.012f), new Vector3(w * 1.02f, 0.06f, 0.02f), Quaternion.identity, m);   // 줄에 걸쳐 넘어온 단
                pivots.Add(p);
            }
            Block(t, "Basket", PrimitiveType.Cylinder, new Vector3(-2.1f, 0.14f, -0.35f), new Vector3(0.55f, 0.14f, 0.55f), Quaternion.identity, wood);
            Block(t, "Basket Cloth", PrimitiveType.Sphere, new Vector3(-2.1f, 0.28f, -0.35f), new Vector3(0.45f, 0.14f, 0.45f), Quaternion.identity, cotton);
            return pivots.ToArray();
        }

        static void BuildWoodpile(Transform root, Vector3 at, Material wood, Material dark)
        {
            var t = new GameObject("Woodpile (장작더미)").transform;
            t.SetParent(root, false);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, -10f, 0f);
            // 장작: 앞뒤로 누운 통나무를 네 단, 위로 갈수록 줄여 쌓아요 (마구리가 앞을 봐요)
            int[] rows = { 6, 5, 4, 2 };
            for (int r = 0; r < rows.Length; r++)
                for (int k = 0; k < rows[r]; k++)
                {
                    float x = (k - (rows[r] - 1) * 0.5f) * 0.24f + (r % 2) * 0.03f;
                    Block(t, "Log", PrimitiveType.Cylinder, new Vector3(x, 0.12f + r * 0.2f, 0.25f), new Vector3(0.22f, 0.4f, 0.22f), Quaternion.Euler(90f, (k * 13 + r * 7) % 9 - 4f, 0f), (k + r) % 3 == 0 ? dark : wood);
                }
            // 모탕과 박힌 도끼, 쪼갠 장작
            Block(t, "Block", PrimitiveType.Cylinder, new Vector3(1.05f, 0.22f, -0.35f), new Vector3(0.45f, 0.22f, 0.45f), Quaternion.identity, wood);
            Block(t, "Axe Handle", PrimitiveType.Cube, new Vector3(1.2f, 0.62f, -0.35f), new Vector3(0.05f, 0.6f, 0.05f), Quaternion.Euler(0f, 0f, -38f), wood);
            Block(t, "Axe Head", PrimitiveType.Cube, new Vector3(1.04f, 0.46f, -0.35f), new Vector3(0.2f, 0.12f, 0.03f), Quaternion.Euler(0f, 0f, -38f), dark);
            for (int k = 0; k < 3; k++)
                Block(t, "Split", PrimitiveType.Cube, new Vector3(0.75f + k * 0.22f, 0.05f, -0.75f - k % 2 * 0.15f), new Vector3(0.1f, 0.1f, 0.36f), Quaternion.Euler(0f, k * 35f - 20f, 45f), wood);
        }

        static void BuildJarTerrace(Transform root, Vector3 at, Material stone, Material glaze, Material dark)
        {
            var t = new GameObject("Jar Terrace (장독대)").transform;
            t.SetParent(root, false);
            t.localPosition = at;
            Block(t, "Platform", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(2.3f, 0.24f, 1.5f), Quaternion.identity, stone);
            for (int k = 0; k < 6; k++)   // 단 가장자리 돌
                Block(t, "Edge Stone", PrimitiveType.Cube, new Vector3(-1.05f + k * 0.42f, 0.07f, -0.8f), new Vector3(0.4f, 0.16f, 0.12f), Quaternion.Euler(0f, k * 11 % 7 - 3f, 0f), stone);
            // 뒷줄 큰 독, 앞줄 작은 단지 (옹기 메시 셋을 돌려 써요), 큰 독엔 뚜껑
            var jars = new (Vector3 at, float s, int v, bool lid)[]
            {
                (new Vector3(-0.75f, 0.24f, 0.3f), 1.45f, 0, true), (new Vector3(0f, 0.24f, 0.35f), 1.6f, 0, true), (new Vector3(0.75f, 0.24f, 0.3f), 1.35f, 1, false),
                (new Vector3(-0.55f, 0.24f, -0.35f), 0.95f, 2, true), (new Vector3(0.1f, 0.24f, -0.3f), 1.0f, 0, true), (new Vector3(0.7f, 0.24f, -0.38f), 0.85f, 2, false),
            };
            int i = 0;
            foreach (var (p, s, v, lid) in jars)
            {
                var jar = new GameObject("Jar");
                jar.transform.SetParent(t, false);
                jar.transform.localPosition = p;
                jar.transform.localRotation = Quaternion.Euler(0f, i++ * 47f, 0f);
                jar.transform.localScale = Vector3.one * s;
                jar.AddComponent<MeshFilter>().sharedMesh = GetJarMesh(v);
                jar.AddComponent<MeshRenderer>().sharedMaterial = glaze;
                if (lid)
                {
                    float top = v == 0 ? 0.7f : v == 2 ? 0.47f : 0.74f;
                    Block(t, "Lid", PrimitiveType.Cylinder, p + Vector3.up * (top * s + 0.02f), new Vector3(0.38f * s, 0.025f, 0.38f * s), Quaternion.identity, dark);
                    Block(t, "Lid Knob", PrimitiveType.Sphere, p + Vector3.up * (top * s + 0.06f), Vector3.one * 0.06f * s, Quaternion.identity, dark);
                }
            }
        }

        static Transform[] BuildCoop(Transform root, Vector3 at, Material wood, Material straw, Material dark, Material feather, Material comb, Material beak)
        {
            var t = new GameObject("Chicken Coop (닭장)").transform;
            t.SetParent(root, false);
            t.localPosition = at;
            // 닭장: 다리 넷 위 나무 상자, 짚 지붕 (맞배), 닭이 오르는 비탈 널
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Block(t, "Leg", PrimitiveType.Cube, new Vector3(sx * 0.48f, 0.2f, 1.0f + sz * 0.3f), new Vector3(0.08f, 0.4f, 0.08f), Quaternion.identity, wood);
            Block(t, "Box", PrimitiveType.Cube, new Vector3(0f, 0.72f, 1.0f), new Vector3(1.1f, 0.65f, 0.75f), Quaternion.identity, wood);
            Block(t, "Door", PrimitiveType.Cube, new Vector3(0.18f, 0.62f, 0.62f), new Vector3(0.26f, 0.3f, 0.02f), Quaternion.identity, dark);
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Thatch", PrimitiveType.Cube, new Vector3(0f, 1.18f, 1.0f + s * 0.24f), new Vector3(1.35f, 0.08f, 0.62f), Quaternion.Euler(-s * 30f, 0f, 0f), straw);
            Block(t, "Ramp", PrimitiveType.Cube, new Vector3(0.18f, 0.25f, 0.3f), new Vector3(0.2f, 0.03f, 0.75f), Quaternion.Euler(-32f, 0f, 0f), wood);
            // 낮은 울타리: 앞마당 셋 면을 말뚝과 가로대로 둘러요 (앞쪽 가운데는 틈)
            var fence = new (Vector3 a, Vector3 b)[]
            {
                (new Vector3(-1.1f, 0f, 1.4f), new Vector3(-1.1f, 0f, -0.9f)), (new Vector3(1.1f, 0f, 1.4f), new Vector3(1.1f, 0f, -0.9f)),
                (new Vector3(-1.1f, 0f, -0.9f), new Vector3(-0.35f, 0f, -0.9f)), (new Vector3(0.35f, 0f, -0.9f), new Vector3(1.1f, 0f, -0.9f)),
            };
            foreach (var (a, b) in fence)
            {
                int n = Mathf.Max(2, Mathf.RoundToInt((b - a).magnitude / 0.4f) + 1);
                for (int k = 0; k < n; k++)
                {
                    var p = Vector3.Lerp(a, b, k / (n - 1f));
                    Block(t, "Stake", PrimitiveType.Cube, p + Vector3.up * 0.28f, new Vector3(0.05f, 0.56f, 0.05f), Quaternion.Euler(k % 2 * 4f - 2f, 0f, k % 3 * 3f - 3f), wood);
                }
                var mid = (a + b) * 0.5f;
                float len = (b - a).magnitude;
                var rot = Quaternion.LookRotation(b - a);
                Block(t, "Rail", PrimitiveType.Cube, mid + Vector3.up * 0.42f, new Vector3(0.04f, 0.04f, len), rot, wood);
                Block(t, "Rail", PrimitiveType.Cube, mid + Vector3.up * 0.2f, new Vector3(0.04f, 0.04f, len), rot, wood);
            }
            Block(t, "Feed", PrimitiveType.Cylinder, new Vector3(-0.5f, 0.05f, -0.2f), new Vector3(0.35f, 0.05f, 0.35f), Quaternion.identity, wood);
            Block(t, "Grain", PrimitiveType.Cylinder, new Vector3(-0.5f, 0.1f, -0.2f), new Vector3(0.3f, 0.01f, 0.3f), Quaternion.identity, straw);
            Block(t, "Hay", PrimitiveType.Sphere, new Vector3(1.55f, 0.15f, 0.9f), new Vector3(0.7f, 0.35f, 0.55f), Quaternion.identity, straw);

            // 닭 셋: 발밑이 원점, 앞이 +Z (InkVillageLife가 발밑을 축으로 숙여 쪼게 해요)
            var spots = new (Vector3 at, float yaw)[] { (new Vector3(-0.45f, 0f, -0.45f), 30f), (new Vector3(0.4f, 0f, -0.1f), -120f), (new Vector3(-0.1f, 0f, 0.35f), 200f) };
            var chickens = new List<Transform>();
            foreach (var (p, yaw) in spots)
            {
                var ch = new GameObject("Chicken (닭)").transform;
                ch.SetParent(t, false);
                ch.localPosition = p;
                ch.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Block(ch, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.18f, 0.28f), Quaternion.Euler(-10f, 0f, 0f), feather);
                Block(ch, "Tail", PrimitiveType.Cube, new Vector3(0f, 0.3f, -0.14f), new Vector3(0.04f, 0.13f, 0.09f), Quaternion.Euler(-30f, 0f, 0f), feather);
                Block(ch, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.34f, 0.13f), new Vector3(0.1f, 0.11f, 0.1f), Quaternion.identity, feather);
                Block(ch, "Comb", PrimitiveType.Cube, new Vector3(0f, 0.41f, 0.13f), new Vector3(0.02f, 0.05f, 0.07f), Quaternion.identity, comb);
                Block(ch, "Wattle", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0.17f), new Vector3(0.025f, 0.04f, 0.025f), Quaternion.identity, comb);
                Block(ch, "Beak", PrimitiveType.Cube, new Vector3(0f, 0.34f, 0.19f), new Vector3(0.03f, 0.025f, 0.05f), Quaternion.identity, beak);
                for (int s = -1; s <= 1; s += 2)
                    Block(ch, "Leg", PrimitiveType.Cube, new Vector3(s * 0.04f, 0.06f, 0f), new Vector3(0.015f, 0.12f, 0.015f), Quaternion.identity, beak);
                chickens.Add(ch);
            }
            return chickens.ToArray();
        }
    }
}
