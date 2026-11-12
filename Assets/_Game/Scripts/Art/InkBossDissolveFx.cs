using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 보스 처치: 쓰러진 보스(녹림왕 · 혈불 · 철룡)와 고수 다섯이 발밑부터 먹으로 흩어질 때 (InkCorpse.BossDissolve 자리 · 시간, 메인 아트)
    //   흩어지는 경계(발밑 → 머리)를 따라 먹물 방울이 튀어 오르고 먹 연기가 위로 흩날려요. 발밑엔 먹 자국이 번졌다 옅어져요.
    // 경계 높이는 쓰러진 몸의 렌더러 높이에 맞춰요 (못 찾으면 2.8칸). 신호가 main에 들어오기 전에도 컴파일되게 이벤트는 이름으로 찾아 붙여요.
    // 그림: BloodBuddhaFx (Drop · Splash), HeavenlyDemonFx/Wisp (먹빛으로 물들여요).
    public class InkBossDissolveFx : MonoBehaviour
    {
        const float BossHeight = 2.8f; // 몸을 못 찾을 때 (보스 키 2.4~3.2, 고수 1.9~2.1)
        static InkBossDissolveFx _host;
        static Sprite _drop, _splash, _wisp;
        static bool _hooked;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (_hooked) return;
            var corpse = typeof(InkBossDissolveFx).Assembly.GetType("AThousandDawns.Art.InkCorpse");
            var ev = corpse != null ? corpse.GetEvent("BossDissolve", BindingFlags.Public | BindingFlags.Static) : null;
            if (ev == null) return;
            var handler = Delegate.CreateDelegate(ev.EventHandlerType, typeof(InkBossDissolveFx).GetMethod(nameof(OnDissolve), BindingFlags.NonPublic | BindingFlags.Static), false);
            if (handler == null) return;
            ev.AddEventHandler(null, handler);
            _hooked = true;
        }

        static void OnDissolve(Vector3 at, float time)
        {
            if (_host == null)
            {
                _drop = Resources.Load<Sprite>("BloodBuddhaFx/Drop");
                _splash = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
                _wisp = Resources.Load<Sprite>("HeavenlyDemonFx/Wisp");
                _host = new GameObject("Ink Boss Dissolve Fx").AddComponent<InkBossDissolveFx>();
            }
            _host.Play(at, Mathf.Max(0.3f, time));
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void Play(Vector3 at, float time)
        {
            Stain(at, time);
            float height = BodyHeight(at);
            float t = 0f, dropTimer = 0f, smokeTimer = 0f;
            _live.Add(dt =>
            {
                t += dt;
                if (t >= time) return false;
                float edge = height * (t / time); // 흩어지는 경계: 발밑에서 머리로
                dropTimer -= dt;
                smokeTimer -= dt;
                if (dropTimer <= 0f) { dropTimer = 0.03f; Drop(at, edge); }
                if (smokeTimer <= 0f) { smokeTimer = 0.07f; Smoke(at, edge); }
                return true;
            });
        }

        // 쓰러진 몸의 키: 그 자리에 있는 적 모델의 렌더러 높이 (고수는 보스보다 작아요)
        static float BodyHeight(Vector3 at)
        {
            AThousandDawns.Enemies.EnemyBrain best = null;
            float bestD = 2.25f; // 1.5칸 안
            foreach (var e in FindObjectsByType<AThousandDawns.Enemies.EnemyBrain>(FindObjectsSortMode.None))
            {
                var d = e.transform.position - at;
                d.y = 0f;
                if (d.sqrMagnitude < bestD) { bestD = d.sqrMagnitude; best = e; }
            }
            if (best == null) return BossHeight;
            float top = float.MinValue;
            foreach (var r in best.GetComponentsInChildren<Renderer>())
                if (r.enabled && !(r is ParticleSystemRenderer) && !(r is LineRenderer) && !(r is TrailRenderer) && !(r is SpriteRenderer)) top = Mathf.Max(top, r.bounds.max.y);
            return top > at.y ? Mathf.Clamp(top - at.y, 1.2f, 3.6f) : BossHeight;
        }

        // 먹물 방울: 경계에서 바깥·위로 튀었다가 떨어지며 옅어져요
        void Drop(Vector3 at, float edge)
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f), r = UnityEngine.Random.Range(0.25f, 0.6f);
            var start = at + new Vector3(Mathf.Cos(a) * r, edge, Mathf.Sin(a) * r);
            var sr = Upright(_drop, start, UnityEngine.Random.Range(0.08f, 0.16f));
            if (sr == null) return;
            sr.color = new Color(0.08f, 0.07f, 0.07f, 0.95f);
            var pivot = sr.transform.parent;
            var v = new Vector3(Mathf.Cos(a) * UnityEngine.Random.Range(0.6f, 1.4f), UnityEngine.Random.Range(1.5f, 3f), Mathf.Sin(a) * UnityEngine.Random.Range(0.6f, 1.4f));
            float t = 0f;
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                v += Vector3.down * 6f * dt;
                pivot.position += v * dt;
                sr.color = new Color(0.08f, 0.07f, 0.07f, 0.95f * (1f - t / 0.9f));
                if (t < 0.9f && pivot.position.y > 0f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // 먹 연기: 경계에서 피어올라 위로 흩날려요
        void Smoke(Vector3 at, float edge)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * 0.5f;
            var start = at + new Vector3(r.x, Mathf.Max(0f, edge - 0.3f), r.y);
            var sr = Upright(_wisp, start, UnityEngine.Random.Range(0.5f, 0.85f), true);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            Vector3 unit = sr.transform.localScale;
            float t = 0f, life = UnityEngine.Random.Range(0.8f, 1.2f), drift = UnityEngine.Random.Range(-0.4f, 0.4f);
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                float k = t / life;
                pivot.position = start + new Vector3(drift * k, 1.4f * k, 0f);
                sr.transform.localScale = new Vector3(unit.x * (1f + 0.5f * k), unit.y * (0.8f + 0.7f * k), unit.z);
                sr.color = new Color(0.14f, 0.13f, 0.14f, 0.7f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI));
                if (k < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // 발밑 먹 자국: 번졌다가 흩어짐이 끝나고 옅어져요
        void Stain(Vector3 at, float time)
        {
            if (_splash == null) return;
            var go = new GameObject("Boss Ink Stain");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, 0.025f, at.z); // 공격 예고(0.03)보다 아래
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            Vector3 unit = Vector3.one * (2.6f / Mathf.Max(0.01f, _splash.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _splash;
            sr.sortingOrder = 3;
            float t = 0f, total = time + 1.5f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                go.transform.localScale = unit * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(Mathf.Clamp01(t / (time * 0.6f))));
                float fade = 1f - Mathf.Clamp01((t - time) / 1.5f);
                sr.color = new Color(0.1f, 0.09f, 0.09f, 0.8f * fade);
                if (t < total) return true;
                Destroy(go);
                return false;
            });
        }

        SpriteRenderer Upright(Sprite sprite, Vector3 at, float width, bool bottom = false)
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
            sr.sortingOrder = 7;
            return sr;
        }
    }
}
