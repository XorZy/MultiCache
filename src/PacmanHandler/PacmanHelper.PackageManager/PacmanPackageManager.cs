namespace MultiCache.PackageManager.Pacman
{
    using Common.MultiCache.Models;
    using LibConsole.Interactive;
    using MultiCache.Config;
    using MultiCache.Helpers;
    using MultiCache.Models;
    using MultiCache.Models.Pacman;
    using MultiCache.Network;
    using MultiCache.Resource;
    using MultiCache.Storage;
    using PacmanHandler.Helpers;
    using PacmanHandler.Network;
    using PacmanHandler.Resource;
    using PacmanHandler.Storage;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    public class PacmanPackageManager : PackageManagerBase, ISupportedDistroProvider
    {
        public static readonly string[] DefaultRepositories = new[]
        {
            "core",
            "extra",
            "community",
            "multilib",
        };

        public PacmanPackageManager(RepositoryConfiguration config) : base(config)
        {
            PackageStorage = new PacmanFSStorage(this);
            MirrorProvider = new PacmanMirrorProvider(this);
            Factory = new PacmanResourceFactory(this);
        }

        internal PacmanResourceFactory Factory { get; }

        public override MirrorProviderBase MirrorProvider { get; }
        public override ResourceStorageProviderBase PackageStorage { get; protected set; }

        public override NetworkResource? GetBenchmarkResource(Mirror mirror)
        {
            return Config.DistroType switch
            {
                DistroType.ArchLinuxX64
                or DistroType.ManjaroX64Stable
                or DistroType.ManjaroX64Testing
                or DistroType.ManjaroX64Unstable
                  => Factory.BuildBenchmarkResource(
                      new PacmanResourceIdentifier(new Repository("core", "x86_64"), "core.db"),
                      mirror
                  ),
                DistroType.ManjaroAarch64Stable
                or DistroType.ManjaroAarch64Testing
                or DistroType.ManjaroAarch64Unstable
                or DistroType.ArchLinuxArm
                  => Factory.BuildBenchmarkResource(
                      new PacmanResourceIdentifier(new Repository("core", "aarch64"), "core.db"),
                      mirror
                  ),
                _ => null
            };
        }

        public async IAsyncEnumerable<PackageInfo> EnumerateRepositoryPackagesAsync(
            Repository repository,
            bool fetchUpstream
        )
        {
            var db = Factory.BuildDbResource(repository);
            if (fetchUpstream)
            {
                await FailureHelper
                    .TryAsync(
                        async () => await Network.FetchResourceAsync(db).ConfigureAwait(false),
                        (ex) =>
                        {
                            // corrupt db file?
                            if (ex is InvalidDataException)
                            {
                                db.DeleteAll();
                            }
                        },
                        retries: Config.AppConfiguration.RetryCount,
                        retryDelayMs: Config.AppConfiguration.RetryDelayMs
                    )
                    .ConfigureAwait(false);
            }

            if (db.FullFile.Exists)
            {
                await foreach (
                    var pkg in new PacmanDbHelper(db.FullFile, repository)
                        .EnumeratePackageInfosAsync()
                        .ConfigureAwait(false)
                )
                {
                    yield return pkg;
                }
            }
        }

        protected override NetworkResource GetResource(Uri request) => Factory.GetResource(request);
        public override async Task<NetworkResource> HandleRequestAsync(
            Uri request,
            HttpListenerContext incomingContext
        )
        {
            var resource = await base
                .HandleRequestAsync(request, incomingContext)
                .ConfigureAwait(false);
            if (resource is PacmanPackageResource pk)
            {
                pk.UpdateLastAccessDate();
            }
            return resource;
        }

        public override async Task SeedPackagesAsync(
            string architecture,
            IEnumerable<string> clientPackages
        )
        {
            var hashedPackageInfos = (
                await TryGetDefaultPackageInfosAsync(architecture).ConfigureAwait(false)
            ).ToDictionary(x => x.Name, x => x);

            foreach (var packageName in clientPackages)
            {
                if (hashedPackageInfos.TryGetValue(packageName, out var packageInfo))
                {
                    new PacmanPackageResource(
                        PacmanResourceIdentifier.FromPackageInfo(packageInfo),
                        this
                    ).Register();
                }
            }
        }
        public static IReadOnlyList<DistroType> SupportedDistros { get; } =
            new[]
            {
                DistroType.ManjaroX64Stable,
                DistroType.ManjaroX64Testing,
                DistroType.ManjaroX64Unstable,
                DistroType.ManjaroAarch64Stable,
                DistroType.ManjaroAarch64Testing,
                DistroType.ManjaroAarch64Unstable,
                DistroType.ArchLinuxX64,
                DistroType.ArchLinuxArm,
                DistroType.Generic,
            }.ToImmutableArray();

        protected override async Task MaintainPackageAsync(
            PackageInfo remotePackageInfo,
            IList<PackageResourceBase> localVersions
        )
        {
            var newPackageResource = Factory.BuildPackageResource(
                PacmanResourceIdentifier.FromPackageInfo(remotePackageInfo)
            );

            if (newPackageResource.IsStale(remotePackageInfo.BuildDate))
            {
                // the package has not been downloaded by a client in a while so we get rid of all stored versions
                Put($"Package {remotePackageInfo.Name} is stale, deleting...", LogLevel.Info);
                newPackageResource.PurgeAllVersions();
            }

            var latestLocalVersion = localVersions.LastOrDefault();

            if (
                latestLocalVersion is not null
                && (remotePackageInfo.Version as PacmanPackageVersion)
                    < (latestLocalVersion.Package.Version as PacmanPackageVersion)
            )
            {
                // upstream mirror must be out of sync
                Put(
                    $"Remote package {remotePackageInfo.Name} is older than the local version",
                    LogLevel.Info
                );
                return;
            }

            if (newPackageResource.IsLocked()) // the file is already being downloaded so we skip it
            {
                return;
            }

            await TryGetSignature(newPackageResource, remotePackageInfo).ConfigureAwait(false);

            if (
                !await EnsurePackageIntegrityAsync(remotePackageInfo, newPackageResource)
                    .ConfigureAwait(false)
            )
            {
                if (latestLocalVersion is null)
                {
                    Put(
                        $"Downloading {remotePackageInfo.Name} v{remotePackageInfo.Version}",
                        LogLevel.Debug
                    );
                }
                else
                {
                    if (latestLocalVersion.Package.Version == remotePackageInfo.Version)
                    {
                        Put(
                            $"Restoring {remotePackageInfo.Name} v{remotePackageInfo.Version}",
                            LogLevel.Debug
                        );
                    }
                    else
                    {
                        Put(
                            $"Updating {remotePackageInfo.Name} v{latestLocalVersion.Package.Version}->v{remotePackageInfo.Version}",
                            LogLevel.Debug
                        );
                    }
                }

                await FailureHelper
                    .TryAsync(
                        async () =>
                        {
                            await ConsoleUtils
                                .DownloadProgressAsync(
                                    remotePackageInfo.Name,
                                    (consoleTaskProgress, ct) =>
                                    {
                                        var dlProgress = new Progress<TransferProgressInfo>();

                                        dlProgress.ProgressChanged += (s, e) =>
                                            consoleTaskProgress.Report(
                                                (e.TotalDownloadedBytes, e.NewBytes)
                                            );
                                        return Network.FetchResourceAsync(
                                            newPackageResource,
                                            progress: dlProgress
                                        );
                                    },
                                    remotePackageInfo.CompressedSize
                                )
                                .ConfigureAwait(false);

                            if (!newPackageResource.FullFile.Exists)
                            {
                                throw new InvalidDataException("Integrity check failed!");
                            }
                        },
                        throwException: false,
                        retries: Config.AppConfiguration.RetryCount,
                        retryDelayMs: Config.AppConfiguration.RetryDelayMs
                    ) // we don't throw so we can continue with the other packages
                    .ConfigureAwait(false);
            }
        }

        protected override async Task<IList<PackageInfo>> TryGetRepositoryPackagesAsync(
            Repository repository
        )
        {
            var db = Factory.BuildDbResource(repository);
            return await FailureHelper
                .TryAsync(
                    async () =>
                    {
                        await Network.FetchResourceAsync(db).ConfigureAwait(false);
                        return await new PacmanDbHelper(db.FullFile, repository)
                            .GetPackagesAsync()
                            .ConfigureAwait(false);
                    },
                    onFailure: (ex) =>
                    {
                        if (ex is InvalidDataException) // corrupt db file?
                        {
                            db.DeleteAll();
                        }
                    },
                    doNotTryAgainWhen: (ex) =>
                        (
                            ex is WebException wEx
                            && wEx.Response is HttpWebResponse hRep
                            && hRep.StatusCode == HttpStatusCode.NotFound
                        ),
                    retries: Config.AppConfiguration.RetryCount,
                    retryDelayMs: Config.AppConfiguration.RetryDelayMs
                )
                .ConfigureAwait(false);
        }

        private async Task<IEnumerable<PackageInfo>> TryGetDefaultPackageInfosAsync(
            string architecture
        )
        {
            var output = new List<PackageInfo>();
            foreach (
                var repository in DefaultRepositories.Select(x => new Repository(x, architecture))
            )
            {
                try
                {
                    output.AddRange(
                        await TryGetRepositoryPackagesAsync(repository).ConfigureAwait(false)
                    );
                }
                catch (WebException ex)
                    when (ex.Response is HttpWebResponse hRep
                        && hRep.StatusCode == HttpStatusCode.NotFound
                    )
                {
                    // the repository might not be available on this mirror
                }
            }

            return output;
        }

        private async Task TryGetSignature(PacmanPackageResource package, PackageInfo info)
        {
            if (!package.Signature.FullFile.Exists)
            {
                // we first try locally it might save us a request
                if (info.Signature is not null)
                {
                    using (var stream = package.Signature.FullFile.OpenReadWriteOrCreate())
                    {
                        await stream.WriteAsync(info.Signature.Value).ConfigureAwait(false);
                    }
                }
                else
                {
                    try
                    {
                        Put(
                            $"Downloading signature for {package.Package.FileName}",
                            LogLevel.Debug
                        );
                        await Network.FetchResourceAsync(package.Signature).ConfigureAwait(false);
                    }
                    catch (WebException wEx)
                        when (wEx.Response is HttpWebResponse hRes
                            && hRes.StatusCode == HttpStatusCode.NotFound
                        )
                    { }
                }
            }
        }
    }
}
