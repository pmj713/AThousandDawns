using UnityEngine;

namespace AThousandDawns.Core
{
    // 설정 → 화면 비율: 고정 비율이면 화면 가운데에 그 비율의 영역만 그리고 남는 곳은 검은 띠 (레터박스 · 필러박스).
    // 모든 카메라의 그리는 영역(Camera.rect)을 맞추고, IMGUI(UiScale)도 같은 영역 안에 그려요. 자동이면 화면 전체.
    public sealed class Letterbox : MonoBehaviour
    {
        // 그리는 영역 (픽셀, 왼쪽 아래가 0 · Camera.pixelRect와 같은 기준)
        public static Rect Viewport => Fit(Screen.width, Screen.height, GameSettings.Ratio(GameSettings.Current.aspect));

        // 화면 w×h 안에 ratio(가로÷세로) 영역을 가운데로. ratio가 0이면 전체.
        public static Rect Fit(float w, float h, float ratio)
        {
            if (ratio <= 0f || w <= 0f || h <= 0f) return new Rect(0f, 0f, w, h);
            if (w / h > ratio)
            {
                float vw = Mathf.Round(h * ratio);
                return new Rect(Mathf.Floor((w - vw) * 0.5f), 0f, vw, h); // 옆이 남음 (필러박스)
            }
            float vh = Mathf.Round(w / ratio);
            return new Rect(0f, Mathf.Floor((h - vh) * 0.5f), w, vh); // 위아래가 남음 (레터박스)
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Letterbox");
            DontDestroyOnLoad(go);
            go.AddComponent<Letterbox>();
        }

        void LateUpdate()
        {
            var vp = Viewport;
            var normalized = new Rect(vp.x / Screen.width, vp.y / Screen.height, vp.width / Screen.width, vp.height / Screen.height);
            foreach (var cam in Camera.allCameras)
                if (cam.targetTexture == null && cam.rect != normalized) cam.rect = normalized;
        }

        // 검은 띠는 다른 IMGUI보다 위에 (카메라가 그리지 않는 곳의 남은 화면도 가려요)
        void OnGUI()
        {
            var vp = Viewport;
            if (vp.width >= Screen.width && vp.height >= Screen.height) return;
            GUI.depth = -10000;
            GUI.matrix = Matrix4x4.identity;
            var old = GUI.color;
            GUI.color = Color.black;
            float top = Screen.height - vp.yMax;
            if (vp.x > 0f)
            {
                GUI.DrawTexture(new Rect(0f, 0f, vp.x, Screen.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(vp.xMax, 0f, Screen.width - vp.xMax, Screen.height), Texture2D.whiteTexture);
            }
            if (vp.y > 0f)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, top), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0f, Screen.height - vp.y, Screen.width, vp.y), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }
    }
}
