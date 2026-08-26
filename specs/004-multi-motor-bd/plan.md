# Implementation Plan: Soporte Multi-Motor de Base de Datos (PostgreSQL / SQL Server)

**Branch**: `004-multi-motor-bd` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)

> **Nota de vigencia (2026-08-25).** Donde este documento dice «esquema de
> tenant», hoy es **una base de datos por cooperativa**: el Principio IV cambió
> con la constitución v2.0.0 y la decisión que lo revierte está registrada como
> **D-03-REV** en [research.md](./research.md) — la traza de D-03 se conserva a
> propósito. El aislamiento es físico y el catálogo administrativo vive en una
> base aparte. Todo lo demás del documento sigue en pie.


**Input**: Feature specification from `/specs/004-multi-motor-bd/spec.md`

## Summary

Habilitar que IngenIA365ERP corra sobre **PostgreSQL o SQL Server eligiendo el motor por configuración** (sección `Database` en appsettings/variables de entorno), sin cambios de código: PostgreSQL para la modalidad SaaS y elección libre en on-premise. Al ser un despliegue **greenfield** (clarificación 2026-08-03), las **migraciones EF Core generadas desde el modelo** pasan a ser la única fuente de verdad del esquema para ambos motores; el corpus DDL T-SQL (`database/schema/`, `database/migration/`) queda congelado como referencia histórica.

Aproximación técnica: una sección de configuración `Database` bindeada a `DatabaseOptions` con validación fail-fast al arranque (`ValidateOnStart`); un `IDbProviderConfigurator` por proveedor (SqlServer / PostgreSql) que encapsula `UseSqlServer`/`UseNpgsql` y se aplica a los **tres DbContexts existentes** (`ApplicationDbContext`, `AdminDbContext`, `TenantDbContext`) desde `AddPersistenceServices`/`AddCentralIdentity` — los contexts permanecen únicos. Dos **ensamblados de migraciones por proveedor** (`IngenIA365ERP.Persistence.Migrations.SqlServer` y `...Migrations.PostgreSql`) con design-time factories conmutadas por la variable `DB_PROVIDER`. Un **inicializador de base de datos** (hosted service que corre antes de servir tráfico) que con `AutoMigrate=true` aplica migraciones con reintentos progresivos y lock nativo del motor (`sp_getapplock` / `pg_advisory_lock`), y con `AutoMigrate=false` + pendientes hace fail-fast enumerándolas. Un **framework de seeding** (`IDataSeeder` con categoría Paramétrico/Pruebas, alcance Admin/Tenant y orden de dependencias) orquestado tras las migraciones — al arranque cubre BD admin + todos los esquemas de tenant —, ejecutable también bajo demanda vía CLI (`tools/IngenIA365ERP.DbMigrator`) y endpoint master (`POST /api/saas/database/seed`, CQRS + auditoría). Paridad verificada con Testcontainers en matriz de proveedor sobre las suites existentes + smoke de aprovisionamiento para el resto de módulos. PostgreSQL es el default de desarrollo y CI.

## Technical Context

**Language/Version**: C# 13 sobre .NET 10.0.5 (alineado con la solución existente).

**Primary Dependencies**:
- **Nuevas**: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x (proveedor EF Core), `AspNetCore.HealthChecks.NpgSql` (health check PG), `Testcontainers.PostgreSql` (tests). Sin Polly nuevo: el retry del inicializador se implementa con loop propio + `EnableRetryOnFailure` por proveedor.
- **Existentes reutilizadas**: `Microsoft.EntityFrameworkCore.SqlServer` 10.x, `Microsoft.EntityFrameworkCore.Design`, `Testcontainers.MsSql` (ya usada por `CentralIdentityApiFixture`), Carter, MediatR, FluentValidation, Serilog, `AspNetCore.HealthChecks.SqlServer`/UI ya presentes.

**Storage**:
- **Relacional (multi-motor)**: BD administrativa (`IngenIA365ERP_Admin`) + BD/esquemas de tenant, sobre SQL Server **o** PostgreSQL según `Database:Provider`. Historial de migraciones EF (`__EFMigrationsHistory`) por base/esquema.
- **Fuera de alcance (sin cambios)**: MongoDB (auditoría), Redis (caché/locks de app).

