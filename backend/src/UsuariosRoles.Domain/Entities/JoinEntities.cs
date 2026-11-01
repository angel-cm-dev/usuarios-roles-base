namespace UsuariosRoles.Domain.Entities;

/// <summary>Relación N:M usuario-rol.</summary>
public sealed class UserRole
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;
}

/// <summary>Relación N:M rol-permiso.</summary>
public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
