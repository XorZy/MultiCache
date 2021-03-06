namespace MultiCache.Utils
{
    using System;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.MultiCache.Models;

    public static class Extensions
    {
        public static async Task CopyToMultiAsync(
            this Stream input,
            int bufferSize,
            IProgress<int>? progress,
            CancellationToken ct,
            int timeoutMs = -1,
            params Stream[] destinations
        )
        {
            var buffer = new byte[bufferSize];
            int read;

            CancellationTokenSource? timeoutSource = null,
                combinedCtSource = null;

            try
            {
                do
                {
                    timeoutSource?.Dispose();
                    combinedCtSource?.Dispose();
                    timeoutSource = new CancellationTokenSource(timeoutMs);
                    combinedCtSource = CancellationTokenSource.CreateLinkedTokenSource(
                        ct,
                        timeoutSource.Token
                    );
                    read = await input
                        .ReadAsync(buffer, combinedCtSource.Token)
                        .ConfigureAwait(false);
                    progress?.Report(read);

                    await Task.WhenAll(destinations.Select(d => d.WriteAsync(buffer, 0, read)))
                        .ConfigureAwait(false);
                } while (read > 0);
            }
            finally
            {
                timeoutSource?.Dispose();
                combinedCtSource?.Dispose();
            }
        }

        public static bool EndsWithInvariant(this string str, string value) =>
            str.EndsWith(value, StringComparison.InvariantCulture);

        public static async Task<int> FillAsync(this Stream source, byte[] buffer)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var currentRead = await source
                    .ReadAsync(buffer.AsMemory(read, buffer.Length - read))
                    .ConfigureAwait(false);
                if (currentRead == 0)
                {
                    break;
                }

                read += currentRead;
            }

            return read;
        }

        /* public static long GetContentRangeLength(this HttpWebResponse response)
         {
             var contentRange = response.Headers["Content-Range"];
             if (contentRange is null)
             {
                 return -1;
             }

             var tmp = contentRange.Split('/');
             if (tmp.Length != 2)
             {
                 return -1;
             }

             if (!long.TryParse(tmp[1], out var result))
             {
                 return -1;
             }

             return result;
         }*/

        public static Uri Combine(this Uri source, string relativePath)
        {
            var a = source.AbsoluteUri.TrimEnd('/');
            //return new Uri($"{a}/{UrlEncoder.Default.Encode(relativePath.TrimStart('/'))}");
            return new Uri($"{a}/{relativePath.TrimStart('/')}");
        }

        public static bool StartsWithInvariant(this string str, string value) =>
            str.StartsWith(value, StringComparison.InvariantCulture);

        public static double ToDoubleInvariant(this string str) =>
            double.Parse(str, CultureInfo.InvariantCulture.NumberFormat);

        public static int ToIntInvariant(this string str) =>
            int.Parse(str, CultureInfo.InvariantCulture.NumberFormat);

        public static long ToLongInvariant(this string str) =>
            long.Parse(str, CultureInfo.InvariantCulture.NumberFormat);

        public static string ToStringInvariant(this long num) =>
            num.ToString(CultureInfo.InvariantCulture.NumberFormat);
    }
}
