using UnityEngine;

namespace AThousandDawns.Prototype
{
    // IMGUI 글자에 마우스를 올려도 색이 바뀌지 않게 (기본 스킨은 올리면 흰색이 돼요): hover · active · focused · on* 글자색을 normal과 같게
    public static class UiStyle
    {
        static bool _skinDone;

        public static GUIStyle Steady(GUIStyle s)
        {
            if (!_skinDone && GUI.skin != null) // GUI.Button(rect, text)처럼 스타일 없이 그리는 것도
            {
                _skinDone = true;
                Copy(GUI.skin.button);
                Copy(GUI.skin.label);
                Copy(GUI.skin.toggle);
                Copy(GUI.skin.box);
            }
            if (s != null) Copy(s);
            return s;
        }

        static void Copy(GUIStyle s)
        {
            var c = s.normal.textColor;
            s.hover.textColor = s.active.textColor = s.focused.textColor = c;
            s.onNormal.textColor = s.onHover.textColor = s.onActive.textColor = s.onFocused.textColor = c;
        }
    }
}
