---

description: "Task list for Soporte Multi-Motor de Base de Datos (PostgreSQL / SQL Server)"
---

# Tasks: Soporte Multi-Motor de Base de Datos (PostgreSQL / SQL Server)

**Input**: Design documents from `/specs/004-multi-motor-bd/`

**Prerequisites**: plan.md ✓, spec.md ✓ (5 clarifications), research.md ✓ (D-01…D-13), data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Tests**: incluidos (la constitución exige tests en cada PR significativo; además SC-003/SC-008 son inverificables sin la matriz de proveedor).

**Organization**: tareas agrupadas por user story (P1 → P3). MVP = US1 + US2 (misma build instalable en ambos motores) + US3 (sistema operable con maestros).

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede correr en paralelo (archivos distintos, sin dependencias en tareas no completadas)
- **[Story]**: user story a la que pertenece (US1…US5)
- Cada descripción incluye la ruta exacta del archivo

## Path Conventions

- Persistence: `src/Infrastructure/IngenIA365ERP.Persistence/`
- Migraciones por proveedor: `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{SqlServer|PostgreSql}/`
- Identity: `src/Infrastructure/IngenIA365ERP.Identity/`
- Application: `src/Core/IngenIA365ERP.Application/`
- API: `src/Presentation/IngenIA365ERP.API/`
- CLI: `tools/IngenIA365ERP.DbMigrator/`
- Tests: `tests/IngenIA365ERP.{Application,Architecture,API.Integration}.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: paquetes, orquestación local dual y esqueleto de proyectos de migraciones.

- [X] T001 Añadir `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x a `src/Infrastructure/IngenIA365ERP.Persistence/IngenIA365ERP.Persistence.csproj` y `AspNetCore.HealthChecks.NpgSql` a `src/Presentation/IngenIA365ERP.API/IngenIA365ERP.API.csproj`; `Testcontainers.PostgreSql` a `tests/IngenIA365ERP.API.IntegrationTests/IngenIA365ERP.API.IntegrationTests.csproj`
- [X] T002 [P] Añadir servicio `postgres` (imagen `postgres:17`, puerto 5432, healthcheck `pg_isready`, volumen `postgres-data`) a `docker-compose.dev.yml` junto al `sqlserver` existente (D-10)
- [X] T003 [P] Crear los dos proyectos de migraciones `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/` y `.../IngenIA365ERP.Persistence.Migrations.PostgreSql/` (classlib net10.0, ref a Persistence + proveedor EF respectivo, carpetas `Admin/`, `Application/`, `DesignTime/`) y agregarlos a `IngenIA365ERP.sln` (D-02)
- [X] T004 [P] Extender `appsettings.json`, `appsettings.Development.json` (Provider=PostgreSQL default, D-10/clarificación #5) y crear `appsettings.QA.json`, `appsettings.Production.json` de ejemplo en `src/Presentation/IngenIA365ERP.API/` con la sección `Database` completa según `contracts/configuration.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: abstracción de proveedor, convenciones multi-motor y árboles de migraciones iniciales. **Ninguna user story puede comenzar sin esto.** Orden interno crítico: convenciones (T009–T012) ANTES de generar migraciones (T015–T016).

### Opciones y configuradores de proveedor

- [X] T005 Crear `src/Infrastructure/IngenIA365ERP.Persistence/Providers/DatabaseProvider.cs` (enum `{ SqlServer, PostgreSql }` + parser case-insensitive) y `DatabaseOptions.cs` (sección `Database` completa según data-model §1: Provider, ConnectionStrings, AdminConnectionStrings opcional, AutoMigrate, Seed, Startup)
- [X] T006 Crear `src/Infrastructure/IngenIA365ERP.Persistence/Providers/DatabaseOptionsValidator.cs` (`IValidateOptions<DatabaseOptions>`) con las reglas y códigos `Database.InvalidProvider` / `Database.ConnectionStringMissing` / `Database.InvalidStartupOptions` en español (data-model §1, D-01)
- [X] T007 [P] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Providers/IDbProviderConfigurator.cs` + `SqlServerProviderConfigurator.cs` + `PostgreSqlProviderConfigurator.cs`: `Configure(DbContextOptionsBuilder, string connectionString, MigrationsTarget target)` fija proveedor, `MigrationsAssembly` del ensamblado correspondiente, `MigrationsHistoryTable`, `EnableRetryOnFailure`; PG añade `UseXminAsConcurrencyToken` vía convención (D-01, D-06)
- [X] T008 [P] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Providers/ConnectionStringMasker.cs` (host/database visibles; user/password enmascarados) + tests unitarios en `tests/IngenIA365ERP.Application.Tests/Infrastructure/ConnectionStringMaskerTests.cs` (D-12)

### Convenciones multi-motor (antes de generar migraciones)

- [X] T009 Añadir convenciones globales de modelo en `src/Infrastructure/IngenIA365ERP.Persistence/DbContext/ApplicationDbContext.cs` y `AdminDbContext.cs` (`ConfigureConventions`): DateTime→UTC (`timestamptz`/`DATETIME2`), precisión decimal explícita, strings con longitud ya declarada — sin condicionales por proveedor en configurations individuales (D-06)
- [X] T010 [P] Revisar `src/Infrastructure/IngenIA365ERP.Persistence/Interceptors/RowVersionInterceptor.cs` y las propiedades `RowVersion` del modelo: mapeo `IsRowVersion()` portable (SQL Server `ROWVERSION`, PG `xmin`); eliminar toda suposición de `byte[8]` fuera de SQL Server (D-06)
- [X] T011 [P] Barrido de defaults de servidor en las 270 configurations (`HasDefaultValueSql("NEWID()")`, `SYSUTCDATETIME()` etc.): mover generación de GUIDs y timestamps a client-side (`Guid.NewGuid()`, interceptores existentes) para comportamiento idéntico entre motores (D-06). Listar excepciones justificadas si las hay
- [X] T012 [P] Barrido de unicidad/búsquedas case-insensitive: confirmar que las críticas usan columnas normalizadas (patrón `NormalizedEmail`); documentar la collation default por motor en `specs/004-multi-motor-bd/research.md` D-06 si surge algún caso nuevo (edge case collation del spec)
- [X] T013 Barrido de SQL crudo dependiente de motor: `Grep ExecuteSqlRaw|ExecuteSqlInterpolated|FromSqlRaw` en `src/` — portar cada ocurrencia a API neutral de EF o al configurador; el resultado del barrido queda anotado en el PR (FR-005)

### DI refactor + design-time factories + migraciones iniciales

- [X] T014 Refactorizar `src/Infrastructure/IngenIA365ERP.Persistence/DependencyInjection.cs`: bind + `ValidateOnStart` de `DatabaseOptions`, registrar el `IDbProviderConfigurator` del provider activo, y los tres `AddDbContext` (Application/Admin/Tenant) delegan en él; conservar interceptores y `IAdminDbContext`/`IApplicationDbContext` (D-01)
- [X] T014a Refactorizar `src/Infrastructure/IngenIA365ERP.Identity/DependencyInjection.cs` (línea ~29): eliminar `UseSqlServer` directo y consumir el mismo `IDbProviderConfigurator` (plan: Identity deja de conocer el proveedor)
- [X] T015 Crear las 4 design-time factories en `.../Migrations.{SqlServer,PostgreSql}/DesignTime/` (`ApplicationDbContextFactory`, `AdminDbContextFactory` × proveedor) que leen `DB_PROVIDER` + connection string de diseño (D-02); crear helper `tools/scripts/add-migration.ps1 -Name <X> -Context Application|Admin` que genera el PAR de migraciones (contracts/cli.md)
- [X] T016 Generar migraciones iniciales `InitialSchema` para `AdminDbContext` en AMBOS ensamblados (regenera el árbol `Persistence/Migrations/Admin` existente y lo elimina — greenfield, D-02) y para `ApplicationDbContext` en AMBOS ensamblados (esquema completo de las 270 entidades); compilación verde de la solución completa
- [X] T017 Validar el par de migraciones: `dotnet ef database update` contra PG y SQL Server limpios (contenedores dev) + inventario rápido de tablas/columnas clave por motor; corregir diferencias de mapeo detectadas (iterar T009–T012 si aparecen)

### Architecture tests (blindaje)

- [X] T018 [P] Crear `tests/IngenIA365ERP.Architecture.Tests/Principles/Feature004_MultiProvider.cs`: (a) `UseSqlServer`/`UseNpgsql` solo referenciables desde `Persistence/Providers/*` y las design-time factories; (b) Application y Domain sin referencias a `Microsoft.EntityFrameworkCore.SqlServer` ni `Npgsql.*`; (c) `IEntityTypeConfiguration` sin condicionales por proveedor (por convención de namespace)

**Checkpoint Foundational**: la solución compila con ambos proveedores seleccionables y el esquema completo se crea en ambos motores vía CLI. Las user stories pueden arrancar.

---

## Phase 3: User Story 1 — Despliegue con el motor elegido por configuración (Priority: P1) 🎯 MVP slice 1/3

**Goal**: la misma build arranca operativa en PostgreSQL o SQL Server cambiando solo configuración; configuración inválida = fail-fast accionable.

**Independent Test**: quickstart §1–§2 — instalar en limpio sobre cada motor (migraciones vía CLI de T016/T017), login master OK; arrancar con `Provider=Oracle` y sin connection string → proceso termina con error claro, nada escuchando.

- [X] T019 [US1] Verificar/endurecer el fail-fast end-to-end en `src/Presentation/IngenIA365ERP.API/Program.cs`: `ValidateOnStart` efectivo (host no arranca), mensajes con códigos `Database.*` en español, exit code ≠ 0, y TODO log de arranque que cite conexión pasa por `ConnectionStringMasker` (FR-003, FR-006, SC-004)
- [X] T020 [P] [US1] Log de arranque estructurado en `Program.cs`/inicializador: motor seleccionado, resultado de validación, origen de la configuración (appsettings vs env var) — sin credenciales (FR-023)
- [X] T021 [P] [US1] Tests unitarios `tests/IngenIA365ERP.Application.Tests/Infrastructure/DatabaseOptionsValidatorTests.cs`: provider inválido, connection string faltante del activo, faltante del NO activo (no error, FR-004), retry inválido, case-insensitivity del provider
- [X] T022 [US1] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Database/Startup_FailFastTests.cs` (parte configuración): host con provider inválido / connstr faltante no arranca y reporta el código correcto
- [X] T023 [US1] Verificación dual manual: quickstart §1 (arranque PG default y SQL Server por env var, login master, health live/ready) documentando tiempos (SC-001, SC-002); registrar resultado en la sección de evidencia del feature
- [X] T024 [P] [US1] Actualizar `README.md` sección "Base de datos multi-motor": selección de provider, variables de entorno, defaults por ambiente (contracts/configuration.md)

**Checkpoint US1**: el motor es una decisión de configuración validada. Falta automatizar el aprovisionamiento (US2).

---

## Phase 4: User Story 2 — Aprovisionamiento automático y seguro del esquema (Priority: P1) 🎯 MVP slice 2/3

**Goal**: `AutoMigrate=true` deja el esquema al día al arranque (con reintentos y lock multi-instancia); `AutoMigrate=false` + pendientes = fail-fast enumerado; scripts idempotentes para DBA; alta de tenant funciona en el motor activo.

**Independent Test**: quickstart §3 — BD apagada al arrancar → reintentos → recuperación; `AutoMigrate=false` sobre BD vacía → fail-fast con lista; `script --provider` genera SQL idempotente; dos instancias simultáneas migran una sola vez.

- [X] T025 [US2] **Spike D-03 (primera tarea de la fase)**: validar en ambos motores la estrategia de aprovisionamiento schema-per-tenant elegida (script idempotente + `search_path`/default schema vs `IModelCacheKeyFactory`); decisión final anotada en `specs/004-multi-motor-bd/research.md` D-03 con el prototipo en rama
- [X] T026 [US2] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Initialization/MigrationLock.cs`: `sp_getapplock` (SQL Server) / `pg_advisory_lock` (PG) sobre recurso `ingenia365:db-init`, `IAsyncDisposable`, timeout configurable (D-04)
- [X] T027 [P] [US2] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Initialization/PendingMigrationsGuard.cs`: calcula pendientes por BD admin y por esquema de tenant (historial per-esquema, data-model §3); produce el error `Database.MigrationsPending` enumerado (FR-011)
- [X] T028 [US2] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Initialization/DatabaseInitializerHostedService.cs`: máquina de estados de data-model §5 — wait+retry con ventana configurable y Warnings enmascarados (`Database.Unreachable` vs `Database.MigrationFailed`), lock, migrate admin → tenants, luego hook de seeding (se conecta en US3); health "ready" en rojo hasta terminar (FR-009, FR-010, FR-013, D-05)
- [X] T029 [US2] Adaptar `src/Infrastructure/IngenIA365ERP.Persistence/MultiTenancy/TenantSchemaService.cs` al resultado del spike T025: creación de esquema + aplicación de migraciones del proveedor activo + `__EFMigrationsHistory` por esquema; usado tanto por el inicializador como por el alta de tenant en runtime (FR-014)
- [X] T030 [P] [US2] Extender `tools/IngenIA365ERP.DbMigrator/` con comandos `migrate` (admin/tenants/all, `--tenant`) y `script` (`--provider`, `--idempotent`, salida a `database/schema/generated/{provider}/` con header estándar del principio XII) según contracts/cli.md (FR-012)
- [X] T031 [P] [US2] Health checks por proveedor en `src/Presentation/IngenIA365ERP.API/Program.cs`: `AddNpgSql`/`AddSqlServer` dinámico con tags `db`,`ready`; `/health/live` sin dependencia de BD (FR-022, D-09)
- [ ] T032 [P] [US2] Tests de integración `tests/IngenIA365ERP.API.IntegrationTests/Database/Initializer_Tests.cs`: (a) BD contenedor pausado → arranca al reanudar dentro de la ventana; (b) ventana agotada → `Database.Unreachable`; (c) `AutoMigrate=false` + pendientes → `Database.MigrationsPending` enumeradas; (d) dos hosts concurrentes → una sola aplicación de migraciones (lock)
- [ ] T033 [P] [US2] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Database/TenantProvisioning_Tests.cs`: alta de tenant en runtime sobre CADA motor → esquema creado + historial al día (FR-014; reusa el flujo `POST /api/saas/tenants/with-admin` del 002)
- [ ] T034 [US2] Verificación manual quickstart §3 completo (retry, fail-fast pendientes, script DBA aplicado a mano produce esquema equivalente) — evidencia registrada

**Checkpoint US2**: instalación desde cero sin intervención en ambos motores; producción protegida (scripts DBA). MVP de infraestructura completo; falta que el sistema sea operable (US3).

---

## Phase 5: User Story 3 — Seed paramétrico de datos maestros (Priority: P2) 🎯 MVP slice 3/3

**Goal**: datos maestros garantizados e idempotentes en todo ambiente y motor (admin + todos los tenants), al arranque y bajo demanda (CLI + endpoint master auditado).

**Independent Test**: quickstart §4 y §6 — 3 ejecuciones = mismo estado; fila maestra borrada se restaura sin tocar el resto; endpoint master siembra y audita; seed sobre esquema desactualizado → rechazo claro.

- [ ] T035 [US3] Crear el contrato del framework en `src/Infrastructure/IngenIA365ERP.Persistence/Seeding/IDataSeeder.cs` + `SeedContext.cs` + enums (`SeedCategory`, `SeedScope`) según data-model §2
- [ ] T036 [US3] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Seeding/SeedOrchestrator.cs`: filtra por categoría/flags/ambiente, ordena por `Order`, transacción por seeder×alcance, itera `TenantDirectory` abriendo cada esquema por separado (principio IV), guard `Database.Seed.SchemaOutdated` vía `PendingMigrationsGuard` (FR-016, FR-018, FR-021)
- [X] T037 [US3] Migrar los seeders Phase 0 existentes (`DomainSecuritySeedData` y afines — ubicarlos con Grep) al framework como `RolesSeeder` (Order 20) y `PermissionsSeeder` (Order 30) en `src/Infrastructure/IngenIA365ERP.Persistence/Seeding/Parametric/` — 8 roles, 112 permisos, asignaciones base; idempotencia por clave natural; `CreatedBy="system:seed"`; eliminar el mecanismo viejo para no duplicar siembra
- [ ] T038 [P] [US3] Crear `CurrenciesSeeder` (Order 40), `DocumentTypesSeeder` (Order 50), `SystemParametersSeeder` (Order 10, Admin), `TenantParametersSeeder` (Order 70) en `.../Seeding/Parametric/` según inventario data-model §4
- [ ] T039 [P] [US3] Crear `ChartOfAccountsSeeder` (Order 60) con el plan de cuentas base PUC cooperativo en `.../Seeding/Parametric/ChartOfAccountsSeeder.cs` (fuente: catálogo PUC existente en el modelo contable; solo inserta faltantes)
- [ ] T040 [US3] Conectar el orquestador al `DatabaseInitializerHostedService` (hook de T028): paramétrico si `RunParametricSeed` (admin + todos los tenants — FR-019a/clarificación #4) y al alta de tenant en `TenantSchemaService` (siembra del esquema nuevo)
- [ ] T041 [P] [US3] Crear abstracciones `src/Core/IngenIA365ERP.Application/Common/Interfaces/Database/IDataSeedRunner.cs` + `IDatabaseStatusReader.cs` e implementarlas en Persistence (`SeedOrchestrator` adapter + status desde `PendingMigrationsGuard`)
- [ ] T042 [US3] Crear `src/Core/IngenIA365ERP.Application/Saas/RunDatabaseSeedCommand.cs` (record + FluentValidation validator con códigos `Database.Seed.*` de contracts/database-admin.md + handler → `IDataSeedRunner`) y `GetDatabaseStatusQuery.cs` (→ `IDatabaseStatusReader`); añadir `Database.Seed.Executed` y `Database.Seed.TestSeedEnabledInProduction` a `src/Core/IngenIA365ERP.Application/Common/Audit/AuditEventTypes.cs`
- [ ] T043 [US3] Crear `src/Presentation/IngenIA365ERP.API/Modules/DatabaseAdminModule.cs`: `POST /api/saas/database/seed` + `GET /api/saas/database/status`, `[RequireMasterAdmin]` + purpose full, solo reenvían a `ISender` (contracts/database-admin.md)
- [ ] T044 [P] [US3] Extender `tools/IngenIA365ERP.DbMigrator/` con comando `seed` (`--category`, `--scope`, `--tenant`, `--confirm-test-seed`) reutilizando el orquestador (contracts/cli.md)
- [ ] T045 [P] [US3] Tests unitarios `tests/IngenIA365ERP.Application.Tests/Saas/RunDatabaseSeedCommandHandlerTests.cs` (validator: categoría/alcance inválidos, tenant inexistente, confirmación demo en Production; handler → runner con alcance correcto) + `tests/IngenIA365ERP.Application.Tests/Infrastructure/SeedOrchestratorTests.cs` (orden, filtro por categoría, no-update de existentes)
- [ ] T046 [P] [US3] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Database/Seed_IdempotencyTests.cs`: 3 ejecuciones = estado idéntico (conteos roles/permisos/monedas/PUC); fila borrada se restaura; registro modificado por cliente NO se pisa; seed sobre esquema desactualizado → `Database.Seed.SchemaOutdated` (SC-006, FR-016, FR-021)
- [ ] T047 [US3] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Database/DatabaseAdminEndpoint_Tests.cs`: endpoint seed con master → 200 + evento Mongo `Database.Seed.Executed`; sin master → 403; status reporta provider y pendientes (FR-019, principio X)

**Checkpoint US3**: MVP completo — instalación en cualquier motor termina en sistema operable (roles, permisos, monedas, PUC listos).

---

## Phase 6: User Story 4 — Seed de demostración por ambiente (Priority: P2)

**Goal**: datos demo on por defecto en Dev/QA, off en producción con opt-in explícito auditado; registros demo reconocibles.

**Independent Test**: quickstart §5 — Dev carga demo; Production no; `RunTestSeed=true` en Production carga + audita; re-ejecución sin duplicados.

- [ ] T048 [US4] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Seeding/Demo/DemoDataSeeder.cs` (Order 900, Category Test, Scope Tenant): personas/asociados demo vía `COR_People` + hijas (principio V), productos, facturas y movimientos de ejemplo **insertados** (nunca editando asentados — principio XI); todo con `CreatedBy="system:seed-demo"` (FR-020)
- [ ] T049 [US4] Implementar la resolución del default por ambiente de `RunTestSeed` (on Development/QA, off Production; override explícito gana) en `DatabaseOptions`/orquestador + evento auditable `Database.Seed.TestSeedEnabledInProduction` al arranque cuando aplica (FR-017)
- [ ] T050 [P] [US4] Documentar el procedimiento de limpieza de datos demo (localización por `system:seed-demo`, script de mantenimiento técnico autorizado conforme principio XI) en `docs/operaciones/limpieza-datos-demo.md`
- [ ] T051 [P] [US4] Tests de integración `tests/IngenIA365ERP.API.IntegrationTests/Database/DemoSeed_Tests.cs`: Development → demo presente; Production default → ausente + log de omisión; Production con flag → presente + evento Mongo; idempotencia (SC-005)

**Checkpoint US4**: ambientes de capacitación habilitados sin riesgo para producción.

---

## Phase 7: User Story 5 — Paridad funcional verificable entre motores (Priority: P3)

**Goal**: suites existentes verdes en ambos motores; omisión de una migración de un proveedor detectable; smoke de aprovisionamiento para módulos sin suite.

**Independent Test**: quickstart §7 — `DB_PROVIDER=PostgreSql|SqlServer` × `dotnet test` integración = 100 % verde; romper la paridad de migraciones a propósito → el check falla.

- [ ] T052 [US5] Parametrizar las fixtures de integración por `DB_PROVIDER` en `tests/IngenIA365ERP.API.IntegrationTests/Fixtures/` (incluida `CentralIdentityApiFixture`): `Testcontainers.MsSql` o `Testcontainers.PostgreSql` + **migraciones EF del proveedor en lugar de los DDL oficiales** (nueva fuente de verdad, D-11); master sembrado y capturador SMTP intactos
- [ ] T053 [P] [US5] Crear `tests/IngenIA365ERP.API.IntegrationTests/Database/Provisioning_SmokeTests.cs`: migrar todo + seed paramétrico + inventario de tablas/columnas esperadas por módulo (COR/ACC/LND/PAY/INV/…) idéntico entre motores (clarificación #2, SC-008)
- [ ] T054 [P] [US5] Crear el check de paridad de migraciones (script `tools/scripts/check-migration-parity.ps1`: mismo conjunto de nombres lógicos en ambos ensamblados, diff ⇒ exit 1) e integrarlo como test en `tests/IngenIA365ERP.Architecture.Tests/Principles/Feature004_MultiProvider.cs` (SC-008)
- [ ] T055 [US5] Ejecutar la matriz completa localmente: `DB_PROVIDER=PostgreSql` y `DB_PROVIDER=SqlServer` × (Application + Architecture + Integration) — 100 % verde en ambos; tiempos y resultados registrados (SC-003)
- [ ] T056 [P] [US5] Documentar la estrategia CI (matriz de proveedor como gate de PR, artefactos `script --idempotent` por release) en `docs/operaciones/ci-multi-motor.md` según contracts/cli.md

**Checkpoint US5**: paridad = propiedad verificada, no promesa.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T057 [P] Congelar el corpus DDL: `database/schema/README-CONGELADO.md` + `database/migration/README-CONGELADO.md` (referencia histórica, fuente de verdad hasta 2026-08, reemplazado por feature 004; D-13) y crear `database/schema/generated/{SqlServer,PostgreSql}/` con `.gitkeep` + primer script generado por release
- [ ] T058 [P] Actualizar documentación operativa: `docs/operaciones/setup-local-pruebas.md` (nuevo flujo: compose dual + AutoMigrate en lugar de DDL manual + gaps) e `docs/INDICE-DOCUMENTACION.md` (entrada feature 004)
- [ ] T059 Ejecutar `quickstart.md` end-to-end completo (§0–§8) sobre ambos motores, capturar evidencia en `docs/release-notes/004-multi-motor-bd/evidencia-pruebas.md` (SC-001…SC-008)
- [ ] T060 Sweep final: `dotnet test` completo (Domain + Application + Architecture + Integration en ambos providers) + verificación de las 12 compuertas constitucionales del plan

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: sin dependencias.
- **Phase 2 (Foundational)**: depende de Phase 1. Orden interno: T005–T013 (opciones/convenciones) → T014/T014a (DI) → T015 (factories) → T016 (migraciones iniciales) → T017 (validación dual) → T018 en paralelo. **BLOQUEA todas las user stories.**
- **US1 (Phase 3)**: depende de Foundational. No depende de US2 (usa CLI migrate de T016/T017 para su test independiente).
- **US2 (Phase 4)**: depende de Foundational. T025 (spike D-03) es la primera tarea y bloquea T029/T033.
- **US3 (Phase 5)**: depende de US2 (el orquestador se cuelga del inicializador T028 y del guard T027). T035→T036→(T037‖T038‖T039)→T040; T041→T042→T043; T044 tras T036.
- **US4 (Phase 6)**: depende de US3 (framework de seeding).
- **US5 (Phase 7)**: depende de US2 (migraciones aplicables); T052 puede arrancar en paralelo con US3/US4; T055 requiere US3 completa (los seeds participan del smoke).
- **Phase 8 (Polish)**: depende de las stories que se cierren para release; T059/T060 al final.

### Parallel Opportunities

- Phase 1: T002, T003, T004 paralelos tras T001.
- Phase 2: T007, T008 paralelos a T005/T006; T010, T011, T012 paralelos entre sí tras T009; T018 paralelo desde T014.
- US1: T020, T021, T024 paralelos; T022 tras T019.
- US2: T026‖T027 tras T025; T030, T031 paralelos a T028; T032, T033 tras T028/T029.
- US3: T038, T039 paralelos a T037; T044, T045 paralelos tras T036/T042.
- US5: T053, T054, T056 paralelos tras T052.

## Implementation Strategy

**MVP first (US1 + US2 + US3)**: Setup (~½ día) → Foundational (~4-5 días: el par de migraciones iniciales de 270 entidades y su validación dual T017 es el grueso) → US1 (~1 día) → US2 (~3-4 días, spike D-03 primero) → US3 (~3 días). **STOP y VALIDAR**: quickstart §1–§4 + §6 en ambos motores — el producto es instalable y operable en PG y SQL Server.

**Post-MVP incremental**: US4 (~1 día) → US5 (~2 días) → Polish (~1-2 días).

**Riesgo mayor**: D-03 (schema-per-tenant multi-proveedor) — por eso el spike T025 abre la Phase 4; si el spike obliga a cambiar de estrategia, solo T029/T033 se replantean (el contrato FR-014 no cambia).

## Notes

- Constitución: tests obligatorios por handler; cero catch silenciado; seeds con campos de auditoría; el endpoint pasa por los 4 behaviors; scripts DBA con header del principio XII.
- Toda migración nueva se genera EN PAR (ambos ensamblados) con `tools/scripts/add-migration.ps1`; la omisión la caza T054.
- Checkpoints válidos de demo: post-Foundational (esquema dual por CLI), post-US1 (selección por config), post-US2 (auto-aprovisionamiento), post-US3 (MVP operable), post-US4, post-US5.
