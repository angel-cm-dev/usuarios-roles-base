# Arquitectura

Este documento explica **cómo está organizado** el sistema, **cómo viaja una petición** y **cómo funcionan los tokens**. Está pensado para que puedas modificar la plantilla con seguridad.

## 1. Capas del backend

```mermaid
flowchart TB
    API["UsuariosRoles.Api<br/>Program.cs · Endpoints · Autorización · Middleware"]
    APP["UsuariosRoles.Application<br/>Casos de uso · DTOs · Validadores · Interfaces"]
    INF["UsuariosRoles.Infrastructure<br/>EF Core · Migraciones · Seed · JWT · Hashing · Email"]
    DOM["UsuariosRoles.Domain<br/>Entidades · Reglas · Catálogo de permisos"]

    API --> APP
    API --> INF
    INF --> APP
    APP --> DOM
```

La regla de dependencias es: **las flechas apuntan hacia el dominio**. El dominio no conoce EF Core ni ASP.NET Core.

| Capa | Responsabilidad | Ejemplos |
|------|-----------------|----------|
| **Domain** | Entidades con sus invariantes. Sin dependencias. | `User.RegisterFailedLogin()` decide cuándo bloquear; `RefreshToken.Revoke()` es idempotente; `UserToken.TryConsume()` garantiza un solo uso; `Permissions` es el catálogo. |
| **Application** | Orquesta casos de uso. Define **puertos** (interfaces) que necesita. | `AuthService`, `RefreshTokenService`, `UserService`, `RoleService`; `IApplicationDbContext`, `IEmailSender`, `IPasswordHasher`, `IAccessTokenGenerator`, `IRequestContext`; validadores FluentValidation; `PasswordPolicy`. |
| **Infrastructure** | Implementa los puertos con tecnología concreta. | `AppDbContext` (EF Core, snake_case, fechas UTC), migraciones PostgreSQL, `DataSeeder`, `JwtAccessTokenGenerator`, `IdentityPasswordHasher`, `SecureTokenGenerator`, `DevMailboxEmailSender`. |
| **Api** | Composición (DI) y HTTP. | Minimal APIs por módulo, `RequirePermission`, `PermissionPolicyProvider`, `GlobalExceptionHandler`, `ValidationFilter`, `SecurityHeadersMiddleware`, rate limiting, Serilog, OpenAPI. |

**¿Por qué Application usa `DbSet` a través de `IApplicationDbContext`?** Es un compromiso pragmático: evitamos un repositorio genérico que solo reenvía llamadas a EF Core, pero Application sigue sin conocer el proveedor (PostgreSQL/SQLite) ni el `DbContext` concreto. Las pruebas unitarias usan el mismo contexto sobre SQLite en memoria.

## 2. Flujo de una petición

Ejemplo: `GET /api/users?search=ana&status=locked` con un access token válido.

```mermaid
sequenceDiagram
    autonumber
    participant C as Cliente (React)
    participant N as nginx
    participant M as Middleware ASP.NET
    participant E as Endpoint /api/users
    participant S as UserService
    participant DB as PostgreSQL

    C->>N: GET /api/users (Authorization: Bearer JWT)
    N->>M: proxy + X-Forwarded-For
    M->>M: ForwardedHeaders → IP real del cliente
    M->>M: SecurityHeaders (CSP, nosniff, no-store…)
    M->>M: Authentication: valida firma, issuer, audience, expiración
    M->>M: Authorization: política "perm:usuarios.leer" → ¿claim permission?
    alt sin token / token inválido
        M-->>C: 401 ProblemDetails {code: "unauthorized"}
    else sin permiso
        M-->>C: 403 ProblemDetails {code: "forbidden"}
    end
    M->>E: [AsParameters] UserListQuery
    E->>S: ListAsync(query)
    S->>DB: SELECT … WHERE … ORDER BY … LIMIT/OFFSET
    DB-->>S: filas + COUNT
    S-->>E: PagedResult<UserListItemDto>
    E-->>C: 200 JSON {items, page, pageSize, totalCount, totalPages}
```

Orden del pipeline (en `Program.cs`, **el orden importa**):

1. `UseForwardedHeaders` — antes que todo, para que el rate limiting y la auditoría vean la IP real.
2. `UseExceptionHandler` + `UseStatusCodePages` — toda excepción o respuesta de error vacía sale como ProblemDetails.
3. `SecurityHeadersMiddleware` y HSTS (fuera de Development).
4. `UseSerilogRequestLogging` — una línea estructurada por petición.
5. `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter`.

