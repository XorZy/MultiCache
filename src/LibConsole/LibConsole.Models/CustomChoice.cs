namespace LibConsole.Models
{
    using System;
    public enum ChoiceType
    {
        Value,
        Quit,
        New,
    }

    public class CustomChoice
    {
        public ChoiceType ChoiceType { get; }

        public CustomChoice(ChoiceType choiceType)
        {
            ChoiceType = choiceType;
        }

        public static readonly CustomChoice New = new CustomChoice(ChoiceType.New);
        public static readonly CustomChoice Quit = new CustomChoice(ChoiceType.Quit);

        public override string ToString() => Enum.GetName(ChoiceType);
    }

    public class ValueChoice<T> : CustomChoice where T : notnull
    {
        public T Choice { get; }

        private readonly Func<T, string>? _toString;

        public ValueChoice(T choice, Func<T, string>? toString = null) : base(ChoiceType.Value)
        {
            Choice = choice;
            _toString = toString;
        }

        public override string ToString() => _toString?.Invoke(Choice) ?? Choice.ToString();
    }
}

