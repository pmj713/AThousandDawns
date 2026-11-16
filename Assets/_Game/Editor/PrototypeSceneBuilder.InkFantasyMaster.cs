using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 판타지 고수 결투장 "무너진 성 안뜰" (Room_FantasyMaster, 지역과 상관없이 같은 모습):
    //   바닥: 이끼 낀 석판 (판 바닥 재질과 따로 — 지역 바닥이 바뀌어도 그대로) · 깨진 석판 조각 · 보랏빛 차원 균열
    //   둘레: 돌벽 상자(충돌체)는 그대로 두고 그림만 끈 뒤, 그 자리에 총안(凹) 위턱이 있는 사암 성벽을 세워요. 군데군데 무너져 돌무더기
    //     · 북쪽 가운데 부서진 고딕 아치 문 · 쓰러진 기둥 · 찢어진 붉은 깃발 (아트 2가 흔들어요: 이름에 "Banner", 피벗은 위쪽 가장자리)
    //   한쪽: 북동쪽 모서리 밖에 무너진 둥근 탑, 무너진 돌덩이가 벽 너머 안뜰로 굴러들어 왔어요
    //   하늘: 북쪽 벽 너머 반은 먹빛 무협 산수, 반은 보랏빛 판타지 하늘, 그 사이를 세로 차원 균열이 갈라요
    // 안쪽 장식은 충돌 없음 (NoColliderParts "Fantasy Courtyard"), 충돌은 원래 돌벽 상자만
    public static partial class PrototypeSceneBuilder
    {
        static void DressFantasyMaster()
        {
            var room = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Room_FantasyMaster");
            if (room == null) return;
            Material Fantasy(string name, string hex) => FantasyLook(GetInkMaterial(name, hex, 1f));
            // 성 돌: 결 있는 돌 그림(구조물 셰이더)에 모래빛 — 판의 화강암 돌담과 같은 그림, 색만 따로
            var sandstone = GetInkTriplanar("Ink3D_CastleStone", "Tex_Stone", 1.2f, 2f);
            sandstone.SetColor("_BaseColor", new Color(0.72f, 0.64f, 0.5f, 1f));
            EditorUtility.SetDirty(sandstone);
            var runestone = Fantasy("Ink_Rift_Runestone", "#565B63");
            var moss = Fantasy("Ink_Rift_Moss", "#4F8A3A");
            var bannerRed = Fantasy("Ink_Castle_Banner", "#A3202A");
            var bannerGold = Fantasy("Ink_Castle_Gold", "#E0B040");
            var wood = Fantasy("Ink_Rift_Wood", "#7A4E2E");
            var rift = GetShaderMaterial("Ink_Rift", "AThousandDawns/InkRift");

            var c = FantasyMasterCenter;
            float hx = MasterSize.x * 0.5f, hz = MasterSize.y * 0.5f;
            var root = new GameObject("Fantasy Courtyard (성 안뜰)").transform;
            root.SetParent(room, false);
            var banners = new GameObject("Fantasy Courtyard Banners (깃발)").transform;
            banners.SetParent(room, false);
            var rng = new System.Random(311);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int seed = 0;

            // 바닥: 이끼 낀 석판 그림, 지역과 상관없이
            var slabs = GetInkTriplanar("Ink3D_CastleSlabs", "Floor_Ground", 3f, 0f);
            slabs.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Env/Temple/Floor_Temple.png"));   // 이끼 낀 석판 (폐사찰 바닥 그림)
            slabs.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.53f, 1f));
            EditorUtility.SetDirty(slabs);
            foreach (Transform t in room)
                if (t.name == "Floor" && t.GetComponent<Renderer>() is Renderer fr) fr.sharedMaterial = slabs;
            for (int k = 0; k < 14; k++, seed++)   // 깨져 들뜬 석판 조각
            {
                var p = c + new Vector3(R(-hx + 1.5f, hx - 1.5f), 0.04f, R(-hz + 1.5f, hz - 1.5f));
                Block(root, "Broken Slab", PrimitiveType.Cube, p, new Vector3(R(0.5f, 0.9f), 0.08f, R(0.4f, 0.7f)), Quaternion.Euler(R(-6f, 6f), R(0f, 90f), R(-6f, 6f)), sandstone);
                if (k % 3 == 0) Block(root, "Moss", PrimitiveType.Sphere, p + new Vector3(0.3f, 0f, 0.2f), new Vector3(0.5f, 0.05f, 0.35f), Quaternion.identity, moss);
            }
            for (int k = 0; k < 7; k++, seed++)   // 보랏빛 차원 균열
                GroundCrack(root, c + new Vector3(R(-hx + 2f, hx - 2f), 0f, R(-hz + 2f, hz - 2f)), R(0f, 180f), R(1.2f, 2.4f), rift);

            // 둘레 성벽: 돌벽 그림은 끄고(기와 지붕도 지워요) 총안 위턱이 있는 성벽을 그 자리에 (PrototypeSceneBuilder.InkFantasyBoss.cs)
            CastleRamparts(room, root, sandstone, moss, rng, ref seed);

            // 북쪽 가운데 부서진 고딕 아치 문 (벽 안쪽에 붙여서)
            GothicArch(root, c + new Vector3(0f, 0f, hz - 0.5f), 0f, 4, sandstone);

            // 쓰러진 기둥 셋
            foreach (var (p, yaw) in new[] { (c + new Vector3(-hx + 2.5f, 0.3f, 2f), 20f), (c + new Vector3(hx - 3f, 0.3f, -2.5f), -35f), (c + new Vector3(-3f, 0.3f, hz - 2.2f), 80f) })
            {
                var t = At(root, "Fallen Pillar (쓰러진 기둥)", p, yaw);
                Block(t, "Shaft", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.6f, 1.3f, 0.6f), Quaternion.Euler(0f, 0f, 90f), sandstone);
                Block(t, "Capital", PrimitiveType.Cube, new Vector3(1.4f, 0.05f, 0f), new Vector3(0.4f, 0.75f, 0.8f), Quaternion.identity, sandstone);
                Rock(t, "Chip", new Vector3(-1.5f, -0.3f, 0.4f), 0.2f, seed++, 40f, sandstone, false);
            }

            // 찢어진 깃발: 피벗이 위쪽 가장자리 (아트 2가 그 축으로 흔들어요). 장대는 성벽 안쪽에 가로로
            var bannerSpots = new[]
            {
                (c + new Vector3(-4f, 2.6f, hz - 0.05f), 180f), (c + new Vector3(4f, 2.6f, hz - 0.05f), 180f),
                (c + new Vector3(-hx + 0.05f, 2.6f, 2f), 90f), (c + new Vector3(hx - 0.05f, 2.6f, -1.5f), -90f),
            };
            int bi = 0;
            foreach (var (top, yaw) in bannerSpots)
            {
                Block(banners, "Pole (장대)", PrimitiveType.Cylinder, top + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.04f, 0.12f), new Vector3(0.06f, 0.55f, 0.06f), Quaternion.Euler(0f, yaw, 90f), wood);
                var flag = new GameObject($"Banner {++bi}").transform;   // 이 자리가 위쪽 가장자리
                flag.SetParent(banners, false);
                flag.position = top + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 0.12f);
                flag.rotation = Quaternion.Euler(0f, yaw, 0f);
                Block(flag, "Cloth", PrimitiveType.Cube, new Vector3(0f, -0.75f, 0f), new Vector3(0.95f, 1.5f, 0.02f), Quaternion.identity, bannerRed);
                Block(flag, "Torn Tail L", PrimitiveType.Cube, new Vector3(-0.28f, -1.62f, 0f), new Vector3(0.36f, 0.3f, 0.02f), Quaternion.Euler(0f, 0f, 12f), bannerRed);
                Block(flag, "Torn Tail R", PrimitiveType.Cube, new Vector3(0.3f, -1.58f, 0f), new Vector3(0.3f, 0.2f, 0.02f), Quaternion.Euler(0f, 0f, -18f), bannerRed);
                Block(flag, "Crest", PrimitiveType.Cube, new Vector3(0f, -0.7f, -0.015f), new Vector3(0.4f, 0.5f, 0.01f), Quaternion.Euler(0f, 0f, 45f), bannerGold);
                Block(flag, "Crest Back", PrimitiveType.Cube, new Vector3(0f, -0.7f, 0.015f), new Vector3(0.4f, 0.5f, 0.01f), Quaternion.Euler(0f, 0f, 45f), bannerGold);
            }

            // 북동쪽 모서리 밖 무너진 탑: 둥근 몸통, 위가 비스듬히 깨지고 돌덩이가 벽 너머 안뜰로 굴러들어 왔어요
            var tower = At(root, "Ruined Tower (무너진 탑)", c + new Vector3(hx + 1.6f, 0f, hz + 1.6f), 0f);
            Block(tower, "Tower Base", PrimitiveType.Cylinder, new Vector3(0f, 2.2f, 0f), new Vector3(3.2f, 2.2f, 3.2f), Quaternion.identity, sandstone);
            Block(tower, "Tower Upper", PrimitiveType.Cylinder, new Vector3(0.2f, 4.9f, 0.1f), new Vector3(2.9f, 0.6f, 2.9f), Quaternion.Euler(0f, 0f, -14f), sandstone);
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 50f + 200f) * Mathf.Deg2Rad;   // 남서쪽(안뜰 쪽) 반은 무너져 없어요
                Block(tower, "Tower Merlon", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 1.35f, 5.7f - k * 0.08f, Mathf.Sin(a) * 1.35f), new Vector3(0.5f, 0.55f, 0.4f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), sandstone);
            }
            Block(tower, "Slit Window", PrimitiveType.Cube, new Vector3(-1.1f, 3f, -1.1f), new Vector3(0.18f, 0.8f, 0.05f), Quaternion.Euler(0f, 45f, 0f), runestone);
            for (int k = 0; k < 6; k++)   // 안뜰 모서리로 굴러들어 온 돌덩이
                Rock(root, "Tower Rubble", c + new Vector3(hx - R(0.8f, 2.6f), 0f, hz - R(0.8f, 2.4f)), R(0.25f, 0.5f), seed++, R(0f, 360f), sandstone, false);

            // 하늘: 북쪽 벽 너머, 반은 먹빛 무협 산수 · 반은 보랏빛 판타지 하늘, 가운데를 세로 균열이 갈라요
            var sky = At(root, "Split Sky (갈라진 하늘)", c + new Vector3(0f, 0f, hz + 7f), 0f);
            var inkSky = GetShaderMaterial("Ink_SkyWuxia", "Universal Render Pipeline/Unlit");
            inkSky.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Env2DArtFolder}/Source/Backdrop_Ink.png"));
            inkSky.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(inkSky);
            var fantasySky = GetShaderMaterial("Ink_SkyFantasy", "Universal Render Pipeline/Unlit");
            fantasySky.SetColor("_BaseColor", new Color(0.32f, 0.2f, 0.5f, 1f));
            EditorUtility.SetDirty(fantasySky);
            Block(sky, "Wuxia Half", PrimitiveType.Quad, new Vector3(-6f, 5f, 0f), new Vector3(12f, 10f, 1f), Quaternion.identity, inkSky);
            Block(sky, "Fantasy Half", PrimitiveType.Quad, new Vector3(6f, 5f, 0f), new Vector3(12f, 10f, 1f), Quaternion.identity, fantasySky);
            Block(sky, "Sky Rift", PrimitiveType.Quad, new Vector3(0f, 5f, -0.1f), new Vector3(2.4f, 10.5f, 1f), Quaternion.identity, rift);
            for (int k = 0; k < 6; k++)   // 판타지 쪽 하늘에 떠 있는 바위섬
                Rock(sky, "Floating Isle", new Vector3(R(2f, 11f), R(3f, 9f), -0.4f), R(0.4f, 0.9f), seed++, R(0f, 360f), runestone, false);

            StripColliders(root);
            StripColliders(banners);
            Debug.Log($"[채색] 무너진 성 안뜰: 깃발 {bi}개");
        }
    }
}
