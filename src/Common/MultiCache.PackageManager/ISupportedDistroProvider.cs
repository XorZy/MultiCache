using MultiCache.Models;

namespace MultiCache.PackageManager
{
    public interface ISupportedDistroProvider
    {
        public abstract static IReadOnlyList<DistroType> SupportedDistros { get; }
    }
}
