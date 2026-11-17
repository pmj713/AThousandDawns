using UnityEngine;

namespace AThousandDawns.Prototype
{
    // IMGUI 글자에 마우스를 올려도 색이 바뀌지 않게 (기본 스킨은 올리면 흰색이 돼요): hover · active · focused · on* 글자색을 normal과 같게
    public static class UiStyle
    {
        public static GUIStyle Steady(GUIStyle s) => AThousandDawns.Fx.InkUi.Steady(s); // UI 규칙 한 곳 (Fx/InkUi)으로 옮겼어요
    }
}
