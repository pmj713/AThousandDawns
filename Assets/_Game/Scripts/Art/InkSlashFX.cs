using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Art
{
    // 평타 판정 부채꼴(SwingFlash)이 뜨는 순간을 감지해, 칼끝이 지나간 자리를 따라가는 가는 먹 궤적을 대신 그려요.
    // SwingFlash 오브젝트에 붙여요. 게임 코드는 그대로 두고 원래 부채꼴만 숨겨요 (범위·방향은 부채꼴 메시에서 읽음).
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class InkSlashFX : MonoBehaviour
    {
        const int Segments = 32;
        static readonly int ProgressId = Shader.PropertyToID("_Progress");
        static readonly int TrailId = Shader.PropertyToID("_Trail");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        [SerializeField] Material slashMaterial;
        [SerializeField] float sweepTime = 0.15f;  // 칼끝이 호 처음부터 끝까지 가는 시간
        [SerializeField] float trail = 2f;        // 꼬리 길이 (호 전체 대비)
        [SerializeField] float innerRatio = 0.8f;  // 궤적 띠가 시작하는 반지름 비율 (칼끝 근처만)

        MeshRenderer _flash;
        MeshFilter _flashFilter;
        MeshRenderer _stroke;
        Mesh _strokeMesh;
        MaterialPropertyBlock _block;
        float _time = -1f;
        bool _reverse;

        void Awake()
        {
            _flash = GetComponent<MeshRenderer>();
            _flashFilter = GetComponent<MeshFilter>();
            _block = new MaterialPropertyBlock();
            _strokeMesh = new Mesh { name = "InkSlash" };

            var go = new GameObject("InkSlash", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = _strokeMesh;
            _stroke = go.GetComponent<MeshRenderer>();
            _stroke.sharedMaterial = slashMaterial;
            _stroke.shadowCastingMode = ShadowCastingMode.Off;
            _stroke.receiveShadows = false;
            _stroke.enabled = false;
        }

        void LateUpdate()
        {
            // SwingFlash.Show가 렌더러를 켜면 새 휘두름
            if (_flash.enabled)
            {
                _flash.enabled = false;
                Begin();
            }
            if (_time < 0f) return;

            // 칼끝(머리)이 호 끝을 지나 꼬리까지 빠져나가면 끝. 머리는 빠르게 출발해 끝에서 조금 느려져요
            _time += Time.deltaTime;
            float total = sweepTime * (1f + trail);
            float t = Mathf.Clamp01(_time / total);
            float head = (1f - (1f - t) * (1f - t) * 0.6f - 0.4f * (1f - t)) * (1f + trail);
            _stroke.GetPropertyBlock(_block);
            _block.SetFloat(ProgressId, head);
            _block.SetFloat(TrailId, trail);
            _block.SetFloat(FadeId, 0f);
            _stroke.SetPropertyBlock(_block);
            if (t >= 1f)
            {
                _stroke.enabled = false;
                _time = -1f;
            }
        }

        void Begin()
        {
            var mesh = _flashFilter.sharedMesh;
            if (mesh == null || slashMaterial == null) return;
            var verts = mesh.vertices;
            if (verts.Length < 3) return;
            Vector3 first = verts[1], last = verts[verts.Length - 1];
            float radius = first.magnitude;
            float a0 = Mathf.Atan2(first.x, first.z), a1 = Mathf.Atan2(last.x, last.z);
            BuildArc(radius, a0, a1, _reverse);
            _reverse = !_reverse; // 연타마다 휘두르는 방향을 바꿔요

            _stroke.GetPropertyBlock(_block);
            _block.SetFloat(SeedId, Random.Range(0f, 50f));
            _block.SetFloat(ProgressId, 0f);
            _block.SetFloat(FadeId, 0f);
            _stroke.SetPropertyBlock(_block);
            _stroke.enabled = true;
            _time = 0f;
        }

        // 칼끝 근처(반지름 innerRatio ~ 105%)만 덮는 가는 호. uv.x는 휘두른 방향(0 → 1), uv.y는 안쪽(0) → 바깥(1)
        void BuildArc(float radius, float a0, float a1, bool reverse)
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            float inner = radius * innerRatio, outer = radius * 1.05f;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                float a = Mathf.Lerp(a0, a1, t);
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float u = reverse ? 1f - t : t;
                vertices[i * 2] = dir * inner + Vector3.up * 0.01f;
                vertices[i * 2 + 1] = dir * outer + Vector3.up * 0.01f;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }
            for (int i = 0; i < Segments; i++)
            {
                int k = i * 2;
                triangles[i * 6] = k; triangles[i * 6 + 1] = k + 1; triangles[i * 6 + 2] = k + 2;
                triangles[i * 6 + 3] = k + 1; triangles[i * 6 + 4] = k + 3; triangles[i * 6 + 5] = k + 2;
            }
            _strokeMesh.Clear();
            _strokeMesh.vertices = vertices;
            _strokeMesh.uv = uvs;
            _strokeMesh.triangles = triangles;
            _strokeMesh.RecalculateBounds();
        }
    }
}
