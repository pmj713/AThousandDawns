using UnityEngine;

namespace AThousandDawns.Combat
{
    // 판정은 바닥 평면(XZ)에서만 해요. 높이는 무시.
    public static class HitShapes
    {
        public static bool InSector(Vector3 origin, Vector3 forward, float radius, float angleDeg, Vector3 point, float targetRadius = 0f)
        {
            Vector2 o = new Vector2(origin.x, origin.z);
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 f = new Vector2(forward.x, forward.z);
            Vector2 toTarget = p - o;
            float dist = toTarget.magnitude;
            if (dist > radius + targetRadius) return false;
            if (dist <= targetRadius || f.sqrMagnitude < 1e-6f) return true;
            float angle = Vector2.Angle(f, toTarget);
            // 대상의 몸 크기만큼 각도를 넓혀 줘요 (가장자리에 걸친 적도 맞게).
            float slack = Mathf.Asin(Mathf.Clamp01(targetRadius / dist)) * Mathf.Rad2Deg;
            return angle <= angleDeg * 0.5f + slack;
        }

        // 몸 앞으로 뻗은 직사각형 (길이 length × 폭 width, 원점이 짧은 변 가운데). 대상 몸 크기만큼 넓혀 줘요
        public static bool InRect(Vector3 origin, Vector3 forward, float length, float width, Vector3 point, float targetRadius = 0f)
        {
            Vector2 f = new Vector2(forward.x, forward.z);
            if (f.sqrMagnitude < 1e-6f) return false;
            f.Normalize();
            Vector2 side = new Vector2(f.y, -f.x);
            Vector2 toTarget = new Vector2(point.x - origin.x, point.z - origin.z);
            float along = Vector2.Dot(toTarget, f), across = Vector2.Dot(toTarget, side);
            return along >= -targetRadius && along <= length + targetRadius && Mathf.Abs(across) <= width * 0.5f + targetRadius;
        }
    }
}
