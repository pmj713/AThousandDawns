using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Player
{
    // 강기 (#강기, 무기 탭 6번): 평타가 멀리 날아가는 기운. 초당 14칸, 사거리 5칸, 폭 0.8칸.
    // 파생 피해 (강인함 무시, 방어력 적용, 경직 없음, 적중 횟수에 세지 않음). 첫 적에게 맞으면 사라지고, 6단계면 모든 적을 관통.
    // 벽·대나무에 막혀요.
    public sealed class GanggiBolt : MonoBehaviour
    {
        static readonly Color BoltColor = new Color(0.8f, 0.94f, 1f); // 임시
        static Material _material;
        static readonly RaycastHit[] Hits = new RaycastHit[32];

        Vector3 _direction;
        float _speed, _remaining, _radius, _damage;
        bool _pierce;
        readonly HashSet<Damageable> _hit = new HashSet<Damageable>();

        // damage = 방어력 빼기 전 강기 피해 (발동 평타 피해 × 60% × 6단계 공력 배율)
        public static void Fire(Vector3 origin, Vector3 direction, float speed, float range, float width, float damage, bool pierce)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "강기";
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            go.transform.localScale = new Vector3(width, 0.12f, 0.9f);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterial.Unlit(ref _material, BoltColor);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var bolt = go.AddComponent<GanggiBolt>();
            bolt._direction = direction.normalized;
            bolt._speed = speed;
            bolt._remaining = range;
            bolt._radius = width * 0.5f;
            bolt._damage = damage;
            bolt._pierce = pierce;
        }

        void Update()
        {
            float step = Mathf.Min(_speed * Time.deltaTime, _remaining);
            Vector3 from = transform.position;
            int count = Physics.SphereCastNonAlloc(from, _radius, _direction, Hits, step, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(Hits, 0, count, HitDistance.Instance);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // 시작부터 겹친 것 (바닥 등)
                if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
                var target = hit.collider.GetComponentInParent<Damageable>();
                if (target == null)
                {
                    Destroy(gameObject); // 벽·대나무
                    return;
                }
                if (target.IsDead || !target.Targetable || !_hit.Add(target)) continue;
                target.ApplyHit(DamageFormula.Proc(_damage, 1f, target.Defense), false);
                if (!_pierce)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position = from + _direction * step;
            _remaining -= step;
            if (_remaining <= 0f) Destroy(gameObject);
        }

        sealed class HitDistance : IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
