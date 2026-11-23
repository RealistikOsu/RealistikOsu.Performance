using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace RealistikOsu.Performance.Calculation
{
    public static class ModFactory
    {
        /// <summary>
        /// Builds the mods of a stable-style request. A lazer mod list wins over the legacy bits when it parses,
        /// because only it carries mod settings.
        /// </summary>
        /// <param name="lazerModsJson">A lazer mod list like <c>[{"acronym":"DT","settings":{"speed_change":1.2}}]</c>.</param>
        /// <param name="playbackRate">The custom rate stable scores keep outside their mods, applied when the mods have a rate change.</param>
        /// <param name="classic">Whether the play is scored the stable way, which is what the Classic mod stands for.</param>
        public static Mod[] Create(Ruleset ruleset, int legacyBits, string? lazerModsJson, double? playbackRate, bool classic)
        {
            Mod[]? mods = fromLazerJson(ruleset, lazerModsJson);
            bool fromBits = mods == null;

            mods ??= ruleset.ConvertFromLegacyMods((LegacyMods)legacyBits).ToArray();

            if (fromBits && playbackRate is > 0)
            {
                foreach (var rate in mods.OfType<ModRateAdjust>())
                    rate.SpeedChange.Value = playbackRate.Value;
            }

            return classic ? withClassic(ruleset, mods) : mods;
        }

        public static Mod[] FromApiMods(Ruleset ruleset, IEnumerable<APIMod> apiMods) => apiMods.Select(m => m.ToMod(ruleset)).ToArray();

        private static Mod[] withClassic(Ruleset ruleset, Mod[] mods)
        {
            if (mods.Any(m => m is ModClassic))
                return mods;

            var classic = ruleset.CreateAllMods().OfType<ModClassic>().FirstOrDefault();
            return classic == null ? mods : mods.Append(classic).ToArray();
        }

        private static Mod[]? fromLazerJson(Ruleset ruleset, string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var apiMods = JsonConvert.DeserializeObject<APIMod[]>(json);
                return apiMods == null ? null : FromApiMods(ruleset, apiMods);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
