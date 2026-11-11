namespace AThousandDawns.Enemies
{
    // 등불 귀신 (폐사찰, 몬스터 시트 45행): 화약 산적처럼 다가와 터져요. 맞으면 감전 (잠시 이동속도 −40%).
    public sealed class LanternBrain : BomberBrain
    {
        float _slow, _slowTime;

        public override EnemyKind Kind => EnemyKind.Lantern;
        protected override string Key => "lantern";
        public override string AttackName => "귀화";

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _slow = Tuning("shock_slow", 0.4f);
            _slowTime = Tuning("shock_time", 1.5f);
        }

        protected override void OnBlastHitPlayer() => Player.ApplySlow(_slow, _slowTime);
    }
}
