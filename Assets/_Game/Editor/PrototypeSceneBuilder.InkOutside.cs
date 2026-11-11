using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 맵 바깥 땅 (InkOutsideGround): 벽 너머가 흰 종이로 비어 보이지 않게, 모든 방 바닥 아래로 큰 수묵 땅 한 장을 깔아요.
    // 방 바닥(floorMat)과 같은 셰이더에 조금 어두운 색 · 지역별 이음새 없는 땅 그림. 충돌 없음 (보기용).
    public static partial class PrototypeSceneBuilder
    {
        const string OutsideArtFolder = "Assets/_Game/Art/Env/Outside";

        static void DressOutsideInk(Material floorMat, Material bambooMat)
        {
            var paths = new[] { $"{OutsideArtFolder}/Ground_Bamboo.png", $"{OutsideArtFolder}/Ground_Temple.png", $"{OutsideArtFolder}/Ground_River.png" }; // 죽림 · 폐사찰 · 장강 수채
            foreach (var p in paths) ImportAsTiledTexture(p);
            var textures = System.Array.ConvertAll(paths, p => AssetDatabase.LoadAssetAtPath<Texture2D>(p));

            // 방들이 서로 멀리 떨어져 있으니 씬의 모든 것을 감싸는 범위로 (땅 높이는 바닥 재질을 쓰는 것 중 가장 낮은 윗면)
            var bounds = new Bounds();
            bool any = false;
            float floorTop = float.MaxValue;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                if (r.sharedMaterial == floorMat) floorTop = Mathf.Min(floorTop, r.bounds.max.y);
            }
            if (!any) bounds = new Bounds(Vector3.zero, new Vector3(100f, 0f, 100f));
            if (floorTop == float.MaxValue) floorTop = 0f;

            var mat = GetShaderMaterial("Ink_OutsideGround", floorMat.shader.name);
            mat.CopyPropertiesFromMaterial(floorMat);
            // 방 바닥 재질은 종이색 덧칠이 세고(PaperLift 0.55) 흑백이라 땅 그림이 하얗게 날아가요 → 덧칠 줄이고 색을 조금 남기고 어둡게
            mat.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.7f, 1f));
            mat.SetFloat("_PaperLift", 0.12f);
            mat.SetFloat("_Saturation", 0.45f);
            mat.SetFloat("_TileSize", 6f);
            mat.SetFloat("_OutlineWidth", 0f);
            if (textures[0] != null) mat.SetTexture("_BaseMap", textures[0]);
            EditorUtility.SetDirty(mat);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Ink Outside Ground (맵 바깥 땅)";
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.position = new Vector3(bounds.center.x, floorTop - 0.06f, bounds.center.z);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(bounds.size.x + 240f, bounds.size.z + 240f, 1f);
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bamboo = DressOutsideBamboo(bambooMat);
            ground.AddComponent<AThousandDawns.Art.InkOutsideGround>().Setup(textures, bamboo.gameObject);
        }

        // 벽 너머 대나무 (사용자 요청 "대충 넣어 놓으면"): 방 바닥마다 북·동·서쪽 바깥 띠에 대나무 묶음을 듬성듬성.
        // 카메라 쪽(남쪽) 띠는 시야를 가리지 않게 잎 없는 낮은 죽순 줄기만 (방 남쪽 울타리와 같은 방식). 다른 방 바닥 위에는 세우지 않아요. 보기용이라 충돌 없음.
        static Transform DressOutsideBamboo(Material bambooMat)
        {
            // 방 바닥 찾기: 납작하고 넓은 바닥 높이의 덩어리
            var floors = new System.Collections.Generic.List<Bounds>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var b = r.bounds;
                if (b.size.y < 0.8f && b.size.x >= 8f && b.size.z >= 6f && b.size.x < 120f && Mathf.Abs(b.max.y) < 0.4f) floors.Add(b);
            }
            // 키 큰 것(건물·벽·기존 대나무·마을 소품) 자리도 피해요
            var tall = new System.Collections.Generic.List<Bounds>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var b = r.bounds;
                if (b.size.y > 0.8f && b.size.x < 60f && b.size.z < 60f) tall.Add(b);
            }
            bool Blocked(Vector3 p)
            {
                foreach (var t in tall)
                    if (p.x > t.min.x - 0.6f && p.x < t.max.x + 0.6f && p.z > t.min.z - 0.6f && p.z < t.max.z + 0.6f) return true;
                return false;
            }
            bool OnFloor(Vector3 p, float margin)
            {
                foreach (var f in floors)
                    if (p.x > f.min.x - margin && p.x < f.max.x + margin && p.z > f.min.z - margin && p.z < f.max.z + margin) return true;
                return false;
            }

            var root = new GameObject("Ink Outside Bamboo (벽 너머 대나무)").transform;
            var rng = new System.Random(31);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const float Band = 12f, SouthBand = 7f, Step = 2.6f;
            var placed = new System.Collections.Generic.List<Vector3>();
            int seed = 500;
            foreach (var f in floors)
                for (float x = f.min.x - Band; x <= f.max.x + Band; x += Step)
                for (float z = f.min.z - SouthBand; z <= f.max.z + Band; z += Step)
                {
                    var p = new Vector3(x + R(-0.9f, 0.9f), 0f, z + R(-0.9f, 0.9f));
                    if (OnFloor(p, 1.6f)) continue;          // 방 안·벽 바로 옆은 비워요
                    if (Blocked(p)) continue;
                    if (rng.NextDouble() < 0.25) continue;   // 듬성듬성
                    bool near = false;
                    foreach (var q in placed) if ((q - p).sqrMagnitude < Step * Step * 0.5f) { near = true; break; }
                    if (near) continue;
                    placed.Add(p);
                    if (p.z < f.min.z) BambooCluster(root, p, R(1f, 1.6f), seed++, bambooMat, 0f); // 남쪽: 낮은 죽순
                    else BambooCluster(root, p, R(5f, 7.5f), seed++, bambooMat);
                }
            StripColliders(root);
            return root;
        }
    }
}
