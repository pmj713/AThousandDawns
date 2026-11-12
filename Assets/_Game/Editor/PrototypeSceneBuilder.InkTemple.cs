using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판의 폐사찰 꾸밈: 죽림 꾸밈(대나무 묶음·벽 너머 대나무 숲)과 같은 자리에 무너진 절을 세우고,
    // InkRegionDressing이 지역(RoomRun.Region)에 맞는 쪽만 켜요. 충돌체는 원래 빌더의 상자 그대로예요 (여기서 만든 건 보기용).
    //   - 대나무 기둥·울타리 → 부서진 돌기둥 · 석탑 · 석등 · 비석 (키 큰 자리), 무너진 돌담 조각 (카메라 쪽 낮은 자리)
    //   - 벽 너머 대나무 숲 → 굽은 소나무 숲 사이사이 무너진 전각 지붕
    //   마을과 녹림왕 방(불타는 죽림 연출이 대나무에 붙어 있음)은 그대로 둬요.
    public static partial class PrototypeSceneBuilder
    {
        static void DressTempleInk(Material floorMat, Material stoneMat, Material woodMat, Material roofX)
        {
            var bamboo = new List<GameObject>();
            var temple = new List<GameObject>();
            int seed = 0;

            bool Skip(Transform t)
            {
                for (var p = t; p != null; p = p.parent)
                    if (p.name == "Village" || p.name.Contains("Village")) return true;
                return false;
            }

            // 1) 벽 너머 대나무 숲 → 소나무 숲 + 무너진 전각
            foreach (var forest in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name == "Forest (3D stalks)" && !Skip(t)).ToList())
            {
                var grove = new GameObject("Temple Grove (소나무·전각)").transform;
                grove.SetParent(forest.parent, false);
                grove.localPosition = forest.localPosition;
                grove.localRotation = forest.localRotation;
                int i = 0;
                foreach (Transform cluster in forest)
                {
                    if (i++ % 5 == 2) RuinedHall(grove, cluster.localPosition, seed++, stoneMat, woodMat, roofX);
                    else PineTree(grove, cluster.localPosition, 4.5f + (seed % 4) * 0.5f, seed++, woodMat);
                }
                StripColliders(grove);
                bamboo.Add(forest.gameObject);
                temple.Add(grove.gameObject);
            }

            // 2) 방 안 대나무 묶음 (기둥·울타리) → 무너진 절 소품
            foreach (var group in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.name == "Bamboo" && !Skip(t) && (t.parent == null || t.parent.name != "Forest (3D stalks)")).ToList())
            {
                float height = group.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds.max.y).DefaultIfEmpty(0f).Max();
                var piece = TempleObstacle(group.parent, group.localPosition, height, seed++, stoneMat, roofX);
                StripColliders(piece);
                bamboo.Add(group.gameObject);
                temple.Add(piece.gameObject);
            }

            // 3) 보스 방: 녹림 산채(목책·화톳불) 대신 무너진 대웅전 앞마당.
            //    불타는 죽림 불꽃(InkBurningGroveFX)은 보스 패턴이라 그대로 둬요 (폐사찰에선 무너진 절이 불타요)
            var camp = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Boss (녹림 산채)");
            if (camp != null)
            {
                foreach (Transform part in camp)
                    if (part.name != "Flame") bamboo.Add(part.gameObject);   // 목책·화톳불만 끄고 불꽃은 남겨요
                var court = BossCourtyard(camp.parent, stoneMat, woodMat, roofX, ref seed);
                StripColliders(court);
                temple.Add(court.gameObject);
            }

            // 4) 돌벽 결투장(죽림은 철권 마웅, 폐사찰은 파계승 혜광): 폐사찰에서만 절 안뜰 꾸밈을 켜요
            var cloister = MasterCloister(stoneMat, woodMat, roofX, ref seed);
            StripColliders(cloister);
            temple.Add(cloister.gameObject);

            // 5) 결투장 돌담: 폐사찰에선 담이 군데군데 무너지고 기와가 깨져 흘러내려요 (충돌체는 원래 담 그대로)
            RuinMasterWalls(bamboo, temple, ref seed);

            // 6) 3지역 장강 수채: 갈대숲·망루·배·수채 짐더미·잔교 공터·본채 선착장 (PrototypeSceneBuilder.InkRiver.cs)
            var river = DressRiverInk(stoneMat, woodMat, roofX, ref seed);

            var dressing = new GameObject("Ink Region Dressing").AddComponent<AThousandDawns.Art.InkRegionDressing>();
            dressing.SetRegions(new[]
            {
                new AThousandDawns.Art.InkRegionDressing.Set { name = "죽림", objects = bamboo.ToArray() },
                new AThousandDawns.Art.InkRegionDressing.Set { name = "폐사찰", objects = temple.ToArray() },
                new AThousandDawns.Art.InkRegionDressing.Set { name = "장강 수채", objects = river.ToArray() },
            });
            foreach (var go in temple) go.SetActive(false);   // 처음(죽림)엔 꺼 둬요

            DressTempleLook(floorMat, dressing);
        }

        // 결투장 돌담 네 변을 지역별로 바꿔 끼워요: 원래 담의 보이는 부분(+기와)은 "죽림" 쪽 자식으로 옮기고,
        // "폐사찰" 쪽엔 높이가 들쭉날쭉 무너진 담 토막·깨져 내려앉은 기와·바닥에 흩어진 돌덩이와 기왓장을 세워요.
        // 담 오브젝트 자체(충돌체)는 그대로 켜 둬요
        static void RuinMasterWalls(List<GameObject> bamboo, List<GameObject> temple, ref int seed)
        {
            var room = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Room_IronMaster");
            if (room == null) return;
            var (roofX, roofZ) = GetInkRoofMaterials();
            foreach (var wall in room.Cast<Transform>().Where(t => t.name.StartsWith("Stone_")).ToList())
            {
                var mr = wall.GetComponent<MeshRenderer>();
                var mf = wall.GetComponent<MeshFilter>();
                if (mr == null || mf == null) continue;
                var mat = mr.sharedMaterial;

                var body = new GameObject("Wall Body (죽림)");
                body.transform.SetParent(wall, false);
                body.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                body.AddComponent<MeshRenderer>().sharedMaterial = mat;
                foreach (var cap in wall.Cast<Transform>().Where(t => t.name.StartsWith("RoofCap")).ToList()) cap.SetParent(body.transform, false);
                Object.DestroyImmediate(mr);
                Object.DestroyImmediate(mf);
                bamboo.Add(body);

                var ruin = RuinWall(wall, mat, roofX, roofZ, seed++);
                StripColliders(ruin);
                temple.Add(ruin.gameObject);
            }
        }

        // 무너진 기와 돌담 (실제 폐사지·장마 피해 담처럼):
        //  - 담은 대부분 제 높이로 서 있고, 한두 곳만 빗물에 무너져 둥글게 패여요(가장자리는 돌이 들쭉날쭉).
        //  - 무너진 자리 양옆 기와는 받칠 데가 없어 무너진 쪽으로 처지고 기울며, 끝은 군데군데 비어요.
        //  - 멀쩡한 구간에도 기와가 떨어져 나간 자리가 있어 아래 흙(보토)이 드러나요. 떨어지다 담 벽에 걸린 기와도 몇 장.
        //  - 무너진 자리 아래엔 흙더미가 쌓이고 그 둘레로 돌과 기와 조각이 부채꼴로 흩어져요 (대부분 바깥쪽).
        static Transform RuinWall(Transform wall, Material mat, Material roofX, Material roofZ, int seed)
        {
            var rng = new System.Random(seed * 7919 + 17);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Vector3 s = wall.lossyScale;
            bool alongX = s.x >= s.z;
            float len = alongX ? s.x : s.z, thick = alongX ? s.z : s.x, H = s.y;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            Vector3 across = alongX ? Vector3.forward : Vector3.right;
            Vector3 Size(float l, float h, float t) => alongX ? new Vector3(l, h, t) : new Vector3(t, h, l);
            Quaternion Tilt(float deg) => Quaternion.AngleAxis(deg, across);   // 담 길이 방향으로 기울이기
            Quaternion Roll(float deg) => Quaternion.AngleAxis(deg, axis);     // 담 옆으로 미끄러지기
            var roofMat = alongX ? roofX : roofZ;
            var earth = GetInkMaterial("Ink_Temple_Earth", "#5E5142", 0.4f);   // 드러난 보토·흙더미

            var g = new GameObject($"Wall Ruin ({wall.name})").transform;
            g.SetParent(wall.parent, false);
            g.position = new Vector3(wall.position.x, wall.position.y - H * 0.5f, wall.position.z);
            float outward = Vector3.Dot(IronMasterCenter - wall.position, across) >= 0f ? -1f : 1f;

            // 무너진 자리 (가운데 c, 반폭 w, 깊이 d): 변마다 하나, 긴 변은 둘까지. 서로 떨어지게
            var breaches = new List<(float c, float w, float d)>();
            int want = len > 12f && rng.NextDouble() < 0.7 ? 2 : 1;
            for (int tries = 0; tries < 20 && breaches.Count < want; tries++)
            {
                float w = R(0.8f, 1.3f), c = R(-len * 0.5f + w + 1f, len * 0.5f - w - 1f);
                if (breaches.All(b => Mathf.Abs(b.c - c) > b.w + w + 2.5f)) breaches.Add((c, w, H * R(0.4f, 0.6f)));
            }
            float Drop(float t)
            {
                float best = 0f;
                foreach (var b in breaches)
                {
                    float x = Mathf.Abs(t - b.c);
                    if (x < b.w) best = Mathf.Max(best, b.d * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(Mathf.PI * x / b.w), 0.7f));
                }
                return best;
            }
            // 가장 가까운 무너진 자리 가장자리까지 거리와 그 방향(+1이면 +axis 쪽)
            (float dist, float side) Nearest(float t)
            {
                float dist = float.MaxValue, side = 1f;
                foreach (var b in breaches)
                {
                    float x = Mathf.Abs(t - b.c) - b.w;
                    if (x < dist) { dist = x; side = Mathf.Sign(b.c - t); }
                }
                return (dist, side);
            }

            void Cap(string name, Vector3 at, float l, float h, float wide, Quaternion rot, Material m)
            {
                var cap = new GameObject(name);
                cap.transform.SetParent(g, false);
                cap.transform.localPosition = at;
                cap.transform.localRotation = rot;
                cap.transform.localScale = Size(l, h, wide);
                cap.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(alongX);
                cap.AddComponent<MeshRenderer>().sharedMaterial = m;
            }

            // 1) 담: 제 높이인 구간은 한 덩이로, 무너진 자리는 가는 토막으로 둥글게 패이게 (가장자리 돌이 들쭉날쭉)
            int n = Mathf.CeilToInt(len / 0.22f);
            float slice = len / n;
            float runStart = -len * 0.5f;
            for (int i = 0; i <= n; i++)
            {
                float t = -len * 0.5f + (i + 0.5f) * slice;
                bool broken = i < n && Drop(t) > 0.01f;
                if (i == n || broken)
                {
                    float runEnd = -len * 0.5f + i * slice;
                    if (runEnd - runStart > 0.01f)
                        Block(g, "Wall", PrimitiveType.Cube, axis * ((runStart + runEnd) * 0.5f) + Vector3.up * H * 0.5f, Size(runEnd - runStart, H, thick), Quaternion.identity, mat);
                    runStart = runEnd + slice;
                }
                if (i < n && broken)
                {
                    float h = H - Drop(t) - R(0f, 0.12f);
                    Block(g, "Wall_Broken", PrimitiveType.Cube, axis * t + across * R(-0.03f, 0.03f) + Vector3.up * h * 0.5f,
                        Size(slice * 1.02f, h, thick * R(0.82f, 1f)), Quaternion.identity, mat);
                    if (rng.NextDouble() < 0.35)   // 빠지다 만 돌
                    {
                        float sz = R(0.12f, 0.22f);
                        Block(g, "Loose Stone", PrimitiveType.Cube, axis * t + Vector3.up * (h + sz * 0.3f), new Vector3(sz, sz * 0.7f, sz), Quaternion.Euler(R(-20f, 20f), R(0f, 90f), R(-20f, 20f)), mat);
                    }
                }
            }

            // 2) 기와: 0.5m 토막. 무너진 자리 옆은 처지고 그쪽으로 기울며 옆으로도 미끄러져요. 끝 토막은 군데군데 빠져요.
            //    멀쩡한 구간 두어 곳은 기와가 떨어져 나가 보토(흙)가 드러나요
            var patches = new List<float>();
            for (int tries = 0; tries < 20 && patches.Count < 2; tries++)
            {
                float p = R(-len * 0.5f + 0.8f, len * 0.5f - 0.8f);
                if (Nearest(p).dist > 1.8f && patches.All(q => Mathf.Abs(q - p) > 2f)) patches.Add(p);
            }
            float capWide = thick + RoofOverhang * 2f;
            const float tile = 0.5f;
            int caps = Mathf.CeilToInt(len / tile);
            float tileLen = len / caps;
            var hanging = new List<float>();
            for (int i = 0; i < caps; i++)
            {
                float t = -len * 0.5f + (i + 0.5f) * tileLen;
                var (dist, side) = Nearest(t);
                if (dist < tileLen * 0.5f) continue;                                   // 무너진 자리 위엔 기와가 없어요
                if (dist < 0.8f && rng.NextDouble() < 0.45) { hanging.Add(t); continue; } // 끝 쪽은 떨어져 나갔어요
                if (patches.Any(p => Mathf.Abs(p - t) < 0.4f))
                {
                    Block(g, "Bedding (보토)", PrimitiveType.Cube, axis * t + Vector3.up * (H + 0.06f), Size(tileLen, 0.12f, thick * 0.95f), Tilt(R(-3f, 3f)), earth);
                    if (rng.NextDouble() < 0.5)   // 한 장만 남아 비뚤어진 기와
                        Cap("Lone Tile", axis * (t + R(-0.1f, 0.1f)) + Vector3.up * (H + 0.16f), tileLen * 0.45f, 0.18f, capWide * 0.6f,
                            Roll(R(-15f, 15f)) * Quaternion.AngleAxis(R(-25f, 25f), Vector3.up), roofMat);
                    hanging.Add(t);
                    continue;
                }
                float k = dist < 1.6f ? Mathf.Pow(1f - dist / 1.6f, 1.6f) : 0f;            // 무너진 자리에 가까울수록 크게
                float sag = k * 0.35f;
                Cap($"RoofCap_{i + 1}", axis * t + across * (k * R(-0.08f, 0.08f)) + Vector3.up * (H + RoofCapHeight * 0.5f - sag),
                    tileLen + 0.03f, RoofCapHeight, capWide, Tilt(-side * k * 28f) * Roll(k * R(-10f, 10f)), roofMat);
            }

            // 3) 떨어지다 담 벽(주로 바깥)에 걸린 기와
            foreach (float t in hanging)
            {
                if (rng.NextDouble() < 0.4) continue;
                float face = rng.NextDouble() < 0.7 ? outward : -outward;
                float y = Mathf.Max(0.35f, H - Drop(t) - R(0.15f, 0.5f));
                Cap("Hanging Tile", axis * (t + R(-0.15f, 0.15f)) + across * face * (thick * 0.5f + 0.06f) + Vector3.up * y,
                    R(0.25f, 0.4f), 0.16f, 0.35f, Roll(face * R(55f, 80f)) * Tilt(R(-20f, 20f)), roofMat);
            }

            // 4) 무너진 자리 아래: 흙더미 + 부채꼴로 흩어진 돌·기와 조각 (바깥쪽이 크고 많아요)
            foreach (var b in breaches)
            {
                for (int sideK = 0; sideK < 2; sideK++)
                {
                    float face = sideK == 0 ? outward : -outward;
                    float big = sideK == 0 ? 1f : 0.55f;
                    Block(g, "Mound (흙더미)", PrimitiveType.Sphere, axis * b.c + across * face * (thick * 0.5f + 0.35f * big) + Vector3.up * 0.02f,
                        Size(b.w * 2.1f * big, b.d * 0.75f * big, 1.4f * big), Quaternion.identity, earth);
                    int stones = sideK == 0 ? 12 : 6;
                    for (int k = 0; k < stones; k++)
                    {
                        float u = (float)rng.NextDouble();
                        float reach = thick * 0.5f + 0.3f + u * u * 1.8f * big;               // 가까이 많고 멀리 드문드문
                        var at = axis * (b.c + R(-b.w * 1.2f, b.w * 1.2f)) + across * face * reach;
                        float sz = R(0.12f, 0.32f) * (1.2f - u * 0.5f);
                        Block(g, "Rubble", PrimitiveType.Cube, at + Vector3.up * sz * 0.3f, new Vector3(sz, sz * R(0.5f, 0.85f), sz * R(0.7f, 1.2f)),
                            Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f)), mat);
                    }
                    int shards = sideK == 0 ? 10 : 4;
                    for (int k = 0; k < shards; k++)
                    {
                        float u = (float)rng.NextDouble();
                        var at = axis * (b.c + R(-b.w * 1.6f, b.w * 1.6f)) + across * face * (thick * 0.5f + 0.2f + u * 2.2f * big);
                        Cap("Tile Shard", at + Vector3.up * 0.05f, R(0.18f, 0.32f), 0.1f, R(0.15f, 0.25f),
                            Quaternion.AngleAxis(R(0f, 360f), Vector3.up) * Roll(R(-25f, 25f)), roofMat);
                    }
                }
            }
            return g;
        }

        // 혜광의 절 안뜰 (돌벽 결투장 MasterSize, 가운데 IronMasterCenter): 북쪽 벽 앞 가운데 깨진 불단(빈 연화대·촛대·쓰러진 위패),
        // 네 모서리 석등, 동·서 벽가에 향로와 깨진 독경대, 북쪽 벽 위 빛바랜 번(幡) 세 폭. 가운데 싸움터는 비워요
        static Transform MasterCloister(Material stoneMat, Material woodMat, Material roofX, ref int seed)
        {
            var court = new GameObject("Master Cloister (혜광의 절 안뜰)").transform;
            Vector3 c = IronMasterCenter;
            float hx = MasterSize.x * 0.5f, hz = MasterSize.y * 0.5f;

            // 깨진 불단: 북쪽 벽 바로 앞 가운데 (불상은 사라지고 연화대만 남았어요)
            var altar = new GameObject("Broken Altar (불단)").transform;
            altar.SetParent(court, false);
            altar.position = c + new Vector3(0f, 0f, hz - 0.9f);
            Block(altar, "Dais", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(3.2f, 0.6f, 1.1f), Quaternion.identity, stoneMat);
            Block(altar, "Dais_Crack", PrimitiveType.Cube, new Vector3(1.3f, 0.62f, 0.1f), new Vector3(0.7f, 0.08f, 0.9f), Quaternion.Euler(0f, 0f, -9f), stoneMat);   // 한쪽 끝이 갈라져 내려앉았어요
            Block(altar, "Lotus", PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(1.0f, 0.15f, 1.0f), Quaternion.identity, stoneMat);   // 빈 연화대
            for (int s = -1; s <= 1; s += 2)
            {
                Block(altar, "Candle", PrimitiveType.Cylinder, new Vector3(s * 1.15f, 0.85f, -0.2f), new Vector3(0.12f, 0.25f, 0.12f), Quaternion.identity, woodMat);
                FlameCards(altar, altar.TransformPoint(new Vector3(s * 1.15f, 1.1f, -0.2f)), 0.25f, 1, seed++ * 31f);   // 아직 꺼지지 않은 촛불
            }
            Block(altar, "Tablet", PrimitiveType.Cube, new Vector3(-0.6f, 0.68f, -0.35f), new Vector3(0.35f, 0.06f, 0.6f), Quaternion.Euler(0f, 25f, 0f), woodMat);   // 쓰러진 위패

            // 네 모서리 석등
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var foot = c + new Vector3(sx * (hx - 0.8f), 0f, sz * (hz - 0.8f));
                    StoneLantern(court, foot, stoneMat, roofX);
                }

            // 동·서 벽가: 서쪽은 향로, 동쪽은 깨진 독경대
            var censer = new GameObject("Censer (향로)").transform;
            censer.SetParent(court, false);
            censer.position = c + new Vector3(-hx + 0.9f, 0f, 0.6f);
            Block(censer, "Bowl", PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0f), new Vector3(0.85f, 0.6f, 0.85f), Quaternion.identity, stoneMat);
            Block(censer, "Rim", PrimitiveType.Cylinder, new Vector3(0f, 0.78f, 0f), new Vector3(0.75f, 0.05f, 0.75f), Quaternion.identity, stoneMat);
            for (int k = 0; k < 3; k++)
            {
                var d = Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0f, 0f, 0.32f);
                Block(censer, $"Leg_{k + 1}", PrimitiveType.Cylinder, d + new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.15f, 0.12f), Quaternion.identity, stoneMat);
            }
            var lectern = new GameObject("Sutra Lectern (독경대)").transform;
            lectern.SetParent(court, false);
            lectern.position = c + new Vector3(hx - 0.9f, 0f, 0.4f);
            Block(lectern, "Top", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.08f, 0.55f), Quaternion.Euler(0f, 0f, 12f), woodMat);   // 한쪽 다리가 부러져 기울었어요
            Block(lectern, "Leg_A", PrimitiveType.Cube, new Vector3(-0.35f, 0.28f, 0f), new Vector3(0.08f, 0.55f, 0.45f), Quaternion.identity, woodMat);
            Block(lectern, "Leg_B", PrimitiveType.Cube, new Vector3(0.38f, 0.12f, 0f), new Vector3(0.08f, 0.22f, 0.45f), Quaternion.identity, woodMat);
            Block(lectern, "Sutra", PrimitiveType.Cube, new Vector3(0.6f, 0.03f, -0.4f), new Vector3(0.45f, 0.04f, 0.3f), Quaternion.Euler(0f, 30f, 0f), woodMat);   // 떨어진 경전

            // 북쪽 벽 위 빛바랜 번 세 폭 (벽 바로 뒤에 세운 장대에 매단 천)
            var bannerMat = GetInkMaterial("Ink_Temple_Banner", "#8C6B3A", 0.35f);   // 바랜 황토빛 천
            for (int k = -1; k <= 1; k++)
            {
                var pole = c + new Vector3(k * 4f, 0f, hz + 0.6f);
                Block(court, $"BannerPole_{k + 2}", PrimitiveType.Cylinder, pole + new Vector3(0f, 2.2f, 0f), new Vector3(0.1f, 2.2f, 0.1f), Quaternion.identity, woodMat);
                Block(court, $"Banner_{k + 2}", PrimitiveType.Cube, pole + new Vector3(0f, 3.1f, -0.08f), new Vector3(0.7f, 1.8f, 0.03f), Quaternion.Euler(0f, 0f, k * 3f), bannerMat);
            }
            seed += 4;
            return court;
        }

        // 무너진 대웅전 앞마당 (보스 방 BossSize, 가운데 BossCenter): 북쪽 담 너머 반쯤 무너진 대웅전 정면,
        // 북쪽 두 모서리에 넘어진 불상과 큰 향로, 동·서 벽가에 깨진 석등 두 쌍. 가운데 싸움터는 비워요
        static Transform BossCourtyard(Transform parent, Material stoneMat, Material woodMat, Material roofX, ref int seed)
        {
            var court = new GameObject("Boss Temple (대웅전 앞마당)").transform;
            court.SetParent(parent, true);   // 자리는 월드 기준 (BossCenter)
            court.position = Vector3.zero; court.rotation = Quaternion.identity;
            Vector3 c = BossCenter;
            float hx = BossSize.x * 0.5f, hz = BossSize.y * 0.5f;

            // 대웅전: 북쪽 담 바로 뒤 높은 기단 위에 기둥 다섯, 가운데가 내려앉은 큰 지붕 두 쪽, 앞에 떨어진 처마 조각
            var hall = new GameObject("Main Hall (대웅전)").transform;
            hall.SetParent(court, false);
            hall.position = c + new Vector3(0f, 0f, hz + 3.2f);
            Block(hall, "Platform", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(15f, 0.9f, 4.5f), Quaternion.identity, stoneMat);
            for (int k = 0; k < 5; k++)
            {
                float x = -6f + k * 3f;
                float h = k == 1 ? 2.2f : k == 3 ? 3.4f : 4.6f;      // 두 기둥은 부러졌어요
                Block(hall, $"Column_{k + 1}", PrimitiveType.Cylinder, new Vector3(x, 0.9f + h * 0.5f, -1.6f), new Vector3(0.45f, h * 0.5f, 0.45f), Quaternion.identity, woodMat);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                var roof = new GameObject($"Roof_{(s < 0 ? "W" : "E")}");
                roof.transform.SetParent(hall, false);
                roof.transform.localPosition = new Vector3(s * 3.8f, s < 0 ? 5.6f : 4.7f, -0.4f);
                roof.transform.localRotation = Quaternion.Euler(0f, 0f, s < 0 ? 4f : -16f);   // 동쪽이 내려앉았어요
                roof.transform.localScale = new Vector3(8.2f, 1.6f, 5.6f);
                roof.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
                roof.AddComponent<MeshRenderer>().sharedMaterial = roofX;
            }
            var eave = new GameObject("Fallen Eave");
            eave.transform.SetParent(hall, false);
            eave.transform.localPosition = new Vector3(4.2f, 0.4f, -2.8f);
            eave.transform.localRotation = Quaternion.Euler(-12f, 20f, 28f);
            eave.transform.localScale = new Vector3(3f, 0.8f, 1.6f);
            eave.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            eave.AddComponent<MeshRenderer>().sharedMaterial = roofX;
            for (int k = 0; k < 4; k++) Rock(hall, $"Rubble_{k + 1}", new Vector3(-5f + k * 3.4f, 0f, -2.6f - (k % 2) * 0.3f), 0.3f + (k % 3) * 0.12f, seed + k, k * 47f, stoneMat, false);

            // 넘어진 불상: 연꽃 받침 위에서 뒤로 넘어간 좌불 (몸·머리·무릎)
            var buddha = new GameObject("Fallen Buddha (넘어진 불상)").transform;
            buddha.SetParent(court, false);
            buddha.position = c + new Vector3(-hx + 1.4f, 0f, hz - 1.3f);
            buddha.rotation = Quaternion.Euler(0f, 25f, 0f);
            Block(buddha, "Pedestal", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.25f, 1.6f), Quaternion.identity, stoneMat);
            Block(buddha, "Lotus", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(1.3f, 0.12f, 1.3f), Quaternion.identity, stoneMat);
            Block(buddha, "Body", PrimitiveType.Capsule, new Vector3(0.1f, 0.95f, 0.75f), new Vector3(1.0f, 0.75f, 0.8f), Quaternion.Euler(70f, 0f, 0f), stoneMat);
            Block(buddha, "Knees", PrimitiveType.Capsule, new Vector3(0f, 0.85f, -0.05f), new Vector3(0.7f, 0.55f, 0.55f), Quaternion.Euler(0f, 0f, 90f), stoneMat);
            Block(buddha, "Head", PrimitiveType.Sphere, new Vector3(0.6f, 0.4f, 2.1f), new Vector3(0.65f, 0.7f, 0.65f), Quaternion.Euler(20f, 0f, 30f), stoneMat);   // 떨어져 굴러간 머리

            // 큰 향로: 세 발 솥에 뚜껑, 꺼진 재
            var censer = new GameObject("Censer (향로)").transform;
            censer.SetParent(court, false);
            censer.position = c + new Vector3(hx - 1.6f, 0f, hz - 1.4f);
            Block(censer, "Bowl", PrimitiveType.Sphere, new Vector3(0f, 0.75f, 0f), new Vector3(1.3f, 0.9f, 1.3f), Quaternion.identity, stoneMat);
            Block(censer, "Rim", PrimitiveType.Cylinder, new Vector3(0f, 1.15f, 0f), new Vector3(1.15f, 0.06f, 1.15f), Quaternion.identity, stoneMat);
            Block(censer, "Lid", PrimitiveType.Sphere, new Vector3(0f, 1.3f, 0f), new Vector3(0.8f, 0.4f, 0.8f), Quaternion.identity, stoneMat);
            for (int k = 0; k < 3; k++)
            {
                var d = Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0f, 0f, 0.5f);
                Block(censer, $"Leg_{k + 1}", PrimitiveType.Cylinder, d + new Vector3(0f, 0.2f, 0f), new Vector3(0.18f, 0.25f, 0.18f), Quaternion.identity, stoneMat);
            }

            // 동·서 벽가 석등 두 쌍 (하나씩은 넘어졌어요)
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 2; k++)
                {
                    var foot = c + new Vector3(s * (hx - 0.8f), 0f, k == 0 ? -1.8f : 2.2f);
                    var lantern = new GameObject("Lantern Spot").transform;
                    lantern.SetParent(court, false);
                    lantern.position = foot;
                    StoneLantern(lantern, foot, stoneMat, roofX);
                    if ((k + (s > 0 ? 1 : 0)) % 2 == 1) lantern.rotation = Quaternion.Euler(0f, 0f, s * -78f);   // 넘어진 석등
                }

            seed += 8;
            return court;
        }

        const string TempleArtFolder = "Assets/_Game/Art/Env/Temple";

        // 폐사찰 바탕: 바닥 무늬(깨진 돌판 마당) · 먼 배경(무너진 절 산수화) · 흩날리는 낙엽과 재 · 바닥에 깔린 옅은 안개.
        // InkRegionLook이 지역에 맞는 것만 켜고 바닥 재질 무늬를 바꿔요
        static void DressTempleLook(Material floorMat, AThousandDawns.Art.InkRegionDressing dressing)
        {
            ImportAsTiledTexture($"{TempleArtFolder}/Floor_Temple.png");
            ImportAsSprite($"{TempleArtFolder}/Source/Backdrop_Temple.png");

            // 먼 배경: 죽림 배경(Backdrop_Ink) 자리마다 폐사찰 배경을 같이 세우고 둘 중 하나만 켜요
            var bambooBacks = new List<GameObject>();
            var templeBacks = new List<GameObject>();
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(s => s.name == "Backdrop_Ink").ToList())
            {
                var t = sr.transform;
                Billboard(t.parent, "Backdrop_Temple", $"{TempleArtFolder}/Source/Backdrop_Temple.png", t.position, sr.bounds.size.y);
                var temple = t.parent.Find("Backdrop_Temple").gameObject;
                temple.transform.SetSiblingIndex(t.GetSiblingIndex() + 1);
                temple.SetActive(false);
                bambooBacks.Add(sr.gameObject);
                templeBacks.Add(temple);
            }
            var backs = new GameObject("Backdrops by Region").transform;
            var bambooGroup = Group(backs, "죽림", bambooBacks);
            var templeGroup = Group(backs, "폐사찰", templeBacks);

            // 분위기: 죽림은 없음, 폐사찰은 낙엽·재 + 바닥 안개
            var air = new GameObject("Temple Atmosphere (낙엽·안개)");
            air.SetActive(false);
            var leafMat = GetShaderMaterial("Ink_Leaf", "Universal Render Pipeline/Unlit");
            leafMat.SetColor("_BaseColor", new Color(0.32f, 0.25f, 0.18f, 1f));   // 마른 낙엽 먹갈색
            leafMat.SetFloat("_Cull", 0f);
            air.AddComponent<AThousandDawns.Art.InkDriftingLeaves>().Setup(leafMat);
            var mistTex = Resources.Load<Texture2D>("FxArt/InkMist");
            if (mistTex != null)
            {
                var mistMat = GetShaderMaterial("Ink_GroundMist", "Universal Render Pipeline/Unlit");
                mistMat.SetTexture("_BaseMap", mistTex);
                mistMat.SetColor("_BaseColor", new Color(0.93f, 0.91f, 0.86f, 0.45f));   // 한지색 옅은 안개
                mistMat.SetFloat("_Surface", 1f);   // 반투명
                mistMat.SetFloat("_Blend", 0f);
                mistMat.SetFloat("_ZWrite", 0f);
                mistMat.SetOverrideTag("RenderType", "Transparent");
                mistMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mistMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mistMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mistMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // 층 방 둘레 벽 너머(북·동·서)에 낮게 깐 안개 판
                foreach (var (pos, size) in new[]
                         {
                             (CombatCenter + new Vector3(0f, 0.25f, CombatSize.y * 0.5f + 3f), new Vector2(CombatSize.x + 10f, 6f)),
                             (CombatCenter + new Vector3(-CombatSize.x * 0.5f - 3f, 0.25f, 0f), new Vector2(6f, CombatSize.y + 6f)),
                             (CombatCenter + new Vector3(CombatSize.x * 0.5f + 3f, 0.25f, 0f), new Vector2(6f, CombatSize.y + 6f)),
                         })
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Ground Mist";
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(air.transform, false);
                    q.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
                    q.transform.localScale = new Vector3(size.x, size.y, 1f);
                    var r = q.GetComponent<Renderer>();
                    r.sharedMaterial = mistMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            // 장강 수채 (지역 2, 아트2): 젖은 나무 널판 바닥 · 물안개와 갈대 솜 (먼 배경은 게임 중 숨겨서 폐사찰 것을 그대로 둬요)
            const string RiverFloor = "Assets/_Game/Art/Env/River/Floor_River.png";
            ImportAsTiledTexture(RiverFloor);
            var riverAir = new GameObject("River Atmosphere (물안개·갈대 솜)");
            riverAir.SetActive(false);
            riverAir.AddComponent<AThousandDawns.Art.InkRiverAir>();
            // 천마총 (지역 3, 아트2): 금 간 검은 대리석 바닥 · 붉은 안개와 재·불씨
            const string TombFloor = "Assets/_Game/Art/Env/Tomb/Floor_Tomb.png";
            ImportAsTiledTexture(TombFloor);
            var tombAir = new GameObject("Tomb Atmosphere (붉은 안개·재·불씨)");
            tombAir.SetActive(false);
            tombAir.AddComponent<AThousandDawns.Art.InkTombAir>();

            var look = new GameObject("Ink Region Look").AddComponent<AThousandDawns.Art.InkRegionLook>();
            look.Setup(floorMat,
                new Texture[] { null, AssetDatabase.LoadAssetAtPath<Texture2D>($"{TempleArtFolder}/Floor_Temple.png"), AssetDatabase.LoadAssetAtPath<Texture2D>(RiverFloor), AssetDatabase.LoadAssetAtPath<Texture2D>(TombFloor) },
                new[] { bambooGroup, templeGroup, templeGroup, templeGroup },
                new[] { new GameObject("죽림 분위기 (없음)"), air, riverAir, tombAir },
                dressing);
        }

        // 흩어진 오브젝트를 한 번에 켜고 끄는 묶음: 실제로는 부모를 바꾸지 않고, 켜고 끌 때 같이 따라오게 표시 오브젝트에 매달아요
        static GameObject Group(Transform parent, string name, List<GameObject> members)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<AThousandDawns.Art.InkActiveMirror>().Setup(members.ToArray());
            return go;
        }

        // 대나무 묶음 자리의 절 소품: 키 큰 자리는 돌기둥·석탑·석등·비석을 돌아가며, 낮은 자리(카메라 쪽)는 무너진 돌담 조각
        static Transform TempleObstacle(Transform parent, Vector3 foot, float height, int seed, Material stoneMat, Material roofX)
        {
            var group = new GameObject("Temple Ruin").transform;
            group.SetParent(parent, false);
            group.localPosition = foot;
            group.localRotation = Quaternion.Euler(0f, seed * 47f % 360f, 0f);
            if (height < 2.2f)
            {
                // 무너진 돌담 조각: 낮은 돌 덩이 두셋 + 굴러떨어진 돌
                int n = 2 + seed % 2;
                for (int k = 0; k < n; k++)
                    Block(group, $"Wall_{k + 1}", PrimitiveType.Cube, new Vector3((k - n * 0.5f + 0.5f) * 0.42f, 0.18f + (k % 2) * 0.12f, 0f),
                        new Vector3(0.4f, 0.36f + (k % 2) * 0.24f, 0.3f), Quaternion.Euler(0f, (k * 11 + seed * 7) % 13 - 6f, (k % 2) * 4f), stoneMat);
                Rock(group, "Rubble", new Vector3(0.25f, 0f, -0.3f), 0.25f, seed, seed * 31f, stoneMat, false);
                return group;
            }
            switch (seed % 4)
            {
                case 0: BrokenPillar(group, Mathf.Clamp(height * 0.65f, 1.6f, 3.2f), seed, stoneMat); break;
                case 1: StonePagoda(group, 3 + seed % 3, stoneMat, roofX); break;
                case 2: StoneLantern(group, group.position, stoneMat, roofX); break;
                default: Stele(group, seed, stoneMat, roofX); break;
            }
            return group;
        }

        // 부서진 돌기둥: 받침돌 + 굵은 기둥 + 비스듬히 부러진 윗동 + 발치에 떨어진 조각
        static void BrokenPillar(Transform group, float height, int seed, Material stoneMat)
        {
            Block(group, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.85f, 0.24f, 0.85f), Quaternion.identity, stoneMat);
            Block(group, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0.24f + height * 0.5f, 0f), new Vector3(0.5f, height * 0.5f, 0.5f), Quaternion.identity, stoneMat);
            Block(group, "Break", PrimitiveType.Cylinder, new Vector3(0.05f, 0.24f + height + 0.12f, 0f), new Vector3(0.44f, 0.14f, 0.44f),
                Quaternion.Euler(18f + seed % 3 * 6f, seed * 29f, 0f), stoneMat);
            Block(group, "Fallen", PrimitiveType.Cylinder, new Vector3(0.55f, 0.2f, -0.35f), new Vector3(0.42f, 0.32f, 0.42f),
                Quaternion.Euler(90f, seed * 53f, 0f), stoneMat);
        }

        // 석탑: 기단 위에 몸돌·지붕돌을 층마다 좁혀 쌓고 꼭대기에 머리장식
        static void StonePagoda(Transform group, int tiers, Material stoneMat, Material roofX)
        {
            Block(group, "Base", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(1.1f, 0.4f, 1.1f), Quaternion.identity, stoneMat);
            float y = 0.4f, w = 0.7f;
            for (int k = 0; k < tiers; k++)
            {
                float body = 0.42f - k * 0.04f;
                Block(group, $"Body_{k + 1}", PrimitiveType.Cube, new Vector3(0f, y + body * 0.5f, 0f), new Vector3(w, body, w), Quaternion.identity, stoneMat);
                y += body;
                var roof = new GameObject($"Roof_{k + 1}");
                roof.transform.SetParent(group, false);
                roof.transform.localPosition = new Vector3(0f, y, 0f);
                roof.transform.localScale = new Vector3(w * 1.55f, 0.2f, w * 1.55f);
                roof.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
                roof.AddComponent<MeshRenderer>().sharedMaterial = roofX;
                y += 0.12f;
                w *= 0.82f;
            }
            Block(group, "Finial", PrimitiveType.Cylinder, new Vector3(0f, y + 0.25f, 0f), new Vector3(0.08f, 0.25f, 0.08f), Quaternion.identity, stoneMat);
        }

        // 비석: 거북 받침(낮은 바위) 위에 넓적한 비석, 머릿돌은 지붕돌
        static void Stele(Transform group, int seed, Material stoneMat, Material roofX)
        {
            Rock(group, "Turtle", Vector3.zero, 0.75f, seed, seed * 13f, stoneMat, false);
            Block(group, "Slab", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.75f, 1.6f, 0.2f), Quaternion.Euler(0f, 0f, seed % 2 == 0 ? 3f : -4f), stoneMat);
            var cap = new GameObject("Cap");
            cap.transform.SetParent(group, false);
            cap.transform.localPosition = new Vector3(0f, 1.98f, 0f);
            cap.transform.localScale = new Vector3(0.95f, 0.22f, 0.4f);
            cap.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            cap.AddComponent<MeshRenderer>().sharedMaterial = roofX;
        }

        // 굽은 소나무: 두 마디로 휜 줄기 + 가지 끝마다 납작한 솔잎 구름 (덤불 덩어리를 눌러 써요)
        // 소나무 (실제 소나무처럼): 줄기가 한 번에 휘어 기울다가 위에서 다시 굽어 오르고(굽은 원통 한 덩이라 이음매가 없어요),
        // 위쪽에서 가지가 거의 수평으로 뻗었다가 끝이 살짝 들려요. 가지 끝·꼭대기에 넓고 납작한 솔잎 층.
        // 모양은 네 가지(메시 저장)를 돌려 쓰고, 나무마다 높이·방향만 달라요
        const float PineDesignHeight = 5f;

        static void PineTree(Transform parent, Vector3 foot, float height, int seed, Material woodMat)
        {
            var tree = new GameObject("Pine (소나무)").transform;
            tree.SetParent(parent, false);
            tree.localPosition = foot;
            tree.localRotation = Quaternion.Euler(0f, seed * 67f % 360f, 0f);
            tree.localScale = Vector3.one * (height / PineDesignHeight);

            int variant = seed % 4;
            var mesh = GetPineMesh(variant, out var pads);
            var trunk = new GameObject("Trunk");
            trunk.transform.SetParent(tree, false);
            trunk.AddComponent<MeshFilter>().sharedMesh = mesh;
            trunk.AddComponent<MeshRenderer>().sharedMaterial = woodMat;

            var mat = GetFoliageBlobMaterial("Bush_B");
            var tex = (Texture2D)mat.GetTexture("_BaseMap");
            float aspect = tex != null ? (float)tex.width / tex.height : 1f;
            for (int k = 0; k < pads.Count; k++)
            {
                var puff = new GameObject($"Needles_{k + 1}");
                puff.transform.SetParent(tree, false);
                puff.transform.localPosition = pads[k];
                float w = k == 0 ? 1.9f : 1.5f - (k % 2) * 0.2f;
                puff.transform.localScale = new Vector3(w, 0.45f, w * 0.85f) * 1.2f;   // 넓고 납작한 솔잎 층
                puff.transform.localRotation = Quaternion.Euler(0f, k * 73f, 0f);
                puff.AddComponent<MeshFilter>().sharedMesh = GetBushBlobMesh((seed + k) % 3, aspect);
                puff.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        // 줄기+가지 메시 (높이 PineDesignHeight 기준). pads = 솔잎 층 자리 (0번이 꼭대기)
        static Mesh GetPineMesh(int variant, out List<Vector3> pads)
        {
            var rnd = new System.Random(variant * 211 + 3);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            pads = new List<Vector3>();

            // 줄기 중심선: 아래는 한쪽으로 기울고(lean), 위에서 반대로 굽어 올라 S자. 옆으로 살짝 비틀려요(sway)
            float lean = R(0.7f, 1.1f) * (variant % 2 == 0 ? 1f : -1f), sway = R(-0.35f, 0.35f), top = PineDesignHeight * 0.88f;
            Vector3 Trunk(float t) => new Vector3(
                lean * Mathf.Sin(Mathf.PI * t * 0.85f) - lean * 0.55f * t * t * t,
                top * t,
                sway * Mathf.Sin(Mathf.PI * 2f * t));
            const int trunkSegs = 14;
            var line = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i <= trunkSegs; i++)
            {
                float t = i / (float)trunkSegs;
                line.Add(Trunk(t));
                radii.Add(Mathf.Lerp(0.2f, 0.06f, t) * (1f + 0.6f * Mathf.Pow(1f - t, 8f)));   // 위로 갈수록 가늘고 뿌리께는 퍼져요
            }
            Tube(line, radii, verts, normals, tris);
            pads.Add(Trunk(1f) + new Vector3(0f, 0.15f, 0f));

            // 가지: 줄기 위쪽 세 곳에서 둘레를 돌아가며 거의 수평으로 뻗고, 가운데가 처졌다가 끝이 들려요
            float[] at = { 0.5f, 0.66f, 0.8f };
            float yaw0 = R(0f, 360f);
            for (int b = 0; b < at.Length; b++)
            {
                Vector3 start = Trunk(at[b]);
                float yaw = (yaw0 + b * R(110f, 150f)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                float blen = R(1.3f, 1.9f) * (1f - b * 0.12f);
                var bl = new List<Vector3>();
                var br = new List<float>();
                const int segs = 6;
                for (int i = 0; i <= segs; i++)
                {
                    float u = i / (float)segs;
                    float y = 0.35f * u - 0.45f * u * u + 0.35f * u * u * u;   // 살짝 오르다 처지고 끝이 들려요
                    bl.Add(start + dir * (blen * u) + Vector3.up * y);
                    br.Add(Mathf.Lerp(0.07f, 0.025f, u));
                }
                Tube(bl, br, verts, normals, tris);
                pads.Add(bl[segs] + new Vector3(0f, 0.12f, 0f));
            }

            string key = $"PineTrunk_{variant}";
            if (s_inkPropMeshes.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = $"{InkMeshFolder}/{key}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = key };
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (isNew)
            {
                EnsureFolder(InkMeshFolder);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);
            s_inkPropMeshes[key] = mesh;
            return mesh;
        }

        // 중심선을 따라 굵기가 변하는 매끈한 관 (평행 이동 틀로 고리가 비틀리지 않게)
        static void Tube(List<Vector3> line, List<float> radii, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            const int sides = 8;
            int baseIndex = verts.Count;
            Vector3 prevT = (line[1] - line[0]).normalized;
            Vector3 n = Vector3.Cross(prevT, Mathf.Abs(prevT.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < line.Count; i++)
            {
                Vector3 t = i == 0 ? prevT : i == line.Count - 1 ? (line[i] - line[i - 1]).normalized : (line[i + 1] - line[i - 1]).normalized;
                n = (Quaternion.FromToRotation(prevT, t) * n).normalized;
                prevT = t;
                Vector3 bnorm = Vector3.Cross(t, n);
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    Vector3 d = n * Mathf.Cos(a) + bnorm * Mathf.Sin(a);
                    verts.Add(line[i] + d * radii[i]);
                    normals.Add(d);
                }
            }
            for (int i = 0; i < line.Count - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = baseIndex + i * sides + k, b = baseIndex + i * sides + (k + 1) % sides;
                    int c = a + sides, d = b + sides;
                    tris.Add(a); tris.Add(b); tris.Add(c);   // 바깥이 앞면 (Cross(b-a, c-a)가 바깥)
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
        }

        // 무너진 전각: 기둥 몇 개만 남고, 기운 지붕이 내려앉은 모습
        static void RuinedHall(Transform parent, Vector3 foot, int seed, Material stoneMat, Material woodMat, Material roofX)
        {
            var hall = new GameObject("Ruined Hall (무너진 전각)").transform;
            hall.SetParent(parent, false);
            hall.localPosition = foot;
            hall.localRotation = Quaternion.Euler(0f, seed * 31f % 360f, 0f);
            Block(hall, "Platform", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(3.2f, 0.5f, 2.2f), Quaternion.identity, stoneMat);
            float[] xs = { -1.3f, 1.3f };
            for (int k = 0; k < 2; k++)
            {
                float h = k == 0 ? 2.6f : 1.4f;   // 한쪽 기둥은 부러졌어요
                Block(hall, $"Column_{k + 1}", PrimitiveType.Cylinder, new Vector3(xs[k], 0.5f + h * 0.5f, 0.6f), new Vector3(0.24f, h * 0.5f, 0.24f), Quaternion.identity, woodMat);
            }
            var roof = new GameObject("Roof (내려앉음)");
            roof.transform.SetParent(hall, false);
            roof.transform.localPosition = new Vector3(0f, 2.4f, 0.3f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            roof.transform.localScale = new Vector3(4f, 0.9f, 2.6f);
            roof.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(true);
            roof.AddComponent<MeshRenderer>().sharedMaterial = roofX;
            Rock(hall, "Debris", new Vector3(1.6f, 0f, -0.9f), 0.45f, seed, seed * 19f, stoneMat, false);
        }
    }
}
