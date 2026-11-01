namespace UsuariosRoles.Domain.Entities;

/// <summary>Rol: agrupa permisos. Los roles de sistema (admin, usuario) no se pueden eliminar.</summary>
public sealed class Role
{
    private Role()
    {
    }

    public Role(string name, string? description, bool isSystem, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        Rename(name);
        Description = description?.Trim();
        IsSystem = isSystem;
        CreatedAt = createdAtUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>Nombre en minúsculas, usado como claim "role" en el JWT.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsSystem { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    public static string Normalize(string name) => name.Trim().ToLowerInvariant();

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = Normalize(name);
    }

    public void Describe(string? description) => Description = description?.Trim();
}
