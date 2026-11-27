# Contratos — 027

Todas las respuestas usan el sobre de errores de siempre (`ErrorEnvelopeFilter`).

## Administrador de la cooperativa (`/api/admin/users/{userPublicId}/login-passes`, permiso `Security.Users.GrantMfaBypass`)

| Método y ruta | Cuerpo | Respuesta |
|---|---|---|
| `GET /` | — | `LoginPassDto[]` del usuario en la cooperativa activa, más recientes primero |
| `POST /` | `{ hours: int (1..max), reason: string(10..500) }` | `{ pass: LoginPassDto, emailSent: bool }` — nunca el enlace |
| `POST /{passPublicId}/resend` | — | `{ pass: LoginPassDto, emailSent: bool }` (pase nuevo; el anterior queda `reemplazado`) |
| `POST /{passPublicId}/void` | `{ reason: string(10..500) }` | `LoginPassDto` |

`GET /api/admin/users/{id}` gana `canGrantLoginPass` (opción encendida + permiso + destinatario elegible) y `loginPassMaxHours`.

## Maestro (`/api/saas/tenants/{tenantPublicId}/login-passes`, sólo maestro)

| Método y ruta | Cuerpo | Respuesta |
|---|---|---|
| `GET /?centralUserPublicId=` | — | `LoginPassDto[]` de la cooperativa (o de un usuario) |
| `POST /` | `{ centralUserId: uuid, hours, reason }` | `{ pass, emailSent }` |
| `POST /{passPublicId}/void` | `{ reason }` | `LoginPassDto` |

## Ingreso (`POST /api/auth/mfa/pass`, token `purpose=mfa-verify`)

Cuerpo `{ pass: string }` (el valor de `t` del enlace, o el enlace completo: el servidor extrae `t`). Respuesta: el mismo `LoginResult` de
`/api/auth/mfa/verify`. Errores: `Identity.LoginPass.Invalid` (401, genérico para inexistente, vencido, anulado o de otra cuenta),
`Identity.Locked.Soft`.

## Política (`PUT /api/tenants/{id}/mfa-policy`)

Gana `permitirPasesDeIngreso: bool?` y `horasMaximasDelPase: int?` (null = no tocar). `GET` devuelve también
`permitirRecuperacionPorCorreo`, `horasDeDemora`, `permitirPasesDeIngreso`, `horasMaximasDelPase`.

## Sesión

- `GET /api/auth/me` gana `loginPassTenantPublicId` (null si no entró con pase).
- `POST /api/auth/switch-tenant` y `select-tenant` a otra cooperativa con pase → `Identity.LoginPass.TenantLocked` (403).

## `LoginPassDto`

```json
{ "publicId": "…", "centralUserId": "…", "userEmail": "…", "tenantPublicId": "…", "grantedByEmail": "…", "grantedByMaster": false,
  "reason": "…", "grantedAt": "…", "expiresAt": "…", "useCount": 0, "lastUsedAt": null, "lastUsedIp": null,
  "status": "Active|Expired|Voided", "voidedAt": null, "voidReason": null }
```

## Errores de concesión

`LoginPass.Disabled`, `LoginPass.Self`, `LoginPass.TargetIsAdmin`, `LoginPass.TargetIsMaster`, `LoginPass.EmailNotVerified`,
`LoginPass.NoSecondFactor`, `LoginPass.NotMember`, `LoginPass.HoursOutOfRange`, `LoginPass.TooMany`, `LoginPass.NotActive`.
