namespace MultiCache.PackageManager
{
    using LibConsole.Interactive;
    using MultiCache.Api;
    using MultiCache.Config;
    using MultiCache.Helpers;
    using MultiCache.Models;
    using MultiCache.Network;
    using MultiCache.Resource;
    using MultiCache.Storage;
    using System.Collections.Concurrent;
    using System.Data;
    using System.Diagnostics;
    using System.Net;

    public abstract class PackageManagerBase
    {
        private int _maintainanceLock;

        protected PackageManagerBase(RepositoryConfiguration configuration)
        {
            Config = configuration;
            Network = new NetworkHelper(this);
            if (Config.AllowApi)
            {
                Api = new ApiProvider(this);
            }
        }

        public ApiProvider? Api { get; }
        public RepositoryConfiguration Config { get; }

        public abstract MirrorProviderBase MirrorProvider { get; }
        public NetworkHelper Network { get; }
        public abstract ResourceStorageProviderBase PackageStorage { get; protected set; }

        private FileInfo MirrorFile =>
            new FileInfo(Path.Combine(Config.CachePath.FullName, ".mirrorlist"));

        public async Task<MirrorList> GetSavedMirrorsAsync()
        {
            if (!MirrorFile.Exists)
            {
                return new MirrorList(DateTime.UtcNow, Config.DistroType, Config.OnlyHttpsMirrors);
            }

            return await MirrorList
                .LoadAsync(MirrorFile, Config.OnlyHttpsMirrors)
                .ConfigureAwait(false);
        }

        public virtual async Task<NetworkResource> HandleRequestAsync(
            Uri request,
            HttpListenerContext incomingContext
        )
        {
            var resource = GetResource(request);

            await Network.FetchResourceAsync(resource, incomingContext).ConfigureAwait(false);

            return resource;
        }

        public abstract NetworkResource? GetBenchmarkResource(Mirror mirror);

        protected abstract NetworkResource GetResource(Uri request);
        public virtual async Task MaintainAsync()
        {
            if (Interlocked.Exchange(ref _maintainanceLock, 1) == 1)
            {
                // we don't want two maintenances running at the same time
                return;
            }

            try
            {
                Put("Starting maintenance", LogLevel.Info);

                // we check mirrors regularly
                await MirrorRanker.RankAndAssignMirrorsAsync(this).ConfigureAwait(false);

                var hashedUpstreamPackageInfos = (
                    await ConsoleUtils
                        .SpinAsync("Updating package databases", TryGetPackageInfosAsync)
                        .ConfigureAwait(false)
                )
                    .GroupBy(x => x.Name)
                    .ToDictionary(x => x.Key, x => x.ToArray());

                var dependencyRoots = new ConcurrentBag<PackageInfo>();

                await Parallel
                    .ForEachAsync(
                        PackageStorage.GetStoredPackages(),
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Config.MaintenanceMaxThreads,
                        },
                        async (localInfo, _) =>
                        {
                            var (packageID, localVersions) = localInfo;

                            if (
                                hashedUpstreamPackageInfos.TryGetValue(
                                    packageID.Name,
                                    out var remotePackageInfos
                                )
                            )
                            {
                                foreach (var remotePackageInfo in remotePackageInfos)
                                {
                                    await MaintainPackageAsync(remotePackageInfo, localVersions)
                                        .ConfigureAwait(false);
                                    dependencyRoots.Add(remotePackageInfo);
                                    Cleanup(localVersions, remotePackageInfo.Version);
                                }
                            }
                            else
                            {
                                Put(
                                    $"Package {packageID.Name} was not found in the database",
                                    LogLevel.Warning
                                );

                                if (Config.FetchReplacements)
                                {
                                    foreach (
                                        var replacement in hashedUpstreamPackageInfos.Values
                                            .SelectMany(x => x)
                                            .Where(x => x.Replaces.Contains(packageID.Name))
                                    )
                                    {
                                        Put(
                                            $"{replacement.Name} replaces {packageID.Name}",
                                            LogLevel.Warning
                                        );
                                        await MaintainPackageAsync(replacement, localVersions)
                                            .ConfigureAwait(false);
                                        dependencyRoots.Add(replacement);
                                    }
                                }
                            }
                        }
                    )
                    .ConfigureAwait(false);

                if (Config.FetchDependencies)
                {
                    await MaintainDependenciesAsync(hashedUpstreamPackageInfos, dependencyRoots)
                        .ConfigureAwait(false);
                }

                Put("Maintenance complete", LogLevel.Info);
            }
            finally
            {
                // release the lock
                Interlocked.Exchange(ref _maintainanceLock, 0);
            }
        }

        public void Put(string message, LogLevel level) =>
            Log.Put($"[[{Config.Prefix}]] {message}", level);

