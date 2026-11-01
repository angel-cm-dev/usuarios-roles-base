using Microsoft.Extensions.Options;
using UsuariosRoles.Api.Authorization;
using UsuariosRoles.Api.Common;
using UsuariosRoles.Application.Auth;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Domain.Security;

namespace UsuariosRoles.Api.Endpoints;

internal static class AuthEndpoints
{
    public const string AuthRateLimitPolicy = "auth";

    private const string GenericEmailMessage = "Si el email corresponde a una cuenta, recibirás un correo con las instrucciones.";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, AuthService auth, CancellationToken ct) =>
            {
                await auth.RegisterAsync(request, ct);
                return TypedResults.Accepted((string?)null, new MessageResponse("Registro recibido. " + GenericEmailMessage));
            })
            .Validate<RegisterRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithSummary("Registra un usuario y envía el correo de verificación (respuesta anti-enumeración).");

        group.MapPost("/verify-email", async (VerifyEmailRequest request, AuthService auth, CancellationToken ct) =>
            {
                await auth.VerifyEmailAsync(request, ct);
                return TypedResults.Ok(new MessageResponse("Email verificado. Ya puedes iniciar sesión."));
            })
            .Validate<VerifyEmailRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithSummary("Confirma el email con el token de un solo uso recibido por correo.");

        group.MapPost("/resend-verification", async (EmailRequest request, AuthService auth, CancellationToken ct) =>
            {
                await auth.ResendVerificationAsync(request, ct);
                return TypedResults.Accepted((string?)null, new MessageResponse(GenericEmailMessage));
            })
            .Validate<EmailRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithSummary("Reenvía el correo de verificación.");

        group.MapPost("/login", async (LoginRequest request, AuthService auth, HttpResponse response, IOptions<AuthCookieOptions> cookie, TimeProvider time, CancellationToken ct) =>
            {
                var result = await auth.LoginAsync(request, ct);
                return TypedResults.Ok(ToResponse(result, response, cookie.Value, time));
            })
            .Validate<LoginRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithSummary("Inicia sesión: devuelve el access token y deja el refresh token en una cookie HttpOnly.");

        group.MapPost("/refresh", async (HttpRequest request, HttpResponse response, AuthService auth, IOptions<AuthCookieOptions> cookie, TimeProvider time, CancellationToken ct) =>
            {
                try
                {
                    var result = await auth.RefreshAsync(RefreshTokenCookie.Read(request, cookie.Value), ct);
                    return TypedResults.Ok(ToResponse(result, response, cookie.Value, time));
                }
                catch (UnauthorizedException)
                {
                    RefreshTokenCookie.Delete(response, cookie.Value);
                    throw;
                }
            })
            .WithSummary("Rota el refresh token (cookie) y emite un nuevo access token. Detecta reuso.");

        group.MapPost("/logout", async (HttpRequest request, HttpResponse response, AuthService auth, IOptions<AuthCookieOptions> cookie, CancellationToken ct) =>
            {
                await auth.LogoutAsync(RefreshTokenCookie.Read(request, cookie.Value), ct);
                RefreshTokenCookie.Delete(response, cookie.Value);
                return TypedResults.NoContent();
            })
            .WithSummary("Cierra la sesión actual revocando su familia de refresh tokens.");

        group.MapPost("/logout-all", async (HttpContext http, AuthService auth, IOptions<AuthCookieOptions> cookie, CancellationToken ct) =>
            {
                await auth.LogoutAllAsync(http.User.GetUserId(), ct);
                RefreshTokenCookie.Delete(http.Response, cookie.Value);
                return TypedResults.NoContent();
            })
            .RequireAuthorization()
            .WithSummary("Cierra todas las sesiones del usuario en todos los dispositivos.");

        group.MapPost("/forgot-password", async (EmailRequest request, AuthService auth, CancellationToken ct) =>
            {
                await auth.ForgotPasswordAsync(request, ct);
                return TypedResults.Accepted((string?)null, new MessageResponse(GenericEmailMessage));
            })
            .Validate<EmailRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithSummary("Envía un enlace de recuperación de un solo uso (respuesta anti-enumeración).");

        group.MapPost("/reset-password", async (ResetPasswordRequest request, AuthService auth, CancellationToken ct) =>
            {
                await auth.ResetPasswordAsync(request, ct);
                return TypedResults.Ok(new MessageResponse("Contraseña actualizada. Inicia sesión con tu nueva contraseña."));
            })
            .Validate<ResetPasswordRequest>()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .WithSummary("Restablece la contraseña con el token recibido y cierra todas las sesiones.");

        group.MapGet("/me", async (HttpContext http, AuthService auth, CancellationToken ct) =>
                TypedResults.Ok(await auth.GetProfileAsync(http.User.GetUserId(), ct)))
            .RequireAuthorization()
            .WithSummary("Perfil, roles y permisos del usuario autenticado.");

        group.MapPut("/me", async (UpdateProfileRequest request, HttpContext http, AuthService auth, CancellationToken ct) =>
                TypedResults.Ok(await auth.UpdateProfileAsync(http.User.GetUserId(), request, ct)))
            .Validate<UpdateProfileRequest>()
            .RequirePermission(Permissions.Perfil.Editar)
            .WithSummary("Actualiza el propio perfil.");

        group.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext http, AuthService auth, IOptions<AuthCookieOptions> cookie, TimeProvider time, CancellationToken ct) =>
            {
                var result = await auth.ChangePasswordAsync(http.User.GetUserId(), request, ct);
                return TypedResults.Ok(ToResponse(result, http.Response, cookie.Value, time));
            })
            .Validate<ChangePasswordRequest>()
            .RequireAuthorization()
            .WithSummary("Cambia la contraseña, revoca las demás sesiones y renueva la actual.");
    }

    private static AuthResponse ToResponse(AuthResult result, HttpResponse response, AuthCookieOptions cookie, TimeProvider time)
    {
        RefreshTokenCookie.Write(response, cookie, result.RefreshToken.RawToken, result.RefreshToken.ExpiresAt);
        var expiresIn = (int)Math.Max(0, (result.AccessTokenExpiresAt - time.GetUtcNow().UtcDateTime).TotalSeconds);
        return new AuthResponse(result.AccessToken, "Bearer", expiresIn, result.AccessTokenExpiresAt, result.User);
    }
}
