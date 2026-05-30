# Phase 1 — Data Model: Fase 0 Cimientos técnicos

**Feature**: 001-cimientos-tecnicos | **Date**: 2026-05-28

Todas las entidades **MUST** heredar de `AuditableEntity` (excepto las del audit log MongoDB y los catálogos inmutables marcados). Todas las entidades exponen `Id : int` (interno) + `PublicId : Guid` (público) y se persisten con `IsRowVersion()` sobre una columna `RowVersion : byte[]` para concurrencia optimista (FR-049).

Convención de nombres SQL: prefijo de módulo (`ADM_`, `SEC_`, `COR_`, `COM_`, `NOT_`). Idioma de columnas: inglés (constitución).

---

## A. Multi-empresa y multi-sede

### A.1 `Tenant` (mapea a `ADM_Tenants` — global, base `IngenIA365ERP_Admin`)

> Entidad ya existe en `src/Core/IngenIA365ERP.Domain/Entities/Admin/Tenant.cs`. Se amplía con los campos marcados con `(+)`.

| Campo | Tipo | Notas |
|-------|------|-------|
| Id | int | PK interna. |
| PublicId | Guid | Identificador público. |
| Nit (+) | string(20) | NIT colombiano con dígito verificación, **único** global. |
| LegalName (+) | string(200) | Razón social. |
| Name | string(200) | Nombre comercial. |
| SchemaName | string(50) | Schema SQL Server (tnt_xxxx). |
| Subdomain | string(100) | Subdominio opcional. |
| PlanType | string(50) | Plan SaaS. |
| IsActive | bool | |
| MaxUsers | int | |
| StorageLimitMb | long | |
| DatabaseName | string(100) | |
| ContactEmail | string(200) | |
| ContactPhone | string(30) | |
| LegalAddress (+) | string(300) | Dirección legal Colombia. |
| TaxRegime (+) | string(50) | Régimen tributario (común, simplificado). |
| ActivatedAt | datetime? | |
| SuspendedAt | datetime? | |
| RowVersion (+) | byte[] | Concurrencia optimista. |
| (audit) | CreatedAt/By, UpdatedAt/By, IsDeleted, DeletedAt/By | |

**Unicidad**: `Nit` global; `SchemaName` global.

**Transiciones**: `Provisioning → Active → Suspended → Active → Terminated (soft-deleted)`.

**Relaciones**: 1‒N `Branch`, 1‒N `Subscription`, 1‒N `TenantSetting`, 1‒N `User` (multi-empresa: un user puede pertenecer a N tenants vía `UserTenantAssignment`).

---

### A.2 `Branch` (+, mapea a `ADM_Branches` — global)

| Campo | Tipo | Notas |
|-------|------|-------|
| Id | int | PK. |
| PublicId | Guid | |
| TenantId | int (FK) | A `Tenant`. |
| Code | string(10) | Código de sucursal, único por tenant. |
| Name | string(150) | |
| Address | string(300) | |
| City | string(100) | |
| Department | string(100) | Departamento Colombia. |
| Phone | string(30) | |
| IsActive | bool | |
| IsHeadquarters | bool | Marcador para "casa matriz" de la cooperativa. |
| RowVersion | byte[] | |
| (audit) | … | |

**Unicidad**: `(TenantId, Code)` único.

**Invariantes**: exactamente una `IsHeadquarters = true` por tenant.

**Relaciones**: pertenece a un `Tenant`. Las asignaciones usuario↔sucursal viven en `UserBranchAssignment` (B.5).

---

## B. Identidad, autenticación y RBAC

### B.1 `User` (mapea a `SEC_Users`)

> Entidad ya existe. Se amplía con los campos marcados con `(+)`.

