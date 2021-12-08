namespace MultiCache.Models.Pacman
{
    using MultiCache.Utils;
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    internal record PacmanPackageVersion : PackageVersion, IComparable<PacmanPackageVersion>
    {
        private static readonly Regex _regex = new Regex(@"\d+|[a-zA-Z]+", RegexOptions.Compiled);
        public PacmanPackageVersion(string versionString) : base(versionString)
        {
            var tmp = versionString.Split(':', 2);
            if (tmp.Length > 1)
            {
                Epoch = tmp[0];
            }
            else
            {
                Epoch = "0";
            }

            tmp = tmp[^1].Split('-', 2);
            if (tmp.Length > 1)
            {
                Rel = tmp[1];
            }
            else
            {
                Rel = "0";
            }

            Version = tmp[0];
        }

        public string Epoch { get; }
        public string Version { get; }
        public string Rel { get; }

        public static bool IsNum(string str) => char.IsNumber(str[0]);

        private static int Compare(string a, string b)
        {
            var isNumA = IsNum(a);
            var isNumB = IsNum(b);

            if (isNumA && isNumB)
            {
                return a.ToLongInvariant().CompareTo(b.ToLongInvariant());
            }

            if (!isNumA && !isNumB)
            {
                return Math.Sign(string.CompareOrdinal(a, b));
            }

            if (isNumA && !isNumB)
            {
                return 1;
            }

            return -1;
        }

        private static (string Match, int SeparatorLen)[] GetMatches(string version)
        {
            return _regex
                .Matches(version)
                .Select(
                    x =>
                        (
                            x.Value,
                            sepLen: (
                                x.NextMatch().Index > 0 ? x.NextMatch().Index : x.Index + x.Length
                            )
                                - x.Index
                                - x.Length
                        )
                )
                .ToArray();
        }

        public int CompareTo(PacmanPackageVersion? other)
        {
            if (other is null)
            {
                return 1;
            }

            if (VersionString == other.VersionString)
            {
                return 0;
            }

            var tmp = Compare(Epoch, other.Epoch);
            if (tmp != 0)
            {
                return tmp;
            }

            var a = GetMatches(Version);
            var b = GetMatches(other.Version);

            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                var res = Compare(a[i].Match, b[i].Match);
                if (res != 0)
                {
                    return res;
                }

                var sepComp = a[i].SeparatorLen.CompareTo(b[i].SeparatorLen);
                if (sepComp != 0)
                {
                    return sepComp;
                }
            }

            if (a.Length > b.Length && !IsNum(a[b.Length].Match))
            {
                return -1;
            }

            if (b.Length > a.Length && !IsNum(b[a.Length].Match))
            {
                return 1;
            }

            tmp = a.Length.CompareTo(b.Length);
            if (tmp != 0)
            {
                return tmp;
            }

            if (Rel != "0" && other.Rel != "0")
            {
                tmp = Compare(Rel, other.Rel);
                if (tmp != 0)
                {
                    return tmp;
                }
            }

            return 0;
        }

        public override string ToString() => VersionString;

        public static bool operator <(PacmanPackageVersion left, PacmanPackageVersion right)
        {
            return left is null ? right is not null : left.CompareTo(right) < 0;
        }

        public static bool operator <=(PacmanPackageVersion left, PacmanPackageVersion right)
        {
            return left is null || left.CompareTo(right) <= 0;
        }

        public static bool operator >(PacmanPackageVersion left, PacmanPackageVersion right)
        {
            return left?.CompareTo(right) > 0;
        }

        public static bool operator >=(PacmanPackageVersion left, PacmanPackageVersion right)
        {
            return left is null ? right is null : left.CompareTo(right) >= 0;
        }
    }
}
