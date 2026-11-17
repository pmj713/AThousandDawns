using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 판타지 적 오크 · 스켈레톤 이펙트 (그림만 — 판정·수치는 게임 코드 그대로). 채색 화풍.
    //   오크 돌진 (OrcBrain = BoarBrain.IsCharging): 내달리는 동안 발뒤꿈치에서 녹갈색 흙먼지가 꼬리처럼 피어나요.
    //     돌진이 끝나는 순간 벽에 박혔으면 (IsWallStunned) 붉은 충격파 + 갈라진 땅 + 돌 부스러기, 아니면 작은 흙먼지 고리.
    //   스켈레톤 무너짐 (SkeletonBrain.Reassembling): 뼛조각과 먼지가 흩어지고, 발밑에 푸른 영혼불 웅덩이가 번지며
    //     푸른 불꽃들이 바닥에서 몸 높이까지 뼈를 끌어올리듯 솟아요 (IsReassembling 1.5초 동안).
    //   다시 일어남 (Reassembled): 푸른 불이 확 번쩍이며 흩어져요.
    //   하피 급강하 (빠르게 덮칠 때): 청회색·청동 깃털과 바람 꼬리 · 늑대인간 발톱 베기 (덮치고 멈추는 순간): 주황빛 세 줄 발톱 자국,
    //     광폭(Enraged) 순간 붉은 김 · IsEnraged 동안 붉은 기운 · 가고일 석화(StoneChanged): 돌가루와 회색 고리 / 풀림: 돌 조각, 석화 중 맞으면 불꽃(금속성).
    // 그림: TempleFx (GhostPool · GhostWisp · Rock), PouchFx/Ring, SkillFx/QuakeRing.
    public class InkBeastFx : MonoBehaviour
    {
        static readonly Color Soul = new Color(0.35f, 0.75f, 1f);

        static InkBeastFx _host;
        static Sprite _pool, _wisp, _ring, _quake, _rock;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly Dictionary<BoarBrain, float> _dust = new Dictionary<BoarBrain, float>(); // 돌진 중인 오크 → 다음 흙먼지까지
        readonly List<BoarBrain> _ended = new List<BoarBrain>();
        EnemyDirector _director;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SkeletonBrain.Reassembling -= OnCollapse;
            SkeletonBrain.Reassembling += OnCollapse;
            SkeletonBrain.Reassembled -= OnRise;
            SkeletonBrain.Reassembled += OnRise;
            WerewolfBrain.Enraged -= OnEnraged;
            WerewolfBrain.Enraged += OnEnraged;
            GargoyleBrain.StoneChanged -= OnStone;
            GargoyleBrain.StoneChanged += OnStone;
            Damageable.AnyDamaged -= OnDamaged;
            Damageable.AnyDamaged += OnDamaged;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkBeastFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Beast Fx").AddComponent<InkBeastFx>();
            return _host;
        }

        static void OnCollapse(SkeletonBrain s) { if (s != null) Host().Collapse(s); }
        static void OnRise(SkeletonBrain s) { if (s != null) Host().Rise(s.transform.position); }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            TickOrcs(dt);
            TickFliers(dt);
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 오크 돌진

        void TickOrcs(float dt)
        {
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            if (_director != null && _director.Enemies != null)
                foreach (var e in _director.Enemies)
                    if (e is BoarBrain b && !b.IsDead && b.Kind == EnemyKind.Orc && b.IsCharging && !_dust.ContainsKey(b))
                        _dust[b] = 0f;
            _ended.Clear();
            _ended.AddRange(_dust.Keys);
            foreach (var b in _ended)
            {
                if (b == null || b.IsDead) { _dust.Remove(b); continue; }
                if (!b.IsCharging) { Impact(b.transform.position, b.IsWallStunned); _dust.Remove(b); continue; } // 돌진이 끝났어요
                float next = _dust[b] - dt;
                if (next <= 0f)
                {
                    next = 0.06f;
                    var back = b.transform.position - b.transform.forward * 0.6f;
                    InkParticles.Burst(back + Vector3.up * 0.15f, 3, new Color(0.62f, 0.7f, 0.35f, 0.75f), new Color(0.55f, 0.32f, 0.18f, 0.4f), 0.8f, 0.45f, 0.6f, -0.3f);
                }
                _dust[b] = next;
            }
        }

        void Impact(Vector3 at, bool wall)
        {
            if (wall)
            {
                Ring(_ring, at, 3.2f, 0.4f, new Color(1f, 0.3f, 0.15f, 1f), 0.028f);
                Ring(_quake, at, 2.6f, 0.9f, new Color(1f, 0.85f, 0.6f, 1f), 0.026f);
                InkParticles.Burst(at + Vector3.up * 0.5f, 18, new Color(0.65f, 0.72f, 0.4f, 0.9f), new Color(0.55f, 0.3f, 0.18f, 0.5f), 3f, 0.4f, 0.8f, -0.4f);
                for (int i = 0; i < 5; i++) Chip(at);
            }
            else
            {
                Ring(_ring, at, 1.8f, 0.3f, new Color(0.7f, 0.75f, 0.4f, 0.8f), 0.028f);
                InkParticles.Burst(at + Vector3.up * 0.3f, 8, new Color(0.62f, 0.7f, 0.35f, 0.8f), new Color(0.55f, 0.32f, 0.18f, 0.4f), 1.6f, 0.4f, 0.6f, -0.3f);
            }
        }

        void Chip(Vector3 at)
        {
            var sr = Billboard(_rock, at + Vector3.up * 0.4f, UnityEngine.Random.Range(0.15f, 0.3f), 6);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            Vector3 v = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized * UnityEngine.Random.Range(1.5f, 3f) + Vector3.up * UnityEngine.Random.Range(3f, 5f);
            float t = 0f;
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                v += Vector3.down * 14f * dt;
                pivot.position += v * dt;
                if (pivot.position.y > 0.05f && t < 1.2f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 스켈레톤 무너짐 · 푸른 영혼불

        void Collapse(SkeletonBrain s)
        {
            Vector3 at = s.transform.position;
            InkParticles.Burst(at + Vector3.up * 0.8f, 16, new Color(0.96f, 0.93f, 0.82f, 1f), new Color(0.75f, 0.7f, 0.6f, 0.4f), 2.6f, 0.12f, 0.7f, 1.2f); // 뼛조각
            InkParticles.Burst(at + Vector3.up * 0.3f, 10, new Color(0.7f, 0.66f, 0.6f, 0.6f), new Color(0.6f, 0.58f, 0.55f, 0f), 1.2f, 0.5f, 0.9f, -0.2f);   // 먼지
            var pool = Flat(_pool, at, 1.9f, 0.024f);
            var wisps = new List<SpriteRenderer>();
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                var sr = Billboard(_wisp, at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.45f, 0.45f, 7);
                if (sr != null) { sr.color = new Color(1f, 1f, 1f, 0f); wisps.Add(sr); }
            }
            Vector3 poolUnit = pool != null ? pool.transform.localScale : Vector3.one;
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                bool on = s != null && !s.IsDead && s.IsReassembling;
                float k = Mathf.Clamp01(t / 1.5f);
                if (s != null) at = s.transform.position;
                if (pool != null)
                {
                    pool.transform.position = new Vector3(at.x, 0.024f, at.z);
                    pool.transform.Rotate(0f, 0f, 30f * dt, Space.Self);
                    pool.transform.localScale = poolUnit * Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(Mathf.Clamp01(t / 0.3f)));
                    pool.color = new Color(1f, 1f, 1f, on ? 0.85f : Mathf.MoveTowards(pool.color.a, 0f, dt / 0.3f));
                }
                for (int i = 0; i < wisps.Count; i++) // 바닥에서 몸 높이까지 솟아요
                {
                    var w = wisps[i];
                    if (w == null) continue;
                    float a = i * Mathf.PI * 0.5f + 0.4f + t * 2f;
                    w.transform.parent.position = at + new Vector3(Mathf.Cos(a) * 0.45f, Mathf.Lerp(0.1f, 1.4f, k), Mathf.Sin(a) * 0.45f);
                    w.color = new Color(1f, 1f, 1f, on ? Mathf.Clamp01(t / 0.25f) * (0.75f + 0.25f * Mathf.Sin(t * 14f + i)) : Mathf.MoveTowards(w.color.a, 0f, dt / 0.25f));
                }
                bool faded = !on && (pool == null || pool.color.a <= 0.01f);
                if (!faded && t < 4f) return true;
                if (pool != null) Destroy(pool.gameObject);
                foreach (var w in wisps) if (w != null) Destroy(w.transform.parent.gameObject);
                return false;
            });
        }

        void Rise(Vector3 at)
        {
            InkParticles.Burst(at + Vector3.up * 1f, 18, new Color(0.75f, 0.95f, 1f, 1f), new Color(Soul.r, Soul.g, Soul.b, 0.5f), 2.8f, 0.14f, 0.5f, 0.2f);
            Ring(_ring, at, 2.2f, 0.35f, new Color(Soul.r, Soul.g, Soul.b, 1f), 0.03f);
        }

        // ── 하피 급강하 · 늑대인간 발톱 베기 · 가고일 석화 (판타지 졸개)

        readonly Dictionary<EnemyBrain, (Vector3 last, float puff, bool fast)> _fliers = new Dictionary<EnemyBrain, (Vector3, float, bool)>();
        readonly Dictionary<Damageable, GargoyleBrain> _gargoyles = new Dictionary<Damageable, GargoyleBrain>();
        readonly List<EnemyBrain> _flierKeys = new List<EnemyBrain>();
        static Sprite _claw;

        void TickFliers(float dt)
        {
            if (_director != null && _director.Enemies != null)
                foreach (var e in _director.Enemies)
                {
                    if (e == null || e.IsDead) continue;
                    if ((e.Kind == EnemyKind.Harpy || e.Kind == EnemyKind.Werewolf) && !_fliers.ContainsKey(e)) _fliers[e] = (e.transform.position, 0f, false);
                    if (e is GargoyleBrain g && g.Damageable != null && !_gargoyles.ContainsKey(g.Damageable)) _gargoyles[g.Damageable] = g;
                }
            _flierKeys.Clear();
            _flierKeys.AddRange(_fliers.Keys);
            foreach (var e in _flierKeys)
            {
                if (e == null || e.IsDead) { _fliers.Remove(e); _hands.Remove(e); continue; }
                var s = _fliers[e];
                Vector3 p = e.transform.position, move = p - s.last;
                float speed = dt > 0f ? move.magnitude / dt : 0f;
                bool fast = speed > 5f;
                if (e.Kind == EnemyKind.Harpy && fast) // 급강하: 깃털이 흩날리는 바람 꼬리
                {
                    s.puff -= dt;
                    if (s.puff <= 0f)
                    {
                        s.puff = 0.05f;
                        InkParticles.Burst(p + Vector3.up * 1f - move.normalized * 0.5f, 3, new Color(0.45f, 0.7f, 0.85f, 0.9f), new Color(0.75f, 0.6f, 0.35f, 0f), 1.2f, 0.18f, 0.7f, 0.8f); // 청회색·청동 깃털
                        InkParticles.Burst(p + Vector3.up * 0.8f, 2, new Color(0.85f, 0.95f, 1f, 0.5f), new Color(0.85f, 0.95f, 1f, 0f), 0.6f, 0.6f, 0.4f, 0f);             // 바람
                    }
                }
                if (e.Kind == EnemyKind.Werewolf)
                {
                    if (s.fast && !fast && e.AttackName == "발톱 베기") Claws(p, s.last - p); // 덮치고 멈추는 순간: 세 줄 발톱 자국
                    var w = e as WerewolfBrain;
                    if (w != null && w.IsEnraged) // 광폭 (플레이테스트 3차: 몸이 아니라 손톱에서): 두 손에서 붉은 기운이 피어오르고, 휘두르면 붉은 손톱 자국 꼬리
                    {
                        var hands = Hands(w);
                        s.puff -= dt;
                        if (s.puff <= 0f)
                        {
                            s.puff = 0.07f;
                            foreach (var h in hands)
                                if (h != null) InkParticles.Burst(h.position, 2, new Color(1f, 0.3f, 0.18f, 0.85f), new Color(0.55f, 0.04f, 0.04f, 0f), 0.5f, 0.22f, 0.5f, -1.4f);
                        }
                    }
                }
                s.fast = fast;
                s.last = p;
                _fliers[e] = s;
            }
        }

        // 세 줄 발톱 자국 (주황빛 붉은 상처가 그어졌다 바래요)
        void Claws(Vector3 at, Vector3 back)
        {
            back.y = 0f;
            Vector3 dir = back.sqrMagnitude > 1e-4f ? -back.normalized : Vector3.forward;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            for (int i = -1; i <= 1; i++)
            {
                var sr = Flat(_claw, at + dir * 0.6f + side * i * 0.28f, 1.1f, 0.9f);
                if (sr == null) continue;
                sr.transform.rotation = Quaternion.Euler(90f, InkSwordQi.Yaw(side) + 20f, 0f);
                sr.sortingOrder = 7;
                float t = -Mathf.Abs(i) * 0.03f;
                _live.Add(dt =>
                {
                    if (sr == null) return false;
                    t += dt;
                    sr.color = new Color(1f, 0.4f, 0.15f, t < 0f ? 0f : Mathf.Clamp01(1f - t / 0.35f));
                    if (t < 0.35f) return true;
                    Destroy(sr.gameObject);
                    return false;
                });
            }
        }

        // 몸 한가운데 (모델 렌더러 범위로, 적마다 처음 한 번만 재요) · half = 몸 높이의 반
        readonly Dictionary<EnemyBrain, (Vector3 offset, float half)> _centers = new Dictionary<EnemyBrain, (Vector3, float)>();
        Vector3 BodyCenter(EnemyBrain e, out float half)
        {
            if (!_centers.TryGetValue(e, out var c))
            {
                c = (Vector3.up * e.Height * 0.5f, e.Height * 0.5f);
                Bounds b = default;
                bool any = false;
                foreach (var r in e.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r is SpriteRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (any) c = (b.center - e.transform.position, Mathf.Clamp(b.extents.y, 0.4f, 2f));
                if (_centers.Count > 64) _centers.Clear();
                _centers[e] = c;
            }
            half = c.half;
            return e.transform.position + c.offset;
        }

        // 늑대인간 두 손 (뼈 LeftHand · RightHand, 늑대인간마다 처음 한 번만 찾아요) — 찾을 때 손마다 붉은 손톱 꼬리(TrailRenderer)를 달아요
        readonly Dictionary<EnemyBrain, Transform[]> _hands = new Dictionary<EnemyBrain, Transform[]>();
        static Material _clawTrail;

        Transform[] Hands(EnemyBrain e)
        {
            if (_hands.TryGetValue(e, out var found)) return found;
            found = new[] { Bone(e.transform, "LeftHand"), Bone(e.transform, "RightHand") };
            _hands[e] = found;
            if (_clawTrail == null) _clawTrail = new Material(Shader.Find("Sprites/Default")) { name = "Werewolf Claw Trail" };
            foreach (var h in found)
            {
                if (h == null) continue;
                var go = new GameObject("Claw Trail");
                go.transform.SetParent(h, false);
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = _clawTrail;
                trail.time = 0.2f;
                trail.minVertexDistance = 0.04f;
                trail.widthMultiplier = 0.22f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                trail.colorGradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(1f, 0.45f, 0.3f), 0f), new GradientColorKey(new Color(0.8f, 0.05f, 0.05f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) },
                };
                trail.alignment = LineAlignment.View;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return found;
        }

        static Transform Bone(Transform root, string name)
        {
            if (root.name == name || root.name.EndsWith(":" + name)) return root;
            foreach (Transform child in root) { var t = Bone(child, name); if (t != null) return t; }
            return null;
        }

        static void OnEnraged(WerewolfBrain w) // 포효: 붉은 김이 터져요
        {
            if (w == null) return;
            var p = w.transform.position;
            foreach (var h in Host().Hands(w)) // 포효: 두 손톱에서 붉은 기운이 터져요
                if (h != null) InkParticles.Burst(h.position, 14, new Color(1f, 0.4f, 0.3f, 0.9f), new Color(0.6f, 0.08f, 0.06f, 0f), 1.8f, 0.4f, 0.7f, -0.8f);
            Host().Ring(_ring, p, 3f, 0.4f, new Color(1f, 0.2f, 0.12f, 1f), 0.03f);
        }

        static void OnStone(GargoyleBrain g, bool stone) // 석화: 돌가루와 회색 막 / 풀림: 돌 조각이 튀어요
        {
            if (g == null) return;
            var h = Host();
            var p = g.transform.position;
            if (stone)
            {
                InkParticles.Burst(p + Vector3.up * 1f, 18, new Color(0.8f, 0.78f, 0.72f, 0.9f), new Color(0.55f, 0.53f, 0.5f, 0f), 1.4f, 0.4f, 0.8f, 0.6f);
                h.Ring(_ring, p, 2.2f, 0.4f, new Color(0.75f, 0.75f, 0.78f, 1f), 0.03f);
            }
            else
            {
                InkParticles.Burst(p + Vector3.up * 1f, 14, new Color(0.85f, 0.82f, 0.75f, 1f), new Color(0.5f, 0.48f, 0.45f, 0.6f), 3f, 0.12f, 0.7f, 1.6f);
                for (int i = 0; i < 4; i++) h.Chip(p);
            }
        }

        static void OnDamaged(Damageable body, int amount) // 석화 중 맞으면: 불꽃 튀는 금속성 타격
        {
            if (_host == null || body == null || !_host._gargoyles.TryGetValue(body, out var g) || g == null || !g.IsStone) return;
            InkParticles.Burst(g.transform.position + Vector3.up * 1.1f, 10, new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.6f, 0.2f, 0.8f), 3.2f, 0.06f, 0.3f, 1.2f);
        }

        // ── 도움

        void Ring(Sprite sprite, Vector3 at, float size, float life, Color color, float height)
        {
            var sr = Flat(sprite, at, size, height);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        SpriteRenderer Billboard(Sprite sprite, Vector3 at, float width, int order)
        {
            if (sprite == null) return null;
            var pivot = new GameObject(sprite.name).transform;
            pivot.SetParent(transform, false);
            pivot.position = at;
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float height)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 4;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _pool = Resources.Load<Sprite>("TempleFx/GhostPool");
            _wisp = Resources.Load<Sprite>("TempleFx/GhostWisp");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _quake = Resources.Load<Sprite>("SkillFx/QuakeRing");
            _rock = Resources.Load<Sprite>("TempleFx/Rock");
            _claw = Resources.Load<Sprite>("BloodBuddhaFx/Slash");
        }
    }
}
