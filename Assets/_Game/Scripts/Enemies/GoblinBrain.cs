using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 (몽둥이): 졸개처럼 다가와 몽둥이로 전진 휘두르기
    public sealed class GoblinBrain : LungeSlashBrain
    {
        public override EnemyKind Kind => EnemyKind.Goblin;
        protected override string Key => "goblin";
        public override string AttackName => "몽둥이 휘두르기";
        protected override bool CircleWhileWaiting => false; // 휘두른 뒤 옆으로 비켜서지 않고 그 자리에서 다시 노려요
    }

}
