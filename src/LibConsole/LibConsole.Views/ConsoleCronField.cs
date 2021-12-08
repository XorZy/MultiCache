using System.Text.RegularExpressions;
using Cronos;
using LibConsole.Interactive;
using Spectre.Console;

namespace LibConsole.Views
{
    public class ConsoleCronField : ConsoleTextField
    {
        public ConsoleCronField()
        {
            Validation = IsValidExpression;
            AnsiConsole.Cursor.MoveDown();
            AnsiConsole.Cursor.MoveUp();
        }
        private static readonly Regex _spaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly string[] _cronFields =
        {
            "minute",
            "hour",
            "day of month",
            "month",
            "day of the week"
        };

        private static void PrintHint(int cursor, string expression)
        {
            AnsiConsole.Cursor.MoveDown();
            ConsoleUtils.ClearCurrentLine();
            var index = _spaceRegex.Matches(expression[0..cursor]).Count;

            if (index >= _cronFields.Length)
            {
                AnsiConsole.Markup("[white on red]Invalid[/]");
            }
            else
            {
                AnsiConsole.Markup($"[black on white]{_cronFields[index]}[/]");
            }
            AnsiConsole.Cursor.MoveUp();
        }

        private static bool IsValidExpression(string cronExpressionString)
        {
            try
            {
                _ = CronExpression.Parse(cronExpressionString);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override void Cleanup()
        {
            AnsiConsole.Cursor.MoveDown();
            ConsoleUtils.ClearCurrentLine();
            AnsiConsole.Cursor.MoveUp();
            ConsoleUtils.ClearCurrentLine();
        }
        protected override void OnRedrawn()
        {
            var valid = IsValidExpression(Buffer);
            int originalCursorLeft = Console.CursorLeft;
            PrintHint(originalCursorLeft, Buffer);
            Console.CursorLeft = Buffer.Length + 1;

            AnsiConsole.Markup(
                valid
                  ? $"[green]{CronExpressionDescriptor.ExpressionDescriptor.GetDescription(Buffer)}[/]"
                  : "[red]Invalid[/]"
            );
            Console.CursorLeft = originalCursorLeft;
        }
    }
}
