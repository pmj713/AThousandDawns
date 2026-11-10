using System.Collections.Generic;
using AThousandDawns.Fx;
using AThousandDawns.Prototype;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 갈림길 지도 미리보기: 플레이 중 메뉴로 띄워요 (게임의 갈림길 화면에 연결하기 전 확인용).
    // 길 셋(가운데가 짙은 길), 마우스를 올리면 짙어지고, 클릭하면 그 길 끝에서 층 사이 먹 번짐 연출. Esc나 메뉴를 다시 누르면 닫혀요.
    public static class InkForkMapPreview
    {
        const string Menu = "AThousandDawns/아트/연출 미리보기/갈림길 지도";

        [MenuItem(Menu)]
        static void Toggle()
        {
            var existing = Object.FindFirstObjectByType<ForkMapPreviewView>();
            if (existing != null) { Object.Destroy(existing.gameObject); return; }
            new GameObject("Fork Map Preview").AddComponent<ForkMapPreviewView>();
        }

        [MenuItem(Menu, true)]
        static bool CanToggle() => Application.isPlaying;
    }

    public sealed class ForkMapPreviewView : MonoBehaviour
    {
        static readonly List<bool> Dense = new List<bool> { false, true, false };
        static readonly string[] Labels = { "기물", "기연", "은자" };
        int _hover = -1;
        float _timeScale;

        void OnEnable() { _timeScale = Time.timeScale; Time.timeScale = 0f; } // 게임이 갈림길에서 멈추듯
        void OnDisable() => Time.timeScale = _timeScale;

        void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { Destroy(gameObject); return; }
            if (InkTransition.IsPlaying) return;
            UiScale.Apply();
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;

            var area = new Rect(UiScale.Width * 0.5f - 640f, UiScale.Height * 0.5f - 360f, 1280f, 720f);
            var ends = InkForkMap.Draw(area, Dense, _hover);

            var style = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            int hover = -1;
            for (int i = 0; i < ends.Length; i++)
            {
                var card = new Rect(ends[i].x - 90f, ends[i].y - 70f, 180f, 60f);
                if (card.Contains(Event.current.mousePosition)) hover = i;
                string label = Dense[i] ? $"{Labels[i]}\n<color=#ff6a5a>기운 짙음</color>" : $"{Labels[i]}\n<color=#3c6b3f>기운 옅음</color>";
                style.richText = true;
                if (GUI.Button(card, label, style))
                {
                    var origin = (Vector2)GUI.matrix.MultiplyPoint3x4(ends[i]); // 화면 픽셀 (위가 0)
                    InkTransition.Play(InkTransition.Kind.Floor, () => Destroy(gameObject), origin: origin);
                }
            }
            if (Event.current.type == EventType.Repaint) _hover = hover;
            GUI.Label(new Rect(area.x, area.yMax + 8f, area.width, 30f), "갈림길 지도 미리보기 · 길을 고르면 그 자리에서 먹이 번져요 · Esc 닫기");
        }
    }
}
