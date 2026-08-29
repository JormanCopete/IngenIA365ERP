# Contract: Profile & Account Recovery

**Módulos Carter**: `ProfileModule.cs`, `AuthRecoveryModule.cs`
**Rutas**: `/api/profile/*` (autenticadas), `/api/auth/password/*` (públicas)

Estos endpoints completan la spec en torno a FR-003 (MFA opt-in), FR-003b (forced enrollment), FR-003c (no MFA partial), FR-043/FR-044 (política NIST + Pwned), FR-045 (retroalimentación dual), y la assumption "Recuperación de contraseña" del spec.

---

## POST /api/profile/mfa/enroll

Inicia el flujo de configuración de MFA. Genera el secret TOTP; NO lo persiste todavía — se confirma en el siguiente call.

> **Este endpoint ya no devuelve `recoveryCodes`, y es un cambio deliberado del
> 2026-08-26.** Los devolvía, y no servían: se generaban aquí con un formato
> propio (`AB12-CD34`), se guardaban en Redis y se **descartaban** al confirmar,
> porque los códigos válidos los emite ASP.NET Identity en `/confirm` con otro
> alfabeto. Quien anotara los del `enroll` y cerrara la pantalla antes de
> confirmar se quedaba con diez códigos que nunca se iban a canjear — justo el
> papelito que uno guarda para el día que pierde el teléfono.
>
> Los códigos de recuperación salen **sólo** de `POST /api/profile/mfa/confirm`
> y de `POST /api/profile/mfa/recovery-codes/regenerate`. Ninguno de los dos
> cambia.

**Authorization**: JWT central con `purpose=full` (enrollment voluntario desde perfil) **o** `purpose=mfa-enroll` (enrollment forzado, emitido por `POST /api/auth/login` cuando la política de algún tenant lo exige). Cualquier otro `purpose` → `403 Identity.WrongTokenPurpose`.

**Request**: `{}` (vacío).

**Response** `200 OK`:

```json
{
  "secretBase32": "JBSWY3DPEHPK3PXP",
  "otpAuthUri": "otpauth://totp/IngenIA365ERP:ana@coop.test?secret=JBSWY3DPEHPK3PXP&issuer=IngenIA365ERP&algorithm=SHA1&digits=6&period=30",
  "qrPngDataUri": "data:image/png;base64,iVBORw0KGgo…",
  "expiresInSeconds": 600
}
```

`qrPngDataUri` es el `otpAuthUri` ya convertido en imagen, listo para un
`<img src>`. Va **incrustado** y no como una ruta que el navegador pida aparte:
una URL que sirviera este QR sería una URL que contiene, en la práctica, el
segundo factor, y acabaría en el registro de accesos del servidor, del proxy y de
cualquier intermediario.

El `secretBase32` se sigue mostrando debajo a propósito: quien use lector de
pantalla, o tenga la cámara rota, necesita poder escribirlo.

El secret pendiente se almacena en Redis con TTL 10 min; si el usuario no
confirma a tiempo, debe reiniciar.

**`409 Conflict`** con `Profile.Mfa.DemasiadasCredenciales` si ya llegó al máximo
de autenticadores (5). Se resuelve retirando alguno.

---

## POST /api/profile/mfa/confirm

Confirma el setup de MFA introduciendo un código TOTP válido generado con el secret entregado en `/enroll`.

**Authorization**: igual que `/enroll` — JWT con `purpose=full` o `purpose=mfa-enroll`.

**Request**:

```json
{ "code": "123456" }
```

**Responses**:

