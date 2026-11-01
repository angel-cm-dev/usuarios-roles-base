using UsuariosRoles.Application.Common;
using UsuariosRoles.Application.Security;

namespace UsuariosRoles.UnitTests;

public sealed class PasswordPolicyTests
{
    private readonly PasswordPolicy policy = new(new PasswordPolicyOptions());

    [Theory]
    [InlineData("Segura#2026")]
    [InlineData("Lima-Peru-99")]
    [InlineData("C0ntr@señaLarga")]
    public void Acepta_contrasenas_que_cumplen_la_politica(string password) =>
        Assert.Empty(policy.Validate(password, "angel@demo.pe"));

    [Theory]
    [InlineData("Ab1!", "al menos 8")]
    [InlineData("segura#2026", "mayúscula")]
    [InlineData("SEGURA#2026", "minúscula")]
    [InlineData("Segura#Lima", "número")]
    [InlineData("Segura2026x", "símbolo")]
    public void Rechaza_cada_regla_incumplida(string password, string expectedFragment)
    {
        var errors = policy.Validate(password);
        Assert.Contains(errors, e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Devuelve_todos_los_errores_a_la_vez()
    {
        var errors = policy.Validate("abc");
        Assert.True(errors.Count >= 3, $"Se esperaban varios errores y llegaron {errors.Count}");
    }

    [Fact]
    public void Rechaza_contrasena_vacia() =>
        Assert.Single(policy.Validate(string.Empty));

    [Fact]
    public void Rechaza_contrasenas_comunes() =>
        Assert.Contains(policy.Validate("P@ssw0rd!"), e => e.Contains("común"));

    [Fact]
    public void Rechaza_contrasena_que_contiene_el_email() =>
        Assert.Contains(policy.Validate("Mariela#2026", "mariela@demo.pe"), e => e.Contains("email"));

    [Fact]
    public void Rechaza_contrasenas_demasiado_largas() =>
        Assert.Contains(policy.Validate("Aa1!" + new string('x', 200)), e => e.Contains("superar"));

    [Fact]
    public void Respeta_la_configuracion()
    {
        var relaxed = new PasswordPolicy(new PasswordPolicyOptions { RequireSymbol = false, RequireUppercase = false, MinLength = 6 });
        Assert.True(relaxed.IsValid("lima2026"));
    }
}
