namespace AThousandDawns.Enemies
{
    // 석상 나한 (폐사찰, 몬스터 시트 42행): 멧돼지처럼 예고 뒤 직선 돌진. 느리고 짧지만 맞으면 기절 (0.8초).
    // 벽에 부딪히면 멧돼지처럼 멈춰 서요. 돌진 중엔 경직이 없어요.
    public sealed class ArhatBrain : BoarBrain
    {
        float _stun;

        public override EnemyKind Kind => EnemyKind.Arhat;
        protected override string Key => "arhat";

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _stun = Tuning("stun", 0.8f);
        }

        protected override void OnChargeHit() => Player.Stagger(_stun);
    }
}
