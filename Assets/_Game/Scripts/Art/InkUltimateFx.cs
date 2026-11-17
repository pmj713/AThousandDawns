using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 만검귀종 (검 절초) 검이 잘 보이게 (플레이테스트): 게임 코드 SwordRain이 만드는 씬 루트 "만검귀종 검" (상자 셋: 날 · 코등이 · 손잡이)을 처음 볼 때 한 번 꾸며요.
    //   크기: 날·코등이·손잡이를 1.4배로 (루트 크기는 게임 코드가 쓰니까 자식만).
    //   밝기: 흰 금빛으로 밝게 칠해요 (빛을 받지 않는 재질이라 어두운 바닥에서도 또렷해요).
    //   테두리: 검 뒤로 금빛 빛무리가 카메라를 향해 은은하게 일렁이고, 날을 따라 가는 금빛 꼬리가 남아요.
    // 그림: FxArt/InkMist (부드러운 빛).
    public class InkUltimateFx : MonoBehaviour
    {
        static readonly Color Blade = new Color(1f, 0.97f, 0.85f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        static InkUltimateFx _host;
        static Material _bright, _trail;
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
            for (int i = 0; i < sword.childCount; i++)
            {
                var part = sword.GetChild(i);
                part.localPosition *= 1.4f;
                part.localScale *= 1.4f;
                var r = part.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = _bright;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
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

        static void Load()
        {
            if (_bright != null) return;
            _bright = new Material(Shader.Find("Sprites/Default")) { name = "Ultimate Sword Bright", color = Blade };
            _trail = new Material(Shader.Find("Sprites/Default")) { name = "Ultimate Sword Tail" };
            var tex = Resources.Load<Texture2D>("FxArt/InkMist");
            if (tex != null)
            {
                _trail.mainTexture = tex;
                _glow = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
