# Contract — Roles & Permissions

**Base path**: `/api/admin/roles`, `/api/admin/permissions`

---

## GET `/api/admin/permissions`

**Permission**: `Roles.View`

Catálogo global inmutable de permisos. Útil para construir el editor de roles.

**Response 200**

```json
{
  "items": [
    {
      "publicId": "Guid",
      "code": "Users.Create",
      "module": "Security",
      "entityType": "User",
      "action": "Create",
      "description": "Crear nuevos usuarios"
    }
  ]
}
```

Filtrable por `module` y `entityType`.

---

## GET `/api/admin/roles`

**Permission**: `Roles.View`

```json
{
  "items": [
    {
      "publicId": "Guid",
      "code": "Auditor",
      "name": "Auditor",
      "description": "Solo lectura + aprobaciones",
      "isBuiltIn": true,
      "isAssignable": true,
      "permissionCount": 18,
      "userCount": 3,
      "concurrencyToken": "..."
    }
  ]
}
```

---

## GET `/api/admin/roles/{publicId}`

**Permission**: `Roles.View`

Detalle con lista completa de permisos asignados.

```json
{
  "publicId": "Guid",
  "code": "Auditor",
  "name": "Auditor",
  "description": "...",
  "isBuiltIn": true,
  "isAssignable": true,
  "permissions": [ { "publicId": "Guid", "code": "AuditLog.View" }, ... ],
  "concurrencyToken": "..."
}
```

---

## POST `/api/admin/roles`

**Permission**: `Roles.Create`

```json
{
  "code": "string (3-40, único por tenant)",
  "name": "string (3-80)",
  "description": "string (<=400)?",
  "permissionCodes": [ "Users.View", "Roles.View" ]
}
```

**Errors**

- `Roles.CodeAlreadyTaken`
- `Roles.UnknownPermission`
- `Roles.BuiltInConflict` — intentar usar code reservado.

---

## PUT `/api/admin/roles/{publicId}`

**Permission**: `Roles.Update`

```json
{
  "name": "string",
  "description": "string?",
  "addPermissionCodes": [ "..." ],
  "removePermissionCodes": [ "..." ],
  "concurrencyToken": "string"
}
```

**Side effects**: invalida `perms:{userPublicId}:*` para todos los usuarios del rol (FR-019 → max 30 min, típicamente inmediato vía pub/sub Redis).

---

## DELETE `/api/admin/roles/{publicId}`

**Permission**: `Roles.Delete`

**Errors**

- `Roles.BuiltInCannotDelete` — `CompanyAdmin` y `Auditor` no son eliminables.
- `Roles.HasAssignedUsers` — el rol está asignado a N usuarios; reasignar antes.

Soft-delete. Side effects: audit log.

---

## Built-in roles (seed)

| Code | Description | Permission summary |
|---|---|---|
| `CompanyAdmin` | Administrador de empresa | Todos los permisos de Admin, Security, AuditLog (excepto cross-tenant SaaS). |
| `Auditor` | Auditor regulatorio | View + Export en todo módulo + Approve. Sin Create/Update/Delete. |
| `Operator` | Operador estándar | View/Create/Update sobre entidades operativas; sin Delete ni Approve. |
| `ReadOnly` | Solo lectura | Solo View. |

Built-ins se siembran por tenant en `ProvisionTenantSchemaCommand`.
