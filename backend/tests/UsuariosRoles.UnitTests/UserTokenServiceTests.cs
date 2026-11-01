using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using UsuariosRoles.Application.Auth;
using UsuariosRoles.Domain.Entities;
using UsuariosRoles.Infrastructure.Security;

namespace UsuariosRoles.UnitTests;

public sealed class UserTokenServiceTests : IDisposable
{
    private readonly TestDatabase database = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
    private readonly UserTokenService service;

    public UserTokenServiceTests() =>
        service = new UserTokenService(database.Context, new SecureTokenGenerator(), time);

    [Fact]
    public async Task Token_valido_se_consume_una_sola_vez()
    {
        var user = await database.AddUserAsync();
        var raw = await service.IssueAsync(user.Id, UserTokenPurpose.PasswordReset, TimeSpan.FromMinutes(30));

        var first = await service.ConsumeAsync(raw, UserTokenPurpose.PasswordReset);
        await database.Context.SaveChangesAsync();
        var second = await service.ConsumeAsync(raw, UserTokenPurpose.PasswordReset);

        Assert.Equal(user.Id, first?.Id);
        Assert.Null(second);
    }

    [Fact]
    public async Task Token_expirado_no_sirve()
    {
        var user = await database.AddUserAsync();
        var raw = await service.IssueAsync(user.Id, UserTokenPurpose.PasswordReset, TimeSpan.FromMinutes(30));

        time.Advance(TimeSpan.FromMinutes(31));

        Assert.Null(await service.ConsumeAsync(raw, UserTokenPurpose.PasswordReset));
    }

    [Fact]
    public async Task No_sirve_para_otro_proposito()
    {
        var user = await database.AddUserAsync();
        var raw = await service.IssueAsync(user.Id, UserTokenPurpose.EmailVerification, TimeSpan.FromHours(24));

        Assert.Null(await service.ConsumeAsync(raw, UserTokenPurpose.PasswordReset));
    }

    [Fact]
    public async Task Emitir_uno_nuevo_invalida_los_anteriores()
    {
        var user = await database.AddUserAsync();
        var old = await service.IssueAsync(user.Id, UserTokenPurpose.PasswordReset, TimeSpan.FromMinutes(30));
        var latest = await service.IssueAsync(user.Id, UserTokenPurpose.PasswordReset, TimeSpan.FromMinutes(30));

        Assert.Null(await service.ConsumeAsync(old, UserTokenPurpose.PasswordReset));
        Assert.NotNull(await service.ConsumeAsync(latest, UserTokenPurpose.PasswordReset));
    }

    [Fact]
    public async Task En_la_bd_solo_se_guarda_el_hash()
    {
        var user = await database.AddUserAsync();
        var raw = await service.IssueAsync(user.Id, UserTokenPurpose.EmailVerification, TimeSpan.FromHours(1));

        Assert.False(await database.Context.UserTokens.AnyAsync(t => t.TokenHash == raw));
    }

    [Fact]
    public async Task Consumo_concurrente_solo_gana_una_vez()
    {
        var user = await database.AddUserAsync();
        var raw = await service.IssueAsync(user.Id, UserTokenPurpose.PasswordReset, TimeSpan.FromMinutes(30));

        // Dos "peticiones" con contextos distintos leen el token antes de que la otra guarde.
        using var ctxA = database.CreateContext();
        using var ctxB = database.CreateContext();
        var serviceA = new UserTokenService(ctxA, new SecureTokenGenerator(), time);
        var serviceB = new UserTokenService(ctxB, new SecureTokenGenerator(), time);
        Assert.NotNull(await serviceA.ConsumeAsync(raw, UserTokenPurpose.PasswordReset));
        Assert.NotNull(await serviceB.ConsumeAsync(raw, UserTokenPurpose.PasswordReset));

        await ctxA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctxB.SaveChangesAsync());
    }

    public void Dispose() => database.Dispose();
}
