using System;
using System.Collections.Generic;
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
        }
    }
}
