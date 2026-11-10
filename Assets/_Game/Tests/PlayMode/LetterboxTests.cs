using System.Collections;
using AThousandDawns.Core;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AThousandDawns.Tests
{
    // 설정 → 화면 비율: 카메라와 IMGUI가 검은 띠 안쪽 영역만 써요
    public class LetterboxTests
    {
        [TearDown]
        public void Restore()
        {
            new GameSettings().Save();
            GameSettings.Reload();
        }

        [UnityTest]
        public IEnumerator 고정_비율이면_카메라와_UI가_그_비율()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var cam = Camera.main;

            // 지금 창보다 훨씬 넓은 비율 → 위아래 띠
            GameSettings.Current.aspect = GameSettings.AspectMode.Ultra21x9;
            yield return null;
            yield return null;
            float screenRatio = (float)Screen.width / Screen.height;
            Assert.Less(screenRatio, 21f / 9f, "시험 창은 21:9보다 좁아요");
            Assert.Less(cam.rect.height, 1f, "위아래 검은 띠");
            Assert.AreEqual(1f, cam.rect.width, 1e-3f);
            Assert.AreEqual(21f / 9f, cam.pixelRect.width / cam.pixelRect.height, 0.02f);
            Assert.AreEqual(21f / 9f, UiScale.Width / UiScale.Height, 0.02f, "UI도 같은 비율");

            GameSettings.Current.aspect = GameSettings.AspectMode.Auto;
            yield return null;
            yield return null;
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), cam.rect, "자동이면 화면 전체");
            PrototypeArena.AutoSpawn = true;
        }
    }
}
