using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Domain.Entities;
using UsuariosRoles.Infrastructure.Persistence;

namespace UsuariosRoles.UnitTests;

/// <summary>
/// Base de datos SQLite en memoria (relacional real, sin Docker). Vive mientras la conexión esté abierta.
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Context = CreateContext();
        Context.Database.EnsureCreated();
    }

    public AppDbContext Context { get; }

    /// <summary>Contexto nuevo sobre la misma BD (simula otra petición, sin entidades en caché).</summary>
    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    public async Task<User> AddUserAsync(string email = "ana@demo.pe")
    {
        var user = new User(email, "Ana Prueba", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        user.SetPasswordHash("hash-no-relevante", user.CreatedAt);
        user.ConfirmEmail();
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    public void Dispose()
    {
        Context.Dispose();
        connection.Dispose();
    }
}
