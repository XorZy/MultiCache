namespace LibConsole.Views
{
    public class ConsolePathField : ConsoleTextField
    {
        public override string Prompt()
        {
            CursorToEnd();
            return base.Prompt();
        }
        protected override void OnKeyPress(ConsoleKeyInfo consoleKey)
        {
            if (consoleKey.Key == ConsoleKey.Tab)
            {
                try
                {
                    var currentDirectory = new DirectoryInfo(Buffer);
                    var currentSearch = Buffer.Split(Path.DirectorySeparatorChar).Last();
                    var parent = currentDirectory.Parent;
                    var dirs = parent.GetDirectories(currentSearch + "*");
                    if (dirs.Length == 1)
                    {
                        Buffer = dirs[0].FullName + "/";
                        Draw();
                        CursorToEnd();
                    }
                }
                catch { }
            }
        }
    }
}
