using AThousandDawns.Player;
using NUnit.Framework;
using UnityEngine;

namespace AThousandDawns.Tests
{
    // 부품: 통맥 (기가 줄지 않고 지남) · 점혈 (기를 막음) · 한옥 (붙은 기물 최대 기 +2)
    public class MeridianPartTests
    {
        static MeridianBoard Board()
        {
            var board = new MeridianBoard(5, 4, 3, 3) { HanokMaxQi = 2 };
            board.AddDanjeon(new Vector2Int(0, 0));
            return board;
        }

        [Test]
        public void 통맥을_지나면_기가_줄지_않음()
        {
            var board = Board();
            Assert.AreEqual(0, board.QiAt(new Vector2Int(3, 0)), "거리 3이면 기 0");
            Assert.IsTrue(board.SetPart(new Vector2Int(1, 0), PartKind.Tongmaek));
            Assert.IsTrue(board.SetPart(new Vector2Int(2, 0), PartKind.Tongmaek));
            Assert.AreEqual(2, board.QiAt(new Vector2Int(3, 0)), "통맥 둘을 지나면 거리 1");
        }

        [Test]
        public void 점혈은_기를_막음()
        {
            var board = Board();
            Assert.AreEqual(1, board.QiAt(new Vector2Int(2, 0)));
            Assert.IsTrue(board.SetPart(new Vector2Int(1, 0), PartKind.Jeomhyeol));
            Assert.AreEqual(0, board.QiAt(new Vector2Int(1, 0)), "점혈 칸에는 기가 없어요");
            Assert.AreEqual(0, board.QiAt(new Vector2Int(2, 0)), "돌아가면 거리 4라 기 0");
            Assert.AreEqual(2, board.QiAt(new Vector2Int(0, 1)), "다른 쪽은 그대로");
        }

        [Test]
        public void 한옥은_붙은_칸의_최대_기를_올림()
        {
            var board = Board();
            Assert.IsTrue(board.SetPart(new Vector2Int(1, 1), PartKind.Hanok));
            Assert.AreEqual(2, board.MaxQiBonusAt(new Vector2Int(1, 0)));
            Assert.AreEqual(2, board.MaxQiBonusAt(new Vector2Int(2, 1)));
            Assert.AreEqual(0, board.MaxQiBonusAt(new Vector2Int(2, 2)), "대각선은 안 붙어요");
        }

        [Test]
        public void 단전_칸과_부품_칸은_겹치지_않음()
        {
            var board = Board();
            Assert.IsFalse(board.SetPart(new Vector2Int(0, 0), PartKind.Hanok), "단전 칸에는 못 놓아요");
            Assert.IsTrue(board.SetPart(new Vector2Int(2, 2), PartKind.Hanok));
            Assert.IsFalse(board.SetPart(new Vector2Int(2, 2), PartKind.Tongmaek), "부품 위에 부품 못 놓아요");
            Assert.IsFalse(board.AddDanjeon(new Vector2Int(2, 2)), "부품 칸에는 단전 못 열어요");
        }
    }
}
