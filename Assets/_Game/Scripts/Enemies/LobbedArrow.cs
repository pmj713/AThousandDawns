using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 궁수 곡사 화살: 활 높이에서 떨어질 자리까지 포물선으로 날아가고 (엄폐물을 넘어가요), 그동안 바닥 원 예고가 차올라요.
    //   떨어지는 순간 원 안의 플레이어만 맞아요 (다른 적은 맞지 않아요). 쏜 궁수가 쓰러져도 마저 떨어져요. 방을 옮기면 사라져요.
    public sealed class LobbedArrow : MonoBehaviour
    {
        PlayerController _player;
        Damageable _owner;
        Telegraph _marker;
        Vector3 _from, _to;
        float _flight, _elapsed, _radius, _attack, _apex;
        bool _landed;

        public Damageable Owner => _owner; // 아트: 쏜 적
        public Vector3 Target => _to;
        public float Radius => _radius;
        public float Progress => Mathf.Clamp01(_elapsed / _flight);

        public static LobbedArrow Launch(EnemyDirector director, Vector3 from, Vector3 to, float flightTime, float radius, float attack, float apex, Damageable owner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "LobbedArrow";
            EnemyFactory.StripCollider(go);
            go.transform.position = from;
            go.transform.localScale = new Vector3(0.08f, 0.08f, 0.7f);
            var renderer = go.GetComponent<Renderer>();
            var v = director.Visuals;
            if (v.arrow != null) renderer.sharedMaterial = v.arrow;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var shot = go.AddComponent<LobbedArrow>();
            shot._player = director.Player;
            shot._owner = owner;
            shot._from = from;
            shot._to = new Vector3(to.x, 0f, to.z);
            shot._flight = Mathf.Max(0.05f, flightTime);
            shot._radius = radius;
            shot._attack = attack;
            shot._apex = apex;

            shot._marker = Telegraph.Create(null, v.telegraphBase, v.telegraphFill, v.telegraphOutline);
            shot._marker.Show(TelegraphShapes.Circle(radius), true);
            shot._marker.SetPose(shot._to, Vector3.forward);
            shot._marker.SetProgress(0f);
            return shot;
        }

        void OnEnable() => EnemyDirector.RoomReset += OnRoomReset;
        void OnDisable() => EnemyDirector.RoomReset -= OnRoomReset;

        void OnRoomReset()
        {
            if (this != null) Destroy(gameObject);
        }

        void Update()
        {
            if (_landed) return;
            _elapsed += Time.deltaTime;
            float t = Progress;
            Vector3 p = Vector3.Lerp(_from, _to, t) + Vector3.up * (4f * _apex * t * (1f - t));
            Vector3 step = p - transform.position;
            if (step.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(step);
            transform.position = p;
            if (_marker != null) _marker.SetProgress(t);
            if (t >= 1f) Land();
        }

        void Land()
        {
            _landed = true;
            bool hit = false;
            if (_player != null && !_player.IsDead)
            {
                Vector3 d = _player.transform.position - _to;
                d.y = 0f;
                if (d.magnitude <= _radius + _player.BodyRadius) hit = _player.TryReceiveHit(_attack, _to, -1f, _owner);
            }
            GoblinArcherBrain.RaiseLanded(this, _to, _radius, hit);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_marker != null) Destroy(_marker.gameObject);
        }
    }
}
