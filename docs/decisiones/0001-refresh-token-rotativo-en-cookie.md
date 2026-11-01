# ADR 0001 — Access token en memoria + refresh token rotativo en cookie HttpOnly

- **Estado:** aceptada
- **Fecha:** 2026-11

## Contexto

Una SPA necesita mantener la sesión del usuario sin pedir la contraseña constantemente. Las opciones habituales son:

1. JWT de larga duración en `localStorage`.
2. Sesión de servidor con cookie.
3. JWT de vida corta + refresh token.

Guardar tokens en `localStorage` los expone a cualquier XSS (un script inyectado puede leerlos y enviarlos fuera). Un JWT de larga duración no se puede revocar fácilmente.

## Decisión

- **Access token JWT de 15 minutos**, devuelto en el cuerpo del login y guardado **solo en memoria** (variable del módulo `api/client.ts`).
- **Refresh token opaco** (256 bits) en una cookie `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`, de 7 días.
- **Rotación** en cada uso: el refresh token usado se revoca y se emite otro de la misma *familia*.
- **Detección de reuso**: si llega un refresh token ya revocado, se revoca **toda la familia** y se audita el evento.
- En BD se guarda solo el **hash SHA-256** del refresh token.
- El cliente renueva en modo **single-flight** (una sola renovación simultánea, y Web Locks entre pestañas).

## Consecuencias

**Positivas**
- Un XSS no puede robar el refresh token (HttpOnly) y el access token robado caduca pronto.
- `SameSite=Strict` + `Path=/api/auth` reducen el CSRF: la cookie no viaja en peticiones originadas en otros sitios ni a otros endpoints.
- Un refresh token robado tiene vida útil muy corta: al primer uso de cualquiera de las dos copias, la otra queda inservible y la familia se revoca.
- Logout, logout global y "cerrar sesiones" de un admin son revocaciones reales en BD.

**Negativas / a vigilar**
- Recargar la página pierde el access token: se recupera con una llamada a `/refresh` al iniciar (coste: una petición).
- Pestañas que renuevan exactamente a la vez pueden provocar un falso positivo de reuso. Se mitiga con Web Locks; un *periodo de gracia* está en el roadmap.
- Requiere mismo origen (o CORS con credenciales). Se resuelve con el proxy de Vite en desarrollo y nginx en Docker.
- Clientes no navegador (curl, móviles) deben manejar cookies (`curl -c/-b`).
