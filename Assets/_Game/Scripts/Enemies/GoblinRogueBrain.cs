using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 단검꾼: 들개처럼 옆 · 뒤로 돌아 빠르게 파고들어 찌르고 옆으로 빠져요
    public sealed class GoblinRogueBrain : HoundBrain
    {
        public override EnemyKind Kind => EnemyKind.GoblinRogue;
        protected override string Key => "goblinrogue";
        public override string AttackName => "단검 찌르기";
    }

}
