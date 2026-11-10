using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 시험판 IMGUI 화면 크기 맞추기 (Canvas Scaler의 Expand와 같은 방식): 기준 1920×1080.
    // 가로·세로 배율 중 작은 쪽으로 화면 전체를 줄이거나 키워서, 어떤 창 크기에서도 같은 비율로 다 보여요.
    // OnGUI 첫 줄에서 Apply()를 부르고, 화면 크기는 Screen 대신 Width·Height(기준 단위)를 써요.
    // 설정에서 화면 비율을 고정하면 검은 띠 안쪽 영역(Letterbox.Viewport)이 화면이에요.
    public static class UiScale
    {
        public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;

        static Rect View => Letterbox.Viewport;
        public static float Factor => Mathf.Max(0.1f, Mathf.Min(View.width / ReferenceWidth, View.height / ReferenceHeight));
        public static float Width => View.width / Factor;
        public static float Height => View.height / Factor;

        // 그리는 영역의 왼쪽 위 (GUI 픽셀, 위가 0)
        static Vector2 Origin
        {
            get
            {
                var v = View;
                return new Vector2(v.x, Screen.height - v.yMax);
            }
        }

        // 이 뒤로 그리는 IMGUI가 배율을 받아요 (마우스 위치도 같은 단위로 바뀌어요).
        // 글자는 상자보다 커도 잘리지 않고 넘쳐 보여요 (기본 스킨을 바꿔 두면 거기서 만든 스타일도 그대로 따라요).
        public static void Apply()
        {
            var o = Origin;
            GUI.matrix = Matrix4x4.TRS(new Vector3(o.x, o.y, 0f), Quaternion.identity, new Vector3(Factor, Factor, 1f));
            GUI.skin.label.clipping = TextClipping.Overflow;
            GUI.skin.button.clipping = TextClipping.Overflow;
            GUI.skin.box.clipping = TextClipping.Overflow;
        }

        // WorldToScreenPoint 결과(픽셀, 아래가 0)를 GUI 좌표(기준 단위, 위가 0)로
        public static Vector2 FromScreen(Vector3 screenPoint)
        {
            var o = Origin;
            return new Vector2((screenPoint.x - o.x) / Factor, (Screen.height - screenPoint.y - o.y) / Factor);
        }
    }
}
