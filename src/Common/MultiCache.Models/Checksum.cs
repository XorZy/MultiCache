namespace MultiCache.Models
{
    using System.Collections.ObjectModel;
    using System.Security.Cryptography;

    public enum ChecksumType
    {
        SHA256,
        MD5,
    }

    public class Checksum : IEquatable<Checksum>
    {
        private readonly byte[] _checksum;

        public Checksum(ChecksumType type, byte[] hash)
        {
            Type = type;
            _checksum = hash;
        }

        public ReadOnlyCollection<byte> Hash => Array.AsReadOnly(_checksum);

        public ChecksumType Type { get; }

        public static bool operator !=(Checksum a, Checksum b)
        {
            return !(a == b);
        }

        public static bool operator ==(Checksum? a, Checksum? b)
        {
            if (a is null && b is null)
            {
                return true;
            }

            if (a is null)
            {
                return false;
            }

            return a.Equals(b);
        }

        public static Checksum Parse(ChecksumType type, string checksum)
        {
            switch (type)
            {
                case ChecksumType.SHA256:
                    if (checksum.Length != 64)
                    {
                        throw new ArgumentException("Invalid checksum string");
                    }

                    var output = HexStringToBin(checksum);
                    return new Checksum(type, output);

                default:
                    if (checksum.Length != 32)
                    {
                        throw new ArgumentException("Invalid checksum string");
                    }

                    output = HexStringToBin(checksum);
                    return new Checksum(type, output);
            }
        }

        public HashAlgorithm CreateHashAlgorithm()
        {
            return Type switch
            {
                ChecksumType.SHA256 => SHA256.Create(),
                ChecksumType.MD5 => MD5.Create(),
                _ => throw new NotImplementedException()
            };
        }

        public bool Equals(Checksum? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other.Type == Type && other.Hash.SequenceEqual(Hash);
        }

        public override bool Equals(object? obj) => Equals(obj as Checksum);

        public override int GetHashCode()
        {
            return HashCode.Combine(_checksum, Hash, Type);
        }

        private static byte[] HexStringToBin(string hex)
        {
            var output = new byte[hex.Length / 2];
            if (output.Length != hex.Length / 2)
            {
                throw new ArgumentException("Invalid checksum string");
            }

            for (int i = 0; i < output.Length; i++)
            {
                output[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return output;
        }
    }
}
