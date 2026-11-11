using UnityEngine;

namespace AThousandDawns.Art
{
    // 장강 수채 분위기 (지역 2): 카메라 둘레 바닥에 낮게 깔린 물안개가 천천히 흘러가고, 갈대 솜이 바람 따라 둥실 떠다녀요.
    // InkRegionLook이 지역에 맞춰 이 오브젝트를 켜고 꺼요 (씬 빌더 PrototypeSceneBuilder.InkTemple.cs의 분위기 목록 세 번째).
    // 그림: FxArt/InkMist(안개 결) · HudArt/Blot(솜 모양 흰 번짐) — 둘 다 Resources에 있어요.
    public class InkRiverAir : MonoBehaviour
    {
        [SerializeField] int mistCount = 14;
        [SerializeField] int fluffCount = 36;
        [SerializeField] Vector3 area = new Vector3(26f, 5f, 18f);

        static readonly Color MistColor = new Color(0.82f, 0.86f, 0.88f, 0.32f);   // 물빛 도는 한지색
        static readonly Color FluffColor = new Color(0.97f, 0.95f, 0.88f, 0.9f);   // 갈대 솜

        Transform[] _mist, _fluff;
        SpriteRenderer[] _mistSr;
        Vector3[] _mistVel, _fluffVel;
        float[] _seed;
        Camera _camera;

        void Start()
        {
            _camera = Camera.main;
            var mistTex = Resources.Load<Texture2D>("FxArt/InkMist");
            var blotTex = Resources.Load<Texture2D>("HudArt/Blot");
            var mist = mistTex != null ? Sprite.Create(mistTex, new Rect(0, 0, mistTex.width, mistTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect) : null;
            var fluff = blotTex != null ? Sprite.Create(blotTex, new Rect(0, 0, blotTex.width, blotTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect) : null;

            _mist = new Transform[mist != null ? mistCount : 0];
            _mistSr = new SpriteRenderer[_mist.Length];
            _mistVel = new Vector3[_mist.Length];
            for (int i = 0; i < _mist.Length; i++)
            {
                var go = new GameObject("River Mist");
                go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f); // 바닥에 눕혀요
                float w = Random.Range(5f, 9f);
                go.transform.localScale = new Vector3(w, w * Random.Range(0.4f, 0.6f), 1f) / Mathf.Max(0.01f, mist.bounds.size.x);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = mist;
                sr.color = MistColor;
                sr.sortingOrder = 3;
                _mist[i] = go.transform;
                _mistSr[i] = sr;
                _mistVel[i] = new Vector3(Random.Range(0.25f, 0.6f), 0f, Random.Range(-0.1f, 0.1f)); // 강물 따라 옆으로
                Place(_mist[i], Random.Range(0.15f, 0.45f), true);
            }

            _fluff = new Transform[fluff != null ? fluffCount : 0];
            _fluffVel = new Vector3[_fluff.Length];
            _seed = new float[_fluff.Length];
            for (int i = 0; i < _fluff.Length; i++)
            {
                var go = new GameObject("Reed Fluff");
                go.transform.SetParent(transform, false);
                go.AddComponent<InkFaceCamera>();
                float s = Random.Range(0.07f, 0.14f);
                go.transform.localScale = Vector3.one * s / Mathf.Max(0.01f, fluff.bounds.size.x);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = fluff;
                sr.color = FluffColor;
                sr.sortingOrder = 7;
                _fluff[i] = go.transform;
                _fluffVel[i] = new Vector3(Random.Range(0.6f, 1.3f), Random.Range(-0.05f, 0.12f), Random.Range(-0.2f, 0.2f)); // 둥실, 거의 안 떨어져요
                _seed[i] = Random.Range(0f, 10f);
                Place(_fluff[i], Random.Range(0.4f, area.y), true);
            }
        }

        Vector3 Center() => _camera != null ? new Vector3(_camera.transform.position.x, 0f, _camera.transform.position.z + 8f) : transform.position;

        void Place(Transform t, float height, bool anywhere)
        {
            var c = Center();
            float x = anywhere ? Random.Range(-0.5f, 0.5f) * area.x : -0.55f * area.x; // 새로 날 땐 바람이 오는 쪽(왼쪽)에서
            t.position = c + new Vector3(x, height, Random.Range(-0.5f, 0.5f) * area.z);
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
                m.Rotate(0f, 0f, 3f * dt, Space.Self);
                _mistSr[i].color = new Color(MistColor.r, MistColor.g, MistColor.b, MistColor.a * (0.75f + 0.25f * Mathf.Sin(time * 0.5f + i)));
                if (Mathf.Abs(m.position.x - c.x) > area.x * 0.6f || Mathf.Abs(m.position.z - c.z) > area.z * 0.6f) Place(m, Random.Range(0.15f, 0.45f), false);
            }
            for (int i = 0; i < _fluff.Length; i++)
            {
                var f = _fluff[i];
                var sway = new Vector3(0f, Mathf.Sin(time * 1.1f + _seed[i]) * 0.25f, Mathf.Cos(time * 0.7f + _seed[i]) * 0.15f);
                f.position += (_fluffVel[i] + sway) * dt;
                var d = f.position - c;
                if (f.position.y < 0.1f || f.position.y > area.y + 1f || Mathf.Abs(d.x) > area.x * 0.6f || Mathf.Abs(d.z) > area.z * 0.6f) Place(f, Random.Range(0.4f, area.y), false);
            }
        }
    }
}
