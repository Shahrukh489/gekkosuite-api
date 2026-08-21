using System.Security.Cryptography;
using System.Text;

using Konscious.Security.Cryptography;

namespace GekkoSuite.Api.Services;

/// <summary>
/// Argon2id password hashing, shared by every place that stores or checks a user_account.password
/// (see docs/auth.md). Extracted from AuthService so UserService can hash a temporary password for a
/// newly created account without duplicating the KDF parameters.
/// </summary>
public static class PasswordHasher
{
    private const int ArgonMemoryKb = 19 * 1024;
    private const int ArgonIterations = 2;
    private const int ArgonParallelism = 1;
    private const int ArgonHashLengthBytes = 32;
    private const int SaltLengthBytes = 16;

    /// <summary>
    /// Hashes a password with Argon2id under a freshly generated random salt, returning the
    /// self-describing "base64(salt):base64(hash)" string stored in user_account.password.
    /// </summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        var hash = HashWithSalt(password, salt);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Checks a plaintext password against a stored "base64(salt):base64(hash)" hash.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHashPassword)
    {
        var parts = storedHashPassword.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = HashWithSalt(password, salt);

        // Fixed-time comparison so a mismatch can't be timed to leak how many leading bytes matched.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <summary>
    /// Generates a random, human-typeable temporary password for an admin-created account. Returned
    /// once to the caller (never stored in plaintext) so it can be handed to the new user out of band —
    /// there is no invite-email flow yet (see docs/auth.md's Security Review Notes).
    /// </summary>
    public static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        const int length = 16;

        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars);
    }

    /// <summary>
    /// Runs the Argon2id KDF over a password with a given salt.
    /// </summary>
    private static byte[] HashWithSalt(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = ArgonParallelism,
            MemorySize = ArgonMemoryKb,
            Iterations = ArgonIterations
        };

        return argon2.GetBytes(ArgonHashLengthBytes);
    }
}
