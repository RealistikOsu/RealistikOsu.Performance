using Newtonsoft.Json;
using osu.Server.QueueProcessor;
using RealistikOsu.Performance.Data;

namespace RealistikOsu.Performance.Queue
{
    /// <summary>
    /// A score whose pp is to be calculated, pushed to Redis as JSON, e.g. <c>{"table":"scores_relax","score_id":123}</c>.
    /// </summary>
    public class ScoreItem : QueueItem
    {
        /// <summary>
        /// Where the score is stored. One of <c>scores</c>, <c>scores_relax</c>, <c>scores_ap</c> and <c>lazer_scores</c>.
        /// </summary>
        [JsonProperty("table")]
        public string Table { get; set; } = string.Empty;

        [JsonProperty("score_id")]
        public long ScoreId { get; set; }

        [JsonIgnore]
        public bool IsLazer => Table == Variants.LAZER_SCORES_TABLE;

        public ScoreItem()
        {
        }

        public ScoreItem(string table, long scoreId)
        {
            Table = table;
            ScoreId = scoreId;
        }

        public override string ToString() => $"{Table}/{ScoreId}";
    }
}
