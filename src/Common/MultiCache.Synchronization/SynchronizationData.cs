namespace MultiCache.Synchronization
{
    using MultiCache.Network;
    using System.Threading.Tasks;

    public class SynchronizationData
    {
        public SynchronizationData(
            Task monitoredTask,
            Task synchronizationTask,
            CountedCancellationTokenSource countedCancellationTokenSource
        )
        {
            MonitoredTask = monitoredTask;
            SynchronizationTask = synchronizationTask;
            CountedCTSource = countedCancellationTokenSource;
        }

        public long ContentLength { get; set; }
        public CountedCancellationTokenSource CountedCTSource { get; }

        public Speed MaxReadSpeed
        {
            get => ThrottledStream.ReadSpeed;
            set { ThrottledStream.ReadSpeed = value; }
        }

        public Task MonitoredTask { get; }
        public Task SynchronizationTask { get; }
        public ThrottledStream? ThrottledStream { private get; set; }
    }
}
