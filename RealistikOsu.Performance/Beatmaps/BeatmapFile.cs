using System;
using osu.Game.Beatmaps;
using osu.Game.Online.API;

namespace RealistikOsu.Performance.Beatmaps
{
    /// <summary>
    /// A parsed .osu file. The checksum is of the file's bytes, so a map that was edited gets new difficulty attributes.
    /// </summary>
    public sealed record BeatmapFile(int BeatmapId, string Checksum, long Length, WorkingBeatmap Working, int CircleCount, int SliderCount, int SpinnerCount)
        : IBeatmapOnlineInfo
    {
        public int? MaxCombo => null;

        public float ApproachRate => Working.BeatmapInfo.Difficulty.ApproachRate;

        public float CircleSize => Working.BeatmapInfo.Difficulty.CircleSize;

        public float DrainRate => Working.BeatmapInfo.Difficulty.DrainRate;

        public float OverallDifficulty => Working.BeatmapInfo.Difficulty.OverallDifficulty;

        public int PlayCount => 0;

        public int PassCount => 0;

        public APIFailTimes? FailTimes => null;

        public double HitLength => 0;
    }
}
