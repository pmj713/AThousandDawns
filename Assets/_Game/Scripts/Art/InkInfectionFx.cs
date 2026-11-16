using System;
using System.Collections.Generic;
using System.Reflection;
using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 차원 감염 정예 이펙트 (그림만 — 판정·수치는 게임 코드 EnemyBrain.Infection.cs 그대로). 채색: 청록 바람 · 주홍 불 · 보랏빛 균열.
    //   경공 (EnemyBrain.Dashed): 출발 자리부터 내달린 길을 따라 청록 바람 줄기가 뻗었다 흩어지고, 출발 자리에 바람 소용돌이와 먼지가 일어요.
    //   순간이동 (EnemyBrain.Blinked): 떠난 자리와 나타난 자리에 보랏빛 세로 균열이 확 열렸다 닫히고, 보랏빛 불티가 튀어요.
    //   화염구 예고 ("핏덩이" BloodBurst, 종류가 BurstKind.Fireball): 핏덩이 대신 하늘에서 불덩이가 불티를 끌며 떨어지고,
    //     바닥 예고 원 둘레에 주홍 불꽃 고리가 차올라요. 터지는 불길은 InkBurstFx(BurstKind.Fireball).
    //     (혈폭자 핏덩이 꾸밈 InkTombEnemyFx는 IsFireball로 이 예고를 건너뛰어요)
    // 그림: EliteFx (Gyeonggong · Fireball · Blink), PouchFx/Ring.
    public class InkInfectionFx : MonoBehaviour
    {
        static readonly Color Teal = new Color(0.2f, 0.9f, 0.85f);
        static readonly Color Violet = new Color(0.7f, 0.3f, 1f);
        static readonly Color Ember = new Color(1f, 0.45f, 0.1f);

        static InkInfectionFx _host;
        static Sprite _wind, _fireball, _rift, _ring;
        static Material _lineMat;
        static bool _loaded;
        static readonly FieldInfo RadiusField = typeof(BloodBurst).GetField("_radius", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo TotalField = typeof(BloodBurst).GetField("_total", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly HashSet<GameObject> _roots = new HashSet<GameObject>();

        // 화염구 예고인지 (BloodBurst.Kind)
        public static bool IsFireball(BloodBurst b) => b != null && b.Kind == BurstKind.Fireball;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.Dashed -= OnDashed;
            EnemyBrain.Dashed += OnDashed;
            EnemyBrain.Blinked -= OnBlinked;
            EnemyBrain.Blinked += OnBlinked;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkInfectionFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Infection Fx").AddComponent<InkInfectionFx>();
            return _host;
        }

        static void OnDashed(EnemyBrain e, Vector3 from) => Host().Dash(e, from);
        static void OnBlinked(EnemyBrain e, Vector3 from, Vector3 to) { Host().Rift(from, 0f); _host.Rift(to, 0.06f); }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 봐요
                if (root.name == "핏덩이")
                {
                    var b = root.GetComponent<BloodBurst>();
                    if (IsFireball(b)) Fireball(b);
                }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 경공: 청록 바람 줄기

        void Dash(EnemyBrain e, Vector3 from)
        {
            if (e == null) return;
            Vector3 start = new Vector3(from.x, 0.7f, from.z);
            var lr = Line(Teal, new Color(0.85f, 1f, 1f), 0.55f);
            var gust = Billboard(_wind, start + Vector3.up * 0.2f, 1.1f, 9);
            InkParticles.Burst(start - Vector3.up * 0.5f, 12, new Color(0.7f, 1f, 0.95f, 0.8f), new Color(Teal.r, Teal.g, Teal.b, 0.4f), 2.2f, 0.25f, 0.5f, -0.2f);
            float t = 0f;
            Vector3 head = start;
            _live.Add(dt =>
            {
                t += dt;
                if (e != null && !e.IsDead && t < 0.35f) head = e.transform.position + Vector3.up * 0.7f; // 내달리는 동안 따라가요
                float fade = t < 0.35f ? 1f : Mathf.Clamp01(1f - (t - 0.35f) / 0.3f);
                if (lr != null)
                {
                    lr.SetPosition(0, Vector3.Lerp(start, head, Mathf.Clamp01((t - 0.2f) / 0.45f))); // 꼬리가 따라 걷혀요
                    lr.SetPosition(1, head);
                    lr.widthMultiplier = 0.55f * fade;
                }
                if (gust != null)
                {
                    gust.transform.localRotation = Quaternion.Euler(0f, 0f, -t * 360f);
                    gust.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - t / 0.5f));
                }
                if (t < 0.65f) return true;
                if (lr != null) Destroy(lr.gameObject);
                if (gust != null) Destroy(gust.transform.parent.gameObject);
                return false;
            });
        }

        // ── 순간이동: 보랏빛 세로 균열이 열렸다 닫혀요

        void Rift(Vector3 at, float delay)
        {
            var sr = Billboard(_rift, new Vector3(at.x, 1.1f, at.z), 1.1f, 9);
            var ring = Flat(_ring, at, 1.8f, 0.03f);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            sr.transform.localScale = new Vector3(0f, unit.y, unit.z);
            float t = -delay;
            bool burst = false;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < 0f) return true;
                if (!burst)
                {
                    burst = true;
                    InkParticles.Burst(new Vector3(at.x, 1f, at.z), 16, new Color(1f, 0.75f, 1f, 1f), new Color(Violet.r, Violet.g, Violet.b, 0.5f), 2.5f, 0.12f, 0.5f, 0.3f);
                }
                float open = t < 0.12f ? t / 0.12f : t < 0.32f ? 1f : Mathf.Clamp01(1f - (t - 0.32f) / 0.25f); // 열렸다 · 잠깐 · 닫혀요
                sr.transform.localScale = new Vector3(unit.x * open * (1f + 0.1f * Mathf.Sin(t * 40f)), unit.y * (0.8f + 0.2f * open), unit.z);
                if (ring != null)
                {
                    ring.transform.Rotate(0f, 0f, 200f * dt, Space.Self);
                    ring.color = new Color(Violet.r, Violet.g, Violet.b, 0.8f * open);
                }
                if (t < 0.6f) return true;
                Destroy(sr.transform.parent.gameObject);
                if (ring != null) Destroy(ring.gameObject);
                return false;
            });
        }

        // ── 화염구 예고: 떨어지는 불덩이 + 차오르는 불꽃 고리

        void Fireball(BloodBurst b)
        {
            var go = b.gameObject;
            Vector3 land = go.transform.position;
            float radius = RadiusField != null && RadiusField.GetValue(b) is float r ? r : 1.3f;
            float fuse = TotalField != null && TotalField.GetValue(b) is float f ? Mathf.Max(0.2f, f) : 1f;
            Vector3 sky = land + new Vector3(-1.5f, 7f, 1.5f); // 비스듬히 떨어져요
            var ball = Billboard(_fireball, sky, 0.9f, 10);
            var embers = ball != null ? InkParticles.Embers(ball.transform.parent, Vector3.zero, 0.25f, 2f) : null;
            var ring = Flat(_ring, land, radius * 2.15f, 0.031f);
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                float k = Mathf.Clamp01(t / fuse);
                if (ball != null)
                {
                    ball.transform.parent.position = Vector3.Lerp(sky, land + Vector3.up * 0.4f, k * k); // 점점 빨라져요
                    ball.transform.localRotation = Quaternion.Euler(0f, 0f, t * 400f);
                    ball.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.15f));
                }
                if (ring != null)
                {
                    ring.transform.Rotate(0f, 0f, 120f * dt, Space.Self);
                    ring.color = new Color(Ember.r, Ember.g, Ember.b, 0.35f + 0.55f * k);
                }
                if (go != null) return true;
                // 터졌어요 (불길은 InkBurstFx)
                if (ball != null) Destroy(ball.transform.parent.gameObject);
                if (ring != null) Destroy(ring.gameObject);
                return false;
            });
        }

        // ── 도움

        LineRenderer Line(Color edge, Color core, float width)
        {
            var go = new GameObject("Infection Line");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMat;
            lr.positionCount = 2;
            lr.widthMultiplier = width;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(1f, 1f));
            lr.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(edge, 0f), new GradientColorKey(core, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 1f) },
            };
            lr.numCapVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
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
            _wind = Resources.Load<Sprite>("EliteFx/Gyeonggong");
            _fireball = Resources.Load<Sprite>("EliteFx/Fireball");
            _rift = Resources.Load<Sprite>("EliteFx/Blink");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _lineMat = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
