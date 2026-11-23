using System;
using NUnit.Framework;
using RealistikOsu.Performance.Data;

namespace RealistikOsu.Performance.Tests.Data
{
    [TestFixture]
    public class VariantsTest
    {
        [TestCase(0, 0)]
        [TestCase(1 << 7, 1)]
        [TestCase(1 << 13, 2)]
        [TestCase((1 << 7) | (1 << 13), 1)]
        public void TestVariantOfMods(int mods, int expected)
        {
            Assert.That(Variants.OfMods(mods), Is.EqualTo(expected));
        }

        [Test]
        public void TestOnlyKnownTablesReachSql()
        {
            Assert.That(Variants.RxOfScoresTable("scores_ap"), Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => Variants.RxOfScoresTable("scores; DROP TABLE users"));
            Assert.Throws<ArgumentException>(() => Variants.RxOfScoresTable("lazer_scores"));
        }
    }
}
