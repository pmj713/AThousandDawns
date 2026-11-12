using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 천마총 싸우는 방 구성의 이펙트 (모델은 메인 아트) — 그림만, 회복·피해는 게임 코드 그대로.
    //   혈지 (RoomFeatures, Kind = BloodPool): 기본 판은 숨기고 검붉은 핏물 웅덩이를 깔아요. 천천히 숨쉬듯 일렁이고 가끔 핏빛 물결 고리가 번져요.
    //   혈지 회복 (RoomRun.BloodPoolHealed): 그 적 발밑 핏물에서 핏방울·핏빛 기운이 몸으로 빨려 오르고,
    //       혈지 위에 서 있는 동안은 몸 둘레에 옅은 핏빛 기운이 감돌아요.
    //   마기 기둥 분출 (RoomRun.MiasmaPulse 자리·반경·예고): 예고 동안 기둥 밑동에서 붉은 기운이 타오르며 짙어지다가,
    //       예고가 끝나면 검은 마기 고리가 반경까지 퍼지고 마기 연기가 피어올라요.
    // 그림: Resources/TombRoomFx (BloodPool · MiasmaRing), RiverFx/Ripple, BloodBuddhaFx/Drop, TombFx/BloodFlame, HeavenlyDemonFx/Wisp.
    public class InkTombRoom : MonoBehaviour
    {
        static InkTombRoom _host;
        static Sprite _pool, _ring, _ripple, _drop, _flame, _wisp;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        const int MaxLive = 400; // 동시에 움직이는 그림 상한
        readonly List<Rect> _pools = new List<Rect>(); // 혈지 자리 (x·z)
        Transform _room; // 방이 바뀌면 통째로 지워요

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            RoomRun.LayoutChanged -= OnLayout;
            RoomRun.LayoutChanged += OnLayout;
            RoomRun.BloodPoolHealed -= OnHealed;
            RoomRun.BloodPoolHealed += OnHealed;
            RoomRun.MiasmaPulse -= OnPulse;
            RoomRun.MiasmaPulse += OnPulse;
        }

        static InkTombRoom Host()
        {
            Load();
            if (_host == null) _host = new GameObject("Ink Tomb Room").AddComponent<InkTombRoom>();
            return _host;
        }

        static void OnLayout(RoomRun run) => Host().Build(run);
        static void OnHealed(EnemyBrain enemy) { if (enemy != null) Host().Healed(enemy); }
        static void OnPulse(Vector3 at, float radius, float warn) => Host().Pulse(at, radius, warn);

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 혈지 표면
        void Build(RoomRun run)
        {
            if (_room != null) Destroy(_room.gameObject);
            _room = new GameObject("Room").transform;
            _room.SetParent(transform, false);
            _pools.Clear();
            if (run == null) return;
            foreach (var f in run.RoomFeatures)
            {
                if (f.Kind != RoomRun.FeatureKind.BloodPool) continue;
                var plate = f.Go != null ? f.Go.GetComponent<Renderer>() : null; // 기본 판만 (자식에 붙은 가장자리 모델은 그대로)
                if (plate != null) plate.enabled = false;
                _pools.Add(new Rect(f.Center.x - f.Size.x * 0.5f, f.Center.z - f.Size.y * 0.5f, f.Size.x, f.Size.y));
                Pool(f.Center, f.Size);
            }
        }

        void Pool(Vector3 center, Vector2 size)
        {
            var sr = Sheet(_pool, center, new Vector2(size.x * 0.9f, size.y * 0.86f), 0.021f, 2, _room); // 메인 아트 가장자리 돌(Feature_BloodPoolRim) 안쪽에
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float timer = 0.5f, seed = UnityEngine.Random.Range(0f, 10f);
            var room = _room;
            _live.Add(dt =>
            {
                if (sr == null || room == null) return false;
                float t = Time.time + seed;
                sr.transform.localScale = new Vector3(unit.x * (1f + 0.025f * Mathf.Sin(t * 1.3f)), unit.y * (1f + 0.025f * Mathf.Sin(t * 1.1f + 1f)), 1f);
                sr.color = new Color(1f, 1f, 1f, 0.9f + 0.08f * Mathf.Sin(t * 0.9f));
                timer -= dt;
                if (timer > 0f) return true;
                timer = UnityEngine.Random.Range(1f, 2f);
                var at = center + new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f) * size.x, 0f, UnityEngine.Random.Range(-0.3f, 0.3f) * size.y);
                Ring(_ripple, at, UnityEngine.Random.Range(0.7f, 1.2f), 1.4f, new Color(0.55f, 0.06f, 0.06f, 0.6f), 0.024f, room);
                return true;
            });
        }

        bool OnPool(Vector3 p)
        {
            foreach (var r in _pools) if (r.Contains(new Vector2(p.x, p.z))) return true;
            return false;
        }

        // ── 혈지 회복: 핏방울과 핏빛 기운이 몸으로 빨려 올라요
        void Healed(EnemyBrain enemy)
        {
            var body = enemy.transform;
            float height = Mathf.Max(1f, enemy.Height);
            for (int i = 0; i < 10; i++) // 핏방울: 발밑 핏물에서 가슴으로
            {
                var sr = Upright(_drop, body.position, 0.14f, 7, false, _room);
                if (sr == null) break;
                var pivot = sr.transform.parent;
                Vector2 r = UnityEngine.Random.insideUnitCircle * 0.9f;
                Vector3 offset = new Vector3(r.x, 0.05f, r.y);
                float t = -i * 0.07f;
                _live.Add(dt =>
                {
                    if (sr == null) return false;
                    t += dt;
                    if (t < 0f) { sr.enabled = false; return true; }
                    sr.enabled = true;
                    if (body == null) { Destroy(pivot.gameObject); return false; }
                    float k = Mathf.Clamp01(t / 0.55f);
                    Vector3 start = body.position + offset, end = body.position + Vector3.up * height * 0.55f;
                    pivot.position = Vector3.Lerp(start, end, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.3f;
                    sr.color = new Color(1f, 1f, 1f, 1f - k * 0.5f);
                    if (k < 1f) return true;
                    Destroy(pivot.gameObject);
                    return false;
                });
            }
            Ring(_ripple, body.position, 1.6f, 0.7f, new Color(0.7f, 0.08f, 0.07f, 0.8f), 0.027f, _room);
            // 혈지 위에 있는 동안 몸 둘레로 핏빛 기운이 감돌아요
            float spawn = 0f;
            _live.Add(dt =>
            {
                if (body == null || enemy.IsDead || !OnPool(body.position)) return false;
                spawn -= dt;
                if (spawn > 0f) return true;
                spawn = 0.18f;
                Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.3f, 0.6f);
                Rise(_flame, body.position + new Vector3(r.x, 0f, r.y), 0.35f, height * 0.9f, 0.8f, new Color(1f, 0.55f, 0.5f, 0.6f));
                return true;
            });
        }

        // ── 마기 기둥 분출
        void Pulse(Vector3 at, float radius, float warn)
        {
            at.y = 0f;
            float t = 0f, spawn = 0f, total = Mathf.Max(0.1f, warn);
            bool burst = false;
            var room = _room;
            _live.Add(dt =>
            {
                if (room == null) return false;
                t += dt;
                if (t < total) // 예고: 기둥 밑동에서 붉은 기운이 점점 짙게 타올라요
                {
                    spawn -= dt;
                    if (spawn <= 0f)
                    {
                        float k = t / total;
                        spawn = Mathf.Lerp(0.12f, 0.04f, k);
                        Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.4f, 0.8f);
                        Rise(_flame, at + new Vector3(r.x, 0f, r.y), Mathf.Lerp(0.35f, 0.6f, k), Mathf.Lerp(1f, 2.4f, k), 0.6f, new Color(1f, 0.3f, 0.22f, Mathf.Lerp(0.4f, 0.95f, k)));
                    }
                    return true;
                }
                if (burst) return false;
                burst = true;
                Ring(_ring, at, radius * 2.1f, 0.55f, new Color(1f, 1f, 1f, 0.95f), 0.07f, room, spin: 140f, from: 0.25f);
                for (int i = 0; i < 14; i++) // 마기 연기가 고리 따라 피어올라요
                {
                    float a = i / 14f * Mathf.PI * 2f;
                    var p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * UnityEngine.Random.Range(0.6f, 1f);
                    Rise(_wisp, p, UnityEngine.Random.Range(0.5f, 0.8f), 1.2f, 0.9f, Color.white);
                }
                return false;
            });
        }

        // ── 도움

        void Rise(Sprite sprite, Vector3 at, float width, float lift, float life, Color color)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Upright(sprite, at, width, 6, true, _room);
            if (sr == null) return;
            var pivot = sr.transform.parent;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                pivot.position = at + Vector3.up * lift * k * 0.5f;
                sr.transform.localScale = new Vector3(unit.x * (1f - 0.4f * k), unit.y * (0.7f + 0.6f * k), unit.z);
                sr.color = new Color(color.r, color.g, color.b, color.a * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI));
                if (k < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        void Ring(Sprite sprite, Vector3 at, float size, float life, Color color, float height, Transform parent, float spin = 0f, float from = 0.4f)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Flat(sprite, at, size, UnityEngine.Random.Range(0f, 360f), height, 4, parent);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(from, 1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                if (spin != 0f) sr.transform.Rotate(0f, 0f, spin * dt, Space.Self);
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        SpriteRenderer Sheet(Sprite sprite, Vector3 at, Vector2 size, float height, int order, Transform parent)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var b = sprite.bounds.size;
            go.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, b.x), size.y / Mathf.Max(0.01f, b.y), 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float yaw, float height, int order, Transform parent)
        {
            if (sprite == null || parent == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer Upright(Sprite sprite, Vector3 at, float width, int order, bool bottom, Transform parent)
        {
            if (sprite == null || parent == null) return null;
            var pivot = new GameObject(sprite.name).transform;
            pivot.SetParent(parent, false);
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

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _pool = Resources.Load<Sprite>("TombRoomFx/BloodPool");
            _ring = Resources.Load<Sprite>("TombRoomFx/MiasmaRing");
            _ripple = Resources.Load<Sprite>("RiverFx/Ripple");
            _drop = Resources.Load<Sprite>("BloodBuddhaFx/Drop");
            _flame = Resources.Load<Sprite>("TombFx/BloodFlame");
            _wisp = Resources.Load<Sprite>("HeavenlyDemonFx/Wisp");
        }
    }
}
