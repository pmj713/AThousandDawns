using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 바닥 예고 모양. x = 오른쪽, y = 앞. 모두 볼록 다각형이고 위에서 봤을 때 시계 방향이에요.
    public static class TelegraphShapes
    {
        // 전진 베기: 부채꼴(반경 radius, 각도 angle)을 앞으로 lunge만큼 밀면서 쓸고 지나간 자리.
        // 시작점의 쐐기 + 옆 폭이 radius·sin(angle/2)인 띠 + 끝의 호.
        public static List<Vector2> Lunge(float radius, float angleDeg, float lunge, int arcSegments = 12)
        {
            float half = angleDeg * 0.5f * Mathf.Deg2Rad;
            float side = radius * Mathf.Sin(half);
            float front = radius * Mathf.Cos(half);

            var points = new List<Vector2> { Vector2.zero };
            if (lunge > 1e-4f) points.Add(new Vector2(-side, front));
            for (int i = 0; i <= arcSegments; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)arcSegments);
                points.Add(new Vector2(Mathf.Sin(a) * radius, lunge + Mathf.Cos(a) * radius));
            }
            if (lunge > 1e-4f) points.Add(new Vector2(side, front));
            return points;
        }

        // 돌진·화살: 폭 width, 길이 length인 직사각형 (시작점이 뒤쪽 가운데).
        public static List<Vector2> Rect(float width, float length)
        {
            float w = width * 0.5f;
            return new List<Vector2> { new Vector2(-w, 0f), new Vector2(-w, length), new Vector2(w, length), new Vector2(w, 0f) };
        }
    }
}
