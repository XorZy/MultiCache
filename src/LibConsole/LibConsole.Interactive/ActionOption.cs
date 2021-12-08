namespace LibConsole.Interactive
{
    public record ActionOption(string Message, Action Action);
    public record AsyncOption
    {
        public string Message { get; }

        private Func<Task>? _taskProvider;
        private Action? _action;

        public AsyncOption(string message, Func<Task> taskProvider)
        {
            Message = message;
            _taskProvider = taskProvider;
        }

        public AsyncOption(string message, Action action)
        {
            Message = message;
            _action = action;
        }

        public async Task Execute()
        {
            if (_action is not null)
            {
                _action();
            }
            else if (_taskProvider is not null)
            {
                await _taskProvider.Invoke().ConfigureAwait(false);
            }
        }
    }
}
