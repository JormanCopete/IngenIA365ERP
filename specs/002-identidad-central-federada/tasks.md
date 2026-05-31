---

description: "Task list for Identidad Central con Autorización Federada por Empresa"
---

# Tasks: Identidad Central con Autorización Federada por Empresa

**Input**: Design documents from `/specs/002-identidad-central-federada/`

**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Tests**: incluidos como parte natural del flujo (la constitución del proyecto exige pruebas en cada PR significativo; este es uno de ellos).

**Organization**: tareas agrupadas por user story (P1 → P3) para permitir implementación incremental y entrega MVP tras US1+US2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede correr en paralelo (archivos distintos, sin dependencias en tareas no completadas)
- **[Story]**: a qué user story pertenece (US1, US2, US3, US4, US5)
- Cada descripción incluye la ruta exacta del archivo

## Path Conventions

Layout del repositorio (existente — ver `plan.md > Project Structure`):

- Backend Domain: `src/Core/IngenIA365ERP.Domain/`
- Backend Application: `src/Core/IngenIA365ERP.Application/`
- Backend Infrastructure: `src/Infrastructure/IngenIA365ERP.{Identity,Persistence,Caching,Storage}/`
- Backend API: `src/Presentation/IngenIA365ERP.API/`
- Frontend Blazor: `src/Presentation/IngenIA365ERP.Web/` (server-side) y `src/Presentation/IngenIA365ERP.Web.Client/` (WASM compartido)
- Tests: `tests/IngenIA365ERP.{Domain,Application,API.Integration,Architecture,Load}.Tests/`
- DDL SQL: `database/schema/`
- Migration SQL: `database/migration/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dependencias NuGet, configuración local y servicios auxiliares para que el resto del trabajo arranque.

- [X] T001 Añadir paquetes NuGet `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.x y `Microsoft.AspNetCore.Identity` 10.x al proyecto `src/Infrastructure/IngenIA365ERP.Identity/IngenIA365ERP.Identity.csproj`
- [X] T002 [P] Añadir servicio MailHog al `docker-compose.dev.yml` (puerto SMTP 1025, UI 8025) para validación local de correos de invitación
- [X] T003 [P] Extender `src/Presentation/IngenIA365ERP.API/appsettings.json` y `appsettings.Development.json` con secciones `CentralIdentity` (issuer/audience/lifetime/keys), `EmailSender:Smtp` (host/port/from/credentials), `PwnedPassword` (base url, timeout, enable flag) y `LoginLockout` (umbrales 5/10/15/20 + durations)
- [X] T004 [P] Documentar las variables de entorno requeridas (`MASTER_ADMIN_EMAIL`, `MASTER_ADMIN_PASSWORD`, `EMAIL_SENDER_*`, `PWNED_PASSWORD_BASE_URL`) en `README.md` sección "Configuración v2 (Identidad Central)"

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: cimientos de Domain, Persistence, Identity y middleware. Refactoriza `TenantResolutionMiddleware` para que lea `active_tenant_id` del JWT — sin esto ningún endpoint nuevo funciona. **Ninguna user story puede comenzar hasta completar esta fase.**

### Domain entities + value objects

- [X] T005 Crear entidad `CentralUser` POCO en `src/Core/IngenIA365ERP.Domain/Entities/Admin/CentralUser.cs` con atributos según `data-model.md §1` (Id Guid, Email, NormalizedEmail, PasswordHash, SecurityStamp, ConcurrencyStamp, TwoFactorEnabled, MfaSecret, LockoutEnd, DefaultTenantId, IsGlobalMasterAdmin, Status, CreatedAt, LastLoginAt) hereda `AuditableEntity`
- [X] T006 [P] Crear entidad `TenantMembership` con máquina de estados (métodos `Activate`, `Suspend`, `Reactivate`, `Revoke`, `PromoteToAdmin`, `DemoteFromAdmin` que validan transiciones FR-009a) en `src/Core/IngenIA365ERP.Domain/Entities/Admin/TenantMembership.cs`
- [X] T007 [P] Crear entidad `Invitation` con máquina de estados (`MarkAccepted`, `MarkExpired`, `Revoke`, `IsValid()`) en `src/Core/IngenIA365ERP.Domain/Entities/Admin/Invitation.cs`
- [X] T008 [P] Crear entidad `TenantMfaPolicy` con métodos `Enable`/`Disable` (audit fields) en `src/Core/IngenIA365ERP.Domain/Entities/Admin/TenantMfaPolicy.cs`
- [X] T009 [P] Crear entidad append-only `CentralUserLoginAttempt` (sin AuditableEntity) en `src/Core/IngenIA365ERP.Domain/Entities/Admin/CentralUserLoginAttempt.cs`
- [X] T010 [P] Crear value objects `Email` (normalización ToUpperInvariant), `InvitationToken` (Base64Url + SHA-256 hash), `MembershipStatus` (enum + transition validator) en `src/Core/IngenIA365ERP.Domain/ValueObjects/`

### Tests de Domain (state machines)

- [X] T011 [P] Tests xUnit para `TenantMembership` state machine en `tests/IngenIA365ERP.Domain.Tests/Admin/TenantMembershipStateMachineTests.cs` (cubrir todas las transiciones FR-009a + rechazos)
- [X] T012 [P] Tests xUnit para `Invitation` state machine en `tests/IngenIA365ERP.Domain.Tests/Admin/InvitationStateMachineTests.cs` (Pending→Accepted/Revoked/Expired y rechazos)
- [X] T013 [P] Tests xUnit para `CentralUser` invariantes en `tests/IngenIA365ERP.Domain.Tests/Admin/CentralUserTests.cs` (Status default Active, password hash no vacío)

### DDL SQL Server

- [X] T014 Crear `database/schema/15a_Admin_CentralIdentity.sql` con tablas `ADM_CentralUsers` + tablas estándar de ASP.NET Identity (`ADM_CentralUserClaims`, `ADM_CentralUserLogins`, `ADM_CentralUserRoles`, `ADM_CentralUserTokens`, `ADM_Roles`, `ADM_RoleClaims`) idempotente con header de migración (contexto + problema + reversibilidad)
- [X] T015 [P] Crear `database/schema/15b_Admin_Memberships_Invitations.sql` con `ADM_TenantMemberships` (índice único `(CentralUserId, TenantId)`, filtrado por `IsTenantAdmin = 1`) y `ADM_Invitations` (UNIQUE TokenHash, índices por NormalizedEmail+TenantId+Status)
- [X] T016 [P] Crear `database/schema/15c_Admin_MfaPolicy_LoginAttempts.sql` con `ADM_TenantMfaPolicies` (UNIQUE TenantId) y `ADM_CentralUserLoginAttempts` (append-only, índice NormalizedEmail+Timestamp)
- [ ] T017 Crear `database/schema/15d_Tenant_Users_Refactor.sql` con DROP de columnas obsoletas de `SEC_Users` (`PasswordHash`, `PasswordSalt`, `MfaSecret`, `IsEmailVerified`, `FailedLoginAttempts`, `LockoutEndAt`, `MustChangePassword`, `LastPasswordChangeAt`, `LegacyLogin`, `IsSaasOperator`) y ADD `CentralUserId UNIQUEIDENTIFIER NOT NULL UNIQUE` + `CentralUserPublicEmail NVARCHAR(256) NULL`. **Header con warning "REQUIERE BD VIRGEN — irreversible sin restore"**
- [X] T018 [P] Crear `database/migration/16_Seed_Default_GlobalMasterAdmin.sql` idempotente: si `ADM_CentralUsers` está vacía, inserta un master admin con email/password tomados de variables de entorno y flag `IsGlobalMasterAdmin = 1`, `EmailConfirmed = 1`, `MustChangePassword = 0`; el hash BCrypt cost 11 se calcula offline y se inyecta vía script preprocesador (documentado en header)

### EF Core Configurations

- [X] T019 Crear `src/Infrastructure/IngenIA365ERP.Persistence/Contexts/AdminDbContext.cs` que herede `IdentityDbContext<CentralUserIdentity, IdentityRole<Guid>, Guid>` y exponga DbSets `TenantMemberships`, `Invitations`, `TenantMfaPolicies`, `CentralUserLoginAttempts`. NO tenant-aware (no implementa `IMultiTenantDbContext`)
- [X] T020 [P] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Configuration/Admin/CentralUserConfiguration.cs` (mapeo Domain `CentralUser` → tabla `ADM_CentralUsers`, índices, conversión cifrada de `MfaSecret` con `IDataProtectionProvider`)
- [X] T021 [P] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Configuration/Admin/TenantMembershipConfiguration.cs` (UNIQUE `(CentralUserId, TenantId)`, índice filtrado `WHERE IsTenantAdmin = 1`, `HasQueryFilter(e => !e.IsDeleted)`)
- [X] T022 [P] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Configuration/Admin/InvitationConfiguration.cs` (UNIQUE `TokenHash`, conversión value object `InvitationToken`)
- [X] T023 [P] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Configuration/Admin/TenantMfaPolicyConfiguration.cs`
- [X] T024 [P] Crear `src/Infrastructure/IngenIA365ERP.Persistence/Configuration/Admin/CentralUserLoginAttemptConfiguration.cs` (sin soft-delete; PK bigint)
- [ ] T025 Modificar `src/Infrastructure/IngenIA365ERP.Persistence/Configuration/Security/UserConfiguration.cs` (per-tenant `SEC_Users`): añadir mapeo de `CentralUserId` UNIQUE, retirar mapeos de columnas eliminadas en T017
- [X] T026 Registrar `AdminDbContext` con la connection string `IngenIA365ERP_Admin` en `src/Infrastructure/IngenIA365ERP.Persistence/DependencyInjection.cs` (ya existía de Fase 0 — verificado, refactor a IdentityDbContext es transparente al DI)
- [ ] T027 Generar migración EF Core inicial para `AdminDbContext` con `dotnet ef migrations add InitialCentralIdentity --context AdminDbContext --output-dir Migrations/Admin` y validar SQL generado contra T014-T017

