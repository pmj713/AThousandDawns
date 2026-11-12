using UnityEngine;

namespace AThousandDawns.Art
{
    // 천마총 분위기 (지역 3): 카메라 둘레 바닥에 낮게 깔린 붉은 안개가 느리게 맴돌고, 재와 불씨가 천천히 떠올라요.
    // InkRegionLook이 지역에 맞춰 이 오브젝트를 켜고 꺼요 (씬 빌더 PrototypeSceneBuilder.InkTemple.cs의 분위기 목록 네 번째).
    // 그림: FxArt/InkMist(안개 결) · HudArt/Blot(재·불씨 모양) — 둘 다 Resources에 있어요.
    public class InkTombAir : MonoBehaviour
    {
        [SerializeField] int mistCount = 12;
        [SerializeField] int ashCount = 30;
        [SerializeField] int emberCount = 14;
        [SerializeField] Vector3 area = new Vector3(26f, 6f, 18f);

        static readonly Color MistColor = new Color(0.55f, 0.12f, 0.1f, 0.28f);   // 핏빛 안개
        static readonly Color AshColor = new Color(0.2f, 0.18f, 0.17f, 0.85f);    // 재
        static readonly Color EmberColor = new Color(1f, 0.42f, 0.15f, 1f);       // 불씨

        Transform[] _mist, _bits;
        SpriteRenderer[] _mistSr, _bitSr;
        Vector3[] _mistVel, _bitVel;
        float[] _seed;
        Camera _camera;

        void Start()
        {
            _camera = Camera.main;
            var mistTex = Resources.Load<Texture2D>("FxArt/InkMist");
            var blotTex = Resources.Load<Texture2D>("HudArt/Blot");
            var mist = mistTex != null ? Sprite.Create(mistTex, new Rect(0, 0, mistTex.width, mistTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect) : null;
            var blot = blotTex != null ? Sprite.Create(blotTex, new Rect(0, 0, blotTex.width, blotTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect) : null;

            int m = mist != null ? mistCount : 0;
            _mist = new Transform[m];
            _mistSr = new SpriteRenderer[m];
            _mistVel = new Vector3[m];
            for (int i = 0; i < m; i++)
            {
                var go = new GameObject("Blood Mist");
                go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                float w = Random.Range(5f, 9f);
                go.transform.localScale = new Vector3(w, w * Random.Range(0.45f, 0.7f), 1f) / Mathf.Max(0.01f, mist.bounds.size.x);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = mist;
                sr.color = MistColor;
                sr.sortingOrder = 3;
                _mist[i] = go.transform;
                _mistSr[i] = sr;
                _mistVel[i] = new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.2f, 0.2f)); // 흐르지 않고 맴돌아요
                Place(_mist[i], Random.Range(0.12f, 0.4f));
            }

            int n = blot != null ? ashCount + emberCount : 0;
            _bits = new Transform[n];
            _bitSr = new SpriteRenderer[n];
            _bitVel = new Vector3[n];
            _seed = new float[n];
            for (int i = 0; i < n; i++)
            {
                bool ember = i >= ashCount;
                var go = new GameObject(ember ? "Ember" : "Ash");
                go.transform.SetParent(transform, false);
                go.AddComponent<InkFaceCamera>();
                float s = ember ? Random.Range(0.05f, 0.09f) : Random.Range(0.06f, 0.13f);
                go.transform.localScale = Vector3.one * s / Mathf.Max(0.01f, blot.bounds.size.x);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = blot;
                sr.color = ember ? EmberColor : AshColor;
                sr.sortingOrder = 7;
                _bits[i] = go.transform;
                _bitSr[i] = sr;
                _bitVel[i] = new Vector3(Random.Range(-0.2f, 0.2f), ember ? Random.Range(0.4f, 0.9f) : Random.Range(0.15f, 0.4f), Random.Range(-0.2f, 0.2f)); // 천천히 떠올라요
                _seed[i] = Random.Range(0f, 10f);
                Place(_bits[i], Random.Range(0f, area.y));
            }
        }

        Vector3 Center() => _camera != null ? new Vector3(_camera.transform.position.x, 0f, _camera.transform.position.z + 8f) : transform.position;

        void Place(Transform t, float height)
        {
            var c = Center();
            t.position = c + new Vector3(Random.Range(-0.5f, 0.5f) * area.x, height, Random.Range(-0.5f, 0.5f) * area.z);
        }

        void Update()
        {
            if (_mist == null) return;
            var c = Center();
            float dt = Time.deltaTime, time = Time.time;
            for (int i = 0; i < _mist.Length; i++)
            {
                var m = _mist[i];
                m.position += _mistVel[i] * dt;
                m.Rotate(0f, 0f, 4f * dt, Space.Self);
                _mistSr[i].color = new Color(MistColor.r, MistColor.g, MistColor.b, MistColor.a * (0.7f + 0.3f * Mathf.Sin(time * 0.4f + i)));
                if (Mathf.Abs(m.position.x - c.x) > area.x * 0.6f || Mathf.Abs(m.position.z - c.z) > area.z * 0.6f) Place(m, Random.Range(0.12f, 0.4f));
            }
            for (int i = 0; i < _bits.Length; i++)
            {
                var b = _bits[i];
                var sway = new Vector3(Mathf.Sin(time * 0.9f + _seed[i]) * 0.3f, 0f, Mathf.Cos(time * 0.7f + _seed[i]) * 0.2f);
                b.position += (_bitVel[i] + sway) * dt;
                if (b.name == "Ember") // 불씨는 깜빡여요
                    _bitSr[i].color = new Color(EmberColor.r, EmberColor.g * (0.7f + 0.3f * Mathf.Sin(time * 9f + _seed[i])), EmberColor.b, 0.6f + 0.4f * Mathf.Sin(time * 6f + _seed[i]));
                var d = b.position - c;
                if (b.position.y > area.y || Mathf.Abs(d.x) > area.x * 0.6f || Mathf.Abs(d.z) > area.z * 0.6f) Place(b, Random.Range(0f, 0.3f)); // 바닥에서 다시 피어요
            }
        }
    }
}
