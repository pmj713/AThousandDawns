namespace AThousandDawns.Enemies
{
    public enum HitResponse { None, Stun, DelayWindup, ExtendRecovery }

    // 일반 적이 맞았을 때 (몬스터 시트 공통 규칙): 평소엔 경직, 예고 중엔 늦춰짐, 빈틈 중엔 빈틈이 길어짐.
    // 경직(또는 늦춰짐)이 끝난 뒤 면역 시간 동안은 다시 걸리지 않아요.
    public sealed class HitReaction
    {
        float _immuneTimer;

        public HitReaction(float hitstun, float immunity, float windupDelay)
        {
            Hitstun = hitstun;
            Immunity = immunity;
            WindupDelay = windupDelay;
        }

        public float Hitstun { get; }
        public float Immunity { get; }
        public float WindupDelay { get; }
        public bool IsImmune => _immuneTimer > 0f;

        public HitResponse OnHit(EnemyAttackTimeline.Phase phase, bool superArmor)
        {
            if (superArmor || IsImmune) return HitResponse.None;

            HitResponse response = phase switch
            {
                EnemyAttackTimeline.Phase.Ready => HitResponse.Stun,
                EnemyAttackTimeline.Phase.Windup => HitResponse.DelayWindup,
                EnemyAttackTimeline.Phase.Recovery => HitResponse.ExtendRecovery,
                _ => HitResponse.None, // 판정 중인 공격은 끊기지 않아요
            };
            if (response == HitResponse.None) return response;

            _immuneTimer = (response == HitResponse.DelayWindup ? WindupDelay : Hitstun) + Immunity;
            return response;
        }

        public void Tick(float dt)
        {
            if (_immuneTimer > 0f) _immuneTimer -= dt;
        }
    }
}