### Application abstractions

- [X] T028 Crear interfaz `ICentralIdentityProvider` en `src/Core/IngenIA365ERP.Application/Common/Interfaces/Identity/ICentralIdentityProvider.cs` con métodos: `ValidatePasswordAsync`, `CreateUserAsync`, `ChangePasswordAsync`, `BeginMfaEnrollmentAsync`, `ConfirmMfaSetupAsync`, `VerifyMfaCodeAsync`, `DisableMfaAsync`, `ResetMfaAsync`, `FindByEmailAsync`, `FindByIdAsync`, `AdminResetPasswordAsync`, `RecordSuccessfulLoginAsync`. (Convención del proyecto: interfaces en `Common/Interfaces/`, no `Common/Abstractions/`)
- [X] T029 [P] Crear interfaz `IEmailSender` en `src/Core/IngenIA365ERP.Application/Common/Interfaces/Notifications/IEmailSender.cs` con método `SendAsync(EmailMessage)` y record `EmailMessage(To, Subject, HtmlBody, PlainTextBody)`. **YA EXISTÍA de Fase 0** — verificado, interfaz compatible con la spec.
- [X] T030 [P] Crear interfaz `IPwnedPasswordService` en `src/Core/IngenIA365ERP.Application/Common/Interfaces/Identity/IPwnedPasswordService.cs` con método `IsPwnedAsync(string password, CancellationToken) → PwnedPasswordResult { IsPwned, ServiceAvailable }`
- [X] T031 [P] Crear interfaz `ITenantMembershipReader` en `src/Core/IngenIA365ERP.Application/Common/Interfaces/Identity/ITenantMembershipReader.cs` con métodos `GetActiveMembershipsAsync(Guid centralUserId)`, `IsMemberOfTenantAsync(Guid centralUserId, Guid tenantId)`, `InvalidateLocalCacheAsync(Guid centralUserId)`
- [X] T032 [P] Crear constantes de `AuditEventTypes` para los 12+ nuevos eventos (`CentralUser.Login.{Success,Failed,Locked}`, `CentralUser.Mfa.{Success,Failed,ResetByMaster}`, `Invitation.{Issued,Accepted,Expired,Revoked,Superseded}`, `Membership.{Activated,Suspended,Revoked,PromotedToAdmin,DemotedFromAdmin,LastAdminProtected}`, `Session.{TenantSelected,TenantSwitched}`, `TenantMfaPolicy.{Activated,Deactivated}`, `Profile.{DefaultTenantChanged,PasswordChanged,MfaEnrolled,MfaDisabled,PasswordResetRequested,PasswordResetConsumed}`, `Tenant.Created`) en `src/Core/IngenIA365ERP.Application/Common/Audit/AuditEventTypes.cs`

### Infrastructure implementations

- [ ] T033 Crear `src/Infrastructure/IngenIA365ERP.Identity/Models/CentralUserIdentity.cs` heredando `IdentityUser<Guid>` con campos custom (DefaultTenantId, IsGlobalMasterAdmin, Status, CreatedAt, LastLoginAt) y método `ToDomain()` que produce un `CentralUser` POCO de Domain
- [ ] T034 Crear `src/Infrastructure/IngenIA365ERP.Identity/Stores/CentralUserStore.cs` implementando `IUserStore<CentralUserIdentity>` que persiste contra `AdminDbContext.CentralUsers`
- [X] T035 [P] Crear `src/Infrastructure/IngenIA365ERP.Identity/CentralIdentity/BcryptPasswordHasher.cs` implementando `IPasswordHasher<CentralUserIdentity>` con BCrypt.Net-Next cost 11 (soporta detección de hashes con cost inferior + rehash on verify)
- [X] T036 [P] Crear `src/Infrastructure/IngenIA365ERP.Identity/CentralIdentity/PwnedPasswordService.cs` con `HttpClient` named `pwned-passwords`, k-anonymity (SHA-1 → primeros 5 chars → GET `https://api.pwnedpasswords.com/range/{prefix}` → comparar sufijos), timeout 2s, fail-open con Warning loggeado
- [X] T037 [P] Crear `src/Infrastructure/IngenIA365ERP.Identity/CentralIdentity/CentralJwtIssuer.cs` que emita JWT RS256 con claims `sub`, `email`, `is_global_master_admin`, `active_tenant_id` (opcional), `tenant_admin` (opcional), `mfa_verified`, `purpose` (`full` | `mfa-verify` | `mfa-enroll` | `tenant-select` | `password-reset`), `iat`, `exp`, `nbf`, `jti`. Métodos: `IssueAccessToken`, `IssueChallengeToken`, `IssueRefreshToken`.
- [X] T038 [P] Crear `RedisTenantMembershipReader` en `src/Infrastructure/IngenIA365ERP.Caching/Services/Identity/RedisTenantMembershipReader.cs` implementando `ITenantMembershipReader` con cache Redis + fallback a `IAdminDbContext` (JOIN con Tenants + MfaPolicies). **Movido a Caching** para co-localizar la lógica de cache; Caching ahora referencia `Microsoft.EntityFrameworkCore` (abstractions) para el JOIN.
- [X] T039 [P] Funcionalidad de cache integrada en `RedisTenantMembershipReader` (key `memberships:{centralUserId:N}`, TTL 60s, JSON serializer, suscriptor pub/sub estático `StartSubscriptionAsync` invocado desde `AddCachingServices`).
- [X] T039a [P] Crear abstracción `IMembershipChangedNotifier` + impl `RedisMembershipChangedNotifier` en Caching. Publica payload `central:{guid}` en canal `membership-changed`. `PublishForTenantMembersAsync` resuelve los miembros activos del tenant y publica uno por cada.
- [X] T040 [P] Crear `src/Infrastructure/IngenIA365ERP.Caching/Services/Identity/RedisLoginAttemptCounter.cs` con escalado progresivo según `research.md > D-11`. Locks por `login-locked:{email}` con TTL escalado. INCR atómico con TTL 24h en `login-attempts:{email}`.
- [X] T041 Crear `src/Infrastructure/IngenIA365ERP.Identity/CentralIdentity/AspNetCoreIdentityProvider.cs` implementando `ICentralIdentityProvider` usando `UserManager<CentralUserIdentity>`. Inyecta `IPwnedPasswordService` en `CreateUserAsync` / `ChangePasswordAsync` / `AdminResetPasswordAsync`. MFA secret cifrado con `IDataProtectionProvider` (purpose `central-identity:mfa-secret`). Recovery codes vía `UserManager.GenerateNewTwoFactorRecoveryCodesAsync`. + `AddCentralIdentity()` DI extension lista (NO wired en Program.cs todavía).
- [ ] T042 Mover el `NotificationEmailSender` actual de Storage a la nueva interfaz `IEmailSender` (renombrar a `SmtpEmailSender`) en `src/Infrastructure/IngenIA365ERP.Storage/Services/SmtpEmailSender.cs`; crear tres plantillas HTML en español en `src/Infrastructure/IngenIA365ERP.Storage/Templates/`:
  - `InvitationEmail.html` con `{{Nombre}}`, `{{Empresa}}`, `{{Emisor}}`, `{{Enlace}}`, `{{Expira}}`
  - `PasswordResetEmail.html` con `{{Nombre}}`, `{{Enlace}}`, `{{Expira}}`, `{{IpAddress}}` (esta última útil para que el legítimo detecte requests no solicitados)
  - `PasswordChangedNotification.html` con `{{Nombre}}`, `{{FechaCambio}}`, `{{IpAddress}}`, `{{UserAgent}}` (notificación post-hoc; si el usuario NO cambió su password, debe usar el flujo de reset inmediatamente — incluir enlace al reset)
  Las tres se sirven con el subject correspondiente desde un `IInvitationEmailDispatcher` / `IPasswordResetEmailDispatcher` / `IPasswordChangedNotifier` (servicios delgados de Application que arman el `EmailMessage`).
- [ ] T043 Wire `AddCentralIdentity()` extension en `src/Infrastructure/IngenIA365ERP.Identity/DependencyInjection.cs`: registra `IdentityCore<CentralUserIdentity>`, `AddRoles<IdentityRole<Guid>>`, `AddEntityFrameworkStores<AdminDbContext>`, sustituye `IPasswordHasher` por `BcryptPasswordHasher`, registra `ICentralIdentityProvider → AspNetCoreIdentityProvider`, `IPwnedPasswordService → PwnedPasswordService`, `ITenantMembershipReader → TenantMembershipReader`, `CentralJwtIssuer`. **Configurar `IdentityOptions` con `Lockout.AllowedForNewUsers = false` y `Lockout.MaxFailedAccessAttempts = int.MaxValue`** para deshabilitar el lockout interno de ASP.NET Identity y dejar al `LoginAttemptCounter` (Redis, T040) como única fuente de verdad para el bloqueo progresivo — evita interacción no coordinada entre dos mecanismos. (depende de T041)

