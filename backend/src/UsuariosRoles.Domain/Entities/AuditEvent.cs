namespace UsuariosRoles.Domain.Entities;

/// <summary>Evento de seguridad auditable (append-only: nunca se actualiza ni se borra desde la app).</summary>
public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    public AuditEvent(
        string eventType,
        bool succeeded,
        DateTime occurredAtUtc,
        Guid? userId,
        string? email,
        string? ipAddress,
        string? userAgent,
        string? details,
        Guid? actorUserId)
    {
        Id = Guid.NewGuid();
        EventType = eventType;
        Succeeded = succeeded;
        OccurredAt = occurredAtUtc;
        UserId = userId;
        Email = email;
        IpAddress = ipAddress;
        UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent;
        Details = details;
        ActorUserId = actorUserId;
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public bool Succeeded { get; private set; }

    public DateTime OccurredAt { get; private set; }

    /// <summary>Usuario afectado por el evento (puede no existir, p. ej. login con email desconocido).</summary>
    public Guid? UserId { get; private set; }

    public string? Email { get; private set; }

    /// <summary>Usuario que ejecutó la acción (p. ej. el admin que bloqueó a otro usuario).</summary>
    public Guid? ActorUserId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? Details { get; private set; }
}
