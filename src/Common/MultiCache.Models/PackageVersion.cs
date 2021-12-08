namespace MultiCache.Models
{
    public record PackageVersion
    {
        public string VersionString { get; init; }

        public PackageVersion(string version)
        {
            VersionString = version;
        }

        public override string ToString()
        {
            return VersionString;
        }
    }
}
