using System.Collections.Generic;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 나팔수: 6칸 거리를 유지하며 도망치다가, 멈춰서 나팔(예고 1.5초) → 졸개 2명 호출. 방마다 최대 2번.
    // 예고 중에 맞히면 끊겨요 (예고 중 경직 규칙의 유일한 예외). 동시 공격 수에는 안 들어가요.
    public class HornBrain : EnemyBrain // 목탁승 (MoktakBrain)이 이어받아요
    {
        const float CallRadius = 1.2f;

        float _keepDistance;
        int _summonCount;

        public override EnemyKind Kind => EnemyKind.Horn;
        protected override string Key => "horn";
        protected override float ActiveTime => 0.1f;
        protected override bool UsesToken => false;
        protected override bool InterruptibleWindup => true;
        protected override bool RadialTelegraph => true;

        protected virtual EnemyKind SummonKind => EnemyKind.Grunt; // 목탁승은 망령

        protected override void ReadTuning()
        {
            _keepDistance = Tuning("keep_distance", 6f);
            _summonCount = Mathf.RoundToInt(Tuning("summon_count", 2f));
        }

        protected override bool WantsToAttack(float distance) => Director.SummonsLeft > 0;

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);

            if (d < _keepDistance - 0.5f) Move(-dir * MoveSpeed, dt); // 도망
            else if (d > _keepDistance + 2f) Move(dir * MoveSpeed, dt);
            else Move(Strafe(dir) * (MoveSpeed * 0.4f), dt);
        }

        // 멈춰서 나팔을 불어요
        protected override void TickWindup(float dt, bool tracking) => FaceToward(ToPlayer());

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Circle(CallRadius);

        AudioSource _horn;

        // 예고 동안 나팔. 예고가 끊기거나 쓰러지면 나팔도 멈춰요
        protected override void OnAttackStarted() => _horn = Kind == EnemyKind.Horn ? Sfx.PlayStoppable(SfxId.HornBlow) : null;
        protected override void OnAttackInterrupted() => Sfx.Stop(_horn);
        protected override void OnDied() => Sfx.Stop(_horn);

        protected override void OnActiveStarted()
        {
            if (!Director.TryUseSummon()) return;
            for (int i = 0; i < _summonCount; i++)
                Director.Spawn(SummonKind, Director.FindFreeSpot(transform.position, CallRadius + 0.3f));
        }

        protected override void TickActive(float dt) { }
    }
}
