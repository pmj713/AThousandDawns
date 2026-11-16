using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 오우거 (차원 겹침, 판타지 지역 보스): 철권 마웅과 같은 규칙에 덩치만 커요 (보스 칸, 50%에서 2페이즈).
    //   몽둥이 2연타 (가까울 때) · 땅 내려치기 (원 충격파, 2페이즈는 2연속) · 분노 돌진 (멀 때, 2페이즈엔 벽에 부딪히면 낙석)
    //   바위 던지기: 중간 거리에서 ogre.throw_interval마다 플레이어 둘레에 바위 셋 (예고 원 뒤 떨어짐)
    public sealed class OgreBrain : IronFistBrain
    {
        public override EnemyKind Kind => EnemyKind.Ogre;
        protected override string Key => "ogre";
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override float DeathDelay => 1.5f;
        protected override string JabName => "몽둥이 2연타";
        protected override string QuakeName => "땅 내려치기";
        protected override string ChargeName => "분노 돌진";

        public static event System.Action<OgreBrain> RockThrown; // 바위 던지기 (아트)

        float _throwTimer = 2f;

        protected override void TickAlways(float dt)
        {
            base.TickAlways(dt);
            if (_throwTimer > 0f) _throwTimer -= dt;
        }

        protected override void TryMidRange()
        {
            if (_throwTimer > 0f || Attack.IsAttacking) return;
            _throwTimer = Tuning("throw_interval", 6f);
            RockThrown?.Invoke(this);
            DropRocks();
        }
    }
}
