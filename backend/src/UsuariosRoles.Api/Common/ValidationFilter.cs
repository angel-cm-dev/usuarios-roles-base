using FluentValidation;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Api.Common;

/// <summary>
/// Filtro de endpoint que valida el cuerpo con FluentValidation antes de ejecutar el handler.
/// Los errores se lanzan como <see cref="RequestValidationException"/> para que el manejador global
/// produzca un único formato de error en toda la API.
/// </summary>
internal sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault()
            ?? throw RequestValidationException.For("body", "El cuerpo de la petición es obligatorio.");

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            throw new RequestValidationException(result.ToDictionary());
        }

        return await next(context);
    }
}

internal static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
