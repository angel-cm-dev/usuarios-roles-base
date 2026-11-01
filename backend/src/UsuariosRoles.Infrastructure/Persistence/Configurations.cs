using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UsuariosRoles.Domain.Entities;

namespace UsuariosRoles.Infrastructure.Persistence;

// Configuraciones Fluent API. Nombres de tabla en snake_case para seguir la convención de PostgreSQL.

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.HasIndex(u => u.NormalizedEmail).IsUnique();
        b.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        b.HasIndex(u => u.CreatedAt);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.HasKey(r => r.Id);
        b.Property(r => r.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(r => r.Name).IsUnique();
        b.Property(r => r.Description).HasMaxLength(250);
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions");
        b.HasKey(p => p.Id);
        b.Property(p => p.Code).HasMaxLength(100).IsRequired();
        b.HasIndex(p => p.Code).IsUnique();
        b.Property(p => p.Module).HasMaxLength(50).IsRequired();
        b.Property(p => p.Description).HasMaxLength(250).IsRequired();
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasKey(ur => new { ur.UserId, ur.RoleId });
        b.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);

        // Restrict: un rol con usuarios no se borra en cascada por accidente (la app lo valida antes).
        b.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        b.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasIndex(t => t.FamilyId);
        b.HasIndex(t => new { t.UserId, t.RevokedAt });
        b.Property(t => t.CreatedByIp).HasMaxLength(64);
        b.Property(t => t.RevokedReason).HasMaxLength(64);

        // Token de concurrencia: si dos peticiones rotan el mismo token en paralelo, solo una lo logra.
        b.Property(t => t.RevokedAt).IsConcurrencyToken();
        b.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> b)
    {
        b.ToTable("user_tokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasIndex(t => new { t.UserId, t.Purpose });
        b.Property(t => t.Purpose).HasConversion<string>().HasMaxLength(32);

        // Garantiza el "un solo uso" incluso con peticiones simultáneas.
        b.Property(t => t.ConsumedAt).IsConcurrencyToken();
        b.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events");
        b.HasKey(e => e.Id);
        b.Property(e => e.EventType).HasMaxLength(64).IsRequired();
        b.Property(e => e.Email).HasMaxLength(256);
        b.Property(e => e.IpAddress).HasMaxLength(64);
        b.Property(e => e.UserAgent).HasMaxLength(512);
        b.Property(e => e.Details).HasMaxLength(1000);
        b.HasIndex(e => e.OccurredAt);
        b.HasIndex(e => new { e.EventType, e.OccurredAt });
        b.HasIndex(e => e.UserId);

        // Sin FK hacia users: la auditoría debe sobrevivir aunque el usuario se elimine.
    }
}
