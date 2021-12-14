namespace MultiCache.Models
{
    using MultiCache.Utils;
    using System;
    using System.Text.RegularExpressions;

    public readonly record struct DataSize : IComparable<DataSize>
    {
        private readonly static Regex _splitRegex = new Regex(@"(?<=\d)(?=[\sa-zA-Z])", RegexOptions.Compiled);
        public long Bits { get; }
        public static DataSize Parse(string input)
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

            return new DataSize(bps);
        }

        public DataSize(long bits)
        {
            Bits = bits;
        }

        public string ToString(int decimalCount)
        {
            if (Bits < 1024)
            {
                return $"{Bits}b";
            }
            else if (Bits < 1024 * 1024)
            {
                return $"{Math.Round(Bits / 1024.0, decimalCount)}Kib";
            }
            else if (Bits < 1024 * 1024 * 1024)
            {
                return $"{Math.Round(Bits / (1024.0 * 1024.0), decimalCount)}Mib";
            }
            else if (Bits < 1024L * 1024 * 1024 * 1024)
            {
                return $"{Math.Round(Bits / (1024.0 * 1024.0 * 1024.0), decimalCount)}Gib";
            }
            else
            {
                return $"{Math.Round(Bits / (1024.0 * 1024.0 * 1024.0 * 1024.0), decimalCount)}Tib";
            }
        }

        public override string ToString() => ToString(4);

        public int CompareTo(DataSize other)
        {
            return Bits.CompareTo(other.Bits);
        }

        public static bool operator <(DataSize left, DataSize right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(DataSize left, DataSize right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(DataSize left, DataSize right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(DataSize left, DataSize right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}