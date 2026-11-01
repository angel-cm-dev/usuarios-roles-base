using UsuariosRoles.Domain.Entities;

namespace UsuariosRoles.UnitTests;

public sealed class UserLockoutTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    [Fact]
    public void Bloquea_al_alcanzar_el_maximo_de_intentos()
    {
        var user = new User("ana@demo.pe", "Ana", Now);

        for (var i = 0; i < 4; i++)
        {
            Assert.False(user.RegisterFailedLogin(Now, 5, Lockout));
        }

        Assert.True(user.RegisterFailedLogin(Now, 5, Lockout));
        Assert.True(user.IsLockedOut(Now));
        Assert.Equal(Now.Add(Lockout), user.LockoutEnd);
    }

    [Fact]
    public void El_bloqueo_expira_con_el_tiempo()
    {
        var user = new User("ana@demo.pe", "Ana", Now);
        user.LockUntil(Now.AddMinutes(15));

        Assert.True(user.IsLockedOut(Now.AddMinutes(14)));
        Assert.False(user.IsLockedOut(Now.AddMinutes(15)));
    }

    [Fact]
    public void Tras_bloquear_el_contador_se_reinicia()
    {
        var user = new User("ana@demo.pe", "Ana", Now);
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(Now, 5, Lockout);
        }

        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public void Login_exitoso_reinicia_intentos_y_bloqueo()
    {
        var user = new User("ana@demo.pe", "Ana", Now);
        user.RegisterFailedLogin(Now, 5, Lockout);
        user.RegisterFailedLogin(Now, 5, Lockout);

        user.RegisterSuccessfulLogin(Now);

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
        Assert.Equal(Now, user.LastLoginAt);
    }

    [Fact]
    public void Normaliza_el_email() =>
        Assert.Equal("ANA@DEMO.PE", new User("  Ana@Demo.pe ", "Ana", Now).NormalizedEmail);
}

public sealed class OneTimeTokenTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Solo_se_puede_consumir_una_vez()
    {
        var token = new UserToken(Guid.NewGuid(), UserTokenPurpose.PasswordReset, "hash", Now, Now.AddMinutes(30));

        Assert.True(token.TryConsume(Now.AddMinutes(1)));
        Assert.False(token.TryConsume(Now.AddMinutes(2)));
    }

    [Fact]
    public void No_se_puede_consumir_expirado()
    {
        var token = new UserToken(Guid.NewGuid(), UserTokenPurpose.EmailVerification, "hash", Now, Now.AddMinutes(30));
        Assert.False(token.TryConsume(Now.AddMinutes(30)));
    }

    [Fact]
    public void Un_token_invalidado_no_se_puede_usar()
    {
        var token = new UserToken(Guid.NewGuid(), UserTokenPurpose.EmailVerification, "hash", Now, Now.AddHours(1));
        token.Invalidate(Now);
        Assert.False(token.CanBeUsed(Now));
    }
}

public sealed class RefreshTokenEntityTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Revocar_es_idempotente_y_conserva_el_primer_motivo()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", Guid.NewGuid(), Now, Now.AddDays(7), null);
        var replacement = Guid.NewGuid();

        token.Revoke(Now, "rotated", replacement);
        token.Revoke(Now.AddMinutes(1), "reuse-detected");

        Assert.Equal("rotated", token.RevokedReason);
        Assert.Equal(replacement, token.ReplacedByTokenId);
        Assert.False(token.IsActive(Now));
    }

    [Fact]
    public void Expira_en_la_fecha_indicada()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", Guid.NewGuid(), Now, Now.AddDays(7), null);
        Assert.True(token.IsActive(Now.AddDays(6)));
        Assert.False(token.IsActive(Now.AddDays(7)));
    }
}
