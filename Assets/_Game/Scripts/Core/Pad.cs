using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Core
{
    // 게임패드로 창 고르기 (이벤트 · 갈림길 · 기물 카드 · 버릴 기물 · 진열대 · 매각 · 결과 · 엔딩).
    //   십자키 (또는 왼쪽 스틱을 한 번 젖힘) 고르기 · A(남) 고르기 · B(동) 닫기 · Y(북) 회귀록 (결과 화면).
    // Active: 마지막으로 쓴 입력이 패드인지 (창이 고른 칸을 표시할지 정해요. 마우스를 움직이면 꺼져요)
    public static class Pad
    {
        static Vector2 _lastStick;
        static int _stickFrame = -1;
        static Vector2Int _stickDir;

        public static bool Active { get; private set; }

        static Gamepad G => Gamepad.current;

        public static bool Up => Dir().y > 0;
        public static bool Down => Dir().y < 0;
        public static bool Left => Dir().x < 0;
        public static bool Right => Dir().x > 0;
        public static bool Confirm => Pressed(G?.buttonSouth);
        public static bool Back => Pressed(G?.buttonEast);
        public static bool North => Pressed(G?.buttonNorth);
        public static bool Start => Pressed(G?.startButton);

        static bool Pressed(UnityEngine.InputSystem.Controls.ButtonControl b)
        {
            if (b == null || !b.wasPressedThisFrame) return false;
            Active = true;
            return true;
        }

        // 이번 프레임에 누른 방향 (십자키, 또는 스틱이 가운데에서 한쪽으로 넘어간 순간)
        static Vector2Int Dir()
        {
            if (_stickFrame == Time.frameCount) return _stickDir;
            _stickFrame = Time.frameCount;
            _stickDir = Vector2Int.zero;
            var g = G;
            if (g == null) return _stickDir;
            if (g.dpad.up.wasPressedThisFrame) _stickDir.y = 1;
            else if (g.dpad.down.wasPressedThisFrame) _stickDir.y = -1;
            if (g.dpad.left.wasPressedThisFrame) _stickDir.x = -1;
            else if (g.dpad.right.wasPressedThisFrame) _stickDir.x = 1;
            Vector2 s = g.leftStick.ReadValue();
            if (_stickDir == Vector2Int.zero)
            {
                if (s.y > 0.6f && _lastStick.y <= 0.6f) _stickDir.y = 1;
                else if (s.y < -0.6f && _lastStick.y >= -0.6f) _stickDir.y = -1;
                else if (s.x < -0.6f && _lastStick.x >= -0.6f) _stickDir.x = -1;
                else if (s.x > 0.6f && _lastStick.x <= 0.6f) _stickDir.x = 1;
            }
            _lastStick = s;
            if (_stickDir != Vector2Int.zero) Active = true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 4f) Active = false;
            return _stickDir;
        }

        // 위아래(또는 좌우) 한 칸 옮기기. 고를 수 없는 칸(skip)은 건너뛰어요
        public static int Step(int cursor, int count, bool horizontal = false, System.Func<int, bool> skip = null)
        {
            if (count <= 0) return 0;
            int step = horizontal ? (Left ? -1 : Right ? 1 : 0) : (Up ? -1 : Down ? 1 : 0);
            cursor = Mathf.Clamp(cursor, 0, count - 1);
            if (step == 0) return cursor;
            for (int i = 0; i < count; i++)
            {
                cursor = (cursor + step + count) % count;
                if (skip == null || !skip(cursor)) break;
            }
            return cursor;
        }
    }
}
