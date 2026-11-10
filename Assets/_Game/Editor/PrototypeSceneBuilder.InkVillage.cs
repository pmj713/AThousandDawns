using System.Collections.Generic;
using AThousandDawns.Prototype;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판의 마을: 원래 빌더(BuildVillage)의 상자·원기둥은 충돌용으로 남기고 렌더러만 끈 뒤,
    // 같은 자리에 수묵 객잔·매화나무·뒷산 동굴·사당·마을 어귀·무기 거치대를 세워요. 자리 값은 원래 빌더·RoomRun과 같아요.
    // 새로 세운 것은 모두 보기용이라 충돌체가 없어요 (길막힘·상호작용은 원래 상자 그대로).
    public static partial class PrototypeSceneBuilder
    {
        const string VillageArtFolder = "Assets/_Game/Art/Env/Village";
        const int PlumBlossomCount = 22; // 회귀록 봉우리 수만큼

        static void DressVillageInk(Material floorMat, Material stoneMat, Material woodMat, Material bambooMat, Material roofX)
        {
            var village = GameObject.Find("Village");
            if (village == null) return;
            ImportVillageArt();
            var (_, roofZ) = GetInkRoofMaterials();
            var plaster = GetInkMaterial("Ink_Plaster", "#E9E0CD", 0f);
            var dark = GetInkMaterial("Ink_CaveDark", "#1B1917", 0f);
            var iron = GetInkMaterial("Ink_Iron", "#2C2F33", 0f);

            // 원래 모양 숨기기 (담·바닥은 남김: 담은 이미 돌담·기와로 바뀌었고 바닥은 재질만 바꿔요)
            var hide = new HashSet<string>
            {
                "Gate_Post_W", "Gate_Post_E", "Gate_Beam", "Inn_Building", "Inn_Porch",
                "Plum_Trunk", "Plum_Blossom", "Hill", "Cave_Mouth", "Shrine", "Shrine_Roof", "Weapon_Rack",
            };
            foreach (Transform t in village.transform)
            {
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                if (hide.Contains(t.name)) r.enabled = false;
                else if (t.name == "Floor") r.sharedMaterial = floorMat;
            }

            var deco = new GameObject("Ink Deco (Village)").transform;
            var c = VillageCenter;
            float hx = VillageSize.x * 0.5f, hz = VillageSize.y * 0.5f;

            BuildVillageInn(deco, c + new Vector3(-hx + 1.5f, 0f, 0f), stoneMat, woodMat, plaster, roofX, roofZ);
            BuildPlumTree(deco, c + RoomRun.TreeOffset + new Vector3(0f, 0f, 1f));
            BuildVillageCave(deco, c + RoomRun.CaveOffset, stoneMat, dark);
            BuildVillageShrine(deco, c + RoomRun.ShrineOffset + new Vector3(-1f, 0f, -1f), stoneMat, woodMat, plaster, roofX);
            BuildVillageGate(deco, c + new Vector3(VillageExit.x, 0f, -hz - 0.25f), stoneMat, woodMat, roofX);
            BuildWeaponRack(deco, c + RoomRun.RackOffset + new Vector3(0f, 0f, -0.6f), woodMat, iron);

            // 담 너머 북쪽 대나무 숲과 먼 풍경 (남쪽은 카메라를 가리지 않게 비워요)
            var ring = new GameObject("Forest (Village)").transform;
            ring.SetParent(deco, false);
            ring.position = c;
            BuildEnv3DForest(ring, hx + 0.5f, hz + 0.3f, 1.75f, bambooMat, 0f);
            Billboard(deco, "Backdrop_Ink", $"{Env2DArtFolder}/Source/Backdrop_Ink.png", c + new Vector3(0f, 0f, hz + 6f), 22f);

            StripColliders(deco);
        }

        static void ImportVillageArt()
        {
            foreach (var name in new[] { "Plum_Blossom", "Plum_Blossom_B", "Door_Lattice" })
                ImportAsSprite($"{VillageArtFolder}/{name}.png");
            ImportAsTiledTexture($"{VillageArtFolder}/Tex_PlumBark.png");
        }

        // 객잔: 돌 기단, 기둥·회벽, 남쪽(카메라 쪽) 박공에 창호문, 동쪽 마루와 처마, 맞배 기와지붕(용마루 남북)
        static void BuildVillageInn(Transform deco, Vector3 foot, Material stone, Material wood, Material plaster, Material roofX, Material roofZ)
        {
            var inn = new GameObject("Inn (객잔)").transform;
            inn.SetParent(deco, false);
            inn.position = foot;
            const float w = 2.8f, d = 5.8f, h = 2.5f;
            Block(inn, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(w + 0.6f, 0.3f, d + 0.6f), Quaternion.identity, stone);
            Block(inn, "Walls", PrimitiveType.Cube, new Vector3(0f, 0.3f + h * 0.5f, 0f), new Vector3(w, h, d), Quaternion.identity, plaster);
            foreach (float x in new[] { -w * 0.5f, w * 0.5f })
                foreach (float z in new[] { -d * 0.5f, 0f, d * 0.5f })
                    Block(inn, "Post", PrimitiveType.Cube, new Vector3(x, 0.3f + h * 0.5f, z), new Vector3(0.24f, h, 0.24f), Quaternion.identity, wood);
            foreach (float x in new[] { -w * 0.5f, w * 0.5f })
                Block(inn, "Beam", PrimitiveType.Cube, new Vector3(x, 0.3f + h - 0.1f, 0f), new Vector3(0.26f, 0.22f, d + 0.1f), Quaternion.identity, wood);
            Block(inn, "Beam_S", PrimitiveType.Cube, new Vector3(0f, 0.3f + h - 0.1f, -d * 0.5f), new Vector3(w + 0.1f, 0.22f, 0.26f), Quaternion.identity, wood);

            // 창호문: 남쪽 박공 둘, 동쪽(마루 쪽) 둘
            for (int s = -1; s <= 1; s += 2)
                SpriteCard(inn, "Door_S", $"{VillageArtFolder}/Door_Lattice.png", new Vector3(s * 0.68f, 0.3f, -d * 0.5f - 0.02f), 1.9f, 0f);
            for (int s = -1; s <= 1; s += 2)
                SpriteCard(inn, "Door_E", $"{VillageArtFolder}/Door_Lattice.png", new Vector3(w * 0.5f + 0.02f, 0.3f, s * 1.45f), 1.9f, -90f);

            RoofMesh(inn, "Roof", new Vector3(0f, 0.3f + h + 0.6f, 0f), new Vector3(w + 1f, 1.3f, d + 0.8f), false, roofZ);

            // 마루: 낮은 대청마루와 처마 기둥, 처마 지붕
            Block(inn, "Maru", PrimitiveType.Cube, new Vector3(w * 0.5f + 0.75f, 0.16f, 0f), new Vector3(1.5f, 0.12f, 4.2f), Quaternion.identity, wood);
            for (int s = -1; s <= 1; s += 2)
                Block(inn, "Eave_Post", PrimitiveType.Cube, new Vector3(w * 0.5f + 1.4f, 1.35f, s * 2f), new Vector3(0.18f, 2.7f, 0.18f), Quaternion.identity, wood);
            RoofMesh(inn, "Eave_Roof", new Vector3(w * 0.5f + 0.9f, 2.85f, 0f), new Vector3(2.2f, 0.5f, 4.8f), false, roofZ);

            // 등불 둘과 술독
            for (int s = -1; s <= 1; s += 2)
                Billboard(inn, "LanternPost_A", $"{Env2DArtFolder}/LanternPost_A.png", foot + new Vector3(w * 0.5f + 2.1f, 0f, s * 2.7f), 2.6f, s > 0);
            var jarMat = GetInkMaterial("Ink_Jar", "#4A3A2E", 0.35f);
            Vector3[] jars = { new Vector3(-0.6f, 0f, -d * 0.5f - 0.9f), new Vector3(0.1f, 0f, -d * 0.5f - 1.1f), new Vector3(-0.2f, 0f, -d * 0.5f - 0.5f) };
            for (int i = 0; i < jars.Length; i++)
            {
                float s = 0.5f + i % 2 * 0.15f;
                Block(inn, "Jar", PrimitiveType.Sphere, jars[i] + Vector3.up * s * 0.6f, new Vector3(s, s * 1.2f, s), Quaternion.identity, jarMat);
            }
        }

        // 매화나무: 굽은 줄기·가지를 코드로 만든 3D 메시에 매화 껍질 텍스처(InkTriplanar)를 입혀요.
        // 꽃송이는 가지 끝마다 카메라를 마주 보는 작은 카드 (PlumBlossoms가 회귀록에서 피운 수만큼 켜요)
        static void BuildPlumTree(Transform deco, Vector3 foot)
        {
            var bark = GetInkTriplanar("Ink3D_PlumBark", "Tex_Wood", 0.6f, 0f);
            bark.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{VillageArtFolder}/Tex_PlumBark.png"));
            bark.SetFloat("_OutlineFromCenter", 0f); // 둥근 가지는 법선 방향으로 외곽선
            bark.SetFloat("_OutlineWidth", 1.5f);
            bark.SetFloat("_StreakLight", 0.35f);
            bark.SetFloat("_StreakScale", 4f);
            bark.SetColor("_BaseColor", new Color(0.5f, 0.45f, 0.42f, 1f)); // 껍질 그림보다 짙게

            var tips = new List<Vector3>();
            var mesh = GetPlumTreeMesh(tips);
            var tree = new GameObject("Plum_Tree (매화나무)");
            tree.transform.SetParent(deco, false);
            tree.transform.position = foot;
            tree.AddComponent<MeshFilter>().sharedMesh = mesh;
            tree.AddComponent<MeshRenderer>().sharedMaterial = bark;

            var blossomSprites = new[] { AssetDatabase.LoadAssetAtPath<Sprite>($"{VillageArtFolder}/Plum_Blossom.png"), AssetDatabase.LoadAssetAtPath<Sprite>($"{VillageArtFolder}/Plum_Blossom_B.png") };
            if (blossomSprites[0] == null || blossomSprites[1] == null) return;
            var facing = Quaternion.Euler(CameraPitch, 0f, 0f); // 카메라를 마주 보게
            var cards = new List<Transform>();
            for (int i = 0; i < tips.Count; i++)
            {
                var go = new GameObject($"Blossom_{i:00}");
                go.transform.SetParent(tree.transform, false);
                go.transform.localRotation = facing * Quaternion.Euler(0f, 0f, (i * 53 % 60) - 30f);
                var sprite = blossomSprites[i % 2];
                float size = 0.3f * (0.8f + (i * 37 % 10) * 0.05f); // 월드 키 약 0.3
                float k = size / sprite.bounds.size.y;
                go.transform.localScale = new Vector3(i % 4 >= 2 ? -k : k, k, k);
                go.transform.localPosition = tips[i] - go.transform.up * size * 0.5f; // 스프라이트는 밑동 기준이라 가운데가 가지 끝에 오게
                go.AddComponent<SpriteRenderer>().sprite = sprite;
                cards.Add(go.transform);
            }
            tree.AddComponent<AThousandDawns.Art.PlumBlossoms>().SetBlossoms(cards.ToArray());
        }

        // 굽은 매화나무 메시 (밑동이 원점). 가지는 마디마다 방향이 꺾이는 원뿔대 관, 법선은 부드럽게.
        // tips에는 꽃송이 자리(가지 끝·곁가지 끝)를 피는 순서대로 채워요: 나무 전체에 흩어지게 섞어서.
        static Mesh GetPlumTreeMesh(List<Vector3> tips)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var rng = new System.Random(20261110);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var allTips = new List<Vector3>();

            // 관 하나: 점들을 따라 반지름이 줄어드는 8각 고리를 이어요
            void Tube(List<Vector3> path, float r0, float r1)
            {
                const int sides = 8;
                int baseIndex = verts.Count;
                for (int i = 0; i < path.Count; i++)
                {
                    var dir = (i < path.Count - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
                    var side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    var up = Vector3.Cross(side, dir);
                    float r = Mathf.Lerp(r0, r1, i / (float)(path.Count - 1));
                    for (int s = 0; s < sides; s++)
                    {
                        float a = s * Mathf.PI * 2f / sides;
                        float bump = 1f + 0.12f * Mathf.Sin(a * 3f + i * 1.7f); // 울퉁불퉁한 껍질
                        verts.Add(path[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * r * bump);
                    }
                }
                for (int i = 0; i < path.Count - 1; i++)
                    for (int s = 0; s < sides; s++)
                    {
                        int a = baseIndex + i * sides + s, b = baseIndex + i * sides + (s + 1) % sides;
                        int c = a + sides, d = b + sides;
                        tris.AddRange(new[] { a, c, b, b, c, d });
                    }
                int tip = verts.Count;
                verts.Add(path[path.Count - 1] + (path[path.Count - 1] - path[path.Count - 2]).normalized * r1);
                int last = baseIndex + (path.Count - 1) * sides;
                for (int s = 0; s < sides; s++) tris.AddRange(new[] { last + s, tip, last + (s + 1) % sides });
            }

            // 고매(古梅) 수형: 실제 늙은 매화나무와 매화도를 참고했어요.
            //  - 줄기: 짧고 굵게, 한쪽으로 크게 기울었다가 한 번 꺾여요. 옆에 부러진 굵은 그루터기 하나
            //  - 큰 가지: 둘셋이 낮고 넓게 퍼지며 마디마다 각지게 꺾여요 (둥글게 휘지 않게). 수관이 키보다 넓어요
            //  - 새 가지(도장지): 큰 가지 위에서 가늘고 곧게 거의 수직으로 솟아요. 매화다운 모습은 대부분 여기서 나와요
            //  - 잔가지: 짧고 가는 가지가 비스듬히. 꽃은 새 가지·잔가지를 따라 점점이 (끝에만 몰리지 않게)
            // 방향은 (수평 방위 yaw, 올려본 각 elevation)으로 다뤄요.
            Vector3 Dir(float yaw, float elevation)
            {
                float y = yaw * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(y) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(y) * Mathf.Cos(e));
            }

            // 마디 몇 개짜리 각진 가지. kink = 마디마다 꺾이는 정도(도), droop = 마디마다 낮아지는 정도
            List<Vector3> Limb(Vector3 start, float yaw, float elevation, float length, int segs, float kink, float droop, float r0, float r1)
            {
                var path = new List<Vector3> { start };
                var p = start;
                for (int i = 0; i < segs; i++)
                {
                    float sign = i % 2 == 0 ? 1f : -1f; // 좌우로 번갈아 꺾여 지그재그
                    yaw += sign * Rand(kink * 0.5f, kink);
                    elevation = Mathf.Clamp(elevation + Rand(-kink * 0.4f, kink * 0.4f) - droop, -10f, 88f);
                    p += Dir(yaw, elevation) * length / segs;
                    path.Add(p);
                }
                Tube(path, r0, r1);
                return path;
            }

            // 가는 가지를 따라 꽃 자리 후보를 찍어요 (from~끝 사이 몇 군데)
            void Spots(List<Vector3> path, float from, int count)
            {
                for (int i = 1; i <= count; i++)
                {
                    float t = Mathf.Lerp(from, 1f, i / (float)count) * (path.Count - 1);
                    int a = Mathf.Min((int)t, path.Count - 2);
                    allTips.Add(Vector3.Lerp(path[a], path[a + 1], t - a));
                }
            }

            // 가지 위 t 지점 (0~1)
            Vector3 At(List<Vector3> path, float t)
            {
                float f = t * (path.Count - 1);
                int a = Mathf.Min((int)f, path.Count - 2);
                return Vector3.Lerp(path[a], path[a + 1], f - a);
            }

            void Shoot(Vector3 from, float yaw)
            {
                // 새 가지: 거의 곧게, 높이 솟아요. 끝 가까이에 잔가지 하나
                var s = Limb(from, yaw + Rand(-45f, 45f), Rand(55f, 80f), Rand(0.7f, 1.4f), 2, 8f, 0f, 0.032f, 0.008f); // 가늘고, 각도·길이가 제각각
                Spots(s, 0.35f, 3);
                if (rng.NextDouble() < 0.6)
                {
                    var spur = Limb(At(s, Rand(0.4f, 0.7f)), yaw + (rng.Next(2) * 2 - 1) * Rand(50f, 90f), Rand(35f, 55f), Rand(0.25f, 0.45f), 1, 0f, 0f, 0.015f, 0.006f);
                    Spots(spur, 0.6f, 1);
                }
            }

            float lean = Rand(0f, 360f);
            // 줄기: 기울어 오르다 반대로 한 번 꺾여요
            var trunkA = Limb(Vector3.zero, lean, 62f, 0.9f, 2, 8f, 0f, 0.36f, 0.3f);
            var trunkB = Limb(trunkA[trunkA.Count - 1], lean + 160f, 70f, 0.7f, 2, 10f, 0f, 0.3f, 0.24f);
            var crown = trunkB[trunkB.Count - 1];
            // 부러진 굵은 그루터기 (끝이 뭉툭하게)
            Tube(new List<Vector3> { At(trunkA, 0.7f), At(trunkA, 0.7f) + Dir(lean - 70f, 35f) * 0.45f }, 0.17f, 0.13f);

            // 큰 가지 셋: 한쪽으로 쏠려요 (늙은 매화는 기운 쪽으로 긴 가지 하나, 양옆에 짧은 가지 둘).
            // 사방으로 고르게 뻗으면 촛대처럼 보여서 일부러 비대칭으로
            float side = lean + 160f; // 줄기 위쪽이 기운 방향
            (float yawOffset, float minElev, float maxElev, float minLen, float maxLen)[] limbs =
            {
                (0f, 32f, 40f, 2.3f, 2.6f), (-80f, 40f, 55f, 1.3f, 1.6f), (75f, 40f, 55f, 1.2f, 1.5f),
            };
            foreach (var l in limbs)
            {
                float yaw = side + l.yawOffset + Rand(-15f, 15f);
                var limb = Limb(crown, yaw, Rand(l.minElev, l.maxElev), Rand(l.minLen, l.maxLen), 4, 22f, 2f, 0.2f, 0.06f);
                // 큰 가지 위로 새 가지 넷, 바깥쪽으로 갈수록 촘촘하게
                for (int k = 0; k < 4; k++) Shoot(At(limb, 0.25f + k * 0.2f + Rand(-0.05f, 0.05f)), yaw);
                // 큰 가지 끝은 가는 가지로 이어져 비스듬히 뻗어요
                var end = Limb(limb[limb.Count - 1], yaw + Rand(-25f, 25f), Rand(25f, 45f), Rand(0.6f, 0.9f), 2, 18f, 0f, 0.05f, 0.015f);
                Spots(end, 0.4f, 2);
            }
            // 줄기 꼭대기에서 바로 솟는 새 가지 둘
            for (int k = 0; k < 2; k++) Shoot(crown, lean + 180f + k * 90f);

            // 밑동: 땅을 움켜쥔 짧은 뿌리 다섯
            for (int i = 0; i < 5; i++)
            {
                var outward = Quaternion.Euler(0f, i * 72f + Rand(-15f, 15f), 0f) * Vector3.forward;
                Tube(new List<Vector3> { Vector3.up * 0.4f, outward * 0.25f + Vector3.up * 0.12f, outward * Rand(0.4f, 0.55f) + Vector3.down * 0.04f }, 0.18f, 0.05f);
            }

            // 꽃송이 자리: 후보를 섞은 뒤 서로 떨어지게 PlumBlossomCount개 골라요 (고른 순서 = 피는 순서, 나무 전체에 흩어지게)
            for (int i = allTips.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (allTips[i], allTips[j]) = (allTips[j], allTips[i]); }
            var picked = new List<Vector3>();
            for (float minDist = 0.6f; picked.Count < PlumBlossomCount && minDist > 0.05f; minDist *= 0.8f)
                foreach (var t in allTips)
                {
                    if (picked.Count >= PlumBlossomCount) break;
                    bool far = true;
                    foreach (var q in picked) if ((q - t).sqrMagnitude < minDist * minDist) { far = false; break; }
                    if (far) picked.Add(t);
                }
            for (int i = picked.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (picked[i], picked[j]) = (picked[j], picked[i]); }
            tips.AddRange(picked);

            string assetPath = $"{VillageArtFolder}/Meshes/PlumTree.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "PlumTree" };
            mesh.Clear();
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) { EnsureFolder($"{VillageArtFolder}/Meshes"); AssetDatabase.CreateAsset(mesh, assetPath); }
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // 뒷산 동굴: 산자락 바위 무더기와 검은 굴 입구 (북쪽 담 앞)
        static void BuildVillageCave(Transform deco, Vector3 spot, Material stone, Material dark)
        {
            var cave = new GameObject("Cave (뒷산 동굴)").transform;
            cave.SetParent(deco, false);
            cave.position = spot;
            float[] xs = { -2.6f, -1.5f, 1.5f, 2.6f };
            float[] sizes = { 1.6f, 2.1f, 2.1f, 1.5f };
            for (int i = 0; i < xs.Length; i++)
                Rock(cave, "Hill_Rock", new Vector3(xs[i], 0f, 1.35f + (i % 2) * 0.2f), sizes[i], i, 30f + i * 70f, stone, false);
            Rock(cave, "Hill_Top", new Vector3(0f, 1.5f, 1.6f), 2.2f, 2, 15f, stone, false);
            Block(cave, "Mouth", PrimitiveType.Sphere, new Vector3(0f, 0.85f, 0.8f), new Vector3(1.7f, 1.8f, 0.35f), Quaternion.identity, dark);
            RockCluster(cave, new Vector3(-1.4f, 0f, 0.4f), 0.45f, 7, stone);
            RockCluster(cave, new Vector3(1.5f, 0f, 0.3f), 0.35f, 9, stone);
        }

        // 사당: 돌 기단, 기둥 넷, 뒷벽, 제단, 맞배지붕(용마루 동서), 옆에 석등
        static void BuildVillageShrine(Transform deco, Vector3 foot, Material stone, Material wood, Material plaster, Material roofX)
        {
            var shrine = new GameObject("Shrine (사당)").transform;
            shrine.SetParent(deco, false);
            shrine.position = foot;
            const float w = 1.7f, d = 1.3f, h = 1.6f;
            Block(shrine, "Plinth", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(w + 0.4f, 0.24f, d + 0.4f), Quaternion.identity, stone);
            Block(shrine, "Step", PrimitiveType.Cube, new Vector3(0f, 0.06f, -d * 0.5f - 0.35f), new Vector3(0.9f, 0.12f, 0.35f), Quaternion.identity, stone);
            foreach (float x in new[] { -w * 0.5f, w * 0.5f })
                foreach (float z in new[] { -d * 0.5f, d * 0.5f })
                    Block(shrine, "Post", PrimitiveType.Cube, new Vector3(x, 0.24f + h * 0.5f, z), new Vector3(0.16f, h, 0.16f), Quaternion.identity, wood);
            Block(shrine, "Back_Wall", PrimitiveType.Cube, new Vector3(0f, 0.24f + h * 0.5f, d * 0.5f), new Vector3(w, h, 0.08f), Quaternion.identity, plaster);
            Block(shrine, "Altar", PrimitiveType.Cube, new Vector3(0f, 0.55f, d * 0.2f), new Vector3(0.9f, 0.6f, 0.45f), Quaternion.identity, wood);
            RoofMesh(shrine, "Roof", new Vector3(0f, 0.24f + h + 0.35f, 0f), new Vector3(w + 1f, 0.9f, d + 0.9f), true, roofX);
            StoneLantern(shrine, foot + new Vector3(w * 0.5f + 0.9f, 0f, -d * 0.5f - 0.4f), stone, roofX);
        }

        // 마을 어귀: 돌 받침 위 굵은 기둥 둘, 들보 둘, 작은 기와지붕 (원래 기둥·들보 자리)
        static void BuildVillageGate(Transform deco, Vector3 spot, Material stone, Material wood, Material roofX)
        {
            var gate = new GameObject("Gate (마을 어귀)").transform;
            gate.SetParent(deco, false);
            gate.position = spot;
            for (int s = -1; s <= 1; s += 2)
            {
                Block(gate, "Post_Base", PrimitiveType.Cube, new Vector3(s * 1.7f, 0.2f, 0f), new Vector3(0.6f, 0.4f, 0.6f), Quaternion.identity, stone);
                Block(gate, "Post", PrimitiveType.Cylinder, new Vector3(s * 1.7f, 1.7f, 0f), new Vector3(0.38f, 1.5f, 0.38f), Quaternion.identity, wood);
            }
            Block(gate, "Beam_Upper", PrimitiveType.Cube, new Vector3(0f, 3.1f, 0f), new Vector3(4.4f, 0.28f, 0.34f), Quaternion.identity, wood);
            Block(gate, "Beam_Lower", PrimitiveType.Cube, new Vector3(0f, 2.6f, 0f), new Vector3(3.4f, 0.16f, 0.22f), Quaternion.identity, wood);
            RoofMesh(gate, "Roof", new Vector3(0f, 3.55f, 0f), new Vector3(5f, 0.7f, 1.3f), true, roofX);
        }

        // 무기 거치대: 나무 틀에 검 한 자루와 비도 몇 자루
        static void BuildWeaponRack(Transform deco, Vector3 foot, Material wood, Material iron)
        {
            var rack = new GameObject("Weapon Rack (무기 거치대)").transform;
            rack.SetParent(deco, false);
            rack.position = foot;
            for (int s = -1; s <= 1; s += 2)
            {
                Block(rack, "Upright", PrimitiveType.Cube, new Vector3(s * 0.75f, 0.65f, 0f), new Vector3(0.12f, 1.3f, 0.12f), Quaternion.identity, wood);
                Block(rack, "Foot", PrimitiveType.Cube, new Vector3(s * 0.75f, 0.04f, 0f), new Vector3(0.14f, 0.08f, 0.5f), Quaternion.identity, wood);
            }
            Block(rack, "Bar_Top", PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(1.7f, 0.1f, 0.12f), Quaternion.identity, wood);
            Block(rack, "Bar_Low", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.08f, 0.1f), Quaternion.identity, wood);
            // 검: 칼날·코등이·자루를 기울여 걸쳐요
            var sword = new GameObject("Sword").transform;
            sword.SetParent(rack, false);
            sword.localPosition = new Vector3(-0.3f, 0.1f, -0.1f);
            sword.localRotation = Quaternion.Euler(0f, 0f, 12f);
            Block(sword, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.07f, 1.0f, 0.02f), Quaternion.identity, iron);
            Block(sword, "Guard", PrimitiveType.Cube, new Vector3(0f, 1.12f, 0f), new Vector3(0.22f, 0.05f, 0.06f), Quaternion.identity, iron);
            Block(sword, "Grip", PrimitiveType.Cube, new Vector3(0f, 1.3f, 0f), new Vector3(0.05f, 0.3f, 0.05f), Quaternion.identity, wood);
            // 비도: 짧은 칼 셋을 위 막대에 매달아요
            for (int i = 0; i < 3; i++)
            {
                var dagger = new GameObject("Dagger").transform;
                dagger.SetParent(rack, false);
                dagger.localPosition = new Vector3(0.25f + i * 0.17f, 0.82f, -0.08f);
                dagger.localRotation = Quaternion.Euler(0f, 0f, 180f + (i - 1) * 6f);
                Block(dagger, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.05f, 0.28f, 0.015f), Quaternion.identity, iron);
                Block(dagger, "Ring", PrimitiveType.Cube, new Vector3(0f, -0.06f, 0f), new Vector3(0.06f, 0.1f, 0.03f), Quaternion.identity, wood);
            }
        }

        // 기와지붕 메시 (GetRoofCapMesh). alongX = 용마루가 동서
        static GameObject RoofMesh(Transform parent, string name, Vector3 localPos, Vector3 size, bool alongX, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(alongX);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // 벽에 붙이는 그림 한 장 (창호문 등). foot은 부모 기준 밑동 가운데, yaw로 벽 방향을 맞춰요.
        // Billboard와 달리 가로를 cos55°만큼 줄이지 않아요: 벽에 붙은 평면이라 원근이 벽과 같아야 해요.
        static void SpriteCard(Transform parent, string name, string spritePath, Vector3 foot, float height, float yaw)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = foot;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
        }
    }
}
