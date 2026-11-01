using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UsuariosRoles.Application.Audit;
using UsuariosRoles.Application.Auth;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Application.Dashboard;
using UsuariosRoles.Application.Permissions;
using UsuariosRoles.Application.Roles;
using UsuariosRoles.Application.Security;
using UsuariosRoles.Application.Users;

namespace UsuariosRoles.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new PasswordPolicy(sp.GetRequiredService<IOptions<SecurityOptions>>().Value.Password));

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<UserTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<RoleService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<AuditQueryService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
