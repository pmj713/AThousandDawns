using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 방 풀숲 (꾸밈). 사람 · 적 모두 지나갈 수 있게 꾸밈 밑 충돌체를 꺼요 (지역 꾸밈 소나무 · 갈대 · 가시 덤불도).
    // 청죽사는 이제 숨지 않아서, 죽림 방에서는 RoomRun이 풀숲을 아예 꺼요 (RoomRun.Layout).
    public sealed class AmbushBush : MonoBehaviour
    {
        [SerializeField] Transform visual;

        void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            var root = visual != null ? visual : transform;
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
                if (!col.isTrigger) col.enabled = false;
        }
    }
}
