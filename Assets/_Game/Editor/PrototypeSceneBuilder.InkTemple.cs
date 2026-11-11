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

            var dressing = new GameObject("Ink Region Dressing").AddComponent<AThousandDawns.Art.InkRegionDressing>();
            dressing.SetRegions(new[]
            {
                new AThousandDawns.Art.InkRegionDressing.Set { name = "죽림", objects = bamboo.ToArray() },
                new AThousandDawns.Art.InkRegionDressing.Set { name = "폐사찰", objects = temple.ToArray() },
            });
            foreach (var go in temple) go.SetActive(false);   // 처음(죽림)엔 꺼 둬요

            DressTempleLook(floorMat, dressing);
        }

        // 무너진 대웅전 앞마당 (보스 방 16×12, 가운데 BossCenter): 북쪽 담 너머 반쯤 무너진 대웅전 정면,
        // 북쪽 두 모서리에 넘어진 불상과 큰 향로, 동·서 벽가에 깨진 석등 두 쌍. 가운데 싸움터는 비워요
        static Transform BossCourtyard(Transform parent, Material stoneMat, Material woodMat, Material roofX, ref int seed)
        {
            var court = new GameObject("Boss Temple (대웅전 앞마당)").transform;
            court.SetParent(parent, true);   // 자리는 월드 기준 (BossCenter)
            court.position = Vector3.zero; court.rotation = Quaternion.identity;
            Vector3 c = BossCenter;
            float hx = 8f, hz = 6f;

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

            var look = new GameObject("Ink Region Look").AddComponent<AThousandDawns.Art.InkRegionLook>();
            look.Setup(floorMat,
                new Texture[] { null, AssetDatabase.LoadAssetAtPath<Texture2D>($"{TempleArtFolder}/Floor_Temple.png") },
                new[] { bambooGroup, templeGroup },
                new[] { new GameObject("죽림 분위기 (없음)"), air },
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
        static void PineTree(Transform parent, Vector3 foot, float height, int seed, Material woodMat)
        {
            var tree = new GameObject("Pine (소나무)").transform;
            tree.SetParent(parent, false);
            tree.localPosition = foot;
            tree.localRotation = Quaternion.Euler(0f, seed * 67f % 360f, 0f);
            float lean = (seed % 2 == 0 ? 1f : -1f) * (6f + seed % 3 * 4f);
            float h1 = height * 0.55f, h2 = height * 0.45f;
            Block(tree, "Trunk_1", PrimitiveType.Cylinder, new Vector3(0f, h1 * 0.5f, 0f), new Vector3(0.28f, h1 * 0.5f, 0.28f), Quaternion.Euler(0f, 0f, lean * 0.4f), woodMat);
            Vector3 top1 = Quaternion.Euler(0f, 0f, lean * 0.4f) * new Vector3(0f, h1, 0f);
            Vector3 dir2 = Quaternion.Euler(0f, 0f, -lean) * Vector3.up;
            Block(tree, "Trunk_2", PrimitiveType.Cylinder, top1 + dir2 * h2 * 0.5f, new Vector3(0.2f, h2 * 0.5f, 0.2f), Quaternion.Euler(0f, 0f, -lean), woodMat);
            Vector3 crown = top1 + dir2 * h2;
            var mat = GetFoliageBlobMaterial("Bush_B");
            var tex = (Texture2D)mat.GetTexture("_BaseMap");
            float aspect = tex != null ? (float)tex.width / tex.height : 1f;
            Vector3[] puffs = { crown, top1 + new Vector3(0.9f, 0.1f, 0.2f), top1 + new Vector3(-0.8f, -0.4f, -0.1f), crown + new Vector3(0.6f, -0.6f, 0.4f) };
            for (int k = 0; k < puffs.Length; k++)
            {
                var puff = new GameObject($"Needles_{k + 1}");
                puff.transform.SetParent(tree, false);
                puff.transform.localPosition = puffs[k];
                puff.transform.localScale = new Vector3(1.5f - k * 0.15f, 0.55f, 1.5f - k * 0.15f) * 1.2f;   // 납작한 솔잎 구름
                puff.AddComponent<MeshFilter>().sharedMesh = GetBushBlobMesh((seed + k) % 3, aspect);
                puff.AddComponent<MeshRenderer>().sharedMaterial = mat;
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
