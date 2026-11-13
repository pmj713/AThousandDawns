using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 망령 (폐사찰, 몬스터 시트 41행 "벽 통과 유도 구체"): 5~7칸을 두고 떠돌다가, 예고 뒤 혼불 하나를 쏴요.
    // 혼불은 느리게 플레이어 쪽으로 꺾이며 벽을 지나와요. 닿으면 사라지고, 대시(무적)로 닿아도 맞지 않고 사라져요.
    public sealed class WraithBrain : EnemyBrain
    {
        const float ShotHeight = 1f;

        float _keepMin, _keepMax, _orbSpeed, _orbTurn, _orbLife, _orbRadius;

        public override EnemyKind Kind => EnemyKind.Wraith;
        protected override string Key => "wraith";
        public override string AttackName => "혼불";
        protected override bool IsRanged => true;
        protected override float ActiveTime => 0.2f;
        protected override bool RadialTelegraph => true;

        protected override void ReadTuning()
        {
            _keepMin = Tuning("keep_min", 5f);
            _keepMax = Tuning("keep_max", 7f);
            _orbSpeed = Tuning("orb_speed", 4f);
            _orbTurn = Tuning("orb_turn", 90f);
            _orbLife = Tuning("orb_life", 4f);
            _orbRadius = Tuning("orb_radius", 0.35f);
        }

        protected override bool WantsToAttack(float distance) => distance <= AttackRange; // 벽 너머로도 쏴요

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            if (d < _keepMin) Move(-dir * MoveSpeed, dt);
            else if (d > _keepMax) Move(dir * MoveSpeed, dt);
            else Move(Strafe(dir) * (MoveSpeed * 0.5f), dt);
        }

        // 혼불을 모으는 동안 제자리에서 노려봐요
        protected override void TickWindup(float dt, bool tracking) => FaceToward(ToPlayer());

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Circle(0.8f);

        protected override void OnActiveStarted()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "혼불";
            EnemyFactory.StripCollider(go);
            go.transform.position = transform.position + Vector3.up * ShotHeight + Facing * (BodyRadius + 0.3f);
            go.transform.localScale = Vector3.one * (_orbRadius * 2f);
            var renderer = go.GetComponent<Renderer>();
            renderer.material.color = new Color(0.65f, 0.85f, 1f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<WraithOrb>().Launch(Player, Facing, _orbSpeed, _orbTurn, _orbLife, _orbRadius, AttackPower, Body);
        }

        protected override void TickActive(float dt) { }
    }

    // 망령의 혼불: 플레이어 쪽으로 초당 turn도씩 꺾이며 날아가요. 벽은 그냥 지나가요.
    public sealed class WraithOrb : MonoBehaviour
    {
        PlayerController _target;
        Vector3 _direction;
        float _speed, _turn, _life, _radius, _attack;
        Damageable _owner;

        public void Launch(PlayerController target, Vector3 direction, float speed, float turnDegrees, float life, float radius, float attack, Damageable owner)
        {
            _target = target;
            _direction = new Vector3(direction.x, 0f, direction.z).normalized;
            _speed = speed;
            _turn = turnDegrees;
            _life = life;
            _radius = radius;
            _attack = attack;
            _owner = owner;
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f || _target == null || _target.IsDead || _owner == null || _owner.IsDead) // 망령을 쓰러뜨리면 쫓아오던 혼불도 꺼져요
            {
                Destroy(gameObject);
                return;
            }
            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-4f)
                _direction = Vector3.RotateTowards(_direction, to.normalized, _turn * Mathf.Deg2Rad * Time.deltaTime, 0f);
            transform.position += _direction * (_speed * Time.deltaTime);

            if (to.magnitude <= _radius + _target.BodyRadius)
            {
                _target.TryReceiveHit(_attack, transform.position, -1f, _owner); // 대시 무적이면 안 맞고 사라져요
                Destroy(gameObject);
            }
        }
    }
}
