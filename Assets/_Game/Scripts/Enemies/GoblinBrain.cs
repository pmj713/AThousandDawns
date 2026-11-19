using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 (몽둥이): 다가와 앞으로 짧게 뛰어올라 (최대 goblin.lunge칸, goblin.lunge_time초, 벽에 막혀요)
    // 내려앉은 자리 둘레 원 (반경 goblin.attack_range)을 몽둥이로 내려쳐요. 예고 원은 내려앉을 자리에 떠요.
    public sealed class GoblinBrain : LungeSlashBrain
    {
        float _maxHop, _hop;

        public override EnemyKind Kind => EnemyKind.Goblin;
        protected override string Key => "goblin";
        public override string AttackName => "뛰어 내려치기";
        protected override bool CircleWhileWaiting => false; // 내려친 뒤 옆으로 비켜서지 않고 그 자리에서 다시 노려요
        protected override int StrikeShape => ShapeRing;
        protected override bool RadialTelegraph => true;
        protected override float StrikeDelay => LungeTime;        // 내려앉은 뒤에 쳐요
        protected override float ActiveTime => LungeTime + 0.12f; // 내려친 판정이 남아 있는 짧은 틈
        protected override float EngageDistance => _maxHop + AttackRange * 0.6f;
        protected override float TrackStopDistance => _maxHop + AttackRange * 0.5f;

        protected override void ReadMoreTuning() => _maxHop = _hop = Lunge;

        // 예고 앞 절반 (따라오는 동안)엔 뛸 거리를 다시 재요: 노리는 상대 몸 바로 앞까지, 최대 goblin.lunge칸. 뒤 절반은 그대로예요
        protected override void UpdateTelegraph()
        {
            if (Attack.IsTracking) _hop = Mathf.Clamp(ToPlayer().magnitude - (BodyRadius + PlayerRadius + 0.1f), 0f, _maxHop);
            Telegraph.SetPose(transform.position + AimDirection * _hop, AimDirection);
        }

        protected override void OnActiveStarted()
        {
            Lunge = _hop; // 예고한 자리까지만 뛰어요 (MoveRaw라 벽에는 막혀요)
            base.OnActiveStarted();
        }
    }

}
