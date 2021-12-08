namespace MultiCache.Helpers
{
    using System;
    using System.Threading.Tasks;

    public static class FailureHelper
    {
        private static async Task<bool> HandleException(
            Exception ex,
            int i,
            Action<Exception>? onFailure,
            Func<Exception, bool>? dontTryAgainWhen,
            int retries,
            int retryDelayMs
        )
        {
            if (i + 1 >= retries)
            {
                Log.Put($"Failure {i + 1}/{retries} ({ex.Message}), giving up!", LogLevel.Debug);
            }
            else
            {
                Log.Put(
                    $"Failure {i + 1}/{retries} ({ex.Message}) retry in {retryDelayMs}ms",
                    LogLevel.Debug
                );
            }

            onFailure?.Invoke(ex);
            if (dontTryAgainWhen?.Invoke(ex) == true)
            {
                return false;
            }
            if (i + 1 < retries)
            {
                await Task.Delay(retryDelayMs).ConfigureAwait(false);
            }
            return true;
        }
        public static async Task<T> TryAsync<T>(
            Func<Task<T>> task,
            Action<Exception>? onFailure = null,
            Func<Exception, bool>? doNotTryAgainWhen = null,
            int retries = 3,
            int retryDelayMs = 5000
        )
        {
            Exception? exception = null;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    return await task().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exception = ex;
                    if (
                        !await HandleException(
                                ex,
                                i,
                                onFailure,
                                doNotTryAgainWhen,
                                retries,
                                retryDelayMs
                            )
                            .ConfigureAwait(false)
                    )
                    {
                        break;
                    }
                }
            }

            throw exception ?? new ArgumentException("The task failed unexpectedly.");
        }

        public static async Task TryAsync(
            Func<Task> task,
            Action<Exception>? onFailure = null,
            Func<Exception, bool>? dontTryAgainWhen = null,
            int retries = 3,
            int retryDelayMs = 5000,
            bool throwException = true
        )
        {
            Exception? exception = null;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    await task().ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    if (
                        !await HandleException(
                                ex,
                                i,
                                onFailure,
                                dontTryAgainWhen,
                                retries,
                                retryDelayMs
                            )
                            .ConfigureAwait(false)
                    )
                    {
                        break;
                    }
                }
            }

            if (exception is not null && throwException)
            {
                throw exception;
            }
        }
    }
}
