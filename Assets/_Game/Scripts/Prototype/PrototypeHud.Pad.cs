using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 게임패드 커서 기본판: 마지막 입력이 패드일 때만 고른 칸에 옅은 띠와 테두리 (아트 쪽이 먹 테두리로 바꿔 그릴 자리)
    public sealed partial class PrototypeHud
    {
        static readonly Color PadBand = new Color(1f, 0.85f, 0.45f, 0.12f);
        static readonly Color PadEdge = new Color(1f, 0.85f, 0.45f, 0.9f);

        void PadMark(Rect r)
        {
            if (!Pad.Active) return;
            var old = GUI.color;
            GUI.color = PadBand;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = PadEdge;
            const float t = 3f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
