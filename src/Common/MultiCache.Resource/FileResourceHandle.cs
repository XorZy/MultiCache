namespace MultiCache.Resource
{
    using MultiCache.Resource.Interfaces;
    using System;
    using System.IO;

    public class FileResourceHandle : IResourceHandle
    {
        private readonly FileInfo _fileInfo;

        public FileResourceHandle(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
        }

        public bool Exists
        {
            get => _fileInfo.Exists;
        }

        public DateTime LastAccessTimeUtc => _fileInfo.LastAccessTimeUtc;

        public DateTime LastWriteTimeUtc => _fileInfo.LastWriteTimeUtc;

        public long Length
        {
            get => _fileInfo.Length;
        }

        public string Name => _fileInfo.Name;

        public string NameWithoutExtension => Path.GetFileNameWithoutExtension(_fileInfo.Name);

        public void Delete()
        {
            if (Exists)
                _fileInfo.Delete();
        }

        public void MoveTo(IResourceHandle destination)
        {
            if (destination is FileResourceHandle fHandle)
            {
                _fileInfo.MoveTo(fHandle._fileInfo.FullName, true);
            }
            else
            {
                throw new NotSupportedException("Unsupported destination resource type");
            }
        }

        public Stream OpenRead() =>
            _fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        public Stream OpenReadWriteOrCreate()
        {
            _fileInfo.Directory.Create(); // make sure the path exists
            return _fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        }
    }
}
