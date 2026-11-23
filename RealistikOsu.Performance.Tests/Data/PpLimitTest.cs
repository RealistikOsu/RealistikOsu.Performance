using NUnit.Framework;
using RealistikOsu.Performance.Data;

namespace RealistikOsu.Performance.Tests.Data
{
    [TestFixture]
    public class PpLimitTest
    {
        private readonly PpLimit limit = new PpLimit { Pp = 1000, FlashlightPp = 700 };

        [Test]
        public void TestAPlayAtTheCapIsNotOverIt()
        {
            Assert.That(limit.IsExceededBy(1000, false), Is.False);
            Assert.That(limit.IsExceededBy(1000.01, false), Is.True);
        }

        [Test]
        public void TestFlashlightHasItsOwnCap()
        {
            Assert.That(limit.IsExceededBy(800, false), Is.False);
            Assert.That(limit.IsExceededBy(800, true), Is.True);
        }

        [TestCase(0, 0, 0)]
        [TestCase(3, 0, 3)]
        [TestCase(0, 1, 4)]
        [TestCase(2, 2, 10)]
        public void TestTheWhitelistBitFollowsTheVariant(int ruleset, int variant, int bit)
        {
            Assert.That(PpLimit.WhitelistBit(ruleset, variant), Is.EqualTo(bit));
        }
    }
}
