using System;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 지역마다 방 꾸밈을 바꿔 끼워요 (죽림: 대나무 · 폐사찰: 부서진 돌기둥·석탑·소나무·무너진 전각).
    // 방은 지역이 바뀌어도 같은 자리를 다시 쓰니, 두 꾸밈을 다 세워 두고 지역(RoomRun.Region)에 맞는 것만 켜요.
    // 충돌체는 원래 빌더의 (안 보이는) 상자 그대로라 게임 규칙은 그대로예요. 지역 번호가 목록보다 크면 마지막 꾸밈을 써요.
    public class InkRegionDressing : MonoBehaviour
    {
        [Serializable]
        public struct Set
        {
            public string name;
            public GameObject[] objects;
        }

        [SerializeField] Set[] regions = Array.Empty<Set>();
        [Tooltip("미리보기: 0 이상이면 판의 지역 대신 이 지역 꾸밈을 보여 줘요 (플레이 중 인스펙터에서 1로 바꾸면 폐사찰)")]
        [SerializeField] int previewRegion = -1;
        RoomRun _run;
        int _shown = -1;

        public void SetRegions(Set[] list) => regions = list ?? Array.Empty<Set>();

        void Start() => Show(0);

        void Update()
        {
            if (_run == null) _run = FindFirstObjectByType<RoomRun>();
            int region = previewRegion >= 0 ? previewRegion : _run != null ? _run.Region : 0;
            if (region != _shown) Show(region);
        }

        void Show(int region)
        {
            _shown = region;
            if (regions.Length == 0) return;
            int pick = Mathf.Clamp(region, 0, regions.Length - 1);
            for (int i = 0; i < regions.Length; i++)
                foreach (var go in regions[i].objects)
                    if (go != null) go.SetActive(i == pick);
        }
    }
}
