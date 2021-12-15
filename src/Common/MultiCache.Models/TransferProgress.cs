namespace Common.MultiCache.Models
{
    public record struct TransferProgress
    {
        public long TotalSize { get; set; }
        public long DownloadedBytes { get; set; }

        public TransferProgress(long totalSize, long downloadedBytes)
        {
            TotalSize = totalSize;
            DownloadedBytes = downloadedBytes;
        }

        public double Ratio => DownloadedBytes / (double)TotalSize;
    }
}