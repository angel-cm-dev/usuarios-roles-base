using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Entities;
using UsuariosRoles.Domain.Security;

namespace UsuariosRoles.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Crea los usuarios demo (solo datos ficticios). Desactívalo en producción real.</summary>
    public bool DemoUsers { get; set; } = true;

    public string DemoPassword { get; set; } = "Demo123!";
}

/// <summary>
/// Seed idempotente: se ejecuta en cada arranque.
/// 1) Sincroniza el catálogo de permisos del código con la BD (agrega nuevos, actualiza descripciones).
/// 2) Garantiza los roles de sistema y que "admin" tenga TODOS los permisos.
/// 3) Opcionalmente crea usuarios demo ficticios.
/// </summary>
public sealed class DataSeeder(AppDbContext db, IPasswordHasher passwordHasher, TimeProvider timeProvider, ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var existingPermissions = await db.Permissions.ToDictionaryAsync(p => p.Code, cancellationToken);
        foreach (var definition in Permissions.All)
        {
            if (existingPermissions.TryGetValue(definition.Code, out var permission))
            {
                permission.UpdateMetadata(definition.Module, definition.Description);
            }
            else
            {
                permission = new Permission(definition.Code, definition.Module, definition.Description);
                db.Permissions.Add(permission);
                existingPermissions[definition.Code] = permission;
                logger.LogInformation("Permiso nuevo sincronizado: {Code}", definition.Code);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var admin = await EnsureRoleAsync(SystemRoles.Admin, "Acceso total al sistema", now, cancellationToken);
        var usuario = await EnsureRoleAsync(SystemRoles.Usuario, "Rol por defecto de los usuarios registrados", now, cancellationToken);

        foreach (var permission in existingPermissions.Values.Where(p => admin.RolePermissions.All(rp => rp.PermissionId != p.Id)))
        {
            admin.RolePermissions.Add(new RolePermission { RoleId = admin.Id, PermissionId = permission.Id });
        }

        // Al rol "usuario" solo se le dan sus permisos por defecto cuando se crea (después lo gestiona el admin).
        if (usuario.RolePermissions.Count == 0 && db.Entry(usuario).State == EntityState.Added)
        {
            foreach (var code in SystemRoles.DefaultUsuarioPermissions)
            {
                usuario.RolePermissions.Add(new RolePermission { RoleId = usuario.Id, PermissionId = existingPermissions[code].Id });
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (options.DemoUsers)
        {
            await EnsureUserAsync("admin@demo.pe", "Administrador Demo", options.DemoPassword, admin, now, cancellationToken);
            await EnsureUserAsync("usuario@demo.pe", "Usuario Demo", options.DemoPassword, usuario, now, cancellationToken);
        }
    }

    private async Task<Role> EnsureRoleAsync(string name, string description, DateTime now, CancellationToken cancellationToken)
    {
        var role = await db.Roles.Include(r => r.RolePermissions).SingleOrDefaultAsync(r => r.Name == name, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role(name, description, isSystem: true, now);
        db.Roles.Add(role);
        return role;
    }

    private async Task EnsureUserAsync(string email, string fullName, string password, Role role, DateTime now, CancellationToken cancellationToken)
    {
        var normalized = User.Normalize(email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, cancellationToken))
        {
            return;
        }

        var user = new User(email, fullName, now);
        user.SetPasswordHash(passwordHasher.Hash(user, password), now);
        user.ConfirmEmail();
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Usuario demo creado: {Email}", email);
    }
}
