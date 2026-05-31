# Phase 1 — Data Model: Identidad Central con Autorización Federada por Empresa

**Branch**: `002-identidad-central-federada` | **Date**: 2026-05-30

Este documento detalla las entidades nuevas, sus atributos, índices, relaciones, validaciones y máquinas de estado, así como el refactor de `SEC_Users` per-tenant.

---

## Convenciones

- Nombres SQL: prefijo de módulo `ADM_*` (Admin DB) o de tenant según corresponda. PascalCase singular para entidades C#.
- Tipos: `Guid` = `UNIQUEIDENTIFIER`, `string` = `NVARCHAR(N)`, `DateTime` UTC = `DATETIMEOFFSET` con default `SYSUTCDATETIME()`.
- Todas las entidades heredan `AuditableEntity` excepto las marcadas como append-only.
- Todas las entidades exponen `Guid PublicId` salvo `CentralUser` (excepción justificada — su `Id` es ya `Guid` por convención ASP.NET Identity y cumple el rol de PublicId).

---

## 1. CentralUser (ADM_CentralUsers)

**Ubicación**: BD `IngenIA365ERP_Admin`. Bridge a ASP.NET Identity `IdentityUser<Guid>`.

**Atributos** (Domain — POCO `CentralUser` en `IngenIA365ERP.Domain.Entities.Admin`):

