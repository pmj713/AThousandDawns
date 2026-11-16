using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 판타지 방 분위기 — 가볍게, 카메라가 그 방 가까이 있을 때만 움직여요. 모델은 메인 아트.
    //   대상 (씬 루트 이름): Room_FantasyMaster "무너진 성 안뜰" (고수 결투장) · Room_FantasyBoss "무너진 왕좌의 방" (오우거 · 데스나이트).
    //   공중: 연보랏빛 균열 티끌이 방 위로 느리게 떠올라요 (방마다 파티클 하나, 낮은 빈도). 왕좌의 방은 먼지도 같이 떠다녀요.
    //   바닥: 1.5~3초마다 메인 아트 바닥 균열("Crack") 자리(없으면 아무 자리)에서 보랏빛이 확 솟았다 스러져요.
    //   깃발: 방 아래 이름이 "Banner"로 시작하는 것 (묶음 "… Banners"는 빼요, 피벗이 위쪽 가장자리)을 바람에 살랑이게 해요.
    //     메인 아트 꾸미기가 늦게 붙을 수 있어서 방을 찾은 뒤 20초 동안 2초마다 새 깃발 · 균열을 찾아요 (이름은 오브젝트마다 한 번만).
    //   왕좌의 방: 뚫린 지붕 틈에서 보랏빛 빛줄기 셋이 비스듬히 내려와 천천히 일렁여요.
    //     보스가 깨어나면 (EnemyBrain.Woke) 왕좌 뒤 균열 ("Throne"이 든 오브젝트 뒤쪽, 없으면 북쪽 벽 가운데)이 한 번 크게 번쩍여요.
    // 그림: EliteFx/Blink (균열).
    public class InkCourtyardFx : MonoBehaviour
    {
        static readonly Color Violet = new Color(0.65f, 0.35f, 1f);
        const float Wake = 14f; // 방 가장자리에서 이만큼 안으로 카메라가 들어오면 움직여요

        sealed class Site
        {
            public Transform Room, Throne;
            public Bounds Area;
            public ParticleSystem Motes, Dust;
            public bool Boss, Awake;
            public float NextCrack, Scan, ScanUntil;
            public readonly HashSet<Transform> Seen = new HashSet<Transform>();
            public readonly List<Transform> Cracks = new List<Transform>();
            public readonly List<LineRenderer> Shafts = new List<LineRenderer>();
            public readonly HashSet<EnemyBrain> Watched = new HashSet<EnemyBrain>();
        }

        static InkCourtyardFx _host;
        static Sprite _rift;
        static Material _lineMat;
        static bool _loaded;

        readonly HashSet<GameObject> _roots = new HashSet<GameObject>();
        readonly List<Site> _sites = new List<Site>();
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        EnemyDirector _director;

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
            FindRooms();
            float dt = Time.deltaTime;
            var cam = Camera.main;
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                var s = _sites[i];
                if (s.Room == null) { Drop(s); _sites.RemoveAt(i); continue; } // 씬이 바뀌었어요
                bool near = cam != null && s.Area.SqrDistance(new Vector3(cam.transform.position.x, s.Area.center.y, cam.transform.position.z)) < Wake * Wake;
                if (near != s.Awake) SetAwake(s, near);
                if (Time.time < s.ScanUntil && (s.Scan -= dt) <= 0f) { s.Scan = 2f; Scan(s); }
                if (!s.Awake) continue;
                if ((s.NextCrack -= dt) <= 0f) { s.NextCrack = UnityEngine.Random.Range(1.5f, 3f); Crack(s); }
                if (s.Boss) { TickShafts(s); WatchBoss(s); }
            }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        void FindRooms()
        {
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 봐요
            {
                string n = root.name;
                bool boss = n == "Room_FantasyBoss";
                if (!boss && n != "Room_FantasyMaster") continue;
                Load();
                var s = new Site { Room = root.transform, Boss = boss };
                var floor = s.Room.Find("Floor");
                var r = floor != null ? floor.GetComponent<Renderer>() : null;
                s.Area = r != null ? r.bounds : boss ? new Bounds(new Vector3(300f, 0f, -160f), new Vector3(24f, 0.2f, 18f)) : new Bounds(new Vector3(300f, 0f, -80f), new Vector3(20f, 0.2f, 14f));
                s.Motes = Particles(s, "Rift Motes", new Color(0.8f, 0.6f, 1f, 1f), 0.6f);
                if (boss)
                {
                    s.Dust = Particles(s, "Throne Dust", new Color(0.85f, 0.8f, 0.72f, 0.8f), 0.5f);
                    for (int i = 0; i < 3; i++) s.Shafts.Add(Shaft(s, i));
                }
                s.ScanUntil = Time.time + 20f;
                SetAwake(s, false);
                _sites.Add(s);
            }
        }

        ParticleSystem Particles(Site s, string name, Color color, float rate)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.position = new Vector3(s.Area.center.x, 0.2f, s.Area.center.z);
            var ps = InkParticles.Frost(root, Vector3.zero, 1f, rate);
            if (ps == null) return null;
            var main = ps.main;
            main.startColor = color;
            InkParticles.EmitBox(ps, Vector3.zero, new Vector3(s.Area.size.x, 0.2f, s.Area.size.z), 0.6f);
            return ps;
        }

        static void SetAwake(Site s, bool on)
        {
            s.Awake = on;
            if (s.Motes != null) s.Motes.transform.parent.gameObject.SetActive(on);
            if (s.Dust != null) s.Dust.transform.parent.gameObject.SetActive(on);
            foreach (var lr in s.Shafts) if (lr != null) lr.enabled = on;
        }

        static void Drop(Site s)
        {
            if (s.Motes != null) Destroy(s.Motes.transform.parent.gameObject);
            if (s.Dust != null) Destroy(s.Dust.transform.parent.gameObject);
            foreach (var lr in s.Shafts) if (lr != null) Destroy(lr.gameObject);
        }

        // 새 깃발 · 바닥 균열 · 왕좌를 찾아요
        static void Scan(Site s)
        {
            foreach (var t in s.Room.GetComponentsInChildren<Transform>(true))
            {
                if (!s.Seen.Add(t)) continue;
                string n = t.name;
                if (n == "Crack") s.Cracks.Add(t);
                if (s.Throne == null && n.IndexOf("Throne", StringComparison.OrdinalIgnoreCase) >= 0) s.Throne = t;
                if (n.StartsWith("Banner", StringComparison.OrdinalIgnoreCase) && !n.StartsWith("Banners", StringComparison.OrdinalIgnoreCase) && t.GetComponent<InkBannerSway>() == null) // "Banner 1" … (묶음 "… Banners"는 빼요)
                    t.gameObject.AddComponent<InkBannerSway>();
            }
        }

        // ── 바닥 균열에서 보랏빛이 솟아요

        void Crack(Site s)
        {
            s.Cracks.RemoveAll(c => c == null);
            var p = s.Cracks.Count > 0 && UnityEngine.Random.value < 0.7f // 메인 아트 바닥 균열이 있으면 대개 그 자리에서
                ? s.Cracks[UnityEngine.Random.Range(0, s.Cracks.Count)].position + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0f, UnityEngine.Random.Range(-0.4f, 0.4f))
                : new Vector3(UnityEngine.Random.Range(s.Area.min.x + 1f, s.Area.max.x - 1f), 0f, UnityEngine.Random.Range(s.Area.min.z + 1f, s.Area.max.z - 1f));
            Flare(p, UnityEngine.Random.Range(0.9f, 1.6f), 0.85f, 1.3f, false);
        }

        // 균열 번쩍: 바닥에 누운 균열 (flat) 또는 세워진 큰 균열 (왕좌 뒤)
        void Flare(Vector3 p, float size, float alpha, float life, bool upright)
        {
            if (_rift == null) return;
            p.y = upright ? 0f : 0.03f;
            var go = new GameObject("Rift Flare");
            go.transform.SetParent(transform, false);
            go.transform.position = p;
            Transform body = go.transform;
            if (upright)
            {
                go.AddComponent<InkFaceCamera>();
                var child = new GameObject("Sprite").transform;
                child.SetParent(go.transform, false);
                child.localPosition = Vector3.up * size * 0.5f;
                body = child;
            }
            else go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
            float unit = size / Mathf.Max(0.01f, _rift.bounds.size.y);
            body.localScale = Vector3.one * unit;
            var sr = body.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = _rift;
            sr.sortingOrder = upright ? 8 : 3;
            sr.color = new Color(1f, 1f, 1f, 0f);
            InkParticles.Burst(p + Vector3.up * (upright ? size * 0.5f : 0.2f), upright ? 30 : 6, new Color(0.9f, 0.75f, 1f, 0.9f), new Color(Violet.r, Violet.g, Violet.b, 0f), upright ? 3f : 0.4f, upright ? 0.4f : 0.25f, 1.1f, -1.5f);
            float t = 0f;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                float a = t < 0.2f ? t / 0.2f : t < life * 0.4f ? 1f : Mathf.Clamp01(1f - (t - life * 0.4f) / (life * 0.6f));
                sr.color = new Color(1f, 1f, 1f, alpha * a);
                if (upright) body.localScale = new Vector3(unit * Mathf.Lerp(0.4f, 1.2f, Mathf.Clamp01(t / 0.25f)), unit, unit);
                if (t < life) return true;
                Destroy(go);
                return false;
            });
        }

        // ── 왕좌의 방: 지붕 틈 빛줄기 · 보스 깨어남

        LineRenderer Shaft(Site s, int i)
        {
            var go = new GameObject("Roof Light");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMat;
            lr.positionCount = 2;
            float x = Mathf.Lerp(s.Area.min.x + 4f, s.Area.max.x - 4f, i / 2f) + UnityEngine.Random.Range(-1.5f, 1.5f);
            float z = s.Area.center.z + UnityEngine.Random.Range(-s.Area.extents.z * 0.5f, s.Area.extents.z * 0.5f);
            lr.SetPosition(0, new Vector3(x - 2.5f, 12f, z + 3f)); // 비스듬히 내려와요
            lr.SetPosition(1, new Vector3(x, 0.05f, z));
            lr.widthMultiplier = UnityEngine.Random.Range(1.6f, 2.4f);
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1f));
            lr.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0.85f, 0.75f, 1f), 0f), new GradientColorKey(Violet, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.18f, 0.5f), new GradientAlphaKey(0.08f, 1f) },
            };
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        static void TickShafts(Site s)
        {
            float t = Time.time;
            for (int i = 0; i < s.Shafts.Count; i++)
            {
                var lr = s.Shafts[i];
                if (lr == null) continue;
                lr.widthMultiplier = (1.8f + 0.25f * Mathf.Sin(t * 0.6f + i * 2.1f)) * (0.9f + 0.1f * Mathf.Sin(t * 2.3f + i));
            }
        }

        void WatchBoss(Site s)
        {
            if (_director == null) _director = FindAnyObjectByType<EnemyDirector>();
            if (_director == null || _director.Enemies == null) return;
            foreach (var e in _director.Enemies)
                if (e != null && !e.IsDead && e.Rank == EnemyRank.Boss && s.Watched.Add(e) && s.Area.SqrDistance(new Vector3(e.transform.position.x, s.Area.center.y, e.transform.position.z)) < 4f)
                {
                    var site = s;
                    e.Woke += _ => ThroneFlare(site);
                }
        }

        void ThroneFlare(Site s)
        {
            if (s.Room == null) return;
            Vector3 p = s.Throne != null ? s.Throne.position + Vector3.forward * 1.2f : new Vector3(s.Area.center.x, 0f, s.Area.max.z - 1f); // 왕좌 뒤 (없으면 북쪽 벽 가운데)
            Flare(p, 5f, 1f, 1.6f, true);
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _rift = Resources.Load<Sprite>("EliteFx/Blink");
            _lineMat = new Material(Shader.Find("Sprites/Default"));
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
