namespace MultiCache.Network
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Net.NetworkInformation;
    using MultiCache.Helpers;
    using MultiCache.Models;
    using MultiCache.PackageManager;
    using MultiCache.Utils;

    public class MirrorList : IList<Mirror>
    {
        public MirrorList(
            DateTime creationTime,
            DistroType distroType,
            bool httpsOnly,
            IEnumerable<Mirror>? mirrors = null
        )
        {
            CreationTime = creationTime;
            DistroType = distroType;
            Mirrors = mirrors?.ToList() ?? new List<Mirror>();
            HttpsOnly = httpsOnly;
        }
        public DateTime CreationTime { get; }

        public DistroType DistroType { get; }

        public List<Mirror> Mirrors { get; private set; }

        private bool _httpsOnly;
        public bool HttpsOnly
        {
            get => _httpsOnly;
            private set
            {
                _httpsOnly = value;
                if (HttpsOnly)
                {
                    Mirrors.RemoveAll(x => x.RootUri.Scheme != "https");
                }
            }
        }

        public int Count => Mirrors.Count;

        public bool IsReadOnly => ((ICollection<Mirror>)Mirrors).IsReadOnly;

        public Mirror this[int index]
        {
            get => Mirrors[index];
            set => Mirrors[index] = value;
        }

        public async Task SaveAsync(FileInfo destination)
        {
            await File.WriteAllLinesAsync(destination.FullName, Serialize()).ConfigureAwait(false);
        }

        public static async Task<MirrorList> LoadAsync(FileInfo destination, bool httpsOnly)
        {
            return Deserialize(
                await File.ReadAllLinesAsync(destination.FullName).ConfigureAwait(false),
                httpsOnly
            );
        }

        public string[] Serialize() =>
            new[] { CreationTime.ToBinary().ToStringInvariant(), DistroType.ToString() }
                .Concat(Mirrors.Select(x => x.RootUri.AbsoluteUri))
                .ToArray();
        public static MirrorList Deserialize(string[] input, bool httpsOnly)
        {
            return new MirrorList(
                creationTime: DateTime.FromBinary(input[0].ToLongInvariant()),
                distroType: Enum.Parse<DistroType>(input[1], true),
                httpsOnly: httpsOnly,
                mirrors: input[2..].Select(x => new Mirror(new Uri(x)))
            );
        }
        public async Task<(Mirror?, bool modified)> DiscardMirrorsUntilFoundAsync(
            PackageManagerBase pkgManager,
            CancellationToken ct = default
        )
        {
            var modified = false;
            for (int i = 0; i < Mirrors.Count; i++)
            {
                var mirror = Mirrors[0];
                if (
                    await mirror
                        .TryConnectAsync(pkgManager.Config.HttpClient, ct)
                        .ConfigureAwait(false)
                )
                {
                    return (mirror, modified);
                }
                else
                {
                    modified = true;
                    pkgManager.Put(
                        $"Moved {mirror.RootUri.AbsoluteUri} down the list",
                        LogLevel.Info
                    );

                    // the mirror may be down so we push it to the end of the list
                    // it may just be temporary so we don't want to delete it
                    Mirrors.RemoveAt(0);
                    Mirrors.Add(mirror);
                }
            }

            return (null, false);
        }

        public async Task RankMirrorsByPingAsync(
            IProgress<double> progress = null,
            CancellationToken ct = default
        )
        {
            var output = new List<(long, Mirror)>();

            int counter = 0;
            await Parallel
                .ForEachAsync(
                    Mirrors,
                    ct,
                    async (mirror, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var result = await mirror.PingAsync().ConfigureAwait(false);
                            if (result.Status == IPStatus.Success)
                            {
                                output.Add((result.RoundtripTime, mirror));
                            }
                        }
                        catch { }
                        finally
                        {
                            lock (output)
                            {
                                counter++;
                                progress?.Report(counter / (double)Mirrors.Count);
                            }
                        }
                    }
                )
                .ConfigureAwait(false);
            Mirrors = output.OrderBy(x => x.Item1).Select(x => x.Item2).ToList();
        }
        public async Task RankMirrorsByDownloadSpeedAsync(
            PackageManagerBase pkgManager,
            int maxCount,
            IProgress<double>? progress = null,
            CancellationToken ct = default
        )
        {
            var benchmarkResults = new List<(Speed speed, Mirror mirror)>();
            int counter = 0;
            var selectedMirrors = Mirrors.Take(maxCount).ToArray();
            foreach (var mirror in selectedMirrors)
            {
                try
                {
                    // this should not be necessary, analyzer bug?
#pragma warning disable CA2000
                    using (var timeoutCtSource = new CancellationTokenSource(20_000))
#pragma warning disable CA2000
                    {
                        using (
                            var combinedCtSource = CancellationTokenSource.CreateLinkedTokenSource(
                                timeoutCtSource.Token,
                                ct
                            )
                        )
                        {
                            //20 seconds seems reasonable
                            var benchmarkResource = pkgManager.GetBenchmarkResource(mirror);
                            if (benchmarkResource is not null)
                            {
                                var result = await pkgManager.Network
                                    .BenchmarkAsync(benchmarkResource, combinedCtSource.Token)
                                    .ConfigureAwait(false);
                                benchmarkResults.Add((new Speed(0), mirror));
                            }
                            else
                            {
                                benchmarkResults.Add((new Speed(0), mirror));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    pkgManager.Put(ex);
                    benchmarkResults.Add((new Speed(0), mirror));
                }
                finally
                {
                    counter++;
                    progress?.Report(counter / (double)selectedMirrors.Length);
                }
            }

            Mirrors = benchmarkResults
                .OrderByDescending(x => x.speed)
                .Select(x => x.mirror)
                .Concat(Mirrors.Skip(maxCount))
                .ToList();
        }

        public int IndexOf(Mirror item)
        {
            return Mirrors.IndexOf(item);
        }

        public void Insert(int index, Mirror item)
        {
            Mirrors.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            Mirrors.RemoveAt(index);
        }

        public void Add(Mirror item)
        {
            Mirrors.Add(item);
        }

        public void Clear()
        {
            Mirrors.Clear();
        }

        public bool Contains(Mirror item)
        {
            return Mirrors.Contains(item);
        }

        public void CopyTo(Mirror[] array, int arrayIndex)
        {
            Mirrors.CopyTo(array, arrayIndex);
        }

        public bool Remove(Mirror item)
        {
            return Mirrors.Remove(item);
        }

        public IEnumerator<Mirror> GetEnumerator()
        {
            return ((IEnumerable<Mirror>)Mirrors).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Mirrors).GetEnumerator();
        }
    }
}
