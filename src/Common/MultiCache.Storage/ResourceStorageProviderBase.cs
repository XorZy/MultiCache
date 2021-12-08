namespace MultiCache.Storage
{
    using MultiCache.Models;
    using MultiCache.PackageManager;
    using MultiCache.Resource;
    using MultiCache.Resource.Interfaces;
    using System.Collections.Generic;

    public abstract class ResourceStorageProviderBase
    {
        private readonly HashSet<CacheableResource> _openedFiles = new();

        protected ResourceStorageProviderBase(PackageManagerBase pkgManager)
        {
            PackageManager = pkgManager;
        }

        protected PackageManagerBase PackageManager { get; }

        public abstract void DeletePackageVersion(Package package);

        public abstract IEnumerable<Repository> EnumerateLocalRepositories();

        public abstract IResourceHandle GetDateFileHandle(PackageResourceBase resource);

        public abstract IResourceHandle GetFullHandle(CacheableResource resource);

        public abstract IResourceHandle GetPartialHandle(CacheableResource resource);

        public abstract IEnumerable<StoredPackageInformation> GetStoredPackages();

        public virtual bool IsResourceLocked(CacheableResource resource)
        {
            lock (_openedFiles)
            {
                return _openedFiles.Contains(resource);
            }
        }

        public abstract void PurgeAllPackageVersions(Package package);

        public abstract void RegisterPackage(Package package);

        public virtual void ReleaseResource(CacheableResource resource)
        {
            lock (_openedFiles)
            {
                _openedFiles.Remove(resource);
            }
        }

        public virtual bool TryLockResource(CacheableResource resource)
        {
            lock (_openedFiles)
            {
                if (_openedFiles.Contains(resource))
                {
                    return false;
                }

                _openedFiles.Add(resource);
                return true;
            }
        }
    }
}