| Campo | Tipo | Notas |
|-------|------|-------|
| Id, PublicId | int, Guid | |
| PersonId | int? (FK) | A `COR_People` (cuando la persona existe como titular; FK opcional para usuarios SaaS-globales). |
| Username | string(60) | Único global. |
| Email | string(200) | Único global. |
| PasswordHash | string(200) | BCrypt cost ≥ 11. |
| PasswordSalt | string(60)? | Mantenido por compatibilidad legacy. |
| IsActive | bool | |
| IsEmailVerified | bool | |
| IsMfaEnabled | bool | |
| MfaSecret | string(200)? | Cifrado con DataProtection. |
| LastLoginAt | datetime? | UTC. |
| LastPasswordChangeAt | datetime? | UTC. |
| FailedLoginAttempts | int | Reset al login exitoso. |
| LockoutEndAt | datetime? | UTC. |
| IsSaasOperator (+) | bool | Marca al usuario como operador global (admin del SaaS). |
| MustChangePassword (+) | bool | true si la última contraseña fue establecida por reset administrativo. |
| RowVersion (+) | byte[] | |
| (audit) | … | |

**Invariantes**: si `IsMfaEnabled = true`, `MfaSecret IS NOT NULL`. Si `LockoutEndAt > now`, login deniega con código `Auth.AccountLocked`.

> **Nota sobre el Principio V (Person centralizada)**:
> `User.Email` es el **email de la cuenta de identidad** (la dirección a la que se envían correos operacionales: bloqueo de cuenta, reset de MFA, cambio de contraseña). Es independiente del `Person.Email`, que es el dato personal del titular.
> - Un usuario operador del SaaS (`IsSaasOperator = true`) puede no tener `PersonId` y aun así requiere `User.Email`.
> - Un asociado-empleado con dos correos distintos (uno personal en `Person.Email`, otro corporativo en `User.Email`) es un caso legítimo.
> - Cuando `User.PersonId IS NOT NULL` y se desea que ambos correos coincidan, esa sincronización es responsabilidad del handler que crea o actualiza al usuario; no es invariante automática.
> Por tanto **no se considera duplicación prohibida** por la constitución V — los dos campos representan conceptos distintos. La misma lógica aplica a `User.Username` (identificador de login) vs. los datos demográficos de `Person`.

**Transiciones**: `Active → Locked → Active`; `Active → Disabled` (soft-delete).

**Relaciones**:
- 1‒N `RefreshToken`, `LoginAttempt`, `MfaBackupCode`, `MfaResetRequest`, `PasswordHistory`.
- N‒M `Role` vía `UserRole` (con scope tenant).
- N‒M `Tenant` vía `UserTenantAssignment`.
- N‒M `Branch` vía `UserBranchAssignment`.

---

### B.2 `Role` (mapea a `SEC_Roles`)

> Ya existe. Se amplía con scope por tenant.

| Campo | Tipo | Notas |
|-------|------|-------|
| Id, PublicId | int, Guid | |
| TenantId (+) | int? (FK) | Null para roles SaaS-globales (operador). |
| Code | string(40) | Único por tenant. |
| Name | string(80) | |
| Description | string(400)? | |
| IsBuiltIn | bool | `true` para los 4 roles preconstruidos. |
| IsAssignable | bool | Roles del sistema que no se asignan (p. ej. "System.Background"). |
| RowVersion | byte[] | |
| (audit) | … | |

**Invariantes**: Un rol `IsBuiltIn = true` y `Code IN ('CompanyAdmin','Auditor')` no se puede eliminar.

**Roles preconstruidos por tenant**: `CompanyAdmin`, `Auditor`, `Operator`, `ReadOnly`.

**Relaciones**: N‒M `Permission` vía `RolePermission`; N‒M `User` vía `UserRole`.

---

### B.3 `Permission` (mapea a `SEC_Permissions` — catálogo global, inmutable)

