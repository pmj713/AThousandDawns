using System.Collections;
using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 화약 산적: 1.5칸 안에서 점화(예고 1초) → 1초 동안 속도 70%로 쫓아오다 폭발 (반경 2칸, 다른 적·목인장도 맞음). 한 번 터지면 끝.
    // 먼저 맞히면(쓰러뜨려도) 0.5초 뒤 그 자리에서 터져요. 화상으로 터지는 건 화상이 생기면 붙여요.
    public sealed class BomberBrain : EnemyBrain
    {
        public override string AttackName => "자폭";

        float _igniteDistance, _fuseMoveRatio, _hitFuse, _hitFuseElapsed;
        bool _exploded, _hitFusing;

        public override EnemyKind Kind => EnemyKind.Bomber;
        protected override string Key => "bomber";
        protected override float ActiveTime => 0.05f;
        protected override bool RadialTelegraph => true;
        protected override float DeathDelay => _hitFuse + 0.15f;

        public bool Exploded => _exploded;

        protected override void ReadTuning()
        {
            _igniteDistance = Tuning("ignite_distance", 1.5f);
            _fuseMoveRatio = Tuning("fuse_move_ratio", 0.7f);
            _hitFuse = Tuning("hit_fuse", 0.5f);
        }

        protected override bool WantsToAttack(float distance) => !_hitFusing && distance <= _igniteDistance;

        protected override void TickMovement(float dt, bool canAttack)
        {
            if (_hitFusing) return;
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);

            if (canAttack)
            {
                Move(dir * MoveSpeed, dt);
                return;
            }
            float ring = _igniteDistance + 2f;
            Vector3 velocity = Strafe(dir) * (MoveSpeed * 0.5f);
            if (d > ring + 0.5f) velocity = dir * MoveSpeed;
            else if (d < ring - 0.5f) velocity = -dir * (MoveSpeed * 0.6f);
            Move(velocity, dt);
        }

        // 점화 뒤 예고 내내 속도 70%로 쫓아와요
        protected override void TickWindup(float dt, bool tracking)
        {
            Vector3 to = ToPlayer();
            FaceToward(to);
            if (to.magnitude > BodyRadius + PlayerRadius) Move(to.normalized * (MoveSpeed * _fuseMoveRatio), dt);
        }

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Circle(AttackRange);

        protected override void OnActiveStarted() => Explode();

        protected override void TickActive(float dt) { }

        protected override void OnHitTaken() => StartHitFuse();

        protected override void OnDied()
        {
            if (_exploded) return;
            if (_hitFusing) Telegraph.Show(TelegraphShapes.Circle(AttackRange), true); // Die가 예고를 숨겨서 다시 켜요
            else StartHitFuse();
        }

        void StartHitFuse()
        {
            if (_exploded || _hitFusing) return;
            _hitFusing = true;
            Sfx.Play(SfxId.FuseHiss);
            if (Attack.IsAttacking) CancelAttack();
            Telegraph.Show(TelegraphShapes.Circle(AttackRange), true);
            Telegraph.SetPose(transform.position, Vector3.forward);
            StartCoroutine(HitFuse());
        }

        IEnumerator HitFuse()
        {
            _hitFuseElapsed = 0f;
            while (_hitFuseElapsed < _hitFuse)
            {
                _hitFuseElapsed += Time.deltaTime;
                Telegraph.SetProgress(_hitFuseElapsed / _hitFuse);
                yield return null;
            }
            Explode();
        }

        void Explode()
        {
            if (_exploded) return;
            _exploded = true;
            Telegraph.Hide();
            Director.SpawnBurst(transform.position, AttackRange);
            if (Player != null && !Player.IsDead && ToPlayer().magnitude <= AttackRange + PlayerRadius) TryHitPlayer();
            Director.Blast(transform.position, AttackRange, AttackPower, Body);
            Die(); // 이미 쓰러져 있으면 아무 일도 없어요
        }
    }
}
