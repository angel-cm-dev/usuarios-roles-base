namespace UsuariosRoles.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "usuarios-roles-base";

    public string Audience { get; set; } = "usuarios-roles-base-clients";

    /// <summary>
    /// Clave HMAC-SHA256 (mínimo 32 bytes). NUNCA la subas al repositorio: usa variables de entorno
    /// (Jwt__Key) o un gestor de secretos. La de appsettings.Development.json es solo de ejemplo.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Vida corta: si un access token se filtra, sirve poco tiempo. La sesión se mantiene con el refresh token.</summary>
    public int AccessTokenMinutes { get; set; } = 15;
}