| Campo | Tipo | Restricciones | Notas |
|-------|------|---------------|-------|
| `Id` | `Guid` | PK, NOT NULL | Sirve como ID público (ver Complexity Tracking del plan). |
| `Email` | `string(256)` | NOT NULL | Sin normalizar — lo que el usuario escribió. |
| `NormalizedEmail` | `string(256)` | NOT NULL, UNIQUE | UPPER del email — usado para lookup case-insensitive. |
| `EmailConfirmed` | `bool` | NOT NULL default 0 | True tras aceptar invitación (la aceptación prueba el correo). |
| `PasswordHash` | `string(256)` | NOT NULL | BCrypt cost 11 vía `BcryptPasswordHasher`. |
| `SecurityStamp` | `string(64)` | NOT NULL | GUID; cambia con cambio de contraseña → invalida refresh tokens. |
| `ConcurrencyStamp` | `string(64)` | NOT NULL | GUID; ASP.NET Identity optimistic concurrency. |
| `TwoFactorEnabled` | `bool` | NOT NULL default 0 | Equivalente a `MfaEnabled` del spec. |
| `MfaSecret` | `string(512)` (encrypted) | NULL | Secreto TOTP (base32) cifrado con `IDataProtectionProvider`. NULL si MFA no configurado. |
| `LockoutEnd` | `DateTimeOffset?` | NULL | Fin del lockout actual (managed por ASP.NET Identity para el lockout local; el bloqueo progresivo por Redis es independiente). |
| `LockoutEnabled` | `bool` | NOT NULL default 0 | ASP.NET Identity flag — **deshabilitado intencionalmente** (ver `research.md > D-11`); el contador de Redis (`LoginAttemptCounter`) es la única fuente de verdad para el lockout progresivo. |
| `AccessFailedCount` | `int` | NOT NULL default 0 | ASP.NET Identity contador — no se usa para lockout (ver fila anterior); se mantiene a 0 para no romper el contrato de la base abstracta. |
| `DefaultTenantId` | `Guid?` | NULL, FK lógica a `ADM_Tenants.PublicId` | Empresa por defecto al login (FR-016). |
| `IsGlobalMasterAdmin` | `bool` | NOT NULL default 0 | Admin master del producto (FR-037 a FR-039). |
| `Status` | `string(20)` | NOT NULL default 'Active' | Enum: `Active`, `Pending`, `Disabled`. |
| `CreatedAt` | `DateTimeOffset` | NOT NULL | UTC. |
| `LastLoginAt` | `DateTimeOffset?` | NULL | Actualizado en cada login exitoso. |
| `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `DeletedAt`, `DeletedBy` | (AuditableEntity) | | Soft-delete + auditoría. |

**Índices**:
- `UQ_ADM_CentralUsers_NormalizedEmail` UNIQUE en `NormalizedEmail`.
- `IX_ADM_CentralUsers_DefaultTenantId` en `DefaultTenantId` (para invalidar `DefaultTenantId` cuando un tenant se elimina).

**Validaciones (Domain + FluentValidation)**:
- `Email` no vacío, formato RFC 5322.
- `PasswordHash` no vacío.
- `DefaultTenantId` cuando no es null DEBE corresponder a una membresía activa del propio usuario (verificado en `SetDefaultTenantCommandHandler`).

**Invariantes**:
- Un `CentralUser` con `IsGlobalMasterAdmin = true` NO puede ser eliminado si es el único master admin activo (verificación previa en `RevokeMembershipCommandHandler` y en cualquier operación que cambie el flag).

**Relaciones**:
- `1:N` con `TenantMembership` (un user, muchas membresías).
- `1:N` con `CentralUserLoginAttempt` (telemetría).
- `1:N` con la tabla estándar `ADM_CentralUserTokens` (gestionada por ASP.NET Identity, ver T014). Allí se persisten los **recovery codes de MFA** generados por `UserManager.GenerateNewTwoFactorRecoveryCodesAsync` (`TokenName = RecoveryCode`), las claves de cifrado de password reset si aplica, y los tokens estándar de email-confirm. NO requiere entidad de dominio propia.

**Nota sobre Fase 0**: la entidad `IngenIA365ERP.Domain.Entities.Security.MfaBackupCode` (per-tenant, introducida en Fase 0) queda **obsoleta** tras este refactor. Su tabla `SEC_MfaBackupCodes` debe declararse deprecated en `database/schema/15d_Tenant_Users_Refactor.sql` y eliminarse en una versión futura (no se elimina en v2 porque podría haber otras referencias en código de Fase 0 fuera del alcance de esta feature).

---

## 2. TenantMembership (ADM_TenantMemberships)

**Ubicación**: BD `IngenIA365ERP_Admin`.

**Atributos**:

| Campo | Tipo | Restricciones | Notas |
|-------|------|---------------|-------|
| `Id` | `int` | PK, IDENTITY, NOT NULL | Interno. |
| `PublicId` | `Guid` | NOT NULL, UNIQUE | Externo. |
| `CentralUserId` | `Guid` | NOT NULL, FK → `ADM_CentralUsers.Id` | |
| `TenantId` | `Guid` | NOT NULL, FK lógica → `ADM_Tenants.PublicId` | |
| `Status` | `string(20)` | NOT NULL | Enum: `Invited`, `Active`, `Suspended`, `Revoked`. |
| `IsTenantAdmin` | `bool` | NOT NULL default 0 | Admin de ESA empresa (FR-024, FR-040). |
| `InvitedByUserId` | `Guid?` | NULL, FK → `ADM_CentralUsers.Id` | Quién invitó. NULL si es el primer admin del tenant designado por master directamente. |
| `InvitedAt` | `DateTimeOffset` | NOT NULL | |
| `ActivatedAt` | `DateTimeOffset?` | NULL | Set cuando Status pasa a Active la primera vez. |
| `SuspendedAt`, `SuspendedByUserId` | `DateTimeOffset?`, `Guid?` | NULL | Última suspensión. |
| `RevokedAt`, `RevokedByUserId` | `DateTimeOffset?`, `Guid?` | NULL | Cuando Status = Revoked. |
| `RowVersion` | `byte[8]` (`ROWVERSION`) | NOT NULL | Concurrencia optimista. |
| (AuditableEntity) | | | Soft-delete + audit. |

**Índices**:
- `UQ_ADM_TenantMemberships_CentralUserId_TenantId` UNIQUE en `(CentralUserId, TenantId)` — un usuario solo puede tener UNA membresía por empresa.
- `IX_ADM_TenantMemberships_TenantId_Status` para listados rápidos por empresa.
- `IX_ADM_TenantMemberships_CentralUserId_Status` para lookup en login (membresías activas).
- `IX_ADM_TenantMemberships_TenantId_IsTenantAdmin` filtered (`WHERE IsTenantAdmin = 1`) para chequeo de "último admin".

**Máquina de estados** (FR-009a):

```text
                  (acceptInvitation)
   Invited  ─────────────────────────► Active
       │                                  ▲
       │                                  │
       │ (revokeInvitation / expire)      │ (activateMembership)
       ▼                                  │
   Revoked                            Suspended
       ▲                                  ▲
       │                                  │
       │ (revokeMembership)              │ (suspendMembership)
       └──────────  Active ──────────────┘
                       │
                       │ (revokeMembership)
                       ▼
                   Revoked

   Revoked → Active SOLO mediante nueva invitación
   (al aceptar, la membresía existente se reactiva — NO se duplica).
