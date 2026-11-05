using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 뛰어들어 물기 계열 (들개, 청죽사): attack_range 안이면 예고 뒤 lunge만큼 뛰어들고, 몸이 닿으면 한 번 물어요.
    // 플레이어 몸 앞에서 멈추고, 벽에 막혀도 멈춰요.
    public abstract class LeapBrain : EnemyBrain
    {
        protected float LeapDistance, LeapTime, BiteReach;
        float _leapRemaining;
        bool _leaping, _bit;

        protected override float ActiveTime => float.PositiveInfinity; // 뛰어들기가 끝나면 EndActive

        protected override void ReadTuning()
        {
            LeapDistance = Tuning("lunge", 2f);
            LeapTime = Mathf.Max(0.05f, Tuning("leap_time", 0.2f));
            BiteReach = Tuning("bite_reach", 0.3f);
            ReadMoreTuning();
        }

        protected virtual void ReadMoreTuning() { }

        protected override bool WantsToAttack(float distance) => distance <= AttackRange;

        protected override List<Vector2> TelegraphShape() =>
            TelegraphShapes.Rect(BodyRadius * 2f, LeapDistance + BodyRadius + BiteReach);

        protected override void OnActiveStarted()
        {
            _leapRemaining = LeapDistance;
            _leaping = true;
            _bit = false;
        }

        protected override void TickActive(float dt)
        {
            if (!_leaping)
            {
                TickAfterLeap(dt);
                return;
            }

            float contact = BodyRadius + PlayerRadius + BiteReach;
            Vector3 to = ToPlayer();
            bool stop = Vector3.Dot(to, AimDirection) > 0f && to.magnitude <= contact - BiteReach * 0.5f;
            float step = Mathf.Min(_leapRemaining, LeapDistance / LeapTime * dt);
            if (!stop && step > 0f)
            {
                var flags = MoveRaw(AimDirection * step);
                _leapRemaining -= step;
                if ((flags & CollisionFlags.Sides) != 0) _leapRemaining = 0f;
            }

            if (!_bit && ToPlayer().magnitude <= contact && TryHitPlayer())
            {
                _bit = true;
                OnBite();
            }

            if (stop || _leapRemaining <= 1e-4f)
            {
                _leaping = false;
                OnLeapFinished();
            }
        }

        protected virtual void OnBite() { }
        protected virtual void OnLeapFinished() => Attack.EndActive();
        protected virtual void TickAfterLeap(float dt) { }
    }
}
