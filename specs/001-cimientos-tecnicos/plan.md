# Implementation Plan: Fase 0 — Cimientos técnicos

**Branch**: `001-cimientos-tecnicos` | **Date**: 2026-05-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-cimientos-tecnicos/spec.md`

## Summary

Construir los cimientos transversales sobre los que correrá el módulo de Nómina y todos los módulos posteriores: aislamiento multi-empresa (cooperativa con NIT + sucursales) bajo schema-per-tenant, autenticación dual (contraseña BCrypt + MFA por TOTP) con políticas de expiración y reset administrativo doble, RBAC granular Entidad×Acción, audit log inmutable en MongoDB con retención SARLAFT (5 años) y exportación CSV + PDF firmado, soft-delete universal, adjuntos cifrados en reposo, servicio de notificaciones (correo + in-app), y registro de autorizaciones habeas data (Ley 1581).

Aproximación técnica: extender la base ya esqueletizada en `src/Infrastructure/IngenIA365ERP.Identity` y `src/Infrastructure/IngenIA365ERP.Audit` con los componentes faltantes (TOTP, códigos de respaldo, refresh token rotation, optimistic concurrency, exportador PDF firmado, almacén de adjuntos cifrado, servicio de notificaciones), exponer Carter endpoints bajo `/api/admin` y `/api/security`, e instalar de manera definitiva los pipeline behaviors `ValidationBehavior`, `AuditBehavior`, `LoggingBehavior` y `PerformanceBehavior` en `IngenIA365ERP.Application`. La Fase 0 termina con (a) panel de administración funcional, (b) consulta filtrada y exportable del audit log, (c) suite `Architecture.Tests` que blinda los principios constitucionales para las fases siguientes.

## Technical Context

**Language/Version**: C# 13 sobre .NET 10.0.5.

**Primary Dependencies**:
- Backend: Carter (Minimal APIs), MediatR (CQRS), FluentValidation, Microsoft.AspNetCore.Identity (solo como base; la mayor parte se reemplaza con dominio propio), BCrypt.Net-Next 4.x para hashing, `Otp.NET` 1.4.x para TOTP, `System.IdentityModel.Tokens.Jwt` 8.x para JWT RS256, MongoDB.Driver 3.9.0 para auditoría, QuestPDF 2024.x para PDF firmado del audit log, Serilog 4.x para logging, Microsoft.AspNetCore.DataProtection para sello de adjuntos.
- Frontend: Blazor Hybrid MAUI + Web + WebAssembly, SyncFusion 33.1.44.
- Compartido: `Microsoft.EntityFrameworkCore.SqlServer` 10.x.

**Storage**:
- SQL Server (transaccional): base `IngenIA365ERP_Admin` con tablas `ADM_*` (globales) + un schema por cooperativa con tablas `COR_*`, `SEC_*`, etc.
- MongoDB (auditoría): colección `audit_events` con TTL de 5 años; sharding por `tenantId` (clave de partición).
- Sistema de archivos cifrado o S3-compatible para adjuntos, con cifrado AES-256 envelope mediante DataProtection / KMS local. Decisión final del backend en `research.md`.
- Redis (caché): bloqueo distribuido para resets MFA, blacklist de refresh tokens revocados, cache de claims efectivos.

**Testing**:
- `IngenIA365ERP.Domain.Tests` — xUnit + FluentAssertions, dominio puro.
- `IngenIA365ERP.Application.Tests` — xUnit + NSubstitute para validators y handlers.
- `IngenIA365ERP.API.IntegrationTests` — xUnit + WebApplicationFactory + Testcontainers (SQL Server + MongoDB efímeros).
- `IngenIA365ERP.Architecture.Tests` — ArchUnitNET para blindar los doce principios.
- Pruebas de carga: NBomber, escenario de 100 usuarios concurrentes para verificar SC-002.

**Target Platform**:
- Servidor: Linux/Windows Server con .NET 10 runtime, contenedorizado (Docker).
- Cliente: navegadores modernos (Chromium, Firefox, Edge); MAUI desktop opcional para administradores.

**Project Type**: Web service multi-capa (backend API + frontend Blazor + admin panel) bajo la Clean Architecture ya establecida en `src/Core`, `src/Infrastructure`, `src/Presentation`.

**Performance Goals**:
- 100 usuarios concurrentes sostenidos (SC-002), p95 < 2 s sobre operaciones típicas de la fase.
- Consulta del audit log para ventana de 1 mes en < 5 s (SC-004).
- Disponibilidad 99.5% mensual (SC-013).

**Constraints**:
- Aislamiento empresa-por-empresa estricto (principio IV de la constitución): cero cross-tenant joins.
- Retención SARLAFT mínima 5 años (FR-023, SC-007).
- Cifrado AES-256 en reposo para adjuntos (FR-033, SC-009).
- BCrypt cost ≥ 11 (FR-007, SC-008).
- Cambio de permisos efectivo en máximo 30 min (FR-019).
- Mensajes de error siempre en español con código namespaced (FR-048).

**Scale/Scope**:
- 100 concurrentes en la fase; objetivo de diseño hasta 1.000 concurrentes para fases siguientes (Nómina + Asociados + Contabilidad).
- Estimado 100 cooperativas tenants en horizonte 24 meses.
- Volumen estimado de audit log: ~5.000–50.000 eventos/día por tenant medianamente activo → ~90 GB acumulados a 5 años para 100 tenants (consideración para el sharding MongoDB).
- 25 entidades nuevas o ampliadas en esta fase (ver `data-model.md`).
- 28 endpoints REST nuevos bajo `/api/admin` y `/api/security` (ver `contracts/`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio                                              | Estado | Nota |
|------|--------------------------------------------------------|--------|------|
| I    | Spec-First (Constitution → Spec → Plan → Tasks) | PASS | Spec.md + clarifications completas antes de planificar. |
| II   | Clean Architecture (Domain libre de infra) | PASS | Domain solo agrega entities Tenant/Branch/User/Role/Permission/AuditEntry/Attachment/Notification/HabeasDataConsent + value objects; sin referencias a EF/Carter/MongoDB. Architecture.Tests reescritos para cubrirlo. |
| III  | CQRS + MediatR (Commands con validator; endpoints solo reenvían) | PASS | Todos los nuevos endpoints son Carter modules que invocan `ISender.Send(...)`. Cada Command tiene `*Validator.cs` hermano. Pipeline behaviors instalados (ValidationBehavior + AuditBehavior + LoggingBehavior + PerformanceBehavior). |
| IV   | Multi-tenancy (toda query bajo tenant context) | PASS | `TenantResolutionMiddleware` (existente) se extiende para emitir/leer claim `tenant_id` del JWT. Todos los DbContexts derivan tenant del `ICurrentUserService`; no se ejecuta una sola query sin tenant resuelto. Tests de integración cubren el caso "cliente intenta cambiar tenant por header" → 403. |
| V    | Person centralizada | PASS | Esta fase no toca `COR_People`; `User` (entidad de identidad) ya tiene FK opcional a `Person` y no duplica datos personales. |
| VI   | PublicId externo / Id interno | PASS | Todos los DTOs nuevos exponen `PublicId` (Guid). Endpoints reciben PublicId en ruta y handlers lo traducen. Architecture test verifica que ningún DTO público expone `int Id`. |
| VII  | Soft-delete + auditoría | PASS | Todas las entidades nuevas heredan `AuditableEntity` (con `IsDeleted`/`DeletedAt`/`DeletedBy`) y sus configurations declaran `HasQueryFilter(e => !e.IsDeleted)`. Las entradas del audit log son la excepción (append-only, inmutables, sin soft-delete por diseño). |
| VIII | Validación dual (frontend + FluentValidation en Command) | PASS | Cada formulario del admin panel (usuarios, roles, asignaciones) usa `EditForm + DataAnnotations` + revalidación; cada Command lleva validator. Mensajes en español con códigos `Security.*`, `Admin.*`. |
| IX   | Errores visibles (sin try/catch silenciados) | PASS | Pipeline `LoggingBehavior` ya loggea todas las excepciones; los handlers nuevos lanzan `Result<T>.Failure(code, msg)` y los endpoints traducen a HTTP. Architecture test detecta cualquier `catch { }` vacío en código nuevo. |
| X    | Trazabilidad SIPLA/SARLAFT (auditoría MongoDB vía `AuditBehavior`) | PASS | `AuditBehavior` se valida en esta fase y se conecta a la colección `audit_events` con TTL 5 años. Todas las escrituras de la fase pasan por MediatR — ningún comando puede saltarlo. La exportación a PDF firmado satisface el requerimiento regulatorio. |
| XI   | Inmutabilidad contable | N/A | Esta fase no introduce movimientos contables; las entidades de cimientos no son cuentas, asientos ni transacciones. El principio aplica a fases posteriores. |
| XII  | Migraciones idempotentes y reversibles | PASS | Las migraciones SQL para `ADM_Tenants`, `ADM_Branches`, `SEC_*`, `COR_HabeasDataConsents`, `COM_Attachments`, `NOT_Notifications` son `IF NOT EXISTS` + reverso documentado en `database/migration/`. Cada archivo lleva header con contexto, problema y modo de ejecución. La colección MongoDB se aprovisiona con un script idempotente que crea índices y TTL solo si no existen. |

Resultado: **12/12 PASS o N/A**. Sin entradas en Complexity Tracking. Re-evaluación tras Phase 1 al final del documento.

## Project Structure

### Documentation (this feature)

```text
specs/001-cimientos-tecnicos/
├── plan.md                # Este documento (output de /speckit-plan)
├── spec.md                # Especificación funcional (existente)
├── research.md            # Phase 0: decisiones técnicas, alternativas evaluadas
├── data-model.md          # Phase 1: entidades, atributos, relaciones, transiciones
├── quickstart.md          # Phase 1: guía de verificación end-to-end de Fase 0
├── contracts/             # Phase 1: contratos REST de los 28 endpoints nuevos
│   ├── auth.md
│   ├── users.md
│   ├── roles-permissions.md
│   ├── tenants-branches.md
│   ├── audit-log.md
│   ├── attachments.md
│   ├── notifications.md
│   └── habeas-data.md
├── checklists/
│   └── requirements.md    # Spec quality checklist (existente, validado)
└── tasks.md               # Phase 2: dependency-ordered task list (output de /speckit-tasks)
```

### Source Code (repository root — layout existente, extensiones marcadas con +)

```text
src/
├── Core/
│   ├── IngenIA365ERP.Domain/
│   │   ├── Common/                              # AuditableEntity, BaseEntity, ITenantEntity (existentes)
│   │   ├── Entities/
│   │   │   ├── Admin/                           # Tenant, Branch (+), Subscription, TenantSetting
│   │   │   ├── Security/                        # User, Role, Permission, RolePermission, UserRole, RefreshToken, LoginAttempt
│   │   │   │   + MfaBackupCode.cs               # Códigos de respaldo MFA
│   │   │   │   + MfaResetRequest.cs             # Solicitud de reset administrativo
│   │   │   │   + PasswordHistory.cs             # Historial de no-reuso
│   │   │   │   + PasswordPolicy.cs (Tenant-scoped)
│   │   │   ├── Audit/                           # Entradas existentes; el `AuditEntry` central vive en MongoDB
│   │   │   ├── Common/                          # + Attachment.cs, Notification.cs
│   │   │   └── Compliance/                      # + HabeasDataPolicyVersion.cs, HabeasDataConsent.cs
│   │   └── ValueObjects/                        # + Nit.cs, BcryptHash.cs, TotpSecret.cs
│   └── IngenIA365ERP.Application/
│       ├── Common/                              # PipelineBehaviors (existentes; revisar)
│       │   ├── Behaviors/                       # ValidationBehavior, AuditBehavior, LoggingBehavior, PerformanceBehavior
│       │   └── Abstractions/                    # ICurrentUserService, ITenantContext, IDateTime
│       ├── Security/
│       │   ├── Auth/                            # + LoginCommand, RefreshTokenCommand, LogoutCommand, EnrollMfaCommand, VerifyMfaCommand, ConsumeBackupCodeCommand, RequestMfaResetCommand, ApproveMfaResetCommand
│       │   ├── Users/                           # + RegisterUserCommand, UpdateUserCommand, DisableUserCommand, ListUsersQuery, GetUserByPublicIdQuery
│       │   ├── Roles/                           # + CreateRoleCommand, UpdateRoleCommand, DeleteRoleCommand, ListRolesQuery
│       │   ├── Permissions/                     # + ListPermissionsQuery (catálogo Entidad×Acción)
│       │   └── PasswordPolicy/                  # + UpdatePasswordPolicyCommand, GetPasswordPolicyQuery
│       ├── Admin/
│       │   ├── Tenants/                         # + RegisterTenantCommand (operación SaaS-global), ListTenantsQuery
│       │   └── Branches/                        # + CreateBranchCommand, UpdateBranchCommand, ListBranchesQuery
│       ├── Audit/                               # + QueryAuditLogQuery, ExportAuditLogCsvQuery, ExportAuditLogPdfQuery
│       ├── Attachments/                         # + UploadAttachmentCommand, DownloadAttachmentQuery, DeleteAttachmentCommand
│       ├── Notifications/                       # + SendNotificationCommand (interna), MarkNotificationReadCommand, ListMyNotificationsQuery
│       └── Compliance/HabeasData/               # + AcceptHabeasDataCommand, RevokeHabeasDataCommand, ListHabeasDataHistoryQuery
├── Infrastructure/
│   ├── IngenIA365ERP.Identity/                  # JWT + BCrypt (existente)
│   │   + Services/TotpService.cs
│   │   + Services/MfaBackupCodeGenerator.cs
│   │   + Services/RefreshTokenStore.cs (Redis)
│   │   + Services/PasswordPolicyEnforcer.cs
│   │   + Policies/CompositePermissionPolicyProvider.cs
│   ├── IngenIA365ERP.Audit/                     # MongoDB writer (existente)
│   │   + Services/AuditPdfExporter.cs (QuestPDF firmado)
│   │   + Services/AuditCsvExporter.cs
│   │   + Indexes/AuditIndexBootstrap.cs (TTL 5 años, índices compuestos)
│   ├── IngenIA365ERP.Persistence/               # EF Core (existente)
│   │   + Interceptors/AuditInterceptor.cs (rellena CreatedBy/UpdatedBy/DeletedBy)
│   │   + Interceptors/RowVersionInterceptor.cs (concurrencia optimista)
│   │   + Interceptors/SoftDeleteInterceptor.cs (convierte DELETE en update)
│   │   + Configuration/SecurityConfigurations (Mfa*, Password*)
│   │   + Configuration/AdminConfigurations (Branch, Nit)
│   │   + Configuration/AttachmentConfiguration.cs
│   │   + Configuration/NotificationConfiguration.cs
│   │   + Configuration/HabeasDataConfigurations.cs
│   │   + Migrations/                            # Migraciones EF para schema-per-tenant
│   ├── IngenIA365ERP.Caching/                   # Redis (existente)
│   │   + Services/PermissionClaimsCache.cs
│   │   + Services/MfaResetCoordinator.cs
│   │   + Services/RevokedTokenBlacklist.cs
│   └── IngenIA365ERP.Storage/                   # + nuevo proyecto: almacén cifrado de adjuntos
│       ├── IngenIA365ERP.Storage.csproj
│       ├── Services/EncryptedFileStore.cs
│       ├── Services/AttachmentEncryptionService.cs (AES-256-GCM + envelope)
│       └── Services/NotificationEmailSender.cs   (SMTP MailKit con reintentos)
└── Presentation/
    ├── IngenIA365ERP.API/                       # Carter modules
    │   + Modules/AuthModule.cs
    │   + Modules/UsersModule.cs
    │   + Modules/RolesModule.cs
    │   + Modules/TenantsModule.cs
    │   + Modules/BranchesModule.cs
    │   + Modules/AuditLogModule.cs
    │   + Modules/AttachmentsModule.cs
    │   + Modules/NotificationsModule.cs
    │   + Modules/HabeasDataModule.cs
    │   + Middleware/TenantResolutionMiddleware.cs (extender)
    │   + Filters/PermissionAuthorizationFilter.cs
    │   + Filters/ErrorEnvelopeFilter.cs (FR-048 — códigos namespaced)
    ├── IngenIA365ERP.Web/                       # Admin panel Blazor Server (host)
    │   + Pages/Admin/Tenants/*
    │   + Pages/Admin/Branches/*
    │   + Pages/Security/Users/*
    │   + Pages/Security/Roles/*
    │   + Pages/Security/MfaEnrollment.razor
    │   + Pages/Security/MfaResetApprovals.razor
    │   + Pages/Audit/AuditLogConsole.razor
    │   + Pages/Compliance/HabeasDataAdmin.razor
    │   + Pages/Notifications/Center.razor
    └── IngenIA365ERP.Web.Client/                # WASM cliente reutilizable
        + Services/AuthClient.cs (refresh token + interceptor 401→/refresh)
        + Services/PermissionGuard.cs (oculta menú si falta permiso)
        + Components/PermissionGate.razor

tests/
├── IngenIA365ERP.Domain.Tests/                 # Existente
├── IngenIA365ERP.Application.Tests/            # Existente + nuevos handlers/validators de la fase
├── IngenIA365ERP.API.IntegrationTests/         # Existente + escenarios de la fase (auth flow, audit query, RBAC)
├── IngenIA365ERP.Architecture.Tests/           # Existente — reglas ArchUnitNET ampliadas para los 12 principios
└── IngenIA365ERP.Load.Tests/                   # + nuevo: NBomber para SC-002 (100 concurrent users)

database/
├── schema/
│   + 13d_Security_Mfa_Password_Policy.sql       # SEC_MfaBackupCodes, SEC_MfaResetRequests, SEC_PasswordHistory, SEC_PasswordPolicies
│   + 13e_Admin_Branches.sql                     # ADM_Branches
│   + 13f_Attachments.sql                        # COM_Attachments
│   + 13g_Notifications.sql                      # NOT_Notifications
│   + 13h_HabeasData.sql                         # COR_HabeasDataPolicyVersions, COR_HabeasDataConsents
└── migration/
    + 14_RowVersion_For_Optimistic_Concurrency.sql
    + 15_Audit_Mongodb_Bootstrap.json            # script idempotente: crea TTL 5 años, índices compuestos
```

**Structure Decision**:
Se conserva la **Clean Architecture en cuatro capas** existente del repositorio (`src/Core` → Domain + Application; `src/Infrastructure` → Identity/Audit/Persistence/Caching/+Storage; `src/Presentation` → API + Web + Web.Client). Esta fase añade un nuevo proyecto `IngenIA365ERP.Storage` (almacén cifrado + remitente SMTP) para mantener desacoplado el manejo de archivos y correo, sin tocar Persistence ni Identity. El panel de administración (FR-046) y la consola de auditoría (FR-047) viven en `IngenIA365ERP.Web` como páginas Blazor Server, autenticadas vía `IngenIA365ERP.Web.Client` que ya consume `IngenIA365ERP.API`. Las migraciones SQL siguen el patrón numerado existente (`13_X.sql`, `13b_Y.sql`, …) y la nueva colección MongoDB se aprovisiona con un script idempotente.

## Phase 0 — Outline & Research

Resuelta en [`research.md`](./research.md). Decisiones tomadas:
- Hashing de contraseña → **BCrypt.Net-Next con cost 11** (alineado con constitución; alternativas Argon2id descartada por sobrecosto y madurez del ecosistema .NET).
- TOTP → **Otp.NET 1.4** (RFC 6238, ventana ±1, 30 s); inscripción mediante QR generado server-side con `QRCoder`.
- Códigos de respaldo → **10 códigos de 10 caracteres alfanuméricos**, almacenados como hash BCrypt (cost 11), uso único.
- JWT → **RS256, access 30 min, refresh 12 h**, refresh rotation con familia (detección de reutilización), claves administradas por DataProtection.
- Concurrencia optimista → **EF Core `IsRowVersion()`** sobre columna `RowVersion (timestamp)` en cada entidad editable; conflicto traducido a `Result.Failure("Concurrency.StaleRowVersion", msg)`.
- Audit log → **MongoDB.Driver 3.9**, colección `audit_events`, índices compuestos `(tenantId, timestamp)`, `(tenantId, userId)`, `(tenantId, entityType, entityPublicId)`, TTL de 5 años (`createdAt` con `expireAfterSeconds = 157_680_000`).
- Exportación auditoría → **CSV (CsvHelper 31.x)** + **PDF firmado con QuestPDF + signing pipeline DataProtection (HMAC-SHA256 del PDF rendido)**.
- Almacén de adjuntos → **sistema de archivos local cifrado por defecto (AES-256-GCM con DEK envelope), interfaz `IBlobStore` para soporte futuro S3-compatible**.
- Servicio de correo → **MailKit 4.x** con reintentos exponencial (3 reintentos, jitter), fallos persistidos en `NOT_NotificationDeliveryFailures` para diagnóstico.
- Carga 100 concurrentes → **NBomber 5.x** con escenario mixto 60/30/10.

## Phase 1 — Design & Contracts

### Data model

Detallado en [`data-model.md`](./data-model.md): 25 entidades nuevas/ampliadas con sus atributos, relaciones, invariantes y transiciones de estado.

### Contracts

Detallados en [`contracts/`](./contracts/). 28 endpoints REST agrupados:

| Módulo | Contrato | Endpoints clave |
|--------|----------|-----------------|
| Auth | `contracts/auth.md` | `POST /api/auth/login`, `POST /api/auth/mfa/verify`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/auth/mfa/enroll`, `POST /api/auth/mfa/backup-code`, `POST /api/auth/mfa/reset/request`, `POST /api/auth/mfa/reset/{publicId}/approve` |
| Users | `contracts/users.md` | `GET /api/admin/users`, `POST /api/admin/users`, `PUT /api/admin/users/{publicId}`, `POST /api/admin/users/{publicId}/disable`, `POST /api/admin/users/{publicId}/roles` |
| Roles & Permissions | `contracts/roles-permissions.md` | `GET /api/admin/roles`, `POST /api/admin/roles`, `PUT /api/admin/roles/{publicId}`, `DELETE /api/admin/roles/{publicId}`, `GET /api/admin/permissions` |
| Tenants & Branches | `contracts/tenants-branches.md` | `GET /api/saas/tenants`, `POST /api/saas/tenants`, `GET /api/admin/branches`, `POST /api/admin/branches`, `PUT /api/admin/branches/{publicId}` |
| Audit log | `contracts/audit-log.md` | `GET /api/audit/events`, `GET /api/audit/events/export.csv`, `GET /api/audit/events/export.pdf` |
| Attachments | `contracts/attachments.md` | `POST /api/attachments`, `GET /api/attachments/{publicId}`, `DELETE /api/attachments/{publicId}` |
| Notifications | `contracts/notifications.md` | `GET /api/notifications`, `POST /api/notifications/{publicId}/read`, `POST /api/notifications/{publicId}/archive` |
| Habeas data | `contracts/habeas-data.md` | `POST /api/compliance/habeas-data/consents`, `POST /api/compliance/habeas-data/revocations`, `GET /api/compliance/habeas-data/persons/{publicId}/history`, `GET /api/compliance/habeas-data/policies` |

Todos los endpoints exigen `RequireAuthorization` salvo `POST /api/auth/login` y `POST /api/auth/mfa/verify`; todos retornan `Result<T>` traducido a HTTP por `ErrorEnvelopeFilter` con códigos namespaced en español.

### Quickstart

[`quickstart.md`](./quickstart.md) recorre los 7 user stories del spec con comandos `dotnet`, curl y pasos en el panel Blazor, demostrando los criterios de aceptación de cada uno.

### Agent context update

Bloque `<!-- SPECKIT START --> ... <!-- SPECKIT END -->` de `CLAUDE.md` actualizado para apuntar a `specs/001-cimientos-tecnicos/plan.md`.

## Constitution Re-check (post Phase 1 design)

Tras escribir `research.md`, `data-model.md`, `contracts/` y `quickstart.md`, las doce compuertas siguen en **PASS o N/A**. No se introdujeron dependencias cruzadas indebidas, ni endpoints sin autorización, ni handlers que salten el pipeline MediatR, ni DTOs públicos con `int Id`, ni edición/eliminación de movimientos contables (principio XI, N/A en esta fase). Sin cambios en Complexity Tracking.

## Complexity Tracking

> Vacío — ninguna compuerta constitucional violada.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
