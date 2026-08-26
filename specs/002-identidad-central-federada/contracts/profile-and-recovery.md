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
  "expiresInSeconds": 600
}
```

El cliente muestra el secret para que la persona lo escriba en su app de
autenticación. **Hoy no se pinta código QR**: no hay ninguna capa que lo genere
—existía un `TotpService` con QRCoder que ninguna pantalla llamó, y se retiró—,
así que la pantalla dice explícitamente que hay que teclear la clave a mano en
vez de prometer un QR que no aparece. El `otpAuthUri` viaja igual, listo para
cuando se implemente.

El secret pendiente se almacena en Redis con TTL 10 min; si el usuario no
confirma a tiempo, debe reiniciar.

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
- `403 Forbidden` con `Profile.Mfa.RequiredByTenantPolicy` y body `{ "tenantName": "Coop. Solidaria" }` si al menos un tenant lo exige.

**Auditoría**: `Profile.MfaDisabled`.

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
