namespace MultiCache.Network
{
    using System;
    using MultiCache.Models;

    public readonly record struct Speed : IComparable<Speed>
    {

        public Speed(long bps)
        {
            _size = new DataSize(bps);
        }

        public Speed(DataSize bps)
        {
            _size = bps;
        }

        public static readonly Speed Unlimited = new Speed(0);

        public bool IsUnlimited => BitsPerSecond == 0;

        public static Speed Parse(string input) => new Speed(DataSize.Parse(input));

        public long BitsPerSecond => _size.Bits;

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

        public override string ToString() => $"{base.ToString()}ps";


        private readonly DataSize _size;
    }
}