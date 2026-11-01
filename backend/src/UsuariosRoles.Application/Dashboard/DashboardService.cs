using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Application.Audit;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Security;

namespace UsuariosRoles.Application.Dashboard;

public sealed record DailyLoginStat(DateOnly Date, int Succeeded, int Failed);

public sealed record DashboardStatsDto(
    int TotalUsers,
    int ActiveUsers,
    int LockedUsers,
    int UnverifiedUsers,
    int TotalRoles,
    int ActiveSessions,
    int EventsLast24h,
    int FailedLoginsLast24h,
    IReadOnlyList<DailyLoginStat> LoginsLast7Days,
    IReadOnlyList<AuditEventDto> RecentEvents);

public sealed class DashboardService(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var last24h = now.AddHours(-24);
        var sevenDaysStart = now.Date.AddDays(-6);

        var totalUsers = await db.Users.CountAsync(cancellationToken);
        var activeUsers = await db.Users.CountAsync(u => u.IsActive, cancellationToken);
        var lockedUsers = await db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now, cancellationToken);
        var unverified = await db.Users.CountAsync(u => !u.EmailConfirmed, cancellationToken);
        var totalRoles = await db.Roles.CountAsync(cancellationToken);
        var activeSessions = await db.RefreshTokens.CountAsync(t => t.RevokedAt == null && t.ExpiresAt > now, cancellationToken);
        var events24h = await db.AuditEvents.CountAsync(e => e.OccurredAt >= last24h, cancellationToken);
        var failed24h = await db.AuditEvents.CountAsync(e => e.OccurredAt >= last24h && e.EventType == AuditEventTypes.LoginFailed, cancellationToken);

        // Se agrupa en memoria (volumen acotado a 7 días) para que la consulta sea portable entre PostgreSQL y SQLite.
        var loginEvents = await db.AuditEvents.AsNoTracking()
            .Where(e => e.OccurredAt >= sevenDaysStart &&
                        (e.EventType == AuditEventTypes.LoginSucceeded || e.EventType == AuditEventTypes.LoginFailed))
            .Select(e => new { e.OccurredAt, e.Succeeded })
            .ToListAsync(cancellationToken);

        var daily = Enumerable.Range(0, 7)
            .Select(offset => DateOnly.FromDateTime(sevenDaysStart.AddDays(offset)))
            .Select(day => new DailyLoginStat(
                day,
                loginEvents.Count(e => DateOnly.FromDateTime(e.OccurredAt) == day && e.Succeeded),
                loginEvents.Count(e => DateOnly.FromDateTime(e.OccurredAt) == day && !e.Succeeded)))
            .ToList();

        var recent = await db.AuditEvents.AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Take(8)
            .Select(e => new AuditEventDto(e.Id, e.OccurredAt, e.EventType, e.Succeeded, e.UserId, e.Email, e.ActorUserId, e.IpAddress, e.UserAgent, e.Details))
            .ToListAsync(cancellationToken);

        return new DashboardStatsDto(totalUsers, activeUsers, lockedUsers, unverified, totalRoles, activeSessions, events24h, failed24h, daily, recent);
    }
}
