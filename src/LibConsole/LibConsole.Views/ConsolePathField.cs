namespace LibConsole.Views
{
    public class ConsolePathField : ConsoleTextField
    {
        public override string? Prompt()
        {
            CursorToEnd();
            return base.Prompt();
        }

        public DirectoryInfo? PromptDirectory()
        {
            CursorToEnd();
            var path = Prompt();
            if (path is null)
            {
                return null;
            }
            return new DirectoryInfo(path);
        }
        protected override void OnKeyPress(ConsoleKeyInfo consoleKey)
        {
            if (consoleKey.Key == ConsoleKey.Tab)
            {
                try
                {
                    var currentDirectory = new DirectoryInfo(Buffer);
                    var currentSearch = Buffer.Split(Path.DirectorySeparatorChar).Last();
                    if (string.IsNullOrWhiteSpace(currentSearch))
                    {
                        return;
                    }
                    var parent = currentDirectory.Parent;
                    var dirs = parent.GetDirectories(currentSearch + "*");
                    if (dirs.Length > 0)
                    {
                        int commonLength = dirs[0].FullName.Length;
                        //if there is more than one match
                        //we find the longest common substring
                        if (dirs.Length > 1)
                        {
                            bool found = false;
                            for (
                                commonLength = 0;
                                !found && commonLength < dirs[0].FullName.Length;
                                commonLength++
                            )
                            {
                                for (int i = 1; i < dirs.Length; i++)
                                {
                                    if (
                                        dirs[i].FullName[commonLength]
                                        != dirs[i - 1].FullName[commonLength]
                                    )
                                    {
                                        commonLength--;
                                        found = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (dirs.Length == 1)
                        {
                            Buffer = dirs[0].FullName + "/";
                        }
                        else
                        {
                            Buffer = dirs[0].FullName[..commonLength];
                        }
                        Draw();
                        CursorToEnd();
                    }
                }
                catch { }
            }
        }
    }
}
