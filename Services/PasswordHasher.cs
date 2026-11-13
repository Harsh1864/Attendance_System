using System;
using System.Security.Cryptography;

namespace project.Services
{
    /// <summary>
    /// PBKDF2-SHA256 password hashing.
    /// Stored format: pbkdf2$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;
    /// Values without the "pbkdf2$" prefix are treated as legacy plain-text
    /// passwords so existing accounts keep working until they are upgraded.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "pbkdf2";
        private const int SaltSize = 16;
        private const int HashSize = 32;
        public const int DefaultIterations = 100_000;

        public static string Hash(string password, int iterations = DefaultIterations)
        {
            if (password is null) throw new ArgumentNullException(nameof(password));
            if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);
            return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool IsHashed(string? stored) =>
            stored != null && stored.StartsWith(Prefix + "$", StringComparison.Ordinal);

        /// <summary>
        /// Verifies a password against a stored value (hashed or legacy plain text).
        /// </summary>
        public static bool Verify(string? password, string? stored)
        {
            if (password is null || string.IsNullOrEmpty(stored)) return false;

            if (!IsHashed(stored))
            {
                // Legacy plain-text value: constant-time compare.
                return CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(password),
                    System.Text.Encoding.UTF8.GetBytes(stored));
            }

            var parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations < 1)
                return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        /// <summary>True when the stored value should be re-hashed (legacy or weaker settings).</summary>
        public static bool NeedsRehash(string? stored)
        {
            if (!IsHashed(stored)) return true;
            var parts = stored!.Split('$');
            return parts.Length != 4 || !int.TryParse(parts[1], out var it) || it < DefaultIterations;
        }
    }
}
