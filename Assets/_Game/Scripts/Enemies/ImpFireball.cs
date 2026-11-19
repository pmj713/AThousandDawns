using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 임프 화염구: 느리게 곧게 날아가다 플레이어가 가까이 오면 (trigger칸) 터져요. 벽 · 대나무에 막히거나 사거리 끝에 닿아도 터져요.
    //   폭발 반경 안의 플레이어만 맞고 화상 중첩이 붙어요 (다른 적은 맞지 않아요). 플레이어가 무적(대시)이면 피해 없이 터져요. 방을 옮기면 사라져요.
    public sealed class ImpFireball : MonoBehaviour
    {
        PlayerController _target;
        EnemyDirector _director;
        Damageable _owner;
        Vector3 _direction;
        float _speed, _remaining, _radius, _trigger, _blast, _attack;
        int _burnStacks;
        bool _done;

        public Damageable Owner => _owner; // 아트: 던진 임프
        public Vector3 Direction => _direction;
        public float BlastRadius => _blast;

        public static ImpFireball Launch(EnemyDirector director, Vector3 origin, Vector3 direction, float speed, float range, float radius,
                                         float trigger, float blast, float attack, int burnStacks, Damageable owner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImpFireball";
            EnemyFactory.StripCollider(go);
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (radius * 2f);
            var renderer = go.GetComponent<Renderer>();
            if (director.Visuals.fire != null) renderer.sharedMaterial = director.Visuals.fire;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var ball = go.AddComponent<ImpFireball>();
            ball._director = director;
            ball._target = director.Player;
            ball._owner = owner;
            ball._direction = new Vector3(direction.x, 0f, direction.z).normalized;
            ball._speed = speed;
            ball._remaining = range;
            ball._radius = radius;
            ball._trigger = trigger;
            ball._blast = blast;
            ball._attack = attack;
            ball._burnStacks = burnStacks;
            go.transform.rotation = Quaternion.LookRotation(ball._direction);
            return ball;
        }

        void OnEnable() => EnemyDirector.RoomReset += OnRoomReset;
        void OnDisable() => EnemyDirector.RoomReset -= OnRoomReset;

        void OnRoomReset()
        {
            _done = true; // 새 방에서 터지지 않게
            if (this != null) Destroy(gameObject);
        }

        void Update()
        {
            if (_done) return;
            float step = Mathf.Min(_speed * Time.deltaTime, _remaining);
            Vector3 from = transform.position;
            float free = ObstacleProbe.Distance(from, _direction, _radius, step);
            bool blocked = free < step;
            Vector3 to = from + _direction * (blocked ? free : step);
            transform.position = to;
            _remaining -= step;

            bool near = _target != null && !_target.IsDead && DistanceXZ(from, to, _target.transform.position) <= _trigger;
            if (near || blocked || _remaining <= 0f) Explode(to);
        }

        void Explode(Vector3 at)
        {
            _done = true;
            bool hit = false;
            if (_target != null && !_target.IsDead)
            {
                Vector3 d = _target.transform.position - at;
                d.y = 0f;
                if (d.magnitude <= _blast + _target.BodyRadius && _target.TryReceiveHit(_attack, at, -1f, _owner))
                {
                    hit = true;
                    float perStack = GameConfig.Get("player_burn.damage", 2f);
                    for (int i = 0; i < _burnStacks; i++) _target.AddBurnStack(perStack);
                }
            }
            Vector3 ground = new Vector3(at.x, 0f, at.z);
            if (_director != null) _director.SpawnBurst(ground, _blast, BurstKind.Fireball);
            ImpBrain.RaiseExploded(this, ground, _blast, hit);
            Destroy(gameObject);
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
