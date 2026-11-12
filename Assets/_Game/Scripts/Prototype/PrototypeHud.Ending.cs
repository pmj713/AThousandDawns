using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 천마 격파 엔딩 기본판 (RoomRun.Ending): 검붉은 화면이 새벽빛으로 밝아지고, 아래 대사 상자에 한 줄씩.
    // 아트 쪽이 수묵 새벽 · 족자 대사 상자로 바꿔 그릴 자리예요.
    public sealed partial class PrototypeHud
    {
        static readonly Color EndingNight = new Color(0.16f, 0.02f, 0.03f, 0.92f);
        static readonly Color EndingDawn = new Color(0.98f, 0.78f, 0.52f, 0.55f);

        void DrawEnding()
        {
            if (run == null || !run.IsEnding) return;
            EnsureStyles();
            float t = Mathf.Clamp01(run.EndingTime / 6f); // 6초 동안 밝아져요
            var old = GUI.color;
            GUI.color = Color.Lerp(EndingNight, EndingDawn, t);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);

            var box = new Rect(UiScale.Width * 0.5f - 640f, UiScale.Height - 260f, 1280f, 150f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = old;
            string line = run.EndingLine;
            if (line != null)
                GUI.Label(new Rect(box.x + 40f, box.y + 30f, box.width - 80f, 70f), line, run.EndingLineIndex == run.EndingLineCount - 1 && run.EndingFirst ? _big : _bossLabel);
            GUI.Label(new Rect(box.x, box.yMax - 40f, box.width - 30f, 30f),
                $"<color=#bdbdbd>{run.EndingLineIndex + 1}/{run.EndingLineCount} · F · Enter 넘기기</color>", _bossLabel);
        }
    }
}
