using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using UsuariosRoles.Domain.Security;

namespace UsuariosRoles.Api.Authorization;

/// <summary>Requisito: el usuario debe tener el permiso indicado.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Comprueba que el JWT contenga el claim "permission" con el código exigido.</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(Permissions.ClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Crea políticas bajo demanda a partir de su nombre ("perm:usuarios.leer"), así no hay que registrar
/// una política por cada permiso: basta con usar <c>.RequirePermission("usuarios.leer")</c> en el endpoint.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string PolicyPrefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[PolicyPrefix.Length..];
            return new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

public static class PermissionEndpointExtensions
{
    /// <summary>Exige un permiso granular. Sin token → 401; con token pero sin permiso → 403.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        if (!Permissions.Exists(permission))
        {
            // Falla al arrancar si alguien protege un endpoint con un permiso que no está en el catálogo.
            throw new InvalidOperationException($"El permiso '{permission}' no existe en el catálogo.");
        }

        return builder.RequireAuthorization(PermissionPolicyProvider.PolicyPrefix + permission);
    }
}
