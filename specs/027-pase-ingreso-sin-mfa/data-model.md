# Modelo de datos — 027

## `ADM_LoginPasses` (nueva, base administrativa) — entidad `LoginPass : AuditableEntity`

| Campo | Tipo | Regla |
|---|---|---|
| `Id`, `PublicId`, auditoría, `IsDeleted`, `RowVersion` | heredados | |
| `CentralUserId` | uuid | destinatario; índice con `TenantId` |
| `TenantId` | uuid | PublicId de la cooperativa que lo concede |
| `GrantedByUserId` | uuid | actor (cuenta central) |
| `GrantedByMaster` | bool | lo dio el maestro |
| `Reason` | varchar(500) | 10–500 |
| `TokenHash` | bytea/varbinary(32) | SHA-256; único entre no borrados |
| `SecurityStampAtGrant` | varchar(256) null | sello de la cuenta al conceder |
| `GrantedAt` | timestamp | índice con `TenantId` (tope diario) |
| `ExpiresAt` | timestamp | `GrantedAt + horas` (1–12) |
| `UseCount` | int | 0 |
| `LastUsedAt`, `LastUsedIp` | null | |
| `VoidedAt`, `VoidedByUserId`, `VoidReason` (varchar 64) | null | anulación |

**Estados** (calculados): `Active` (sin anular y `ExpiresAt > ahora`), `Expired`, `Voided`.
**Motivos de anulación**: `revocado-por-administrador`, `reemplazado`, `entro-con-segundo-factor`, `opcion-apagada`,
`cambio-de-credenciales`, `cuenta-inactiva`, `sin-membresia`.

## `ADM_TenantMfaPolicies` (existente, dos columnas)

| Campo | Tipo | Defecto |
|---|---|---|
| `AllowLoginPass` | bool | `false` |
| `LoginPassMaxHours` | int | `12` (1–12) |

## Sesión

- Access JWT: claim `login_pass_tenant` = PublicId de la cooperativa (sólo en sesiones abiertas con pase).
- `CentralRefreshSession.PaseDeIngresoCooperativa` (Guid?, defecto `null`).