**Testing**: xUnit + Testcontainers con **matriz de proveedor** (variable `DB_PROVIDER` en las fixtures): suites existentes (Application 182, Architecture 34, integración de identidad sobre `CentralIdentityApiFixture`) corren contra ambos motores; smoke test de aprovisionamiento (migrar todo + seed paramétrico + conteo de tablas) por motor para los módulos sin suite profunda. Architecture.Tests nuevos: prohibir `UseSqlServer`/`UseNpgsql` fuera de los configuradores de proveedor.

**Target Platform**: servidor .NET 10 en VPS Linux (Docker/Kubernetes) para SaaS con PostgreSQL; Windows/Linux on-premise con motor a elección. Desarrollo local: PostgreSQL default (contenedor), SQL Server disponible en la orquestación dual.

**Project Type**: extensión de infraestructura sobre la Clean Architecture existente (Persistence + Identity + API + tooling); sin UI nueva salvo el endpoint administrativo.

**Performance Goals**:
- SC-002: instalación desde cero (motor vacío → esquema completo → seed paramétrico → login master OK) en < 10 min por motor.
- Arranque en caliente (sin migraciones ni seeds pendientes) añade < 5 s al startup actual (verificación idempotente de admin + ~N tenants).
- Los objetivos de la Fase 1 (p95 login < 800 ms en el VPS) se mantienen con cualquiera de los dos motores.

**Constraints**:
- Fail-fast en: provider inválido, connection string faltante del provider activo, migraciones pendientes con `AutoMigrate=false`, seed sobre esquema desactualizado (FR-003, FR-011, FR-021).
- Lock de migración nativo del motor (una sola instancia migra en despliegues multi-réplica) — FR-013.
- Credenciales enmascaradas en todo log/error (FR-006).
- Diferencias por motor centralizadas en `Persistence` (configuradores + convenciones); cero condicionales por proveedor en Application/Domain (FR-005, principio II).
- `RunTestSeed` default por ambiente (on en Development/QA, off en Production) y su activación en producción auditada (FR-017).

**Scale/Scope**: 270 entidades / 140 DbSets en `ApplicationDbContext` + entidades Admin; ~100 tenants objetivo; 2 ensamblados de migraciones nuevos; 1 endpoint REST nuevo; 3 comandos CLI; ~8 seeders paramétricos iniciales + 1 demo.

## Constitution Check

