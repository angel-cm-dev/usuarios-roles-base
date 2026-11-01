# ADR 0003 — PostgreSQL en producción, SQLite en memoria para pruebas y desarrollo local

- **Estado:** aceptada
- **Fecha:** 2026-11

## Contexto

Queremos pruebas de integración **reales** (HTTP → middleware → EF Core → SQL) que corran en cualquier máquina y en el CI **sin Docker**. EF Core InMemory no es relacional: no aplica índices únicos, claves foráneas ni tokens de concurrencia, justo lo que protege reglas de seguridad como el "un solo uso" de los tokens.

## Decisión

- **Producción / Docker:** PostgreSQL 16 con **migraciones EF Core** (aplicadas al arrancar) y un script SQL idempotente generado (`backend/scripts/schema-postgres.sql`).
- **Pruebas:** **SQLite en memoria** (relacional real). Las unitarias usan `Data Source=:memory:`; las de integración levantan la API con `WebApplicationFactory` sobre una BD en memoria compartida (`mode=memory&cache=shared`) mantenida viva por una conexión abierta, una por clase de prueba.
- **Desarrollo sin Docker:** SQLite en archivo (`appsettings.Development.json`).
- El proveedor se elige por configuración (`Database:Provider`). Con PostgreSQL se usa `Migrate()`; con SQLite, `EnsureCreated()` (las migraciones son específicas de PostgreSQL).
- El código evita SQL específico de un proveedor: fechas como `DateTime` UTC (SQLite no ordena `DateTimeOffset`), agregaciones por día hechas en memoria sobre un rango acotado, búsquedas con `ToLower()/ToUpper()` traducibles en ambos.

## Consecuencias

**Positivas**
- `dotnet test` funciona en segundos sin servicios externos; el CI no necesita contenedores.
- Índices únicos, FKs y tokens de concurrencia se prueban de verdad.
- Arranque local inmediato para quien solo quiere ver la plantilla.

**Negativas / a vigilar**
- Diferencias entre motores (collation, tipos) podrían ocultar errores específicos de PostgreSQL. Mitigación recomendada para equipos: un job adicional con Testcontainers/PostgreSQL.
- El esquema de SQLite sale del modelo (`EnsureCreated`), no de las migraciones; el CI verifica con `has-pending-model-changes` que las migraciones están al día con el modelo.
