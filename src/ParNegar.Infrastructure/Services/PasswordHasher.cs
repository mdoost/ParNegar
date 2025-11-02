using System.Security.Cryptography;
using System.Text;
using ParNegar.Application.Interfaces.Services;

namespace ParNegar.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32; // 256 bit
    private const int Iterations = 600000; // OWASP 2023 recommendation
    private const char Delimiter = ':';

    public string HashPassword(string password)
    {
        using var algorithm = new Rfc2898DeriveBytes(
            password,
            SaltSize,
            Iterations,
            HashAlgorithmName.SHA256);

        var salt = algorithm.Salt;
        var key = algorithm.GetBytes(KeySize);

        return string.Join(
            Delimiter,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key),
            Iterations);
    }

    public bool VerifyPassword(string password, string hash)
    {
        var parts = hash.Split(Delimiter);

        if (parts.Length != 3)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var key = Convert.FromBase64String(parts[1]);
        var iterations = int.Parse(parts[2]);

        using var algorithm = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256);

        var keyToCheck = algorithm.GetBytes(KeySize);

        // Constant time comparison
        return keyToCheck.SequenceEqual(key);
    }
}
