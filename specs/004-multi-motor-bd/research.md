# Research — Soporte Multi-Motor de Base de Datos (Feature 004)

**Phase 0 output** · Branch `004-multi-motor-bd` · 2026-08-03

Formato por decisión: **Decision** / **Rationale** / **Alternatives considered**.

---

## D-01 — Patrón de selección de proveedor

**Decision**: sección `Database` bindeada a `DatabaseOptions` (`Provider`, `ConnectionStrings:{PostgreSQL,SqlServer}`, `AutoMigrate`, `Seed:{RunParametricSeed,RunTestSeed}`, `Startup:{RetryWindowSeconds,RetryIntervalSeconds}`) con `IValidateOptions<DatabaseOptions>` + `ValidateOnStart()` para fail-fast. Un `IDbProviderConfigurator` (implementaciones `SqlServerProviderConfigurator`, `PostgreSqlProviderConfigurator`) registrado como singleton según el provider activo; los tres `AddDbContext` existentes (Application, Admin, Tenant) y el de Identity delegan en él. Variables de entorno estándar de .NET (`Database__Provider`, `Database__ConnectionStrings__PostgreSQL`) tienen precedencia sobre appsettings sin código adicional.

**Rationale**: `ValidateOnStart` es el mecanismo idiomático .NET para fail-fast de configuración (el host no arranca y el error nombra la propiedad). Un configurador único evita repetir el `switch` de proveedor en 4 puntos de registro y da el lugar natural para las opciones específicas (`EnableRetryOnFailure`, `MigrationsAssembly`, `MigrationsHistoryTable`).

**Alternatives considered**: (a) `switch` inline en cada `AddDbContext` — duplica lógica en Persistence e Identity y viola la centralización exigida por FR-005; (b) dos DbContexts por proveedor — explota el número de tipos y rompe la premisa del spec de contexts únicos; (c) provider por reflexión/plugins — sobreingeniería para exactamente dos motores conocidos.

## D-02 — Ensamblados de migraciones por proveedor

**Decision**: dos proyectos nuevos, `IngenIA365ERP.Persistence.Migrations.SqlServer` y `IngenIA365ERP.Persistence.Migrations.PostgreSql`, cada uno con carpetas `Admin/` y `Application/` (migraciones de `AdminDbContext` y `ApplicationDbContext` respectivamente) y sus `IDesignTimeDbContextFactory` que leen `DB_PROVIDER` + connection string de diseño. `MigrationsAssembly` se fija desde el configurador según el provider activo. Las migraciones Admin existentes (`Persistence/Migrations/Admin`) se **regeneran** en el ensamblado SQL Server (greenfield — sin baseline que preservar) y se elimina la carpeta vieja.

**Rationale**: EF Core exige un árbol de migraciones (y `ModelSnapshot`) por proveedor; separarlos en ensamblados es la práctica documentada por Microsoft para multi-proveedor y evita colisiones de snapshot en `Persistence`. `TenantDbContext` comparte la BD Admin y su esquema ya lo cubren las migraciones de `AdminDbContext` (mismas tablas `ADM_*`) — no necesita árbol propio; se valida en Phase 1 de implementación.

**Alternatives considered**: (a) un solo árbol de migraciones con `if (ActiveProvider == ...)` dentro de cada migración — patrón frágil, los snapshots divergen y EF lo desaconseja; (b) carpetas por proveedor dentro de `Persistence` — funciona pero obliga a compilar siempre ambos proveedores en el ensamblado principal y ensucia el snapshot; los proyectos separados dan frontera limpia y Architecture tests simples.

## D-03 — Aprovisionamiento de tenant multi-proveedor (schema-per-tenant)

**Decision**: `TenantSchemaService` (existente) se vuelve provider-aware: crea el esquema del tenant (`CREATE SCHEMA` en ambos motores) y aplica el **script idempotente generado** del árbol Application del proveedor activo, con traducción de esquema (los objetos del modelo tenant se generan sin esquema fijo / esquema por defecto y el servicio los ejecuta con `search_path` (PG) o esquema default del usuario/`sp_executesql` con reemplazo controlado (SQL Server)). El historial de migraciones del tenant vive en `__EFMigrationsHistory` dentro del esquema del tenant, lo que permite al inicializador detectar tenants desactualizados y migrarlos al arranque.

**Rationale**: `Database.Migrate()` de EF apunta a un esquema fijo por modelo; para N esquemas idénticos la práctica establecida es aplicar el script de migración por esquema con el `search_path`/default schema conmutado, manteniendo el historial por esquema. Es el mínimo cambio sobre el mecanismo ya existente (`TenantSchemaService`) y funciona igual en ambos motores.

