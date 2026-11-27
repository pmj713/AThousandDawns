using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 맵 바깥 안개 (죽림 시안 2차 · 2026-11-27 적용): 지금 방의 벽에서 멀어질수록 바깥이 지역 그늘색으로 묻혀 빈 땅이 덜 보여요.
    //   Ink 셰이더 셋(InkTriplanar · InkCharacter · InkFoliageCard)이 InkRoomFog.hlsl로 읽는 전역 값만 넣어요 (정점 · 렌더러 추가 없음).
    //   방 = RoomRun.RoomCenter · RoomExtent (방을 옮기면 따라가요), 안개색 = 지역마다 (RenderSettings.fogColor). 층 방에서만 켜요 (고수 · 보스 · 마을 등은 꺼요).
    //   장면 안개는 씬 빌더가 선형 · 시작 5000으로 켜 둬요 (안개 변형을 빌드에 남기면서, 다른 셰이더엔 안개가 안 끼게)
    public class InkRoomFog : MonoBehaviour
    {
        [SerializeField] Color[] regionColors =
        {
            new Color(0.25f, 0.29f, 0.17f),   // 죽림: 짙은 흙빛 초록
            new Color(0.30f, 0.29f, 0.24f),   // 폐사찰: 회갈색
            new Color(0.22f, 0.27f, 0.29f),   // 장강 수채: 청회색
            new Color(0.17f, 0.12f, 0.14f),   // 천마총: 검붉은 어둠
        };
        [SerializeField] float startFromWall = 2f, fadeWidth = 10f, maxDensity = 0.8f;
        [SerializeField] InkRegionDressing dressing;   // 미리보기 지역 (에디터)

        static readonly int RectId = Shader.PropertyToID("_InkRoomRect"), FadeId = Shader.PropertyToID("_InkRoomFade");
        RoomRun _run;
        int _region = -1;
        bool _on;
        Vector4 _rect;

        public void Setup(InkRegionDressing dress) => dressing = dress;

        void Update()
        {
            if (_run == null) _run = FindFirstObjectByType<RoomRun>();
            // 층 방(전투 장소)에서만: 고수 · 보스 · 객잔 · 정비실은 다른 자리에 따로 지은 방이라 RoomCenter(마지막 전투 장소)와 어긋나서
            //   결투장까지 안개에 묻혔어요 (플레이테스트 2차 11-27) → 꺼요. 마을도 꺼요
            var phase = _run != null ? _run.CurrentPhase : RoomRun.Phase.None;
            bool on = phase == RoomRun.Phase.Floor || phase == RoomRun.Phase.Fork;
            var rect = on ? new Vector4(_run.RoomCenter.x, _run.RoomCenter.z, _run.RoomExtent.x * 0.5f, _run.RoomExtent.y * 0.5f) : Vector4.zero;
            if (on != _on || rect != _rect)
            {
                _on = on; _rect = rect;
                Shader.SetGlobalVector(RectId, rect);
                Shader.SetGlobalVector(FadeId, new Vector4(startFromWall, fadeWidth, on ? maxDensity : 0f, 0f));
            }
            int region = dressing != null && dressing.PreviewRegion >= 0 ? dressing.PreviewRegion : _run != null ? _run.Region : 0;
            if (region != _region && regionColors.Length > 0)
            {
                _region = region;
                RenderSettings.fogColor = regionColors[Mathf.Clamp(region, 0, regionColors.Length - 1)];
            }
        }

        void OnDisable() => Shader.SetGlobalVector(FadeId, Vector4.zero);   // 다른 장면 · 끝낼 때 안개를 남기지 않아요
    }
}
