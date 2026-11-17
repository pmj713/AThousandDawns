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
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // 지역 바닥 색: 바닥 그림(채색)에 곱하는 빛 — 죽림 흙과 낙엽 · 폐사찰 이끼 낀 돌마당 · 장강 젖은 자갈과 물웅덩이 · 천마총 불씨 금 간 검붉은 돌
        static readonly Color[] FloorColors =
        {
            new Color(0.6f, 0.57f, 0.49f), new Color(0.55f, 0.53f, 0.5f), new Color(0.52f, 0.54f, 0.56f), new Color(0.55f, 0.5f, 0.5f),   // 바닥 그림이 이미 채색이라 거의 중립 (지역 빛만 살짝)
        };

        [SerializeField] Material floorMaterial;
        [SerializeField] Texture[] floorTextures = new Texture[0];      // 지역별 바닥 무늬 (비면 원래 것)
        [SerializeField] GameObject[] backdrops = new GameObject[0];     // 지역별 먼 배경 (그 지역 것만 켜요)
        [SerializeField] GameObject[] atmosphere = new GameObject[0];    // 지역별 분위기 (낙엽·안개)
        [SerializeField] InkRegionDressing dressing;

        RoomRun _run;
        Texture _original;
        Color _originalColor;
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
            if (floorMaterial != null) { _original = floorMaterial.GetTexture(BaseMapId); _originalColor = floorMaterial.GetColor(BaseColorId); }
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
                floorMaterial.SetColor(BaseColorId, FloorColors[Mathf.Clamp(region, 0, FloorColors.Length - 1)]);
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
            if (floorMaterial != null && _original != null) { floorMaterial.SetTexture(BaseMapId, _original); floorMaterial.SetColor(BaseColorId, _originalColor); }
        }
    }
}
