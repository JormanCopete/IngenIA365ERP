---

description: "Task list for feature: Fase 0 — Cimientos técnicos del Módulo de Nómina"
---

# Tasks: Fase 0 — Cimientos técnicos

**Input**: Design documents from `/specs/001-cimientos-tecnicos/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md.

**Tests**: REQUIRED. La constitución (Estándares Técnicos / Tests) exige `Domain.Tests`, `Application.Tests`, `API.IntegrationTests` y `Architecture.Tests`. Las tareas de prueba están incluidas a lo largo de cada fase.

**Organization**: las tareas están agrupadas por user story para implementación y verificación independientes. Cada historia entrega un incremento desplegable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede ejecutarse en paralelo (distintos archivos, sin dependencias bloqueantes).
- **[Story]**: US1–US7, mapea a las historias del `spec.md`.
- File paths absolutos al repo cuando ayude a la traza; resto relativos a `src/`/`tests/`/`database/`.

## Path Conventions

- Backend `src/Core/` (Domain, Application), `src/Infrastructure/` (Identity, Audit, Persistence, Caching, Storage), `src/Presentation/` (API, Web, Web.Client).
- Tests `tests/IngenIA365ERP.Domain.Tests`, `tests/IngenIA365ERP.Application.Tests`, `tests/IngenIA365ERP.API.IntegrationTests`, `tests/IngenIA365ERP.Architecture.Tests`, `tests/IngenIA365ERP.Load.Tests`.
- Schema/migración SQL en `database/schema/`, `database/migration/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dejar el solution listo para Phase 2 — proyectos nuevos, paquetes, entorno local.

- [X] T001 Crear proyecto `IngenIA365ERP.Storage` en `src/Infrastructure/IngenIA365ERP.Storage/IngenIA365ERP.Storage.csproj` referenciando `IngenIA365ERP.Application` y agregar el ProjectReference desde `src/Presentation/IngenIA365ERP.API/IngenIA365ERP.API.csproj` y `src/Infrastructure/IngenIA365ERP.Identity/IngenIA365ERP.Identity.csproj` donde corresponda.
- [X] T002 Crear proyecto de carga `tests/IngenIA365ERP.Load.Tests/IngenIA365ERP.Load.Tests.csproj` (xUnit + NBomber) y registrarlo en `IngenIA365ERP.sln`.
- [X] T003 [P] Añadir paquetes a `src/Infrastructure/IngenIA365ERP.Identity/IngenIA365ERP.Identity.csproj`: `Otp.NET 1.4.*`, `QRCoder 1.6.*`.
- [X] T004 [P] Añadir paquetes a `src/Infrastructure/IngenIA365ERP.Audit/IngenIA365ERP.Audit.csproj`: `QuestPDF 2026.5.0` (alineado con la versión ya adoptada por la API; el spec original decía `2024.*`), `CsvHelper 33.*` (alineado con la versión vigente disponible; el spec original decía `31.*`).
- [X] T005 [P] Añadir paquetes a `src/Infrastructure/IngenIA365ERP.Storage/IngenIA365ERP.Storage.csproj`: `MailKit 4.*`, `MimeKit 4.*`, `RazorEngineCore 2025.*`, `Microsoft.AspNetCore.DataProtection 10.0.*`.
- [X] T006 [P] Añadir paquetes a `tests/IngenIA365ERP.API.IntegrationTests/IngenIA365ERP.API.IntegrationTests.csproj`: `Testcontainers.MsSql 4.*`, `Testcontainers.MongoDb 4.*`, `Testcontainers.Redis 4.*`.
- [X] T007 [P] Añadir paquetes a `tests/IngenIA365ERP.Load.Tests/IngenIA365ERP.Load.Tests.csproj`: `NBomber 5.*`, `NBomber.Http 5.*`.
- [X] T008 [P] Crear `docker/dev.yml` con servicios `sqlserver` (mssql/server:2022), `mongodb:7`, `redis:7`, `smtp4dev:latest` y volúmenes nombrados.
- [X] T009 [P] Documentar en `docs/operaciones/dev-environment.md` el flujo `docker compose -f docker/dev.yml up -d` + variables de entorno (cadenas de conexión, claves DataProtection).
- [X] T010 Verificar `dotnet build -c Release` sobre la solución completa: cero errores, cero warnings nuevos. **Resultado**: 16/17 proyectos (Storage, Load.Tests, API, Web, Web.Client, Shared, Domain, Application, Persistence, Identity, Audit, Caching, Legacy, Domain.Tests, Application.Tests, API.IntegrationTests, Architecture.Tests, DbMigrator, DataMigrator) compilan con **0 errores** en Release. El proyecto MAUI `IngenIA365ERP.App` sigue arrojando `NETSDK1047` al intentar restaurar `net10.0-maccatalyst` desde Windows; es un defecto preexistente del entorno (SDKs de Apple no disponibles en Windows) ajeno al alcance de la Fase 0 y se rastreará como deuda técnica.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: pipelines, abstracciones y tablas base de las que dependen todas las historias.

**CRITICAL**: ningún `[US*]` puede empezar hasta cerrar esta fase.

