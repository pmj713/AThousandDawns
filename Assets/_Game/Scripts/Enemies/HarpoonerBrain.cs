using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 작살수 (장강 수채, 몬스터 시트 47행 "맞으면 끌려감"): 궁수처럼 멀리서 쏘지 않고 4칸쯤을 두고 작살을 찔러 뻗어요.
    // 예고 0.7초 (앞 절반만 따라 겨눔) 뒤 앞으로 4.5칸 직선 찌르기. 걸리면 작살수 1.5칸 앞까지 끌려와요 (근접 적 앞으로).
    // 너무 붙으면 물러나요. 대시로 피하면 아무 일도 없어요.
    public sealed class HarpoonerBrain : EnemyBrain
    {
        float _reach, _width, _stop, _keepMin, _keepMax;
        public static event System.Action<HarpoonerBrain, Vector3, Vector3, float, bool> Thrust; // 작살 찌르기: 자리 · 방향 · 길이 · 끌어왔는지 (아트: InkRiverEnemyFx)

        public override EnemyKind Kind => EnemyKind.Harpooner;
        protected override string Key => "harpooner";
        public override string AttackName => "작살 찌르기";
        protected override float ActiveTime => 0.1f;

        protected override void ReadTuning()
        {
            _reach = Tuning("reach", 4.5f);
            _width = Tuning("width", 0.6f);
            _stop = Tuning("pull_stop", 1.5f);
            _keepMin = Tuning("keep_min", 3f);
            _keepMax = Tuning("keep_max", 4.5f);
        }

        protected override bool WantsToAttack(float distance) => distance <= _reach && distance >= _keepMin * 0.7f;

        protected override void TickMovement(float dt, bool canAttack)
        {
            Vector3 to = ToPlayer();
            float d = to.magnitude;
            Vector3 dir = d > 1e-3f ? to / d : Facing;
            FaceToward(dir);
            if (d < _keepMin) Move(-dir * MoveSpeed, dt);
            else if (d > _keepMax) Move(dir * MoveSpeed, dt);
            else Move(Strafe(dir) * (MoveSpeed * 0.4f), dt);
        }

        protected override List<Vector2> TelegraphShape() => TelegraphShapes.Rect(_width, _reach);

        protected override void OnActiveStarted()
        {
            Vector3 rel = Player.transform.position - transform.position;
            rel.y = 0f;
            float along = Vector3.Dot(rel, AimDirection);
            float side = Mathf.Abs(Vector3.Dot(rel, new Vector3(AimDirection.z, 0f, -AimDirection.x)));
            bool pulled = !(along < 0f || along > _reach + PlayerRadius || side > _width * 0.5f + PlayerRadius) && TryHitPlayer();
            Thrust?.Invoke(this, transform.position, AimDirection, _reach, pulled);
            if (pulled) Player.PullToward(transform.position, _stop + BodyRadius);
        }

        protected override void TickActive(float dt) { }
    }
}
