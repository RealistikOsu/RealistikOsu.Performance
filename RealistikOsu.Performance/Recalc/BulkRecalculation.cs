using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;
using osu.Server.QueueProcessor;
using RealistikOsu.Performance.Data;

namespace RealistikOsu.Performance.Recalc
{
    /// <summary>
    /// The whole-server passes behind the recalculation commands. One bad score is logged and counted rather than
    /// stopping a pass that can take hours, and <see cref="ThrowIfFailed"/> reports them at the end.
    /// </summary>
    internal sealed class BulkRecalculation
    {
        private const int chunk_size = 100;
        private const int map_fetch_parallelism = 8;
        private const int user_parallelism = 16;
        private const string no_such_table = "42S02";

        private readonly ScoreRecalculator recalculator;
        private int failures;

        public BulkRecalculation(ScoreRecalculator recalculator)
        {
            this.recalculator = recalculator;
        }

        public void ThrowIfFailed()
        {
            if (failures > 0)
                throw new InvalidOperationException($"{failures} recalculations failed, see the log above.");
        }

        /// <returns>The players whose scores were recalculated.</returns>
        public async Task<HashSet<int>> RecalculateModeScoresAsync(int mode, int rx, bool onlyZero)
        {
            string table = Variants.ScoresTable(rx);

            List<StableScore> scores;

            using (var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false))
            {
                scores = (await connection.QueryAsync<StableScore>(
                    $@"{StableScore.SELECT} FROM {table} s INNER JOIN beatmaps b USING(beatmap_md5)
                    WHERE s.completed IN (2, 3) AND s.play_mode = @mode {(onlyZero ? "AND s.pp = 0 AND b.ranked IN (2, 3)" : string.Empty)}
                    ORDER BY s.pp DESC", new { mode }).ConfigureAwait(false)).ToList();
            }

            scores = await keepCachedAsync(scores, s => s.BeatmapId).ConfigureAwait(false);

            await forEachAsync(scores, Environment.ProcessorCount * 2, score => recalculator.RecalculateScoreAsync(table, score)).ConfigureAwait(false);

            return scores.Select(s => s.UserId).ToHashSet();
        }

        public async Task RecalculateModeUsersAsync(int mode, int rx)
        {
            List<int> userIds;

            using (var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false))
                userIds = (await connection.QueryAsync<int>("SELECT id FROM users").ConfigureAwait(false)).ToList();

            await RecalculateUsersAsync(userIds, mode, rx).ConfigureAwait(false);
        }

        public Task RecalculateUsersAsync(IReadOnlyCollection<int> userIds, int mode, int rx) =>
            forEachAsync(userIds, user_parallelism, userId => recalculator.RecalculateUserAsync(userId, mode, rx));

        /// <returns>The players and variants whose scores were recalculated.</returns>
        public async Task<HashSet<(int UserId, int Variant)>> RecalculateModeLazerScoresAsync(int mode, bool onlyZero)
        {
            List<LazerScore> scores;

            try
            {
                using var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false);

                scores = (await connection.QueryAsync<LazerScore>(
                    $@"{LazerScore.SELECT} WHERE l.ruleset_id = @mode AND l.passed = 1 AND b.ranked IN (2, 3) {(onlyZero ? "AND l.pp = 0" : string.Empty)}",
                    new { mode }).ConfigureAwait(false)).ToList();
            }
            catch (MySqlException e) when (e.SqlState == no_such_table)
            {
                // The lazer tables haven't been migrated, so there's nothing to recalculate.
                return new HashSet<(int, int)>();
            }

            scores = await keepCachedAsync(scores, s => s.BeatmapId).ConfigureAwait(false);

            await forEachAsync(scores, Environment.ProcessorCount * 2, score => recalculator.RecalculateLazerScoreAsync(score)).ConfigureAwait(false);

            return scores.Select(s => (s.UserId, s.Variant)).ToHashSet();
        }

        public async Task RecalculateModeLazerUsersAsync(int mode)
        {
            foreach (int variant in Variants.LazerVariants(mode))
            {
                string prefix = Variants.StatsPrefix(mode);
                List<int> userIds;

                try
                {
                    using var connection = await DatabaseAccess.GetConnectionAsync().ConfigureAwait(false);

                    userIds = (await connection.QueryAsync<int>(
                        $@"SELECT user_id FROM lazer_scores WHERE ruleset_id = @mode AND variant = @variant AND ranked_mods = 1 AND passed = 1 AND pp > 0
                        UNION
                        SELECT id FROM {Variants.LazerNames(variant).StatsTable} WHERE pp_{prefix} > 0",
                        new { mode, variant }).ConfigureAwait(false)).ToList();
                }
                catch (MySqlException e) when (e.SqlState == no_such_table)
                {
                    // The lazer tables haven't been migrated, so there are no lazer players.
                    return;
                }

                await forEachAsync(userIds, user_parallelism, userId => recalculator.RecalculateLazerUserAsync(userId, mode, variant)).ConfigureAwait(false);
            }
        }

        public Task RecalculateLazerUsersAsync(IReadOnlyCollection<(int UserId, int Variant)> players, int mode) =>
            forEachAsync(players, user_parallelism, player => recalculator.RecalculateLazerUserAsync(player.UserId, mode, player.Variant));

        /// <summary>
        /// Fetches each map the scores are on once, then drops the scores of a map that can't be had.
        /// </summary>
        private async Task<List<T>> keepCachedAsync<T>(List<T> scores, Func<T, int> beatmapId)
        {
            var ids = scores.Select(beatmapId).Distinct().ToList();
            var missing = new ConcurrentBag<int>();

            await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = map_fetch_parallelism }, async (id, _) =>
            {
                try
                {
                    await recalculator.Beatmaps.EnsureOnDiskAsync(id).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Couldn't get .osu for beatmap {id}, skipping its scores: {e.Message}");
                    missing.Add(id);
                }
            }).ConfigureAwait(false);

            var skipped = missing.ToHashSet();

            return scores.Where(s => !skipped.Contains(beatmapId(s))).OrderBy(beatmapId).ToList();
        }

        private async Task forEachAsync<T>(IReadOnlyCollection<T> items, int parallelism, Func<T, Task> action)
        {
            foreach (var chunk in items.Chunk(chunk_size))
            {
                await Parallel.ForEachAsync(chunk, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, async (item, _) =>
                {
                    try
                    {
                        await action(item).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Interlocked.Increment(ref failures);
                        Console.WriteLine($"Recalculation failed for {item}: {e}");
                    }
                }).ConfigureAwait(false);
            }
        }
    }
}
