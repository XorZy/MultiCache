using System.Globalization;
using MultiCache.Utils;

namespace MultiCache.Config.Interactive
{
    public static class NumericHelper
    {
        private static string GetOrdinalSuffix(int num)
        {
            var str = num.ToString(CultureInfo.InvariantCulture);
            if (
                str.EndsWithInvariant("11")
                || str.EndsWithInvariant("12")
                || str.EndsWithInvariant("13")
            )
            {
                return "th";
            }
            return (num % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        }

        public static string ToOrdinal(int num)
        {
            return num switch
            {
                1 => "new",
                _ => $"{num}{GetOrdinalSuffix(num)}"
            };
        }
    }
}