Los errores de negocio se lanzan como excepciones tipadas (`NotFoundException`, `ConflictException`, `BusinessRuleException`, `AccountLockedException`, `RequestValidationException`…) y el `GlobalExceptionHandler` las traduce a un único formato:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Datos inválidos",
  "status": 400,
  "detail": "Uno o más campos no son válidos.",
  "code": "validation_error",
  "errors": { "password": ["Debe incluir al menos un número."] },
  "traceId": "0HN…",
  "instance": "/api/auth/register"
}
```

## 3. Tokens: diseño

| Token | Formato | Vida | Dónde vive | Qué se guarda en BD |
|-------|---------|------|-----------|---------------------|
| Access token | JWT HS256 con `sub`, `email`, `name`, `role[]`, `permission[]`, `jti` | 15 min | **Memoria** del navegador (variable JS) | Nada (es autocontenido) |
| Refresh token | Opaco, 256 bits aleatorios (Base64Url) | 7 días, rota en cada uso | Cookie `HttpOnly; SameSite=Strict; Path=/api/auth` | SHA-256 del valor + familia + estado |
| Verificación de email | Opaco, 256 bits | 24 h, un solo uso | Enlace en el correo | SHA-256 + propósito + `ConsumedAt` |
| Recuperación de contraseña | Opaco, 256 bits | 30 min, un solo uso | Enlace en el correo | SHA-256 + propósito + `ConsumedAt` |

¿Por qué SHA-256 y no PBKDF2 para los tokens? Porque son **aleatorios y largos** (256 bits): no se pueden adivinar por diccionario, así que no necesitan un hash lento. Las contraseñas, que sí son adivinables, usan PBKDF2 con 100 000 iteraciones.

### 3.1 Login

```mermaid
sequenceDiagram
    autonumber
    participant U as Navegador
    participant A as API
    participant DB as BD

    U->>A: POST /api/auth/login {email, password}
    A->>A: Rate limit por IP (10/min)
    A->>DB: Buscar usuario por email normalizado
    alt no existe
        A->>A: Verifica contra un hash ficticio (igualar tiempos)
        A->>DB: Audit auth.login-failed
        A-->>U: 401 invalid_credentials
    else bloqueado
        A-->>U: 423 account_locked
    else contraseña incorrecta
        A->>DB: AccessFailedCount++ (al 5.º → LockoutEnd = ahora + 15 min)
        A-->>U: 401 invalid_credentials (o 423 si se acaba de bloquear)
    else email sin verificar / cuenta inactiva
        A-->>U: 403 email_not_verified / account_disabled
    else OK
        A->>DB: Reiniciar contador, LastLoginAt, nuevo RefreshToken (familia nueva, solo hash)
        A->>DB: Audit auth.login-succeeded
        A-->>U: 200 {accessToken, expiresIn, user} + Set-Cookie urb_refresh (HttpOnly)
    end
```

Los chequeos de "email sin verificar" y "cuenta inactiva" van **después** de validar la contraseña: así no revelan el estado de una cuenta a quien no conoce la contraseña.

### 3.2 Refresh con rotación

```mermaid
sequenceDiagram
    autonumber
    participant U as Navegador
    participant A as API
    participant DB as BD

    Note over U: El access token expiró → una petición devuelve 401
    U->>A: POST /api/auth/refresh (cookie urb_refresh = R1)
    A->>DB: Buscar por SHA-256(R1)
    A->>A: R1 activo (no revocado, no expirado)
    A->>DB: Crear R2 (misma FamilyId) · R1.RevokedAt = ahora, ReplacedBy = R2
    Note over A,DB: RevokedAt es token de concurrencia:<br/>dos rotaciones simultáneas de R1 → solo una gana
    A-->>U: 200 {accessToken nuevo (roles/permisos actualizados)} + Set-Cookie R2
    U->>U: Reintenta la petición original con el nuevo access token
```

El cliente hace la renovación **single-flight**: si varias peticiones reciben 401 a la vez, todas esperan **una sola** llamada a `/refresh` (y entre pestañas se serializa con la Web Locks API). Si no, el navegador enviaría R1 dos veces y el servidor lo interpretaría como reuso.

### 3.3 Detección de reuso

```mermaid
sequenceDiagram
    autonumber
    participant V as Víctima
    participant X as Atacante (robó R1)
    participant A as API
    participant DB as BD

    V->>A: refresh con R1
    A->>DB: R1 → revocado ("rotated"), emite R2
    A-->>V: R2
    X->>A: refresh con R1 (copia robada)
    A->>DB: R1 ya está revocado → ¡REUSO!
    A->>DB: Revocar TODA la familia (R2 incluido) · Audit auth.refresh-token-reuse
    A-->>X: 401 refresh_token_reused
    V->>A: refresh con R2
    A-->>V: 401 (familia revocada) → debe iniciar sesión de nuevo
