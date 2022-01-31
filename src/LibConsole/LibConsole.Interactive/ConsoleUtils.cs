namespace LibConsole.Interactive
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using LibConsole.Models;
    using Spectre.Console;

    public static class ConsoleUtils
    {
        public static void ClearCurrentLine()
        {
            Console.CursorLeft = 0;
            AnsiConsole.Write(new string(' ', AnsiConsole.Profile.Width - 1));
            Console.CursorLeft = 0;
        }

        public static bool YesNo(string message, bool yes = true)
        {
            return AnsiConsole.Confirm(message, yes);
        }
        public static void MultiChoice(string message, params ActionOption[] options)
        {
            (
                MultiChoice(message, options, (x) => x.Message) as ValueChoice<ActionOption>
            ).Choice.Action.Invoke();
        }

        public static Task MultiChoiceAsync(string message, params AsyncOption[] options)
        {
            return (
                MultiChoice(message, options, (x) => x.Message) as ValueChoice<AsyncOption>
            ).Choice.Execute();
        }

        public static T MultiChoice<T>(string message, IEnumerable<T> choices) where T : notnull
        {
            AnsiConsole.WriteLine();
            return AnsiConsole.Prompt(new SelectionPrompt<T>().Title(message).AddChoices(choices));
        }

        public static CustomChoice MultiChoiceWithQuit<T>(
            string message,
            IEnumerable<T> valueChoices
        ) where T : notnull
        {
            return MultiChoice(message, valueChoices, new[] { CustomChoice.Quit });
        }

        public static CustomChoice MultiChoiceWithNewQuit<T>(
            string message,
            IEnumerable<T> valueChoices
        ) where T : notnull
        {
            return MultiChoice(
                message,
                valueChoices,
                new[] { CustomChoice.New, CustomChoice.Quit }
            );
        }

        public static CustomChoice MultiChoice<T>(
            string message,
            IEnumerable<T> valueChoices,
            params CustomChoice[] otherChoices
        ) where T : notnull
        {
            AnsiConsole.WriteLine();
            return AnsiConsole.Prompt(
                new SelectionPrompt<CustomChoice>()
                    .Title(message)
                    .AddChoices(valueChoices.Select(x => new ValueChoice<T>(x)).ToArray())
                    .AddChoices(otherChoices)
            );
        }

        public static CustomChoice MultiChoice<T>(
            string message,
            IEnumerable<T> valueChoices,
            Func<T, string> toString,
            params CustomChoice[] otherChoices
        ) where T : notnull
        {
            return AnsiConsole.Prompt(
                new SelectionPrompt<CustomChoice>()
                    .Title(message)
                    .AddChoices(valueChoices.Select(x => new ValueChoice<T>(x, toString)).ToArray())
                    .AddChoices(otherChoices)
            );
        }

        public static Task<T> SpinAsync<T>(string message, Func<Task<T>> action)
        {
            return AnsiConsole
                .Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(message, (_) => action());
        }

        public static Task SpinAsync(string message, Func<Task> action)
        {
            return AnsiConsole
                .Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(message, (_) => action());
        }

        public static Task RatioProgressAsync(
            string message,
            Func<IProgress<double>, CancellationToken, Task> action,
            CancellationToken ct = default
        )
        {
            var progress = new Progress<double>();
            return AnsiConsole
                .Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(
                    (ctx) =>
                    {
                        var consoleProgress = ctx.AddTask(message, true, 1.0);
                        progress.ProgressChanged += (s, e) => consoleProgress.Value = e;
                        return action(progress, ct);
                    }
                );
        }

        public static Task DownloadProgressAsync(
            string message,
            Func<IProgress<(long totalBytes, long newBytes)>, CancellationToken, Task> action,
            long dlSize,
            CancellationToken ct = default
        )
        {
            var progress = new Progress<(long totalBytes, long newBytes)>();
            return AnsiConsole
                .Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new TransferSpeedColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(
                    (ctx) =>
                    {
                        var consoleProgressTask = ctx.AddTask(message, true, dlSize);
                        if (dlSize > 0)
                        {
                            progress.ProgressChanged += (s, e) =>
                            {
                                if (consoleProgressTask.Value == 0)
                                {
                                    //take into account partially downloaded files
                                    consoleProgressTask.Value = e.totalBytes;
                                }
                                else
                                {
                                    consoleProgressTask.Increment(e.newBytes);
                                }
                            };
                        }
                        return action(progress, ct);
                    }
                );
        }

        public static void Error(string error)
        {
            AnsiConsole.MarkupLine($"[red]{error.EscapeMarkup()}[/]");
        }
    }
}
