using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 회귀 기록: 판이 끝날 때마다 meta.json에 쌓아요 (무공 요결 창의 "회귀 기록" 탭이 보여 줘요).
    //   플레이 시간 합 · 가장 깊이 간 지역 · 천마를 격파한 가장 높은 업보 · 쓰러뜨린 것 순위 · 끝까지 지닌 기물 · 쓰던 초식 · 최근 판 10개
    public sealed partial class RoomRun
    {
        const int RecentRunsKept = 10;

        void RecordHistory(MetaSave meta, bool victory, string where, int memory)
        {
            meta.totalPlaySeconds += _record.PlaySeconds;
            meta.bestRegion = Mathf.Max(meta.bestRegion, _region);
            if (_record.FinalBoss) meta.bestKarma = Mathf.Max(meta.bestKarma, _karma);
            if (!victory) MetaSave.Bump(meta.deathCauses, string.IsNullOrEmpty(Player.LastHitBy) ? "알 수 없음" : Player.LastHitBy);
            if (Player.Items != null)
                foreach (var item in Player.Items.Items)
                    if (item.Def != null) MetaSave.Bump(meta.itemCounts, item.Def.name);
            MetaSave.Bump(meta.skillCounts, PlayerController.SkillLabel(Player.Skill));
            meta.recentRuns.Insert(0, new RunLog
            {
                run = meta.runs, victory = victory, where = where, floors = _record.FloorsCleared,
                seconds = _record.PlaySeconds, karma = _karma, memory = memory,
            });
            if (meta.recentRuns.Count > RecentRunsKept) meta.recentRuns.RemoveRange(RecentRunsKept, meta.recentRuns.Count - RecentRunsKept);
        }
    }
}