### Middleware + filters + DI principal

- [ ] T044 Refactorizar `src/Presentation/IngenIA365ERP.API/Middleware/TenantResolutionMiddleware.cs`: dejar de leer header `X-Tenant-Id` y subdominio; leer claim `active_tenant_id` del JWT validado. Si el claim no está (estado intermedio post-login pre-select-tenant) y la ruta es de `/api/sessions/*` o `/api/auth/*` o `/api/invitations/*` → permitir sin tenant; en cualquier otra ruta → `401 Unauthorized` con `Session.TenantNotSelected`
- [X] T045 [P] Crear `src/Presentation/IngenIA365ERP.API/Middleware/CentralIdentity/CentralIdentityChallengeMiddleware.cs` que traduzca `401` no autenticado a respuesta JSON `{ "errorCode": "Identity.Unauthenticated" }` consistente con el `ErrorEnvelopeFilter`. **NO wired en Program.cs todavía** (Chunk D).
- [X] T046 [P] Crear filtro `src/Presentation/IngenIA365ERP.API/Filters/CentralIdentity/RequireTenantAdminAttribute.cs` con helper estático `Check(http, routeTenantPublicId)` que verifica claim `tenant_admin == true`, `active_tenant_id` igual al `{tenantPublicId}`, **y `purpose == full`**. Master admin pasa el filtro automáticamente.
- [X] T047 [P] Crear filtro `src/Presentation/IngenIA365ERP.API/Filters/CentralIdentity/RequireMasterAdminAttribute.cs` con helper `Check(http)` que verifica claim `is_global_master_admin == true` **y `purpose == full`**.
- [X] T047a [P] Crear filtro `src/Presentation/IngenIA365ERP.API/Filters/CentralIdentity/RequirePurposeAttribute.cs` con parámetro `params string[] allowedPurposes` para endpoints scoped (login challenges). Helper estático `IsAllowed(http, allowedPurposes)`.
- [ ] T048 Wire DI completo en `src/Presentation/IngenIA365ERP.API/Program.cs`: `AddCentralIdentity()`, `AddAdminDbContext`, registrar `IEmailSender → SmtpEmailSender`, registrar middlewares en orden (Authentication → CentralIdentityChallenge → TenantResolution → Authorization)

### Architecture tests (blindaje de los doce principios)

- [X] T049 [P] Crear `tests/IngenIA365ERP.Architecture.Tests/Principles/Feature002_CentralIdentity.cs` (combinado T049+T050): (a) Application NEVER referencia `Microsoft.AspNetCore.Identity.*` (todo pasa por ICentralIdentityProvider); (b) Domain NEVER referencia Identity ni el bridge `CentralUserIdentity`; (c) Application NEVER referencia la clase concreta `AdminDbContext` ni `CentralUserIdentity` (usa abstracciones IAdminDbContext + ICentralIdentityProvider).
- [X] T050 [P] **Integrado en T049 (Feature002_CentralIdentity.cs)** — los 5 architecture tests cubren ambas reglas: AdminDbContext concreto solo desde Persistence, CentralUserIdentity solo desde Persistence/Identity, no exposición de tipos de infraestructura desde Domain/Application.

**Checkpoint Foundational**: con T001-T050 completas, las cinco user stories pueden empezar en paralelo. La piedra angular es T044 (TenantResolutionMiddleware refactor) + T048 (DI completo) — todo lo demás compila pero el sistema no arranca hasta esos dos.

---

## Phase 3: User Story 1 — Onboarding mediante invitación (Priority: P1) 🎯 MVP slice 1/2

**Goal**: emitir invitaciones (por master admin o admin de empresa), enviar correo con enlace, permitir al destinatario aceptar y crear su credencial central (caso nuevo) o reutilizarla (caso existente), activando la membresía con la empresa que lo invitó.

**Independent Test**: ejecutar el caso 1.1 + 1.2 + 1.4 del `quickstart.md`. Verificar que un email nuevo puede aceptar y entrar a la empresa invitante, que un email ya registrado solo confirma sin pedir nueva contraseña, que un token aceptado no es reutilizable.

### Commands, queries, validators, handlers

- [ ] T051 [US1] Implementar `IssueTenantInvitationCommand` (record) + `IssueTenantInvitationCommandValidator` (email format, no IsTenantAdmin permitido) + handler que verifica que el emisor es tenant admin del tenant indicado, marca invitación previa pendiente como `Superseded`, genera token + hash, persiste `Invitation`, dispara envío de correo via `IEmailSender`. Archivo: `src/Core/IngenIA365ERP.Application/Invitations/IssueTenantInvitationCommand.cs`
- [ ] T052 [P] [US1] Implementar `IssueMasterInvitationCommand` + Validator (solo master admin emisor, `InviteAsTenantAdmin` opcional `true`) + handler análogo en `src/Core/IngenIA365ERP.Application/Invitations/IssueMasterInvitationCommand.cs`
- [ ] T053 [P] [US1] Implementar `PreviewInvitationQuery` (input: token plano; output: `{ tenantPublicId, tenantName, email, isExistingCentralUser, inviteAsTenantAdmin, expiresAt, isValid, errorCode? }`) + handler en `src/Core/IngenIA365ERP.Application/Invitations/PreviewInvitationQuery.cs`. NO consume el token.
- [ ] T054 [US1] Implementar `AcceptInvitationCommand` (input: token + `registration?` o `existingCredentials?` o `activeSessionCentralUserId?`) + Validator (XOR entre las tres opciones; si registration → password ≥ 12) + handler que: (a) toma lock Redis `lock:invitation:{tokenHash}` TTL 30s; (b) carga `Invitation` con `RowVersion`; (c) valida estado `Pending` y `ExpiresAt > now`; (d) decide rama nueva-vs-existente-vs-sesión-activa comparando contra `NormalizedEmail`; (e) en rama nueva → `ICentralIdentityProvider.CreateUserAsync` (con validación Pwned); (f) en rama existente con password → `ICentralIdentityProvider.ValidatePasswordAsync`; (g) **en rama sesión activa → el endpoint ya validó que el JWT del header `Authorization` pertenece al mismo email de la invitación; el handler salta la verificación de password (FR-029(b))**; (h) crea o reactiva `TenantMembership` (de `Revoked → Active` o nueva en `Active`); (i) provisiona fila en `SEC_Users` del tenant via `TenantUserProvisioner`; (j) marca `Invitation.Accepted` con UPDATE condicional sobre `RowVersion`; (k) emite JWT con `active_tenant_id` ya seteado (sustituye el de la sesión activa si lo hubiera); (l) emite eventos auditables. Archivo: `src/Core/IngenIA365ERP.Application/Invitations/AcceptInvitationCommand.cs`
- [ ] T055 [P] [US1] Implementar `RevokeInvitationCommand` + handler (verifica autoría: tenant admin de su empresa, o master) que marca `Pending → Revoked`. Archivo: `src/Core/IngenIA365ERP.Application/Invitations/RevokeInvitationCommand.cs`
- [ ] T056 [US1] Crear servicio `TenantUserProvisioner` en `src/Core/IngenIA365ERP.Application/Common/Services/TenantUserProvisioner.cs` con método `EnsureExistsAsync(centralUserId, centralUserEmail, tenantId)` que crea fila `SEC_Users` en la BD del tenant si no existe, o reactiva si existía soft-deleted. Inyectado en `AcceptInvitationCommandHandler`.
- [ ] T057 [US1] Refactorizar `IEmailSender` consumer: `InvitationEmailDispatcher` en `src/Core/IngenIA365ERP.Application/Invitations/Services/InvitationEmailDispatcher.cs` que toma `Invitation` + `Tenant` + emisor y arma el `EmailMessage` con la plantilla `InvitationEmail.html` interpolando enlace `https://{baseUrl}/auth/accept-invitation?token={base64url}`

### Carter endpoints

- [ ] T058 [US1] Crear `src/Presentation/IngenIA365ERP.API/Modules/InvitationsModule.cs` con rutas: `POST /api/tenants/{tenantPublicId}/invitations` `[RequireTenantAdmin]`, `POST /api/saas/invitations` `[RequireMasterAdmin]`, `GET /api/invitations/{token}/preview` `[AllowAnonymous]`, `POST /api/invitations/accept` `[AllowAnonymous]`, `DELETE /api/invitations/{publicId}` (autorización por handler)

### Application + integration tests

