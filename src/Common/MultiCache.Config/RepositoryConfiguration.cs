namespace MultiCache.Config
{
    using System.ComponentModel;
    using System.Net;
    using System.Text.Json.Serialization;
    using MultiCache.Models;
    using MultiCache.Network;
    using MultiCache.Scheduling;

    public class RepositoryConfiguration
    {
        private int _maintenanceMaxThreads = 1;
        public RepositoryConfiguration(
            PackageManagerType packageManagerType,
            string prefix,
            AppConfiguration appConfig
        )
        {
            AppConfiguration = appConfig;
            PackageManagerType = packageManagerType;
            Prefix = prefix;
            CachePath = new DirectoryInfo(Path.Combine(appConfig.CachePath.FullName, Prefix));
            BufferSize = appConfig.BufferSize;
            Proxy = appConfig.Proxy;
            HttpClient = appConfig.HttpClient;
        }

        [JsonIgnore]
        public int BufferSize { get; set; }

        [JsonIgnore]
        public AppConfiguration AppConfiguration { get; }

        [JsonIgnore]
        public PackageManagerType PackageManagerType { get; }
        [JsonIgnore]
        public string Prefix { get; }
        [JsonIgnore]
        public HttpClient? HttpClient { get; }

        [JsonIgnore]
        public DirectoryInfo CachePath { get; }

        public List<SchedulingOptions> SchedulingOptions { get; set; } =
            new List<SchedulingOptions>();
        public WebProxy? Proxy { get; }

        [Description("Whether to enable the API endpoint for this repository.")]
        public bool AllowApi { get; set; } = true;

        [Description(
            "The maximum download speed during background operations (may be overridden by schedule settings)."
        )]
        public Speed BackgroundReadMaxSpeed { get; set; }

        [Description(
            "Whether to use a hashing method such as SHA256 to ensure the integrity of already downloaded packages.\nNote that even when this is disabled, the application will still verify the integrity of resources while they are being downloaded."
        )]
        public bool ChecksumIntegrityCheck { get; set; }

        [Browsable(false)]
        public DistroType DistroType { get; set; } = DistroType.Generic;

        [Description(
            "Whether dependencies should also be fetched during maintenance, even if they are not tracked by the application."
        )]
        public bool FetchDependencies { get; set; } = true;

        [Description("The maximum download speed for forward operations.")]
        public Speed ForegroundReadMaxSpeed { get; set; }

        [Description("The maximum upload speed for forward operations.")]
        public Speed ForegroundWriteMaxSpeed { get; set; }

        [Description(
            "Whether to keep downloading a resource even after the last client has disconnected."
        )]
        public bool KeepDownloading { get; set; }

        [Description(
            "Whether the application should keep old package versions during maintenance."
        )]
        public bool KeepOldPackages { get; set; }

        [Description("The maximum number of threads to use during maintenance")]
        public int MaintenanceMaxThreads
        {
            get => _maintenanceMaxThreads;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("The number of thread must be >0");
                }

                _maintenanceMaxThreads = value;
            }
        }

        [Description("Whether the application should only use Https upstream mirrors.")]
        public bool OnlyHttpsMirrors { get; set; }

        [Description(
            "A package which has been updated but not requested by a client after that many days will be purged from the repository."
        )]
        public int? PackageStalenessDelayDays { get; set; } = 45;

        [Description("Whether to resume interrupted downloads.")]
        public bool ReusePartialDownloads { get; set; } = true;
        public List<Mirror> Mirrors { get; set; } = new List<Mirror>();
    }
}
