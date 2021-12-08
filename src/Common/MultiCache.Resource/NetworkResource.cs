namespace MultiCache.Resource
{
    using System;

    public record NetworkResource
    {
        public bool IsDynamic { get; set; }

        public Uri DownloadUri { get; }

        public NetworkResource(Uri uri, bool isDynamic)
        {
            DownloadUri = uri;
            IsDynamic = isDynamic;
        }
    }
}
