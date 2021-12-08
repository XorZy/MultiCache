using System.Globalization;
using Spectre.Console;

namespace LibConsole.Views
{
    public class NumericField : ConsoleTextField
    {
        public NumericField()
        {
            Validation = (i) => int.TryParse(i, out var _);
        }
        public int? Prompt(string message, int initialValue)
        {
            AnsiConsole.WriteLine(message);
            Buffer = initialValue.ToString(CultureInfo.InvariantCulture);
            CursorToEnd();
            if (Prompt() is null)
                return null;
            return int.Parse(Buffer, CultureInfo.InvariantCulture);
        }
    }
}
