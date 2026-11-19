using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 판타지 지역 보스 방 "무너진 왕좌의 방" (Room_FantasyBoss: 오우거·데스나이트, 지역과 상관없이 같은 모습):
    //   북쪽 벽 앞 계단 위 부서진 큰 왕좌 · 해골과 녹슨 왕관 · 양옆 벽을 따라 줄지은 기둥(몇은 쓰러짐) · 깨진 붉은 카펫 · 바닥 차원 균열
    //   · 벽 위에 걸린 찢어진 긴 깃발("Banner N", 피벗은 위쪽 가장자리 — 아트 2가 흔들어요) · 무너진 천장 들보 조각
    //   · 북쪽 벽 너머 지붕 없이 뚫린 하늘: 보랏빛 차원 하늘과 큰 균열, 떠 있는 바위섬
    // 보스가 돌진·도약으로 방 안을 크게 움직이니 서 있는 장식은 벽 쪽 띠(1.6칸)에만 두고, 가운데엔 바닥에 깔린 카펫·균열뿐이에요.
    // 충돌은 원래 돌벽 상자만 (NoColliderParts "Fantasy Courtyard" 와 같은 규칙으로 "Fantasy Hall", 왕좌 이름에만 "Throne" — 아트 2가 그 뒤(+Z)에서 균열을 번쩍여요)
    public static partial class PrototypeSceneBuilder
    {
        static void DressFantasyBoss()
        {
            var room = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Room_FantasyBoss");
            if (room == null) return;   // 게임 코드(Run.cs)가 방을 만들기 전에는 건너뛰어요
            var floor = room.Cast<Transform>().FirstOrDefault(t => t.name == "Floor");
            if (floor == null) return;
            var c = new Vector3(floor.position.x, 0f, floor.position.z);
            float hx = (floor.lossyScale.x - 1f) * 0.5f, hz = (floor.lossyScale.z - 1f) * 0.5f;

            Material Fantasy(string name, string hex) => FantasyLook(GetInkMaterial(name, hex, 1f));
            var stone = GetInkTriplanar("Ink3D_CastleStone", "Tex_Stone", 1.2f, 2f);
            stone.SetColor("_BaseColor", new Color(0.72f, 0.64f, 0.5f, 1f));
            EditorUtility.SetDirty(stone);
            var dark = Fantasy("Ink_Rift_Runestone", "#565B63");
            var moss = Fantasy("Ink_Rift_Moss", "#4F8A3A");
            var red = Fantasy("Ink_Castle_Banner", "#A3202A");
            var gold = Fantasy("Ink_Castle_Gold", "#E0B040");
            var rust = Fantasy("Ink_Castle_RustGold", "#9A6A2A");
            var bone = Fantasy("Ink_Castle_Bone", "#E8DEC4");
            var carpet = Fantasy("Ink_Castle_Carpet", "#8E1A26");
            var wood = Fantasy("Ink_Rift_Wood", "#7A4E2E");
            var rift = GetShaderMaterial("Ink_Rift", "AThousandDawns/InkRift");

            var root = new GameObject("Fantasy Hall (왕좌의 방)").transform;
            root.SetParent(room, false);
            var banners = new GameObject("Fantasy Hall Banners (깃발)").transform;
            banners.SetParent(room, false);
            var rng = new System.Random(733);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int seed = 0;

            // 바닥: 이끼 낀 석판 (지역과 상관없이)
            var slabs = GetInkTriplanar("Ink3D_CastleSlabs", "Floor_Ground", 3f, 0f);
            slabs.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Env/Temple/Floor_Temple.png"));
            slabs.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.53f, 1f));
            EditorUtility.SetDirty(slabs);
            if (floor.GetComponent<Renderer>() is Renderer fr) fr.sharedMaterial = slabs;

            CastleRamparts(room, root, stone, moss, rng, ref seed);

            // 왕좌: 북쪽 벽 앞 세 단 계단 위, 등받이가 깨져 비스듬해요
            var throneFoot = c + new Vector3(0f, 0f, hz - 1.3f);
            for (int k = 0; k < 3; k++)
                Block(root, "Dais Step", PrimitiveType.Cube, throneFoot + new Vector3(0f, 0.1f + k * 0.2f, -0.35f * (2 - k)), new Vector3(4.4f - k * 0.8f, 0.2f, 1.2f - k * 0.2f + 0.7f * (2 - k)), Quaternion.identity, stone);
            var throne = At(root, "Broken Throne (부서진 왕좌)", throneFoot + new Vector3(0f, 0.6f, 0.1f), 0f);
            // 플레이테스트 10차: VARCO 등받이 높은 검은 쇠·나무 의자(위에 가시, 높이 2.6)를 왕좌로. 이름의 "Throne"은 그대로 (아트 2가 그 뒤에서 균열을 번쩍여요). 없으면 예전 상자 왕좌
            bool varcoThrone = VarcoProp(throne, "DarkChair", Vector3.zero, 0f, 1f, RoomPropFolder) != null;
            if (!varcoThrone)
            {
            Block(throne, "Seat", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 1.2f), Quaternion.identity, dark);
            Block(throne, "Back", PrimitiveType.Cube, new Vector3(0.1f, 1.6f, 0.55f), new Vector3(1.6f, 2.2f, 0.25f), Quaternion.Euler(0f, 0f, 6f), dark);
            Block(throne, "Back Chip", PrimitiveType.Cube, new Vector3(-0.55f, 2.55f, 0.55f), new Vector3(0.5f, 0.6f, 0.26f), Quaternion.Euler(0f, 0f, 35f), dark);   // 위 귀퉁이가 깨져 기울었어요
            foreach (float sx in new[] { -0.9f, 0.9f })
                Block(throne, "Armrest", PrimitiveType.Cube, new Vector3(sx, 0.9f, 0.05f), new Vector3(0.25f, 0.35f, 1.1f), Quaternion.identity, dark);
            Block(throne, "Cushion", PrimitiveType.Cube, new Vector3(0f, 0.74f, -0.05f), new Vector3(1.3f, 0.08f, 0.9f), Quaternion.Euler(3f, 0f, -4f), carpet);
            Block(throne, "Gold Trim", PrimitiveType.Cube, new Vector3(0.1f, 2.72f, 0.42f), new Vector3(1.2f, 0.12f, 0.06f), Quaternion.Euler(0f, 0f, 6f), gold);
            }
            // 해골과 녹슨 왕관이 계단 앞에 굴러 있어요
            var skull = At(root, "Skull (해골)", throneFoot + new Vector3(0.9f, 0.12f, -1.9f), 30f);
            Block(skull, "Cranium", PrimitiveType.Sphere, new Vector3(0f, 0.14f, 0f), new Vector3(0.3f, 0.26f, 0.32f), Quaternion.identity, bone);
            Block(skull, "Jaw", PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.1f), new Vector3(0.2f, 0.08f, 0.14f), Quaternion.identity, bone);
            foreach (float sx in new[] { -0.06f, 0.06f })
                Block(skull, "Socket", PrimitiveType.Sphere, new Vector3(sx, 0.16f, -0.14f), Vector3.one * 0.07f, Quaternion.identity, dark);
            var crown = At(root, "Rusted Crown (녹슨 왕관)", throneFoot + new Vector3(-0.7f, 0.05f, -2.3f), 0f);
            crown.localRotation = Quaternion.Euler(70f, 25f, 0f);   // 옆으로 쓰러져 굴렀어요
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad;
                Block(crown, "Band", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.18f, 0f, Mathf.Sin(a) * 0.18f), new Vector3(0.15f, 0.1f, 0.03f), Quaternion.Euler(0f, -k * 45f + 90f, 0f), rust);
                if (k % 2 == 0) Block(crown, "Spike", PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.18f, 0.1f, Mathf.Sin(a) * 0.18f), new Vector3(0.05f, 0.14f, 0.05f), Quaternion.Euler(0f, 0f, 45f), rust);
            }
            for (int k = 0; k < 4; k++)
                Rock(root, "Bones", throneFoot + new Vector3(R(-1.8f, 1.8f), 0f, R(-2.4f, -1.6f)), 0.1f, seed++, R(0f, 360f), bone, false);

            // 깨진 붉은 카펫: 남쪽 문에서 왕좌까지, 군데군데 찢겨 끊겨요 (바닥에 깔려서 보스 길을 막지 않아요)
            float zFrom = -hz + 1f, zTo = hz - 3.2f;
            int pieces = 7;
            for (int k = 0; k < pieces; k++)
            {
                float z0 = Mathf.Lerp(zFrom, zTo, k / (float)pieces), z1 = Mathf.Lerp(zFrom, zTo, (k + 1) / (float)pieces);
                if (k == 2 || k == 5) continue;   // 찢겨 나간 자리
                // 플레이테스트 10차: VARCO 금테 두른 짙은 붉은 카펫 한 장을 문에서 왕좌 앞까지 길게 깔아요 (조각마다 금테가 둘러져 따로 놓인 깔개처럼 보여서). 없으면 예전 상자 카펫
                if (k == 0 && VarcoProp(root, "RedCarpet", c + new Vector3(0f, 0.005f, (zFrom + zTo) * 0.5f), 0f, 1f, RoomPropFolder) is Transform rug)
                {
                    var rs = rug.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size;   // 메시 축: X 너비 · Y 길이(누운 카펫) · Z 두께
                    rug.localScale = new Vector3(2.6f / Mathf.Max(0.01f, rs.x), (zTo - zFrom) / Mathf.Max(0.01f, rs.y), 0.04f / Mathf.Max(0.01f, rs.z));
                    break;
                }
                Block(root, "Carpet", PrimitiveType.Cube, c + new Vector3(R(-0.1f, 0.1f), 0.015f, (z0 + z1) * 0.5f), new Vector3(2.4f, 0.02f, (z1 - z0) * 0.92f), Quaternion.Euler(0f, R(-3f, 3f), 0f), carpet);
                Block(root, "Carpet Hem", PrimitiveType.Cube, c + new Vector3(1.22f, 0.02f, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.02f, (z1 - z0) * 0.9f), Quaternion.identity, gold);
                Block(root, "Carpet Hem", PrimitiveType.Cube, c + new Vector3(-1.22f, 0.02f, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.02f, (z1 - z0) * 0.9f), Quaternion.identity, gold);
            }
            for (int k = 0; k < 8; k++)   // 바닥 차원 균열
                GroundCrack(root, c + new Vector3(R(-hx + 2f, hx - 2f), 0f, R(-hz + 2f, hz - 3f)), R(0f, 180f), R(1.4f, 2.6f), rift);

            // 양옆 벽을 따라 줄지은 기둥 (벽에서 1칸 안쪽), 몇은 부러지고 둘은 쓰러졌어요
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 5; k++)
                {
                    var foot = c + new Vector3(side * (hx - 1f), 0f, -hz + 2.5f + k * (2f * hz - 5f) / 4f);
                    int kind = (k + (side > 0 ? 2 : 0)) % 5;
                    if (kind == 3)
                    {
                        // 쓰러진 기둥: 벽 쪽으로 누워 있어요 (가운데를 막지 않게)
                        var t = At(root, "Fallen Column", foot + new Vector3(0f, 0.32f, 0f), side > 0 ? 10f : -10f);
                        Block(t, "Shaft", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.65f, 1.5f, 0.65f), Quaternion.Euler(90f, 0f, 0f), stone);
                        continue;
                    }
                    float h = kind == 1 ? R(1.2f, 2f) : 4.2f;   // 부러진 기둥은 낮아요
                    Block(root, "Column Base", PrimitiveType.Cube, foot + new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.3f, 0.9f), Quaternion.identity, stone);
                    Block(root, "Column", PrimitiveType.Cylinder, foot + new Vector3(0f, 0.3f + h * 0.5f, 0f), new Vector3(0.6f, h * 0.5f, 0.6f), Quaternion.identity, stone);
                    if (kind != 1) Block(root, "Column Capital", PrimitiveType.Cube, foot + new Vector3(0f, 0.3f + h + 0.12f, 0f), new Vector3(0.85f, 0.24f, 0.85f), Quaternion.identity, stone);
                    else Rock(root, "Column Chunk", foot + new Vector3(side * -0.2f, 0f, 0.6f), 0.28f, seed++, R(0f, 360f), stone, false);
                }

            // 무너진 천장 들보: 양옆 벽 위에 걸쳐 있다가 부러져 비스듬히 (가운데 위로 넘어오지 않게 벽 쪽에만)
            for (int k = 0; k < 3; k++)
            {
                int side = k % 2 == 0 ? -1 : 1;
                var p = c + new Vector3(side * (hx - 0.4f), 3.6f, -hz + 4f + k * 4.5f);
                Block(root, "Roof Beam", PrimitiveType.Cube, p, new Vector3(2.4f, 0.3f, 0.35f), Quaternion.Euler(0f, 0f, side * -24f), wood);
            }

            // 찢어진 긴 깃발: 북쪽 벽 왕좌 양옆과 양옆 벽에 (피벗 = 위쪽 가장자리)
            var spots = new[]
            {
                (c + new Vector3(-3.3f, 3.6f, hz - 0.05f), 180f), (c + new Vector3(3.3f, 3.6f, hz - 0.05f), 180f),
                (c + new Vector3(-hx + 0.05f, 3f, -1f), 90f), (c + new Vector3(hx - 0.05f, 3f, -1f), -90f),
                (c + new Vector3(-hx + 0.05f, 3f, 4.5f), 90f), (c + new Vector3(hx - 0.05f, 3f, 4.5f), -90f),
            };
            int bi = 0;
            foreach (var (top, yaw) in spots)
            {
                Block(banners, "Pole (장대)", PrimitiveType.Cylinder, top + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.04f, 0.12f), new Vector3(0.06f, 0.6f, 0.06f), Quaternion.Euler(0f, yaw, 90f), wood);
                var flag = new GameObject($"Banner {++bi}").transform;
                flag.SetParent(banners, false);
                flag.position = top + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 0.12f);
                flag.rotation = Quaternion.Euler(0f, yaw, 0f);
                // 플레이테스트 10차: VARCO 해골·검 문장 붉은 깃발 (쇠 장대에 걸린, 높이 2.6) — 위쪽 가장자리가 피벗(flag)이라 그 아래로 매달아요. 없으면 예전 천
                if (VarcoProp(flag, "WallBanner", new Vector3(0f, -2.5f, 0f), 180f, 1f, RoomPropFolder) != null) continue;
                Block(flag, "Cloth", PrimitiveType.Cube, new Vector3(0f, -1.15f, 0f), new Vector3(1f, 2.3f, 0.02f), Quaternion.identity, red);
                Block(flag, "Torn Tail L", PrimitiveType.Cube, new Vector3(-0.3f, -2.42f, 0f), new Vector3(0.36f, 0.3f, 0.02f), Quaternion.Euler(0f, 0f, 14f), red);
                Block(flag, "Torn Tail R", PrimitiveType.Cube, new Vector3(0.28f, -2.38f, 0f), new Vector3(0.32f, 0.22f, 0.02f), Quaternion.Euler(0f, 0f, -16f), red);
                foreach (float z in new[] { -0.015f, 0.015f })
                    Block(flag, "Crest", PrimitiveType.Cube, new Vector3(0f, -0.9f, z), new Vector3(0.42f, 0.42f, 0.01f), Quaternion.Euler(0f, 0f, 45f), gold);
            }

            // 지붕 없이 뚫린 하늘: 북쪽 벽 너머 보랏빛 차원 하늘에 큰 균열과 떠 있는 바위섬
            var sky = At(root, "Rift Sky (뚫린 하늘)", c + new Vector3(0f, 0f, hz + 7f), 0f);
            var fantasySky = GetShaderMaterial("Ink_SkyFantasy", "Universal Render Pipeline/Unlit");
            fantasySky.SetColor("_BaseColor", new Color(0.32f, 0.2f, 0.5f, 1f));
            EditorUtility.SetDirty(fantasySky);
            Block(sky, "Sky", PrimitiveType.Quad, new Vector3(0f, 6f, 0f), new Vector3(30f, 12f, 1f), Quaternion.identity, fantasySky);
            Block(sky, "Great Rift", PrimitiveType.Quad, new Vector3(-2f, 6f, -0.1f), new Vector3(4f, 12.5f, 1f), Quaternion.Euler(0f, 0f, 8f), rift);
            Block(sky, "Small Rift", PrimitiveType.Quad, new Vector3(8f, 7f, -0.1f), new Vector3(2f, 6f, 1f), Quaternion.Euler(0f, 0f, -12f), rift);
            for (int k = 0; k < 8; k++)
                Rock(sky, "Floating Isle", new Vector3(R(-13f, 13f), R(3f, 11f), -0.4f), R(0.4f, 1f), seed++, R(0f, 360f), dark, false);

            StripColliders(root);
            StripColliders(banners);
            Debug.Log($"[채색] 무너진 왕좌의 방: 깃발 {bi}개");
        }

        // 서양 성벽: 방의 돌벽 상자(Stone_*)는 충돌체로 두고 그림만 끈 뒤(기와 지붕도 지워요), 총안(凹) 위턱이 있는 성벽 토막을 그 자리에 세워요.
        //   변마다 한 군데 무너져 낮고 돌무더기, 남쪽(카메라 쪽)은 낮게 위턱 없이. 성 안뜰·왕좌의 방이 같이 써요
        static void CastleRamparts(Transform room, Transform root, Material stone, Material moss, System.Random rng, ref int seed)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            foreach (var wall in room.Cast<Transform>().Where(t => t.name.StartsWith("Stone_")).ToList())
            {
                if (wall.GetComponent<Renderer>() is Renderer wr) wr.enabled = false;
                foreach (var cap in wall.Cast<Transform>().Where(t => t.name.StartsWith("RoofCap")).ToList()) Object.DestroyImmediate(cap.gameObject);
                var s = wall.lossyScale;
                bool alongX = s.x >= s.z;
                float len = alongX ? s.x : s.z, h = s.y, thick = alongX ? s.z : s.x;
                bool south = wall.name == "Stone_S";
                var w = new GameObject($"Rampart ({wall.name})").transform;
                w.SetParent(root, false);
                w.position = new Vector3(wall.position.x, 0f, wall.position.z);
                w.rotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
                float breach = (rng.NextDouble() < 0.5 ? -1f : 1f) * R(len * 0.18f, len * 0.36f);
                int blocks = Mathf.RoundToInt(len / 1.2f);
                for (int k = 0; k < blocks; k++)
                {
                    float x = -len * 0.5f + (k + 0.5f) * len / blocks;
                    float drop = Mathf.Abs(x - breach) < 1.3f ? R(0.45f, 0.75f) : 0f;
                    float bh = (south ? h * 0.65f : h) * (1f - drop);
                    Block(w, "Wall Block", PrimitiveType.Cube, new Vector3(x, bh * 0.5f, 0f), new Vector3(len / blocks + 0.02f, bh, thick + 0.1f), Quaternion.identity, stone);
                    if (drop == 0f && !south)
                        Block(w, "Merlon", PrimitiveType.Cube, new Vector3(x - len / blocks * 0.25f, bh + 0.25f, 0f), new Vector3(len / blocks * 0.45f, 0.5f, thick + 0.12f), Quaternion.identity, stone);
                    if (drop > 0f)
                        for (int r = 0; r < 3; r++)
                            Rock(w, "Rubble", new Vector3(x + R(-0.5f, 0.5f), 0f, R(-0.9f, 0.9f)), R(0.2f, 0.35f), seed++, R(0f, 360f), stone, false);
                }
                Block(w, "Wall Moss", PrimitiveType.Cube, new Vector3(R(-len * 0.3f, len * 0.3f), 0.1f, -thick * 0.5f - 0.03f), new Vector3(1.2f, 0.2f, 0.04f), Quaternion.identity, moss);
            }
        }
    }
}