- **Si el JWT entrante tenía `purpose=full` (enrollment voluntario)** → `204 No Content`. Los refresh tokens existentes siguen siendo válidos — solo el comportamiento de futuros logins cambia.
- **Si el JWT entrante tenía `purpose=mfa-enroll` (enrollment forzado desde login)** → `200 OK` con el JWT operativo completo, **elevando la sesión scope-limited a una sesión full sin requerir re-login**:
  ```json
  {
    "accessToken": "eyJ... (purpose=full, active_tenant_id resuelto)",
    "refreshToken": "eyJ...",
    "expiresInSeconds": 1800,
    "tenant": { "tenantPublicId": "...", "tenantName": "..." },
    "autoSelected": true
  }
  ```
  Si tras el enrollment el usuario tiene más de un tenant Y no hay `DefaultTenantId` válido, en lugar de un access/refresh tokens, la respuesta es `{ "challenge": "TenantSelection", "challengeToken": "...", "challengeTokenPurpose": "tenant-select", "activeTenants": [...] }` (idéntica a la rama TenantSelection del login).
- `400 Bad Request` con `Profile.Mfa.SetupExpired` si no hay secret pendiente en Redis.
- `401 Unauthorized` con `Profile.Mfa.InvalidCode` si el TOTP no coincide.

**Auditoría**: `Profile.MfaEnrolled`.

---

## POST /api/profile/mfa/disable

Desactiva MFA del usuario. Requiere validación de contraseña actual y **rechaza** si alguno de los tenants donde el usuario es miembro activo tiene política "MFA obligatorio" (FR-003c).

**Authorization**: JWT central válido.

**Request**:

```json
{ "currentPassword": "..." }
```

**Responses**:

- `204 No Content`.
- `401 Unauthorized` con `Identity.InvalidCredentials` si la contraseña actual no coincide.
- `403 Forbidden` con `Profile.Mfa.RequiredByTenantPolicy` si al menos un tenant
  lo exige. **No hay body `{ "tenantName": … }`**: eso lo prometía este contrato y
  nunca existió. El nombre de la cooperativa va dentro de `message`, que es lo que
  la pantalla muestra. El 403 sí es cierto desde el 2026-08-26 — antes caía al
  422 por defecto.
- `403 Forbidden` con `Profile.Mfa.RequiredForMasterAdmin` si quien llama es el
  administrador maestro. No tiene membresías por diseño, así que la comprobación
  de arriba no lo cubría: la única cuenta que crea cooperativas y borra el segundo
  factor de cualquiera era, justamente, la que podía quedarse sin el suyo.

**Auditoría**: `Profile.MfaDisabled`.

---

## GET /api/profile/mfa/credentials

Los autenticadores de quien llama. Sin parámetros: siempre son los suyos, sacados
del token.

**Response** `200 OK`:

```json
{
  "credenciales": [
    {
      "publicId": "…",
      "label": "iPhone de Ana",
      "createdAt": "2026-08-20T14:02:11Z",
      "confirmedAt": "2026-08-20T14:02:40Z",
      "lastUsedAt": "2026-08-26T09:31:02Z"
    }
  ],
  "codigosDeRecuperacionRestantes": 8
}
```

`label` y `confirmedAt` vienen `null` en las credenciales trasladadas desde el
modelo de una sola columna: ese dato no existía en ninguna parte y se prefirió el
hueco a inventarlo. `lastUsedAt` es lo que permite distinguir el dispositivo que
se tiene en la mano del que se perdió cuando ninguno tiene nombre.

---

## PATCH /api/profile/mfa/credentials/{publicId}

Le cambia el nombre a un autenticador. Body: `{ "label": "Teléfono viejo" }`.
Se admite `null` para quitárselo.

- `204 No Content`.
- `404 Not Found` con `Profile.Mfa.CredentialNotFound` si no existe **o no es
  suya**. Mismo código en ambos casos a propósito: distinguirlos confirmaría que
  ese identificador existe y es de alguien.

---

## DELETE /api/profile/mfa/credentials/{publicId}

Retira un autenticador conservando los demás. Si era el último, apaga el segundo
factor.

- `204 No Content`.
- `404 Not Found` con `Profile.Mfa.CredentialNotFound`.
- `403 Forbidden` con `Profile.Mfa.RequiredByTenantPolicy` o
  `Profile.Mfa.RequiredForMasterAdmin` **si era la última**: quedarse sin segundo
  factor por esta puerta no puede estar permitido si no lo está por la de
  `disable`.

