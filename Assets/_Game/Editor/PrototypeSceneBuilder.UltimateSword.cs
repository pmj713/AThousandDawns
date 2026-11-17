using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 만검귀종 (검 절초) 떨어지는 검의 3D 모델 (플레이테스트 2차: 상자 셋 검 대신). Resources/Models/UltimateSword.prefab
    //   좌표는 게임 코드 SwordRain의 상자 검과 같아요: 칼끝이 원점, +Y로 길이 약 1.8 (날 1.39 · 코등이 · 손잡이 · 꼭지).
    //   메시 하나에 정점 색: 날은 흰 강철(가장자리) · 푸른빛 피홈(가운데 능선), 금 코등이(양끝이 위로 휜 구름 끝), 붉은 감개 손잡이에 금띠 넷, 금 꼭지와 고리.
    //   재질은 채색 캐릭터 셰이더(정점 색). 아트 2의 InkUltimateFx가 루트 자식으로 끼우고 빛무리·꼬리를 달아요
    public static partial class PrototypeSceneBuilder
    {
        const string UltimateSwordPrefabPath = "Assets/_Game/Resources/Models/UltimateSword.prefab";

        static void BuildUltimateSwordPrefab()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var col); return col; }
            var steel = Hex("#EEF2F8"); var fuller = Hex("#9DB8E8"); var gold = Hex("#F2C24A"); var goldDark = Hex("#C8922A");
            var wrap = Hex("#9E1C22"); var wrapDark = Hex("#6E1218");

            // 감김 방향: 면 가운데에서 hint(지금 도형의 축·중심)를 등지게 맞춰요 (앞면 = 바깥)
            System.Func<Vector3, Vector3> hint = p => new Vector3(0f, p.y, 0f);
            void Tri(Vector3 a, Vector3 b, Vector3 d, Color ca, Color cb, Color cd)
            {
                var normal = Vector3.Cross(b - a, d - a).normalized;
                var mid = (a + b + d) / 3f;
                if (Vector3.Dot(normal, mid - hint(mid)) < 0f) { (b, d) = (d, b); (cb, cd) = (cd, cb); normal = -normal; }
                int s = v.Count;
                v.Add(a); v.Add(b); v.Add(d);
                n.Add(normal); n.Add(normal); n.Add(normal);
                c.Add(ca.linear); c.Add(cb.linear); c.Add(cd.linear);
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color ca, Color cb, Color cd, Color ce)
            {
                Tri(a, b, d, ca, cb, cd);
                Tri(a, d, e, ca, cd, ce);
            }
            // 링 둘을 잇는 옆면 (각 링은 같은 수의 점, 바깥을 향하게 감아요)
            void Loft(Vector3[] lo, Vector3[] hi, Color[] clo, Color[] chi)
            {
                for (int i = 0; i < lo.Length; i++)
                {
                    int j = (i + 1) % lo.Length;
                    Quad(lo[i], hi[i], hi[j], lo[j], clo[i], chi[i], chi[j], clo[j]);
                }
            }
            void Cap(Vector3[] ring, Vector3 center, Color col, bool up)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    int j = (i + 1) % ring.Length;
                    if (up) Tri(center, ring[j], ring[i], col, col, col);
                    else Tri(center, ring[i], ring[j], col, col, col);
                }
            }
            Vector3[] Ring(int sides, float y, float rx, float rz, float phase = 0f)
            {
                var r = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = (i / (float)sides) * Mathf.PI * 2f + phase;
                    r[i] = new Vector3(Mathf.Cos(a) * rx, y, Mathf.Sin(a) * rz);
                }
                return r;
            }
            Color[] Fill(int k, Color col) { var a = new Color[k]; for (int i = 0; i < k; i++) a[i] = col; return a; }
            void Box(Vector3 center, Vector3 size, Quaternion rot, Color top, Color side)
            {
                var h = size * 0.5f;
                var keep = hint; hint = _ => center;
                Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
                Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), top, top, top, top);
                Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), side, side, side, side);
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), side, top, top, side);
                Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), side, top, top, side);
                Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), side, top, top, side);
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), side, top, top, side);
                hint = keep;
            }

            // 날: 마름모 단면 (가장자리 둘은 흰 강철, 앞뒤 능선은 푸른 피홈), 칼끝에서 점점 넓어져요
            float[] ys = { 0.16f, 0.6f, 1.05f, 1.39f };
            float[] ws = { 0.085f, 0.1f, 0.105f, 0.11f };
            const float thick = 0.03f;
            Vector3[] Diamond(float y, float w) => new[] { new Vector3(w * 0.5f, y, 0f), new Vector3(0f, y, thick * 0.5f), new Vector3(-w * 0.5f, y, 0f), new Vector3(0f, y, -thick * 0.5f) };
            var bladeCols = new[] { steel, fuller, steel, fuller };
            var tip = Vector3.zero;
            var first = Diamond(ys[0], ws[0]);
            for (int i = 0; i < 4; i++)
                Tri(tip, first[(i + 1) % 4], first[i], steel, bladeCols[(i + 1) % 4], bladeCols[i]);
            for (int k = 0; k < ys.Length - 1; k++)
                Loft(Diamond(ys[k], ws[k]), Diamond(ys[k + 1], ws[k + 1]), bladeCols, bladeCols);
            // 코등이: 가운데 두툼한 금 덩이 + 양끝이 위로 휜 구름 끝 (끝에 작은 금 구슬)
            Box(new Vector3(0f, 1.42f, 0f), new Vector3(0.2f, 0.07f, 0.09f), Quaternion.identity, gold, goldDark);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(new Vector3(s * 0.15f, 1.445f, 0f), new Vector3(0.14f, 0.05f, 0.07f), Quaternion.Euler(0f, 0f, s * 28f), gold, goldDark);
                var ball = Ring(8, 1.5f, 0.035f, 0.035f);
                var ballLo = Ring(8, 1.455f, 0.035f, 0.035f);
                for (int i = 0; i < 8; i++) { ball[i].x += s * 0.215f; ballLo[i].x += s * 0.215f; }
                float bx = s * 0.215f;
                hint = p => new Vector3(bx, Mathf.Clamp(p.y, 1.47f, 1.49f), 0f);
                Loft(ballLo, ball, Fill(8, goldDark), Fill(8, gold));
                Cap(ball, new Vector3(s * 0.215f, 1.52f, 0f), gold, true);
                Cap(ballLo, new Vector3(s * 0.215f, 1.44f, 0f), goldDark, false);
                hint = p => new Vector3(0f, p.y, 0f);
            }
            // 손잡이: 팔각 붉은 감개, 금띠 넷
            const int G = 8;
            float gy0 = 1.455f, gy1 = 1.76f;
            for (int k = 0; k < 6; k++)
            {
                float a = Mathf.Lerp(gy0, gy1, k / 6f), b = Mathf.Lerp(gy0, gy1, (k + 1) / 6f);
                var col = k % 2 == 0 ? wrap : wrapDark;
                Loft(Ring(G, a, 0.032f, 0.032f, k * 0.2f), Ring(G, b, 0.032f, 0.032f, (k + 1) * 0.2f), Fill(G, col), Fill(G, col));
            }
            foreach (float y in new[] { 1.47f, 1.57f, 1.66f, 1.75f })
                Loft(Ring(G, y - 0.008f, 0.037f, 0.037f), Ring(G, y + 0.008f, 0.037f, 0.037f), Fill(G, goldDark), Fill(G, gold));
            // 꼭지: 금 구슬과 고리
            var p0 = Ring(G, 1.76f, 0.04f, 0.04f); var p1 = Ring(G, 1.8f, 0.05f, 0.05f); var p2 = Ring(G, 1.84f, 0.035f, 0.035f);
            Loft(p0, p1, Fill(G, goldDark), Fill(G, gold));
            Loft(p1, p2, Fill(G, gold), Fill(G, gold));
            hint = p => new Vector3(0f, Mathf.Min(p.y, 1.8f), 0f);   // 꼭지 윗면은 아래 가운데를 등지게
            Cap(p2, new Vector3(0f, 1.86f, 0f), gold, true);
            Box(new Vector3(0f, 1.9f, 0f), new Vector3(0.012f, 0.06f, 0.06f), Quaternion.identity, gold, goldDark);

            EnsureFolder("Assets/_Game/Resources/Models");
            string meshPath = "Assets/_Game/Resources/Models/UltimateSword_Mesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "UltimateSword" };
            mesh.Clear();
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(c); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, meshPath); else EditorUtility.SetDirty(mesh);

            var mat = FantasyLook(GetInkMaterial("Ink_UltimateSword3D", "#FFFFFF", 1f));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_VertexTint", 1f);
            mat.SetFloat("_OutlineWidth", 2f);
            mat.SetFloat("_ShadowInk", 0.2f);
            EditorUtility.SetDirty(mat);

            var go = new GameObject("UltimateSword");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(go, UltimateSwordPrefabPath);
            Object.DestroyImmediate(go);
        }
    }
}
