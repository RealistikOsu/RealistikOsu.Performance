using System;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;

namespace RealistikOsu.Performance
{
    public static class Rulesets
    {
        private static readonly Ruleset[] all =
        {
            new OsuRuleset(),
            new TaikoRuleset(),
            new CatchRuleset(),
            new ManiaRuleset(),
        };

        public static Ruleset ById(int id) => id >= 0 && id < all.Length ? all[id] : throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown ruleset.");
    }
}