**Auditoría**: `Profile.MfaCredentialRevoked`.

---

## Passkeys (WebAuthn)

Son **dos viajes** porque WebAuthn es reto/respuesta: el servidor emite un reto,
el navegador lo firma con la llave, y el servidor comprueba la firma contra el
reto que emitió. **El reto se queda en el servidor** — lo que vuelve es sólo un
identificador para recuperarlo—, y eso es lo que impide reproducir una respuesta
capturada.

### POST /api/profile/mfa/webauthn/begin

**Authorization**: `purpose=full` o `purpose=mfa-enroll`.

**Response** `200 OK`: `{ "opcionesJson": "…", "retoId": "…" }`

`opcionesJson` se le pasa tal cual a `navigator.credentials.create()`. Lleva
dentro `excludeCredentials` con las llaves que la persona ya tiene, para que el
navegador avise en el momento si intenta inscribir una repetida.

- `409 Conflict` con `Profile.Mfa.DemasiadasCredenciales` si llegó al máximo (5).

### POST /api/profile/mfa/webauthn/confirm

Body: `{ "retoId": "…", "respuestaJson": "…", "label": "YubiKey azul" }`

`respuestaJson` es lo que devolvió el navegador, serializado tal cual. Viaja como
texto y no como objeto tipado porque la librería que sabe interpretarlo vive en
Infrastructure y no puede asomar al contrato.

**Response** `200 OK`:
`{ "credencialPublicId": "…", "codigosDeRecuperacion": [ … ] }`

Los códigos **sólo vienen si era la primera credencial** de la persona, del tipo
que sea. Con la segunda la lista va vacía: emitirlos invalidaría los que ya
guardó.

Cuando el token entrante era `purpose=mfa-enroll` —inscripción forzada— la
respuesta trae además `accessToken`, `refreshToken`, sus vencimientos y la
cooperativa activa, exactamente igual que `POST /api/profile/mfa/confirm`. Sin
eso, quien inscribe una passkey porque su cooperativa lo exige quedaría con la
llave puesta y de vuelta en el login. Los campos van en null si la persona tiene
un número de cooperativas distinto de una: entonces hay que elegir, y esa
pantalla se alimenta de otro token.

- `422` con `Profile.Mfa.RetoVencido` si el reto caducó o **ya se usó**. Es de un
  solo uso.
- `422` con `Profile.Mfa.WebAuthnInvalido` si la firma no verifica.

### POST /api/auth/mfa/webauthn/challenge

**Authorization**: el `challengeToken` con `purpose=mfa-verify`, el mismo del
ingreso con código.

**Response** `200 OK`: `{ "opcionesJson": "…", "retoId": "…" }`

- `422` con `Identity.SinPasskeys` si la cuenta no tiene ninguna llave inscrita.
  No es un 401: la contraseña ya se acertó y el token de desafío es válido. Lo
  que falta es un método que esta persona no tiene.
- `422` con `Identity.Locked.Soft` si está bloqueada por intentos. **Emitir un
  reto no cuenta como intento**: si contara, pedirlos en bucle sería una forma
  gratuita de bloquear a alguien.

### POST /api/auth/mfa/webauthn/verify

Body: `{ "retoId": "…", "respuestaJson": "…" }`

**Response**: la misma forma que `POST /api/auth/mfa/verify` — sesión operativa,
selección de cooperativa, o la salida del maestro global. A partir de la
verificación es exactamente el mismo camino.

- `422` con `Identity.RetoVencido` si caducó o ya se usó. **No cuenta como
  intento fallido**: el servidor no llegó a juzgar ninguna llave, y contarlo
  castigaría a quien dejó la pantalla abierta cinco minutos.
- `401` con `Identity.WebAuthnInvalido` si la firma no verifica **o si la llave
  no es de esta persona**. Mismo código en ambos casos.

