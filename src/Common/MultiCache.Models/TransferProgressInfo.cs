namespace Common.MultiCache.Models
{
    public record struct TransferProgressInfo
    {
        public long TotalSize { get; set; }
        public long TotalDownloadedBytes { get; set; }

        public long NewBytes { get; set; }

        public TransferProgressInfo(long totalSize, long totalDownloadedBytes, long newBytes)
        {
            TotalSize = totalSize;
            TotalDownloadedBytes = totalDownloadedBytes;
            NewBytes = newBytes;
        }
    }
}