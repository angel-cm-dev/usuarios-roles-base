using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Application.Emails;
using UsuariosRoles.Application.Security;
using UsuariosRoles.Domain.Entities;
using UsuariosRoles.Domain.Security;

namespace UsuariosRoles.Application.Auth;

/// <summary>Casos de uso de autenticación: registro, verificación, login, refresh, logout y contraseñas.</summary>
public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    RefreshTokenService refreshTokens,
    UserTokenService userTokens,
    PasswordPolicy passwordPolicy,
    IEmailSender emailSender,
    IAuditLogger audit,
    IRequestContext requestContext,
    TimeProvider timeProvider,
    IOptions<SecurityOptions> securityOptions,
    IOptions<AppUrlsOptions> urlOptions,
    ILogger<AuthService> logger)
{
    // Hash calculado una sola vez para igualar el tiempo de respuesta cuando el email no existe
    // (evita que un atacante descubra qué emails están registrados midiendo la latencia).
    private static string? dummyPasswordHash;

    private SecurityOptions Security => securityOptions.Value;

    private string FrontendUrl => urlOptions.Value.FrontendBaseUrl.TrimEnd('/');

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = User.Normalize(request.Email);
        var existing = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);
        if (existing is not null)
        {
            // Anti-enumeración: la respuesta es idéntica a la de un registro nuevo.
            // Avisamos al dueño real del email por correo en lugar de decir "ya existe" en la API.
            await emailSender.SendAsync(
                EmailTemplates.RegistrationAttempt(existing.Email, existing.FullName, $"{FrontendUrl}/login"),
                cancellationToken);
            await audit.LogAsync(new AuditEntry(AuditEventTypes.RegistrationDuplicate, false, existing.Id, existing.Email), cancellationToken);
            return;
        }

        var user = new User(request.Email, request.FullName, Now);
        user.SetPasswordHash(passwordHasher.Hash(user, request.Password), Now);

        var defaultRole = await db.Roles.SingleOrDefaultAsync(r => r.Name == SystemRoles.Usuario, cancellationToken)
            ?? throw new InvalidOperationException("No existe el rol por defecto 'usuario'. ¿Se ejecutó el seed?");
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id });

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        await SendVerificationEmailAsync(user, cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.UserRegistered, true, user.Id, user.Email), cancellationToken);
    }

    public async Task ResendVerificationAsync(EmailRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = User.Normalize(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        // Respuesta siempre igual (anti-enumeración); solo se envía si aplica.
        if (user is { EmailConfirmed: false, IsActive: true })
        {
            await SendVerificationEmailAsync(user, cancellationToken);
        }
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userTokens.ConsumeAsync(request.Token, UserTokenPurpose.EmailVerification, cancellationToken);
        if (user is null)
        {
            await audit.LogAsync(new AuditEntry(AuditEventTypes.EmailVerificationFailed, false, Details: "Token inválido, usado o expirado"), cancellationToken);
            throw new BusinessRuleException("invalid_token", "El enlace de verificación es inválido, ya fue usado o expiró.");
        }

        user.ConfirmEmail();
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.EmailVerified, true, user.Id, user.Email), cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var now = Now;
        var normalized = User.Normalize(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        if (user is null)
        {
            SimulatePasswordCheck(request.Password);
            await audit.LogAsync(new AuditEntry(AuditEventTypes.LoginFailed, false, Email: request.Email, Details: "Email no registrado"), cancellationToken);
            throw InvalidCredentials();
        }

        if (user.IsLockedOut(now))
        {
            await audit.LogAsync(new AuditEntry(AuditEventTypes.LoginBlocked, false, user.Id, user.Email, "Intento durante bloqueo"), cancellationToken);
            throw new AccountLockedException(user.LockoutEnd!.Value);
        }

        var check = passwordHasher.Verify(user, request.Password);
        if (check == PasswordCheckResult.Failed)
        {
            var lockout = Security.Lockout;
            var lockedNow = user.RegisterFailedLogin(now, lockout.MaxFailedAttempts, TimeSpan.FromMinutes(lockout.LockoutMinutes));
            await db.SaveChangesAsync(cancellationToken);
            await audit.LogAsync(new AuditEntry(AuditEventTypes.LoginFailed, false, user.Id, user.Email, "Contraseña incorrecta"), cancellationToken);

            if (lockedNow)
            {
                await audit.LogAsync(
                    new AuditEntry(AuditEventTypes.AccountLocked, true, user.Id, user.Email, $"Bloqueada {lockout.LockoutMinutes} min tras {lockout.MaxFailedAttempts} intentos"),
                    cancellationToken);
                throw new AccountLockedException(user.LockoutEnd!.Value);
            }

            throw InvalidCredentials();
        }

        // Estas comprobaciones van DESPUÉS de validar la contraseña: así no revelan el estado
        // de la cuenta a quien no conoce la contraseña.
        if (!user.EmailConfirmed)
        {
            await audit.LogAsync(new AuditEntry(AuditEventTypes.LoginFailed, false, user.Id, user.Email, "Email no verificado"), cancellationToken);
            throw new ForbiddenException("email_not_verified", "Debes verificar tu email antes de iniciar sesión.");
        }

        if (!user.IsActive)
        {
            await audit.LogAsync(new AuditEntry(AuditEventTypes.LoginFailed, false, user.Id, user.Email, "Cuenta desactivada"), cancellationToken);
            throw new ForbiddenException("account_disabled", "Tu cuenta está desactivada. Contacta a un administrador.");
        }

        if (check == PasswordCheckResult.SuccessRehashNeeded)
        {
            // El hash se generó con parámetros antiguos: se actualiza aprovechando que tenemos la contraseña en claro.
            user.UpgradePasswordHash(passwordHasher.Hash(user, request.Password));
        }

        user.RegisterSuccessfulLogin(now);
        var refresh = refreshTokens.Issue(user.Id, requestContext.IpAddress);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.LoginSucceeded, true, user.Id, user.Email), cancellationToken);

        return await BuildAuthResultAsync(user, refresh, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string? rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var rotation = await refreshTokens.RotateAsync(rawRefreshToken, requestContext.IpAddress, cancellationToken);
        switch (rotation.Status)
        {
            case RefreshTokenStatus.ReuseDetected:
                logger.LogWarning("Reuso de refresh token detectado para el usuario {UserId}; familia revocada", rotation.UserId);
                await audit.LogAsync(
                    new AuditEntry(AuditEventTypes.RefreshTokenReuseDetected, false, rotation.UserId, Details: "Se revocó la familia completa de tokens"),
                    cancellationToken);
                throw new UnauthorizedException("refresh_token_reused", "La sesión fue revocada por seguridad. Inicia sesión nuevamente.");
            case RefreshTokenStatus.Invalid:
            case RefreshTokenStatus.Expired:
                throw new UnauthorizedException("invalid_refresh_token", "La sesión expiró. Inicia sesión nuevamente.");
        }

        var user = await db.Users.SingleAsync(u => u.Id == rotation.UserId, cancellationToken);
        if (!user.IsActive || user.IsLockedOut(Now))
        {
            await refreshTokens.RevokeAllForUserAsync(user.Id, "account-disabled", cancellationToken);
            throw new UnauthorizedException("account_unavailable", "La cuenta no está disponible.");
        }

        return await BuildAuthResultAsync(user, rotation.NewToken!, cancellationToken);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var userId = await refreshTokens.RevokeFamilyAsync(rawRefreshToken, "logout", cancellationToken);
        if (userId is not null)
        {
            await audit.LogAsync(new AuditEntry(AuditEventTypes.Logout, true, userId), cancellationToken);
        }
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var count = await refreshTokens.RevokeAllForUserAsync(userId, "logout-all", cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.LogoutAll, true, userId, Details: $"{count} sesiones revocadas"), cancellationToken);
    }

    public async Task ForgotPasswordAsync(EmailRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = User.Normalize(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);
        if (user is not { IsActive: true })
        {
            // Misma respuesta exista o no el email (anti-enumeración).
            return;
        }

        var minutes = Security.Tokens.PasswordResetMinutes;
        var raw = await userTokens.IssueAsync(user.Id, UserTokenPurpose.PasswordReset, TimeSpan.FromMinutes(minutes), cancellationToken);
        var link = $"{FrontendUrl}/restablecer-password?token={Uri.EscapeDataString(raw)}";
        await emailSender.SendAsync(EmailTemplates.PasswordReset(user.Email, user.FullName, link, minutes), cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.PasswordResetRequested, true, user.Id, user.Email), cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userTokens.ConsumeAsync(request.Token, UserTokenPurpose.PasswordReset, cancellationToken);
        if (user is null)
        {
            await audit.LogAsync(new AuditEntry(AuditEventTypes.PasswordResetFailed, false, Details: "Token inválido, usado o expirado"), cancellationToken);
            throw new BusinessRuleException("invalid_token", "El enlace para restablecer la contraseña es inválido, ya fue usado o expiró.");
        }

        EnsurePasswordPolicy(request.NewPassword, user.Email, nameof(request.NewPassword));

        user.SetPasswordHash(passwordHasher.Hash(user, request.NewPassword), Now);

        // Quien recibe el correo de recuperación demuestra ser dueño del email.
        user.ConfirmEmail();
        user.Unlock();
        await db.SaveChangesAsync(cancellationToken);

        // Si alguien robó una sesión, el cambio de contraseña debe expulsarlo.
        await refreshTokens.RevokeAllForUserAsync(user.Id, "password-reset", cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.PasswordResetCompleted, true, user.Id, user.Email), cancellationToken);
        await emailSender.SendAsync(EmailTemplates.PasswordChanged(user.Email, user.FullName, $"{FrontendUrl}/login"), cancellationToken);
    }

    /// <summary>Cambia la contraseña, cierra todas las sesiones y abre una nueva para el dispositivo actual.</summary>
    public async Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Usuario", userId);

        if (passwordHasher.Verify(user, request.CurrentPassword) == PasswordCheckResult.Failed)
        {
            throw RequestValidationException.For(nameof(request.CurrentPassword), "La contraseña actual no es correcta.");
        }

        EnsurePasswordPolicy(request.NewPassword, user.Email, nameof(request.NewPassword));

        user.SetPasswordHash(passwordHasher.Hash(user, request.NewPassword), Now);
        await db.SaveChangesAsync(cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(user.Id, "password-changed", cancellationToken);

        var refresh = refreshTokens.Issue(user.Id, requestContext.IpAddress);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.PasswordChanged, true, user.Id, user.Email), cancellationToken);
        await emailSender.SendAsync(EmailTemplates.PasswordChanged(user.Email, user.FullName, $"{FrontendUrl}/login"), cancellationToken);

        return await BuildAuthResultAsync(user, refresh, cancellationToken);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Usuario", userId);
        return await db.GetProfileAsync(user, cancellationToken);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Usuario", userId);
        user.Rename(request.FullName);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(new AuditEntry(AuditEventTypes.ProfileUpdated, true, user.Id, user.Email), cancellationToken);
        return await db.GetProfileAsync(user, cancellationToken);
    }

    private async Task SendVerificationEmailAsync(User user, CancellationToken cancellationToken)
    {
        var hours = Security.Tokens.EmailVerificationHours;
        var raw = await userTokens.IssueAsync(user.Id, UserTokenPurpose.EmailVerification, TimeSpan.FromHours(hours), cancellationToken);
        var link = $"{FrontendUrl}/verificar-email?token={Uri.EscapeDataString(raw)}";
        await emailSender.SendAsync(EmailTemplates.VerifyEmail(user.Email, user.FullName, link, hours), cancellationToken);
    }

    private async Task<AuthResult> BuildAuthResultAsync(User user, IssuedRefreshToken refresh, CancellationToken cancellationToken)
    {
        var profile = await db.GetProfileAsync(user, cancellationToken);
        var accessToken = accessTokenGenerator.Create(user, profile.Roles.ToList(), profile.Permissions.ToList());
        return new AuthResult(accessToken.Token, accessToken.ExpiresAt, refresh, profile);
    }

    private void EnsurePasswordPolicy(string password, string email, string field)
    {
        var errors = passwordPolicy.Validate(password, email);
        if (errors.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { [field] = errors.ToArray() });
        }
    }

    private void SimulatePasswordCheck(string password)
    {
        var dummy = new User("timing@invalid.local", "timing", DateTime.UnixEpoch);
        dummyPasswordHash ??= passwordHasher.Hash(dummy, "Timing-Dummy-Password-1!");
        dummy.SetPasswordHash(dummyPasswordHash, DateTime.UnixEpoch);
        _ = passwordHasher.Verify(dummy, password);
    }

    private static UnauthorizedException InvalidCredentials() =>
        new("invalid_credentials", "Email o contraseña incorrectos.");
}
