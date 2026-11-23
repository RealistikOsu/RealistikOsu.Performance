using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace RealistikOsu.Performance.Calculation
{
    /// <summary>
    /// Works out the hit counts of a play from what a request carries, which is only its accuracy and miss count.
    /// </summary>
    public static class HitStatistics
    {
        /// <param name="maximum">The hit counts of a perfect play of the map.</param>
        /// <param name="accuracy">In percent.</param>
        /// <param name="passedObjects">How many objects the play got through, when it failed.</param>
        public static Dictionary<HitResult, int> FromAccuracy(int rulesetId, IReadOnlyDictionary<HitResult, int> maximum, double accuracy, int misses, int? passedObjects)
        {
            double fraction = Math.Clamp(accuracy / 100, 0, 1);
            var basic = rulesetId switch
            {
                0 => osu(maximum, fraction, misses, passedObjects),
                1 => taiko(maximum, fraction, misses, passedObjects),
                2 => fruits(maximum, fraction, misses),
                3 => mania(maximum, fraction, misses, passedObjects),
                _ => throw new ArgumentOutOfRangeException(nameof(rulesetId), rulesetId, null),
            };

            return Complete(basic, maximum);
        }

        /// <summary>
        /// Adds what a stable score doesn't store, like slider ends and ticks, as all hit.
        /// </summary>
        public static Dictionary<HitResult, int> Complete(Dictionary<HitResult, int> given, IReadOnlyDictionary<HitResult, int> maximum)
        {
            var result = new Dictionary<HitResult, int>(given);

            foreach ((HitResult hit, int count) in maximum)
            {
                if (!hit.IsBasic() && !result.ContainsKey(hit))
                    result[hit] = count;
            }

            return result;
        }

        private static Dictionary<HitResult, int> osu(IReadOnlyDictionary<HitResult, int> maximum, double accuracy, int misses, int? passedObjects)
        {
            int total = passedObjects is { } passed ? Math.Min(passed, maximum[HitResult.Great]) : maximum[HitResult.Great];
            misses = Math.Min(misses, total);

            int remaining = total - misses;

            // A 300 is worth 6, a 100 is 2 and a 50 is 1, so starting from all 50s each upgrade adds 5 or 1.
            int delta = Math.Max((int)Math.Round(accuracy * total * 6) - remaining, 0);
            int great = Math.Min(delta / 5, remaining);
            int ok = Math.Min(delta % 5, remaining - great);

            return new Dictionary<HitResult, int>
            {
                [HitResult.Great] = great,
                [HitResult.Ok] = ok,
                [HitResult.Meh] = remaining - great - ok,
                [HitResult.Miss] = misses,
            };
        }

        private static Dictionary<HitResult, int> taiko(IReadOnlyDictionary<HitResult, int> maximum, double accuracy, int misses, int? passedObjects)
        {
            int total = passedObjects is { } passed ? Math.Min(passed, maximum[HitResult.Great]) : maximum[HitResult.Great];
            misses = Math.Min(misses, total);

            int remaining = total - misses;
            int great = Math.Clamp((int)Math.Round(accuracy * total * 2) - remaining, 0, remaining);

            return new Dictionary<HitResult, int>
            {
                [HitResult.Great] = great,
                [HitResult.Ok] = remaining - great,
                [HitResult.Miss] = misses,
            };
        }

        private static Dictionary<HitResult, int> fruits(IReadOnlyDictionary<HitResult, int> maximum, double accuracy, int misses)
        {
            int fruits = maximum.GetValueOrDefault(HitResult.Great);
            int droplets = maximum.GetValueOrDefault(HitResult.LargeTickHit);
            int tiny = maximum.GetValueOrDefault(HitResult.SmallTickHit);

            // Droplets are what gets missed first, then fruits.
            int missedDroplets = Math.Min(misses, droplets);
            int missedFruits = Math.Min(misses - missedDroplets, fruits);

            int fruitHits = fruits - missedFruits;
            int dropletHits = droplets - missedDroplets;

            int allObjects = fruits + droplets + tiny;
            int tinyHits = Math.Clamp((int)Math.Round(accuracy * allObjects) - fruitHits - dropletHits, 0, tiny);

            return new Dictionary<HitResult, int>
            {
                [HitResult.Great] = fruitHits,
                [HitResult.Miss] = missedFruits,
                [HitResult.LargeTickHit] = dropletHits,
                [HitResult.LargeTickMiss] = missedDroplets,
                [HitResult.SmallTickHit] = tinyHits,
                [HitResult.SmallTickMiss] = tiny - tinyHits,
            };
        }

        private static Dictionary<HitResult, int> mania(IReadOnlyDictionary<HitResult, int> maximum, double accuracy, int misses, int? passedObjects)
        {
            int total = passedObjects is { } passed ? Math.Min(passed, maximum[HitResult.Perfect]) : maximum[HitResult.Perfect];
            misses = Math.Min(misses, total);

            int remaining = total - misses;

            // Start from all perfects (6 each) and take points off with 100s (4 each) and 200s (2 each).
            int missing = Math.Max(remaining * 6 - (int)Math.Round(accuracy * total * 6), 0);
            int ok = Math.Min(missing / 4, remaining);
            int good = Math.Min(missing % 4 / 2, remaining - ok);

            return new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = remaining - ok - good,
                [HitResult.Good] = good,
                [HitResult.Ok] = ok,
                [HitResult.Miss] = misses,
            };
        }
    }
}
