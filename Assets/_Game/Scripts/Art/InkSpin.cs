using UnityEngine;

namespace AThousandDawns.Art
{
    // 관문 먹 소용돌이: 바닥에 누운 채 천천히 돌아요
    public class InkSpin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = -25f;

        public InkSpin Speed(float degrees) { degreesPerSecond = degrees; return this; }

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
