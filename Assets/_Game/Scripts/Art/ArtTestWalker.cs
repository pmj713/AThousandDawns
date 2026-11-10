using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Art
{
    // 아트 시험 씬용: WASD(또는 방향키)로 걸려 보고, 카메라가 처음 구도 그대로 따라와요. 벽·대나무 충돌체에 막혀요.
    // 동작 확인용으로 게임의 평타 3연타·대시 박자만 흉내 내요 (판정·피해 없음). 값은 data_balance.csv와 같게 맞췄어요.
    //   마우스 왼쪽·J: 평타 (1·2타 0.32초, 3타 0.5초 + 반 칸 전진). 후딜이 끝나고 0.25초 안에 누르거나, 공격 중에 미리 누르면 다음 타로 이어져요
    //   Space: 대시 (0.18초에 4칸, 누른 방향 또는 바라보는 방향). 공격 중에도 끊고 나가요
    // 자식 애니메이터에 Speed(float)·Combo(int)·Attack(trigger)·Dash(trigger)를 넘겨요.
    [RequireComponent(typeof(CharacterController))]
    public class ArtTestWalker : MonoBehaviour
    {
        [SerializeField] float speed = 6f;
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float cameraFollow = 8f;

        static readonly float[] ComboDurations = { 0.32f, 0.32f, 0.5f };
        const float ChainWindow = 0.25f;
        const float Combo3LungeStart = 0.14f, Combo3LungeTime = 0.08f, Combo3Lunge = 0.5f;
        const float DashDistance = 4f, DashDuration = 0.18f;

        CharacterController _controller;
        Transform _camera;
        Vector3 _cameraOffset;
        float _fallSpeed;
        Animator _animator;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int ComboId = Animator.StringToHash("Combo");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int DashId = Animator.StringToHash("Dash");

        int _attack;            // 지금 휘두르는 타 (1~3), 0 = 공격 중 아님
        float _attackTime;
        bool _queued;           // 공격 중에 미리 누른 다음 타
        int _lastAttack;
        float _lastAttackEnd = -10f;
        float _dashTimer;
        Vector3 _dashDirection;

        bool Animated => _animator != null && _animator.runtimeAnimatorController != null;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
        }

        void Start()
        {
            if (Camera.main == null) return;
            _camera = Camera.main.transform;
            _cameraOffset = _camera.position - transform.position;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            Vector3 input = Vector3.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.z += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.z -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            }
            if (input.sqrMagnitude > 1f) input.Normalize();
            bool attackPressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (keyboard != null && keyboard.jKey.wasPressedThisFrame);
            bool dashPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;

            _fallSpeed = _controller.isGrounded ? -1f : _fallSpeed - 20f * Time.deltaTime;
            Vector3 move = Vector3.zero;

            if (dashPressed && _dashTimer <= 0f)
            {
                _dashDirection = input.sqrMagnitude > 0.01f ? input.normalized : transform.forward;
                _dashTimer = DashDuration;
                _attack = 0; _queued = false;
                transform.rotation = Quaternion.LookRotation(_dashDirection, Vector3.up);
                if (Animated) _animator.SetTrigger(DashId);
            }

            if (_dashTimer > 0f)
            {
                _dashTimer -= Time.deltaTime;
                move = _dashDirection * (DashDistance / DashDuration);
            }
            else
            {
                if (attackPressed)
                {
                    if (_attack > 0) _queued = true;
                    else StartAttack(Time.time - _lastAttackEnd <= ChainWindow && _lastAttack < 3 ? _lastAttack + 1 : 1, input);
                }

                if (_attack > 0)
                {
                    _attackTime += Time.deltaTime;
                    if (_attack == 3 && _attackTime > Combo3LungeStart && _attackTime < Combo3LungeStart + Combo3LungeTime)
                        move = transform.forward * (Combo3Lunge / Combo3LungeTime);
                    if (_attackTime >= ComboDurations[_attack - 1])
                    {
                        _lastAttack = _attack; _lastAttackEnd = Time.time; _attack = 0;
                        if (_queued) StartAttack(_lastAttack < 3 ? _lastAttack + 1 : 1, input);
                    }
                }
                else
                {
                    move = input * speed;
                    if (input.sqrMagnitude > 0.01f)
                    {
                        var target = Quaternion.LookRotation(input, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
                    }
                }
            }

            _controller.Move((move + Vector3.up * _fallSpeed) * Time.deltaTime);
            if (Animated) _animator.SetFloat(SpeedId, _attack > 0 || _dashTimer > 0f ? 0f : input.magnitude * speed);
        }

        void StartAttack(int index, Vector3 input)
        {
            _attack = index; _attackTime = 0f; _queued = false;
            if (input.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(input, Vector3.up);   // 누른 방향으로 휘둘러요
            if (!Animated) return;
            _animator.SetInteger(ComboId, index);
            _animator.SetTrigger(AttackId);
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            float t = 1f - Mathf.Exp(-cameraFollow * Time.deltaTime);
            _camera.position = Vector3.Lerp(_camera.position, transform.position + _cameraOffset, t);
        }
    }
}
