using System.Security.Cryptography;
using System.Text;

namespace EmailPagamenti.Web.Security;

/// <summary>
/// Verifica la password di accesso. Il confronto avviene sugli hash e a tempo costante,
/// cosi' il tempo di risposta non racconta nulla su quanti caratteri erano giusti.
/// </summary>
public static class PasswordGate
{
    public static bool Verify(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        Span<byte> providedHash = stackalloc byte[32];
        Span<byte> expectedHash = stackalloc byte[32];

        SHA256.HashData(Encoding.UTF8.GetBytes(provided), providedHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(expected), expectedHash);

        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}
