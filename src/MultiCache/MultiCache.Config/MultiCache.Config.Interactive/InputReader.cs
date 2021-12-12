namespace MultiCache.Config.Interactive
{
    using System;
    using System.IO;
    using Cronos;
    using LibConsole.Interactive;
    using LibConsole.Views;
    using MultiCache.Network;
    using Spectre.Console;

    public static class InputReader
    {
        public static Speed? ReadSpeed()
        {
            while (true)
            {
                AnsiConsole.WriteLine("Please input your desired maximum speed.");
                AnsiConsole.WriteLine(
                    "I understand all common units such as kbps kBps Mibps etc..."
                );
                AnsiConsole.WriteLine("A value of 0 represent an unlimited speed.");
                try
                {
                    var value = ReadString(true);
                    if (value is null)
                    {
                        return null;
                    }
                    return Speed.Parse(value);
                }
                catch
                {
                    if (!ContinueOnBadInput())
                    {
                        return new Speed(0);
                    }
                }
            }
        }

        public static CronExpression? ReadCron()
        {
            AnsiConsole.Write(new Rule("Cron expression editor") { Alignment = Justify.Left });
            AnsiConsole.WriteLine("(minute) (hour) (day of the month) (month) (day of the week)");

            var cronField = new ConsoleCronField() { Buffer = "0 23 * * *", AllowEscape = true };
            var cronString = cronField.Prompt();
            if (cronString is null)
            {
                return null;
            }
            return CronExpression.Parse(cronField.Buffer);
        }

        public static string? ReadString(bool allowEscape)
        {
            var textField = new ConsoleTextField() { AllowEscape = allowEscape };
            return textField.Prompt();
        }

        public static DirectoryInfo? ReadPath(string defaultPath, bool allowEscape)
        {
            var pathField = new ConsolePathField()
            {
                Buffer = defaultPath,
                AllowEscape = allowEscape
            };
            var res = pathField.PromptDirectory();
            AnsiConsole.WriteLine();
            return res;
        }

        public static string? ValidatedInput(
            string message,
            Func<string, bool> validation,
            bool allowEscape
        )
        {
            while (true)
            {
                AnsiConsole.WriteLine(message);
                var input = ReadString(allowEscape);
                if (input is null)
                {
                    return null;
                }
                if (validation(input))
                {
                    return input;
                }
                else
                {
                    ConsoleUtils.Error("Invalid input");
                }
            }
        }

        public static bool ContinueOnBadInput()
        {
            ConsoleUtils.Error("Ouch! This expression is not valid! Please check you input.");
            return ConsoleUtils.YesNo("Try again?");
        }
    }
}
