using System.Collections.Generic;
using System.IO;
using AThousandDawns.Data;
using AThousandDawns.EditorTools;
using NUnit.Framework;

namespace AThousandDawns.Tests
{
    public class SheetDataTests
    {
        [Test]
        public void Csv_따옴표와_쉼표가_든_칸을_읽음()
        {
            var rows = CsvReader.Parse("key,value,note\r\na,1,\"쉼표, 있음\"\r\nb,\"2\",\"따옴표 \"\"안\"\"\"\r\n");
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("쉼표, 있음", rows[1][2]);
            Assert.AreEqual("따옴표 \"안\"", rows[2][2]);
        }

        [TestCase("40%", 0.4f)]
        [TestCase("1,100", 1100f)]
        [TestCase(" 0.18 ", 0.18f)]
        [TestCase("150%", 1.5f)]
        public void 시트_표시값을_숫자로_읽음(string raw, float expected)
        {
            Assert.IsTrue(SheetValue.TryParseFloat(raw, out float value));
            Assert.AreEqual(expected, value, 1e-5f);
        }

        [Test]
        public void 같은_키가_두번이면_오류()
        {
            var errors = new List<string>();
            BalanceTable.Read("key,value\na,1\na,2\n", errors);
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void 프로젝트의_data_balance_CSV에_오류가_없고_필수_키가_있음()
        {
            Assert.IsTrue(File.Exists(SheetImporter.BalanceCsvPath), "data_balance.csv가 없어요.");
            var errors = new List<string>();
            var entries = BalanceTable.Read(File.ReadAllText(SheetImporter.BalanceCsvPath), errors);
            CollectionAssert.IsEmpty(errors);

            var keys = new HashSet<string>();
            foreach (var e in entries) keys.Add(e.key);
            foreach (var required in new[] { "damage.toughness_floor_ratio", "damage.gongryeok_per_point", "player.move_speed", "dash.iframe", "burn.per_stack", "enemy.hitstun" })
                Assert.IsTrue(keys.Contains(required), $"'{required}' 키가 없어요.");
        }
    }
}
