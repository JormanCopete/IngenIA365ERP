# Contract — Users administration

**Base path**: `/api/admin/users`
**Auth**: authenticated + permiso granular por endpoint.
**Concurrency**: writes carry `concurrencyToken` (base64 RowVersion); 409 on mismatch.

---

## GET `/api/admin/users`

**Permission**: `Users.View`

**Query parameters**

| Name | Type | Notes |
|---|---|---|
| q | string? | Filtro libre sobre username/email. |
| isActive | bool? | |
| roleCode | string? | |
| branchPublicId | Guid? | |
| includeDisabled | bool | Default false. |
| page | int | Default 1. |
| pageSize | int | Default 50, max 200. |

**Response 200**

```json
{
  "page": 1,
  "pageSize": 50,
  "totalCount": 234,
  "items": [
    {
      "publicId": "Guid",
      "username": "jcopete",
      "email": "j@coopx.co",
      "isActive": true,
      "isMfaEnabled": true,
      "lastLoginAt": "2026-05-28T09:12:00Z",
      "roles": [ "Auditor" ],
      "branches": [ "MAT", "B-CALI" ]
    }
  ]
}
```

---

## POST `/api/admin/users`

**Permission**: `Users.Create`

**Request body**

```json
{
  "username": "string (3-60)",
  "email": "string (RFC 5322)",
  "personPublicId": "Guid? (link a COR_People si existe)",
  "initialPassword": "string (cumple política tenant)",
  "mustChangePassword": true,
  "roles": [ "RoleCode1", "RoleCode2" ],
  "branchPublicIds": [ "Guid", "Guid" ],
  "defaultBranchPublicId": "Guid?",
  "sendInvitationEmail": true
}
```

**Response 201**

```json
{ "publicId": "Guid", "username": "string", "concurrencyToken": "..." }
```

**Errors**

- `Users.UsernameAlreadyTaken`
- `Users.EmailAlreadyTaken`
- `Auth.PasswordPolicyViolation`
- `Users.BranchNotInTenant`
- `Users.RoleNotInTenant`

**Side effects**: audit log `Create`; correo `UserInvitationCreated` si `sendInvitationEmail = true`.

---

## PUT `/api/admin/users/{publicId}`

**Permission**: `Users.Update`

**Request body**

```json
{
  "email": "string",
  "personPublicId": "Guid?",
  "concurrencyToken": "string"
}
```

**Errors**

- `Concurrency.StaleRowVersion`
- `Users.NotFound`
- `Users.EmailAlreadyTaken`

---

## POST `/api/admin/users/{publicId}/disable`

**Permission**: `Users.Delete` (soft-delete)

**Request body**

```json
{ "reason": "string (<=400)", "concurrencyToken": "string" }
```

**Response 200**: vacío. Side effects: `IsActive = false`, `IsDeleted = true`, revoca todos los refresh tokens, audit log.

---

## POST `/api/admin/users/{publicId}/restore`

**Permission**: `Users.Restore`

Reactiva un usuario soft-deleted. `IsActive = true`, `IsDeleted = false`. No reactiva sesiones — el usuario debe loguearse nuevamente.

---

## POST `/api/admin/users/{publicId}/roles`

**Permission**: `Users.AssignRole`

**Request body**

```json
{
  "addRoleCodes": [ "Auditor" ],
  "removeRoleCodes": [ "Operator" ],
  "concurrencyToken": "string"
}
```

**Side effects**: invalida `PermissionClaimsCache` del usuario; emite notificación `RoleAssigned`/`RoleRemoved`.

---

## POST `/api/admin/users/{publicId}/branches`

**Permission**: `Users.AssignBranch`

**Request body**

```json
{
  "addBranchPublicIds": [ "Guid" ],
  "removeBranchPublicIds": [ "Guid" ],
  "defaultBranchPublicId": "Guid?",
  "concurrencyToken": "string"
}
```

---

## POST `/api/admin/users/{publicId}/reset-password`

**Permission**: `Users.ResetPassword`

Reset administrativo: fija nueva contraseña aleatoria que satisface la política, marca `MustChangePassword = true`, envía correo con instrucciones (sin la contraseña en claro — entrega por canal seguro fuera de banda).

**Response 200**: vacío.

---

## POST `/api/admin/users/{publicId}/unlock`

**Permission**: `Users.Unlock`

Limpia `LockoutEndAt` y `FailedLoginAttempts`. Audit log.
