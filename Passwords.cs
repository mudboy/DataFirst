using System.Security.Cryptography;
using DataFirst.Lodash;

namespace DataFirst;

/// Password hashing, stored as generic data like everything else.
///
/// The book stores base64 of the password and calls it encrypted. That is not a
/// hash and offers no protection, so this uses PBKDF2-SHA256 with a per-user salt
/// instead. The iteration count travels with the hash so stored passwords can be
/// upgraded later without guessing how they were made.
public static class Passwords
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int DefaultIterations = 210_000;

    /// Produces {salt, hash, iterations} -- a map, so it diffs and serialises like
    /// any other value.
    public static DataMap Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, iterations);

        return Map.Of(
            "salt", Convert.ToBase64String(salt),
            "hash", Convert.ToBase64String(hash),
            "iterations", (long)iterations);
    }

    /// Compares in constant time, so a wrong password takes as long as a right one.
    public static bool Verify(DataValue stored, string password)
    {
        if (stored is not DataMap record || string.IsNullOrEmpty(password)) return false;

        try
        {
            var salt = Convert.FromBase64String(_.Get<string>(record, "salt"));
            var expected = Convert.FromBase64String(_.Get<string>(record, "hash"));
            var iterations = (int)_.Get<long>(record, "iterations");

            return CryptographicOperations.FixedTimeEquals(Derive(password, salt, iterations), expected);
        }
        catch (Exception e) when (e is FormatException or KeyNotFoundException or InvalidOperationException)
        {
            // A malformed credential record is a failed login, not a crash.
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
