namespace MultiCache.Network
{
    using LibConsole.Interactive;
    using MultiCache.Helpers;
    using MultiCache.Models;
    using MultiCache.PackageManager;

    public static class MirrorRanker
    {
        public static async Task RankAndAssignMirrorsAsync(
            PackageManagerBase pkgManager,
            bool forceRefresh = false
        )
        {
            pkgManager.Put(
                "Trying to find the best mirror, this operation should take about one minute on the first launch",
                LogLevel.Info
            );

            // only use user-specified mirrors if generic is chosen
            var mirrorList =
                pkgManager.Config.DistroType != DistroType.Generic
                    ? await pkgManager.GetSavedMirrorsAsync().ConfigureAwait(false)
                    : new MirrorList(
                          DateTime.UtcNow,
                          pkgManager.Config.DistroType,
                          pkgManager.Config.OnlyHttpsMirrors,
                          pkgManager.Config.Mirrors.ToList()
                      );
            if (
                forceRefresh
                || (
                    (DateTime.Now - mirrorList.CreationTime).TotalDays > 15
                    && pkgManager.Config.DistroType != DistroType.Generic
                )
            )
            {
                pkgManager.Put("Mirror list is old, refreshing", LogLevel.Debug);
                // we refresh mirrors periodically but only if know how to fetch them
                // if the is generated from user-provided mirrors then we never
                // reach this code anyway
                mirrorList = new MirrorList(
                    DateTime.UtcNow,
                    pkgManager.Config.DistroType,
                    pkgManager.Config.OnlyHttpsMirrors
                );
            }
            if (mirrorList.DistroType != pkgManager.Config.DistroType)
            {
                pkgManager.Put("DistroType mismatch! Discarding saved mirrors", LogLevel.Warning);
                mirrorList = new MirrorList(
                    DateTime.UtcNow,
                    pkgManager.Config.DistroType,
                    pkgManager.Config.OnlyHttpsMirrors
                );
            }

            (_, bool modified) = await ConsoleUtils
                .SpinAsync(
                    "Checking network connectivity",
                    async () =>
                        await mirrorList
                            .DiscardMirrorsUntilFoundAsync(pkgManager)
                            .ConfigureAwait(false)
                )
                .ConfigureAwait(false);

            if (modified)
            {
                // the mirror list has been modified so we save it but we don't
                // change the creation time since no new mirrors have been added
                await pkgManager.SaveMirrorListAsync(mirrorList).ConfigureAwait(false);
            }

            if (mirrorList.Count == 0)
            {
                // we could not find a good mirror stored locally
                var freshMirrors = new MirrorList(
                    DateTime.UtcNow,
                    pkgManager.Config.DistroType,
                    pkgManager.Config.OnlyHttpsMirrors,
                    await ConsoleUtils
                        .SpinAsync(
                            "Fetching mirror list...",
                            () =>
                                pkgManager.MirrorProvider.GetMirrorListAsync(
                                    pkgManager.Config.DistroType,
                                    pkgManager.Config.OnlyHttpsMirrors
                                )
                        )
                        .ConfigureAwait(false)
                );

                if (freshMirrors.Count > 0)
                {
                    // first we sort by ping because it's quite fast and gives a somewhat
                    // good approximation of distance
                    await ConsoleUtils
                        .ProgressAsync(
                            "Finding the closest mirror",
                            freshMirrors.RankMirrorsByPingAsync
                        )
                        .ConfigureAwait(false);

                    // then we sort by download speed, but only a subset of the results
                    // since it can be slow
                    await ConsoleUtils
                        .ProgressAsync(
                            "Ranking mirrors by download speed",
                            (progress, ct) =>
                                freshMirrors.RankMirrorsByDownloadSpeedAsync(
                                    pkgManager,
                                    15,
                                    progress,
                                    ct
                                )
                        )
                        .ConfigureAwait(false);

                    mirrorList = new MirrorList(
                        DateTime.UtcNow,
                        pkgManager.Config.DistroType,
                        pkgManager.Config.OnlyHttpsMirrors,
                        freshMirrors
                    );
                    await pkgManager.SaveMirrorListAsync(mirrorList).ConfigureAwait(false);
                }
            }

            if (mirrorList.Count > 0)
            {
                pkgManager.Config.Mirrors.Clear();
                pkgManager.Config.Mirrors.AddRange(mirrorList);
                pkgManager.Put(
                    $"Chosen mirror is: {mirrorList[0].RootUri.AbsoluteUri}",
                    LogLevel.Info
                );
            }
            else
            {
                throw new ArgumentException("No mirrors were found");
            }
        }
    }
}
