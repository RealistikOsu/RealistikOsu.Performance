using System;

namespace RealistikOsu.Performance.Data
{
    /// <summary>
    /// The three stable leaderboards are told apart by a number: 0 vanilla, 1 relax and 2 autopilot.
    /// Each has its own scores table, stats table and Redis leaderboard.
    /// </summary>
    public static class Variants
    {
        public const int RELAX_BIT = 1 << 7;
        public const int AUTOPILOT_BIT = 1 << 13;

        public const string LAZER_SCORES_TABLE = "lazer_scores";

        public static int OfMods(int mods) => (mods & RELAX_BIT) > 0 ? 1 : (mods & AUTOPILOT_BIT) > 0 ? 2 : 0;

        public static string ScoresTable(int rx) => rx switch
        {
            0 => "scores",
            1 => "scores_relax",
            2 => "scores_ap",
            _ => throw new ArgumentOutOfRangeException(nameof(rx), rx, null),
        };

        /// <summary>
        /// The only way a table name from outside, like a queue item, gets into SQL.
        /// </summary>
        public static int RxOfScoresTable(string table) => table switch
        {
            "scores" => 0,
            "scores_relax" => 1,
            "scores_ap" => 2,
            _ => throw new ArgumentException($"Unknown scores table '{table}'.", nameof(table)),
        };

        public static string StatsTable(int rx) => rx switch
        {
            0 => "users_stats",
            1 => "rx_stats",
            2 => "ap_stats",
            _ => throw new ArgumentOutOfRangeException(nameof(rx), rx, null),
        };

        public static string LeaderboardKey(int rx) => rx switch
        {
            0 => "leaderboard",
            1 => "leaderboard_relax",
            2 => "leaderboard_ap",
            _ => throw new ArgumentOutOfRangeException(nameof(rx), rx, null),
        };

        public static string StatsPrefix(int mode) => mode switch
        {
            0 => "std",
            1 => "taiko",
            2 => "ctb",
            3 => "mania",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

        /// <summary>
        /// The variants that have a board in the mode, matching the stable boards that exist for it.
        /// </summary>
        public static int[] LazerVariants(int mode) => mode switch
        {
            0 => new[] { 0, 1, 2 },
            1 or 2 => new[] { 0, 1 },
            _ => new[] { 0 },
        };

        public static (string StatsTable, string LeaderboardKey) LazerNames(int variant) => variant switch
        {
            0 => ("lazer_stats", "ripple:leaderboard_lazer"),
            1 => ("lazer_rx_stats", "ripple:leaderboard_lazer_relax"),
            2 => ("lazer_ap_stats", "ripple:leaderboard_lazer_ap"),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
        };
    }
}
