namespace MultiCache.Network
{
    using MultiCache.Utils;
    using System;
    using System.Text.RegularExpressions;

    public readonly record struct Speed : IComparable<Speed>
    {
        private readonly static Regex _splitRegex = new Regex(@"(?<=\d)(?=[\sa-zA-Z])", RegexOptions.Compiled);
        public long BitsPerSecond { get; }

        public bool IsUnlimited => BitsPerSecond < 1;
        public static Speed Parse(string input)
        {
            input = input.Trim();
            var split = _splitRegex.Split(input);
            if (split.Length != 2)
            {
                throw new ArgumentException("Invalid format");
            }

            var quantity = split[0].ToDoubleInvariant();
            var unit = split[1].Trim();

            int power = unit[0] switch
            {
                'b' or 'B' => 0,
                'k' or 'K' => 1,
                'm' or 'M' => 2,
                'g' or 'G' => 3,
                't' or 'T' => 4,
                _ => throw new FormatException("Unknown format")
            };

            int bitIndex = power == 0 ? 0 : 1;
            int baseUnit = 1000;

            if (unit[1] == 'i') // IEC prefix
            {
                baseUnit = 1024;
                bitIndex = 2;
            }

            int bitMultiplier = unit[bitIndex] switch
            {
                'b' => 1,
                'B' => 8,
                _ => throw new FormatException("Unknown format")
            };

            long bps = (long)Math.Round(

                Math.Pow(baseUnit, power) * bitMultiplier * quantity);

            return new Speed(bps);
        }

        public Speed(long bps)
        {
            BitsPerSecond = bps;
        }

        public override string ToString()
        {
            if (BitsPerSecond < 1024)
            {
                return $"{BitsPerSecond}bps";
            }
            else if (BitsPerSecond < 1024 * 1024)
            {
                return $"{BitsPerSecond / 1024.0}Kibps";
            }
            else if (BitsPerSecond < 1024 * 1024 * 1024)
            {
                return $"{BitsPerSecond / (1024.0 * 1024.0)}Mibps";
            }
            else if (BitsPerSecond < 1024L * 1024 * 1024 * 1024)
            {
                return $"{BitsPerSecond / (1024.0 * 1024.0 * 1024.0)}Gibps";
            }
            else
            {
                return $"{BitsPerSecond / (1024.0 * 1024.0 * 1024.0 * 1024.0)}Tibps";
            }
        }

        public int CompareTo(Speed other)
        {
            return BitsPerSecond.CompareTo(other.BitsPerSecond);
        }

        public static bool operator <(Speed left, Speed right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(Speed left, Speed right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(Speed left, Speed right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(Speed left, Speed right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}