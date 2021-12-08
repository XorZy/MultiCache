namespace PacmanHandler.Storage
{
    using MultiCache.Models;
    using MultiCache.Models.Pacman;
    using MultiCache.PackageManager;
    using MultiCache.PackageManager.Pacman;
    using MultiCache.Resource;
    using MultiCache.Resource.Interfaces;
    using MultiCache.Storage;
    using PacmanHandler.Resource;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides a filesystem-based storage backend for pacman
    /// </summary>
    internal class PacmanFSStorage : FSStorageProviderBase
    {
        public PacmanFSStorage(PackageManagerBase pkgManager) : base(pkgManager) { }
        public override IEnumerable<Repository> EnumerateLocalRepositories()
        {
            var output = new List<Repository>();
            foreach (var repository in _rootDir.EnumerateDirectories())
            {
                foreach (var architecture in repository.EnumerateDirectories())
                {
                    output.Add(new Repository(repository.Name, architecture.Name));
                }
            }

            return output;
        }

        public override IResourceHandle GetDateFileHandle(PackageResourceBase resource)
        {
            return new FileResourceHandle(
                new FileInfo(Path.Combine(GetPackagePath(resource.Package), "date"))
            );
        }

        public override IEnumerable<StoredPackageInformation> GetStoredPackages()
        {
            var output = new List<StoredPackageInformation>();
            foreach (var repositoryDir in _rootDir.EnumerateDirectories())
            {
                foreach (var repositoryArchitectureDir in repositoryDir.EnumerateDirectories())
                {
                    foreach (var package in repositoryArchitectureDir.EnumerateDirectories())
                    {
                        var packageBase = new PackageIdentifier(
                            package.Name,
                            repositoryArchitectureDir.Name
                        );
                        var versions = new List<PackageResourceBase>();
                        foreach (var version in package.EnumerateDirectories())
                        {
                            foreach (var file in version.EnumerateFiles("*.pkg.tar*"))
                            {
                                var cleanupRegex = new Regex(
                                    @"(\.partial|\.sig)$",
                                    RegexOptions.Compiled
                                );
                                var cleanedFileName = cleanupRegex.Replace(file.Name, string.Empty);
                                var repository = new Repository(
                                    repositoryDir.Name,
                                    repositoryArchitectureDir.Name
                                );
                                var resourceIdentifier = new PacmanResourceIdentifier(
                                    repository,
                                    cleanedFileName
                                );
                                versions.Add(
                                    (
                                        PackageManager as PacmanPackageManager
                                    ).Factory.BuildPackageResource(resourceIdentifier)
                                );
                                break; // we only need one file to get the proper file name
                            }
                        }

                        versions = versions
                            .OrderBy(x => x.Package.Version as PacmanPackageVersion)
                            .ToList(); // we sort versions in ascending order
                        output.Add(new StoredPackageInformation(packageBase, versions));
                    }
                }
            }

            return output;
        }

        protected override FileInfo GetFileInfo(CacheableResource resource, bool partialFile)
        {
            FileInfo output;

            if (resource is PacmanPackageResource pkg)
            {
                output = new FileInfo(
                    Path.Combine(GetPackageVersionPath(pkg.Package), pkg.Package.FileName)
                );
            }
            else if (resource is PacmanCacheableResource pcr)
            {
                output = new FileInfo(Path.Combine(GetRepositoryPath(pcr.RID), pcr.RID.FileName));
            }
            else
            {
                throw new NotSupportedException("Unsupported resource type");
            }

            if (partialFile)
            {
                output = new FileInfo(output.FullName + ".partial");
            }

            return output;
        }

        protected override string GetPackagePath(Package package) =>
            Path.Combine(
                GetRepositoryPath(
                    new PacmanResourceIdentifier(package.Repository, package.FileName)
                ),
                package.Name
            );

        protected override string GetPackageVersionPath(Package package) =>
            Path.Combine(GetPackagePath(package), package.Version.VersionString);

        private string GetRepositoryPath(PacmanResourceIdentifier rid) =>
            Path.Combine(_rootDir.FullName, rid.Repository.Name, rid.Repository.Architecture);
    }
}
