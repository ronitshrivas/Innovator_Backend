using System.Security.Cryptography;
using System.Text;

namespace AuthService.Services;

/// <summary>
/// Password hashing that stays compatible with data migrated from the old
/// Django backend. New passwords use BCrypt; existing users whose hashes are in
/// Django's "pbkdf2_sha256$iterations$salt$hash" format are still verified, and
/// their hash is upgraded to BCrypt the first time they log in successfully.
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password);

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

        if (storedHash.StartsWith("pbkdf2_sha256$", StringComparison.Ordinal))
            return VerifyDjangoPbkdf2(password, storedHash);

        // Anything else is treated as a BCrypt hash.
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True when the stored hash is not BCrypt and should be re-hashed after a successful login.</summary>
    public static bool NeedsUpgrade(string storedHash) =>
        !string.IsNullOrEmpty(storedHash) && !storedHash.StartsWith("$2", StringComparison.Ordinal);

    private static bool VerifyDjangoPbkdf2(string password, string storedHash)
    {
        // Format: pbkdf2_sha256$<iterations>$<salt>$<base64 digest>
        var parts = storedHash.Split('$');
        if (parts.Length != 4)
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        var salt = Encoding.UTF8.GetBytes(parts[2]);
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256);

        var actual = pbkdf2.GetBytes(expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
