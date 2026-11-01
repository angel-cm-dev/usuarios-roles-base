using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;
using UsuariosRoles.Domain.Security;

namespace UsuariosRoles.Application.Roles;

/// <summary>Gestión de roles y de los permisos asignados a cada rol.</summary>
public sealed class RoleService(IApplicationDbContext db, IAuditLogger audit, TimeProvider timeProvider)
{
    public async Task<PagedResult<RoleListItemDto>> ListAsync(RoleListQuery query, CancellationToken cancellationToken = default)
    {
        var roles = db.Roles.AsNoTracking();
        if (query.SearchTerm is { } search)
        {
            var lower = search.ToLowerInvariant();
            roles = roles.Where(r => r.Name.Contains(lower) || (r.Description != null && r.Description.ToLower().Contains(lower)));
        }

        roles = query.SortBy?.ToLowerInvariant() switch
        {
            "createdat" => roles.OrderByDirection(r => r.CreatedAt, query.Descending),
            "usercount" => roles.OrderByDirection(r => r.UserRoles.Count, query.Descending),
            _ => roles.OrderByDirection(r => r.Name, query.Descending),
        };

        return await roles.Select(Projection()).ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<RoleListItemDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Roles.AsNoTracking().Where(r => r.Id == id).Select(Projection()).SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Rol", id);

    public async Task<RoleListItemDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var name = Role.Normalize(request.Name);
        await EnsureNameAvailableAsync(name, null, cancellationToken);

        var role = new Role(name, request.Description, isSystem: false, timeProvider.GetUtcNow().UtcDateTime);
        foreach (var permission in await ResolvePermissionsAsync(request.Permissions ?? [], cancellationToken))
        {
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.RoleCreated, true, Details: $"rol={role.Name}"), cancellationToken);
        return await GetAsync(role.Id, cancellationToken);
    }

    public async Task<RoleListItemDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await FindAsync(id, cancellationToken);
        var name = Role.Normalize(request.Name);
        if (role.IsSystem && name != role.Name)
        {
            // El código depende de estos nombres (seed, registro); renombrarlos rompería la app.
            throw new BusinessRuleException("system_role", "No se puede renombrar un rol de sistema.");
        }

        await EnsureNameAvailableAsync(name, id, cancellationToken);
        role.Rename(name);
        role.Describe(request.Description);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.RoleUpdated, true, Details: $"rol={role.Name}"), cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<RoleListItemDto> SetPermissionsAsync(Guid id, SetRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.Include(r => r.RolePermissions).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Rol", id);
        if (role.Name == SystemRoles.Admin)
        {
            // El rol admin siempre tiene el catálogo completo (el seed lo re-sincroniza al iniciar).
            throw new BusinessRuleException("admin_role_locked", "Los permisos del rol admin no se pueden modificar.");
        }

        var permissions = await ResolvePermissionsAsync(request.Permissions, cancellationToken);
        var wanted = permissions.Select(p => p.Id).ToHashSet();

        foreach (var link in role.RolePermissions.Where(rp => !wanted.Contains(rp.PermissionId)).ToList())
        {
            role.RolePermissions.Remove(link);
        }

        foreach (var permissionId in wanted.Where(pid => role.RolePermissions.All(rp => rp.PermissionId != pid)))
        {
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        }

        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(
            new AuditEntry(AuditEventTypes.RolePermissionsChanged, true, Details: $"rol={role.Name}; permisos={string.Join(',', permissions.Select(p => p.Code))}"),
            cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await FindAsync(id, cancellationToken);
        if (role.IsSystem)
        {
            throw new BusinessRuleException("system_role", "No se puede eliminar un rol de sistema.");
        }

        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id, cancellationToken))
        {
            throw new ConflictException("role_in_use", "El rol tiene usuarios asignados. Quítalo de los usuarios antes de eliminarlo.");
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.RoleDeleted, true, Details: $"rol={role.Name}"), cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<Role, RoleListItemDto>> Projection() =>
        r => new RoleListItemDto(
            r.Id,
            r.Name,
            r.Description,
            r.IsSystem,
            r.UserRoles.Count,
            r.RolePermissions.Select(rp => rp.Permission.Code).OrderBy(c => c).ToList(),
            r.CreatedAt);

    private async Task<Role> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Roles.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new NotFoundException("Rol", id);

    private async Task EnsureNameAvailableAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.Roles.AnyAsync(r => r.Name == name && r.Id != exceptId, cancellationToken))
        {
            throw new ConflictException("role_name_taken", $"Ya existe un rol llamado '{name}'.");
        }
    }

    private async Task<List<Permission>> ResolvePermissionsAsync(IReadOnlyList<string> codes, CancellationToken cancellationToken)
    {
        var distinct = codes.Select(c => c.Trim()).Distinct().ToList();
        var found = await db.Permissions.Where(p => distinct.Contains(p.Code)).ToListAsync(cancellationToken);
        var unknown = distinct.Except(found.Select(p => p.Code)).ToList();
        if (unknown.Count > 0)
        {
            throw RequestValidationException.For("permissions", $"Permisos desconocidos: {string.Join(", ", unknown)}.");
        }

        return found;
    }
}
