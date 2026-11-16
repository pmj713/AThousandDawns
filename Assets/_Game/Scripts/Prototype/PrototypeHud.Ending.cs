using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 천마 격파 엔딩 (RoomRun.Ending): 그리기는 Fx/InkEnding (검은 화면 · 하늘의 틈이 닫히는 새벽 · 두루마리 대사)
    public sealed partial class PrototypeHud
    {
        void DrawEnding()
        {
            if (run == null || !run.IsEnding) return;
            AThousandDawns.Fx.InkEnding.Draw(run.EndingTime, run.EndingLine, run.EndingLineIndex, run.EndingLineCount, run.EndingFirst);
        }
    }
}
