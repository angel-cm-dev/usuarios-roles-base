# Guía de la API con ejemplos `curl`

Base: `http://localhost:5080` (sin Docker o puerto expuesto de Docker) o `http://localhost:8080` (vía nginx). Documentación interactiva: `/scalar`. Especificación OpenAPI: `/openapi/v1.json`.

> El refresh token viaja en una **cookie HttpOnly**. Con `curl` usa un *cookie jar*: `-c cookies.txt` para guardarla y `-b cookies.txt` para enviarla.

```bash
API=http://localhost:5080
```

## Errores

Todas las respuestas de error son `application/problem+json` con `code` estable:

| HTTP | `code` | Cuándo |
|------|--------|--------|
| 400 | `validation_error` | Campos inválidos (`errors` por campo) |
| 400 | `invalid_token` | Token de verificación/reset inválido, usado o vencido |
| 400 | `last_admin`, `system_role`, `admin_role_locked`, `cannot_lock_self`… | Reglas de negocio |
| 401 | `unauthorized`, `invalid_credentials`, `invalid_refresh_token`, `refresh_token_reused` | Autenticación |
| 403 | `forbidden`, `email_not_verified`, `account_disabled` | Sin permiso / cuenta no habilitada |
| 404 | `not_found` | Recurso inexistente |
| 409 | `email_taken`, `role_name_taken`, `role_in_use`, `concurrency_conflict` | Conflictos |
| 423 | `account_locked` | Cuenta bloqueada (incluye `lockoutEnd`) |
| 429 | `too_many_requests` | Rate limit (cabecera `Retry-After`) |

## 1. Registro y verificación

```bash
curl -i -X POST $API/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"lucia@ejemplo.pe","fullName":"Lucía Ramos","password":"Registro#2026"}'
# 202 {"message":"Registro recibido. Si el email corresponde a una cuenta, recibirás un correo..."}

# Ver el correo simulado (requiere Features:DevMailbox=true)
curl -s "$API/api/dev/mailbox?to=lucia@ejemplo.pe"

# Verificar con el token del enlace
curl -X POST $API/api/auth/verify-email -H "Content-Type: application/json" \
  -d '{"token":"<TOKEN_DEL_CORREO>"}'
```

Contraseña débil → 400:

```json
{ "status": 400, "code": "validation_error",
  "errors": { "password": ["Debe tener al menos 8 caracteres.", "Debe incluir al menos una letra mayúscula."] } }
```

## 2. Login, perfil y refresh

```bash
curl -s -c cookies.txt -X POST $API/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.pe","password":"Demo123!"}'
```

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "expiresAt": "2026-11-01T15:15:00Z",
  "user": { "id": "…", "email": "admin@demo.pe", "fullName": "Administrador Demo",
            "roles": ["admin"], "permissions": ["auditoria.leer", "dashboard.ver", "…"] }
}
```

```bash
TOKEN=<accessToken>
curl -s $API/api/auth/me -H "Authorization: Bearer $TOKEN"

# Renovar: envía la cookie y guarda la nueva (rotación)
curl -s -b cookies.txt -c cookies.txt -X POST $API/api/auth/refresh
```

### Ver la detección de reuso

```bash
cp cookies.txt viejo.txt                                   # copia del refresh token actual
curl -s -b cookies.txt -c cookies.txt -X POST $API/api/auth/refresh   # rota → token nuevo
curl -i -b viejo.txt -X POST $API/api/auth/refresh                    # reusa el viejo
# 401 {"code":"refresh_token_reused", ...}  → toda la familia queda revocada
curl -i -b cookies.txt -X POST $API/api/auth/refresh                  # el "nuevo" también falla: 401
```

## 3. Logout

```bash
curl -i -b cookies.txt -X POST $API/api/auth/logout            # 204, revoca la sesión actual
curl -i -X POST $API/api/auth/logout-all -H "Authorization: Bearer $TOKEN"   # 204, todas las sesiones
```

## 4. Recuperación de contraseña

```bash
curl -X POST $API/api/auth/forgot-password -H "Content-Type: application/json" \
  -d '{"email":"usuario@demo.pe"}'                     # 202 siempre (anti-enumeración)

