using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Art
{
    // 맞을 때 바닥에 먹물이 튀어 번졌다가 마르듯 사라져요. 적은 먹, 플레이어는 붉은 물감.
    // 씬의 Damageable을 주기적으로 찾아 Damaged 이벤트만 듣고, 게임 규칙은 건드리지 않아요.
    public class InkHitSplatter : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BloomId = Shader.PropertyToID("_Bloom");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        [SerializeField] Material splatMaterial;
        [SerializeField] Color enemyInk = new Color(0.06f, 0.06f, 0.07f, 1f);
        [SerializeField] Color playerPaint = new Color(0.62f, 0.08f, 0.06f, 1f);
        [SerializeField] float life = 2.2f;
        [SerializeField] int poolSize = 40;

        sealed class Splat
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age = -1f, Size;
            public Color Color;
        }

        readonly HashSet<Damageable> _known = new HashSet<Damageable>();
        readonly List<Splat> _pool = new List<Splat>();
        MaterialPropertyBlock _block;
        int _next;
        float _scanTimer;

        void Awake() => _block = new MaterialPropertyBlock();

        void Update()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 0.5f;
                _known.RemoveWhere(d => d == null);
                foreach (var d in FindObjectsByType<Damageable>())
                    if (_known.Add(d)) d.Damaged += OnDamaged;
            }

            foreach (var s in _pool)
            {
                if (s.Age < 0f) continue;
                s.Age += Time.deltaTime;
                float bloom = 1f - Mathf.Pow(1f - Mathf.Clamp01(s.Age / 0.1f), 3f);
                float fade = Mathf.Clamp01((s.Age - life * 0.45f) / (life * 0.55f));
                s.Renderer.GetPropertyBlock(_block);
                _block.SetFloat(BloomId, bloom);
                _block.SetFloat(FadeId, fade);
                s.Renderer.SetPropertyBlock(_block);
                if (fade >= 1f)
                {
                    s.Age = -1f;
                    s.Renderer.enabled = false;
                }
            }
        }

        void OnDestroy()
        {
            foreach (var d in _known)
                if (d != null) d.Damaged -= OnDamaged;
        }

        void OnDamaged(Damageable target, int amount)
        {
            if (target == null || amount <= 0 || splatMaterial == null) return;
            bool isPlayer = target.GetComponent<PlayerController>() != null;
            var color = isPlayer ? playerPaint : enemyInk;
            Vector3 p = target.transform.position;
            float size = Mathf.Clamp(0.6f + amount * 0.01f, 0.6f, 1.4f);
            Spawn(p, size, color);
            for (int i = 0; i < 2; i++)
            {
                var offset = Random.insideUnitCircle * 0.9f;
                Spawn(p + new Vector3(offset.x, 0f, offset.y), size * Random.Range(0.25f, 0.45f), color);
            }
        }

        void Spawn(Vector3 position, float size, Color color)
        {
            Splat s;
            if (_pool.Count < poolSize)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "InkSplat";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, false);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = splatMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
                s = new Splat { Transform = quad.transform, Renderer = r };
                _pool.Add(s);
            }
            else
            {
                s = _pool[_next];
                _next = (_next + 1) % _pool.Count;
            }

            s.Age = 0f;
            s.Size = size;
            s.Color = color;
            s.Transform.SetPositionAndRotation(new Vector3(position.x, 0.03f + Random.Range(0f, 0.01f), position.z),
                Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
            s.Transform.localScale = Vector3.one * size * 2f;
            s.Renderer.enabled = true;
            s.Renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _block.SetFloat(SeedId, Random.Range(0f, 50f));
            _block.SetFloat(BloomId, 0f);
            _block.SetFloat(FadeId, 0f);
            s.Renderer.SetPropertyBlock(_block);
        }
    }
}
