namespace PacmanHandler.Resource
{
    using MultiCache.Models;
    using MultiCache.Models.Pacman;
    using MultiCache.Network;
    using MultiCache.Utils;
    using System;

    /// <summary>
    /// Provides an abstract way to uniquely identify a pacman resource regardless of a mirror uri scheme
    /// </summary>
    internal record PacmanResourceIdentifier
    {
        public PacmanResourceIdentifier(Repository repository, string fileName)
        {
            Repository = repository;
            FileName = fileName;
        }

        public Repository Repository { get; init; }

        public string FileName { get; init; }

        public static PacmanResourceIdentifier FromLocalUri(Uri request)
        {
            var split = request.OriginalString.Split('/');
            return new PacmanResourceIdentifier(
                repository: new Repository(Name: split[^3], Architecture: split[^2]),
                fileName: split[^1]
            );
        }

        public static PacmanResourceIdentifier FromPackageInfo(PackageInfo info) =>
            new PacmanResourceIdentifier(info.Repository, info.FileName);

        public Uri ToUri(Mirror mirror)
        {
            return new Uri(
                mirror.RootUri.AbsoluteUri
                    .Replace("$repo", Repository.Name, StringComparison.OrdinalIgnoreCase)
                    .Replace("$arch", Repository.Architecture, StringComparison.OrdinalIgnoreCase)
            ).Combine(FileName);
        }

        public Package ToPackage()
        {
            var split = FileName.Split('-');
            var name = string.Join('-', split[0..(split.Length - 3)]);
            var version = new PacmanPackageVersion(string.Join('-', split[^3..^1]));
            var architecture = split[^1].Split('.')[0];
            return new Package(Repository, name, architecture, FileName, version);
        }
    }
}
