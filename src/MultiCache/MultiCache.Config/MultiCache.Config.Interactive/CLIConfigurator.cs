namespace MultiCache.Config.Interactive
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using LibConsole.Interactive;
    using LibConsole.Models;
    using LibConsole.Views;
    using MultiCache.Global;
    using MultiCache.Models;
    using MultiCache.Network;
    using MultiCache.PackageManager;
    using MultiCache.Scheduling;
    using Spectre.Console;

    public static class CLIConfigurator
    {
        private static void ManageSchedules(
            RepositoryConfiguration config,
            FileInfo repoConfigFileInfo
        )
        {
            while (true)
            {
                var choice = ConsoleUtils.MultiChoiceWithNewQuit(
                    "Please select a scheduled maintenance",
                    config.SchedulingOptions
                );

                if (choice == CustomChoice.New)
                {
                    var newSchedule = SetupNewSchedulingOptions();
                    if (newSchedule is not null)
                    {
                        config.SchedulingOptions.Add(newSchedule);
                    }
                }
                if (choice == CustomChoice.Quit)
                {
                    StaticConfigurationParser.SerializeConfigFile(
                        config,
                        repoConfigFileInfo.FullName,
                        _referenceRepoConfig
                    );
                    return;
                }

                if (
                    choice is ValueChoice<SchedulingOptions> vChoice
                    && ConsoleUtils.YesNo("Delete this schedule?", false)
                )
                {
                    config.SchedulingOptions.Remove(vChoice.Choice);
                }
            }
        }

        private static SchedulingOptions? SetupNewSchedulingOptions()
        {
            while (true)
            {
                AnsiConsole.WriteLine("Please input the Cron expression:");

                var cronExpression = InputReader.ReadCron();
                if (cronExpression is null)
                {
                    return null;
                }
                try
                {
                    var description = CronExpressionDescriptor.ExpressionDescriptor.GetDescription(
                        cronExpression.ToString()
                    );

                    if (ConsoleUtils.YesNo($"Your schedule is : {description}. Is that okay?"))
                    {
                        AnsiConsole.WriteLine("Awesome!");
                        if (
                            ConsoleUtils.YesNo(
                                "Would you like to set up a maximum download speed during this scheduled maintenance?"
                            )
                        )
                        {
                            var speed = InputReader.ReadSpeed();
                            if (speed is null)
                            {
                                return new SchedulingOptions(cronExpression);
                            }

                            return new SchedulingOptions(cronExpression, speed.Value);
                        }
                        else
                        {
                            return new SchedulingOptions(cronExpression);
                        }
                    }
                }
                catch
                {
                    if (!InputReader.ContinueOnBadInput())
                    {
                        return null;
                    }
                }
            }
        }

        private static async Task<List<Mirror>> GetUserMirrorsAsync()
        {
            using var client = new HttpClient();
            var userMirrors = new List<Mirror>();
            do
            {
                AnsiConsole.WriteLine("Please add an upstream mirror. You must add at least one.");
                var mirrorUrl = InputReader.ReadString(true);
                if (mirrorUrl is null)
                {
                    continue;
                }
                try
                {
                    var mirror = new Mirror(new Uri(mirrorUrl));

                    var success = await ConsoleUtils
                        .SpinAsync(
                            $"Trying to contact {mirror.RootUri.Host}",
                            () => mirror.TryConnectAsync(client)
                        )
                        .ConfigureAwait(false);

                    if (!success)
                    {
                        if (
                            ConsoleUtils.YesNo(
                                "Unable to reach the specified mirror. Are you sure you want to add it? It may cause issues!",
                                false
                            )
                        )
                        {
                            userMirrors.Add(mirror);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    userMirrors.Add(mirror);

                    AnsiConsole.MarkupLine($"[green]{mirror.RootUri.Host} added![/]");
                }
                catch
                {
                    ConsoleUtils.Error("Invalid Uri");
                }
            } while (userMirrors.Count < 1 || ConsoleUtils.YesNo("Add another mirror?"));

            return userMirrors;
        }

        private static readonly AppConfiguration _referenceAppConfig = new AppConfiguration(
            "/dummypath"
        );
        private static readonly RepositoryConfiguration _referenceRepoConfig =
            new RepositoryConfiguration(
                PackageManagerType.Pacman,
                string.Empty,
                _referenceAppConfig
            );
        private static async Task CreateRepositoriesAsync(
            DirectoryInfo repoDir,
            AppConfiguration appConfig,
            string[] repositoryNames
        )
        {
            repoDir.Create();

            for (
                int repoCount = 1;
                ConsoleUtils.YesNo(
                    $"Would you like to set up a {NumericHelper.ToOrdinal(repoCount)} repository?"
                );
                repoCount++
            )
            {
                var packageManagerType = ConsoleUtils.MultiChoice(
                    "Please choose a packet manager for this repository",
                    Enum.GetValues<PackageManagerType>()
                );
                var name = InputReader.ValidatedInput(
                    "Please chose a name for this repository. (The name may not contain spaces)",
                    (input) =>
                    {
                        if (
                            input.Contains(' ', StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrWhiteSpace(input)
                        )
                        {
                            return false;
                        }
                        if (input.Any(x => Path.GetInvalidFileNameChars().Contains(x)))
                        {
                            return false;
                        }
                        return true;
                    },
                    true
                );

                if (name is null)
                {
                    repoCount--;
                    continue;
                }

                if (repositoryNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    AnsiConsole.WriteLine();
                    ConsoleUtils.Error("A repository with this name already exists.");
                    repoCount--;
                    continue;
                }

                AnsiConsole.Write(new Rule(name) { Alignment = Justify.Left });

                DistroType distroType;
                List<Mirror> userMirrors = new List<Mirror>();

                while (true)
                {
                    var supportedDistros = PackageManagerProvider.GetSupportedDistributions(
                        packageManagerType
                    );
                    distroType = ConsoleUtils.MultiChoice(
                        "Please choose a distribution for this repository",
                        supportedDistros.ToArray()
                    );

                    if (distroType == DistroType.Generic)
                    {
                        AnsiConsole.MarkupLine(
                            "[yellow]You have selected the Generic distribution.[/]"
                        );
                        AnsiConsole.WriteLine(
                            "Only pick Generic if your distribution is not on the list or you wish to add upstream mirrors manually"
                        );
                        if (ConsoleUtils.YesNo("Continue with Generic?", false))
                        {
                            userMirrors.AddRange(await GetUserMirrorsAsync().ConfigureAwait(false));
                        }
                        else
                        {
                            continue;
                        }
                    }
                    break;
                }

                AnsiConsole.WriteLine("All right!");

                var repoConfig = new RepositoryConfiguration(packageManagerType, name, appConfig)
                {
                    DistroType = distroType
                };

                if (userMirrors.Count > 0)
                {
                    repoConfig.Mirrors.AddRange(userMirrors);
                }

                while (
                    ConsoleUtils.YesNo(
                        $"Would you like to set up a {NumericHelper.ToOrdinal(repoConfig.SchedulingOptions.Count + 1)} scheduled maintenance for {name}?"
                    )
                )
                {
                    var schedule = SetupNewSchedulingOptions();
                    if (schedule is null)
                    {
                        break;
                    }
                    repoConfig.SchedulingOptions.Add(schedule);
                    AnsiConsole.MarkupLine("[green]Scheduled maintenance registered.[/]");
                }

                var repoFile = new FileInfo(
                    Path.Combine(repoDir.FullName, name + "." + packageManagerType)
                );

                StaticConfigurationParser.SerializeConfigFile(
                    repoConfig,
                    repoFile.FullName,
                    _referenceRepoConfig
                );

                AnsiConsole.MarkupLine($"[green]Repository {name} created![/]");
                AnsiConsole.Write(new Rule());
            }
        }

        private static bool TestWrite(DirectoryInfo info)
        {
            var testfile = new FileInfo(Path.Combine(info.FullName, Path.GetRandomFileName()));
            try
            {
                using (var test = testfile.Create()) { }
                testfile.Delete();
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static void DirectoryError()
        {
            ConsoleUtils.Error(
                "Unable to write in the specified path. Are you sure this is the correct location? Does the path exist? Is it a directory? Does the current user have write permissions?"
            );
        }

        private static void EnsureInteractive()
        {
            if (!AnsiConsole.Profile.Out.IsTerminal)
            {
                throw new IOException(
                    "This console does not support interaction! Please try again with a terminal emulator."
                );
            }
        }

        public static async Task FirstTimeConfigureAsync()
        {
            EnsureInteractive();

            AnsiConsole.Write(new FigletText(Globals.AppName).LeftAligned());

            AnsiConsole.WriteLine($"Welcome to the {Globals.AppName} configuration utility");

            string storagePath = "/";
            while (true)
            {
                AnsiConsole.WriteLine("Where should the application store its data?");
                storagePath = InputReader.ReadPath(storagePath);
                DirectoryInfo directoryInfo;
                try
                {
                    directoryInfo = new DirectoryInfo(storagePath);
                }
                catch
                {
                    continue;
                }

                if (
                    !directoryInfo.Exists
                    && ConsoleUtils.YesNo("Directory does not exist, create it?")
                )
                {
                    try
                    {
                        directoryInfo.Create();
                    }
                    catch
                    {
                        DirectoryError();
                        continue;
                    }
                }
                if (!TestWrite(directoryInfo))
                {
                    DirectoryError();
                    continue;
                }
                break;
            }
            var configDir = new DirectoryInfo(Globals.ConfigurationDirPaths.First());
            configDir.Create();

            var appConfig = new AppConfiguration(storagePath);

            await CreateRepositoriesAsync(
                    Globals.GetRepoConfigurationDir(configDir),
                    appConfig,
                    Array.Empty<string>()
                )
                .ConfigureAwait(false);

            StaticConfigurationParser.SerializeConfigFile(
                appConfig,
                Globals.AppConfigFiles.First().FullName,
                _referenceAppConfig
            );

            AnsiConsole.WriteLine(
                $"That's it, configuration done! Thank you for using  {Globals.AppName}!"
            );
        }

        private static async Task ManageRepositoryAsync(
            AppConfiguration appConfig,
            PackageManagerBase pkgManager,
            FileInfo repoConfigFileInfo
        )
        {
            var repoName = pkgManager.Config.Prefix;
            var repoDir = new DirectoryInfo(Path.Combine(appConfig.CachePath, repoName));

            bool keepGoing = true;

            while (keepGoing)
            {
                AnsiConsole.Write(new Rule(repoName) { Alignment = Justify.Left });

                AnsiConsole.WriteLine();

                await ConsoleUtils
                    .MultiChoiceAsync(
                        "What do you want to do?",
                        new AsyncOption("Update", pkgManager.MaintainAsync),
                        new AsyncOption(
                            "Manage Schedules",
                            () => ManageSchedules(pkgManager.Config, repoConfigFileInfo)
                        ),
                        new AsyncOption(
                            "Other settings",
                            () =>
                                ObjectEditor.EditSettings(
                                    pkgManager.Config,
                                    repoConfigFileInfo,
                                    _referenceRepoConfig
                                )
                        ),
                        new AsyncOption(
                            "Delete",
                            () =>
                            {
                                if (
                                    ConsoleUtils.YesNo(
                                        "ARE YOU SURE? ALL DATA WILL BE LOST FOREVER",
                                        false
                                    )
                                )
                                {
                                    repoDir.Delete(true);
                                    repoConfigFileInfo.Delete();
                                }
                            }
                        ),
                        new AsyncOption("Quit", () => keepGoing = false)
                    )
                    .ConfigureAwait(false);
            }
        }

        private static async Task ManageRepositoriesAsync(
            DirectoryInfo appConfigDir,
            AppConfiguration appConfig
        )
        {
            while (true)
            {
                var repoFiles = Globals.GetRepoConfigurationDir(appConfigDir).GetFiles();

                if (repoFiles.Length == 0)
                {
                    ConsoleUtils.Error("No repositories to manage!");
                    return;
                }

                var choice = ConsoleUtils.MultiChoice(
                    "Please choose a repository",
                    repoFiles,
                    (x) =>
                        $"{Path.GetFileNameWithoutExtension(x.Name)} [[{Path.GetExtension(x.Name)[1..]}]]",
                    CustomChoice.New,
                    CustomChoice.Quit
                );

                if (choice == CustomChoice.New)
                {
                    await CreateRepositoriesAsync(
                            Globals.GetRepoConfigurationDir(appConfigDir),
                            appConfig,
                            repoFiles
                                .Select(x => Path.GetFileNameWithoutExtension(x.Name))
                                .ToArray()
                        )
                        .ConfigureAwait(false);
                    continue;
                }

                if (choice == CustomChoice.Quit)
                {
                    return;
                }

                if (choice is ValueChoice<FileInfo> fileChoice)
                {
                    var repoConfigFileInfo = fileChoice.Choice;
                    var repoName = Path.GetFileNameWithoutExtension(repoConfigFileInfo.Name);

                    var pkgManager = PackageManagerProvider.InstantiatePackageManager(
                        StaticConfigurationParser.ParseRepositoryConfiguration(
                            repoConfigFileInfo,
                            appConfig
                        )
                    );

                    await ManageRepositoryAsync(appConfig, pkgManager, repoConfigFileInfo)
                        .ConfigureAwait(false);
                }
            }
        }

        public static async Task ManageAsync(DirectoryInfo appConfigDir, AppConfiguration appConfig)
        {
            EnsureInteractive();

            AnsiConsole.Write(new FigletText(Globals.AppName).LeftAligned());

            AnsiConsole.WriteLine($"Welcome to {Globals.AppName}");
            AnsiConsole.WriteLine("Please note that in this mode the server is not running.");
            bool keepGoing = true;
            while (keepGoing)
            {
                await ConsoleUtils
                    .MultiChoiceAsync(
                        "What would you like to do?",
                        new AsyncOption(
                            "Manage repositories",
                            () => ManageRepositoriesAsync(appConfigDir, appConfig)
                        ),
                        new AsyncOption(
                            "Change global settings",
                            () =>
                                ObjectEditor.EditSettings(
                                    appConfig,
                                    Globals.GetAppConfigFile(appConfigDir),
                                    _referenceAppConfig
                                )
                        ),
                        new AsyncOption("Exit", () => keepGoing = false)
                    )
                    .ConfigureAwait(false);
            }
        }
    }
}
