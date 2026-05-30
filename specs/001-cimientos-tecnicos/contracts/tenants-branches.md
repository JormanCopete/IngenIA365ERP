# Contract — Tenants (SaaS-global) & Branches (per-tenant)

---

## Tenants — `/api/saas/tenants`

> Solo accesible para operadores SaaS globales (`User.IsSaasOperator = true`). Permiso `Saas.Tenants.*`.

### GET `/api/saas/tenants`

Permission `Saas.Tenants.View`. Lista todos los tenants.

```json
{
  "items": [
    {
      "publicId": "Guid",
      "nit": "900123456-7",
      "legalName": "Cooperativa X",
      "name": "CoopX",
      "subdomain": "coopx",
      "planType": "Basic",
      "isActive": true,
      "activatedAt": "...",
      "userCount": 18,
      "branchCount": 3,
      "concurrencyToken": "..."
    }
  ]
}
```

### POST `/api/saas/tenants`

Permission `Saas.Tenants.Create`.

```json
{
  "nit": "string (NIT con DV)",
  "legalName": "string",
  "name": "string",
  "subdomain": "string?",
  "legalAddress": "string",
  "taxRegime": "Comun | Simplificado",
  "contactEmail": "string",
  "contactPhone": "string?",
  "planType": "Basic | Standard | Enterprise",
  "maxUsers": 10,
  "storageLimitMb": 5120
}
```

**Side effects**: aprovisiona schema (`tnt_xxxx`), siembra catálogo de roles built-in y permisos, registra audit log; el primer `CompanyAdmin` se da de alta vía endpoint subsecuente.

### PUT `/api/saas/tenants/{publicId}`

Permission `Saas.Tenants.Update`. Actualiza datos legales, contacto y límites. Concurrency token requerido.

### POST `/api/saas/tenants/{publicId}/suspend` / `/activate`

Permission `Saas.Tenants.Manage`. Actualiza `SuspendedAt` y `IsActive`. Los usuarios del tenant suspendido no pueden autenticarse (`Auth.TenantSuspended`).

---

## Branches — `/api/admin/branches`

> Permiso por empresa.

### GET `/api/admin/branches`

Permission `Branches.View`. Lista sucursales del tenant del JWT.

```json
{
  "items": [
    {
      "publicId": "Guid",
      "code": "MAT",
      "name": "Casa Matriz",
      "city": "Cali",
      "department": "Valle del Cauca",
      "isActive": true,
      "isHeadquarters": true,
      "concurrencyToken": "..."
    }
  ]
}
```

### POST `/api/admin/branches`

Permission `Branches.Create`.

```json
{
  "code": "string (1-10, único por tenant)",
  "name": "string",
  "address": "string",
  "city": "string",
  "department": "string",
  "phone": "string?",
  "isHeadquarters": false
}
```

**Errors**

- `Branches.CodeAlreadyTaken`
- `Branches.HeadquartersAlreadyExists`

### PUT `/api/admin/branches/{publicId}`

Permission `Branches.Update`. Concurrency token requerido.

### POST `/api/admin/branches/{publicId}/deactivate`

Permission `Branches.Delete`. Soft-delete; no se puede desactivar la sucursal `IsHeadquarters` mientras el tenant esté activo.
