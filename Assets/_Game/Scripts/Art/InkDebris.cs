using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 판타지 적 피격·쓰러짐 파편 (뼛조각·돌 조각·슬라임 방울): 작은 조각을 사방으로 튀겨 포물선으로 떨어뜨리고, 바닥에서 한두 번 튕긴 뒤 줄어들며 사라져요.
    //   재질은 그 적 몸 재질을 복제해 그림 없이 색만 칠해요 (채색 캐릭터 셰이더 설정을 그대로 써서 장면과 같은 명암). 충돌체 없음
    public class InkDebris : MonoBehaviour
    {
        public enum Shape { Bone, Stone, Drop }

        static Mesh _cube, _capsule, _sphere;
        static readonly Dictionary<(Material, Color32), Material> Mats = new Dictionary<(Material, Color32), Material>();
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), BaseColorId = Shader.PropertyToID("_BaseColor");

        Vector3 _vel, _spinAxis;
        float _spin, _ground, _t, _life;
        Vector3 _scale;
        int _bounces;

        // 방을 옮기면 (같은 크기 방은 같은 월드 자리를 다시 써요) 남은 파편을 모두 지워요 (플레이테스트 4차)
        static readonly List<InkDebris> Live = new List<InkDebris>();
        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            AThousandDawns.Enemies.EnemyDirector.RoomReset -= ClearRoom;
            AThousandDawns.Enemies.EnemyDirector.RoomReset += ClearRoom;
        }

        static void ClearRoom()
        {
            for (int i = Live.Count - 1; i >= 0; i--) if (Live[i] != null) Destroy(Live[i].gameObject);
            Live.Clear();
        }

        // origin: 터지는 자리 · up: 위로 솟는 세기 · spread: 옆으로 퍼지는 세기 · size: 조각 크기(월드)
        public static void Burst(Vector3 origin, float ground, Shape shape, Color color, Material template, int count, float size, float up, float spread, float life = 1.4f)
        {
            if (template == null) return;
            var mat = Mat(template, color);
            var mesh = shape == Shape.Bone ? Capsule() : shape == Shape.Stone ? Cube() : Sphere();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(shape == Shape.Bone ? "Bone Chip" : shape == Shape.Stone ? "Stone Chip" : "Slime Drop");
                go.transform.position = origin + Random.insideUnitSphere * size;
                go.transform.rotation = Random.rotation;
                float s = size * Random.Range(0.6f, 1.3f);
                var scale = shape == Shape.Bone ? new Vector3(s * 0.45f, s, s * 0.45f) : shape == Shape.Stone ? new Vector3(s, s * Random.Range(0.6f, 1f), s * Random.Range(0.7f, 1.1f)) : Vector3.one * s;
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var d = go.AddComponent<InkDebris>();
                var flat = Random.insideUnitCircle * spread;
                d._vel = new Vector3(flat.x, up * Random.Range(0.6f, 1.2f), flat.y);
                d._spinAxis = Random.onUnitSphere;
                d._spin = Random.Range(360f, 900f) * (shape == Shape.Drop ? 0f : 1f);
                d._ground = ground + scale.y * 0.3f;
                d._life = life * Random.Range(0.8f, 1.2f);
                d._scale = scale;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            _vel.y -= 14f * dt;
            var p = transform.position + _vel * dt;
            if (p.y < _ground)
            {
                p.y = _ground;
                if (_bounces < 2 && _vel.y < -1.5f) { _vel.y *= -0.35f; _vel.x *= 0.5f; _vel.z *= 0.5f; _spin *= 0.5f; _bounces++; }
                else { _vel = Vector3.zero; _spin = 0f; }
            }
            transform.position = p;
            if (_spin > 0f) transform.rotation = Quaternion.AngleAxis(_spin * dt, _spinAxis) * transform.rotation;
            float fade = Mathf.Clamp01((_t - _life * 0.7f) / (_life * 0.3f));
            transform.localScale = _scale * (1f - fade);
            if (_t >= _life) Destroy(gameObject);
        }

        static Material Mat(Material template, Color color)
        {
            var key = (template, (Color32)color);
            if (Mats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(template) { name = $"Debris {ColorUtility.ToHtmlStringRGB(color)}" };
            if (m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, Texture2D.whiteTexture);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            Mats[key] = m;
            return m;
        }

        static Mesh Grab(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }
            Destroy(go);
            return mesh;
        }

        static Mesh Cube() => _cube != null ? _cube : _cube = Grab(PrimitiveType.Cube);
        static Mesh Capsule() => _capsule != null ? _capsule : _capsule = Grab(PrimitiveType.Capsule);
        static Mesh Sphere() => _sphere != null ? _sphere : _sphere = Grab(PrimitiveType.Sphere);
    }
}
