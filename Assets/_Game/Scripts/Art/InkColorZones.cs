using UnityEngine;

namespace AThousandDawns.Art
{
    // 채색 전환: 지역마다 색을 빼던 처리(죽림 0.9 → 천마총 0, 지역 사이 한지빛 바램)를 걷어 냈어요.
    // 아직 이 값을 읽는 셰이더(이펙트 쪽 등)를 위해 전역 값 _InkColorAmount를 늘 1(원래 색 전부)로 둬요.
    public class InkColorZones : MonoBehaviour
    {
        static readonly int AmountId = Shader.PropertyToID("_InkColorAmount");

        void OnEnable() => Shader.SetGlobalFloat(AmountId, 1f);
    }
}