| Campo | Tipo | Notas |
|-------|------|-------|
| Id, PublicId | int, Guid | |
| Code | string(80) | Único global. Formato `Entidad.Accion` (p. ej. `Employees.Create`). |
| EntityType | string(50) | `Employee`, `User`, `Role`, `AuditLog`, ... |
| Action | string(20) | `View`, `Create`, `Update`, `Delete`, `Approve`, `Restore`, `Export`. |
| Module | string(20) | `Security`, `Admin`, `Audit`, `Compliance`, ... |
| Description | string(200) | |

**Catálogo inmutable**: se siembra en bootstrap y solo se amplía vía migración. No tiene `AuditableEntity` — es lookup puro (excepción justificada por constitución VII).

---

### B.4 `RolePermission`, `UserRole`

Tablas de unión (ya existen). `UserRole` incluye `TenantId` para scope por empresa.

| `UserRole` | |
|---|---|
| UserId | FK |
| RoleId | FK |
| TenantId | FK (alcance de la asignación) |
| AssignedAt | datetime UTC |
| AssignedBy | string |
| ExpiresAt | datetime? UTC |

---

### B.5 `UserTenantAssignment`, `UserBranchAssignment` (+)

Vinculan usuario↔tenant y usuario↔sucursal, con permiso de cambio de contexto.

| `UserTenantAssignment` | |
|---|---|
| UserId, TenantId | PK compuesta |
| IsPrimary | bool |
| AssignedAt | datetime UTC |
| AssignedBy | string |

| `UserBranchAssignment` | |
|---|---|
| UserId, BranchId | PK compuesta |
| IsDefault | bool |

---

### B.6 `RefreshToken` (mapea a `SEC_RefreshTokens` — existente)

| Campo | Tipo | Notas |
|-------|------|-------|
| Id, PublicId | int, Guid | |
| UserId | int (FK) | |
| TokenHash | string(120) | SHA-256 del refresh token opaco. |
| FamilyId (+) | Guid | Identifica la familia de tokens derivados; se invalida la familia si se detecta reutilización. |
| IssuedAt | datetime UTC | |
| ExpiresAt | datetime UTC | |
| RevokedAt | datetime? UTC | |
| RevocationReason | string(40)? | `Rotated`, `LogoutUser`, `LogoutAll`, `ReuseDetected`, `AdminRevoke`. |
| RowVersion | byte[] | |

**Invariantes**: el token solo es válido si `RevokedAt IS NULL AND ExpiresAt > now`.

---

### B.7 `LoginAttempt` (existente)

| Campo | Tipo | Notas |
|---|---|---|
| Id, PublicId | int, Guid | |
| UserId | int? (FK) | Null si username inexistente (intento sin usuario válido). |
| UsernameTried | string(60) | Lo que el atacante introdujo. |
| Ip | string(45) | |
| UserAgent | string(400) | |
| Result | string(40) | `Success`, `BadPassword`, `BadMfa`, `Locked`, `Unknown`. |
| AttemptedAt | datetime UTC | |

No hereda de `AuditableEntity` (excepción: append-only, lookup forense).

---

### B.8 `MfaBackupCode` (+, mapea a `SEC_MfaBackupCodes`)

| Campo | Tipo | Notas |
|---|---|---|
| Id, PublicId | int, Guid | |
| UserId | int (FK) | |
| CodeHash | string(120) | BCrypt cost 11 del código. |
| GeneratedAt | datetime UTC | |
| UsedAt | datetime? UTC | |
| BatchId | Guid | Todos los códigos de una generación comparten BatchId; al regenerar, se invalidan los anteriores en bloque. |

**Invariante**: una vez `UsedAt` queda fijado, nunca cambia (uso único).

---

### B.9 `MfaResetRequest` (+, mapea a `SEC_MfaResetRequests`)

