using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealistikOsu.Performance.Calculation;
using StatsdClient;

namespace RealistikOsu.Performance.Api
{
    public static class ApiHost
    {
        public static async Task RunAsync(PlayCalculator calculator)
        {
            var builder = WebApplication.CreateBuilder();

            builder.WebHost.UseUrls($"http://{Settings.ApiHost}:{Settings.ApiPort}");
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
            });

            var app = builder.Build();

            var statsd = createStatsd();

            app.Use(async (context, next) =>
            {
                var stopwatch = Stopwatch.StartNew();
                await next().ConfigureAwait(false);
                statsd?.Timer("performance.request.time", stopwatch.Elapsed.TotalMilliseconds, tags: new[] { $"status:{context.Response.StatusCode / 100}xx" });
            });

            app.MapGet("/api/v1/status", () => new { status = 200, online = true });
            app.MapPost("/api/v1/calculate", async (List<CalculateRequest> requests) =>
                await Task.WhenAll(requests.Select(r => calculateAsync(calculator, r))).ConfigureAwait(false));

            Console.WriteLine($"serving on {Settings.ApiHost}:{Settings.ApiPort}");
            await app.RunAsync().ConfigureAwait(false);
        }

        private static async Task<CalculateResponse> calculateAsync(PlayCalculator calculator, CalculateRequest request)
        {
            try
            {
                if (request.Mode is < 0 or > 3)
                    return toResponse(PlayResult.ZERO);

                var ruleset = Rulesets.ById(request.Mode);
                var mods = ModFactory.Create(ruleset, request.Mods, request.LazerMods?.GetRawText(), request.PlaybackRate, classic: !request.Lazer);

                var result = await calculator.CalculateAsync(new PlayInput
                {
                    BeatmapId = request.BeatmapId,
                    RulesetId = request.Mode,
                    Mods = mods,
                    MaxCombo = request.MaxCombo,
                    Accuracy = request.Accuracy,
                    Misses = request.MissCount,
                    PassedObjects = request.PassedObjects,
                }).ConfigureAwait(false);

                return toResponse(result);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"couldn't get .osu for beatmap {request.BeatmapId}: {e.Message}");
                return toResponse(PlayResult.ZERO);
            }
            catch (Exception e)
            {
                // A map that can't be read or converted to the asked mode has no pp, which isn't worth failing the rest of the batch.
                Console.WriteLine($"couldn't calculate beatmap {request.BeatmapId}: {e}");
                return toResponse(PlayResult.ZERO);
            }
        }

        private static CalculateResponse toResponse(PlayResult result) => new CalculateResponse
        {
            Stars = Math.Round(result.Stars, 2),
            Pp = Math.Round(result.Pp, 2),
            Ar = result.ApproachRate,
            Od = result.OverallDifficulty,
            MaxCombo = result.MaxCombo,
        };

        private static DogStatsdService? createStatsd()
        {
            if (string.IsNullOrEmpty(Settings.StatsdHost))
                return null;

            var statsd = new DogStatsdService();
            statsd.Configure(new StatsdConfig { StatsdServerName = Settings.StatsdHost, StatsdPort = Settings.StatsdPort });
            return statsd;
        }
    }
}
