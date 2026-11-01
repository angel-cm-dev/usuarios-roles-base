using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Application.Audit;

public sealed class AuditListQuery : PagedQuery
{
    public string? EventType { get; set; }

    public bool? Succeeded { get; set; }

    public Guid? UserId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}

public sealed record AuditEventDto(
    Guid Id,
    DateTime OccurredAt,
    string EventType,
    bool Succeeded,
    Guid? UserId,
    string? Email,
    Guid? ActorUserId,
    string? IpAddress,
    string? UserAgent,
    string? Details);

public sealed class AuditQueryService(IApplicationDbContext db)
{
    public async Task<PagedResult<AuditEventDto>> ListAsync(AuditListQuery query, CancellationToken cancellationToken = default)
    {
        var events = db.AuditEvents.AsNoTracking();

        if (query.SearchTerm is { } search)
        {
            var lower = search.ToLowerInvariant();
            events = events.Where(e =>
                (e.Email != null && e.Email.ToLower().Contains(lower)) ||
                (e.Details != null && e.Details.ToLower().Contains(lower)) ||
                (e.IpAddress != null && e.IpAddress.Contains(lower)));
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            events = events.Where(e => e.EventType == query.EventType);
        }

        if (query.Succeeded is { } succeeded)
        {
            events = events.Where(e => e.Succeeded == succeeded);
        }

        if (query.UserId is { } userId)
        {
            events = events.Where(e => e.UserId == userId || e.ActorUserId == userId);
        }

        if (query.From is { } from)
        {
            var fromUtc = DateTime.SpecifyKind(from.ToUniversalTime(), DateTimeKind.Utc);
            events = events.Where(e => e.OccurredAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = DateTime.SpecifyKind(to.ToUniversalTime(), DateTimeKind.Utc);
            events = events.Where(e => e.OccurredAt <= toUtc);
        }

        // Por defecto, lo más reciente primero.
        var ascending = string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        events = ascending ? events.OrderBy(e => e.OccurredAt) : events.OrderByDescending(e => e.OccurredAt);

        return await events
            .Select(e => new AuditEventDto(e.Id, e.OccurredAt, e.EventType, e.Succeeded, e.UserId, e.Email, e.ActorUserId, e.IpAddress, e.UserAgent, e.Details))
            .ToPagedResultAsync(query, cancellationToken);
    }
}
