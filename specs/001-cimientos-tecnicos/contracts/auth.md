# Contract — Auth (Fase 0)

> ## ⚠️ Mayormente superado — leé esto antes de usarlo
>
> **De las once rutas que documenta, hoy sólo existen cuatro.** Las demás se
> retiraron el 2026-08-25 al desmontar la superficie de autenticación de Fase 0:
> operaban sobre `SEC_Users` —el modelo por cooperativa— cuando la
> autenticación ya vivía en la identidad central, así que devolvían «no
> autenticado» pasaran las credenciales que pasaran.
>
> | Ruta | Hoy |
> |---|---|
> | `/login`, `/mfa/verify`, `/refresh`, `/logout` | Existen, pero **el contrato vivo es** [specs/002/contracts/auth.md](../../002-identidad-central-federada/contracts/auth.md). El cuerpo cambió: `{email, password}`, sin tenant ni username. |
> | `/logout-all` | **Vive.** Reescrito: rota el `SecurityStamp` de quien llama y con eso caen todas sus sesiones. |
> | `/mfa/reset/request`, `/mfa/reset/{id}/approve`, `/mfa/reset/requests` | **Viven, y desde el 2026-08-26 además funcionan.** Ver la nota de abajo. |
>
> **Sobre el reseteo con doble aprobación.** Existía desde Fase 0 y no servía,
> por dos fallos encadenados. Resolvía a quien llamaba con
> `ICurrentUserService.UserId`, que parsea como `int` el claim `uid` —un claim
> que el emisor central no pone—, así que las tres rutas respondían **401
> «No autenticado» a cualquiera**: nadie podía radicar una solicitud, ni
> aprobarla, ni ver la cola, y la pantalla `MfaResetApprovals.razor` era
> decorativa. Y aunque se llegara, el reset limpiaba `MfaSecret` sobre
> `SEC_Users` mientras el login verifica contra `ADM_CentralUsers`: la persona
> habría seguido bloqueada tras recibir el correo que le decía lo contrario.
>
> Hoy quien llama se resuelve por el puente `SEC_Users.CentralUserId`, el reset
> cae sobre la identidad central y rota el `SecurityStamp`, y si ese puente
> falta el handler **falla** con `Auth.MfaResetSinIdentidadCentral` en vez de
> decir `Executed`. Las tres rutas exigen ahora los permisos que esta misma
> tabla ya documentaba (`Security.MfaReset.Request` / `.Approve`) y que nunca
> se habían exigido: mientras el reset no hacía nada daba igual quién aprobara,
> pero en cuanto funciona, sin permiso bastarían dos cuentas cualesquiera de la
> cooperativa para dejar a quien fuera sin segundo factor. Falta de permiso
> responde **404 indistinguible**, no 403.
>
> Lo cubre `EndToEnd_MfaResetDobleAprobacion`, que cierra el circuito: tras las
> dos aprobaciones vuelve al login del afectado y exige `MfaEnrollmentRequired`.
> Esa prueba no existía, y por eso el defecto llegó hasta acá.
> | `/mfa/enroll/start`, `/mfa/enroll/confirm`, `/mfa/backup-codes/regenerate`, `/password/change` | **404.** Lo vivo es `/api/profile/mfa/enroll`, `/api/profile/mfa/confirm`, `/api/profile/mfa/recovery-codes/regenerate` y `/api/profile/password`. |
>
> Dos cosas más que este documento da por buenas y ya no lo son: los fallos de
> autenticación responden **401**, no 422; y el administrador maestro **necesita
> segundo factor** para entrar.
>
> Se conserva como registro de lo que fue Fase 0. No se reescribe: falsificaría
> la traza.

**Module**: `IngenIA365ERP.API/Modules/AuthModule.cs`
**Base path**: `/api/auth`
**Auth**: anonymous (login, mfa/verify, refresh); authenticated for the rest.
**Common error envelope**: `{ code: "Modulo.Codigo", message: "texto en español", traceId: "..." }`.

All identifiers in payload are `PublicId` (Guid). All timestamps are ISO 8601 UTC.

---

## POST `/api/auth/login`

Inicia el flujo de autenticación con usuario + contraseña.

**Request body**

```json
{
  "tenantSubdomainOrNit": "string (optional, used if user belongs to multiple tenants)",
  "username": "string",
  "password": "string"
}
```

**Responses**

- `200 OK` — credenciales correctas; MFA pendiente.
  ```json
  {
    "mfaChallengeToken": "opaque-short-lived (5 min)",
    "mustChangePassword": false
  }
  ```
- `200 OK + mustChangePassword=true` — cambio de contraseña obligatorio antes de continuar.
- `400 Bad Request`
  - `Auth.InvalidCredentials` — usuario o contraseña inválidos. (Mensaje genérico — no revela cuál.)
- `403 Forbidden`
  - `Auth.AccountLocked` — cuenta bloqueada hasta `LockoutEndAt`.
  - `Auth.AccountDisabled` — `User.IsActive = false`.
- `429 Too Many Requests` — throttling de login por IP.

**Side effects**: registra `LoginAttempt`; si falla, incrementa `FailedLoginAttempts`; al cruzar `LockoutThreshold`, emite notificación `AccountLocked` y entrada en audit log.

---

## POST `/api/auth/mfa/verify`

Verifica el segundo factor TOTP (o código de respaldo) usando el `mfaChallengeToken` del login.

**Request body**

