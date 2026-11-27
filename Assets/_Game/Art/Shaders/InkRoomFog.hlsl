// 맵 바깥 안개 (InkRoomFog.cs가 전역 값을 넣어요): 지금 방(사각형) 벽에서 멀어질수록 안개색으로 묻어요 — 카메라 거리 안개가 아니라서 방 안은 늘 맑아요.
//   MixFog가 쓰는 '보이는 정도'(1 = 맑음)를 돌려줘요. 안개색은 RenderSettings.fogColor (지역마다), 장면 안개는 선형 · 시작 5000이라 다른 셰이더엔 안개가 없어요.
//   _InkRoomFade.z(최대 짙기)가 0이면 (마을 · 다른 장면) 안개 없음
float4 _InkRoomRect;   // 방 가운데 x, z, 반폭 x, z
float4 _InkRoomFade;   // 벽에서 시작 거리, 짙어지는 폭, 최대 짙기
half InkRoomFog(float3 positionWS)
{
    float2 d = max(abs(positionWS.xz - _InkRoomRect.xy) - _InkRoomRect.zw, 0);
    float t = saturate((length(d) - _InkRoomFade.x) / max(_InkRoomFade.y, 0.01));
    return 1 - t * t * (3 - 2 * t) * _InkRoomFade.z;
}
