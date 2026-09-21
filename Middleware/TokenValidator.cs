using System.Security.Cryptography;
using System.Text;

namespace UserManagementAPI.Middleware;

// Opaque shared service token for this exercise, not a JWT or a user login system.
public sealed class TokenValidator
{
    private readonly byte[] expectedHash;

    public TokenValidator(IConfiguration configuration)
    {
        var token = configuration["Authentication:Token"];
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32 || token.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Configure Authentication:Token with at least 32 characters and no whitespace.");
        expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    public bool IsValid(string token) => CryptographicOperations.FixedTimeEquals(
        expectedHash, SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
