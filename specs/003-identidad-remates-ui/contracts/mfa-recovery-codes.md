# Contract — Canje y regeneración de recovery codes

## 1. Canje en el desafío MFA (endpoint existente, rama nueva)

`POST /api/auth/mfa/verify` — Bearer challenge token `purpose=mfa-verify`
(sin cambios de autorización).

### Request

```json
{
  "code": "RYBK3-Y7MF3",
  "useRecoveryCode": true
}
```

- `useRecoveryCode` (bool, opcional, default `false`): con `false` el
  comportamiento actual (TOTP 6–8 dígitos) no cambia.
- Con `true`, `code` acepta el formato de los códigos de Identity
  (alfanumérico con guion, 5–20 caracteres).

### Response 200 — igual que el verify actual, más el contador

```json
{
  "challenge": "None",
  "accessToken": "eyJ…",
  "refreshToken": "…",
  "activeTenantPublicId": "…",
  "activeTenantName": "…",
  "recoveryCodesRemaining": 7
}
```

- `recoveryCodesRemaining` (int, solo presente cuando se canjeó un recovery
  code): la UI muestra el aviso "te quedan N códigos" y recomienda
  regenerar cuando `N <= 3` (FR-110).
- Las demás ramas del verify (TenantSelection, NoActiveMembership) aplican
  igual que hoy.

### Errores

| HTTP | code | Caso |
|---|---|---|
| 422 | `Identity.MfaInvalid` | Código de recuperación usado, inexistente o mal formado — **mismo mensaje que un TOTP incorrecto** (FR-109). Cuenta para el lockout progresivo. |
| 401 | `Identity.WrongTokenPurpose` | Token sin purpose `mfa-verify` (sin cambios). |

### Auditoría

- Éxito → `CentralUser.Mfa.RecoveryCodeUsed` (con códigos restantes).
- Fallo → `CentralUser.Mfa.RecoveryCodeFailed` (sin el código intentado).

## 2. Regeneración desde el perfil (endpoint nuevo)

`POST /api/profile/mfa/recovery-codes/regenerate` — Bearer `purpose=full`.

### Request — exactamente UNA confirmación de identidad

```json
{ "currentPassword": "…" }
```

o bien

```json
{ "totpCode": "123456" }
```

Validator: XOR estricto entre ambas; MFA debe estar activo para regenerar.

### Response 200

```json
{
  "recoveryCodes": [
    "AAAAA-BBBBB", "…8 más…", "JJJJJ-KKKKK"
  ]
}
```

- Los 10 códigos nuevos se muestran **una sola vez**; el juego anterior
  queda invalidado por completo (FR-111).

### Errores

| HTTP | code | Caso |
|---|---|---|
| 422 | `Identity.InvalidCredentials` | Password o TOTP de confirmación incorrectos. |
| 422 | `Profile.Mfa.NotEnrolled` | El usuario no tiene MFA activo. |
| 400 | `Validation.Invalid` | Cero o dos métodos de confirmación. |

### Auditoría

- `Profile.RecoveryCodesRegenerated` con el método de confirmación usado.

## 3. Consumidores de UI

- `MfaChallenge.razor`: toggle "Usar un código de recuperación" que cambia
  el input y setea `useRecoveryCode=true`; tras canjear muestra el aviso de
  restantes (banner destacado si quedan ≤ 3 o 0).
- `MfaEnrollmentCentral.razor` (/profile/mfa): sección "Códigos de
  recuperación" con el conteo actual (vía `GET /api/auth/me`, ver contrato
  del 002) y el botón regenerar con su confirmación.
