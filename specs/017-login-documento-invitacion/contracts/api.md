# Contratos de la API — 017

Todos los errores van en el sobre de siempre (`code`, `message`, `data`). Los fallos de autenticación
responden **401**, no 422. Las rutas que no se mencionan no cambian.

## 1. Ingreso

### `POST /api/auth/login` (cambia, compatible)

```json
{ "identifier": "1.234.567", "password": "…" }
```

- `identifier` es nuevo. `email` se sigue aceptando: si llega sin `identifier`, se usa como
  identificador, para no romper a los clientes MAUI viejos.
- Validación: no vacío, 256 caracteres como máximo; formato de correo sólo si lleva `@`.
- Las respuestas son las de hoy (`MfaRequired`, `MfaEnrollmentRequired`, `TenantSelection`, sesión).
- Error: 401 `Identity.InvalidCredentials`, con `data.hint`, la pista fija armada con los prefijos del
  catálogo: «Si tu documento no es cédula, antepón el tipo: CE, PPT, PEP, TI, PA».
- Bloqueo: 401 `Identity.Locked.Soft`, como hoy.

### `GET /api/auth/me` (agrega campos)

```json
{ "...": "…", "documentPending": true, "documentUsername": null,
  "documentRequiredFrom": [{ "tenantId": "…", "from": "2027-01-15" }] }
```

### Desafío `document-required` (nuevo propósito)

Lo emiten las siete puertas cuando una cooperativa ya exige el documento. Sólo sirve para
`POST /api/auth/document`. Respuesta del login o de elegir cooperativa:
`{ "status": "DocumentRequired", "challengeToken": "…" }`.

### `POST /api/auth/document` (nuevo)

Lo usa la persona con sesión o con el desafío `document-required`.

```json
{ "documentType": "CC", "documentNumber": "1234567", "documentNumberConfirmation": "1.234.567" }
```

- 200: `{ "documentUsername": "1234567" }`.
- 422:
  - `Identity.Document.Mismatch`: las dos digitaciones no coinciden.
  - `Identity.Document.Invalid`: formato o tipo no válidos.
  - `Identity.Document.TypeNotAccepted`: algún tipo no aceptado por sus cooperativas.
  - `Identity.Document.Unavailable`: ya tomado; abre un caso sin decir de quién.

## 2. Invitación

### `POST /api/tenants/{tenantId}/invitations` (cambia)

```json
{
  "documentType": "CC", "documentNumber": "1234567", "documentNumberConfirmation": "1234567",
  "firstName": "Ana", "otherNames": "María", "lastName": "Pérez", "secondLastName": "Gómez",
  "email": "ana.perez@gmail.com", "mobilePhone": "3001234567",
  "rolePublicId": "…", "positionPublicId": "…"
}
```

- 201: `{ "invitationPublicId": "…", "message": "Invitación enviada. Si la persona ya tiene cuenta, le llegará a su correo registrado." }`.
  Es la misma respuesta haya o no identidad.
- 422:
  - `Invitation.EmailTaken`: «Correo ya registrado».
  - `Invitation.AlreadyMember`.
  - `Identity.Document.Mismatch`, `.Invalid`, `.TypeNotAccepted`.
  - `Invitation.RoleNotFound`, `Invitation.PositionNotFound`.
- `POST /api/saas/invitations` (maestro) acepta los mismos campos más `inviteAsTenantAdmin`.
- `POST /api/saas/tenants/with-admin` conserva `firstAdminEmail` y agrega, planos y opcionales,
  `firstAdminDocumentType`, `firstAdminDocumentNumber`, `firstAdminDocumentNumberConfirmation`,
  `firstAdminFirstName`, `firstAdminOtherNames`, `firstAdminLastName`, `firstAdminSecondLastName` y
  `firstAdminMobilePhone` (sin rol ni cargo). *Construido así en el MVP: más simple que un objeto
  anidado y compatible con los clientes de hoy.*
- **Compatibilidad**: sin `documentType`/`documentNumber` la API sigue emitiendo la invitación de
  antes, sólo por correo. La pantalla exige el documento.

### `PUT /api/tenants/{tenantId}/invitations/{invitationPublicId}` (nuevo)

Corrige una invitación `Pending` o `UnderReview`: mismo cuerpo que la de crear. Deja la anterior
`Superseded` y emite otra.

### `GET /api/tenants/{tenantId}/invitations` (agrega campos)

