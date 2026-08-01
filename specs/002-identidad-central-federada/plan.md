# Implementation Plan: Identidad Central con Autorización Federada por Empresa

**Branch**: `002-identidad-central-federada` | **Date**: 2026-05-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-identidad-central-federada/spec.md`

## Summary

Migrar el modelo de identidad del ERP de **autenticación por tenant con selector manual de cliente** a **identidad central única con autorización federada por empresa**. La credencial (email + contraseña + MFA) vive una sola vez en la BD `IngenIA365ERP_Admin`; cada tenant conserva intacto su modelo de roles/perfiles/permisos. El usuario inicia sesión en un único punto de entrada (`app.ingenia365.com`), el sistema resuelve sus membresías activas y (1) entra directo si solo tiene una, (2) muestra selector si tiene varias, (3) bloquea si no tiene ninguna. Dentro de la sesión activa, un control en el encabezado permite cambiar de empresa sin re-loguearse. El alta de nuevos usuarios es exclusivamente por invitación por correo (con token criptográfico de un solo uso, válido 7 días). El admin master designa admins de empresa; los admins de empresa invitan usuarios regulares de su empresa, asignan roles dentro del modelo existente del tenant, y entre pares pueden promover/degradar otros admins respetando la salvaguarda del "último admin activo".

Aproximación técnica: introducir una capa `CentralIdentity` en `IngenIA365ERP.Identity` que envuelva **ASP.NET Core Identity** sobre la base `IngenIA365ERP_Admin` con un store custom (`CentralUserStore` mapeando a `ADM_CentralUsers`), expuesta detrás de la interfaz **`ICentralIdentityProvider`** (login, register-from-invitation, change password, MFA enroll/verify, validate-against-pwned). Esta interfaz se inyecta desde Application; la implementación concreta (`AspNetCoreIdentityProvider`) reside en Infrastructure y puede sustituirse en el futuro por un adaptador a Entra External ID sin tocar los handlers. Las entidades nuevas `CentralUser`, `TenantMembership`, `Invitation`, `TenantMfaPolicy` viven en `ADM_*` (no se duplican en tenant). La entidad existente `User` de cada tenant se **refactoriza**: se le añade `CentralUserId Guid` (FK lógica), se eliminan `PasswordHash`, `PasswordSalt`, `MfaSecret`, `MustChangePassword`, `FailedLoginAttempts`, `LockoutEndAt` (responsabilidad delegada al centro); `Username`, `Email`, `IsActive`, `IsMfaEnabled`, `LastLoginAt` quedan como **denormalizaciones de lectura** para listados internos. El `TenantResolutionMiddleware` se reescribe para resolver el tenant desde el claim `active_tenant_id` del JWT (no desde header ni subdominio), y el JWT pasa a incluir `central_user_id`, `email`, `is_global_master_admin` y `active_tenant_id`. El cambio de empresa re-emite el JWT con el nuevo `active_tenant_id`. La autorización por permisos sigue resolviéndose por tenant (`CompositePermissionPolicyProvider` existente, consultado contra la BD del tenant activo). Los invitations se envían por **SMTP propio** (reutilizando `NotificationEmailSender` ya existente en `IngenIA365ERP.Storage`) detrás de `IEmailSender`. Las contraseñas se validan contra **HaveIBeenPwned Pwned Passwords v3 API** con k-anonymity (hash SHA-1 → primeros 5 chars al endpoint, comparación local del sufijo).

## Technical Context

**Language/Version**: C# 13 sobre .NET 10.0.5 (alineado con `IngenIA365ERP.sln` y el plan de Fase 0).

**Primary Dependencies**:
- **Nuevas**:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.x — store base para `CentralUser` sobre la BD Admin.
  - `Microsoft.AspNetCore.Identity` 10.x — `UserManager<CentralUser>`, `SignInManager<CentralUser>`, password hasher PBKDF2 (override a BCrypt vía hasher custom para alinear con principio constitucional sobre hashing).
  - `HaveIBeenPwned.Client` (o llamada HTTP directa con `HttpClient`) — comprobación de contraseñas comprometidas vía k-anonymity. Sin nuevas dependencias si se hace con `HttpClient` nativo.
- **Reutilizadas (ya presentes en repo)**: Carter, MediatR, FluentValidation, `BCrypt.Net-Next` 4.x (hashing), `Otp.NET` 1.4.x (TOTP), `System.IdentityModel.Tokens.Jwt` 8.x (JWT RS256), MongoDB.Driver 3.9 (auditoría), Serilog 4.x, EF Core 10.x, MailKit 4.x (SMTP), QuestPDF 2024.x.
- **Frontend**: Blazor Hybrid existente (MAUI + Web + WebAssembly) + SyncFusion 33.1.44.

**Storage**:
- **SQL Server — BD `IngenIA365ERP_Admin`** (existente): nuevas tablas `ADM_CentralUsers`, `ADM_CentralUserRoles` (ASP.NET Identity standard), `ADM_TenantMemberships`, `ADM_Invitations`, `ADM_TenantMfaPolicies`, `ADM_CentralUserLoginAttempts`. Las migraciones cumplen el principio XII (idempotentes, reversibles, numeradas).
- **SQL Server — schema por tenant** (existente): refactor de `SEC_Users` para sumar `CentralUserId UNIQUEIDENTIFIER` y retirar columnas de credenciales (`PasswordHash`, `PasswordSalt`, `MfaSecret`, `FailedLoginAttempts`, `LockoutEndAt`, `MustChangePassword`, `IsEmailVerified`, `LegacyLogin`). Estas eliminaciones son admisibles porque la spec declara **"instalación nueva"** (sin migración de datos legacy, Sección "Sin migración" del prompt original).
- **MongoDB** (existente): auditoría en `audit_events` recibe eventos nuevos (`CentralUser.Login.Success`, `CentralUser.Login.Failed`, `Invitation.Issued/Accepted/Expired/Revoked`, `Membership.Activated/Suspended/Revoked`, `Membership.Promoted/Demoted`, `TenantMfaPolicy.Activated/Deactivated`, `Session.TenantSwitched`) con la retención SARLAFT (5 años) heredada de la colección.
- **Redis** (existente): cache de membresías activas por `CentralUserId` (TTL 60s) para evitar round-trip a SQL en cada petición; lista negra de tokens revocados; lock distribuido para aceptación concurrente de invitación.

**Testing**:
- `IngenIA365ERP.Domain.Tests` — entidades nuevas y máquina de estados de `TenantMembership` y `Invitation` con FluentAssertions.
- `IngenIA365ERP.Application.Tests` — validators y handlers (login, accept-invitation, switch-tenant, invite-user, promote/demote admin) con NSubstitute mockeando `ICentralIdentityProvider` y `IEmailSender`.
- `IngenIA365ERP.API.IntegrationTests` — escenarios end-to-end con WebApplicationFactory + Testcontainers (SQL Server + MongoDB efímeros): flujo completo de invitación → registro → login → entrada → cambio de empresa → revocación.
- `IngenIA365ERP.Architecture.Tests` — extender reglas ArchUnitNET: (a) `ICentralIdentityProvider` solo accesible vía Application, (b) ningún handler invoca `UserManager<CentralUser>` directamente, (c) ningún DTO público expone `CentralUserId` (debe exponerse `PublicId`).
- `IngenIA365ERP.Load.Tests` — escenario NBomber: 100 logins/seg sostenidos durante 5 min sobre `POST /api/auth/login`.

**Target Platform**: Servidor .NET 10 en VPS Linux (contenedor Docker); cliente Blazor Web (Chromium/Edge/Firefox); MAUI desktop opcional para administradores. Dominio único `app.ingenia365.com` con HTTPS obligatorio.

**Project Type**: Web service multi-capa (backend API + frontend Blazor + admin panel) bajo la Clean Architecture existente.

**Performance Goals**:
- SC-002: login → dashboard de empresa única en < 5 s end-to-end (incluye 1 round-trip a Pwned Passwords solo si está habilitado para el flujo de cambio de contraseña; el login no consulta Pwned).
- SC-003: cambio de empresa en sesión activa en < 3 s desde click hasta dashboard nuevo.
- p95 < 800 ms en `POST /api/auth/login` con 100 concurrent users (validación contra `CentralUser` + carga de membresías desde Redis cache).
- p95 < 500 ms en `POST /api/sessions/switch-tenant` (recarga de tenant context).

**Constraints**:
- Aislamiento estricto (principio IV): toda query a una BD de tenant DEBE ocurrir bajo el `active_tenant_id` del JWT validado. Cero cross-tenant joins. Los handlers de `Admin/*` (los que tocan `ADM_*`) NEVER consultan datos de tenants ajenos.
- Cifrado en reposo: la columna `MfaSecret` de `ADM_CentralUsers` se cifra con `IDataProtectionProvider` (clave en KMS local del VPS) — no se almacena en claro.
- Tokens de invitación: 32 bytes aleatorios (`RandomNumberGenerator.GetBytes(32)`) → Base64Url, almacenado solo como hash SHA-256 en `ADM_Invitations.TokenHash` (defensa frente a leak de BD).
- BCrypt cost ≥ 11 (constitución) — el `IPasswordHasher<CentralUser>` se reemplaza por implementación BCrypt para mantener alineación con Fase 0.
- Mensajes de error en español con códigos namespaced (`Identity.InvalidCredentials`, `Invitation.Expired`, `Membership.NotActive`, `Tenant.MfaPolicyEnforced`).
- Validación contra Pwned Passwords es **best-effort no bloqueante** si el servicio externo falla (timeout 2 s; fallo → permite la contraseña pero loggea Warning); evita acoplar el flujo crítico de registro/cambio a la disponibilidad de un servicio externo.

**Scale/Scope**:
- 100 cooperativas tenants (mismo horizonte de la Fase 0) × ~50 usuarios promedio = ~5.000 `CentralUser` registros en Admin DB.
- ~10.000 `TenantMembership` (algunos usuarios multi-empresa).
- ~500 `Invitation` activas concurrentes en pico (cuando una cooperativa hace un alta masiva inicial).
- 100 logins concurrentes sostenidos (objetivo de carga compartido con Fase 0).
- **7 entidades nuevas (`CentralUser`, `TenantMembership`, `Invitation`, `TenantMfaPolicy`, `CentralUserLoginAttempt`, `PasswordResetToken`) + 1 refactor de `SEC_Users`** (ver `data-model.md`).
- **28 endpoints REST nuevos / refactorizados** bajo `/api/auth`, `/api/sessions`, `/api/profile`, `/api/invitations`, `/api/admin/...`, `/api/tenants/{publicId}/...`, `/api/saas/...` (ver `contracts/`).
- **11 páginas Blazor nuevas / refactorizadas**: login, registro desde invitación, selector de empresa, switcher en header, gestión de invitaciones (admin de empresa), consola de tenants (master admin), enrollment MFA (voluntario y forzado), cambio de contraseña, forgot/reset password.

## Constitution Check

*GATE: Debe pasar antes de Phase 0 research. Re-check tras Phase 1 design.*

| #    | Principio                                              | Estado | Nota |
|------|--------------------------------------------------------|--------|------|
| I    | Spec-First | PASS | Constitution → Spec → Clarifications (5 Q/A) → Plan. Cero código antes de cerrar este plan. |
| II   | Clean Architecture | PASS | `ICentralIdentityProvider` definida en Application; implementación `AspNetCoreIdentityProvider` aislada en Infrastructure/Identity. Domain solo agrega `CentralUser` (POCO), `TenantMembership`, `Invitation`, `TenantMfaPolicy` + value objects (`Email`, `InvitationToken`, `MembershipStatus`). Cero referencias a EF/Identity/Carter en Domain. Architecture.Tests extendidos para verificarlo. |
| III  | CQRS + MediatR | PASS | 22 nuevos endpoints son Carter modules que solo reenvían a `ISender`. Cada Command lleva validator hermano (`LoginCommandValidator`, `AcceptInvitationCommandValidator`, etc.). Pipeline behaviors (ValidationBehavior, AuditBehavior, LoggingBehavior, PerformanceBehavior) aplican uniformemente. NEVER se invoca `UserManager<CentralUser>` desde un endpoint — siempre vía handler → `ICentralIdentityProvider`. |
| IV   | Multi-tenancy schema-per-tenant | PASS (con ajuste de resolución) | El `TenantResolutionMiddleware` se refactoriza: **deja de leer header `X-Tenant-Id` y subdominio** y pasa a leer el claim `active_tenant_id` del JWT validado. Esto es un cambio en el **mecanismo** de resolución pero **respeta el espíritu** del principio IV (cero queries sin tenant context). Toda query a tablas de tenant sigue ejecutándose bajo el `ITenantContext` resuelto por el middleware. Las queries a tablas `ADM_*` (Admin DB) usan un DbContext separado `AdminDbContext` que NO requiere tenant (son globales). Architecture test garantiza que las tablas de tenant nunca se consultan sin tenant context. |
| V    | Person centralizada | PASS | Esta fase no toca `COR_People`. La eliminación de columnas de credenciales del `SEC_Users` (per-tenant) no afecta a Person — los datos personales siguen en `COR_People`, y `SEC_Users.PersonId` se conserva intacto. |
| VI   | PublicId externo / Id interno | PASS con excepción justificada | Las entidades nuevas en Admin DB que NO son ASP.NET Identity (`TenantMembership`, `Invitation`, `TenantMfaPolicy`) siguen el patrón `int Id` + `Guid PublicId`. La entidad `CentralUser` que hereda de `IdentityUser<Guid>` usa **un único `Guid Id`** que actúa simultáneamente como PK y como identificador público (es lo que ASP.NET Identity impone). Esto se documenta en **Complexity Tracking** como excepción justificada por convención del framework. Toda exposición externa de `CentralUser` ocurre por su `Id` (Guid no-secuencial → cumple el objetivo anti-enumeración del principio VI). Architecture test verifica que ningún DTO público expone `int Id` interno de las entidades non-Identity. |
| VII  | Soft-delete + auditoría | PASS | `CentralUser`, `TenantMembership`, `Invitation`, `TenantMfaPolicy` heredan `AuditableEntity` (con `IsDeleted`, `DeletedAt`, `DeletedBy`). Las configuraciones EF declaran `HasQueryFilter(e => !e.IsDeleted)`. `ADM_CentralUserLoginAttempts` es append-only por diseño (sin soft-delete) — excepción admitida como tabla de telemetría inmutable. |
| VIII | Validación dual (frontend + backend) | PASS | Cada formulario Blazor (login, registro desde invitación, invitar, cambiar empresa, configurar empresa por defecto) usa `EditForm` + revalidación; cada Command tiene `FluentValidation` validator. Mensajes en español con códigos `Identity.*`, `Invitation.*`, `Membership.*`, `Tenant.*`. |
| IX   | Errores visibles | PASS | Todos los handlers retornan `Result<T>.Failure(code, message)`. NEVER `try { } catch { }` silenciado. `LoggingBehavior` loggea toda excepción con contexto (CentralUserId, TenantId, Operation, IP). Architecture test prohíbe catch vacío en código nuevo. La validación Pwned Passwords es la única excepción permitida — su fallo NO bloquea el flujo pero queda en Serilog como Warning con `Pwned.UnavailableFallback`. |
| X    | Trazabilidad SIPLA/SARLAFT | PASS | Todos los Commands de esta fase pasan por `AuditBehavior`, generando eventos en `audit_events` con TTL 5 años. Eventos clave: `CentralUser.*`, `Invitation.*`, `Membership.*`, `Session.TenantSwitched`, `TenantMfaPolicy.*`. Sin Command que escape al pipeline MediatR — verificado por Architecture test. |
| XI   | Inmutabilidad contable | N/A | Esta fase no introduce ni toca movimientos contables. |
| XII  | Migraciones idempotentes y reversibles | PASS | DDL nuevas en `database/schema/` (15a — Admin Central Identity; 15b — Admin Memberships & Invitations; 15c — Admin Mfa Policy & Login Attempts; 15d — Per-Tenant Users Refactor). Cada archivo lleva header con contexto + problema + reversión + nota de "requiere backup". El refactor de `SEC_Users` (drop de columnas) es destructivo y declara explícitamente "irreversible sin restauración de backup" — admitido porque la spec declara "instalación nueva, sin migración de datos existentes". Las migraciones EF Core que provisionan las tablas de Identity se generan con `--idempotent` y se versionan junto al DDL. |

**Resultado**: **11/12 PASS + 1 N/A**, con una **excepción justificada** documentada en Complexity Tracking (Id de `CentralUser` por convención de ASP.NET Identity). Re-evaluación tras Phase 1 al final del documento.

## Project Structure

### Documentation (this feature)

```text
specs/002-identidad-central-federada/
├── plan.md                # Este documento
├── spec.md                # Especificación funcional (con clarifications)
├── research.md            # Phase 0: decisiones técnicas
├── data-model.md          # Phase 1: entidades, atributos, relaciones, máquina de estados
├── quickstart.md          # Phase 1: guía end-to-end de los 5 user stories
├── contracts/             # Phase 1: contratos REST
│   ├── auth.md
│   ├── sessions.md
│   ├── invitations.md
│   ├── memberships.md
│   ├── tenant-mfa-policy.md
│   └── saas-admin.md
├── checklists/
│   └── requirements.md    # (existente, validado)
└── tasks.md               # Phase 2: lo genera /speckit-tasks
```

### Source Code (repository root — layout existente, extensiones marcadas con +)

```text
src/
├── Core/
│   ├── IngenIA365ERP.Domain/
│   │   ├── Common/                              # AuditableEntity (existente — reutilizado)
│   │   ├── Entities/
│   │   │   ├── Admin/                           # Tenant, Subscription, TenantSetting (existentes)
│   │   │   │   + CentralUser.cs                 # Identidad central (POCO, sin atributos Identity)
│   │   │   │   + TenantMembership.cs            # Relación N:N persona ↔ empresa
│   │   │   │   + Invitation.cs                  # Invitación con token de un solo uso
│   │   │   │   + TenantMfaPolicy.cs             # Política "MFA obligatorio" por tenant
│   │   │   │   + CentralUserLoginAttempt.cs     # Telemetría append-only para protección anti-bruteforce
│   │   │   └── Security/                        # Existente (User será refactorizado, no eliminado)
│   │   └── ValueObjects/                        # + Email.cs (normalizado), InvitationToken.cs, MembershipStatus.cs
│   └── IngenIA365ERP.Application/
│       ├── Common/Abstractions/                 # Existente
│       │   + ICentralIdentityProvider.cs        # Interfaz central (login, register, MFA, password)
│       │   + IEmailSender.cs                    # Promovida desde Storage; redefinida con plantilla "invitación"
│       │   + IPwnedPasswordService.cs           # Comprobación HaveIBeenPwned
│       │   + ITenantMembershipReader.cs         # Lectura cacheada de membresías por CentralUserId
│       ├── Identity/
│       │   + Auth/                              # LoginCommand, MfaVerifyCommand, RefreshTokenCommand, LogoutCommand
│       │   + Sessions/                          # SelectTenantCommand, SwitchTenantCommand, GetMyActiveTenantsQuery, SetDefaultTenantCommand
│       │   + Profile/                           # BeginMfaEnrollmentCommand, ConfirmMfaEnrollmentCommand, DisableMfaCommand, ChangePasswordCommand, RequestPasswordResetCommand, ResetPasswordCommand, SetDefaultTenantCommand
│       ├── Invitations/
│       │   + IssueTenantInvitationCommand.cs    # Por admin de empresa
│       │   + IssueMasterInvitationCommand.cs    # Por master admin (puede marcar IsTenantAdmin)
│       │   + RevokeInvitationCommand.cs
│       │   + AcceptInvitationCommand.cs         # Con token; resuelve "nuevo" vs "existente"
│       │   + PreviewInvitationQuery.cs          # GET por token sin consumir
│       ├── Memberships/
│       │   + SuspendMembershipCommand.cs
│       │   + ActivateMembershipCommand.cs       # Suspended → Active
│       │   + RevokeMembershipCommand.cs
│       │   + PromoteToTenantAdminCommand.cs
│       │   + DemoteFromTenantAdminCommand.cs    # con guardrail FR-040 (último admin)
│       │   + ListTenantMembersQuery.cs
│       ├── Tenants/
│       │   + UpdateTenantMfaPolicyCommand.cs    # MFA obligatorio on/off
│       │   + GetTenantMfaPolicyQuery.cs
│       └── Saas/                                # Master admin
│           + ListAllTenantsQuery.cs
│           + RegisterTenantCommand.cs           # (refactor del existente para integrar con master flow)
├── Infrastructure/
│   ├── IngenIA365ERP.Identity/                  # Refactor mayor
│   │   + Models/CentralUserIdentity.cs          # IdentityUser<Guid> bridge a CentralUser (POCO Domain)
│   │   + Stores/CentralUserStore.cs             # Mapeo IdentityUser ↔ Domain CentralUser
│   │   + Providers/AspNetCoreIdentityProvider.cs # Implementa ICentralIdentityProvider
│   │   + Hashers/BcryptPasswordHasher.cs        # Sustituye PBKDF2 default por BCrypt cost 11
│   │   + Services/PwnedPasswordService.cs       # K-anonymity contra HaveIBeenPwned API
│   │   + Services/CentralJwtIssuer.cs           # Emite/refresca JWT con central_user_id + active_tenant_id
│   │   + Services/TenantMembershipReader.cs     # Lectura cacheada en Redis
│   │   + Configuration/CentralIdentityConfiguration.cs
│   │   + DependencyInjection.cs                 # AddCentralIdentity() — wires Identity Core sin scaffolding UI
│   ├── IngenIA365ERP.Persistence/
│   │   + Contexts/AdminDbContext.cs             # DbContext separado para ADM_* (no tenant-aware)
│   │   + Configuration/Admin/CentralUserConfiguration.cs
│   │   + Configuration/Admin/TenantMembershipConfiguration.cs
│   │   + Configuration/Admin/InvitationConfiguration.cs
│   │   + Configuration/Admin/TenantMfaPolicyConfiguration.cs
│   │   + Configuration/Admin/CentralUserLoginAttemptConfiguration.cs
│   │   + Migrations/Admin/                      # Migraciones EF para ADM_*
│   │   ~ Configuration/Security/UserConfiguration.cs  # Modificado: añade CentralUserId, retira columnas de credencial
│   ├── IngenIA365ERP.Caching/
│   │   + Services/TenantMembershipCache.cs      # TTL 60s, invalidación por membership-change events
│   └── IngenIA365ERP.Storage/                   # Existente
│       ~ Services/NotificationEmailSender.cs    # Refactor: implementa IEmailSender con plantilla "invitación"
│       + Templates/InvitationEmail.html         # Plantilla HTML en español
│       + Templates/PasswordResetEmail.html      # Reutilizable
└── Presentation/
    ├── IngenIA365ERP.API/
    │   ~ Modules/AuthModule.cs                  # Refactor: login sin tenant, mfa-verify, refresh, logout
    │   + Modules/SessionsModule.cs              # POST /api/sessions/select-tenant, switch-tenant; GET active-tenants
    │   + Modules/InvitationsModule.cs           # CRUD de invitaciones
    │   + Modules/MembershipsModule.cs           # Suspender/activar/revocar + promote/demote
    │   + Modules/TenantMfaPolicyModule.cs
    │   + Modules/SaasAdminModule.cs             # Listado global de tenants (master only)
    │   ~ Middleware/TenantResolutionMiddleware.cs  # Refactor: lee claim active_tenant_id
    │   + Middleware/CentralIdentityChallengeMiddleware.cs  # Maneja 401 → desafío de login central
    │   + Filters/RequireTenantAdminAttribute.cs # Verifica IsTenantAdmin del active_tenant_id
    │   + Filters/RequireMasterAdminAttribute.cs # Verifica is_global_master_admin del claim
    ├── IngenIA365ERP.Web/                       # Blazor Server admin/funcional
    │   ~ Pages/Auth/Login.razor                 # Refactor: SIN combo box; solo email + password (+ MFA si aplica)
    │   + Pages/Auth/AcceptInvitation.razor      # Formulario: si new → registro; si exists → autenticar y confirmar
    │   + Pages/Auth/SelectTenant.razor          # Selector cuando hay varias membresías activas
    │   + Pages/Auth/NoMembershipNotice.razor    # "Solicita una invitación"
    │   + Pages/Profile/DefaultTenantSetting.razor
    │   + Pages/Admin/Tenant/Invitations.razor   # Admin de empresa: invitar, revocar, listar pendientes
    │   + Pages/Admin/Tenant/MfaPolicy.razor     # Toggle "MFA obligatorio"
    │   + Pages/Admin/Tenant/Members.razor       # Listar miembros + promote/demote/suspend/revoke
    │   + Pages/Saas/Tenants.razor               # Master admin: catálogo de tenants
    │   + Pages/Saas/MasterInvitations.razor     # Invitar admin de empresa
    └── IngenIA365ERP.Web.Client/                # Componentes WASM compartidos
        + Components/Header/TenantSwitcher.razor # Selector siempre visible en header (si > 1 membresía activa)
        + Services/TenantSessionClient.cs        # GET /api/sessions/active-tenants + POST switch-tenant
        + Services/InvitationClient.cs

tests/
├── IngenIA365ERP.Domain.Tests/
│   + Admin/TenantMembershipStateMachineTests.cs   # Verifica FR-009a (transiciones válidas)
│   + Admin/InvitationStateMachineTests.cs
│   + Admin/CentralUserTests.cs
├── IngenIA365ERP.Application.Tests/
│   + Identity/Auth/LoginCommandHandlerTests.cs
│   + Identity/Sessions/SwitchTenantCommandHandlerTests.cs
│   + Invitations/AcceptInvitationCommandHandlerTests.cs  # Caso nuevo + caso existente + caso expirado + caso revocado + concurrencia
│   + Invitations/IssueTenantInvitationCommandValidatorTests.cs
│   + Memberships/PromoteToTenantAdminCommandHandlerTests.cs
│   + Memberships/DemoteLastAdminGuardTests.cs            # FR-040
│   + Tenants/UpdateTenantMfaPolicyCommandHandlerTests.cs
├── IngenIA365ERP.API.IntegrationTests/
│   + Identity/EndToEnd_InviteRegisterLogin.cs
│   + Identity/EndToEnd_MultiTenantSwitch.cs
│   + Identity/EndToEnd_MfaPolicyEnforcement.cs
│   + Identity/Security_TenantCrossover.cs               # Verifica que no se puede operar sobre tenant sin membership active
│   + Identity/Security_InvitationReplay.cs              # Verifica FR-030 (no reuso)
├── IngenIA365ERP.Architecture.Tests/
│   + CentralIdentityArchTests.cs                        # ICentralIdentityProvider solo accesible vía Application
│   + AdminDbContextArchTests.cs                         # AdminDbContext NEVER usado en handlers de tenant
└── IngenIA365ERP.Load.Tests/
    + Identity/LoginThroughputScenario.cs                # NBomber 100 logins/seg × 5 min

database/
├── schema/
│   + 15a_Admin_CentralIdentity.sql       # ADM_CentralUsers, ADM_CentralUserRoles, ADM_CentralUserClaims (ASP.NET Identity standard)
│   + 15b_Admin_Memberships_Invitations.sql # ADM_TenantMemberships, ADM_Invitations
│   + 15c_Admin_MfaPolicy_LoginAttempts.sql # ADM_TenantMfaPolicies, ADM_CentralUserLoginAttempts
│   + 15d_Tenant_Users_Refactor.sql       # Refactor de SEC_Users: drop credentials, add CentralUserId
└── migration/
    + 16_Seed_Default_GlobalMasterAdmin.sql # Idempotente; crea un master admin inicial si la BD está vacía
```

**Structure Decision**: se conserva la **Clean Architecture en cuatro capas** existente. La identidad central vive en `IngenIA365ERP.Identity` (Infrastructure) detrás de `ICentralIdentityProvider` definida en `IngenIA365ERP.Application`. La persistencia usa un **DbContext separado `AdminDbContext`** (paralelo al tenant-aware `ErpDbContext`) para que las queries `ADM_*` nunca dependan del `ITenantContext` (que ahora puede ser nulo durante el login). El `TenantResolutionMiddleware` se refactoriza para leer el claim `active_tenant_id`; el JWT añade tres claims nuevos. Las páginas Blazor de identidad viven en `IngenIA365ERP.Web/Pages/Auth/*`; el switcher en header vive en `IngenIA365ERP.Web.Client/Components/Header/TenantSwitcher.razor` para que sea reutilizable entre Web y MAUI. No se introducen proyectos nuevos.

## Phase 0 — Outline & Research

Resuelta en [`research.md`](./research.md). Decisiones tomadas:

- **ASP.NET Core Identity como base de la implementación de `ICentralIdentityProvider`** (alineado con la decisión del prompt). Custom `IdentityUser<Guid>` bridge a `CentralUser` POCO de Domain. PBKDF2 default sustituido por BCrypt cost 11 vía `IPasswordHasher<CentralUserIdentity>` custom para mantener alineación con Fase 0.
- **JWT estructura**: claims `sub` (= `central_user_id`), `email`, `is_global_master_admin`, `active_tenant_id`, `tenant_admin` (bool resuelto desde membership), `mfa_verified` (bool), `iat`, `exp`. Access 30 min, refresh 12 h (mismo esquema que Fase 0). Cambio de empresa → re-emisión completa (nuevo `iat`, nuevo `active_tenant_id`).
- **Comprobación de contraseñas comprometidas**: HaveIBeenPwned Pwned Passwords v3 API (gratuita, k-anonymity, no requiere API key). Llamada solo en registro y cambio de contraseña (NO en login — performance). Timeout 2 s. Si falla, log Warning + permite la contraseña (fail-open por disponibilidad, fail-closed sería peor UX y solo aplica a contraseñas que ya están comprometidas — la longitud mínima 12 ofrece base de seguridad).
- **Token de invitación**: 32 bytes `RandomNumberGenerator.GetBytes(32)` → Base64Url (= 43 chars URL-safe). Solo el hash SHA-256 se guarda en BD. La URL del correo es `https://app.ingenia365.com/auth/accept-invitation?token={base64url}`. Single-use atómico mediante optimistic locking (RowVersion en `ADM_Invitations`).
- **Concurrencia en aceptación**: lock distribuido en Redis (key `invitation:{id}`, TTL 30s) + verificación final con UPDATE condicional `WHERE Status = 'Pending' AND RowVersion = @x`. Si dos clientes intentan aceptar al mismo tiempo, uno gana, el otro recibe `Invitation.AlreadyAccepted`.
- **Refresh de membresías en caché**: TTL 60s en Redis (key `memberships:{centralUserId}`); invalidación explícita al activar/suspender/revocar (publish a `IMembershipChangedNotifier`).
- **Email provider v1**: SMTP propio vía MailKit (ya disponible en `IngenIA365ERP.Storage.NotificationEmailSender`). Se promueve `IEmailSender` desde Storage a Application como abstracción de primer orden. Plantillas HTML en `IngenIA365ERP.Storage/Templates/` con interpolación simple.
- **Auditoría de invitaciones, membresías, sesiones**: 12 nuevos `AuditEventType` declarados en `Application.Common.Audit.AuditEventTypes` (constantes); el `AuditBehavior` existente los captura automáticamente al pasar por MediatR.
- **Salvaguarda "último admin"**: implementada en `DemoteFromTenantAdminCommandHandler` y `RevokeMembershipCommandHandler` con consulta `COUNT(*) WHERE TenantId = @t AND IsTenantAdmin = 1 AND Status = 'Active'` → si ≤1 y es el afectado → retornar `Membership.LastAdminProtected`.
- **Política MFA por tenant**: tabla `ADM_TenantMfaPolicies` (TenantId PK, IsRequired bool, ActivatedAt, ActivatedByUserId). Login flow consulta política para todos los tenants donde el usuario es miembro activo; si AL MENOS UNO requiere MFA → exigir MFA en login central (FR-003c).
- **Migración del legacy `User` per-tenant**: se documenta el procedimiento "instalación nueva" — el script `15d_Tenant_Users_Refactor.sql` se ejecuta sobre BD virgen; cualquier intento de aplicarlo sobre datos existentes es responsabilidad del operador y queda fuera del alcance (assumption "Sin migración de usuarios" del spec).
- **Bloqueo progresivo**: contador en Redis por email (NO por IP — defensa contra DDoS distribuido); 5 fallos → bloqueo 1 min; 10 fallos → 5 min; 15 fallos → 15 min; 20+ → 60 min hasta reset administrativo. Reset administrativo: master admin desde consola.

## Phase 1 — Design & Contracts

### Data model

Detallado en [`data-model.md`](./data-model.md): 5 entidades nuevas (`CentralUser`, `TenantMembership`, `Invitation`, `TenantMfaPolicy`, `CentralUserLoginAttempt`) + 1 refactor (`SEC_Users`). Incluye atributos, índices, restricciones únicas, relaciones, validaciones y la máquina de estados completa de `TenantMembership` (transiciones FR-009a) y de `Invitation` (Pending → Accepted/Expired/Revoked).

### Contracts

Detallados en [`contracts/`](./contracts/). 22 endpoints REST agrupados:

| Módulo | Contrato | Endpoints clave |
|--------|----------|-----------------|
| Auth (central) | `contracts/auth.md` | `POST /api/auth/login`, `POST /api/auth/mfa/verify`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me` |
| Sessions (selección/cambio de empresa) | `contracts/sessions.md` | `GET /api/sessions/active-tenants`, `POST /api/sessions/select-tenant`, `POST /api/sessions/switch-tenant`, `PUT /api/profile/default-tenant` |
| Invitations | `contracts/invitations.md` | `POST /api/tenants/{tenantPublicId}/invitations`, `POST /api/saas/invitations`, `GET /api/invitations/{token}/preview`, `POST /api/invitations/accept`, `DELETE /api/invitations/{publicId}` |
| Memberships | `contracts/memberships.md` | `GET /api/tenants/{tenantPublicId}/members`, `POST /api/tenants/{tenantPublicId}/members/{publicId}/suspend`, `POST .../activate`, `POST .../revoke`, `POST .../promote-admin`, `POST .../demote-admin` |
| Tenant MFA Policy | `contracts/tenant-mfa-policy.md` | `GET /api/tenants/{tenantPublicId}/mfa-policy`, `PUT /api/tenants/{tenantPublicId}/mfa-policy` |
| Profile & Recovery | `contracts/profile-and-recovery.md` | `POST /api/profile/mfa/enroll`, `POST /api/profile/mfa/confirm`, `POST /api/profile/mfa/disable`, `POST /api/profile/password`, `POST /api/auth/password/forgot`, `POST /api/auth/password/reset` |
| SaaS admin | `contracts/saas-admin.md` | `GET /api/saas/tenants`, `POST /api/saas/tenants`, `POST /api/saas/users/{centralUserPublicId}/force-mfa-reset` |

Todos los endpoints requieren `RequireAuthorization` salvo:
- `POST /api/auth/login`
- `POST /api/auth/mfa/verify` (requiere un "challenge token" temporal, no JWT principal)
- `POST /api/auth/refresh`
- `POST /api/auth/password/forgot`
- `POST /api/auth/password/reset` (requiere el token de reset)
- `GET /api/invitations/{token}/preview`
- `POST /api/invitations/accept` (token de invitación; opcional JWT central para rama "sesión activa")

Todos retornan `Result<T>` traducido a HTTP por `ErrorEnvelopeFilter` con códigos namespaced en español.

### Quickstart

[`quickstart.md`](./quickstart.md) recorre los 5 user stories del spec con comandos `dotnet`, `curl`, capturas SQL y pasos en el panel Blazor, demostrando los acceptance scenarios y los criterios de éxito SC-001 a SC-012.

### Agent context update

Bloque `<!-- SPECKIT START --> ... <!-- SPECKIT END -->` de `CLAUDE.md` actualizado para apuntar a `specs/002-identidad-central-federada/plan.md` y sus artefactos.

## Constitution Re-check (post Phase 1 design)

Tras escribir `research.md`, `data-model.md`, `contracts/` y `quickstart.md`, las doce compuertas siguen en **PASS / N/A** con la única excepción justificada del Id de `CentralUser` (documentada). No se introducen dependencias cruzadas indebidas: Domain no toca ASP.NET Identity (solo Infrastructure lo hace), Application solo conoce `ICentralIdentityProvider`, los endpoints no contienen lógica de negocio, todo Command tiene validator + pasa por AuditBehavior. La refactorización del `TenantResolutionMiddleware` cambia el **mecanismo** de resolución de tenant pero conserva el principio (toda query a tablas de tenant ocurre bajo tenant context). El nuevo `AdminDbContext` es no-tenant-aware por diseño (sus tablas son globales del SaaS).

Sin nuevas entradas en Complexity Tracking más allá de la ya existente.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `CentralUser.Id` es `Guid` único (no patrón "int Id + Guid PublicId" del principio VI) | ASP.NET Core Identity impone un único campo `Id` como PK del `IdentityUser<TKey>`. Usar `TKey = Guid` cumple el objetivo anti-enumeración del principio VI (los Guids no son secuenciales adivinables). | Alternativa rechazada: implementar IdentityUser custom con dos columnas (`int Id` interno + `Guid PublicId` externo) — habría requerido reescribir el `UserManager`, `SignInManager`, `IUserStore`, `IUserPasswordStore`, `IUserLoginStore`, `IUserSecurityStampStore` y todos sus tests. Costo desproporcionado para una ganancia marginal (ningún Id se expone secuencialmente al exterior de todos modos, dado que es Guid). Mitigación: documentado explícitamente; Architecture test garantiza que ningún DTO público expone `int Id` de las entidades non-Identity. |
