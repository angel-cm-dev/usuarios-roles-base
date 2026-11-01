using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Api.Common;

/// <summary>
/// Traduce excepciones a respuestas RFC 7807 (ProblemDetails) consistentes. Todas incluyen "code"
/// (estable, para el frontend) y "traceId" (para correlacionar con los logs). Los errores 500 nunca
/// exponen detalles internos al cliente.
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case RequestValidationException validation:
                problem = new HttpValidationProblemDetails(CamelCaseKeys(validation.Errors))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = validation.Title,
                    Detail = validation.Message,
                };
                problem.Extensions["code"] = validation.Code;
                break;

            case AppException app:
                problem = new ProblemDetails { Status = app.StatusCode, Title = app.Title, Detail = app.Message };
                problem.Extensions["code"] = app.Code;
                if (app is AccountLockedException locked)
                {
                    problem.Extensions["lockoutEnd"] = locked.LockoutEnd;
                }

                break;

            case DbUpdateConcurrencyException:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflicto de concurrencia",
                    Detail = "El recurso fue modificado por otra operación. Vuelve a intentarlo.",
                };
                problem.Extensions["code"] = "concurrency_conflict";
                break;

            case BadHttpRequestException badRequest:
                problem = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = "Petición inválida",
                    Detail = "El cuerpo o los parámetros de la petición no tienen un formato válido.",
                };
                problem.Extensions["code"] = "bad_request";
                break;

            default:
                logger.LogError(exception, "Error no controlado procesando {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Error interno",
                    Detail = "Ocurrió un error inesperado. Si persiste, contacta al administrador con el traceId.",
                };
                problem.Extensions["code"] = "internal_error";
                break;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static Dictionary<string, string[]> CamelCaseKeys(IDictionary<string, string[]> errors) =>
        errors.ToDictionary(
            kv => kv.Key.Length == 0 ? kv.Key : char.ToLowerInvariant(kv.Key[0]) + kv.Key[1..],
            kv => kv.Value);
}
