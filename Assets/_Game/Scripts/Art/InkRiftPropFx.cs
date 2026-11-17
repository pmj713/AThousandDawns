using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Prototype;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 차원 겹침 소품 (무너진 성벽 · 기사 석상 · 룬 기둥 · 마나 수정, RoomRun.IsRift) 둘레 이펙트 — 그림만, 모델은 메인 아트.
    //   소품마다 (가볍게, 방마다 1~4개): 밑에 보랏빛 균열이 은은하게 숨 쉬고, 몇 초에 한 번 틈새 빛이 깜박이며,
    //     연보랏빛 룬 불티가 드문드문 위로 올라요 (소품마다 파티클 하나, 아주 낮은 빈도).
    //   룬 기둥: 밑에 보라·청색 룬 원이 천천히 돌며 숨 쉬듯 밝아졌다 어두워져요.
    //   새 방에 들어서면 (RoomRun.LayoutChanged): 소품 자리마다 보랏빛 균열이 세로로 확 찢어졌다 닫히며 소품이 나타나요.
    //   방 구성이 바뀌어 없어진 소품 (깨진 마나 수정 · 방을 나감): 균열이 서서히 걷혀요.
    //   마나 수정 폭발 (RoomRun.CrystalBurst): 청보라 수정 조각이 사방으로 튀고 마력 고리 두 겹이 반경까지 퍼져요 (기둥은 InkBurstFx Rune).
    // 그림: EliteFx/Blink (균열), HeavenlyDemonFx/RuneCircle, PouchFx/Ring.
    public class InkRiftPropFx : MonoBehaviour
    {
        static readonly Color Violet = new Color(0.65f, 0.35f, 1f);

        sealed class Prop
        {
            public RoomRun.RoomFeature Feature;
            public SpriteRenderer Crack, Rune;
            public ParticleSystem Sparks;
            public float Seed, NextFlicker, Flicker, Fade = 1f;
            public bool Leaving;
        }

        static InkRiftPropFx _host;
        static Sprite _rift, _rune, _ring;
        static bool _loaded;

        readonly List<Prop> _props = new List<Prop>();
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            RoomRun.LayoutChanged -= OnLayout;
            RoomRun.LayoutChanged += OnLayout;
            RoomRun.CrystalBurst -= OnCrystal;
            RoomRun.CrystalBurst += OnCrystal;
            if (FindAnyObjectByType<EnemyDirector>() != null) Host();
            SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 싸우는 씬이 열리면 깨어나요
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (FindAnyObjectByType<EnemyDirector>() != null) Host(); }

        static InkRiftPropFx Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Rift Prop Fx").AddComponent<InkRiftPropFx>();
            return _host;
        }

        static void OnLayout(RoomRun run) { if (run != null) Host().Dress(run); }
        static void OnCrystal(RoomRun run, Vector3 at, float radius) => Host().Shatter(at, radius);

        void OnDestroy() { if (_host == this) _host = null; }

        // ── 방 구성: 새 소품엔 균열을 달고 (찢어지며 나타나요), 없어진 소품의 균열은 걷어요

        void Dress(RoomRun run)
        {
            var features = run.RoomFeatures;
            foreach (var p in _props)
                if (!p.Leaving && !features.Contains(p.Feature)) p.Leaving = true;
            foreach (var f in features)
            {
                if (!RoomRun.IsRift(f.Kind) || Has(f)) continue;
                var p = new Prop { Feature = f, Seed = UnityEngine.Random.Range(0f, 10f), NextFlicker = UnityEngine.Random.Range(1.5f, 4f), Fade = 0f };
                float size = Mathf.Max(0.8f, Mathf.Max(f.Size.x, f.Size.y));
                p.Crack = Flat(_rift, f.Center, size * 1.4f, UnityEngine.Random.Range(0f, 360f), 0.022f, 3);
                if (f.Kind == RoomRun.FeatureKind.RunePillar) p.Rune = Flat(_rune, f.Center, size * 2.2f, 0f, 0.024f, 4);
                var sparkRoot = new GameObject("Rift Sparks").transform;
                sparkRoot.SetParent(transform, false);
                sparkRoot.position = new Vector3(f.Center.x, 0.1f, f.Center.z);
                p.Sparks = InkParticles.Frost(sparkRoot, Vector3.zero, size * 0.5f, 0.2f);
                if (p.Sparks != null) { var main = p.Sparks.main; main.startColor = new Color(0.85f, 0.6f, 1f, 1f); }
                _props.Add(p);
                Tear(f.Center, size);
            }
        }

        bool Has(RoomRun.RoomFeature f)
        {
            foreach (var p in _props) if (p.Feature == f) return true;
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime, time = Time.time;
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                var p = _props[i];
                p.Fade = Mathf.MoveTowards(p.Fade, p.Leaving ? 0f : 1f, dt / (p.Leaving ? 0.6f : 0.3f));
                if (p.Leaving && p.Fade <= 0f) { Remove(p); _props.RemoveAt(i); continue; }
                p.NextFlicker -= dt;
                if (p.NextFlicker <= 0f) { p.NextFlicker = UnityEngine.Random.Range(2f, 5f); p.Flicker = 1f; } // 틈새 빛이 깜박
                p.Flicker = Mathf.MoveTowards(p.Flicker, 0f, dt / 0.25f);
                float breath = 0.35f + 0.12f * Mathf.Sin(time * 1.3f + p.Seed);
                if (p.Crack != null) p.Crack.color = new Color(Violet.r, Violet.g, Violet.b, (breath + 0.5f * p.Flicker) * p.Fade);
                if (p.Rune != null) // 룬 기둥: 룬 원이 천천히 돌며 숨 쉬어요
                {
                    p.Rune.transform.Rotate(0f, 0f, 12f * dt, Space.Self);
                    p.Rune.color = new Color(1f, 1f, 1f, (0.35f + 0.3f * (0.5f + 0.5f * Mathf.Sin(time * 1.6f + p.Seed))) * p.Fade);
                }
            }
            InkRoomReset.Tick(_live, dt); // 방 꾸밈이라 방을 옮겨도 지우지 않아요
        }

        static void Remove(Prop p)
        {
            if (p.Crack != null) Destroy(p.Crack.gameObject);
            if (p.Rune != null) Destroy(p.Rune.gameObject);
            if (p.Sparks != null) Destroy(p.Sparks.transform.parent.gameObject);
        }

        // 새 방: 소품 자리에 보랏빛 균열이 세로로 확 찢어졌다 닫혀요
        void Tear(Vector3 at, float size)
        {
            if (_rift == null) return;
            var pivot = new GameObject("Rift Tear").transform;
            pivot.SetParent(transform, false);
            pivot.position = new Vector3(at.x, 0f, at.z);
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            float k = Mathf.Max(1.2f, size * 1.1f) / Mathf.Max(0.01f, _rift.bounds.size.y);
            float h = _rift.bounds.size.y * k;
            go.transform.localPosition = Vector3.up * h * 0.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _rift;
            sr.sortingOrder = 9;
            float t = -UnityEngine.Random.Range(0f, 0.15f);
            bool burst = false;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < 0f) { sr.enabled = false; return true; }
                sr.enabled = true;
                if (!burst) { burst = true; InkParticles.Burst(at + Vector3.up * h * 0.5f, 10, new Color(0.9f, 0.75f, 1f, 1f), new Color(Violet.r, Violet.g, Violet.b, 0.4f), 2f, 0.12f, 0.45f, 0.2f); }
                float open = t < 0.12f ? t / 0.12f : t < 0.3f ? 1f : Mathf.Clamp01(1f - (t - 0.3f) / 0.3f);
                go.transform.localScale = new Vector3(k * open, k, k);
                if (t < 0.6f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 마나 수정 폭발 (플레이테스트 3차: 돌려쓰지 않고 판타지풍으로 새로): 마력이 한 점으로 빨려 들었다(0.12초)
        //   청록 · 보랏빛 마력 구가 확 터지며 → 3D 수정 조각(양끝이 뾰족한 결정)이 빙글 돌며 사방으로 튀어 바닥에 떨어져 녹아 사라지고,
        //   갈래 번개가 바깥으로 번쩍, 바닥엔 마법진이 돌며 퍼지고 마력 고리 두 겹, 청록 불빛(Light)이 번쩍, 마력 안개가 피어올라요.
        static Mesh _shard;
        static Material _shardMat, _arcMat;
        static Sprite _glowSprite;

        void Shatter(Vector3 at, float radius)
        {
            at.y = 0f;
            if (_shardMat == null)
            {
                _shardMat = new Material(Shader.Find("Sprites/Default")) { name = "Mana Shard" };
                _arcMat = new Material(Shader.Find("Sprites/Default")) { name = "Mana Arc" };
                var mist = Resources.Load<Texture2D>("FxArt/InkMist");
                if (mist != null) _glowSprite = Sprite.Create(mist, new Rect(0f, 0f, mist.width, mist.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (_shard == null) _shard = ShardMesh();
            Vector3 core = at + Vector3.up * 0.8f;
            InkParticles.Burst(core, 16, new Color(0.6f, 0.9f, 1f, 0.9f), new Color(0.6f, 0.4f, 1f, 0f), radius * 0.6f, 0.2f, 0.25f, 0f, false); // 모여드는 반짝임
            float t = 0f;
            _live.Add(dt =>
            {
                t += dt;
                if (t < 0.12f) return true;
                Explode(at, core, radius);
                return false;
            });
        }

        void Explode(Vector3 at, Vector3 core, float radius)
        {
            // 수정 조각
            const int count = 12;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Mana Shard");
                go.transform.SetParent(transform, false);
                go.transform.position = core;
                go.transform.rotation = UnityEngine.Random.rotation;
                float size = UnityEngine.Random.Range(0.25f, 0.55f);
                go.transform.localScale = Vector3.one * size;
                go.AddComponent<MeshFilter>().sharedMesh = _shard;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _shardMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 dir = Quaternion.Euler(0f, i * 360f / count + UnityEngine.Random.Range(-12f, 12f), 0f) * Vector3.forward;
                Vector3 v = dir * UnityEngine.Random.Range(radius * 1.6f, radius * 2.6f) + Vector3.up * UnityEngine.Random.Range(3f, 6f);
                Vector3 spin = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(360f, 900f);
                float life = UnityEngine.Random.Range(0.8f, 1.1f), st = 0f;
                bool landed = false;
                _live.Add(dt =>
                {
                    if (go == null) return false;
                    st += dt;
                    if (!landed)
                    {
                        v += Vector3.down * 16f * dt;
                        go.transform.position += v * dt;
                        go.transform.Rotate(spin * dt, Space.World);
                        if (go.transform.position.y <= 0.1f)
                        {
                            landed = true;
                            go.transform.position = new Vector3(go.transform.position.x, 0.1f, go.transform.position.z);
                            InkParticles.Burst(go.transform.position, 3, new Color(0.7f, 1f, 1f, 1f), new Color(0.6f, 0.4f, 1f, 0f), 0.8f, 0.1f, 0.3f, 0.5f);
                        }
                    }
                    float melt = Mathf.Clamp01((st - life * 0.6f) / (life * 0.4f)); // 바닥에서 녹아 사라져요
                    go.transform.localScale = Vector3.one * size * (1f - melt);
                    if (st < life) return true;
                    Destroy(go);
                    return false;
                });
            }
            // 마력 구 번쩍
            if (_glowSprite != null)
            {
                var pivot = new GameObject("Mana Flash").transform;
                pivot.SetParent(transform, false);
                pivot.position = core;
                pivot.gameObject.AddComponent<InkFaceCamera>();
                var outer = GlowChild(pivot, new Color(0.55f, 0.45f, 1f, 0.8f), 9);
                var inner = GlowChild(pivot, new Color(0.85f, 1f, 1f, 1f), 10);
                float unit = 1f / Mathf.Max(0.01f, _glowSprite.bounds.size.x), ft = 0f;
                _live.Add(dt =>
                {
                    if (pivot == null) return false;
                    ft += dt;
                    float k = Mathf.Clamp01(ft / 0.35f);
                    outer.transform.localScale = Vector3.one * unit * radius * Mathf.Lerp(1f, 3.2f, Mathf.Sqrt(k));
                    inner.transform.localScale = Vector3.one * unit * radius * Mathf.Lerp(0.8f, 1.6f, k);
                    outer.color = new Color(0.55f, 0.45f, 1f, 0.8f * (1f - k));
                    inner.color = new Color(0.85f, 1f, 1f, 1f - k * k);
                    if (k < 1f) return true;
                    Destroy(pivot.gameObject);
                    return false;
                });
            }
            // 청록 불빛
            var flash = new GameObject("Mana Light").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.transform.position = core;
            flash.type = LightType.Point;
            flash.color = new Color(0.5f, 0.85f, 1f);
            flash.range = radius * 5f;
            flash.shadows = LightShadows.None;
            float lt = 0f;
            _live.Add(dt =>
            {
                if (flash == null) return false;
                lt += dt;
                flash.intensity = 7f * Mathf.Clamp01(1f - lt / 0.45f);
                if (lt < 0.45f) return true;
                Destroy(flash.gameObject);
                return false;
            });
            // 갈래 번개
            for (int i = 0; i < 6; i++)
                Arc(core, Quaternion.Euler(UnityEngine.Random.Range(-25f, 10f), i * 60f + UnityEngine.Random.Range(-15f, 15f), 0f) * Vector3.forward, radius * UnityEngine.Random.Range(1.2f, 2f));
            // 바닥 마법진 · 고리 · 안개 · 불티
            RuneFlash(at, radius * 2.6f);
            Ring(at, radius * 2f, 0.45f, new Color(0.45f, 0.85f, 1f, 1f), 0f);
            Ring(at, radius * 2.8f, 0.6f, new Color(0.7f, 0.4f, 1f, 0.9f), 0.08f);
            InkParticles.Burst(at + Vector3.up * 0.3f, 18, new Color(0.6f, 0.75f, 1f, 0.7f), new Color(0.45f, 0.3f, 0.9f, 0f), radius * 0.9f, 0.6f, 1.2f, -0.4f);
            InkParticles.Burst(core, 24, new Color(0.9f, 1f, 1f, 1f), new Color(0.5f, 0.6f, 1f, 0f), radius * 3f, 0.08f, 0.45f, 0.6f);
        }

        // 번개 한 갈래: 꺾인 선이 0.18초 동안 두 번 깜빡이며 사라져요
        void Arc(Vector3 from, Vector3 dir, float length)
        {
            var go = new GameObject("Mana Arc");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _arcMat;
            lr.positionCount = 6;
            lr.widthMultiplier = 0.08f;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f));
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            side = side.sqrMagnitude < 0.01f ? Vector3.right : side.normalized;
            for (int p = 0; p < 6; p++)
                lr.SetPosition(p, from + dir * (length * p / 5f) + (p == 0 ? Vector3.zero : side * UnityEngine.Random.Range(-0.3f, 0.3f) + Vector3.up * UnityEngine.Random.Range(-0.2f, 0.2f)));
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                float on = (t < 0.06f || (t > 0.1f && t < 0.15f)) ? 1f : 0.25f;
                lr.startColor = new Color(0.9f, 1f, 1f, on);
                lr.endColor = new Color(0.55f, 0.5f, 1f, on * 0.6f);
                if (t < 0.18f) return true;
                Destroy(go);
                return false;
            });
        }

        // 바닥 마법진: 펼쳐지며 돌다가 옅어져요
        void RuneFlash(Vector3 at, float size)
        {
            var sr = Flat(_rune, at, size, UnityEngine.Random.Range(0f, 360f), 0.035f, 4);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / 0.8f;
                sr.transform.localScale = unit * Mathf.Lerp(0.5f, 1.1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.transform.Rotate(0f, 0f, 120f * dt, Space.Self);
                sr.color = new Color(0.65f, 0.75f, 1f, 0.9f * (1f - k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        static SpriteRenderer GlowChild(Transform parent, Color color, int order)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _glowSprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // 수정 조각: 양끝이 뾰족한 육각 결정 (가운데 흰빛 → 끝 청록 · 보라)
        static Mesh ShardMesh()
        {
            var b = new MeshBits();
            Color mid = new Color(0.9f, 1f, 1f, 0.95f), tip = new Color(0.45f, 0.75f, 1f, 0.85f), tip2 = new Color(0.65f, 0.4f, 1f, 0.85f);
            b.Cone(0f, 0.6f, 0.18f, 0f, 6, mid, tip);
            b.Cone(0f, -0.35f, 0.18f, 0f, 6, mid, tip2);
            return b.Build("Mana Shard");
        }

        void Ring(Vector3 at, float size, float life, Color color, float delay)
        {
            var sr = Flat(_ring, at, size, UnityEngine.Random.Range(0f, 360f), 0.03f, 5);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = -delay;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                if (t < 0f) return true;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.transform.Rotate(0f, 0f, 200f * dt, Space.Self);
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float yaw, float height, int order)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _rift = Resources.Load<Sprite>("EliteFx/Blink");
            _rune = Resources.Load<Sprite>("HeavenlyDemonFx/RuneCircle");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
        }
    }
}