- [ ] T059 [P] [US1] Tests `tests/IngenIA365ERP.Application.Tests/Invitations/AcceptInvitationCommandHandlerTests.cs`: casos rama-nueva-éxito, rama-existente-éxito, password-débil, password-pwned, token-expirado, token-revocado, token-ya-aceptado, concurrencia-doble-aceptación (con NSubstitute mockeando ICentralIdentityProvider y lock)
- [ ] T060 [P] [US1] Tests `tests/IngenIA365ERP.Application.Tests/Invitations/IssueTenantInvitationCommandValidatorTests.cs` (email inválido, IsTenantAdmin true → rechazo, emisor no admin → rechazo)
- [ ] T061 [P] [US1] Tests `tests/IngenIA365ERP.Application.Tests/Invitations/RevokeInvitationCommandHandlerTests.cs` (revocación por admin de otro tenant → rechazo, revocación de invitación ya aceptada → rechazo)
- [ ] T062 [US1] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_InviteRegisterLogin.cs` con WebApplicationFactory + Testcontainers (SQL Server + Mongo + Redis efímeros + mock SMTP): flujo master invita → preview → accept rama nueva → login → membership activa, validando BD y `audit_events`
- [ ] T063 [P] [US1] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/Security_InvitationReplay.cs`: aceptar → reintentar mismo token → 410 Gone Invitation.AlreadyAccepted

### UI Blazor

- [ ] T064 [US1] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Auth/AcceptInvitation.razor` con ruta `/auth/accept-invitation` que: (a) llama `GET /api/invitations/{token}/preview` al `OnInitializedAsync`; (b) muestra mensaje "expirada"/"revocada"/"ya aceptada" si `isValid=false`; (c) si `isExistingCentralUser=true` Y hay JWT central válido en sesión Y el claim `email` del JWT coincide con `preview.email` (case-insensitive) → mostrar pantalla de un solo botón "Confirmar incorporación a {Empresa}" que llama a `/api/invitations/accept` sin requerir password (el backend reutiliza la sesión activa — FR-029(b)); (d) si `isExistingCentralUser=true` Y no hay sesión activa (o email no coincide) → formulario "confirma con tu contraseña"; (e) si `isExistingCentralUser=false` → formulario registro con password meter + retroalimentación en tiempo real; (f) POST a `/api/invitations/accept`; (g) en éxito → almacena JWT (sustituye el anterior si lo había) y navega al dashboard del tenant invitante
- [ ] T065 [P] [US1] Crear `src/Presentation/IngenIA365ERP.Web.Client/Services/InvitationClient.cs` con métodos `PreviewAsync(token)`, `AcceptAsync(token, registration|existingCredentials)` consumiendo los endpoints REST

**Checkpoint US1**: las invitaciones se emiten, llegan por correo, se aceptan, crean identidad central y activan membresía. La función ya es demostrable aunque el login operativo todavía requiere el JWT emitido por la propia aceptación.

---

## Phase 4: User Story 2 — Login centralizado sin combo box (Priority: P1) 🎯 MVP slice 2/2

**Goal**: que un usuario ya invitado pueda entrar por la URL única sin elegir empresa: si tiene una sola membresía activa entra directo, si no tiene ninguna recibe el mensaje "solicita invitación", si tiene MFA se le pide TOTP, si una empresa exige MFA y no lo tiene se le fuerza a configurarlo.

**Independent Test**: ejecutar los casos 2.1 a 2.4 del `quickstart.md`. La pantalla `Login.razor` ya no muestra combo box; un usuario mono-empresa entra directo; un usuario con MFA pasa por challenge; un usuario sin membresía ve `NoMembershipNotice.razor`; un usuario con membresía en una empresa que exige MFA y sin MFA pasa por enrollment forzado.

### Commands + handlers

- [ ] T066 [US2] Implementar `LoginCommand` (record: `Email`, `Password`, `IpAddress?`, `UserAgent?`) + Validator + handler: (a) consulta `LoginAttemptCounter` → si bloqueado → `423 Identity.Locked.Soft`; (b) `ICentralIdentityProvider.ValidatePasswordAsync`; (c) si falla → incrementar contador, registrar `CentralUserLoginAttempt(Result=InvalidPassword|UserNotFound|LockedOut|Disabled)`, retornar `Identity.InvalidCredentials`; (d) si éxito → registrar `CentralUserLoginAttempt(Result=Success)` y actualizar `LastLoginAt`; (e) cargar `ITenantMembershipReader.GetActiveMembershipsAsync`; (f) decidir challenge según count: 0 → `NoActiveMembership`; (g) consultar políticas MFA de los tenants — si AL MENOS UNO exige MFA y usuario sin MFA → `MfaEnrollmentRequired` + emitir `challengeToken` JWT 5min con claim `purpose=mfa-enroll` (válido SOLO para `/api/profile/mfa/enroll` y `/api/profile/mfa/confirm`); (h) si usuario con MFA → `MfaRequired` + emitir `challengeToken` JWT 5min con claim `purpose=mfa-verify` (válido SOLO para `/api/auth/mfa/verify`); (i) si MFA OK o no requerido y 1 tenant → emitir access+refresh **operativos** con `active_tenant_id` resuelto y `purpose=full` (default); (j) si >1 tenant → comprobar `DefaultTenantId` válido → entrar directo; (k) **si `DefaultTenantId` ya NO apunta a una membresía Active → UPDATE silencioso `ADM_CentralUsers.DefaultTenantId = NULL` + emitir evento auditable `Profile.DefaultTenantInvalidated.Cleared` (cumple FR-018, evita preferencias zombi en BD)**; (l) en otro caso retornar lista `activeTenants` con `challengeToken` con claim `purpose=tenant-select` (válido SOLO para `/api/sessions/select-tenant`). Archivo: `src/Core/IngenIA365ERP.Application/Identity/Auth/LoginCommand.cs`
- [ ] T067 [P] [US2] Implementar `MfaVerifyCommand` (challengeToken + code) + handler que valida token, ejecuta `ICentralIdentityProvider.VerifyMfaAsync`, emite JWT operativo análogamente al login. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Auth/MfaVerifyCommand.cs`
- [ ] T068 [P] [US2] Implementar `RefreshTokenCommand` con family-rotation y blacklist (reutilizar patrón Fase 0). Archivo: `src/Core/IngenIA365ERP.Application/Identity/Auth/RefreshTokenCommand.cs`
- [ ] T069 [P] [US2] Implementar `LogoutCommand` (invalida refresh actual + clears security stamp si aplica). Archivo: `src/Core/IngenIA365ERP.Application/Identity/Auth/LogoutCommand.cs`
- [ ] T070 [P] [US2] Implementar `GetMeQuery` que devuelve `centralUserId`, `email`, `isGlobalMasterAdmin`, `mfaEnabled`, `activeTenant`, `availableTenants`, `defaultTenantPublicId`. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Auth/GetMeQuery.cs`

### Carter endpoint refactor

- [ ] T071 [US2] Refactorizar `src/Presentation/IngenIA365ERP.API/Modules/AuthModule.cs`: eliminar parámetro `tenant` del request de login, mapear las nuevas respuestas typed (`autoSelected`, `challenge: NoActiveMembership|MfaRequired|MfaEnrollmentRequired|TenantSelection`), añadir endpoints `mfa/verify`, `refresh`, `logout`, `me`

### Tests

- [ ] T072 [P] [US2] Tests `tests/IngenIA365ERP.Application.Tests/Identity/Auth/LoginCommandHandlerTests.cs`: 0 membresías → NoActiveMembership; 1 membresía sin MFA → direct entry; 1 membresía con MFA → MfaRequired; >1 membresía sin default → TenantSelection; >1 con default válido → auto-select default; >1 con default inválido → TenantSelection sin default; credenciales inválidas → InvalidCredentials + incremento de contador; lockout → Identity.Locked.Soft; tenant exige MFA + usuario sin MFA → MfaEnrollmentRequired
- [ ] T073 [P] [US2] Tests `tests/IngenIA365ERP.Application.Tests/Identity/Auth/MfaVerifyCommandHandlerTests.cs` (código válido, código inválido, challenge expirado)
- [ ] T074 [US2] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_LoginSingleTenant.cs` (usuario invitado + aceptado → login → JWT con active_tenant_id correcto)
- [ ] T075 [P] [US2] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_LoginGenericErrors.cs` (email inexistente y password incorrecta devuelven el mismo mensaje)
- [ ] T076 [P] [US2] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_LockoutProgression.cs` (5 fallos consecutivos → 423 lockout 60s; 10 fallos → 5min; tras éxito el contador se resetea)

### UI Blazor

