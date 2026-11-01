using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UsuariosRoles.Infrastructure.Persistence;

/// <summary>
/// Usado solo por "dotnet ef" para generar migraciones de PostgreSQL sin arrancar la API.
/// No necesita una base de datos real para crear migraciones (solo para aplicarlas).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=usuarios_roles;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        return new AppDbContext(options);
    }
}
