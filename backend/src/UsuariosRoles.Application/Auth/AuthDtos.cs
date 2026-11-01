namespace UsuariosRoles.Application.Auth;

public sealed record RegisterRequest(string Email, string FullName, string Password);

public sealed record VerifyEmailRequest(string Token);

public sealed record EmailRequest(string Email);

public sealed record LoginRequest(string Email, string Password);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateProfileRequest(string FullName);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

/// <summary>Resultado interno de un login/refresh. El refresh token NO viaja en el cuerpo: la API lo pone en una cookie HttpOnly.</summary>
public sealed record AuthResult(string AccessToken, DateTime AccessTokenExpiresAt, IssuedRefreshToken RefreshToken, UserProfileDto User);

/// <summary>Respuesta pública de login/refresh.</summary>
public sealed record AuthResponse(string AccessToken, string TokenType, int ExpiresIn, DateTime ExpiresAt, UserProfileDto User);

public sealed record MessageResponse(string Message);
