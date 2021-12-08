namespace PacmanHandler.Resource
{
    using MultiCache.Helpers;
    using MultiCache.Models;
    using MultiCache.PackageManager.Pacman;
    using MultiCache.Resource;
    using System.Threading.Tasks;

    internal record PacmanPackageResource : PackageResourceBase
    {
        public PacmanPackageResource(PacmanResourceIdentifier rid, PacmanPackageManager pkgManager)
            : base(rid.ToUri(pkgManager.Config.Mirrors[0]), pkgManager)
        {
            RID = rid;
            Package = rid.ToPackage();
        }

        public PacmanPackageSignatureResource Signature =>
            new PacmanPackageSignatureResource(
                RID with
                {
                    FileName = RID.FileName + ".sig"
                },
                PackageManager as PacmanPackageManager
            );

        protected PacmanResourceIdentifier RID { get; }

        public override Package Package { get; }

        public override async Task<PackageInfo?> GetPackageInfoAsync(bool askUpstream)
        {
            await foreach (
                var packageInfo in (
                    PackageManager as PacmanPackageManager
                ).EnumerateRepositoryPackagesAsync(RID.Repository, askUpstream)
            )
            {
                if (packageInfo.Name == Package.Name && packageInfo.Version == Package.Version)
                {
                    return packageInfo;
                }
            }

            return null;
        }

        public override async Task<bool> QueryIntegrityAsync(long size, Checksum checksum)
        {
            // we first try without checking the upstream server
            var upstreamPackage = await GetPackageInfoAsync(false).ConfigureAwait(false);
            if (upstreamPackage is not null)
            {
                return upstreamPackage.CompressedSize == size
                    && upstreamPackage.Checksum == checksum;
            }

            // we try once more to see if an up to date version of the database is available
            upstreamPackage = await GetPackageInfoAsync(true).ConfigureAwait(false);

            if (upstreamPackage is not null)
            {
                return upstreamPackage.CompressedSize == size
                    && upstreamPackage.Checksum == checksum;
            }
            else
            {
                PackageManager.Put(
                    $"Unable to find package info for {Package.Name}",
                    LogLevel.Warning
                );
            }

            return true; // we assume true in case we can't find the checksum
        }
    }
}
