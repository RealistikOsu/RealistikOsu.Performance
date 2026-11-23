using System;

namespace RealistikOsu.Performance
{
    public static class Settings
    {
        public static string BeatmapsPath => get("BEATMAPS_PATH", ".");

        public static string BeatmapBaseUrl => get("OSU_BEATMAP_BASE_URL", "https://old.ppy.sh/osu");

        public static string ApiHost => get("API_HOST", "127.0.0.1");

        public static int ApiPort => int.Parse(get("API_PORT", "8080"));

        public static string StatsdHost => get("STATSD_HOST", string.Empty);

        public static int StatsdPort => int.Parse(get("STATSD_PORT", "8125"));

        /// <summary>
        /// Redis queue the score worker reads. The queue library prefixes it with <c>osu-queue:</c>.
        /// </summary>
        public static string QueueName => get("PERFORMANCE_QUEUE", "performance");

        public static long ParsedBeatmapBytes => long.Parse(get("PARSED_BEATMAP_CACHE_BYTES", (128L << 20).ToString()));

        public static long DifficultyCacheEntries => long.Parse(get("DIFFICULTY_CACHE_ENTRIES", "20000"));

        public static TimeSpan DifficultyRedisTtl => TimeSpan.FromHours(double.Parse(get("DIFFICULTY_CACHE_TTL_HOURS", "168")));

        /// <summary>
        /// The queue library reads its MySQL and Redis settings from its own variables, so the ones the old
        /// service took are copied over unless the library's are set already.
        /// </summary>
        public static void MapLegacyEnvironment()
        {
            setDefault("DB_HOST", Environment.GetEnvironmentVariable("MYSQL_HOST"));
            setDefault("DB_PORT", Environment.GetEnvironmentVariable("MYSQL_PORT"));
            setDefault("DB_USER", Environment.GetEnvironmentVariable("MYSQL_USER"));
            setDefault("DB_PASS", Environment.GetEnvironmentVariable("MYSQL_PASSWORD"));
            setDefault("DB_NAME", Environment.GetEnvironmentVariable("MYSQL_DATABASE"));

            string? redisHost = Environment.GetEnvironmentVariable("REDIS_HOST");
            string? redisPort = Environment.GetEnvironmentVariable("REDIS_PORT");

            if (!string.IsNullOrEmpty(redisHost) && !string.IsNullOrEmpty(redisPort) && !redisHost.Contains(':'))
                Environment.SetEnvironmentVariable("REDIS_HOST", $"{redisHost}:{redisPort}");

            setDefault("DD_AGENT_HOST", Environment.GetEnvironmentVariable("STATSD_HOST"));
        }

        private static string get(string name, string fallback)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void setDefault(string name, string? value)
        {
            if (string.IsNullOrEmpty(value) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                return;

            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
