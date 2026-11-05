using UnityEngine;

namespace AThousandDawns.Player
{
    // 평타 판정 범위를 바닥에 잠깐 그려요 (시험판용 표시).
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SwingFlash : MonoBehaviour
    {
        const int Segments = 20;

        Mesh _mesh;
        MeshRenderer _renderer;
        float _timer;

        void Awake()
        {
            _mesh = new Mesh { name = "SwingFlash" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.enabled = false;
        }

        public void Show(Vector3 origin, Vector3 forward, float radius, float angleDeg, float duration)
        {
            BuildFan(radius, angleDeg);
            transform.SetPositionAndRotation(new Vector3(origin.x, 0.02f, origin.z), Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.z)));
            _timer = duration;
            _renderer.enabled = true;
        }

        void Update()
        {
            if (!_renderer.enabled) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f) _renderer.enabled = false;
        }

        void BuildFan(float radius, float angleDeg)
        {
            var vertices = new Vector3[Segments + 2];
            var triangles = new int[Segments * 3];
            vertices[0] = Vector3.zero;
            float half = angleDeg * 0.5f * Mathf.Deg2Rad;
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)Segments);
                vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            }
            for (int i = 0; i < Segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }
}
