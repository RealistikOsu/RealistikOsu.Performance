namespace RealistikOsu.Performance.Data
{
    /// <summary>
    /// A row of <c>scores</c>, <c>scores_relax</c> or <c>scores_ap</c> joined to its beatmap.
    /// </summary>
    public class StableScore
    {
        public const string SELECT = @"SELECT s.id AS Id, s.userid AS UserId, s.max_combo AS MaxCombo, s.mods AS Mods,
            (s.playback_rate + 0e0) AS PlaybackRate, CAST(s.mods_json AS CHAR) AS ModsJson,
            s.`300_count` AS Count300, s.`100_count` AS Count100, s.`50_count` AS Count50,
            s.katus_count AS CountKatu, s.gekis_count AS CountGeki, s.misses_count AS CountMiss,
            s.play_mode AS PlayMode, s.completed AS Completed, COALESCE(s.pp, 0) AS Pp, b.beatmap_id AS BeatmapId,
            b.dodge_the_beat AS DodgeTheBeat";

        public long Id { get; set; }

        public int UserId { get; set; }

        public int MaxCombo { get; set; }

        public int Mods { get; set; }

        public double PlaybackRate { get; set; }

        public string? ModsJson { get; set; }

        public int Count300 { get; set; }

        public int Count100 { get; set; }

        public int Count50 { get; set; }

        public int CountKatu { get; set; }

        public int CountGeki { get; set; }

        public int CountMiss { get; set; }

        public int PlayMode { get; set; }

        public int Completed { get; set; }

        public double Pp { get; set; }

        public int BeatmapId { get; set; }

        /// <summary>
        /// A catch map played by avoiding every object. It ranks by how much was avoided and never awards pp, so the
        /// score servers keep its best plays, and the pp stays 0.
        /// </summary>
        public bool DodgeTheBeat { get; set; }
    }
}
