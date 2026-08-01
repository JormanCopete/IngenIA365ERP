# Contract: Invitations

**Módulo Carter**: `InvitationsModule.cs`
**Rutas**: `/api/tenants/{tenantPublicId}/invitations`, `/api/saas/invitations`, `/api/invitations/*`
**Autorización**: variable según endpoint.

---

## POST /api/tenants/{tenantPublicId}/invitations

Un admin de empresa emite una invitación a su propia empresa (FR-024).

**Authorization**: JWT central + `[RequireTenantAdmin]` filter (verifica `active_tenant_id == tenantPublicId` Y `tenant_admin == true` en el claim).

**Request**:

```json
{
  "email": "nuevo.usuario@empresa.coop",
  "inviteAsTenantAdmin": false
}
```

`inviteAsTenantAdmin` ignorado/rechazado en este endpoint — un tenant admin solo puede invitar usuarios regulares. Para invitar otro admin de su misma empresa, usar `POST .../members/{publicId}/promote-admin` después de que la persona acepte (o invitar el master admin).

**Responses**:

- `201 Created`:
  ```json
  {
    "invitationPublicId": "...",
    "email": "nuevo.usuario@empresa.coop",
    "expiresAt": "2026-06-06T12:34:56Z",
    "status": "Pending"
  }
  ```
- `409 Conflict` con `Invitation.AlreadyMember` si el email ya tiene membresía activa en este tenant.
- `403 Forbidden` si no es admin del tenant.

**Side effects**:
- Si existía una invitación `Pending` para el mismo `(email, tenantId)`, se marca `Revoked` con razón `Invitation.Superseded` y se emite la nueva.
- Se envía email via `IEmailSender` con plantilla `InvitationEmail.html` (asíncrono, fail-soft con reintentos).

**Auditoría**: `Invitation.Issued.ByTenantAdmin`.

---

## POST /api/saas/invitations

El master admin emite invitaciones a cualquier empresa, opcionalmente marcándolas como admin (FR-025).

**Authorization**: JWT central + `[RequireMasterAdmin]` (claim `is_global_master_admin == true`).

**Request**:

```json
{
  "email": "admin@coop.coop",
  "tenantPublicId": "...",
  "inviteAsTenantAdmin": true
}
```

**Responses**: mismo formato que el endpoint anterior.

**Auditoría**: `Invitation.Issued.ByMaster`.

---

## GET /api/invitations/{token}/preview

Permite al cliente Blazor mostrar la pantalla de aceptación SIN consumir el token. Reporta si el email destinatario corresponde a un `CentralUser` existente (para decidir si pedir registro o login).

**Authorization**: pública.

**Response** `200 OK`:

```json
{
  "tenantPublicId": "...",
  "tenantName": "Coop. Solidaria",
  "email": "nuevo@coop.coop",
  "isExistingCentralUser": false,
  "inviteAsTenantAdmin": false,
  "expiresAt": "...",
  "isValid": true
}
```

Si el token no existe, está expirado o revocado: `200 OK` con `isValid: false` + `errorCode` (`Invitation.NotFound`, `Invitation.Expired`, `Invitation.Revoked`, `Invitation.AlreadyAccepted`).

Razón de retornar 200 incluso en errores: la UI necesita un payload uniforme para renderizar el mensaje sin discriminar por status code. Es información no sensible (el token es ya parte de la URL del receptor).

---

## POST /api/invitations/accept

Acepta una invitación. Maneja tres casos: nuevo `CentralUser`, usuario existente que se autentica, o usuario ya autenticado con sesión activa (FR-029(b)).

**Authorization**: opcional. Si se envía `Authorization: Bearer <JWT central>` y el `email` del JWT coincide con el de la invitación, el backend usa la rama "sesión activa" y NO requiere password en el body.

**Request (caso usuario nuevo)**:

```json
{
  "token": "abc...",
  "registration": {
    "password": "M1Cl4v3Seg|ra2026",
    "displayName": "Ana Pérez"
  }
}
```

**Request (caso usuario existente, sin sesión activa)**:

```json
{
  "token": "abc...",
  "existingCredentials": {
    "password": "su contraseña actual"
  }
}
```

**Request (caso sesión activa — reutiliza la credencial central ya autenticada)**:

```json
{ "token": "abc..." }
```

Body sin `registration` ni `existingCredentials`; **requiere** header `Authorization: Bearer <JWT>` cuyo claim `email` coincida con el destinatario de la invitación. Si el email no coincide → `403 Forbidden Invitation.SessionEmailMismatch`. Si no hay JWT → `401 Unauthorized Invitation.AuthRequired` con indicación de que debe enviar `existingCredentials` o re-autenticarse.

El servidor decide entre las tres ramas comparando el `Email` de la invitación contra `ADM_CentralUsers.NormalizedEmail` y la presencia/validez del JWT.

**Responses**:

- `200 OK`:
  ```json
  {
    "centralUserId": "...",
    "tenant": { "tenantPublicId": "...", "tenantName": "..." },
    "accessToken": "eyJ... (con active_tenant_id ya seteado al tenant invitante)",
    "refreshToken": "eyJ...",
    "isTenantAdmin": true
  }
  ```
- `400 Bad Request` con `Invitation.Password.Weak` si la contraseña no cumple política (longitud < 12 o aparece en Pwned Passwords).
- `400 Bad Request` con `Invitation.Mismatch.ExistingUser` si se envía `registration` cuando el email ya existe (o viceversa).
- `401 Unauthorized` con `Identity.InvalidCredentials` si en el caso existente la contraseña no coincide.
- `410 Gone` con `Invitation.Expired` o `Invitation.Revoked` o `Invitation.AlreadyAccepted`.
- `423 Locked` con `Identity.Locked.Soft` si la cuenta existente está en lockout.

**Side effects**:
- Crea o actualiza fila en `SEC_Users` del tenant (con `CentralUserId` ligado).
- Marca invitación `Accepted` con `AcceptedByCentralUserId`.
- Si la membresía existía revocada (caso "reactivación") → la membresía pasa a `Active` (no se crea una nueva — FR-009a).
- Emite el primer JWT operativo con `active_tenant_id` igual al tenant de la invitación.

**Auditoría**: `Invitation.Accepted` + `Membership.Activated`.

---

## DELETE /api/invitations/{publicId}

Revoca una invitación pendiente (FR-031).

**Authorization**:
- Admin de empresa solo puede revocar invitaciones de SU empresa.
- Master admin puede revocar cualquiera.

**Response**: `204 No Content`.

**Errors**:
- `404 Not Found` si no existe.
- `409 Conflict` con `Invitation.NotPending` si ya fue aceptada/expirada/revocada.

**Auditoría**: `Invitation.Revoked`.
