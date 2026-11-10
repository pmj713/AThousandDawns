using System;
using System.IO;
using AThousandDawns.Core;
using NUnit.Framework;
using UnityEngine;

namespace AThousandDawns.Tests
{
    // 모든 테스트가 진짜 설정 파일(settings.json) 대신 임시 파일을 써요 (기본값에서 시작). 튜토리얼은 꺼 둬요.
    [SetUpFixture]
    public class TestSettingsFixture
    {
        string _path;

        [OneTimeSetUp]
        public void UseTempSettings()
        {
            AThousandDawns.Prototype.RoomRun.TutorialEnabled = false; // 튜토리얼 테스트만 켜요 (나머지는 첫 판도 바로 갈림길)
            _path = Path.Combine(Application.temporaryCachePath, $"settings_test_{Guid.NewGuid():N}.json");
            GameSettings.PathOverride = _path;
            GameSettings.Reload();
        }

        [OneTimeTearDown]
        public void RestoreSettings()
        {
            AThousandDawns.Prototype.RoomRun.TutorialEnabled = true;
            if (File.Exists(_path)) File.Delete(_path);
            GameSettings.PathOverride = null;
            GameSettings.Reload();
        }
    }
}
