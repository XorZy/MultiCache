namespace MultiCache.Models
{
    using MultiCache.Resource;

    public record StoredPackageInformation(
        PackageIdentifier Package,
        IList<PackageResourceBase> StoredVersions
    );
}
