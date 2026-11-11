using System;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 지역마다 색을 얼마나 남길지 정해요 (수묵 셰이더·화면 종이 효과가 전역 값 _InkColorAmount를 읽어요).
    //   1 = 원래 색 전부, 0 = 재질에 정한 수묵 채도 그대로. 마을은 1, 판은 지역이 깊어질수록 한 단계씩 바래요
    //   (죽림 0.9 → 폐사찰 0.6 → 장강 수채 0.3 → 천마총 0: 같은 폭으로, 마지막엔 색이 다 빠진 먹빛).
    // 플레이어(또는 카메라)가 어느 구역 안에 있는지 보고, 구역을 넘나들면 천천히 바뀌어요.
    // 지역 사이 연출 (아트 목록 1. 연출): 다음 지역에 들어서면 (갈림길을 고르고 1층에 내려선 뒤) 색이 2.5초에 걸쳐
    // 눈에 보이게 한 단계 빠지고, 그동안 화면이 한지빛으로 살짝 바래요.
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

        [SerializeField, Range(0f, 1f)] float defaultAmount = 0.9f;   // 어느 구역에도 없을 때 (첫 지역 죽림)
        [SerializeField] float[] regionAmounts = { 0.9f, 0.6f, 0.3f, 0f }; // 지역 번호(RoomRun.Region)별 판의 색
        [SerializeField] Zone[] zones = Array.Empty<Zone>();
        [SerializeField] float fadeSpeed = 1.5f;                       // 초당 바뀌는 양
        [SerializeField] float drainSeconds = 2.5f;                    // 지역 사이 색 바램 길이
        [SerializeField] Transform target;                             // 비우면 플레이어 태그 → 메인 카메라

        float _current = -1f;
        RoomRun _run;
        int _region = -1;
        float _drainStart = -1f, _drainFrom, _drainTo;

        static readonly Color Paper = new Color(0.937f, 0.902f, 0.824f);

        public void SetZones(float fallback, Zone[] list)
        {
            defaultAmount = fallback;
            zones = list ?? Array.Empty<Zone>();
        }

        // 지역 사이 색 바램을 바로 틀어요 (to = 바랜 뒤 색 양). 미리보기 메뉴에서도 써요
        public void Drain(float to)
        {
            _drainFrom = _current < 0f ? defaultAmount : _current;
            _drainTo = to;
            _drainStart = Time.unscaledTime;
            defaultAmount = to;
        }

        public float RegionAmount(int region) => regionAmounts.Length == 0 ? defaultAmount : regionAmounts[Mathf.Clamp(region, 0, regionAmounts.Length - 1)];

        void LateUpdate()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<AThousandDawns.Player.PlayerController>();
                target = player != null ? player.transform : Camera.main != null ? Camera.main.transform : null;
            }
            TrackRegion();

            if (_drainStart >= 0f)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _drainStart) / drainSeconds);
                _current = Mathf.Lerp(_drainFrom, _drainTo, t * t * (3f - 2f * t));
                if (t >= 1f) _drainStart = -1f;
                Shader.SetGlobalFloat(AmountId, _current);
                return;
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

        // 게임의 지역 번호를 따라가요. 처음엔 그 지역 색으로 바로, 지역이 바뀌면 갈림길을 고른 뒤 바램 연출
        void TrackRegion()
        {
            if (_run == null)
            {
                _run = FindAnyObjectByType<RoomRun>();
                if (_run == null) return;
            }
            int region = _run.Region;
            if (_region < 0)
            {
                _region = region;
                defaultAmount = RegionAmount(region);
                return;
            }
            if (region == _region || _run.IsForking) return;
            _region = region;
            Drain(RegionAmount(region));
        }

        // 바래는 동안 화면에 한지빛이 살짝 스며들었다 빠져요
        void OnGUI()
        {
            if (_drainStart < 0f) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _drainStart) / drainSeconds);
            float a = Mathf.Sin(t * Mathf.PI) * 0.32f;
            if (a <= 0.001f) return;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = -100;
            GUI.color = new Color(Paper.r, Paper.g, Paper.b, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        void OnDisable() => Shader.SetGlobalFloat(AmountId, 0f);
    }
}
