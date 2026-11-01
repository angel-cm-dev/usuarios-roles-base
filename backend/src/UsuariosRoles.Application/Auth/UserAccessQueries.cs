using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;

namespace UsuariosRoles.Application.Auth;

/// <summary>Consultas reutilizables para obtener roles y permisos efectivos de un usuario.</summary>
public static class UserAccessQueries
{
    public static async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> GetAccessAsync(
        this IApplicationDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var roles = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        // Permisos efectivos = unión de los permisos de todos sus roles.
        var permissions = await db.RolePermissions
            .Where(rp => rp.Role.UserRoles.Any(ur => ur.UserId == userId))
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return (roles, permissions);
    }

    public static async Task<UserProfileDto> GetProfileAsync(this IApplicationDbContext db, User user, CancellationToken cancellationToken)
    {
        var (roles, permissions) = await db.GetAccessAsync(user.Id, cancellationToken);
        return new UserProfileDto(user.Id, user.Email, user.FullName, user.EmailConfirmed, roles, permissions, user.CreatedAt, user.LastLoginAt);
    }
}
