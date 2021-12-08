using System;
using MultiCache.PackageManager;
using MultiCache.Resource;

namespace PacmanHandler.Resource
{
    internal record PacmanCacheableResource : CacheableResource
    {
        public PacmanResourceIdentifier RID { get; }
        public PacmanCacheableResource(
            Uri uri,
            bool isDynamic,
            PackageManagerBase pkgManager,
            PacmanResourceIdentifier rid
        ) : base(uri, isDynamic, pkgManager)
        {
            RID = rid;
        }
    }
}
