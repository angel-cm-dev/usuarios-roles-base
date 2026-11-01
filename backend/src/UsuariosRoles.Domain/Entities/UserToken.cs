namespace UsuariosRoles.Domain.Entities;

public enum UserTokenPurpose
{
    EmailVerification = 1,
    PasswordReset = 2,
}

/// <summary>
/// Token de un solo uso (verificación de email, recuperación de contraseña).
/// Se guarda solo su hash y caduca; una vez consumido no puede reutilizarse.
/// </summary>
public sealed class UserToken
{
    private UserToken()
    {
    }

    public UserToken(Guid userId, UserTokenPurpose purpose, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Purpose = purpose;
        TokenHash = tokenHash;
        CreatedAt = createdAtUtc;
        ExpiresAt = expiresAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public UserTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? ConsumedAt { get; private set; }

    public bool CanBeUsed(DateTime nowUtc) => ConsumedAt is null && nowUtc < ExpiresAt;

    /// <summary>Marca el token como usado. Devuelve <c>false</c> si ya se usó o expiró.</summary>
    public bool TryConsume(DateTime nowUtc)
    {
        if (!CanBeUsed(nowUtc))
        {
            return false;
        }

        ConsumedAt = nowUtc;
        return true;
    }

    /// <summary>Invalida el token sin usarlo (p. ej. al emitir uno nuevo del mismo tipo).</summary>
    public void Invalidate(DateTime nowUtc) => ConsumedAt ??= nowUtc;
}
