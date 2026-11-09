using AThousandDawns.Core;
using AThousandDawns.Prototype;
using NUnit.Framework;

namespace AThousandDawns.Tests
{
    // 설정: 진행 저장과 따로 저장되고, 설정창 줄마다 값이 돌아가요.
    public class SettingsTests
    {
        [Test]
        public void 저장하고_다시_읽으면_그대로()
        {
            var s = GameSettings.Current;
            s.aim = GameSettings.AimMode.Move;
            s.damageNumbers = false;
            s.screenShake = 40;
            s.Save();
            GameSettings.Reload();
            Assert.AreEqual(GameSettings.AimMode.Move, GameSettings.Current.aim);
            Assert.IsFalse(GameSettings.Current.damageNumbers);
            Assert.AreEqual(40, GameSettings.Current.screenShake);
            StringAssert.Contains("settings_test_", GameSettings.FilePath, "테스트는 임시 설정 파일");
            Assert.AreNotEqual(MetaSave.FilePath, GameSettings.FilePath, "진행 저장과 따로");

            new GameSettings().Save(); // 다음 테스트를 위해 기본값으로
            GameSettings.Reload();
        }

        [Test]
        public void 설정창_줄마다_값이_돌아감()
        {
            new GameSettings().Save();
            GameSettings.Reload();
            var panel = new SettingsPanel();
            int Row(string label) => System.Linq.Enumerable.First(System.Linq.Enumerable.Range(0, panel.AllRows.Count), i => panel.AllRows[i].Label == label);

            panel.Change(Row("적 체력바"), +1);
            Assert.AreEqual(GameSettings.EnemyBars.Off, GameSettings.Current.enemyBars, "피격 시만 → 끄기");
            panel.Change(Row("적 체력바"), +1);
            Assert.AreEqual(GameSettings.EnemyBars.Always, GameSettings.Current.enemyBars, "한 바퀴");

            panel.Change(Row("화면 흔들림"), -1);
            Assert.AreEqual(90, GameSettings.Current.screenShake);
            for (int i = 0; i < 20; i++) panel.Change(Row("화면 흔들림"), +1);
            Assert.AreEqual(100, GameSettings.Current.screenShake, "100까지");

            panel.Change(Row("확인 창"), +1);
            Assert.IsFalse(GameSettings.Current.confirmDialogs);
            GameSettings.Reload();
            Assert.IsFalse(GameSettings.Current.confirmDialogs, "바꾸면 바로 저장");

            new GameSettings().Save();
            GameSettings.Reload();
        }

        [Test]
        public void 화면_비율_글자()
        {
            Assert.AreEqual("16:9", SettingsPanel.Aspect(1920, 1080));
            Assert.AreEqual("16:10", SettingsPanel.Aspect(1920, 1200));
            Assert.AreEqual("21:9", SettingsPanel.Aspect(2560, 1080));
            Assert.AreEqual("21:9", SettingsPanel.Aspect(3440, 1440));
            Assert.AreEqual("4:3", SettingsPanel.Aspect(1024, 768));
        }
    }
}
