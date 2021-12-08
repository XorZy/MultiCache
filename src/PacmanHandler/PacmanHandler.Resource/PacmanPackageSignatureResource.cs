namespace PacmanHandler.Resource
{
    using MultiCache.Models;
    using MultiCache.PackageManager.Pacman;
    using MultiCache.Utils;
    using System;
    using System.Threading.Tasks;

    internal record PacmanPackageSignatureResource : PacmanPackageResource
    {
        public PacmanPackageSignatureResource(
            PacmanResourceIdentifier rid,
            PacmanPackageManager pkgManager
        ) : base(rid, pkgManager)
        {
            if (!rid.FileName.EndsWithInvariant(".sig"))
            {
                throw new ArgumentException("Not a valid signature resource");
            }
        }

        public override Task<bool> QueryIntegrityAsync(long size, Checksum checksum)
        {
            // there is no way to check the integrity of a signature file
            // so we only check that it's not empty
            return Task.FromResult(size > 0);
        }
    }
}
