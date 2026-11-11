using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Combat
{
    // 진전 (기연 방에서 낮은 확률): 그 판 동안 절초 모양이 바뀌어요. 검 3가지 · 비도 3가지.
    public enum UltimateForm { None, OneSword, Formation, Echo, Storm, Bloom, Backflow }

    public static class UltimateForms
    {
        public static readonly UltimateForm[] Sword = { UltimateForm.OneSword, UltimateForm.Formation, UltimateForm.Echo };
        public static readonly UltimateForm[] Dagger = { UltimateForm.Storm, UltimateForm.Bloom, UltimateForm.Backflow };

        public static bool ForDagger(UltimateForm form) => form >= UltimateForm.Storm;

        public static string Name(UltimateForm form) => form switch
        {
            UltimateForm.OneSword => "일검",
            UltimateForm.Formation => "검진",
            UltimateForm.Echo => "귀원",
            UltimateForm.Storm => "폭우",
            UltimateForm.Bloom => "산화",
            UltimateForm.Backflow => "회류",
            _ => "없음",
        };

        public static string Text(UltimateForm form) => form switch
        {
            UltimateForm.OneSword => "검 여러 자루 대신 큰 검 한 자루가 체력이 가장 많은 적에게 떨어져 주변까지 베어요",
            UltimateForm.Formation => "검이 3초 동안 내 주위를 돌며 닿는 적을 계속 베어요",
            UltimateForm.Echo => "떨어진 검이 땅에 꽂혔다가 1초 뒤 한 번 더 터져요",
            UltimateForm.Storm => "비도가 더 많이, 더 넓게 쏟아져요 (한 발 피해는 줄어요)",
            UltimateForm.Bloom => "비도가 꽂힌 적이 마지막에 한꺼번에 터져요",
            UltimateForm.Backflow => "떨어진 비도가 전부 나에게 돌아오며 길에 있는 적을 한 번 더 맞혀요",
            _ => "",
        };
    }

    // 만검귀종 (검 절초): 시전 시간 동안 검이 범위 안 적에게 고르게 나뉘어 떨어져요. 적이 1명이면 전부 그 적에게.
    // 검은 떨어지는 동안 대상을 따라가서 반드시 맞고, 강인함을 무시해요 (방어력은 적용). 범위에 적이 없으면 빈 땅에 꽂혀요.
    // 범위는 방 하나 크기 (가로 20 × 세로 12칸, 조작 탭), 시전한 자리가 가운데예요.
    public sealed class SwordRain : MonoBehaviour
    {
        const float DropHeight = 7f;
        const float RestHeight = -0.3f; // 칼끝이 땅에 살짝 박힌 높이
        const float Linger = 0.35f;
        const float EmptyScatter = 3f;  // 맞힐 적이 없을 때 검이 꽂히는 범위 (플레이어 둘레)

        // 가운데가 center인 가로 size.x × 세로 size.y 사각형 안 (몸이 걸쳐도 들어감)
        public static bool InArea(Vector3 center, Vector2 size, Damageable d) =>
            Mathf.Abs(d.transform.position.x - center.x) <= size.x * 0.5f + d.BodyRadius
            && Mathf.Abs(d.transform.position.z - center.z) <= size.y * 0.5f + d.BodyRadius;

        [SerializeField] Material swordMaterial;

        // 비도 절초 만천화우: 검 대신 작은 비도가 비스듬히 쏟아져요 (Begin 전에 정해요)
        public bool DaggerLook { get; set; }
        const float DaggerSlantAngle = 40f; // 만천화우가 기울어 내려오는 각도 (수직에서)
        public Vector3 DaggerDirection { get; set; } = Vector3.forward; // 만천화우가 날아가는 쪽 (시전할 때 바라보던 방향)
        Vector3 _slant;

        // 진전 (Begin 전에 정해요). Owner = 시전한 사람 (검진이 그 둘레를 돌고, 회류가 그쪽으로 돌아와요)
        public UltimateForm Form { get; set; }
        public Transform Owner { get; set; }
        public float OneSwordRadius = 3f;       // 일검: 떨어진 자리 둘레도 베요
        public float EchoDelay = 1f, EchoRatio = 0.6f, EchoRadius = 1.2f;   // 귀원
        public float BloomRatio = 0.5f, BloomRadius = 1.5f;                 // 산화: 꽂힌 비도 1개당
        public float BackflowSpeed = 20f, BackflowRadius = 0.6f;            // 회류
        public float OrbitTime = 3f, OrbitRadius = 2.2f, OrbitSpeed = 300f, OrbitRatio = 0.5f, OrbitInterval = 0.3f; // 검진

        struct Sword
        {
            public Transform Body;
            public Damageable Target;
            public Vector3 Point;
            public Vector3 Slant; // 떨어지기 시작할 때 옆으로 벗어난 거리 (비도는 비스듬히, 검은 0)
            public float Age;
            public bool Landed;
            public bool Echoed;   // 귀원: 한 번 더 터졌음
            public HashSet<Damageable> Returned; // 회류: 돌아오며 맞힌 적
        }

        // 경지 (Begin 전에 정해요): 검흔 = 검이 처음 꽂힌 적을 0.5초마다 4번 (한 자루 피해 × 비율, 한 적에 한 번) · 마무리 = 다 내린 뒤 가장 센 적에게 큰 검 한 번 (× 비율)
        public float ScarRatio { get; set; }
        public float FinaleRatio { get; set; }
        const float ScarInterval = 0.5f;
        const int ScarTicks = 4;
        readonly List<(Damageable target, int left, float timer)> _scars = new List<(Damageable, int, float)>();
        readonly HashSet<Damageable> _scarred = new HashSet<Damageable>();

        readonly Dictionary<Damageable, int> _bloomHits = new Dictionary<Damageable, int>();
        readonly Dictionary<Damageable, float> _orbitCooldown = new Dictionary<Damageable, float>();
        readonly List<Damageable> _orbitKeys = new List<Damageable>();
        float _orbitLeft;

        readonly List<Sword> _swords = new List<Sword>();
        readonly Dictionary<Damageable, int> _counts = new Dictionary<Damageable, int>();
        readonly List<Damageable> _candidates = new List<Damageable>();

        Vector3 _center;
        Vector2 _area;
        float _interval, _fallTime, _elapsed;
        int _remaining, _spawned;
        HitDamageInput _hit;
        DamageRules _rules;

        public bool IsRaining => _remaining > 0 || _swords.Count > 0 || _orbitLeft > 0f;

        // 절초 각인용: 검이 땅에 닿을 때 (맞힌 적, 없으면 null · 떨어진 자리), 마지막 검까지 닿았을 때 (피해 합)
        public event Action<Damageable, Vector3> SwordLanded;
        public event Action<int> Finished;
        int _totalDamage;
        bool _finishPending;
        int _landed;

        public void Begin(Vector3 center, Vector2 area, int count, float duration, float fallTime, HitDamageInput hit, in DamageRules rules)
        {
            _center = new Vector3(center.x, 0f, center.z);
            _area = area;
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
            // 만천화우: 바라보던 쪽으로 비스듬히 내려요 (뒤 위에서 앞 아래로)
            var dir = new Vector3(DaggerDirection.x, 0f, DaggerDirection.z);
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            _slant = DaggerLook ? -dir * DropHeight * Mathf.Tan(DaggerSlantAngle * Mathf.Deg2Rad) : Vector3.zero;
            _landed = 0;
            _bloomHits.Clear();
            _orbitCooldown.Clear();
            _scarred.Clear();
            _orbitLeft = 0f;

            // 검진: 검이 한꺼번에 나와 시전한 사람 둘레를 돌아요 (떨어지지 않음)
            if (Form == UltimateForm.Formation && count > 0)
            {
                _orbitLeft = OrbitTime;
                for (int i = 0; i < count; i++)
                    _swords.Add(new Sword { Body = CreateSword(), Point = new Vector3(i * 360f / count, 0f, 0f), Landed = true });
                _remaining = 0;
                _spawned = _landed = count;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            TickScars(dt); // 검흔은 비가 끝나도 이어져요
            if (_orbitLeft > 0f)
            {
                TickOrbit(dt);
                return;
            }
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
                    s.Body.position = s.Point + s.Slant * (1f - t * t) + Vector3.up * Mathf.Lerp(DropHeight, RestHeight, t * t);
                    if (t >= 1f)
                    {
                        s.Landed = true;
                        s.Age = 0f;
                        Land(s.Target, s.Point);
                        Sfx.PlayEach(SfxId.SwordLand, -1, 0.6f); // 검마다 "슉" (두 소리 중 무작위)
                        _landed++;
                        SwordLanded?.Invoke(s.Target, s.Point);
                    }
                }
                else if (LandedDone(ref s))
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
                if (Form == UltimateForm.Bloom) BurstBloom();
                if (FinaleRatio > 0f) Finale();
                Finished?.Invoke(_totalDamage);
            }
        }

        // 땅에 닿은 뒤: 보통은 잠깐 남았다 사라져요. 귀원은 한 번 더 터지고, 회류는 시전한 사람에게 돌아와요.
        bool LandedDone(ref Sword s)
        {
            if (Form == UltimateForm.Echo)
            {
                if (!s.Echoed && s.Age >= EchoDelay)
                {
                    s.Echoed = true;
                    HitAround(s.Point, EchoRadius, EchoRatio, null);
                    s.Body.localScale = Vector3.one * 1.3f; // 터지는 순간 살짝 커져요
                }
                return s.Echoed && s.Age >= EchoDelay + 0.15f;
            }
            if (Form == UltimateForm.Backflow && Owner != null)
            {
                if (s.Age < Linger) return false;
                s.Returned ??= new HashSet<Damageable>();
                Vector3 goal = Owner.position + Vector3.up;
                Vector3 to = goal - s.Body.position;
                float step = BackflowSpeed * Time.deltaTime;
                if (to.magnitude <= step + 0.3f || s.Age >= Linger + 2f) return true;
                s.Body.position += to.normalized * step;
                s.Body.rotation = Quaternion.FromToRotation(Vector3.up, to.normalized);
                foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                {
                    if (d.IsDead || !d.Targetable || s.Returned.Contains(d) || d.transform == Owner) continue;
                    if (DistanceXZ(d.transform.position, s.Body.position) > BackflowRadius + d.BodyRadius) continue;
                    s.Returned.Add(d);
                    Strike(d, 1f);
                }
                return false;
            }
            return s.Age >= Linger;
        }

        // 검진: 둘레를 돌며 닿는 적을 베요 (적마다 간격에 한 번). 끝나면 검이 사라져요.
        void TickOrbit(float dt)
        {
            _orbitLeft -= dt;
            _orbitKeys.Clear();
            _orbitKeys.AddRange(_orbitCooldown.Keys);
            foreach (var k in _orbitKeys) _orbitCooldown[k] -= dt;

            Vector3 center = Owner != null ? Owner.position : _center;
            center.y = 0f;
            for (int i = 0; i < _swords.Count; i++)
            {
                var s = _swords[i];
                s.Point.x += OrbitSpeed * dt;
                Vector3 outward = Quaternion.Euler(0f, s.Point.x, 0f) * Vector3.forward;
                s.Body.position = center + outward * (OrbitRadius + 0.8f) + Vector3.up;
                s.Body.rotation = Quaternion.FromToRotation(Vector3.up, -outward); // 칼끝이 바깥, 손잡이가 안쪽
                _swords[i] = s;
                Vector3 tip = center + outward * OrbitRadius;
                foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                {
                    if (d.IsDead || !d.Targetable || (Owner != null && d.transform == Owner)) continue;
                    if (_orbitCooldown.TryGetValue(d, out float cd) && cd > 0f) continue;
                    if (DistanceXZ(d.transform.position, tip) > 0.9f + d.BodyRadius) continue;
                    bool first = !_orbitCooldown.ContainsKey(d);
                    _orbitCooldown[d] = OrbitInterval;
                    Strike(d, OrbitRatio);
                    if (first) SwordLanded?.Invoke(d, Ground(d.transform.position)); // 절초 각인은 적마다 처음 한 번
                }
            }

            if (_orbitLeft > 0f) return;
            foreach (var s in _swords)
                if (s.Body != null) Destroy(s.Body.gameObject);
            _swords.Clear();
            _finishPending = false;
            Finished?.Invoke(_totalDamage);
        }

        // 산화: 비도가 꽂힌 적마다 (꽂힌 수 × 비율) 피해로 주변까지 터져요
        void BurstBloom()
        {
            foreach (var kv in _bloomHits)
            {
                var d = kv.Key;
                if (d == null || d.IsDead) continue;
                HitAround(Ground(d.transform.position), BloomRadius, BloomRatio * kv.Value, null);
            }
            _bloomHits.Clear();
        }

        // 한 자루 피해 × ratio로 그 적을 베요 (강인함 무시, 방어력 적용)
        void Strike(Damageable target, float ratio)
        {
            if (target == null || target.IsDead || !target.Targetable) return;
            var hit = _hit;
            hit.SpecialMultiplier *= ratio;
            hit.Toughness = target.Toughness;
            hit.Defense = target.Defense;
            int damage = DamageFormula.ToEnemy(hit, _rules);
            target.ApplyHit(damage);
            _totalDamage += damage;
        }

        void HitAround(Vector3 point, float radius, float ratio, Damageable skip)
        {
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (d == skip || d.IsDead || !d.Targetable || (Owner != null && d.transform == Owner)) continue;
                if (DistanceXZ(point, d.transform.position) > radius + d.BodyRadius) continue;
                Strike(d, ratio);
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
                Vector2 r = UnityEngine.Random.insideUnitCircle * EmptyScatter;
                point = _center + new Vector3(r.x, 0f, r.y);
            }

            var body = CreateSword();
            body.position = point + _slant + Vector3.up * DropHeight;
            if (DaggerLook) body.rotation = Quaternion.FromToRotation(Vector3.up, (_slant + Vector3.up * DropHeight).normalized); // 칼끝이 날아가는 쪽 (손잡이가 뒤)
            if (Form == UltimateForm.OneSword) body.localScale = Vector3.one * 2.5f; // 일검: 큰 검 한 자루
            _swords.Add(new Sword { Body = body, Target = target, Point = point, Slant = _slant });
            _remaining--;
            _spawned++;
        }

        // 지금 범위 안에 있는 적 가운데 검을 가장 적게 받은 적. 같으면 가운데에 가까운 적.
        Damageable PickTarget()
        {
            _candidates.Clear();
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                if (!d.IsDead && d.Targetable && InArea(_center, _area, d)) _candidates.Add(d);

            if (Form == UltimateForm.OneSword) // 일검: 체력이 가장 많은 적
            {
                Damageable strongest = null;
                foreach (var d in _candidates)
                    if (strongest == null || d.Health > strongest.Health) strongest = d;
                return strongest;
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

        void Land(Damageable target, Vector3 point)
        {
            if (Form == UltimateForm.OneSword) HitAround(point, OneSwordRadius, 1f, target); // 일검: 떨어진 자리 둘레도
            if (target == null || target.IsDead || !target.Targetable) return;
            Strike(target, 1f);
            if (Form == UltimateForm.Bloom) _bloomHits[target] = _bloomHits.TryGetValue(target, out int n) ? n + 1 : 1; // 산화: 꽂힌 수
            if (ScarRatio > 0f && _scarred.Add(target)) _scars.Add((target, ScarTicks, ScarInterval)); // 중단전 검흔
        }

        void TickScars(float dt)
        {
            for (int i = _scars.Count - 1; i >= 0; i--)
            {
                var (target, left, timer) = _scars[i];
                timer -= dt;
                if (timer > 0f)
                {
                    _scars[i] = (target, left, timer);
                    continue;
                }
                if (target == null || target.IsDead) { _scars.RemoveAt(i); continue; }
                Strike(target, ScarRatio);
                if (--left <= 0) _scars.RemoveAt(i);
                else _scars[i] = (target, left, ScarInterval);
            }
        }

        // 상단전 마무리: 가장 센 적에게 큰 검 한 자루 (비도면 큰 비도)
        void Finale()
        {
            Damageable best = null;
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                if (!d.IsDead && d.Targetable && InArea(_center, _area, d) && (Owner == null || d.transform != Owner) && (best == null || d.Health > best.Health)) best = d;
            if (best == null) return;
            Strike(best, FinaleRatio);
            var body = CreateSword();
            body.localScale = Vector3.one * 3f;
            body.position = Ground(best.transform.position) + Vector3.up * RestHeight;
            _swords.Add(new Sword { Body = body, Target = best, Point = Ground(best.transform.position), Landed = true });
        }

        // 칼끝이 원점(아래)에 오는 검: 날 + 코등이 + 손잡이 (비도면 작은 비도, 기울기는 SpawnSword에서)
        Transform CreateSword()
        {
            if (DaggerLook)
            {
                var dagger = new GameObject("만천화우 비도").transform;
                Part(dagger, new Vector3(0f, 0.28f, 0f), new Vector3(0.06f, 0.56f, 0.02f));
                Part(dagger, new Vector3(0f, 0.6f, 0f), new Vector3(0.14f, 0.04f, 0.05f));
                Part(dagger, new Vector3(0f, 0.7f, 0f), new Vector3(0.05f, 0.16f, 0.05f));
                return dagger;
            }
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
