using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NUnit.Framework;
using RealistikOsu.Performance.Beatmaps;

namespace RealistikOsu.Performance.Tests
{
    [TestFixture]
    public class BeatmapFilesTest
    {
        private string folder = null!;
        private byte[] updated = null!;
        private HttpListener server = null!;
        private BeatmapFiles beatmaps = null!;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string source = Path.Combine(folder, "source");
            Directory.CreateDirectory(source);

            TestMap.Write(folder, 1);
            TestMap.Write(source, 1, circleSize: 5);
            updated = File.ReadAllBytes(Path.Combine(source, "1.osu"));

            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            string url = $"http://localhost:{((IPEndPoint)probe.LocalEndpoint).Port}/";
            probe.Stop();

            server = new HttpListener();
            server.Prefixes.Add(url);
            server.Start();
            _ = serve();

            beatmaps = new BeatmapFiles(folder, url, 1 << 20);
        }

        [TearDown]
        public void TearDown()
        {
            beatmaps.Dispose();
            server.Close();
            Directory.Delete(folder, true);
        }

        private async Task serve()
        {
            while (server.IsListening)
            {
                var context = await server.GetContextAsync();
                await context.Response.OutputStream.WriteAsync(updated);
                context.Response.Close();
            }
        }

        [Test]
        public async Task TestOutdatedFileIsDownloadedAgain()
        {
            await beatmaps.GetAsync(1);

            var file = await beatmaps.GetAsync(1, checksum(updated));

            Assert.That(file.CircleSize, Is.EqualTo(5));
            Assert.That(File.ReadAllBytes(beatmaps.PathOf(1)), Is.EqualTo(updated));
        }

        [Test]
        public void TestWrongDownloadIsRefusedAndNotKept()
        {
            byte[] cached = File.ReadAllBytes(beatmaps.PathOf(1));

            Assert.ThrowsAsync<HttpRequestException>(() => beatmaps.GetAsync(1, new string('0', 32)));
            Assert.That(File.ReadAllBytes(beatmaps.PathOf(1)), Is.EqualTo(cached));
        }

        private static string checksum(byte[] bytes) => Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }
}