| Campo | Tipo | Notas |
|---|---|---|
| Id, PublicId | int, Guid | |
| UserId | int (FK) | Usuario a quien se resetea el MFA. |
| RequestedBy | int (FK User) | Usuario que solicita (puede ser el propio). |
| RequestedAt | datetime UTC | |
| Reason | string(500) | Motivo en texto libre. |
| EvidenceAttachmentId | int? (FK Attachment) | Documento adjunto (foto de cédula, autorización). |
| Status | string(20) | `Pending`, `Approved`, `Rejected`, `Executed`, `Expired`. |
| FirstApproverId | int? (FK User) | |
| FirstApprovalAt | datetime? UTC | |
| SecondApproverId | int? (FK User) | |
| SecondApprovalAt | datetime? UTC | |
| ExecutedAt | datetime? UTC | Momento en que el nuevo MFA queda habilitado. |
| ExpiresAt | datetime UTC | RequestedAt + 24 h. |
| RowVersion | byte[] | |
| (audit) | … | |

**Invariantes**:
- `FirstApproverId != SecondApproverId` (dos personas distintas).
- `RequestedBy != FirstApproverId AND RequestedBy != SecondApproverId` (el solicitante no se aprueba a sí mismo).
- Status transitions: `Pending → Approved → Executed`, o `Pending → Rejected`, o `Pending → Expired` (auto).
- Solo se ejecuta si Status = Approved y now < ExpiresAt.

---

### B.10 `PasswordHistory` (+, mapea a `SEC_PasswordHistory`)

| Campo | Tipo | Notas |
|---|---|---|
| Id | int | |
| UserId | int (FK) | |
| PasswordHash | string(200) | BCrypt. |
| SetAt | datetime UTC | |

**Invariante**: retención máxima por usuario = `PasswordPolicy.HistorySize` (FIFO).

No `AuditableEntity` (append-only, hashes únicamente).

---

### B.11 `PasswordPolicy` (+, mapea a `SEC_PasswordPolicies`)

| Campo | Tipo | Notas |
|---|---|---|
| Id, PublicId | int, Guid | |
| TenantId | int (FK) | Una política por tenant; null = política global por defecto. |
| MinLength | int (8–32) | Default 12. |
| RequireUppercase | bool | Default true. |
| RequireLowercase | bool | Default true. |
| RequireDigit | bool | Default true. |
| RequireSymbol | bool | Default true. |
| ExpiryDays | int (30–180) | Default 90. |
| HistorySize | int (5–24) | Default 5. |
| LockoutThreshold | int (3–10) | Default 5. |
| LockoutMinutes | int (5–60) | Default 15. |
| RowVersion | byte[] | |
| (audit) | … | |

**Invariante**: una política activa por tenant a la vez.

---

## C. Audit log (MongoDB)

### C.1 `AuditEvent` — documento MongoDB en colección `audit_events`

| Campo | Tipo | Notas |
|---|---|---|
| _id | ObjectId | |
| publicId | Guid | Único. |
| tenantId | Guid | Tenant.PublicId. |
| userId | Guid | User.PublicId. |
| username | string | Snapshot para legibilidad post-eliminación. |
| ip | string | IPv4/IPv6. |
| userAgent | string | |
| operationType | string | `Create`, `Update`, `Delete`, `Restore`, `Login`, `Logout`, `Approve`, `Reject`, `Export`, `Failure`. |
| entityType | string | Nombre de la entidad de dominio. |
| entityPublicId | Guid? | Entity.PublicId, null para operaciones sin entidad (login). |
| changes | array<{field,before,after}> | Solo campos modificados; before/after serializados como JSON-friendly. |
| result | string | `Success` o `Failure`. |
| failureCode | string? | Código namespaced en caso de fallo (`Security.AccessDenied`, etc.). |
| occurredAt | ISODate | UTC, momento del evento. |
| createdAt | ISODate | UTC, momento de inserción en Mongo (usado para TTL). |
| schemaVersion | int | 1 al lanzar. |

**Índices** (ver `research.md §6`).

**Inmutabilidad**: el writer solo expone `AppendAsync`; rol de BD niega `update`/`delete` (defense-in-depth).

**No hereda `AuditableEntity`**: es append-only por diseño y no participa del modelo de soft-delete.

