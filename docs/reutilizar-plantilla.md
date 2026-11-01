# Cómo reutilizar esta plantilla

Esta guía explica cómo partir de `usuarios-roles-base` para construir **tu** sistema (inventario, reservas, CRM…) sin rehacer la autenticación ni la autorización.

## 0. Antes de empezar

```bash
# Copia la plantilla (o usa "Use this template" en GitHub)
git clone https://github.com/angel-cm-dev/usuarios-roles-base mi-proyecto
cd mi-proyecto
rm -rf .git && git init
```

Verifica que todo funciona antes de cambiar nada:

```bash
cd backend && dotnet test && cd ../frontend && npm install && npm test && npm run build
```

## 1. Renombrar

| Qué | Dónde | Ejemplo |
|-----|-------|---------|
| Solución y proyectos | `backend/UsuariosRoles.sln`, carpetas `src/UsuariosRoles.*`, `tests/UsuariosRoles.*` | `MiProyecto.Domain`… |
| Namespaces | Todos los `.cs` (`namespace UsuariosRoles.…`) | `MiProyecto.…` |
| Referencias | `.csproj` (`ProjectReference`, `InternalsVisibleTo`), `Dockerfile` (rutas y `UsuariosRoles.Api.dll`), `ci.yml` | |
| Nombre del producto | `Directory.Build.props` (`Product`), `JwtOptions` (`Issuer`, `Audience`), `appsettings.json` | `mi-proyecto` |
| Cookie del refresh | `AuthCookie:Name` en `appsettings.json` (y `urb_refresh` en `tests/.../ApiFactory.cs`) | `mp_refresh` |
| Clave de bloqueo entre pestañas | `frontend/src/api/client.ts` (`urb-refresh`) | `mp-refresh` |
| Textos del panel | `index.html` (`<title>`), `AppLayout.tsx`, `AuthLayout.tsx`, `EmailTemplates.cs`, `favicon.svg` | |
| Paquete npm | `frontend/package.json` (`name`) | |
| Proyecto compose | `docker-compose.yml` (`name:`) y nombre de la BD | |

Script orientativo (Git Bash / Linux / macOS):

```bash
cd backend
for d in src/UsuariosRoles.* tests/UsuariosRoles.*; do git mv "$d" "${d/UsuariosRoles/MiProyecto}"; done
find . -type f \( -name "*.cs" -o -name "*.csproj" -o -name "*.sln" -o -name "Dockerfile" \) \
  -not -path "*/bin/*" -not -path "*/obj/*" -exec sed -i 's/UsuariosRoles/MiProyecto/g' {} +
for f in $(find . -name "UsuariosRoles*.csproj"); do git mv "$f" "${f//UsuariosRoles/MiProyecto}"; done
git mv UsuariosRoles.sln MiProyecto.sln
sed -i 's/UsuariosRoles/MiProyecto/g' ../.github/workflows/ci.yml
dotnet build && dotnet test
```

> Las migraciones existentes conservan su nombre de clase; no hace falta regenerarlas.

## 2. Agregar un permiso

Ejemplo: permiso para exportar usuarios.

**1. Backend — catálogo** (`backend/src/UsuariosRoles.Domain/Security/Permissions.cs`):

```csharp
public static class Usuarios
{
    // ...
    public const string Exportar = "usuarios.exportar";
}

public static IReadOnlyList<PermissionDefinition> All { get; } =
[
    // ...
    new(Usuarios.Exportar, "usuarios", "Exportar el listado de usuarios a CSV"),
];
```

**2. Backend — proteger el endpoint**:

```csharp
group.MapGet("/export", async (UserService users, CancellationToken ct) => /* ... */)
    .RequirePermission(P.Usuarios.Exportar);
```

**3. Frontend — espejo del catálogo** (`frontend/src/auth/permissions.ts`) y uso en la UI:

```ts
usuarios: { /* ... */ exportar: 'usuarios.exportar' },
```

```tsx
<Can permission={PERMISSIONS.usuarios.exportar}>
  <Button onClick={exportCsv}>Exportar</Button>
</Can>
```

**4. Arranca la API.** El seed inserta el permiso en `permissions` y lo asigna al rol `admin`. Para otros roles, asígnalo desde **Roles → Permisos** en el panel.

No hace falta migración: los permisos son **datos**, no esquema. Si usas en un endpoint un código que no está en `Permissions.All`, la API falla al arrancar (error temprano en vez de un 403 misterioso).

## 3. Agregar un módulo de negocio

Ejemplo: módulo **Proyectos** (CRUD) protegido por permisos.

1. **Domain** — entidad con sus reglas: `Domain/Entities/Project.cs` (constructor que valida, métodos como `Archive()`).
2. **Domain** — permisos `proyectos.leer`, `proyectos.crear`, `proyectos.editar` en `Permissions.cs` (sección anterior).
3. **Application**:
   - Agrega `DbSet<Project> Projects { get; }` a `IApplicationDbContext`.
   - `Projects/ProjectDtos.cs`: `ProjectListQuery : PagedQuery`, DTOs y validadores FluentValidation (`AbstractValidator<CreateProjectRequest>`; se registran solos).
   - `Projects/ProjectService.cs`: lista con `ToPagedResultAsync`, `OrderByDirection`, excepciones `NotFoundException`/`ConflictException`, y `IAuditLogger` si es una acción sensible.
   - Registra el servicio en `Application/DependencyInjection.cs`.
