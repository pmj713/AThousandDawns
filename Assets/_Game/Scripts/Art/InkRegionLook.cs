using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 지역마다 판의 바탕을 바꿔요: 바닥 무늬(바닥 재질의 _BaseMap), 먼 배경 그림, 흩날리는 것(죽림은 없음 · 폐사찰은 낙엽과 재), 바닥 안개.
    // 바닥 재질은 판 전체가 같이 쓰는 에셋이라, 시작할 때 원래 무늬를 기억했다가 꺼질 때 되돌려요 (에디터에서 에셋이 바뀐 채 남지 않게).
    // 지역 번호는 RoomRun.Region. InkRegionDressing의 미리보기 지역(previewRegion)이 있으면 그걸 따라요.
    public class InkRegionLook : MonoBehaviour
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] Material floorMaterial;
        [SerializeField] Texture[] floorTextures = new Texture[0];      // 지역별 바닥 무늬 (비면 원래 것)
        [SerializeField] GameObject[] backdrops = new GameObject[0];     // 지역별 먼 배경 (그 지역 것만 켜요)
        [SerializeField] GameObject[] atmosphere = new GameObject[0];    // 지역별 분위기 (낙엽·안개)
        [SerializeField] InkRegionDressing dressing;

        RoomRun _run;
        Texture _original;
        int _shown = -1;

        public void Setup(Material floor, Texture[] floors, GameObject[] backs, GameObject[] airs, InkRegionDressing dress)
        {
            floorMaterial = floor;
            floorTextures = floors;
            backdrops = backs;
            atmosphere = airs;
            dressing = dress;
        }

        void Start()
        {
            if (floorMaterial != null) _original = floorMaterial.GetTexture(BaseMapId);
            Show(0);
        }

        void Update()
        {
            if (_run == null) _run = FindFirstObjectByType<RoomRun>();
            int region = dressing != null && dressing.PreviewRegion >= 0 ? dressing.PreviewRegion : _run != null ? _run.Region : 0;
            if (region != _shown) Show(region);
        }

        void Show(int region)
        {
            _shown = region;
            if (floorMaterial != null && floorTextures.Length > 0)
            {
                var tex = floorTextures[Mathf.Clamp(region, 0, floorTextures.Length - 1)];
                floorMaterial.SetTexture(BaseMapId, tex != null ? tex : _original);
            }
            Toggle(backdrops, region);
            Toggle(atmosphere, region);
        }

        static void Toggle(GameObject[] list, int region)
        {
            if (list.Length == 0) return;
            int pick = Mathf.Clamp(region, 0, list.Length - 1);
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null) list[i].SetActive(i == pick);
        }

        void OnDestroy()
        {
            if (floorMaterial != null && _original != null) floorMaterial.SetTexture(BaseMapId, _original);
        }
    }

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

    // 이 오브젝트가 켜지고 꺼질 때 흩어진 members도 같이 켜고 꺼요 (부모가 제각각인 배경을 한 묶음으로 다루려고)
    public class InkActiveMirror : MonoBehaviour
    {
        [SerializeField] GameObject[] members = new GameObject[0];

        public void Setup(GameObject[] list) => members = list;

        void OnEnable() => Set(true);
        void OnDisable() => Set(false);

        void Set(bool on)
        {
            foreach (var m in members)
                if (m != null) m.SetActive(on);
        }
    }
}
