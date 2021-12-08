namespace MultiCache.Scheduling
{
    using Cronos;
    using MultiCache.Helpers;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class SimpleScheduler
    {
        private DateTime? _nextOccurrence;

        public SimpleScheduler(CronExpression cronExpression, Func<Task> action)
        {
            CronExpression = cronExpression;
            Action = action;
        }

        public Func<Task> Action { get; set; }
        public CronExpression CronExpression { get; set; }
        public int PollingIntervalMs { get; set; } = 10_000;

        public async Task RunAsync(CancellationToken ct = default)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var now = DateTime.UtcNow;
                if (_nextOccurrence is null)
                {
                    _nextOccurrence = CronExpression.GetNextOccurrence(now);
                }
                else
                {
                    if (now >= _nextOccurrence)
                    {
                        // if the computer was suspended it is possible the last scheduled task
                        // was due a long time ago so we only care about recent tasks, with a little margin for safety
                        if (
                            (now - _nextOccurrence.Value).TotalMilliseconds <= 3 * PollingIntervalMs
                        )
                        {
                            try
                            {
                                await Action().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Log.Put(ex);
                            }
                        }

                        _nextOccurrence = CronExpression.GetNextOccurrence(now);
                    }

                    await Task.Delay(PollingIntervalMs, ct).ConfigureAwait(false);
                }
            }
        }
    }
}
