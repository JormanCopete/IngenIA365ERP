# Contract: Auth (autenticación central)

**Módulo Carter**: `AuthModule.cs` (refactor del existente)
**Ruta base**: `/api/auth`
**Autorización**: pública (login/refresh) salvo `GET /api/auth/me` y `POST /api/auth/logout`.

---

## POST /api/auth/login

Autentica al usuario contra la identidad central. NO recibe ni acepta tenant.

**Request**:

```json
{
  "email": "ana.perez@cooperativa.coop",
  "password": "M1Cl4v3Seg|ra2026"
}
```

**Responses**:

- `200 OK` (login exitoso, sin MFA y sin política de MFA obligatorio pendiente):
  ```json
  {
    "challenge": "TenantSelection",
    "centralUserId": "8f4...c2",
    "email": "ana.perez@cooperativa.coop",
    "isGlobalMasterAdmin": false,
    "challengeToken": "eyJ...",
    "activeTenants": [
      { "tenantPublicId": "...", "tenantName": "Coop. Solidaria", "isTenantAdmin": false },
      { "tenantPublicId": "...", "tenantName": "Coop. del Pacífico", "isTenantAdmin": true }
    ],
    "defaultTenantPublicId": "...",
    "autoSelected": false
  }
  ```
  Cuando `activeTenants.length == 1` o cuando hay `defaultTenantPublicId` válida → `autoSelected = true` y la respuesta incluye `accessToken`/`refreshToken` directamente (sin intermediar selector).

- `200 OK` con `challenge = "MfaRequired"`:
  ```json
  {
    "challenge": "MfaRequired",
    "challengeToken": "eyJ...",
    "challengeTokenPurpose": "mfa-verify",
    "expiresInSeconds": 300
  }
  ```
  El `challengeToken` es un JWT firmado con claim `purpose=mfa-verify`. Solo es válido para `POST /api/auth/mfa/verify`; cualquier otro endpoint lo rechaza con `403 Identity.WrongTokenPurpose`. El cliente lo envía en `Authorization: Bearer ...`.

- `200 OK` con `challenge = "MfaEnrollmentRequired"`:
  Cuando alguna empresa del usuario tiene política "MFA obligatorio" y el usuario aún no configuró MFA (FR-003b, FR-003c).
  ```json
  {
    "challenge": "MfaEnrollmentRequired",
    "challengeToken": "eyJ...",
    "challengeTokenPurpose": "mfa-enroll",
    "enrollmentUrl": "/auth/enroll-mfa-forced",
    "tenantsRequiringMfa": [{ "tenantPublicId": "...", "tenantName": "..." }]
  }
  ```
  El `challengeToken` tiene claim `purpose=mfa-enroll` y solo es aceptado por `POST /api/profile/mfa/enroll` y `POST /api/profile/mfa/confirm`. Tras la confirmación exitosa, `mfa/confirm` devuelve en su respuesta un JWT operativo (`purpose=full`) con `active_tenant_id` ya resuelto, completando el login sin re-loguear.

- `200 OK` con `challenge = "TenantSelection"`:
  ```json
  {
    "challenge": "TenantSelection",
    "challengeToken": "eyJ...",
    "challengeTokenPurpose": "tenant-select",
    "activeTenants": [ { "tenantPublicId": "...", "tenantName": "...", "isTenantAdmin": true } ],
    "defaultTenantPublicId": null
  }
  ```
  El `challengeToken` (`purpose=tenant-select`) solo es aceptado por `POST /api/sessions/select-tenant`, que responde con el JWT operativo final.

- `200 OK` con `challenge = "NoActiveMembership"` (FR-015):
  ```json
  {
    "challenge": "NoActiveMembership",
    "message": "No tienes acceso a ninguna empresa. Solicita una invitación."
  }
  ```
  El cliente muestra `Pages/Auth/NoMembershipNotice.razor`. NO se emite JWT.

  **Excepción: el administrador maestro global.** No tiene membresías por
  diseño —gobierna el conjunto de cooperativas, no pertenece a ninguna— y no
  recibe `NoActiveMembership` sino el challenge de su segundo factor:
  `MfaEnrollmentRequired` si aún no lo tiene inscrito, `MfaRequired` si sí. Su
  sesión operativa la emite `POST /api/auth/mfa/verify`, nunca este endpoint.
  Antes entraba con sólo contraseña, y es la cuenta que crea cooperativas,
  apaga la política de MFA de una cooperativa ajena y borra el segundo factor
  de cualquiera (FR-003).