```

No podemos saber quién es el legítimo, así que se corta la sesión para ambos. El usuario real vuelve a autenticarse; el atacante, sin la contraseña, ya no puede. Si el atacante usara R1 **antes** que la víctima, ocurre lo mismo en el siguiente refresh de la víctima.

### 3.4 Recuperación de contraseña

```mermaid
sequenceDiagram
    autonumber
    participant U as Usuario
    participant A as API
    participant M as IEmailSender
    participant DB as BD

    U->>A: POST /auth/forgot-password {email}
    A->>DB: ¿Existe y está activo?
    opt existe
        A->>DB: Invalidar tokens de reset previos · guardar SHA-256(T) (30 min)
        A->>M: Correo con /restablecer-password?token=T
    end
    A-->>U: 202 mismo mensaje exista o no (anti-enumeración)
    U->>A: POST /auth/reset-password {token: T, newPassword}
    A->>DB: Buscar SHA-256(T), propósito PasswordReset, sin usar, no vencido
    A->>A: Política de contraseñas (incluye "no contener el email")
    A->>DB: Nuevo hash · ConsumedAt = ahora (token de concurrencia) · email confirmado · desbloqueo
    A->>DB: Revocar TODOS los refresh tokens · Audit
    A->>M: Aviso "tu contraseña fue cambiada"
    A-->>U: 200
```

## 4. Autorización por permisos

```mermaid
flowchart LR
    U[Usuario] -- N:M --> R[Rol]
    R -- N:M --> P[Permiso]
    P -. "claims 'permission' en el JWT" .-> T[Access token]
    T --> H{PermissionAuthorizationHandler}
    E["Endpoint<br/>.RequirePermission('roles.editar')"] --> PP[PermissionPolicyProvider<br/>crea la política 'perm:roles.editar']
    PP --> H
```

- `RequirePermission("x")` registra la política `perm:x` en el endpoint (y falla al arrancar si `x` no está en el catálogo).
- `PermissionPolicyProvider` crea esa política al vuelo: requiere usuario autenticado + `PermissionRequirement("x")`.
- `PermissionAuthorizationHandler` comprueba el claim `permission` del JWT.

Los permisos viajan en el JWT (sin consulta a BD por petición). Un cambio de roles/permisos se aplica **en la siguiente renovación** del access token (≤ 15 min). Ver [ADR 0002](decisiones/0002-permisos-en-el-jwt.md).

## 5. Frontend

```mermaid
flowchart TB
    main[main.tsx] --> AP[AuthProvider]
    AP --> R[Rutas: GuestOnly / RequireAuth / RequirePermission]
    R --> L[AppLayout: sidebar filtrado por permisos]
    L --> P[Páginas]
    P --> S[api/services.ts]
    S --> C[api/client.ts<br/>token en memoria · refresh single-flight · ApiError]
    C -->|fetch credentials: include| API[(API /api)]
```

- **`api/client.ts`**: adjunta el access token, ante 401 intenta un refresh y reintenta una vez; convierte ProblemDetails en `ApiError` (con `code` y errores por campo).
- **`AuthContext`**: al cargar la app intenta recuperar la sesión con la cookie (el access token no sobrevive a un F5 porque vive en memoria, a propósito).
- **`<Can permission>` / `RequirePermission`**: ocultan acciones y rutas. Es solo UX: la API valida siempre.
- **`usePagedQuery`**: estado de tablas (página, tamaño, búsqueda con debounce, filtros, orden) con cancelación de peticiones obsoletas (`AbortController`).

## 6. Despliegue

```mermaid
flowchart LR
    B[Navegador] -->|:8080| N[nginx-unprivileged<br/>SPA + /api proxy]
    N -->|backend:8080| A[aspnet:10.0<br/>usuario no root]
    A -->|db:5432| P[(postgres:16)]
```

- La API corre como el usuario sin privilegios `$APP_UID` de la imagen oficial; nginx usa la variante *unprivileged*.
- La API aplica migraciones y seed al arrancar (idempotente). Para entornos con varias réplicas conviene aplicar migraciones en un paso previo del pipeline usando `backend/scripts/schema-postgres.sql` o `dotnet ef database update`.
