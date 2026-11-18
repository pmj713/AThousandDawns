using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 스켈레톤 (차원 겹침, 판타지): 졸개처럼 다가와 녹슨 검으로 전진 베기.
    // 처음 쓰러질 피해를 받으면 쓰러지지 않고 뼈가 무너져요 (Damageable.Undying). skeleton.reassemble_time 동안 맞지 않고 꼼짝 못 하다가
    // 체력 skeleton.revive_health로 다시 맞춰져 일어나요. 한 번만이에요 (두 번째는 그대로 쓰러져요).
    public sealed class SkeletonBrain : LungeSlashBrain
    {
        public override EnemyKind Kind => EnemyKind.Skeleton;
        protected override string Key => "skeleton";
        public override string AttackName => "녹슨 검 베기";

        public static event System.Action<SkeletonBrain> Reassembling; // 뼈가 무너졌을 때 (아트)
        public static event System.Action<SkeletonBrain> Reassembled;  // 다시 일어났을 때

        float _reassemble;
        public bool IsReassembling => _reassemble > 0f;
        protected override bool IsAnchored => IsReassembling; // 다시 맞춰지는 동안은 밀리거나 끌려가지도 않아요

        protected override void OnInitialized()
        {
            Body.Undying = 1;
            Body.Collapsed += OnCollapsed;
        }

        void OnCollapsed(Damageable _)
        {
            _reassemble = Tuning("reassemble_time", 1.5f);
            if (Attack.IsAttacking) CancelAttack();
            Body.Targetable = false; // 무너진 동안은 맞지 않아요
            HoldStill(_reassemble); // 무너뜨린 그 타격의 경직이 이걸 짧게 덮지 않아요 (EnemyBrain.HoldStill은 더 긴 쪽)
            Reassembling?.Invoke(this);
        }

        protected override void TickAlways(float dt)
        {
            if (_reassemble <= 0f) return;
            _reassemble -= dt;
            if (_reassemble > 0f) return;
            Body.Targetable = true;
            Body.Heal(Body.MaxHealth * Tuning("revive_health", 0.5f));
            Reassembled?.Invoke(this);
        }
    }
}