        public void Put(Exception ex) => Log.Put($"[[{Config.Prefix}]] {ex}", LogLevel.Error);

        public async Task SaveMirrorListAsync(MirrorList list)
        {
            await list.SaveAsync(MirrorFile).ConfigureAwait(false);
        }

        public abstract Task SeedPackagesAsync(
            string architecture,
            IEnumerable<string> clientPackages
        );

        protected static IEnumerable<PackageInfo> ResolveDependenciesRecursive(
            IDictionary<string, PackageInfo[]> hashedInfos,
            IEnumerable<PackageInfo> rootPackages
        )
        {
            var sw = new Stopwatch();
            sw.Start();
            var allDependencies = rootPackages.SelectMany(x => x.Dependencies).ToHashSet();
            var resolvedDependencies = new HashSet<PackageInfo>();
            var stack = new Stack<string>(allDependencies);

            while (stack.Count > 0)
            {
                var dependencyName = stack.Pop();

                if (hashedInfos.TryGetValue(dependencyName, out var infos))
                {
                    foreach (var depInfo in infos)
                    {
                        if (!resolvedDependencies.Add(depInfo))
                        {
                            foreach (var dep2 in depInfo.Dependencies)
                            {
                                stack.Push(dep2);
                            }
                        }
                    }
                }
            }
            sw.Stop();
            Log.Put($"Depency resolution took {sw.ElapsedMilliseconds}ms", LogLevel.Debug);
            return resolvedDependencies;
        }

        protected async Task<bool> CheckPackageIntegrityAsync(
            PackageInfo packageInfo,
            PackageResourceBase resource
        )
        {
            Put($"Checking the integrity of {packageInfo.Name}", LogLevel.Debug);
            if (!resource.FullFile.Exists || resource.FullFile.Length != packageInfo.CompressedSize)
            {
                return false;
            }

            if (!Config.ChecksumIntegrityCheck)
            {
                return true;
            }

            using (var hashAlgo = packageInfo.Checksum.CreateHashAlgorithm())
            {
                using (var fileStream = resource.FullFile.OpenRead())
                {
                    var checksum = new Checksum(
                        packageInfo.Checksum.Type,
                        await hashAlgo.ComputeHashAsync(fileStream).ConfigureAwait(false)
                    );
                    return packageInfo.Checksum == checksum;
                }
            }
        }

        protected virtual void Cleanup(
            IEnumerable<PackageResourceBase> localVersions,
            PackageVersion newVersion
        )
        {
            foreach (var version in localVersions)
            {
                // just as a precaution
                if (version.FullFile.Exists && version.PartialFile.Exists)
                {
                    version.PartialFile.Delete();
                }
            }

            if (!Config.KeepOldPackages)
            {
                // we get rid of old package versions and unnecessary files
                foreach (
                    var packageResource in localVersions.Where(x => x.Package.Version != newVersion)
                )
                {
                    Put($"Deleting {packageResource.Package}", LogLevel.Debug);
                    packageResource.DeleteAll();
                }
            }
        }

        protected async Task<bool> EnsurePackageIntegrityAsync(
            PackageInfo package,
            PackageResourceBase resource
        )
        {
            if (!await CheckPackageIntegrityAsync(package, resource).ConfigureAwait(false))
            {
                Put($"Package {package} failed integrity check", LogLevel.Error);
                if (resource.FullFile.Exists)
                {
                    resource.FullFile.Delete();
                }

                return false;
            }

            return true;
        }

        protected abstract Task MaintainPackageAsync(
            PackageInfo remotePackageInfo,
            IList<PackageResourceBase> localVersions
        );

        protected abstract Task<IList<PackageInfo>> TryGetRepositoryPackagesAsync(
            Repository repository
        );

        private async Task MaintainDependenciesAsync(
            IDictionary<string, PackageInfo[]> hashedupstreamPackageInfos,
            IEnumerable<PackageInfo> rootPackages
        )
        {
            Put("Now handling dependencies", LogLevel.Info);
            var allDependencies = ResolveDependenciesRecursive(
                hashedupstreamPackageInfos,
                rootPackages
            );

            await Parallel
                .ForEachAsync(
                    allDependencies,
                    new ParallelOptions { MaxDegreeOfParallelism = Config.MaintenanceMaxThreads },
                    async (dependency, _) =>
                        await MaintainPackageAsync(dependency, Array.Empty<PackageResourceBase>())
                            .ConfigureAwait(false)
                )
                .ConfigureAwait(false);
        }

        private async Task<IEnumerable<PackageInfo>> TryGetPackageInfosAsync()
        {
            var output = new List<PackageInfo>();
            foreach (var repository in PackageStorage.EnumerateLocalRepositories())
            {
                output.AddRange(
                    await TryGetRepositoryPackagesAsync(repository).ConfigureAwait(false)
                );
            }
            return output;
        }
    }
}
