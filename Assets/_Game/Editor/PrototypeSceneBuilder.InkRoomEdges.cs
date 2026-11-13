using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 중간·큰 층 방 가장자리 장식: 넓어진 방 바닥이 비어 보이지 않게, 벽 안쪽 띠(벽에서 1칸 남짓)에 지역마다 다른 소품을 둘러요.
    //   죽림: 잘린 대나무 그루터기·죽순, 마른 댓잎 더미 · 폐사찰: 깨진 석등(쓰러진 것도), 돌계단 조각, 부스러진 돌
    //   장강 수채: 밧줄 더미, 짐 상자, 술통 · 천마총: 부서진 마두 석상 조각, 해골, 찢긴 붉은 천
    // 문(벽 가운데) 앞은 비우고, 카메라 쪽(남쪽) 띠는 낮은 것만 둬요. 충돌 없음(보기용). InkRegionDressing이 지역에 맞는 것만 켜요.
    // 작은 방은 원래 크기라 그대로 둬요
    public static partial class PrototypeSceneBuilder
    {
        static void DressRoomEdges(List<GameObject> bamboo, List<GameObject> temple, List<GameObject> river, List<GameObject> tomb, Material stoneMat, Material woodMat, Material roofX, ref int seed)
        {
            var (_, _, _, bambooMat) = GetInkEnvMaterials();
            var dark = RiverMat("Ink_River_DarkWood", "#3A2D22", 0.35f);
            var rope = RiverMat("Ink_River_Rope", "#A88F62", 0.45f);
            var marble = GetInkMaterial("Ink_Tomb_Marble", "#1E1C1F", 0.2f);
            var red = GetInkMaterial("Ink_Tomb_Red", "#8E1A16", 0.8f);
            var bone = GetInkMaterial("Ink_Tomb_Bone", "#CFC6B0", 0.2f);
            var moss = GetInkMaterial("Ink_Temple_Moss", "#5E6B3A", 0.5f);

            foreach (var (room, center, size) in InkFloorRooms())
            {
                if (size.x <= CombatSize.x + 0.5f) continue;   // 작은 방은 그대로
                var roots = new[]
                {
                    new GameObject($"Edge Decor 죽림 ({room.name})").transform, new GameObject($"Edge Decor 폐사찰 ({room.name})").transform,
                    new GameObject($"Edge Decor 장강 ({room.name})").transform, new GameObject($"Edge Decor 천마총 ({room.name})").transform,
                };
                foreach (var r in roots) r.position = center;
                foreach (var (local, low) in EdgeSpots(size))
                {
                    float yaw = (seed * 47) % 360;
                    EdgeBamboo(Spot(roots[0], local, yaw), seed, low, bambooMat);
                    EdgeTemple(Spot(roots[1], local, yaw), seed, low, stoneMat, moss, roofX);
                    EdgeRiver(Spot(roots[2], local, yaw), seed, low, woodMat, dark, rope);
                    EdgeTomb(Spot(roots[3], local, yaw), seed, low, marble, bone, red);
                    seed++;
                }
                foreach (var r in roots) StripColliders(r);
                bamboo.Add(roots[0].gameObject);
                temple.Add(roots[1].gameObject);
                river.Add(roots[2].gameObject);
                tomb.Add(roots[3].gameObject);
                for (int i = 1; i < roots.Length; i++) roots[i].gameObject.SetActive(false);   // 처음(죽림)엔 꺼 둬요
            }
        }

        // 벽 안쪽 띠의 자리 (방 가운데 기준). 문 앞(벽 가운데)은 비우고, 남쪽(카메라 쪽)은 낮은 것만
        static List<(Vector3 local, bool low)> EdgeSpots(Vector2 size)
        {
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            const float inset = 1.1f, step = 3.4f, doorGap = 2.8f;
            var spots = new List<(Vector3, bool)>();
            for (float x = -hx + 1.6f; x <= hx - 1.6f; x += step)
            {
                if (Mathf.Abs(x) < doorGap) continue;
                spots.Add((new Vector3(x, 0f, hz - inset), false));
                spots.Add((new Vector3(x + step * 0.5f, 0f, -hz + inset * 0.8f), true));
            }
            for (float z = -hz + 2.2f; z <= hz - 2.2f; z += step)
            {
                if (Mathf.Abs(z) < doorGap) continue;
                spots.Add((new Vector3(-hx + inset, 0f, z), false));
                spots.Add((new Vector3(hx - inset, 0f, z), false));
            }
            return spots;
        }

        static Transform Spot(Transform root, Vector3 local, float yaw)
        {
            var t = new GameObject("Edge").transform;
            t.SetParent(root, false);
            t.localPosition = local;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        // 죽림: 잘린 그루터기 묶음(잎 없는 낮은 줄기)과 마른 댓잎 더미, 가끔 쓰러진 줄기
        static void EdgeBamboo(Transform t, int seed, bool low, Material bamboo)
        {
            BambooCluster(t, Vector3.zero, low ? 0.5f : 0.8f + (seed % 3) * 0.18f, seed, bamboo, 0f);
            BambooCluster(t, new Vector3(0.35f, 0f, -0.25f), low ? 0.35f : 0.55f, seed + 3, bamboo, 0f);   // 옆에 낮은 그루터기 묶음 하나 더
            FoliageCards(t, "Litter", seed % 2 == 0 ? "BambooLeaves_A" : "BambooLeaves_B", new Vector3(0.45f, 0.08f, 0.2f), 0.3f, 2, seed * 31f);
            if (!low && seed % 3 == 0)
            {
                var s = Stalk(t, new Vector3(-0.6f, 0.06f, -0.3f), 0.12f, 1.6f, 0f, bamboo);
                s.localRotation = Quaternion.Euler(0f, 20f, -90f);
            }
        }

        // 폐사찰: 깨진 석등 (받침·기둥·화사석·지붕돌, 셋째마다 쓰러져 있어요), 돌계단 조각, 이끼 낀 부스러기
        static void EdgeTemple(Transform t, int seed, bool low, Material stone, Material moss, Material roofX)
        {
            if (low || seed % 3 == 1)
            {
                // 돌계단 조각: 넓적한 판 둘을 비껴 쌓고 모서리가 깨졌어요
                Block(t, "Step", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.2f, 0.5f), Quaternion.Euler(0f, 0f, 2f), stone);
                Block(t, "Step", PrimitiveType.Cube, new Vector3(0.1f, 0.28f, 0.12f), new Vector3(0.8f, 0.16f, 0.36f), Quaternion.Euler(0f, 6f, -3f), stone);
                Block(t, "Chip", PrimitiveType.Cube, new Vector3(0.62f, 0.06f, -0.25f), Vector3.one * 0.14f, Quaternion.Euler(20f, 35f, 10f), stone);
                Block(t, "Moss", PrimitiveType.Sphere, new Vector3(-0.4f, 0.2f, 0.1f), new Vector3(0.35f, 0.08f, 0.25f), Quaternion.identity, moss);
                return;
            }
            bool fallen = seed % 3 == 2;
            var lantern = new GameObject("Stone Lantern (석등)").transform;
            lantern.SetParent(t, false);
            if (fallen) { lantern.localPosition = new Vector3(0f, 0.18f, 0f); lantern.localRotation = Quaternion.Euler(0f, 0f, 84f); }
            Block(lantern, "Base", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.2f, 0.5f), Quaternion.identity, stone);
            Block(lantern, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.22f, 0.3f, 0.22f), Quaternion.identity, stone);
            Block(lantern, "Light Box", PrimitiveType.Cube, new Vector3(0f, 0.95f, 0f), new Vector3(0.36f, 0.3f, 0.36f), Quaternion.identity, stone);
            Block(lantern, "Window", PrimitiveType.Cube, new Vector3(0f, 0.95f, -0.181f), new Vector3(0.16f, 0.16f, 0.01f), Quaternion.identity, roofX);
            var cap = new GameObject("Cap");
            cap.transform.SetParent(lantern, false);
            cap.transform.localPosition = new Vector3(0f, 1.18f, 0f);
            cap.transform.localRotation = Quaternion.Euler(0f, 0f, fallen ? 0f : 8f);   // 서 있는 것도 지붕돌이 비뚤어요
            cap.transform.localScale = new Vector3(0.62f, 0.2f, 0.62f);
            cap.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            cap.AddComponent<MeshRenderer>().sharedMaterial = roofX;
            Rock(t, "Rubble", new Vector3(0.5f, 0f, -0.35f), 0.22f, seed, seed * 37f, stone, false);
            Block(t, "Moss", PrimitiveType.Sphere, new Vector3(-0.3f, 0.04f, 0.25f), new Vector3(0.4f, 0.08f, 0.3f), Quaternion.identity, moss);
        }

        // 장강 수채: 밧줄 더미, 짐 상자 쌓음, 술통 (남쪽은 밧줄과 낮은 상자 하나만)
        static void EdgeRiver(Transform t, int seed, bool low, Material wood, Material dark, Material rope)
        {
            RopeCoil(t, new Vector3(0.55f, 0f, 0.1f), rope);
            if (low) { CrateStack(t, seed, wood, dark, new Vector3(-0.3f, 0f, -0.1f), 1); return; }
            if (seed % 2 == 0) CrateStack(t, seed, wood, dark, new Vector3(-0.2f, 0f, -0.1f), 2 + seed % 3);
            else Barrels(t, seed, dark, rope);
        }

        // 천마총: 부서진 마두 석상 (받침과 기운 몸통, 떨어진 뿔 난 머리), 해골, 찢긴 붉은 천 (남쪽은 머리·해골만)
        static void EdgeTomb(Transform t, int seed, bool low, Material marble, Material bone, Material red)
        {
            if (!low)
            {
                Block(t, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.6f, 0.3f, 0.6f), Quaternion.identity, marble);
                Block(t, "Torso Stump", PrimitiveType.Cube, new Vector3(0.02f, 0.55f, 0f), new Vector3(0.4f, 0.5f, 0.3f), Quaternion.Euler(4f, 10f, -9f), marble);
                Block(t, "Break", PrimitiveType.Cube, new Vector3(0.05f, 0.82f, 0f), new Vector3(0.42f, 0.04f, 0.32f), Quaternion.Euler(10f, 10f, 14f), red);
                Block(t, "Cloth", PrimitiveType.Cube, new Vector3(-0.22f, 0.45f, -0.16f), new Vector3(0.05f, 0.5f, 0.2f), Quaternion.Euler(0f, 20f, 8f), red);
            }
            // 떨어진 머리: 둥근 머리에 휜 뿔 둘
            var head = new Vector3(0.55f, 0.14f, -0.3f);
            Block(t, "Head", PrimitiveType.Sphere, head, new Vector3(0.3f, 0.26f, 0.3f), Quaternion.Euler(0f, 0f, 70f), marble);
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Horn", PrimitiveType.Capsule, head + new Vector3(0.08f, 0.1f, s * 0.12f), new Vector3(0.05f, 0.14f, 0.05f), Quaternion.Euler(s * 30f, 0f, -40f), marble);
            Block(t, "Skull", PrimitiveType.Sphere, new Vector3(-0.45f, 0.08f, 0.3f), new Vector3(0.17f, 0.15f, 0.19f), Quaternion.Euler(-15f, seed * 40f, 10f), bone);
            Block(t, "Bone", PrimitiveType.Capsule, new Vector3(-0.2f, 0.04f, 0.45f), new Vector3(0.04f, 0.2f, 0.04f), Quaternion.Euler(0f, seed * 53f, 88f), bone);
        }
    }
}
