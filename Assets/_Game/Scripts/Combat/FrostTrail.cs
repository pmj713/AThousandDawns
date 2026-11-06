using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Combat
{
    // 한철 비녀: 대시 공격 경로에 서리 길. 밟은 적은 둔화 1중첩 (길 하나에 적마다 한 번).
    public sealed class FrostTrail : MonoBehaviour
    {
        static readonly Color FrostColor = new Color(0.72f, 0.9f, 1f); // 옅은 하늘색 (임시)
        static Material _material;
        static readonly Collider[] Overlap = new Collider[32];
        const float CheckInterval = 0.1f;

        Vector3 _a, _b;
        float _halfWidth, _life, _checkTimer, _slowBonus;
        bool _freezeAtMax;
        readonly HashSet<Damageable> _slowed = new HashSet<Damageable>();

        public static FrostTrail Create(Vector3 a, Vector3 b, float width, float life, float slowBonus, bool freezeAtMax)
        {
            a.y = 0f;
            b.y = 0f;
            Vector3 along = b - a;
            if (along.sqrMagnitude < 0.01f) along = Vector3.forward * 0.1f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "서리 길";
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.transform.position = (a + b) * 0.5f + Vector3.up * 0.02f;
            go.transform.rotation = Quaternion.LookRotation(along.normalized);
            go.transform.localScale = new Vector3(width, 0.01f, along.magnitude + width);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterial.Unlit(ref _material, FrostColor);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var trail = go.AddComponent<FrostTrail>();
            trail._a = a;
            trail._b = b;
            trail._halfWidth = width * 0.5f;
            trail._life = life;
            trail._slowBonus = slowBonus;
            trail._freezeAtMax = freezeAtMax;
            return trail;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            _checkTimer -= dt;
            if (_checkTimer > 0f) return;
            _checkTimer = CheckInterval;

            Vector3 center = (_a + _b) * 0.5f;
            float reach = (_b - _a).magnitude * 0.5f + _halfWidth + 1f;
            int found = Physics.OverlapSphereNonAlloc(center, reach, Overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < found; i++)
            {
                var d = Overlap[i].GetComponentInParent<Damageable>();
                if (d == null || d.IsDead || !d.Targetable || _slowed.Contains(d)) continue;
                if (DistanceToPath(d.transform.position) > _halfWidth + d.BodyRadius) continue;
                _slowed.Add(d);
                StatusEffects.For(d).AddSlow(_slowBonus, _freezeAtMax);
            }
        }

        float DistanceToPath(Vector3 p)
        {
            p.y = 0f;
            Vector3 ab = _b - _a;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(p - _a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (_a + ab * t)).magnitude;
        }
    }
}
