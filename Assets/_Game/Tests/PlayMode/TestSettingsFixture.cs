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
            AThousandDawns.Prototype.RoomRun.Transitions = false; // 수묵 전환 연출도 꺼요 (층·회귀가 바로 바뀌게)
            AThousandDawns.Player.PlayerItems.ModeOverride = 0; // 예전 테스트는 경맥도(격자판) 기준이에요. 장비 칸 테스트(EquipTests)만 장비 칸으로 바꿔요
            AThousandDawns.Prototype.RoomBudget.FantasyOverride = 0; // 예전 테스트는 지역마다 무협 적만 나온다고 봐요
            _path = Path.Combine(Application.temporaryCachePath, $"settings_test_{Guid.NewGuid():N}.json");
            GameSettings.PathOverride = _path;
            GameSettings.Reload();
        }

        [OneTimeTearDown]
        public void RestoreSettings()
        {
            AThousandDawns.Prototype.RoomRun.TutorialEnabled = true;
            AThousandDawns.Prototype.RoomRun.Transitions = true;
            AThousandDawns.Player.PlayerItems.ModeOverride = -1;
            AThousandDawns.Prototype.RoomBudget.FantasyOverride = -1;
            if (File.Exists(_path)) File.Delete(_path);
            GameSettings.PathOverride = null;
            GameSettings.Reload();
        }
    }
}
