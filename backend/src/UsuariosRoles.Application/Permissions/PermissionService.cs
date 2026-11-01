using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Application.Permissions;

public sealed class PermissionListQuery : PagedQuery
{
    public string? Module { get; set; }
}

public sealed record PermissionDto(Guid Id, string Code, string Module, string Description, IReadOnlyList<string> Roles);

/// <summary>Consulta del catálogo de permisos (los permisos se definen en código, no se crean desde la UI).</summary>
public sealed class PermissionService(IApplicationDbContext db)
{
    public async Task<PagedResult<PermissionDto>> ListAsync(PermissionListQuery query, CancellationToken cancellationToken = default)
    {
        var permissions = db.Permissions.AsNoTracking();
        if (query.SearchTerm is { } search)
        {
            var lower = search.ToLowerInvariant();
            permissions = permissions.Where(p => p.Code.Contains(lower) || p.Description.ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            var module = query.Module.Trim().ToLowerInvariant();
            permissions = permissions.Where(p => p.Module == module);
        }

        permissions = query.SortBy?.ToLowerInvariant() switch
        {
            "module" => permissions.OrderByDirection(p => p.Module, query.Descending).ThenBy(p => p.Code),
            _ => permissions.OrderByDirection(p => p.Code, query.Descending),
        };

        return await permissions
            .Select(p => new PermissionDto(
                p.Id,
                p.Code,
                p.Module,
                p.Description,
                p.RolePermissions.Select(rp => rp.Role.Name).OrderBy(n => n).ToList()))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListModulesAsync(CancellationToken cancellationToken = default) =>
        await db.Permissions.AsNoTracking().Select(p => p.Module).Distinct().OrderBy(m => m).ToListAsync(cancellationToken);
}
