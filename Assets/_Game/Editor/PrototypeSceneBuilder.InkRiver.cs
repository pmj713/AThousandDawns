using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 3지역 장강 수채 (RoomRun.Region == 2): 강가 수적 소굴. InkRegionDressing의 세 번째 꾸밈이에요 (보이는 것만, 충돌은 원래 상자 그대로).
    //  - 벽 너머 대나무 숲 → 갈대숲, 군데군데 망루(찢어진 깃발)·반쯤 부서진 배·물가 말뚝
    //  - 방 안 대나무 묶음 → 쌓인 나무 상자·술통·밧줄 말뚝·말린 그물 건조대
    //  - 고수 결투장(돌벽) → 강 위 잔교 공터: 널빤지 바닥, 돌벽 대신 널판 울, 모서리 말뚝과 밧줄, 묶어 둔 거룻배
    //  - 보스 방 → 수채 본채 앞 선착장: 북쪽 담 너머 물 위 기둥에 올린 본채, 깃발 장대, 계류 말뚝, 정박한 배
    public static partial class PrototypeSceneBuilder
    {
        static Material RiverMat(string name, string hex, float sat) => GetInkMaterial(name, hex, sat);

        static List<GameObject> DressRiverInk(Material stoneMat, Material woodMat, Material roofX, ref int seed)
        {
            var river = new List<GameObject>();
            var dark = RiverMat("Ink_River_DarkWood", "#3A2D22", 0.35f);   // 물에 젖어 검은 나무
            var rope = RiverMat("Ink_River_Rope", "#A88F62", 0.45f);
            var reed = RiverMat("Ink_River_Reed", "#7C8452", 0.45f);
            var flag = RiverMat("Ink_River_Flag", "#7E2B22", 0.55f);       // 수적 깃발 (바랜 붉은 천)
            var net = RiverMat("Ink_River_Net", "#4B4A43", 0.2f);

            bool Skip(Transform t)
            {
                for (var p = t; p != null; p = p.parent)
                    if (p.name.Contains("Village")) return true;
                return false;
            }

            // 1) 벽 너머 숲 → 갈대숲 + 망루·부서진 배·물가 말뚝
            foreach (var forest in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name == "Forest (3D stalks)" && !Skip(t)).ToList())
            {
                var bank = new GameObject("River Bank (갈대·망루·배)").transform;
                bank.SetParent(forest.parent, false);
                bank.localPosition = forest.localPosition;
                bank.localRotation = forest.localRotation;
                int i = 0;
                foreach (Transform cluster in forest)
                {
                    var p = cluster.localPosition;
                    switch (i++ % 9)
                    {
                        case 3: Watchtower(bank, p, seed++, woodMat, dark, flag, roofX); break;
                        case 7: Boat(bank, p, seed++, dark, woodMat, true); break;
                        case 5: Pilings(bank, p, seed++, dark, rope); ReedClump(bank, p + new Vector3(0.5f, 0f, 0.4f), seed++, reed); break;
                        default: ReedClump(bank, p, seed++, reed); ReedClump(bank, p + new Vector3(0.6f, 0f, 0.5f), seed++, reed); break;
                    }
                }
                StripColliders(bank);
                river.Add(bank.gameObject);
            }

            // 2) 방 안 대나무 묶음 → 수채 짐더미
            foreach (var group in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name == "Bamboo" && !Skip(t) && (t.parent == null || t.parent.name != "Forest (3D stalks)")).ToList())
            {
                float height = group.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds.max.y).DefaultIfEmpty(0f).Max();
                var piece = new GameObject("River Clutter (수채 짐)").transform;
                piece.SetParent(group.parent, false);
                piece.localPosition = group.localPosition;
                piece.localRotation = Quaternion.Euler(0f, seed * 53f % 360f, 0f);
                switch (seed % 4)
                {
                    case 0: CrateStack(piece, seed, woodMat, dark); break;
                    case 1: Barrels(piece, seed, dark, rope); CrateStack(piece, seed + 1, woodMat, dark, new Vector3(0.7f, 0f, 0.3f), 1); break;
                    case 2: Bollard(piece, Vector3.zero, dark, rope); Bollard(piece, new Vector3(0.7f, 0f, -0.2f), dark, rope); RopeCoil(piece, new Vector3(-0.5f, 0f, 0.3f), rope); break;
                    default: if (height > 2f) NetRack(piece, seed, dark, net, rope); else CrateStack(piece, seed, woodMat, dark); break;
                }
                seed++;
                StripColliders(piece);
                river.Add(piece.gameObject);
            }

            // 3) 고수 결투장 → 강 위 잔교 공터
            var pier = RiverPierArena(woodMat, dark, rope, net, ref seed);
            StripColliders(pier);
            river.Add(pier.gameObject);
            RiverMasterWalls(woodMat, dark, rope, river);

            // 4) 보스 방 → 수채 본채 앞 선착장
            var dock = RiverBossDock(woodMat, dark, rope, flag, roofX, ref seed);
            StripColliders(dock);
            river.Add(dock.gameObject);

            foreach (var go in river) go.SetActive(false);
            return river;
        }

        // ── 강가 소품

        // 갈대 한 무더기 (줄기 + 끝 이삭). 메시 셋을 돌려 써요
        static void ReedClump(Transform parent, Vector3 foot, int seed, Material mat)
        {
            var go = new GameObject("Reeds (갈대)");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = foot;
            go.transform.localRotation = Quaternion.Euler(0f, seed * 71f % 360f, 0f);
            go.transform.localScale = Vector3.one * (0.85f + seed % 3 * 0.15f);
            go.AddComponent<MeshFilter>().sharedMesh = GetReedMesh(seed % 3);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Mesh GetReedMesh(int variant)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var rng = new System.Random(variant * 37 + 11);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int k = 0; k < 16; k++)
            {
                float ang = R(0f, 360f), dist = R(0f, 0.45f);
                var foot = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0f, dist);
                float h = R(1.4f, 2.4f);
                var lean = Quaternion.Euler(R(-12f, 12f), 0f, R(-12f, 12f));   // 바람에 저마다 기울어요
                BoxInto(verts, tris, foot + lean * new Vector3(0f, h * 0.5f, 0f), new Vector3(0.03f, h, 0.03f), lean);
                if (k % 2 == 0)   // 이삭
                    BoxInto(verts, tris, foot + lean * new Vector3(0f, h + 0.12f, 0f), new Vector3(0.06f, 0.26f, 0.06f), lean);
                if (k % 3 == 0)   // 처진 잎
                {
                    var leaf = lean * Quaternion.Euler(0f, R(0f, 360f), R(35f, 60f));
                    BoxInto(verts, tris, foot + lean * new Vector3(0f, h * 0.45f, 0f) + leaf * new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.6f, 0.01f), leaf);
                }
            }
            return SaveRunPropMesh($"River_Reeds_{variant}", verts, tris);
        }

        // 망루: 기둥 넷 위 판자 마루와 난간, 작은 지붕, 사다리, 장대 끝 찢어진 깃발
        static void Watchtower(Transform parent, Vector3 foot, int seed, Material wood, Material dark, Material flag, Material roofX)
        {
            var t = new GameObject("Watchtower (망루)").transform;
            t.SetParent(parent, false);
            t.localPosition = foot;
            t.localRotation = Quaternion.Euler(0f, seed * 41f % 360f, 0f);
            const float h = 3.2f, w = 1.4f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Block(t, "Post", PrimitiveType.Cylinder, new Vector3(sx * w * 0.5f, h * 0.5f, sz * w * 0.5f), new Vector3(0.16f, h * 0.5f + 0.4f, 0.16f), Quaternion.Euler(sz * 3f, 0f, -sx * 3f), dark);
            Block(t, "Deck", PrimitiveType.Cube, new Vector3(0f, h, 0f), new Vector3(w + 0.3f, 0.12f, w + 0.3f), Quaternion.identity, wood);
            for (int s = 0; s < 4; s++)
            {
                var q = Quaternion.Euler(0f, s * 90f, 0f);
                if (s == 1) continue;   // 한 면은 난간이 부서져 없어요
                Block(t, "Rail", PrimitiveType.Cube, q * new Vector3(0f, h + 0.5f, (w + 0.3f) * 0.5f), new Vector3(w + 0.3f, 0.07f, 0.07f), q, wood);
            }
            var roof = new GameObject("Roof");
            roof.transform.SetParent(t, false);
            roof.transform.localPosition = new Vector3(0f, h + 1.5f, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            roof.transform.localScale = new Vector3(w + 0.8f, 0.5f, w + 0.6f);
            roof.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            roof.AddComponent<MeshRenderer>().sharedMaterial = roofX;
            for (int sx = -1; sx <= 1; sx += 2)
                Block(t, "Roof Post", PrimitiveType.Cylinder, new Vector3(sx * w * 0.45f, h + 0.7f, 0f), new Vector3(0.08f, 0.65f, 0.08f), Quaternion.identity, dark);
            // 사다리 (앞면에 기대어)
            for (int s = -1; s <= 1; s += 2)
                Block(t, "Ladder", PrimitiveType.Cube, new Vector3(s * 0.22f, h * 0.5f, -w * 0.5f - 0.45f), new Vector3(0.06f, h + 0.2f, 0.06f), Quaternion.Euler(-12f, 0f, 0f), wood);
            for (int k = 1; k < 8; k++)
            {
                float y = k * h / 8f;
                Block(t, "Rung", PrimitiveType.Cube, new Vector3(0f, y, -w * 0.5f - 0.45f + (h * 0.5f - y) * Mathf.Tan(12f * Mathf.Deg2Rad)), new Vector3(0.48f, 0.04f, 0.05f), Quaternion.identity, wood);
            }
            // 깃발 장대와 찢어진 깃발
            Block(t, "Flag Pole", PrimitiveType.Cylinder, new Vector3(w * 0.5f, h + 1.4f, w * 0.5f), new Vector3(0.06f, 1.4f, 0.06f), Quaternion.identity, dark);
            TornFlag(t, new Vector3(w * 0.5f + 0.03f, h + 2.4f, w * 0.5f), 1.1f, 0.7f, seed, flag);
        }

        // 찢어진 깃발: 장대에 단 천. 끝이 들쭉날쭉 찢겼고 바람에 살짝 휘어요 (양면)
        static void TornFlag(Transform parent, Vector3 at, float length, float height, int seed, Material mat)
        {
            var go = new GameObject("Torn Flag (찢어진 깃발)");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, seed * 29f % 60f - 30f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = GetTornFlagMesh(length, height);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Mesh GetTornFlagMesh(float length, float height)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int n = 8;
            float[] tear = { 0f, 0f, 0f, 0.05f, 0.12f, 0.08f, 0.3f, 0.18f, 0.45f };   // 아래 가장자리가 끝으로 갈수록 찢겨 올라가요
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float x = u * length;
                float wave = 0.08f * Mathf.Sin(u * 5f) * u;
                float topY = -u * 0.08f;
                float bottomY = -height + tear[i] * height + (i % 2 == 1 && i > 3 ? 0.12f * height : 0f);   // 끝은 갈기갈기
                verts.Add(new Vector3(x, topY, wave));
                verts.Add(new Vector3(x, bottomY, wave));
            }
            int front = verts.Count;
            verts.AddRange(verts.ToList());   // 뒷면용 복제
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                a += front; b += front; c += front; d += front;
                tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c);
            }
            return SaveRunPropMesh($"River_Flag_{Mathf.RoundToInt(length * 10)}_{Mathf.RoundToInt(height * 10)}", verts, tris);
        }

        // 거룻배: 길쭉한 배 몸통. broken이면 한쪽 뱃전이 깨져 기울어 반쯤 가라앉았어요
        static void Boat(Transform parent, Vector3 foot, int seed, Material hullMat, Material plankMat, bool broken)
        {
            var b = new GameObject(broken ? "Wrecked Boat (부서진 배)" : "Boat (거룻배)").transform;
            b.SetParent(parent, false);
            b.localPosition = foot + (broken ? new Vector3(0f, -0.25f, 0f) : Vector3.zero);
            b.localRotation = Quaternion.Euler(broken ? 8f : 0f, seed * 37f % 360f, broken ? -14f : 0f);
            var hull = new GameObject("Hull");
            hull.transform.SetParent(b, false);
            hull.AddComponent<MeshFilter>().sharedMesh = GetBoatHullMesh(broken);
            hull.AddComponent<MeshRenderer>().sharedMaterial = hullMat;
            // 가로 좌판 셋 (부서진 배는 하나가 빠졌어요)
            for (int k = -1; k <= 1; k++)
            {
                if (broken && k == 1) continue;
                Block(b, "Thwart", PrimitiveType.Cube, new Vector3(0f, 0.42f, k * 0.9f), new Vector3(1.0f, 0.05f, 0.18f), Quaternion.identity, plankMat);
            }
            if (broken)
                Block(b, "Loose Plank", PrimitiveType.Cube, new Vector3(0.9f, 0.08f, 1.2f), new Vector3(0.18f, 0.04f, 1.1f), Quaternion.Euler(0f, 25f, 8f), plankMat);
            else
                Block(b, "Oar", PrimitiveType.Cube, new Vector3(0.35f, 0.48f, -0.2f), new Vector3(0.06f, 0.04f, 2.2f), Quaternion.Euler(0f, 12f, 6f), plankMat);
        }

        // 배 몸통: 길이 3.4, 폭 1.2, 높이 0.55. U자 단면을 이물·고물로 좁혀요. 안팎 양면
        static Mesh GetBoatHullMesh(bool broken)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int rings = 12, around = 9;   // 단면 점: 왼 뱃전 → 바닥 → 오른 뱃전
            for (int i = 0; i <= rings; i++)
            {
                float u = i / (float)rings;
                float z = Mathf.Lerp(-1.7f, 1.7f, u);
                float width = 0.6f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.04f, 0.96f, u)), 0.6f);
                float rise = 0.18f * Mathf.Pow(Mathf.Abs(u - 0.5f) * 2f, 3f);   // 이물·고물이 들려요
                for (int k = 0; k <= around - 1; k++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, k / (float)(around - 1));
                    float x = Mathf.Sin(a) * width;
                    float y = 0.55f - Mathf.Cos(a) * 0.5f + rise;
                    if (broken && k == around - 1 && i > 3 && i < 8) y -= 0.3f;   // 깨진 뱃전
                    verts.Add(new Vector3(x, y, z));
                }
            }
            int outer = verts.Count;
            verts.AddRange(verts.ToList());
            for (int i = 0; i < rings; i++)
                for (int k = 0; k < around - 1; k++)
                {
                    int a = i * around + k, b = a + 1, c = a + around, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c);
                    a += outer; b += outer; c += outer; d += outer;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                }
            return SaveRunPropMesh(broken ? "River_Boat_Broken" : "River_Boat", verts, tris);
        }

        // 물가 말뚝 몇 개 (기울고, 하나엔 밧줄)
        static void Pilings(Transform parent, Vector3 foot, int seed, Material dark, Material rope)
        {
            for (int k = 0; k < 3; k++)
            {
                var p = foot + new Vector3((k - 1) * 0.55f, 0f, (k % 2) * 0.35f);
                float h = 0.9f + (seed + k) % 3 * 0.35f;
                Block(parent, "Piling", PrimitiveType.Cylinder, p + Vector3.up * h * 0.5f, new Vector3(0.2f, h * 0.5f, 0.2f), Quaternion.Euler((k - 1) * 6f, 0f, (seed % 2 == 0 ? 5f : -5f)), dark);
            }
            Bollard(parent, foot + new Vector3(0.9f, 0f, -0.3f), dark, rope);
        }

        // 나무 상자 더미 (아래 둘셋, 위 하나). 상자마다 테두리 띠
        static void CrateStack(Transform parent, int seed, Material wood, Material dark, Vector3 offset = default, int count = 4)
        {
            Vector3[] spots = { new Vector3(-0.3f, 0f, 0f), new Vector3(0.32f, 0f, 0.05f), new Vector3(0f, 0f, 0.6f), new Vector3(-0.05f, 1f, 0.15f) };
            for (int k = 0; k < Mathf.Min(count, spots.Length); k++)
            {
                float s = k == 3 ? 0.5f : 0.58f;
                var c = offset + spots[k];
                var rot = Quaternion.Euler(0f, (seed * 17 + k * 23) % 30 - 15f, 0f);
                var crate = new GameObject("Crate");
                crate.transform.SetParent(parent, false);
                crate.transform.localPosition = new Vector3(c.x, (k == 3 ? 0.58f : 0f) + s * 0.5f, c.z);
                crate.transform.localRotation = rot;
                Block(crate.transform, "Box", PrimitiveType.Cube, Vector3.zero, Vector3.one * s, Quaternion.identity, wood);
                for (int e = -1; e <= 1; e += 2)
                    Block(crate.transform, "Band", PrimitiveType.Cube, new Vector3(0f, e * s * 0.42f, 0f), new Vector3(s + 0.02f, 0.05f, s + 0.02f), Quaternion.identity, dark);
            }
        }

        static void Barrels(Transform parent, int seed, Material dark, Material rope)
        {
            for (int k = 0; k < 2; k++)
            {
                var p = new Vector3(-0.35f + k * 0.62f, 0f, -0.45f);
                Block(parent, "Barrel", PrimitiveType.Cylinder, p + Vector3.up * 0.42f, new Vector3(0.5f, 0.42f, 0.5f), Quaternion.identity, dark);
                for (int e = -1; e <= 1; e += 2)
                    Block(parent, "Hoop", PrimitiveType.Cylinder, p + Vector3.up * (0.42f + e * 0.25f), new Vector3(0.54f, 0.03f, 0.54f), Quaternion.identity, rope);
            }
            Block(parent, "Barrel (누운)", PrimitiveType.Cylinder, new Vector3(0.1f, 0.25f, -1.05f), new Vector3(0.48f, 0.4f, 0.48f), Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, seed * 31f, 0f), dark);
        }

        // 밧줄 말뚝: 굵은 말뚝 + 감긴 밧줄
        static void Bollard(Transform parent, Vector3 foot, Material dark, Material rope)
        {
            Block(parent, "Bollard", PrimitiveType.Cylinder, foot + Vector3.up * 0.45f, new Vector3(0.32f, 0.45f, 0.32f), Quaternion.identity, dark);
            Block(parent, "Bollard Cap", PrimitiveType.Cylinder, foot + Vector3.up * 0.92f, new Vector3(0.4f, 0.04f, 0.4f), Quaternion.identity, dark);
            var coil = new GameObject("Rope Wrap");
            coil.transform.SetParent(parent, false);
            coil.transform.localPosition = foot + Vector3.up * 0.55f;
            coil.AddComponent<MeshFilter>().sharedMesh = GetRopeRingMesh(0.18f, 0.035f, 3, 0.09f);
            coil.AddComponent<MeshRenderer>().sharedMaterial = rope;
        }

        // 바닥에 사려 놓은 밧줄
        static void RopeCoil(Transform parent, Vector3 foot, Material rope)
        {
            var coil = new GameObject("Rope Coil");
            coil.transform.SetParent(parent, false);
            coil.transform.localPosition = foot + Vector3.up * 0.04f;
            coil.AddComponent<MeshFilter>().sharedMesh = GetRopeRingMesh(0.3f, 0.04f, 4, 0.06f);
            coil.AddComponent<MeshRenderer>().sharedMaterial = rope;
        }

        // 감긴 밧줄 (나선): turns 바퀴, 한 바퀴마다 rise만큼 올라가고 반지름이 조금씩 작아져요
        static Mesh GetRopeRingMesh(float radius, float thick, int turns, float rise)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var line = new List<Vector3>();
            int steps = turns * 16;
            for (int i = 0; i <= steps; i++)
            {
                float u = i / 16f;
                float a = u * Mathf.PI * 2f;
                float r = radius * (1f - 0.06f * u);
                line.Add(new Vector3(Mathf.Cos(a) * r, u * rise, Mathf.Sin(a) * r));
            }
            TubeInto(line, thick, thick, verts, tris);
            return SaveRunPropMesh($"River_Rope_{Mathf.RoundToInt(radius * 100)}_{turns}", verts, tris);
        }

        // 말린 그물 건조대: 기둥 둘에 가로대, 처진 그물(가는 줄 격자)
        static void NetRack(Transform parent, int seed, Material dark, Material net, Material rope)
        {
            const float w = 2.2f, h = 2f;
            for (int s = -1; s <= 1; s += 2)
                Block(parent, "Rack Post", PrimitiveType.Cylinder, new Vector3(s * w * 0.5f, h * 0.5f, 0f), new Vector3(0.12f, h * 0.5f, 0.12f), Quaternion.Euler(0f, 0f, s * 4f), dark);
            Block(parent, "Rack Pole", PrimitiveType.Cylinder, new Vector3(0f, h - 0.05f, 0f), new Vector3(0.07f, w * 0.55f, 0.07f), Quaternion.Euler(0f, 0f, 90f), dark);
            var go = new GameObject("Drying Net (그물)");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, h - 0.08f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = GetNetMesh(w * 0.95f, 1.5f);
            go.AddComponent<MeshRenderer>().sharedMaterial = net;
            // 그물 아래 끝에 매단 찌·추
            for (int k = 0; k < 4; k++)
                Block(parent, "Float", PrimitiveType.Sphere, new Vector3(-w * 0.35f + k * w * 0.23f, h - 1.55f + Mathf.Sin(k * 1.7f) * 0.05f, 0f), Vector3.one * 0.09f, Quaternion.identity, rope);
        }

        // 그물: 위에서 늘어진 세로줄과 가운데가 처진 가로줄 격자 (가는 상자들)
        static Mesh GetNetMesh(float width, float drop)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int cols = 11, rows = 7;
            float Sag(float x) => 0.25f * (1f - Mathf.Pow(2f * x / width, 2f));   // 가운데가 더 처져요
            for (int c = 0; c < cols; c++)
            {
                float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, c / (float)(cols - 1));
                float len = drop * 0.85f + Sag(x);
                BoxInto(verts, tris, new Vector3(x, -len * 0.5f, 0f), new Vector3(0.015f, len, 0.015f), Quaternion.identity);
            }
            for (int r = 1; r <= rows; r++)
            {
                float y = -r * drop / (rows + 1);
                const int seg = 10;
                for (int s = 0; s < seg; s++)
                {
                    float x0 = Mathf.Lerp(-width * 0.5f, width * 0.5f, s / (float)seg), x1 = Mathf.Lerp(-width * 0.5f, width * 0.5f, (s + 1) / (float)seg);
                    var p0 = new Vector3(x0, y - Sag(x0) * r / rows, 0f);
                    var p1 = new Vector3(x1, y - Sag(x1) * r / rows, 0f);
                    var d = p1 - p0;
                    BoxInto(verts, tris, (p0 + p1) * 0.5f, new Vector3(d.magnitude, 0.015f, 0.015f), Quaternion.FromToRotation(Vector3.right, d));
                }
            }
            return SaveRunPropMesh("River_Net", verts, tris);
        }

        // ── 고수 결투장: 강 위 잔교 공터 (MasterSize, 가운데 IronMasterCenter)
        static Transform RiverPierArena(Material wood, Material dark, Material rope, Material net, ref int seed)
        {
            var pier = new GameObject("River Pier Arena (잔교 공터)").transform;
            Vector3 c = IronMasterCenter;
            float hx = MasterSize.x * 0.5f, hz = MasterSize.y * 0.5f;
            // 널빤지 바닥: 동서로 길게 깐 널 (폭 0.32, 틈 0.03). 군데군데 높이·색이 달라요
            var rng = new System.Random(seed * 13 + 5);
            int planks = Mathf.CeilToInt(MasterSize.y / 0.35f);
            for (int k = 0; k < planks; k++)
            {
                float z = -hz + (k + 0.5f) * MasterSize.y / planks;
                float jitter = (float)rng.NextDouble() * 0.015f;
                Block(pier, "Plank", PrimitiveType.Cube, c + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.2f, 0.015f + jitter, z),
                    new Vector3(MasterSize.x + 0.4f, 0.03f, MasterSize.y / planks - 0.03f), Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 0.6f, 0f), k % 5 == 3 ? dark : wood);
            }
            // 널을 받치는 가로 장선이 군데군데 드러나요
            for (int s = -2; s <= 2; s++)
                Block(pier, "Joist", PrimitiveType.Cube, c + new Vector3(s * hx * 0.45f, 0.035f, 0f), new Vector3(0.12f, 0.02f, MasterSize.y), Quaternion.identity, dark);
            // 네 모서리 굵은 말뚝 + 밧줄 난간 (벽 안쪽)
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Bollard(pier, c + new Vector3(sx * (hx - 0.6f), 0f, sz * (hz - 0.6f)), dark, rope);
            // 북쪽 벽가: 묶어 둔 거룻배(벽 너머 물 위)와 그물 건조대, 짐더미
            Boat(pier, c + new Vector3(-hx * 0.45f, -0.2f, hz + 1.6f), seed++, dark, wood, false);
            var rack = new GameObject("Net Rack").transform;
            rack.SetParent(pier, false);
            rack.position = c + new Vector3(hx * 0.4f, 0f, hz - 0.7f);
            NetRack(rack, seed++, dark, net, rope);
            var crates = new GameObject("Crates").transform;
            crates.SetParent(pier, false);
            crates.position = c + new Vector3(-hx + 1.2f, 0f, -hz + 1.4f);
            CrateStack(crates, seed++, wood, dark);
            RopeCoil(pier, c + new Vector3(hx - 1.6f, 0f, -hz + 1.1f), rope);
            return pier;
        }

        // 고수 결투장 돌벽 → 널판 울: 돌벽의 "죽림" 몸통(Wall Body)과 "폐사찰" 무너진 담 자리에, 장강 수채는 세로 널판을 엮은 울을 세워요
        static void RiverMasterWalls(Material wood, Material dark, Material rope, List<GameObject> river)
        {
            var room = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Room_IronMaster");
            if (room == null) return;
            foreach (var wall in room.Cast<Transform>().Where(t => t.name.StartsWith("Stone_")).ToList())
            {
                Vector3 s = wall.lossyScale;
                bool alongX = s.x >= s.z;
                float len = alongX ? s.x : s.z, H = s.y;
                Vector3 axis = alongX ? Vector3.right : Vector3.forward;
                var g = new GameObject($"Plank Fence ({wall.name})").transform;
                g.SetParent(wall.parent, false);
                g.position = new Vector3(wall.position.x, wall.position.y - H * 0.5f, wall.position.z);
                int n = Mathf.CeilToInt(len / 0.3f);
                for (int k = 0; k < n; k++)
                {
                    float t = -len * 0.5f + (k + 0.5f) * len / n;
                    float h = H + 0.15f + ((k * 37) % 5) * 0.06f - (k % 11 == 4 ? 0.6f : 0f);   // 키가 들쭉날쭉, 군데군데 부러진 널
                    Block(g, "Plank", PrimitiveType.Cube, axis * t + Vector3.up * h * 0.5f, alongX ? new Vector3(len / n - 0.03f, h, 0.12f) : new Vector3(0.12f, h, len / n - 0.03f), Quaternion.identity, k % 6 == 2 ? dark : wood);
                }
                for (int r = 0; r < 2; r++)   // 가로대 둘
                    Block(g, "Rail", PrimitiveType.Cube, Vector3.up * (0.45f + r * 1.0f), alongX ? new Vector3(len, 0.1f, 0.2f) : new Vector3(0.2f, 0.1f, len), Quaternion.identity, dark);
                StripColliders(g);
                river.Add(g.gameObject);
            }
        }

        // ── 보스 방: 수채 본채 앞 선착장 (BossSize, 가운데 BossCenter)
        static Transform RiverBossDock(Material wood, Material dark, Material rope, Material flag, Material roofX, ref int seed)
        {
            var dock = new GameObject("River Boss Dock (수채 본채 선착장)").transform;
            Vector3 c = BossCenter;
            float hx = BossSize.x * 0.5f, hz = BossSize.y * 0.5f;

            // 북쪽 담 너머: 물 위 기둥에 올린 본채 (마루·벽·큰 지붕, 앞에 깃발 장대 둘)
            var hall = new GameObject("Main Hall (본채)").transform;
            hall.SetParent(dock, false);
            hall.position = c + new Vector3(0f, 0f, hz + 3.6f);
            for (int k = -4; k <= 4; k++)
                for (int r = 0; r < 2; r++)
                    Block(hall, "Stilt", PrimitiveType.Cylinder, new Vector3(k * 1.9f, 0.5f, -1.8f + r * 3.2f), new Vector3(0.28f, 0.75f, 0.28f), Quaternion.identity, dark);
            Block(hall, "Floor", PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(17f, 0.2f, 4.6f), Quaternion.identity, wood);
            Block(hall, "Wall", PrimitiveType.Cube, new Vector3(0f, 2.6f, 0.8f), new Vector3(15f, 2.6f, 0.25f), Quaternion.identity, dark);
            for (int k = -3; k <= 3; k++)
                Block(hall, "Column", PrimitiveType.Cylinder, new Vector3(k * 2.4f, 2.6f, -1.6f), new Vector3(0.3f, 1.4f, 0.3f), Quaternion.identity, dark);
            var roof = new GameObject("Roof");
            roof.transform.SetParent(hall, false);
            roof.transform.localPosition = new Vector3(0f, 4.6f, -0.2f);
            roof.transform.localScale = new Vector3(18.5f, 1.8f, 5.8f);
            roof.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            roof.AddComponent<MeshRenderer>().sharedMaterial = roofX;
            Block(hall, "Plaque", PrimitiveType.Cube, new Vector3(0f, 3.7f, -1.75f), new Vector3(2.2f, 0.6f, 0.08f), Quaternion.identity, wood);   // 현판
            for (int s = -1; s <= 1; s += 2)
            {
                Block(hall, "Banner Pole", PrimitiveType.Cylinder, new Vector3(s * 5.6f, 3.2f, -2.6f), new Vector3(0.12f, 3.2f, 0.12f), Quaternion.identity, dark);
                TornFlag(hall, new Vector3(s * 5.6f + 0.06f, 6.1f, -2.6f), 1.8f, 1.2f, seed++, flag);
            }
            // 본채에서 방으로 내려오는 계단 다리 (북쪽 담 가운데 문 자리 너머)
            for (int k = 0; k < 4; k++)
                Block(hall, "Step", PrimitiveType.Cube, new Vector3(0f, 1.05f - k * 0.26f, -2.6f - k * 0.45f), new Vector3(3.2f, 0.12f, 0.5f), Quaternion.identity, wood);

            // 방 안: 동·서 벽가 계류 말뚝과 짐더미, 북동쪽 모서리에 정박한 큰 배(벽 너머), 남쪽은 비워요
            for (int s = -1; s <= 1; s += 2)
            {
                Bollard(dock, c + new Vector3(s * (hx - 0.8f), 0f, hz - 1.2f), dark, rope);
                Bollard(dock, c + new Vector3(s * (hx - 0.8f), 0f, -1f), dark, rope);
                var crates = new GameObject("Crates").transform;
                crates.SetParent(dock, false);
                crates.position = c + new Vector3(s * (hx - 1.4f), 0f, hz * 0.45f);
                crates.rotation = Quaternion.Euler(0f, s * 20f, 0f);
                CrateStack(crates, seed++, wood, dark);
                RopeCoil(dock, c + new Vector3(s * (hx - 1.8f), 0f, hz - 0.9f), rope);
            }
            var bigBoat = new GameObject("Moored Boat").transform;
            bigBoat.SetParent(dock, false);
            bigBoat.position = c + new Vector3(hx + 2.4f, -0.1f, hz * 0.3f);
            bigBoat.localScale = Vector3.one * 1.6f;
            Boat(bigBoat, Vector3.zero, seed++, dark, wood, false);
            var wreck = new GameObject("Sunken Boat").transform;
            wreck.SetParent(dock, false);
            wreck.position = c + new Vector3(-hx - 2.6f, 0f, -hz * 0.2f);
            wreck.localScale = Vector3.one * 1.3f;
            Boat(wreck, Vector3.zero, seed++, dark, wood, true);
            return dock;
        }
    }
}
