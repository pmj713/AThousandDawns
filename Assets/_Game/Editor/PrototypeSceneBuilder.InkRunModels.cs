using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 판 도중에 게임이 만드는 소품의 수묵 3D 모델 (InkRunProps가 입혀요):
    //  - 기물 주머니: 끈으로 목을 졸라 묶은 비단 주머니. 아래는 둥글게 처지고, 목 위로 주름진 입이 꽃잎처럼 벌어져요.
    //    끈은 목을 한 바퀴 감고 매듭에서 두 가닥이 늘어져요.
    //  - 금 간 벽: 벽 면에 붙인 돌 조각들. 세로로 지그재그 금이 지나가고, 금 양옆 돌은 밀려 나오고 비뚤어져 틈 사이로 먹빛 속이 보여요.
    public static partial class PrototypeSceneBuilder
    {
        static void DressRunModelsInk(AThousandDawns.Art.InkRunProps props, Material stoneMat)
        {
            var silk = GetInkMaterial("Ink_Pouch_Silk", "#8E2A22", 0.6f);    // 짙은 붉은 비단
            var cord = GetInkMaterial("Ink_Pouch_Cord", "#C9A04A", 0.6f);    // 금빛 끈
            var dark = GetInkMaterial("Ink_Crack_Dark", "#141210", 0f);      // 금 속 먹빛
            props.SetupModels(GetPouchMesh(), silk, GetPouchCordMesh(), cord, GetCrackStonesMesh(), stoneMat, GetCrackDarkMesh(), dark);
        }

        // 비단 주머니 (높이 약 0.72, 밑이 바닥 y=0): 돌림 단면 + 세로 주름
        static Mesh GetPouchMesh()
        {
            Vector2[] profile =   // (반지름, 높이) 밑에서 위로
            {
                new Vector2(0f, 0f), new Vector2(0.16f, 0.015f), new Vector2(0.27f, 0.07f), new Vector2(0.33f, 0.17f),
                new Vector2(0.34f, 0.27f), new Vector2(0.3f, 0.37f), new Vector2(0.2f, 0.45f), new Vector2(0.105f, 0.5f),   // 끈에 졸린 목
                new Vector2(0.1f, 0.53f), new Vector2(0.14f, 0.59f), new Vector2(0.2f, 0.66f), new Vector2(0.22f, 0.7f),    // 벌어진 입 (주름)
                new Vector2(0.17f, 0.72f), new Vector2(0.09f, 0.66f), new Vector2(0f, 0.63f),                              // 안쪽으로 접혀 닫혀요
            };
            const int segs = 28;
            var verts = new List<Vector3>();
            for (int i = 0; i < profile.Length; i++)
            {
                float y = profile[i].y;
                // 주름: 목 위 입은 크게, 몸통은 끈에 당겨진 잔주름 (목에 가까울수록)
                float fold = y > 0.5f ? 0.16f : Mathf.Lerp(0.015f, 0.07f, Mathf.InverseLerp(0.15f, 0.48f, y));
                for (int k = 0; k < segs; k++)
                {
                    float a = k * Mathf.PI * 2f / segs;
                    float r = profile[i].x * (1f + fold * Mathf.Sin(a * 7f + y * 3f));
                    if (y < 0.3f) r *= 1f + 0.04f * Mathf.Cos(a);                 // 한쪽으로 조금 처진 몸통
                    verts.Add(new Vector3(Mathf.Cos(a) * r, y + (y > 0.55f ? 0.02f * Mathf.Sin(a * 7f) : 0f), Mathf.Sin(a) * r));
                }
            }
            return SaveRunPropMesh("Pouch_Silk", verts, LatheTris(profile.Length, segs));
        }

        // 끈: 목을 감은 고리 + 매듭 + 늘어진 두 가닥
        static Mesh GetPouchCordMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // 목 고리 (반지름 0.115, 굵기 0.022)
            var ring = new List<Vector3>();
            for (int k = 0; k <= 24; k++)
            {
                float a = k * Mathf.PI * 2f / 24;
                ring.Add(new Vector3(Mathf.Cos(a) * 0.118f, 0.505f, Mathf.Sin(a) * 0.118f));
            }
            TubeInto(ring, 0.022f, 0.022f, verts, tris);
            // 매듭 (앞쪽, -z)
            Vector3 knot = new Vector3(0f, 0.5f, -0.13f);
            var knotLine = new List<Vector3> { knot + new Vector3(-0.035f, 0f, 0f), knot + new Vector3(0f, 0.01f, -0.02f), knot + new Vector3(0.035f, 0f, 0f) };
            TubeInto(knotLine, 0.035f, 0.035f, verts, tris);
            // 두 가닥: 매듭에서 몸통을 따라 늘어져요
            for (int s = -1; s <= 1; s += 2)
            {
                var strand = new List<Vector3>();
                for (int i = 0; i <= 6; i++)
                {
                    float u = i / 6f;
                    float y = 0.5f - u * 0.3f;
                    float out_ = 0.13f + 0.2f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.3f)) * 0.5f + u * 0.08f;   // 몸통 바깥으로 둥글게
                    strand.Add(new Vector3(s * (0.02f + u * 0.07f), y, -out_));
                }
                TubeInto(strand, 0.016f, 0.012f, verts, tris);
                Vector3 end = strand[strand.Count - 1];
                var tassel = new List<Vector3> { end, end + new Vector3(0f, -0.04f, -0.005f), end + new Vector3(0f, -0.09f, -0.01f) };
                TubeInto(tassel, 0.02f, 0.03f, verts, tris);   // 끝 술
            }
            return SaveRunPropMesh("Pouch_Cord", verts, tris);
        }

        // 금 간 돌벽 조각 (벽 면 기준: x 가로, y 위(바닥 -1.1 ~ 0.3), -z가 방 쪽)
        static Mesh GetCrackStonesMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var rng = new System.Random(4211);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const float bottom = -1.1f, top = 0.28f, half = 0.75f;
            const int rows = 5;
            float rowH = (top - bottom) / rows;
            for (int row = 0; row < rows; row++)
            {
                float y0 = bottom + row * rowH, y1 = y0 + rowH;
                float crackX = CrackX((y0 + y1) * 0.5f);
                float x = -half + (row % 2 == 0 ? 0f : -0.2f);
                while (x < half)
                {
                    float w = R(0.32f, 0.48f);
                    float x0 = Mathf.Max(-half, x), x1 = Mathf.Min(half, x + w);
                    x += w;
                    if (x1 - x0 < 0.08f) continue;
                    // 금이 지나가면 두 조각으로 갈라 틈을 둬요
                    var pieces = new List<(float a, float b)>();
                    if (crackX > x0 + 0.05f && crackX < x1 - 0.05f) { pieces.Add((x0, crackX - 0.035f)); pieces.Add((crackX + 0.035f, x1)); }
                    else pieces.Add((x0, x1));
                    foreach (var (a, b) in pieces)
                    {
                        float near = Mathf.Abs((a + b) * 0.5f - crackX);
                        float push = near < 0.35f ? R(0.05f, 0.12f) : R(0.0f, 0.03f);   // 금 옆 돌은 밀려 나와요
                        float tilt = near < 0.35f ? R(-9f, 9f) : R(-2f, 2f);
                        float depth = R(0.07f, 0.11f) + push;
                        var c = new Vector3((a + b) * 0.5f, (y0 + y1) * 0.5f, -depth * 0.5f);
                        var size = new Vector3(b - a - 0.025f, rowH - 0.025f, depth);
                        BoxInto(verts, tris, c, size, Quaternion.Euler(R(-4f, 4f), R(-6f, 6f), tilt));
                    }
                }
            }
            return SaveRunPropMesh("Crack_Stones", verts, tris);
        }

        // 금 속 먹빛: 돌 틈 뒤 벽 면에 지그재그 띠 (틈으로 보여요)
        static Mesh GetCrackDarkMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int n = 16;
            for (int i = 0; i <= n; i++)
            {
                float y = Mathf.Lerp(-1.1f, 0.3f, i / (float)n);
                float w = 0.05f + 0.03f * Mathf.Sin(i * 1.7f);
                float x = CrackX(y);
                verts.Add(new Vector3(x - w, y, -0.01f));
                verts.Add(new Vector3(x + w, y, -0.01f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(c); tris.Add(b);   // 방 쪽(-z)이 앞면
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            return SaveRunPropMesh("Crack_Dark", verts, tris);
        }

        // 금이 지나가는 가로 자리 (높이에 따라 지그재그)
        static float CrackX(float y) => 0.08f * Mathf.Sin(y * 7.3f) + 0.05f * Mathf.Sin(y * 17.1f + 1f) - 0.04f;

        // 돌림 단면 삼각형 (고리 rows개 × segs, 바깥이 앞면)
        static List<int> LatheTris(int rows, int segs)
        {
            var tris = new List<int>();
            for (int i = 0; i < rows - 1; i++)
                for (int k = 0; k < segs; k++)
                {
                    int a = i * segs + k, b = i * segs + (k + 1) % segs, c = a + segs, d = b + segs;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            return tris;
        }

        // 굵기가 r0 → r1로 변하는 관 (평행 이동 틀)
        static void TubeInto(List<Vector3> line, float r0, float r1, List<Vector3> verts, List<int> tris)
        {
            const int sides = 6;
            int baseIndex = verts.Count;
            Vector3 prevT = (line[1] - line[0]).normalized;
            Vector3 nrm = Vector3.Cross(prevT, Mathf.Abs(prevT.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < line.Count; i++)
            {
                Vector3 t = i == 0 ? prevT : i == line.Count - 1 ? (line[i] - line[i - 1]).normalized : (line[i + 1] - line[i - 1]).normalized;
                nrm = (Quaternion.FromToRotation(prevT, t) * nrm).normalized;
                prevT = t;
                Vector3 bin = Vector3.Cross(t, nrm);
                float r = Mathf.Lerp(r0, r1, i / (float)(line.Count - 1));
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    verts.Add(line[i] + (nrm * Mathf.Cos(a) + bin * Mathf.Sin(a)) * r);
                }
            }
            for (int i = 0; i < line.Count - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = baseIndex + i * sides + k, b = baseIndex + i * sides + (k + 1) % sides, c = a + sides, d = b + sides;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
        }

        // 각진 상자 (면마다 꼭짓점을 따로 둬 법선이 면 단위)
        static void BoxInto(List<Vector3> verts, List<int> tris, Vector3 center, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rot * Vector3.Scale(new Vector3(x, y, z), h);
            Vector3[][] faces =
            {
                new[] { P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1) },   // -z (방 쪽)
                new[] { P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1) },       // +z
                new[] { P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1) },       // +y
                new[] { P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1) },   // -y
                new[] { P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1) },   // -x
                new[] { P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1) },       // +x
            };
            foreach (var f in faces)
            {
                int i = verts.Count;
                verts.AddRange(f);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
        }
    }
}