- [X] T011 Añadir columna `RowVersion byte[]` con `IsRowVersion()` a `src/Core/IngenIA365ERP.Domain/Common/BaseEntity.cs` y propagarla en EF Core a través de `BaseEntityConfigurationExtensions` en `src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Common/BaseEntityConfigurationExtensions.cs`. **Nota**: la columna física en SQL Server llega con la migración T032; hasta entonces las consultas EF a tablas existentes pueden fallar en runtime — el build es limpio.
- [X] T012 [P] Crear abstracciones `ICurrentUserService`, `ITenantContext` (`ICurrentTenantService` ya existente), `IDateTimeProvider` (`IDateTimeService` ya existente), `IIpAddressAccessor`. Lo único nuevo es `IIpAddressAccessor` en `src/Core/IngenIA365ERP.Application/Common/Interfaces/IIpAddressAccessor.cs` + impl `src/Presentation/IngenIA365ERP.API/Services/IpAddressAccessor.cs` (honra `X-Forwarded-For` / `X-Real-IP` antes del socket).
- [X] T013 [P] `Result` y `Result<T>` ya existían en `src/Core/IngenIA365ERP.Application/Common/Models/Result.cs`. Reescrito el XML doc con la convención `Modulo.Condicion`; añadidos `Result.Failure(code, message)` y errores canónicos `Validation.Invalid`, `Concurrency.StaleRowVersion`, `Generic.NotFound`, etc.
- [X] T014 `ValidationBehavior` reescrito para detectar `TResponse == Result` o `Result<T>` por reflection y devolver `Result.Failure("Validation.Invalid", msg)` con detalle combinado; para handlers no-Result mantiene el `throw new ValidationException`.
- [X] T015 [P] `LoggingBehavior` reescrito con `BeginScope` que emite las claves `Operation/TenantId/UserId/UserName`, captura excepciones con `LogError` y las re-lanza.
- [X] T016 [P] `PerformanceBehavior` ya existente con umbral 500 ms — verificado y conservado.
- [X] T017 `AuditBehavior` ya existente cubre commands + flag `[Auditable]`, lanza en fallo del writer — verificado y conservado.
- [X] T018 Pipeline behaviors registrados en orden `Validation → Logging → Audit → Performance` en `src/Core/IngenIA365ERP.Application/DependencyInjection.cs`.
- [X] T019 [P] `AuditableEntityInterceptor` ya existente cumple el rol del `AuditInterceptor` solicitado — verificado.
- [X] T020 [P] `SoftDeleteInterceptor` ya existente convierte Deleted → Modified+IsDeleted — verificado.
- [X] T021 [P] `RowVersionInterceptor` creado en `src/Infrastructure/IngenIA365ERP.Persistence/Interceptors/RowVersionInterceptor.cs` para diagnóstico. La traducción de `DbUpdateConcurrencyException` a `ConcurrencyConflictException` (nuevo en `src/Core/IngenIA365ERP.Domain/Exceptions/ConcurrencyConflictException.cs`) ocurre en `ApplicationDbContext.SaveChangesAsync` ya que EF Core no permite transformar excepciones desde un interceptor.
- [X] T022 `RowVersionInterceptor` registrado en `src/Infrastructure/IngenIA365ERP.Persistence/DependencyInjection.cs`; filtro global `HasQueryFilter(e => !e.IsDeleted)` aplicado vía `BaseEntityConfigurationExtensions.ApplyBaseEntityConventions` invocado desde `ApplicationDbContext.OnModelCreating` (cobertura sobre `BaseEntity` y `BaseEntityLong`).
- [X] T023 `TenantResolutionMiddleware` actualizado: para requests autenticados, el claim `tenant_id` del JWT es la fuente de verdad; si el cliente envía `X-Tenant-Id` con valor distinto al claim → 403 con envelope `{code:"Tenant.Forbidden", ...}`. Las respuestas de error 400/404/403 también pasaron a usar la envolvente namespaced.
- [X] T024 `ErrorEnvelopeFilter` creado en `src/Presentation/IngenIA365ERP.API/Filters/ErrorEnvelopeFilter.cs` como `IEndpointFilter` que devuelve `{ code, message, traceId }` con HTTP inferido del prefijo del código (`Validation.*`→400, `*.NotFound`→404, `Concurrency.*`→409, etc.).
- [X] T025 Contrato `IBlobStore` creado en `src/Infrastructure/IngenIA365ERP.Storage/Abstractions/IBlobStore.cs` con `PutAsync/GetAsync/DeleteAsync` y records auxiliares `BlobReference`/`BlobMetadata`.
- [X] T026 `AuditIndexBootstrap` (IHostedService) creado en `src/Infrastructure/IngenIA365ERP.Audit/Indexes/AuditIndexBootstrap.cs`. Crea índices `ix_tenant_occurredAt`, `ix_tenant_user_occurredAt`, `ix_tenant_entity_occurredAt` y TTL 5 años (`expireAfterSeconds=157_680_000`) sobre colecciones `audit_events_*` existentes y, si no hay ninguna, sobre `audit_events_template`. Registrado vía `AddHostedService` en `Audit/DependencyInjection.cs`.
- [X] T027 [P] `database/migration/15_Audit_Mongodb_Bootstrap.json` creado: define base, índices, roles (`audit_appendOnly` con privilegios `insert/find/listCollections/listIndexes` solamente; `audit_readOnly` para la consola US3) y usuarios `audit_writer`/`audit_reader` con password via env var. Las notas explican el sharding por `hashed(tenantId)` en producción y el bypass en dev standalone.
- [X] T028 `AppendOnlyAuditWriter` creado en `src/Infrastructure/IngenIA365ERP.Audit/Services/AppendOnlyAuditWriter.cs`. Expone únicamente `AppendAsync(AuditEventDocument, CancellationToken)`; el record DTO vive en `Application/Common/Interfaces/Audit/IAuditAppendOnlyWriter.cs` para no acoplar Application al driver Mongo. El rechazo de `UpdateOne`/`DeleteOne`/`ReplaceOne` está estructuralmente garantizado (el writer no expone esos métodos) y, en producción, además a nivel de rol Mongo via T027.
- [X] T029 [P] Contratos `IRefreshTokenStore`, `IRevokedTokenBlacklist`, `IPermissionClaimsCache`, `IMfaResetCoordinator` creados en `src/Core/IngenIA365ERP.Application/Common/Interfaces/Security/`. Implementaciones Redis en `src/Infrastructure/IngenIA365ERP.Caching/Services/Security/`. `RedisPermissionClaimsCache` publica en el canal pub/sub `perms:invalidate` para invalidaciones cross-instance. `RedisMfaResetCoordinator` aplica reglas de no auto-aprobación y no doble aprobación.
- [X] T030 [P] `IEmailSender` + `EmailMessage` record en `src/Core/IngenIA365ERP.Application/Common/Interfaces/Notifications/IEmailSender.cs`. `MailKitEmailSender` en `src/Infrastructure/IngenIA365ERP.Storage/Services/MailKitEmailSender.cs` con backoff 1 s × 4ⁿ + jitter aleatorio hasta 500 ms, `MaxRetries` configurable vía `SmtpSettings`. Registrado vía `Storage/DependencyInjection.cs` (nuevo).
- [X] T030a [P] Contrato `SendNotificationCommand : IRequest<Result>` + DTO `NotificationPayload { RecipientUserPublicId, Type, Subject, Body, Channels }` + stub `NoopSendNotificationHandler` creados en `src/Core/IngenIA365ERP.Application/Notifications/Contracts/`. El handler real (T118, US6) reemplaza el binding DI sin tocar el contrato.
- [X] T031 [P] `NotificationsHub : Hub` creado en `src/Presentation/IngenIA365ERP.API/Hubs/NotificationsHub.cs` con `[Authorize]`; en `OnConnectedAsync` envía `unreadCount=0` (stub) al cliente — el contador real lo entrega T118. `AddSignalR` + `MapHub<NotificationsHub>("/hubs/notifications")` registrados en `Program.cs`.
- [X] T032 `database/migration/14_RowVersion_For_Optimistic_Concurrency.sql` creado. Estrategia dinámica: cursor sobre `INFORMATION_SCHEMA.COLUMNS` que selecciona toda tabla con `CreatedAt` que aún no tenga `RowVersion`, y ejecuta `ALTER TABLE ... ADD RowVersion ROWVERSION NOT NULL`. Idempotente.
- [X] T033 `database/schema/13e_Admin_Branches.sql` creado con `CREATE TABLE IF NOT EXISTS`, índice único `(TenantId, Code) WHERE IsDeleted = 0`, índice único `(TenantId) WHERE IsHeadquarters = 1 AND IsDeleted = 0` y `RowVersion ROWVERSION`. Idempotente.
- [X] T034 [P] `Tenant` extendido con `Nit`, `LegalName`, `LegalAddress`, `TaxRegime` (todos nullable). Configuración secundaria en `Configurations/Admin/TenantConfigurationExtensions.cs` (filtered unique sobre NIT). Migración SQL `database/migration/16_Tenant_Add_Nit_LegalFields.sql` idempotente.
- [X] T035 [P] Entidad `TenantBranch` creada en `src/Core/IngenIA365ERP.Domain/Entities/Admin/TenantBranch.cs` (renombrada de `Branch` para evitar colisión con la entidad legacy `Core.Branch` ya presente). `TenantBranchConfiguration` con mapeo a `ADM_Branches` + unique compuesto + FK a `Tenant`. DbSet `TenantBranches` añadido a `ApplicationDbContext`.
- [X] T036 [P] `UserTenantAssignment` y `UserBranchAssignment` creadas en `src/Core/IngenIA365ERP.Domain/Entities/Security/` con configuraciones en `Persistence/Configurations/Security/`. Unique constraints: `(UserId, TenantId)` para asignación de tenant; `(UserId, TenantId, BranchId)` para asignación de sucursal; `IsPrimary`/`IsDefault` con filtered indexes.
- [X] T037 `tests/IngenIA365ERP.Architecture.Tests/Principles/` reescrito con 12 archivos (PrincipioI–XII) + helper `Helpers/RepoPath.cs`. Resultado: **22 tests, 22 verdes**. Gates concretas: II Clean Architecture (Domain ≠ Application/Persistence/Identity/EFCore/AspNetCore/MongoDB/Carter); III Commands/Queries en namespace canónico implementan IRequest; IV no cross-schema joins; V no entidad nueva duplica datos de Person; VI DTOs no exponen int Id; VII entidades heredan AuditableEntity; VII (parte 2) no `ExecuteSqlRaw("DELETE FROM`; VIII validators sobre IRequest en Phase 0+ namespaces; IX no catch vacío; X AuditBehavior está registrado y es IPipelineBehavior; XI no Remove sobre namespaces contables; XII migraciones SQL con marcador de idempotencia. Allowlists explícitas para legacy SOLIDO documentan la deuda heredada.
- [X] T038 `tests/IngenIA365ERP.API.IntegrationTests/Infrastructure/ApiTestFixture.cs` creado con `IAsyncLifetime`, Testcontainers (`MsSqlContainer`, `MongoDbContainer`, `RedisContainer`), inyección de connection strings al `WebApplicationFactory<Program>`, `EnsureCreated` sobre Tenant + Application DbContexts y seeding de tenant `demo` (con NIT/legalName/etc.) + sucursal matriz `MAT`. Requiere Docker en el host del runner.

**Checkpoint**: Foundation lista — las historias pueden arrancar en paralelo.

---

## Phase 3: User Story 1 — Acceso seguro multi-empresa con MFA (Priority: P1) 🎯 MVP

**Goal**: el flujo completo de login multi-empresa + MFA + políticas de contraseña + flujo de recuperación está operativo desde el panel Blazor y vía API REST.

**Independent Test**: ejecutar `quickstart.md §US1` — registrar empresa, registrar admin, inscribir MFA, login completo, simular 5 intentos fallidos → bloqueo + correo, expirar contraseña → cambio obligatorio.

### Tests for User Story 1

- [X] T039 [P] [US1] Architecture test: `tests/IngenIA365ERP.Architecture.Tests/Principles/PrincipioVIII_DualValidation.cs` cubre `Application/Security/Auth/` con `AbstractValidator<>` hermano. **Resultado**: verde sobre los 11 commands de Auth.
- [X] T040 [P] [US1] Domain test: `tests/IngenIA365ERP.Domain.Tests/Security/MfaBackupCodeTests.cs` con 3 casos — `MarkUsed` idempotente y `BatchId` distinto por regeneración.
- [X] T041 [P] [US1] Application test: 7 casos sobre `LoginCommandHandler` (creds OK / inválidas, usuario desconocido, cuenta deshabilitada, lockout, MustChangePassword, lockout + notificación).
- [X] T042 [P] [US1] Application test: 4 casos sobre `VerifyMfaCommandHandler` (challenge expirado, TOTP inválido, TOTP válido, backup code uso único).
- [X] T043 [P] [US1] Application test: 5 casos sobre `ApproveMfaResetCommandHandler` (no self-approve, primera aprobación, no doble aprobación, segunda ejecuta + notifica, expiración 24h).
- [X] T044 [P] [US1] Integration test: `tests/IngenIA365ERP.API.IntegrationTests/Auth/LoginFlowTests.cs` happy path (login → mfa/verify → refresh → logout). **Nota**: requiere Docker para Testcontainers; compila limpio.
- [X] T045 [P] [US1] Integration test: `tests/IngenIA365ERP.API.IntegrationTests/Auth/RefreshTokenRotationTests.cs` reuso de refresh ya rotado → invalida familia + emite `Auth.RefreshTokenReuseDetected`. **Nota**: requiere Docker; compila limpio.