Cada fila trae `fullName`, `documentType`, `documentNumberMasked` (los cuatro últimos dígitos),
`status` (incluido `UnderReview`), `role` y `position`. Nunca trae nada de otras cooperativas.

### `GET /api/invitations/{token}/preview` (cambia)

- Nunca devuelve el documento.
- Marca el correo verificado si el token es válido.
- Agrega `firstName`, `requiresDocument` y `attemptsLeft`. *El correo se sigue devolviendo completo:
  quien abre el enlace es el dueño de ese buzón.*

### `POST /api/invitations/accept` (cambia)

```json
{ "token": "…", "documentNumber": "1234567",
  "newRegistration": { "password": "…" } }
```

- O `existingCredentials: { password }`, o la sesión abierta, como hoy.
- 200: los mismos resultados de hoy más `documentUsername` («Puedes ingresar con tu correo o con tu
  usuario: …»).
- 422 `Invitation.DocumentMismatch`, con `data.attemptsLeft`.
- 409 `Invitation.UnderReview`: al agotarse los intentos, o si se usa un enlace en revisión.

## 3. Administración de la cooperativa

| Ruta | Permiso | Qué hace |
|---|---|---|
| `POST /api/admin/users/{publicId}/send-recovery-link` | `Security.Users.Edit` | envía el enlace de recuperación al correo verificado; reemplaza a `reset-password`, **retirada sin alias** |
| `POST /api/admin/users/{publicId}/correct-document` | `Security.Users.CorrectDocument` (nuevo, `CompanyAdmin`) | cuerpo `{documentType, documentNumber, documentNumberConfirmation, reason: "Correction"\|"Update", notes, supportAttachmentPublicId?}`. Si la persona está en otra cooperativa, 403 `Identity.Document.PlatformOnly` |
| `GET /api/admin/users` | igual que hoy | agrega `documentUsername`, `documentMismatch`, `canCorrectDocument`, `position` |
| `GET/PUT /api/tenants/{tenantId}/identity-policy` | administrador de la cooperativa o maestro (el mismo guardián de la política de segundo factor; *construido así en el MVP en vez de un permiso nuevo*) | `tiposAceptados`, `documentoExigidoDesde`, `intentosAlActivar` |

## 4. Maestro de plataforma (`/api/saas/identities`)

| Ruta | Qué hace |
|---|---|
| `GET /api/saas/identities?q=` | busca por correo, usuario o documento; devuelve identidades con sus membresías |
| `GET /api/saas/identities/{id}` | detalle, historial de documentos y casos |
| `POST /api/saas/identities/{id}/correct-document` | corrige con motivo, como el administrador pero sin límite de membresías |
| `POST /api/saas/identities/{id}/change-email` | rescate: `{newEmail, reason}`; avisa a los dos correos |
| `POST /api/saas/identities/merge` | `{sourceId, targetId, reason}` → `{casePublicId}`. 409 `Identity.Merge.SharedTenant` si comparten cooperativa |
| `GET /api/saas/identity-cases?status=` | casos abiertos |
| `POST /api/saas/identity-cases/{id}/dismiss` | cierra un caso con motivo |

## 5. La persona

| Ruta | Qué hace |
|---|---|
| `POST /api/auth/step-up` | clave más segundo factor → token de propósito `step-up` de 5 minutos |
| `POST /api/auth/email-change` | con `step-up`: `{newEmail}` → envía el código. 422 `Identity.Email.Taken` |
| `POST /api/auth/email-change/confirm` | `{code}` → cambia el correo, avisa al anterior y cierra las demás sesiones |
| `POST /api/auth/password/forgot` | ahora `{identifier}` (`email` se sigue aceptando); siempre 200 |

## 6. Eventos de auditoría nuevos

`Identity.Document.Registered`, `.Corrected`, `.Updated`; `Identity.Merged`;
`Identity.Email.ChangeRequested`, `.Changed`, `.ChangedByMaster`; `Identity.RecoveryLinkSent`;
`Invitation.Corrected`, `.UnderReview`; `Membership.UnderReview`; `IdentityPolicy.Changed`. Además se
emiten explícitamente `Invitation.Issued.*`, `Revoked` y `Superseded`.

## 7. Personas por documento (cambio de comportamiento)

`GET /api/core/people/by-document?taxId=&type=` admite el tipo opcional. Sin tipo y con dos personas
del mismo número (CC y CE) responde **409** `Person.DocumentAmbiguous` y pide el tipo; antes no podía
pasar porque el número era único.
