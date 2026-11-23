using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using RealistikOsu.Performance.Beatmaps;
using StackExchange.Redis;

namespace RealistikOsu.Performance.Calculation
{
    /// <summary>
    /// Difficulty attributes are the expensive part of a calculation, so each is worked out once per map file, ruleset and
    /// set of difficulty mods. They are kept in memory and, when Redis is given, shared through it between instances.
    /// </summary>
    public sealed class DifficultyCache : IDisposable
    {
        private readonly IDatabase? redis;
        private readonly TimeSpan redisTtl;
        private readonly MemoryCache memory;
        private readonly SemaphoreSlim cpu = new SemaphoreSlim(Environment.ProcessorCount);
        private readonly ConcurrentDictionary<string, Lazy<Task<DifficultyAttributes>>> inFlight = new ConcurrentDictionary<string, Lazy<Task<DifficultyAttributes>>>();

        public long Hits => hits;
        public long Misses => misses;

        private long hits;
        private long misses;

        public DifficultyCache(IDatabase? redis, long memoryEntries, TimeSpan redisTtl)
        {
            this.redis = redis;
            this.redisTtl = redisTtl;

            memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = memoryEntries });
        }

        public async Task<DifficultyAttributes> GetAsync(BeatmapFile file, Ruleset ruleset, Mod[] mods)
        {
            var calculator = ruleset.CreateDifficultyCalculator(file.Working);
            string key = $"performance:difficulty:{ruleset.RulesetInfo.OnlineID}:{calculator.Version}:{file.Checksum}:{modKey(mods)}";

            if (memory.TryGetValue(key, out CachedAttributes? cached) && cached != null)
            {
                Interlocked.Increment(ref hits);
                return cached.Attributes;
            }

            return await inFlight.GetOrAdd(key, _ => new Lazy<Task<DifficultyAttributes>>(() => loadAsync(key, file, calculator, mods))).Value.ConfigureAwait(false);
        }

        private async Task<DifficultyAttributes> loadAsync(string key, BeatmapFile file, DifficultyCalculator calculator, Mod[] mods)
        {
            try
            {
                DifficultyAttributes? attributes = await readRedisAsync(key, file).ConfigureAwait(false);

                if (attributes != null)
                {
                    Interlocked.Increment(ref hits);
                }
                else
                {
                    Interlocked.Increment(ref misses);

                    await cpu.WaitAsync().ConfigureAwait(false);

                    try
                    {
                        attributes = await Task.Run(() => calculator.Calculate(mods)).ConfigureAwait(false);
                    }
                    finally
                    {
                        cpu.Release();
                    }

                    await writeRedisAsync(key, attributes).ConfigureAwait(false);
                }

                memory.Set(key, new CachedAttributes(attributes), new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromHours(1) });
                return attributes;
            }
            finally
            {
                inFlight.TryRemove(key, out Lazy<Task<DifficultyAttributes>>? _);
            }
        }

        private async Task<DifficultyAttributes?> readRedisAsync(string key, BeatmapFile file)
        {
            if (redis == null)
                return null;

            RedisValue value = await redis.StringGetAsync(key).ConfigureAwait(false);

            if (value.IsNullOrEmpty)
                return null;

            return Deserialise(value.ToString(), file);
        }

        private async Task writeRedisAsync(string key, DifficultyAttributes attributes)
        {
            if (redis == null)
                return;

            await redis.StringSetAsync(key, Serialise(attributes), redisTtl).ConfigureAwait(false);
        }

        /// <summary>
        /// Stored as the attribute ids and values osu-web keeps, which is what every ruleset restores itself from.
        /// </summary>
        internal static string Serialise(DifficultyAttributes attributes) =>
            JsonConvert.SerializeObject(new RedisEntry(attributes.GetType().FullName!, attributes.ToDatabaseAttributes().ToDictionary(a => a.attributeId, a => Convert.ToDouble(a.value))));

        internal static DifficultyAttributes Deserialise(string value, IBeatmapOnlineInfo beatmap)
        {
            var entry = JsonConvert.DeserializeObject<RedisEntry>(value)!;
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(entry.Type)).First(t => t != null)!;

            var attributes = (DifficultyAttributes)Activator.CreateInstance(type)!;
            attributes.FromDatabaseAttributes(entry.Values, beatmap);
            return attributes;
        }

        /// <summary>
        /// Mods that can't change difficulty are left out so plays that differ only by them share an entry.
        /// </summary>
        private static string modKey(Mod[] mods) =>
            string.Join(',', mods
                             .Where(m => m is not (ModNoFail or ModSuddenDeath or ModPerfect))
                             .Select(m => new APIMod(m))
                             .Select(m => $"{m.Acronym}{JsonConvert.SerializeObject(m.Settings)}")
                             .Order());

        public void Dispose()
        {
            memory.Dispose();
            cpu.Dispose();
        }

        private sealed record CachedAttributes(DifficultyAttributes Attributes);

        private sealed record RedisEntry(string Type, Dictionary<int, double> Values);
    }
}
