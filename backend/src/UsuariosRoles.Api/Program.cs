using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;
using UsuariosRoles.Api.Authorization;
using UsuariosRoles.Api.Common;
using UsuariosRoles.Api.Endpoints;
using UsuariosRoles.Application;
using UsuariosRoles.Application.Common;
using UsuariosRoles.Infrastructure;
using UsuariosRoles.Infrastructure.Persistence;
using UsuariosRoles.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Logging estructurado (Serilog) ----------
// Desarrollo: texto legible. Resto de entornos: JSON compacto (listo para Loki/ELK/Seq).
builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(config).ReadFrom.Services(services).Enrich.FromLogContext();
    if (builder.Environment.IsDevelopment())
    {
        logger.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }
    else
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
});

builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

// ---------- Capas ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();
builder.Services.Configure<AuthCookieOptions>(config.GetSection(AuthCookieOptions.SectionName));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- Errores: ProblemDetails consistentes ----------
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    if (!context.ProblemDetails.Extensions.ContainsKey("code"))
    {
        context.ProblemDetails.Extensions["code"] = context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => "bad_request",
            StatusCodes.Status401Unauthorized => "unauthorized",
            StatusCodes.Status403Forbidden => "forbidden",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status429TooManyRequests => "too_many_requests",
            _ => "error",
        };
    }
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- Autenticación JWT ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        // Sin mapeo a los nombres largos de WS-Federation: los claims se leen tal cual ("sub", "role", "permission").
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });

// ---------- Autorización basada en permisos ----------
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// ---------- Rate limiting en endpoints sensibles (login, registro, recuperación) ----------
builder.Services.Configure<RateLimitingOptions>(config.GetSection(RateLimitingOptions.SectionName));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.AuthRateLimitPolicy, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        // Partición por IP: frena fuerza bruta y relleno de credenciales desde un mismo origen.
        // (El bloqueo por cuenta complementa esto frente a ataques distribuidos.)
        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.AuthPermitLimit,
                Window = TimeSpan.FromSeconds(settings.AuthWindowSeconds),
                QueueLimit = 0,
            });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiadas solicitudes",
                Detail = "Has superado el límite de intentos. Espera un momento antes de volver a intentarlo.",
            },
        });
    };
});

// ---------- CORS (solo si el frontend se sirve desde otro origen) ----------
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ---------- Proxy inverso (nginx) ----------
// Solo se confía en X-Forwarded-For si la API está detrás de un proxy controlado; si no, cualquiera
// podría falsear su IP y saltarse el rate limiting.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (config.GetValue<bool>("ReverseProxy:TrustAllProxies"))
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

// ---------- Pipeline HTTP (el orden importa) ----------
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment())
{
    // HSTS solo se emite en peticiones HTTPS (detrás de un proxy con TLS).
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("usuarios-roles-base API"));
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() }),
    }),
}).AllowAnonymous();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapRoleEndpoints();
app.MapCatalogEndpoints();
if (app.Configuration.GetValue<bool>("Features:DevMailbox"))
{
    app.MapDevEndpoints();
}

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();

/// <summary>Opciones de rate limiting (sección "RateLimiting").</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; set; } = 10;

    public int AuthWindowSeconds { get; set; } = 60;
}

/// <summary>Expuesto para WebApplicationFactory en las pruebas de integración.</summary>
public partial class Program;
