using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 단검꾼: 들개처럼 옆 · 뒤로 돌아 빠르게 파고들어 찌르고 그 자리에 서요 (옆걸음은 뺐어요)
    public sealed class GoblinRogueBrain : HoundBrain
    {
        public override EnemyKind Kind => EnemyKind.GoblinRogue;
        protected override string Key => "goblinrogue";
        public override string AttackName => "단검 찌르기";
        protected override void OnLeapFinished() => Attack.EndActive(); // 찌른 뒤 옆으로 빠지지 않고 그 자리에서 빈틈
    }

}
