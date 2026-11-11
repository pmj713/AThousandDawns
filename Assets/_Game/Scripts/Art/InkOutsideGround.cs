using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 맵 바깥 땅: 모든 방 아래로 깔린 큰 바닥 한 장 (방 바닥보다 살짝 낮아 방 안에선 안 보이고, 벽 너머 흰 빈 곳을 채워요).
    // 지역마다 이음새 없는 수묵 땅 그림으로 바꿔요 — 죽림: 풀·이끼·댓잎 / 폐사찰: 흙·재·깨진 기와. 그 뒤 지역은 마지막 그림을 써요.
    // 지역 번호는 RoomRun.Region, InkRegionDressing의 미리보기 지역이 있으면 그걸 따라요 (InkRegionLook과 같은 규칙).
    // 씬 빌더(PrototypeSceneBuilder.InkOutside.cs)가 붙여요. 재질은 복사본을 써서 에셋은 바뀌지 않아요.
    // 게임 중엔 벽 너머 먼 배경 그림 판(Backdrop_*)을 숨겨요.
    public class InkOutsideGround : MonoBehaviour
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] Texture[] regionTextures;

        RoomRun _run;
        InkRegionDressing _dressing;
        Material _material;
        int _shown = -1;

        public void Setup(Texture[] textures) => regionTextures = textures;

        void Start()
        {
            var r = GetComponent<Renderer>();
            if (r != null) _material = r.material; // 복사본

            // 벽 너머에 세운 먼 배경 그림 판(Backdrop_*)은 게임 중엔 숨겨요 — 바깥 땅이 대신 채워요 (사용자 요청).
            // 지역마다 켜고 끄는 건 그쪽(InkRegionLook) 그대로 두고, 그림만 안 그리게 해요.
            foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (sr.name.StartsWith("Backdrop_")) sr.enabled = false;
        }

        void Update()
        {
            if (_material == null || regionTextures == null || regionTextures.Length == 0) return;
            if (_run == null) _run = FindAnyObjectByType<RoomRun>();
            if (_dressing == null) _dressing = FindAnyObjectByType<InkRegionDressing>();
            int region = _dressing != null && _dressing.PreviewRegion >= 0 ? _dressing.PreviewRegion : _run != null ? _run.Region : 0;
            region = Mathf.Clamp(region, 0, regionTextures.Length - 1);
            if (region == _shown) return;
            _shown = region;
            if (regionTextures[region] != null) _material.SetTexture(BaseMapId, regionTextures[region]);
        }
    }
}