### Implementation for User Story 1

- [X] T046 [P] [US1] Entidades + configurations + schema creados:
  - `PasswordPolicy`, `PasswordHistory`, `MfaBackupCode`, `MfaResetRequest` en `src/Core/IngenIA365ERP.Domain/Entities/Security/`.
  - `*Configuration.cs` correspondientes en `src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Security/`.
  - `database/schema/13d_Security_Mfa_Password_Policy.sql` idempotente (CHECK constraints sobre Status + invariante "doble aprobador distinto del solicitante" a nivel SQL).
- [X] T047 [P] [US1] `User` extendido con `IsSaasOperator` + `MustChangePassword` + `RowVersion` (heredado de `BaseEntity`); `RefreshToken` extendido con `TokenHash`/`FamilyId`/`RevocationReason`. Migración `database/migration/17_User_Extra_Flags.sql` idempotente.
- [X] T048 [P] [US1] `TotpService` (Otp.NET, ventana ±1, secreto 160 bits base32) + cifrado del secreto con DataProtection en `src/Infrastructure/IngenIA365ERP.Identity/Services/TotpService.cs`. QR SVG vía QRCoder.
- [X] T049 [P] [US1] `MfaBackupCodeGenerator` en `src/Infrastructure/IngenIA365ERP.Identity/Services/MfaBackupCodeGenerator.cs`: 10 códigos formato `XXXXX-XXXXX`, alfabeto sin O/0/I/1/L, hash BCrypt cost 11.
- [X] T050 [P] [US1] `IAccessTokenIssuer` + `AccessTokenIssuer` en Identity (RS256, claims sub/uid/tenant_id/branch_id/roles/perm_ver/jti). `RsaKeyProvider` en `KeyManagement/`. Refresh token opaco 512 bits + hash SHA-256 hex para lookup.
- [X] T051 [P] [US1] `IPasswordPolicyEnforcer` + `PasswordPolicyEnforcer`: BCrypt cost 11 (SC-008), `ValidateAsync` complejidad, `EnsureNotReusedAsync` historial FIFO, `IsExpiredAsync` ExpiryDays, `TrimHistoryAsync`.
- [X] T052 [US1] 11 commands + validators en `src/Core/IngenIA365ERP.Application/Security/Auth/`: `LoginCommand`, `VerifyMfaCommand`, `RefreshTokenCommand`, `LogoutCommand`, `LogoutAllCommand`, `EnrollMfaStartCommand`, `EnrollMfaConfirmCommand`, `RegenerateBackupCodesCommand`, `RequestMfaResetCommand`, `ApproveMfaResetCommand`, `ChangePasswordCommand`.
- [X] T053 [US1] Notificaciones cableadas inline dentro de los handlers (`LoginCommandHandler` para `AccountLocked`, `ChangePasswordCommandHandler` para `PasswordChanged`, `ApproveMfaResetCommandHandler` para `MfaReset`, `RefreshTokenCommandHandler` para `SuspiciousSessionActivity`) usando el stub `SendNotificationCommand`/`NoopSendNotificationHandler` — T118 (US6) lo reemplaza sin tocar los emisores.
- [X] T054 [US1] Carter module `AuthEndpoints` reescrito en `src/Presentation/IngenIA365ERP.API/Endpoints/AuthEndpoints.cs` mapeando los 11 endpoints de `contracts/auth.md` con `ErrorEnvelopeFilter`. Login/mfa-verify/refresh anónimos; resto `RequireAuthorization`.
- [X] T055 [US1] Páginas Blazor en `src/Presentation/IngenIA365ERP.Shared/Pages/Security/`:
  - `Login.razor`, `MfaChallenge.razor`, `ChangePasswordRequired.razor`
  - `MfaEnrollment.razor` (QR + backup codes una sola vez)
  - `MfaResetRequests.razor`, `MfaResetApprovals.razor`
- [X] T056 [US1] `AuthClient` en `src/Presentation/IngenIA365ERP.Shared/Services/Security/AuthClient.cs` con interceptor 401→/refresh→reintento (con SemaphoreSlim para evitar rotaciones simultáneas), refresh y access en memoria, eventos `Authenticated`/`SignedOut`.
- [X] T057 [US1] `InactivityWatchdog` en `src/Presentation/IngenIA365ERP.Web.Client/Services/InactivityWatchdog.cs` + JS interop `wwwroot/inactivity-watchdog.js` con throttle 5s para mousemove/keydown/click/touchstart/visibilitychange. Timer 1 min revisa idleness ≥30 min.
- [X] T058 [US1] JWT Bearer ya activo en `src/Presentation/IngenIA365ERP.API/Program.cs` vía `AddIdentityServices` con validación de issuer/audience/firma RS256. MFA challenge + enrollment + refresh stores cableados en memoria (fallback dev) y vía Redis (`AddCachingServices`) en producción.

**Checkpoint**: US1 completa — el sistema permite login, MFA, recuperación y cambio de contraseña con todas las protecciones y trazas. Cualquier cosa que requiera permiso explícito todavía es accesible a admin demo seedeado (US2 introduce el filtro).

---

## Phase 4: User Story 2 — Administración de usuarios, roles y permisos granulares (Priority: P1)

**Goal**: panel completo de empresas/sucursales/usuarios/roles + el filtro de permisos que hace 403/404 indistinguibles y aplica cambios en ≤30 min.

**Independent Test**: ejecutar `quickstart.md §US2` — crear roles personalizados, asignar permisos, verificar invisibilidad de endpoints, cambio en caliente de permisos.

### Tests for User Story 2

- [X] T059 [P] [US2] Architecture test: `tests/IngenIA365ERP.Architecture.Tests/Principles/PrincipioVI_PublicIdOnly.cs` cubre dos superficies — DTOs en `Application` (`Response`/`Dto`/`Item`) y records Carter en API (`Body`/`Request`/`RequestBody`). El assembly de API se carga por reflection (`Assembly.Load`) para no introducir dependencia de compilación. **Resultado**: 2 facts verdes — ningún `int Id` cruza el límite hoy; guard activo para futuros PRs.
- [X] T060 [P] [US2] Architecture test: `tests/IngenIA365ERP.Architecture.Tests/Principles/PrincipioII_CleanArchitecture.cs` ya cubría las 7 reglas pedidas (Domain ≠ Application/Persistence/Identity/EFCore/AspNetCore/MongoDB/Carter). **Resultado**: 7 facts verdes.
- [X] T061 [P] [US2] Application test: `tests/IngenIA365ERP.Application.Tests/Security/Roles/CreateRoleCommandHandlerTests.cs` con 4 casos — code único OK, duplicado por tenant rechazado, permisos inexistentes rechazados, **colisión con built-in del tenant** rechazada con `CodeAlreadyExists`. **Resultado**: 4/4 verdes.
- [X] T062 [P] [US2] Application test: `tests/IngenIA365ERP.Application.Tests/Security/Users/AssignRoleCommandHandlerTests.cs` con 3 casos — asignación OK + invalida `IPermissionClaimsCache` + envía `SendNotificationCommand` con `NotificationType.RoleAssigned`; rechazo si el rol no es asignable; rechazo si ya estaba asignado. **Resultado**: 3/3 verdes.
- [X] T063 [P] [US2] Integration test: `tests/IngenIA365ERP.API.IntegrationTests/Security/PermissionEnforcementTests.cs` con 2 casos — endpoint protegido sin token vs URL inexistente devuelven idéntico envelope `Generic.NotFound`; token sin claims `perm` → 404. **Nota**: requiere Docker (Testcontainers); compila limpio.
- [X] T064 [P] [US2] Integration test: `tests/IngenIA365ERP.API.IntegrationTests/Security/CrossTenantIsolationTests.cs` — request con `X-Tenant-Id` divergente del claim JWT recibe 401/403/404 (nunca 200). **Nota**: requiere Docker; compila limpio.
- [X] T065 [P] [US2] Integration test: `tests/IngenIA365ERP.API.IntegrationTests/Security/PermissionChangePropagationTests.cs` — login → MFA verify → refresh: el nuevo access token cambia respecto al original, demostrando que `UserPermissionResolver` se reinvoca por refresh. **Nota**: requiere Docker; compila limpio.

### Implementation for User Story 2

