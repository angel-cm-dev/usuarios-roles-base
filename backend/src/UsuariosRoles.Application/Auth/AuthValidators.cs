using FluentValidation;
using UsuariosRoles.Application.Security;

namespace UsuariosRoles.Application.Auth;

internal static class ValidationRules
{
    public const int EmailMaxLength = 256;
    public const int NameMaxLength = 100;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("El email es obligatorio.")
            .MaximumLength(EmailMaxLength)
            .EmailAddress().WithMessage("El email no tiene un formato válido.");

    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("El nombre es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre debe tener al menos 2 caracteres.")
            .MaximumLength(NameMaxLength);

    /// <summary>Aplica la política de contraseñas y reporta cada incumplimiento como un error del campo.</summary>
    public static IRuleBuilderOptionsConditions<T, string> MeetsPolicy<T>(
        this IRuleBuilder<T, string> rule,
        PasswordPolicy policy,
        Func<T, string?>? emailSelector = null) =>
        rule.Custom((password, context) =>
        {
            var email = emailSelector?.Invoke(context.InstanceToValidate);
            foreach (var error in policy.Validate(password, email))
            {
                context.AddFailure(error);
            }
        });
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(PasswordPolicy policy)
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FullName).ValidFullName();
        RuleFor(x => x.Password).MeetsPolicy(policy, x => x.Email);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("El email es obligatorio.").MaximumLength(ValidationRules.EmailMaxLength);

        // En el login NO se aplica la política: solo se exige que venga algo (y con tope, por costo del hash).
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.").MaximumLength(256);
    }
}

public sealed class EmailRequestValidator : AbstractValidator<EmailRequest>
{
    public EmailRequestValidator() => RuleFor(x => x.Email).ValidEmail();
}

public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator() =>
        RuleFor(x => x.Token).NotEmpty().WithMessage("El token es obligatorio.").MaximumLength(200);
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator(PasswordPolicy policy)
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("El token es obligatorio.").MaximumLength(200);
        RuleFor(x => x.NewPassword).MeetsPolicy(policy);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator(PasswordPolicy policy)
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("La contraseña actual es obligatoria.");
        RuleFor(x => x.NewPassword).MeetsPolicy(policy);
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser distinta de la actual.");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator() => RuleFor(x => x.FullName).ValidFullName();
}
