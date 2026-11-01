namespace UsuariosRoles.Domain.Entities;

/// <summary>
/// Permiso granular con formato "modulo.accion" (p. ej. "usuarios.leer").
/// El catálogo vive en código (<see cref="Security.Permissions"/>) y se sincroniza a la BD al iniciar.
/// </summary>
public sealed class Permission
{
    private Permission()
    {
    }

    public Permission(string code, string module, string description)
    {
        Id = Guid.NewGuid();
        Code = code;
        Module = module;
        Description = description;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Module { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    public void UpdateMetadata(string module, string description)
    {
        Module = module;
        Description = description;
    }
}
