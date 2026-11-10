using AThousandDawns.Core;
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
        [SerializeField] float circleTrail = 0.5f; // 원(회선참) 꼬리 길이 (한 바퀴 대비)

        MeshRenderer _flash;
        MeshFilter _flashFilter;
        MeshRenderer _stroke;
        Mesh _strokeMesh;
        MaterialPropertyBlock _block;
        float _time = -1f;
        bool _circle;        // 회선참: 칼과 같은 속도로 돌아요
        float _spinTime;     // 몸이 한 바퀴 도는 시간 (게임: 초식 판정 시간 + 0.08초)
        float _spinAtHit;    // 판정(부채꼴이 뜨는) 순간까지 이미 돈 시간 = 초식 판정 시간

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

            _time += Time.deltaTime;
            if (_circle)
            {
                // 회선참: 게임이 몸을 _spinTime에 시계방향으로 한 바퀴 돌려요. 머리는 칼(오른쪽에서 출발)이 지금 있는 자리에 —
                // 판정 순간 이미 돈 만큼(_spinAtHit) 앞에서 시작해 같은 속도로 따라가고, 지나온 자리가 꼬리로 남아요
                float circleHead = (_spinAtHit + _time) / _spinTime;
                _stroke.GetPropertyBlock(_block);
                _block.SetFloat(ProgressId, circleHead);
                _block.SetFloat(TrailId, circleTrail);
                _block.SetFloat(FadeId, 0f);
                _stroke.SetPropertyBlock(_block);
                if (circleHead - circleTrail >= 1f)
                {
                    _stroke.enabled = false;
                    _time = -1f;
                }
                return;
            }

            // 칼끝(머리)이 호 끝을 지나 꼬리까지 빠져나가면 끝. 머리는 빠르게 출발해 끝에서 조금 느려져요
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
            _circle = false;
            if (verts.Length == 4)
                BuildLine(verts[2].z, 0f); // 직사각형 범위 = 세로베기: 범위 끝까지 곧게 내리그은 획
            else
            {
                float span = Vector3.Angle(Vector3.forward, new Vector3(first.x, 0f, first.z)) * 2f; // 부채꼴 각 = 정면~첫 꼭짓점 각의 2배
                if (span < 60f)
                    BuildLine(radius, Mathf.LerpAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg, 0.5f) * Mathf.Deg2Rad); // 좁은 범위 = 세로베기: 정면으로 곧게 내리그은 획
                else if (span > 359f)
                {
                    BuildArc(radius, Mathf.PI * 0.5f, Mathf.PI * 2.5f, false); // 원(회선참): 칼이 있는 오른쪽에서 시작해 시계방향으로 한 바퀴 (몸도 시계방향으로 돌아요)
                    _circle = true;
                    _spinAtHit = GameConfig.GetOptional("sword.skill_hit_time", 0.12f);
                    _spinTime = Mathf.Max(0.01f, _spinAtHit + 0.08f);   // PlayerController.ApplySkillSpin과 같은 식
                }
                else
                    BuildArc(radius, a0, a1, true);                                 // 부채꼴(2타·3타 등): 항상 오른쪽 → 왼쪽 (모델이 휘두르는 방향)
            }

            _stroke.GetPropertyBlock(_block);
            _block.SetFloat(SeedId, Random.Range(0f, 50f));
            _block.SetFloat(ProgressId, 0f);
            _block.SetFloat(FadeId, 0f);
            _stroke.SetPropertyBlock(_block);
            _stroke.enabled = true;
            _time = 0f;
        }

        // 세로베기: 몸 앞에서 범위 끝까지 곧게 뻗은 가는 획. uv.x는 가까운 쪽(0) → 먼 쪽(1), uv.y는 획의 한쪽(0) → 다른 쪽(1)
        void BuildLine(float radius, float angle)
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * 6];
            var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            var side = new Vector3(dir.z, 0f, -dir.x);
            float near = radius * 0.2f, far = radius * 1.05f, half = radius * (1f - innerRatio) * 0.5f;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                var c = dir * Mathf.Lerp(near, far, t) + Vector3.up * 0.01f;
                float w = half * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, t)); // 끝으로 갈수록 붓끝처럼 가늘게
                vertices[i * 2] = c - side * w;
                vertices[i * 2 + 1] = c + side * w;
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
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
