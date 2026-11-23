using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Relax;
using RealistikOsu.Performance.Beatmaps;
using RealistikOsu.Performance.Calculation;

namespace RealistikOsu.Performance.Tests
{
    [TestFixture]
    public class PlayCalculatorTest
    {
        private string folder = null!;
        private BeatmapFiles beatmaps = null!;
        private DifficultyCache cache = null!;
        private PlayCalculator calculator = null!;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            TestMap.Write(folder, 1);

            beatmaps = new BeatmapFiles(folder, "http://localhost:1", 1 << 20);
            cache = new DifficultyCache(null, 100, TimeSpan.FromMinutes(1));
            calculator = new PlayCalculator(beatmaps, cache);
        }

        [TearDown]
        public void TearDown()
        {
            beatmaps.Dispose();
            cache.Dispose();
            Directory.Delete(folder, true);
        }

        private Task<PlayResult> calculate(int ruleset, int bits, double accuracy = 97, int misses = 0, bool classic = true, double? rate = null, string? lazerMods = null)
        {
            var mods = ModFactory.Create(Rulesets.ById(ruleset), bits, lazerMods, rate, classic);

            return calculator.CalculateAsync(new PlayInput
            {
                BeatmapId = 1,
                RulesetId = ruleset,
                Mods = mods,
                MaxCombo = 1000,
                Accuracy = accuracy,
                Misses = misses,
            });
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public async Task TestEveryRulesetGivesPp(int ruleset)
        {
            var result = await calculate(ruleset, 0);

            Assert.That(result.Pp, Is.GreaterThan(0));
            Assert.That(result.Stars, Is.GreaterThan(0));
        }

        [Test]
        public async Task TestHigherAccuracyIsWorthMore()
        {
            var low = await calculate(0, 0, 90);
            var high = await calculate(0, 0, 99);

            Assert.That(high.Pp, Is.GreaterThan(low.Pp));
        }

        [Test]
        public async Task TestMissesCostPp()
        {
            var clean = await calculate(0, 0, 98);
            var missed = await calculate(0, 0, 98, 5);

            Assert.That(missed.Pp, Is.LessThan(clean.Pp));
        }

        [Test]
        public async Task TestRelaxUsesRelaxDifficulty()
        {
            const int relax = 1 << 7;

            var file = await beatmaps.GetAsync(1);
            var mods = ModFactory.Create(Rulesets.ById(0), relax, null, null, true);
            var attributes = await cache.GetAsync(file, Rulesets.ById(0), mods);

            Assert.That(attributes, Is.TypeOf<OsuRelaxDifficultyAttributes>());

            var vanilla = await calculate(0, 0);
            var relaxed = await calculate(0, relax);

            Assert.That(relaxed.Pp, Is.GreaterThan(0));
            Assert.That(relaxed.Pp, Is.Not.EqualTo(vanilla.Pp));
        }

        [Test]
        public async Task TestCustomRateChangesDifficulty()
        {
            const int doubleTime = 1 << 6;

            var normal = await calculate(0, doubleTime);
            var slow = await calculate(0, doubleTime, rate: 1.03);

            Assert.That(slow.Stars, Is.LessThan(normal.Stars));
        }

        [Test]
        public async Task TestLazerModsOverrideTheBits()
        {
            var result = await calculate(0, 0, lazerMods: "[{\"acronym\":\"DT\",\"settings\":{\"speed_change\":1.3}}]");
            var plain = await calculate(0, 0);

            Assert.That(result.Stars, Is.GreaterThan(plain.Stars));
        }

        [Test]
        public async Task TestBrokenLazerModsFallBackToTheBits()
        {
            var result = await calculate(0, 1 << 6, lazerMods: "not json");
            var doubleTime = await calculate(0, 1 << 6);

            Assert.That(result.Pp, Is.EqualTo(doubleTime.Pp));
        }

        [Test]
        public async Task TestDifficultyIsCalculatedOncePerMods()
        {
            await calculate(0, 0, 90);
            await calculate(0, 0, 95);
            await calculate(0, 1);

            Assert.That(cache.Misses, Is.EqualTo(1));
            Assert.That(cache.Hits, Is.EqualTo(2));
        }

        [TestCase(0, 0)]
        [TestCase(0, 1 << 7)]
        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 0)]
        public async Task TestCachedAttributesGiveTheSamePp(int ruleset, int bits)
        {
            var file = await beatmaps.GetAsync(1);
            var mods = ModFactory.Create(Rulesets.ById(ruleset), bits, null, null, true);
            var fresh = await cache.GetAsync(file, Rulesets.ById(ruleset), mods);
            var restored = DifficultyCache.Deserialise(DifficultyCache.Serialise(fresh), file);

            Assert.That(restored.GetType(), Is.EqualTo(fresh.GetType()));
            Assert.That(restored.StarRating, Is.EqualTo(fresh.StarRating));
            Assert.That(restored.MaxCombo, Is.EqualTo(fresh.MaxCombo));

            var performance = Rulesets.ById(ruleset).CreatePerformanceCalculator()!;
            var processor = Rulesets.ById(ruleset).CreateScoreProcessor();
            processor.ApplyBeatmap(file.Working.GetPlayableBeatmap(Rulesets.ById(ruleset).RulesetInfo, mods));

            var score = new osu.Game.Scoring.ScoreInfo(file.Working.BeatmapInfo, Rulesets.ById(ruleset).RulesetInfo)
            {
                Mods = mods,
                MaxCombo = fresh.MaxCombo,
                Statistics = HitStatistics.FromAccuracy(ruleset, processor.MaximumStatistics, 96, 2, null),
                MaximumStatistics = processor.MaximumStatistics,
            };
            score.Accuracy = osu.Game.Database.StandardisedScoreMigrationTools.ComputeAccuracy(score, processor);

            Assert.That(performance.Calculate(score, restored)!.Total, Is.EqualTo(performance.Calculate(score, fresh)!.Total).Within(1e-9));
        }
    }
}
