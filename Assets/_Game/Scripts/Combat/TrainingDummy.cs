using UnityEngine;

namespace AThousandDawns.Combat
{
    // 목인장: 첫 타부터 피해를 재고, 3초 동안 안 맞으면 결과를 고정해요. 다시 때리면 새로 재요.
    [RequireComponent(typeof(Damageable))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        [SerializeField] string label = "목인장";
        [SerializeField] float idleResetSeconds = 3f;

        Damageable _damageable;
        float _startTime = -1f;
        float _lastHitTime = -1f;
        int _total;

        public string Label => label;
        public int Total => _total;
        public float Elapsed => _startTime < 0f ? 0f : Mathf.Max(0.001f, (IsMeasuring ? Time.time : _lastHitTime) - _startTime);
        public bool IsMeasuring => _startTime >= 0f && Time.time - _lastHitTime < idleResetSeconds;
        public float Dps => _startTime < 0f ? 0f : _total / Mathf.Max(Elapsed, 0.5f);

        void Awake()
        {
            _damageable = GetComponent<Damageable>();
            _damageable.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
        }

        void OnDamaged(Damageable _, int amount)
        {
            if (!IsMeasuring)
            {
                _startTime = Time.time;
                _total = 0;
            }
            _total += amount;
            _lastHitTime = Time.time;
        }
    }
}
