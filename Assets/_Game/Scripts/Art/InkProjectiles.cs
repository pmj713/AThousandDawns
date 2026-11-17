using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 수묵 시험판: 적 투사체와 폭발에 수묵 겉모습을 입혀요 (게임 코드는 그대로, 새로 생긴 오브젝트 이름을 보고 렌더러만 끄고 모양을 덧달아요).
    //   "Arrow"     궁수 화살: 먹색 화살대·촉·깃 + 붓 먹선 꼬리. 근처에서 쏜 게 살수면 표창: 빙빙 도는 네 날 표창 + 가는 꼬리
    //               쏜 게 혈궁수(천마총)면 핏빛 화살대·깃 + 붉은 꼬리
    //               쏜 게 고블린 궁수(차원 겹침, Arrow.Owner)면 금빛 화살대·붉은 유리 촉·청록 깃 + 붉은 금빛 꼬리
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
            // 처음 보는 루트만 이름을 봐요 (go.name을 읽을 때마다 문자열이 생겨, 매 프레임 모든 루트를 읽으면 쓰레기가 많이 나와요)
            SceneManager.GetActiveScene().GetRootGameObjects(_roots);
            foreach (var go in _roots)
            {
                if (!_seenBursts.Add(go)) continue;
                if (go.name == "Arrow")
                {
                    if (go.transform.Find(Marker) == null) DressArrow(go);
                }
                else if (go.name == "Telegraph" && IsBurst(go)) SpawnBurst(go);
            }
            if ((_pruneTimer -= Time.unscaledDeltaTime) <= 0f) // 사라진 루트는 가끔 비워요
            {
                _pruneTimer = 2f;
                _seenBursts.RemoveWhere(IsGone);
            }
        }

        float _pruneTimer;
        static readonly System.Predicate<GameObject> IsGone = x => x == null;

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

            var shooter = ShooterKind(arrow);
            if (shooter == EnemyKind.GoblinArcher) BuildGoblinArrow(visual); // 차원 겹침: 붉은·금빛 유리 화살
            else if (shooter == EnemyKind.Imp) BuildImpBolt(visual); // 임프 화염탄: 작은 불덩이 + 불꼬리
            else if (shooter != null && BuildVolley(visual, shooter.Value)) { } // 보스 부채꼴 견제 (BossVolley): 보스마다 다른 투사체
            else if (FiredByAssassin(t.position)) BuildShuriken(visual);
            else BuildArrow(visual, FiredBy(t.position, EnemyKind.BloodArcher)); // 혈궁수면 붉은 화살
        }

        // 쏜 적 (Arrow.Owner) — 화살마다 처음 한 번만 읽어요

        static EnemyKind? ShooterKind(GameObject go)
        {
            var arrow = go.GetComponent<Arrow>();
            if (arrow == null) return null;
            var owner = arrow.Owner;
            var brain = owner != null ? owner.GetComponentInParent<EnemyBrain>() : null;
            return brain != null ? brain.Kind : (EnemyKind?)null;
        }

        // 고블린 궁수 (판타지): 금빛 화살대 · 붉은 유리 촉 · 청록 깃 + 붉은→금빛 빛 꼬리 (수묵과 다른 또렷한 채색)
        // 임프 화염탄 (판타지): 돌아가는 작은 불덩이 + 불길 + 주홍 불꼬리. 사라질 때(맞거나 사거리 끝) 작은 불꽃이 튀어요 (InkImpBolt)
        // (플레이테스트: 문장 그림 + 위로 솟는 불길이 어색) → 빛나는 불덩이 구슬 (흰 노란 속 + 주황 겉불이 일렁여요) + 화면을 향한 가는 불꼬리 + 드문 불티
        void BuildImpBolt(Transform visual)
        {
            var glow = GlowSprite();
            if (glow != null)
            {
                var pivot = new GameObject("Bolt").transform;
                pivot.SetParent(visual, false);
                pivot.gameObject.AddComponent<InkFaceCamera>();
                Orb(pivot, glow, 0.75f, new Color(1f, 0.38f, 0.08f, 0.85f), 4);   // 겉불
                Orb(pivot, glow, 0.38f, new Color(1f, 0.92f, 0.6f, 1f), 5);       // 속
                pivot.gameObject.AddComponent<InkFlicker>();
            }
            InkParticles.Embers(visual, Vector3.zero, 0.08f, 0.6f);
            var tail = new GameObject("Fire Tail");
            tail.transform.SetParent(visual, false);
            var trail = tail.AddComponent<TrailRenderer>();
            trail.sharedMaterial = GlowMaterial();
            trail.time = 0.18f;
            trail.minVertexDistance = 0.04f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = 0.32f;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(0.9f, 0.2f, 0.05f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            trail.alignment = LineAlignment.View; // 카메라를 향해 (바닥에 눕히면 납작해 보여요)
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            visual.gameObject.AddComponent<InkImpBolt>();
        }

        static void Orb(Transform parent, Sprite glow, float size, Color color, int order)
        {
            var go = new GameObject("Orb");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, glow.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = glow;
            sr.color = color;
            sr.sortingOrder = order;
        }

        // 부드러운 둥근 빛 (FxArt/InkMist 흰 마스크를 스프라이트로) · 꼬리용 재질 — 한 번만 만들어요
        static Sprite _glow;
        static Material _glowMat;
        static Sprite GlowSprite()
        {
            if (_glow != null) return _glow;
            var tex = Resources.Load<Texture2D>("FxArt/InkMist");
            if (tex != null) _glow = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _glow;
        }
        static Material GlowMaterial()
        {
            if (_glowMat != null) return _glowMat;
            _glowMat = new Material(Shader.Find("Sprites/Default")) { name = "Imp Fire Tail" };
            var tex = Resources.Load<Texture2D>("FxArt/InkMist");
            if (tex != null) _glowMat.mainTexture = tex;
            return _glowMat;
        }

        // 보스 부채꼴 견제 3발 (EnemyBrain.BossVolley, "Arrow"의 Owner가 그 보스): 녹림왕 대도 검풍 = 금빛 검기 초승달 · 혈불 혈장 = 핏빛 손바닥
        //   철룡 작살 = 작살 · 천마 마기탄 = 진홍 마기 덩어리 + 보랏빛 불티 · 오우거 돌팔매 = 구르는 바위. 꼬리 색도 보스마다.
        bool BuildVolley(Transform visual, EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Nokrim: Flat(visual, "SkillFx/SwordQi", 1f, 0f, new Color(1f, 0.85f, 0.35f)); Trail(visual, Vector3.zero, 0.3f, 0.2f, new Color(1f, 0.75f, 0.25f, 0.8f)); return true;
                case EnemyKind.BloodBuddha: Face(visual, "BloodBuddhaFx/Palm", 0.75f, false); Trail(visual, Vector3.zero, 0.3f, 0.25f, new Color(0.95f, 0.15f, 0.1f, 0.85f)); return true;
                case EnemyKind.IronDragon: Flat(visual, "RiverFx/Harpoon", 1.1f, 90f, Color.white); Trail(visual, new Vector3(0f, 0f, -0.5f), 0.12f, 0.2f, new Color(0.45f, 0.75f, 1f, 0.7f)); return true;
                case EnemyKind.HeavenlyDemon:
                    Face(visual, "HeavenlyDemonFx/Wisp", 0.7f, false);
                    InkParticles.Embers(visual, Vector3.zero, 0.2f, 1.5f);
                    Trail(visual, Vector3.zero, 0.3f, 0.3f, new Color(0.75f, 0.1f, 0.35f, 0.85f)); return true;
                case EnemyKind.Ogre: Face(visual, "TempleFx/Rock", 0.6f, true); Trail(visual, Vector3.zero, 0.2f, 0.18f, new Color(0.8f, 0.65f, 0.45f, 0.6f)); return true;
                // 고수 견제 (약한 판, 2발): 청죽객 죽엽비도 · 철권 마웅 권풍 · 혜광 금강 염주 · 탁랑객 물보라 갈고리 · 혈영검마 혈영 비검 · 고블린 족장 투척 도끼
                case EnemyKind.Qingzhu: Flat(visual, "MasterFx/LeafKnife", 0.7f, 0f, Color.white); Trail(visual, Vector3.zero, 0.14f, 0.2f, new Color(0.45f, 0.95f, 0.35f, 0.8f)); return true;
                case EnemyKind.IronFist: Face(visual, "MasterFx/FistWind", 0.8f, false); Trail(visual, Vector3.zero, 0.35f, 0.22f, new Color(0.6f, 0.9f, 1f, 0.6f)); return true;
                case EnemyKind.Hyegwang: Face(visual, "MasterFx/Bead", 0.5f, false); Trail(visual, Vector3.zero, 0.2f, 0.22f, new Color(1f, 0.82f, 0.25f, 0.85f)); return true;
                case EnemyKind.Takrang: Flat(visual, "MasterFx/Hook", 0.8f, 0f, Color.white); InkParticles.Frost(visual, Vector3.zero, 0.15f, 1.2f); Trail(visual, new Vector3(0f, 0f, -0.3f), 0.16f, 0.25f, new Color(0.35f, 0.7f, 1f, 0.8f)); return true;
                case EnemyKind.BloodShadow: Flat(visual, "MasterFx/BloodDagger", 0.65f, 0f, Color.white); Trail(visual, Vector3.zero, 0.16f, 0.25f, new Color(0.95f, 0.1f, 0.15f, 0.85f)); return true;
                case EnemyKind.GoblinChief: Face(visual, "MasterFx/Axe", 0.6f, true); Trail(visual, Vector3.zero, 0.14f, 0.18f, new Color(0.8f, 0.7f, 0.5f, 0.6f)); return true;
                case EnemyKind.DarkElf: Flat(visual, "DarkElfFx/PoisonDagger", 0.7f, 0f, Color.white); Trail(visual, Vector3.zero, 0.14f, 0.22f, new Color(0.5f, 1f, 0.3f, 0.8f)); return true; // 다크 엘프 독 단검
                case EnemyKind.DeathKnight: Flat(visual, "DeathKnightFx/GhostCrescent", 1.2f, -90f, Color.white); InkParticles.Frost(visual, Vector3.zero, 0.3f, 1.5f); Trail(visual, Vector3.zero, 0.35f, 0.25f, new Color(0.3f, 1f, 0.85f, 0.8f)); return true; // 데스나이트 저주 검기: 청록 유령불 초승달
            }
            return false;
        }

        static readonly System.Collections.Generic.Dictionary<string, Sprite> _volley = new System.Collections.Generic.Dictionary<string, Sprite>();
        static Sprite VolleySprite(string path)
        {
            if (!_volley.TryGetValue(path, out var s)) _volley[path] = s = Resources.Load<Sprite>(path);
            return s;
        }

        // 바닥과 나란히 누운 그림 (그림 위쪽이 날아가는 쪽, roll = 그림을 먼저 돌리는 각 · 작살은 오른쪽을 보고 있어서 90)
        static void Flat(Transform visual, string path, float width, float roll, Color color)
        {
            var sprite = VolleySprite(path);
            if (sprite == null) return;
            var go = new GameObject("Volley");
            go.transform.SetParent(visual, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, roll);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
        }

        // 카메라를 보는 그림 (roll이면 굴러가요)
        static void Face(Transform visual, string path, float width, bool roll)
        {
            var sprite = VolleySprite(path);
            if (sprite == null) return;
            var pivot = new GameObject("Volley").transform;
            pivot.SetParent(visual, false);
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)));
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            if (roll) go.AddComponent<InkRoll>();
        }

        void BuildGoblinArrow(Transform visual)
        {
            var shaft = Tinted(shaftMaterial, new Color(0.85f, 0.6f, 0.15f));
            var head = Tinted(featherMaterial, new Color(0.85f, 0.1f, 0.12f));
            var feather = Tinted(featherMaterial, new Color(0.1f, 0.65f, 0.6f), "Teal");
            Box(visual, "Shaft", new Vector3(0f, 0f, -0.05f), new Vector3(0.04f, 0.04f, 0.78f), Quaternion.identity, shaft);
            Box(visual, "Head", new Vector3(0f, 0f, 0.38f), new Vector3(0.11f, 0.03f, 0.11f), Quaternion.Euler(0f, 45f, 0f), head);
            for (int i = -1; i <= 1; i += 2)
                Box(visual, "Fletch", new Vector3(i * 0.04f, 0.01f, -0.38f), new Vector3(0.07f, 0.012f, 0.16f), Quaternion.Euler(0f, i * 18f, 0f), feather);
            Trail(visual, new Vector3(0f, 0f, -0.42f), 0.2f, 0.28f, new Color(1f, 0.55f, 0.15f, 0.9f));
        }

        readonly Dictionary<string, Material> _tinted = new Dictionary<string, Material>();
        Material Tinted(Material source, Color color, string key = null)
        {
            if (source == null) return null;
            key = source.name + (key ?? color.ToString());
            if (_tinted.TryGetValue(key, out var m)) return m;
            m = new Material(source) { name = source.name + " (Fantasy)" };
            m.SetColor("_BaseColor", color);
            _tinted[key] = m;
            return m;
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
            Trail(visual, new Vector3(0f, 0f, -0.42f), blood ? 0.2f : 0.16f, blood ? 0.3f : 0.22f, blood ? new Color(0.62f, 0.06f, 0.05f, 0.8f) : new Color(1f, 0.78f, 0.35f, 0.75f));
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
            Trail(visual, Vector3.zero, 0.1f, 0.15f, new Color(0.55f, 0.75f, 1f, 0.6f));
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
        static readonly Color Ink = new Color(1f, 0.38f, 0.1f, 0.92f);
        static readonly Color Flame = new Color(0.92f, 0.36f, 0.12f, 0.95f);
        static readonly Color Smoke = new Color(0.5f, 0.44f, 0.4f, 0.6f);

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

    // 불덩이 일렁임: 크기가 빠르게 들썩여요
    public class InkFlicker : MonoBehaviour
    {
        float _seed;
        void Start() => _seed = Random.Range(0f, 10f);
        void Update() { float t = Time.time * 18f + _seed; transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t) + 0.06f * Mathf.Sin(t * 2.3f)); }
    }

    // 그림을 화면 안에서 빙글빙글 (구르는 바위)
    public class InkRoll : MonoBehaviour
    {
        void Update() => transform.Rotate(0f, 0f, -540f * Time.deltaTime, Space.Self);
    }

    // 임프 화염탄이 사라질 때 (맞거나 사거리 끝) 그 자리에 작은 불꽃이 튀어요. 씬이 닫히며 지워질 때는 그리지 않아요.
    public class InkImpBolt : MonoBehaviour
    {
        void OnDestroy()
        {
            if (!gameObject.scene.isLoaded) return;
            InkParticles.Burst(transform.position, 12, new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.3f, 0.05f, 0.9f), 2.4f, 0.12f, 0.35f, 0.5f);
        }
    }
}
