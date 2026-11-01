using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Domain.Entities;

namespace UsuariosRoles.Application.Common;

/// <summary>
/// Puerto de persistencia. Application depende de esta interfaz y no del DbContext concreto,
/// así los casos de uso no conocen el proveedor (PostgreSQL, SQLite, ...).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<UserToken> UserTokens { get; }

    DbSet<AuditEvent> AuditEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Envío de correo. La implementación por defecto guarda los mensajes en un buzón de desarrollo;
/// en producción se reemplaza por SMTP o un proveedor (ver docs/reutilizar-plantilla.md).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public enum PasswordCheckResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordCheckResult Verify(User user, string password);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface IAccessTokenGenerator
{
    AccessToken Create(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions);
}

/// <summary>Genera tokens aleatorios criptográficamente seguros y su hash para persistirlos.</summary>
public interface ISecureTokenGenerator
{
    string Generate();

    string Hash(string rawToken);
}

/// <summary>Datos de la petición HTTP actual que necesitan los casos de uso (sin acoplarse a ASP.NET Core).</summary>
public interface IRequestContext
{
    Guid? UserId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }
}

public sealed record AuditEntry(string EventType, bool Succeeded, Guid? UserId = null, string? Email = null, string? Details = null);

public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
