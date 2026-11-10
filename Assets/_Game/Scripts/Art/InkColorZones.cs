using System;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 지역마다 색을 얼마나 남길지 정해요 (수묵 셰이더·화면 종이 효과가 전역 값 _InkColorAmount를 읽어요).
    //   1 = 원래 색 전부, 0 = 재질에 정한 수묵 채도 그대로. 마을은 1, 그 밖(죽림)은 0.9 — 게임이 진행될수록 지역마다 낮춰 갈 계획
    // 플레이어(또는 카메라)가 어느 구역 안에 있는지 보고, 구역을 넘나들면 천천히 바뀌어요.
    public class InkColorZones : MonoBehaviour
    {
        static readonly int AmountId = Shader.PropertyToID("_InkColorAmount");

        [Serializable]
        public struct Zone
        {
            public string name;
            public Vector3 center;
            public Vector2 size;      // 가로(x) × 세로(z)
            [Range(0f, 1f)] public float amount;
        }

        [SerializeField, Range(0f, 1f)] float defaultAmount = 0.9f;   // 어느 구역에도 없을 때 (죽림)
        [SerializeField] Zone[] zones = Array.Empty<Zone>();
        [SerializeField] float fadeSpeed = 1.5f;                       // 초당 바뀌는 양
        [SerializeField] Transform target;                             // 비우면 플레이어 태그 → 메인 카메라

        float _current = -1f;

        public void SetZones(float fallback, Zone[] list)
        {
            defaultAmount = fallback;
            zones = list ?? Array.Empty<Zone>();
        }

        void LateUpdate()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<AThousandDawns.Player.PlayerController>();
                target = player != null ? player.transform : Camera.main != null ? Camera.main.transform : null;
            }
            float goal = defaultAmount;
            if (target != null)
            {
                var p = target.position;
                foreach (var z in zones)
                    if (Mathf.Abs(p.x - z.center.x) <= z.size.x * 0.5f && Mathf.Abs(p.z - z.center.z) <= z.size.y * 0.5f)
                    {
                        goal = z.amount;
                        break;
                    }
            }
            // 처음엔 바로, 이후엔 천천히 (방 이동은 순간이동이라 화면이 어두운 사이에 거의 다 바뀌어요)
            _current = _current < 0f ? goal : Mathf.MoveTowards(_current, goal, fadeSpeed * Time.deltaTime);
            Shader.SetGlobalFloat(AmountId, _current);
        }

        void OnDisable() => Shader.SetGlobalFloat(AmountId, 0f);
    }
}
