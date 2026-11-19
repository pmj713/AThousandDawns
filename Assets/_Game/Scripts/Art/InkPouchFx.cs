using System;
using System.Collections.Generic;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 기물 주머니 반짝임 (금빛 먹). 게임이 놓은 주머니(RoomRun.PouchObject)를 따라다니며 그림만 그려요.
    //   놓여 있는 동안: 바닥에 금빛 먹 번짐이 천천히 돌며 숨 쉬고, 둘레로 금빛 반짝이가 피어올라 사라져요
    //   (플레이테스트 10차: 열 때 터지던 금빛 먹 · 반짝이는 뺐어요 — 고르는 동안 시간이 멈춰 있다가 고른 뒤에 터져 보였어요)
    // 그림: Art/Env/PouchFx/Resources/PouchFx (Glint · Ring · Burst). 씬에 미리 둘 것 없이 게임이 켜지면 저절로 들어요.
    public class InkPouchFx : MonoBehaviour
    {
        static Sprite _glint, _ring, _burst;
        static bool _loaded;

        RoomRun _run;
        SpriteRenderer _halo;
        float _spawn;
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();

        static InkPouchFx Host()
        {
            var host = FindAnyObjectByType<InkPouchFx>();
            if (host == null) host = new GameObject("Ink Pouch Fx").AddComponent<InkPouchFx>();
            return host;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (FindAnyObjectByType<RoomRun>() != null) Host(); // 판 구조 씬에만
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; // 시작 화면부터 켜도 판 씬이 열리면 깨어나요
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) { if (FindAnyObjectByType<RoomRun>() != null) Host(); }

        void Update()
        {
            Load();
            float dt = Time.deltaTime;
            if (_run == null) _run = FindAnyObjectByType<RoomRun>();
            var pouch = _run != null ? _run.PouchObject : null;
            bool shown = pouch != null && pouch.activeInHierarchy;

            // 바닥 금빛 번짐 (돌며 숨 쉬어요)
            if (shown && _halo == null) _halo = Flat(_ring, Vector3.zero, 2.4f, 0f, 0.03f);
            if (_halo != null)
            {
                _halo.gameObject.SetActive(shown);
                if (shown)
                {
                    var p = pouch.transform.position;
                    _halo.transform.position = new Vector3(p.x, 0.03f, p.z);
                    _halo.transform.Rotate(0f, 0f, 20f * dt, Space.Self);
                    float breathe = 0.6f + 0.2f * Mathf.Sin(Time.time * 2.5f);
                    _halo.color = new Color(1f, 1f, 1f, breathe);
                }
            }

            // 반짝이 가루가 피어올라요
            if (shown)
            {
                _spawn -= dt;
                while (_spawn <= 0f)
                {
                    _spawn += 0.09f;
                    float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f), r = UnityEngine.Random.Range(0.25f, 0.85f);
                    var from = pouch.transform.position + new Vector3(Mathf.Cos(a) * r, UnityEngine.Random.Range(-0.3f, 0.3f), Mathf.Sin(a) * r);
                    Glint(from, Vector3.up * UnityEngine.Random.Range(0.5f, 0.9f), UnityEngine.Random.Range(0.18f, 0.38f), UnityEngine.Random.Range(0.8f, 1.3f));
                }
            }

            TickForge(pouch, shown);
            InkRoomReset.Tick(_live, dt, transform); // 방을 옮기면 제 밑의 연출을 지워요
        }

        // ── 대장간 보상 화로 불 (대나무 줄기 룩): 보상 소품 "Reward Forge (대장간 화로)"가 켜져 있으면 화로 위에
        //   차분한 주홍 붓 불꽃 · 먹빛 그을음 · 드문드문 불티 · 은은한 숯 빛(부드러운 빛 + 따뜻한 불빛)을 피워요. 멀리서도 화로가 살아 있게.
        //   주머니가 바뀔 때 한 번만 자식 이름을 보고, 화로가 처음 켜진 프레임에 그 모델 크기에 맞춰 불을 달아요 (모델의 자식이라 같이 켜지고 꺼져요).
        GameObject _forgePouch, _forgeFire;
        Transform _forgeView;
        static Sprite _forgeGlow;

        void TickForge(GameObject pouch, bool shown)
        {
            if (pouch != _forgePouch)
            {
                _forgePouch = pouch;
                _forgeView = null;
                if (_forgeFire != null) Destroy(_forgeFire);
                _forgeFire = null;
                if (pouch != null)
                    foreach (var t in pouch.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("Reward Forge")) { _forgeView = t; break; }
            }
            if (_forgeView == null || !shown || _forgeFire != null || !_forgeView.gameObject.activeInHierarchy) return;
            var renderers = _forgeView.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var root = new GameObject("Forge Fire").transform;
            root.SetParent(_forgeView, true);
            root.position = new Vector3(b.center.x, b.max.y - b.size.y * 0.12f, b.center.z);
            float w = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.35f, 0.15f, 0.5f);
            var fire = InkParticles.Fire(root, Vector3.zero, 0.45f, 1.1f);
            var fm = fire.main; fm.startColor = new Color(0.9f, 0.45f, 0.24f, 0.9f); // 원색 주황 대신 차분한 주홍
            InkParticles.EmitBox(fire, Vector3.zero, new Vector3(w, 0.05f, w), 1.1f);
            var soot = InkParticles.Smoke(root, Vector3.up * 0.4f, w, 0.6f, 1f); // 먹빛 그을음
            var embers = InkParticles.Embers(root, Vector3.zero, w, 0.5f);
            var em = embers.main; em.startColor = new Color(0.95f, 0.6f, 0.35f, 0.9f);
            if (_forgeGlow == null)
            {
                var tex = Resources.Load<Texture2D>("FxArt/InkMist");
                if (tex != null) _forgeGlow = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (_forgeGlow != null) // 숯 빛: 멀리서도 보이는 부드러운 주홍 빛무리 (숨 쉬듯)
            {
                var pivot = new GameObject("Coal Glow").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = Vector3.up * 0.1f;
                pivot.gameObject.AddComponent<InkFaceCamera>();
                var go = new GameObject("Glow");
                go.transform.SetParent(pivot, false);
                go.transform.localScale = Vector3.one * (1.5f / Mathf.Max(0.01f, _forgeGlow.bounds.size.x));
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _forgeGlow;
                sr.color = new Color(0.95f, 0.45f, 0.22f, 0.35f);
                sr.sortingOrder = 5;
                go.AddComponent<InkFlicker>();
            }
            var light = new GameObject("Coal Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = Vector3.up * 0.3f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.3f);
            light.range = 4f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;
            _forgeFire = root.gameObject;
        }

        // 반짝이 한 알: 커졌다 작아지며 날아가 사라져요 (카메라를 봐요)
        void Glint(Vector3 at, Vector3 velocity, float size, float life, float gravity = 0f)
        {
            if (_glint == null) return;
            var go = new GameObject("Glint");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.AddComponent<InkFaceCamera>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _glint;
            sr.sortingOrder = 7;
            float unit = 1f / Mathf.Max(0.01f, _glint.bounds.size.x);
            float t = 0f;
            _live.Add(dt =>
            {
                if (sr == null) return false;
                t += dt;
                velocity += Vector3.down * gravity * dt;
                go.transform.position += velocity * dt;
                float k = t / life;
                go.transform.localScale = Vector3.one * unit * size * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI); // 피었다 져요
                sr.color = Color.white; // 그림 자체가 금빛
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
            sr.sortingOrder = 4;
            return sr;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _glint = Resources.Load<Sprite>("PouchFx/Glint");
            _ring = Resources.Load<Sprite>("PouchFx/Ring");
            _burst = Resources.Load<Sprite>("PouchFx/Burst");
        }
    }
}
