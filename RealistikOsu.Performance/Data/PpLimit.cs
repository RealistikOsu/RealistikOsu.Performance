namespace RealistikOsu.Performance.Data
{
    /// <summary>
    /// A row of <c>pp_limits</c>: the most pp a play may be worth in a mode and variant before it is held back.
    /// </summary>
    public class PpLimit
    {
        public double Pp { get; set; }

        public double FlashlightPp { get; set; }

        /// <summary>
        /// Flashlight plays have a cap of their own.
        /// </summary>
        public bool IsExceededBy(double pp, bool flashlight) => pp > (flashlight ? FlashlightPp : Pp);

        /// <summary>
        /// The bit of <c>whitelist.modes</c> that lifts the cap for a mode and variant.
        /// </summary>
        public static int WhitelistBit(int rulesetId, int variant) => rulesetId + variant * 4;
    }
}
