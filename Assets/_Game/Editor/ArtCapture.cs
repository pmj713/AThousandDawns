using System.IO;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 아트 확인용: 열린 씬의 Main Camera 각도·화각 그대로 지정한 자리를 찍어 Temp/ArtCapture/에 PNG로 저장해요.
    // 게임을 돌리지 않고 마을처럼 멀리 있는 곳을 플레이 화면 각도로 볼 수 있어요.
    public static class ArtCapture
    {
        const string OutFolder = "Temp/ArtCapture";

        [MenuItem("AThousandDawns/아트/마을 미리보기 저장")]
        static void CaptureVillage()
        {
            var center = new Vector3(360f, 0f, 0f); // PrototypeSceneBuilder.VillageCenter
            Capture("village_near", center + new Vector3(-4f, 0f, 0f), 1f);
            Capture("village_far", center, 1.9f);
            Capture("village_inn", center + new Vector3(-9f, 0f, 0f), 0.75f);
            Capture("village_tree", center + new Vector3(-3f, 1.5f, 1f), 0.45f);
            EditorUtility.RevealInFinder(Path.GetFullPath(OutFolder));
        }

        // target: 화면 가운데에 올 바닥 자리. zoom: 원래 카메라 거리의 배수
        public static void Capture(string name, Vector3 target, float zoom, int width = 1920, int height = 1080)
        {
            var main = Camera.main;
            if (main == null) { Debug.LogWarning("ArtCapture: Main Camera가 없어요"); return; }
            var fwd = main.transform.forward;
            float dist = fwd.y < -0.01f ? main.transform.position.y / -fwd.y : 15f; // 지금 카메라에서 바닥까지

            var go = new GameObject("ArtCapture Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.transform.SetPositionAndRotation(target - fwd * dist * zoom, main.transform.rotation);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            Directory.CreateDirectory(OutFolder);
            File.WriteAllBytes($"{OutFolder}/{name}.png", tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
            Debug.Log($"ArtCapture: {OutFolder}/{name}.png");
        }
    }
}
