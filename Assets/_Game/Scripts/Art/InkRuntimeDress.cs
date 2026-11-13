using System;
using System.Collections.Generic;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 게임이 단색 도형으로 만드는 것들을 수묵으로 (4차 플레이 테스트) — 그림만, 판정·수명은 게임 그대로.
    //   "Smoke (연막)" 살수 연막: 납작한 원기둥은 숨기고 바닥 먹 번짐 + 그 둘레에서 짙은 먹 연기가 뭉게뭉게 솟아요. 사라지면 연기가 흩어져요.
    //   "번개" 뇌정 연쇄 번개 · 낙뢰: 노란 직선은 숨기고 지그재그 번개 (흰 심 + 노란 빛 + 먹 테두리)가 번쩍여요.
    //   "잔상" 잔영 가면: 캡슐은 숨기고 그 순간 플레이어 모델 모습을 먹빛 반투명으로 떠서 세워 둬요.
    //   "강기" 강기 (평타 3타째 · 절초 #강기 각인): 상자는 숨기고 쪽빛 테 붓질 초승달 검기 (InkSwordQi)가 날아가요.
    // 게임이 만든 루트 오브젝트는 이름을 처음 볼 때 한 번만 확인해요 (InkRootWatch). 짧게 사는 번개 때문에 매 프레임 봐요.
    public class InkRuntimeDress : MonoBehaviour
    {
        readonly HashSet<GameObject> _roots = new HashSet<GameObject>();
        readonly List<Func<float, bool>> _live = new List<Func<float, bool>>();
        static Material _lineMat, _ghostMat;
        static Sprite _blot;
        PlayerController _player;
        float _prune;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var go = new GameObject("Ink Runtime Dress");
            DontDestroyOnLoad(go);
            go.AddComponent<InkRuntimeDress>();
            _blot = Resources.Load<Sprite>("BloodBuddhaFx/Splash");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _prune -= dt;
            if (_prune <= 0f) { _prune = 1f; InkRootWatch.Prune(_roots); }
            foreach (var root in InkRootWatch.New(_roots))
            {
                switch (root.name)
                {
                    case "Smoke (연막)": Smoke(root); break;
                    case "번개": Bolt(root); break;
                    case "잔상": Ghost(root); break;
                    case "강기": Ganggi(root); break;
                }
            }
            for (int i = _live.Count - 1; i >= 0; i--)
                if (!_live[i](dt)) _live.RemoveAt(i);
        }

        // ── 살수 연막
        void Smoke(GameObject disc)
        {
            var source = disc.GetComponent<Renderer>();
            if (source != null) source.enabled = false;
            float radius = Mathf.Max(0.5f, disc.transform.localScale.x * 0.5f);
            var at = new Vector3(disc.transform.position.x, 0f, disc.transform.position.z);
            var holder = new GameObject("Ink Smoke Screen").transform;
            holder.SetParent(transform, false);
            holder.position = at;
            SpriteRenderer stain = null;
            if (_blot != null)
            {
                var go = new GameObject("Smoke Stain");
                go.transform.SetParent(holder, false);
                go.transform.position = at + Vector3.up * 0.022f;
                go.transform.rotation = Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * (radius * 2.4f / Mathf.Max(0.01f, _blot.bounds.size.x));
                stain = go.AddComponent<SpriteRenderer>();
                stain.sprite = _blot;
                stain.sortingOrder = 3;
            }
            var smoke = InkParticles.Smoke(holder, Vector3.zero, radius * 0.9f, radius * 5f, 1f);
            var main = smoke.main;
            main.maxParticles = 120;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            InkParticles.EmitBox(smoke, Vector3.up * 0.6f, new Vector3(radius * 1.8f, 1f, radius * 1.8f), 0.5f);
            float t = 0f, fade = -1f;
            _live.Add(dt =>
            {
                if (holder == null) return false;
                t += dt;
                if (disc == null && fade < 0f) { fade = 0f; smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
                float a = Mathf.Clamp01(t / 0.3f);
                if (fade >= 0f) { fade += dt; a *= 1f - Mathf.Clamp01(fade / 0.6f); }
                if (stain != null) stain.color = new Color(0.12f, 0.11f, 0.12f, 0.75f * a);
                if (fade < 2.6f) return true; // 남은 연기가 흩어질 때까지
                Destroy(holder.gameObject);
                return false;
            });
        }

        // ── 뇌정 번개
        void Bolt(GameObject source)
        {
            var line = source.GetComponent<LineRenderer>();
            if (line == null || line.positionCount < 2) return;
            line.enabled = false;
            Vector3 a = line.GetPosition(0), b = line.GetPosition(line.positionCount - 1);
            if (_lineMat == null) _lineMat = new Material(Shader.Find("Sprites/Default"));
            var ink = Zigzag(a, b, 0.2f, new Color(0.1f, 0.08f, 0.06f, 0.8f), 7, 0.28f);
            var glow = Zigzag(a, b, 0.12f, new Color(1f, 0.85f, 0.3f, 1f), 8, 0.2f, ink);
            var core = Zigzag(a, b, 0.05f, new Color(1f, 1f, 0.92f, 1f), 9, 0.2f, ink);
            float t = 0f, fade = -1f;
            _live.Add(dt =>
            {
                t += dt;
                if (source == null && fade < 0f) fade = 0f;
                if (fade >= 0f) fade += dt;
                float flick = (t % 0.06f) < 0.03f ? 1f : 0.6f; // 파르르
                float a2 = fade < 0f ? flick : flick * (1f - fade / 0.1f);
                SetAlpha(ink, 0.8f * a2);
                SetAlpha(glow, a2);
                SetAlpha(core, a2);
                if (fade < 0.1f) return true;
                Destroy(ink.gameObject);
                Destroy(glow.gameObject);
                Destroy(core.gameObject);
                return false;
            });
        }

        LineRenderer Zigzag(Vector3 a, Vector3 b, float width, Color color, int order, float jitter, LineRenderer copyFrom = null)
        {
            var lr = new GameObject("Ink Bolt").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.sharedMaterial = _lineMat;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (copyFrom != null) // 같은 모양에 겹쳐요 (먹 테두리 · 빛 · 흰 심)
            {
                lr.positionCount = copyFrom.positionCount;
                for (int i = 0; i < copyFrom.positionCount; i++) lr.SetPosition(i, copyFrom.GetPosition(i));
            }
            else
            {
                int n = Mathf.Clamp(Mathf.RoundToInt((b - a).magnitude / 0.5f) + 2, 4, 14);
                lr.positionCount = n;
                var dir = b - a;
                var side = Vector3.Cross(dir.normalized, Vector3.up);
                if (side.sqrMagnitude < 1e-3f) side = Vector3.right;
                for (int i = 0; i < n; i++)
                {
                    float k = i / (float)(n - 1);
                    var off = (i == 0 || i == n - 1) ? Vector3.zero : side.normalized * UnityEngine.Random.Range(-jitter, jitter) + Vector3.up * UnityEngine.Random.Range(-jitter, jitter) * 0.5f;
                    lr.SetPosition(i, a + dir * k + off);
                }
            }
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = order;
            return lr;
        }

        static void SetAlpha(LineRenderer lr, float a)
        {
            if (lr == null) return;
            var c = lr.startColor;
            lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        }

        // ── 잔영 가면: 플레이어 모습을 먹빛 반투명으로 떠서 세워요
        void Ghost(GameObject capsule)
        {
            var source = capsule.GetComponent<Renderer>();
            if (source != null) source.enabled = false;
            if (_player == null) _player = FindAnyObjectByType<PlayerController>();
            if (_player == null) return;
            if (_ghostMat == null) _ghostMat = new Material(Shader.Find("Sprites/Default")) { name = "Ink Afterimage" };
            var at = new Vector3(capsule.transform.position.x, 0f, capsule.transform.position.z);
            var holder = new GameObject("Ink Afterimage").transform;
            holder.SetParent(transform, false);
            var meshes = new List<(Mesh mesh, MeshRenderer renderer)>();
            foreach (var skinned in _player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skinned.enabled || !skinned.gameObject.activeInHierarchy) continue;
                var mesh = new Mesh { name = "Afterimage" };
                skinned.BakeMesh(mesh, true); // 그 순간 자세 그대로
                var white = new Color32[mesh.vertexCount]; // Sprites/Default는 정점 색을 곱해요 (없으면 안 보일 수 있어요)
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                mesh.colors32 = white;
                var go = new GameObject("Afterimage Part");
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(skinned.transform.position - _player.transform.position + at, skinned.transform.rotation);
                go.transform.localScale = Vector3.one; // BakeMesh(useScale)로 크기가 들어 있어요
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _ghostMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                meshes.Add((mesh, mr));
            }
            var block = new MaterialPropertyBlock();
            float t = 0f, fade = -1f;
            _live.Add(dt =>
            {
                if (holder == null) return false;
                t += dt;
                if (capsule == null && fade < 0f) fade = 0f;
                if (fade >= 0f) fade += dt;
                float a = Mathf.Clamp01(t / 0.15f) * (fade < 0f ? 1f : 1f - Mathf.Clamp01(fade / 0.3f));
                float flicker = 0.85f + 0.15f * Mathf.Sin(t * 14f); // 일렁이는 그림자
                block.SetColor("_Color", new Color(0.12f, 0.1f, 0.14f, 0.5f * a * flicker));
                foreach (var m in meshes) if (m.renderer != null) m.renderer.SetPropertyBlock(block);
                if (fade < 0.3f) return true;
                foreach (var m in meshes) if (m.mesh != null) Destroy(m.mesh);
                Destroy(holder.gameObject);
                return false;
            });
        }

        // ── 강기: 쪽빛 테 붓질 초승달 검기
        void Ganggi(GameObject bolt)
        {
            var source = bolt.GetComponent<Renderer>();
            Vector3 last = bolt.transform.position, dir = bolt.transform.forward;
            float width = Mathf.Clamp(bolt.transform.localScale.x * 1.4f, 0.9f, 2.4f);
            var qi = new InkSwordQi(transform, last, dir, width, new Color(0.35f, 0.6f, 1f, 0.9f), last.y);
            if (!qi.Valid) return;
            if (source != null) source.enabled = false;
            float fade = 0f;
            _live.Add(dt =>
            {
                if (bolt != null)
                {
                    var p = bolt.transform.position;
                    if ((p - last).sqrMagnitude > 1e-5f) dir = p - last;
                    last = p;
                }
                else fade += dt / 0.15f;
                qi.Pose(last, dir, 1f, 1f - Mathf.Clamp01(fade), Mathf.Max(0.6f, last.y));
                if (fade < 1f) return true;
                qi.Destroy();
                return false;
            });
        }
    }
}
