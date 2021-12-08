namespace MultiCache.Resource.Interfaces
{
    using System;
    using System.IO;

    public interface IResourceHandle
    {
        bool Exists { get; }

        DateTime LastAccessTimeUtc { get; }
        DateTime LastWriteTimeUtc { get; }
        long Length { get; }

        string Name { get; }

        string NameWithoutExtension { get; }

        void Delete();

        void MoveTo(IResourceHandle destination);

        Stream OpenRead();

        Stream OpenReadWriteOrCreate();
    }
}
