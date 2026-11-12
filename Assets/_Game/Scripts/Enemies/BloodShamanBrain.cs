using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 혈인 (천마총, 몬스터 시트 54행 "쓰러진 적 부활"): 나팔수처럼 거리를 두고, 근처 (8칸)에 쓰러진 적이 있으면
    // 예고 1.2초 (맞으면 끊김) 뒤 그 적 하나를 그 자리에 체력 절반의 핏빛 모습으로 되살려요. 공격 간격 8초, 혈인 하나당 최대 3번.
    // 되살아난 적은 다시 되살리지 않아요. 고수·보스·살수는 되살리지 않아요. 먼저 잡아야 해요.
    public sealed class BloodShamanBrain : HornBrain
    {
        static readonly Color Blood = new Color(0.65f, 0.12f, 0.12f);

        struct Corpse
        {
            public EnemyKind Kind;
            public Vector3 At;
            public float MaxHealth, Attack;
        }

        static readonly List<Corpse> Corpses = new List<Corpse>(); // 이 방에 쓰러진 적 (방을 옮기면 비워요)
        float _radius, _ratio;
        int _left;

        public override EnemyKind Kind => EnemyKind.BloodShaman;
        protected override string Key => "bloodshaman";
        public override string AttackName => "혈인술";
        public int RevivesLeft => _left;

        static BloodShamanBrain() => AnyDied += Remember;

        static void Remember(EnemyBrain dead)
        {
            if (dead == null || dead.Revived || dead.Rank >= EnemyRank.Assassin || dead.Kind == EnemyKind.BloodShaman) return;
            Corpses.Add(new Corpse { Kind = dead.Kind, At = dead.transform.position, MaxHealth = dead.Damageable.MaxHealth, Attack = dead.AttackStat });
        }

        public static void ClearCorpses() => Corpses.Clear();
        public static int CorpseCount => Corpses.Count;

        protected override void ReadTuning()
        {
            base.ReadTuning();
            _radius = Tuning("revive_radius", 8f);
            _ratio = Tuning("revive_health", 0.5f);
            _left = Mathf.RoundToInt(Tuning("revive_max", 3f));
        }

        int Nearest()
        {
            int best = -1;
            float bestDist = _radius;
            for (int i = 0; i < Corpses.Count; i++)
            {
                float d = (Corpses[i].At - transform.position).magnitude;
                if (d <= bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        protected override bool WantsToAttack(float distance) => _left > 0 && Nearest() >= 0;

        protected override void OnActiveStarted()
        {
            int i = Nearest();
            if (i < 0) return;
            var c = Corpses[i];
            Corpses.RemoveAt(i);
            _left--;
            var risen = Director.Spawn(c.Kind, Director.FindFreeSpot(c.At, 1f)); // 지역 배율은 쓰러진 적 수치로 맞춰요
            if (risen == null) return;
            risen.Revived = true;
            risen.Damageable.ScaleMaxHealth(c.MaxHealth * _ratio / Mathf.Max(1f, risen.Damageable.MaxHealth));
            risen.ScaleAttack(c.Attack / Mathf.Max(0.01f, risen.AttackStat));
            risen.Damageable.SetTint(Blood);
            Director.SpawnBurst(c.At, 1.2f);
        }
    }
}
