using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    public enum EnemyFaction { Wuxia, Fantasy }

    // 적끼리 싸움 (차원 겹침): 무협 적과 판타지 적은 서로 적이에요. 가까이에 다른 진영 적이 있고 플레이어보다 가까우면 그쪽을 노려요 (Rival).
    //   노리는 동안: 움직임 · 조준 · 공격 판단이 그 적을 향하고 (ToPlayer), 공격 순간 사거리 안이면 그 적이 맞아요 (공격력 × fantasy.rival_ratio).
    //   공격 토큰은 쓰지 않아요 (플레이어를 둘러싸는 수 제한은 플레이어를 노릴 때만).
    //   협력 (Allied): 방에 나올 때 확률로 정해요. 협력한 적은 다른 진영을 노리지도 않고 노려지지도 않아요. 깊이 갈수록 협력이 늘어요 (RoomRun.AllyChance).
    //   고수 · 보스 · 살수 · 숨은 적은 끼어들지 않아요. 다른 적이 쓰러뜨린 적은 수련치가 적어요 (fantasy.rival_kill_xp).
    public abstract partial class EnemyBrain
    {
        public EnemyFaction Faction => (int)Kind >= (int)EnemyKind.Slime ? EnemyFaction.Fantasy : EnemyFaction.Wuxia;
        public bool Allied { get; set; }
        public EnemyBrain Rival { get; private set; }
        public bool KilledByRival { get; private set; }

        public static event System.Action<EnemyBrain, EnemyBrain, Vector3> RivalHit;   // 때린 적, 맞은 적, 맞은 자리 (아트)
        public static event System.Action<EnemyBrain, EnemyBrain> RivalChanged;        // 노리기 시작 · 그만 (rival null)

        public static bool RivalryEnabled = true; // 테스트용 끄기
        float _rivalScan;

        // 서로 적대하는가: 다른 진영이고 둘 다 협력하지 않았을 때 (적끼리 싸움이 꺼져 있으면 아무도 적대하지 않아요)
        public bool HostileTo(EnemyBrain other) => other != null && HostileTo(other.Faction, other.Allied);

        // 쓰러진 적처럼 몸이 없을 때 (혈인 부활): 진영과 협력 여부만으로
        protected bool HostileTo(EnemyFaction faction, bool allied) => faction != Faction && !Allied && !allied && RivalryOn;

        // 도움 (깃발수 지휘 · 고블린 주술사 광폭화 · 목탁승 가호 · 혈인 부활)은 같은 진영, 또는 적대하지 않는 (협력한) 다른 진영 적에게만 걸어요
        public bool CanSupport(EnemyBrain other) => other != null && other != this && !other.IsDead && !HostileTo(other);

        static bool RivalryOn => RivalryEnabled && GameConfig.GetOptional("fantasy.rivalry", 1f) > 0.5f;

        bool CanFeud =>RivalryEnabled && !Allied && !IsHidden && !_dead && (Rank == EnemyRank.Normal || Rank == EnemyRank.Elite)
                        && GameConfig.GetOptional("fantasy.rivalry", 1f) > 0.5f;

        // Update에서: 0.4초마다 노릴 적을 다시 골라요
        void TickRival(float dt)
        {
            if (Rival != null && (Rival == null || Rival.IsDead || !Rival.CanFeud || !CanFeud)) SetRival(null);
            _rivalScan -= dt;
            if (_rivalScan > 0f) return;
            _rivalScan = 0.4f;
            if (!CanFeud || !_awake || Player == null) { SetRival(null); return; }
            float reach = GameConfig.GetOptional("fantasy.rival_radius", 6f);
            float toPlayer = RivalFlat(Player.transform.position - transform.position).magnitude;
            EnemyBrain best = null;
            float bestDist = reach;
            foreach (var e in Director.Enemies)
            {
                if (e == null || e == this || e.Faction == Faction || !e.CanFeud || !e._awake) continue;
                float d = RivalFlat(e.transform.position - transform.position).magnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            if (best != null && bestDist > toPlayer) best = null; // 플레이어가 더 가까우면 플레이어
            SetRival(best);
        }

        void SetRival(EnemyBrain rival)
        {
            if (Rival == rival) return;
            Rival = rival;
            RivalChanged?.Invoke(this, rival);
        }

        static Vector3 RivalFlat(Vector3 v) { v.y = 0f; return v; }

        // 공격 순간: 노리는 적이 사거리 안이면 맞아요
        void HitRival()
        {
            var rival = Rival;
            if (rival == null || rival.IsDead) return;
            float d = RivalFlat(rival.transform.position - transform.position).magnitude;
            float reach = (IsRanged ? AttackRange : AttackRange + GameConfig.GetOptional("fantasy.rival_melee_reach", 1.5f)) + rival.BodyRadius;
            if (d > reach) return;
            int damage = Mathf.Max(1, Mathf.RoundToInt(AttackPower * GameConfig.GetOptional("fantasy.rival_ratio", 1f)));
            if (rival.Body.Health <= damage) rival.KilledByRival = true;
            rival.Body.ApplyHit(damage, true);
            RivalHit?.Invoke(this, rival, rival.transform.position);
        }
    }
}
