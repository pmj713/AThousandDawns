using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AThousandDawns.Core
{
    // 바꿀 수 있는 키 (설정 → 조작 키). 값은 Input System 경로 ("<Keyboard>/f", "<Mouse>/leftButton")로 settings.json에 저장해요.
    // 일시정지(Esc)와 창 안의 위아래·Enter는 고정이에요. 게임패드는 고정 배치 (PadPressed).
    public enum Bind { MoveUp, MoveDown, MoveLeft, MoveRight, Attack, Skill, Dash, Ultimate, Potion, Interact, Meridian }

    public static class KeyBindings
    {
        public static readonly string[] Defaults =
        {
            "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d",
            "<Mouse>/leftButton", "<Mouse>/rightButton", "<Keyboard>/space", "<Keyboard>/q", "<Keyboard>/r",
            "<Keyboard>/f", "<Keyboard>/e",
        };

        public static readonly string[] Names =
        {
            "위로 이동", "아래로 이동", "왼쪽 이동", "오른쪽 이동",
            "평타", "초식", "대시", "절초", "금창약", "상호작용", "경맥도",
        };

        public static int Count => Defaults.Length;

        // 지금 설정된 경로 (비었거나 잘못됐으면 기본값)
        public static string Path(Bind bind)
        {
            var keys = GameSettings.Current.keys;
            int i = (int)bind;
            return keys != null && i < keys.Length && !string.IsNullOrEmpty(keys[i]) ? keys[i] : Defaults[i];
        }

        // 이번 프레임에 눌렀는지 (상호작용·경맥도처럼 Input Action 없이 읽는 키)
        public static bool Pressed(Bind bind) => (InputSystem.FindControl(Path(bind)) is ButtonControl b && b.wasPressedThisFrame) || PadPressed(bind);

        // 게임패드 고정 배치: 상호작용 B(동) · 경맥도 Select · 대시 A(남) (평타 · 초식 · 절초 · 금창약은 PlayerInputReader)
        static bool PadPressed(Bind bind)
        {
            var g = Gamepad.current;
            if (g == null) return false;
            var c = bind == Bind.Interact ? g.buttonEast : bind == Bind.Meridian ? g.selectButton : bind == Bind.Dash ? g.buttonSouth : null;
            return c != null && c.wasPressedThisFrame;
        }

        // 화면에 보이는 이름 ("F", "좌클릭", "Space")
        public static string Label(Bind bind) => LabelOf(Path(bind));

        public static string LabelOf(string path)
        {
            switch (path)
            {
                case "<Mouse>/leftButton": return "좌클릭";
                case "<Mouse>/rightButton": return "우클릭";
                case "<Mouse>/middleButton": return "휠 클릭";
                case "<Keyboard>/space": return "Space";
            }
            var control = InputSystem.FindControl(path);
            if (control != null && !string.IsNullOrEmpty(control.displayName)) return control.displayName.Length == 1 ? control.displayName.ToUpperInvariant() : control.displayName;
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1).ToUpperInvariant() : path;
        }

        // 키 하나를 바꿔요. 다른 동작이 이미 쓰던 키면 서로 맞바꿔요.
        public static void Set(Bind bind, string path)
        {
            var s = GameSettings.Current;
            if (s.keys == null || s.keys.Length != Count)
            {
                var fresh = new string[Count];
                for (int i = 0; i < Count; i++) fresh[i] = s.keys != null && i < s.keys.Length && !string.IsNullOrEmpty(s.keys[i]) ? s.keys[i] : Defaults[i];
                s.keys = fresh;
            }
            int me = (int)bind;
            string old = Path(bind);
            for (int i = 0; i < Count; i++)
                if (i != me && Path((Bind)i) == path) s.keys[i] = old; // 맞바꾸기
            s.keys[me] = path;
            s.Save();
        }

        public static void ResetAll()
        {
            GameSettings.Current.keys = (string[])Defaults.Clone();
            GameSettings.Current.Save();
        }

        // 지금 눌린 키보드 키나 마우스 버튼 (키 바꾸기 대기 중). 없으면 null. Esc는 취소라서 빼요.
        public static string ListenPressed()
        {
            var k = Keyboard.current;
            if (k != null)
                foreach (var key in k.allKeys)
                    if (key != null && key.wasPressedThisFrame && key.keyCode != Key.Escape) return $"<Keyboard>/{key.name}";
            var m = Mouse.current;
            if (m != null)
            {
                if (m.leftButton.wasPressedThisFrame) return "<Mouse>/leftButton";
                if (m.rightButton.wasPressedThisFrame) return "<Mouse>/rightButton";
                if (m.middleButton.wasPressedThisFrame) return "<Mouse>/middleButton";
            }
            return null;
        }
    }
}