*GATE: Debe pasar antes de Phase 0 research. Re-check tras Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | Constitution → Spec (con 5 clarifications) → este Plan → Tasks. Cero código antes de cerrar el plan. |
| II   | Clean Architecture | PASS | Toda la selección de proveedor vive en Infrastructure (`Persistence/Providers/*`, cambios en `DependencyInjection` de Persistence e Identity). Application solo conoce abstracciones nuevas (`IDatabaseInitializationStatus`, `IDataSeedRunner`). Domain no cambia. Architecture test nuevo prohíbe `UseSqlServer`/`UseNpgsql` fuera de los configuradores. |
| III  | CQRS + MediatR | PASS | El endpoint de seed bajo demanda es un Carter module que reenvía a `RunDatabaseSeedCommand` (con validator hermano) vía `ISender`; pasa por los 4 pipeline behaviors. El inicializador de arranque no es un flujo de usuario (hosted service de infraestructura) y no salta ningún behavior porque no ejecuta Commands. |
| IV   | Multi-tenancy | PASS | El seeding/migración de tenants itera la lista oficial de tenants (`TenantDirectory`) y opera cada esquema **explícitamente y por separado** (requisito constitucional para background jobs); cero cross-tenant joins. El seed paramétrico de tenant escribe solo en el esquema del tenant en curso. |
| V    | Person centralizada | PASS | Los seeders no duplican datos personales; el seed demo crea personas vía `COR_People` + tablas hijas respetando los flags de rol. |
| VI   | PublicId externo / Id interno | PASS | El endpoint de seed no expone `int Id`; `tenantPublicId` opcional en el request. Los datos sembrados generan `PublicId` como cualquier entidad. |
| VII  | Soft-delete + auditoría | PASS | Los seeds crean entidades con campos de auditoría poblados (`CreatedBy = "system:seed"` / `"system:seed-demo"` — este último es además la marca reconocible exigida por FR-020). Sin `DELETE FROM` en código de producción. |
| VIII | Validación dual | PASS | No hay formulario nuevo de usuario final. `DatabaseOptions` valida server-side con `ValidateOnStart` (fail-fast); `RunDatabaseSeedCommand` lleva FluentValidation validator. Mensajes en español con códigos `Database.*` (`Database.InvalidProvider`, `Database.ConnectionStringMissing`, `Database.MigrationsPending`, `Database.Seed.SchemaOutdated`). |
| IX   | Errores visibles | PASS | Fail-fast con mensajes accionables; cada reintento de conexión se loggea (Warning) con causa; cero catch silenciado. El fallo final distingue `Database.Unreachable` de `Database.MigrationFailed`. |
| X    | Trazabilidad SIPLA/SARLAFT | PASS | El seed bajo demanda pasa por `AuditBehavior` (evento `Database.Seed.Executed` con actor, categoría, alcance, resultado). La activación efectiva de `RunTestSeed` en Production se registra como evento auditable al arranque (`Database.Seed.TestSeedEnabledInProduction`). |
| XI   | Inmutabilidad contable | PASS | El seed demo **inserta** movimientos de ejemplo (permitido); nunca edita/borra asentados. La limpieza de datos demo llegados a un ambiente equivocado se ejecuta como mantenimiento técnico autorizado documentado (la vía que el propio principio XI admite), localizándolos por la marca `system:seed-demo`. |
| XII  | Migraciones idempotentes y reversibles | PASS (mecanismo actualizado — ver Complexity Tracking) | La fuente de verdad pasa del DDL manual a las migraciones EF por proveedor. El espíritu del principio se conserva: los scripts para DBA se generan con `dotnet ef migrations script --idempotent` por motor, se versionan en `database/schema/generated/{provider}/` con header documentado, y el `Down()` de cada migración cubre la reversibilidad (o se declara irreversible). `database/schema/` y `database/migration/` actuales quedan congelados con README de cierre. |

