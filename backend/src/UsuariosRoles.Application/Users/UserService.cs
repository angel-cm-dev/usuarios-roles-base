using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Application.Auth;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;
using UsuariosRoles.Domain.Security;

namespace UsuariosRoles.Application.Users;

/// <summary>Gestión de usuarios desde el panel de administración.</summary>
public sealed class UserService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    RefreshTokenService refreshTokens,
    IAuditLogger audit,
    IRequestContext requestContext,
    TimeProvider timeProvider)
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<UserListItemDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var users = db.Users.AsNoTracking();

        if (query.SearchTerm is { } search)
        {
            var upper = search.ToUpperInvariant();
            var lower = search.ToLowerInvariant();
            users = users.Where(u => u.NormalizedEmail.Contains(upper) || u.FullName.ToLower().Contains(lower));
        }

        users = query.Status?.ToLowerInvariant() switch
        {
            "active" => users.Where(u => u.IsActive),
            "inactive" => users.Where(u => !u.IsActive),
            "locked" => users.Where(u => u.LockoutEnd != null && u.LockoutEnd > now),
            "unverified" => users.Where(u => !u.EmailConfirmed),
            _ => users,
        };

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = Role.Normalize(query.Role);
            users = users.Where(u => u.UserRoles.Any(ur => ur.Role.Name == role));
        }

        users = query.SortBy?.ToLowerInvariant() switch
        {
            "email" => users.OrderByDirection(u => u.Email, query.Descending),
            "fullname" => users.OrderByDirection(u => u.FullName, query.Descending),
            "lastloginat" => users.OrderByDirection(u => u.LastLoginAt, query.Descending),
            _ => users.OrderByDirection(u => u.CreatedAt, query.SortDir is null || query.Descending),
        };

        return await users
            .Select(u => new UserListItemDto(
                u.Id,
                u.Email,
                u.FullName,
                u.IsActive,
                u.EmailConfirmed,
                u.LockoutEnd != null && u.LockoutEnd > now,
                u.LockoutEnd,
                u.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList(),
                u.CreatedAt,
                u.LastLoginAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<UserDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("Usuario", id);

        var now = Now;
        var (_, permissions) = await db.GetAccessAsync(id, cancellationToken);
        var activeSessions = await db.RefreshTokens.CountAsync(t => t.UserId == id && t.RevokedAt == null && t.ExpiresAt > now, cancellationToken);

        return new UserDetailDto(
            user.Id,
            user.Email,
            user.FullName,
            user.IsActive,
            user.EmailConfirmed,
            user.IsLockedOut(now),
            user.LockoutEnd,
            user.AccessFailedCount,
            user.UserRoles.Select(ur => new RoleRefDto(ur.RoleId, ur.Role.Name)).OrderBy(r => r.Name).ToList(),
            permissions,
            activeSessions,
            user.CreatedAt,
            user.LastLoginAt,
            user.PasswordChangedAt);
    }

    public async Task<UserDetailDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = User.Normalize(request.Email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, cancellationToken))
        {
            // En el panel de admin sí se informa el conflicto: el actor está autenticado y autorizado.
            throw new ConflictException("email_taken", "Ya existe un usuario con ese email.");
        }

        var user = new User(request.Email, request.FullName, Now);
        user.SetPasswordHash(passwordHasher.Hash(user, request.Password), Now);
        if (request.EmailConfirmed)
        {
            user.ConfirmEmail();
        }

        var roleIds = request.RoleIds is { Count: > 0 }
            ? request.RoleIds.Distinct().ToList()
            : await db.Roles.Where(r => r.Name == SystemRoles.Usuario).Select(r => r.Id).ToListAsync(cancellationToken);
        await EnsureRolesExistAsync(roleIds, cancellationToken);
        foreach (var roleId in roleIds)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.UserCreated, true, user.Id, user.Email), cancellationToken);
        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task<UserDetailDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(id, cancellationToken);
        if (!request.IsActive && id == requestContext.UserId)
        {
            throw new BusinessRuleException("cannot_deactivate_self", "No puedes desactivar tu propia cuenta.");
        }

        if (!request.IsActive && user.IsActive && await IsLastActiveAdminAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("last_admin", "No puedes desactivar al último administrador activo.");
        }

        var wasActive = user.IsActive;
        user.Rename(request.FullName);
        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken);
        if (wasActive && !request.IsActive)
        {
            await refreshTokens.RevokeAllForUserAsync(id, "account-deactivated", cancellationToken);
        }

        await audit.LogAsync(new AuditEntry(AuditEventTypes.UserUpdated, true, id, user.Email, $"activo={request.IsActive}"), cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<UserDetailDto> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("Usuario", id);

        var requested = request.RoleIds.Distinct().ToList();
        await EnsureRolesExistAsync(requested, cancellationToken);

        var adminRoleId = await db.Roles.Where(r => r.Name == SystemRoles.Admin).Select(r => r.Id).SingleAsync(cancellationToken);
        var removingAdmin = user.UserRoles.Any(ur => ur.RoleId == adminRoleId) && !requested.Contains(adminRoleId);
        if (removingAdmin && await IsLastActiveAdminAsync(id, cancellationToken))
        {
            // Evita dejar el sistema sin nadie capaz de administrarlo.
            throw new BusinessRuleException("last_admin", "No puedes quitar el rol admin al último administrador activo.");
        }

        foreach (var link in user.UserRoles.Where(ur => !requested.Contains(ur.RoleId)).ToList())
        {
            user.UserRoles.Remove(link);
        }

        foreach (var roleId in requested.Where(rid => user.UserRoles.All(ur => ur.RoleId != rid)))
        {
            user.UserRoles.Add(new UserRole { UserId = id, RoleId = roleId });
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.UserRolesChanged, true, id, user.Email, $"roles={string.Join(',', requested)}"), cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<UserDetailDto> LockAsync(Guid id, LockUserRequest request, CancellationToken cancellationToken = default)
    {
        if (id == requestContext.UserId)
        {
            throw new BusinessRuleException("cannot_lock_self", "No puedes bloquear tu propia cuenta.");
        }

        var user = await FindAsync(id, cancellationToken);
        if (await IsLastActiveAdminAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("last_admin", "No puedes bloquear al último administrador activo.");
        }

        // Sin minutos = bloqueo "indefinido" (100 años) hasta que un admin lo desbloquee.
        var until = request.Minutes is { } minutes ? Now.AddMinutes(minutes) : Now.AddYears(100);
        user.LockUntil(until);
        await db.SaveChangesAsync(cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(id, "locked-by-admin", cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.UserLocked, true, id, user.Email, $"hasta={until:O}"), cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<UserDetailDto> UnlockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(id, cancellationToken);
        user.Unlock();
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.UserUnlocked, true, id, user.Email), cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<int> RevokeSessionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(id, cancellationToken);
        var count = await refreshTokens.RevokeAllForUserAsync(id, "revoked-by-admin", cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.SessionsRevoked, true, id, user.Email, $"{count} sesiones"), cancellationToken);
        return count;
    }

    private async Task<User> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken)
        ?? throw new NotFoundException("Usuario", id);

    private async Task EnsureRolesExistAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var found = await db.Roles.CountAsync(r => roleIds.Contains(r.Id), cancellationToken);
        if (found != roleIds.Count)
        {
            throw RequestValidationException.For("roleIds", "Uno o más roles no existen.");
        }
    }

    private async Task<bool> IsLastActiveAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeAdmins = await db.Users
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == SystemRoles.Admin))
            .Select(u => u.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        return activeAdmins.Count == 1 && activeAdmins[0] == userId;
    }
}
