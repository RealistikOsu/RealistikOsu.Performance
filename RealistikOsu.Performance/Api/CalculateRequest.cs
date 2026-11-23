using System.Text.Json;

namespace RealistikOsu.Performance.Api
{
    /// <summary>
    /// The request body of the old service, field for field, so the score servers calling it don't change.
    /// </summary>
    public sealed class CalculateRequest
    {
        public int BeatmapId { get; set; }

        public int Mode { get; set; }

        public int Mods { get; set; }

        public int MaxCombo { get; set; }

        public double Accuracy { get; set; }

        public int MissCount { get; set; }

        public int? PassedObjects { get; set; }

        public double? PlaybackRate { get; set; }

        /// <summary>
        /// Whether the score is from osu!lazer and scored the lazer way. Everything else counts as stable.
        /// </summary>
        public bool Lazer { get; set; }

        public JsonElement? LazerMods { get; set; }
    }

    public sealed class CalculateResponse
    {
        public double Stars { get; set; }

        public double Pp { get; set; }

        public double Ar { get; set; }

        public double Od { get; set; }

        public int MaxCombo { get; set; }
    }
}
