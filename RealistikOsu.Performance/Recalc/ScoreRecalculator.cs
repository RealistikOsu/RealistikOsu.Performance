using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Game.Online.API;
using osu.Game.Rulesets.Scoring;
using osu.Server.QueueProcessor;
using RealistikOsu.Performance.Beatmaps;
using RealistikOsu.Performance.Calculation;
using RealistikOsu.Performance.Data;
using StackExchange.Redis;

namespace RealistikOsu.Performance.Recalc
{
    /// <summary>
    /// Recalculates the pp of stored scores and the totals and leaderboards of the players they belong to.
    /// </summary>
    public sealed class ScoreRecalculator
    {
        private const int status_batch_size = 1000;

        private readonly PlayCalculator calculator;
        private readonly IConnectionMultiplexer redis;

        public BeatmapFiles Beatmaps { get; }

        public ScoreRecalculator(PlayCalculator calculator, BeatmapFiles beatmaps, IConnectionMultiplexer redis)
        {
            this.calculator = calculator;
            this.redis = redis;

            Beatmaps = beatmaps;
        }

        /// <summary>
        /// Recalculates a stable score's pp from the counts it stores.
        /// </summary>
        /// <returns>The score's owner, or <c>null</c> when the score is missing or isn't one of its player's kept plays.</returns>
        public async Task<ScoreOwner?> RecalculateScoreAsync(string table, long scoreId)
        {
            int rx = Variants.RxOfScoresTable(table);

            using var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false);

            var score = await connection.QuerySingleOrDefaultAsync<StableScore>(
                $"{StableScore.SELECT} FROM {table} s INNER JOIN beatmaps b USING(beatmap_md5) WHERE s.id = @scoreId",
                new { scoreId }).ConfigureAwait(false);

            if (score == null || score.Completed is not (2 or 3))
                return null;

            await RecalculateScoreAsync(table, score).ConfigureAwait(false);
            return new ScoreOwner(score.UserId, score.PlayMode, rx);
        }

        public async Task RecalculateScoreAsync(string table, StableScore score)
        {
            int rx = Variants.RxOfScoresTable(table);
            double pp = 0;

            if (!score.DodgeTheBeat)
            {
                var ruleset = Rulesets.ById(score.PlayMode);

                // Stable scores are scored the classic way, which the Classic mod stands for.
                var result = await calculator.CalculateAsync(new PlayInput
                {
                    BeatmapId = score.BeatmapId,
                    RulesetId = score.PlayMode,
                    Mods = ModFactory.Create(ruleset, score.Mods, score.ModsJson, score.PlaybackRate, true),
                    MaxCombo = score.MaxCombo,
                    Statistics = StableStatistics.Of(score),
                }).ConfigureAwait(false);

                pp = Math.Round(result.Pp, 2);
            }

            if (Math.Abs(pp - score.Pp) < 0.005)
                return;

            using (var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false))
            {
                await connection.ExecuteAsync($"UPDATE {table} SET pp = @pp WHERE id = @id", new { pp, id = score.Id }).ConfigureAwait(false);
            }

            // Only a player's best play is cached.
            if (score.Completed == 3)
            {
                await publishAsync("cache:update_score_pp", new JObject
                {
                    ["beatmap_id"] = score.BeatmapId,
                    ["user_id"] = score.UserId,
                    ["score_id"] = score.Id,
                    ["new_pp"] = pp,
                    ["mode_vn"] = score.PlayMode,
                    ["relax"] = rx,
                }.ToString(Formatting.None)).ConfigureAwait(false);
            }