```

Transiciones válidas (cualquier otra es rechazada por el handler):
- `Invited → Active` (al aceptar invitación)
- `Invited → Revoked` (al revocar invitación pendiente o expirar)
- `Active → Suspended`
- `Suspended → Active`
- `Active → Revoked`
- `Suspended → Revoked`
- `Revoked → Active` SOLO al aceptar nueva invitación emitida para la misma `(CentralUserId, TenantId)`.

**Invariantes**:
- FR-040: nunca un tenant queda sin al menos una membresía con `IsTenantAdmin = 1 AND Status = Active`. Los handlers `Demote*` y `Revoke*` lo verifican transaccionalmente (ver `research.md` D-09).

---

## 3. Invitation (ADM_Invitations)

**Ubicación**: BD `IngenIA365ERP_Admin`.

**Atributos**:

| Campo | Tipo | Restricciones | Notas |
|-------|------|---------------|-------|
| `Id` | `int` | PK, IDENTITY | Interno. |
| `PublicId` | `Guid` | NOT NULL, UNIQUE | Externo. |
| `Email` | `string(256)` | NOT NULL | Email destinatario sin normalizar. |
| `NormalizedEmail` | `string(256)` | NOT NULL | Para lookup case-insensitive. |
| `TenantId` | `Guid` | NOT NULL, FK lógica → `ADM_Tenants.PublicId` | |
| `InvitedByUserId` | `Guid` | NOT NULL, FK → `ADM_CentralUsers.Id` | Emisor (tenant admin o master). |
| `InviteAsTenantAdmin` | `bool` | NOT NULL default 0 | Si la invitación designa admin. Solo master admin puede ponerlo en true (FR-025/FR-026). |
| `TokenHash` | `binary(32)` | NOT NULL, UNIQUE | SHA-256 del token. El token plano NEVER se almacena. |
| `Status` | `string(20)` | NOT NULL default 'Pending' | Enum: `Pending`, `Accepted`, `Expired`, `Revoked`. |
| `CreatedAt` | `DateTimeOffset` | NOT NULL | |
| `ExpiresAt` | `DateTimeOffset` | NOT NULL | Default `CreatedAt + 7 días`. |
| `AcceptedAt` | `DateTimeOffset?` | NULL | |
| `AcceptedByCentralUserId` | `Guid?` | NULL | Quién aceptó (puede ser CentralUser nuevo o existente). |
| `RevokedAt`, `RevokedByUserId` | `DateTimeOffset?`, `Guid?` | NULL | |
| `RowVersion` | `byte[8]` | NOT NULL | Concurrencia optimista (clave para FR-030). |
| (AuditableEntity) | | | Soft-delete + audit. |

**Índices**:
- `UQ_ADM_Invitations_TokenHash` UNIQUE en `TokenHash`.
- `IX_ADM_Invitations_NormalizedEmail_TenantId_Status` para buscar invitaciones pendientes del mismo email/tenant (edge case: invitación duplicada).
- `IX_ADM_Invitations_TenantId_Status` para listados del admin de empresa.
- `IX_ADM_Invitations_Status_ExpiresAt` para job de expiración (puede correr cada hora).

**Máquina de estados**:

```text
   Pending  ──(acceptInvitation, atómico)──► Accepted
      │
      │  (revokeInvitation)
      ▼
   Revoked
      ▲
      │  (job de expiración: ExpiresAt < now)
      │
   Pending ──► Expired
