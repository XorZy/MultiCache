namespace PacmanHandler.Helpers
{
    using MultiCache.Models;
    using MultiCache.Models.Pacman;
    using MultiCache.Resource.Interfaces;
    using MultiCache.Utils;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading.Tasks;

    internal class PacmanDbHelper
    {
        private const string Separator = "\n\n";
        private readonly IResourceHandle _file;
        private readonly Repository _repository;

        public PacmanDbHelper(IResourceHandle file, Repository repository)
        {
            _file = file;
            _repository = repository;
        }

        /// <summary>
        /// Enumerates the entries of a pacman .db file
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidDataException"></exception>
        public async IAsyncEnumerable<PackageInfo> EnumeratePackageInfosAsync()
        {
            var header = new byte[512];
            var block = new byte[512];
            var stringBuffer = new StringBuilder();
            Dictionary<string, string> data = new Dictionary<string, string>();
            using (var file = _file.OpenRead())
            {
                using (var stream = new GZipStream(file, CompressionMode.Decompress))
                {
                    while (await stream.FillAsync(header).ConfigureAwait(false) == 512)
                    {
                        var fileLength = GetSize(header.AsSpan(124, 11));
                        var blockCount = Math.Ceiling(fileLength / 512.0);
                        var type = (char)header[156];

                        for (int i = 0; i < blockCount; i++)
                        {
                            if (await stream.FillAsync(block).ConfigureAwait(false) != 512)
                            {
                                throw new InvalidDataException("Premature file end");
                            }
                            if (type == '0') // file
                            {
                                stringBuffer.Append(Encoding.ASCII.GetString(block));
                            }
                        }
                        if (type == '5' && data.Keys.Count > 0) // directory
                        {
                            yield return ParsePackageInfo(data);
                            data.Clear();
                        }

                        if (stringBuffer.Length > 0)
                        {
                            // archlinuxarm seems to use two files for each packet
                            // instead of one for the regular archlinux
                            // so we need to combine to two before parsing them
                            foreach (var (key, value) in ExtractData(stringBuffer.ToString()))
                            {
                                data[key] = value;
                            }

                            stringBuffer.Clear();
                        }
                    }
                }
            }
            // last entry
            if (data.Keys.Count > 0)
            {
                yield return ParsePackageInfo(data);
                data.Clear();
            }
        }

        public async Task<IList<PackageInfo>> GetPackagesAsync()
        {
            var output = new List<PackageInfo>();
            await foreach (var packageInfo in EnumeratePackageInfosAsync())
            {
                output.Add(packageInfo);
            }

            return output;
        }

        private static long GetSize(Span<byte> sizeBytes)
        {
            long size = 0;
            for (int i = 0; i < sizeBytes.Length; i++)
            {
                size += (long)Math.Pow(8, sizeBytes.Length - 1 - i) * (sizeBytes[i] - '0');
            }

            return size;
        }

        private static Dictionary<string, string> ExtractData(string data)
        {
            var fields = data.Split(Separator);
            var info = new Dictionary<string, string>();
            foreach (var field in fields)
            {
                if (field.Length > 0 && field[0] == '%') // there may be padding at the end of the string
                {
                    var lines = field.Split('\n', 2);
                    var header = lines[0];
                    info[header] = lines[1];
                }
            }
            return info;
        }
        private PackageInfo ParsePackageInfo(Dictionary<string, string> packageInfo)
        {
            var output = new PackageInfo(
                repository: _repository,
                name: packageInfo["%NAME%"],
                version: new PacmanPackageVersion(packageInfo["%VERSION%"]),
                architecture: packageInfo["%ARCH%"],
                fileName: packageInfo["%FILENAME%"],
                checksum: Checksum.Parse(ChecksumType.SHA256, packageInfo["%SHA256SUM%"]),
                compressedSize: packageInfo["%CSIZE%"].ToLongInvariant(),
                trueSize: packageInfo["%ISIZE%"].ToLongInvariant(),
                buildDate: DateTime.UnixEpoch.AddSeconds(
                    packageInfo["%BUILDDATE%"].ToLongInvariant()
                )
            ) {
                Signature = Convert.FromBase64String(packageInfo["%PGPSIG%"])
            };

            packageInfo.TryGetValue("%DEPENDS%", out var dependencies);
            output.Dependencies = dependencies?.Split('\n') ?? Array.Empty<string>();

            packageInfo.TryGetValue("%REPLACES%", out var replaces);
            output.Replaces = replaces?.Split('\n') ?? Array.Empty<string>();

            return output;
        }
    }
}
