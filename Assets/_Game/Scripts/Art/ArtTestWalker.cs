using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Art
{
    // 아트 시험 씬용: WASD(또는 방향키)로 캡슐을 걸려 보고, 카메라가 처음 구도 그대로 따라와요.
    // 벽·대나무 충돌체에 막혀요. 전투·대시 같은 게임 기능은 없어요 (보기 확인용).
    [RequireComponent(typeof(CharacterController))]
    public class ArtTestWalker : MonoBehaviour
    {
        [SerializeField] float speed = 6f;
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float cameraFollow = 8f;

        CharacterController _controller;
        Transform _camera;
        Vector3 _cameraOffset;
        float _fallSpeed;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
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
            Vector3 input = Vector3.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.z += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.z -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            }
            if (input.sqrMagnitude > 1f) input.Normalize();

            _fallSpeed = _controller.isGrounded ? -1f : _fallSpeed - 20f * Time.deltaTime;
            _controller.Move((input * speed + Vector3.up * _fallSpeed) * Time.deltaTime);

            if (input.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(input, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            float t = 1f - Mathf.Exp(-cameraFollow * Time.deltaTime);
            _camera.position = Vector3.Lerp(_camera.position, transform.position + _cameraOffset, t);
        }
    }
}
