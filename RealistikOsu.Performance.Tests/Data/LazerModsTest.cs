using NUnit.Framework;
using RealistikOsu.Performance.Data;

namespace RealistikOsu.Performance.Tests.Data
{
    [TestFixture]
    public class LazerModsTest
    {
        [TestCase(0, "[]", true)]
        [TestCase(0, "[{\"acronym\":\"HD\",\"settings\":{}}]", true)]
        [TestCase(0, "[{\"acronym\":\"HD\",\"settings\":{\"only_fade_approach_circles\":true}}]", false)]
        [TestCase(0, "[{\"acronym\":\"DT\",\"settings\":{\"speed_change\":1.3}}]", true)]
        [TestCase(0, "[{\"acronym\":\"RX\",\"settings\":{}}]", true)]
        [TestCase(0, "[{\"acronym\":\"AP\",\"settings\":{}}]", true)]
        [TestCase(0, "[{\"acronym\":\"DA\",\"settings\":{}}]", false)]
        [TestCase(0, "[{\"acronym\":\"HR\",\"settings\":{}}]", true)]
        [TestCase(3, "[{\"acronym\":\"HR\",\"settings\":{}}]", false)]
        [TestCase(3, "[{\"acronym\":\"MR\",\"settings\":{}}]", true)]
        [TestCase(0, "[{\"acronym\":\"MR\",\"settings\":{}}]", false)]
        [TestCase(3, "[{\"acronym\":\"4K\",\"settings\":{}}]", true)]
        [TestCase(0, "[{\"acronym\":\"HD\",\"settings\":{}},{\"acronym\":\"DA\",\"settings\":{}}]", false)]
        [TestCase(0, "{}", true)]
        public void TestRankedMods(int mode, string mods, bool expected)
        {
            Assert.That(LazerMods.IsRanked(mode, mods), Is.EqualTo(expected));
        }
    }
}
