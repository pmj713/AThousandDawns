using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 게임패드 커서: 마지막 입력이 패드일 때만 고른 칸에 옅은 금빛 띠와 먹 붓 테두리 (Fx/InkPadUi.Frame).
    // 커서가 있는 화면이 열려 있으면 아래 가운데에 붉은 낙관 버튼 안내 (A 고르기 · B 닫기).
    public sealed partial class PrototypeHud
    {
        static readonly Color PadBand = new Color(1f, 0.85f, 0.45f, 0.1f);
        int _padMarkFrame = -1;

        void PadMark(Rect r)
        {
            if (!Pad.Active) return;
            _padMarkFrame = Time.frameCount;
            var old = GUI.color;
            GUI.color = PadBand;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
            AThousandDawns.Fx.InkPadUi.Frame(r);
        }

        // OnGUI 끝에서: 이번 프레임에 패드 커서를 그렸으면 버튼 안내
        void DrawPadHints()
        {
            if (!Pad.Active || _padMarkFrame != Time.frameCount) return;
            const float size = 34f;
            AThousandDawns.Fx.InkPadUi.Hints(UiScale.Width * 0.5f, UiScale.Height - size - 24f, size, ("A", "고르기"), ("B", "닫기"));
        }
    }
}
