using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 일반 적 등장 동작(InkEnemyModels "Spawn", 처음 나타날 때 한 번)에 맞춘 이펙트 — 새로 생긴 적을 찾아 붙여요.
    //   멧돼지·들개 (26프레임): 뛰어들어 착지하는 13프레임(0.43초)에 발밑 흙먼지와 흙 부스러기가 튀어요.
    //   등불 귀신 (24프레임): 바닥 근처 푸른 불씨가 일렁이다 커지며 떠올라요 (0~0.8초).
    //   원귀 (28프레임): 몸이 바닥 아래에 있는 0~0.6초 동안 발밑에 먹 연기 웅덩이가 번지고, 떠오르며 옅어져요.
    // 그림: BloodBuddhaFx/Splash, TempleFx (Rock · GhostWisp · GhostPool), HeavenlyDemonFx/Wisp.
    public class InkEnemySpawnFx : MonoBehaviour
    {
        const float Fps = 30f;
        static Sprite _splash, _rock, _ghostWisp, _ghostPool, _wisp;

        readonly HashSet<EnemyBrain> _seen = new HashSet<EnemyBrain>();
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        const int MaxLive = 400; // 동시에 움직이는 그림 상한
        float _scan;
        EnemyDirector _director;
        float _findDirector;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            _splash = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
            _rock = Resources.Load<Sprite>("TempleFx/Rock");
            _ghostWisp = Resources.Load<Sprite>("TempleFx/GhostWisp");
            _ghostPool = Resources.Load<Sprite>("TempleFx/GhostPool");
            _wisp = Resources.Load<Sprite>("HeavenlyDemonFx/Wisp");
            var go = new GameObject("Ink Enemy Spawn Fx");
            DontDestroyOnLoad(go);
            go.AddComponent<InkEnemySpawnFx>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _scan -= dt;
            if (_scan <= 0f)
            {
                _scan = 0.05f; // 등장 동작 시작과 거의 같이
                if (_director == null && (_findDirector -= 0.05f) <= 0f) { _findDirector = 1f; _director = FindAnyObjectByType<EnemyDirector>(); } // 전체 검색은 1초에 한 번만
                var list = _director != null ? _director.Enemies : null;
                if (list != null && _seen.Count > 64) _seen.RemoveWhere(e => e == null);
                if (list != null) foreach (var e in list)
                {
                    if (e == null || !_seen.Add(e) || e.IsDead || e.IsHidden) continue;
                    switch (e.Kind)
                    {
                        case EnemyKind.Boar:
                        case EnemyKind.Hound: Landing(e.transform, 13f / Fps); break;
                        case EnemyKind.Lantern: Ember(e.transform); break;
                        case EnemyKind.Wraith: SmokePool(e.transform); break;
                    }
                }
            }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 멧돼지·들개: 착지 흙먼지
        void Landing(Transform body, float delay)
        {
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                if (t < delay) return true;
                if (body == null) return false;
                var at = body.position;
                Puff(at, 1.5f, 0.5f, new Color(0.45f, 0.38f, 0.3f, 0.6f));
                for (int i = 0; i < 6; i++) Chip(at);
                return false;
            });
        }

        void Chip(Vector3 at)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Upright(_rock, at + Vector3.up * 0.1f, UnityEngine.Random.Range(0.1f, 0.18f), false);
            if (sr == null) return;
            sr.color = new Color(0.55f, 0.45f, 0.35f);
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var v = new Vector3(Mathf.Cos(a) * UnityEngine.Random.Range(1f, 2f), UnityEngine.Random.Range(2.5f, 3.8f), Mathf.Sin(a) * UnityEngine.Random.Range(1f, 2f));
            var pivot = sr.transform.parent;
            float t = 0f, spin = UnityEngine.Random.Range(-600f, 600f);
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                v += Vector3.down * 14f * dt;
                pivot.position += v * dt;
                sr.transform.Rotate(0f, 0f, spin * dt, Space.Self);
                if (pivot.position.y > 0.05f && t < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 등불 귀신: 바닥 불씨가 일렁이다 커지며 떠올라요
        void Ember(Transform body)
        {
            var at = body.position;
            var pool = Flat(_ghostPool, at, 0.9f, 0.04f);
            var flame = Upright(_ghostWisp, at, 0.25f, true);
            if (flame == null) return;
            var pivot = flame.transform.parent;
            Vector3 unit = flame.transform.localScale;
            Vector3 poolUnit = pool != null ? pool.transform.localScale : Vector3.one;
            float t = 0f, life = 24f / Fps;
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                float k = t / life;
                if (body != null) pivot.position = new Vector3(body.position.x, at.y + Mathf.Lerp(0f, 0.5f, k), body.position.z);
                float flick = 1f + 0.18f * Mathf.Sin(t * 30f);
                float grow = Mathf.Lerp(1f, 3f, Mathf.Clamp01(k * 1.4f));
                flame.transform.localScale = new Vector3(unit.x * grow / flick, unit.y * grow * flick, unit.z);
                flame.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 4f) * (1f - Mathf.Clamp01((k - 0.7f) / 0.3f)));
                if (pool != null)
                {
                    pool.transform.localScale = poolUnit * Mathf.Lerp(0.4f, 1.2f, Mathf.Clamp01(k * 2f));
                    pool.color = new Color(1f, 1f, 1f, 0.7f * (1f - k));
                }
                if (k < 1f) return true;
                Destroy(pivot.gameObject);
                if (pool != null) Destroy(pool.gameObject);
                return false;
            });
        }

        // ── 원귀: 바닥 먹 연기 웅덩이에서 떠올라요
        void SmokePool(Transform body)
        {
            var at = body.position;
            var pool = Flat(_splash, at, 2f, 0.04f);
            if (pool == null) return;
            Vector3 unit = pool.transform.localScale;
            float t = 0f, rise = 18f / Fps, spawn = 0f;
            _live.Add(dt =>
            {
                if (pool == null) return false;
                t += dt;
                float k = t / (rise + 0.4f);
                pool.transform.localScale = unit * Mathf.Lerp(0.5f, 1.15f, Mathf.Sqrt(Mathf.Clamp01(t / 0.3f)));
                pool.transform.Rotate(0f, 0f, 30f * dt, Space.Self);
                pool.color = new Color(0.12f, 0.11f, 0.13f, 0.85f * (1f - Mathf.Clamp01((t - 0.4f) / (rise + 0.4f - 0.4f))));
                spawn -= dt;
                if (t < rise && spawn <= 0f) // 몸이 올라오는 동안 연기가 피어올라요
                {
                    spawn = 0.07f;
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 0.6f;
                    Rise(at + new Vector3(r.x, 0f, r.y));
                }
                if (k < 1f) return true;
                Destroy(pool.gameObject);
                return false;
            });
        }

        void Rise(Vector3 at)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Upright(_wisp, at, UnityEngine.Random.Range(0.4f, 0.65f), true);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                float k = t / 0.7f;
                pivot.position = at + Vector3.up * k * 0.6f;
                sr.transform.localScale = new Vector3(unit.x * (1f - 0.4f * k), unit.y * (0.7f + 0.6f * k), unit.z);
                sr.color = new Color(0.32f, 0.3f, 0.34f, Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.75f); // 먹빛 (마기의 붉은 기는 빼요)
                if (k < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 도움

        void Puff(Vector3 at, float size, float life, Color color)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Flat(_splash, at, size, 0.05f);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.4f, 1.1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
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
            return sr;
        }

        SpriteRenderer Upright(Sprite sprite, Vector3 at, float width, bool bottom)
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
            sr.sortingOrder = 6;
            return sr;
        }
    }
}
