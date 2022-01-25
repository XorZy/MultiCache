using System.Security.Cryptography.X509Certificates;

namespace MultiCache.Models
{
    public record PackageIdentifier
    {
        public PackageIdentifier(string name, string architecture, Repository repository)
        {
            Name = name;
            Architecture = architecture;
            Repository = repository;
        }

        public Repository Repository { get; init; }

        public string Architecture { get; init; }
        public string Name { get; init; }
    }
}