            Console.WriteLine($"Recalculated score ID {score.Id} (mode: {score.PlayMode}) | {score.Pp} -> {pp}");
        }

        /// <returns>The score's owner, or <c>null</c> when the score is missing, failed or isn't on a ranked or approved map.</returns>
        public async Task<ScoreOwner?> RecalculateLazerScoreAsync(long scoreId)
        {
            using var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false);

            var score = await connection.QuerySingleOrDefaultAsync<LazerScore>(
                $"{LazerScore.SELECT} WHERE l.id = @scoreId AND l.passed = 1 AND b.ranked IN (2, 3)",
                new { scoreId }).ConfigureAwait(false);

            if (score == null)
                return null;

            await RecalculateLazerScoreAsync(score).ConfigureAwait(false);
            return new ScoreOwner(score.UserId, score.RulesetId, score.Variant);
        }

        public async Task RecalculateLazerScoreAsync(LazerScore score)
        {
            var ruleset = Rulesets.ById(score.RulesetId);

            var mods = ModFactory.FromApiMods(ruleset, JsonConvert.DeserializeObject<APIMod[]>(score.Mods) ?? Array.Empty<APIMod>());
            var statistics = JsonConvert.DeserializeObject<Dictionary<HitResult, int>>(score.Statistics) ?? new Dictionary<HitResult, int>();

            // A result the score doesn't list didn't happen, which the calculator must not read as all hit.
            foreach (var result in Enum.GetValues<HitResult>())
                statistics.TryAdd(result, 0);

            var calculation = await calculator.CalculateAsync(new PlayInput
            {
                BeatmapId = score.BeatmapId,
                RulesetId = score.RulesetId,
                Mods = mods,
                MaxCombo = score.MaxCombo,
                Statistics = statistics,
            }).ConfigureAwait(false);

            double pp = Math.Round(calculation.Pp, 2);
            bool rankedMods = LazerMods.IsRanked(score.RulesetId, score.Mods);

            // Over the cap the score keeps no pp, rather than the play restricting its player.
            if (pp > 0 && await exceedsPpCapAsync(score, pp).ConfigureAwait(false))
            {
                Console.WriteLine($"Lazer score ID {score.Id} from user {score.UserId} is {pp}pp, over the cap; leaving it without pp");
                pp = 0;
            }

            if (Math.Abs(pp - score.Pp) < 0.005 && rankedMods == score.RankedMods)
                return;

            using var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false);

            await connection.ExecuteAsync("UPDATE lazer_scores SET pp = @pp, ranked_mods = @rankedMods WHERE id = @id",
                new { pp, rankedMods, id = score.Id }).ConfigureAwait(false);

            Console.WriteLine($"Recalculated lazer score ID {score.Id} (mode: {score.RulesetId}) | {score.Pp} -> {pp}");
        }

        /// <summary>
        /// Settles which of the player's scores on each map is their best, then rebuilds their pp and leaderboard places.
        /// </summary>
        public async Task RecalculateUserAsync(int userId, int mode, int rx)
        {
            string table = Variants.ScoresTable(rx);
            string prefix = Variants.StatsPrefix(mode);

            using var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false);

            await recalculateStatusesAsync(connection, table, userId, mode).ConfigureAwait(false);

            var topPp = (await connection.QueryAsync<double>(
                $@"SELECT COALESCE(s.pp, 0) FROM {table} s INNER JOIN beatmaps b USING(beatmap_md5)
                WHERE s.userid = @userId AND s.completed = 3 AND s.play_mode = @mode AND b.ranked IN (3, 2)
                AND b.dodge_the_beat = 0
                ORDER BY s.pp DESC LIMIT 100", new { userId, mode }).ConfigureAwait(false)).ToList();

            int scoreCount = await connection.ExecuteScalarAsync<int>(
                $@"SELECT COUNT(s.id) FROM {table} s INNER JOIN beatmaps b USING(beatmap_md5)
                WHERE s.userid = @userId AND s.completed = 3 AND s.play_mode = @mode AND b.ranked IN (3, 2)
                AND b.dodge_the_beat = 0",
                new { userId, mode }).ConfigureAwait(false);

            int newPp = PerformanceTotals.Weighted(topPp, scoreCount);

            await connection.ExecuteAsync($"UPDATE {Variants.StatsTable(rx)} SET pp_{prefix} = @newPp WHERE id = @userId", new { newPp, userId }).ConfigureAwait(false);

            var user = await connection.QuerySingleAsync<UserInfo>(
                "SELECT users.country AS Country, users.privileges AS Privileges FROM users INNER JOIN users_stats USING(id) WHERE id = @userId",
                new { userId }).ConfigureAwait(false);

            var db = redis.GetDatabase();

            // Only unrestricted players are on the boards.
            if ((user.Privileges & 1) > 0)
            {
                string board = $"ripple:{Variants.LeaderboardKey(rx)}:{prefix}";

                await db.SortedSetAddAsync(board, userId.ToString(), newPp).ConfigureAwait(false);
                await db.SortedSetAddAsync($"{board}:{user.Country.ToLowerInvariant()}", userId.ToString(), newPp).ConfigureAwait(false);
            }

            await publishAsync("peppy:update_cached_stats", userId.ToString()).ConfigureAwait(false);

            Console.WriteLine($"Recalculated user {userId} in mode {mode} (rx: {rx}) | pp: {newPp}");
        }

        /// <summary>
        /// Rebuilds a lazer player's pp and accuracy for a mode and variant from their best play on each ranked map.
        /// </summary>
        public async Task RecalculateLazerUserAsync(int userId, int mode, int variant)
        {
            string prefix = Variants.StatsPrefix(mode);
            var (statsTable, leaderboardKey) = Variants.LazerNames(variant);

            using var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false);

            var plays = (await connection.QueryAsync<LazerBestPlay>(
                @"SELECT best.pp AS Pp, best.accuracy AS Accuracy FROM (
                    SELECT l.pp, l.accuracy, ROW_NUMBER() OVER (PARTITION BY l.beatmap_md5 ORDER BY l.pp DESC) AS rn
                    FROM lazer_scores l
                    INNER JOIN beatmaps b USING(beatmap_md5)
                    WHERE l.user_id = @userId AND l.ruleset_id = @mode AND l.variant = @variant AND l.ranked_mods = 1 AND l.passed = 1
                        AND l.pp > 0 AND b.ranked IN (2, 3)
                ) best
                WHERE best.rn = 1
                ORDER BY best.pp DESC
                LIMIT 1000", new { userId, mode, variant }).ConfigureAwait(false)).ToList();

            var (newPp, accuracy) = PerformanceTotals.Lazer(plays.Select(p => (p.Pp, p.Accuracy)).ToList());

            await connection.ExecuteAsync(
                $@"INSERT INTO {statsTable} (id, username, pp_{prefix}, avg_accuracy_{prefix})
                SELECT id, username, @newPp, @accuracy FROM users WHERE id = @userId
                ON DUPLICATE KEY UPDATE pp_{prefix} = @newPp, avg_accuracy_{prefix} = @accuracy",
                new { newPp, accuracy, userId }).ConfigureAwait(false);

            var user = await connection.QuerySingleAsync<UserInfo>("SELECT country AS Country, privileges AS Privileges FROM users WHERE id = @userId", new { userId }).ConfigureAwait(false);

            var db = redis.GetDatabase();

            if ((user.Privileges & 1) > 0)
            {
                string board = $"{leaderboardKey}:{prefix}";
                string countryBoard = $"{board}:{user.Country.ToLowerInvariant()}";

                if (newPp > 0)
                {
                    await db.SortedSetAddAsync(board, userId.ToString(), newPp).ConfigureAwait(false);
                    await db.SortedSetAddAsync(countryBoard, userId.ToString(), newPp).ConfigureAwait(false);
                }
                else
                {
                    await db.SortedSetRemoveAsync(board, userId.ToString()).ConfigureAwait(false);
                    await db.SortedSetRemoveAsync(countryBoard, userId.ToString()).ConfigureAwait(false);
                }
            }

            await publishAsync("peppy:update_cached_stats", userId.ToString()).ConfigureAwait(false);

            Console.WriteLine($"Recalculated lazer user {userId} in mode {mode} (variant: {variant}) | pp: {newPp}");
        }

        /// <summary>
        /// Whether a lazer play is worth more than the cap of its mode and variant, unless its player is whitelisted for it
        /// or carries the verified badge.
        /// </summary>
        private static async Task<bool> exceedsPpCapAsync(LazerScore score, double pp)
        {
            using var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false);

            var limit = await connection.QuerySingleOrDefaultAsync<PpLimit>(
                @"SELECT CAST(pp AS DOUBLE) AS Pp, CAST(flashlight_pp AS DOUBLE) AS FlashlightPp FROM pp_limits
                WHERE mode = @mode AND relax = @variant LIMIT 1",
                new { mode = score.RulesetId, variant = score.Variant }).ConfigureAwait(false);

            bool flashlight = JArray.Parse(score.Mods).Any(m => (string?)m["acronym"] == "FL");

            if (limit == null || !limit.IsExceededBy(pp, flashlight))
                return false;

            int whitelisted = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM whitelist WHERE user_id = @userId AND (modes & (1 << @bit)) <> 0",
                new { userId = score.UserId, bit = PpLimit.WhitelistBit(score.RulesetId, score.Variant) }).ConfigureAwait(false);

            if (whitelisted > 0)
                return false;

            if (Settings.VerifiedBadgeId > 0)
            {
                int verified = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM user_badges WHERE `user` = @userId AND badge = @badge",
                    new { userId = score.UserId, badge = Settings.VerifiedBadgeId }).ConfigureAwait(false);

                if (verified > 0)
                    return false;
            }

            return true;
        }

        private static async Task recalculateStatusesAsync(MySqlConnection connection, string table, int userId, int mode)
        {
            // A dodge the beat map has its own rule for the best play, which the score servers apply.
            var scores = await connection.QueryAsync<StatusRow>(
                $@"SELECT s.id AS Id, s.beatmap_md5 AS BeatmapMd5, s.completed AS Completed FROM {table} s
                WHERE s.userid = @userId AND s.play_mode = @mode AND s.completed IN (2, 3)
                AND NOT EXISTS (SELECT 1 FROM beatmaps b WHERE b.beatmap_md5 = s.beatmap_md5 AND b.dodge_the_beat = 1)
                ORDER BY s.beatmap_md5, s.pp DESC, s.id", new { userId, mode }).ConfigureAwait(false);

            var bests = new List<long>();
            var others = new List<long>();

            foreach (var onMap in scores.GroupBy(s => s.BeatmapMd5))
            {
                foreach (var (score, index) in onMap.Select((s, i) => (s, i)))
                {
                    int status = index == 0 ? 3 : 2;

                    if (score.Completed != status)
                        (status == 3 ? bests : others).Add(score.Id);
                }
            }

            foreach (var ids in bests.Chunk(status_batch_size))
                await connection.ExecuteAsync($"UPDATE {table} SET completed = 3 WHERE id IN @ids", new { ids }).ConfigureAwait(false);

            foreach (var ids in others.Chunk(status_batch_size))
                await connection.ExecuteAsync($"UPDATE {table} SET completed = 2 WHERE id IN @ids", new { ids }).ConfigureAwait(false);
        }

        private Task publishAsync(string channel, string message) =>
            redis.GetSubscriber().PublishAsync(RedisChannel.Literal(channel), message);

        private sealed class UserInfo
        {
            public string Country { get; set; } = string.Empty;

            public long Privileges { get; set; }
        }

        private sealed class StatusRow
        {
            public long Id { get; set; }

            public string BeatmapMd5 { get; set; } = string.Empty;

            public int Completed { get; set; }
        }

        private sealed class LazerBestPlay
        {
            public double Pp { get; set; }

            public double Accuracy { get; set; }
        }
    }
}
