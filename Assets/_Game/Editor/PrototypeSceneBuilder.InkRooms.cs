using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판의 방별 꾸미기: 녹림왕 방(불타는 죽림·화톳불·목책), 객잔(탁자·항아리·등불), 정비실(석등·바위),
    // 고수 결투장 둘, 그리고 방마다 벽 너머 대나무 숲. 모두 보기용이라 충돌체가 없어요 (게임 규칙·길막힘 그대로).
    public static partial class PrototypeSceneBuilder
    {
        static Material GetInkFireGroundMaterial() => GetShaderMaterial("Ink_FireGround", "AThousandDawns/InkFireGround");
        static Material GetInkFlameMaterial() => GetShaderMaterial("Ink_Flame", "AThousandDawns/InkFlame");

        static Material GetShaderMaterial(string name, string shaderName)
        {
            EnsureFolder(Env2DMaterialFolder);
            string path = $"{Env2DMaterialFolder}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null) mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void DressRoomsInk(Material stoneMat, Material woodMat, Material bambooMat, Material roofX)
        {
            var deco = new GameObject("Ink Deco (Rooms)").transform;
            int seed = 100;

            // 방마다 벽 너머 대나무 숲 (층 방은 DressRunInk에서 따로)
            (string name, Vector3 center, Vector2 size)[] rooms =
            {
                ("Rest", RestCenter, new Vector2(12f, 10f)), ("Inn", InnCenter, new Vector2(12f, 10f)),
                ("Master", MasterCenter, new Vector2(14f, 10f)), ("IronMaster", IronMasterCenter, new Vector2(14f, 10f)),
                ("Boss", BossCenter, new Vector2(16f, 12f)),
            };
            foreach (var (name, center, size) in rooms)
            {
                var ring = new GameObject($"Forest ({name})").transform;
                ring.SetParent(deco, false);
                ring.position = center;
                // 결투장·보스 방은 이미 대나무 울타리가 있어 답답하지 않게: 북쪽과 방 위쪽 절반 옆에만
                bool fenced = name == "Master" || name == "IronMaster" || name == "Boss";
                BuildEnv3DForest(ring, size.x * 0.5f + 0.5f, size.y * 0.5f + 0.3f, 1.75f, bambooMat, fenced ? 0f : float.NegativeInfinity);
            }

            DressBossRoom(deco, stoneMat, woodMat, ref seed);
            DressInn(deco, woodMat, roofX);
            DressRest(deco, stoneMat, roofX, ref seed);

            StripColliders(deco);
        }

        // 녹림왕 방: 북쪽 울타리 뒤 목책, 북쪽 모서리 화톳불 둘, 대나무 숲에 숨긴 불꽃 (불타는 죽림이 붙으면 번져요)
        static void DressBossRoom(Transform deco, Material stoneMat, Material woodMat, ref int seed)
        {
            var boss = new GameObject("Boss (녹림 산채)").transform;
            boss.SetParent(deco, false);
            float hx = 8f, hz = 6f;

            for (float x = -hx; x <= hx + 0.01f; x += 0.42f, seed++)
            {
                float h = 2.6f + (seed * 37 % 7) * 0.08f;
                var log = new GameObject("Log");
                log.transform.SetParent(boss, false);
                log.transform.position = BossCenter + new Vector3(x, 0f, hz + 1.1f);
                log.transform.localRotation = Quaternion.Euler(0f, seed * 47f, 0f);
                log.transform.localScale = new Vector3(0.38f, h, 0.38f);
                log.AddComponent<MeshFilter>().sharedMesh = GetSharpLogMesh();
                log.AddComponent<MeshRenderer>().sharedMaterial = woodMat;
            }
            for (int s = -1; s <= 1; s += 2) Brazier(boss, BossCenter + new Vector3(s * 7.1f, 0f, hz - 0.9f), stoneMat, seed++);

            // 불타는 대나무: 녹림왕 방 울타리 대나무(Room_Boss 아래 줄기 묶음)마다 밑동 불꽃, 몇 곳은 중간에도
            var bossRoom = GameObject.Find("Room_Boss");
            if (bossRoom == null) return;
            var flames = new List<GameObject>();
            foreach (Transform cluster in bossRoom.transform)
            {
                if (cluster.name != "Bamboo") continue;
                var f = FlameCards(boss, cluster.position + new Vector3(0.1f, 0f, 0.05f), 1.6f + (seed % 4) * 0.25f, 2, seed * 29f);
                f.SetActive(false);
                flames.Add(f);
                if (seed % 3 == 0)
                {
                    var mid = FlameCards(boss, cluster.position + new Vector3(-0.1f, 1.6f, 0f), 1.1f, 2, seed * 41f);
                    mid.SetActive(false);
                    flames.Add(mid);
                }
                seed++;
            }
            // 번지는 순서를 섞어요 (고정 시드라 씬을 다시 만들어도 같음)
            var rnd = new System.Random(7);
            var order = flames.OrderBy(_ => rnd.Next()).ToArray();
            var fx = bossRoom.AddComponent<AThousandDawns.Art.InkBurningGroveFX>();
            var so = new SerializedObject(fx);
            var arr = so.FindProperty("flames");
            arr.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 객잔: 손님 탁자·긴 의자 셋, 구석 항아리, 입구 양옆 등불. 가운데 길(입구 → 북쪽 문)과 계산대 앞은 비워요
        static void DressInn(Transform deco, Material woodMat, Material roofX)
        {
            var inn = new GameObject("Inn (객잔)").transform;
            inn.SetParent(deco, false);
            Vector3[] tables = { new Vector3(3.2f, 0f, -1.2f), new Vector3(3.4f, 0f, 2.3f), new Vector3(-3.6f, 0f, -1.6f) };
            foreach (var t in tables)
            {
                var c = InnCenter + t;
                Block(inn, "Table", PrimitiveType.Cube, c + new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 0.1f, 0.9f), Quaternion.identity, woodMat);
                for (int lx = -1; lx <= 1; lx += 2)
                    for (int lz = -1; lz <= 1; lz += 2)
                        Block(inn, "Leg", PrimitiveType.Cube, c + new Vector3(lx * 0.6f, 0.33f, lz * 0.35f), new Vector3(0.09f, 0.66f, 0.09f), Quaternion.identity, woodMat);
                for (int s = -1; s <= 1; s += 2)
                    Block(inn, "Bench", PrimitiveType.Cube, c + new Vector3(0f, 0.22f, s * 0.8f), new Vector3(1.4f, 0.14f, 0.3f), Quaternion.identity, woodMat);
            }
            var jarMat = GetInkMaterial("Ink_Jar", "#4A3A2E", 0.35f);
            Vector3[] jars = { new Vector3(-5.2f, 0f, -3.9f), new Vector3(-4.5f, 0f, -4.1f), new Vector3(-5.1f, 0f, -3.1f) };
            for (int i = 0; i < jars.Length; i++)
            {
                float s = 0.55f + i % 2 * 0.15f;
                Block(inn, "Jar", PrimitiveType.Sphere, InnCenter + jars[i] + Vector3.up * s * 0.6f, new Vector3(s, s * 1.2f, s), Quaternion.identity, jarMat);
            }
            for (int s = -1; s <= 1; s += 2)
                Billboard(inn, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png", InnCenter + new Vector3(s * 2.4f, 0f, 5.6f), 3.2f, s > 0);
        }

        // 정비실: 석등 둘(불씨가 켜진), 구석 바위. 목인장 둘 자리는 비워요
        static void DressRest(Transform deco, Material stoneMat, Material roofX, ref int seed)
        {
            var rest = new GameObject("Rest (정비실)").transform;
            rest.SetParent(deco, false);
            for (int s = -1; s <= 1; s += 2) StoneLantern(rest, RestCenter + new Vector3(s * 4.6f, 0f, 3.4f), stoneMat, roofX);
            RockCluster(rest, rest.InverseTransformPoint(RestCenter + new Vector3(-4.8f, 0f, -3.2f)), 0.9f, seed++, stoneMat);
            RockCluster(rest, rest.InverseTransformPoint(RestCenter + new Vector3(4.7f, 0f, -3.4f)), 0.7f, seed++, stoneMat);
        }

        // 화톳불: 돌 화로 위에 늘 켜진 불꽃
        static void Brazier(Transform parent, Vector3 foot, Material stoneMat, int seed)
        {
            var group = new GameObject("Brazier (화톳불)").transform;
            group.SetParent(parent, false);
            group.position = foot;
            Rock(group, "Bowl", Vector3.zero, 0.9f, seed, seed * 37f, stoneMat, false);
            FlameCards(group.parent, foot + Vector3.up * 0.45f, 1.3f, 3, seed * 23f);
        }

        // 석등: 받침·기둥·화사석(불 켜진 칸)·기와 지붕돌
        static void StoneLantern(Transform parent, Vector3 foot, Material stoneMat, Material roofX)
        {
            var group = new GameObject("Stone Lantern (석등)").transform;
            group.SetParent(parent, false);
            group.position = foot;
            Block(group, "Base", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.8f, 0.24f, 0.8f), Quaternion.identity, stoneMat);
            Block(group, "Pillar", PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.28f, 0.5f, 0.28f), Quaternion.identity, stoneMat);
            Block(group, "Light", PrimitiveType.Cube, new Vector3(0f, 1.5f, 0f), new Vector3(0.55f, 0.5f, 0.55f), Quaternion.identity, stoneMat);
            var roof = new GameObject("Roof");
            roof.transform.SetParent(group, false);
            roof.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            roof.transform.localScale = new Vector3(0.95f, 0.35f, 0.95f);
            roof.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            roof.AddComponent<MeshRenderer>().sharedMaterial = roofX;
            FlameCards(group, group.TransformPoint(new Vector3(0f, 1.32f, -0.29f)), 0.36f, 1, 0f); // 화창으로 보이는 불씨 (FlameCards는 월드 위치를 받아요)
        }

        // 불꽃 카드 count장을 엇갈려 꽂아요. footWorld는 불꽃 밑동(월드), height는 불꽃 키
        static GameObject FlameCards(Transform parent, Vector3 footWorld, float height, int count, float yaw)
        {
            var mat = GetInkFlameMaterial();
            var group = new GameObject("Flame");
            group.transform.SetParent(parent, false);
            group.transform.position = footWorld;
            for (int k = 0; k < count; k++)
            {
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"Card_{k + 1}";
                Object.DestroyImmediate(card.GetComponent<Collider>());
                card.transform.SetParent(group.transform, false);
                card.transform.localPosition = Vector3.up * height * 0.5f;
                card.transform.localRotation = Quaternion.Euler(0f, yaw + k * 180f / count, 0f);
                card.transform.localScale = new Vector3(height * 0.7f, height, 1f);
                var r = card.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return group;
        }
    }
}