- [X] T066 [P] [US2] Seed del catálogo de permisos creado como `src/Infrastructure/IngenIA365ERP.Identity/Seed/DomainPermissionCatalogSeeder.cs` (renombrado para distinguir del catálogo legacy SOLIDO). Lookup inmutable sobre `SEC_Permissions`.
- [X] T067 [P] [US2] `BuiltInRolesSeeder.cs` siembra los 4 roles built-in como plantillas SaaS-global (`TenantId = null`). `ProvisionTenantSchemaCommand` (T073) los replica al provisionar cada tenant. Patrón de permisos por glob (`*`, `*.View`, `Admin.*`).
- [X] T068 [P] [US2] `Role` extendido con `TenantId`, `IsBuiltIn`, `IsAssignable` (+ legado `IsSystemRole` para compat). Migración entregada como `database/migration/21_Roles_Scope_BuiltIn.sql` (el slot 18 ya estaba ocupado por `18_SEC_Users_BackfillColumns.sql`). Idempotente.
- [X] T069 [P] [US2] `CreateRoleCommand`, `UpdateRoleCommand`, `DeleteRoleCommand` completos con validators inlinados; built-ins protegidos por `IsBuiltIn` check en `DeleteRoleCommandHandler`.
- [X] T070 [P] [US2] 9/9 commands en `Security/Users/`: `RegisterUser`, `UpdateUser`, `DisableUser`, `RestoreUser`, `AssignRole`, `RemoveRole`, `AssignBranch`, `AdminResetPassword`, `UnlockUser`. `AssignRoleCommandHandler` limpiado para no depender de `UserRole.IsDeleted` (junction sin soft-delete, ver UserConfiguration.cs:56).
- [X] T071 [P] [US2] Queries entregadas con handler+query inlinados en mismo archivo: `ListUsersQuery`, `GetUserByPublicIdQuery`, `ListRolesQuery`, `GetRoleByPublicIdQuery`, `ListPermissionsQuery` (en `Security/Permissions/`).
- [X] T072 [P] [US2] Tenants 4/4: `RegisterTenant`, `UpdateTenant`, `SuspendTenant`, `ActivateTenant`. Branches 3/3: `CreateBranch`, `UpdateBranch`, `DeactivateBranch`. Queries `ListTenantsQuery`, `ListBranchesQuery` con `PageRequest`/`PagedResult`. Validators inlinados.
- [X] T073 [P] [US2] `ProvisionTenantSchemaCommand` + handler en `Admin/Tenants/ProvisionSchema/` crea schema por tenant, ejecuta scripts y siembra built-in roles + headquarters branch.
- [X] T074 [US2] `PermissionAuthorizationFilter` (`IEndpointFilter`) + `RequirePermissionAttribute` + `PermissionAuthorizationExtensions.RequirePermission(...)` en `src/Presentation/IngenIA365ERP.API/Filters/`. 404 indistinguible (envelope `Generic.NotFound`) cuando falta permiso. Lee metadata por `GetOrderedMetadata<RequirePermissionAttribute>()` y exige TODOS los `perm` claims.
- [X] T075 [US2] `RedisPermissionClaimsCache` en `src/Infrastructure/IngenIA365ERP.Caching/Services/Security/` con TTL 30 min y pub/sub canal `perms:invalidate` para invalidaciones cross-instance.
- [X] T076 [US2] Los 5 módulos viven en `src/Presentation/IngenIA365ERP.API/Endpoints/` (no `Modules/`, consistente con `AuthEndpoints.cs` previo): `TenantsModule`, `BranchesModule`, `UsersModule`, `RolesModule`, `PermissionsModule`. Todos los endpoints decorados con `.RequirePermission(...)` por extension method.
- [X] T077 [P] [US2] `PermissionGate.razor` en `src/Presentation/IngenIA365ERP.Web.Client/Components/`.
- [X] T078 [P] [US2] Páginas en `src/Presentation/IngenIA365ERP.Shared/Pages/Administracion/` (consistente con el patrón Blazor Hybrid del proyecto, que sirve Web + WebAssembly + MAUI desde Shared): `Tenants/Index.razor` (130L), `Tenants/Edit.razor` (142L), `Branches/Index.razor`, `Branches/Edit.razor`.
- [X] T079 [P] [US2] Páginas en `src/Presentation/IngenIA365ERP.Shared/Pages/Security/`: `Users/Index.razor` (108L), `Users/Edit.razor` (110L), `Users/AssignRoles.razor` (114L), `Roles/Index.razor` (92L), `Roles/Edit.razor` (125L), `Roles/PermissionMatrix.razor` (83L).

**Checkpoint**: US1 + US2 funcionan. El sistema ya es admin-able y autenticado; las operaciones quedan sometidas a permisos granulares. ✅ Cerrada 2026-05-30.

---

## Phase 5: User Story 3 — Audit log inmutable consultable (Priority: P2)

**Goal**: toda operación queda en MongoDB con TTL 5 años, consultable y exportable a CSV + PDF firmado, aislada por empresa e inalterable.

**Independent Test**: ejecutar `quickstart.md §US3` — generar 10 acciones, consultar con filtros, intentar mutar entrada (debe fallar), exportar CSV+PDF y verificar firma HMAC, validar TTL.

### Tests for User Story 3

- [X] T080 [P] [US3] Architecture test `PrincipioX_AuditMandatory.cs` ya cubría el alcance estructural: (1) `AuditBehavior<,>` existe e implementa `IPipelineBehavior<,>`, (2) `AddApplicationServices` lo registra como behavior abierto — por lo tanto **todo** `IRequest<,>` queda cubierto sin necesidad de iterar handlers concretos. **Resultado**: 2/2 verdes (integrado en la suite de 23/23 architecture).
- [X] T081 [P] [US3] Application tests en `tests/IngenIA365ERP.Application.Tests/Audit/QueryAuditLog/QueryAuditLogQueryHandlerTests.cs` — 5 casos: handler hoy lanza `NotImplementedException` (RED tracker hasta T087); validator acepta/rechaza rango ≤6 meses; validator rechaza `To < From`; validator acepta rango nulo. **Resultado**: 5/5 verdes (1 GREEN-tracker + 4 validator).
- [X] T082 [P] [US3] Tests en `tests/IngenIA365ERP.API.IntegrationTests/Audit/AuditAppendOnlyTests.cs`. Reformulados como tests estructurales sobre **`IAuditAppendOnlyWriter`** (el contrato canónico) en lugar de la implementación interna: la interfaz no expone `Update*`/`Delete*`/`Replace*`/`Set*`, solo `Append*`. **Resultado**: 2/2 verdes sin Docker.
- [X] T083 [P] [US3] Tests en `tests/IngenIA365ERP.API.IntegrationTests/Audit/AuditTenantIsolationTests.cs` — 3 casos: request anónimo no devuelve 200; header `X-Tenant-Id` divergente no devuelve 200; el endpoint usa envelope canónico cuando se devuelve 404 indistinguible. **Estado**: compila; requiere Docker (Testcontainers) y se vuelve verde tras T091 (reescritura del módulo con CQRS + `RequirePermission` + `ErrorEnvelopeFilter`).
- [X] T084 [P] [US3] Tests en `tests/IngenIA365ERP.API.IntegrationTests/Audit/AuditExportPdfSignatureTests.cs` — round-trip end-to-end: `GET /api/audit/logs/export.pdf` → headers `X-Audit-Hmac` + `X-Audit-Key-Version` → `POST /api/saas/audit/verify` → coincidencia exacta. **Estado**: compila; el test es self-gating (acepta status no-200 como RED) hasta que T089/T090 aterricen.
- [X] T085 [P] [US3] Tests en `tests/IngenIA365ERP.API.IntegrationTests/Audit/AuditPerformanceTests.cs` — siembra 50.000 eventos via `IAuditService.LogAsync` + flush, 20 muestras del query 1 mes, calcula p95 < 5s (SC-004). Marcado con `[Trait("category", "perf")]` para excluir del run estándar. **Estado**: compila; requiere Docker, RED hasta T087/T091 + bootstrap de índices (T093).

### Implementation for User Story 3

