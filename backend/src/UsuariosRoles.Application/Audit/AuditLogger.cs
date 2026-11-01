using Microsoft.Extensions.Logging;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;

namespace UsuariosRoles.Application.Audit;

/// <summary>
/// Registra eventos de seguridad en la tabla de auditoría y también en el log estructurado
/// (útil para enviarlos a un SIEM). Se guarda de inmediato para que los intentos fallidos queden
/// registrados aunque la operación termine en excepción.
/// </summary>
public sealed class AuditLogger(
    IApplicationDbContext db,
    IRequestContext requestContext,
    TimeProvider timeProvider,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEvent(
            entry.EventType,
            entry.Succeeded,
            timeProvider.GetUtcNow().UtcDateTime,
            entry.UserId,
            entry.Email,
            requestContext.IpAddress,
            requestContext.UserAgent,
            entry.Details,
            requestContext.UserId);

        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Audit {EventType} succeeded={Succeeded} user={UserId} actor={ActorUserId} ip={IpAddress}",
            entry.EventType,
            entry.Succeeded,
            entry.UserId,
            requestContext.UserId,
            requestContext.IpAddress);
    }
}
