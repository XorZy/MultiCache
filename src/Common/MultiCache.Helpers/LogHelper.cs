namespace MultiCache.Helpers
{
    using System;
    using LibConsole.Interactive;
    using Spectre.Console;

    public enum LogLevel
    {
        Nothing,
        Error,
        Warning,
        Info,
        Debug,
    }

    /// <summary>
    /// Facilitates logging
    /// </summary>
    public static class Log
    {
#if DEBUG
        public static LogLevel DesiredLevel { get; set; } = LogLevel.Debug;

#else
        public static LogLevel DesiredLevel { get; set; } = LogLevel.Info;
#endif

        public static void Put(Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }

        public static void Put(object message, LogLevel level)
        {
            if (level > DesiredLevel)
            {
                return;
            }

            var colorString = level switch
            {
                LogLevel.Error => "red",
                LogLevel.Warning => "yellow",
                LogLevel.Info => "blue",
                LogLevel.Debug => "magenta",
                _ => "white"
            };

            if (level != LogLevel.Error)
            {
                AnsiConsole.MarkupLine(
                    $"[{colorString}]{Enum.GetName(typeof(LogLevel), level)}: {message}[/]"
                );
            }
            else
            {
                ConsoleUtils.Error($"Error: {message}");
            }
        }
    }
}
