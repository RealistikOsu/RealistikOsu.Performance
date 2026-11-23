using Newtonsoft.Json;
using NUnit.Framework;
using RealistikOsu.Performance.Queue;

namespace RealistikOsu.Performance.Tests.Queue
{
    [TestFixture]
    public class ScoreItemTest
    {
        [Test]
        public void TestReadsWhatOtherServicesPush()
        {
            var item = JsonConvert.DeserializeObject<ScoreItem>("{\"table\":\"scores_relax\",\"score_id\":123}")!;

            Assert.That(item.Table, Is.EqualTo("scores_relax"));
            Assert.That(item.ScoreId, Is.EqualTo(123));
            Assert.That(item.IsLazer, Is.False);
        }

        [Test]
        public void TestSurvivesBeingRequeued()
        {
            var item = JsonConvert.DeserializeObject<ScoreItem>(JsonConvert.SerializeObject(new ScoreItem("lazer_scores", 9) { TotalRetries = 2 }))!;

            Assert.That(item.IsLazer, Is.True);
            Assert.That(item.ScoreId, Is.EqualTo(9));
            Assert.That(item.TotalRetries, Is.EqualTo(2));
        }
    }
}
