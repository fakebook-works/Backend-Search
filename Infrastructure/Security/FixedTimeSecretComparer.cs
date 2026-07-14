using System.Security.Cryptography;
using System.Text;

namespace BackEndSearchFakebook.Infrastructure.Security;

public static class FixedTimeSecretComparer
{
    public const int MinimumSecretBytes = 32;

    public static bool IsStrongEnough(string? secret) =>
        !string.IsNullOrWhiteSpace(secret) &&
        Encoding.UTF8.GetByteCount(secret) >= MinimumSecretBytes;

    public static bool Matches(string? supplied, string? configured)
    {
        if (string.IsNullOrEmpty(supplied) || !IsStrongEnough(configured))
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured!));

        return CryptographicOperations.FixedTimeEquals(suppliedHash, configuredHash);
    }
}
