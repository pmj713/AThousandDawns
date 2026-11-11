using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 등불 귀신 (폐사찰, 몬스터 시트 45행): 터지지 않고 플레이어 둘레 2.5칸쯤을 떠돌며, 지나간 자리에 도깨비불 장판을 남겨요.
    //   장판: 0.7초마다 하나 (반경 0.9칸, 3.5초). 안에 서 있으면 0.5초마다 ×0.5 + 감전 (잠시 이동속도 −40%).
    //   쓰러지면 불씨가 둘로 갈라져 양옆에 장판 두 개. 직접 때리지는 않아요 (공격 토큰 없음).
    public sealed class LanternBrain : EnemyBrain
    {
        float _orbit, _gap, _radius, _life, _ratio, _slow, _slowTime, _timer, _split;
        Vector3 _lastDrop;

        public override EnemyKind Kind => EnemyKind.Lantern;
        protected override string Key => "lantern";
        public override string AttackName => "도깨비불";
        protected override bool UsesToken => false;
        protected override float ActiveTime => 0.1f;

        public int FiresDropped { get; private set; }

        protected override void ReadTuning()
        {
            _orbit = Tuning("orbit", 2.5f);
            _gap = Tuning("fire_gap", 0.7f);
            _radius = Tuning("fire_radius", 0.9f);
            _life = Tuning("fire_time", 3.5f);
            _ratio = Tuning("fire_ratio", 0.5f);
            _slow = Tuning("shock_slow", 0.4f);
            _slowTime = Tuning("shock_time", 1.5f);
            _split = Tuning("split_distance", 1.2f);
        }

        protected override void OnWoke() => _lastDrop = transform.position;

        protected override bool WantsToAttack(float distance) => false;

        // 둘레를 돌며 다가갔다 물러났다 (장판으로 길을 막아요)
        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            Vector3 velocity = Strafe(dir) * MoveSpeed;
            if (d > _orbit + 0.5f) velocity += dir * MoveSpeed;
            else if (d < _orbit - 0.5f) velocity -= dir * MoveSpeed * 0.6f;
            Move(Vector3.ClampMagnitude(velocity, MoveSpeed), dt);

            _timer -= dt;
            if (_timer > 0f || (transform.position - _lastDrop).magnitude < _radius * 0.8f) return;
            _timer = _gap;
            Drop(transform.position);
        }

        void Drop(Vector3 at)
        {
            _lastDrop = at;
            FiresDropped++;
            GhostFire.Create(at, _radius, _life, AttackPower * _ratio, _slow, _slowTime, Player, Body);
        }

        // 불씨가 둘로 갈라져요
        protected override void OnDied()
        {
            Vector3 side = Vector3.Cross(Vector3.up, Facing).normalized * _split;
            Drop(transform.position + side);
            Drop(transform.position - side);
        }

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Circle(0.5f);

        protected override void TickActive(float dt) { }
    }
}
