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
  El `challengeToken` tiene claim `purpose=mfa-enroll` y lo aceptan las cuatro rutas de alta: `POST /api/profile/mfa/enroll` y `/confirm` (app de códigos) y `POST /api/profile/mfa/webauthn/begin` y `/confirm` (passkey). Las de passkey se añadieron porque quien no puede usar una app de autenticación quedaría obligado a usarla igual para cumplir la exigencia de su cooperativa, que es lo contrario de para lo que existen las passkeys. La respuesta trae además `metodosAceptados` cuando la cooperativa restringe: sin esa lista la pantalla ofrece los dos botones y el que no sirve vuelve a encerrar a la persona. Tras la confirmación exitosa, `mfa/confirm` devuelve en su respuesta un JWT operativo (`purpose=full`) con `active_tenant_id` ya resuelto, completando el login sin re-loguear.

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

Verifica el segundo factor con un código: TOTP o código de recuperación.

**Authorization**: el `challengeToken` con `purpose=mfa-verify`, en
`Authorization: Bearer`.

> El cuerpo de ejemplo de este documento traía además un campo `challengeToken`,
> y eso era falso: el handler lo lee de la cabecera y siempre lo hizo. El propio
> documento se contradecía cuatro secciones más arriba, donde ya decía
> «el cliente lo envía en `Authorization: Bearer`». Un contrato que se contradice
> a sí mismo es peor que uno incompleto: quien lo lea va a creer la mitad
> equivocada.

**Request**:

```json
{ "code": "123456", "useRecoveryCode": false }
```

**Responses**:

- `200 OK` con la misma forma que la respuesta `200 OK` exitosa de `/login`.
  Puede venir con `challenge = "MfaEnrollmentRequired"` aunque el código fuera
  correcto: significa que la identidad quedó probada pero el método usado no lo
  acepta ninguna de sus cooperativas. Ver
  [tenant-mfa-policy.md](tenant-mfa-policy.md).
- `401 Unauthorized` con `Identity.MfaInvalid` si el código es incorrecto.
- `422` con `Identity.Locked.Soft` si superó los intentos del segundo factor,
  que tienen su propio ámbito: un login correcto no los resetea.

**Auditoría**: `CentralUser.Mfa.{Success|Failed}`, y
`CentralUser.Mfa.RecoveryCode{Used|Failed}` cuando se canjea un código.

---

## POST /api/auth/mfa/webauthn/challenge · /verify

Ingreso con passkey. Rutas propias y no un campo más en `/mfa/verify`, cuyo
validador limita `code` a 6-8 caracteres. Ver
[profile-and-recovery.md](profile-and-recovery.md).

---

## Recuperación del segundo factor por correo

Tres rutas. **Existe sólo si la cooperativa la enciende**, y nace apagada.

### POST /api/auth/mfa/recovery/request

**Authorization**: el `challengeToken` con `purpose=mfa-verify` — es decir, la
contraseña ya está acertada. Nunca anónimo: si lo fuera, este endpoint sería un
oráculo para averiguar qué correos tienen cuenta, y permitiría inundar el buzón
de una víctima sin saber siquiera su contraseña.

**Response** `200 OK`:

```json
{ "ejecutableDesde": "2026-08-29T10:00:00Z", "correoEnviado": true }
```

`correoEnviado: false` significa que la solicitud quedó creada pero el SMTP
falló. Se distingue a propósito: responder «te enviamos un correo» cuando no
salió deja a la persona esperando un mensaje que no existe, con el reloj
corriendo. El SMTP es autoalojado; esto pasa.

- `422` con `Identity.RecuperacionNoDisponible` si alguna cooperativa que le
  exige segundo factor no permite esta vía, si ninguna lo exige, o si es el
  administrador maestro (apagado por defecto para él, `Mfa:RecuperacionPorCorreoParaMaestro`).
- `422` con `Identity.DemasiadasSolicitudes` al pasar el tope diario. Es un tope
  propio en Redis y no el contador de intentos: aquel escala castigando fallos, y
  aquí no hay fallo ninguno — lo que se acota es el volumen de avisos, porque
  inundar el buzón es como se derrota una defensa basada en avisar.

**Con varias cooperativas manda la más estricta**, y la demora es la más larga.
Si bastara una permisiva, cualquiera entraría en la cooperativa exigente pidiendo
la recuperación «por» la otra.

### POST /api/auth/mfa/recovery/confirm

**Anónimo**: se llega desde el enlace del correo, posiblemente en otro navegador.

```json
{ "token": "...", "password": "..." }
```

La contraseña **otra vez**, y aquí y no al solicitar: entre una cosa y otra pasan
horas o días, y en ese tiempo el enlace puede acabar en otras manos.

- `204` al completarse. **No devuelve sesión.** Recuperar no es entrar: es poder
  volver a inscribir un segundo factor. Devolver una sesión convertiría «tengo el
  buzón y la contraseña» en «estoy dentro», que es justo lo que el segundo factor
  existe para impedir.
