using System.Threading.Tasks;
using osu.Server.QueueProcessor;
using RealistikOsu.Performance.Recalc;

namespace RealistikOsu.Performance.Queue
{
    /// <summary>
    /// Calculates the pp of each score pushed to the queue, then updates its player's totals and leaderboards.
    /// </summary>
    public class ScoreQueueProcessor : QueueProcessor<ScoreItem>
    {
        private readonly ScoreRecalculator recalculator;

        public ScoreQueueProcessor(ScoreRecalculator recalculator)
            : base(new QueueConfiguration
            {
                InputQueueName = Settings.QueueName,
                UseBlockingPop = true,
            })
        {
            this.recalculator = recalculator;
        }

        // The workers are blocked on this call, so the work runs on the default scheduler where its continuations can still run.
        protected override void ProcessResult(ScoreItem item) => Task.Run(() => processAsync(item)).GetAwaiter().GetResult();

        private async Task processAsync(ScoreItem item)
        {
            item.Tags = new[] { $"table:{item.Table}" };

            if (item.IsLazer)
            {
                var owner = await recalculator.RecalculateLazerScoreAsync(item.ScoreId).ConfigureAwait(false);

                if (owner != null)
                    await recalculator.RecalculateLazerUserAsync(owner.UserId, owner.Mode, owner.Variant).ConfigureAwait(false);

                return;
            }

            var stableOwner = await recalculator.RecalculateScoreAsync(item.Table, item.ScoreId).ConfigureAwait(false);

            if (stableOwner != null)
                await recalculator.RecalculateUserAsync(stableOwner.UserId, stableOwner.Mode, stableOwner.Variant).ConfigureAwait(false);
        }
    }
}
