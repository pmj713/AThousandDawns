using System;
using System.IO;
using AThousandDawns.Core;
using NUnit.Framework;
using UnityEngine;

namespace AThousandDawns.Tests
{
    // 모든 테스트가 진짜 설정 파일(settings.json) 대신 임시 파일을 써요 (기본값에서 시작).
    [SetUpFixture]
    public class TestSettingsFixture
    {
        string _path;

        [OneTimeSetUp]
        public void UseTempSettings()
        {
            _path = Path.Combine(Application.temporaryCachePath, $"settings_test_{Guid.NewGuid():N}.json");
            GameSettings.PathOverride = _path;
            GameSettings.Reload();
        }

        [OneTimeTearDown]
        public void RestoreSettings()
        {
            if (File.Exists(_path)) File.Delete(_path);
            GameSettings.PathOverride = null;
            GameSettings.Reload();
        }
    }
}
