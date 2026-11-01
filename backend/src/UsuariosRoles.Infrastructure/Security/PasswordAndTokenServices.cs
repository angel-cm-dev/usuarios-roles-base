using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;

namespace UsuariosRoles.Infrastructure.Security;

/// <summary>
/// Adaptador sobre PasswordHasher de ASP.NET Core Identity: PBKDF2-HMAC-SHA512 con sal aleatoria y
/// 100.000 iteraciones (formato v3). El formato incluye la versión, por lo que si Microsoft sube
/// las iteraciones, Verify devuelve SuccessRehashNeeded y re-hasheamos en el siguiente login.
/// </summary>
internal sealed class IdentityPasswordHasher : Application.Common.IPasswordHasher
{
    private readonly PasswordHasher<User> inner = new();

    public string Hash(User user, string password) => inner.HashPassword(user, password);

    public PasswordCheckResult Verify(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return PasswordCheckResult.Failed;
        }

        return inner.VerifyHashedPassword(user, user.PasswordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheckResult.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
            _ => PasswordCheckResult.Failed,
        };
    }
}

/// <summary>
/// Tokens opacos de 256 bits (refresh, verificación, reset). Se persiste SHA-256 del valor:
/// al ser aleatorios y largos no necesitan sal ni un hash lento (no son adivinables como una contraseña).
/// </summary>
internal sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    private const int TokenBytes = 32;

    public string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    public string Hash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
