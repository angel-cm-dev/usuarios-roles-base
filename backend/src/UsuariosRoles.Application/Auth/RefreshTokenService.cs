using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;

namespace UsuariosRoles.Application.Auth;

public enum RefreshTokenStatus
{
    Success,
    Invalid,
    Expired,
    ReuseDetected,
}

public sealed record IssuedRefreshToken(string RawToken, DateTime ExpiresAt, Guid FamilyId);

public sealed record RefreshRotationResult(RefreshTokenStatus Status, Guid? UserId = null, IssuedRefreshToken? NewToken = null)
{
    public static RefreshRotationResult Invalid { get; } = new(RefreshTokenStatus.Invalid);
}

/// <summary>
/// Emisión, rotación y revocación de refresh tokens.
///
/// Rotación: cada uso de un refresh token lo invalida y emite uno nuevo de la misma "familia".
/// Detección de reuso: si llega un token que YA fue rotado/revocado, significa que alguien
/// (el usuario legítimo o un atacante) tiene una copia antigua. Como no podemos saber quién es quién,
/// revocamos toda la familia: ambos pierden la sesión y el legítimo vuelve a iniciar sesión.
/// </summary>
public sealed class RefreshTokenService(
    IApplicationDbContext db,
    ISecureTokenGenerator tokenGenerator,
    TimeProvider timeProvider,
    IOptions<SecurityOptions> options)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Crea un token (nueva familia si <paramref name="familyId"/> es null). No guarda cambios.</summary>
    public IssuedRefreshToken Issue(Guid userId, string? ipAddress, Guid? familyId = null) =>
        Create(userId, ipAddress, familyId).Issued;

    private (RefreshToken Entity, IssuedRefreshToken Issued) Create(Guid userId, string? ipAddress, Guid? familyId)
    {
        var now = Now;
        var raw = tokenGenerator.Generate();
        var expiresAt = now.AddDays(options.Value.Tokens.RefreshTokenDays);
        var token = new RefreshToken(userId, tokenGenerator.Hash(raw), familyId ?? Guid.NewGuid(), now, expiresAt, ipAddress);
        db.RefreshTokens.Add(token);
        return (token, new IssuedRefreshToken(raw, expiresAt, token.FamilyId));
    }

    public async Task<RefreshRotationResult> RotateAsync(string? rawToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return RefreshRotationResult.Invalid;
        }

        var hash = tokenGenerator.Hash(rawToken);
        var current = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (current is null)
        {
            return RefreshRotationResult.Invalid;
        }

        var now = Now;
        if (current.IsRevoked)
        {
            // Reuso detectado: se revoca la familia completa (incluido el token "vigente" que tenga el otro portador).
            await RevokeFamilyInternalAsync(current.FamilyId, now, "reuse-detected", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return new RefreshRotationResult(RefreshTokenStatus.ReuseDetected, current.UserId);
        }

        if (current.IsExpired(now))
        {
            return new RefreshRotationResult(RefreshTokenStatus.Expired, current.UserId);
        }

        var (nextEntity, next) = Create(current.UserId, ipAddress, current.FamilyId);
        current.Revoke(now, "rotated", nextEntity.Id);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Dos peticiones rotaron el mismo token a la vez: solo una gana (RevokedAt es token de concurrencia).
            return RefreshRotationResult.Invalid;
        }

        return new RefreshRotationResult(RefreshTokenStatus.Success, current.UserId, next);
    }

    /// <summary>Revoca la familia del token indicado (logout de un dispositivo). Devuelve el usuario dueño.</summary>
    public async Task<Guid?> RevokeFamilyAsync(string? rawToken, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = tokenGenerator.Hash(rawToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return null;
        }

        await RevokeFamilyInternalAsync(token.FamilyId, Now, reason, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return token.UserId;
    }

    /// <summary>Revoca todas las sesiones de un usuario (logout global, cambio de contraseña, bloqueo).</summary>
    public async Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.Revoke(now, reason);
        }

        await db.SaveChangesAsync(cancellationToken);
        return active.Count;
    }

    private async Task RevokeFamilyInternalAsync(Guid familyId, DateTime now, string reason, CancellationToken cancellationToken)
    {
        var family = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in family)
        {
            token.Revoke(now, reason);
        }
    }
}