- [ ] T077 [US2] Refactorizar `src/Presentation/IngenIA365ERP.Web/Pages/Auth/Login.razor`: eliminar combo box de cliente, dejar solo email + password; manejar respuestas `challenge` (redirigir según challenge) **almacenando el `challengeToken` recibido como JWT temporal scoped en el `IAuthTokenStore` del cliente (clave separada de la sesión operativa, ej. `challenge-jwt`) para que las subsecuentes llamadas lo envíen en `Authorization: Bearer`**; SI MfaRequired → `Pages/Auth/MfaChallenge.razor`; SI MfaEnrollmentRequired → `/auth/enroll-mfa-forced` (ver T079l — distinta del enrollment voluntario `/profile/mfa`; usa el `challenge-jwt` con purpose=mfa-enroll); SI TenantSelection → `Pages/Auth/SelectTenant.razor` (con `challenge-jwt` purpose=tenant-select); SI NoActiveMembership → `Pages/Auth/NoMembershipNotice.razor`
- [ ] T078 [P] [US2] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Auth/NoMembershipNotice.razor` con mensaje "No tienes acceso a ninguna empresa. Solicita una invitación." + botón "Cerrar sesión"
- [ ] T079 [P] [US2] Crear/refactorizar `src/Presentation/IngenIA365ERP.Web/Pages/Auth/MfaChallenge.razor` para consumir `POST /api/auth/mfa/verify`

**Checkpoint US2**: con US1 + US2 completadas se tiene MVP funcional: alta por invitación + login centralizado (mono-empresa). Apto para piloto con una sola cooperativa.

---

## Phase 4b: Profile & Account Recovery (Priority: P1 — extiende US2)

**Goal**: completar los flujos de identidad central que la spec exige bajo US2 pero que necesitan endpoints/UI propios: enrollment de MFA (voluntario y forzado), cambio de contraseña, y el flujo "olvidé mi contraseña" anclado en la assumption de la spec. Sin esta fase no se cumplen FR-003 (opt-in MFA), FR-003b (forced enrollment), FR-044 (Pwned al cambio/reset), ni la assumption "Recuperación de contraseña".

**Independent Test**: (a) usuario activa MFA desde su perfil y al siguiente login se le pide TOTP; (b) admin de empresa activa política MFA obligatorio → un miembro sin MFA hace login → es redirigido a `/auth/enroll-mfa-forced` sin acceso al dashboard hasta completar setup; (c) usuario cambia su contraseña → todos sus refresh tokens son invalidados; (d) usuario solicita reset → recibe correo en MailHog → consume token → entra con nueva contraseña; (e) usuario solicita reset con email inexistente → retorna 202 sin revelar existencia.

### Commands + handlers

- [ ] T079a [US2] Implementar `BeginMfaEnrollmentCommand` (sin input — usa `central_user_id` del JWT) + handler que invoca `ICentralIdentityProvider.EnrollMfaAsync`, persiste el secret pendiente en una key Redis efímera `mfa-pending:{centralUserId}` TTL 10 min, devuelve `{ secretBase32, otpAuthUri, recoveryCodes[] }`. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Profile/BeginMfaEnrollmentCommand.cs`
- [ ] T079b [P] [US2] Implementar `ConfirmMfaEnrollmentCommand` (input: `code` TOTP) + Validator + handler que carga secret pendiente desde Redis, llama `ICentralIdentityProvider.VerifyMfaAsync(secret, code)`, si OK persiste `MfaSecret` cifrado + `TwoFactorEnabled = true` + **genera recovery codes vía `UserManager<CentralUserIdentity>.GenerateNewTwoFactorRecoveryCodesAsync(user, count=10)` (estos se almacenan automáticamente en la tabla estándar `ADM_CentralUserTokens` creada en T014 — NO se requiere entidad de dominio ni tabla custom)** + limpia Redis + invalida caché de membresías + devuelve los códigos al cliente UNA SOLA VEZ. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Profile/ConfirmMfaEnrollmentCommand.cs`
- [ ] T079c [P] [US2] Implementar `DisableMfaCommand` (input: `currentPassword`) + Validator + handler que: (a) valida currentPassword vía `ICentralIdentityProvider.ValidatePasswordAsync`; (b) consulta `ADM_TenantMfaPolicies` para todos los tenants del usuario — **si AL MENOS UNO tiene `IsRequired=true` → rechazar con `Profile.Mfa.RequiredByTenantPolicy(tenantName)` (cumple FR-003c en sentido inverso)**; (c) si todos los tenants permiten → desactivar MFA (clear `MfaSecret`, `TwoFactorEnabled=false`). Archivo: `src/Core/IngenIA365ERP.Application/Identity/Profile/DisableMfaCommand.cs`
- [ ] T079d [P] [US2] Implementar `ChangePasswordCommand` (input: `currentPassword`, `newPassword`, `ipAddress?`, `userAgent?`) + Validator (newPassword ≥ 12, ≠ currentPassword) + handler: (a) valida currentPassword; (b) `IPwnedPasswordService.IsPwnedAsync(newPassword)` → si comprometida → `Profile.Password.Pwned`; (c) `ICentralIdentityProvider.ChangePasswordAsync` (regenera security stamp → invalida todos los refresh tokens del usuario); (d) envía notificación de seguridad vía `IPasswordChangedNotifier` (plantilla `PasswordChangedNotification.html` con IP + UA del cambio) — **fail-soft**: si el envío falla, log Warning pero NO se aborta el cambio; (e) audita `Profile.PasswordChanged`. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Profile/ChangePasswordCommand.cs`
- [ ] T079e [US2] Implementar `RequestPasswordResetCommand` (input: `email`, `ipAddress?`) + handler: (a) busca `CentralUser` por `NormalizedEmail`; (b) si existe → genera token 32 bytes random → Base64Url → hash SHA-256 → persiste en `ADM_PasswordResetTokens` con TTL 1 hora; (c) envía correo via `IEmailSender` con plantilla `PasswordResetEmail.html` y enlace `https://app.ingenia365.com/auth/reset-password?token={base64url}`; (d) **siempre retorna 202 Accepted aunque el email no exista, para no revelar existencia (alineado con FR-041)**; (e) audita `Profile.PasswordResetRequested` con email (sea o no existente). Archivo: `src/Core/IngenIA365ERP.Application/Identity/Profile/RequestPasswordResetCommand.cs`
- [ ] T079f [US2] Implementar `ResetPasswordCommand` (input: `token`, `newPassword`) + Validator + handler: (a) toma lock Redis `lock:pwdreset:{tokenHash}` TTL 30s; (b) busca `ADM_PasswordResetTokens` por hash; (c) valida `ConsumedAt IS NULL AND ExpiresAt > now`; (d) `IPwnedPasswordService.IsPwnedAsync(newPassword)`; (e) `ICentralIdentityProvider.ChangePasswordAsync` (regenera security stamp); (f) UPDATE condicional `ConsumedAt = now WHERE RowVersion = @x`; (g) si Pwned o token inválido → mensajes diferenciados sin filtrar info sensible. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Profile/ResetPasswordCommand.cs`

### DDL + EF

- [ ] T079g [P] [US2] Crear `database/schema/15e_Admin_PasswordResetTokens.sql` con tabla `ADM_PasswordResetTokens` (Id BIGINT IDENTITY, PublicId UNIQUEIDENTIFIER UNIQUE, CentralUserId UNIQUEIDENTIFIER NOT NULL FK, TokenHash BINARY(32) NOT NULL UNIQUE, RequesterIp NVARCHAR(45), CreatedAt DATETIMEOFFSET NOT NULL, ExpiresAt DATETIMEOFFSET NOT NULL, ConsumedAt DATETIMEOFFSET NULL, RowVersion ROWVERSION, auditable fields). Header idempotente.
- [ ] T079h [P] [US2] Crear EF Configuration `src/Infrastructure/IngenIA365ERP.Persistence/Configuration/Admin/PasswordResetTokenConfiguration.cs` + añadir `DbSet<PasswordResetToken> PasswordResetTokens` a `AdminDbContext` (T019)

### Carter endpoints

- [ ] T079i [US2] Crear `src/Presentation/IngenIA365ERP.API/Modules/ProfileModule.cs` con rutas:
  - `POST /api/profile/mfa/enroll` con `[RequirePurpose("mfa-enroll", "full")]` — admite tanto el JWT scoped del login forzado como un JWT operativo (enrollment voluntario).
  - `POST /api/profile/mfa/confirm` con `[RequirePurpose("mfa-enroll", "full")]` — al éxito, **si el JWT era `mfa-enroll`, el handler invoca `CentralJwtIssuer.IssueAccessToken(...,purpose=full)` y devuelve el JWT operativo en la respuesta** (eleva la sesión de scope-limited a full sin requerir re-login).
  - `POST /api/profile/mfa/disable` con `[RequirePurpose("full")]`.
  - `POST /api/profile/password` con `[RequirePurpose("full")]`.
  Todos reenvían a `ISender.Send(...)` (depende de T079a-T079d, T079h, T047a)
- [ ] T079j [P] [US2] Crear `src/Presentation/IngenIA365ERP.API/Modules/AuthRecoveryModule.cs` con rutas `[AllowAnonymous]`: `POST /api/auth/password/forgot`, `POST /api/auth/password/reset` (depende de T079e, T079f)

### Tests

- [ ] T079k [P] [US2] Tests unitarios en `tests/IngenIA365ERP.Application.Tests/Identity/Profile/`:
  - `ChangePasswordCommandHandlerTests.cs` (currentPassword erróneo → InvalidCredentials; newPassword Pwned → rechazo; éxito → security stamp regenerado mockeable; misma password → rechazo)
  - `DisableMfaCommandHandlerTests.cs` (al menos un tenant con MFA obligatoria → rechazo con nombre de tenant; ningún tenant exige → éxito)
  - `RequestPasswordResetCommandHandlerTests.cs` (email existente → token generado + email enviado; email inexistente → 202 sin enviar email + audit registrado; doble request rápido → reemplaza el token anterior)
  - `ResetPasswordCommandHandlerTests.cs` (token expirado → rechazo; token ya consumido → rechazo; éxito → password cambiada + security stamp regenerado + token marcado ConsumedAt)
  - `ConfirmMfaEnrollmentCommandHandlerTests.cs` (código TOTP inválido → rechazo; sin secret pendiente → rechazo; éxito → TwoFactorEnabled true + recoveryCodes generados)

### UI Blazor

- [ ] T079l [US2] Crear páginas Blazor en `src/Presentation/IngenIA365ERP.Web/Pages/`:
  - `Profile/MfaEnrollment.razor` (ruta `/profile/mfa`, voluntaria): muestra QR + recovery codes + input código; botón "Activar"; al éxito redirige al perfil. Si MFA ya activo → muestra estado + botón "Desactivar" (con confirmación de password).
  - `Auth/MfaEnrollmentForced.razor` (ruta `/auth/enroll-mfa-forced`, page guard): solo permite navegación a sí misma y a `/auth/logout` mientras la sesión tenga el flag `mfaEnrollmentRequired`. Mismo formulario que `MfaEnrollment.razor` pero con banner "La empresa {Nombre} requiere MFA. Configúralo para continuar." y sin opción de cancelar. Tras éxito → POST `/api/profile/mfa/confirm` → invalidar caché de membresías → redirigir al dashboard.
  - `Profile/ChangePassword.razor` (ruta `/profile/password`): formulario `currentPassword`/`newPassword`/`newPasswordConfirm` con password meter en tiempo real + advertencia clara "Esto afectará tu acceso a todas tus empresas y cerrará todas tus sesiones activas en otros dispositivos."
  - `Auth/ForgotPassword.razor` (ruta pública `/auth/forgot-password`): input email + botón "Enviar enlace"; tras submit muestra **siempre** mensaje genérico "Si el correo existe, hemos enviado las instrucciones." (cumple FR-041).
  - `Auth/ResetPassword.razor` (ruta pública `/auth/reset-password?token=...`): formulario newPassword + retroalimentación dual; **al éxito (204) redirige a `/auth/login` con `?reset=1` en query** mostrando banner "Tu contraseña fue restablecida. Inicia sesión con tu nueva contraseña." NO almacena tokens — la sesión empieza limpia desde login.
- [ ] T079m [US2] Integration tests `tests/IngenIA365ERP.API.IntegrationTests/Identity/`:
  - `EndToEnd_MfaEnrollmentForced.cs` (admin de tenant activa política → otro user sin MFA hace login → recibe `MfaEnrollmentRequired` → llama enroll → confirma → entra)
  - `EndToEnd_PasswordChangeInvalidatesSessions.cs` (user con refresh token activo → cambia password → próximo `/api/auth/refresh` falla con `Identity.RefreshToken.Invalid`)
  - `EndToEnd_PasswordResetFlow.cs` (forgot → correo capturado en MailHog mock → reset → login OK con nueva password; token reusado → `Identity.PasswordReset.AlreadyConsumed`)
  - `Security_PasswordResetEnumeration.cs` (forgot a email inexistente → 202; forgot a email existente → 202 — respuesta indistinguible; latencia equivalente)

**Checkpoint Phase 4b**: la identidad central queda completa — los usuarios gestionan su MFA y su contraseña de forma autónoma; el "olvidé mi contraseña" no exige intervención del master admin; la política "MFA obligatorio" por tenant es enforced end-to-end.

---

## Phase 5: User Story 3 — Multi-empresa: selector, cambio y default (Priority: P2)

**Goal**: usuarios con varias membresías ven selector al login, pueden cambiar de empresa desde el header, y pueden fijar una empresa por defecto.

**Independent Test**: ejecutar casos 3.1 a 3.5 del `quickstart.md`.

### Commands + handlers

- [ ] T080 [US3] Implementar `SelectTenantCommand` (input: tenantPublicId, challengeToken o JWT central sin tenant) + Validator + handler: valida membership Active, valida política MFA del tenant, emite JWT con `active_tenant_id` + `tenant_admin` + `mfa_verified`. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Sessions/SelectTenantCommand.cs`
- [ ] T081 [P] [US3] Implementar `SwitchTenantCommand` (input: tenantPublicId) + Validator + handler: similar a Select pero parte de un JWT con `active_tenant_id` ya seteado, valida membership Active en destino, emite nuevo JWT, registra evento `Session.TenantSwitched`. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Sessions/SwitchTenantCommand.cs`
- [ ] T082 [P] [US3] Implementar `GetMyActiveTenantsQuery` + handler que devuelve lista con `tenantPublicId`, `tenantName`, `isTenantAdmin`, `isDefault`. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Sessions/GetMyActiveTenantsQuery.cs`
- [ ] T083 [P] [US3] Implementar `SetDefaultTenantCommand` (input: tenantPublicId? — null para limpiar) + Validator (si no null, debe haber membership Active) + handler que actualiza `CentralUser.DefaultTenantId`. Archivo: `src/Core/IngenIA365ERP.Application/Identity/Profile/SetDefaultTenantCommand.cs`

