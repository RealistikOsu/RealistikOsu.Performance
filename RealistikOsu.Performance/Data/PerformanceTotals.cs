using System;
using System.Collections.Generic;

namespace RealistikOsu.Performance.Data
{
    public static class PerformanceTotals
    {
        /// <summary>
        /// A stable player's pp: their best plays weighted by 0.95 per place, plus a bonus for how many ranked plays they have.
        /// </summary>
        /// <param name="pp">The top plays, best first.</param>
        /// <param name="scoreCount">All the player's best ranked plays, not only the ones given.</param>
        public static int Weighted(IReadOnlyList<double> pp, int scoreCount)
        {
            float total = 0;

            for (int i = 0; i < pp.Count; i++)
                total += (float)pp[i] * MathF.Pow(0.95f, i);

            total += 416.6667f * (1 - MathF.Pow(0.995f, scoreCount));

            return (int)MathF.Round(total, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// A lazer player's pp and accuracy over their best 100 plays of the variant.
        /// </summary>
        /// <param name="plays">Every best play of the variant, best first, with accuracy as a fraction.</param>
        public static (int Pp, float Accuracy) Lazer(IReadOnlyList<(double Pp, double Accuracy)> plays)
        {
            if (plays.Count == 0)
                return (0, 0);

            float weightedPp = 0;
            float weightedAccuracy = 0;

            int top = Math.Min(plays.Count, 100);

            for (int i = 0; i < top; i++)
            {
                float weight = MathF.Pow(0.95f, i);
                weightedPp += (float)plays[i].Pp * weight;
                weightedAccuracy += (float)plays[i].Accuracy * 100 * weight;
            }

            float bonus = 416.6667f * (1 - MathF.Pow(0.995f, plays.Count));
            float accuracy = weightedAccuracy / (20 * (1 - MathF.Pow(0.95f, top)));

            return ((int)MathF.Round(weightedPp + bonus, MidpointRounding.AwayFromZero), accuracy);
        }
    }
}
