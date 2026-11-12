using System;
using System.Collections.Generic;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 장강 수채 싸우는 방의 물 (RoomRun.RoomFeatures 중 Kind == Water) — 그림만, 느려짐·밀림은 게임 코드 그대로.
    //   흐르는 물 (여울 · 물길): 청회색 상자는 숨기고 옅은 쪽빛 먹 물판을 깔아요. 그 위로 먹 물결 획이 물살 방향으로
    //       흘러가며 나타났다 사라져요 (물살이 셀수록 빨리). 물가 돌·갈대는 메인 아트 모델.
    //   고인 물 (웅덩이): 둥근 먹 웅덩이 위로 잔잔한 물결 고리가 가끔 번져요.
    //   발밑 (RoomRun.PlayerInWater): 물에 들어가 있는 동안 발밑에 물결 고리가 번지고, 움직이면 물보라가 튀어요.
    //   죽림 개울 (RoomRun.Region == 0): 쪽빛 대신 맑은 연둣빛 물판 · 연둣빛 물결 획, 갈대 대신 댓잎이 빙글 돌며 떠내려가요.
    // 그림: Art/Env/RiverFx/Resources/RiverWater (WaterSheet · RippleStroke · Pond · StreamSheet · StreamStroke), TitleArt/BambooLeaf, RiverFx (Ripple · Splash).
    // 방이 바뀔 때 (RoomRun.LayoutChanged) 다시 깔아요.
    public class InkRiverWater : MonoBehaviour
    {
        static InkRiverWater _host;
        static Sprite _sheet, _stroke, _pond, _ring, _splash, _streamSheet, _streamStroke, _leaf;
        static bool _loaded;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        const int MaxLive = 400; // 동시에 움직이는 그림 상한
        RoomRun _run;
        PlayerController _player;
        float _footTimer;
        Vector3 _lastFoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            RoomRun.LayoutChanged -= OnLayout;
            RoomRun.LayoutChanged += OnLayout;
        }

        static void OnLayout(RoomRun run)
        {
            Load();
            if (_host == null) _host = new GameObject("Ink River Water").AddComponent<InkRiverWater>();
            _host.Build(run);
        }

        void OnDestroy() { if (_host == this) _host = null; }

        void Build(RoomRun run)
        {
            _run = run;
            foreach (Transform child in transform) Destroy(child.gameObject); // 물판 · 물결 · 고리 · 물보라 모두
            _live.Clear();
            if (run == null) return;
            foreach (var f in run.RoomFeatures)
            {
                if (f.Kind != RoomRun.FeatureKind.Water) continue;
                if (f.Go != null) foreach (var r in f.Go.GetComponentsInChildren<Renderer>()) r.enabled = false;
                if (f.Current.sqrMagnitude > 1e-4f) Flowing(f, run.Region == 0);
                else Still(f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
            TickFoot(dt);
        }

        // ── 흐르는 물: 물판 + 물살 방향으로 흐르는 먹 물결 획
        void Flowing(RoomRun.RoomFeature f, bool bamboo)
        {
            var body = Sheet(bamboo && _streamSheet != null ? _streamSheet : _sheet, f.Center, f.Size * 1.08f, 0.021f, 2); // 죽림 개울은 맑은 연둣빛
            if (body != null) body.color = new Color(1f, 1f, 1f, 0.92f);
            Vector3 dir = f.Current.normalized;
            float speed = f.Current.magnitude;
            Vector2 half = f.Size * 0.5f;
            int count = Mathf.Clamp(Mathf.RoundToInt(f.Size.x * f.Size.y / 2.2f), 6, 40);
            for (int i = 0; i < count; i++) Stroke(f.Center, half, dir, speed, UnityEngine.Random.value, bamboo && _streamStroke != null ? _streamStroke : _stroke);
            if (bamboo) { for (int i = 0; i < 4; i++) Leaf(f.Center, half, dir, speed, i / 4f); }
            // 물가 돌·갈대·말뚝은 메인 아트 물가 모델 (InkRoomFeatures)이 둘러요
        }

        void Stroke(Vector3 center, Vector2 half, Vector3 dir, float speed, float startPhase, Sprite sprite)
        {
            var sr = Flat(sprite, center, UnityEngine.Random.Range(0.9f, 1.6f), Yaw(dir), 0.024f, 3);
            if (sr == null) return;
            float life = 0f, total = 0f;
            Vector3 pos = Vector3.zero;
            void Respawn(float phase)
            {
                total = UnityEngine.Random.Range(1.4f, 2.4f);
                life = total * phase;
                // 물판 안 아무 자리 (가장자리는 조금 비워요)
                pos = center + new Vector3(UnityEngine.Random.Range(-half.x, half.x) * 0.92f, 0f, UnityEngine.Random.Range(-half.y, half.y) * 0.8f);
            }
            Respawn(startPhase);
            float drift = Mathf.Max(0.6f, speed * 1.3f); // 그림은 물살보다 조금 빨리 흘러 보이게
            _live.Add(dt =>
            {
                if (sr == null) return false;
                life += dt;
                if (life >= total) Respawn(0f);
                pos += dir * drift * dt;
                Vector3 local = pos - center;
                if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.z) > half.y) Respawn(0f); // 물판 끝에 닿으면 다른 자리에서
                sr.transform.position = new Vector3(pos.x, 0.024f, pos.z);
                float k = life / total;
                sr.color = new Color(1f, 1f, 1f, Mathf.Sin(k * Mathf.PI) * 0.85f);
                return true;
            });
        }

        // 죽림 개울: 댓잎이 물살 따라 빙글 돌며 떠내려가요
        void Leaf(Vector3 center, Vector2 half, Vector3 dir, float speed, float startPhase)
        {
            var sr = Flat(_leaf, center, UnityEngine.Random.Range(0.3f, 0.45f), UnityEngine.Random.Range(0f, 360f), 0.026f, 4);
            if (sr == null) return;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            float along = Mathf.Abs(Vector3.Dot(new Vector3(half.x, 0f, half.y), dir)), across = Mathf.Abs(Vector3.Dot(new Vector3(half.x, 0f, half.y), side));
            float s = Mathf.Lerp(-along, along, startPhase), lane = UnityEngine.Random.Range(-0.6f, 0.6f) * across, spin = UnityEngine.Random.Range(-40f, 40f), seed = UnityEngine.Random.Range(0f, 10f);
            float drift = Mathf.Max(0.5f, speed * 1.2f);
            _live.Add(dt =>
            {
                if (sr == null) return false;
                s += drift * dt;
                if (s > along) { s = -along; lane = UnityEngine.Random.Range(-0.6f, 0.6f) * across; }
                float wobble = Mathf.Sin(Time.time * 1.7f + seed) * 0.12f;
                var p = center + dir * s + side * (lane + wobble);
                sr.transform.position = new Vector3(p.x, 0.026f, p.z);
                sr.transform.Rotate(0f, 0f, spin * dt, Space.Self);
                float edge = Mathf.Clamp01((along - Mathf.Abs(s)) / 0.8f); // 끝에서 나타나고 사라져요
                sr.color = new Color(1f, 1f, 1f, edge);
                return true;
            });
        }

        // ── 고인 물: 둥근 웅덩이 + 가끔 번지는 물결 고리
        void Still(RoomRun.RoomFeature f)
        {
            var body = Sheet(_pond != null ? _pond : _sheet, f.Center, f.Size * 1.15f, 0.021f, 2);
            if (body != null) body.color = new Color(1f, 1f, 1f, 0.95f);
            Vector2 half = f.Size * 0.5f;
            float timer = UnityEngine.Random.Range(0f, 1.5f);
            var center = f.Center;
            _live.Add(dt =>
            {
                timer -= dt;
                if (timer > 0f) return true;
                timer = UnityEngine.Random.Range(1.2f, 2.4f);
                var at = center + new Vector3(UnityEngine.Random.Range(-half.x, half.x) * 0.6f, 0f, UnityEngine.Random.Range(-half.y, half.y) * 0.6f);
                Ring(at, UnityEngine.Random.Range(0.8f, 1.4f), 1.6f, 0.45f);
                return true;
            });
            // 웅덩이 둘레도 메인 아트 물가 모델이에요
        }

        // ── 발밑: 물결 고리 · 물보라
        void TickFoot(float dt)
        {
            if (_run == null) return;
            if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            if (_player == null) return;
            var p = _player.transform.position;
            if (!InWater(p)) return; // RoomRun.PlayerInWater와 같은 판정 (물판 안)
            float moved = (p - _lastFoot).magnitude;
            _lastFoot = p;
            _footTimer -= dt;
            if (_footTimer > 0f) return;
            bool moving = moved > dt * 1.5f;
            _footTimer = moving ? 0.22f : 0.6f;
            Ring(p, moving ? 0.9f : 1.2f, moving ? 0.5f : 0.9f, 0.7f);
            if (moving && _splash != null && UnityEngine.Random.value < 0.6f) Splash(p);
        }

        void Ring(Vector3 at, float size, float life, float alpha)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Flat(_ring, at, size, UnityEngine.Random.Range(0f, 360f), 0.027f, 4);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / life;
                sr.transform.localScale = unit * Mathf.Lerp(0.4f, 1.3f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(1f, 1f, 1f, alpha * (1f - k));
                if (k < 1f) return true;
                Destroy(sr.gameObject);
                return false;
            });
        }

        void Splash(Vector3 at)
        {
            if (_live.Count > MaxLive) return; // 한꺼번에 많이 터져도 이 이상은 그리지 않아요
            var sr = Upright(_splash, at + new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), 0f, UnityEngine.Random.Range(-0.2f, 0.2f)), 0.55f, 6, bottom: true);
            if (sr == null) return;
            Vector3 unit = sr.transform.localScale;
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                float k = t / 0.35f;
                sr.transform.localScale = unit * Mathf.Lerp(0.5f, 1.1f, Mathf.Clamp01(k));
                sr.color = new Color(1f, 1f, 1f, 0.85f * (1f - k));
                if (k < 1f) return true;
                Destroy(sr.transform.parent.gameObject);
                return false;
            });
        }

        bool InWater(Vector3 p)
        {
            foreach (var f in _run.RoomFeatures)
                if (f.Kind == RoomRun.FeatureKind.Water && Mathf.Abs(p.x - f.Center.x) <= f.Size.x * 0.5f && Mathf.Abs(p.z - f.Center.z) <= f.Size.y * 0.5f) return true;
            return false;
        }

        // ── 도움

        static float Yaw(Vector3 dir) => -Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;

        // 바닥에 누운 그림을 x·z 폭에 맞춰 늘려요
        SpriteRenderer Sheet(Sprite sprite, Vector3 at, Vector2 size, float height, int order)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var b = sprite.bounds.size;
            go.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, b.x), size.y / Mathf.Max(0.01f, b.y), 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, float yaw, float height, int order)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, height, at.z);
            go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

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

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _sheet = Resources.Load<Sprite>("RiverWater/WaterSheet");
            _stroke = Resources.Load<Sprite>("RiverWater/RippleStroke");
            _pond = Resources.Load<Sprite>("RiverWater/Pond");
            _streamSheet = Resources.Load<Sprite>("RiverWater/StreamSheet");
            _streamStroke = Resources.Load<Sprite>("RiverWater/StreamStroke");
            var leaf = Resources.Load<Texture2D>("TitleArt/BambooLeaf"); // 제목 화면의 날리는 댓잎
            if (leaf != null) _leaf = Sprite.Create(leaf, new Rect(0, 0, leaf.width, leaf.height), new Vector2(0.5f, 0.5f), leaf.width);
            _ring = Resources.Load<Sprite>("RiverFx/Ripple");
            _splash = Resources.Load<Sprite>("RiverFx/Splash");
        }
    }
}
