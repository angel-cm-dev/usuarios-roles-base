namespace UsuariosRoles.Application.Common;

/// <summary>
/// Excepción de aplicación con estado HTTP y código estable. El manejador global la traduce a ProblemDetails;
/// el campo <see cref="Code"/> permite al frontend reaccionar sin depender del texto del mensaje.
/// </summary>
public abstract class AppException(int statusCode, string code, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public string Title { get; } = title;
}

public sealed class NotFoundException(string resource, object key)
    : AppException(404, "not_found", "Recurso no encontrado", $"{resource} '{key}' no existe.");

public sealed class ConflictException(string code, string detail)
    : AppException(409, code, "Conflicto", detail);

public sealed class BusinessRuleException(string code, string detail)
    : AppException(400, code, "Operación no válida", detail);

public sealed class UnauthorizedException(string code, string detail)
    : AppException(401, code, "No autenticado", detail);

public sealed class ForbiddenException(string code, string detail)
    : AppException(403, code, "Acceso denegado", detail);

public sealed class AccountLockedException(DateTime lockoutEndUtc)
    : AppException(423, "account_locked", "Cuenta bloqueada", "La cuenta está bloqueada temporalmente por intentos fallidos. Inténtalo más tarde.")
{
    public DateTime LockoutEnd { get; } = lockoutEndUtc;
}

/// <summary>Errores de validación por campo (mismo formato que ValidationProblemDetails).</summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : AppException(400, "validation_error", "Datos inválidos", "Uno o más campos no son válidos.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public static RequestValidationException For(string field, params string[] messages) =>
        new(new Dictionary<string, string[]> { [field] = messages });
}