- `422` con `Identity.RecuperacionTodaviaNoDisponible` antes de cumplirse la
  espera. Mensaje distinto de «no sirve» a propósito: confundirlos haría que la
  persona volviera a pedirla, reiniciando el reloj cada vez.
- `422` con `Identity.RecuperacionNoValida` si no existe, ya se usó, caducó o se
  canceló. Un mensaje único para los cuatro: un enlace ajeno no puede servir para
  averiguar si existía.
- `401` con `Identity.InvalidCredentials` si la contraseña falla. Cuenta en un
  ámbito de intentos propio.

### POST /api/auth/mfa/recovery/cancel

**Anónimo y sin contraseña**, deliberadamente.

```json
{ "token": "..." }
```

Cancelar tiene que ser más fácil que ejecutar: quien recibe el aviso sin haberlo
pedido está viendo un ataque, y sólo lo va a parar si pararlo es trivial. Los dos
lados del error no pesan lo mismo — cancelar de más obliga a volver a pedirla,
ejecutar de más le retira el segundo factor a alguien.

Es idempotente. Y **cualquier ingreso correcto cancela las solicitudes vivas**:
si pudo entrar, no las necesitaba.

**Auditoría**: `MfaRecovery.{Requested|Cancelled|Executed|ConfirmFailed}`.

---

## POST /api/auth/refresh

Renueva el access token usando el refresh token (rotation).

**Duración de la sesión.** Dos límites, configurables en la sección `Sesion` de la
API (`Sesion__InactividadMinutos`, por defecto 30; `Sesion__DuracionMaximaHoras`, por
defecto 12) y publicados en `GET /api/auth/session-policy` para que el cliente cuente
con los mismos números:

- **Duración máxima**, desde el ingreso. Tope absoluto: cada canje devuelve un refresh
  nuevo cuyo `refreshTokenExpiresAt` es **el mismo** del ingreso, no doce horas más.
  Hasta el 2026-09-10 cada rotación abría otras doce horas, así que una sesión que se
  renovara sola no vencía nunca.
- **Inactividad**. El servidor la mide entre rotaciones (no ve cada petición): un
  refresh que llegue más de `InactividadMinutos` después de la rotación anterior se
  rechaza. Para que eso no expulse a alguien activo que lleva veinte minutos leyendo,
  el cliente rota sola la sesión de una pestaña activa dos minutos antes de ese límite,
  y **corta exactamente a los 30 minutos** la pestaña sin peticiones propias: cinco
  minutos antes avisa con «Seguir trabajando» (que rota y reinicia el reloj) y al
  llegar a cero cierra la sesión local, revoca el refresh con `/logout` y vuelve al
  login con `?returnUrl=`. Recargar la página cuenta como actividad; mover el ratón o
  escribir sin guardar, no.

El cliente (`RenovadorDeSesion`) renueva en silencio cuando al access le queda menos
de un minuto y, con un 401 inesperado, renueva y reintenta una vez. En los últimos
cinco minutos de la duración máxima muestra una cuenta atrás no modal
(`AvisoDeVencimientoDeSesion`) para que la persona termine lo que hace y vuelva a
entrar; si coinciden los dos avisos manda ése, que no se puede extender.

**Request**:

```json
{ "refreshToken": "eyJ..." }
```

**Responses**:

- `200 OK`:
  ```json
  {
    "accessToken": "eyJ...",
    "accessTokenExpiresAt": "2026-09-10T12:15:00Z",
    "refreshToken": "eyJ... (nuevo, el anterior queda invalidado)",
    "refreshTokenExpiresAt": "2026-09-10T23:40:00Z  (el tope de la sesión, fijo desde el ingreso)",
    "expiresInSeconds": 900
  }
  ```
- `401 Unauthorized` con `Identity.RefreshToken.Invalid` (token revocado, expirado, o family-violation).
- `401 Unauthorized` con `Identity.RefreshToken.Reused` (el token ya había rotado; la familia entera queda invalidada).
- `401 Unauthorized` con `Identity.RefreshToken.SessionExpired` (la sesión alcanzó su duración máxima; no se invalida nada, sólo hay que volver a entrar).
- `401 Unauthorized` con `Identity.RefreshToken.InactivityExpired` (más de `InactividadMinutos` desde la rotación anterior; tampoco se invalida nada).
- `422` con `Tenant.MfaMethodNotAccepted` (la cooperativa dejó de aceptar el método con el que se entró; ver `tenant-mfa-policy.md`).

---

## GET /api/auth/session-policy

Anónima. Los límites de sesión vigentes, tal como el servidor los hace cumplir en
`/refresh`; el cliente los lee al montar el layout y, si no puede, usa estos mismos
valores por defecto.

```json
{ "inactivityMinutes": 30, "maxDurationHours": 12 }
```

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
