using UsuariosRoles.Api.Authorization;
using UsuariosRoles.Api.Common;
using UsuariosRoles.Application.Audit;
using UsuariosRoles.Application.Dashboard;
using UsuariosRoles.Application.Permissions;
using UsuariosRoles.Application.Roles;
using UsuariosRoles.Application.Users;
using UsuariosRoles.Domain.Security;
using UsuariosRoles.Infrastructure.Email;
using P = UsuariosRoles.Domain.Security.Permissions;

namespace UsuariosRoles.Api.Endpoints;

/// <summary>Endpoints del panel de administración. Cada uno exige un permiso granular.</summary>
internal static class AdminEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Usuarios").RequireAuthorization();

        group.MapGet("/", async ([AsParameters] UserListQuery query, UserService users, CancellationToken ct) =>
                TypedResults.Ok(await users.ListAsync(query, ct)))
            .RequirePermission(P.Usuarios.Leer)
            .WithSummary("Lista paginada. Filtros: search, status (active|inactive|locked|unverified), role. Orden: sortBy, sortDir.");

        group.MapGet("/{id:guid}", async (Guid id, UserService users, CancellationToken ct) =>
                TypedResults.Ok(await users.GetAsync(id, ct)))
            .RequirePermission(P.Usuarios.Leer);

        group.MapPost("/", async (CreateUserRequest request, UserService users, CancellationToken ct) =>
            {
                var created = await users.CreateAsync(request, ct);
                return TypedResults.Created($"/api/users/{created.Id}", created);
            })
            .Validate<CreateUserRequest>()
            .RequirePermission(P.Usuarios.Crear);

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UserService users, CancellationToken ct) =>
                TypedResults.Ok(await users.UpdateAsync(id, request, ct)))
            .Validate<UpdateUserRequest>()
            .RequirePermission(P.Usuarios.Editar);

        group.MapPut("/{id:guid}/roles", async (Guid id, SetUserRolesRequest request, UserService users, CancellationToken ct) =>
                TypedResults.Ok(await users.SetRolesAsync(id, request, ct)))
            .Validate<SetUserRolesRequest>()
            .RequirePermission(P.Usuarios.AsignarRoles);

        group.MapPost("/{id:guid}/lock", async (Guid id, LockUserRequest request, UserService users, CancellationToken ct) =>
                TypedResults.Ok(await users.LockAsync(id, request, ct)))
            .Validate<LockUserRequest>()
            .RequirePermission(P.Usuarios.Bloquear)
            .WithSummary("Bloquea al usuario (minutes opcional; sin valor = indefinido) y revoca sus sesiones.");

        group.MapPost("/{id:guid}/unlock", async (Guid id, UserService users, CancellationToken ct) =>
                TypedResults.Ok(await users.UnlockAsync(id, ct)))
            .RequirePermission(P.Usuarios.Bloquear);

        group.MapPost("/{id:guid}/revoke-sessions", async (Guid id, UserService users, CancellationToken ct) =>
                TypedResults.Ok(new { revoked = await users.RevokeSessionsAsync(id, ct) }))
            .RequirePermission(P.Usuarios.Bloquear);
    }

    public static void MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles").WithTags("Roles").RequireAuthorization();

        group.MapGet("/", async ([AsParameters] RoleListQuery query, RoleService roles, CancellationToken ct) =>
                TypedResults.Ok(await roles.ListAsync(query, ct)))
            .RequirePermission(P.Roles.Leer);

        group.MapGet("/{id:guid}", async (Guid id, RoleService roles, CancellationToken ct) =>
                TypedResults.Ok(await roles.GetAsync(id, ct)))
            .RequirePermission(P.Roles.Leer);

        group.MapPost("/", async (CreateRoleRequest request, RoleService roles, CancellationToken ct) =>
            {
                var created = await roles.CreateAsync(request, ct);
                return TypedResults.Created($"/api/roles/{created.Id}", created);
            })
            .Validate<CreateRoleRequest>()
            .RequirePermission(P.Roles.Crear);

        group.MapPut("/{id:guid}", async (Guid id, UpdateRoleRequest request, RoleService roles, CancellationToken ct) =>
                TypedResults.Ok(await roles.UpdateAsync(id, request, ct)))
            .Validate<UpdateRoleRequest>()
            .RequirePermission(P.Roles.Editar);

        group.MapPut("/{id:guid}/permissions", async (Guid id, SetRolePermissionsRequest request, RoleService roles, CancellationToken ct) =>
                TypedResults.Ok(await roles.SetPermissionsAsync(id, request, ct)))
            .Validate<SetRolePermissionsRequest>()
            .RequirePermission(P.Roles.Editar)
            .WithSummary("Reemplaza el conjunto de permisos del rol. Los usuarios lo verán al renovar su access token.");

        group.MapDelete("/{id:guid}", async (Guid id, RoleService roles, CancellationToken ct) =>
            {
                await roles.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .RequirePermission(P.Roles.Eliminar);
    }

    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var permissions = app.MapGroup("/api/permissions").WithTags("Permisos").RequireAuthorization();
        permissions.MapGet("/", async ([AsParameters] PermissionListQuery query, PermissionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(query, ct)))
            .RequirePermission(P.Permisos.Leer);
        permissions.MapGet("/modules", async (PermissionService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListModulesAsync(ct)))
            .RequirePermission(P.Permisos.Leer);

        var audit = app.MapGroup("/api/audit").WithTags("Auditoría").RequireAuthorization();
        audit.MapGet("/", async ([AsParameters] AuditListQuery query, AuditQueryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(query, ct)))
            .RequirePermission(P.Auditoria.Leer)
            .WithSummary("Eventos de seguridad. Filtros: search, eventType, succeeded, userId, from, to.");
        audit.MapGet("/event-types", () => TypedResults.Ok(AuditEventTypes.All))
            .RequirePermission(P.Auditoria.Leer);

        app.MapGet("/api/dashboard/stats", async (DashboardService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetStatsAsync(ct)))
            .WithTags("Dashboard")
            .RequirePermission(P.Dashboard.Ver);
    }

    /// <summary>
    /// Buzón de desarrollo: permite ver los correos "enviados" (verificación, reset).
    /// Solo se registra si Features:DevMailbox = true. NUNCA lo actives con un IEmailSender real en producción.
    /// </summary>
    public static void MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev").WithTags("Dev").AllowAnonymous();
        group.MapGet("/mailbox", (string? to, DevMailbox mailbox) => TypedResults.Ok(mailbox.List(to)))
            .WithSummary("Correos simulados más recientes (opcionalmente filtrados por destinatario).");
        group.MapDelete("/mailbox", (DevMailbox mailbox) =>
        {
            mailbox.Clear();
            return TypedResults.NoContent();
        });
    }
}
