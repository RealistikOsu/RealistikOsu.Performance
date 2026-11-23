using System;
using System.Linq;
using System.Threading.Tasks;

namespace RealistikOsu.Performance.Recalc
{
    /// <summary>
    /// The operator commands of the old service. Each takes its answers from the arguments, or asks on the console for
    /// whatever the arguments leave out.
    /// </summary>
    public static class RecalcCommands
    {
        /// <summary>
        /// Recalculates every stable score of the modes, then every player's totals and leaderboards.
        /// Takes the modes and the relax variants as comma separated lists.
        /// </summary>
        public static async Task DeployAsync(ScoreRecalculator recalculator, string[] args)
        {
            var modes = parseList(ask(args, 0, "Enter the modes (comma delimited) to deploy: "));
            var variants = parseList(ask(args, 1, "Enter the relax bits (comma delimited) to deploy: "));

            var bulk = new BulkRecalculation(recalculator);

            foreach (int mode in modes)
            {
                foreach (int rx in variantsOf(mode, variants))
                    await bulk.RecalculateModeScoresAsync(mode, rx, false).ConfigureAwait(false);
            }

            foreach (int mode in modes)
            {
                foreach (int rx in variantsOf(mode, variants))
                    await bulk.RecalculateModeUsersAsync(mode, rx).ConfigureAwait(false);
            }

            bulk.ThrowIfFailed();
        }

        /// <summary>
        /// Recalculates only osu!lazer: every passed score on a ranked map, then each player's totals and leaderboards.
        /// </summary>
        public static async Task LazerDeployAsync(ScoreRecalculator recalculator, string[] args)
        {
            var modes = parseList(ask(args, 0, "Enter the modes (comma delimited) to deploy: "));

            var bulk = new BulkRecalculation(recalculator);

            foreach (int mode in modes)
            {
                await bulk.RecalculateModeLazerScoresAsync(mode, false).ConfigureAwait(false);
                await bulk.RecalculateModeLazerUsersAsync(mode).ConfigureAwait(false);
            }

            bulk.ThrowIfFailed();
        }

        public static async Task RecalcUserAsync(ScoreRecalculator recalculator, string[] args)
        {
            int userId = int.Parse(ask(args, 0, "Enter the user ID of the user to recalculate: ").Trim());

            for (int rx = 0; rx < 3; rx++)
            {
                for (int mode = 0; mode < 4; mode++)
                    await recalculator.RecalculateUserAsync(userId, mode, rx).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Recalculates every passed score still on 0pp on a ranked or approved map, stable and lazer, then the totals and
        /// leaderboards of only the players who had one. For scores set while their map's .osu couldn't be read.
        /// </summary>
        public static async Task RecalcZeroAsync(ScoreRecalculator recalculator, string[] args)
        {
            var bulk = new BulkRecalculation(recalculator);

            for (int mode = 0; mode < 4; mode++)
            {
                for (int rx = 0; rx < 3; rx++)
                {
                    var players = await bulk.RecalculateModeScoresAsync(mode, rx, true).ConfigureAwait(false);
                    await bulk.RecalculateUsersAsync(players, mode, rx).ConfigureAwait(false);
                }

                var lazerPlayers = await bulk.RecalculateModeLazerScoresAsync(mode, true).ConfigureAwait(false);
                await bulk.RecalculateLazerUsersAsync(lazerPlayers, mode).ConfigureAwait(false);
            }

            bulk.ThrowIfFailed();
        }

        /// <summary>
        /// Relax and autopilot boards exist for the three oldest modes, mania only has the vanilla one.
        /// </summary>
        private static int[] variantsOf(int mode, int[] requested) => mode is >= 0 and <= 2 ? requested : new[] { 0 };

        private static int[] parseList(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();

        private static string ask(string[] args, int index, string question)
        {
            if (index < args.Length)
                return args[index];

            Console.Write(question);
            return Console.ReadLine() ?? string.Empty;
        }
    }
}
