using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 만검귀종 (검 절초) 검이 잘 보이게 (플레이테스트): 게임 코드 SwordRain이 만드는 씬 루트 "만검귀종 검" (상자 셋: 날 · 코등이 · 손잡이)을 처음 볼 때 한 번 꾸며요.
    //   모델 (플레이테스트 3차): 판타지 검 대신 코드로 만든 무협풍 곧은 양날 검 (마름모 단면 날 · 금동 코등이 · 붉은 끈 손잡이 · 붉은 검수 술). 상자 셋은 끄고 1.4배로.
    //   밝기: 흰 금빛으로 밝게 칠해요 (빛을 받지 않는 재질이라 어두운 바닥에서도 또렷해요).
    //   테두리: 검 뒤로 금빛 빛무리가 카메라를 향해 은은하게 일렁이고, 날을 따라 가는 금빛 꼬리가 남아요.
    // 그림: FxArt/InkMist (부드러운 빛).
    public class InkUltimateFx : MonoBehaviour
    {
        static readonly Color Blade = new Color(1f, 0.97f, 0.85f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        static InkUltimateFx _host;
        static Material _bright, _trail, _vertexBright;
        static Sprite _glow;

        readonly HashSet<GameObject> _roots = new HashSet<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (_host != null) return;
            _host = new GameObject("Ink Ultimate Fx").AddComponent<InkUltimateFx>();
            DontDestroyOnLoad(_host.gameObject);
        }

        void Update()
        {
            InkRootWatch.Prune(_roots);
            foreach (var root in InkRootWatch.New(_roots)) // 처음 보는 루트만 이름을 봐요
                if (root.name == "만검귀종 검") Dress(root.transform);
        }

        static void Dress(Transform sword)
        {
            Load();
            // 무협풍 곧은 양날 검 (플레이테스트 3차: 판타지 검 느낌 → 검(劍)): 상자 셋은 끄고 그 자리에 코드로 만든 검을 1.4배로
            for (int i = 0; i < sword.childCount; i++)
            {
                var r = sword.GetChild(i).GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
            var jian = new GameObject("Ultimate Jian");
            jian.transform.SetParent(sword, false);
            jian.transform.localScale = Vector3.one * 1.4f;
            jian.AddComponent<MeshFilter>().sharedMesh = JianMesh();
            var jr = jian.AddComponent<MeshRenderer>();
            jr.sharedMaterial = _vertexBright; // 빛을 받지 않는 정점 색 그대로 (어두운 바닥에서도 또렷하게)
            jr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tassel = new GameObject("Jian Tassel");                     // 검수 (붉은 술): 손잡이 끝에서 흩날려요
            tassel.transform.SetParent(jian.transform, false);
            tassel.transform.localPosition = new Vector3(0f, 1.86f, 0f);
            tassel.AddComponent<MeshFilter>().sharedMesh = TasselMesh();
            var tr = tassel.AddComponent<MeshRenderer>();
            tr.sharedMaterial = _vertexBright;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tassel.AddComponent<InkTasselSway>();
            if (_glow != null) // 금빛 빛무리 (테두리)
            {
                var halo = new GameObject("Sword Halo").transform;
                halo.SetParent(sword, false);
                halo.localPosition = Vector3.up * 1.1f;
                halo.gameObject.AddComponent<InkFaceCamera>();
                var go = new GameObject("Glow");
                go.transform.SetParent(halo, false);
                go.transform.localScale = new Vector3(0.9f, 2.9f, 1f) / Mathf.Max(0.01f, _glow.bounds.size.x);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _glow;
                sr.color = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
                sr.sortingOrder = -1;
                go.AddComponent<InkFlicker>();
            }
            var tail = new GameObject("Sword Tail");
            tail.transform.SetParent(sword, false);
            tail.transform.localPosition = Vector3.up * 0.2f;
            var trail = tail.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _trail;
            trail.time = 0.15f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = 0.35f;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Blade, 0f), new GradientColorKey(Gold, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        // ── 검 (칼끝이 원점, +Y로): 날 0~1.4 (마름모 단면, 가운데 등줄) · 코등이 1.4~1.47 (금동, 양끝이 살짝 들린 여의 모양) · 손잡이 1.47~1.78 (붉은 끈 감김) · 칼자루 머리 1.78~1.86 (금동)
        static Mesh _jian, _tassel;
        static readonly Color Steel = new Color(0.93f, 0.95f, 1f), SteelEdge = new Color(1f, 1f, 1f), Ridge = new Color(0.62f, 0.68f, 0.78f);
        static readonly Color Bronze = new Color(0.92f, 0.7f, 0.28f), BronzeDark = new Color(0.6f, 0.4f, 0.14f);
        static readonly Color Wrap = new Color(0.55f, 0.08f, 0.08f), WrapDark = new Color(0.32f, 0.04f, 0.05f), Tassel = new Color(0.95f, 0.12f, 0.1f);

        static Mesh JianMesh()
        {
            if (_jian != null) return _jian;
            var b = new MeshBits();
            // 날: 칼끝 → 0.16에서 폭이 다 차고 → 밑동까지 곧게 (조금 넓어져요)
            var tip = new Vector3(0f, 0f, 0f);
            Vector3[] Ring(float y, float w, float t) => new[] { new Vector3(w, y, 0f), new Vector3(0f, y, t), new Vector3(-w, y, 0f), new Vector3(0f, y, -t) };
            var r1 = Ring(0.16f, 0.065f, 0.018f);
            var r2 = Ring(1.4f, 0.075f, 0.02f);
            Color[] ringColor = { SteelEdge, Ridge, SteelEdge, Ridge };
            Color[] faceTint = { Steel, new Color(0.82f, 0.86f, 0.95f), Steel, new Color(0.82f, 0.86f, 0.95f) };
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                b.Tri(tip, r1[i], r1[j], SteelEdge, ringColor[i], ringColor[j]);
                b.Quad(r1[i], r2[i], r2[j], r1[j], Color.Lerp(ringColor[i], faceTint[i], 0.4f), Color.Lerp(ringColor[i], faceTint[i], 0.4f), Color.Lerp(ringColor[j], faceTint[i], 0.4f), Color.Lerp(ringColor[j], faceTint[i], 0.4f));
            }
            // 코등이: 가운데 납작한 판 + 양끝이 위로 살짝 들린 여의 머리
            b.Box(new Vector3(0f, 1.435f, 0f), new Vector3(0.3f, 0.07f, 0.09f), Bronze, BronzeDark);
            b.Box(new Vector3(0.17f, 1.455f, 0f), new Vector3(0.07f, 0.09f, 0.08f), Bronze, BronzeDark);
            b.Box(new Vector3(-0.17f, 1.455f, 0f), new Vector3(0.07f, 0.09f, 0.08f), Bronze, BronzeDark);
            // 손잡이: 팔각 기둥 + 감긴 끈 마디
            b.Prism(1.47f, 1.78f, 0.038f, 8, Wrap, WrapDark);
            for (float y = 1.5f; y < 1.77f; y += 0.06f) b.Prism(y, y + 0.022f, 0.043f, 8, WrapDark, WrapDark);
            // 칼자루 머리
            b.Prism(1.78f, 1.86f, 0.05f, 8, Bronze, BronzeDark);
            return _jian = b.Build("Ultimate Jian");
        }

        // 술: 짧은 끈 + 아래로 퍼지는 붉은 술 (원점이 매듭, +Y로 0.5)
        static Mesh TasselMesh()
        {
            if (_tassel != null) return _tassel;
            var b = new MeshBits();
            b.Prism(0f, 0.12f, 0.012f, 6, Bronze, BronzeDark);
            b.Prism(0.12f, 0.16f, 0.03f, 6, Bronze, BronzeDark);                 // 매듭
            b.Cone(0.16f, 0.52f, 0.035f, 0.08f, 8, Tassel, new Color(0.6f, 0.05f, 0.05f));
            return _tassel = b.Build("Jian Tassel");
        }

        static void Load()
        {
            if (_bright != null) return;
            _bright = new Material(Shader.Find("Sprites/Default")) { name = "Ultimate Sword Bright", color = Blade };
            _vertexBright = new Material(Shader.Find("Sprites/Default")) { name = "Ultimate Sword Vertex" };
            _trail = new Material(Shader.Find("Sprites/Default")) { name = "Ultimate Sword Tail" };
            var tex = Resources.Load<Texture2D>("FxArt/InkMist");
            if (tex != null)
            {
                _trail.mainTexture = tex;
                _glow = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }

    // 검수 술 흔들림: 바람에 날리듯 두 축으로 천천히 흔들려요 (검이 기울어도 술은 따로 흔들려요)
    public class InkTasselSway : MonoBehaviour
    {
        float _seed;
        void Awake() => _seed = Random.Range(0f, 10f);
        void Update()
        {
            float t = Time.time * 4f + _seed;
            transform.localRotation = Quaternion.Euler(Mathf.Sin(t) * 18f + 10f, 0f, Mathf.Sin(t * 1.3f + 1f) * 22f);
        }
    }
}
