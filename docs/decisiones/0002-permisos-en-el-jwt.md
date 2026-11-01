# ADR 0002 — RBAC con permisos granulares incluidos en el JWT

- **Estado:** aceptada
- **Fecha:** 2026-11

## Contexto

Autorizar por **rol** (`[Authorize(Roles = "admin")]`) acopla el código a nombres de roles: crear un rol "soporte" que pueda leer la auditoría obligaría a cambiar código. Necesitamos que los administradores compongan roles libremente.

Además hay que decidir **dónde** se evalúan los permisos: consultando la BD en cada petición o leyéndolos del token.

## Decisión

- Los endpoints exigen **permisos** (`usuarios.leer`, `roles.editar`…), nunca roles. Los roles son solo agrupaciones de permisos editables desde el panel.
- El catálogo de permisos vive en **código** (`Domain/Security/Permissions.cs`) y se sincroniza a la BD al arrancar. Un permiso que no existe en el catálogo hace fallar el arranque si se usa en un endpoint.
- Se usa un **`IAuthorizationPolicyProvider` dinámico** (`perm:<codigo>`) + un `AuthorizationHandler`, de modo que basta con `.RequirePermission("x")`.
- Los permisos efectivos del usuario se incluyen como claims `permission` en el **access token** (15 min).

## Consecuencias

**Positivas**
- Autorización sin consultas a BD por petición (escala bien, la API es *stateless* para la autorización).
- Agregar un rol nuevo no requiere desplegar código.
- El frontend obtiene los mismos permisos (en `/auth/me` o en la respuesta de login) para ocultar acciones.

**Negativas / a vigilar**
- Un cambio de permisos o roles se aplica cuando el usuario **renueva** su access token (máximo 15 min, o inmediato si cierra sesión). Hay una prueba de integración que documenta este comportamiento.
- Bloquear o desactivar a un usuario revoca sus refresh tokens, pero su access token sigue siendo válido hasta expirar. Para revocación inmediata se puede añadir un "sello de seguridad" por usuario validado en `OnTokenValidated` (en el roadmap).
- Con muchos permisos el JWT crece; con cientos convendría usar un identificador de "versión de permisos" y caché en servidor.
