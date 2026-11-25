namespace RealistikOsu.Performance.Recalc
{
    /// <summary>
    /// Whose totals a recalculated score belongs to. <see cref="Variant"/> is the rx number for a stable score.
    /// <see cref="BeatmapMd5"/> is the map it was played on, the only one whose best play it can change.
    /// </summary>
    public sealed record ScoreOwner(int UserId, int Mode, int Variant, string BeatmapMd5 = "");
}