---

## D. Adjuntos

### D.1 `Attachment` (+, mapea a `COM_Attachments`)

| Campo | Tipo | Notas |
|---|---|---|
| Id, PublicId | int, Guid | |
| TenantId | int (FK) | |
| OwnerEntityType | string(60) | Tipo de la entidad propietaria. |
| OwnerEntityId | int | FK polimórfica al registro propietario. |
| OriginalFileName | string(260) | |
| ContentType | string(120) | MIME. |
| SizeBytes | long | Hasta tamaño máximo permitido. |
| Sha256 | string(64) | Hash del cleartext, hex. |
| StoragePath | string(500) | Ruta interna en `IBlobStore`. |
| Iv | byte[12] | Nonce AES-GCM. |
| EncryptedDataKey | byte[] | DEK cifrada por la KEK (envelope encryption). |
| KeyVersion | string(40) | Versión de la KEK con la que se cifró (rotación). |
| Status | string(20) | `Active`, `SoftDeleted`, `Purged`. |
| RowVersion | byte[] | |
| (audit) | … | |

**Invariantes**:
- `SizeBytes <= TenantSetting.MaxAttachmentSizeBytes`.
- `ContentType ∈ TenantSetting.AllowedAttachmentMimeTypes`.
- `Sha256` se recalcula y valida en cada descarga.

---

## E. Notificaciones

### E.1 `Notification` (+, mapea a `NOT_Notifications`)

| Campo | Tipo | Notas |
|---|---|---|
| Id, PublicId | int, Guid | |
| TenantId | int (FK) | |
| RecipientUserId | int (FK User) | |
| Type | string(40) | `AccountLocked`, `PasswordChanged`, `MfaReset`, `RoleAssigned`, `MfaResetRequested`, ... |
| Subject | string(200) | Para correo + título en panel. |
| Body | string(2000) | Markdown ligero. |
| Channels | string(40) | CSV de `InApp`, `Email`. |
| Status | string(20) | `Pending`, `Delivered`, `Read`, `Archived`, `Failed`. |
| CreatedAtUtc | datetime UTC | |
| DeliveredAtUtc | datetime? UTC | |
| ReadAtUtc | datetime? UTC | |
| ArchivedAtUtc | datetime? UTC | |
| RowVersion | byte[] | |
| (audit) | … | |

**Transiciones**: `Pending → Delivered → Read → Archived`; `Pending → Failed` (entrega; el in-app puede seguir visible).

---

### E.2 `NotificationDeliveryFailure` (+, mapea a `NOT_NotificationDeliveryFailures`)

| Campo | Tipo | Notas |
|---|---|---|
| Id | int | |
| NotificationId | int (FK) | |
| Channel | string(20) | `Email`. |
| AttemptNumber | int | 1..3. |
| FailureCategory | string(40) | `SmtpAuth`, `MailboxFull`, `BounceHard`, `BounceSoft`, `Timeout`. |
| FailureMessage | string(500) | Sin payload, solo razón. |
| AttemptedAtUtc | datetime UTC | |

No `AuditableEntity` (append-only, diagnóstico).

---

## F. Habeas data

### F.1 `HabeasDataPolicyVersion` (+, mapea a `COR_HabeasDataPolicyVersions`)

| Campo | Tipo | Notas |
|---|---|---|
| Id, PublicId | int, Guid | |
| TenantId | int? (FK) | Null = versión global del SaaS aplicable a todos los tenants. |
| VersionLabel | string(20) | Semver `MAJOR.MINOR.PATCH`. |
| Title | string(200) | |
| BodyMarkdown | string(MAX) | Texto completo. |
| Sha256 | string(64) | Integridad del texto. |
| EffectiveFrom | datetime UTC | |
| EffectiveTo | datetime? UTC | Null = vigente. |
| PublishedAt | datetime UTC | |
| PublishedBy | string | |
| RowVersion | byte[] | |
| (audit) | … | |