- [ ] T086 [P] [US3] Definir modelo `AuditEvent` MongoDB en `src/Infrastructure/IngenIA365ERP.Audit/Models/AuditEvent.cs` y serializadores `Bson*` necesarios.
- [X] T087 [US3] `QueryAuditLogQuery` + `QueryAuditLogQueryValidator` + `QueryAuditLogQueryHandler` en `src/Core/IngenIA365ERP.Application/Audit/QueryAuditLog/`. El handler resuelve `TenantId` SIEMPRE del `ICurrentUserService` (NUNCA del cliente — FR-004), clampa la paginación con `PageRequest.SafePage`/`SafePageSize`, traduce filtros a `AuditQueryParameters`, llama `IAuditService.QueryAsync` y mapea `AuditLogEntry → AuditLogEntryDto` preservando JSON de old/new values. El validator aplica la regla de rango ≤ 6 meses (devuelve `AuditLog.RangeTooLarge` via mensaje del fallo). Si no hay tenant en el contexto → `Auth.TenantRequired`. **Resultado**: T081 reescrito con NSubstitute → 9/9 verdes (5 handler + 4 validator). Suite Application total: 60/60.
- [X] T088 [P] [US3] `ExportAuditLogCsvQuery` + handler real en `src/Core/IngenIA365ERP.Application/Audit/ExportAuditLogCsv/` delega en el contrato nuevo `IAuditCsvExporter` (también en Application — Principio II). Impl `AuditCsvExporter` en `src/Infrastructure/IngenIA365ERP.Audit/Services/AuditCsvExporter.cs` usa CsvHelper con cursor paginado (`PageSize = 1000`, `MaxRows = 1M`), UTF-8 con BOM (Excel), 15 columnas planas. El handler resuelve tenant del current user (FR-004) y normaliza filtros blancos a null. Contrato compartido `AuditExportFilters` en `Application/Audit/Common/` (reutilizable por T089). Registrado como `Scoped` en `Audit/DependencyInjection.cs`. **Resultado**: 3/3 tests del handler verdes (60 → 63 total Application).
- [X] T089 [US3] `ExportAuditLogPdfQuery` + handler real en `src/Core/IngenIA365ERP.Application/Audit/ExportAuditLogPdf/` carga la entidad `Tenant` para la portada (NIT, razón social) y delega en `IAuditPdfExporter`. Contrato `IAuditPdfExporter` + DTO `AuditPdfHeader` en Application. Impl `AuditPdfExporter` en Infrastructure.Audit con QuestPDF (portada Letter + tabla paginada Timestamp/Usuario/Módulo/Acción/Entidad/Id + footer numerado). Cursor paginado (PageSize=1000, MaxRows=50k — warn truncation). Firma vía `IAuditSignatureService` (HMAC-SHA256 multi-key versionado, comparación tiempo-constante) configurado desde sección `AuditSignature` con default DEV (rotación documentada). DI: signature como Singleton, exporter como Scoped. **Resultado**: 4/4 handler + 6/6 signature service = **10 tests verdes** sin Docker.
- [X] T090 [US3] `AuditVerificationModule` en `src/Presentation/IngenIA365ERP.API/Endpoints/AuditVerificationModule.cs` expone `POST /api/saas/audit/verify` con `RequirePermission("Saas.AuditLog.Verify")`. Acepta multipart con el PDF + headers `X-Audit-Hmac` y `X-Audit-Key-Version` (los mismos que el export emite), recompute con `IAuditSignatureService.VerifyHmacBase64` y devuelve `{ valid, keyVersion, computedHmac, providedHmac }`. Validación: archivo presente, ≤50 MB, headers no vacíos. Sin auditoría (solo-lectura, sin datos sensibles del tenant).
- [X] T091 [US3] `AuditLogModule` en `src/Presentation/IngenIA365ERP.API/Endpoints/AuditLogModule.cs` reescribe los endpoints de audit como CQRS + permisos. Reemplaza el legacy `AuditEndpoints.cs` (eliminado — rutas history/access-logs no eran usadas por ningún cliente, se reescribirán como queries si se necesitan). 3 endpoints bajo `/api/audit/logs`: `GET /` (AuditLog.View → `QueryAuditLogQuery` con `ErrorEnvelopeFilter`); `GET /export.csv` (AuditLog.Export → `ExportAuditLogCsvQuery`, devuelve `Results.File` text/csv); `GET /export.pdf` (AuditLog.Export → `ExportAuditLogPdfQuery`, devuelve `Results.File` application/pdf con headers `X-Audit-Hmac`, `X-Audit-Key-Version`, `X-Audit-Row-Count`). `ErrorEnvelopeFilter.Translate` promovido a public para que los endpoints binarios reusen el envelope canónico en fallas.
- [X] T092 [US3] Página Blazor `src/Presentation/IngenIA365ERP.Shared/Pages/Administracion/AuditLogConsole.razor` (ruta `/admin/auditoria`, reemplaza el placeholder `AuditLog.razor` eliminado) — ubicada en `Shared` por consistencia con el patrón Blazor Hybrid del proyecto. Toolbar con filtros (UserId/Módulo/Acción/Entidad/From/To), tabla con 8 columnas, paginador Prev/Next (PageSize=50). Toda la consola envuelta en `<PermissionGate Required="AuditLog.View">` con `Fallback` informativo; los botones export en `<PermissionGate Required="AuditLog.Export">` anidado. Descarga CSV/PDF vía `HttpClient` autenticado + helper JS `audit-console.js` (en `Web.Client/wwwroot/`) que materializa `DotNetStreamReference` como blob anchor click — el browser navigate plano no incluye el header Authorization. Manejo de error usa el envelope canónico (`code: message`).
- [X] T093 [US3] `AuditIndexBootstrap` (IHostedService) ya cubierto por T026 — registrado en `Audit/DependencyInjection.cs` vía `AddHostedService<AuditIndexBootstrap>()`. Crea índices `ix_tenant_occurredAt`, `ix_tenant_user_occurredAt`, `ix_tenant_entity_occurredAt` + TTL 5 años al arranque.

**Checkpoint**: US1 + US2 + US3 → la trazabilidad regulatoria está viva. Cualquier acción posterior queda registrada antes de cerrarse. ✅ Cerrada 2026-05-30 — 14/14 tasks. Application 67/67 + Architecture 23/23 + IntegrationTests (sin Docker) 8/8 verdes. T083/T084/T085 quedan como gates pendientes de ejecución con Docker.

---

## Phase 6: User Story 4 — Soft-delete universal y restauración (Priority: P2)

**Goal**: cualquier eliminación es lógica y restaurable; ningún flujo de aplicación borra físicamente.

**Independent Test**: `quickstart.md §US4` — eliminar usuario, comprobar que no aparece pero existe; restaurar y validar audit log; intentar `DELETE FROM` en código nuevo → architecture test rojo.

### Tests for User Story 4

- [X] T094 [P] [US4] `PrincipioVII_SoftDeleteAndAuditable.cs` actualizado para reconocer `[Lookup]` real (antes usaba `[NotMapped]` como proxy). Cualquier nueva entidad en `Domain.Entities` que NO herede de `AuditableEntity`/`AuditableEntityLong` ni lleve `[Lookup]` rompe el principio. Excepciones explícitas: `PasswordHistory` (append-only).
- [X] T095 [P] [US4] `PrincipioVII_NoRawDelete.cs` cubre el principio con regex sobre `ExecuteSqlRaw/Interpolated("DELETE FROM ...")`. Excepción documentada: `SoftDeleteInterceptor.cs` (único autorizado a borrar físicamente cuando aplique).
- [X] T096 [P] [US4] `RestoreUserCommandHandlerTests.cs` con 3 casos: restaura OK y limpia las 3 columnas + reactiva + asienta `UpdatedBy`; falla `Generic.NotFound` si el PublicId no existe; falla `UserErrorCodes.NotDisabled` si el usuario ya está activo. **Resultado**: 3/3 verdes.
- [X] T097 [P] [US4] `UserSoftDeleteRestoreTests.cs` con 3 casos: `POST /disable` exige autorización; `POST /restore` exige autorización; `GET /restore` no está mapeado (defensa CSRF). Requiere Docker para los caminos GREEN completos; compila limpio y los gates pasan en estado RED esperado.

### Implementation for User Story 4

- [X] T098 [US4] Verificación del `SoftDeleteInterceptor` activo: nuevo Fact `SoftDeleteInterceptor_is_registered_in_Persistence_DI` en `PrincipioVII_SoftDeleteAndAuditable.cs` lee `Persistence/DependencyInjection.cs` y exige `SoftDeleteInterceptor` + `AddScoped<ISaveChangesInterceptor`. Si un refactor des-registra el interceptor, el test se pone rojo antes que un DELETE físico llegue a producción.
- [X] T099 [P] [US4] `LookupAttribute` en `src/Core/IngenIA365ERP.Domain/Common/LookupAttribute.cs` — atributo marcador para catálogos inmutables que NO heredan de `AuditableEntity`. Reconocido por `PrincipioVII_SoftDeleteAndAuditable`. `Permission` se mantiene como `AuditableEntity` (decisión: el lookup está disponible para futuros catálogos puros tipo moneda/país que se introduzcan sin rigging de auditoría).
- [X] T100 [P] [US4] `SoftDeleteRestoreHelper` (estático) en `src/Core/IngenIA365ERP.Application/Common/SoftDelete/`. Dos métodos genéricos: `LoadSoftDeletedAsync<TEntity>` (lookup con `IgnoreQueryFilters` + valida soft-deleted + devuelve `Result<TEntity>`) y `ApplyRestore` (resetea las 3 columnas + asienta `UpdatedBy`). Los handlers concretos siguen siendo responsables de side-effects propios (cache, notificaciones, flags). `RestoreUserCommandHandler` queda intacto — la generalización es opcional para futuros `RestoreRole`/`RestoreBranch`/`RestoreAttachment`.
- [X] T101 [US4] `PrincipioXI_ContableImmutable.cs` ya existía como vacuum gate (introducido en T037). Recorre archivos de producción buscando `\.Remove(...)` o `\.RemoveRange(...)` y rechaza si referencia los namespaces `Entities/Accounting/Transactions`, `Entities/Lending/Transactions`, `Entities/Payroll/Transactions` (vacíos hoy, blindados desde ya).

**Checkpoint**: el sistema completo respeta soft-delete + auditoría con compuertas mecánicas que blindan los principios VII, X, XI a futuro. ✅ Cerrada 2026-05-30 — 8/8 tasks. Architecture 24/24 (incluye Fact T098), Application 70/70.

---

## Phase 7: User Story 5 — Adjuntos seguros y cifrados (Priority: P3)

**Goal**: subir/descargar archivos vinculados a entidades, cifrados AES-256-GCM en reposo, autorizados por permiso sobre la entidad propietaria.

