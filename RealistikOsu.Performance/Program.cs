using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Server.QueueProcessor;
using RealistikOsu.Performance.Api;
using RealistikOsu.Performance.Beatmaps;
using RealistikOsu.Performance.Calculation;
using RealistikOsu.Performance.Queue;
using RealistikOsu.Performance.Recalc;
using StackExchange.Redis;

namespace RealistikOsu.Performance
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Settings.MapLegacyEnvironment();

            string? component = Environment.GetEnvironmentVariable("APP_COMPONENT");

            if (string.IsNullOrEmpty(component))
            {
                component = args.FirstOrDefault();
                args = args.Skip(1).ToArray();
            }

            using var cancellation = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                Console.WriteLine("Cancellation requested!");
                cancellation.Cancel();
                e.Cancel = true;
            };

            using var beatmaps = new BeatmapFiles(Settings.BeatmapsPath, Settings.BeatmapBaseUrl, Settings.ParsedBeatmapBytes);
            using var difficulty = new DifficultyCache(shareThroughRedis(), Settings.DifficultyCacheEntries, Settings.DifficultyRedisTtl);
            var calculator = new PlayCalculator(beatmaps, difficulty);

            switch (component)
            {
                case "api":
                    await ApiHost.RunAsync(calculator).ConfigureAwait(false);
                    return 0;

                case "queue":
                    new ScoreQueueProcessor(createRecalculator(calculator, beatmaps)).Run(cancellation.Token);
                    return 0;

                case "deploy":
                    await RecalcCommands.DeployAsync(createRecalculator(calculator, beatmaps), args).ConfigureAwait(false);
                    return 0;

                case "lazer_deploy":
                    await RecalcCommands.LazerDeployAsync(createRecalculator(calculator, beatmaps), args).ConfigureAwait(false);
                    return 0;

                case "recalc":
                    await RecalcCommands.RecalcUserAsync(createRecalculator(calculator, beatmaps), args).ConfigureAwait(false);
                    return 0;

                case "recalc_zero":
                    await RecalcCommands.RecalcZeroAsync(createRecalculator(calculator, beatmaps), args).ConfigureAwait(false);
                    return 0;

                default:
                    Console.Error.WriteLine($"unknown app component '{component}', expected api, queue, deploy, lazer_deploy, recalc or recalc_zero");
                    return 1;
            }
        }

        // The cache works from memory alone, so a Redis that can't be reached only costs the sharing between instances.
        private static IDatabase? shareThroughRedis()
        {
            try
            {
                return RedisAccess.GetConnection().GetDatabase();
            }
            catch (RedisConnectionException e)
            {
                Console.WriteLine($"Redis is unreachable, difficulty attributes won't be shared: {e.Message}");
                return null;
            }
        }

        private static ScoreRecalculator createRecalculator(PlayCalculator calculator, BeatmapFiles beatmaps) =>
            new ScoreRecalculator(calculator, beatmaps, RedisAccess.GetConnection());
    }
}
