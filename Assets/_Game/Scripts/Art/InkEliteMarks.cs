using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 수묵 시험판: 정예 적을 수묵 모델에서도 알아보게 해요 (게임 코드·적 모델 파일은 그대로).
    //   머리 위: 원래 도형(네모·공·기둥) 대신 카메라를 보는 붓 표식 그림 (철포삼 쇠 방패 · 회춘 새싹 · 열화 불꽃), 천천히 위아래로 떠요
    //   몸 둘레: 먹·붉은 기운. 발밑에 붓으로 그은 고리, 몸 둘레로 피어오르는 먹 연기 (특성 색이 살짝 섞여요)
    // 적 본체 자식으로 달아서 모델이 움찔·회전해도 흔들리지 않고, 숨은 적(풀숲 청죽사 등)이면 같이 숨어요.
    public class InkEliteMarks : MonoBehaviour
    {
        [SerializeField] Sprite ironMark, rejuvMark, blazeMark;
        [SerializeField] Material mistMaterial;   // InkBrushLine + InkMist
        [SerializeField] Material lineMaterial;   // InkBrushLine + BrushLine

        const string Marker = "Ink Elite Aura";
        const float ScanInterval = 0.25f;
        float _nextScan;
        readonly List<(EnemyBrain enemy, GameObject aura)> _dressed = new List<(EnemyBrain, GameObject)>();

        public void Setup(Sprite iron, Sprite rejuv, Sprite blaze, Material mist, Material line)
        {
            ironMark = iron; rejuvMark = rejuv; blazeMark = blaze; mistMaterial = mist; lineMaterial = line;
        }

        void Update()
        {
            // 숨거나 쓰러지면 기운도 같이
            for (int i = _dressed.Count - 1; i >= 0; i--)
            {
                var (enemy, aura) = _dressed[i];
                if (enemy == null || aura == null) { _dressed.RemoveAt(i); continue; }
                bool show = !enemy.IsHidden && !enemy.IsDead;
                if (aura.activeSelf != show) aura.SetActive(show);
            }

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;
            foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                if (enemy == null || !enemy.IsElite || enemy.transform.Find(Marker) != null) continue;
                _dressed.Add((enemy, Dress(enemy)));
            }
        }

        GameObject Dress(EnemyBrain enemy)
        {
            var trait = enemy.Trait;
            // 원래 머리 위 도형은 숨겨요 (자리는 그대로 써요)
            var oldMark = enemy.transform.Find($"Elite_{trait}");
            float markY = enemy.Height * 1.2f + 0.45f;
            if (oldMark != null)
            {
                markY = oldMark.localPosition.y + 0.1f;
                var r = oldMark.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }

            var aura = new GameObject(Marker);
            aura.transform.SetParent(enemy.transform, false);
            Color tint = trait == EliteTrait.IronShirt ? new Color(0.25f, 0.28f, 0.34f)
                : trait == EliteTrait.Rejuvenation ? new Color(0.22f, 0.42f, 0.26f)
                : new Color(0.62f, 0.16f, 0.08f);

            // 머리 위 표식
            var sprite = trait == EliteTrait.IronShirt ? ironMark : trait == EliteTrait.Rejuvenation ? rejuvMark : blazeMark;
            if (sprite != null)
            {
                var mark = new GameObject("Mark");
                mark.transform.SetParent(aura.transform, false);
                mark.transform.localPosition = Vector3.up * markY;
                var sr = mark.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                float k = 0.55f / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                mark.transform.localScale = Vector3.one * k;
                mark.AddComponent<InkFaceCamera>().Bob(0.06f, 1.6f);
            }

            float radius = 0.55f * 1.2f;
            // 발밑 붓 고리 (먹 + 특성 색)
            if (lineMaterial != null)
            {
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
                    ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, -0.04f)); // 회전한 고리라 z가 위
                }
                var c = Color.Lerp(new Color(0.08f, 0.06f, 0.06f), tint, 0.5f);
                ring.startColor = ring.endColor = new Color(c.r, c.g, c.b, 0.85f);
                ring.gameObject.AddComponent<InkSpin>().Speed(40f);
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
            return aura;
        }
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
