using System;
using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 판타지 고수 결투장 "무너진 성 안뜰" (씬 루트 Room_FantasyMaster) 분위기 — 가볍게, 카메라가 안뜰 가까이 있을 때만 움직여요.
    //   공중: 연보랏빛 균열 티끌이 안뜰 위로 느리게 떠올라요 (파티클 하나, 낮은 빈도).
    //   바닥: 1.5~3초마다 메인 아트 바닥 균열("Crack") 자리(없으면 아무 자리)에서 보랏빛이 확 솟았다 스러져요.
    //   깃발: Room_FantasyMaster 아래 이름이 "Banner"로 시작하는 것 ("Banner 1"~4, 묶음 "… Banners"는 빼요, 메인 아트, 피벗이 위쪽 가장자리)을 바람에 살랑이게 해요.
    //     메인 아트 꾸미기가 늦게 붙을 수 있어서 처음 20초 동안 2초마다 새 깃발을 찾아요 (이름은 오브젝트마다 한 번만).
    // 그림: EliteFx/Blink (균열).
    public class InkCourtyardFx : MonoBehaviour
    {
        static readonly Color Violet = new Color(0.65f, 0.35f, 1f);
        const float Wake = 14f; // 안뜰 가장자리에서 이만큼 안으로 카메라가 들어오면 움직여요

        static InkCourtyardFx _host;
        static Sprite _rift;
        static bool _loaded;

        readonly HashSet<GameObject> _roots = new HashSet<GameObject>();
        readonly HashSet<Transform> _seen = new HashSet<Transform>();
        readonly List<Transform> _cracks = new List<Transform>(); // 메인 아트 바닥 균열 ("Crack")
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        Transform _room;
        Bounds _area;
        ParticleSystem _motes;
        float _nextCrack, _scan, _scanUntil;
        bool _awake;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (_host == null)
            {
                _host = new GameObject("Ink Courtyard Fx").AddComponent<InkCourtyardFx>();
                DontDestroyOnLoad(_host.gameObject);
            }
        }

        void Update()
        {
            if (_room == null)
            {
                if (_motes != null) { Destroy(_motes.transform.parent.gameObject); _motes = null; _awake = false; } // 씬이 바뀌었어요
                FindRoom();
                return;
            }
            var cam = Camera.main;
            bool near = cam != null && _area.SqrDistance(new Vector3(cam.transform.position.x, _area.center.y, cam.transform.position.z)) < Wake * Wake;
            if (near != _awake) { _awake = near; if (_motes != null) _motes.gameObject.SetActive(near); }
            if (Time.time < _scanUntil && (_scan -= Time.deltaTime) <= 0f) { _scan = 2f; FindBanners(); }
            if (!_awake) return;
            float dt = Time.deltaTime;
            if ((_nextCrack -= dt) <= 0f) { _nextCrack = UnityEngine.Random.Range(1.5f, 3f); Crack(); }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void FindRoom()
        {
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 봐요
            {
                if (root.name != "Room_FantasyMaster") continue;
                _room = root.transform;
                Load();
                var floor = _room.Find("Floor");
                _area = floor != null && floor.GetComponent<Renderer>() != null ? floor.GetComponent<Renderer>().bounds : new Bounds(new Vector3(300f, 0f, -80f), new Vector3(20f, 0.2f, 14f));
                var motes = new GameObject("Courtyard Motes").transform;
                motes.SetParent(transform, false);
                motes.position = new Vector3(_area.center.x, 0.2f, _area.center.z);
                _motes = InkParticles.Frost(motes, Vector3.zero, 1f, 0.6f);
                if (_motes != null)
                {
                    var main = _motes.main;
                    main.startColor = new Color(0.8f, 0.6f, 1f, 1f);
                    InkParticles.EmitBox(_motes, Vector3.zero, new Vector3(_area.size.x, 0.2f, _area.size.z), 0.6f);
                }
                motes.gameObject.SetActive(false);
                _seen.Clear();
                _cracks.Clear();
                _scan = 0f;
                _scanUntil = Time.time + 20f;
                return;
            }
        }

        // 깃발: 이름에 "Banner"가 든 새 오브젝트에 바람 흔들림을 달아요
        void FindBanners()
        {
            foreach (var t in _room.GetComponentsInChildren<Transform>(true))
            {
                if (!_seen.Add(t)) continue;
                string n = t.name;
                if (n == "Crack") _cracks.Add(t); // 바닥 균열 자리
                if (n.StartsWith("Banner", StringComparison.OrdinalIgnoreCase) && !n.StartsWith("Banners", StringComparison.OrdinalIgnoreCase) && t.GetComponent<InkBannerSway>() == null) // "Banner 1" … (묶음 "… Banners"는 빼요)
                    t.gameObject.AddComponent<InkBannerSway>();
            }
        }

        // 바닥 균열에서 보랏빛이 솟아요
        void Crack()
        {
            if (_rift == null) return;
            _cracks.RemoveAll(c => c == null);
            var p = _cracks.Count > 0 && UnityEngine.Random.value < 0.7f // 메인 아트 바닥 균열이 있으면 대개 그 자리에서
                ? _cracks[UnityEngine.Random.Range(0, _cracks.Count)].position + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0f, UnityEngine.Random.Range(-0.4f, 0.4f))
                : new Vector3(UnityEngine.Random.Range(_area.min.x + 1f, _area.max.x - 1f), 0f, UnityEngine.Random.Range(_area.min.z + 1f, _area.max.z - 1f));
            p.y = 0.03f;
            var go = new GameObject("Courtyard Crack");
            go.transform.SetParent(transform, false);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * (UnityEngine.Random.Range(0.9f, 1.6f) / Mathf.Max(0.01f, _rift.bounds.size.y));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _rift;
            sr.sortingOrder = 3;
            sr.color = new Color(1f, 1f, 1f, 0f);
            InkParticles.Burst(p + Vector3.up * 0.2f, 6, new Color(0.9f, 0.75f, 1f, 0.9f), new Color(Violet.r, Violet.g, Violet.b, 0f), 0.4f, 0.25f, 1.1f, -1.5f);
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                float a = t < 0.2f ? t / 0.2f : t < 0.5f ? 1f : Mathf.Clamp01(1f - (t - 0.5f) / 0.8f);
                sr.color = new Color(1f, 1f, 1f, 0.85f * a);
                if (t < 1.3f) return true;
                Destroy(go);
                return false;
            });
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _rift = Resources.Load<Sprite>("EliteFx/Blink");
        }
    }

    // 깃발 살랑임: 피벗(위쪽 가장자리)을 축으로 앞뒤 · 좌우로 바람 따라 흔들려요
    public class InkBannerSway : MonoBehaviour
    {
        Quaternion _base;
        float _seed;

        void Start() { _base = transform.localRotation; _seed = UnityEngine.Random.Range(0f, 10f); }

        void Update()
        {
            float t = Time.time + _seed;
            float x = 6f * Mathf.Sin(t * 1.7f) + 2.5f * Mathf.Sin(t * 4.3f);
            float z = 3f * Mathf.Sin(t * 1.1f + 1.3f);
            transform.localRotation = _base * Quaternion.Euler(x, 0f, z);
        }
    }
}
