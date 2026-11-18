using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 입체 채색 잎·덤불 (플레이 테스트: 그림 카드 덤불·대나무 잎이 채색 입체 장면에서 어색했어요).
    //   대나무 잎: 잔가지에 가늘고 긴 잎(마름모 칼날 모양, 앞뒤 면)을 마디마다 부채꼴로 달아 끝이 처지게 — 그림 카드 자리·크기 그대로 바꿔 끼워요
    //   덤불: 울퉁불퉁한 공 여러 개를 겹친 둥근 덩어리 + 겉에 작은 잎 뭉치, 아래는 짙고 위는 밝은 정점 색 (셀 명암이 덩이마다 생겨요)
    //   재질은 대나무 줄기와 같은 구조물 셰이더(InkTriplanar)·같은 값에 줄기 색으로 그린 잎 그림 (아래 StalkStyle). 메시는 Art/Env/Foliage3D에 변형 셋씩 저장
    public static partial class PrototypeSceneBuilder
    {
        const string Foliage3DFolder = "Assets/_Game/Art/Env/Foliage3D";
        static readonly Dictionary<string, Mesh> s_foliage3D = new Dictionary<string, Mesh>();

        // 플레이 테스트 2차: 채색 캐릭터 셰이더의 납작한 셀 명암·굵은 외곽선이 대나무 줄기(그림 텍스처 + 세로 붓결)와 그림풍이 달랐어요.
        //   잎·덤불도 줄기와 같은 구조물 셰이더(InkTriplanar)에 같은 값(명암 단계·가장자리 먹·붓결·얼룩)을 쓰고,
        //   줄기 그림과 같은 색으로 그린 잎 그림(Tex_LeafPaint, 겹친 잎 붓질)을 입혀요. 정점 색은 쓰지 않아요 (_VertexInk 0)
        static Material StalkStyle(Material m, float rim, float outline)
        {
            m.SetFloat("_OutlineFromCenter", 0f);
            m.SetFloat("_OutlineWidth", outline);
            m.SetFloat("_VertexInk", 0f);
            m.SetFloat("_PaperLift", 0f);
            m.SetFloat("_Saturation", 0f);
            m.SetFloat("_RimInk", rim);
            m.SetFloat("_RimPower", 2f);
            m.SetFloat("_Bands", 3f);
            m.SetFloat("_BandSoftness", 0.3f);
            m.SetColor("_BaseColor", new Color(0.53f, 0.53f, 0.53f, 1f));
            m.SetFloat("_StreakLight", 0.6f);
            m.SetFloat("_StreakScale", 8f);
            m.SetFloat("_PatchLight", 0.45f);
            m.SetFloat("_PatchScale", 0.9f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material LeafMaterial()
        {
            LeafPaintTexture();
            var m = StalkStyle(GetInkTriplanar("Ink3D_BambooLeafPaint", "Tex_LeafPaint", 0.5f, 0f), 0.3f, 0f);   // 가는 잎은 외곽선 없이
            // 플레이 테스트 3차: 그래도 움직일 때마다 깜빡였어요 — 줄기·다른 잎 그림자 경계가 카메라를 따라 얇은 잎 위를 오가요. 잎은 그림자를 받지 않아요
            m.SetFloat("_ShadowReceive", 0f);
            // 플레이테스트 9차: 잎 색이 바래고 회색 사선 얼룩이 끼었어요 — 마른 붓 결(사선 줄)·옅은 얼룩·번짐·종이 결이 월드 좌표에 칠해져서,
            //   바람에 흔들리는 잎 위로 미끄러지며 군데군데 잎을 회색으로 바래게 했어요 → 그 넷을 끄고 잎 그림 초록만 남겨요
            m.SetFloat("_StreakLight", 0f);
            m.SetFloat("_PatchLight", 0f);
            m.SetFloat("_WashNoiseStrength", 0f);
            m.SetFloat("_PaperGrain", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material BushMaterial()
        {
            LeafPaintTexture();
            return StalkStyle(GetInkTriplanar("Ink3D_BushPaint", "Tex_LeafPaint", 0.9f, 1f), 0.55f, 1f);    // 줄기처럼 얇은 외곽선
        }

        // 잎 그림 (이음새 없음): 줄기 그림의 초록들(짙은 초록 · 초록 · 연두 · 노란 결)로 끝이 뾰족한 잎 붓질 수백 개를 겹쳐 칠해요. 가운데 잎맥은 밝게
        static void LeafPaintTexture()
        {
            string path = $"{Env2DArtFolder}/Tex_LeafPaint.png";
            if (System.IO.File.Exists(path)) return;
            const int S = 512;
            var rnd = new System.Random(919);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var col); return col; }
            var dark = Hex("#1E6A0C"); var mid = Hex("#3C9A12"); var green = Hex("#52B018"); var light = Hex("#8CCD28"); var glow = Hex("#C8E63C");
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = Color.Lerp(dark, mid, 0.35f);
            void Leaf(float cx, float cy, float len, float wid, float rot, Color col, Color vein)
            {
                float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                int r = Mathf.CeilToInt(len) + 1;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        float u = (dx * cs + dy * sn) / len, w = (-dx * sn + dy * cs);
                        if (u < -1f || u > 1f) continue;
                        float half = wid * (1f - u * u) * (u < 0f ? 1f : 1f - 0.35f * u);   // 끝이 뾰족한 잎
                        if (Mathf.Abs(w) > half) continue;
                        int xx = (((int)cx + dx) % S + S) % S, yy = (((int)cy + dy) % S + S) % S;
                        float edge = Mathf.Abs(w) / Mathf.Max(half, 0.01f);
                        var c = Color.Lerp(col, Color.Lerp(col, dark, 0.35f), edge * edge);          // 가장자리는 조금 짙게
                        if (Mathf.Abs(w) < 0.7f) c = Color.Lerp(c, vein, 0.6f * (1f - Mathf.Abs(u)));   // 잎맥
                        px[yy * S + xx] = c;
                    }
            }
            for (int k = 0; k < 520; k++)
            {
                float pick = (float)rnd.NextDouble();
                var col = pick < 0.25f ? mid : pick < 0.6f ? green : pick < 0.9f ? light : glow;
                Leaf(R(0f, S), R(0f, S), R(14f, 26f), R(3.5f, 6f), R(0f, Mathf.PI * 2f), col, Color.Lerp(col, glow, 0.7f));
            }
            var tex = new Texture2D(S, S, TextureFormat.RGB24, false);
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            ImportAsTiledTexture(path);
        }

        // 그림 카드 대신: 대나무 잎 카드 → 잎 가지 count개를 같은 각도로 엇갈려 꽂아요. 덤불 카드 → 둥근 덤불 하나 (카드 아래 끝이 밑동)
        static bool Foliage3D(Transform parent, string name, string textureName, Vector3 center, float height, int count, float yaw)
        {
            bool leaves = textureName.StartsWith("BambooLeaves"), bush = textureName.StartsWith("Bush_");
            if (!leaves && !bush) return false;
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = center;
            int variant = Mathf.Abs(Mathf.RoundToInt(yaw * 7f + center.x * 13f + center.y * 5f)) % 3;
            if (leaves)
            {
                var mesh = LeafSprayMesh(variant);
                var mat = LeafMaterial();
                for (int k = 0; k < count; k++)
                {
                    var go = new GameObject($"Spray_{k + 1}");
                    go.transform.SetParent(group, false);
                    go.transform.localRotation = Quaternion.Euler(0f, yaw + k * 180f / count, 0f);
                    go.transform.localScale = new Vector3(k % 2 == 0 ? height : -height, height, height);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var leafRenderer = go.AddComponent<MeshRenderer>();
                    leafRenderer.sharedMaterial = mat;
                    // 플레이 테스트 2차: 움직일 때마다 잎이 검게·초록으로 번갈아 반짝였어요 — 얇은 잎이 제 그림자를 받는 그림자 여드름이라 잎은 그림자를 드리우지 않아요
                    leafRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    variant = (variant + 1) % 3;
                    mesh = LeafSprayMesh(variant);
                }
            }
            else
            {
                var go = new GameObject("Bush (3D)");
                go.transform.SetParent(group, false);
                go.transform.localPosition = Vector3.down * height * 0.5f;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = new Vector3(height * 1.1f, height * 0.85f, height * 1.1f);
                go.AddComponent<MeshFilter>().sharedMesh = BushMesh(variant);
                go.AddComponent<MeshRenderer>().sharedMaterial = BushMaterial();
            }
            return true;
        }

        static Mesh SaveFoliageMesh(string key, List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t)
        {
            string path = $"{Foliage3DFolder}/{key}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = key };
            mesh.Clear();
            mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            if (isNew)
            {
                EnsureFolder(Foliage3DFolder);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else EditorUtility.SetDirty(mesh);
            s_foliage3D[key] = mesh;
            return mesh;
        }

        // 잎 가지 (카드와 같은 틀: 가로 약 1.7 · 세로 1, 가운데가 원점, 카드 면 = XY): 왼 아래에서 오른 위로 휜 잔가지에 마디 일곱,
        //   마디마다 잎 셋이 아래로 처지며 부채처럼 퍼져요. 잎은 앞뒤 면을 따로 (뒷면도 셀 명암), 앞뒤(Z)로도 조금 흩어져 입체로 보여요
        static Mesh LeafSprayMesh(int variant)
        {
            string key = $"LeafSpray_{variant}";
            if (s_foliage3D.TryGetValue(key, out var cached) && cached != null) return cached;
            var rnd = new System.Random(variant * 131 + 17);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            var dark = new Color(0.26f, 0.56f, 0.22f); var mid = new Color(0.4f, 0.72f, 0.28f); var light = new Color(0.66f, 0.88f, 0.36f);

            void Quad2(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color ca, Color cb)
            {
                // 앞뒤 두 면 (a-b-c-d 순서, a·c는 잎 밑동·끝)
                // 잎 명암: 면 법선을 위쪽으로 기울여 앞뒤 면 모두 햇빛을 받게 (늘어진 잎의 아랫면이 검게 되지 않게)
                var normal = Vector3.Cross(cc - a, b - d).normalized;
                var lit = (Vector3.up * 1.6f + new Vector3(0f, 0f, -0.4f)).normalized;
                for (int side = 0; side < 2; side++)
                {
                    int s = v.Count;
                    var nn = ((side == 0 ? normal : -normal) * 0.35f + lit).normalized;
                    v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
                    n.Add(nn); n.Add(nn); n.Add(nn); n.Add(nn);
                    c.Add(ca.linear); c.Add(Color.Lerp(ca, cb, 0.5f).linear); c.Add(cb.linear); c.Add(Color.Lerp(ca, cb, 0.5f).linear);
                    if (side == 0) t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                    else t.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                }
            }

            // 잔가지
            var from = new Vector3(-0.8f, -0.42f, 0f);
            var to = new Vector3(0.82f, 0.28f + R(-0.05f, 0.08f), R(-0.1f, 0.1f));
            Vector3 Twig(float u) => Vector3.Lerp(from, to, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.12f;
            var twigCol = new Color(0.42f, 0.55f, 0.2f);
            for (int k = 0; k < 8; k++)
            {
                float u0 = k / 8f, u1 = (k + 1) / 8f;
                Vector3 p0 = Twig(u0), p1 = Twig(u1);
                var up = Vector3.up * Mathf.Lerp(0.022f, 0.01f, u0);
                Quad2(p0 - up, p1 - up, p1 + up, p0 + up, twigCol, twigCol);
            }

            int nodes = 7;
            for (int k = 0; k < nodes; k++)
            {
                float u = 0.18f + k * (0.8f / nodes) + R(-0.03f, 0.03f);
                var p = Twig(u);
                var along = (Twig(u + 0.02f) - p).normalized;
                int leavesHere = 4 + (k % 2);
                for (int l = 0; l < leavesHere; l++)
                {
                    // 가지 방향에서 아래로 처진 부채꼴 (-25 ~ -110도), 앞뒤로 조금씩
                    float ang = Mathf.Lerp(-20f, -105f, (l + R(0f, 0.6f)) / leavesHere) * Mathf.Deg2Rad;
                    var dir = (along * Mathf.Cos(ang) + Vector3.Cross(Vector3.forward, along).normalized * -Mathf.Sin(ang) * -1f);
                    dir = (dir + new Vector3(0f, 0f, R(-0.6f, 0.6f))).normalized;
                    if (dir.y > 0.2f) dir.y = 0.2f - (dir.y - 0.2f);   // 위로 솟지 않게 (대나무 잎은 늘어져요)
                    dir.Normalize();
                    float len = R(0.32f, 0.48f) * (1f - u * 0.25f), wid = len * R(0.2f, 0.26f);
                    var side = Vector3.Cross(dir, new Vector3(R(-0.3f, 0.3f), 0.3f, 1f)).normalized * wid * 0.5f;
                    var tip = p + dir * len + Vector3.down * len * R(0.1f, 0.25f);   // 끝이 처져요
                    var belly = p + dir * len * 0.38f;
                    var col0 = Color.Lerp(dark, mid, R(0.2f, 0.8f));
                    var col1 = Color.Lerp(mid, light, R(0.2f, 1f));
                    Quad2(p, belly + side, tip, belly - side, col0, col1);
                }
            }
            return SaveFoliageMesh(key, v, n, c, t);
        }

        // 둥근 덤불 (밑동이 원점, 지름 약 1.2 · 높이 약 0.9): 울퉁불퉁한 공 5~7개를 둥글게 겹치고 겉에 작은 잎 뭉치 공을 붙여요
        static Mesh BushMesh(int variant)
        {
            string key = $"Bush3D_{variant}";
            if (s_foliage3D.TryGetValue(key, out var cached) && cached != null) return cached;
            var rnd = new System.Random(variant * 71 + 3);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            var dark = new Color(0.12f, 0.33f, 0.14f); var mid = new Color(0.24f, 0.55f, 0.2f); var light = new Color(0.5f, 0.78f, 0.28f);
            var sphere = UnitSphereTris();

            void Ball(Vector3 center, Vector3 radii, float lift, float bumpSeed)
            {
                foreach (var dirRaw in sphere)
                {
                    var dir = dirRaw.normalized;
                    float bump = 1f + 0.1f * Mathf.Sin(dir.x * 7f + bumpSeed) * Mathf.Sin(dir.y * 6f + bumpSeed * 1.7f) * Mathf.Sin(dir.z * 5f + bumpSeed * 0.6f);
                    var p = center + Vector3.Scale(dir * bump, radii);
                    p.y = Mathf.Max(p.y, 0f);
                    float h = Mathf.Clamp01(p.y / 0.9f);
                    var col = h < 0.5f ? Color.Lerp(dark, mid, h * 2f) : Color.Lerp(mid, light, (h - 0.5f) * 2f * lift);
                    v.Add(p); n.Add(dir); c.Add(col.linear);
                    t.Add(v.Count - 1);
                }
            }

            int lobes = 5 + variant % 3;
            for (int l = 0; l < lobes; l++)
            {
                float a = (l * 360f / lobes + R(-25f, 25f)) * Mathf.Deg2Rad;
                float ring = l == 0 ? 0f : R(0.22f, 0.36f);
                var ctr = new Vector3(Mathf.Cos(a) * ring, R(0.28f, 0.42f) + (l == 0 ? 0.12f : 0f), Mathf.Sin(a) * ring);
                float r = l == 0 ? R(0.38f, 0.44f) : R(0.26f, 0.34f);
                Ball(ctr, new Vector3(r, r * R(0.85f, 1f), r), R(0.7f, 1f), R(0f, 10f));
            }
            // 겉에 붙은 작은 잎 뭉치 (밝은 볼록이): 위쪽 반구에만
            int tufts = 12 + variant * 2;
            for (int k = 0; k < tufts; k++)
            {
                float phi = R(0f, Mathf.PI * 2f), yu = R(0.15f, 0.95f);
                float ring = Mathf.Sqrt(1f - yu * yu);
                var dir = new Vector3(Mathf.Cos(phi) * ring, yu, Mathf.Sin(phi) * ring);
                var ctr = new Vector3(0f, 0.32f, 0f) + Vector3.Scale(dir, new Vector3(0.55f, 0.48f, 0.55f));
                float r = R(0.09f, 0.14f);
                Ball(ctr, Vector3.one * r, 1f, R(0f, 10f));
            }
            return SaveFoliageMesh(key, v, n, c, t);
        }

        // 채색 땅 그림 (입체 채색 덤불·잎과 어울리게, 사진풍 흙 그림 대신): 바탕 + 넓은 얼룩(높은 곳 두 단 · 낮은 곳 한 단)을 납작한 색 띠로 (셀 명암처럼),
        //   그 위에 가늘고 긴 조각(마른 잎·솔잎·뼛조각)과 작은 돌, 둥근 웅덩이·얼룩을 드문드문. 격자 잡음을 감아 돌려 이음매 없이 이어져요.
        //   한 번 만들어 두면 그대로 써요 (파일을 지우면 다시 만들어요)
        //   죽림(지역 0): 밝은 황토빛 올리브 흙 · 이끼 얼룩 · 마른 대나무 잎
        //   폐사찰 바깥(1): 잿빛 베이지 흙 · 이끼 얼룩 · 떨어진 솔잎 · 돌 조각
        //   장강(2): 젖은 모래 강변 · 짙은 젖은 흙 · 하늘빛 물웅덩이 · 갈대 잎 · 조약돌 (방 바닥과 바깥 땅)
        //   천마총 바깥(3): 잿빛 자주 재 땅 · 검붉은 핏자국 얼룩 · 흰 뼛조각
        const string BambooGroundPath = "Assets/_Game/Art/Env/BurningBamboo/Floor_BambooGrove.png";
        const string StylizedGroundFolder = "Assets/_Game/Art/Env/Outside";

        static Texture2D BambooGroundTexture() => StylizedGround(BambooGroundPath, 4242, "#CDB47C", "#93AE58", "#A9C46A", "#A88E5E",
            new[] { ("#D2B460", 49, 9f, 14f, 1.8f, 2.6f), ("#A8BE5A", 21, 9f, 14f, 1.8f, 2.6f) }, 36, "#9E978A", "#C2BCAE", null);

        static Texture2D TempleGroundTexture() => StylizedGround($"{StylizedGroundFolder}/Ground_TempleColor.png", 5151, "#B8AA8E", "#8FA05E", "#A3B370", "#968872",
            new[] { ("#4F6B3A", 90, 5f, 8f, 0.9f, 1.3f), ("#7C8A55", 40, 5f, 8f, 0.9f, 1.3f) }, 55, "#8E8A82", "#B9B5AC", null);

        static Texture2D RiverGroundTexture() => StylizedGround($"{StylizedGroundFolder}/Ground_RiverColor.png", 6262, "#BFB08C", "#8E8566", "#9D9575", "#A69A78",
            new[] { ("#9AA25E", 40, 10f, 16f, 1.6f, 2.4f) }, 60, "#8C8C88", "#B8B8B2", new[] { ("#7FA9BF", 9, 16f, 30f), ("#9CC0D0", 6, 8f, 14f) });

        static Texture2D TombGroundTexture() => StylizedGround($"{StylizedGroundFolder}/Ground_TombColor.png", 7373, "#5A5058", "#4A4249", "#433B42", "#6A5E64",
            new[] { ("#D8D0C0", 45, 4f, 8f, 1.2f, 1.8f) }, 30, "#3E383C", "#6C6468", new[] { ("#6B1E1C", 10, 10f, 22f), ("#8A2A24", 6, 5f, 10f) });

        static Texture2D StylizedGround(string path, int seed, string baseHex, string highHex, string highLightHex, string lowHex,
            (string hex, int count, float lenMin, float lenMax, float widMin, float widMax)[] specks, int pebbles, string pebbleHex, string pebbleLightHex,
            (string hex, int count, float rMin, float rMax)[] blobs)
        {
            if (!System.IO.File.Exists(path))
            {
                const int S = 512;
                var rnd = new System.Random(seed);
                float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
                // 감아 도는 값 잡음 (칸 수가 S를 나눠야 이어져요)
                float[] Grid(int cells) { var g = new float[cells * cells]; for (int i = 0; i < g.Length; i++) g[i] = (float)rnd.NextDouble(); return g; }
                var g1 = Grid(4); var g2 = Grid(8); var g3 = Grid(16); var g4 = Grid(64);
                float Sample(float[] g, int cells, float x, float y)
                {
                    float fx = x / S * cells, fy = y / S * cells;
                    int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                    float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
                    float V(int i, int j) => g[((j % cells + cells) % cells) * cells + ((i % cells + cells) % cells)];
                    return Mathf.Lerp(Mathf.Lerp(V(x0, y0), V(x0 + 1, y0), tx), Mathf.Lerp(V(x0, y0 + 1), V(x0 + 1, y0 + 1), tx), ty);
                }
                Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
                var baseCol = Hex(baseHex); var high = Hex(highHex); var highLight = Hex(highLightHex); var low = Hex(lowHex);
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float n = Sample(g1, 4, x, y) * 0.5f + Sample(g2, 8, x, y) * 0.3f + Sample(g3, 16, x, y) * 0.2f;
                        n = Mathf.Clamp01((n - 0.5f) * 2.4f + 0.5f);   // 잡음을 섞으면 가운데로 몰려요: 얼룩이 보이게 펴요
                        float fine = Sample(g4, 64, x, y);
                        // 납작한 색 띠: 높은 곳 두 단 · 바탕 · 낮은 곳, 경계는 좁게 부드럽게
                        var c = baseCol;
                        c = Color.Lerp(c, high, Mathf.SmoothStep(0.56f, 0.6f, n));
                        c = Color.Lerp(c, highLight, Mathf.SmoothStep(0.68f, 0.71f, n));
                        c = Color.Lerp(c, low, Mathf.SmoothStep(0.36f, 0.32f, n));
                        c *= 0.96f + fine * 0.08f;
                        c.a = 1f;
                        px[y * S + x] = c;
                    }
                void Dot(float cx, float cy, float rx, float ry, float rot, Color col)
                {
                    float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                    int r = Mathf.CeilToInt(Mathf.Max(rx, ry)) + 1;
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            float u = (dx * cs + dy * sn) / rx, v = (-dx * sn + dy * cs) / ry;
                            float d = u * u + v * v;
                            if (d > 1f) continue;
                            int xx = (((int)cx + dx) % S + S) % S, yy = (((int)cy + dy) % S + S) % S;
                            px[yy * S + xx] = Color.Lerp(px[yy * S + xx], col, Mathf.Clamp01((1f - d) * 4f));
                        }
                }
                // 웅덩이·얼룩: 큰 둥근 덩이 두셋을 겹쳐 울퉁불퉁하게 (조각·돌보다 먼저 깔아요)
                if (blobs != null)
                    foreach (var (hex, count, rMin, rMax) in blobs)
                        for (int k = 0; k < count; k++)
                        {
                            float cx = R(0f, S), cy = R(0f, S), r = R(rMin, rMax);
                            for (int j = 0; j < 3; j++)
                                Dot(cx + R(-r, r) * 0.6f, cy + R(-r, r) * 0.5f, r * R(0.7f, 1.1f), r * R(0.5f, 0.8f), R(0f, Mathf.PI), Hex(hex));
                        }
                // 가늘고 긴 조각 (잎·솔잎·뼛조각)
                foreach (var (hex, count, lenMin, lenMax, widMin, widMax) in specks)
                    for (int k = 0; k < count; k++)
                        Dot(R(0f, S), R(0f, S), R(lenMin, lenMax), R(widMin, widMax), R(0f, Mathf.PI), Hex(hex));
                // 작은 돌: 동그라미 + 위쪽 밝은 점
                for (int k = 0; k < pebbles; k++)
                {
                    float cx = R(0f, S), cy = R(0f, S), r = R(2.5f, 5f);
                    Dot(cx, cy, r * 1.2f, r, R(0f, 3f), Hex(pebbleHex));
                    Dot(cx - r * 0.25f, cy + r * 0.25f, r * 0.5f, r * 0.4f, 0f, Hex(pebbleLightHex));
                }
                var tex = new Texture2D(S, S, TextureFormat.RGB24, false);
                tex.SetPixels(px);
                tex.Apply();
                EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            ImportAsTiledTexture(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 폐사찰 소나무 솔잎 층: 납작한 입체 덤불 덩어리에 짙은 청록 솔잎색 (예전 덤불 그림 카드 덩어리 대신)
        static Material PineNeedleMaterial()
        {
            var m = FantasyLook(GetInkMaterial("Ink3D_PineNeedles", "#5E8C78", 1f));
            m.SetColor("_BaseColor", new Color(0.37f, 0.55f, 0.47f, 1f));   // 덤불 초록(정점 색)에 곱해 짙은 청록 솔잎으로
            m.SetFloat("_VertexTint", 1f);
            m.SetFloat("_OutlineWidth", 2.5f);
            m.SetFloat("_RimInk", 0.15f);
            m.SetFloat("_ShadowInk", 0.35f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // 정팔면체를 두 번 쪼갠 공 (삼각형 128개, 꼭짓점은 삼각형마다 따로: 방향만)
        static List<Vector3> s_sphereTris;
        static List<Vector3> UnitSphereTris()
        {
            if (s_sphereTris != null) return s_sphereTris;
            var tris = new List<Vector3>();
            Vector3[] o = { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
            int[,] f = { { 0, 1, 2 }, { 0, 2, 3 }, { 0, 3, 4 }, { 0, 4, 1 }, { 5, 2, 1 }, { 5, 3, 2 }, { 5, 4, 3 }, { 5, 1, 4 } };
            for (int i = 0; i < 8; i++) tris.AddRange(new[] { o[f[i, 0]], o[f[i, 1]], o[f[i, 2]] });
            for (int s = 0; s < 2; s++)
            {
                var next = new List<Vector3>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                    next.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }
                tris = next;
            }
            for (int i = 0; i < tris.Count; i++) tris[i] = tris[i].normalized;
            s_sphereTris = tris;
            return tris;
        }
    }
}