4. **Infrastructure**:
   - `DbSet` en `AppDbContext` y un `IEntityTypeConfiguration<Project>` en `Configurations.cs` (tabla `projects`, longitudes, índices).
   - Migración: `dotnet tool run dotnet-ef migrations add AddProjects --project src/UsuariosRoles.Infrastructure --startup-project src/UsuariosRoles.Infrastructure --output-dir Persistence/Migrations`.
5. **Api** — `Endpoints/ProjectEndpoints.cs` siguiendo el patrón de `AdminEndpoints.cs`:

   ```csharp
   var group = app.MapGroup("/api/projects").WithTags("Proyectos").RequireAuthorization();
   group.MapGet("/", async ([AsParameters] ProjectListQuery q, ProjectService s, CancellationToken ct) =>
           TypedResults.Ok(await s.ListAsync(q, ct)))
       .RequirePermission(P.Proyectos.Leer);
   group.MapPost("/", async (CreateProjectRequest r, ProjectService s, CancellationToken ct) => /* ... */)
       .Validate<CreateProjectRequest>()
       .RequirePermission(P.Proyectos.Crear);
   ```

   y llama a `app.MapProjectEndpoints()` en `Program.cs`.
6. **Pruebas** — unitarias para las reglas de la entidad y una clase de integración con `IClassFixture<ApiFactory>` que compruebe 401/403/200.
7. **Frontend**:
   - Tipos en `api/types.ts` y funciones en `api/services.ts` (`projectsApi.list`, …).
   - Página `pages/ProjectsPage.tsx` con `usePagedQuery` + `DataTable` + `Pagination` (copia `PermissionsPage.tsx` como punto de partida).
   - Ruta en `App.tsx` envuelta en `<RequirePermission permission={PERMISSIONS.proyectos.leer}>` y entrada en `NAV_ITEMS` de `AppLayout.tsx` con su `permission`.

## 4. Cambiar `IEmailSender` por SMTP real

La aplicación solo conoce la interfaz:

```csharp
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
```

**1.** Agrega MailKit (recomendado sobre `System.Net.Mail.SmtpClient`, que Microsoft no recomienda para código nuevo):

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="MailKit" Version="4.x.x" />
<!-- UsuariosRoles.Infrastructure.csproj -->
<PackageReference Include="MailKit" />
```

**2.** Implementación (`Infrastructure/Email/SmtpEmailSender.cs`):

```csharp
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using UsuariosRoles.Application.Common;

namespace UsuariosRoles.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "no-reply@midominio.pe";
    public string FromName { get; set; } = "Mi Proyecto";
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(o.Host, o.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(o.User, o.Password, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
```

**3.** Registro en `Infrastructure/DependencyInjection.cs`, eligiendo por configuración:

```csharp
if (configuration["Email:Provider"] == "Smtp")
{
    services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("Email:Smtp"));
    services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    services.AddSingleton<DevMailbox>();
    services.AddSingleton<IEmailSender, DevMailboxEmailSender>();
}
```

> Si eliminas `DevMailbox` del contenedor, no registres `MapDevEndpoints()` (o deja `Features:DevMailbox=false`).

**4.** Configuración por entorno (nunca credenciales en el repo):

```bash
Email__Provider=Smtp
Email__Smtp__Host=smtp.midominio.pe
Email__Smtp__User=no-reply@midominio.pe
Email__Smtp__Password=********
Features__DevMailbox=false
AppUrls__FrontendBaseUrl=https://app.midominio.pe
```

**Recomendaciones de producción:** envía los correos en segundo plano (cola/outbox con reintentos) para no bloquear el login ni filtrar por latencia si un email existe; usa un proveedor transaccional (SES, SendGrid, Mailgun) con SPF/DKIM/DMARC configurados.

## 5. Ajustar la seguridad

Todo es configurable sin tocar código (`appsettings.json` o variables de entorno):

```bash
Security__Password__MinLength=12
Security__Password__RequireSymbol=false
Security__Lockout__MaxFailedAttempts=10
Security__Lockout__LockoutMinutes=30
Security__Tokens__RefreshTokenDays=14
Jwt__AccessTokenMinutes=10
RateLimiting__AuthPermitLimit=5
```

Si cambias la política de contraseñas, actualiza también `frontend/src/lib/passwordStrength.ts` para que el medidor muestre las mismas reglas.

## 6. Checklist de producción

- [ ] `Jwt__Key` aleatoria de ≥ 32 bytes, guardada en un gestor de secretos (Key Vault, AWS Secrets Manager, variables cifradas del CI).
- [ ] HTTPS de extremo a extremo y `AuthCookie__Secure=true`.
- [ ] `Seed__DemoUsers=false` y crear el primer admin de forma controlada (o cambiar su contraseña al primer arranque).
- [ ] `Features__DevMailbox=false` y `IEmailSender` real.
- [ ] `OpenApi__Enabled=false` si la API no debe ser pública.
- [ ] `ReverseProxy__TrustAllProxies=true` **solo** si la API no es accesible directamente (si no, configura `KnownProxies`).
- [ ] Migraciones aplicadas como paso del despliegue si hay varias réplicas.
- [ ] Logs JSON enviados a tu plataforma de observabilidad; alertas sobre `auth.refresh-token-reuse` y picos de `auth.login-failed`.
- [ ] Copias de seguridad de PostgreSQL y política de retención de `audit_events`.
