using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Infrastructure.Email;
using UsuariosRoles.Infrastructure.Persistence;
using UsuariosRoles.Infrastructure.Security;

namespace UsuariosRoles.Infrastructure;

public static class DependencyInjection
{
    public const string PostgresProvider = "Postgres";
    public const string SqliteProvider = "Sqlite";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // La configuración se lee al resolver el DbContext (no al registrar), así las pruebas
        // pueden sobrescribirla con WebApplicationFactory.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var cfg = serviceProvider.GetRequiredService<IConfiguration>();
            var provider = cfg["Database:Provider"] ?? PostgresProvider;
            var connectionString = cfg.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Falta ConnectionStrings:Default.");

            if (string.Equals(provider, SqliteProvider, StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                // Reintentos ante fallos transitorios (p. ej. PostgreSQL aún arrancando en Docker).
                options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(5));
            }
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DataSeeder>();

        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));
        services.AddOptions<AppUrlsOptions>().Bind(configuration.GetSection(AppUrlsOptions.SectionName));
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= 32, "Jwt:Key debe tener al menos 32 bytes (256 bits).")
            .Validate(o => o.AccessTokenMinutes is > 0 and <= 60, "Jwt:AccessTokenMinutes debe estar entre 1 y 60.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();

        // Para usar SMTP real, reemplaza este registro (ver docs/reutilizar-plantilla.md).
        services.AddSingleton<DevMailbox>();
        services.AddSingleton<IEmailSender, DevMailboxEmailSender>();

        return services;
    }

    /// <summary>
    /// Crea/actualiza el esquema y ejecuta el seed.
    /// PostgreSQL: aplica migraciones EF Core. SQLite (dev sin Docker y pruebas): EnsureCreated.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.IsNpgsql())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync(scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value, cancellationToken);
    }
}
