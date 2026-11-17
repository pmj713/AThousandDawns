using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 코드로 만드는 단색 정점 색 메시 (빛 없는 Sprites/Default 재질용): 삼각형 · 사각형 · 상자 · 각기둥 · 원뿔, 모두 양면
    //   만검귀종 검 (InkUltimateFx) · 망령 기병 말 (InkBossPatternFx) · 권풍 주먹 (InkProjectiles)이 써요.
    sealed class MeshBits
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Color> _c = new List<Color>();
        readonly List<int> _t = new List<int>();
        public Matrix4x4 Xf = Matrix4x4.identity; // 다음 도형에 적용할 변환 (예: 눕힌 각기둥)

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            int i = _v.Count;
            _v.Add(Xf.MultiplyPoint3x4(a)); _v.Add(Xf.MultiplyPoint3x4(b)); _v.Add(Xf.MultiplyPoint3x4(c));
            _c.Add(ca); _c.Add(cb); _c.Add(cc);
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 1); // 양면 (빛 없는 재질이라 뒤집혀도 보이게)
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd)
        {
            Tri(a, b, c, ca, cb, cc);
            Tri(a, c, d, ca, cc, cd);
        }

        public void Box(Vector3 center, Vector3 size, Color top, Color bottom)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
            Color side = Color.Lerp(top, bottom, 0.4f);
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), top, top, top, top);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), bottom, bottom, bottom, bottom);
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), side, side, top, top);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), side, top, top, side);
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), side, top, top, side);
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), side, side, top, top);
        }

        public void Prism(float y0, float y1, float r, int sides, Color light, Color dark) => Cone(y0, y1, r, r, sides, light, dark);

        public void Cone(float y0, float y1, float r0, float r1, int sides, Color light, Color dark)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Color c = Color.Lerp(dark, light, 0.5f + 0.5f * Mathf.Cos((a0 + a1) * 0.5f - 0.8f)); // 한쪽이 밝게
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0), b1 = new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1), t1 = new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1);
                Quad(b0, t0, t1, b1, c, c, c, c);
                Tri(new Vector3(0f, y1, 0f), t0, t1, light, light, light);
                Tri(new Vector3(0f, y0, 0f), b1, b0, dark, dark, dark);
            }
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(_v);
            m.SetColors(_c);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
