# Data Model — Remates de Identidad Central (feature 003)

**Este feature no introduce entidades ni columnas nuevas.** Documenta los
almacenes existentes que reutiliza y los eventos de auditoría que agrega.

## 1. Almacenes reutilizados

### 1.1 Recovery codes — `ADM_CentralUsers` + `ADM_CentralUserTokens` (BD Admin)

Los códigos de recuperación se generan en el enrollment del feature 002 vía
`UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10)` y se
persisten **hasheados** en la tabla estándar de ASP.NET Identity
`ADM_CentralUserTokens` (login-provider `[AspNetUserStore]`, name
`RecoveryCodes`). Operaciones que este feature agrega sobre ese almacén:

| Operación | API de Identity | Semántica |
|---|---|---|
| Canje | `RedeemTwoFactorRecoveryCodeAsync` | One-shot atómico: el código se elimina de la lista al canjear; reuso imposible. |
| Conteo | `CountRecoveryCodesAsync` | Alimenta `RecoveryCodesRemaining` y el aviso de "quedan ≤ 3". |
| Regeneración | `GenerateNewTwoFactorRecoveryCodesAsync` | Reemplaza el juego completo; los anteriores quedan inválidos. |

Restricciones heredadas: los códigos jamás se re-muestran después de la
generación; los fallos de canje cuentan para el lockout progresivo del 002.

### 1.2 Solicitudes de MFA reset — `SEC_MfaResetRequests` (BD del tenant, Fase 0)

Entidad existente `MfaResetRequest : AuditableEntity`
(`src/Core/IngenIA365ERP.Domain/Entities/Security/MfaResetRequest.cs`):
`UserId`, `RequestedBy`, `RequestedAt`, `Reason`, `EvidenceAttachmentId?`,
`Status (Pending|Approved|Rejected|Executed|Expired)`, primera/segunda
aprobación (aprobadores distintos, el solicitante no se auto-aprueba),
`ExpiresAt` (ventana 24 h).

Este feature agrega **solo lectura**: `ListMfaResetRequestsQuery` paginada
por `Status`, bajo el tenant context del middleware. El DTO expone
`PublicId` (nunca `int Id`, principio VI) y resuelve los nombres/correos de
solicitante y afectado para que el aprobador no maneje identificadores
técnicos (SC-106).

### 1.3 Sesión de cliente — `sessionStorage` del navegador (Web/WASM)

No es una tabla: las keys existentes `auth_token`/`refresh_token` del
`ISecureStorage` pasan del diccionario en memoria a
`window.sessionStorage` (alcance solo-pestaña, decisión de Clarifications).
Sin cambios en el modelo de sesión del servidor (Redis) ni en los TTL.

## 2. Estados y transiciones relevantes

- **Recovery code**: `Emitido → Canjeado (terminal)` o
  `Emitido → Invalidado por regeneración (terminal)`. No existe "reactivar".
- **MfaResetRequest** (existente, sin cambios): `Pending → Approved →
  Executed`, `Pending → Rejected`, `Pending → Expired`. El listado solo
  refleja estos estados; la doble aprobación sigue en los handlers de Fase 0.

## 3. Eventos de auditoría nuevos (MongoDB, TTL SARLAFT heredado)

| Evento | Cuándo | Payload mínimo |
|---|---|---|
| `CentralUser.Mfa.RecoveryCodeUsed` | Canje exitoso en el desafío MFA | userId, códigos restantes, IP/UA |
| `CentralUser.Mfa.RecoveryCodeFailed` | Canje fallido (código usado/inexistente) | userId, IP/UA (sin el código) |
| `Profile.RecoveryCodesRegenerated` | Regeneración desde el perfil | userId, método de confirmación (password/TOTP) |

`Session.TenantSwitched` (ya existente desde el 002) cubre la auditoría del
switcher sin eventos nuevos.

## 4. Invariantes que el diseño debe conservar

1. Un recovery code canjeado o invalidado NUNCA vuelve a ser válido, ni ante
   canjes concurrentes (garantizado por el token store de Identity — la
   lista se reescribe en el canje).
2. El mensaje de error del canje es indistinguible del TOTP incorrecto
   (FR-109).
3. `ListMfaResetRequestsQuery` jamás cruza tenants (principio IV).
4. El logout elimina todo artefacto de `sessionStorage` (FR-114).
