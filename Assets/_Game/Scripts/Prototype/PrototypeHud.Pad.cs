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
            float w = 300f, x = (UiScale.Width - w) * 0.5f, y = UiScale.Height - size - 24f;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(x - 16f, y - 8f, w + 32f, size + 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            x += AThousandDawns.Fx.InkPadUi.Button(new Vector2(x, y), "A", "고르기", size);
            AThousandDawns.Fx.InkPadUi.Button(new Vector2(x, y), "B", "닫기", size);
            GUI.color = old;
        }
    }
}
