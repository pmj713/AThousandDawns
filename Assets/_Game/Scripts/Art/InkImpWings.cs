using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 임프 날갯짓: 모델의 LeftWing · RightWing 뼈(prepare_enemy.py "wings")를 동작 클립 위에 덧돌려 위아래로 퍼덕여요.
    // 몸 앞 방향 둘레로 양쪽이 거울처럼 (빠르게 쳐 올렸다 내리기). 쓰러지면 멈춰요. 씬 빌더(PrototypeSceneBuilder.InkEnemy.cs)가 임프 프리팹에 붙여요
    public class InkImpWings : MonoBehaviour
    {
        [SerializeField] float beatsPerSecond = 3.2f;
        [SerializeField] float amplitude = 32f;

        Transform _left, _right;
        Quaternion _leftRest, _rightRest;
        EnemyBrain _brain;
        float _phase;

        void Start()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LeftWing") _left = t;
                else if (t.name == "RightWing") _right = t;
            }
            if (_left != null) _leftRest = _left.localRotation;
            if (_right != null) _rightRest = _right.localRotation;
            _brain = GetComponentInParent<EnemyBrain>();
            _phase = Random.value * 6.28f;
        }

        void LateUpdate()
        {
            if (_left == null || _right == null) return;
            if (_brain != null && _brain.IsDead) return;
            float s = Mathf.Sin(Time.time * beatsPerSecond * 2f * Mathf.PI + _phase);
            float angle = amplitude * (s > 0f ? s : s * 0.6f);   // 쳐 올릴 때 크게, 내릴 때 조금
            var fwd = transform.forward;
            Flap(_left, _leftRest, angle, fwd);
            Flap(_right, _rightRest, -angle, fwd);
        }

        static void Flap(Transform wing, Quaternion rest, float angle, Vector3 axis)
        {
            var parent = wing.parent;
            var restWorld = parent != null ? parent.rotation * rest : rest;
            wing.rotation = Quaternion.AngleAxis(angle, axis) * restWorld;
        }
    }
}
