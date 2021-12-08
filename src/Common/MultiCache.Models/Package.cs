namespace MultiCache.Models
{
    public class Package : PackageIdentifier
    {
        public Package(
            Repository repository,
            string name,
            string architecture,
            string fileName,
            PackageVersion version
        ) : base(name, architecture)
        {
            Repository = repository;
            Name = name;
            Architecture = architecture;
            FileName = fileName;
            Version = version;
        }

        public string FileName { get; init; }
        public Repository Repository { get; init; }
        public PackageVersion Version { get; init; }

        public override string ToString() => Name + "-" + Version + "-" + Architecture;
    }
}