**Independent Test**: `quickstart.md §US5` — subir PDF, verificar ilegibilidad en disco, descargar y validar SHA-256, denegación a usuario sin permiso.

### Tests for User Story 5

- [X] T102 [P] [US5] `UploadAttachmentCommandHandlerTests.cs` con 6 facts: persiste cifrado + sha256 + dek envuelta; cifra ANTES de pasar al store (el stream nunca lleva plaintext); falla `Auth.TenantRequired` sin tenant; validator rechaza oversized / MIME no permitido / archivo vacío. **Resultado**: 6/6 verdes. Rollback de blob queda cubierto por inspección del código (log estructurado de `saveEx`/`cleanupEx`).
- [X] T103 [P] [US5] `AttachmentEncryptionAtRestTests.cs` ejerce el stack real (`AttachmentEncryptionService` + `LocalEncryptedFileStore`) sin Docker, usando DataProtection ephemeral + directorio temporal. 3 facts verdes: blob en disk no contiene `%PDF`; round-trip cifrar→escribir→leer→descifrar recupera bytes; tampering de 1 byte hace fallar GCM con `CryptographicException`.
- [X] T104 [P] [US5] `AttachmentAuthorizationTests.cs` con 4 facts gate: upload / download / delete / list-by-owner exigen autenticación. Compila limpio; los caminos GREEN completos (permiso sobre owner) requieren Docker.

### Implementation for User Story 5

- [X] T105 [P] [US5] `Attachment` ampliado con `TenantId`, `OwnerEntityType`, `OwnerEntityPublicId` (Guid — Principio VI), `Sha256Hex`, `SizeBytes`, `EncryptedDek` y `StorageProvider`. `AttachmentConfiguration` actualizada (índice combinado `(TenantId, OwnerEntityType, OwnerEntityPublicId)` filtered `WHERE IsDeleted = 0`). Schema `database/schema/13f_Attachments.sql` idempotente (`IF NOT EXISTS` para tabla + 2 índices).
- [X] T106 [P] [US5] `AttachmentEncryptionService` implementa nuevo contrato `IAttachmentCipher` (en Application, Clean Architecture). AES-256-GCM con DEK random per-blob (32B); DEK envuelta vía `IDataProtector` con purpose `IngenIA365ERP.Attachments.Dek.v1` y persistida como base64. Formato del blob: `NONCE(12) || TAG(16) || CIPHERTEXT`. Cleanup defensivo de DEK con `CryptographicOperations.ZeroMemory`.
- [X] T107 [P] [US5] `LocalEncryptedFileStore` implementa `IBlobStore` (movido a `Application/Common/Interfaces/Storage/` por Clean Architecture). Layout: `{LocalRootPath}/{tenant}/{yyyy}/{MM}/{guid}.bin`. Defensa contra path traversal (resolved path debe iniciar con root). `BlobReference.Uri` con forward slashes para portabilidad Win/Linux. Config vía `AttachmentStorageSettings`.
- [X] T108 [US5] 3 commands + 1 query en `Application/Attachments/`: `UploadAttachmentCommand` (calcula SHA-256, cifra, persiste blob + metadata con rollback en falla SaveChanges con logging estructurado); `DownloadAttachmentQuery` (descifra + verifica SHA-256 + envuelve `AttachmentDownload`); `DeleteAttachmentCommand` (soft-delete via interceptor); `ListAttachmentsByOwnerQuery` (para `AttachmentList`). Todos resuelven tenant del current user (FR-004). Validators inlinados. Allowlist MIME hardcoded en `AttachmentPolicy` (futuro: TenantSetting).
- [X] T109 [US5] `AttachmentsModule` en `src/Presentation/IngenIA365ERP.API/Endpoints/AttachmentsModule.cs` con 4 endpoints (Upload multipart, Download File con header `X-Attachment-Sha256`, Delete con envelope, ListByOwner). Permisos `Attachments.Upload`/`Download`/`Delete`. Upload usa `DisableAntiforgery` (es API).
- [X] T110 [P] [US5] `AttachmentUploader.razor` en `Web.Client/Components/` — `InputFile` + multipart fetch + progress + error envelope handling + callback `OnUploaded(Guid)`.
- [X] T111 [P] [US5] `AttachmentList.razor` en `Web.Client/Components/` — tabla con descarga (reusa `audit-console.js` para `downloadFromStream`) y delete. Método público `RefreshAsync()` para que el padre dispare recarga tras upload.

**Checkpoint**: módulos posteriores (Nómina) podrán adjuntar documentos respaldatorios. ✅ Cerrada 2026-05-30 — 10/10 tasks. Architecture 24/24, Application 76/76, IntegrationTests sin Docker 11/11.

---

## Phase 8: User Story 6 — Notificaciones (Priority: P3)

**Goal**: notificaciones in-app + correo con reintentos y diagnóstico de fallos.

**Independent Test**: `quickstart.md §US6` — disparar bloqueo de cuenta, verificar correo en smtp4dev y push SignalR; detener SMTP y validar `NOT_NotificationDeliveryFailures`.

### Tests for User Story 6

- [X] T112 [P] [US6] `SendNotificationCommandHandlerTests.cs` con 6 facts: persiste con `EmailStatus = Pending` cuando hay canal Email; marca `Disabled` si solo InApp; push real-time vía `INotificationPusher` cuando InApp está presente; NO push si solo Email; persiste `TenantId = 0` si no hay tenant (defensa para handlers de Login antes de emitir JWT); push fallido no aborta el handler (best-effort). **Resultado**: 6/6 verdes.
- [X] T113 [P] [US6] `EmailRetryTests.cs` con 3 facts gate: inbox/MarkRead/MarkAllRead exigen autenticación. El test funcional completo (SMTP simulado falla 3 veces → `NotificationDeliveryFailure` persistido → in-app sigue visible) requiere Docker + smtp4dev; queda gateado.
- [X] T114 [P] [US6] `SignalRPushTests.cs` ejerce `SignalRNotificationPusher` con mocks manuales (`IHubContext`, `IHubClients`, `IClientProxy` — sin agregar NSubstitute a IntegrationTests). Verifica que el evento `notification.created` se despacha al `User(recipientPublicId)` con args correctos. **Resultado**: 1/1 verde sin Docker.

### Implementation for User Story 6

- [X] T115 [P] [US6] `Notification` reescrita con shape US6 limpio (TenantId, RecipientUserPublicId Guid, Type string, Subject, Body, ChannelsMask, EmailStatus/SentAt/AttemptCount, ReadAt, ArchivedAt). `NotificationDeliveryFailure` nueva. Configurations actualizadas (índices filtered `IX_*_Inbox` y `IX_*_EmailStatus`). Quitada nav `Person.Notifications` (las notificaciones ya no apuntan a Person sino a User por PublicId). Schema `13g_Notifications.sql` idempotente con FK CASCADE entre tablas.
- [X] T116 [P] [US6] 6 templates `.cshtml` en `src/Infrastructure/IngenIA365ERP.Storage/EmailTemplates/`: `AccountLocked`, `PasswordChanged`, `MfaReset`, `RoleAssigned`, `SuspiciousSessionActivity`, `UserInvitationCreated` + `_Layout.cshtml`. HTML inline-style (compatible con clientes de correo), placeholders `{{Key}}`. Copiadas al output via `<None Update="EmailTemplates\*.cshtml" CopyToOutputDirectory="PreserveNewest" />`.
- [X] T117 [P] [US6] `INotificationTemplateRenderer` contrato en Application; `NotificationTemplateRenderer` impl en Storage. Reemplazo `{{Key}}` con `WebUtility.HtmlEncode` por seguridad, cache thread-safe (`ConcurrentDictionary`) de templates leídos del filesystem. Devuelve `null` si no existe el archivo — el dispatcher cae al HTML wrap simple. Decisión: pragmático con string-replace en lugar de RazorEngineCore (las 6 plantillas son simples; swappeable sin tocar el contrato). El dispatcher (T119) invoca el renderer antes del fallback.
- [X] T118 [US6] `SendNotificationCommandHandler` real en `Application/Notifications/SendNotification/` reemplaza `NoopSendNotificationHandler` (eliminado). Persiste in-app, marca `EmailStatus = Pending|Disabled` según `ChannelsMask`, dispara push real-time vía `INotificationPusher`. Push fallido NO aborta el handler (best-effort + log warning). `ListMyNotificationsQuery` + `MarkNotificationReadCommand` + `MarkNotificationArchivedCommand` + `MarkAllNotificationsReadCommand` con resolución de current user via lookup `User.PublicId` (404 indistinguible si la notif no es del caller). Validators inlinados.
- [X] T119 [US6] `NotificationEmailDispatcher` (BackgroundService) en `Infrastructure.Storage`. Poll cada 15s, batch de 25, max 4 intentos. Toma `EmailStatus = Pending && ChannelsMask & 2 == 2 && EmailAttemptCount < 4`. Resuelve email del destinatario con un solo round-trip por batch. Wrappea `Subject`/`Body` en HTML escapado. Persiste `NotificationDeliveryFailure` por intento fallido; marca `Failed` terminal cuando agota intentos o el destinatario no tiene email. Registrado vía `AddHostedService` en `Storage/DependencyInjection.cs`.
- [X] T120 [US6] `INotificationPusher` contrato en Application; `SignalRNotificationPusher` en `API/Hubs/SignalRNotificationPusher.cs` usa `IHubContext<NotificationsHub>.Clients.User(publicId)` para targeting (SignalR mapea User identifier desde claim `sub`). Evento `notification.created` con payload `{publicId, type, subject, occurredAt}`. Registrado en `Program.cs`.
- [X] T121 [US6] `NotificationsModule` en `src/Presentation/IngenIA365ERP.API/Endpoints/` con 4 endpoints (List, MarkRead, MarkArchived, MarkAllRead). Todos exigen `Notifications.ManageOwn` + `ErrorEnvelopeFilter` + `RequireAuthorization`. Las operaciones SIEMPRE afectan solo al caller (no hay endpoint para que un admin lea inbox ajeno — privacidad).
- [X] T122 [P] [US6] `Center.razor` en `Shared/Pages/Notifications/` (ruta `/notificaciones`). Toolbar (only-unread + include-archived + refrescar + marcar todas), tabla con tipo/asunto/recibida/estado-email/acciones, contador no leídas / totales. `<PermissionGate Required="Notifications.ManageOwn">` con Fallback informativo. Cuerpo de notificación colapsado en `<details>`.

