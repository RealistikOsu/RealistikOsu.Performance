using System.IO;
using System.Text;

namespace RealistikOsu.Performance.Tests
{
    public static class TestMap
    {
        /// <summary>
        /// Writes a map of circles and sliders to the folder and returns its id.
        /// </summary>
        public static int Write(string folder, int id, float circleSize = 4)
        {
            var osu = new StringBuilder();
            osu.Append("osu file format v14\n\n[General]\nMode: 0\n\n[Metadata]\nTitle:test\nArtist:test\nCreator:test\nVersion:test\n\n");
            osu.Append($"[Difficulty]\nHPDrainRate:5\nCircleSize:{circleSize}\nOverallDifficulty:8\nApproachRate:9\nSliderMultiplier:1.4\nSliderTickRate:1\n\n");
            osu.Append("[TimingPoints]\n0,500,4,2,0,100,1,0\n\n[HitObjects]\n");

            for (int i = 0; i < 300; i++)
            {
                int x = 64 + i * 37 % 384;
                int y = 48 + i * 53 % 288;
                int time = 1000 + i * 180;

                if (i % 5 == 4)
                    osu.Append($"{x},{y},{time},2,0,L|{(x + 120) % 500}:{y},1,120\n");
                else
                    osu.Append($"{x},{y},{time},1,0,0:0:0:0:\n");
            }

            File.WriteAllText(Path.Combine(folder, $"{id}.osu"), osu.ToString());
            return id;
        }
    }
}
