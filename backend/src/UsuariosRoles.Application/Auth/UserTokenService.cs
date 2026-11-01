using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;

namespace UsuariosRoles.Application.Auth;

/// <summary>
/// Tokens de un solo uso para verificación de email y recuperación de contraseña.
/// - Solo se guarda el hash SHA-256: quien lea la BD no puede usar los enlaces pendientes.
/// - Emitir uno nuevo invalida los anteriores del mismo propósito (solo vale el último correo).
/// - ConsumedAt es token de concurrencia: dos usos simultáneos del mismo enlace no pueden ganar ambos.
/// </summary>
public sealed class UserTokenService(IApplicationDbContext db, ISecureTokenGenerator tokenGenerator, TimeProvider timeProvider)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Emite un token y guarda los cambios. Devuelve el valor en claro (solo viaja en el correo).</summary>
    public async Task<string> IssueAsync(Guid userId, UserTokenPurpose purpose, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var pending = await db.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in pending)
        {
            token.Invalidate(now);
        }

        var raw = tokenGenerator.Generate();
        db.UserTokens.Add(new UserToken(userId, purpose, tokenGenerator.Hash(raw), now, now.Add(lifetime)));
        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    /// <summary>
    /// Marca el token como consumido y devuelve su usuario, o <c>null</c> si no existe, expiró o ya se usó.
    /// NO guarda: el llamador persiste el consumo junto con el cambio que autoriza (atomicidad).
    /// </summary>
    public async Task<User?> ConsumeAsync(string? rawToken, UserTokenPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = tokenGenerator.Hash(rawToken.Trim());
        var token = await db.UserTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, cancellationToken);

        return token is not null && token.TryConsume(Now) ? token.User : null;
    }
}
