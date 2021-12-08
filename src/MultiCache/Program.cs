namespace MultiCache
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using MultiCache.Config;
    using MultiCache.Config.Interactive;
    using MultiCache.Global;
    using MultiCache.Helpers;
    using MultiCache.PackageManager;
    using MultiCache.Scheduling;

    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            var arguments = ParseArguments(args);

            if (arguments.TryGetValue("config-dir", out var configDir))
            {
                // user-specified directory takes precedence
                Globals.ConfigurationDirPaths = new[] { configDir };
            }

            AppConfiguration? appConfig = null;
            DirectoryInfo? configurationDir = null;
            try
            {
                (configurationDir, appConfig) = LoadAppConfiguration();
            }
            catch (FileNotFoundException)
            {
                //not configured yet
                await CLIConfigurator.FirstTimeConfigureAsync().ConfigureAwait(false);
                (configurationDir, appConfig) = LoadAppConfiguration();
            }

            Log.Put(
                $"Welcome to {Globals.AppName} ! You are running v{Assembly.GetExecutingAssembly().GetName().Version}",
                LogLevel.Info
            );
            if (!Globals.IsStableRelease)
            {
                Log.Put(
                    "[slowblink]You are running a testing release, expect bugs[/]",
                    LogLevel.Warning
                );
            }

            using (appConfig.HttpClient)
            {
                if (arguments.ContainsKey("manage"))
                {
                    await CLIConfigurator
                        .ManageAsync(configurationDir, appConfig)
                        .ConfigureAwait(false);
                }
                var repoConfigs = LoadRepositoryConfigurations(configurationDir, appConfig);
                var repos = repoConfigs
                    .Select(x => PackageManagerProvider.SetupRepositoryAsync(x).Result)
                    .ToDictionary(x => x.Config.Prefix, x => x);

                StartSchedulers(repos);

                await new Proxy(appConfig, repos).RunAsync().ConfigureAwait(false);
            }
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            var output = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--config-dir")
                {
                    output["config-dir"] = args[i + 1];
                    i++;
                }
                if (args[i] == "manage")
                {
                    output["manage"] = "";
                    i++;
                }
            }
            return output;
        }

        private static (DirectoryInfo, AppConfiguration) LoadAppConfiguration()
        {
            var configFile = Globals.AppConfigFiles.FirstOrDefault(x => x.Exists);
            if (configFile is null)
            {
                throw new FileNotFoundException(
                    $"Could not find a configuration file.\nPossible locations are:\n{string.Join("\n", Globals.AppConfigFiles.Select(x => "-" + x.FullName))}"
                );
            }

            var appConfig = StaticConfigurationParser.LoadAppConfigFromFile(configFile.FullName);

            return (configFile.Directory, appConfig);
        }

        private static IEnumerable<RepositoryConfiguration> LoadRepositoryConfigurations(
            DirectoryInfo repoConfigDir,
            AppConfiguration config
        )
        {
            var output = new List<RepositoryConfiguration>();

            foreach (
                var configFile in Globals.GetRepoConfigurationDir(repoConfigDir).EnumerateFiles()
            )
            {
                var repoConfig = StaticConfigurationParser.ParseRepositoryConfiguration(
                    configFile,
                    config
                );

                output.Add(repoConfig);
            }

            return output;
        }
        private static void StartSchedulers(IDictionary<string, PackageManagerBase> pkgManagers)
        {
            foreach (var (key, pkgManager) in pkgManagers)
            {
                foreach (var option in pkgManager.Config.SchedulingOptions)
                {
                    IEnumerable<PackageManagerBase> nextRepositories = option.NextRepositories
                        .Select(x => pkgManagers[x])
                        .ToArray();

                    var description = CronExpressionDescriptor.ExpressionDescriptor.GetDescription(
                        option.CronExpression.ToString()
                    );
                    string limitString = option.BackgroundReadMaxSpeed.IsUnlimited
                        ? "with no speed limit"
                        : $"with download speed capped at {option.BackgroundReadMaxSpeed}";
                    string nextString = nextRepositories.Any()
                        ? $"followed by {string.Join(',', nextRepositories.Select(x => x.Config.Prefix))}"
                        : "";
                    pkgManager.Put(
                        $"Scheduled maintenance activated ({description}) {limitString} {nextString}",
                        LogLevel.Info
                    );

                    var scheduler = new SimpleScheduler(
                        option.CronExpression,
                        async () =>
                        {
                            pkgManager.Config.BackgroundReadMaxSpeed =
                                option.BackgroundReadMaxSpeed;
                            await pkgManager.MaintainAsync().ConfigureAwait(false);
                            foreach (var nextRepo in nextRepositories)
                            {
                                await nextRepo.MaintainAsync().ConfigureAwait(false);
                            }
                        }
                    );
                    scheduler.RunAsync();
                }
            }
        }
    }
}
