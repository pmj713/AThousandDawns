using System.Collections.Generic;
using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 데스나이트 (차원 겹침, 판타지 지역 보스): 철권 마웅 · 오우거와 같은 규칙 (보스 칸, 50%에서 2페이즈). 패턴은 겹치지 않게 저만의 것:
    //   대검 올려베기 → 내려베기 (가까울 때): 앞 직선 두 번
    //   망령 기병 돌격 (중간 거리): deathknight.lane_interval마다 결투장을 가로지르는 줄 셋 예고 → 망령 기병이 지나가며 쳐요
    //   어둠 걸음 (멀 때): 플레이어 곁 예고 원 → 그 자리에서 솟아오르며 베기
    //   죽은 자의 부름: 2페이즈에 들어설 때 스켈레톤을 deathknight.summon_count만큼 일으켜요 (부하는 지역 배율을 받아요)
    public sealed class DeathKnightBrain : IronFistBrain
    {
        public override EnemyKind Kind => EnemyKind.DeathKnight;
        protected override string Key => "deathknight";
        public override EnemyRank Rank => EnemyRank.Boss;
        protected override float DeathDelay => 1.5f;
        protected override string JabName => "대검 올려베기";
        protected override string FarLeapName => "어둠 걸음";
        protected override MeleeKind Melee => MeleeKind.TwinRect;
        protected override SpecialKind Special => SpecialKind.None;
        protected override FarKind Far => FarKind.Leap;

        public static event System.Action<DeathKnightBrain, Vector3> CurseWave;              // 망령 기병 돌격 예고 (달리는 방향, 아트: 청록 유령불)
        public static event System.Action<DeathKnightBrain> Raise;                           // 죽은 자의 부름 (아트: 청록 룬 원)
        public static event System.Action<DeathKnightBrain, Vector3, Vector3> SpectralRide; // 줄 하나를 망령 기병이 지나감 (시작, 끝)

        struct Lane { public Vector3 From, Dir; public float Timer; public Telegraph Marker; }
        readonly List<Lane> _lanes = new List<Lane>();
        float _laneTimer = 2f, _laneWidth = 1.4f, _laneDelay = 1.1f, _laneRatio = 0.8f;

        public int LanesPending => _lanes.Count;

        protected override void ReadMoreTuning()
        {
            _laneWidth = Tuning("lane_width", 1.4f);
            _laneDelay = Tuning("lane_delay", 1.1f);
            _laneRatio = Tuning("lane_ratio", 0.8f);
        }

        float LaneLength => Mathf.Max(ZoneSize.x, ZoneSize.y) + 2f;

        protected override void TickMore(float dt)
        {
            if (_laneTimer > 0f) _laneTimer -= dt;
            float length = LaneLength;
            for (int i = _lanes.Count - 1; i >= 0; i--)
            {
                var lane = _lanes[i];
                lane.Timer -= dt;
                if (lane.Marker != null) lane.Marker.SetProgress(1f - Mathf.Clamp01(lane.Timer / Mathf.Max(0.01f, _laneDelay)));
                if (lane.Timer > 0f) { _lanes[i] = lane; continue; }
                if (lane.Marker != null) Destroy(lane.Marker.gameObject);
                _lanes.RemoveAt(i);
                SpectralRide?.Invoke(this, lane.From, lane.From + lane.Dir * length);
                if (HitShapes.InRect(lane.From, lane.Dir, length, _laneWidth, Player.transform.position, PlayerRadius))
                    Player.TryReceiveHit(AttackPower * _laneRatio, Player.transform.position - lane.Dir, Tuning("lane_knockback", 1.5f), Body);
            }
        }

        // 플레이어를 지나는 줄 하나 + 양옆 줄 (결투장 끝에서 끝까지, 데스나이트 → 플레이어 방향을 가로질러 달려요)
        protected override void TryMidRange()
        {
            if (_laneTimer > 0f || Attack.IsAttacking) return;
            _laneTimer = Tuning("lane_interval", 6f);
            Vector3 to = ToPlayer();
            to.y = 0f;
            Vector3 aim = to.sqrMagnitude > 1e-4f ? to.normalized : Facing;
            Vector3 dir = Quaternion.Euler(0f, 90f, 0f) * aim;
            int count = Mathf.Max(1, Mathf.RoundToInt(Tuning("lane_count", 3f)));
            float gap = Tuning("lane_gap", 3f), stagger = Tuning("lane_stagger", 0.15f), length = LaneLength;
            Vector3 p = Player.transform.position;
            p.y = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 from = p + aim * ((i - (count - 1) * 0.5f) * gap) - dir * (length * 0.5f);
                var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
                marker.Show(TelegraphShapes.Rect(_laneWidth, length));
                marker.SetPose(from, dir);
                marker.SetProgress(0f);
                _lanes.Add(new Lane { From = from, Dir = dir, Timer = _laneDelay + i * stagger, Marker = marker });
            }
            FaceToward(aim);
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

        protected override void OnDiedMore()
        {
            foreach (var l in _lanes) if (l.Marker != null) Destroy(l.Marker.gameObject);
            _lanes.Clear();
        }
    }
}
