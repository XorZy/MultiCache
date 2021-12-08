namespace MultiCache.Resource
{
    using MultiCache.Models;
    using MultiCache.PackageManager;
    using MultiCache.Resource.Interfaces;
    using MultiCache.Utils;
    using System;
    using System.Threading.Tasks;

    public abstract record PackageResourceBase : CacheableResource
    {
        protected PackageResourceBase(Uri uri, PackageManagerBase pkgManager)
            : base(uri, false, pkgManager) { }

        public IResourceHandle DateFile => PackageManager.PackageStorage.GetDateFileHandle(this);
        public abstract Package Package { get; }
        public void UpdateLastAccessDate()
        {
            // you could in theory use the LastAccessTimeUtc attribute  but
            // if for some reason the underlying fs does not support it
            // it could be problematic so let's just rely on good old ascii
            using (var writer = new StreamWriter(DateFile.OpenReadWriteOrCreate()))
            {
                writer.Write(DateTime.UtcNow.ToBinary().ToStringInvariant());
            }
        }

        public void DeleteLastAccessDate()
        {
            if (DateFile.Exists)
            {
                DateFile.Delete();
            }
        }

        public DateTime? LastAccessDate
        {
            get
            {
                if (!DateFile.Exists)
                    return null;
                using (var reader = new StreamReader(DateFile.OpenRead()))
                {
                    return DateTime.FromBinary(reader.ReadToEnd().ToLongInvariant());
                }
            }
        }

        public bool IsStale(DateTime buildDate) =>
            PackageManager.Config.PackageStalenessDelayDays is not null
            && LastAccessDate is not null
            && buildDate > LastAccessDate // the package has been updated since the last access
            && (DateTime.Now - buildDate)
                >= TimeSpan.FromDays(PackageManager.Config.PackageStalenessDelayDays.Value);

        public abstract Task<PackageInfo?> GetPackageInfoAsync(bool askUpstream);

        public void Register() => PackageManager.PackageStorage.RegisterPackage(Package);

        public override void DeleteAll() =>
            PackageManager.PackageStorage.DeletePackageVersion(Package);

        public void PurgeAllVersions() =>
            PackageManager.PackageStorage.PurgeAllPackageVersions(Package);
    }
}
