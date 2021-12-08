namespace MultiCache.Models
{
    public class PackageIdentifier
    {
        public PackageIdentifier(string name, string architecture)
        {
            Name = name;
            Architecture = architecture;
        }

        public string Architecture { get; init; }
        public string Name { get; init; }
    }
}
