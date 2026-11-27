using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 나무 (죽림 시안 2차·뒤틀린 나무 시안 확정, 2026-11-23): 잎은 죽림 대나무와 같은 그림 언어 — 잎다발(앞뒤 면 잎 카드를 잔가지에 단 것)
    //   + 덩어리 속 채움, 재질은 대나무 잎과 같은 구조물 셰이더(InkTriplanar)·잎 그림(Tex_LeafPaint)에 색만 나무마다.
    //   - 판타지 참나무·전나무 (VARCO): 줄기는 VARCO 모델의 갈색 면 그대로, 잎 덩어리만 잎다발로 다시 (참나무 = 넓은잎 공 일곱, 전나무 = 처진 바늘잎 층)
    //   - 폐사찰 소나무: 줄기·가지는 Blender에서 한 몸으로 (Trees/Tools~/treeskel.py → JSON), 솔잎 층은 속 채움 없이 성글게 (대나무처럼 가볍게)
    //   - 뒤틀린 나무 (지역이 깊어질수록 많아져요: 죽림 10% → 폐사찰 23% → 장강 37% → 천마총 50%):
    //       죽림 = 죽참나무(대나무 줄기가 참나무로) · 세 갈래(대나무·참나무·전나무 줄기가 감김), 폐사찰 = 소참나무(소나무 모양에 참나무 잎 층),
    //       장강 = 늘어진 전나무(가지 끝에서 버들가지), 천마총 = 마른 고목(잔가지 끝에 바늘잎 술)
    //   나무 하나 = 메시 하나(부위마다 하위 메시) + 렌더러 하나. 메시는 Art/Env/Trees에 저장 (빌드마다 같은 값으로 덮어써요)
    public static partial class PrototypeSceneBuilder
    {
        const string TreeFolder = "Assets/_Game/Art/Env/Trees";
        const string TreeSourceFolder = "Assets/_Game/Art/Env/Trees/Tools~";

        // 지역별 뒤틀린 나무 비율: 첫 지역 10% → 마지막 지역 50% (사용자 결정)
        static float TwistedRatio(int region) => 0.1f + 0.4f * region / 3f;

        // 자리마다 고정 난수 (0~1): 빌드마다 같은 자리가 같은 나무
        static float SlotHash(int i, int salt)
        {
            uint h = (uint)i * 0x9E3779B1u + (uint)salt * 0x85EBCA77u;   // 자리 번호가 규칙적이어도(9칸마다 망루 등) 고르게 섞이게 murmur 마무리
            h ^= h >> 16; h *= 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
            return (h >> 8) / 16777216f;
        }

        static readonly Dictionary<string, (Mesh mesh, Material[] mats)> s_trees = new Dictionary<string, (Mesh, Material[])>();

        // 장면에 나무 하나 (발밑 기준). kind: "Pine_0..3", "BambooOak", "Braid", "PineOak", "WillowFir", "DeadFir"
        static Transform PlaceTree(Transform parent, string kind, string label, Vector3 foot, float yaw, float scale)
        {
            var t = GetTree(kind);
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = foot;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = t.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = t.mats;
            return go.transform;
        }

        static Transform TwistedTree(Transform parent, Vector3 foot, int region, int slot)
        {
            string kind = region == 0 ? (slot % 2 == 0 ? "BambooOak" : "Braid") : region == 1 ? "PineOak" : region == 2 ? "WillowFir" : "DeadFir";
            return PlaceTree(parent, kind, $"Twisted Tree (뒤틀린 나무 · {kind})", foot, SlotHash(slot, 11) * 360f, 0.9f + 0.3f * SlotHash(slot, 12));
        }

        // ── 재질 ──
        static Material TreeBark(string name, Color tint)
        {
            ImportAsTiledTexture($"{Env2DArtFolder}/Tex_TrunkBrush.png");
            var m = GetInkTriplanar(name, "Tex_TrunkBrush", 1.2f, 1f);   // 줄기 붓결 (대나무 줄기 그림처럼 세로 붓 자국, 마디 없음)
            m.SetFloat("_OutlineFromCenter", 0f);
            m.SetFloat("_OutlineWidth", 1f);
            m.SetFloat("_VertexInk", 0f);
            m.SetFloat("_PaperLift", 0f);
            m.SetFloat("_Saturation", 0f);
            m.SetFloat("_RimInk", 0.55f);
            m.SetFloat("_RimPower", 2f);
            m.SetFloat("_Bands", 3f);
            m.SetFloat("_BandSoftness", 0.3f);
            m.SetColor("_BaseColor", new Color(0.53f * tint.r, 0.53f * tint.g, 0.53f * tint.b, 1f));
            m.SetFloat("_StreakLight", 0.6f);
            m.SetFloat("_StreakScale", 8f);
            m.SetFloat("_PatchLight", 0.45f);
            m.SetFloat("_PatchScale", 0.9f);
            m.SetColor("_InkColor", new Color(0.07f, 0.1f, 0.07f, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // 잎: 대나무 잎 재질(LeafMaterial)과 같은 값에 색만 (밝은 잎 · 그늘 잎 · 속 채움 세 톤)
        static Material TreeLeaf(string name, Color tint, float mul)
        {
            LeafPaintTexture();
            var m = StalkStyle(GetInkTriplanar(name, "Tex_LeafPaint", 0.5f, 0f), 0.3f, 0f);
            m.SetFloat("_ShadowReceive", 0f);
            m.SetFloat("_StreakLight", 0f);
            m.SetFloat("_PatchLight", 0f);
            m.SetFloat("_WashNoiseStrength", 0f);
            m.SetFloat("_PaperGrain", 0f);
            m.SetColor("_BaseColor", new Color(0.53f * tint.r * mul, 0.53f * tint.g * mul, 0.53f * tint.b * mul, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material[] LeafSet(string name, Color tint, float lit, float shade, float core) =>
            new[] { TreeLeaf($"Ink3D_Tree{name}_Lit", tint, lit), TreeLeaf($"Ink3D_Tree{name}_Shade", tint, shade), TreeLeaf($"Ink3D_Tree{name}_Core", tint, core) };

        static Material[] OakLeaves() => LeafSet("Oak", new Color(1.02f, 1f, 0.85f), 1.2f, 0.95f, 0.8f);
        static Material[] FirLeaves() => LeafSet("Fir", new Color(0.68f, 0.88f, 0.92f), 1.05f, 0.82f, 0.62f);
        static Material[] DarkFirLeaves() => LeafSet("DarkFir", new Color(0.55f, 0.72f, 0.74f), 0.85f, 0.66f, 0.5f);
        static Material[] PineLeaves() => LeafSet("Pine", new Color(0.8f, 0.95f, 0.85f), 1.05f, 0.85f, 0.7f);
        static Material[] WillowLeaves() => LeafSet("Willow", new Color(1.03f, 1.04f, 0.72f), 1.25f, 1.0f, 0.9f);
        static Material BrownBark() => TreeBark("Ink3D_TreeBark", Color.white);
        static Material PineBark() => TreeBark("Ink3D_TreeBarkPine", new Color(1.1f, 0.78f, 0.7f));
        static Material DeadBark() => TreeBark("Ink3D_TreeBarkDead", new Color(0.55f, 0.45f, 0.55f));

        // ── 나무 짓기: 부위(MB)마다 하위 메시 세 칸(0 밝은 잎·줄기, 1 그늘 잎·지역 껍질, 2 속 채움)을 재질과 짝지어 메시 하나로 ──
        static (Mesh mesh, Material[] mats) GetTree(string kind)
        {
            if (s_trees.TryGetValue(kind, out var cached) && cached.mesh != null) return cached;
            var parts = new List<(TreeKit.MB mb, Material[] mats)>();
            TreeKit.Seed(kind.Aggregate(17, (h, ch) => h * 31 + ch));   // 이름에서 고정 씨앗 (빌드마다 같은 모양)
            var trunk = new TreeKit.MB();
            switch (kind)
            {
                case "BambooOak":
                case "Braid":
                {
                    var oak = new TreeKit.MB(); var fir = new TreeKit.MB(); var leaf = new TreeKit.MB();
                    if (kind == "BambooOak") TreeKit.BambooOak(trunk, oak, leaf, LeafSprayMesh); else TreeKit.Braid(trunk, oak, fir, leaf, LeafSprayMesh);
                    parts.Add((trunk, new[] { BrownBark(), GetInkTriplanar("Ink3D_Bamboo", "Tex_BambooStalk", 1.2f, 2f), BrownBark() }));
                    parts.Add((oak, OakLeaves())); parts.Add((fir, FirLeaves()));
                    var lm = LeafMaterial(); parts.Add((leaf, new[] { lm, lm, lm }));
                    break;
                }
                case "PineOak":
                {
                    var oak = new TreeKit.MB(); var pine = new TreeKit.MB();
                    var tj = LoadTreeSkeleton("pineoak_7");
                    trunk.Raw(tj.v, tj.n, tj.t, 1);
                    foreach (var tip in tj.tips) TreeKit.OakPad(oak, pine, tip.P, tip.w);
                    parts.Add((trunk, new[] { BrownBark(), PineBark(), BrownBark() }));
                    parts.Add((oak, OakLeaves())); parts.Add((pine, PineLeaves()));
                    break;
                }
                case "WillowFir":
                {
                    var fir = new TreeKit.MB(); var willow = new TreeKit.MB();
                    var tj = LoadTreeSkeleton("willowfir_3");
                    trunk.Raw(tj.v, tj.n, tj.t, 0);
                    foreach (var tip in tj.tips) TreeKit.WeepTip(fir, willow, tip);
                    parts.Add((trunk, new[] { BrownBark(), BrownBark(), BrownBark() }));
                    parts.Add((fir, FirLeaves())); parts.Add((willow, WillowLeaves()));
                    break;
                }
                case "DeadFir":
                {
                    var fir = new TreeKit.MB();
                    var tj = LoadTreeSkeleton("deadfir_5");
                    trunk.Raw(tj.v, tj.n, tj.t, 1);
                    foreach (var tip in tj.tips) TreeKit.NeedleTuft(fir, tip);
                    parts.Add((trunk, new[] { BrownBark(), DeadBark(), BrownBark() }));
                    parts.Add((fir, DarkFirLeaves()));
                    break;
                }
                default:   // Pine_0..3
                {
                    var pine = new TreeKit.MB();
                    var tj = LoadTreeSkeleton("pine_" + (int.Parse(kind.Substring(5)) + 1));
                    trunk.Raw(tj.v, tj.n, tj.t, 1);
                    foreach (var tip in tj.tips) TreeKit.AiryPinePad(pine, tip.P, tip.w);
                    parts.Add((trunk, new[] { BrownBark(), PineBark(), BrownBark() }));
                    parts.Add((pine, PineLeaves()));
                    break;
                }
            }
            var result = SaveTreeMesh("Tree_" + kind, parts, Matrix4x4.identity);
            s_trees[kind] = result;
            return result;
        }

        // 부위들을 메시 하나로 (빈 하위 메시는 빼요) → Art/Env/Trees/{key}.asset
        static (Mesh mesh, Material[] mats) SaveTreeMesh(string key, List<(TreeKit.MB mb, Material[] mats)> parts, Matrix4x4 toLocal, List<Vector3> extraV = null, List<Vector3> extraN = null, List<Vector2> extraUv = null, List<int> extraT = null, Material extraMat = null)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var subs = new List<List<int>>(); var mats = new List<Material>();
            if (extraV != null)
            {
                v.AddRange(extraV); n.AddRange(extraN); uv.AddRange(extraUv);
                subs.Add(new List<int>(extraT)); mats.Add(extraMat);
            }
            foreach (var (mb, pm) in parts)
            {
                int b = v.Count;
                v.AddRange(mb.v.Select(p => toLocal.MultiplyPoint3x4(p)));
                n.AddRange(mb.n.Select(d => toLocal.MultiplyVector(d).normalized));
                uv.AddRange(Enumerable.Repeat(Vector2.zero, mb.v.Count));
                for (int s = 0; s < 3; s++)
                {
                    if (mb.sub[s].Count == 0) continue;
                    subs.Add(mb.sub[s].Select(i => i + b).ToList()); mats.Add(pm[s]);
                }
            }
            string path = $"{TreeFolder}/{key}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = key };
            mesh.Clear();
            mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateBounds();
            if (isNew) { EnsureFolder(TreeFolder); AssetDatabase.CreateAsset(mesh, path); }
            else EditorUtility.SetDirty(mesh);
            return (mesh, mats.ToArray());
        }

        [System.Serializable] public class TreeTip { public float[] p, d; public float w; public string k; public Vector3 P => new Vector3(p[0], p[1], p[2]); public Vector3 D => new Vector3(d[0], d[1], d[2]); }
        [System.Serializable] public class TreeSkeleton { public float[] v, n; public int[] t; public TreeTip[] tips; }
        // Blender에서 지은 줄기 (Trees/Tools~/treeskel.py: 뼈대 → 마디 공·원뿔 → Voxel Remesh로 한 몸 → 껍질 골 → 줄이기). tips = 잎이 붙는 가지 끝
        static TreeSkeleton LoadTreeSkeleton(string n) => JsonUtility.FromJson<TreeSkeleton>(File.ReadAllText($"{TreeSourceFolder}/{n}.json"));

        // ── 판타지 참나무·전나무 (VARCO): 줄기는 모델의 갈색 면 그대로, 잎 덩어리는 잎다발로 다시 ──
        //   모델 텍스처에서 초록인 면 = 잎 덩어리 (자리·크기만 읽고 버려요), 회색 조각은 버리고, 잎 속에 묻히는 줄기 면도 버려요
        static readonly Dictionary<string, (Mesh mesh, Material[] mats)> s_varcoTrees = new Dictionary<string, (Mesh, Material[])>();

        static void ApplyVarcoTreeLeaves(Transform tree, string name)
        {
            var mf = tree.GetComponentInChildren<MeshFilter>(true);
            if (mf == null) return;
            if (!s_varcoTrees.TryGetValue(name, out var built) || built.mesh == null)
            {
                // 기준 자세: VarcoProp의 눕힘 보정(X 90°)만 (장면 자리·방향·크기는 빼요 — 메시가 뿌리에 바로 붙어 있어 그 값까지 넣었더니 잎이 어긋났어요)
                var local = mf.transform == tree ? Matrix4x4.identity : Matrix4x4.TRS(mf.transform.localPosition, mf.transform.localRotation, mf.transform.localScale);
                built = BuildVarcoTree(name, mf.sharedMesh, Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)) * local);
                s_varcoTrees[name] = built;
            }
            mf.sharedMesh = built.mesh;
            mf.GetComponent<MeshRenderer>().sharedMaterials = built.mats;
        }

        static (Mesh, Material[]) BuildVarcoTree(string name, Mesh src, Matrix4x4 M)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes($"{BambooFantasyFolder}/{name}_Albedo.png"));
            var uv = src.uv; var vs = src.vertices; var tri = src.triangles; var nrm = src.normals;
            var keep = new List<int>(); var green = new List<Vector3>();
            for (int i = 0; i < tri.Length; i += 3)
            {
                var uvc = (uv[tri[i]] + uv[tri[i + 1]] + uv[tri[i + 2]]) / 3f;
                var c = tex.GetPixelBilinear(uvc.x, uvc.y);
                if (c.r < c.b * 1.15f && c.g <= c.r * 1.05f) continue;
                if (c.g > c.r * 1.05f) { for (int k = 0; k < 3; k++) green.Add(M.MultiplyPoint3x4(vs[tri[i + k]])); }
                else { keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]); }
            }
            Object.DestroyImmediate(tex);
            TreeKit.Seed(name.Length * 977);
            var mb = new TreeKit.MB();
            System.Func<Vector3, bool> hide;
            Material[] leafMats;
            var allC = green.Aggregate(Vector3.zero, (a, b) => a + b) / green.Count;
            if (name == "OakTree")
            {
                const int K = 7;   // 잎 덩어리 → 공 일곱 (k-평균), 공마다 짙은 속 + 겉에 넓은잎 다발
                var cs = new List<Vector3> { green[0] };
                while (cs.Count < K) cs.Add(green.OrderByDescending(p => cs.Min(c => (p - c).sqrMagnitude)).First());
                var asg = new int[green.Count];
                for (int it = 0; it < 12; it++)
                {
                    for (int i = 0; i < green.Count; i++) { float best = 1e9f; for (int k = 0; k < K; k++) { float d = (green[i] - cs[k]).sqrMagnitude; if (d < best) { best = d; asg[i] = k; } } }
                    for (int k = 0; k < K; k++) { var mem = Enumerable.Range(0, green.Count).Where(i => asg[i] == k).Select(i => green[i]).ToList(); if (mem.Count > 0) cs[k] = mem.Aggregate(Vector3.zero, (a, b) => a + b) / mem.Count; }
                }
                var rs = new float[K];
                for (int k = 0; k < K; k++)
                {
                    var d = Enumerable.Range(0, green.Count).Where(i => asg[i] == k).Select(i => (green[i] - cs[k]).magnitude).OrderBy(x => x).ToList();
                    rs[k] = d.Count == 0 ? 0f : d[(int)(d.Count * 0.85f)];
                }
                hide = q => Enumerable.Range(0, K).Any(k => (q - cs[k]).magnitude < rs[k] * 0.76f);
                var lobes = Enumerable.Range(0, K).Select(k => (cs[k], rs[k])).ToList();
                for (int k = 0; k < K; k++) TreeKit.OakLobe(mb, cs[k], rs[k], lobes, allC, k);
                leafMats = OakLeaves();
            }
            else
            {
                var axis = allC;   // 전나무: 원뿔 잎의 가운데 = 줄기 축. 층 넷, 층마다 짙은 속 원뿔 + 처진 바늘잎 다발
                float h0 = green.Min(p => p.y), h1 = green.Max(p => p.y);
                hide = q => q.y > h0 + 0.3f && new Vector2(q.x - axis.x, q.z - axis.z).magnitude > 0.45f;
                const int T = 4;
                float sp = (h1 - h0) / (T + 0.4f);
                float RadAt(float lo, float hi)
                {
                    var d = green.Where(p => p.y >= lo && p.y <= hi).Select(p => new Vector2(p.x - axis.x, p.z - axis.z).magnitude).OrderBy(x => x).ToList();
                    return d.Count == 0 ? 0.2f : d[(int)(d.Count * 0.95f)];
                }
                float rMax = RadAt(h0, h1);
                for (int i = 0; i < T; i++) { float bottom = h0 + sp * i * 0.95f; TreeKit.FirTier(mb, axis, bottom, sp, RadAt(bottom - sp * 0.2f, bottom + sp * 0.5f), rMax, i); }
                TreeKit.FirTop(mb, axis, h1, sp);
                leafMats = FirLeaves();
            }
            // 줄기: 갈색 면만 (잎에 묻히는 면 빼고) — 모델 공간 그대로, UV도 그대로
            var map = new Dictionary<int, int>(); var tv = new List<Vector3>(); var tn = new List<Vector3>(); var tuv = new List<Vector2>(); var tt = new List<int>();
            for (int q = 0; q < keep.Count; q += 3)
            {
                if (hide(M.MultiplyPoint3x4((vs[keep[q]] + vs[keep[q + 1]] + vs[keep[q + 2]]) / 3f))) continue;
                for (int e = 0; e < 3; e++) { int i = keep[q + e]; if (!map.TryGetValue(i, out var j)) { j = tv.Count; map[i] = j; tv.Add(vs[i]); tn.Add(nrm[i]); tuv.Add(uv[i]); } tt.Add(j); }
            }
            return SaveTreeMesh("Tree_Varco" + name, new List<(TreeKit.MB, Material[])> { (mb, leafMats) }, M.inverse, tv, tn, tuv, tt, BrownBark());
        }

        // ── 잎·줄기 모양 도구 (시안에서 다듬은 그대로, 정점은 시안보다 줄였어요) ──
        static class TreeKit
        {
            static System.Random rnd = new System.Random(1);
            public static void Seed(int s) => rnd = new System.Random(s);
            static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // 잎 메시 (위 = +Y). 하위 메시 0 = 밝은 잎(줄기), 1 = 그늘 잎(지역 껍질), 2 = 속 채움
            public sealed class MB
            {
                public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
                public readonly List<int>[] sub = { new List<int>(), new List<int>(), new List<int>() };
                // 앞뒤 두 면 잎 카드: 면 법선을 위쪽(shadeN)으로 기울여 앞뒤 모두 빛을 받게 (대나무 잎과 같은 방법)
                public void Poly2(Vector3[] p, Vector3 shadeN, int s)
                {
                    var face = Vector3.Cross(p[1] - p[0], p[p.Length - 1] - p[0]).normalized;
                    for (int side = 0; side < 2; side++)
                    {
                        int b = v.Count;
                        var nn = ((side == 0 ? face : -face) * 0.25f + shadeN).normalized;
                        foreach (var q in p) { v.Add(q); n.Add(nn); }
                        for (int k = 1; k + 1 < p.Length; k++)
                            if (side == 0) sub[s].AddRange(new[] { b, b + k, b + k + 1 });
                            else sub[s].AddRange(new[] { b, b + k + 1, b + k });
                    }
                }
                public void Solid(List<Vector3> pts, List<int> tris, Vector3 center, int s)
                {
                    int b = v.Count;
                    foreach (var p in pts) { v.Add(p); n.Add((p - center).normalized); }
                    foreach (var t in tris) sub[s].Add(b + t);
                }
                public void Raw(float[] pv, float[] pn, int[] pt, int s)
                {
                    int b = v.Count;
                    for (int i = 0; i < pv.Length; i += 3) { v.Add(new Vector3(pv[i], pv[i + 1], pv[i + 2])); n.Add(new Vector3(pn[i], pn[i + 1], pn[i + 2])); }
                    foreach (var t in pt) sub[s].Add(b + t);
                }
            }

            // 속 채움 공 (이십면체를 두 번 나눈 162점)
            public static void Ico(float r, Vector3 c, Vector3 squash, out List<Vector3> pts, out List<int> tris)
            {
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var p = new List<Vector3> { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
                    new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
                var f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b) { long k = a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a; if (cache.TryGetValue(k, out var i)) return i; p.Add((p[a] + p[b]) * 0.5f); cache[k] = p.Count - 1; return p.Count - 1; }
                for (int lv = 0; lv < 2; lv++)
                {
                    var f2 = new List<int>();
                    for (int i = 0; i < f.Count; i += 3)
                    {
                        int a = f[i], b = f[i + 1], cc = f[i + 2], ab = Mid(a, b), bc = Mid(b, cc), ca = Mid(cc, a);
                        f2.AddRange(new[] { a, ab, ca, b, bc, ab, cc, ca, bc, ab, bc, ca });
                    }
                    f = f2;
                }
                pts = p.Select(q => c + Vector3.Scale(q.normalized * r, squash)).ToList(); tris = f;
            }

            // 참나무 잎다발: 잔가지 하나에 넓은 마름모 잎 일곱 장 (어긋나기 + 끝잎)
            public static void OakSpray(MB mb, Vector3 p, Vector3 o, float s, int sub, Vector3 shadeN)
            {
                var tw = (o + Vector3.down * 0.25f).normalized;
                var sx = Vector3.Cross(tw, Vector3.up); sx = sx.sqrMagnitude < 1e-4f ? Vector3.right : sx.normalized;
                Vector3 a = p - tw * s * 0.25f, b = p + tw * s * 0.45f;
                const int leaves = 7;
                for (int l = 0; l < leaves; l++)
                {
                    bool tip = l == leaves - 1;
                    float u = tip ? 1f : 0.15f + 0.8f * l / (leaves - 1);
                    var at = Vector3.Lerp(a, b, u);
                    var dir = tip ? tw : (tw * 0.55f + sx * (l % 2 == 0 ? 1f : -1f) * 0.8f + Vector3.up * R(-0.15f, 0.25f) + o * 0.25f).normalized;
                    float L = s * R(0.34f, 0.44f) * (tip ? 1.1f : 1f), W = L * R(0.5f, 0.6f);
                    var side = Vector3.Cross(dir, (o + Vector3.up * 0.4f).normalized).normalized * W * 0.5f;
                    var belly = at + dir * L * 0.45f;
                    mb.Poly2(new[] { at, belly + side, at + dir * L + Vector3.down * L * R(0.05f, 0.18f), belly - side }, shadeN, sub);
                }
            }

            // 전나무 바늘잎 다발: 처진 잔가지 양쪽으로 가는 바늘잎(삼각형) 아홉 쌍
            public static void FirSpray(MB mb, Vector3 p, Vector3 o, float len, int sub, Vector3 shadeN)
            {
                var sx = Vector3.Cross(o, Vector3.up); sx = sx.sqrMagnitude < 1e-4f ? Vector3.right : sx.normalized;
                var b = p + o * len;
                var w = Vector3.up * len * 0.02f;
                mb.Poly2(new[] { p - w, b - w, b + w, p + w }, shadeN, sub);
                const int pairs = 9;
                for (int k = 0; k < pairs; k++)
                {
                    float u = 0.12f + 0.88f * k / (pairs - 1);
                    var at = Vector3.Lerp(p, b, u);
                    float nl = len * 0.3f * (1f - 0.4f * u);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        var dir = (sx * sgn * 0.55f + Vector3.down * 0.75f + o * 0.35f + new Vector3(R(-0.1f, 0.1f), 0, R(-0.1f, 0.1f))).normalized;
                        var wd = Vector3.Cross(dir, sx).normalized * nl * 0.16f;
                        var off = o * nl * 0.06f;
                        mb.Poly2(new[] { at - off, at + dir * nl, at + off + wd }, shadeN, sub);
                    }
                }
            }

            // 솔잎 다발: 바늘잎이 위·옆으로 부채처럼 (다섯 쌍)
            public static void PineSpray(MB mb, Vector3 p, Vector3 o, float len, int sub, Vector3 shadeN)
            {
                var sx = Vector3.Cross(o, Vector3.up); sx = sx.sqrMagnitude < 1e-4f ? Vector3.right : sx.normalized;
                var b = p + o * len;
                var w = Vector3.up * len * 0.02f;
                mb.Poly2(new[] { p - w, b - w, b + w, p + w }, shadeN, sub);
                for (int k = 0; k < 5; k++)
                {
                    float u = 0.2f + 0.8f * k / 4f;
                    var at = Vector3.Lerp(p, b, u);
                    float nl = len * 0.38f * (1f - 0.3f * u);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        var dir = (sx * sgn * 0.5f + Vector3.up * 0.6f + o * 0.45f + new Vector3(R(-0.1f, 0.1f), 0, R(-0.1f, 0.1f))).normalized;
                        var wd = Vector3.Cross(dir, sx).normalized * nl * 0.15f;
                        mb.Poly2(new[] { at - o * nl * 0.05f, at + dir * nl, at + o * nl * 0.05f + wd }, shadeN, sub);
                    }
                }
            }

            // 참나무 잎 공: 짙은 속 + 겉에 넓은잎 다발 (다른 공 속·바닥 쪽은 건너뛰어요)
            public static void OakLobe(MB mb, Vector3 c, float r, List<(Vector3 c, float r)> lobes, Vector3 treeC, int k)
            {
                if (r < 0.05f) return;
                Ico(r * 0.8f, c, new Vector3(1f, 0.92f, 1f), out var ip, out var it2);
                mb.Solid(ip, it2, c, 2);
                const int per = 18;
                for (int i = 0; i < per; i++)
                {
                    float yy = 1f - 2f * (i + 0.5f) / per, rr = Mathf.Sqrt(1f - yy * yy), ph = i * 2.39996f + k;
                    var dir = new Vector3(Mathf.Cos(ph) * rr, yy, Mathf.Sin(ph) * rr);
                    if (dir.y < -0.55f) continue;
                    var at = c + dir * r * 0.72f;
                    if (lobes.Any(l => l.c != c && (at - l.c).magnitude < l.r * 0.9f)) continue;
                    var outward = ((at - treeC).normalized * 0.6f + dir * 0.6f).normalized;
                    OakSpray(mb, at, dir, r * 0.9f, dir.y < -0.2f ? 1 : 0, (outward * 0.55f + Vector3.up * 0.8f).normalized);
                }
            }

            // 전나무 층: 짙은 속 원뿔 + 축에서 아래로 처진 바늘잎 다발을 빙 둘러요 (끝이 처진 치마)
            public static void FirTier(MB mb, Vector3 axis, float bottom, float sp, float R0, float rMax, int i)
            {
                var apex = new Vector3(axis.x, bottom + sp * 1.35f, axis.z);
                var cp = new List<Vector3> { apex }; var ct = new List<int>();
                for (int s = 0; s < 12; s++) { float a = s * Mathf.PI / 6f; cp.Add(new Vector3(axis.x + Mathf.Cos(a) * R0 * 0.8f, bottom + sp * 0.05f, axis.z + Mathf.Sin(a) * R0 * 0.8f)); }
                for (int s = 0; s < 12; s++) ct.AddRange(new[] { 0, 1 + (s + 1) % 12, 1 + s });
                mb.Solid(cp, ct, new Vector3(axis.x, bottom - sp, axis.z), 1);
                int m = Mathf.RoundToInt(7f + 6f * R0 / rMax);
                for (int s = 0; s < m; s++)
                {
                    float a = (s + (i % 2) * 0.5f) * Mathf.PI * 2f / m + R(-0.12f, 0.12f);
                    var hor = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var start = apex + Vector3.down * sp * 0.12f + hor * R0 * 0.08f;
                    var end = new Vector3(axis.x, bottom - sp * 0.15f, axis.z) + hor * R0;
                    FirSpray(mb, start, (end - start).normalized, (end - start).magnitude, 0, (hor * 0.55f + Vector3.up * 0.8f).normalized);
                }
            }

            public static void FirTop(MB mb, Vector3 axis, float h1, float sp) =>   // 꼭대기: 위로 솟은 다발 (뾰족한 끝)
                FirSpray(mb, new Vector3(axis.x, h1 - sp * 0.6f, axis.z), Vector3.up, sp * 0.9f, 0, Vector3.up);

            static void FirTiers(MB fir, Vector3 axis, float h0, float h1, float rBottom, int T)
            {
                float sp = (h1 - h0) / (T + 0.4f);
                for (int i = 0; i < T; i++) FirTier(fir, axis, h0 + sp * i * 0.95f, sp, rBottom * (1f - 0.22f * i), rBottom, i);
                FirTop(fir, axis, h1, sp);
            }

            // 대나무 잎처럼 가벼운 솔잎 층: 속 채움 없이, 바깥 고리·안쪽 고리·윗면에 솔잎 다발을 성글게
            public static void AiryPinePad(MB mb, Vector3 c, float w)
            {
                foreach (var (ring, count, lift) in new[] { (0.42f, 9 + (int)(w * 3f), 0f), (0.2f, 5 + (int)(w * 2f), 0.06f) })
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * Mathf.PI * 2f / count + ring * 7f + R(-0.15f, 0.15f);
                        var hor = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        var at = c + new Vector3(hor.x * w * ring * 0.8f, lift * w + R(-0.03f, 0.05f) * w, hor.z * w * ring * 0.7f);
                        PineSpray(mb, at, (hor + Vector3.up * 0.1f).normalized, w * 0.36f, i % 4 == 0 ? 1 : 0, (hor * 0.45f + Vector3.up * 0.9f).normalized);
                    }
                for (int i = 0; i < 3 + (int)w; i++)
                {
                    float a = i * 2.39996f; var hor = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    PineSpray(mb, c + Vector3.up * w * 0.1f + hor * w * 0.08f, (hor * 0.6f + Vector3.up * 0.6f).normalized, w * 0.28f, 0, Vector3.up);
                }
            }

            // 참나무 잎 층 (소나무 모양의 층을 넓은잎으로): 얇은 그늘 톤 속 + 둘레·윗면 넓은잎 다발, 가장자리 몇 곳에 솔잎 다발
            public static void OakPad(MB oakMb, MB pineMb, Vector3 c, float w)
            {
                Ico(1f, c, new Vector3(w * 0.42f, w * 0.15f, w * 0.36f), out var ip, out var it);
                oakMb.Solid(ip, it, c - Vector3.up * w * 0.3f, 1);
                int rim = 6 + (int)(w * 2f);
                for (int i = 0; i < rim; i++)
                {
                    float a = i * Mathf.PI * 2f / rim + R(-0.15f, 0.15f);
                    var hor = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var at = c + new Vector3(hor.x * w * 0.36f, R(-0.03f, 0.05f) * w, hor.z * w * 0.3f);
                    var shadeN = (hor * 0.5f + Vector3.up * 0.85f).normalized;
                    if (i % 5 == 2) PineSpray(pineMb, at, (hor + Vector3.up * 0.15f).normalized, w * 0.3f, 0, shadeN);
                    else OakSpray(oakMb, at, (hor + Vector3.up * 0.05f).normalized, w * 0.45f, 0, shadeN);
                }
                int top = 3 + (int)w;
                for (int i = 0; i < top; i++)
                {
                    float a = i * 2.39996f, rr = Mathf.Sqrt((i + 0.5f) / top) * 0.5f; var hor = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    OakSpray(oakMb, c + new Vector3(hor.x * w * 0.3f * rr, w * 0.11f, hor.z * w * 0.25f * rr), (hor * 0.6f + Vector3.up * 0.6f).normalized, w * 0.38f, 0, Vector3.up);
                }
            }

            // 버들가지: 가는 가지가 길게 늘어지고 양쪽으로 좁은 잔잎이 어긋나게
            public static void WillowStrand(MB mb, Vector3 q, Vector3 hor, float L, int seed)
            {
                var side = Vector3.Cross(hor, Vector3.up).normalized;
                var off = side * R(-0.15f, 0.15f);
                Vector3 Pt(float u) => q + off + Vector3.down * L * u + hor * 0.22f * u * (1f - u) + side * Mathf.Sin(u * 4f + seed) * 0.06f;
                var shadeN = (hor * 0.6f + Vector3.up * 0.7f).normalized;
                for (int g = 0; g < 3; g++)
                {
                    Vector3 a = Pt(g / 3f), b = Pt((g + 1) / 3f); var w = side * 0.012f;
                    mb.Poly2(new[] { a - w, b - w, b + w, a + w }, shadeN, 1);
                }
                const int n = 8;
                for (int k = 0; k < n; k++)
                {
                    float u = (k + 0.6f) / n; var at = Pt(u);
                    var dir = (Vector3.down * 0.7f + side * (k % 2 == 0 ? 1f : -1f) * 0.6f + hor * 0.3f).normalized;
                    float len = 0.38f * (1f - 0.3f * u), wid = len * 0.3f;
                    var wv = Vector3.Cross(dir, hor).normalized * wid * 0.5f;
                    var belly = at + dir * len * 0.45f;
                    mb.Poly2(new[] { at, belly + wv, at + dir * len, belly - wv }, shadeN, 0);
                }
            }

            // 장강 늘어진 전나무의 가지 자리: 꼭대기 = 솟은 다발, 가지 안쪽 = 바늘잎 다발, 가지 끝 = 바늘잎 + 늘어진 버들가지 둘
            public static void WeepTip(MB fir, MB willow, TreeTip tip)
            {
                var d = tip.D;
                var hor = new Vector3(d.x, 0f, d.z); hor = hor.sqrMagnitude < 1e-4f ? Vector3.right : hor.normalized;
                var shadeN = (hor * 0.5f + Vector3.up * 0.8f).normalized;
                if (tip.k == "top") FirSpray(fir, tip.P - Vector3.up * 0.5f, Vector3.up, 0.8f, 0, Vector3.up);
                else if (tip.k == "needle") FirSpray(fir, tip.P, (hor + Vector3.Cross(hor, Vector3.up) * R(-0.6f, 0.6f) + Vector3.down * 0.2f).normalized, tip.w * 0.45f, 0, shadeN);
                else
                {
                    FirSpray(fir, tip.P - hor * tip.w * 0.25f, (hor + Vector3.down * 0.3f).normalized, tip.w * 0.35f, 0, shadeN);
                    for (int q = 0; q < 2; q++) WillowStrand(willow, tip.P + Vector3.Cross(hor, Vector3.up) * (q - 0.5f) * 0.2f - hor * q * 0.12f, hor, 0.7f + tip.w * R(0.75f, 1.0f), q);
                }
            }

            // 천마총 마른 고목의 잔가지 끝: 짙은 바늘잎 술 (다발 셋이 사방으로)
            public static void NeedleTuft(MB fir, TreeTip tip)
            {
                var d = tip.D;
                var sx = Vector3.Cross(d, Vector3.up); sx = sx.sqrMagnitude < 1e-4f ? Vector3.right : sx.normalized;
                for (int q = 0; q < 3; q++)
                {
                    float ang = q * Mathf.PI * 2f / 3f + R(-0.3f, 0.3f);
                    var o = (d * 0.6f + (sx * Mathf.Cos(ang) + Vector3.Cross(sx, d) * Mathf.Sin(ang)) * 0.7f).normalized;
                    FirSpray(fir, tip.P - d * 0.15f, o, R(0.5f, 0.7f), q == 2 ? 1 : 0, (o * 0.4f + Vector3.up * 0.8f).normalized);
                }
            }

            // ── 죽림 뒤틀린 나무 둘 (코드로 짓는 굽은 관 줄기) ──
            static void Tube(MB mb, List<Vector3> c, List<float> r, int sub, int sides = 8)
            {
                int b0 = mb.v.Count; var prevN = Vector3.zero;
                for (int i = 0; i < c.Count; i++)
                {
                    var t = (i + 1 < c.Count ? c[i + 1] - c[i] : c[i] - c[i - 1]).normalized;
                    var nrm = i == 0 ? Vector3.Cross(t, Vector3.forward) : prevN - t * Vector3.Dot(prevN, t);
                    if (nrm.sqrMagnitude < 1e-5f) nrm = Vector3.Cross(t, Vector3.right);
                    nrm.Normalize(); prevN = nrm;
                    var bin = Vector3.Cross(t, nrm);
                    for (int k = 0; k <= sides; k++) { float a = k * Mathf.PI * 2f / sides; var d = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a); mb.v.Add(c[i] + d * r[i]); mb.n.Add(d); }
                }
                bool flip = Vector3.Dot(Vector3.Cross(mb.v[b0 + sides + 1] - mb.v[b0], mb.v[b0 + 1] - mb.v[b0]), mb.n[b0]) < 0f;   // 바깥이 앞면이 되게
                for (int i = 0; i + 1 < c.Count; i++)
                    for (int k = 0; k < sides; k++)
                    {
                        int a = b0 + i * (sides + 1) + k, b = a + sides + 1;
                        if (!flip) mb.sub[sub].AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                        else mb.sub[sub].AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                    }
            }

            static void Curve(System.Func<float, Vector3> f, float t0, float t1, float r0, float r1, MB mb, int sub, int n = 14, float flare = 0f)
            {
                var c = new List<Vector3>(); var r = new List<float>();
                for (int i = 0; i <= n; i++) { float u = i / (float)n; c.Add(f(Mathf.Lerp(t0, t1, u))); r.Add(Mathf.Lerp(r0, r1, u) * (1f + flare * Mathf.Pow(1f - u, 4f))); }
                Tube(mb, c, r, sub);
            }

            static System.Func<float, Vector3> Bez(Vector3 a, Vector3 m, Vector3 b) => t => Vector3.Lerp(Vector3.Lerp(a, m, t), Vector3.Lerp(m, b, t), t);

            // 대나무 잎다발 (게임의 LeafSpray 메시 그대로): 잔가지 밑동을 p에 두고 d 쪽으로
            static void BambooSpray(MB mb, System.Func<int, Mesh> spray, Vector3 p, Vector3 d, float scale, int variant)
            {
                var m = spray(variant % 3);
                d.y = 0f; d.Normalize();
                var xf = Matrix4x4.TRS(p + d * 0.8f * scale, Quaternion.FromToRotation(Vector3.right, d), Vector3.one * scale);
                int b = mb.v.Count;
                foreach (var q in m.vertices) mb.v.Add(xf.MultiplyPoint3x4(q));
                foreach (var q in m.normals) mb.n.Add(xf.MultiplyVector(q).normalized);
                foreach (var t in m.triangles) mb.sub[0].Add(b + t);
            }

            static void Lobes(MB oak, List<(Vector3 c, float r)> lobes, Vector3 treeC) { int k = 0; foreach (var l in lobes) OakLobe(oak, l.c, l.r, lobes, treeC, k++); }

            // 죽참나무: 꼬이며 오르는 대나무 줄기(마디 잎다발)가 중간에 참나무 껍질로 바뀌어 세 갈래 → 참나무 잎 공
            public static void BambooOak(MB trunk, MB oak, MB leaf, System.Func<int, Mesh> spray)
            {
                System.Func<float, Vector3> f = t => new Vector3(0.45f * Mathf.Sin(t * Mathf.PI * 1.6f) * t, 3.4f * t, 0.3f * (1f - Mathf.Cos(t * Mathf.PI * 1.6f)) * t);
                Curve(f, 0f, 0.6f, 0.3f, 0.26f, trunk, 1, 14, 0.5f);
                Curve(f, 0.57f, 1f, 0.27f, 0.2f, trunk, 0);
                var top = f(1f);
                var lobes = new List<(Vector3 c, float r)>();
                for (int k = 0; k < 3; k++)
                {
                    float a = k * Mathf.PI * 2f / 3f + 0.4f;
                    var end = top + new Vector3(Mathf.Cos(a) * 1.1f, 0.9f + 0.2f * k, Mathf.Sin(a) * 1.1f);
                    Curve(Bez(top, Vector3.Lerp(top, end, 0.5f) + Vector3.up * 0.25f, end), 0f, 1f, 0.17f, 0.08f, trunk, 0, 8);
                    lobes.Add((end + Vector3.up * 0.25f, 0.85f - 0.05f * k));
                }
                lobes.Add((top + Vector3.up * 1.35f, 0.9f));
                Lobes(oak, lobes, top + Vector3.up * 1.2f);
                int sv = 0;
                foreach (var t in new[] { 0.18f, 0.33f, 0.48f })
                    for (int j = 0; j < 2; j++) { float a = t * 9f + j * Mathf.PI; BambooSpray(leaf, spray, f(t), new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), 0.85f, sv++); }
            }

            // 세 갈래: 대나무·참나무·전나무 줄기가 서로 감겨 오르다 위에서 갈라져 각자의 잎
            public static void Braid(MB trunk, MB oak, MB fir, MB leaf, System.Func<int, Mesh> spray)
            {
                System.Func<float, Vector3> F(int k) => t =>
                {
                    float ph = k * Mathf.PI * 2f / 3f + 2.6f * Mathf.PI * t, b = 0.25f + Mathf.Max(0f, t - 0.65f) * 1.8f;
                    return new Vector3(b * Mathf.Cos(ph), 3.4f * t, b * Mathf.Sin(ph));
                };
                Curve(F(0), 0f, 1f, 0.2f, 0.16f, trunk, 1, 20, 0.3f);
                Curve(F(1), 0f, 1f, 0.22f, 0.18f, trunk, 0, 20, 0.4f);
                Curve(F(2), 0f, 1f, 0.2f, 0.16f, trunk, 0, 20, 0.4f);
                var b0 = F(0)(1f); var o1 = F(1)(1f); var f2 = F(2)(1f);
                Curve(t => b0 + Vector3.up * 1.8f * t, 0f, 1f, 0.16f, 0.11f, trunk, 1, 6);
                int sv = 0;
                foreach (var h in new[] { 0.7f, 1.2f, 1.7f })
                    for (int j = 0; j < 2; j++) { float a = h * 4f + j * Mathf.PI; BambooSpray(leaf, spray, b0 + Vector3.up * h, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), 0.8f, sv++); }
                var outDir = new Vector3(o1.x, 0, o1.z).normalized;
                Lobes(oak, new List<(Vector3 c, float r)> { (o1 + outDir * 0.45f + Vector3.up * 0.55f, 0.85f), (o1 + outDir * 0.9f + Vector3.up * 0.05f, 0.55f) }, o1 + outDir * 0.5f);
                Curve(t => f2 + Vector3.up * 2.4f * t, 0f, 1f, 0.16f, 0.04f, trunk, 0, 6);
                FirTiers(fir, f2, f2.y - 0.1f, f2.y + 2.6f, 0.95f, 3);
            }
        }
    }
}
