using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 수묵 시험판: 정예 적을 수묵 모델에서도 알아보게 해요 (게임 코드·적 모델 파일은 그대로).
    //   머리 위: 원래 도형(네모·공·기둥) 대신 카메라를 보는 붓 표식 그림 (철포삼 쇠 방패 · 회춘 새싹 · 열화 불꽃 · 분열 금 간 보랏빛 먹 원), 천천히 위아래로 떠요
    //   특성이 둘 이상이면 표식을 머리 위에 가로로 나란히 (게임의 "Elite Marks" 자리), 높이는 수묵 모델의 실제 머리 꼭대기 + 0.35. 특성이 더 붙으면 다시 그려요.
    //   특성이 둘이면 (장강 수채부터) 발밑 고리도 둘 (둘째는 조금 크게 반대로 돌아요), 연기는 두 색을 섞어요
    //   몸 둘레: 먹·붉은 기운. 발밑에 붓으로 그은 고리, 몸 둘레로 피어오르는 먹 연기 (특성 색이 살짝 섞여요)
    // 적 본체 자식으로 달아서 모델이 움찔·회전해도 흔들리지 않고, 숨은 적(풀숲 청죽사 등)이면 같이 숨어요.
    public class InkEliteMarks : MonoBehaviour
    {
        [SerializeField] Sprite ironMark, rejuvMark, blazeMark;
        [SerializeField] Material mistMaterial;   // InkBrushLine + InkMist
        [SerializeField] Material lineMaterial;   // InkBrushLine + BrushLine

        const string Marker = "Ink Elite Aura";
        const float ScanInterval = 0.25f, MarkGap = 0.45f, AboveHead = 0.35f;

        // 정예 하나: 기운 묶음 + 머리 위 표식들 (특성 수가 바뀌면 다시 그려요)
        sealed class Dressed
        {
            public EnemyBrain Enemy;
            public GameObject Aura;
            public int TraitCount;
            public readonly List<EliteTrait> Traits = new List<EliteTrait>();
            public readonly List<Transform> Pivots = new List<Transform>();  // 표식 자리 (월드 위치를 매 프레임 맞춰요)
            public readonly List<Transform> Olds = new List<Transform>();    // 게임의 원래 도형 (나란히 놓인 자리)
            public float HeadY = -1f, HeadTimer;                              // 수묵 모델의 실제 머리 높이 (발 기준)
        }

        float _nextScan;
        readonly Dictionary<EnemyBrain, Dressed> _dressed = new Dictionary<EnemyBrain, Dressed>();
        readonly List<EnemyBrain> _gone = new List<EnemyBrain>();
        readonly List<EliteTrait> _traitScratch = new List<EliteTrait>();

        public void Setup(Sprite iron, Sprite rejuv, Sprite blaze, Material mist, Material line)
        {
            ironMark = iron; rejuvMark = rejuv; blazeMark = blaze; mistMaterial = mist; lineMaterial = line;
        }

        void Update()
        {
            // 숨거나 쓰러지면 기운도 같이 · 머리 위 표식은 원래 도형 자리(가로로 나란히)와 모델 머리 높이에 맞춰요
            _gone.Clear();
            foreach (var pair in _dressed)
            {
                var d = pair.Value;
                if (d.Enemy == null || d.Aura == null) { _gone.Add(pair.Key); continue; }
                bool show = !d.Enemy.IsHidden && !d.Enemy.IsDead;
                if (d.Aura.activeSelf != show) d.Aura.SetActive(show);
                if (show) PlaceMarks(d);
            }
            for (int i = 0; i < _gone.Count; i++) _dressed.Remove(_gone[i]);

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;
            foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                if (enemy == null || !enemy.IsElite) continue;
                int count = Traits(enemy, _traitScratch);
                if (_dressed.TryGetValue(enemy, out var old))
                {
                    if (old.TraitCount == count) continue;
                    if (old.Aura != null) Destroy(old.Aura); // 특성이 하나 더 붙었어요 (AddEliteTrait): 다시 그려요
                    _dressed.Remove(enemy);
                }
                _dressed[enemy] = Dress(enemy);
            }
        }

        // 특성 (중복 없이, 하나도 없으면 첫 특성)
        static int Traits(EnemyBrain enemy, List<EliteTrait> into)
        {
            into.Clear();
            foreach (var t in enemy.Traits) if (t != EliteTrait.None && !into.Contains(t)) into.Add(t);
            if (into.Count == 0) into.Add(enemy.Trait);
            return into.Count;
        }

        // 게임의 원래 도형: 이제 "Elite Marks" 묶음 아래 (예전엔 적 바로 아래)
        static Transform OldMark(EnemyBrain enemy, EliteTrait trait)
        {
            var t = enemy.transform.Find($"Elite Marks/Elite_{trait}");
            return t != null ? t : enemy.transform.Find($"Elite_{trait}");
        }

        void PlaceMarks(Dressed d)
        {
            var root = d.Enemy.transform.position;
            d.HeadTimer -= Time.deltaTime;
            if (d.HeadY < 0f || d.HeadTimer <= 0f) { d.HeadTimer = 0.5f; d.HeadY = HeadHeight(d); }
            var cam = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            cam.y = 0f;
            int n = d.Pivots.Count;
            for (int i = 0; i < n; i++)
            {
                var pivot = d.Pivots[i];
                if (pivot == null) continue;
                var old = d.Olds[i];
                if (old == null) { old = OldMark(d.Enemy, d.Traits[i]); d.Olds[i] = old; if (old != null) HideOld(old); }
                Vector3 p = old != null ? old.position // 게임이 가로로 나란히 둔 자리
                    : root + cam.normalized * ((i - (n - 1) * 0.5f) * MarkGap); // 없으면 화면 가로로 나란히
                pivot.position = new Vector3(p.x, root.y + d.HeadY + AboveHead, p.z);
            }
        }

        static void HideOld(Transform old)
        {
            foreach (var r in old.GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        // 수묵 모델의 머리 꼭대기 (발 기준 높이): 몸 렌더러들의 위쪽 끝. 표식·고리·연기·입자는 빼요. 못 찾으면 게임 키 × 1.2
        static readonly List<Renderer> RendererBuffer = new List<Renderer>();

        static float HeadHeight(Dressed d)
        {
            var enemy = d.Enemy;
            float baseY = enemy.transform.position.y, top = float.MinValue;
            enemy.GetComponentsInChildren(RendererBuffer); // 할당 없이 (0.5초마다)
            foreach (var r in RendererBuffer)
            {
                if (!r.enabled || r is SpriteRenderer || r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
                if (r.transform.IsChildOf(d.Aura.transform)) continue;
                if (r.name.StartsWith("Elite") || r.transform.parent != null && r.transform.parent.name == "Elite Marks") continue;
                top = Mathf.Max(top, r.bounds.max.y);
            }
            return top > baseY + 0.3f ? top - baseY : enemy.Height * 1.2f;
        }

        Dressed Dress(EnemyBrain enemy)
        {
            var d = new Dressed { Enemy = enemy };
            Traits(enemy, d.Traits);
            d.TraitCount = d.Traits.Count;
            var traits = d.Traits;

            var aura = new GameObject(Marker);
            aura.transform.SetParent(enemy.transform, false);
            d.Aura = aura;
            Color tint = Tint(traits[0]);
            if (traits.Count > 1) tint = Color.Lerp(tint, Tint(traits[1]), 0.5f); // 연기는 두 특성 색을 섞어요

            // 머리 위 표식: 특성마다 하나, 가로로 나란히 (자리는 PlaceMarks가 매 프레임 맞춰요)
            for (int i = 0; i < traits.Count; i++)
            {
                var pivot = new GameObject("Mark Pivot").transform;
                pivot.SetParent(aura.transform, false);
                d.Pivots.Add(pivot);
                var old = OldMark(enemy, traits[i]);
                if (old != null) HideOld(old); // 원래 도형은 숨겨요
                d.Olds.Add(old);
                var sprite = IsSplit(traits[i]) ? SplitMark : traits[i] == EliteTrait.IronShirt ? ironMark : traits[i] == EliteTrait.Rejuvenation ? rejuvMark : blazeMark;
                if (sprite == null) continue;
                var mark = new GameObject("Mark");
                mark.transform.SetParent(pivot, false);
                var sr = mark.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                float k = 0.42f / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y); // 나란히 놓여도 겹치지 않게 (간격 0.45)
                mark.transform.localScale = Vector3.one * k;
                mark.AddComponent<InkFaceCamera>().Bob(0.05f, 1.6f + i * 0.3f); // 조금 엇갈려 떠요
            }
            PlaceMarks(d);

            float radius = 0.55f * 1.2f;
            // 발밑 붓 고리 (먹 + 특성 색) — 특성마다 하나, 둘째는 조금 크게 반대로 돌아요
            if (lineMaterial != null)
                for (int t = 0; t < traits.Count; t++)
                {
                    float rr = radius * (1f + t * 0.22f);
                    var ring = new GameObject("Ring").AddComponent<LineRenderer>();
                    ring.transform.SetParent(aura.transform, false);
                    ring.useWorldSpace = false;
                    ring.loop = true;
                    ring.sharedMaterial = lineMaterial;
                    ring.widthMultiplier = 0.12f;
                    ring.alignment = LineAlignment.TransformZ;
                    ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    const int n = 32;
                    ring.positionCount = n;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * Mathf.PI * 2f / n;
                        ring.SetPosition(i, new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, -0.04f)); // 회전한 고리라 z가 위
                    }
                    var c = Color.Lerp(new Color(0.08f, 0.06f, 0.06f), Tint(traits[t]), 0.5f);
                    ring.startColor = ring.endColor = new Color(c.r, c.g, c.b, 0.85f);
                    ring.gameObject.AddComponent<InkSpin>().Speed(t == 0 ? 40f : -30f);
                }

            // 몸 둘레 먹 연기 (붉은 기운이 섞인 먹)
            if (mistMaterial != null)
            {
                var smoke = Color.Lerp(new Color(0.1f, 0.08f, 0.08f), tint, 0.35f);
                for (int i = 0; i < 4; i++)
                {
                    var wisp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    wisp.name = "Wisp";
                    Destroy(wisp.GetComponent<Collider>());
                    wisp.transform.SetParent(aura.transform, false);
                    var r = wisp.GetComponent<Renderer>();
                    r.sharedMaterial = mistMaterial;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    wisp.AddComponent<InkEliteWisp>().Setup(i / 4f, radius, enemy.Height * 1.2f, new Color(smoke.r, smoke.g, smoke.b, 0.38f));
                }
            }
            if (enemy.IsHidden) aura.SetActive(false);
            return d;
        }

        // 분열 (EliteTrait.Split, 천마총 기획): 금 간 먹 원이 둘로 갈라지는 보랏빛 표식. 그림은 Resources/EliteFx/Split (씬을 다시 만들지 않아도 돼요)
        static Sprite _splitMark;
        static bool _splitLoaded;
        static Sprite SplitMark
        {
            get
            {
                if (!_splitLoaded) { _splitLoaded = true; _splitMark = Resources.Load<Sprite>("EliteFx/Split"); }
                return _splitMark;
            }
        }
        static bool IsSplit(EliteTrait t) => t == EliteTrait.Split;

        static Color Tint(EliteTrait t) => IsSplit(t) ? new Color(0.4f, 0.18f, 0.52f)
            : t == EliteTrait.IronShirt ? new Color(0.25f, 0.28f, 0.34f)
            : t == EliteTrait.Rejuvenation ? new Color(0.22f, 0.42f, 0.26f)
            : new Color(0.62f, 0.16f, 0.08f);
    }

    // 카메라를 마주 보고, 원하면 천천히 위아래로 떠요
    public class InkFaceCamera : MonoBehaviour
    {
        float _amp, _period, _seed;
        Vector3 _base;

        public InkFaceCamera Bob(float amplitude, float period)
        {
            _amp = amplitude; _period = period; _base = transform.localPosition; _seed = Random.value * 10f;
            return this;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
            if (_amp > 0f) transform.localPosition = _base + Vector3.up * Mathf.Sin((Time.time + _seed) * Mathf.PI * 2f / _period) * _amp;
        }
    }

    // 몸 둘레로 피어오르는 먹 연기 한 장: 발목에서 머리 높이까지 오르며 커졌다 옅어지고, 다시 아래에서 피어나요
    public class InkEliteWisp : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        float _phase, _radius, _height;
        Color _color;
        MaterialPropertyBlock _block;
        Renderer _renderer;
        float _angle;

        public void Setup(float phase, float radius, float height, Color color)
        {
            _phase = phase; _radius = radius; _height = height; _color = color;
            _angle = phase * 360f + Random.Range(-30f, 30f);
            _block = new MaterialPropertyBlock();
            _renderer = GetComponent<Renderer>();
        }

        void LateUpdate()
        {
            const float period = 2.2f;
            float t = Mathf.Repeat(Time.time / period + _phase, 1f);
            var around = Quaternion.Euler(0f, _angle + t * 60f, 0f) * Vector3.forward * _radius * Mathf.Lerp(0.8f, 1.2f, t);
            transform.localPosition = around + Vector3.up * Mathf.Lerp(0.2f, _height, t);
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.1f, t) * _radius * 1.6f;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up) * Quaternion.Euler(0f, 0f, _angle);
            _block.SetColor(ColorId, new Color(_color.r, _color.g, _color.b, _color.a * Mathf.Sin(t * Mathf.PI)));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