curl -X POST $API/api/auth/reset-password -H "Content-Type: application/json" \
  -d '{"token":"<TOKEN_DEL_CORREO>","newPassword":"Renovada#2026"}'   # 200 (2.º uso → 400 invalid_token)

# Cambio de contraseña autenticado (devuelve una sesión nueva y revoca las demás)
curl -s -c cookies.txt -X POST $API/api/auth/change-password \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"currentPassword":"Demo123!","newPassword":"Nueva#Clave2026"}'
```

## 5. Usuarios (panel de administración)

```bash
# Lista paginada con filtros y orden
curl -s "$API/api/users?page=1&pageSize=10&search=demo&status=active&role=admin&sortBy=email&sortDir=asc" \
  -H "Authorization: Bearer $TOKEN"
```

```json
{ "items": [ { "id": "…", "email": "admin@demo.pe", "fullName": "Administrador Demo", "isActive": true,
               "emailConfirmed": true, "isLockedOut": false, "roles": ["admin"], "…": "…" } ],
  "page": 1, "pageSize": 10, "totalCount": 1, "totalPages": 1 }
```

`status`: `active` · `inactive` · `locked` · `unverified`. `sortBy`: `email` · `fullName` · `createdAt` · `lastLoginAt`.

```bash
# Crear (usuarios.crear)
curl -s -X POST $API/api/users -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"email":"soporte@ejemplo.pe","fullName":"Agente Soporte","password":"Soporte#2026","emailConfirmed":true,"roleIds":[]}'

# Editar nombre/estado (usuarios.editar)
curl -s -X PUT $API/api/users/<ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"fullName":"Agente de Soporte","isActive":true}'

# Asignar roles (usuarios.asignar-roles)
curl -s -X PUT $API/api/users/<ID>/roles -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"roleIds":["<ROLE_ID>"]}'

# Bloquear 30 min (sin "minutes" = indefinido), desbloquear, cerrar sesiones (usuarios.bloquear)
curl -s -X POST $API/api/users/<ID>/lock -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"minutes":30}'
curl -s -X POST $API/api/users/<ID>/unlock -H "Authorization: Bearer $TOKEN"
curl -s -X POST $API/api/users/<ID>/revoke-sessions -H "Authorization: Bearer $TOKEN"
```

## 6. Roles y permisos

```bash
curl -s "$API/api/roles?search=sop&sortBy=userCount&sortDir=desc" -H "Authorization: Bearer $TOKEN"

curl -s -X POST $API/api/roles -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"soporte","description":"Mesa de ayuda","permissions":["usuarios.leer","auditoria.leer"]}'

curl -s -X PUT $API/api/roles/<ROLE_ID>/permissions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"permissions":["usuarios.leer","usuarios.bloquear"]}'

curl -i -X DELETE $API/api/roles/<ROLE_ID> -H "Authorization: Bearer $TOKEN"   # 204 o 409 role_in_use

curl -s "$API/api/permissions?module=usuarios" -H "Authorization: Bearer $TOKEN"
curl -s "$API/api/permissions/modules" -H "Authorization: Bearer $TOKEN"
```

## 7. Auditoría y dashboard

```bash
curl -s "$API/api/audit?eventType=auth.login-failed&succeeded=false&from=2026-11-01T00:00:00Z&pageSize=20" \
  -H "Authorization: Bearer $TOKEN"
curl -s "$API/api/audit/event-types" -H "Authorization: Bearer $TOKEN"
curl -s "$API/api/dashboard/stats" -H "Authorization: Bearer $TOKEN"
```

## 8. Autorización: 401 vs 403

```bash
curl -i $API/api/users                                   # 401 {"code":"unauthorized"}

USER_TOKEN=$(curl -s -X POST $API/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"usuario@demo.pe","password":"Demo123!"}' | jq -r .accessToken)
curl -i $API/api/users -H "Authorization: Bearer $USER_TOKEN"   # 403 {"code":"forbidden"}
```

## 9. Rate limiting

```bash
for i in $(seq 1 12); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST $API/api/auth/login \
    -H "Content-Type: application/json" -d '{"email":"x@x.pe","password":"x"}'
done
# 401 ×10 y luego 429 (con cabecera Retry-After)
```

## 10. Salud

```bash
curl -s $API/health
# {"status":"Healthy","checks":[{"name":"database","status":"Healthy"}]}
```
