namespace LibConsole.Views
{
    using LibConsole.Interactive;
    using Spectre.Console;

    public class ConsoleTextField
    {
        public string Buffer { get; set; } = string.Empty;

        public bool AllowEscape { get; set; }

        public void CursorToStart() => Console.CursorLeft = 0;
        public void CursorToEnd() => Console.CursorLeft = Buffer.Length;
        public void Draw()
        {
            int cursorLeft = Console.CursorLeft;
            ConsoleUtils.ClearCurrentLine();
            AnsiConsole.Write(Buffer);
            Console.CursorLeft = cursorLeft;
            OnRedrawn();
        }

        public Func<string, bool>? Validation { get; set; }

        protected virtual void OnRedrawn() { }

        protected virtual void OnKeyPress(ConsoleKeyInfo consoleKey) { }

        protected virtual void Cleanup() { }

        public string? Prompt(string message, string defaultValue)
        {
            AnsiConsole.WriteLine(message);
            Buffer = defaultValue;
            CursorToEnd();
            return Prompt();
        }

        public virtual string? Prompt()
        {
            while (true)
            {
                Draw();

                var c = AnsiConsole.Console.Input.ReadKeyAsync(true, CancellationToken.None).Result;
                if (c is null)
                {
                    continue;
                }
                switch (c.Value.Key)
                {
                    case ConsoleKey.Escape:
                        if (AllowEscape)
                        {
                            return null;
                        }
                        break;
                    case ConsoleKey.Backspace:
                        if (Console.CursorLeft > 0)
                        {
                            Buffer =
                                Buffer[0..(Console.CursorLeft - 1)] + Buffer[Console.CursorLeft..];
                        }

                        if (Console.CursorLeft > 0)
                        {
                            Console.CursorLeft--;
                        }
                        break;
                    case ConsoleKey.Delete:
                        if (Console.CursorLeft < Buffer.Length)
                        {
                            Buffer =
                                Buffer[0..Console.CursorLeft] + Buffer[(Console.CursorLeft + 1)..];
                        }
                        break;
                    case ConsoleKey.LeftArrow:

                        if (Console.CursorLeft > 0)
                        {
                            Console.CursorLeft--;
                        }
                        break;
                    case ConsoleKey.RightArrow:
                        if (Console.CursorLeft < Buffer.Length)
                        {
                            Console.CursorLeft++;
                        }
                        break;

                    case ConsoleKey.Enter:
                        if (Validation is null || Validation(Buffer))
                        {
                            Cleanup();
                            return Buffer;
                        }
                        break;

                    default:
                        if (!char.IsControl(c.Value.KeyChar))
                        {
                            var len = Buffer.Length;
                            var newExpression =
                                Console.CursorLeft == 0 ? "" : Buffer[0..Console.CursorLeft];

                            newExpression += c.Value.KeyChar;
                            if (Console.CursorLeft < len)
                            {
                                newExpression += Buffer[Console.CursorLeft..];
                            }

                            Buffer = newExpression;

                            Console.CursorLeft++;
                        }
                        break;
                }
                OnKeyPress(c.Value);
            }
        }
    }
}
