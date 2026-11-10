using UnityEngine;

namespace AThousandDawns.Art
{
    // 마을 매화나무: 회귀록에서 피운 봉우리만큼 실제 나무에 꽃송이가 켜져요.
    // 꽃송이 카드(자식)는 씬 빌더가 나무 가지 위에 미리 달아 두고, 처음 피는 순서대로 정렬돼 있어요.
    // 게임 쪽에서 Show(피운 수, 전체 수)를 부르면 비율만큼 앞에서부터 켜요. 부르기 전에는 다 켜져 있어요.
    public class PlumBlossoms : MonoBehaviour
    {
        public static PlumBlossoms Instance { get; private set; }

        [SerializeField] Transform[] blossoms = new Transform[0];

        public int Count => blossoms.Length;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetBlossoms(Transform[] cards) => blossoms = cards;

        public void Show(int bloomed, int total)
        {
            int on = total <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(blossoms.Length * (float)bloomed / total), 0, blossoms.Length);
            for (int i = 0; i < blossoms.Length; i++)
                if (blossoms[i] != null) blossoms[i].gameObject.SetActive(i < on);
        }
    }
}
