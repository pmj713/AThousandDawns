using System;
using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Combat
{
    // 만검귀종 (검 절초): 시전 시간 동안 검이 범위 안 적에게 고르게 나뉘어 떨어져요. 적이 1명이면 전부 그 적에게.
    // 검은 떨어지는 동안 대상을 따라가서 반드시 맞고, 강인함을 무시해요 (방어력은 적용). 범위에 적이 없으면 빈 땅에 꽂혀요.
    // 반경이 0 이하면 맵 전체 (멀리서 자고 있던 적도 맞고 깨어나요).
    public sealed class SwordRain : MonoBehaviour
    {
        const float DropHeight = 7f;
        const float RestHeight = -0.3f; // 칼끝이 땅에 살짝 박힌 높이
        const float Linger = 0.35f;
        const float EmptyScatter = 3f;  // 맞힐 적이 없을 때 검이 꽂히는 범위 (플레이어 둘레)

        bool WholeMap => _radius <= 0f;

        [SerializeField] Material swordMaterial;

        struct Sword
        {
            public Transform Body;
            public Damageable Target;
            public Vector3 Point;
            public float Age;
            public bool Landed;
        }

        readonly List<Sword> _swords = new List<Sword>();
        readonly Dictionary<Damageable, int> _counts = new Dictionary<Damageable, int>();
        readonly List<Damageable> _candidates = new List<Damageable>();
        readonly Collider[] _overlap = new Collider[64];

        Vector3 _center;
        float _radius, _interval, _fallTime, _elapsed;
        int _remaining, _spawned;
        HitDamageInput _hit;
        DamageRules _rules;

        public bool IsRaining => _remaining > 0 || _swords.Count > 0;

        // 절초 각인용: 검이 땅에 닿을 때 (맞힌 적, 없으면 null · 떨어진 자리), 마지막 검까지 닿았을 때 (피해 합)
        public event Action<Damageable, Vector3> SwordLanded;
        public event Action<int> Finished;
        int _totalDamage;
        bool _finishPending;
        int _landed;

        public void Begin(Vector3 center, float radius, int count, float duration, float fallTime, HitDamageInput hit, in DamageRules rules)
        {
            _center = new Vector3(center.x, 0f, center.z);
            _radius = radius;
            _remaining = count;
            _spawned = 0;
            _interval = count > 0 ? duration / count : duration;
            _fallTime = Mathf.Max(0.01f, fallTime);
            _elapsed = 0f;
            _hit = hit;
            _hit.IgnoreToughness = true;
            _rules = rules;
            _counts.Clear();
            _totalDamage = 0;
            _finishPending = count > 0;
            _landed = 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_remaining > 0)
            {
                _elapsed += dt;
                // i번째 검은 (i + 0.5) × 간격에 떨어지기 시작해요 → 시전 시간 안에 고르게
                while (_remaining > 0 && _elapsed >= (_spawned + 0.5f) * _interval) SpawnSword();
            }

            for (int i = _swords.Count - 1; i >= 0; i--)
            {
                var s = _swords[i];
                s.Age += dt;
                if (!s.Landed)
                {
                    if (s.Target != null && !s.Target.IsDead) s.Point = Ground(s.Target.transform.position);
                    float t = Mathf.Clamp01(s.Age / _fallTime);
                    s.Body.position = s.Point + Vector3.up * Mathf.Lerp(DropHeight, RestHeight, t * t);
                    if (t >= 1f)
                    {
                        s.Landed = true;
                        s.Age = 0f;
                        Land(s.Target);
                        _landed++;
                        SwordLanded?.Invoke(s.Target, s.Point);
                    }
                }
                else if (s.Age >= Linger)
                {
                    Destroy(s.Body.gameObject);
                    _swords.RemoveAt(i);
                    continue;
                }
                _swords[i] = s;
            }

            // 마지막 검까지 땅에 닿으면 한 번 (절초 #뇌정 각인: 피해 합으로 연쇄 번개)
            if (_finishPending && _remaining == 0 && _landed >= _spawned)
            {
                _finishPending = false;
                Finished?.Invoke(_totalDamage);
            }
        }

        void SpawnSword()
        {
            var target = PickTarget();
            Vector3 point;
            if (target != null)
            {
                point = Ground(target.transform.position);
                _counts[target] = _counts.TryGetValue(target, out int n) ? n + 1 : 1;
            }
            else
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * (WholeMap ? EmptyScatter : _radius);
                point = _center + new Vector3(r.x, 0f, r.y);
            }

            var body = CreateSword();
            body.position = point + Vector3.up * DropHeight;
            _swords.Add(new Sword { Body = body, Target = target, Point = point });
            _remaining--;
            _spawned++;
        }

        // 지금 범위 안에 있는 적 가운데 검을 가장 적게 받은 적. 같으면 가운데에 가까운 적.
        Damageable PickTarget()
        {
            _candidates.Clear();
            if (WholeMap)
            {
                foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                    if (!d.IsDead && d.Targetable) _candidates.Add(d);
            }
            else
            {
                int found = Physics.OverlapSphereNonAlloc(_center, _radius + 1f, _overlap, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < found; i++)
                {
                    var d = _overlap[i].GetComponentInParent<Damageable>();
                    if (d == null || d.IsDead || !d.Targetable || _candidates.Contains(d)) continue;
                    if (DistanceXZ(_center, d.transform.position) > _radius + d.BodyRadius) continue;
                    _candidates.Add(d);
                }
            }

            Damageable best = null;
            int bestCount = int.MaxValue;
            float bestDistance = float.MaxValue;
            foreach (var d in _candidates)
            {
                int count = _counts.TryGetValue(d, out int n) ? n : 0;
                float distance = DistanceXZ(_center, d.transform.position);
                if (count < bestCount || (count == bestCount && distance < bestDistance))
                {
                    best = d;
                    bestCount = count;
                    bestDistance = distance;
                }
            }
            return best;
        }

        void Land(Damageable target)
        {
            if (target == null || target.IsDead || !target.Targetable) return;
            var hit = _hit;
            hit.Toughness = target.Toughness;
            hit.Defense = target.Defense;
            int damage = DamageFormula.ToEnemy(hit, _rules);
            target.ApplyHit(damage);
            _totalDamage += damage;
        }

        // 칼끝이 원점(아래)에 오는 검: 날 + 코등이 + 손잡이
        Transform CreateSword()
        {
            var root = new GameObject("만검귀종 검").transform;
            Part(root, new Vector3(0f, 0.7f, 0f), new Vector3(0.1f, 1.4f, 0.03f));
            Part(root, new Vector3(0f, 1.42f, 0f), new Vector3(0.42f, 0.06f, 0.08f));
            Part(root, new Vector3(0f, 1.62f, 0f), new Vector3(0.06f, 0.34f, 0.06f));
            return root;
        }

        void Part(Transform parent, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var collider = go.GetComponent<Collider>();
            collider.enabled = false; // Destroy는 프레임 끝에 지워져서 같은 프레임 판정에 걸리지 않게 먼저 꺼요
            Destroy(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (swordMaterial != null) go.GetComponent<Renderer>().sharedMaterial = swordMaterial;
        }

        static Vector3 Ground(Vector3 p) => new Vector3(p.x, 0f, p.z);

        static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        void OnDestroy()
        {
            foreach (var s in _swords)
                if (s.Body != null) Destroy(s.Body.gameObject);
            _swords.Clear();
        }
    }
}
