using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 청죽사가 숨는 풀숲. 안에 숨어 있으면 풀이 흔들려요 (몬스터 시트: 풀 흔들림이 단서). 충돌체는 없어서 지나갈 수 있어요.
    public sealed class AmbushBush : MonoBehaviour
    {
        [SerializeField] Transform visual;
        float _phase;

        public EnemyBrain Occupant { get; set; } // 숨어 있거나 숨으러 가는 중인 적

        public void Release(EnemyBrain owner)
        {
            if (Occupant == owner) Occupant = null;
        }

        void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            _phase = Random.value * 10f;
        }

        void Update()
        {
            if (visual == null) return;
            float amount = Occupant != null && Occupant.IsHidden ? 5f : 0f;
            visual.localRotation = Quaternion.Euler(
                Mathf.Sin(Time.time * 11f + _phase) * amount, 0f, Mathf.Sin(Time.time * 8f + _phase * 1.7f) * amount);
        }
    }
}
