namespace PacmanHandler.Resource
{
    using MultiCache.Models;
    using MultiCache.Network;
    using MultiCache.PackageManager.Pacman;
    using MultiCache.Resource;
    using MultiCache.Utils;
    using System;

    internal class PacmanResourceFactory
    {
        private readonly PacmanPackageManager _pkgManager;

        public PacmanResourceFactory(PacmanPackageManager pkgManager)
        {
            _pkgManager = pkgManager;
        }

        public NetworkResource BuildBenchmarkResource(PacmanResourceIdentifier rid, Mirror mirror)
        {
            return new NetworkResource(rid.ToUri(mirror), true);
        }

        public PacmanCacheableResource BuildDbResource(Repository repository)
        {
            var rid = new PacmanResourceIdentifier(repository, repository.Name + ".db");
            return BuildDynamicResource(rid);
        }

        public PacmanCacheableResource BuildDynamicResource(
            PacmanResourceIdentifier resourceIdentifier
        )
        {
            var uri = resourceIdentifier.ToUri(_pkgManager.Config.Mirrors[0]);
            return new PacmanCacheableResource(uri, true, _pkgManager, resourceIdentifier);
        }

        public NetworkResource BuildNetworkResource(PacmanResourceIdentifier resourceIdentifier)
        {
            var uri = resourceIdentifier.ToUri(_pkgManager.Config.Mirrors[0]);
            return new NetworkResource(uri, true);
        }

        public PacmanPackageResource BuildPackageResource(
            PacmanResourceIdentifier resourceIdentifier
        ) => new PacmanPackageResource(resourceIdentifier, _pkgManager);

        public PacmanPackageSignatureResource BuildPackageSignatureResource(
            PacmanResourceIdentifier resourceIdentifier
        ) => new PacmanPackageSignatureResource(resourceIdentifier, _pkgManager);

        public NetworkResource GetResource(Uri request)
        {
            var rid = PacmanResourceIdentifier.FromLocalUri(request);

            var requestString = request.OriginalString;
            if (IsDbResource(requestString))
            {
                return BuildDynamicResource(rid);
            }
            else if (IsPackageSigResource(requestString))
            {
                return BuildPackageSignatureResource(rid);
            }
            else if (IsPackageResource(requestString))
            {
                return BuildPackageResource(rid);
            }
            else
            {
                return BuildNetworkResource(rid); // not cacheable
            }
        }

        private static bool IsDbResource(string relativePath) =>
            relativePath.EndsWithInvariant(".db")
            || relativePath.EndsWithInvariant(".files")
            || relativePath.EndsWithInvariant(".db.sig")
            || relativePath.EndsWithInvariant(".files.sig");

        private static bool IsPackageResource(string relativePath)
        {
            return relativePath.Contains(".pkg.tar.", StringComparison.Ordinal);
        }

        private static bool IsPackageSigResource(string relativePath) =>
            relativePath.EndsWithInvariant(".sig");
    }
}
