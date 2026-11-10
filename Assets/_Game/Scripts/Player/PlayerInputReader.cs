using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Player
{
    // 시험판 입력 (기본): WASD 이동, 좌클릭 평타(누르고 있기), 우클릭 초식, Q 절초, Space 대시, R 금창약(누르고 있기). 조준은 마우스가 가리키는 바닥.
    // 키보드·마우스 키는 설정에서 바꿀 수 있어요 (KeyBindings).
    public sealed class PlayerInputReader : MonoBehaviour
    {
        InputAction _move;
        InputAction _attack;
        InputAction _dash;
        InputAction _skill;
        InputAction _ultimate;
        InputAction _potion;
        Camera _camera;

        public Vector2 Move => _move.ReadValue<Vector2>();
        public bool AttackHeld => _attack.IsPressed();
        public bool AttackPressed => _attack.WasPressedThisFrame();
        public bool DashPressed => _dash.WasPressedThisFrame();
        public bool SkillPressed => _skill.WasPressedThisFrame();
        public bool UltimatePressed => _ultimate.WasPressedThisFrame();
        public bool PotionPressed => _potion.WasPressedThisFrame();
        public bool PotionHeld => _potion.IsPressed();

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

            _skill = new InputAction("Skill", InputActionType.Button, "<Mouse>/rightButton");
            _skill.AddBinding("<Gamepad>/buttonNorth");

            _ultimate = new InputAction("Ultimate", InputActionType.Button, "<Keyboard>/q");
            _ultimate.AddBinding("<Gamepad>/rightShoulder");

            _potion = new InputAction("Potion", InputActionType.Button, "<Keyboard>/r");
            _potion.AddBinding("<Gamepad>/leftShoulder");
            ApplyKeys();
            AThousandDawns.Core.GameSettings.Changed += ApplyKeys;
        }

        // 설정 → 조작 키: 키보드·마우스 바인딩(각 동작의 첫 바인딩, 이동은 2DVector 네 방향)을 바꿔요. 게임패드 바인딩은 그대로.
        void ApplyKeys()
        {
            string P(AThousandDawns.Core.Bind b) => AThousandDawns.Core.KeyBindings.Path(b);
            _move.ApplyBindingOverride(1, P(AThousandDawns.Core.Bind.MoveUp));
            _move.ApplyBindingOverride(2, P(AThousandDawns.Core.Bind.MoveDown));
            _move.ApplyBindingOverride(3, P(AThousandDawns.Core.Bind.MoveLeft));
            _move.ApplyBindingOverride(4, P(AThousandDawns.Core.Bind.MoveRight));
            _attack.ApplyBindingOverride(0, P(AThousandDawns.Core.Bind.Attack));
            _skill.ApplyBindingOverride(0, P(AThousandDawns.Core.Bind.Skill));
            _dash.ApplyBindingOverride(0, P(AThousandDawns.Core.Bind.Dash));
            _ultimate.ApplyBindingOverride(0, P(AThousandDawns.Core.Bind.Ultimate));
            _potion.ApplyBindingOverride(0, P(AThousandDawns.Core.Bind.Potion));
        }

        void OnEnable()
        {
            _move.Enable();
            _attack.Enable();
            _dash.Enable();
            _skill.Enable();
            _ultimate.Enable();
            _potion.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _dash.Disable();
            _skill.Disable();
            _ultimate.Disable();
            _potion.Disable();
        }

        void OnDestroy()
        {
            AThousandDawns.Core.GameSettings.Changed -= ApplyKeys;
            _move.Dispose();
            _attack.Dispose();
            _dash.Dispose();
            _skill.Dispose();
            _ultimate.Dispose();
            _potion.Dispose();
        }

        // 마우스가 가리키는 바닥(y = 0) 위치
        public bool TryGetAimPoint(out Vector3 point)
        {
            point = default;
            if (AThousandDawns.Core.GameSettings.Current.aim == AThousandDawns.Core.GameSettings.AimMode.Move) return false; // 설정: 조준 = 이동 방향 (바라보는 쪽 그대로)
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
