namespace MultiCache.PackageManager
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MultiCache.Config;
    using MultiCache.Models;
    using MultiCache.Network;
    using MultiCache.PackageManager.Pacman;
    public static class PackageManagerProvider
    {
        public static PackageManagerBase InstantiatePackageManager(RepositoryConfiguration config)
        {
            return config.PackageManagerType switch
            {
                PackageManagerType.Pacman => new PacmanPackageManager(config),
                _ => throw new NotImplementedException("Unimplemented package manager"),
            };
        }

        public static async Task<PackageManagerBase> SetupRepositoryAsync(
            RepositoryConfiguration config
        )
        {
            var pkgManager = InstantiatePackageManager(config);
            if (pkgManager.Config.DistroType != DistroType.Generic)
            {
                await MirrorRanker.RankAndAssignMirrorsAsync(pkgManager).ConfigureAwait(false);
            }
            return pkgManager;
        }

        public static IReadOnlyList<DistroType> GetSupportedDistributions(
            PackageManagerType packageManagerType
        )
        {
            return packageManagerType switch
            {
                PackageManagerType.Pacman => PacmanPackageManager.SupportedDistros,
                _ => throw new NotImplementedException()
            };
        }
    }
}
