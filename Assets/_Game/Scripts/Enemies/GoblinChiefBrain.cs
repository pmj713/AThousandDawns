namespace AThousandDawns.Enemies
{
    // 고블린 족장 (차원 겹침, 판타지 고수): 철권 마웅과 같은 규칙 (결투장, 50%에서 2페이즈, 같은 패턴 연속 2번까지).
    //   철퇴 2연타 (가까울 때) · 철퇴 내려찍기 (원 충격파) · 돌진 (멀 때, 벽에 부딪히면 기절)
    //   함성: 2페이즈에 들어설 때 고블린을 goblinchief.summon_count만큼 불러 모아요 (부하는 지역 배율을 받아요)
    public sealed class GoblinChiefBrain : IronFistBrain
    {
        public override EnemyKind Kind => EnemyKind.GoblinChief;
        protected override string Key => "goblinchief";
        protected override string JabName => "철퇴 2연타";
        protected override string QuakeName => "철퇴 내려찍기";
        protected override string ChargeName => "돌진";

        public static event System.Action<GoblinChiefBrain> WarCry; // 함성 (아트: 붉은 음파 고리)

        protected override void OnEnterPhase2()
        {
            WarCry?.Invoke(this);
            int count = UnityEngine.Mathf.RoundToInt(Tuning("summon_count", 3f));
            for (int i = 0; i < count; i++)
            {
                var at = Director.FindFreeSpot(transform.position + UnityEngine.Quaternion.Euler(0f, i * 360f / count, 0f) * UnityEngine.Vector3.forward * 2.5f, 0.5f);
                Director.SpawnSummon(EnemyKind.Goblin, at);
            }
        }
    }
}
