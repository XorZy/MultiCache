namespace MultiCache.Resource
{
    using MultiCache.Models;
    using MultiCache.PackageManager;
    using MultiCache.Resource.Interfaces;
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    public record CacheableResource : NetworkResource
    {
        public IResourceHandle FullFile => PackageManager.PackageStorage.GetFullHandle(this);
        public IResourceHandle PartialFile => PackageManager.PackageStorage.GetPartialHandle(this);
        protected PackageManagerBase PackageManager { get; }

        public CacheableResource(Uri uri, bool isDynamic, PackageManagerBase pkgManager)
            : base(uri, isDynamic)
        {
            PackageManager = pkgManager;
        }

        public bool TryLock() => PackageManager.PackageStorage.TryLockResource(this);
        public void Release() => PackageManager.PackageStorage.ReleaseResource(this);
        public bool IsLocked() => PackageManager.PackageStorage.IsResourceLocked(this);

        public void Complete()
        {
            if (PartialFile.Exists)
            {
                PartialFile.MoveTo(FullFile);
            }
        }

        public virtual void DeleteAll()
        {
            FullFile.Delete();
            PartialFile.Delete();
        }

        public virtual Task<bool> QueryIntegrityAsync(long size, Checksum checksum)
        {
            // the default is true
            return Task.FromResult(true);
        }

        public async Task<bool> IsFreshAndComplete()
        {
            if (!FullFile.Exists)
            {
                return false;
            }

            return await IsFreshAsync().ConfigureAwait(false);
        }

        public async Task<bool> IsFreshAsync()
        {
            // we assume that static resources are always fresh since they cannot change
            // this way we avoid making a request
            if (!IsDynamic)
            {
                return true;
            }

            var lastModified = FullFile.Exists
                ? FullFile.LastWriteTimeUtc
                : PartialFile.LastWriteTimeUtc;
            using (var request = new HttpRequestMessage(HttpMethod.Head, DownloadUri))
            {
                request.Headers.IfModifiedSince = lastModified;

                try
                {
                    var reply = await PackageManager.Config.HttpClient
                        .SendAsync(request)
                        .ConfigureAwait(false);
                    return reply.StatusCode == HttpStatusCode.NotModified;
                }
                catch (HttpRequestException ex)
                    when (ex.InnerException is SocketException sockex && sockex.ErrorCode == -131073
                    )
                {
                    PackageManager.Put(
                        "No connectivity, assuming resource is still fresh",
                        Helpers.LogLevel.Debug
                    );
                    // Name or service not know
                    // internet connectivity is probably down
                    // in that case it's better to assume the resource is fresh
                    // that way we may be able to deliver some resources already stored locally
                    return true;
                }
            }
        }
    }
}