```json
{
  "mfaChallengeToken": "string",
  "totpCode": "string (6 digits)",
  "useBackupCode": false,
  "backupCode": "string (optional, when useBackupCode=true)",
  "branchPublicId": "Guid? (selección de sucursal)"
}
```

**Responses**

- `200 OK`
  ```json
  {
    "accessToken": "JWT RS256, ttl 30 min",
    "refreshToken": "opaque, ttl 12 h",
    "accessTokenExpiresAt": "2026-05-28T14:30:00Z",
    "refreshTokenExpiresAt": "2026-05-29T01:00:00Z",
    "user": {
      "publicId": "Guid",
      "username": "string",
      "tenant": { "publicId": "Guid", "name": "..." },
      "branch": { "publicId": "Guid", "name": "..." } | null,
      "roles": [ "CompanyAdmin", "Operator" ]
    }
  }
  ```
- `400`
  - `Auth.InvalidMfaCode` — TOTP inválido o ventana excedida.
  - `Auth.InvalidBackupCode` — código de respaldo inválido o ya usado.
  - `Auth.MfaChallengeExpired` — el challenge token expiró (5 min).
- `403`
  - `Auth.BranchNotAuthorized` — el usuario no tiene asignación a la sucursal elegida.

**Side effects**: emite audit log `Login`; resetea `FailedLoginAttempts`; si se usó backup code, lo marca `UsedAt`.

---

## POST `/api/auth/refresh`

Rota access + refresh.

**Request body**

```json
{
  "refreshToken": "string"
}
```

**Responses**

- `200 OK` (mismo shape que mfa/verify, sin `user`).
- `401`
  - `Auth.InvalidRefreshToken` — token desconocido, expirado o revocado.
  - `Auth.RefreshTokenReuseDetected` — token ya invalidado: se invalida la familia completa y se emite notificación `SuspiciousSessionActivity`.

---

## POST `/api/auth/logout`

Revoca el refresh actual.

**Request body**

```json
{ "refreshToken": "string" }
```

**Response**: `204 No Content`.

---

## POST `/api/auth/logout-all`

Revoca **todas** las sesiones del usuario (todas las familias). Requiere auth.

**Response**: `204 No Content`.

---

## POST `/api/auth/mfa/enroll/start`

Inicia inscripción de MFA. Exige contraseña vigente.

**Request body**

```json
{ "password": "string" }
```

**Response 200**

```json
{
  "secret": "string (base32)",
  "qrCodeSvg": "string (SVG inline)",
  "enrollmentToken": "opaque, ttl 10 min"
}
```

---

## POST `/api/auth/mfa/enroll/confirm`

Confirma la inscripción enviando un TOTP válido.

**Request body**

```json
{
  "enrollmentToken": "string",
  "totpCode": "string"
}
```

**Response 200**

```json
{
  "backupCodes": [ "ABCDE-FGHJK", "..." ]  // 10 códigos en claro, mostrados UNA VEZ
}
```

**Errors**

- `Auth.InvalidMfaCode`
- `Auth.EnrollmentExpired`

**Side effects**: `User.IsMfaEnabled = true`, `MfaSecret = cifrado(secret)`; genera 10 `MfaBackupCode` con `BatchId` nuevo; audit log.

---

## POST `/api/auth/mfa/backup-codes/regenerate`

Regenera el set de códigos de respaldo invalidando los anteriores. Exige TOTP vigente.

**Request body**

```json
{ "totpCode": "string" }
```

**Response 200**: shape igual a enroll/confirm (10 códigos nuevos en claro).

---

## POST `/api/auth/mfa/reset/request`

Solicita reset administrativo del MFA. Solo permitido si el usuario no puede usar TOTP ni backup codes.

**Request body**

```json
{
  "targetUserPublicId": "Guid",
  "reason": "string (<=500 chars)",
  "evidenceAttachmentPublicId": "Guid? (Attachment.PublicId con foto cédula)"
}
```

**Response 201**

```json
{ "requestPublicId": "Guid", "expiresAt": "..." }
```

---

## POST `/api/auth/mfa/reset/{requestPublicId}/approve`

Aprobación por un administrador de empresa. Necesita dos aprobaciones distintas para ejecutar.

**Response**

- `200 OK { "status": "Approved | Executed" }`
- `400 Auth.MfaResetAlreadyApprovedBySameUser`
- `403 Auth.MfaResetCannotApproveOwnRequest`
- `409 Auth.MfaResetExpired`

**Side effects**: cuando se acumulan dos aprobaciones, ejecuta automáticamente el reset (limpia `MfaSecret`, invalida backup codes), envía correo al usuario, emite audit log con identidad de ambos aprobadores y la evidencia.

---

## POST `/api/auth/password/change`

Cambia la contraseña del usuario autenticado. Aplica al flujo de cambio obligatorio post-login (no requiere MFA adicional dentro del mismo challenge).

**Request body**

```json
{
  "currentPassword": "string",
  "newPassword": "string"
}
```

**Errors**

- `Auth.WrongCurrentPassword`
- `Auth.PasswordPolicyViolation` — detalle en mensaje (largo, complejidad, reuso).

---

## Authorization summary

| Endpoint | Permission |
|---|---|
| login, mfa/verify, refresh, password/change (en flujo obligado) | anonymous |
| logout, logout-all, mfa/enroll/*, mfa/backup-codes/regenerate | authenticated |
| mfa/reset/request | `Security.MfaReset.Request` |
| mfa/reset/{id}/approve | `Security.MfaReset.Approve` |
