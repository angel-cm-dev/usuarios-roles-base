using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using UsuariosRoles.Api.Authorization;
using UsuariosRoles.Domain.Entities;
using UsuariosRoles.Domain.Security;
using UsuariosRoles.Infrastructure.Security;

namespace UsuariosRoles.UnitTests;

public sealed class PermissionAuthorizationTests
{
    private static ClaimsPrincipal UserWith(params string[] permissions)
    {
        var claims = permissions.Select(p => new Claim(Permissions.ClaimType, p)).Append(new Claim("sub", Guid.NewGuid().ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"));
    }

    private static async Task<bool> EvaluateAsync(ClaimsPrincipal user, string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        await new PermissionAuthorizationHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task Concede_si_el_usuario_tiene_el_permiso() =>
        Assert.True(await EvaluateAsync(UserWith(Permissions.Usuarios.Leer), Permissions.Usuarios.Leer));

    [Fact]
    public async Task Deniega_si_no_tiene_el_permiso() =>
        Assert.False(await EvaluateAsync(UserWith(Permissions.Usuarios.Leer), Permissions.Usuarios.Crear));

    [Fact]
    public async Task Deniega_a_usuarios_no_autenticados()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Permissions.ClaimType, Permissions.Usuarios.Leer)]));
        Assert.False(await EvaluateAsync(anonymous, Permissions.Usuarios.Leer));
    }

    [Fact]
    public async Task La_comparacion_es_exacta()
    {
        // "usuarios.leer" no debe conceder "usuarios.leer-todo" ni viceversa.
        Assert.False(await EvaluateAsync(UserWith("usuarios"), Permissions.Usuarios.Leer));
    }

    [Fact]
    public async Task El_proveedor_crea_politicas_a_partir_del_nombre()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync("perm:roles.editar");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
        Assert.Equal("roles.editar", requirement.Permission);
    }

    [Fact]
    public void El_catalogo_no_tiene_codigos_duplicados_y_sigue_el_formato()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Select(p => p.Code).Distinct().Count());
        Assert.All(Permissions.All, p => Assert.Matches("^[a-z]+\\.[a-z-]+$", p.Code));
    }
}

public sealed class CryptoTests
{
    [Fact]
    public void Los_tokens_son_aleatorios_y_largos()
    {
        var generator = new SecureTokenGenerator();
        var tokens = Enumerable.Range(0, 100).Select(_ => generator.Generate()).ToList();

        Assert.Equal(100, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.True(t.Length >= 43)); // 32 bytes en Base64Url
    }

    [Fact]
    public void El_hash_es_determinista_y_no_revela_el_token()
    {
        var generator = new SecureTokenGenerator();
        var raw = generator.Generate();

        Assert.Equal(generator.Hash(raw), generator.Hash(raw));
        Assert.Equal(64, generator.Hash(raw).Length);
        Assert.DoesNotContain(raw, generator.Hash(raw));
    }

    [Fact]
    public void El_hasher_de_contrasenas_usa_sal_y_verifica()
    {
        var hasher = new IdentityPasswordHasher();
        var user = new User("ana@demo.pe", "Ana", DateTime.UtcNow);

        var hashA = hasher.Hash(user, "Segura#2026");
        var hashB = hasher.Hash(user, "Segura#2026");
        Assert.NotEqual(hashA, hashB); // sal aleatoria

        user.SetPasswordHash(hashA, DateTime.UtcNow);
        Assert.Equal(Application.Common.PasswordCheckResult.Success, hasher.Verify(user, "Segura#2026"));
        Assert.Equal(Application.Common.PasswordCheckResult.Failed, hasher.Verify(user, "otra"));
    }
}
