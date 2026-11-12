using UnityEngine;

namespace AThousandDawns.Art
{
    // 이 오브젝트가 켜지고 꺼질 때 흩어진 members도 같이 켜고 꺼요 (부모가 제각각인 배경을 한 묶음으로 다루려고)
    public class InkActiveMirror : MonoBehaviour
    {
        [SerializeField] GameObject[] members = new GameObject[0];

        public void Setup(GameObject[] list) => members = list;

        void OnEnable() => Set(true);
        void OnDisable() => Set(false);

        void Set(bool on)
        {
            foreach (var m in members)
                if (m != null) m.SetActive(on);
        }
    }
}
