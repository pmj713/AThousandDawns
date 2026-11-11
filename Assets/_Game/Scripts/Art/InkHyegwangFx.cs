using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 파계승 혜광 이펙트 (HyegwangBrain.Cue 신호를 듣고 그림만 그려요 — 판정·수치는 게임 코드 그대로).
    //   금강불괴: 발밑에 금빛 고리가 충격파 반경만큼 깔려 숨 쉬고 반짝이가 피어올라요.
    //     2페이즈에서 맞을 때마다(VajraGrow) 고리가 커진 반경으로 번쩍 넓어지고, 금빛이 짙은 호박색으로 변하며 바닥 금빛 균열이 짙어져요.
    //     터질 때(VajraBurst) 금빛 균열 충격파와 금빛 터짐이 그 반경 크기로 퍼져요.
    //   축지: 다가간 길에 굵은 먹 붓 잔상과 먹 번짐이 남았다 바래요.
    // 그림은 다른 이펙트 것을 같이 써요: PouchFx(Ring · Glint · Burst), SkillFx(QuakeRing), BloodBuddhaFx(Splash), HudArt/HpStroke.
    public class InkHyegwangFx : MonoBehaviour
    {
        static InkHyegwangFx _host;
        static Sprite _ring, _glint, _burst, _crack, _splash;
        static Material _lineMat;
        static bool _loaded;

        class Vajra { public HyegwangBrain Boss; public SpriteRenderer Ring, Crack; public float Radius, Grow, Flash, T, Life, Spawn; public Vector3 RingUnit, CrackUnit; }
        readonly Dictionary<HyegwangBrain, Vajra> _vajra = new Dictionary<HyegwangBrain, Vajra>();
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            HyegwangBrain.Cue -= OnCue;
            HyegwangBrain.Cue += OnCue;
        }

        static void OnCue(HyegwangBrain boss, HyegwangBrain.FxCue c)
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Hyegwang Fx").AddComponent<InkHyegwangFx>();
            switch (c.Kind)
            {
                case HyegwangBrain.FxKind.VajraStart: _host.VajraStart(boss, c); break;
                case HyegwangBrain.FxKind.VajraGrow: _host.VajraGrow(boss, c); break;
                case HyegwangBrain.FxKind.VajraBurst: _host.VajraBurst(boss, c); break;
                case HyegwangBrain.FxKind.Rush: _host.Rush(c); break;
            }
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            TickVajra(dt);
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 금강불괴

        void VajraStart(HyegwangBrain boss, HyegwangBrain.FxCue c)
        {
            EndVajra(boss);
            var v = new Vajra { Boss = boss, Radius = c.Radius, Life = c.Time + 0.6f }; // 얼려서 끊기면 알아서 꺼져요
            v.Ring = Flat(_ring, c.At, 1f, 0f, 0.04f);
            v.Crack = Flat(_crack, c.At, 1f, UnityEngine.Random.Range(0f, 360f), 0.035f);
            if (v.Ring != null) v.RingUnit = v.Ring.transform.localScale;
            if (v.Crack != null) { v.CrackUnit = v.Crack.transform.localScale; v.Crack.color = new Color(1f, 0.8f, 0.35f, 0f); }
            _vajra[boss] = v;
        }

        void VajraGrow(HyegwangBrain boss, HyegwangBrain.FxCue c)
        {
            if (!_vajra.TryGetValue(boss, out var v)) return;
            v.Radius = c.Radius;
            v.Grow = c.Grow;
            v.Flash = 1f;
            for (int i = 0; i < 6; i++) // 번쩍일 때 반짝이가 튀어요
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var vel = new Vector3(Mathf.Cos(a), UnityEngine.Random.Range(1f, 2f), Mathf.Sin(a)) * UnityEngine.Random.Range(1.5f, 2.5f);
                Glint(boss.transform.position + Vector3.up * 1.2f, vel, UnityEngine.Random.Range(0.3f, 0.45f), 0.6f, 4f);
            }
        }

        void TickVajra(float dt)
        {
            List<HyegwangBrain> done = null;
            foreach (var pair in _vajra)
            {
                var v = pair.Value;
                v.T += dt;
                v.Flash = Mathf.Max(0f, v.Flash - dt * 4f);
                if (v.Boss == null || v.Boss.IsDead || v.T > v.Life) { (done ??= new List<HyegwangBrain>()).Add(pair.Key); continue; }
                Vector3 at = v.Boss.transform.position;
                // 금빛이 짙어져요: 옅은 금 → 짙은 호박색
                Color gold = Color.Lerp(new Color(1f, 0.92f, 0.6f), new Color(1f, 0.55f, 0.15f), v.Grow);
                if (v.Ring != null)
                {
                    v.Ring.transform.position = new Vector3(at.x, 0.04f, at.z);
                    v.Ring.transform.Rotate(0f, 0f, 40f * dt, Space.Self);
                    float breathe = 1f + 0.05f * Mathf.Sin(v.T * 8f) + 0.15f * v.Flash;
                    v.Ring.transform.localScale = v.RingUnit * v.Radius * 2.2f * breathe;
                    v.Ring.color = new Color(gold.r, gold.g, gold.b, Mathf.Clamp01(Mathf.Min(1f, v.T / 0.25f) * (0.75f + 0.25f * v.Grow) + v.Flash * 0.3f));
                }
                if (v.Crack != null) // 맞을수록 바닥이 금빛으로 갈라져요
                {
                    v.Crack.transform.position = new Vector3(at.x, 0.035f, at.z);
                    v.Crack.transform.localScale = v.CrackUnit * v.Radius * 1.4f;
                    v.Crack.color = new Color(gold.r, gold.g, gold.b, v.Grow * 0.9f);
                }
                v.Spawn -= dt;
                while (v.Spawn <= 0f) // 반짝이가 피어올라요 (짙어질수록 많이)
                {
                    v.Spawn += Mathf.Lerp(0.14f, 0.05f, v.Grow);
                    float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f), r = UnityEngine.Random.Range(0.4f, 1f);
                    Glint(at + new Vector3(Mathf.Cos(a) * r, UnityEngine.Random.Range(0.3f, 1.8f), Mathf.Sin(a) * r), Vector3.up * 0.8f, UnityEngine.Random.Range(0.2f, 0.35f), 0.9f, 0f);
                }
            }
            if (done != null) foreach (var b in done) EndVajra(b);
        }

        void EndVajra(HyegwangBrain boss)
        {
            if (!_vajra.TryGetValue(boss, out var v)) return;
            if (v.Ring != null) Destroy(v.Ring.gameObject);
            if (v.Crack != null) Destroy(v.Crack.gameObject);
            _vajra.Remove(boss);
        }

        void VajraBurst(HyegwangBrain boss, HyegwangBrain.FxCue c)
        {
            EndVajra(boss);
            float size = Mathf.Max(1f, c.Radius) * 2.2f;
            Color gold = Color.Lerp(new Color(1f, 0.9f, 0.55f), new Color(1f, 0.6f, 0.2f), c.Grow);
            Expand(_crack, c.At, size, 0.25f, 0.6f, gold, 0.05f);
            Expand(_burst, c.At, size * 0.9f, 0.2f, 0.5f, Color.white, 0.06f);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                var vel = new Vector3(Mathf.Cos(a), 0.4f, Mathf.Sin(a)) * c.Radius * 2.2f;
                Glint(c.At + Vector3.up * 0.5f, vel, 0.4f, 0.45f, 2f);
            }
        }

        // ── 축지: 먹 붓 잔상 + 먹 번짐

        void Rush(HyegwangBrain.FxCue c)
        {
            Vector3 dir = c.Dir; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f || c.Length < 0.3f) return;
            dir.Normalize();
            Vector3 a = c.At + Vector3.up * 1f, b = a + dir * c.Length;
            var lr = new GameObject("Rush Trail").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, a);
            lr.sharedMaterial = _lineMat;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.8f, 1.1f), new Keyframe(1f, 0.7f));
            lr.startColor = lr.endColor = new Color(0.07f, 0.06f, 0.06f, 0.9f);
            lr.sortingOrder = 6;
            float t = 0f, dash = Mathf.Max(0.05f, c.Time);
            _live.Add(dt =>
            {
                if (lr == null) return false;
                t += dt;
                float reach = Mathf.Clamp01(t / dash);                  // 다가가는 동안 앞이 늘어나요
                float fade = Mathf.Clamp01((t - dash) / 0.5f);           // 다가간 뒤 꼬리부터 걷혀요
                lr.SetPosition(0, Vector3.Lerp(a, b, fade * 0.9f));
                lr.SetPosition(1, Vector3.Lerp(a, b, reach));
                lr.startColor = lr.endColor = new Color(0.07f, 0.06f, 0.06f, 0.9f * (1f - fade));
                if (fade < 1f) return true;
                Destroy(lr.gameObject);
                return false;
            });
            int puffs = Mathf.Clamp(Mathf.RoundToInt(c.Length / 1.5f), 2, 6);
            for (int i = 0; i < puffs; i++) // 먹 번짐이 길에 남아요
            {
                float s = (i + 0.5f) / puffs;
                Expand(_splash, c.At + dir * c.Length * s + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, UnityEngine.Random.Range(-0.3f, 0.3f)),
                    UnityEngine.Random.Range(1f, 1.6f), 0.15f + s * dash, 0.7f, new Color(0.08f, 0.07f, 0.07f), 0.03f);
            }
        }

        // ── 도움

        // 바닥 그림이 delay 뒤 퍼졌다(grow 0.2초) life 동안 바래요
        void Expand(Sprite sprite, Vector3 at, float size, float delay, float life, Color color, float height)
        {
            var sr = Flat(sprite, at, 1f, UnityEngine.Random.Range(0f, 360f), height);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            sr.color = new Color(color.r, color.g, color.b, 0f);
            float t = -delay;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < 0f) return true;
                float g = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.2f), 3f);
                sr.transform.localScale = unit * Mathf.Lerp(size * 0.3f, size, g);
                sr.color = new Color(color.r, color.g, color.b, t < 0.15f ? color.a : Mathf.Clamp01(1f - (t - 0.15f) / life) * color.a);
                if (t < 0.15f + life) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        void Glint(Vector3 at, Vector3 velocity, float size, float life, float gravity)
        {
            if (_glint == null) return;
            var go = new GameObject("Glint");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.AddComponent<InkFaceCamera>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _glint;
            sr.sortingOrder = 7;
            float unit = 1f / Mathf.Max(0.01f, _glint.bounds.size.x), t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                velocity += Vector3.down * gravity * dt;
                go.transform.position += velocity * dt;
                float k = t / life;
                go.transform.localScale = Vector3.one * unit * size * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                if (k < 1f) return true;
                Destroy(go);
                return false;
            });
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
            sr.sortingOrder = 5;
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _glint = Resources.Load<Sprite>("PouchFx/Glint");
            _burst = Resources.Load<Sprite>("PouchFx/Burst");
            _crack = Resources.Load<Sprite>("SkillFx/QuakeRing");
            _splash = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
            _lineMat = new Material(Shader.Find("Sprites/Default"));
            var brush = Resources.Load<Texture2D>("HudArt/HpStroke");
            if (brush != null) _lineMat.mainTexture = brush;
        }
    }
}
