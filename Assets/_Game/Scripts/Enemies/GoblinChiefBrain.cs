using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 족장 (차원 겹침, 판타지 고수): 철권 마웅과 같은 규칙 (결투장, 50%에서 2페이즈, 같은 패턴 연속 2번까지). 패턴은 겹치지 않게 저만의 것:
    //   철퇴 휘돌리기 (가까울 때): 앞 220° 넓은 부채꼴 한 번, 맞으면 크게 밀려나요
    //   덫 던지기 (중간 거리): goblinchief.trap_interval마다 플레이어 둘레에 덫 셋 (밟으면 거의 못 움직여요)
    //   점프 덮치기 (멀 때): 내려앉을 자리 예고 원 → 그 자리로 뛰어 내려찍기
    //   함성: 2페이즈에 들어설 때 고블린을 goblinchief.summon_count만큼 불러 모아요 (부하는 지역 배율을 받아요)
    public sealed class GoblinChiefBrain : IronFistBrain
    {
        public override EnemyKind Kind => EnemyKind.GoblinChief;
        protected override string Key => "goblinchief";
        protected override string JabName => "철퇴 휘돌리기";
        protected override string FarLeapName => "점프 덮치기";
        protected override MeleeKind Melee => MeleeKind.Sweep;
        protected override SpecialKind Special => SpecialKind.None;
        protected override FarKind Far => FarKind.Leap;

        public static event System.Action<GoblinChiefBrain> WarCry;               // 함성 (아트: 붉은 음파 고리)
        public static event System.Action<GoblinChiefBrain, Vector3> TrapThrown; // 덫 하나가 떨어진 자리 (아트)

        float _trapTimer = 2f;

        protected override void TickMore(float dt)
        {
            if (_trapTimer > 0f) _trapTimer -= dt;
        }

        protected override void TryMidRange()
        {
            if (_trapTimer > 0f || Attack.IsAttacking) return;
            _trapTimer = Tuning("trap_interval", 6f);
            int count = Mathf.Max(1, Mathf.RoundToInt(Tuning("trap_count", 3f)));
            float radius = Tuning("trap_radius", 0.9f), life = Tuning("trap_life", 6f), slow = Tuning("trap_slow", 0.8f), slowTime = Tuning("trap_slow_time", 1.2f);
            float damage = AttackPower * Tuning("trap_ratio", 0.3f), spread = Tuning("trap_spread", 2f);
            Vector3 p = Player.transform.position;
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = i == 0 ? p : p + Quaternion.Euler(0f, start + i * 360f / count, 0f) * Vector3.forward * spread;
                at = Clamp(at);
                GhostFire.Create(at, radius, life, damage, slow, slowTime, Player, Body).name = "덫";
                TrapThrown?.Invoke(this, at);
            }
        }

        protected override void OnEnterPhase2()
        {
            WarCry?.Invoke(this);
            int count = Mathf.RoundToInt(Tuning("summon_count", 3f));
            for (int i = 0; i < count; i++)
            {
                var at = Director.FindFreeSpot(transform.position + Quaternion.Euler(0f, i * 360f / count, 0f) * Vector3.forward * 2.5f, 0.5f);
                Director.SpawnSummon(EnemyKind.Goblin, at);
            }
        }
    }
}
