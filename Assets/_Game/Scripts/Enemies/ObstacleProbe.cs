using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 벽·대나무·목인장까지의 거리. 플레이어와 적은 장애물로 치지 않아요.
    public static class ObstacleProbe
    {
        static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static float Distance(Vector3 origin, Vector3 direction, float radius, float maxDistance)
        {
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, Hits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = maxDistance;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // 시작부터 겹쳐 있던 것 (바닥 등)
                if (IsActor(hit.collider)) continue;
                if (hit.distance < best) best = hit.distance;
            }
            return best;
        }

        public static bool IsActor(Collider collider) =>
            collider.GetComponentInParent<EnemyBrain>() != null || collider.GetComponentInParent<PlayerController>() != null;
    }
}
