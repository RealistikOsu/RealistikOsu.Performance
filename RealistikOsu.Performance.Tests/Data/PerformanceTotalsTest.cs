using NUnit.Framework;
using RealistikOsu.Performance.Data;

namespace RealistikOsu.Performance.Tests.Data
{
    [TestFixture]
    public class PerformanceTotalsTest
    {
        [Test]
        public void TestNoPlaysIsNothing()
        {
            Assert.That(PerformanceTotals.Weighted(new double[0], 0), Is.EqualTo(0));
        }

        [Test]
        public void TestPlaysAreWeightedAndGetTheBonus()
        {
            // 100 + 50 * 0.95 = 147.5, plus 416.6667 * (1 - 0.995^2) = 4.156
            Assert.That(PerformanceTotals.Weighted(new double[] { 100, 50 }, 2), Is.EqualTo(152));
        }

        [Test]
        public void TestBonusCountsEveryPlayNotOnlyTheTopOnes()
        {
            Assert.That(PerformanceTotals.Weighted(new double[] { 100 }, 1000), Is.EqualTo(100 + (int)System.Math.Round(416.6667 * (1 - System.Math.Pow(0.995, 1000)))));
        }

        [Test]
        public void TestLazerWithoutPlaysIsNothing()
        {
            Assert.That(PerformanceTotals.Lazer(new (double, double)[0]), Is.EqualTo((0, 0f)));
        }

        [Test]
        public void TestLazerPpAndAccuracy()
        {
            var (pp, accuracy) = PerformanceTotals.Lazer(new[] { (200.0, 1.0), (100.0, 0.9) });

            // 200 + 100 * 0.95 = 295, plus 416.6667 * (1 - 0.995^2) = 4.156
            Assert.That(pp, Is.EqualTo(299));

            // (100 + 90 * 0.95) / (20 * (1 - 0.95^2))
            Assert.That(accuracy, Is.EqualTo(95.128).Within(0.001));
        }
    }
}
