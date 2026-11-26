using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Objects.Types;

namespace RealistikOsu.Performance.Beatmaps
{
    /// <summary>
    /// Keeps .osu files on disk, fetching a missing one once however many requests want it, and keeps the
    /// most recently used parsed maps in memory.
    /// </summary>
    public sealed class BeatmapFiles : IDisposable
    {
        private readonly string root;
        private readonly string baseUrl;
        private readonly HttpClient http;
        private readonly MemoryCache parsed;
        private readonly ConcurrentDictionary<int, Lazy<Task>> downloads = new ConcurrentDictionary<int, Lazy<Task>>();

        public BeatmapFiles(string root, string baseUrl, long parsedCacheBytes)
        {
            this.root = root;
            this.baseUrl = baseUrl.TrimEnd('/');

            http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            parsed = new MemoryCache(new MemoryCacheOptions { SizeLimit = parsedCacheBytes });
        }

        public string PathOf(int beatmapId) => Path.Combine(root, $"{beatmapId}.osu");

        /// <param name="beatmapId">The map to get.</param>
        /// <param name="checksum">The MD5 of the version that is needed. Null takes whichever is on disk.</param>
        /// <exception cref="HttpRequestException">The map, or the needed version of it, couldn't be fetched.</exception>
        public async Task<BeatmapFile> GetAsync(int beatmapId, string? checksum = null)
        {
            string path = PathOf(beatmapId);

            await ensureOnDiskAsync(path, beatmapId).ConfigureAwait(false);

            BeatmapFile file = await readAsync(path, beatmapId).ConfigureAwait(false);

            if (checksum == null || matches(file.Checksum, checksum))
                return file;

            // The map was updated after its file was fetched.
            await downloadOnceAsync(path, beatmapId, checksum).ConfigureAwait(false);
            file = await readAsync(path, beatmapId).ConfigureAwait(false);

            // A download of another version may have been running already.
            if (!matches(file.Checksum, checksum))
                throw new HttpRequestException($"The .osu of beatmap {beatmapId} isn't the version {checksum}.");

            return file;
        }

        private async Task<BeatmapFile> readAsync(string path, int beatmapId)
        {
            var info = new System.IO.FileInfo(path);
            string stamp = $"{beatmapId}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";

            if (parsed.TryGetValue(stamp, out BeatmapFile? cached) && cached != null)
                return cached;

            BeatmapFile file = await Task.Run(() => parse(path, beatmapId)).ConfigureAwait(false);

            parsed.Set(stamp, file, new MemoryCacheEntryOptions { Size = Math.Max(file.Length, 1), SlidingExpiration = TimeSpan.FromHours(1) });
            return file;
        }

        private static BeatmapFile parse(string path, int beatmapId)
        {
            byte[] bytes = File.ReadAllBytes(path);

            using var stream = new MemoryStream(bytes);
            using var reader = new LineBufferedReader(stream);

            var beatmap = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            beatmap.BeatmapInfo.Ruleset = Rulesets.ById(beatmap.BeatmapInfo.Ruleset.OnlineID).RulesetInfo;
            beatmap.BeatmapInfo.OnlineID = beatmapId;

            int sliders = beatmap.HitObjects.Count(h => h is IHasPath);
            int spinners = beatmap.HitObjects.Count(h => h is IHasDuration && h is not IHasPath);

            return new BeatmapFile(beatmapId, Convert.ToHexString(MD5.HashData(bytes)), bytes.Length, new FlatWorkingBeatmap(beatmap),
                beatmap.HitObjects.Count - sliders - spinners, sliders, spinners);
        }

        /// <summary>
        /// Fetches the .osu when it isn't on disk, without parsing it.
        /// </summary>
        /// <exception cref="HttpRequestException">The map isn't on disk and couldn't be fetched.</exception>
        public Task EnsureOnDiskAsync(int beatmapId) => ensureOnDiskAsync(PathOf(beatmapId), beatmapId);

        private Task ensureOnDiskAsync(string path, int beatmapId) =>
            isUsable(path) ? Task.CompletedTask : downloadOnceAsync(path, beatmapId, null);

        private Task downloadOnceAsync(string path, int beatmapId, string? checksum)
        {
            // The download runs as a task of its own, so a caller that goes away doesn't cancel it for the others.
            return downloads.GetOrAdd(beatmapId, _ => new Lazy<Task>(() => Task.Run(async () =>
            {
                try
                {
                    await downloadAsync(path, beatmapId, checksum).ConfigureAwait(false);
                }
                finally
                {
                    downloads.TryRemove(beatmapId, out Lazy<Task>? _);
                }
            }))).Value;
        }

        private async Task downloadAsync(string path, int beatmapId, string? checksum)
        {
            if (checksum == null && isUsable(path))
                return;

            Console.WriteLine($"downloading .osu for beatmap {beatmapId}");

            byte[] bytes = await http.GetByteArrayAsync($"{baseUrl}/{beatmapId}").ConfigureAwait(false);

            if (bytes.Length == 0)
                throw new HttpRequestException($"Empty .osu received for beatmap {beatmapId}.");

            // Another version doesn't replace the file, which may be one that was put there by hand.
            if (checksum != null && !matches(Convert.ToHexString(MD5.HashData(bytes)), checksum))
                throw new HttpRequestException($"The .osu received for beatmap {beatmapId} isn't the version {checksum}.");

            // Written under its own name and moved into place, so a crash can't leave a cut-off file behind.
            string partial = $"{path}.{Guid.NewGuid():N}.part";

            try
            {
                await File.WriteAllBytesAsync(partial, bytes).ConfigureAwait(false);
                File.Move(partial, path, true);
            }
            finally
            {
                File.Delete(partial);
            }
        }

        private static bool matches(string actual, string expected) => actual.Equals(expected, StringComparison.OrdinalIgnoreCase);

        private static bool isUsable(string path) => File.Exists(path) && new System.IO.FileInfo(path).Length > 0;

        public void Dispose()
        {
            http.Dispose();
            parsed.Dispose();
        }
    }
}
