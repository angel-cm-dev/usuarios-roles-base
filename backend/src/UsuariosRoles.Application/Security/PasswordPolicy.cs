using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Application.Security;

/// <summary>
/// Política de contraseñas. Devuelve TODOS los errores a la vez (mejor UX que fallar de uno en uno).
/// Se usa en los validadores de entrada y de nuevo en los casos de uso como defensa en profundidad.
/// </summary>
public sealed class PasswordPolicy(PasswordPolicyOptions options)
{
    // Lista mínima de contraseñas muy comunes. En un sistema real conviene usar una lista amplia
    // (p. ej. top 100k de filtraciones) o la API k-anonymity de "Have I Been Pwned".
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "password123!", "passw0rd!", "contraseña", "contraseña1",
        "12345678", "123456789", "1234567890", "qwerty123", "qwerty123!", "admin123", "admin123!",
        "welcome1", "welcome123!", "iloveyou", "letmein1", "abc12345", "p@ssw0rd", "p@ssw0rd!",
    };

    public PasswordPolicyOptions Options { get; } = options;

    public IReadOnlyList<string> Validate(string? password, string? email = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password))
        {
            errors.Add("La contraseña es obligatoria.");
            return errors;
        }

        if (password.Length < Options.MinLength)
        {
            errors.Add($"Debe tener al menos {Options.MinLength} caracteres.");
        }

        if (password.Length > Options.MaxLength)
        {
            errors.Add($"No puede superar {Options.MaxLength} caracteres.");
        }

        if (Options.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Debe incluir al menos una letra mayúscula.");
        }

        if (Options.RequireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("Debe incluir al menos una letra minúscula.");
        }

        if (Options.RequireDigit && !password.Any(char.IsDigit))
        {
            errors.Add("Debe incluir al menos un número.");
        }

        if (Options.RequireSymbol && password.All(char.IsLetterOrDigit))
        {
            errors.Add("Debe incluir al menos un símbolo (por ejemplo ! @ # $).");
        }

        if (CommonPasswords.Contains(password))
        {
            errors.Add("La contraseña es demasiado común.");
        }

        if (Options.ForbidEmailInPassword && !string.IsNullOrWhiteSpace(email))
        {
            var localPart = email.Split('@')[0];
            if (localPart.Length >= 3 && password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("No debe contener tu email.");
            }
        }

        return errors;
    }

    public bool IsValid(string? password, string? email = null) => Validate(password, email).Count == 0;
}
