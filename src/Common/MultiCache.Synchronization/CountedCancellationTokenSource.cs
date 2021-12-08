namespace MultiCache.Synchronization
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class CountedCancellationTokenSource : IDisposable
    {
        private readonly CancellationTokenSource _src = new CancellationTokenSource();
        private int _counter;

        private bool disposedValue;

        public CancellationToken Token => _src.Token;

        public async Task CountTask(Task task)
        {
            Increment();
            try
            {
                await task.ConfigureAwait(false);
            }
            finally
            {
                Decrement();
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _src.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        private void Decrement()
        {
            if (Interlocked.Decrement(ref _counter) == 0)
            {
                _src.Cancel();
                Dispose();
            }
        }

        private void Increment()
        {
            Interlocked.Increment(ref _counter);
        }
    }
}
