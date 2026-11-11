using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Art
{
    // 맞을 때 먹물이 튀어요. 적은 먹, 플레이어는 붉은 물감.
    //   - 바닥 얼룩: 맞은 반대쪽으로 길게 번졌다가 마르듯 사라져요
    //   - 먹 방울: 몸에서 맞은 반대쪽으로 튀어 날아가 떨어진 자리에 작은 얼룩을 남겨요
    //   - 쓰러짐: 방울이 사방으로 크게 터지고, 큰 얼룩이 오래 남아요
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
        [SerializeField] float deathLife = 6f;          // 쓰러진 자리 얼룩은 오래 남아요
        [SerializeField] int poolSize = 60;
        [SerializeField] int dropPoolSize = 80;
        [SerializeField] int hitDrops = 5, deathDrops = 16;
        [SerializeField] float gravity = 14f;

        sealed class Splat
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float Age = -1f, Life;
        }

        sealed class Drop
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public float Size;
            public Color Color;
            public bool Alive;
        }

        readonly HashSet<Damageable> _known = new HashSet<Damageable>();
        readonly List<Splat> _pool = new List<Splat>();
        readonly List<Drop> _drops = new List<Drop>();
        MaterialPropertyBlock _block;
        int _next, _nextDrop;
        float _scanTimer;
        Transform _player;
        Camera _camera;

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
                if (_player == null)
                {
                    var p = FindFirstObjectByType<PlayerController>();
                    if (p != null) _player = p.transform;
                }
            }
            if (_camera == null) _camera = Camera.main;

            foreach (var s in _pool)
            {
                if (s.Age < 0f) continue;
                s.Age += Time.deltaTime;
                float bloom = 1f - Mathf.Pow(1f - Mathf.Clamp01(s.Age / 0.1f), 3f);
                float fade = Mathf.Clamp01((s.Age - s.Life * 0.45f) / (s.Life * 0.55f));
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

            // 먹 방울: 포물선으로 날아가 바닥에 닿으면 작은 얼룩
            Quaternion face = _camera != null ? Quaternion.LookRotation(_camera.transform.forward, _camera.transform.up) : Quaternion.identity;
            foreach (var d in _drops)
            {
                if (!d.Alive) continue;
                d.Velocity.y -= gravity * Time.deltaTime;
                var p = d.Transform.position + d.Velocity * Time.deltaTime;
                d.Transform.SetPositionAndRotation(p, face);
                if (p.y <= 0.04f)
                {
                    d.Alive = false;
                    d.Renderer.enabled = false;
                    var flat = new Vector3(d.Velocity.x, 0f, d.Velocity.z);
                    SpawnSplat(p, d.Size * 2.2f, d.Color, life, flat, 1.6f);
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
            // 맞은 반대쪽: 플레이어에게서 멀어지는 쪽 (플레이어가 맞으면 무작위)
            Vector3 away = !isPlayer && _player != null ? p - _player.position : Random.insideUnitSphere;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            bool dead = target.IsDead;
            float size = Mathf.Clamp(0.6f + amount * 0.01f, 0.6f, 1.4f);
            if (dead) size *= 1.6f;

            // 바닥 얼룩: 맞은 반대쪽으로 길쭉하게, 한 칸쯤 밀려서
            SpawnSplat(p + away * size * 0.35f, size, color, dead ? deathLife : life, away, dead ? 1.2f : 1.7f);
            for (int i = 0; i < 2; i++)
            {
                var offset = Random.insideUnitCircle * 0.9f;
                SpawnSplat(p + new Vector3(offset.x, 0f, offset.y) + away * 0.4f, size * Random.Range(0.25f, 0.45f), color, life, away, 1.3f);
            }

            // 먹 방울: 몸 가운데 높이에서 맞은 반대쪽으로 부채꼴 (쓰러지면 사방으로 크게)
            float height = isPlayer ? 1f : Mathf.Max(0.4f, target.transform.lossyScale.y);
            int n = dead ? deathDrops : hitDrops;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = dead
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward
                    : Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * away;
                float speed = Random.Range(2.5f, dead ? 6.5f : 5f);
                var v = dir * speed + Vector3.up * Random.Range(2f, dead ? 6f : 4.5f);
                SpawnDrop(p + Vector3.up * height * 0.9f + dir * 0.2f, v, Random.Range(0.06f, dead ? 0.2f : 0.14f), color);
            }
        }

        Splat NextSplat()
        {
            if (_pool.Count < poolSize)
            {
                var r = MakeQuad("InkSplat");
                var s = new Splat { Transform = r.transform, Renderer = r };
                _pool.Add(s);
                return s;
            }
            var reuse = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            return reuse;
        }

        MeshRenderer MakeQuad(string name)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = splatMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return r;
        }

        // 바닥 얼룩: stretch만큼 dir 쪽으로 길게 (쿼드는 −Z가 앞이라 아래를 보게 눕히고, 로컬 Y를 dir에 맞춰요)
        void SpawnSplat(Vector3 position, float size, Color color, float lifetime, Vector3 dir, float stretch)
        {
            var s = NextSplat();
            s.Age = 0f;
            s.Life = lifetime;
            var forward = dir.sqrMagnitude > 1e-4f ? new Vector3(dir.x, 0f, dir.z).normalized : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            s.Transform.SetPositionAndRotation(new Vector3(position.x, 0.03f + Random.Range(0f, 0.01f), position.z),
                Quaternion.LookRotation(Vector3.down, forward));
            s.Transform.localScale = new Vector3(size * 2f / Mathf.Sqrt(stretch), size * 2f * Mathf.Sqrt(stretch), 1f);
            s.Renderer.enabled = true;
            s.Renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _block.SetFloat(SeedId, Random.Range(0f, 50f));
            _block.SetFloat(BloomId, 0f);
            _block.SetFloat(FadeId, 0f);
            s.Renderer.SetPropertyBlock(_block);
        }

        void SpawnDrop(Vector3 position, Vector3 velocity, float size, Color color)
        {
            Drop d;
            if (_drops.Count < dropPoolSize)
            {
                var r = MakeQuad("InkDrop");
                d = new Drop { Transform = r.transform, Renderer = r };
                _drops.Add(d);
            }
            else
            {
                d = _drops[_nextDrop];
                _nextDrop = (_nextDrop + 1) % _drops.Count;
            }
            d.Alive = true;
            d.Velocity = velocity;
            d.Size = size;
            d.Color = color;
            d.Transform.position = position;
            d.Transform.localScale = Vector3.one * size * 2.5f;
            d.Renderer.enabled = true;
            d.Renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _block.SetFloat(SeedId, Random.Range(0f, 50f));
            _block.SetFloat(BloomId, 1f);
            _block.SetFloat(FadeId, 0f);
            d.Renderer.SetPropertyBlock(_block);
        }
    }
}
