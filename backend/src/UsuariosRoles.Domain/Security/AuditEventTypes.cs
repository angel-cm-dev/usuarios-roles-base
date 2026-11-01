namespace UsuariosRoles.Domain.Security;

/// <summary>Tipos de eventos de auditoría. Se guardan como texto para que sean legibles en la BD.</summary>
public static class AuditEventTypes
{
    public const string UserRegistered = "user.registered";
    public const string RegistrationDuplicate = "user.registration-duplicate";
    public const string EmailVerified = "user.email-verified";
    public const string EmailVerificationFailed = "user.email-verification-failed";
    public const string LoginSucceeded = "auth.login-succeeded";
    public const string LoginFailed = "auth.login-failed";
    public const string LoginBlocked = "auth.login-blocked";
    public const string AccountLocked = "auth.account-locked";
    public const string RefreshTokenReuseDetected = "auth.refresh-token-reuse";
    public const string Logout = "auth.logout";
    public const string LogoutAll = "auth.logout-all";
    public const string PasswordResetRequested = "auth.password-reset-requested";
    public const string PasswordResetCompleted = "auth.password-reset-completed";
    public const string PasswordResetFailed = "auth.password-reset-failed";
    public const string PasswordChanged = "auth.password-changed";
    public const string ProfileUpdated = "user.profile-updated";
    public const string UserCreated = "admin.user-created";
    public const string UserUpdated = "admin.user-updated";
    public const string UserRolesChanged = "admin.user-roles-changed";
    public const string UserLocked = "admin.user-locked";
    public const string UserUnlocked = "admin.user-unlocked";
    public const string SessionsRevoked = "admin.sessions-revoked";
    public const string RoleCreated = "admin.role-created";
    public const string RoleUpdated = "admin.role-updated";
    public const string RolePermissionsChanged = "admin.role-permissions-changed";
    public const string RoleDeleted = "admin.role-deleted";

    public static IReadOnlyList<string> All { get; } =
    [
        UserRegistered, RegistrationDuplicate, EmailVerified, EmailVerificationFailed,
        LoginSucceeded, LoginFailed, LoginBlocked, AccountLocked, RefreshTokenReuseDetected,
        Logout, LogoutAll, PasswordResetRequested, PasswordResetCompleted, PasswordResetFailed,
        PasswordChanged, ProfileUpdated, UserCreated, UserUpdated, UserRolesChanged, UserLocked,
        UserUnlocked, SessionsRevoked, RoleCreated, RoleUpdated, RolePermissionsChanged, RoleDeleted,
    ];
}
