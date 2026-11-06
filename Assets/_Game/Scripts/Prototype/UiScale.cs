using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 시험판 IMGUI 화면 크기 맞추기 (Canvas Scaler의 Expand와 같은 방식): 기준 1920×1080.
    // 가로·세로 배율 중 작은 쪽으로 화면 전체를 줄이거나 키워서, 어떤 창 크기에서도 같은 비율로 다 보여요.
    // OnGUI 첫 줄에서 Apply()를 부르고, 화면 크기는 Screen 대신 Width·Height(기준 단위)를 써요.
    public static class UiScale
    {
        public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;

        public static float Factor => Mathf.Max(0.1f, Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight));
        public static float Width => Screen.width / Factor;
        public static float Height => Screen.height / Factor;

        // 이 뒤로 그리는 IMGUI가 배율을 받아요 (마우스 위치도 같은 단위로 바뀌어요).
        public static void Apply() => GUI.matrix = Matrix4x4.Scale(new Vector3(Factor, Factor, 1f));

        // WorldToScreenPoint 결과(픽셀, 아래가 0)를 GUI 좌표(기준 단위, 위가 0)로
        public static Vector2 FromScreen(Vector3 screenPoint) =>
            new Vector2(screenPoint.x / Factor, (Screen.height - screenPoint.y) / Factor);
    }
}
