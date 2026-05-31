# Contract: Tenant MFA Policy

**Módulo Carter**: `TenantMfaPolicyModule.cs`
**Rutas**: `/api/tenants/{tenantPublicId}/mfa-policy`

---

## GET /api/tenants/{tenantPublicId}/mfa-policy

Devuelve la política MFA del tenant.

**Authorization**: admin del tenant o master.

**Response** `200 OK`:

```json
{
  "tenantPublicId": "...",
  "isRequired": false,
  "activatedAt": null,
  "activatedByEmail": null
}
```

---

## PUT /api/tenants/{tenantPublicId}/mfa-policy

Activa o desactiva la política "MFA obligatorio para todos los miembros de la empresa" (FR-003a).

**Authorization**: admin del tenant o master.

**Request**:

```json
{ "isRequired": true }
```

**Response**: `204 No Content`.

**Side effects**:
- Si `isRequired` pasa de `false` a `true`: en el próximo login de cada miembro activo sin MFA, el sistema le forzará a configurarlo antes de operar (FR-003b).
- Invalida caché de membresías del tenant en Redis (los flags `isMfaRequired` se recomputan).
- Las sesiones activas NO se cierran inmediatamente; en su próximo `/api/auth/refresh` el sistema verificará y, si aplica, exigirá MFA antes de re-emitir.

**Auditoría**: `TenantMfaPolicy.Activated` o `TenantMfaPolicy.Deactivated`.