```

Cualquier transición distinta es rechazada. `Accepted` y `Revoked` y `Expired` son terminales.

**Validaciones**:
- `Email` formato válido.
- `ExpiresAt > CreatedAt`.
- `InviteAsTenantAdmin = true` solo es válido si el emisor (`InvitedByUserId`) tiene `IsGlobalMasterAdmin = true` (verificado en `IssueMasterInvitationCommandHandler` y en validator).
- `InvitedByUserId` para invitación de tenant admin debe tener una membresía activa con `IsTenantAdmin = 1` en `TenantId` (verificado en `IssueTenantInvitationCommandHandler`).

**Invariante de duplicados**: si existe una invitación con `(NormalizedEmail, TenantId, Status = Pending)`, una nueva emisión la **reemplaza** (la previa pasa a `Revoked` automáticamente con razón `Invitation.Superseded`). Esto evita acumular tokens válidos en paralelo.

---

## 4. TenantMfaPolicy (ADM_TenantMfaPolicies)

**Ubicación**: BD `IngenIA365ERP_Admin`.

**Atributos**:

| Campo | Tipo | Restricciones | Notas |
|-------|------|---------------|-------|
| `Id` | `int` | PK, IDENTITY | |
| `PublicId` | `Guid` | NOT NULL, UNIQUE | |
| `TenantId` | `Guid` | NOT NULL, UNIQUE, FK lógica → `ADM_Tenants.PublicId` | Un registro por tenant. |
| `IsRequired` | `bool` | NOT NULL default 0 | Si true → todos los miembros activos deben tener MFA. |
| `ActivatedAt`, `ActivatedByUserId` | `DateTimeOffset?`, `Guid?` | NULL | Última activación. |
| `DeactivatedAt`, `DeactivatedByUserId` | `DateTimeOffset?`, `Guid?` | NULL | Última desactivación. |
| `RowVersion` | `byte[8]` | NOT NULL | |
| (AuditableEntity) | | | |

**Índices**:
- `UQ_ADM_TenantMfaPolicies_TenantId` UNIQUE en `TenantId`.

**Validaciones**:
- Solo un admin activo del tenant o el master pueden modificar.
- Activar la política con miembros sin MFA dispara el forzado en el siguiente login de cada uno (no afecta sesiones activas hasta que refresh).

---

## 5. CentralUserLoginAttempt (ADM_CentralUserLoginAttempts)

**Ubicación**: BD `IngenIA365ERP_Admin`. **Append-only**, sin AuditableEntity.

**Atributos**:

| Campo | Tipo | Restricciones | Notas |
|-------|------|---------------|-------|
| `Id` | `bigint` | PK, IDENTITY | Volumen alto esperado. |
| `CentralUserId` | `Guid?` | NULL | NULL si el email no corresponde a ningún user (intento contra cuenta inexistente). |
| `NormalizedEmail` | `string(256)` | NOT NULL | El email intentado (para correlación incluso si no existe el user). |
| `Result` | `string(40)` | NOT NULL | Enum: `Success`, `InvalidPassword`, `UserNotFound`, `MfaRequired`, `MfaInvalid`, `LockedOut`, `Disabled`, `Other`. |
| `IpAddress` | `string(45)` | NULL | IPv4/IPv6. |
| `UserAgent` | `string(512)` | NULL | |
| `Timestamp` | `DateTimeOffset` | NOT NULL default `SYSUTCDATETIME()` | |
| `LockoutAppliedSeconds` | `int?` | NULL | Si el intento resultó en bloqueo progresivo, cuántos segundos. |

**Índices**:
- `IX_ADM_CentralUserLoginAttempts_NormalizedEmail_Timestamp` para conteo de fallos por email.
- `IX_ADM_CentralUserLoginAttempts_IpAddress_Timestamp` para análisis forense.

**Retención**: 1 año (TTL aplicado por job mantenimiento). Los eventos críticos (lockout, intentos masivos) también van a `audit_events` con retención 5 años SARLAFT.

---

## 6. PasswordResetToken (ADM_PasswordResetTokens)

**Ubicación**: BD `IngenIA365ERP_Admin`.

**Atributos**:

| Campo | Tipo | Restricciones | Notas |
|-------|------|---------------|-------|
| `Id` | `bigint` | PK, IDENTITY | Volumen moderado. |
| `PublicId` | `Guid` | NOT NULL, UNIQUE | Externo. |
| `CentralUserId` | `Guid` | NOT NULL, FK → `ADM_CentralUsers.Id` | |
| `TokenHash` | `binary(32)` | NOT NULL, UNIQUE | SHA-256 del token entregado por correo. |
| `RequesterIp` | `string(45)` | NULL | IPv4/IPv6 desde donde se solicitó. |
| `CreatedAt` | `DateTimeOffset` | NOT NULL | |
| `ExpiresAt` | `DateTimeOffset` | NOT NULL | Default `CreatedAt + 1 hora`. |
| `ConsumedAt` | `DateTimeOffset?` | NULL | Set al consumirse exitosamente. |
| `RowVersion` | `byte[8]` | NOT NULL | Concurrencia. |
| (AuditableEntity) | | | Soft-delete + audit. |

**Índices**:
- `UQ_ADM_PasswordResetTokens_TokenHash` UNIQUE.
- `IX_ADM_PasswordResetTokens_CentralUserId_CreatedAt` para limpieza periódica.

**Invariante**: al consumir, UPDATE condicional `WHERE ConsumedAt IS NULL AND ExpiresAt > now AND RowVersion = @x`. Cualquier reuso retorna `Identity.PasswordReset.AlreadyConsumed`. Una emisión nueva por el mismo `CentralUserId` invalida los tokens anteriores aún `Pending` (marca `ConsumedAt = now` con razón `Superseded` en una transacción aparte — opcional pero recomendable para minimizar superficie de ataque).

**Retención**: registros consumidos o expirados se purgan a los 30 días por un job de limpieza (similar al `InvitationExpiryJob`).

---

## 7. Refactor de SEC_Users (per-tenant)

**Antes** (estado actual, ver `src/Core/IngenIA365ERP.Domain/Entities/Security/User.cs`): 17 columnas incluyendo `PasswordHash`, `PasswordSalt`, `MfaSecret`, `IsEmailVerified`, `FailedLoginAttempts`, `LockoutEndAt`, `MustChangePassword`, `LegacyLogin`, `LastPasswordChangeAt`.

**Después** (post-refactor):

| Campo | Cambio | Justificación |
|-------|--------|---------------|
| `Id`, `PublicId` | Conservar | Identificación interna/externa. |
| `PersonId` | Conservar | Vínculo a `COR_People` (principio V). |
| `Username` | Conservar | Solo para display interno; el login NEVER usa username. |
| `Email` | Conservar (denormalizado) | Lectura rápida para listados; NEVER fuente de verdad para auth. |
| `IsActive` | Conservar | Bandera operativa del tenant (puede desactivarse independiente de la membresía global). |
| `IsMfaEnabled` | Conservar (denormalizado) | Reflejo del flag central; útil para mostrar en consola del tenant. |
| `LastLoginAt` | Conservar | Última vez que entró a ESTE tenant. |
| `CanApproveLoansMin`, `CanApproveLoansMax`, `CanOverrideLimits` | Conservar | Límites operativos del tenant — siguen viviendo aquí. |
| `IdentificationNumber` | Conservar | Dato de display del tenant. |
| `IsSaasOperator` | **Eliminar** | Reemplazado por `CentralUser.IsGlobalMasterAdmin` (vive en Admin DB). |
| `PasswordHash`, `PasswordSalt` | **Eliminar** | Credencial vive en `ADM_CentralUsers`. |
| `MfaSecret` | **Eliminar** | Idem. |
| `IsEmailVerified` | **Eliminar** | Equivalente a `EmailConfirmed` en `ADM_CentralUsers`. |
| `FailedLoginAttempts`, `LockoutEndAt` | **Eliminar** | Idem. |
| `MustChangePassword` | **Eliminar** | Idem. |
| `LastPasswordChangeAt` | **Eliminar** | Vive en `ADM_CentralUsers`. |
| `LegacyLogin` | **Eliminar** | Era para compatibilidad con sistema anterior; no aplica en nueva identidad. |
| **`CentralUserId Guid`** | **Añadir** | FK lógica a `ADM_CentralUsers.Id`. Único por tenant. |
| **`CentralUserPublicEmail string(256)`** | **Añadir** (denormalizado) | Espejo del email central para listados sin cross-DB join. |

**Nuevo índice**: `UQ_SEC_Users_CentralUserId` UNIQUE en `CentralUserId` (una sola fila de usuario per-tenant por CentralUser).

**Invariante**: una membresía activa en `ADM_TenantMemberships(CentralUserId=X, TenantId=Y)` DEBE corresponder a una fila en `SEC_Users(CentralUserId=X)` dentro de la BD del tenant Y. La creación/eliminación se sincroniza en los handlers `AcceptInvitationCommand` (crea la fila del tenant) y `RevokeMembershipCommand` (soft-delete la fila del tenant). Un test de integración verifica esta consistencia.

**Tablas dependientes** (relaciones existentes que se conservan): `UserRole`, `UserBranchAssignment`, `UserMenuAccess`, `UserSession`, `UserTenantAssignment`. Estas siguen funcionando — su FK es a `SEC_Users.Id` (no a `CentralUserId`). El refactor es transparente para ellas. **`UserTenantAssignment` queda deprecado** en código nuevo (su rol lo asume `ADM_TenantMemberships`), pero su DDL se conserva para no romper migraciones existentes. En una versión futura se eliminará.

---

## Resumen

| Entidad | Ubicación | Tipo | Auditable | Estado clave |
|---------|-----------|------|-----------|--------------|
| `CentralUser` | Admin DB | Nueva (bridge ASP.NET Identity) | Sí | `Status` (Active/Pending/Disabled) |
| `TenantMembership` | Admin DB | Nueva | Sí | `Status` (Invited/Active/Suspended/Revoked) |
| `Invitation` | Admin DB | Nueva | Sí | `Status` (Pending/Accepted/Expired/Revoked) |
| `TenantMfaPolicy` | Admin DB | Nueva | Sí | `IsRequired` (bool) |
| `CentralUserLoginAttempt` | Admin DB | Nueva, append-only | No | `Result` (enum) |
| `PasswordResetToken` | Admin DB | Nueva | Sí | `ConsumedAt IS NULL` (válido) |
| `User` (SEC_Users) | Tenant DB | **Refactorizada** | Sí | `IsActive`; auth delegada |

Total: 6 entidades nuevas + 1 refactor. 13 índices nuevos. 2 máquinas de estado explícitas (membership, invitation).
