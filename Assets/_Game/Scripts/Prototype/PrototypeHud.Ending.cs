using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 천마 격파 엔딩 (RoomRun.Ending): 수묵 그리기는 Fx/InkEnding (검붉은 먹구름이 걷히는 새벽 · 두루마리 대사 · 제목 낙관)
    public sealed partial class PrototypeHud
    {
        void DrawEnding()
        {
            if (run == null || !run.IsEnding) return;
            AThousandDawns.Fx.InkEnding.Draw(run.EndingTime, run.EndingLine, run.EndingLineIndex, run.EndingLineCount);
        }
    }
}
