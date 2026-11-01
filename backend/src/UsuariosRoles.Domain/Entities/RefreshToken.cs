namespace UsuariosRoles.Domain.Entities;

/// <summary>
/// Refresh token rotativo. Solo se persiste el HASH (SHA-256) del valor: si la BD se filtra,
/// los tokens no se pueden usar. Todos los tokens nacidos de un mismo login comparten
/// <see cref="FamilyId"/>, lo que permite revocar la cadena completa ante un reuso.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, Guid familyId, DateTime createdAtUtc, DateTime expiresAtUtc, string? createdByIp)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAt = createdAtUtc;
        ExpiresAt = expiresAtUtc;
        CreatedByIp = createdByIp;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public string TokenHash { get; private set; } = string.Empty;

    public Guid FamilyId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>Token que reemplazó a este en la rotación (trazabilidad de la cadena).</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAt;

    public bool IsActive(DateTime nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    /// <summary>Revoca el token. Es idempotente: no sobrescribe el motivo de una revocación previa.</summary>
    public void Revoke(DateTime nowUtc, string reason, Guid? replacedByTokenId = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = nowUtc;
        RevokedReason = reason;
        ReplacedByTokenId = replacedByTokenId;
    }
}