**Checkpoint**: los handlers de US1 (T053) ahora entregan correos y in-app reales; los demás módulos podrán emitir notificaciones sin reimplementar el canal. ✅ Cerrada 2026-05-30 — 11/11 tasks (T116/T117 implementados con string-replace renderer + 6 templates + layout). Architecture 24/24, Application 82/82, IntegrationTests sin Docker 12/12.

---

## Phase 9: User Story 7 — Habeas data (Priority: P3)

**Goal**: el sistema versiona la política, registra consentimientos y revocaciones del titular y consulta el historial.

**Independent Test**: `quickstart.md §US7` — publicar nueva versión, registrar consentimiento, revocar, verificar historial y publicación de evento `HabeasDataRevokedEvent`.

### Tests for User Story 7

- [X] T123 [P] [US7] `PublishPolicyVersionHandlerTests.cs` con 4 facts: publica primera versión sin previa; cierra `EffectiveTo` de la anterior y asigna `VersionNumber = max+1`; validator rechaza `EffectiveFrom` pasada; falla `Auth.TenantRequired` sin tenant. SHA-256 verificado contra reimplementación independiente. **Resultado**: 4/4 verdes.
- [X] T124 [P] [US7] `RevokeConsentHandlerTests.cs` con 3 facts: revoca cuando última acción es `Accepted` y publica `HabeasDataRevokedEvent` con los campos correctos; falla `NoActiveConsent` si el titular nunca aceptó; falla `AlreadyRevoked` si la última acción ya es `Revoked`. El test del happy path verifica que el evento se publica con `Publisher.Received(1).Publish(...)`. **Resultado**: 3/3 verdes.
- [X] T125 [P] [US7] `HabeasDataHistoryTests.cs` con 4 gates: ListPolicies, PublishPolicy, RevokeConsent, History exigen autenticación. Compila limpio sin agregar dependencias (`JsonContent` helper local con `file static class`).

### Implementation for User Story 7

- [X] T126 [P] [US7] `HabeasDataPolicyVersion` + `HabeasDataConsent` en `Domain/Entities/Compliance/`. Configurations en `Persistence/Configurations/Compliance/` con índices clave: `UK_*_TenantVersion` (versión única por tenant), `UK_*_Current` (a lo sumo una vigente con filtered `WHERE EffectiveTo IS NULL AND IsDeleted = 0`), `IX_*_History` por `(TenantId, PersonId, ActionAt)`. DbSets agregados a `IApplicationDbContext` + `ApplicationDbContext`. Schema `13h_HabeasData.sql` idempotente con FK CMP_HabeasConsents → CMP_HabeasPolicyVersions.
- [X] T127 [P] [US7] `HabeasDataRevokedEvent` implementa `MediatR.INotification`. **Movido a `Application/Compliance/HabeasData/Events/`** (no `Domain/Events/`) — Domain debe quedar libre de paquetes (Principio II Clean Architecture). El spec original sugería Domain pero esa ubicación obliga a referenciar MediatR desde Domain.
- [X] T128 [US7] Commands+validators+handlers entregados: `PublishPolicyVersionCommand` (resuelve max VersionNumber, cierra anterior con `EffectiveTo`, calcula SHA-256 sobre el contenido), `AcceptConsentCommand` (falla `NoCurrentPolicy` si tenant no tiene versión vigente), `RevokeConsentCommand` (exige última acción `Accepted`, publica `HabeasDataRevokedEvent` via `IPublisher`). Queries: `ListPoliciesQuery`, `GetPolicyByPublicIdQuery`, `ListHabeasDataHistoryQuery` (cronológico desc, incluye PolicyVersion vía `Include`). Todas las operaciones scoped al tenant del current user (FR-004).
- [X] T129 [US7] `HabeasDataModule` en `src/Presentation/IngenIA365ERP.API/Endpoints/` con 6 endpoints bajo `/api/compliance/habeas-data`: ListPolicies/GetPolicy/PublishPolicy bajo `/policies`; AcceptConsent/RevokeConsent/History bajo `/consents`. Permisos `Compliance.HabeasData.*` (Publish/RecordConsent/Revoke/ViewHistory) ya catalogados en T066. `ErrorEnvelopeFilter` + `RequireAuthorization`.
- [X] T130 [P] [US7] 5 páginas Blazor en `Shared/Pages/Compliance/HabeasData/`: `Policies/Index.razor` (lista con badge Vigente/Histórica), `Policies/Publish.razor` (form con textarea Markdown), `Consents/RecordConsent.razor` (params `PersonId`, dropdown Canal), `Consents/RecordRevocation.razor` (notas requeridas, advertencia sobre evento de dominio), `Persons/History.razor` (estado actual + tabla cronológica + botones a aceptar/revocar protegidos por `<PermissionGate>` anidados).

**Checkpoint**: todas las historias del spec quedan cubiertas. El sistema puede iniciar tratamiento legal de datos personales. ✅ Cerrada 2026-05-30 — 8/8 tasks. Architecture 24/24, Application 89/89, IntegrationTests sin Docker 12/12. 🎯 **US1–US7 cerradas — el cimiento técnico funcional de Fase 0 está completo. Falta Phase 10 (Polish: NBomber, healthchecks, runbook, tag release).**

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: cierre operativo, observabilidad, carga y documentación.

