namespace MultiCache.Models
{
    using System;
    using System.Collections.Generic;

    public class PackageInfo : Package
    {
        public PackageInfo(
            Repository repository,
            string name,
            PackageVersion version,
            string architecture,
            string fileName,
            Checksum checksum,
            string[] dependencies,
            long compressedSize,
            long trueSize,
            DateTime buildDate
        ) : base(repository, name, architecture, fileName, version)
        {
            Checksum = checksum;
            Dependencies = dependencies;
            CompressedSize = compressedSize;
            TrueSize = trueSize;
            BuildDate = buildDate;
        }

        public ReadOnlyMemory<byte>? Signature { get; set; }
        public DateTime BuildDate { get; }
        public Checksum Checksum { get; }

        public long CompressedSize { get; }
        public IReadOnlyCollection<string> Dependencies { get; }
        public long TrueSize { get; }
    }
}
