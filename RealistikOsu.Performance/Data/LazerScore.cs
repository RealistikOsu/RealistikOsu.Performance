namespace RealistikOsu.Performance.Data
{
    /// <summary>
    /// A row of <c>lazer_scores</c> joined to its beatmap.
    /// </summary>
    public class LazerScore
    {
        public const string SELECT = @"SELECT l.id AS Id, l.user_id AS UserId, l.ruleset_id AS RulesetId, l.variant AS Variant,
            l.beatmap_id AS BeatmapId, l.beatmap_md5 AS BeatmapMd5, l.max_combo AS MaxCombo, CAST(l.mods AS CHAR) AS Mods,
            CAST(l.statistics AS CHAR) AS Statistics, l.pp AS Pp, l.ranked_mods AS RankedMods
            FROM lazer_scores l
            INNER JOIN beatmaps b ON b.beatmap_md5 = l.beatmap_md5";

        public long Id { get; set; }

        public int UserId { get; set; }

        public int RulesetId { get; set; }

        public int Variant { get; set; }

        public int BeatmapId { get; set; }

        public string BeatmapMd5 { get; set; } = string.Empty;

        public int MaxCombo { get; set; }

        public string Mods { get; set; } = "[]";

        public string Statistics { get; set; } = "{}";

        public double Pp { get; set; }

        public bool RankedMods { get; set; }
    }
}