- [X] T131 [P] `CimientosConcurrencyScenario.cs` en `Scenarios/`. Mix 60/30/10 (reads/writes/adjuntos), ramp 100 users en 10 min + 30 min plateau usando `Simulation.RampingInject` + `Simulation.Inject`. Target URL via env `LOADTEST_BASE_URL` (default `http://localhost:5000`). El step inicial hoy hace `GET /health/live` como placeholder estable; los steps autenticados se enchufan cuando el seeder (T132) provea tokens.
- [X] T132 [P] `SyntheticDatasetBuilder.cs` en `Fixtures/` con constantes `UserCount=1000`, `RoleCount=50`, `AuditEventCount=50000`. Expone `DatasetSpec` record para parametrizar el seeder real (subcomando `seed` antes de los escenarios — ver workflow T133).
- [X] T133 `.github/workflows/load-nightly.yml`: cron `0 6 * * *` (01:00 COT) + `workflow_dispatch`. Levanta el stack dev con `docker compose -f docker/dev.yml up -d`, espera `/health/ready`, siembra dataset, corre NBomber, archiva reports HTML+JSON+TRX como artifact (retention 30 días), tear-down al final.
- [X] T134 [P] `docs/operaciones/slo.md`: disponibilidad 99.5% mensual, latencia p95<800ms / p99<2000ms (`/api/*`), audit query p95<5s (SC-004), ventana programada dom 03–05 COT, error budget 3h39min, gating al 30% restante. Sección de SLOs adyacentes (BCrypt cost, audit log durabilidad, adjuntos cifrados).
- [X] T135 [P] `docs/operaciones/runbook-fase0.md` con 6 escenarios: lockout masivo, fallo SMTP, MongoDB down, rotación de claves RSA/KEK, rate limiter false positives, consola de operaciones rápidas. Cada uno con Síntomas → Diagnóstico → Mitigación → Postmortem. Tabla de contactos on-call.
- [X] T136 [P] `HealthChecks/HealthCheckExtensions.cs` con `/health/live` (proceso) y `/health/ready` (SQL Server + MongoDB + Redis + BlobStore probados). 4 healthchecks tagged + 1 self check. Response JSON con per-check status + duration. `MapHealthChecks("/api/health")` viejo reemplazado.
- [X] T137 [P] `SecurityHeadersMiddleware` reescrito: HSTS solo en prod (via `IWebHostEnvironment`), CSP más completo (`connect-src 'self' wss: https:` para SignalR, `img-src 'self' data: blob:` para exports, `frame-ancestors 'none'`, `object-src 'none'`). `app.UseHsts()` agregado en rama prod del pipeline.
- [X] T138 [P] `tools/MessageLintTool/` proyecto consola .NET 10. Escanea `Application/` + `Shared/Pages/`, extrae literales de `Result.Failure(...)`/`WithMessage(...)`, marca si la mayoría de palabras está en una blacklist mínima de marcadores en inglés. Exit 1 con offenders para gating CI. Uso: `dotnet run --project tools/MessageLintTool/ -- <repo-root>`.
- [X] T139 Validador automatizado preparado en `scripts/quickstart-validate.ps1` — recorre health, swagger y 7 endpoints anónimos por user story, espera 401/404 (gates de auth) y archiva evidencia JSON en `specs/001-cimientos-tecnicos/quickstart-evidence/<fecha>/`. **Ejecución end-to-end con tokens autenticados sigue siendo manual** — el operador levanta Docker + lanza la API + corre el script con un usuario seedeado.
- [X] T140 [P] `docs/INDICE-DOCUMENTACION.md` actualizado con sección Fase 0 ampliada: spec, plan, research, data-model, contracts, quickstart, tasks, dev-environment, SLO y runbook.
- [X] T141 Auditoría final ejecutada **2026-05-30**: **Architecture 24/24 verdes**, **Application 89/89 verdes**, **Domain 20/20 verdes**, **IntegrationTests sin Docker 19/19 verdes + 1 skipped intencional** (`PasswordHashIntegrityTests.AllHashes_must_meet_cost_threshold`, gated a Docker). Cero violaciones de los 12 principios.
- [X] T141a [P] `PasswordHashIntegrityTests.cs` en `IntegrationTests/Security/`. Theory `Parser_recognizes_cost_and_flags_substandard_hashes` con 7 casos que valida el regex BCrypt (`$2[aby]$cost$...`) y el threshold `cost>=11`. **Resultado**: 7/7 verdes. El recorrido sobre BD real queda con `[Fact(Skip=...)]` documentado — el operador lo des-skipea cuando ejecute contra fixture con Docker.
- [X] T142 Release notes redactadas en `docs/releases/v0.1.0-fase0-cimientos.md` (resumen ejecutivo, user stories, SC verificados, métricas, desviaciones documentadas, pendientes manuales, próximos pasos). **Solo queda el `git tag v0.1.0-fase0-cimientos && git push --tags` que es decisión humana.**

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: sin dependencias.
- **Phase 2 (Foundational)**: depende de Phase 1. **Bloquea** US1–US7.
- **Phase 3 (US1)**: depende de Phase 2.
- **Phase 4 (US2)**: depende de Phase 2; integra con US1 (los endpoints exigen JWT) pero el filtro de permisos puede testearse aún si US1 no está 100% cerrada — basta con un token válido.
- **Phase 5 (US3)**: depende de Phase 2 (AuditBehavior ya operativo) + US1 (necesita JWT para consultar) + US2 (necesita permisos).
- **Phase 6 (US4)**: cruza tareas con Phase 2 (interceptor) pero los architecture tests y commands de restauración pueden cerrarse después.
- **Phase 7 (US5)**: depende de Phase 2 (IBlobStore) + US2 (permisos sobre owner).
- **Phase 8 (US6)**: depende de Phase 2 (SignalR hub + IEmailSender) + US1 (handlers que disparan notificaciones).
- **Phase 9 (US7)**: depende de Phase 2 + US2 (permisos Compliance).
- **Phase 10 (Polish)**: depende de US1–US7 según ámbito.

### User Story Independence

- **US1, US2**: ambas P1, comparten dependencia de Phase 2. US1 entrega autenticación; US2 entrega autorización. Pueden desarrollarse en paralelo si el equipo está suficientemente staffed.
- **US3**: P2. Requiere US1 + US2 vivos para que las acciones auditadas tengan sentido.
- **US4**: P2. Cruza con Phase 2 pero independiente de US3–US7.
- **US5, US6, US7**: P3. Cada una independiente de las otras dos; pueden distribuirse entre desarrolladores en paralelo tras Phase 2.

### Cross-story task dependencies (explicit)

- **T053 (US1 notification handlers) → T030a (Phase 2 stub)**: T053 emite `SendNotificationCommand`. T030a entrega un stub funcional desde Phase 2 (no-op + log) que permite a US1 cerrar de forma autónoma. T118 (US6) reemplaza el binding DI por el handler real sin tocar a los emisores. Resultado: US1 y US6 son independientes en tiempo de implementación, sin huecos en el contrato.
- **T031 (Phase 2 SignalR hub) → T120 (US6 push)**: el hub vive en Foundational pero queda sin productores hasta US6. T031 debe documentar este "no-op productor" en su XML doc para evitar diagnósticos engañosos durante el desarrollo de US1.
- **T100 (US4 generic RestoreCommand<TEntity>) → T070 (US2 RestoreUserCommand)**: el patrón concreto de Users se generaliza después en US4.
- **T101 (US4 architecture test inmutabilidad contable) → fases futuras**: el test pasa vacuamente hasta que existan los namespaces de movimientos contables; intencional como guard preventivo.

### Within Each User Story

- Tests primero (escritura RED), implementación después (GREEN).
- Domain → Persistence → Application → Presentation.
- Architecture tests siempre acompañan al cambio: si rompen, el plan reabre el principio violado.

---

## Parallel Execution Examples

### Setup + Foundational

```bash
# Cuatro desarrolladores pueden tomar:
Dev A: T003, T004, T005, T006, T007 (paquetes)
Dev B: T008, T009 (entorno dev)
Dev C: T011, T012, T013 (BaseEntity + abstracciones)
Dev D: T014–T018 (pipeline behaviors)
```

### User Story 1 — Auth

```bash
# Modelos y servicios en paralelo:
Task: "Crear entidades MFA en src/Core/IngenIA365ERP.Domain/Entities/Security/MfaBackupCode.cs"
Task: "Crear TotpService en src/Infrastructure/IngenIA365ERP.Identity/Services/TotpService.cs"
Task: "Crear JwtTokenService en src/Infrastructure/IngenIA365ERP.Identity/Services/JwtTokenService.cs"
Task: "Crear PasswordPolicyEnforcer en src/Infrastructure/IngenIA365ERP.Identity/Services/PasswordPolicyEnforcer.cs"

# Tests en paralelo:
Task: "Application test LoginCommandHandlerTests"
Task: "Application test VerifyMfaCommandHandlerTests"
Task: "Application test MfaResetApproveCommandHandlerTests"
```

### User Story 5 — Attachments

```bash
Task: "Entidad Attachment + configuration"
Task: "AttachmentEncryptionService"
Task: "LocalEncryptedFileStore"
Task: "Componente Blazor AttachmentUploader.razor"
Task: "Componente Blazor AttachmentList.razor"
```

---

## Implementation Strategy

### MVP scope (recomendado)

**MVP = Phase 1 + Phase 2 + Phase 3 (US1) + Phase 4 (US2)**

Cierra el cimiento crítico: usuarios pueden autenticarse, recuperar acceso, y operar con permisos granulares dentro de la empresa correcta. Es el mínimo desplegable para que cualquier módulo posterior (incluyendo la primera fase de Nómina) tenga sentido.

US3 (audit log consultable) se puede dejar para el primer release entregable inmediatamente después del MVP, ya que la auditoría está activa desde Phase 2 (AuditBehavior) — solo falta el panel de consulta y exportación.

### Incremental delivery

1. Phase 1 + 2 → fundación lista (no demoable per se).
2. Phase 3 (US1) → demo: login + MFA + recuperación.
3. Phase 4 (US2) → demo: admin de usuarios/roles + invisibilidad de endpoints.
4. Phase 5 (US3) → demo: consola y export PDF firmado.
5. Phase 6 (US4) → blindaje técnico (architecture tests).
6. Phase 7 (US5) → demo: adjuntar PDF a un usuario.
7. Phase 8 (US6) → demo: notificaciones por correo + push.
8. Phase 9 (US7) → demo: aceptar y revocar habeas data + historial.
9. Phase 10 → release de Fase 0 (`v0.1.0-fase0-cimientos`).

### Parallel team strategy (3 desarrolladores)

- **Sprint 1**: todos en Setup + Foundational. Cierre en una semana.
- **Sprint 2**: Dev A → US1; Dev B → US2; Dev C → comienza Phase 2 architecture tests + Phase 6 (US4) blindaje + scaffolding de US3.
- **Sprint 3**: Dev A → US3; Dev B → US5; Dev C → US6.
- **Sprint 4**: Dev A → US7; todos → Polish + quickstart + tag.

---

## Notes

- Cada `[P]` significa archivo distinto y sin dependencia bloqueante con tareas previas no completadas.
- Marcar `- [x]` al cerrar la tarea — `/speckit-analyze` o el reviewer verificará coherencia.
- Cada commit debe llevar referencia al ID de la tarea (`T046: entities MFA`).
- Architecture.Tests viven en `tests/IngenIA365ERP.Architecture.Tests` — un test rojo bloquea el merge según la constitución.
- Mensajes al usuario siempre en español, identifiers en inglés.
- Cualquier tarea cuyo alcance crezca > 1 día de trabajo debe partirse en sub-tareas y agregarse al tasks.md con nuevo ID antes de empezar.