**Alternatives considered**: (a) database-per-tenant — cambiaría el modelo operativo actual (schema-per-tenant es principio IV) y multiplica la administración; (b) `IModelCacheKeyFactory` + un `DbContext` re-modelado por esquema ejecutando `Migrate()` por tenant — soportado pero costoso en memoria (un modelo compilado por tenant) con 270 entidades × 100 tenants; (c) DDL manual por tenant — reintroduce el doble mantenimiento que la clarificación #1 eliminó.

**Resultado del spike T025 (2026-08-04)**: `search_path` queda descartado como mecanismo único — SQL Server no tiene default-schema por sesión, y las migraciones generadas llevan `schema: "dbo"` explícito (el modelo usa `HasDefaultSchema`). Estrategia final implementada:
1. **BD administrativa y esquema operativo default (`dbo`)**: `Database.MigrateAsync()` directo del árbol del proveedor activo (historial en `dbo.__EFMigrationsHistory`).
2. **Esquemas de tenant (`tenant_*`)**: `TenantSchemaService` genera el **script idempotente** del árbol del proveedor (`IMigrator.GenerateScript(idempotent)`) y lo aplica con **reemplazo controlado del esquema** (`[dbo]`/`"dbo"`/`'dbo'` → esquema del tenant), historial `__EFMigrationsHistory` incluido dentro del esquema del tenant (detección de pendientes por tenant via consulta directa a esa tabla).
3. **Runtime**: se añade el `IModelCacheKeyFactory` keyed por esquema (hallazgo del spike: no existía — el modelo del primer tenant quedaba cacheado para todos; bug latente pre-004).

## D-04 — Lock de migración para instancias concurrentes

**Decision**: lock nativo del motor tomado por el inicializador antes de migrar/sembrar: `sp_getapplock` (`@LockOwner='Session'`, recurso `ingenia365:db-init`) en SQL Server; `pg_advisory_lock(hashtext('ingenia365:db-init'))` en PostgreSQL. Encapsulado en `MigrationLock` con `IAsyncDisposable`. Las instancias que no obtienen el lock esperan y re-verifican pendientes al obtenerlo (normalmente ya no hay nada que aplicar).

**Rationale**: el lock debe vivir en el recurso que se protege (la BD), no en Redis — si Redis está caído la migración igual debe ser segura, y el lock de BD sobrevive exactamente lo que dura la sesión que migra (FR-013).

**Alternatives considered**: lock distribuido Redis (dependencia extra y semántica de expiración TTL peligrosa para migraciones largas); tabla de lock propia con UPDATE condicional (reinventa lo que ambos motores ya ofrecen nativo).

## D-05 — Política de reintentos de arranque

**Decision**: ventana configurable (`Startup:RetryWindowSeconds`, default 60; `RetryIntervalSeconds`, default 5) con backoff lineal y log Warning por intento (causa incluida, credenciales enmascaradas). Agotada la ventana: excepción terminal `Database.Unreachable`. Errores DURANTE una migración no se reintentan (terminal inmediato `Database.MigrationFailed`) — reintentar DDL a medias es peor que fallar.

**Rationale**: cubre el caso Docker/K8s (la BD tarda segundos en aceptar conexiones) sin enmascarar problemas reales; la distinción unreachable/failed la exige FR-010.

