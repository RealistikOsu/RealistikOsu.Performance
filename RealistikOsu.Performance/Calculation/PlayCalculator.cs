using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Scoring;
using RealistikOsu.Performance.Beatmaps;

namespace RealistikOsu.Performance.Calculation
{
    public sealed class PlayCalculator
    {
        private readonly BeatmapFiles beatmaps;
        private readonly DifficultyCache difficulty;

        public PlayCalculator(BeatmapFiles beatmaps, DifficultyCache difficulty)
        {
            this.beatmaps = beatmaps;
            this.difficulty = difficulty;
        }

        public async Task<PlayResult> CalculateAsync(PlayInput input)
        {
            var ruleset = Rulesets.ById(input.RulesetId);
            var file = await beatmaps.GetAsync(input.BeatmapId).ConfigureAwait(false);

            DifficultyAttributes attributes = input.PassedObjects is { } passed
                ? await Task.Run(() => difficultyAtObject(ruleset.CreateDifficultyCalculator(file.Working), input.Mods, passed)).ConfigureAwait(false)
                : await difficulty.GetAsync(file, ruleset, input.Mods).ConfigureAwait(false);

            var playable = file.Working.GetPlayableBeatmap(ruleset.RulesetInfo, input.Mods);
            var processor = ruleset.CreateScoreProcessor();
            processor.ApplyBeatmap(playable);

            var maximum = processor.MaximumStatistics;
            var statistics = input.Statistics != null
                ? HitStatistics.Complete(input.Statistics, maximum)
                : HitStatistics.FromAccuracy(input.RulesetId, maximum, input.Accuracy, input.Misses, input.PassedObjects);

            var score = new ScoreInfo(file.Working.BeatmapInfo, ruleset.RulesetInfo)
            {
                Mods = input.Mods,
                MaxCombo = input.MaxCombo,
                Statistics = statistics,
                MaximumStatistics = maximum,
                Passed = true,
            };

            score.Accuracy = StandardisedScoreMigrationTools.ComputeAccuracy(score, processor);

            var performance = ruleset.CreatePerformanceCalculator()?.Calculate(score, attributes);

            if (performance == null)
                return PlayResult.ZERO;

            return new PlayResult(
                finite(attributes.StarRating),
                finite(performance.Total),
                approachRate(input.RulesetId, playable.Difficulty, input.Mods),
                overallDifficulty(input.RulesetId, playable.Difficulty, input.Mods),
                attributes.MaxCombo);
        }

        private static DifficultyAttributes difficultyAtObject(DifficultyCalculator calculator, osu.Game.Rulesets.Mods.Mod[] mods, int passedObjects)
        {
            var timed = calculator.CalculateTimed(mods);

            if (timed.Count == 0)
                return calculator.Calculate(mods);

            return timed[Math.Clamp(passedObjects, 1, timed.Count) - 1].Attributes;
        }

        private static double approachRate(int rulesetId, IBeatmapDifficultyInfo difficulty, Mod[] mods)
        {
            if (rulesetId is not (0 or 2))
                return 0;

            double rate = osu.Game.Utils.ModUtils.CalculateRateWithMods(mods);
            double ar = withMods(difficulty, mods).ApproachRate;

            double preempt = IBeatmapDifficultyInfo.DifficultyRange(ar, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN) / rate;
            return IBeatmapDifficultyInfo.InverseDifficultyRange(preempt, OsuHitObject.PREEMPT_MAX, OsuHitObject.PREEMPT_MID, OsuHitObject.PREEMPT_MIN);
        }

        private static double overallDifficulty(int rulesetId, IBeatmapDifficultyInfo difficulty, Mod[] mods)
        {
            if (rulesetId != 0)
                return 0;

            double rate = osu.Game.Utils.ModUtils.CalculateRateWithMods(mods);

            var hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(withMods(difficulty, mods).OverallDifficulty);

            return (79.5 - hitWindows.WindowFor(HitResult.Great) / rate) / 6;
        }

        private static BeatmapDifficulty withMods(IBeatmapDifficultyInfo difficulty, Mod[] mods)
        {
            var adjusted = new BeatmapDifficulty(difficulty);

            foreach (var mod in mods.OfType<IApplicableToDifficulty>())
                mod.ApplyToDifficulty(adjusted);

            return adjusted;
        }

        private static double finite(double value) => double.IsFinite(value) ? value : 0;
    }
}
