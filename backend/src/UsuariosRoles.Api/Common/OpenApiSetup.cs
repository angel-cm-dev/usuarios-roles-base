using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UsuariosRoles.Api.Common;

/// <summary>Declara el esquema Bearer en el documento OpenAPI para poder probar endpoints protegidos desde Scalar.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "usuarios-roles-base API",
            Version = "v1",
            Description = "Plantilla base de autenticación y autorización (JWT + refresh token rotativo + RBAC).",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token obtenido en POST /api/auth/login",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });
        return Task.CompletedTask;
    }
}