**Qué NO cuenta como intento fallido**: cancelar el diálogo, quedarse sin tiempo
o no tener la llave conectada. Nada de eso llega a la API. Si contara, cinco
tropiezos legítimos dejarían a alguien sin passkey, sin TOTP y sin códigos de
respaldo.

**Auditoría**: `CentralUser.Mfa.{Success|Failed}`, igual que la verificación con
código.

---

## POST /api/profile/password

Cambia la contraseña del usuario autenticado.

**Authorization**: JWT central válido.

**Request**:

```json
{
  "currentPassword": "...",
  "newPassword": "M1Cl4v3Nu3v4Seg|ra2026"
}
```

**Responses**:

- `204 No Content`. **Efecto colateral**: se regenera el security stamp del usuario, lo que invalida todos sus refresh tokens. La sesión actual del cliente debe re-loguearse en su próximo `/api/auth/refresh`.
- `401 Unauthorized` con `Identity.InvalidCredentials` si la contraseña actual no coincide.
- `400 Bad Request` con `Profile.Password.TooShort` (< 12 chars), `Profile.Password.SameAsOld`, o `Profile.Password.Pwned` (validación HIBP).
- `503 Service Unavailable` **NUNCA** — el servicio Pwned tiene fail-open (Warning loggeado, password aceptada). El cliente nunca ve este error.

**Side effect adicional**: se envía un correo "Tu contraseña fue cambiada" al `email` del usuario (notificación de seguridad).

**Auditoría**: `Profile.PasswordChanged`.

---

## POST /api/auth/password/forgot

Inicia el flujo "olvidé mi contraseña".

**Authorization**: pública.

**Request**:

```json
{ "email": "ana@coop.test" }
```

**Response**: `202 Accepted` con body `{ "message": "Si el correo existe, hemos enviado las instrucciones." }`. **Siempre 202** — el endpoint NEVER revela si el email existe o no (FR-041).

**Side effects**:
- Si el email existe → genera token (32 bytes random → Base64Url → hash SHA-256), persiste en `ADM_PasswordResetTokens` con TTL 1 hora; envía correo via `IEmailSender` con plantilla `PasswordResetEmail.html`.
- Si el email NO existe → no hace nada (pero registra evento auditable `Profile.PasswordResetRequested.NoSuchEmail` para detección de enumeración).
- Latencia equivalente entre los dos casos (la verificación de existencia es la única diferencia; el envío SMTP es asíncrono).

**Auditoría**: `Profile.PasswordResetRequested` (con flag `userExists` boolean para análisis interno; nunca se devuelve al cliente).

---

## POST /api/auth/password/reset

Consume un token de reset y establece nueva contraseña.

**Authorization**: pública (el token actúa como autenticación de un solo uso).

**Request**:

```json
{
  "token": "abc...",
  "newPassword": "M1Cl4v3Nu3v4Seg|ra2026"
}
```

**Responses**:

- `204 No Content`. **Sin tokens en la respuesta — el cliente debe redirigir al login**. Decisión consciente: post-reset NO se auto-loguea al usuario porque (a) el reset puede haber sido iniciado por un atacante con acceso temporal al inbox y queremos forzar al legítimo a usar el nuevo password, (b) el security stamp ya fue regenerado e invalida cualquier sesión previa, (c) re-loguear es trivial (≤5 s) y deja una marca clara en `CentralUserLoginAttempt`.
- `410 Gone` con `Identity.PasswordReset.TokenExpired` o `Identity.PasswordReset.AlreadyConsumed`.
- `400 Bad Request` con `Profile.Password.Pwned`, `Profile.Password.TooShort`.

**Concurrencia**: lock Redis `lock:pwdreset:{tokenHash}` TTL 30s + UPDATE condicional sobre `RowVersion` para garantizar single-use estricto (FR-030 análogo).

**Auditoría**: `Profile.PasswordReset.Consumed`.
