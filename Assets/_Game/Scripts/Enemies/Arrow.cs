using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 궁수 화살: 직선으로 날아가다 플레이어에게 맞거나 벽·대나무·목인장에 막히면 사라져요.
    // 플레이어가 무적(대시)이면 그대로 지나가요.
    public sealed class Arrow : MonoBehaviour
    {
        PlayerController _target;
        Vector3 _direction;
        float _speed, _remaining, _radius, _attack;
        Damageable _owner; // 쏜 궁수 (#경공 6단계 간파 반격 대상)

        public void Launch(PlayerController target, Vector3 direction, float speed, float range, float radius, float attack, Damageable owner = null)
        {
            _target = target;
            _direction = new Vector3(direction.x, 0f, direction.z).normalized;
            _speed = speed;
            _remaining = range;
            _radius = radius;
            _attack = attack;
            _owner = owner;
            transform.rotation = Quaternion.LookRotation(_direction);
        }

        void Update()
        {
            float step = Mathf.Min(_speed * Time.deltaTime, _remaining);
            Vector3 from = transform.position;
            float free = ObstacleProbe.Distance(from, _direction, _radius, step);
            bool blocked = free < step;
            Vector3 to = from + _direction * (blocked ? free : step);

            if (_target != null && !_target.IsDead && DistanceXZ(from, to, _target.transform.position) <= _radius + _target.BodyRadius
                && _target.TryReceiveHit(_attack, from, -1f, _owner))
            {
                Destroy(gameObject);
                return;
            }

            transform.position = to;
            _remaining -= step;
            if (blocked || _remaining <= 0f) Destroy(gameObject);
        }

        static float DistanceXZ(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), p2 = new Vector2(p.x, p.z);
            Vector2 ab = b2 - a2;
            float t = ab.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude);
            return Vector2.Distance(a2 + ab * t, p2);
        }
    }
}
