using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using UsuariosRoles.Application.Auth;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Infrastructure.Security;

namespace UsuariosRoles.UnitTests;

public sealed class RefreshTokenServiceTests : IDisposable
{
    private readonly TestDatabase database = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
    private readonly SecureTokenGenerator generator = new();
    private readonly RefreshTokenService service;

    public RefreshTokenServiceTests() =>
        service = new RefreshTokenService(database.Context, generator, time, Options.Create(new SecurityOptions()));

    [Fact]
    public async Task Emitir_guarda_solo_el_hash()
    {
        var user = await database.AddUserAsync();
        var issued = service.Issue(user.Id, "127.0.0.1");
        await database.Context.SaveChangesAsync();

        var stored = await database.Context.RefreshTokens.SingleAsync();
        Assert.NotEqual(issued.RawToken, stored.TokenHash);
        Assert.Equal(generator.Hash(issued.RawToken), stored.TokenHash);
    }

    [Fact]
    public async Task Rotar_revoca_el_anterior_y_emite_uno_de_la_misma_familia()
    {
        var user = await database.AddUserAsync();
        var first = service.Issue(user.Id, null);
        await database.Context.SaveChangesAsync();

        var result = await service.RotateAsync(first.RawToken, null);

        Assert.Equal(RefreshTokenStatus.Success, result.Status);
        Assert.Equal(first.FamilyId, result.NewToken!.FamilyId);
        Assert.NotEqual(first.RawToken, result.NewToken.RawToken);

        var old = await database.Context.RefreshTokens.SingleAsync(t => t.TokenHash == generator.Hash(first.RawToken));
        Assert.Equal("rotated", old.RevokedReason);
        Assert.NotNull(old.ReplacedByTokenId);
    }

    [Fact]
    public async Task Reusar_un_token_rotado_revoca_toda_la_familia()
    {
        var user = await database.AddUserAsync();
        var first = service.Issue(user.Id, null);
        await database.Context.SaveChangesAsync();
        var second = (await service.RotateAsync(first.RawToken, null)).NewToken!;

        // Un atacante (o el usuario) presenta el token viejo ya rotado.
        var reuse = await service.RotateAsync(first.RawToken, null);
        Assert.Equal(RefreshTokenStatus.ReuseDetected, reuse.Status);
        Assert.Equal(user.Id, reuse.UserId);

        // El token "vigente" de la familia también quedó revocado.
        var afterReuse = await service.RotateAsync(second.RawToken, null);
        Assert.Equal(RefreshTokenStatus.ReuseDetected, afterReuse.Status);

        using var fresh = database.CreateContext();
        Assert.All(await fresh.RefreshTokens.ToListAsync(), t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task La_revocacion_por_reuso_no_afecta_otras_familias()
    {
        var user = await database.AddUserAsync();
        var laptop = service.Issue(user.Id, null);
        var phone = service.Issue(user.Id, null);
        await database.Context.SaveChangesAsync();
        await service.RotateAsync(laptop.RawToken, null);

        await service.RotateAsync(laptop.RawToken, null); // reuso en la familia "laptop"

        Assert.Equal(RefreshTokenStatus.Success, (await service.RotateAsync(phone.RawToken, null)).Status);
    }

    [Fact]
    public async Task Token_expirado_no_se_puede_rotar()
    {
        var user = await database.AddUserAsync();
        var issued = service.Issue(user.Id, null);
        await database.Context.SaveChangesAsync();

        time.Advance(TimeSpan.FromDays(8));

        Assert.Equal(RefreshTokenStatus.Expired, (await service.RotateAsync(issued.RawToken, null)).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-que-no-existe")]
    public async Task Token_desconocido_es_invalido(string? raw) =>
        Assert.Equal(RefreshTokenStatus.Invalid, (await service.RotateAsync(raw, null)).Status);

    [Fact]
    public async Task Revocar_todo_cierra_todas_las_sesiones_del_usuario()
    {
        var user = await database.AddUserAsync();
        service.Issue(user.Id, null);
        service.Issue(user.Id, null);
        await database.Context.SaveChangesAsync();

        var count = await service.RevokeAllForUserAsync(user.Id, "logout-all");

        Assert.Equal(2, count);
        Assert.False(await database.Context.RefreshTokens.AnyAsync(t => t.RevokedAt == null));
    }

    public void Dispose() => database.Dispose();
}
