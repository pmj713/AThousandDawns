using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 천마 격파 엔딩의 새벽빛: RoomRun.IsEnding이 켜지면 EndingTime(실제 초) 0→6초 동안
    // 해(방향광)가 붉은 기 도는 새벽빛으로 밝아지며 낮게 기울고, 주변광도 따뜻하게 밝아져요. 엔딩이 끝나면 원래대로 돌려놔요.
    // 게임 코드는 건드리지 않고 RoomRun.IsEnding · EndingTime을 읽기만 해요
    public class InkEndingDawn : MonoBehaviour
    {
        [SerializeField] float duration = 6f;
        [SerializeField] Color dawnLight = new Color(1f, 0.78f, 0.58f);
        [SerializeField] float dawnIntensity = 1.8f;
        [SerializeField] Color dawnAmbient = new Color(0.98f, 0.86f, 0.74f);

        RoomRun _run;
        Light _sun;
        Color _lightColor, _ambient;
        float _intensity;
        Quaternion _sunRot;
        bool _saved;

        void Update()
        {
            if (_run == null) _run = FindFirstObjectByType<RoomRun>();
            if (_run == null) return;
            bool ending = _run.IsEnding;
            if (!ending)
            {
                if (_saved) Restore();
                return;
            }
            if (!_saved) Save();
            float t = _run.EndingTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, duration)));
            if (_sun != null)
            {
                _sun.color = Color.Lerp(_lightColor, dawnLight, k);
                _sun.intensity = Mathf.Lerp(_intensity, dawnIntensity, k);
                _sun.transform.rotation = Quaternion.Slerp(_sunRot, Quaternion.Euler(22f, _sunRot.eulerAngles.y + 30f, 0f), k);   // 낮게 비껴 드는 첫 햇살
            }
            RenderSettings.ambientLight = Color.Lerp(_ambient, dawnAmbient, k);
        }

        void Save()
        {
            _saved = true;
            _sun = RenderSettings.sun;
            if (_sun == null)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { _sun = l; break; }
            if (_sun != null) { _lightColor = _sun.color; _intensity = _sun.intensity; _sunRot = _sun.transform.rotation; }
            _ambient = RenderSettings.ambientLight;
        }

        void Restore()
        {
            _saved = false;
            if (_sun != null) { _sun.color = _lightColor; _sun.intensity = _intensity; _sun.transform.rotation = _sunRot; }
            RenderSettings.ambientLight = _ambient;
        }

        void OnDestroy()
        {
            if (_saved) Restore();
        }
    }
}
