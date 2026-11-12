using UnityEngine;

namespace AThousandDawns.Art
{
    // 폐사찰 분위기: 카메라 둘레에서 낙엽과 재가 천천히 흩날리며 떨어져요 (먹색·고동색 작은 카드)
    public class InkDriftingLeaves : MonoBehaviour
    {
        [SerializeField] Material leafMaterial;
        [SerializeField] int count = 40;
        [SerializeField] Vector3 area = new Vector3(22f, 8f, 16f);

        Transform[] _leaves;
        Vector3[] _velocity;
        float[] _spin;
        Camera _camera;

        public void Setup(Material material) => leafMaterial = material;

        void Start()
        {
            _camera = Camera.main;
            _leaves = new Transform[count];
            _velocity = new Vector3[count];
            _spin = new float[count];
            for (int i = 0; i < count; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Leaf";
                q.transform.SetParent(transform, false);
                var r = q.GetComponent<Renderer>();
                r.sharedMaterial = leafMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float s = Random.Range(0.08f, 0.18f);
                q.transform.localScale = new Vector3(s, s * 0.6f, 1f);
                _leaves[i] = q.transform;
                Respawn(i, true);
            }
        }

        Vector3 Center() => _camera != null ? new Vector3(_camera.transform.position.x, 0f, _camera.transform.position.z + 8f) : transform.position;

        void Respawn(int i, bool anywhere)
        {
            var c = Center();
            _leaves[i].position = c + new Vector3(Random.Range(-0.5f, 0.5f) * area.x, anywhere ? Random.Range(0f, area.y) : area.y, Random.Range(-0.5f, 0.5f) * area.z);
            _velocity[i] = new Vector3(Random.Range(0.4f, 1.2f), -Random.Range(0.35f, 0.8f), Random.Range(-0.3f, 0.3f));   // 바람 따라 비스듬히
            _spin[i] = Random.Range(-160f, 160f);
        }

        void Update()
        {
            if (_leaves == null) return;
            var c = Center();
            float t = Time.time;
            for (int i = 0; i < _leaves.Length; i++)
            {
                var l = _leaves[i];
                var sway = new Vector3(Mathf.Sin(t * 1.3f + i) * 0.4f, 0f, Mathf.Cos(t * 0.9f + i * 1.7f) * 0.2f);
                l.position += (_velocity[i] + sway) * Time.deltaTime;
                l.rotation = Quaternion.Euler(30f + Mathf.Sin(t * 2f + i) * 40f, t * _spin[i], 0f);
                var d = l.position - c;
                if (l.position.y < 0.02f || Mathf.Abs(d.x) > area.x * 0.6f || Mathf.Abs(d.z) > area.z * 0.6f) Respawn(i, false);
            }
        }
    }
}