### Carter endpoint

- [ ] T084 [US3] Crear `src/Presentation/IngenIA365ERP.API/Modules/SessionsModule.cs` con rutas:
  - `GET /api/sessions/active-tenants` con `[RequirePurpose("full")]`
  - `POST /api/sessions/select-tenant` con `[RequirePurpose("tenant-select", "full")]` (acepta el challenge JWT emitido por login)
  - `POST /api/sessions/switch-tenant` con `[RequirePurpose("full")]`
  - `PUT /api/profile/default-tenant` con `[RequirePurpose("full")]`

### Tests

- [ ] T085 [P] [US3] Tests `tests/IngenIA365ERP.Application.Tests/Identity/Sessions/SwitchTenantCommandHandlerTests.cs` (membership inactiva → 403; tenant exige MFA y usuario sin MFA → 403 Tenant.MfaPolicyEnforced; éxito → JWT re-emitido)
- [ ] T086 [P] [US3] Tests `tests/IngenIA365ERP.Application.Tests/Identity/Profile/SetDefaultTenantCommandHandlerTests.cs` (membership no activa → 400; null → limpia preferencia)
- [ ] T087 [US3] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_MultiTenantSwitch.cs` (usuario en 3 tenants → selector → entra A → cambia a B → datos de B accesibles, de A bloqueados)
- [ ] T088 [P] [US3] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/Security_TenantCrossover.cs` (JWT con `active_tenant_id` manipulado a un tenant sin membership → 403 al primer endpoint que toque datos del tenant)

### UI Blazor

- [ ] T089 [US3] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Auth/SelectTenant.razor` (lista, click → select-tenant, manejo de auto-select por default)
- [ ] T090 [P] [US3] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Profile/DefaultTenantSetting.razor` (dropdown con membresías activas + botón "Establecer como por defecto" / "Quitar default")
- [ ] T091 [US3] Crear `src/Presentation/IngenIA365ERP.Web.Client/Components/Header/TenantSwitcher.razor` visible cuando `availableTenants.Count > 1`, con detección de `hasUnsavedChanges` global vía `IFormDirtyStateService` (ver T091a) y modal de confirmación antes de invocar `/api/sessions/switch-tenant`
- [ ] T091a [P] [US3] Crear servicio `IFormDirtyStateService` en `src/Presentation/IngenIA365ERP.Web.Client/Services/IFormDirtyStateService.cs` + implementación `InMemoryFormDirtyStateService` con: `Register(formId)`, `MarkDirty(formId)`, `MarkClean(formId)`, `bool HasDirtyForms`. Crear componente base `DirtyTrackingEditForm.razor` (wrapper de `EditForm`) que llama `OnFieldChanged → MarkDirty` y `OnValidSubmit → MarkClean` automáticamente. Documentar en `docs/desarrollo/dirty-state.md` cómo migrar formularios existentes para que el TenantSwitcher detecte cambios pendientes.
- [ ] T092 [P] [US3] Crear `src/Presentation/IngenIA365ERP.Web.Client/Services/TenantSessionClient.cs` con métodos `GetActiveTenantsAsync`, `SelectAsync(tenantPublicId)`, `SwitchAsync(tenantPublicId)`, `SetDefaultAsync(tenantPublicId?)`

**Checkpoint US3**: multi-empresa completamente operativa. La pantalla `SelectTenant.razor` y el `TenantSwitcher.razor` cubren los flujos al login y en sesión activa.

---

## Phase 6: User Story 4 — Admin de empresa: gestión de usuarios y MFA (Priority: P2)

**Goal**: que un admin de empresa pueda invitar miembros (cubierto en US1), listar, suspender, reactivar, revocar, promover/degradar admins (con salvaguarda del último admin) y activar/desactivar la política "MFA obligatorio" en su empresa.

**Independent Test**: ejecutar caso 4.1 a 4.4 del `quickstart.md`. Verificar que un tenant admin solo opera en su empresa (403 en otra) y que no puede degradarse a sí mismo si es el último admin activo.

### Commands + handlers