**Invariantes**: para cada (TenantId), a lo sumo una versión con `EffectiveTo IS NULL` simultáneamente.

---

### F.2 `HabeasDataConsent` (+, mapea a `COR_HabeasDataConsents`)

| Campo | Tipo | Notas |
|---|---|---|
| Id, PublicId | int, Guid | |
| TenantId | int (FK) | |
| PersonId | int (FK) | Titular. |
| PolicyVersionId | int (FK) | Versión aceptada. |
| EvidenceText | string(4000) | Texto exacto del consentimiento mostrado. |
| Ip | string(45) | Si fue captura electrónica. |
| UserAgent | string(400)? | |
| Status | string(20) | `Accepted`, `Revoked`. |
| AcceptedAtUtc | datetime UTC | |
| RevokedAtUtc | datetime? UTC | |
| RevocationReason | string(500)? | |
| RowVersion | byte[] | |
| (audit) | … | |

**Invariantes**:
- Una persona puede tener múltiples consentimientos a lo largo del tiempo.
- "Vigente" = último Accepted posterior al último Revoked.
- `RevokedAtUtc IS NULL` si y solo si `Status = 'Accepted'`.

**Evento de dominio**: `HabeasDataRevokedEvent { PersonPublicId, TenantPublicId, RevokedAtUtc }` se publica en cada revocación; handlers de módulos consumidores lo suscriben (fase posterior).

---

## Resumen de relaciones (alto nivel)

```text
Tenant 1─N Branch
Tenant 1─N TenantSetting
Tenant 1─N PasswordPolicy
Tenant 1─N HabeasDataPolicyVersion
Tenant 1─N HabeasDataConsent

User N─M Tenant   (vía UserTenantAssignment, IsPrimary)
User N─M Branch   (vía UserBranchAssignment, IsDefault)
User N─M Role     (vía UserRole, scope TenantId)
Role N─M Permission (vía RolePermission)

User 1─N RefreshToken
User 1─N LoginAttempt
User 1─N MfaBackupCode (con BatchId)
User 1─N MfaResetRequest (como Subject, FirstApprover, SecondApprover)
User 1─N PasswordHistory

Attachment N─1 Tenant; polymorphic OwnerEntityType/OwnerEntityId
Notification N─1 Tenant, N─1 User (Recipient)
NotificationDeliveryFailure N─1 Notification

HabeasDataConsent N─1 Person, N─1 HabeasDataPolicyVersion, N─1 Tenant

AuditEvent (MongoDB) — sin FK física; referencia via { tenantId, userId, entityType, entityPublicId } como Guids.
```

---

## Reglas de validación cruzadas con el spec

