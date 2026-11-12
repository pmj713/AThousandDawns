using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 천마총 이벤트 방 가운데 소품 (메인 아트 프리팹, 씬 빌더가 연결해요): 피의 연못 · 마교 제단 · 갇힌 무림맹 고수.
    // 고수를 구하면 애니메이터 "Free" + 쇠사슬("Chains")을 끄고, 일어나 포권한 뒤 사라져요.
    public sealed partial class RoomRun
    {
        [SerializeField] GameObject bloodPoolProp;
        [SerializeField] GameObject demonAltarProp;
        [SerializeField] GameObject captiveHeroProp;

        GameObject _eventProp;

        public GameObject EventProp => _eventProp;

        GameObject PropFor(string id) => id switch
        {
            "blood_pool" => bloodPoolProp,
            "demon_altar" => demonAltarProp,
            "captive_hero" => captiveHeroProp,
            _ => null,
        };

        // 방에 들어갈 때: 이전 소품을 치우고 이벤트 방이면 가운데에 세워요 (구한 고수는 다시 세우지 않아요)
        void PlaceEventProp(FloorRoom room)
        {
            if (_eventProp != null) Destroy(_eventProp);
            _eventProp = null;
            if (room == null || room.Kind != FloorRoomKind.Event) return;
            if (room.EventId == "captive_hero" && room.Cleared) return;
            var prefab = PropFor(room.EventId);
            if (prefab == null) return;
            _eventProp = Instantiate(prefab, combat.center, Quaternion.identity);
        }

        // 구출 전투에서 이겼을 때
        void FreeCaptiveHero()
        {
            if (_eventProp == null) return;
            var chains = _eventProp.transform.Find("Chains");
            if (chains != null) chains.gameObject.SetActive(false);
            var hero = _eventProp.GetComponentInChildren<Animator>();
            if (hero != null) hero.SetTrigger("Free");
            Destroy(_eventProp, AThousandDawns.Core.GameConfig.GetOptional("event.hero_leave_delay", 2.5f)); // 일어나 포권 (약 1.5초) 뒤
            _eventProp = null;
        }
    }
}
