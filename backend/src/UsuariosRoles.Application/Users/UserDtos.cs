using FluentValidation;
using UsuariosRoles.Application.Auth;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Application.Security;

namespace UsuariosRoles.Application.Users;

public sealed class UserListQuery : PagedQuery
{
    /// <summary>all | active | inactive | locked | unverified</summary>
    public string? Status { get; set; }

    /// <summary>Nombre de rol para filtrar.</summary>
    public string? Role { get; set; }
}

public sealed record UserListItemDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    bool EmailConfirmed,
    bool IsLockedOut,
    DateTime? LockoutEnd,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record UserDetailDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    bool EmailConfirmed,
    bool IsLockedOut,
    DateTime? LockoutEnd,
    int AccessFailedCount,
    IReadOnlyList<RoleRefDto> Roles,
    IReadOnlyList<string> Permissions,
    int ActiveSessions,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    DateTime? PasswordChangedAt);

public sealed record RoleRefDto(Guid Id, string Name);

public sealed record CreateUserRequest(string Email, string FullName, string Password, bool EmailConfirmed, IReadOnlyList<Guid>? RoleIds);

public sealed record UpdateUserRequest(string FullName, bool IsActive);

public sealed record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds);

public sealed record LockUserRequest(int? Minutes);

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(PasswordPolicy policy)
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.Password).MeetsPolicy(policy, x => x.Email);
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator() => RuleFor(x => x.FullName).ValidFullName();
}

public sealed class SetUserRolesRequestValidator : AbstractValidator<SetUserRolesRequest>
{
    public SetUserRolesRequestValidator() =>
        RuleFor(x => x.RoleIds).NotNull().Must(ids => ids.Count <= 50).WithMessage("Demasiados roles.");
}

public sealed class LockUserRequestValidator : AbstractValidator<LockUserRequest>
{
    public LockUserRequestValidator() =>
        RuleFor(x => x.Minutes).InclusiveBetween(1, 525600).When(x => x.Minutes.HasValue)
            .WithMessage("Los minutos deben estar entre 1 y 525600 (1 año).");
}
