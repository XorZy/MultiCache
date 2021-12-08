namespace PacmanHandler.Network
{
    using MultiCache.Models;
    using MultiCache.Network;
    using MultiCache.PackageManager;
    using MultiCache.Utils;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class PacmanMirrorProvider : MirrorProviderBase
    {
        public PacmanMirrorProvider(PackageManagerBase repository) : base(repository) { }

        public override async Task<IEnumerable<Mirror>> GetMirrorListAsync(
            DistroType type,
            bool httpsOnly
        )
        {
            return type switch
            {
                //MANJARO_X64
                DistroType.ManjaroX64Stable
                  => await GetManjaroMirrorsAsync(httpsOnly, "stable", false).ConfigureAwait(false),
                DistroType.ManjaroX64Testing
                  => await GetManjaroMirrorsAsync(httpsOnly, "testing", false)
                      .ConfigureAwait(false),
                DistroType.ManjaroX64Unstable
                  => await GetManjaroMirrorsAsync(httpsOnly, "unstable", false)
                      .ConfigureAwait(false),
                DistroType.ManjaroAarch64Stable
                  //MANJARO AARCH64
                  => await GetManjaroMirrorsAsync(httpsOnly, "stable", true).ConfigureAwait(false),
                DistroType.ManjaroAarch64Testing
                  => await GetManjaroMirrorsAsync(httpsOnly, "testing", true).ConfigureAwait(false),
                DistroType.ManjaroAarch64Unstable
                  => await GetManjaroMirrorsAsync(httpsOnly, "unstable", true)
                      .ConfigureAwait(false),
                //ARCH
                DistroType.ArchLinuxX64
                  => await GetArchX64MirrorsAsync(httpsOnly).ConfigureAwait(false),
                DistroType.ArchLinuxArm
                  => await GetArchArmMirrorsAsync(httpsOnly).ConfigureAwait(false),
                _ => throw new NotSupportedException("Distribution is not supported")
            };
        }

        private static async Task<IEnumerable<Mirror>> GetArchArmMirrorsAsync(bool httpsOnly)
        {
            if (!httpsOnly)
                return new[] { new Mirror(new Uri("http://mirror.archlinuxarm.org/$arch/$repo/")) };
            else
                return Array.Empty<Mirror>();
        }

        private async Task<IEnumerable<Mirror>> GetArchX64MirrorsAsync(bool httpsOnly)
        {
            var output = new List<(double score, Mirror Mirror)>();

            var json = await Repository.Config.HttpClient
                .GetStringAsync(new Uri("https://archlinux.org/mirrors/status/json/"))
                .ConfigureAwait(false);
            foreach (
                var mirror in JsonSerializer
                    .Deserialize<JsonElement>(json)
                    .GetProperty("urls")
                    .EnumerateArray()
            )
            {
                var protocol = mirror.GetProperty("protocol").ToString();
                if (double.TryParse(mirror.GetProperty("score").ToString(), out var score))
                {
                    if (
                        mirror.GetProperty("active").ToString() == "True"
                        && (protocol == "http" || protocol == "https")
                    )
                    {
                        if (protocol == "http" && httpsOnly)
                        {
                            continue;
                        }

                        output.Add(
                            (
                                score,
                                new Mirror(
                                    new Uri(mirror.GetProperty("url").ToString()).Combine(
                                        "$repo/os/$arch/"
                                    )
                                )
                            )
                        );
                    }
                }
            }

            return output.OrderBy(x => x.score).Select(x => x.Mirror).Take(50);
        }

        private async Task<IEnumerable<Mirror>> GetManjaroMirrorsAsync(
            bool httpsOnly,
            string branch,
            bool arm
        )
        {
            var output = new List<Mirror>();

            var json = await Repository.Config.HttpClient
                .GetStringAsync(new Uri("https://repo.manjaro.org/mirrors.json"))
                .ConfigureAwait(false);
            foreach (var mirror in JsonSerializer.Deserialize<JsonElement>(json).EnumerateArray())
            {
                var url = mirror.GetProperty("url").ToString();
                if (
                    (!url.StartsWithInvariant("http"))
                    || (httpsOnly && !url.StartsWithInvariant("https"))
                )
                {
                    continue;
                }

                var uriSuffix = $"{(arm ? "arm-" : "")}{branch}/$repo/$arch/";

                output.Add(
                    new Mirror(new Uri(mirror.GetProperty("url").ToString()).Combine(uriSuffix))
                );
            }

            return output.Take(100);
        }
    }
}