- `401 Unauthorized` con `{ "errorCode": "Identity.InvalidCredentials", "message": "Credenciales inválidas." }` (FR-041 — mensaje genérico).

- `423 Locked` con `{ "errorCode": "Identity.Locked.Soft", "message": "Cuenta bloqueada temporalmente. Reintenta en {N} segundos.", "retryAfterSeconds": 60 }` (FR-042, FR-046).

**Auditoría**: cada intento genera evento `CentralUser.Login.{Success|Failed|Locked}` con email, IP, User-Agent, resultado.

---

## POST /api/auth/mfa/verify

Verifica el código TOTP del segundo factor.

**Request**:

```json
{
  "challengeToken": "eyJ...",
  "code": "123456"
}
```

**Responses**:

- `200 OK` con la misma forma que la respuesta `200 OK` exitosa de `/login` (incluye `activeTenants` y posiblemente `accessToken` si `autoSelected`).
- `401 Unauthorized` con `Identity.MfaInvalid` si el código es incorrecto.
- `410 Gone` con `Identity.ChallengeExpired` si el `challengeToken` expiró (>5 min).

**Auditoría**: `CentralUser.Mfa.{Success|Failed}`.

---

## POST /api/auth/refresh

Renueva el access token usando el refresh token (rotation).

**Request**:

```json
{ "refreshToken": "eyJ..." }
```

**Responses**:

- `200 OK`:
  ```json
  {
    "accessToken": "eyJ...",
    "refreshToken": "eyJ... (nuevo, el anterior queda invalidado)",
    "expiresInSeconds": 1800
  }
  ```
- `401 Unauthorized` con `Identity.RefreshToken.Invalid` (token revocado, expirado, o family-violation).

---

## POST /api/auth/logout

Invalida el refresh token actual y limpia la sesión.

**Request**: `{}` (el access token va en `Authorization: Bearer ...`).

**Response**: `204 No Content`.

**Auditoría**: `CentralUser.Logout`.

---

## POST /api/auth/logout-all

Cierra **todas** las sesiones de quien llama, en todos sus dispositivos.

Rota el `SecurityStamp` del usuario. No recorre sesiones una por una porque no
hace falta: el refresh compara el stamp guardado en la sesión contra el actual,
así que un stamp nuevo las invalida todas de golpe — incluidas las que ya
estaban emitidas. No toca la contraseña.

Cierra las sesiones **de quien llama**, no las de otro: el usuario sale del
token, nunca del cuerpo. Lo prescribe el runbook ante sospecha de fuga de un
token privilegiado.

**Request**: `{}` (el access token va en `Authorization: Bearer ...`).

**Response**: `204 No Content`.

- `401 Unauthorized` con `Identity.Unauthenticated` si no hay sesión.
- `404 Not Found` con `Generic.NotFound` si la cuenta ya no existe — y no
  `204`: devolver éxito sin haber cerrado nada haría creer que se contuvo el
  incidente.

---

## GET /api/auth/me

Información del usuario autenticado y su sesión.

**Authorization**: `Bearer` JWT.

**Response** `200 OK`:

```json
{
  "centralUserId": "...",
  "email": "...",
  "isGlobalMasterAdmin": false,
  "mfaEnabled": true,
  "activeTenant": {
    "tenantPublicId": "...",
    "tenantName": "Coop. Solidaria",
    "isTenantAdmin": true,
    "isMfaRequiredByPolicy": false
  },
  "availableTenants": [
    { "tenantPublicId": "...", "tenantName": "...", "isTenantAdmin": false },
    { "tenantPublicId": "...", "tenantName": "...", "isTenantAdmin": true }
  ],
  "defaultTenantPublicId": "..."
}
```

Cubre los datos necesarios para renderizar el `TenantSwitcher.razor` y la pantalla de perfil sin extra round-trips.
