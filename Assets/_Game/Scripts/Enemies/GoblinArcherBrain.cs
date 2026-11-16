using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 궁수: 산적 궁수처럼 거리를 두고 쏴요
    public sealed class GoblinArcherBrain : ArcherBrain
    {
        public override EnemyKind Kind => EnemyKind.GoblinArcher;
        protected override string Key => "goblinarcher";
    }

}
