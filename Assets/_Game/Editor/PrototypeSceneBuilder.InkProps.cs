using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 구조물 소품: 돌담 기와지붕, 깎인 바위, 끝을 깎은 통나무. 메시는 코드로 만들어 Meshes 폴더에 저장해요.
    // 면마다 꼭짓점을 따로 둬(각진 면) 먹 농담이 면 단위로 끊겨 붓으로 깎아 그린 느낌이 나요.
    public static partial class PrototypeSceneBuilder
    {
        const string InkMeshFolder = "Assets/_Game/Art/Env/BurningBamboo/Meshes";
        const float RoofCapHeight = 0.45f;   // 돌담 위 기와지붕 높이 (월드)
        const float RoofOverhang = 0.18f;    // 담 양옆으로 내민 처마 (월드, 한쪽)

        static readonly Dictionary<string, Mesh> s_inkPropMeshes = new Dictionary<string, Mesh>();

        // 돌담 기와·바위·통나무 재질 (값은 코드가 정해요, 씬을 다시 만들면 덮어씀)
        static (Material roofX, Material roofZ) GetInkRoofMaterials()
        {
            foreach (var name in new[] { "Tex_RoofTile", "Tex_RoofTile_Z" }) ImportAsTiledTexture($"{Env2DArtFolder}/{name}.png");
            // 기와 골이 지붕 경사를 따라 흐르게: 용마루가 x축이면 원본, z축이면 90° 돌린 텍스처
            var x = GetInkTriplanar("Ink3D_RoofX", "Tex_RoofTile", 1.2f, 1.5f);
            var z = GetInkTriplanar("Ink3D_RoofZ", "Tex_RoofTile_Z", 1.2f, 1.5f);
            foreach (var m in new[] { x, z })
            {
                m.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.6f, 1f));
                m.SetFloat("_Saturation", 0f);
                m.SetFloat("_PaperLift", 0f);
                m.SetFloat("_RimInk", 0.4f);
                m.SetFloat("_PatchLight", 0.25f);
                m.SetFloat("_PatchScale", 0.7f);
                m.SetFloat("_OutlineFromCenter", 1f);
            }
            return (x, z);
        }

        // 돌·나무 재질도 대나무처럼 군데군데 옅은 마른 붓 얼룩
        static void TuneInkStoneWood(Material stone, Material wood)
        {
            stone.SetFloat("_PatchLight", 0.25f);
            stone.SetFloat("_PatchScale", 0.6f);
            wood.SetFloat("_StreakLight", 0.4f);
            wood.SetFloat("_StreakScale", 3f);
            wood.SetFloat("_PatchLight", 0.2f);
            wood.SetFloat("_PatchScale", 0.8f);
        }

        // 상자 담(돌벽) 위에 기와지붕을 얹어요. 담의 자식으로 달아 담이 켜지고 꺼질 때(문 막이) 같이 따라가요.
        // 담은 회전 없는 상자라고 봐요. 용마루는 담의 긴 방향.
        static void AddRoofCap(Transform wall, Material roofX, Material roofZ)
        {
            Vector3 s = wall.lossyScale;
            if (s.x <= 0f || s.y <= 0f || s.z <= 0f) return;
            bool alongX = s.x >= s.z;
            var cap = new GameObject("RoofCap (기와)");
            cap.transform.SetParent(wall, false);
            cap.transform.localPosition = new Vector3(0f, 0.5f + RoofCapHeight * 0.5f / s.y, 0f);
            float along = alongX ? s.x : s.z, across = alongX ? s.z : s.x;
            float alongScale = (along + 0.1f) / along, acrossScale = (across + RoofOverhang * 2f) / across;
            cap.transform.localScale = alongX
                ? new Vector3(alongScale, RoofCapHeight / s.y, acrossScale)
                : new Vector3(acrossScale, RoofCapHeight / s.y, alongScale);
            cap.AddComponent<MeshFilter>().sharedMesh = GetRoofCapMesh(alongX);
            cap.AddComponent<MeshRenderer>().sharedMaterial = alongX ? roofX : roofZ;
        }

        // 기와지붕 단면(용마루가 있는 맞배): 가로 -0.5~0.5, 높이 -0.5~0.5, 길이 방향으로 -0.5~0.5 밀어낸 각기둥
        static Mesh GetRoofCapMesh(bool alongX)
        {
            string key = alongX ? "RoofCapX" : "RoofCapZ";
            if (s_inkPropMeshes.TryGetValue(key, out var cached) && cached != null) return cached;

            // (가로, 높이) 단면: 처마 끝 → 경사 → 용마루 → 반대편
            Vector2[] section =
            {
                new Vector2(-0.5f, -0.5f), new Vector2(-0.5f, -0.36f), new Vector2(-0.08f, 0.36f), new Vector2(-0.08f, 0.5f),
                new Vector2(0.08f, 0.5f), new Vector2(0.08f, 0.36f), new Vector2(0.5f, -0.36f), new Vector2(0.5f, -0.5f),
            };
            Vector3 P(Vector2 c, float t) => alongX ? new Vector3(t, c.y, c.x) : new Vector3(c.x, c.y, t);

            var b = new FlatMeshBuilder(Vector3.zero);
            for (int i = 0; i < section.Length; i++)
            {
                var a = section[i];
                var c = section[(i + 1) % section.Length];
                b.Quad(P(a, -0.5f), P(a, 0.5f), P(c, 0.5f), P(c, -0.5f));
            }
            // 양 끝 박공: 단면 가운데에서 부채꼴로
            var mid = new Vector2(0f, -0.1f);
            for (int i = 0; i < section.Length; i++)
            {
                var a = section[i];
                var c = section[(i + 1) % section.Length];
                b.Tri(P(mid, 0.5f), P(a, 0.5f), P(c, 0.5f));
                b.Tri(P(mid, -0.5f), P(c, -0.5f), P(a, -0.5f));
            }
            return SaveInkPropMesh(key, b);
        }

        // 깎인 바위: 정이십면체를 한 번 쪼갠 뒤 노이즈로 울퉁불퉁하게, 바닥은 평평하게. 발밑이 원점, 지름 약 1
        static Mesh GetRockMesh(int variant)
        {
            string key = $"Rock_{variant}";
            if (s_inkPropMeshes.TryGetValue(key, out var cached) && cached != null) return cached;

            var rnd = new System.Random(variant * 131 + 7);
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // 한 번 쪼개기 (모서리 가운데 점 공유)
            var midCache = new Dictionary<long, int>();
            int Mid(int a, int c)
            {
                long k = a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
                if (midCache.TryGetValue(k, out int idx)) return idx;
                verts.Add((verts[a] + verts[c]) * 0.5f);
                midCache[k] = verts.Count - 1;
                return verts.Count - 1;
            }
            var tris = new List<int>();
            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i], c = faces[i + 1], d = faces[i + 2];
                int ab = Mid(a, c), bc = Mid(c, d), ca = Mid(d, a);
                tris.AddRange(new[] { a, ab, ca, c, bc, ab, d, ca, bc, ab, bc, ca });
            }
            // 공 모양으로 편 뒤 꼭짓점마다 반지름을 흔들고, 납작하게 눌러 바닥을 평평하게
            var stretch = new Vector3(1f + (float)rnd.NextDouble() * 0.4f, 0.7f + (float)rnd.NextDouble() * 0.25f, 0.85f + (float)rnd.NextDouble() * 0.3f);
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i].normalized * (0.75f + (float)rnd.NextDouble() * 0.35f);
                v = Vector3.Scale(v, stretch) * 0.5f;
                v.y = Mathf.Max(v.y, -0.15f) + 0.15f;
                verts[i] = v;
            }
            var b = new FlatMeshBuilder(new Vector3(0f, 0.25f, 0f));
            for (int i = 0; i < tris.Count; i += 3) b.Tri(verts[tris[i]], verts[tris[i + 2]], verts[tris[i + 1]]);
            return SaveInkPropMesh(key, b);
        }

        // 끝을 뾰족하게 깎은 통나무 (목책): 지름 1, 키 1, 발밑이 원점. 위 15%가 깎인 끝
        static Mesh GetSharpLogMesh()
        {
            const string key = "SharpLog";
            if (s_inkPropMeshes.TryGetValue(key, out var cached) && cached != null) return cached;
            const int sides = 8;
            var b = new FlatMeshBuilder(new Vector3(0f, 0.5f, 0f));
            var tip = new Vector3(0.08f, 1f, 0f); // 살짝 비스듬히 깎은 끝
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                var bottom0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                var bottom1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                var top0 = bottom0 + Vector3.up * 0.85f;
                var top1 = bottom1 + Vector3.up * 0.85f;
                b.Quad(bottom0, top0, top1, bottom1);
                b.Tri(top0, tip, top1);
            }
            return SaveInkPropMesh(key, b);
        }

        static Mesh SaveInkPropMesh(string key, FlatMeshBuilder b)
        {
            string path = $"{InkMeshFolder}/{key}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = key };
            b.WriteTo(mesh);
            if (isNew)
            {
                EnsureFolder(InkMeshFolder);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);
            s_inkPropMeshes[key] = mesh;
            return mesh;
        }

        // 각진 면 메시: 면마다 꼭짓점을 따로 둬 법선이 면 단위.
        // 안쪽 기준점(inside)에서 면 쪽으로 향하게 삼각형 감는 방향을 알아서 맞춰요 (안팎이 뒤집혀 보이지 않게)
        sealed class FlatMeshBuilder
        {
            readonly List<Vector3> _verts = new List<Vector3>();
            readonly List<int> _tris = new List<int>();
            readonly Vector3 _inside;

            public FlatMeshBuilder(Vector3 inside) => _inside = inside;

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-10f) return;
                if (Vector3.Dot(normal, (a + b + c) / 3f - _inside) < 0f) (b, c) = (c, b);
                int i = _verts.Count;
                _verts.Add(a); _verts.Add(b); _verts.Add(c);
                _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public void WriteTo(Mesh mesh)
            {
                mesh.Clear();
                mesh.SetVertices(_verts);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
        }
    }
}
