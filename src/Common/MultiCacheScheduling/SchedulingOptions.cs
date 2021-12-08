namespace MultiCache.Scheduling
{
    using Common.MultiCache.Config;
    using Cronos;
    using MultiCache.Network;

    public record SchedulingOptions : ISimpleSerializable
    {
        public CronExpression CronExpression { get; }
        public Speed BackgroundReadMaxSpeed { get; }

        public IEnumerable<string> NextRepositories { get; }

        public SchedulingOptions(
            CronExpression expression,
            Speed backgroundReadMaxSpeed = default,
            IEnumerable<string>? nextRepositories = null
        )
        {
            CronExpression = expression;
            BackgroundReadMaxSpeed = backgroundReadMaxSpeed;
            NextRepositories = nextRepositories ?? Array.Empty<string>();
        }

        public override string ToString()
        {
            var cronDesc = CronExpressionDescriptor.ExpressionDescriptor.GetDescription(
                CronExpression.ToString()
            );
            if (!BackgroundReadMaxSpeed.IsUnlimited)
            {
                return $"{cronDesc} at {BackgroundReadMaxSpeed}";
            }
            return cronDesc;
        }

        private static string CleanupCron(CronExpression expression)
        {
            var split = expression.ToString().Split();
            // for some reasons seconds are included
            // so we get rid of them
            return string.Join(" ", split[1..]);
        }
        public string Serialize() =>
            $"{CleanupCron(CronExpression)}|{BackgroundReadMaxSpeed}|{string.Join(',', NextRepositories)}";

        public static SchedulingOptions Parse(string input)
        {
            var split = input.Split('|').Select(x => x.Trim()).ToArray();
            var cronExpression = CronExpression.Parse(split[0]);
            Speed backgroundReadMaxSpeed = default;
            string[] nextRepositories = Array.Empty<string>();
            if (split.Length > 1)
            {
                backgroundReadMaxSpeed = Speed.Parse(split[1]);
            }
            if (split.Length > 2)
            {
                nextRepositories = split[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
            }

            return new SchedulingOptions(cronExpression, backgroundReadMaxSpeed, nextRepositories);
        }
    }
}

