using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 4지역 천마총 (RoomRun.Region == 3): 마교 총단 지하의 천마 무덤. InkRegionDressing의 네 번째 꾸밈이에요 (보이는 것만, 충돌은 원래 상자 그대로).
    //  - 벽 너머 대나무 숲 → 어둠 속 붉은 등불 기둥 · 해골 석상 · 마두(뿔 달린 마귀 머리) 석상 · 깨진 관 · 피로 쓴 비문 석판
    //  - 방 안 대나무 묶음 → 깨진 관 · 해골 석상 · 피 비문 · 쇠사슬 감긴 기둥
    //  - 방 바닥 가장자리 군데군데 피 웅덩이 (검붉은 원판 + 둘레 돌)
    //  - 고수 결투장(돌벽) → 마교 수련장: 검은 대리석 바닥 판석, 돌벽 대신 검은 대리석 벽과 붉은 휘장 · 걸린 쇠사슬, 무기 시렁
    //  - 보스 방 → 무덤 안쪽: 북쪽 계단 위 천마의 옥좌, 그 앞 제단(촛대·피 그릇), 양옆으로 늘어선 붉은 등불 기둥
    public static partial class PrototypeSceneBuilder
    {
        static List<GameObject> DressTombInk(Material stoneMat, Material roofX, ref int seed)
        {
            var tomb = new List<GameObject>();
            var marble = GetInkMaterial("Ink_Tomb_Marble", "#1E1C1F", 0.2f);     // 검은 대리석
            var red = GetInkMaterial("Ink_Tomb_Red", "#8E1A16", 0.8f);           // 붉은 휘장·등불
            var blood = GetInkMaterial("Ink_Tomb_Blood", "#4E0B0A", 0.7f);       // 마른 피
            var bone = GetInkMaterial("Ink_Tomb_Bone", "#CFC6B0", 0.2f);         // 해골
            var iron = GetInkMaterial("Ink_Tomb_Iron", "#2E3236", 0.1f);         // 쇠사슬

            bool Skip(Transform t)
            {
                for (var p = t; p != null; p = p.parent)
                    if (p.name.Contains("Village")) return true;
                return false;
            }

            // 1) 벽 너머 → 어둠 속 무덤 석물
            foreach (var forest in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name == "Forest (3D stalks)" && !Skip(t)).ToList())
            {
                var crypt = new GameObject("Tomb Crypt (등불·석상·관)").transform;
                crypt.SetParent(forest.parent, false);
                crypt.localPosition = forest.localPosition;
                crypt.localRotation = forest.localRotation;
                int i = 0;
                foreach (Transform cluster in forest)
                {
                    var p = cluster.localPosition;
                    switch (i++ % 6)
                    {
                        case 0: LanternPillar(crypt, p, seed++, stoneMat, red, roofX); break;
                        case 2: SkullStatue(crypt, p, seed++, stoneMat, bone); break;
                        case 3: BrokenCoffin(crypt, p, seed++, marble, bone); break;
                        case 4: BloodStele(crypt, p, seed++, stoneMat, blood, roofX); break;
                        default: DemonHead(crypt, p, seed++, stoneMat, red); break;
                    }
                }
                StripColliders(crypt);
                tomb.Add(crypt.gameObject);
            }

            // 2) 방 안 대나무 묶음 → 무덤 석물
            foreach (var group in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name == "Bamboo" && !Skip(t) && (t.parent == null || t.parent.name != "Forest (3D stalks)")).ToList())
            {
                var piece = new GameObject("Tomb Piece (천마총 석물)").transform;
                piece.SetParent(group.parent, false);
                piece.localPosition = group.localPosition;
                piece.localRotation = Quaternion.Euler(0f, seed * 53f % 360f, 0f);
                switch (seed % 4)
                {
                    case 0: BrokenCoffin(piece, Vector3.zero, seed, marble, bone); break;
                    case 1: SkullStatue(piece, Vector3.zero, seed, stoneMat, bone); break;
                    case 2: BloodStele(piece, Vector3.zero, seed, stoneMat, blood, roofX); break;
                    default: ChainedPillar(piece, Vector3.zero, stoneMat, iron); break;
                }
                seed++;
                StripColliders(piece);
                tomb.Add(piece.gameObject);
            }

            // 3) 층 방 바닥 가장자리 피 웅덩이
            var pools = new GameObject("Blood Pools (피 웅덩이)").transform;
            float hx = CombatSize.x * 0.5f, hz = CombatSize.y * 0.5f;
            Vector3[] spots = { new Vector3(-hx + 1.4f, 0f, hz - 1.2f), new Vector3(hx - 1.6f, 0f, -hz + 1.3f), new Vector3(hx - 1.2f, 0f, hz - 2.2f), new Vector3(-hx + 2.2f, 0f, -hz + 1.1f) };
            for (int k = 0; k < spots.Length; k++) BloodPool(pools, CombatCenter + spots[k], 0.9f + k % 2 * 0.4f, seed++, blood, stoneMat);
            StripColliders(pools);
            tomb.Add(pools.gameObject);

            // 4) 고수 결투장 → 마교 수련장
            var hall = TombTrainingHall(marble, red, iron, stoneMat, ref seed);
            StripColliders(hall);
            tomb.Add(hall.gameObject);
            TombMasterWalls(marble, red, iron, tomb);

            // 5) 보스 방 → 천마의 옥좌와 제단
            var throne = TombThroneRoom(marble, red, blood, bone, stoneMat, roofX, ref seed);
            StripColliders(throne);
            tomb.Add(throne.gameObject);

            foreach (var go in tomb) go.SetActive(false);
            return tomb;
        }

        // ── 석물

        // 붉은 등불 기둥: 각진 돌기둥 위에 붉은 종이 등불(기와 지붕 얹은 상자)
        static void LanternPillar(Transform parent, Vector3 foot, int seed, Material stone, Material red, Material roofX)
        {
            var t = new GameObject("Red Lantern Pillar (붉은 등불 기둥)").transform;
            t.SetParent(parent, false);
            t.localPosition = foot;
            t.localRotation = Quaternion.Euler(0f, seed * 37f % 90f, 0f);
            float h = 2.6f + seed % 3 * 0.3f;
            Block(t, "Base", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.7f, 0.3f, 0.7f), Quaternion.identity, stone);
            Block(t, "Shaft", PrimitiveType.Cube, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.36f, h, 0.36f), Quaternion.identity, stone);
            Block(t, "Lantern", PrimitiveType.Cube, new Vector3(0f, h + 0.3f, 0f), new Vector3(0.55f, 0.6f, 0.55f), Quaternion.identity, red);
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Frame", PrimitiveType.Cube, new Vector3(0f, h + 0.3f + s * 0.31f, 0f), new Vector3(0.62f, 0.05f, 0.62f), Quaternion.identity, stone);
            var cap = new GameObject("Cap");
            cap.transform.SetParent(t, false);
            cap.transform.localPosition = new Vector3(0f, h + 0.78f, 0f);
            cap.transform.localScale = new Vector3(0.95f, 0.32f, 0.95f);
            cap.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            cap.AddComponent<MeshRenderer>().sharedMaterial = roofX;
        }

        // 해골 석상: 받침 위 커다란 해골 (둥근 머리 · 눈구멍 · 코 · 이빨 턱)
        static void SkullStatue(Transform parent, Vector3 foot, int seed, Material stone, Material bone)
        {
            var t = new GameObject("Skull Statue (해골 석상)").transform;
            t.SetParent(parent, false);
            t.localPosition = foot;
            t.localRotation = Quaternion.Euler(0f, seed * 71f % 360f, 0f);
            Block(t, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), new Vector3(1.0f, 0.7f, 1.0f), Quaternion.identity, stone);
            Block(t, "Cranium", PrimitiveType.Sphere, new Vector3(0f, 1.3f, 0.05f), new Vector3(0.95f, 0.9f, 1.0f), Quaternion.identity, bone);
            Block(t, "Jaw", PrimitiveType.Cube, new Vector3(0f, 0.85f, -0.22f), new Vector3(0.55f, 0.28f, 0.45f), Quaternion.Euler(-10f, 0f, 0f), bone);
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Eye", PrimitiveType.Sphere, new Vector3(s * 0.2f, 1.3f, -0.4f), new Vector3(0.26f, 0.24f, 0.2f), Quaternion.identity, stone);   // 어두운 눈구멍
            Block(t, "Nose", PrimitiveType.Cube, new Vector3(0f, 1.12f, -0.45f), new Vector3(0.1f, 0.14f, 0.08f), Quaternion.Euler(0f, 45f, 0f), stone);
            for (int k = -2; k <= 2; k++)
                Block(t, "Tooth", PrimitiveType.Cube, new Vector3(k * 0.09f, 0.98f, -0.44f), new Vector3(0.06f, 0.1f, 0.05f), Quaternion.identity, bone);
        }

        // 마두 석상: 기둥 위 뿔 달린 마귀 머리, 붉게 칠한 눈
        static void DemonHead(Transform parent, Vector3 foot, int seed, Material stone, Material red)
        {
            var t = new GameObject("Demon Head Statue (마두 석상)").transform;
            t.SetParent(parent, false);
            t.localPosition = foot;
            t.localRotation = Quaternion.Euler(0f, seed * 53f % 360f, 0f);
            Block(t, "Column", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 0.8f, 0.6f), Quaternion.identity, stone);
            Block(t, "Head", PrimitiveType.Cube, new Vector3(0f, 2.0f, 0f), new Vector3(0.8f, 0.75f, 0.7f), Quaternion.Euler(0f, 0f, 0f), stone);
            Block(t, "Brow", PrimitiveType.Cube, new Vector3(0f, 2.18f, -0.33f), new Vector3(0.82f, 0.14f, 0.16f), Quaternion.Euler(-12f, 0f, 0f), stone);
            Block(t, "Snout", PrimitiveType.Cube, new Vector3(0f, 1.85f, -0.42f), new Vector3(0.4f, 0.3f, 0.3f), Quaternion.identity, stone);
            for (int s = -1; s <= 1; s += 2)
            {
                Block(t, "Horn", PrimitiveType.Cylinder, new Vector3(s * 0.42f, 2.55f, 0.05f), new Vector3(0.12f, 0.35f, 0.12f), Quaternion.Euler(0f, 0f, -s * 30f), stone);
                Block(t, "Horn Tip", PrimitiveType.Cylinder, new Vector3(s * 0.62f, 2.95f, 0.05f), new Vector3(0.07f, 0.2f, 0.07f), Quaternion.Euler(0f, 0f, -s * 55f), stone);
                Block(t, "Eye", PrimitiveType.Cube, new Vector3(s * 0.18f, 2.05f, -0.36f), new Vector3(0.14f, 0.07f, 0.04f), Quaternion.Euler(0f, 0f, s * 15f), red);
                Block(t, "Fang", PrimitiveType.Cube, new Vector3(s * 0.12f, 1.66f, -0.5f), new Vector3(0.06f, 0.16f, 0.06f), Quaternion.identity, stone);
            }
        }

        // 깨진 관: 검은 대리석 관, 뚜껑이 비껴 미끄러져 떨어졌고 안에서 뼈가 비죽
        static void BrokenCoffin(Transform parent, Vector3 foot, int seed, Material marble, Material bone)
        {
            var t = new GameObject("Broken Coffin (깨진 관)").transform;
            t.SetParent(parent, false);
            t.localPosition = foot;
            t.localRotation = Quaternion.Euler(0f, seed * 47f % 360f, 0f);
            Block(t, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(0.9f, 0.16f, 2.0f), Quaternion.identity, marble);
            for (int s = -1; s <= 1; s += 2)
            {
                Block(t, "Side", PrimitiveType.Cube, new Vector3(s * 0.42f, 0.4f, 0f), new Vector3(0.08f, 0.65f, 2.0f), Quaternion.identity, marble);
                Block(t, "End", PrimitiveType.Cube, new Vector3(0f, 0.4f, s * 0.98f), new Vector3(0.9f, 0.65f, 0.08f), Quaternion.identity, marble);
            }
            Block(t, "Lid (깨짐)", PrimitiveType.Cube, new Vector3(0.75f, 0.3f, 0.3f), new Vector3(1.0f, 0.1f, 1.4f), Quaternion.Euler(0f, 18f, 28f), marble);
            Block(t, "Lid Shard", PrimitiveType.Cube, new Vector3(-0.8f, 0.05f, -0.7f), new Vector3(0.5f, 0.08f, 0.5f), Quaternion.Euler(0f, 40f, 6f), marble);
            Block(t, "Skull", PrimitiveType.Sphere, new Vector3(0.05f, 0.65f, 0.6f), new Vector3(0.24f, 0.22f, 0.26f), Quaternion.identity, bone);
            Block(t, "Arm Bone", PrimitiveType.Cylinder, new Vector3(0.3f, 0.75f, 0.1f), new Vector3(0.05f, 0.3f, 0.05f), Quaternion.Euler(0f, 0f, -55f), bone);
        }

        // 피로 쓴 비문 석판: 돌 비석 위로 검붉은 글줄이 세로로 흘러내려요
        static void BloodStele(Transform parent, Vector3 foot, int seed, Material stone, Material blood, Material roofX)
        {
            var t = new GameObject("Blood Stele (피 비문)").transform;
            t.SetParent(parent, false);
            t.localPosition = foot;
            t.localRotation = Quaternion.Euler(0f, seed * 29f % 360f, 0f);
            Block(t, "Turtle Base", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(1.3f, 0.4f, 0.8f), Quaternion.identity, stone);
            Block(t, "Slab", PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), new Vector3(1.0f, 2.0f, 0.22f), Quaternion.Euler(0f, 0f, seed % 2 == 0 ? 3f : -2f), stone);
            for (int k = 0; k < 4; k++)   // 세로 글줄 넷 (길이가 제각각, 아래로 흘러내린 자국)
            {
                float len = 1.0f + (seed + k) % 3 * 0.25f;
                Block(t, "Blood Script", PrimitiveType.Cube, new Vector3(-0.3f + k * 0.2f, 2.2f - len * 0.5f, -0.115f), new Vector3(0.07f, len, 0.01f), Quaternion.identity, blood);
            }
            var cap = new GameObject("Cap");
            cap.transform.SetParent(t, false);
            cap.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            cap.transform.localScale = new Vector3(1.3f, 0.35f, 0.5f);
            cap.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            cap.AddComponent<MeshRenderer>().sharedMaterial = roofX;
        }

        // 쇠사슬 감긴 기둥
        static void ChainedPillar(Transform parent, Vector3 foot, Material stone, Material iron)
        {
            var t = new GameObject("Chained Pillar (쇠사슬 기둥)").transform;
            t.SetParent(parent, false);
            t.localPosition = foot;
            Block(t, "Pillar", PrimitiveType.Cylinder, new Vector3(0f, 1.3f, 0f), new Vector3(0.6f, 1.3f, 0.6f), Quaternion.identity, stone);
            var coil = new GameObject("Chain Wrap");
            coil.transform.SetParent(t, false);
            coil.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            coil.AddComponent<MeshFilter>().sharedMesh = GetRopeRingMesh(0.34f, 0.05f, 4, 0.35f);
            coil.AddComponent<MeshRenderer>().sharedMaterial = iron;
            Block(t, "Shackle", PrimitiveType.Cylinder, new Vector3(0.2f, 0.05f, -0.5f), new Vector3(0.2f, 0.04f, 0.2f), Quaternion.identity, iron);
        }

        // 피 웅덩이: 납작한 검붉은 원판 둘셋을 겹치고 가장자리에 돌 부스러기
        static void BloodPool(Transform parent, Vector3 at, float r, int seed, Material blood, Material stone)
        {
            var t = new GameObject("Blood Pool (피 웅덩이)").transform;
            t.SetParent(parent, false);
            t.position = at;
            Block(t, "Pool", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(r * 2f, 0.01f, r * 1.6f), Quaternion.Euler(0f, seed * 31f, 0f), blood);
            Block(t, "Pool Lobe", PrimitiveType.Cylinder, new Vector3(r * 0.6f, 0.014f, r * 0.4f), new Vector3(r, 0.01f, r * 0.8f), Quaternion.Euler(0f, seed * 17f, 0f), blood);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + seed * 23f) * Mathf.Deg2Rad;
                Block(t, "Rim Stone", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * r * 1.05f, 0.06f, Mathf.Sin(a) * r * 0.9f), Vector3.one * (0.14f + k % 2 * 0.06f), Quaternion.Euler(10f, k * 40f, 6f), stone);
            }
        }

        // ── 고수 결투장: 마교 수련장 (MasterSize, 가운데 IronMasterCenter)
        static Transform TombTrainingHall(Material marble, Material red, Material iron, Material stone, ref int seed)
        {
            var hall = new GameObject("Demon Cult Training Hall (마교 수련장)").transform;
            Vector3 c = IronMasterCenter;
            float hx = MasterSize.x * 0.5f, hz = MasterSize.y * 0.5f;
            // 검은 대리석 판석 (2칸 격자, 줄눈은 살짝 틈)
            for (float x = -hx + 1f; x < hx; x += 2f)
                for (float z = -hz + 1f; z < hz; z += 2f)
                    Block(hall, "Marble Tile", PrimitiveType.Cube, c + new Vector3(x, 0.015f + ((int)(x + z) & 1) * 0.004f, z), new Vector3(1.94f, 0.03f, 1.94f), Quaternion.identity, marble);
            // 가운데 붉은 마교 문양 (겹친 원판 + 십자)
            Block(hall, "Sigil Ring", PrimitiveType.Cylinder, c + new Vector3(0f, 0.04f, 0f), new Vector3(4.2f, 0.01f, 4.2f), Quaternion.identity, red);
            Block(hall, "Sigil Core", PrimitiveType.Cylinder, c + new Vector3(0f, 0.045f, 0f), new Vector3(3.6f, 0.01f, 3.6f), Quaternion.identity, marble);
            for (int k = 0; k < 4; k++)
                Block(hall, "Sigil Ray", PrimitiveType.Cube, c + Quaternion.Euler(0f, k * 45f, 0f) * new Vector3(0f, 0.05f, 0f), new Vector3(0.12f, 0.01f, 3.4f), Quaternion.Euler(0f, k * 45f, 0f), red);
            // 북쪽 벽가 무기 시렁 둘
            for (int s = -1; s <= 1; s += 2)
            {
                var rack = new GameObject("Weapon Rack (무기 시렁)").transform;
                rack.SetParent(hall, false);
                rack.position = c + new Vector3(s * hx * 0.45f, 0f, hz - 0.6f);
                for (int e = -1; e <= 1; e += 2)
                    Block(rack, "Post", PrimitiveType.Cube, new Vector3(e * 0.9f, 0.8f, 0f), new Vector3(0.12f, 1.6f, 0.12f), Quaternion.identity, marble);
                Block(rack, "Bar", PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), new Vector3(1.9f, 0.08f, 0.1f), Quaternion.identity, marble);
                for (int k = 0; k < 4; k++)   // 세워 둔 곡도·창
                    Block(rack, "Blade", PrimitiveType.Cube, new Vector3(-0.6f + k * 0.4f, 0.9f, -0.08f), new Vector3(0.05f, 1.5f, 0.03f), Quaternion.Euler(0f, 0f, (k % 2 == 0 ? 6f : -4f)), iron);
            }
            // 네 모서리 화로
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var foot = c + new Vector3(sx * (hx - 0.8f), 0f, sz * (hz - 0.8f));
                    Block(hall, "Brazier", PrimitiveType.Cylinder, foot + Vector3.up * 0.45f, new Vector3(0.6f, 0.45f, 0.6f), Quaternion.identity, iron);
                    Block(hall, "Embers", PrimitiveType.Cylinder, foot + Vector3.up * 0.92f, new Vector3(0.5f, 0.03f, 0.5f), Quaternion.identity, red);
                }
            seed += 4;
            return hall;
        }

        // 고수 결투장 돌벽 → 검은 대리석 벽 + 붉은 휘장 + 걸린 쇠사슬 (돌벽 몸통은 "죽림" 쪽, 충돌체는 원래 담)
        static void TombMasterWalls(Material marble, Material red, Material iron, List<GameObject> tomb)
        {
            var room = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Room_IronMaster");
            if (room == null) return;
            foreach (var wall in room.Cast<Transform>().Where(t => t.name.StartsWith("Stone_")).ToList())
            {
                Vector3 s = wall.lossyScale;
                bool alongX = s.x >= s.z;
                float len = alongX ? s.x : s.z, H = s.y, thick = alongX ? s.z : s.x;
                Vector3 axis = alongX ? Vector3.right : Vector3.forward, across = alongX ? Vector3.forward : Vector3.right;
                float inward = Vector3.Dot(IronMasterCenter - wall.position, across) >= 0f ? 1f : -1f;
                var g = new GameObject($"Marble Wall ({wall.name})").transform;
                g.SetParent(wall.parent, false);
                g.position = new Vector3(wall.position.x, wall.position.y - H * 0.5f, wall.position.z);
                Block(g, "Marble", PrimitiveType.Cube, Vector3.up * (H + 0.3f) * 0.5f, alongX ? new Vector3(len, H + 0.3f, thick) : new Vector3(thick, H + 0.3f, len), Quaternion.identity, marble);
                Block(g, "Coping", PrimitiveType.Cube, Vector3.up * (H + 0.35f), alongX ? new Vector3(len + 0.1f, 0.12f, thick + 0.2f) : new Vector3(thick + 0.2f, 0.12f, len + 0.1f), Quaternion.identity, marble);
                int n = Mathf.Max(2, Mathf.RoundToInt(len / 3.5f));
                for (int k = 0; k < n; k++)
                {
                    float t = -len * 0.5f + (k + 0.5f) * len / n;
                    var face = axis * t + across * inward * (thick * 0.5f + 0.02f);
                    // 붉은 휘장 (위에서 늘어진 천, 끝이 갈라짐)
                    Block(g, "Red Drape", PrimitiveType.Cube, face + Vector3.up * (H - 0.45f), alongX ? new Vector3(0.9f, 1.3f, 0.02f) : new Vector3(0.02f, 1.3f, 0.9f), Quaternion.identity, red);
                    Block(g, "Drape Rod", PrimitiveType.Cube, face + Vector3.up * (H + 0.2f), alongX ? new Vector3(1.1f, 0.06f, 0.06f) : new Vector3(0.06f, 0.06f, 1.1f), Quaternion.identity, iron);
                    // 휘장 사이에 늘어진 쇠사슬
                    if (k < n - 1)
                    {
                        float tc = t + len / n * 0.5f;
                        var chain = new GameObject("Hanging Chain");
                        chain.transform.SetParent(g, false);
                        chain.transform.localPosition = axis * tc + across * inward * (thick * 0.5f + 0.06f) + Vector3.up * (H - 0.2f);
                        chain.transform.localRotation = Quaternion.LookRotation(across * inward);
                        chain.AddComponent<MeshFilter>().sharedMesh = GetHangingChainMesh();
                        chain.AddComponent<MeshRenderer>().sharedMaterial = iron;
                    }
                }
                StripColliders(g);
                tomb.Add(g.gameObject);
            }
        }

        // 벽에 걸린 쇠사슬: 양 끝을 벽에 걸고 가운데가 처진 고리 엮음 (길이 1.4)
        static Mesh GetHangingChainMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int links = 12;
            for (int k = 0; k < links; k++)
            {
                float u0 = k / (float)links, u1 = (k + 1) / (float)links;
                Vector3 P(float u) => new Vector3(Mathf.Lerp(-0.7f, 0.7f, u), -0.55f * Mathf.Sin(u * Mathf.PI), 0f);
                Vector3 c = (P(u0) + P(u1)) * 0.5f, d = (P(u1) - P(u0)).normalized;
                var q = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 0f, k % 2 == 0 ? 0f : 90f);
                var link = new List<Vector3>();
                for (int j = 0; j <= 8; j++)
                {
                    float a = j * Mathf.PI * 2f / 8;
                    link.Add(c + q * new Vector3(Mathf.Cos(a) * 0.035f, 0f, Mathf.Sin(a) * 0.065f));
                }
                TubeInto(link, 0.014f, 0.014f, verts, tris);
            }
            return SaveRunPropMesh("Tomb_Hanging_Chain", verts, tris);
        }

        // ── 보스 방: 천마의 옥좌와 제단 (BossSize, 가운데 BossCenter)
        static Transform TombThroneRoom(Material marble, Material red, Material blood, Material bone, Material stone, Material roofX, ref int seed)
        {
            var room = new GameObject("Heavenly Demon Throne (천마의 옥좌)").transform;
            Vector3 c = BossCenter;
            float hx = BossSize.x * 0.5f, hz = BossSize.y * 0.5f;
            // 북쪽 벽 너머 계단 셋 위 옥좌 (높은 등받이 꼭대기에 마두 장식)
            var throne = new GameObject("Throne (옥좌)").transform;
            throne.SetParent(room, false);
            throne.position = c + new Vector3(0f, 0f, hz + 2.4f);
            for (int k = 0; k < 3; k++)
                Block(throne, "Step", PrimitiveType.Cube, new Vector3(0f, 0.2f + k * 0.4f, -1.2f + k * 0.5f), new Vector3(7f - k * 1.2f, 0.4f, 3.2f - k * 0.6f), Quaternion.identity, marble);
            Block(throne, "Seat", PrimitiveType.Cube, new Vector3(0f, 1.6f, 0.5f), new Vector3(1.8f, 0.35f, 1.2f), Quaternion.identity, marble);
            Block(throne, "Back", PrimitiveType.Cube, new Vector3(0f, 3.0f, 1.05f), new Vector3(2.0f, 2.8f, 0.3f), Quaternion.identity, marble);
            for (int s = -1; s <= 1; s += 2)
            {
                Block(throne, "Arm", PrimitiveType.Cube, new Vector3(s * 1.0f, 2.0f, 0.4f), new Vector3(0.25f, 0.5f, 1.3f), Quaternion.identity, marble);
                Block(throne, "Wing", PrimitiveType.Cube, new Vector3(s * 1.25f, 3.6f, 1.05f), new Vector3(0.5f, 1.8f, 0.2f), Quaternion.Euler(0f, 0f, s * -18f), marble);
            }
            Block(throne, "Red Cushion", PrimitiveType.Cube, new Vector3(0f, 1.8f, 0.45f), new Vector3(1.6f, 0.08f, 1.0f), Quaternion.identity, red);
            DemonHead(throne, new Vector3(0f, 2.3f, 1.2f), seed++, marble, red);
            // 옥좌 앞 제단: 검은 석대 위 촛대 넷과 피 그릇
            var altar = new GameObject("Altar (제단)").transform;
            altar.SetParent(room, false);
            altar.position = c + new Vector3(0f, 0f, hz - 1.6f);
            Block(altar, "Altar", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(2.6f, 0.9f, 1.0f), Quaternion.identity, marble);
            Block(altar, "Altar Cloth", PrimitiveType.Cube, new Vector3(0f, 0.6f, -0.51f), new Vector3(1.4f, 0.6f, 0.02f), Quaternion.identity, red);
            Block(altar, "Blood Bowl", PrimitiveType.Cylinder, new Vector3(0f, 1.0f, 0f), new Vector3(0.6f, 0.1f, 0.6f), Quaternion.identity, stone);
            Block(altar, "Blood", PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0f), new Vector3(0.5f, 0.01f, 0.5f), Quaternion.identity, blood);
            for (int k = 0; k < 4; k++)
            {
                float x = -1.1f + k * 0.73f;
                Block(altar, "Candle", PrimitiveType.Cylinder, new Vector3(x, 1.05f, 0.25f), new Vector3(0.1f, 0.15f, 0.1f), Quaternion.identity, bone);
                FlameCards(altar, altar.TransformPoint(new Vector3(x, 1.3f, 0.25f)), 0.2f, 1, seed++ * 17f);
            }
            Block(altar, "Skull Offering", PrimitiveType.Sphere, new Vector3(0.75f, 1.0f, -0.15f), new Vector3(0.24f, 0.22f, 0.26f), Quaternion.identity, bone);
            // 양옆 벽가로 늘어선 붉은 등불 기둥, 앞쪽 모서리 피 웅덩이
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                    LanternPillar(room, room.InverseTransformPoint(c + new Vector3(s * (hx - 0.9f), 0f, hz - 2f - k * hz * 0.6f)), seed++, stone, red, roofX);
            BloodPool(room, c + new Vector3(-hx + 2.4f, 0f, -hz + 1.6f), 1.2f, seed++, blood, stone);
            BloodPool(room, c + new Vector3(hx - 2.6f, 0f, -hz + 2.0f), 0.9f, seed++, blood, stone);
            return room;
        }
    }
}
