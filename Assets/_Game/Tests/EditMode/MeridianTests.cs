using AThousandDawns.Player;
using NUnit.Framework;
using UnityEngine;

namespace AThousandDawns.Tests
{
    // 경맥도 탭: 칸의 기 = 단전마다 max(0, 공력 − 거리)의 합. 거리는 상하좌우로 센 칸 수.
    public class MeridianTests
    {
        [Test]
        public void 단전_하나면_멀어질수록_기가_1씩_줄고_대각선은_2칸()
        {
            var board = new MeridianBoard(5, 4, 3, 3);
            Assert.IsTrue(board.AddDanjeon(new Vector2Int(2, 1)));
            Assert.AreEqual(3, board.QiAt(new Vector2Int(2, 1)), "단전 칸 = 공력");
            Assert.AreEqual(2, board.QiAt(new Vector2Int(3, 1)));
            Assert.AreEqual(1, board.QiAt(new Vector2Int(3, 2)), "대각선은 2칸");
            Assert.AreEqual(0, board.QiAt(new Vector2Int(0, 3)));
        }

        [Test]
        public void 단전이_여럿이면_더함()
        {
            var board = new MeridianBoard(5, 4, 3, 3);
            board.AddDanjeon(new Vector2Int(0, 0));
            board.AddDanjeon(new Vector2Int(2, 0));
            Assert.AreEqual(2 + 2, board.QiAt(new Vector2Int(1, 0)));
            Assert.AreEqual(3 + 1, board.QiAt(new Vector2Int(0, 0)));
        }

        [Test]
        public void 단전은_최대_개수까지만_판_안에만()
        {
            var board = new MeridianBoard(5, 4, 2, 3);
            Assert.IsTrue(board.AddDanjeon(new Vector2Int(0, 0)));
            Assert.IsFalse(board.AddDanjeon(new Vector2Int(0, 0)), "같은 칸");
            Assert.IsFalse(board.AddDanjeon(new Vector2Int(5, 0)), "판 밖");
            Assert.IsTrue(board.AddDanjeon(new Vector2Int(4, 3)));
            Assert.IsFalse(board.AddDanjeon(new Vector2Int(2, 2)), "최대 2개");
        }

        [Test]
        public void 기혈은_그_칸만_기_1_더함()
        {
            var board = new MeridianBoard(5, 4, 3, 3);
            board.AddDanjeon(new Vector2Int(0, 0));
            Assert.IsTrue(board.OpenAcupoint(new Vector2Int(2, 0), Acupoint.Gihyeol));
            Assert.AreEqual(1 + 1, board.QiAt(new Vector2Int(2, 0)));
            Assert.AreEqual(0, board.QiAt(new Vector2Int(3, 0)), "옆 칸은 그대로");
            Assert.IsFalse(board.OpenAcupoint(new Vector2Int(2, 0), Acupoint.Hanhyeol), "한 칸에 하나");
        }

        [Test]
        public void 통혈을_지나면_기가_줄지_않음()
        {
            var board = new MeridianBoard(5, 4, 3, 3);
            board.AddDanjeon(new Vector2Int(0, 0));
            board.OpenAcupoint(new Vector2Int(1, 0), Acupoint.Tonghyeol);
            Assert.AreEqual(3, board.QiAt(new Vector2Int(1, 0)), "통혈 칸 = 단전과 같음");
            Assert.AreEqual(2, board.QiAt(new Vector2Int(2, 0)), "통혈 너머 한 칸");
            Assert.AreEqual(1, board.QiAt(new Vector2Int(3, 0)));
            Assert.AreEqual(2, board.QiAt(new Vector2Int(0, 1)), "통혈을 안 거치는 칸은 그대로");
        }

        [Test]
        public void 한혈_칸의_기물은_최대_기가_1_오름()
        {
            var board = new MeridianBoard(5, 4, 3, 3);
            board.OpenAcupoint(new Vector2Int(1, 1), Acupoint.Hanhyeol);
            Assert.AreEqual(1, board.MaxQiBonusAt(new Vector2Int(1, 1)));
            var item = new OwnedItem { Def = new Items.ItemDefinition { minQi = 1, maxQi = 2 }, Qi = 3, OnBoard = true, MaxQiBonus = 1 };
            Assert.AreEqual(QiState.Circulating, PlayerItems.StateOf(item), "기 3이 최대 기 2+1 안");
            Assert.AreEqual(3, PlayerItems.LevelOf(item));
        }

        [Test]
        public void 수련치가_차면_레벨이_오르고_개혈_대기가_쌓임()
        {
            var level = new PlayerLevel(25f, 3f);
            Assert.AreEqual(28, level.NeedForNext, "25 + 1×3");
            Assert.AreEqual(0, level.Add(27));
            Assert.AreEqual(2, level.Add(1 + 31), "28 채우고 다음 31도 채움");
            Assert.AreEqual(3, level.Level);
            Assert.AreEqual(2, level.Pending);
            Assert.IsTrue(level.ConsumePending());
            Assert.AreEqual(1, level.Pending);
        }

        [Test]
        public void 공력이_오르면_기도_오름()
        {
            var board = new MeridianBoard(5, 4, 3, 3);
            board.AddDanjeon(new Vector2Int(0, 0));
            Assert.AreEqual(0, board.QiAt(new Vector2Int(3, 0)));
            board.Power = 4;
            Assert.AreEqual(1, board.QiAt(new Vector2Int(3, 0)));
        }
    }
}
