using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 마을 매화나무: 회귀록에서 피운 봉우리만큼 실제 나무에 꽃송이가 켜져요.
    // 꽃송이 카드(자식)는 씬 빌더가 가지 끝에 미리 달아 두고, 피는 순서대로 정렬돼 있어요 (나무 전체에 흩어지게 섞인 순서).
    // 씬이 켜질 때 저장된 회귀록을 읽어 맞추고, 봉우리를 피우면(RegressionBook.BloomChanged) 바로 다시 맞춰요.
    public class PlumBlossoms : MonoBehaviour
    {
        public static PlumBlossoms Instance { get; private set; }

        [SerializeField] Transform[] blossoms = new Transform[0];

        public int Count => blossoms.Length;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnEnable()
        {
            Refresh();
            RegressionBook.BloomChanged += Refresh; // 봉우리를 피우면 바로 꽃이 켜져요
        }

        void OnDisable() => RegressionBook.BloomChanged -= Refresh;

        public void SetBlossoms(Transform[] cards) => blossoms = cards;

        public void Refresh() => Refresh(MetaSave.Load());

        public void Refresh(MetaSave meta) => Show(RegressionBook.BloomedCount(meta), RegressionBook.Nodes.Length);

        public void Show(int bloomed, int total)
        {
            int on = total <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(blossoms.Length * (float)bloomed / total), 0, blossoms.Length);
            for (int i = 0; i < blossoms.Length; i++)
                if (blossoms[i] != null) blossoms[i].gameObject.SetActive(i < on);
        }
    }
}
