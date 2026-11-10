using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AThousandDawns.Core;
using AThousandDawns.Prototype;
using NUnit.Framework;

namespace AThousandDawns.Tests
{
    // 무공 요결: 조작은 처음부터, 나머지는 첫 안내를 본 뒤에 펼쳐져요. 코드의 모든 첫 안내 id가 요결에 있어야 해요.
    public class HintBookTests
    {
        [Test]
        public void 조작은_처음부터_나머지는_본_안내만()
        {
            var meta = new MetaSave();
            var first = HintBook.Unlocked(meta);
            Assert.IsTrue(first.All(e => e.Group == "조작"), "처음엔 조작만");
            Assert.AreEqual(HintBook.All.Count(e => e.Always), first.Count);

            meta.seenHints.Add("inn");
            meta.seenHints.Add("qi_deviated");
            var later = HintBook.Unlocked(meta);
            Assert.AreEqual(first.Count + 2, later.Count);
            Assert.IsTrue(later.Any(e => e.Id == "inn"));
        }

        [Test]
        public void 코드의_첫_안내는_모두_요결에_있음()
        {
            var ids = HintBook.All.Select(e => e.Id).ToHashSet();
            Assert.AreEqual(HintBook.All.Length, ids.Count, "id가 겹치면 안 돼요");
            var missing = Directory.GetFiles("Assets/_Game/Scripts/Prototype", "*.cs")
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"Notify\(""([a-z_]+)""").Cast<Match>().Select(m => m.Groups[1].Value))
                .Where(id => !ids.Contains(id)).Distinct().ToList();
            CollectionAssert.IsEmpty(missing, "요결에 없는 첫 안내");
        }
    }
}
