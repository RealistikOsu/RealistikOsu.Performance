using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace RealistikOsu.Performance.Data
{
    public static class StableStatistics
    {
        /// <summary>
        /// Stable stores a play as five counters whose meaning depends on the mode.
        /// In osu! and taiko they are the 300s, 100s and 50s. In mania the geki is the 320 and the katu the 200.
        /// In catch the 300s are fruits, the 100s droplets, the 50s tiny droplets, the katus missed tiny droplets,
        /// and the misses count missed fruits and droplets together.
        /// </summary>
        public static Dictionary<HitResult, int> Of(StableScore score) => score.PlayMode switch
        {
            0 => new Dictionary<HitResult, int>
            {
                [HitResult.Great] = score.Count300,
                [HitResult.Ok] = score.Count100,
                [HitResult.Meh] = score.Count50,
                [HitResult.Miss] = score.CountMiss,
            },
            1 => new Dictionary<HitResult, int>
            {
                [HitResult.Great] = score.Count300,
                [HitResult.Ok] = score.Count100,
                [HitResult.Miss] = score.CountMiss,
            },
            2 => new Dictionary<HitResult, int>
            {
                [HitResult.Great] = score.Count300,
                [HitResult.LargeTickHit] = score.Count100,
                [HitResult.SmallTickHit] = score.Count50,
                [HitResult.SmallTickMiss] = score.CountKatu,
                [HitResult.Miss] = score.CountMiss,
            },
            3 => new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = score.CountGeki,
                [HitResult.Great] = score.Count300,
                [HitResult.Good] = score.CountKatu,
                [HitResult.Ok] = score.Count100,
                [HitResult.Meh] = score.Count50,
                [HitResult.Miss] = score.CountMiss,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(score), score.PlayMode, "Unknown mode."),
        };
    }
}
