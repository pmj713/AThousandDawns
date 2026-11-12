using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 폐사찰 적 이펙트 (그림만 — 판정·수치는 게임 코드 그대로).
    //   목탁승 가호: 가호(Damageable.Ward)가 걸린 적 둘레에 금빛 막이 떠서 돌며 반짝여요 → 다 깎이면 금빛으로 깨져 흩어져요.
    //     목탁을 칠 때(MoktakBrain.Struck) 금빛 소리 물결이 가호 범위까지 두 겹으로 퍼져요.
    //   등불 귀신 도깨비불(GhostFire): 불투명한 하늘색 원판은 숨기고, 투명한 푸른 불 장판 + 일렁이는 푸른 불꽃으로 바꿔요 (끝 무렵 흐려지는 것도 그대로).
    //   석상 나한 돌진 끝(ArhatBrain.ChargeQuake): 갈라진 땅 충격파와 돌 부스러기가 튀었다 떨어져요.
    // 그림: TempleFx(GhostPool · GhostWisp · Rock), PouchFx(Ring · Glint · Burst), SkillFx(QuakeRing).
    public class InkTempleEnemyFx : MonoBehaviour
    {
        static InkTempleEnemyFx _host;
        static Sprite _pool, _wisp, _rock, _ring, _glint, _burst, _crack;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly Dictionary<EnemyBrain, SpriteRenderer> _wards = new Dictionary<EnemyBrain, SpriteRenderer>();
        readonly HashSet<GhostFire> _dressed = new HashSet<GhostFire>();
        EnemyDirector _director;
        float _scan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            MoktakBrain.Struck -= OnStruck;
            MoktakBrain.Struck += OnStruck;
            ArhatBrain.ChargeQuake -= OnQuake;
            ArhatBrain.ChargeQuake += OnQuake;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
        }

        static InkTempleEnemyFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Temple Enemy Fx").AddComponent<InkTempleEnemyFx>();
            return _host;
        }

        static void OnStruck(MoktakBrain m, Vector3 at, float radius) => Host().Ripple(at, radius);
        static void OnQuake(ArhatBrain a, Vector3 at, float radius) => Host().Quake(at, radius);

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            TickWards(dt);
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.1f; DressGhostFires(); }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 목탁승 가호

        void TickWards(float dt)
        {
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            if (_director != null && _director.Enemies != null)
                foreach (var e in _director.Enemies)
                {
                    if (e == null || e.IsDead || e.Damageable == null || e.Damageable.Ward <= 0 || _wards.ContainsKey(e)) continue;
                    var shell = Upright(_ring, Center(e), Mathf.Max(1.2f, e.Height * 1.3f), 6);
                    if (shell != null) { _wards[e] = shell; Pop(shell); }
                }
            List<EnemyBrain> gone = null;
            foreach (var pair in _wards)
            {
                var e = pair.Key;
                var shell = pair.Value;
                if (shell == null) { (gone ??= new List<EnemyBrain>()).Add(e); continue; }
                bool alive = e != null && !e.IsDead && e.Damageable != null;
                if (!alive || e.Damageable.Ward <= 0)
                {
                    if (alive) Shatter(Center(e), e.Height); // 가호가 다 깎였어요
                    Destroy(shell.gameObject);
                    (gone ??= new List<EnemyBrain>()).Add(e);
                    continue;
                }
                shell.transform.parent.position = Center(e);
                shell.transform.Rotate(0f, 0f, 50f * dt, Space.Self);
                shell.color = new Color(1f, 0.95f, 0.75f, 0.55f + 0.2f * Mathf.Sin(Time.time * 4f + e.GetInstanceHashCode()));
                if (UnityEngine.Random.value < dt * 4f) // 가끔 반짝
                    Glint(Center(e) + UnityEngine.Random.insideUnitSphere * e.Height * 0.5f, Vector3.up * 0.4f, 0.25f, 0.6f, 0f);
            }
            if (gone != null) foreach (var e in gone) _wards.Remove(e);
        }

        static Vector3 Center(EnemyBrain e) => e.transform.position + Vector3.up * Mathf.Max(0.6f, e.Height * 0.55f);

        void Pop(SpriteRenderer shell)
        {
            float t = 0f;
            Vector3 unit = shell.transform.localScale;
            _live.Add(dt =>
            {
                if (shell == null) return false;
                t += dt;
                float k = Mathf.Clamp01(t / 0.2f);
                shell.transform.localScale = unit * (k < 1f ? Mathf.Lerp(0.3f, 1.15f, k) : 1f);
                return k < 1f;
            });
        }

        void Shatter(Vector3 at, float height)
        {
            var burst = Upright(_burst, at, Mathf.Max(1.4f, height * 1.4f), 7);
            if (burst != null)
            {
                float t = 0f;
                Vector3 unit = burst.transform.localScale;
                _live.Add(dt =>
                {
                    if (burst == null) return false;
                    t += dt;
                    burst.transform.localScale = unit * Mathf.Lerp(0.6f, 1.5f, Mathf.Clamp01(t / 0.25f));
                    burst.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(t / 0.45f));
                    if (t < 0.45f) return true;
                    Destroy(burst.transform.parent.gameObject);
                    return false;
                });
            }
            for (int i = 0; i < 12; i++)
            {
                var v = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 3.5f);
                v.y = Mathf.Abs(v.y) + 0.5f;
                Glint(at, v, UnityEngine.Random.Range(0.25f, 0.4f), 0.5f, 5f);
            }
        }

        // 목탁 소리: 금빛 물결이 두 겹으로 퍼져요
        void Ripple(Vector3 at, float radius)
        {
            for (int k = 0; k < 2; k++)
            {
                var sr = Flat(_ring, at, 1f, UnityEngine.Random.Range(0f, 360f), 0.04f + k * 0.005f);
                if (sr == null) continue;
                Vector3 unit = sr.transform.localScale;
                float t = -k * 0.15f, size = Mathf.Max(1f, radius) * 2f;
                sr.color = new Color(1f, 1f, 1f, 0f);
                _live.Add(dt =>
                {
                    if (sr == null) return false;
                    t += dt;
                    if (t < 0f) return true;
                    float g = Mathf.Clamp01(t / 0.6f);
                    sr.transform.localScale = unit * Mathf.Lerp(0.6f, size, 1f - (1f - g) * (1f - g));
                    sr.color = new Color(1f, 0.95f, 0.75f, 0.8f * (1f - g));
                    if (g < 1f) return true;
                    Destroy(sr.gameObject);
                    return false;
                });
            }
        }

        // ── 등불 귀신 도깨비불

        void DressGhostFires()
        {
            _dressed.RemoveWhere(f => f == null);
            foreach (var fire in FindObjectsByType<GhostFire>(FindObjectsSortMode.None))
            {
                if (_dressed.Contains(fire) || fire.name == "마기 장판") continue; // 천마 마기 장판은 InkHeavenlyDemonFx가 검붉게
                _dressed.Add(fire);
                var source = fire.GetComponent<Renderer>();
                if (source != null) source.enabled = false; // 불투명한 하늘색 원판은 숨기고 (흐려지는 값은 그 재질 알파를 읽어요)
                float radius = fire.transform.localScale.x * 0.5f;
                var pool = Flat(_pool, fire.transform.position, radius * 2.3f, UnityEngine.Random.Range(0f, 360f), 0.035f);
                var wisps = new List<(SpriteRenderer sr, Vector3 at, float seed)>();
                int n = Mathf.Clamp(Mathf.RoundToInt(radius * 3f), 2, 5);
                for (int i = 0; i < n; i++)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * radius * 0.7f;
                    var at = fire.transform.position + new Vector3(r.x, 0.05f, r.y);
                    var sr = Upright(_wisp, at, UnityEngine.Random.Range(0.35f, 0.55f), 6, bottom: true);
                    if (sr != null) wisps.Add((sr, at, UnityEngine.Random.Range(0f, 10f)));
                }
                Vector3 poolUnit = pool != null ? pool.transform.localScale : Vector3.one;
                _live.Add(dt =>
                {
                    if (fire == null) // 꺼졌어요
                    {
                        if (pool != null) Destroy(pool.gameObject);
                        foreach (var w in wisps) if (w.sr != null) Destroy(w.sr.transform.parent.gameObject);
                        return false;
                    }
                    float alpha = source != null ? Mathf.Clamp01(source.sharedMaterial.color.a / 0.55f) : 1f;
                    float tt = Time.time;
                    if (pool != null)
                    {
                        pool.transform.Rotate(0f, 0f, -25f * dt, Space.Self);
                        pool.transform.localScale = poolUnit * (1f + 0.05f * Mathf.Sin(tt * 7f));
                        pool.color = new Color(1f, 1f, 1f, 0.85f * alpha);
                    }
                    foreach (var w in wisps)
                    {
                        if (w.sr == null) continue;
                        float flick = 1f + 0.15f * Mathf.Sin(tt * 11f + w.seed) + 0.08f * Mathf.Sin(tt * 23f + w.seed * 2f);
                        w.sr.transform.parent.position = w.at + Vector3.up * 0.06f * Mathf.Sin(tt * 3f + w.seed);
                        w.sr.transform.localScale = new Vector3(1f / flick, flick, 1f) * w.sr.transform.localScale.z;
                        w.sr.color = new Color(1f, 1f, 1f, alpha);
                    }
                    return true;
                });
            }
        }

        // ── 석상 나한: 땅이 갈라지고 돌 부스러기가 튀어요

        void Quake(Vector3 at, float radius)
        {
            var crack = Flat(_crack, at, 1f, UnityEngine.Random.Range(0f, 360f), 0.05f);
            if (crack != null)
            {
                Vector3 unit = crack.transform.localScale;
                float t = 0f, size = Mathf.Max(1f, radius) * 2.3f;
                crack.color = new Color(0.75f, 0.72f, 0.68f, 1f); // 잿빛 돌
                _live.Add(dt =>
                {
                    if (crack == null) return false;
                    t += dt;
                    crack.transform.localScale = unit * Mathf.Lerp(0.4f, size, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.2f), 3f));
                    crack.color = new Color(0.75f, 0.72f, 0.68f, t < 0.3f ? 1f : Mathf.Clamp01(1f - (t - 0.3f) / 0.6f));
                    if (t < 0.9f) return true;
                    Destroy(crack.gameObject);
                    return false;
                });
            }
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                var v = new Vector3(Mathf.Cos(a) * UnityEngine.Random.Range(1.5f, 3.5f), UnityEngine.Random.Range(3f, 5.5f), Mathf.Sin(a) * UnityEngine.Random.Range(1.5f, 3.5f));
                Rock(at + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * radius * 0.4f, v, UnityEngine.Random.Range(0.25f, 0.5f));
            }
        }

        void Rock(Vector3 at, Vector3 velocity, float size)
        {
            var sr = Upright(_rock, at, size, 6);
            if (sr == null) return;
            float t = 0f, spin = UnityEngine.Random.Range(-540f, 540f);
            var pivot = sr.transform.parent;
            bool landed = false;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (!landed)
                {
                    velocity += Vector3.down * 14f * dt;
                    pivot.position += velocity * dt;
                    sr.transform.Rotate(0f, 0f, spin * dt, Space.Self);
                    if (pivot.position.y <= 0.1f) { landed = true; pivot.position = new Vector3(pivot.position.x, 0.1f, pivot.position.z); t = 0f; }
                    return true;
                }
                sr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((t - 0.4f) / 0.5f)); // 떨어진 뒤 잠깐 있다 사라져요
                if (t < 0.9f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 도움

        void Glint(Vector3 at, Vector3 velocity, float size, float life, float gravity)
        {
            var sr = Upright(_glint, at, size, 7);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                velocity += Vector3.down * gravity * dt;
                pivot.position += velocity * dt;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                if (k < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // 카메라를 보는 받침(부모) 안의 그림 — 그림은 그 안에서 돌릴 수 있어요. bottom이면 아래 끝이 at
        SpriteRenderer Upright(Sprite sprite, Vector3 at, float width, int order, bool bottom = false)
        {
            if (sprite == null) return null;
            var pivot = new GameObject(sprite.name).transform;
            pivot.SetParent(transform, false);
            pivot.position = at;
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            float s = width / Mathf.Max(0.01f, sprite.bounds.size.x);
            go.transform.localScale = Vector3.one * s;
            if (bottom) go.transform.localPosition = Vector3.up * sprite.bounds.size.y * s * 0.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float yaw, float height)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 4;
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _pool = Resources.Load<Sprite>("TempleFx/GhostPool");
            _wisp = Resources.Load<Sprite>("TempleFx/GhostWisp");
            _rock = Resources.Load<Sprite>("TempleFx/Rock");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _glint = Resources.Load<Sprite>("PouchFx/Glint");
            _burst = Resources.Load<Sprite>("PouchFx/Burst");
            _crack = Resources.Load<Sprite>("SkillFx/QuakeRing");
        }
    }

    static class InkTempleEnemyFxExt
    {
        public static float GetInstanceHashCode(this EnemyBrain e) => (e.GetHashCode() % 100) * 0.1f;
    }
}
