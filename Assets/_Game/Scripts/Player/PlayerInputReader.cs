using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Player
{
    // 시험판 입력: WASD 이동, 좌클릭 평타(누르고 있기), Space 대시. 조준은 마우스가 가리키는 바닥.
    public sealed class PlayerInputReader : MonoBehaviour
    {
        InputAction _move;
        InputAction _attack;
        InputAction _dash;
        Camera _camera;

        public Vector2 Move => _move.ReadValue<Vector2>();
        public bool AttackHeld => _attack.IsPressed();
        public bool AttackPressed => _attack.WasPressedThisFrame();
        public bool DashPressed => _dash.WasPressedThisFrame();

        void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            _attack.AddBinding("<Gamepad>/buttonWest");

            _dash = new InputAction("Dash", InputActionType.Button, "<Keyboard>/space");
            _dash.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable()
        {
            _move.Enable();
            _attack.Enable();
            _dash.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _dash.Disable();
        }

        void OnDestroy()
        {
            _move.Dispose();
            _attack.Dispose();
            _dash.Dispose();
        }

        // 마우스가 가리키는 바닥(y = 0) 위치
        public bool TryGetAimPoint(out Vector3 point)
        {
            point = default;
            if (_camera == null) _camera = Camera.main;
            var mouse = Mouse.current;
            if (_camera == null || mouse == null) return false;
            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance);
            return true;
        }
    }
}
