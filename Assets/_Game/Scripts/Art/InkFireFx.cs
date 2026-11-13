using System;
using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 바닥 불길 (플레이 테스트: 주황 원판이 밋밋해요) — 그림만, 화상·범위는 게임 코드 그대로.
    //   화염 장판 (FirePatch: 열양 각인 · 염화 호리병) · 열화 장판 (EnemyFirePatch, 열화 정예): 단색 원판은 숨기고
    //       그을린 먹 바닥에 불씨가 박힌 불길 판을 깔고, 그 위로 먹 테두리 불꽃 혀가 일렁이며 피었다 사그라들기를 되풀이해요.
    //   녹림왕 불타는 죽림 (BurningGrove): 불이 붙은 뒤 방 가장자리 불길 띠 위로 같은 불길 판과 불꽃 혀를 줄지어 깔아요.
    //       띠가 좁혀지면 다시 깔아요. (붙기 전 깜빡이는 예고는 그대로)
    // 바닥 판 높이 0.022는 공격 예고(0.03) 아래예요. 그림: Art/Env/SkillFx/Resources/FireFx (FireGround · FireTongue).
    public class InkFireFx : MonoBehaviour
    {
        const int MaxLive = 400;
        const float GroundHeight = 0.022f;
        static InkFireFx _host;
        static Sprite _ground, _tongue;

        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        readonly HashSet<Component> _dressed = new HashSet<Component>();
        float _scan, _groveScan;
        BurningGrove _grove;
        readonly Transform[] _strips = new Transform[4];
        readonly Vector3[] _stripKey = new Vector3[8];
        readonly List<GameObject>[] _stripFx = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        Renderer[] _stripRenderers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            _ground = Resources.Load<Sprite>("FireFx/FireGround");
            _tongue = Resources.Load<Sprite>("FireFx/FireTongue");
            if (_ground == null && _tongue == null) return;
            var go = new GameObject("Ink Fire Fx");
            DontDestroyOnLoad(go);
            _host = go.AddComponent<InkFireFx>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.12f; DressPatches(); }
            TickGrove(dt);
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 화염 · 열화 장판

        void DressPatches()
        {
            _dressed.RemoveWhere(c => c == null);
            foreach (var p in FindObjectsByType<FirePatch>()) if (_dressed.Add(p)) Patch(p);
            foreach (var p in FindObjectsByType<EnemyFirePatch>()) if (_dressed.Add(p)) Patch(p);
        }

        void Patch(Component patch)
        {
            var source = patch.GetComponent<Renderer>();
            if (source != null) source.enabled = false; // 단색 원판은 숨겨요
            var at = patch.transform.position;
            float radius = Mathf.Max(0.4f, patch.transform.localScale.x * 0.5f);
            var ground = Flat(_ground, at, radius * 2.3f);
            Vector3 unit = ground != null ? ground.transform.localScale : Vector3.one;
            int tongues = Mathf.Clamp(Mathf.RoundToInt(radius * 3f), 3, 7);
            float t = 0f, fadeOut = -1f, spawn = 0f, seed = UnityEngine.Random.Range(0f, 10f);
            _live.Add(dt =>
            {
                t += dt;
                bool alive = patch != null;
                if (!alive && fadeOut < 0f) fadeOut = 0f;
                float a = Mathf.Clamp01(t / 0.2f); // 확 번져요
                if (fadeOut >= 0f) { fadeOut += dt; a *= 1f - Mathf.Clamp01(fadeOut / 0.35f); }
                if (ground != null)
                {
                    ground.transform.Rotate(0f, 0f, 12f * dt, Space.Self);
                    ground.transform.localScale = unit * Mathf.Lerp(0.6f, 1f, Mathf.Sqrt(Mathf.Clamp01(t / 0.25f))) * (1f + 0.03f * Mathf.Sin(Time.time * 6f + seed));
                    ground.color = new Color(1f, 1f, 1f, 0.92f * a * (0.9f + 0.1f * Mathf.Sin(Time.time * 9f + seed)));
                }
                spawn -= dt;
                if (alive && spawn <= 0f) // 불꽃 혀: 둘레 아무 데서나 피었다 사그라들기를 되풀이
                {
                    spawn = 0.5f / tongues;
                    Vector2 r = UnityEngine.Random.insideUnitCircle * radius * 0.75f;
                    Tongue(at + new Vector3(r.x, 0f, r.y), UnityEngine.Random.Range(0.35f, 0.6f) * Mathf.Clamp(radius, 0.8f, 1.6f));
                }
                if (fadeOut < 0.35f) return true;
                if (ground != null) Destroy(ground.gameObject);
                return false;
            });
        }

        // 불꽃 혀 하나: 아래에서 솟아 흔들리다 위로 가늘어지며 사라져요
        void Tongue(Vector3 at, float width, Transform parent = null)
        {
            if (_tongue == null || _live.Count > MaxLive) return;
            var pivot = new GameObject("Fire Tongue").transform;
            pivot.SetParent(parent != null ? parent : transform, false);
            pivot.position = at;
            pivot.gameObject.AddComponent<InkFaceCamera>();
            var go = new GameObject("Sprite");
            go.transform.SetParent(pivot, false);
            float s = width / Mathf.Max(0.01f, _tongue.bounds.size.x);
            go.transform.localPosition = Vector3.up * _tongue.bounds.size.y * s * 0.5f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _tongue;
            sr.sortingOrder = 6;
            float t = 0f, life = UnityEngine.Random.Range(0.55f, 0.9f), seed = UnityEngine.Random.Range(0f, 10f);
            _live.Add(dt =>
            {
                if (pivot == null) return false;
                t += dt;
                float k = t / life;
                float grow = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f * 0.6f;
                float flick = 1f + 0.12f * Mathf.Sin(t * 28f + seed);
                go.transform.localScale = new Vector3(s / flick * Mathf.Lerp(1f, 0.6f, k), s * grow * flick, s);
                go.transform.localPosition = new Vector3(Mathf.Sin(t * 9f + seed) * 0.04f, _tongue.bounds.size.y * s * grow * flick * 0.5f, 0f);
                sr.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                if (k < 1f) return true;
                Destroy(pivot.gameObject);
                return false;
            });
        }

        // ── 녹림왕 불타는 죽림

        void TickGrove(float dt)
        {
            if (_grove == null)
            {
                _groveScan -= dt;
                if (_groveScan > 0f) return;
                _groveScan = 1f;
                _grove = FindAnyObjectByType<BurningGrove>();
                if (_grove == null) { ClearStrips(); return; }
                _stripRenderers = _grove.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < 4 && i < _grove.transform.childCount; i++) _strips[i] = _grove.transform.GetChild(i);
            }
            if (!_grove.IsBurning) return; // 붙기 전 깜빡이는 예고는 그대로
            foreach (var r in _stripRenderers) if (r != null && r.enabled) r.enabled = false; // 단색 띠는 숨겨요
            for (int i = 0; i < 4; i++)
            {
                var strip = _strips[i];
                if (strip == null) continue;
                Vector3 pos = strip.position, size = strip.localScale;
                if (_stripKey[i * 2] == pos && _stripKey[i * 2 + 1] == size && _stripFx[i].Count > 0) continue;
                _stripKey[i * 2] = pos;
                _stripKey[i * 2 + 1] = size;
                Strip(i, pos, size); // 띠가 좁혀지면 다시 깔아요
            }
        }

        void Strip(int index, Vector3 center, Vector3 size)
        {
            foreach (var go in _stripFx[index]) if (go != null) Destroy(go);
            _stripFx[index].Clear();
            if (size.x <= 0.05f || size.z <= 0.05f) return;
            var holder = new GameObject("Grove Fire " + index).transform;
            holder.SetParent(transform, false);
            _stripFx[index].Add(holder.gameObject);
            const float tile = 2.4f;
            int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / tile)), nz = Mathf.Max(1, Mathf.CeilToInt(size.z / tile));
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                var at = center + new Vector3((x + 0.5f) / nx * size.x - size.x * 0.5f, 0f, (z + 0.5f) / nz * size.z - size.z * 0.5f);
                var g = Flat(_ground, at, tile * 1.35f, holder);
                if (g != null) g.color = new Color(1f, 1f, 1f, 0.9f);
            }
            float area = size.x * size.z;
            int count = Mathf.Clamp(Mathf.RoundToInt(area / 2.2f), 4, 48);
            var h = holder;
            float spawn = 0f;
            _live.Add(dt =>
            {
                if (h == null || _grove == null) return false;
                spawn -= dt;
                if (spawn > 0f) return true;
                spawn = 0.7f / count; // 띠 위 아무 데서나 불꽃 혀가 피었다 사그라들기를 되풀이
                var at = center + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f) * size.x, 0f, UnityEngine.Random.Range(-0.5f, 0.5f) * size.z);
                Tongue(at, UnityEngine.Random.Range(0.5f, 0.85f), h);
                return true;
            });
        }

        void ClearStrips()
        {
            for (int i = 0; i < 4; i++)
            {
                foreach (var go in _stripFx[i]) if (go != null) Destroy(go);
                _stripFx[i].Clear();
                _strips[i] = null;
            }
            _stripRenderers = null;
        }

        SpriteRenderer Flat(Sprite sprite, Vector3 at, float width, Transform parent = null)
        {
            if (sprite == null) return null;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.position = new Vector3(at.x, GroundHeight, at.z);
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;
            return sr;
        }
    }
}
