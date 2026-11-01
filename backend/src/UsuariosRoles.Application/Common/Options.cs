namespace UsuariosRoles.Application.Common;

/// <summary>Opciones de seguridad configurables desde appsettings (sección "Security").</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public PasswordPolicyOptions Password { get; set; } = new();

    public LockoutOptions Lockout { get; set; } = new();

    public TokenLifetimeOptions Tokens { get; set; } = new();
}

public sealed class PasswordPolicyOptions
{
    public int MinLength { get; set; } = 8;

    // Un máximo evita ataques de DoS con contraseñas enormes (el hash es costoso por diseño).
    public int MaxLength { get; set; } = 128;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireDigit { get; set; } = true;

    public bool RequireSymbol { get; set; } = true;

    public bool ForbidEmailInPassword { get; set; } = true;
}

public sealed class LockoutOptions
{
    public int MaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;
}

public sealed class TokenLifetimeOptions
{
    public int RefreshTokenDays { get; set; } = 7;

    public int EmailVerificationHours { get; set; } = 24;

    public int PasswordResetMinutes { get; set; } = 30;
}

/// <summary>URLs públicas (para construir enlaces en los correos).</summary>
public sealed class AppUrlsOptions
{
    public const string SectionName = "AppUrls";

    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
