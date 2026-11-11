using System.Collections.Generic;
using System.Linq;
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
        static void DressTempleInk(Material stoneMat, Material woodMat, Material roofX)
        {
            var bamboo = new List<GameObject>();
            var temple = new List<GameObject>();
            int seed = 0;

            bool Skip(Transform t)
            {
                for (var p = t; p != null; p = p.parent)
                    if (p.name == "Village" || p.name.StartsWith("Room_Boss") || p.name.Contains("Village")) return true;
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

            var dressing = new GameObject("Ink Region Dressing").AddComponent<AThousandDawns.Art.InkRegionDressing>();
            dressing.SetRegions(new[]
            {
                new AThousandDawns.Art.InkRegionDressing.Set { name = "죽림", objects = bamboo.ToArray() },
                new AThousandDawns.Art.InkRegionDressing.Set { name = "폐사찰", objects = temple.ToArray() },
            });
            foreach (var go in temple) go.SetActive(false);   // 처음(죽림)엔 꺼 둬요
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
