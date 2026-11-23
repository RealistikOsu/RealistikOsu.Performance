using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace RealistikOsu.Performance.Data
{
    public static class LazerMods
    {
        private static readonly string[] any_settings =
        {
            "AC", "AL", "BL", "CO", "MU", "NS", "PF", "SD", "SG", "SW", "TC", "DT", "HT", "NC", "DC", "RX", "AP",
        };

        private static readonly string[] default_settings =
        {
            "EZ", "HD", "HR", "FL", "NF", "SO", "TD", "4K", "5K", "6K", "7K", "8K", "9K",
        };

        /// <summary>
        /// Mirrors the lazer server: a play counts only when every mod is one lazer ranks, kept with default
        /// settings unless it's a rate change, or is RX or AP.
        /// </summary>
        public static bool IsRanked(int mode, JToken mods)
        {
            if (mods is not JArray array)
                return true;

            return array.All(m => isRanked(mode, m));
        }

        private static bool isRanked(int mode, JToken mod)
        {
            string acronym = mod["acronym"]?.Value<string>() ?? string.Empty;
            bool hasSettings = mod["settings"] is JObject { HasValues: true };

            if (any_settings.Contains(acronym))
                return true;

            bool ranked = acronym switch
            {
                "MR" => mode == 3,
                "HR" => mode != 3,
                _ => default_settings.Contains(acronym),
            };

            return ranked && !hasSettings;
        }

        public static bool IsRanked(int mode, string modsJson) => IsRanked(mode, JToken.Parse(modsJson));
    }
}
