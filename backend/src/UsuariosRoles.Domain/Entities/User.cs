namespace UsuariosRoles.Domain.Entities;

/// <summary>
/// Usuario del sistema. Encapsula las reglas de bloqueo y estado de la cuenta
/// para que no puedan saltarse desde otras capas.
/// </summary>
public sealed class User
{
    // Constructor para EF Core.
    private User()
    {
    }

    public User(string email, string fullName, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        SetEmail(email);
        FullName = fullName.Trim();
        CreatedAt = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    /// <summary>Email en mayúsculas: permite búsquedas e índice único sin depender de la collation de la BD.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public bool EmailConfirmed { get; private set; }

    public bool IsActive { get; private set; }

    public int AccessFailedCount { get; private set; }

    public DateTime? LockoutEnd { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public DateTime? PasswordChangedAt { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();

    public bool IsLockedOut(DateTime nowUtc) => LockoutEnd.HasValue && LockoutEnd.Value > nowUtc;

    /// <summary>
    /// Registra un intento fallido. Al alcanzar el máximo, bloquea la cuenta durante <paramref name="lockoutDuration"/>
    /// y reinicia el contador: así, cuando el bloqueo expira, el atacante vuelve a tener solo N intentos.
    /// </summary>
    /// <returns><c>true</c> si este intento provocó el bloqueo.</returns>
    public bool RegisterFailedLogin(DateTime nowUtc, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        if (maxFailedAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFailedAttempts));
        }

        AccessFailedCount++;
        if (AccessFailedCount < maxFailedAttempts)
        {
            return false;
        }

        LockoutEnd = nowUtc.Add(lockoutDuration);
        AccessFailedCount = 0;
        return true;
    }

    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
        LastLoginAt = nowUtc;
    }

    public void LockUntil(DateTime untilUtc) => LockoutEnd = untilUtc;

    public void Unlock()
    {
        LockoutEnd = null;
        AccessFailedCount = 0;
    }

    public void SetPasswordHash(string passwordHash, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        PasswordChangedAt = nowUtc;
    }

    /// <summary>Reemplaza el hash sin marcar cambio de contraseña (re-hash por cambio de algoritmo/iteraciones).</summary>
    public void UpgradePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void ConfirmEmail() => EmailConfirmed = true;

    public void Rename(string fullName) => FullName = fullName.Trim();

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private void SetEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim();
        NormalizedEmail = Normalize(email);
    }
}