**Resultado**: **12/12 PASS**, con una nota de transición de mecanismo en el principio XII documentada en Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/004-multi-motor-bd/
├── plan.md              # Este documento
├── spec.md              # Especificación (5 clarifications integradas)
├── research.md          # Phase 0: decisiones técnicas D-01..D-13
├── data-model.md        # Phase 1: modelo de configuración, contrato de seeders, historial de migraciones
├── quickstart.md        # Phase 1: verificación end-to-end dual-motor
├── contracts/
│   ├── configuration.md     # Contrato de la sección Database + variables de entorno + errores de validación
│   ├── database-admin.md    # Endpoint POST /api/saas/database/seed + GET /api/saas/database/status
│   └── cli.md               # Comandos DbMigrator (migrate, seed, script) y comandos dotnet-ef por proveedor
├── checklists/
│   └── requirements.md  # Validado 16/16
└── tasks.md             # Phase 2: lo genera /speckit-tasks
```

### Source Code (repository root — layout existente, extensiones marcadas con +)

```text
src/
├── Core/
│   └── IngenIA365ERP.Application/
│       ├── Common/Interfaces/Database/
│       │   + IDataSeedRunner.cs               # Abstracción para el Command de seed bajo demanda
│       │   + IDatabaseStatusReader.cs         # Provider activo + migraciones pendientes (para /status)
│       ├── Common/Audit/AuditEventTypes.cs    # ~ + Database.Seed.Executed, Database.Seed.TestSeedEnabledInProduction
│       └── Saas/
│           + RunDatabaseSeedCommand.cs        # (record + validator + handler) → IDataSeedRunner
│           + GetDatabaseStatusQuery.cs        # → IDatabaseStatusReader
├── Infrastructure/
│   ├── IngenIA365ERP.Persistence/
│   │   ├── Providers/
│   │   │   + DatabaseProvider.cs              # enum { SqlServer, PostgreSql }
│   │   │   + DatabaseOptions.cs               # Sección "Database" + validación (IValidateOptions)
│   │   │   + IDbProviderConfigurator.cs       # Configure(DbContextOptionsBuilder, string connStr, string migrationsAssembly)
│   │   │   + SqlServerProviderConfigurator.cs
│   │   │   + PostgreSqlProviderConfigurator.cs
│   │   ├── Initialization/
│   │   │   + DatabaseInitializerHostedService.cs  # Orquesta: wait+retry → lock → migrate → seed → release
│   │   │   + MigrationLock.cs                     # sp_getapplock / pg_advisory_lock según proveedor
│   │   │   + PendingMigrationsGuard.cs            # Fail-fast FR-011 + FR-021
│   │   ├── Seeding/
│   │   │   + IDataSeeder.cs                   # { Order, Category, Scope, Environments, SeedAsync(ctx) }
│   │   │   + SeedOrchestrator.cs              # Ordena, transacciona, itera tenants (via TenantDirectory)
│   │   │   + Parametric/                      # RolesSeeder, PermissionsSeeder, CurrenciesSeeder,
│   │   │   │                                  # DocumentTypesSeeder, SystemParametersSeeder, ChartOfAccountsSeeder, ...
│   │   │   + Demo/DemoDataSeeder.cs           # clientes/productos/facturas/movimientos (marca system:seed-demo)
│   │   ├── MultiTenancy/TenantSchemaService.cs # ~ aprovisionamiento de tenant multi-proveedor (D-03)
│   │   └── DependencyInjection.cs             # ~ selección de proveedor + ValidateOnStart + health checks
│   ├── IngenIA365ERP.Persistence.Migrations.SqlServer/    # + NUEVO proyecto (migraciones Admin + Application)
│   │   + Admin/ · Application/ · DesignTime/*Factory.cs
│   └── IngenIA365ERP.Persistence.Migrations.PostgreSql/   # + NUEVO proyecto (idem)
│       + Admin/ · Application/ · DesignTime/*Factory.cs
│   └── IngenIA365ERP.Identity/DependencyInjection.cs      # ~ usa IDbProviderConfigurator (hoy UseSqlServer directo)
├── Presentation/
│   └── IngenIA365ERP.API/
│       ├── Program.cs                         # ~ registra inicializador + health checks por proveedor
│       └── Modules/
│           + DatabaseAdminModule.cs           # POST /api/saas/database/seed · GET /api/saas/database/status [RequireMasterAdmin]
tools/
└── IngenIA365ERP.DbMigrator/                  # ~ comandos: migrate --provider, seed --category --scope, script --provider --idempotent
tests/
├── IngenIA365ERP.API.IntegrationTests/
│   ├── Fixtures/                              # ~ CentralIdentityApiFixture parametrizada por DB_PROVIDER (Testcontainers MsSql/PostgreSql)
│   └── Database/
│       + Provisioning_SmokeTests.cs           # migrar todo + seed paramétrico + inventario de tablas, por motor
│       + Seed_IdempotencyTests.cs             # 3 ejecuciones = mismo estado
│       + Startup_FailFastTests.cs             # provider inválido, connstr faltante, pendientes con AutoMigrate=false
└── IngenIA365ERP.Architecture.Tests/
    + Principles/Feature004_MultiProvider.cs   # UseSqlServer/UseNpgsql solo en Providers/*; Application sin refs a proveedores
database/
├── schema/    · migration/                    # ~ CONGELADOS + README de cierre (referencia histórica)
└── schema/generated/{SqlServer,PostgreSql}/   # + scripts idempotentes para DBA generados por release
docker-compose.dev.yml                          # ~ + servicio postgres (default dev) junto al sqlserver existente
```

**Structure Decision**: se conserva la Clean Architecture y los **tres DbContexts existentes sin duplicar** (`ApplicationDbContext`, `AdminDbContext`, `TenantDbContext`); la variabilidad de motor se concentra en `Persistence/Providers/*` y dos ensamblados de migraciones nuevos (EF Core exige migraciones por proveedor, y separarlas en proyectos evita contaminar `Persistence` con snapshots duales). `IngenIA365ERP.Identity` deja de llamar `UseSqlServer` directo y consume el mismo configurador. Application solo gana dos abstracciones y un Command/Query — el resto es infraestructura.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| El principio XII asume DDL manual numerado en `database/migration/`; este feature traslada la fuente de verdad a migraciones EF por proveedor | Mantener dos corpus DDL manuales (T-SQL y PG) por cada cambio de esquema duplica el trabajo y garantiza divergencia — exactamente el riesgo que el multi-motor no puede permitirse. Greenfield confirmado (clarificación #1) elimina el costo de transición. | Alternativa rechazada: DDL dual mantenido a mano (opción B de la clarificación #1) — cada cambio se escribiría dos veces sin verificación estructural de equivalencia. El espíritu de XII se conserva: scripts DBA generados con `--idempotent`, versionados y documentados en `database/schema/generated/{provider}/`, reversibilidad vía `Down()` o declaración explícita de irreversibilidad. Enmienda formal de la constitución NO requerida: el principio exige idempotencia/documentación/numeración de lo que vive en `database/`, y eso se mantiene para los artefactos generados. |

## Phase 0 — Outline & Research

Resuelta en [`research.md`](./research.md). Decisiones D-01 a D-13: patrón de selección de proveedor, ensamblados de migraciones y design-time factories, aprovisionamiento de tenant multi-proveedor, lock de migración nativo, política de reintentos, manejo centralizado de diferencias entre motores (tipos, concurrencia optimista, collations/case-insensitivity, fechas UTC, GUIDs), framework de seeding e idempotencia, disparadores de seed, health checks, orquestación local dual, estrategia de tests en matriz, secretos/enmascaramiento, y destino del corpus DDL congelado.

## Phase 1 — Design & Contracts

### Data model

Detallado en [`data-model.md`](./data-model.md): forma completa de `DatabaseOptions` (sección `Database`), contrato `IDataSeeder`/`SeedOrchestrator` (categorías, alcances, orden, transaccionalidad, marcas), tablas de historial de migraciones por base/esquema, y el inventario inicial de seeders paramétricos y demo. **Sin entidades de dominio nuevas ni tablas nuevas** más allá de `__EFMigrationsHistory` (estándar EF).

### Contracts

Detallados en [`contracts/`](./contracts/):

| Contrato | Contenido |
|----------|-----------|
| `configuration.md` | Sección `Database` completa (claves, defaults por ambiente, precedencia env-vars > appsettings, errores de validación con códigos `Database.*`) |
| `database-admin.md` | `POST /api/saas/database/seed` y `GET /api/saas/database/status` — `[RequireMasterAdmin]`, request/response, códigos de error, auditoría |
| `cli.md` | Comandos `DbMigrator` (migrate/seed/script) y recetas `dotnet ef` por proveedor (add/remove/script) con `DB_PROVIDER` |

### Quickstart

[`quickstart.md`](./quickstart.md): levantar la orquestación dual (PostgreSQL + SQL Server), instalar desde cero en cada motor, verificar fail-fast, idempotencia de seeds, seed demo por ambiente, endpoint master y generación de scripts DBA — cubriendo SC-001 a SC-008.

### Agent context update

Bloque `<!-- SPECKIT START --> ... <!-- SPECKIT END -->` de `CLAUDE.md` actualizado para apuntar a `specs/004-multi-motor-bd/plan.md` y sus artefactos.

## Constitution Re-check (post Phase 1 design)

Tras diseñar `research.md`, `data-model.md` y `contracts/`, las doce compuertas siguen en **PASS**: Domain intacto; Application solo con abstracciones puras + 1 Command/1 Query con validators; la variabilidad de proveedor encapsulada en `Persistence/Providers` y los dos ensamblados de migraciones; seeding de tenants iterando explícitamente el directorio oficial de tenants (principio IV para background jobs); auditoría del seed bajo demanda y de la activación de demo en producción (principio X); datos demo insertados nunca editados (principio XI); scripts DBA idempotentes generados y versionados (principio XII, mecanismo actualizado según Complexity Tracking). Sin entradas nuevas en Complexity Tracking.
