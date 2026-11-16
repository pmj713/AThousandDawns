using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 데스나이트 (차원 겹침, 판타지 지역 보스): 철권 마웅 · 오우거와 같은 규칙 (보스 칸, 50%에서 2페이즈).
    //   룬검 2연격 (가까울 때) · 저주 내려찍기 (원 충격파, 2페이즈는 2연속) · 망령마 돌격 (멀 때)
    //   저주 검기: 중간 거리에서 deathknight.wave_interval마다 플레이어 쪽 부채꼴로 검기 셋
    //   죽은 자의 부름: 2페이즈에 들어설 때 스켈레톤을 deathknight.summon_count만큼 일으켜요 (부하는 지역 배율을 받아요)
    public sealed class DeathKnightBrain : IronFistBrain
    {
        public override EnemyKind Kind => EnemyKind.DeathKnight;
        protected override string Key => "deathknight";
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override float DeathDelay => 1.5f;
        protected override string JabName => "룬검 2연격";
        protected override string QuakeName => "저주 내려찍기";
        protected override string ChargeName => "망령마 돌격";

        public static event System.Action<DeathKnightBrain, Vector3> CurseWave; // 저주 검기 (아트: 청록 유령불 초승달)
        public static event System.Action<DeathKnightBrain> Raise;              // 죽은 자의 부름 (아트: 청록 룬 원)

        float _waveTimer = 2f;

        protected override void TickAlways(float dt)
        {
            base.TickAlways(dt);
            if (_waveTimer > 0f) _waveTimer -= dt;
        }

        protected override void TryMidRange()
        {
            if (_waveTimer > 0f || Attack.IsAttacking) return;
            _waveTimer = Tuning("wave_interval", 5f);
            Vector3 to = ToPlayer();
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : Facing;
            FaceToward(dir);
            int count = Mathf.Max(1, Mathf.RoundToInt(Tuning("wave_count", 3f)));
            float spread = Tuning("wave_spread", 20f), speed = Tuning("wave_speed", 9f), range = Tuning("wave_range", 12f);
            float damage = AttackPower * Tuning("wave_ratio", 0.6f);
            Vector3 origin = transform.position + Vector3.up * 1.2f + dir * (BodyRadius + 0.3f);
            for (int i = 0; i < count; i++)
            {
                var wave = Director.SpawnArrow(origin, Quaternion.Euler(0f, (i - (count - 1) * 0.5f) * spread, 0f) * dir, speed, range, 0.5f, damage, Body);
                if (wave != null) wave.gameObject.name = "Arrow"; // 아트가 Owner Kind로 저주 검기 그림을 입혀요
            }
            CurseWave?.Invoke(this, dir);
        }

        protected override void OnEnterPhase2()
        {
            Raise?.Invoke(this);
            int count = Mathf.RoundToInt(Tuning("summon_count", 3f));
            for (int i = 0; i < count; i++)
            {
                var at = Director.FindFreeSpot(transform.position + Quaternion.Euler(0f, i * 360f / Mathf.Max(1, count), 0f) * Vector3.forward * 2.5f, 0.5f);
                Director.SpawnSummon(EnemyKind.Skeleton, at);
            }
        }
    }
}
