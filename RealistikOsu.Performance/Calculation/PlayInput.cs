using System.Collections.Generic;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace RealistikOsu.Performance.Calculation
{
    public sealed class PlayInput
    {
        public required int BeatmapId { get; init; }

        public required int RulesetId { get; init; }

        public required Mod[] Mods { get; init; }

        public required int MaxCombo { get; init; }

        /// <summary>
        /// The hit counts of the play when they're known. A stable score stores only its judgements, which is fine.
        /// </summary>
        public Dictionary<HitResult, int>? Statistics { get; init; }

        /// <summary>
        /// In percent. Used with <see cref="Misses"/> to work out the hit counts when <see cref="Statistics"/> is missing.
        /// </summary>
        public double Accuracy { get; init; }

        public int Misses { get; init; }

        public int? PassedObjects { get; init; }
    }

    public sealed record PlayResult(double Stars, double Pp, double ApproachRate, double OverallDifficulty, int MaxCombo)
    {
        public static readonly PlayResult ZERO = new PlayResult(0, 0, 0, 0, 0);
    }
}
