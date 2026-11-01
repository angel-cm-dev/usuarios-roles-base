using FluentValidation;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Application.Roles;

public sealed class RoleListQuery : PagedQuery;

public sealed record RoleListItemDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    int UserCount,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt);

public sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string>? Permissions);

public sealed record UpdateRoleRequest(string Name, string? Description);

public sealed record SetRolePermissionsRequest(IReadOnlyList<string> Permissions);

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).ValidRoleName();
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).ValidRoleName();
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public sealed class SetRolePermissionsRequestValidator : AbstractValidator<SetRolePermissionsRequest>
{
    public SetRolePermissionsRequestValidator() => RuleFor(x => x.Permissions).NotNull();
}

internal static class RoleRules
{
    // Nombres simples en minúsculas: se usan como claim "role" en el JWT y en URLs.
    public static IRuleBuilderOptions<T, string> ValidRoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("El nombre es obligatorio.")
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,49}$")
            .WithMessage("Usa de 2 a 50 caracteres: letras, números, guion, punto o guion bajo (sin espacios).");
}
