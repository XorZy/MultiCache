namespace MultiCache.Network
{
    using MultiCache.Models;
    using MultiCache.PackageManager;

    public abstract class MirrorProviderBase
    {
        protected PackageManagerBase Repository { get; }
        protected MirrorProviderBase(PackageManagerBase repository)
        {
            Repository = repository;
        }
        public abstract Task<IEnumerable<Mirror>> GetMirrorListAsync(
            DistroType type,
            bool httpsOnly
        );
    }
}
