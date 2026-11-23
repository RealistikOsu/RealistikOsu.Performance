using NUnit.Framework;
using osu.Game.Rulesets.Scoring;
using RealistikOsu.Performance.Data;

namespace RealistikOsu.Performance.Tests.Data
{
    [TestFixture]
    public class StableStatisticsTest
    {
        private static StableScore score(int mode) => new StableScore
        {
            PlayMode = mode,
            Count300 = 300,
            Count100 = 100,
            Count50 = 50,
            CountKatu = 20,
            CountGeki = 200,
            CountMiss = 3,
        };

        [Test]
        public void TestOsu()
        {
            var statistics = StableStatistics.Of(score(0));

            Assert.That(statistics[HitResult.Great], Is.EqualTo(300));
            Assert.That(statistics[HitResult.Ok], Is.EqualTo(100));
            Assert.That(statistics[HitResult.Meh], Is.EqualTo(50));
            Assert.That(statistics[HitResult.Miss], Is.EqualTo(3));
            Assert.That(statistics, Has.Count.EqualTo(4));
        }

        [Test]
        public void TestTaiko()
        {
            var statistics = StableStatistics.Of(score(1));

            Assert.That(statistics[HitResult.Great], Is.EqualTo(300));
            Assert.That(statistics[HitResult.Ok], Is.EqualTo(100));
            Assert.That(statistics[HitResult.Miss], Is.EqualTo(3));
            Assert.That(statistics, Has.Count.EqualTo(3));
        }

        [Test]
        public void TestCatch()
        {
            var statistics = StableStatistics.Of(score(2));

            Assert.That(statistics[HitResult.Great], Is.EqualTo(300));
            Assert.That(statistics[HitResult.LargeTickHit], Is.EqualTo(100));
            Assert.That(statistics[HitResult.SmallTickHit], Is.EqualTo(50));
            Assert.That(statistics[HitResult.SmallTickMiss], Is.EqualTo(20));
            Assert.That(statistics[HitResult.Miss], Is.EqualTo(3));
        }

        [Test]
        public void TestMania()
        {
            var statistics = StableStatistics.Of(score(3));

            Assert.That(statistics[HitResult.Perfect], Is.EqualTo(200));
            Assert.That(statistics[HitResult.Great], Is.EqualTo(300));
            Assert.That(statistics[HitResult.Good], Is.EqualTo(20));
            Assert.That(statistics[HitResult.Ok], Is.EqualTo(100));
            Assert.That(statistics[HitResult.Meh], Is.EqualTo(50));
            Assert.That(statistics[HitResult.Miss], Is.EqualTo(3));
        }
    }
}