| FR | Entidad / Atributo / Invariante |
|----|--------------------------------|
| FR-001 | `Tenant.Nit` único; `Tenant.LegalName`, `Tenant.LegalAddress`, `Tenant.TaxRegime`. |
| FR-002 | `Branch.TenantId`, `(TenantId, Code)` único, `IsActive`. |
| FR-003 | Schema-per-tenant + `ITenantEntity` + filtro global EF Core por `TenantId`. |
| FR-004 | `tenant_id` derivado del JWT, no del request. |
| FR-005 | `User.IsMfaEnabled`, `User.MfaSecret`, flujo `LoginCommand` + `VerifyMfaCommand`. |
| FR-006 | `EnrollMfaCommand` exige password reverify; rotación regenera secreto + invalida backup codes batch. |
| FR-007 | `User.PasswordHash` = BCrypt cost 11, validado por `PasswordPolicyEnforcer`. |
| FR-008 | `PasswordPolicy` por tenant, rangos validados. |
| FR-009 | `PasswordPolicy.ExpiryDays`, `User.LastPasswordChangeAt`, login fuerza `MustChangePassword`. |
| FR-010 | `PasswordHistory.HistorySize`. |
| FR-011 | `PasswordPolicy.LockoutThreshold`, `LockoutMinutes`; notificación `AccountLocked`. |
| FR-012 | `RefreshToken.FamilyId`, rotación, claims con `tenant_id`/`branch_id`. |
| FR-013 | `MfaBackupCode` + `MfaResetRequest` con doble aprobación. |
| FR-014 | `Permission.Code` `Entidad.Accion`, acciones enumeradas. |
| FR-015 | `Role` + `UserRole` scoped por tenant. |
| FR-016 | `PermissionClaimsCache` resuelve unión de roles activos. |
| FR-017 | `PermissionAuthorizationFilter` → 404 indistinguible. |
| FR-018 | `PermissionGate.razor` oculta menús. |
| FR-019 | `perm_ver` claim + TTL 30 min del access token. |
| FR-020 | Seed de roles `CompanyAdmin`, `Auditor`, `Operator`, `ReadOnly` con `IsBuiltIn`. |
| FR-021 | `AuditEvent` con todos los campos. |
| FR-022 | Wrapper que bloquea update/delete + rol Mongo. |
| FR-023 | TTL `expireAfterSeconds: 157_680_000` (5 años). |
| FR-024 | `QueryAuditLogQuery`, `ExportAuditLogCsvQuery`, `ExportAuditLogPdfQuery`. |
| FR-025 | Índices con `tenantId` siempre como prefijo; query siempre filtra por tenant del JWT. |
| FR-026 | `AuditBehavior` envuelve transacción; fallo de Mongo => rollback SQL. |
| FR-027 | `AuditableEntity` + `BaseEntity` (existente). |
| FR-028 | `HasQueryFilter(e => !e.IsDeleted)` en todas las configurations. |
| FR-029 | Acción `Restore` + permiso `Entidad.Restore`. |
| FR-030 | `SoftDeleteInterceptor` convierte DELETE en UPDATE; Architecture.Test prohíbe `ExecuteSqlRaw("DELETE...")`. |
| FR-031 | Architecture.Test sobre namespaces de movimientos contables (futura fase). |
| FR-032 | `Attachment.SizeBytes`, `ContentType` validados por tenant settings. |
| FR-033 | `EncryptedDataKey`, `Iv`, AES-256-GCM. |
| FR-034 | `DownloadAttachmentQuery` verifica permiso sobre la entidad propietaria. |
| FR-035 | `AuditEvent` para alta/descarga/eliminación de adjuntos. |
| FR-036 | `UploadAttachmentValidator` rechaza con código `Attachment.UnsupportedType` o `Attachment.SizeExceeded`. |
| FR-037 | `Notification.Channels = 'InApp,Email'`. |
| FR-038 | Status transitions y SignalR push. |
| FR-039 | `NotificationDeliveryFailure` + reintentos exponenciales. |
| FR-040 | Triggers automáticos en `LoginAttempt` / `ChangePasswordCommand` / `EnrollMfaCommand` / `AssignRoleCommand`. |
| FR-041 | `HabeasDataConsent.PolicyVersionId`, `EvidenceText`, `AcceptedAtUtc`. |
| FR-042 | `HabeasDataPolicyVersion` con `VersionLabel` semver y `Sha256`. |
| FR-043 | `HabeasDataConsent.RevokedAtUtc` + `HabeasDataRevokedEvent`. |
| FR-044 | `ListHabeasDataHistoryQuery`. |
| FR-045 | NBomber escenario 100 concurrentes. |
| FR-046 | Pages Blazor `Pages/Admin/*` y `Pages/Security/*`. |
| FR-047 | Page Blazor `Pages/Audit/AuditLogConsole.razor`. |
| FR-048 | `ErrorEnvelopeFilter` traduce `Result.Failure(code, msg)` a payload con `code`, `message`, `traceId`. |
| FR-049 | `RowVersion` + `Result.Failure("Concurrency.StaleRowVersion", msg)`. |
