using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Api.Common;

/// <summary>Implementación de <see cref="IRequestContext"/> basada en el HttpContext actual.</summary>
internal sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public Guid? UserId => accessor.HttpContext?.User.GetUserIdOrNull();

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}

internal static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserIdOrNull(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        principal.GetUserIdOrNull() ?? throw new UnauthorizedException("invalid_token", "El token no contiene el identificador del usuario.");
}

public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookie";

    public string Name { get; set; } = "urb_refresh";

    /// <summary>true en producción (HTTPS). Solo se desactiva en desarrollo sobre http.</summary>
    public bool Secure { get; set; } = true;
}

/// <summary>
/// El refresh token viaja SOLO en una cookie HttpOnly + SameSite=Strict con Path=/api/auth:
/// - HttpOnly: JavaScript (y por tanto un XSS) no puede leerla.
/// - SameSite=Strict: el navegador no la envía en peticiones originadas por otros sitios (mitiga CSRF).
/// - Path=/api/auth: no se adjunta al resto de llamadas de la API.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Path = "/api/auth";

    public static void Write(HttpResponse response, AuthCookieOptions options, string token, DateTime expiresAtUtc) =>
        response.Cookies.Append(options.Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.Secure,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
            IsEssential = true,
        });

    public static string? Read(HttpRequest request, AuthCookieOptions options) =>
        request.Cookies.TryGetValue(options.Name, out var value) ? value : null;

    public static void Delete(HttpResponse response, AuthCookieOptions options) =>
        response.Cookies.Delete(options.Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.Secure,
            SameSite = SameSiteMode.Strict,
            Path = Path,
        });
}

/// <summary>Cabeceras de seguridad HTTP aplicadas a todas las respuestas de la API.</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        var path = context.Request.Path;
        var isDocs = path.StartsWithSegments("/scalar") || path.StartsWithSegments("/openapi");
        if (!isDocs)
        {
            // La API solo devuelve JSON: una CSP restrictiva impide que una respuesta se interprete como página.
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        }

        if (path.StartsWithSegments("/api"))
        {
            // Las respuestas pueden contener tokens o datos personales: que no queden en cachés intermedias.
            headers.CacheControl = "no-store";
        }

        return next(context);
    }
}
