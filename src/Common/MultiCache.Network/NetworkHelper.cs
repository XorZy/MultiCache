namespace MultiCache.Network
{
    using Common.MultiCache.Models;
    using MultiCache.Config;
    using MultiCache.Helpers;
    using MultiCache.Models;
    using MultiCache.PackageManager;
    using MultiCache.Resource;
    using MultiCache.Synchronization;
    using MultiCache.Utils;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;

    public class NetworkHelper
    {
        private readonly RepositoryConfiguration _config;

        private readonly PackageManagerBase _pkgManager;

        private readonly ConcurrentDictionary<CacheableResource, SynchronizationData> _syncDic =
            new();

        public NetworkHelper(PackageManagerBase repository)
        {
            _pkgManager = repository;
            _config = repository.Config;
        }
        public async Task<Speed> BenchmarkAsync(
            NetworkResource resource,
            CancellationToken ct = default
        )
        {
            _pkgManager.Put($"Initiating benchmark with {resource.DownloadUri}", LogLevel.Debug);
            var sw = new Stopwatch();
            sw.Start();
            using (
                var response = await GetResponseAsync(resource, 0, false, ct).ConfigureAwait(false)
            )
            {
                var buffer = new byte[_pkgManager.Config.BufferSize];
                long totalRead = 0;
                using (
                    var responseStream = await response.Content
                        .ReadAsStreamAsync(ct)
                        .ConfigureAwait(false)
                )
                {
                    while (true)
                    {
                        var read = await responseStream.ReadAsync(buffer, ct).ConfigureAwait(false);
                        totalRead += read;
                        if (read == 0)
                        {
                            break;
                        }
                    }
                }
                sw.Stop();

                var speed = new Speed((long)Math.Round((totalRead * 8) / sw.Elapsed.TotalSeconds));
                _pkgManager.Put($"Benchmark done : {speed}", LogLevel.Debug);
                return speed;
            }
        }

        public async Task FetchResourceAsync(
            NetworkResource resource,
            HttpListenerContext? clientContext = null,
            IProgress<TransferProgressInfo>? progress = null
        )
        {
            if (resource is CacheableResource cr)
            {
                await HandleCacheableResource(cr, clientContext, progress).ConfigureAwait(false);
            }
            else if (clientContext is not null)
            {
                await PassthroughAsync(
                        resource,
                        GetRequestedClientOffset(clientContext),
                        clientContext,
                        _config.ForegroundReadMaxSpeed
                    )
                    .ConfigureAwait(false);
            }
        }

        private bool CheckTotalSize(HttpResponseMessage response, long offset)
        {
            var trueContentLength = response.Content.Headers.ContentRange?.Length;
            if (
                trueContentLength is not null
                && response.Content.Headers.ContentLength is not null
                && offset + response.Content.Headers.ContentLength != trueContentLength
            )
            {
                _pkgManager.Put(
                    "The total size does not match the expected size, discarding previous data",
                    LogLevel.Error
                );
                return false;
            }

            return true;
        }

        private static void CopyHeaders(
            HttpResponseMessage source,
            HttpListenerResponse destination
        )
        {
            if (source.Content.Headers.ContentLength is not null)
            {
                destination.ContentLength64 = source.Content.Headers.ContentLength.Value;
            }
            if (source.Content.Headers.LastModified is not null)
            {
                destination.Headers["Last-Modified"] =
                    source.Content.Headers.LastModified.ToString();
            }
            if (source.Content.Headers.ContentRange is not null)
            {
                destination.Headers["Content-Range"] =
                    source.Content.Headers.ContentRange.ToString();
            }
            if (source.Content.Headers is not null)
            {
                destination.Headers["Content-Range"] =
                    source.Content.Headers.ContentRange.ToString();
            }
            var etag = source.Headers.ETag;
            if (etag is not null)
            {
                destination.Headers["Etag"] = etag.Tag;
            }

            var acceptRanges = source.Headers.AcceptRanges;
            if (acceptRanges.Count > 0)
            {
                destination.Headers["Accept-Ranges"] = acceptRanges.First();
            }
        }

        private static long GetRequestedClientOffset(HttpListenerContext clientContext)
        {
            var rangeHeader = clientContext.Request.Headers["Range"];
            if (
                rangeHeader is not null
                && RangeHeaderValue.TryParse(rangeHeader, out var range)
                && range.Ranges.Count == 1
            )
            {
                return range.Ranges.First().From ?? 0;
            }

            return 0;
        }

        private async Task<HttpResponseMessage> GetResponseAsync(
            NetworkResource resource,
            long offset,
            bool autoSwitchMirror = true,
            CancellationToken ct = default
        )
        {
            try
            {
                using (
                    var request = new HttpRequestMessage()
                    {
                        Method = HttpMethod.Get,
                        RequestUri = resource.DownloadUri
                    }
                )
                {
                    request.Headers.Range = new RangeHeaderValue(offset, null);
                    var response = await _pkgManager.Config.HttpClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                        .ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();
                    return response;
                }
            }
            catch (HttpRequestException ex)
            {
                if (autoSwitchMirror)
                {
                    switch (ex.StatusCode)
                    {
                        case HttpStatusCode.Forbidden:
                        case HttpStatusCode.GatewayTimeout:
                        case HttpStatusCode.BadGateway:
                        case HttpStatusCode.ServiceUnavailable:
                        case HttpStatusCode.Unauthorized:
                        case HttpStatusCode.InternalServerError:
                        case HttpStatusCode.TooManyRequests:
                            // perhaps something is wrong with the mirror?
                            await MirrorRanker
                                .RankAndAssignMirrorsAsync(_pkgManager)
                                .ConfigureAwait(false);
                            break;
                    }
                }
                throw;
            }
        }
        private async Task HandleCacheableResource(
            CacheableResource cr,
            HttpListenerContext? clientContext,
            IProgress<TransferProgressInfo>? progress = null
        )
        {
            long clientStartOffset = clientContext is null
                ? 0
                : GetRequestedClientOffset(clientContext);
            if (await cr.IsFreshAndComplete().ConfigureAwait(false))
            {
                if (clientContext is not null)
                {
                    await TransferFullyCachedFile(
                            cr,
                            clientContext,
                            clientStartOffset,
                            _config.ForegroundWriteMaxSpeed
                        )
                        .ConfigureAwait(false);
                }
            }
            else if (clientStartOffset > 0 && clientContext is not null)
            {
                // we cannot handle a range header from clients
                // if the client requests a range that is beyond what we already
                // have in the partial file it could create gaps in the file
                // this case should be relatively rare so it's better just to
                // give up on trying to cache it
                await PassthroughAsync(
                        cr,
                        clientStartOffset,
                        clientContext,
                        _config.ForegroundReadMaxSpeed
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                var tasks = new List<Task>();
                if (cr.TryLock()) // we make sure only one task is actually downloading the data
                {
                    var syncData = InitiateDownload(
                        cr,
                        clientContext is null
                          ? _config.BackgroundReadMaxSpeed
                          : _config.ForegroundReadMaxSpeed,
                        progress
                    );

                    async Task BuildDownloadTask()
                    {
                        try
                        {
                            await (
                                clientContext is null || _config.KeepDownloading
                                    ? syncData.CountedCTSource.MonitorTask(syncData.MonitoredTask) // make sure a client cannot cancel the task
                                    : syncData.MonitoredTask
                            ).ConfigureAwait(false);
                        }
                        finally
                        {
                            cr.Release();
                        }
                    }

                    tasks.Add(BuildDownloadTask());
                }

                if (clientContext is not null)
                {
                    var downloadInfo = _syncDic[cr];

                    tasks.Add(
                        downloadInfo.CountedCTSource.MonitorTask(
                            HookedTransfer(
                                cr,
                                downloadInfo,
                                clientContext,
                                _config.ForegroundWriteMaxSpeed
                            )
                        )
                    );
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        private async Task HookedTransfer(
            CacheableResource resource,
            SynchronizationData info,
            HttpListenerContext clientContext,
            Speed maxSpeed
        )
        {
            // we wait for the connection to be established
            await info.SynchronizationTask.ConfigureAwait(false);

            _pkgManager.Put(
                $"Beginning hooked transfer {resource.DownloadUri} to {clientContext.Request.RemoteEndPoint}",
                LogLevel.Debug
            );

            // we update the bandwidth limit in case the download started as a background task
            info.MaxReadSpeed = _config.ForegroundReadMaxSpeed;

            long totalWritten = 0;

            var contentLength = info.ContentLength;

            if (contentLength > 0)
            {
                clientContext.Response.ContentLength64 = contentLength;
            }

            clientContext.Response.AddHeader("X-Cache", "STREAM");

            using (var inputFile = resource.PartialFile.OpenRead())
            {
                using (var throttledStream = new ThrottledStream(inputFile, readSpeed: maxSpeed))
                {
                    var buffer = new byte[_config.BufferSize];
                    while (true)
                    {
                        var read = await throttledStream.ReadAsync(buffer).ConfigureAwait(false);
                        if (read == 0)
                        {
                            if (info.MonitoredTask.IsCompleted)
                            {
                                if (info.MonitoredTask.IsFaulted)
                                {
                                    throw (info.MonitoredTask.Exception as Exception)
                                        ?? new IOException("The download task ended abruptly");
                                }
                                break;
                            }

                            await Task.Delay(100).ConfigureAwait(false); // we wait for more data to arrive
                            continue;
                        }

                        await clientContext.Response.OutputStream
                            .WriteAsync(buffer.AsMemory(0, read))
                            .ConfigureAwait(false);
                        totalWritten += read;
                    }
                }
            }
        }

        private SynchronizationData InitiateDownload(
            CacheableResource cacheableResource,
            Speed maxSpeed,
            IProgress<TransferProgressInfo>? progress = null
        )
        {
            var syncTaskSource = new TaskCompletionSource();

            var ctSource = new CountedCancellationTokenSource();

            return _syncDic[cacheableResource] = new SynchronizationData(
                UpstreamFetchAndSaveAsync(
                    cacheableResource,
                    syncTaskSource,
                    maxSpeed,
                    progress,
                    ctSource.Token
                ),
                syncTaskSource.Task,
                ctSource
            );
        }

        private async Task PassthroughAsync(
            NetworkResource resource,
            long offset,
            HttpListenerContext client,
            Speed maxSpeed
        )
        {
            _pkgManager.Put(
                $"Beginning passtrhough {resource.DownloadUri} to {client.Request.UserHostName}",
                LogLevel.Debug
            );

            using (
                HttpResponseMessage response = await GetResponseAsync(resource, offset)
                    .ConfigureAwait(false)
            )
            {
                CopyHeaders(response, client.Response);
                client.Response.StatusCode = (int)response.StatusCode;
                client.Response.AddHeader("X-Cache", "PASSTROUGH");
                using (
                    var responseStream = await response.Content
                        .ReadAsStreamAsync()
                        .ConfigureAwait(false)
                )
                {
                    using (
                        var throttledStream = new ThrottledStream(
                            responseStream,
                            readSpeed: maxSpeed
                        )
                    )
                    {
                        await throttledStream
                            .CopyToAsync(client.Response.OutputStream, _config.BufferSize)
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task TransferFullyCachedFile(
            CacheableResource resource,
            HttpListenerContext clientContext,
            long clientStartOffset,
            Speed maxSpeed
        )
        {
            _pkgManager.Put(
                $"Beginning HIT transfer {resource.DownloadUri} to {clientContext.Request.UserHostName} (offset {clientStartOffset})",
                LogLevel.Debug
            );
            var ifModifiedSince = clientContext.Request.Headers.Get("If-Modified-Since");
            if (ifModifiedSince is not null)
            {
                var date = DateTime.Parse(
                    ifModifiedSince,
                    CultureInfo.InvariantCulture.DateTimeFormat
                );
                if (resource.FullFile.LastWriteTimeUtc <= date) // the client already has the latest version
                {
                    clientContext.Response.StatusCode = (int)HttpStatusCode.NotModified;
                    _pkgManager.Put(
                        $"{resource.DownloadUri} not modified since {date}",
                        LogLevel.Debug
                    );
                    return;
                }
            }

            clientContext.Response.AddHeader("X-Cache", "HIT");

            using (var fileStream = resource.FullFile.OpenRead())
            {
                using (var throttledStream = new ThrottledStream(fileStream, readSpeed: maxSpeed))
                {
                    if (clientStartOffset > 0)
                    {
                        fileStream.Seek(clientStartOffset, SeekOrigin.Begin);
                        clientContext.Response.StatusCode = (int)HttpStatusCode.PartialContent;
                        clientContext.Response.AddHeader(
                            "Content-Range",
                            $"bytes {clientStartOffset}-{fileStream.Length - 1}/{fileStream.Length}"
                        );
                    }

                    clientContext.Response.ContentLength64 = fileStream.Length - clientStartOffset;
                    await throttledStream
                        .CopyToAsync(clientContext.Response.OutputStream, _config.BufferSize)
                        .ConfigureAwait(false);
                }
            }
        }

        private async Task UpstreamFetchAndSaveAsync(
            CacheableResource resource,
            TaskCompletionSource sync,
            Speed maxSpeed,
            IProgress<TransferProgressInfo>? progress = null,
            CancellationToken ct = default
        )
        {
            try
            {
                // Dynamic resources such as db files change frequently and
                // can vary in length so we cannot use any partial content
                // before checking the freshness
                if (
                    !_config.ReusePartialDownloads
                    || !await resource.IsFreshAsync().ConfigureAwait(false)
                )
                {
                    resource.DeleteAll();
                }

                long offset = 0;
                if (_config.ReusePartialDownloads && resource.PartialFile.Exists)
                {
                    offset = resource.PartialFile.Length;
                }

                using (var hashAlgorithm = SHA256.Create())
                {
                    using (
                        var cryptoStream = new CryptoStream(
                            Stream.Null,
                            hashAlgorithm,
                            CryptoStreamMode.Write
                        )
                    )
                    {
                        using (
                            HttpResponseMessage response = await GetResponseAsync(resource, offset)
                                .ConfigureAwait(false)
                        )
                        {
                            using (
                                var responseStream = await response.Content
                                    .ReadAsStreamAsync(ct)
                                    .ConfigureAwait(false)
                            )
                            {
                                using (
                                    var throttledStream = new ThrottledStream(
                                        responseStream,
                                        readSpeed: maxSpeed
                                    )
                                )
                                {
                                    if (response.Content.Headers.ContentLength is not null)
                                    {
                                        _syncDic[resource].ContentLength =
                                            offset + response.Content.Headers.ContentLength.Value;
                                    }
                                    else
                                    {
                                        _syncDic[resource].ContentLength = -1; // no content length header
                                    }

                                    if (offset > 0 && !CheckTotalSize(response, offset))
                                    {
                                        // somehow a resource that is static has changed
                                        // this should not happen but better be safe than sorry
                                        resource.DeleteAll();
                                        await UpstreamFetchAndSaveAsync(
                                                resource,
                                                sync,
                                                maxSpeed,
                                                progress,
                                                ct
                                            )
                                            .ConfigureAwait(false);
                                        return;
                                    }

                                    _syncDic[resource].ThrottledStream = throttledStream;

                                    using (
                                        var fileStream =
                                            resource.PartialFile.OpenReadWriteOrCreate()
                                    )
                                    {
                                        // we inform other clients that the connection is established
                                        // and they can start reading the file
                                        // better to do it asap since pacman does not like delays
                                        sync.SetResult();
                                        // we read the eventual existing data for the hash computation
                                        await fileStream
                                            .CopyToAsync(cryptoStream, ct)
                                            .ConfigureAwait(false);

                                        _pkgManager.Put(
                                            $"Beginning upstream download {resource.DownloadUri} (offset {offset})",
                                            LogLevel.Debug
                                        );

                                        long totalBytes = offset;
                                        var transferProgress = new Progress<int>();

                                        if (_syncDic[resource].ContentLength > -1)
                                        {
                                            transferProgress.ProgressChanged += (_, readBytes) =>
                                                progress?.Report(
                                                    new TransferProgressInfo(
                                                        _syncDic[resource].ContentLength,
                                                        totalBytes += readBytes,
                                                        readBytes
                                                    )
                                                );
                                        }

                                        await throttledStream
                                            .CopyToMultiAsync(
                                                _config.BufferSize,
                                                transferProgress,
                                                ct,
                                                _config.NetworkTimeoutMs,
                                                new[] { fileStream, cryptoStream }
                                            )
                                            .ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                    }

                    // we verify the integrity of the data before finalizing it
                    if (
                        hashAlgorithm.Hash is null
                        || !await resource
                            .QueryIntegrityAsync(
                                resource.PartialFile.Length,
                                new Checksum(ChecksumType.SHA256, hashAlgorithm.Hash)
                            )
                            .ConfigureAwait(false)
                    )
                    {
                        // resource is corrupt and must be deleted
                        _pkgManager.Put(
                            $"{resource.FullFile.Name} has failed the integrity check",
                            LogLevel.Error
                        );
                        resource.DeleteAll();
                    }

                    if (resource.PartialFile.Exists)
                    {
                        // it should be okay to move the file
                        // even if it's being read by clients
                        // since we are just renaming it the
                        // inode should stay the same
                        resource.Complete();
                    }
                }
            }
            catch (WebException ex)
                when (ex.Response is HttpWebResponse httpResponse
                    && httpResponse.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable
                )
            {
                // most likely our local partial file is corrupt
                // this can happen if the task was cancelled abruptly, just before finalizing the download
                // so we delete it and try again from scratch
                resource.DeleteAll();
                await UpstreamFetchAndSaveAsync(resource, sync, maxSpeed, progress, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                sync.TrySetException(ex);
                throw;
            }
        }
    }
}
