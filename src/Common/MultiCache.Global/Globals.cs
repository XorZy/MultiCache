namespace MultiCache.Global
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public static class Globals
    {
        static Globals()
        {
            var xdgConf = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrWhiteSpace(xdgConf))
            {
                ConfigurationDirPaths = new[] { Path.Combine(xdgConf, AppName) };
            }
            else
            {
                ConfigurationDirPaths = new[]
                {
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".config",
                        AppName
                    ),
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        $".{AppName}"
                    ),
                    $"/etc/{AppName}/",
                };
            }
        }
        public static readonly string AppName = Assembly.GetEntryAssembly().GetName().Name;
        public const bool IsStableRelease = false;

        public static IEnumerable<string> ConfigurationDirPaths { get; set; }

        public static IEnumerable<FileInfo> AppConfigFiles =>
            ConfigurationDirPaths.Select(x => new FileInfo(Path.Combine(x, "conf")));

        public static Dictionary<string, Type> Handlers { get; } =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        public static DirectoryInfo GetRepoConfigurationDir(DirectoryInfo configRoot) =>
            new DirectoryInfo(Path.Combine(configRoot.FullName, "repos"));
        public static FileInfo GetAppConfigFile(DirectoryInfo configRoot) =>
            new FileInfo(Path.Combine(configRoot.FullName, "conf"));
    }
}
