namespace MultiCache.Config
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Net;
    using System.Text.Json.Serialization;
    using MultiCache.Helpers;
    public class AppConfiguration
    {
        public AppConfiguration(string cachePath)
        {
            CachePath = cachePath;
        }

        [Description("The buffer size used for streaming operations.")]
        [Range(0, int.MaxValue)]
        public int BufferSize { get; set; } = 4096;

        [Description("Where the application should store its data.")]
        public string CachePath { get; set; }

        [Description("The hostname to which the application should respond to requests.")]
        public string Hostname { get; set; } = "*";
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        [Description("The port on which to listen for incoming requests.")]
        [Range(0, int.MaxValue)]
        public int Port { get; set; } = 5050;

        [Description("The delay after which a network connection is considered closed.")]
        [Range(0, int.MaxValue)]
        public int NetworkTimeoutMs { get; set; } = 60_000;

        [Description("How many times the application should retry a failed task.")]
        [Range(0, int.MaxValue)]
        public int RetryCount { get; set; } = 3;

        [Description("How long to wait between each failed task.")]
        [Range(0, int.MaxValue)]
        public int RetryDelayMs { get; set; } = 5000;

        public WebProxy? Proxy { get; set; }

        [JsonIgnore]
        public HttpClient? HttpClient { get; set; }
    }
}
