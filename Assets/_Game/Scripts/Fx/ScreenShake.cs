using AThousandDawns.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace AThousandDawns.Fx
{
    // 화면 흔들림 (설정 · 접근성 "화면 흔들림" %를 곱해요, 0이면 꺼짐).
    // Kick(세기, 시간)을 부르면 시네머신 카메라에 ScreenShakeExtension이 붙어 흔들려요 (처음 부를 때 스스로 붙어요).
    //   맞았을 때 (받은 피해 비율만큼) · 절초 · 보스가 쓰러질 때.
    public static class ScreenShake
    {
        static float _strength, _start, _duration;

        // 지금 세기 (칸): 시간이 지나면 잦아들어요 (일시정지와 상관없이 실제 시간)
        public static float Current
        {
            get
            {
                if (_duration <= 0f) return 0f;
                float left = 1f - (Time.unscaledTime - _start) / _duration;
                return left > 0f ? _strength * left : 0f;
            }
        }

        // strength: 최대 흔들림 (칸), duration: 잦아드는 시간 (초)
        public static void Kick(float strength, float duration)
        {
            float scale = GameSettings.Current.screenShake / 100f;
            if (scale <= 0f || strength <= 0f) return;
            strength *= scale;
            if (strength < Current) return; // 더 센 흔들림이 이미 있으면 그대로
            _strength = strength;
            _duration = Mathf.Max(0.05f, duration);
            _start = Time.unscaledTime;
            ScreenShakeExtension.Ensure();
        }
    }

    public sealed class ScreenShakeExtension : CinemachineExtension
    {
        static ScreenShakeExtension _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var cam = FindFirstObjectByType<CinemachineCamera>();
            if (cam != null) _instance = cam.gameObject.AddComponent<ScreenShakeExtension>();
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise) return;
            float s = ScreenShake.Current;
            if (s <= 0f) return;
            float t = Time.unscaledTime * 28f;
            state.PositionCorrection += new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 0.6f, Mathf.PerlinNoise(t, 5.1f) - 0.5f) * (2f * s);
        }

        protected override void OnDestroy()
        {
            if (_instance == this) _instance = null;
            base.OnDestroy();
        }
    }
}
