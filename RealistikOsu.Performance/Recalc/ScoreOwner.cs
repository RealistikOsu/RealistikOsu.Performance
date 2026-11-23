namespace RealistikOsu.Performance.Recalc
{
    /// <summary>
    /// Whose totals a recalculated score belongs to. <see cref="Variant"/> is the rx number for a stable score.
    /// </summary>
    public sealed record ScoreOwner(int UserId, int Mode, int Variant);
}
