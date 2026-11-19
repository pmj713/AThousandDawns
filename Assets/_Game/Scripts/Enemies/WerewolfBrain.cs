using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 늑대인간 (차원 겹침, 판타지): 뛰어들지 않고 다가와서 할퀴기 3연타 (플레이테스트 11차).
    //   예고 뒤 앞으로 걸어 나가며 werewolf.claw_gap초 간격으로 claw_count번 할퀴어요 (부채꼴 claw_radius · claw_angle, 한 번에 claw_step칸 전진).
    //   할퀼 때마다 노리는 쪽으로 claw_turn°까지만 돌아요 (옆으로 비키면 피해요). 한 번 피해 = 공격 × claw_ratio
    //   체력 werewolf.enrage_hp 아래로 떨어지면 광폭화: 이동 werewolf.enrage_speed배 · 공격 간격 werewolf.enrage_interval배 (한 번만)
    public sealed class WerewolfBrain : EnemyBrain
    {
        float _clawRadius, _clawAngle, _clawGap, _clawStep, _clawRatio, _clawTurn, _clawTimer;
        int _claws, _clawIndex;

        public override EnemyKind Kind => EnemyKind.Werewolf;
        protected override string Key => "werewolf";
        public override string AttackName => "할퀴기 3연타";
        protected override float ActiveTime => float.PositiveInfinity; // 마지막 할퀴기가 끝나면 EndActive
        protected override float TrackStopDistance => _clawRadius * 0.7f;

        public static event System.Action<WerewolfBrain> Enraged; // 광폭화 (아트: 포효 · 붉은 김)
        // 할퀴는 순간마다 (아트: 발톱 자국 · 휘두르는 팔): 누가, 어디서, 방향, 몇 번째 (0부터, 왼손 · 오른손 번갈아)
        public static event System.Action<WerewolfBrain, Vector3, Vector3, int> Clawed;
        public bool IsEnraged { get; private set; }
        public int ClawCount => _claws;

        protected override void ReadTuning()
        {
            _claws = Mathf.Max(1, Mathf.RoundToInt(Tuning("claw_count", 3f)));
            _clawGap = Mathf.Max(0.05f, Tuning("claw_gap", 0.25f));
            _clawRadius = Tuning("claw_radius", 1.6f);
            _clawAngle = Tuning("claw_angle", 100f);
            _clawStep = Tuning("claw_step", 0.4f);
            _clawRatio = Tuning("claw_ratio", 0.5f);
            _clawTurn = Tuning("claw_turn", 25f);
        }

        protected override bool WantsToAttack(float distance) => distance <= AttackRange;

        // 다가와요. 공격 간격 동안은 사거리 바깥을 옆으로 돌아요
        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);

            if (canAttack)
            {
                Move(dir * MoveSpeed, dt);
                return;
            }

            float ring = AttackRange + 1.5f;
            Vector3 velocity = Strafe(dir) * (MoveSpeed * 0.5f);
            if (d > ring + 0.5f) velocity = dir * MoveSpeed;
            else if (d < ring - 0.5f) velocity = -dir * (MoveSpeed * 0.6f) + velocity * 0.5f;
            Move(velocity, dt);
        }

        // 걸어 나가며 할퀴는 자리 전체 (첫 할퀴기부터 마지막 전진까지)
        protected override List<Vector2> TelegraphShape() =>
            TelegraphShapes.Lunge(_clawRadius, _clawAngle, _clawStep * (_claws - 1));

        protected override void OnActiveStarted()
        {
            _clawIndex = 0;
            Claw();
        }

        protected override void TickActive(float dt)
        {
            if (_clawIndex >= _claws) return;
            // 다음 할퀴기까지 앞으로 걸어 나가요 (플레이어 몸에 닿으면 더 밀고 들어가지 않아요)
            Vector3 to = ToRealPlayer();
            bool touching = Vector3.Dot(to, AimDirection) > 0f && to.magnitude < BodyRadius + PlayerRadius + 0.2f;
            if (!touching) MoveRaw(AimDirection * (_clawStep / _clawGap * dt));
            _clawTimer -= dt;
            if (_clawTimer <= 0f) Claw();
        }

        void Claw()
        {
            if (_clawIndex > 0) // 할퀼 때마다 노리는 쪽으로 조금만 돌아요
            {
                Vector3 to = ToPlayer();
                if (to.sqrMagnitude > 1e-4f) AimDirection = Vector3.RotateTowards(AimDirection, to.normalized, _clawTurn * Mathf.Deg2Rad, 0f);
                FaceToward(AimDirection);
            }
            Clawed?.Invoke(this, transform.position, AimDirection, _clawIndex);
            // 판정은 언제나 진짜 플레이어 자리로 (다른 적을 노릴 때 멀리 있는 플레이어가 맞지 않게)
            if (HitShapes.InSector(transform.position, AimDirection, _clawRadius, _clawAngle, Player.transform.position, PlayerRadius)
                && Player.TryReceiveHit(AttackPower * _clawRatio, transform.position, HitKnockback, Body))
                Sfx.Play(SfxId.HoundBite);
            _clawIndex++;
            _clawTimer = _clawGap;
            if (_clawIndex >= _claws) Attack.EndActive();
        }

        protected override void OnHitTaken()
        {
            if (IsEnraged || Body.Health > Body.MaxHealth * Tuning("enrage_hp", 0.5f)) return;
            IsEnraged = true;
            MoveSpeed *= Tuning("enrage_speed", 1.4f);
            AttackInterval *= Tuning("enrage_interval", 0.6f);
            Enraged?.Invoke(this);
        }
    }
}