**Alternatives considered**: Polly (dependencia nueva para un loop de 15 líneas); reintentos infinitos con readiness probe (elegante en K8s puro pero on-premise sin orquestador dejaría el proceso colgado sin veredicto — contradice el fail-fast elegido en la clarificación #3).

## D-06 — Diferencias entre motores, centralizadas

**Decision**:
- **Fechas**: todo `DateTime` persistido es UTC (convención ya vigente). PG: `timestamp with time zone`; SQL Server: `DATETIME2`. Convención global en `OnModelCreating` compartido, no por entidad.
- **Concurrencia optimista**: propiedades `RowVersion` se mapean con `IsRowVersion()`; en SQL Server → `ROWVERSION`, en PG el proveedor Npgsql las mapea a `xmin` (system column) mediante `UseXminAsConcurrencyToken`. El `RowVersionInterceptor` existente se revisa para no asumir `byte[]` de 8 bytes fuera de SQL Server.
- **Case-insensitivity**: se mantiene el patrón del proyecto de **columnas normalizadas** (`NormalizedEmail` etc.) para búsquedas/unicidad críticas; para el resto, collation por defecto del motor documentada y los índices únicos sensibles definidos sobre columnas normalizadas. No se usa `citext` ni collations no deterministas de ICU (limitaciones con LIKE e índices).
- **GUIDs**: generación **client-side** (`Guid.NewGuid()` / `Guid.CreateVersion7()` donde el orden importe); se eliminan defaults de servidor (`NEWID()`) del modelo — mismo comportamiento en ambos motores.
- **Decimales monetarios**: precisión explícita (`decimal(18,2)` u homóloga por columna) vía convención, idéntica en ambos.
- **Strings**: `HasMaxLength` explícito ya presente en las configurations; PG usa `varchar(n)`, SQL Server `NVARCHAR(n)` — sin cambios de código.
- Todo condicional por proveedor que resulte imprescindible vive en los configuradores o en una convención central — **nunca** en una `IEntityTypeConfiguration` individual salvo justificación comentada.

**Rationale**: minimiza el diff sobre 270 configurations (las convenciones globales hacen el trabajo), cumple FR-005 y el edge case de collation del spec con el patrón normalizado que el proyecto ya usa en identidad.

**Alternatives considered**: `citext`/ICU nondeterministic collations en PG (limitaciones operativas conocidas); dobles configurations por proveedor (explosión de mantenimiento).

## D-07 — Framework de seeding

**Decision**: interfaz `IDataSeeder` `{ int Order; SeedCategory Category (Parametric|Test); SeedScope Scope (Admin|Tenant); string[] Environments (para Test); Task SeedAsync(SeedContext ctx, CancellationToken ct) }`. `SeedOrchestrator` resuelve los seeders del DI, filtra por categoría/ambiente/flags, ordena por `Order`, y ejecuta: alcance Admin sobre `AdminDbContext`; alcance Tenant iterando `TenantDirectory` y ejecutando por esquema de tenant. **Transacción por seeder por alcance** (un seeder de tenant = una transacción por tenant). Idempotencia por **clave natural** (código/nombre único) con patrón read-then-insert de lo faltante; los registros existentes NO se actualizan (respeta personalizaciones del cliente, FR-016). Marcas: `CreatedBy = "system:seed"` (paramétrico) / `"system:seed-demo"` (demo, FR-020).

**Rationale**: contrato mínimo que cubre orden, categorías, alcances y ambientes pedidos por el spec; transacción por unidad coherente cumple FR-018 sin transacciones globales gigantes; no-update de existentes es la lectura más segura de "completa lo faltante".

**Alternatives considered**: `HasData` de EF (atado a migraciones, pésimo para datos por-tenant y para "completar faltantes" runtime); MERGE/upsert SQL crudo (violaría principio IV/constitución sobre SQL directo y es por-motor); tabla de historial de seeds (innecesaria: la idempotencia por clave natural la reemplaza — menos estado que mantener).

## D-08 — Disparadores de seed

**Decision**: (1) **Arranque**: el inicializador, tras migrar, ejecuta paramétrico (si `RunParametricSeed`, default true en todos los ambientes) sobre Admin + todos los tenants (clarificación #4), y demo (si `RunTestSeed` efectivo: default on en Development/QA, off en Production). (2) **Alta de tenant**: `TenantSchemaService` siembra el esquema recién creado (paramétrico siempre; demo según flag efectivo). (3) **Bajo demanda**: comando `DbMigrator seed --category --scope [--tenant]` y endpoint `POST /api/saas/database/seed` `[RequireMasterAdmin]` → `RunDatabaseSeedCommand` (CQRS, AuditBehavior). La activación efectiva de demo en Production emite evento auditable al arranque.

**Rationale**: cubre FR-017/FR-019/FR-019a con los tres puntos de entrada pedidos; el endpoint pasa por MediatR para heredar auditoría (principio X) sin código especial.

**Alternatives considered**: seed solo por CLI (dejaría al SaaS sin vía operativa remota); seed lazy por tenant al primer acceso (rechazado en la clarificación #4).

## D-09 — Health checks

**Decision**: registrar el health check del proveedor activo (`AddSqlServer(...)` o `AddNpgSql(...)`) con tags `db` y `ready`; `/health/live` no depende de la BD, `/health/ready` sí (y reporta "no listo" mientras el inicializador no terminó). Se integra con los health checks existentes del proyecto.

**Rationale**: FR-022 pide distinguir vivo de listo; el paquete AspNetCore.HealthChecks ya está en el stack para SQL Server.

**Alternatives considered**: health check propio con `CanConnectAsync` (más código para lo mismo).

## D-10 — Orquestación local dual

**Decision**: `docker-compose.dev.yml` suma servicio `postgres` (imagen `postgres:17`, puerto 5432, healthcheck `pg_isready`, volumen persistente) junto al `sqlserver` existente. `appsettings.Development.json` default `Provider=PostgreSQL` (clarificación #5) con ambas connection strings de ejemplo. Perfil documentado para conmutar a SQL Server vía `Database__Provider=SqlServer` sin editar archivos.

**Rationale**: FR-024; PG default alinea dev↔SaaS; el SQL Server nativo de la máquina dev actual sigue siendo utilizable apuntando la connection string.

**Alternatives considered**: PG nativo Windows (menos reproducible que contenedor); dev containers completos (fuera de alcance).

## D-11 — Estrategia de tests en matriz

**Decision**: las fixtures de integración (p. ej. `CentralIdentityApiFixture`) se parametrizan por `DB_PROVIDER`: `Testcontainers.MsSql` o `Testcontainers.PostgreSql`, aplicando las migraciones EF del proveedor (ya no los DDL oficiales — coherente con la nueva fuente de verdad). CI corre la matriz `{PostgreSql, SqlServer}` sobre las suites existentes (identidad/admin/seguridad/núcleo, clarificación #2). Nuevos: `Provisioning_SmokeTests` (migrar todo + seed paramétrico + inventario de tablas/columnas esperadas por motor), `Seed_IdempotencyTests` (3 ejecuciones = estado idéntico), `Startup_FailFastTests` (FR-003/FR-011). Architecture tests: `UseSqlServer`/`UseNpgsql` solo en `Persistence/Providers`; Application/Domain sin referencias a paquetes de proveedor.

**Rationale**: SC-003/SC-008 exigen paridad verificable y detección de omisiones; la parametrización de fixture reutiliza toda la suite sin duplicarla.

**Alternatives considered**: suites duplicadas por motor (mantenimiento doble); correr PG siempre y SQL Server nightly (aceptable como optimización de CI futura, pero el gate de release exige ambos).

## D-12 — Secretos y enmascaramiento

**Decision**: connection strings suministrables por variables de entorno / user-secrets / secret stores del host (precedencia estándar .NET). Un helper único de enmascaramiento (`ConnectionStringMasker`) se usa en TODO log o error que cite una connection string (deja host/database, oculta password/user id). Prohibido loggear la cadena cruda — verificado por revisión y test unitario del masker.

**Rationale**: FR-006; el masker único evita 10 implementaciones ad-hoc.

**Alternatives considered**: no loggear nunca la cadena (dificulta diagnóstico de "¿contra qué host estoy?"); Azure Key Vault provider (no aplica a on-premise, queda abierto detrás de la configuración estándar).

## D-13 — Destino del corpus DDL congelado

**Decision**: `database/schema/` y `database/migration/` reciben un `README-CONGELADO.md` que declara el corpus como referencia histórica (fuente de verdad hasta 2026-08; reemplazado por migraciones EF del feature 004) y ningún script nuevo se agrega allí. Los scripts para DBA se generan por release con `dotnet ef migrations script --idempotent` por proveedor hacia `database/schema/generated/{SqlServer|PostgreSql}/NNN_<release>.sql` con header estándar (contexto, reversión, requisito de backup) — manteniendo la letra operativa del principio XII para lo que se entrega a un DBA.

**Rationale**: cierra la clarificación #1 sin borrar historia (los DDL congelados documentan el esquema de la Fase 0/1) y da al DBA artefactos idempotentes revisables como siempre.

**Alternatives considered**: borrar `database/schema` (pierde trazabilidad histórica); seguir numerando allí a mano (reintroduce la doble fuente de verdad).

---

## D-03-REV — Revertida: database-per-tenant (2026-08-22)

**Revierte D-03**, que descarto una base por cooperativa. La decision no se
borra: queda como traza de por que se eligio lo otro.

D-03 se apoyaba en un unico argumento — el Principio IV de la constitucion
exigia schema-per-tenant. Ese principio se enmendo en la version 2.0.0, asi que
el argumento ya no existe.

Lo que decidio la enmienda no fue la teoria, sino la medicion. El
schema-per-tenant dejaba el aislamiento en manos de la disciplina del codigo:
`ApplicationDbContext.OnModelCreating` resolvia el esquema con
`_tenantInfo?.Schema ?? "dbo"`, y `ErpTenantInfo` no se registraba en ningun
contenedor. El operador `??` degradaba en silencio, en cada peticion, y las
siete cooperativas compartian espacio sin excepcion, sin log y sin que ninguna
prueba lo detectara.

La base por cooperativa traslada la garantia del codigo al motor: una conexion
apuntada al sitio equivocado no mezcla datos, falla.

Ver constitucion v2.0.0, Principio IV.
