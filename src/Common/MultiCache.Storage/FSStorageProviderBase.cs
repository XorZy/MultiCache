namespace MultiCache.Storage
{
    using MultiCache.Models;
    using MultiCache.PackageManager;
    using MultiCache.Resource;
    using MultiCache.Resource.Interfaces;
    using System.IO;

    public abstract class FSStorageProviderBase : ResourceStorageProviderBase
    {
        protected readonly DirectoryInfo _rootDir;

        protected FSStorageProviderBase(PackageManagerBase pkgManager) : base(pkgManager)
        {
            _rootDir = pkgManager.Config.CachePath;
            _rootDir.Create();
        }

        public override void DeletePackageVersion(Package package)
        {
            new DirectoryInfo(Path.Combine(GetPackageVersionPath(package))).Delete(true);
        }

        public override IResourceHandle GetFullHandle(CacheableResource resource) =>
            new FileResourceHandle(GetFileInfo(resource, false));

        public override IResourceHandle GetPartialHandle(CacheableResource resource) =>
            new FileResourceHandle(GetFileInfo(resource, true));

        public override void PurgeAllPackageVersions(Package package)
        {
            new DirectoryInfo(Path.Combine(GetPackagePath(package))).Delete(true);
        }

        public override void RegisterPackage(Package package)
        {
            new DirectoryInfo(GetPackagePath(package)).Create();
        }

        protected abstract FileInfo GetFileInfo(CacheableResource resource, bool partialFile);

        protected abstract string GetPackagePath(Package package);

        protected abstract string GetPackageVersionPath(Package package);
    }
}
