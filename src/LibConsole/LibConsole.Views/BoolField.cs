using LibConsole.Interactive;

namespace LibConsole.Views
{
    public static class BoolField
    {
        public static bool Prompt(string message, bool defaultChoice) =>
            ConsoleUtils.MultiChoice(
                message,
                defaultChoice ? new[] { true, false } : new[] { false, true }
            );
    }
}
