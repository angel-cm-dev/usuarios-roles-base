namespace UsuariosRoles.Domain.Security;

public sealed record PermissionDefinition(string Code, string Module, string Description);

/// <summary>
/// Catálogo de permisos de la aplicación (fuente de verdad).
/// Para agregar un permiso: declara la constante, añádela a <see cref="All"/> y protege el endpoint
/// con <c>.RequirePermission(...)</c>. El seeder lo crea en la BD y lo asigna al rol admin al iniciar.
/// </summary>
public static class Permissions
{
    /// <summary>Tipo de claim usado en el JWT para cada permiso.</summary>
    public const string ClaimType = "permission";

    public static class Usuarios
    {
        public const string Leer = "usuarios.leer";
        public const string Crear = "usuarios.crear";
        public const string Editar = "usuarios.editar";
        public const string Bloquear = "usuarios.bloquear";
        public const string AsignarRoles = "usuarios.asignar-roles";
    }

    public static class Roles
    {
        public const string Leer = "roles.leer";
        public const string Crear = "roles.crear";
        public const string Editar = "roles.editar";
        public const string Eliminar = "roles.eliminar";
    }

    public static class Permisos
    {
        public const string Leer = "permisos.leer";
    }

    public static class Auditoria
    {
        public const string Leer = "auditoria.leer";
    }

    public static class Dashboard
    {
        public const string Ver = "dashboard.ver";
    }

    public static class Perfil
    {
        public const string Editar = "perfil.editar";
    }

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(Usuarios.Leer, "usuarios", "Ver el listado y detalle de usuarios"),
        new(Usuarios.Crear, "usuarios", "Crear usuarios desde el panel"),
        new(Usuarios.Editar, "usuarios", "Editar datos y estado (activo/inactivo) de usuarios"),
        new(Usuarios.Bloquear, "usuarios", "Bloquear, desbloquear y cerrar sesiones de usuarios"),
        new(Usuarios.AsignarRoles, "usuarios", "Asignar o quitar roles a usuarios"),
        new(Roles.Leer, "roles", "Ver roles y sus permisos"),
        new(Roles.Crear, "roles", "Crear roles"),
        new(Roles.Editar, "roles", "Editar roles y sus permisos"),
        new(Roles.Eliminar, "roles", "Eliminar roles no usados"),
        new(Permisos.Leer, "permisos", "Ver el catálogo de permisos"),
        new(Auditoria.Leer, "auditoria", "Consultar la auditoría de seguridad"),
        new(Dashboard.Ver, "dashboard", "Ver los indicadores del panel"),
        new(Perfil.Editar, "perfil", "Editar el propio perfil"),
    ];

    public static bool Exists(string code) => All.Any(p => p.Code == code);
}

/// <summary>Roles creados por el sistema. No se pueden eliminar.</summary>
public static class SystemRoles
{
    public const string Admin = "admin";
    public const string Usuario = "usuario";

    /// <summary>Permisos con los que nace el rol "usuario" (el admin recibe siempre todo el catálogo).</summary>
    public static IReadOnlyList<string> DefaultUsuarioPermissions { get; } = [Permissions.Perfil.Editar];
}