- [ ] T093 [US4] Implementar `SuspendMembershipCommand` + Validator + handler (Active → Suspended; verificar autoridad: admin tenant o master; **al éxito publica `IMembershipChangedNotifier.PublishAsync(centralUserId)` para invalidar caché en todas las instancias**). Archivo: `src/Core/IngenIA365ERP.Application/Memberships/SuspendMembershipCommand.cs`
- [ ] T094 [P] [US4] Implementar `ActivateMembershipCommand` + handler (Suspended → Active; publica `IMembershipChangedNotifier`). Archivo: `src/Core/IngenIA365ERP.Application/Memberships/ActivateMembershipCommand.cs`
- [ ] T095 [P] [US4] Implementar `RevokeMembershipCommand` + Validator + handler (transición a Revoked; salvaguarda último admin si afectado es admin; soft-delete fila `SEC_Users`; publica `IMembershipChangedNotifier`). Archivo: `src/Core/IngenIA365ERP.Application/Memberships/RevokeMembershipCommand.cs`
- [ ] T096 [P] [US4] Implementar `PromoteToTenantAdminCommand` + handler (set `IsTenantAdmin = true`; emite evento `Membership.PromotedToAdmin`; publica `IMembershipChangedNotifier`). Archivo: `src/Core/IngenIA365ERP.Application/Memberships/PromoteToTenantAdminCommand.cs`
- [ ] T097 [US4] Implementar `DemoteFromTenantAdminCommand` + Validator + handler (salvaguarda último admin transaccional según `research.md > D-09`; rechaza con `Membership.LastAdminProtected` o `Membership.SelfDemoteBlocked.LastAdmin`; publica `IMembershipChangedNotifier`). Archivo: `src/Core/IngenIA365ERP.Application/Memberships/DemoteFromTenantAdminCommand.cs`
- [ ] T098 [P] [US4] Implementar `ListTenantMembersQuery` + handler (paginación, filtro por status). Archivo: `src/Core/IngenIA365ERP.Application/Memberships/ListTenantMembersQuery.cs`
- [ ] T099 [US4] Implementar `UpdateTenantMfaPolicyCommand` + Validator + handler (activa/desactiva; al cambiar publica `IMembershipChangedNotifier.PublishAsync` para CADA `centralUserId` con membresía Active en este tenant — invalida caché para que el flag `isMfaRequired` se recompute en próximos logins). Archivo: `src/Core/IngenIA365ERP.Application/Tenants/UpdateTenantMfaPolicyCommand.cs`
- [ ] T100 [P] [US4] Implementar `GetTenantMfaPolicyQuery` + handler. Archivo: `src/Core/IngenIA365ERP.Application/Tenants/GetTenantMfaPolicyQuery.cs`

### Carter endpoints

- [ ] T101 [US4] Crear `src/Presentation/IngenIA365ERP.API/Modules/MembershipsModule.cs` con rutas `GET /api/tenants/{tenantPublicId}/members`, `POST .../members/{publicId}/suspend`, `.../activate`, `.../revoke`, `.../promote-admin`, `.../demote-admin` (todas `[RequireTenantAdmin]` salvo cuando actor master)
- [ ] T102 [P] [US4] Crear `src/Presentation/IngenIA365ERP.API/Modules/TenantMfaPolicyModule.cs` con `GET` / `PUT /api/tenants/{tenantPublicId}/mfa-policy`

### Tests

- [ ] T103 [P] [US4] Tests `tests/IngenIA365ERP.Application.Tests/Memberships/PromoteToTenantAdminCommandHandlerTests.cs`
- [ ] T104 [P] [US4] Tests `tests/IngenIA365ERP.Application.Tests/Memberships/DemoteLastAdminGuardTests.cs` (un admin: rechazo; dos admins: éxito; auto-degrade del último: rechazo específico)
- [ ] T105 [P] [US4] Tests `tests/IngenIA365ERP.Application.Tests/Memberships/RevokeMembershipCommandHandlerTests.cs` (incluye salvaguarda último admin al revocar)
- [ ] T106 [P] [US4] Tests `tests/IngenIA365ERP.Application.Tests/Tenants/UpdateTenantMfaPolicyCommandHandlerTests.cs`
- [ ] T107 [US4] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_MfaPolicyEnforcement.cs` (activar política → usuario sin MFA hace login → MfaEnrollmentRequired; configura MFA → entra)
- [ ] T108 [P] [US4] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/Security_TenantAdminScope.cs` (admin de A intenta invitar/listar/modificar miembros de B → 403)

### UI Blazor

- [ ] T109 [US4] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Admin/Tenant/Invitations.razor` (emitir invitación, listar pendientes, revocar)
- [ ] T110 [P] [US4] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Admin/Tenant/MfaPolicy.razor` (toggle con confirmación: "Esto obligará a todos los miembros sin MFA a configurarlo en su próximo login")
- [ ] T111 [P] [US4] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Admin/Tenant/Members.razor` (tabla con acciones suspender/activar/revocar/promover/degradar; banner de advertencia "queda 1 administrador activo" cuando aplica)

**Checkpoint US4**: las cooperativas operan autónomas, gestionan sus miembros y endurecen seguridad por su cuenta.

---

## Phase 7: User Story 5 — Master admin: gobierno global (Priority: P3)

**Goal**: el master admin crea nuevos tenants, invita al primer administrador de cada uno, ve el catálogo global y puede ejecutar recuperación operativa (reset MFA forzado).

**Independent Test**: ejecutar casos 5.1 a 5.3 del `quickstart.md`.

### Commands + handlers

- [ ] T112 [US5] Implementar `RegisterTenantCommand` (refactor del existente de Fase 0 si lo hubiera; integra emisión automática de invitación marcada admin para `firstAdminEmail`) en `src/Core/IngenIA365ERP.Application/Saas/RegisterTenantCommand.cs`
- [ ] T113 [P] [US5] Implementar `ListAllTenantsQuery` + handler con paginación y filtros (search, subscriptionStatus, mfaPolicyEnabled). Archivo: `src/Core/IngenIA365ERP.Application/Saas/ListAllTenantsQuery.cs`
- [ ] T114 [P] [US5] Implementar `ForceMfaResetCommand` + Validator + handler que invoca `ICentralIdentityProvider.ResetMfaAsync`, regenera security stamp (invalida refresh tokens), audita con razón obligatoria. Archivo: `src/Core/IngenIA365ERP.Application/Saas/ForceMfaResetCommand.cs`

### Carter endpoint

- [ ] T115 [US5] Crear `src/Presentation/IngenIA365ERP.API/Modules/SaasAdminModule.cs` con rutas `GET /api/saas/tenants`, `POST /api/saas/tenants`, `POST /api/saas/users/{centralUserPublicId}/force-mfa-reset`, todas `[RequireMasterAdmin]`

### Tests

- [ ] T116 [P] [US5] Tests `tests/IngenIA365ERP.Application.Tests/Saas/RegisterTenantCommandHandlerTests.cs` (verifica que se crea tenant + invitación con `inviteAsTenantAdmin=true` en una sola transacción atómica)
- [ ] T117 [P] [US5] Tests `tests/IngenIA365ERP.Application.Tests/Saas/ForceMfaResetCommandHandlerTests.cs` (reset → MfaSecret null + TwoFactorEnabled false + security stamp regenerado + refresh tokens invalidados)
- [ ] T118 [US5] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_MasterRegisterTenant.cs` (POST /api/saas/tenants → invitación enviada → admin acepta → entra como tenant admin)

### UI Blazor

- [ ] T119 [US5] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Saas/Tenants.razor` (tabla con buscador, columnas: nombre, NIT, suscripción, admins, miembros activos, política MFA)
- [ ] T120 [P] [US5] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Saas/MasterInvitations.razor` (formulario para emitir invitación a cualquier tenant marcable como admin)
- [ ] T121 [P] [US5] Crear `src/Presentation/IngenIA365ERP.Web/Pages/Saas/UserMfaReset.razor` (búsqueda de usuario por email + razón obligatoria + confirmación de reset)

**Checkpoint US5**: el producto puede onboardear nuevas cooperativas y recuperarse de incidentes operativos de identidad sin tocar BD manualmente.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: cierre de bordes — observabilidad, jobs, pruebas de carga, documentación.

