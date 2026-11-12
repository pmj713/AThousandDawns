using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 수묵 시험판: 적 투사체와 폭발에 수묵 겉모습을 입혀요 (게임 코드는 그대로, 새로 생긴 오브젝트 이름을 보고 렌더러만 끄고 모양을 덧달아요).
    //   "Arrow"     궁수 화살: 먹색 화살대·촉·깃 + 붓 먹선 꼬리. 근처에서 쏜 게 살수면 표창: 빙빙 도는 네 날 표창 + 가는 꼬리
    //               쏜 게 혈궁수(천마총)면 핏빛 화살대·깃 + 붉은 꼬리
    //   "Telegraph" (부모 없는 것 = EnemyDirector.SpawnBurst, 화약 산적 폭발·녹림왕 내려찍기): 바닥에 먹이 확 번지고,
    //               가운데 주홍 불꽃이 번쩍했다 빠지고, 먹 연기가 피어올라 옅어져요
    // 재질은 씬 빌더(PrototypeSceneBuilder.InkRunProps)가 채워요.
    public class InkProjectiles : MonoBehaviour
    {
        [SerializeField] Material lineMaterial;   // 붓선 (InkBrushLine + BrushLine)
        [SerializeField] Material blotMaterial;   // 먹 번짐 (InkBrushLine + InkBlot)
        [SerializeField] Material mistMaterial;   // 먹 연기 (InkBrushLine + InkMist)
        [SerializeField] Material shaftMaterial;  // 화살대·표창 (수묵 캐릭터 재질, 짙은 먹)
        [SerializeField] Material featherMaterial;

        const string Marker = "Ink Visual";
        readonly List<GameObject> _roots = new List<GameObject>();
        readonly HashSet<GameObject> _seenBursts = new HashSet<GameObject>();
        Mesh _starMesh;

        public void Setup(Material line, Material blot, Material mist, Material shaft, Material feather)
        {
            lineMaterial = line; blotMaterial = blot; mistMaterial = mist; shaftMaterial = shaft; featherMaterial = feather;
        }

        void Update()
        {
            SceneManager.GetActiveScene().GetRootGameObjects(_roots);
            foreach (var go in _roots)
            {
                if (go.name == "Arrow")
                {
                    if (go.transform.Find(Marker) == null) DressArrow(go);
                }
                else if (go.name == "Telegraph" && !_seenBursts.Contains(go))
                {
                    _seenBursts.RemoveWhere(x => x == null);
                    _seenBursts.Add(go);
                    if (IsBurst(go)) SpawnBurst(go);
                }
            }
        }

        // ── 화살·표창

        // 미리보기용: 화살·표창을 바로 입혀요 (표창이면 쏜 적을 찾지 않고)
        public void DressNow(GameObject arrow, bool shuriken)
        {
            var r = arrow.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var visual = new GameObject(Marker).transform;
            visual.SetParent(arrow.transform, false);
            var s = arrow.transform.lossyScale;
            visual.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            if (shuriken) BuildShuriken(visual); else BuildArrow(visual);
        }

        void DressArrow(GameObject arrow)
        {
            var r = arrow.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var t = arrow.transform;
            var visual = new GameObject(Marker).transform;
            visual.SetParent(t, false);
            var s = t.lossyScale;
            visual.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

            if (FiredByAssassin(t.position)) BuildShuriken(visual);
            else BuildArrow(visual, FiredBy(t.position, EnemyKind.BloodArcher)); // 혈궁수면 붉은 화살
        }

        // 쏜 자리 가까이 살수가 있으면 표창이에요 (화살과 표창은 같은 길로 나와요)
        static bool FiredByAssassin(Vector3 origin) => FiredBy(origin, EnemyKind.Assassin);

        static bool FiredBy(Vector3 origin, EnemyKind kind)
        {
            foreach (var e in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                if (e == null || e.Kind != kind) continue;
                var d = e.transform.position - origin;
                d.y = 0f;
                if (d.sqrMagnitude < 2.5f * 2.5f) return true;
            }
            return false;
        }

        // blood: 혈궁수 (천마총) — 핏빛 화살대·깃 + 붉은 꼬리
        void BuildArrow(Transform visual, bool blood = false)
        {
            var shaft = blood ? BloodMaterial(shaftMaterial, new Color(0.42f, 0.05f, 0.05f)) : shaftMaterial;
            var feather = blood ? BloodMaterial(featherMaterial, new Color(0.7f, 0.12f, 0.1f)) : featherMaterial;
            Box(visual, "Shaft", new Vector3(0f, 0f, -0.05f), new Vector3(0.035f, 0.035f, 0.78f), Quaternion.identity, shaft);
            Box(visual, "Head", new Vector3(0f, 0f, 0.38f), new Vector3(0.09f, 0.025f, 0.09f), Quaternion.Euler(0f, 45f, 0f), shaft);
            for (int i = -1; i <= 1; i += 2)
                Box(visual, "Fletch", new Vector3(i * 0.04f, 0.01f, -0.38f), new Vector3(0.06f, 0.012f, 0.16f), Quaternion.Euler(0f, i * 18f, 0f), feather);
            Trail(visual, new Vector3(0f, 0f, -0.42f), blood ? 0.2f : 0.16f, blood ? 0.3f : 0.22f, blood ? new Color(0.62f, 0.06f, 0.05f, 0.8f) : new Color(0.08f, 0.07f, 0.07f, 0.7f));
        }

        readonly System.Collections.Generic.Dictionary<Material, Material> _blood = new System.Collections.Generic.Dictionary<Material, Material>();
        Material BloodMaterial(Material source, Color color)
        {
            if (source == null) return null;
            if (_blood.TryGetValue(source, out var m)) return m;
            m = new Material(source) { name = source.name + " (Blood)" };
            m.SetColor("_BaseColor", color);
            _blood[source] = m;
            return m;
        }

        void BuildShuriken(Transform visual)
        {
            var spin = new GameObject("Spin").transform;
            spin.SetParent(visual, false);
            spin.gameObject.AddComponent<InkSpin>().Speed(-1080f);
            var star = new GameObject("Star");
            star.transform.SetParent(spin, false);
            star.transform.localScale = Vector3.one * 0.32f;
            star.AddComponent<MeshFilter>().sharedMesh = StarMesh();
            star.AddComponent<MeshRenderer>().sharedMaterial = shaftMaterial;
            Trail(visual, Vector3.zero, 0.1f, 0.15f, new Color(0.1f, 0.08f, 0.1f, 0.55f));
        }

        void Trail(Transform parent, Vector3 local, float width, float time, Color color)
        {
            if (lineMaterial == null) return;
            var go = new GameObject("Ink Trail");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = lineMaterial;
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = width;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) },
            };
            trail.alignment = LineAlignment.TransformZ;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 바닥과 나란히 납작하게
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        // 네 날 표창 (납작한 별, 위아래 두 면)
        Mesh StarMesh()
        {
            if (_starMesh != null) return _starMesh;
            var verts = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                float r = i % 2 == 0 ? 0.5f : 0.14f;
                verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            var tris = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % 8;
                tris.AddRange(new[] { 0, b, a, 0, a, b }); // 양면
            }
            _starMesh = new Mesh { name = "Ink Shuriken" };
            _starMesh.SetVertices(verts);
            _starMesh.SetTriangles(tris, 0);
            _starMesh.RecalculateNormals();
            return _starMesh;
        }

        static void Box(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ── 폭발

        // SpawnBurst만 바닥·채움 두 층에 같은 재질을 써요 (다른 표식은 바닥 재질이 따로). 그래서 그걸로 골라요
        static bool IsBurst(GameObject telegraph)
        {
            var b = telegraph.transform.Find("Base");
            var f = telegraph.transform.Find("Fill");
            if (b == null || f == null) return false;
            var br = b.GetComponent<Renderer>();
            var fr = f.GetComponent<Renderer>();
            return br != null && fr != null && br.enabled && br.sharedMaterial == fr.sharedMaterial;
        }

        void SpawnBurst(GameObject telegraph)
        {
            if (blotMaterial == null) return;
            float radius = 1.5f;
            var mf = telegraph.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) radius = Mathf.Max(0.5f, mf.sharedMesh.bounds.extents.x);
            var p = telegraph.transform.position;
            var burst = new GameObject("Ink Burst").AddComponent<InkBurst>();
            burst.transform.position = new Vector3(p.x, 0f, p.z);
            burst.Play(radius, blotMaterial, mistMaterial);
        }
    }

    // 폭발 하나: 먹 번짐(바닥) → 주홍 불꽃(가운데) → 먹 연기(위로). 1.6초 뒤 사라져요
    public class InkBurst : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color Ink = new Color(0.07f, 0.06f, 0.06f, 0.92f);
        static readonly Color Flame = new Color(0.92f, 0.36f, 0.12f, 0.95f);
        static readonly Color Smoke = new Color(0.18f, 0.16f, 0.16f, 0.6f);

        Renderer _splash, _flash;
        readonly List<(Renderer r, Vector3 drift, float size)> _smoke = new List<(Renderer, Vector3, float)>();
        float _radius, _start;
        MaterialPropertyBlock _block;

        public void Play(float radius, Material blot, Material mist)
        {
            _radius = radius;
            _start = Time.time;
            _block = new MaterialPropertyBlock();
            float yaw = Random.Range(0f, 360f);
            _splash = Card("Splash", blot, new Vector3(0f, 0.06f, 0f), Quaternion.Euler(90f, yaw, 0f));
            _flash = Card("Flash", blot, new Vector3(0f, 0.08f, 0f), Quaternion.Euler(90f, yaw + 70f, 0f));
            if (mist != null)
            {
                var cam = Camera.main;
                for (int i = 0; i < 5; i++)
                {
                    var dir = Quaternion.Euler(0f, i * 72f + Random.Range(-20f, 20f), 0f) * Vector3.forward;
                    var rot = cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
                    var r = Card("Smoke", mist, dir * radius * 0.3f + Vector3.up * 0.4f, rot * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                    _smoke.Add((r, dir * radius * 0.5f + Vector3.up * Random.Range(0.9f, 1.4f), radius * Random.Range(0.8f, 1.2f)));
                }
            }
            Update();
        }

        Renderer Card(string name, Material mat, Vector3 local, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void Set(Renderer r, Color c)
        {
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        void Update()
        {
            float t = Time.time - _start;
            // 먹 번짐: 0.15초에 확 퍼졌다가 1.6초까지 옅어져요
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.15f), 3f);
            _splash.transform.localScale = Vector3.one * _radius * Mathf.Lerp(0.5f, 2.3f, grow);
            Set(_splash, new Color(Ink.r, Ink.g, Ink.b, Ink.a * (1f - Mathf.Clamp01((t - 0.5f) / 1.1f))));
            // 불꽃: 가운데에서 번쩍했다가 0.3초에 빠져요
            _flash.transform.localScale = Vector3.one * _radius * Mathf.Lerp(0.6f, 1.5f, Mathf.Clamp01(t / 0.12f));
            Set(_flash, new Color(Flame.r, Flame.g, Flame.b, Flame.a * (1f - Mathf.Clamp01(t / 0.3f))));
            // 연기: 위로 피어오르며 커지고 옅어져요
            float u = Mathf.Clamp01(t / 1.4f);
            foreach (var (r, drift, size) in _smoke)
            {
                r.transform.localPosition += drift * Time.deltaTime / 1.4f;
                r.transform.localScale = Vector3.one * size * Mathf.Lerp(0.6f, 1.6f, u);
                Set(r, new Color(Smoke.r, Smoke.g, Smoke.b, Smoke.a * Mathf.Sin(Mathf.Clamp01(t / 1.4f) * Mathf.PI)));
            }
            if (t > 1.6f) Destroy(gameObject);
        }
    }
}