- [ ] T122 [P] Añadir job en background `InvitationExpiryJob` que cada hora: (a) marca `Pending → Expired` las invitaciones cuyo `ExpiresAt < UtcNow`; (b) **en cascada, si la `TenantMembership` correspondiente quedó en estado `Invited` (caso: invitación a un email ya con CentralUser que nunca aceptó), la marca también `Revoked` con razón `Invitation.Expired`**; (c) emite eventos auditables `Invitation.Expired` y, cuando aplique, `Membership.Revoked.InvitationExpired`. Archivo: `src/Infrastructure/IngenIA365ERP.Identity/Jobs/InvitationExpiryJob.cs` (registrado como `BackgroundService`). Análogamente, T122 incluye `PasswordResetTokenCleanupJob` que purga tokens consumidos o expirados > 30 días (mencionado en data-model §6).
- [ ] T123 [P] Añadir enrichers Serilog para `central_user_id` y `active_tenant_id` extraídos del JWT en `src/Presentation/IngenIA365ERP.API/Program.cs` (vía `IHttpContextAccessor` + LoggingBehavior existente)
- [ ] T124 Test de carga `tests/IngenIA365ERP.Load.Tests/Identity/LoginThroughputScenario.cs` con NBomber: 100 logins/segundo durante 5 minutos contra `POST /api/auth/login` con usuarios pre-creados. Aceptación: p95 < 800 ms, cero errores 5xx
- [ ] T125 [P] Actualizar `docs/INDICE-DOCUMENTACION.md` añadiendo entrada de Fase 1 → feature 002 con enlaces a `specs/002-identidad-central-federada/{spec,plan,research,data-model,quickstart}.md`
- [ ] T126 [P] Actualizar `README.md` con sección "Identidad central v2": instrucciones de bootstrap del master admin (variables de entorno), nuevo flujo de login (sin combo box), instrucciones de configuración SMTP y feature flag de Pwned Passwords
- [ ] T127 Ejecutar `quickstart.md` end-to-end manualmente contra entorno dev con MailHog, capturar screenshots de cada user story, archivar en `docs/release-notes/002-identidad-central-federada/` como evidencia
- [ ] T128 [P] Cross-check final: ejecutar `dotnet test` completo (Domain + Application + Architecture + Integration + Load) y validar que las 12 compuertas constitucionales siguen en verde tras todo el trabajo

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: sin dependencias, arranca inmediatamente.
- **Phase 2 (Foundational)**: depende de Phase 1; BLOQUEA todas las user stories. Su tarea crítica es T044 (TenantResolutionMiddleware refactor) + T048 (Program.cs DI), porque sin ellas ningún endpoint nuevo es servible.
- **Phase 3-7 (User Stories)**: dependen de Phase 2 completa. Una vez liberada Phase 2 las user stories pueden avanzar en paralelo, salvo que:
  - US4 reutiliza el `IssueTenantInvitationCommand` de US1 (Phase 3) — si se trabaja en paralelo, US4 puede mockear esa pieza mientras US1 madura.
  - US5 reutiliza el flujo de invitación de US1 — misma observación.
- **Phase 4b (Profile & Recovery)**: extiende US2. Sus tareas dependen de Phase 2 (T028 `ICentralIdentityProvider` con `EnrollMfa/VerifyMfa/ChangePassword`, T035 `BcryptPasswordHasher`, T036 `PwnedPasswordService`, T037 `CentralJwtIssuer`, T042 `IEmailSender`). Es **prerrequisito de US4** (T097 `UpdateTenantMfaPolicyCommand` activa la política, pero el enforcement end-to-end requiere `/auth/enroll-mfa-forced` que vive en T079l). Puede trabajarse en paralelo a US3 si hay equipo.
- **Phase 8 (Polish)**: depende de las user stories que se quieran cerrar para release.

### User Story Dependencies (entrega incremental)

- **US1 (P1)**: independiente. Cubre el camino crítico de alta.
- **US2 (P1)**: independiente de US1 funcionalmente (login no necesita haber invitado a nadie), pero un usuario invitado por US1 es el caso natural de prueba. **MVP mínimo = US1 + US2** (login mono-empresa con MFA opcional ya activado vía Phase 4b).
- **Phase 4b**: completa US2 con el enrollment de MFA, el cambio de contraseña, y el flujo "olvidé mi contraseña". **MVP completo = US1 + US2 + Phase 4b** (necesario para activar MFA en cualquier user).
- **US3 (P2)**: requiere US1 (necesita usuarios con varias membresías) y US2 (necesita login funcional).
- **US4 (P2)**: requiere US1 (para emitir nuevas invitaciones desde tenant admin), US2 (para que el tenant admin pueda autenticarse), y **Phase 4b** (para que el flujo de política "MFA obligatorio" lleve a un enrollment forzado real, no a una pantalla rota).
- **US5 (P3)**: requiere US1 (emite invitaciones desde el master) y US2 (el master debe poder loguearse). Opcionalmente Phase 4b si el master usa MFA.

### Within Each User Story

- Commands/Handlers (capa Application) antes de endpoints Carter.
- Endpoints antes de UI Blazor que los consume.
- Tests de Application en paralelo con la implementación (no estricto TDD, pero ambos antes de marcar la US como cerrada).
- Tests de integración como último paso de la story (validan end-to-end).

### Parallel Opportunities

- **Phase 1**: T002, T003, T004 paralelos.
- **Phase 2 — Domain entities/VOs**: T005-T010 paralelos (archivos distintos). T011-T013 paralelos.
- **Phase 2 — DDL**: T015, T016 paralelos (T014 primero por ser referencia FK). T018 paralelo.
- **Phase 2 — EF Configs**: T020-T024 paralelos (mismo módulo, archivos distintos). T025 paralelo (otro path).
- **Phase 2 — Application abstractions**: T029-T032 paralelos.
- **Phase 2 — Infra impls**: T035, T036, T037, T038, T039, T040 paralelos (todos distintos archivos y no se referencian entre sí). T033 → T034 → T041 secuencial. T042 paralelo.
- **Phase 2 — Middleware/filters**: T045, T046, T047 paralelos. T044 antes de T048. T049, T050 paralelos.
- **Cada User Story**: los commands/queries marcados [P] son archivos distintos; sus handlers, validators y tests pueden trabajarse por personas distintas si hay equipo.

---

## Parallel Example: User Story 1

```bash
# Tras completar T048 (DI principal) y T050 (architecture tests) — Foundational en verde:
# El equipo de US1 puede arrancar 5 tareas en paralelo:
Task: "T051 [US1] IssueTenantInvitationCommand + handler"
Task: "T052 [US1] IssueMasterInvitationCommand + handler"
Task: "T053 [US1] PreviewInvitationQuery + handler"
Task: "T055 [US1] RevokeInvitationCommand + handler"
Task: "T057 [US1] InvitationEmailDispatcher service"
# (T054 — AcceptInvitationCommand — depende de T056 TenantUserProvisioner; arranca cuando esa esté lista)

# Una vez listos los handlers, dos personas pueden tomar simultáneamente:
Task: "T058 [US1] InvitationsModule.cs Carter"
Task: "T064 [US1] AcceptInvitation.razor"   # mockea endpoint hasta que T058 esté integrado

# Tests en paralelo:
Task: "T059 AcceptInvitationCommandHandler tests"
Task: "T060 IssueTenantInvitation validator tests"
Task: "T061 RevokeInvitation handler tests"
```

---

## Implementation Strategy

### MVP First (US1 + US2 + Phase 4b)

1. Completar **Phase 1** (Setup) — ~½ día.
2. Completar **Phase 2** (Foundational) — ~3-4 días con un dev, 2 días con dos devs trabajando en paralelo sobre los grupos marcados [P].
3. Completar **Phase 3** (US1) — ~3 días.
4. Completar **Phase 4** (US2) — ~2 días.
5. Completar **Phase 4b** (Profile & Recovery) — ~2-3 días.
6. **STOP y VALIDAR**: ejecutar `quickstart.md` US1 + US2 + casos de Phase 4b (enroll MFA, cambiar password, reset). Cooperativa piloto puede operar end-to-end con esto, incluyendo gestión autónoma de credenciales.
7. Demo / deploy MVP.

### Entrega incremental post-MVP

1. **Phase 5 (US3)** — habilita asesores y consultores multi-empresa. ~2 días.
2. **Phase 6 (US4)** — devuelve autonomía operativa a cada cooperativa (incluye política MFA obligatorio que ahora sí funciona end-to-end gracias a Phase 4b). ~3 días.
3. **Phase 7 (US5)** — formaliza el onboarding de nuevas cooperativas. ~2 días.
4. **Phase 8 (Polish)** — observabilidad + carga + docs. ~2 días.

### Parallel Team Strategy

Con dos developers tras Phase 2:

- **Dev A**: US1 → US3 → US5 (línea de invitaciones y master admin).
- **Dev B**: US2 → Phase 4b → US4 (línea de auth + gestión de tenant + profile/recovery).
- Sincronización en US4 cuando necesite `IssueTenantInvitationCommand` (de US1) y el enrollment forzado (de Phase 4b) — coordinar mocking temporal.

Con tres developers tras Phase 2: Dev C puede tomar Phase 4b en paralelo con US2 (Dev B) y US1 (Dev A), reduciendo el camino crítico al MVP a ~5 días en vez de 7.

---

## Notes

- **Tests no son opcionales en este proyecto** (constitución, principio I — Spec-First Development). Cada handler con `*Handler.cs` debe tener su `*HandlerTests.cs` antes de marcar la tarea como completada.
- **Soft-delete y auditoría son automáticos** vía `AuditableEntity` + `AuditBehavior`; ninguna tarea las gestiona "a mano".
- **Las migraciones SQL son idempotentes y reversibles** (principio XII) excepto T017 que está marcada explícitamente como destructiva con header de warning.
- **Cero `try { } catch { }` silenciado** en código nuevo (principio IX); `LoggingBehavior` ya cubre la traza.
- **El refactor `SEC_Users` es irreversible**: ejecutar T017 solo sobre BD virgen. En entornos con datos legacy queda fuera del alcance (spec dice "sin migración").
- **El JWT siempre lleva `active_tenant_id`** una vez seleccionada empresa; el `TenantResolutionMiddleware` rechaza endpoints de tenant cuando ese claim falta (T044).
- **Stop at any checkpoint to validate** — los cuatro checkpoints (post-Foundational, post-US1, post-US2 MVP, post-US3, post-US4, post-US5) son puntos de demo válidos.
