# Contract: Sessions (selección y cambio de empresa activa)

**Módulo Carter**: `SessionsModule.cs`
**Ruta base**: `/api/sessions`, `/api/profile/default-tenant`
**Autorización**: JWT central válido. NO requiere `active_tenant_id` en el token para `select-tenant` (el JWT inicial del login puede traerlo en blanco si hay > 1 tenant).

---

## GET /api/sessions/active-tenants

Lista las empresas donde el usuario tiene `TenantMembership.Status = Active`.

**Authorization**: `Bearer` JWT central.

**Response** `200 OK`:

```json
[
  { "tenantPublicId": "...", "tenantName": "Coop. Solidaria", "isTenantAdmin": false, "isDefault": true },
  { "tenantPublicId": "...", "tenantName": "Coop. del Pacífico", "isTenantAdmin": true, "isDefault": false }
]
```

Usado por `Pages/Auth/SelectTenant.razor` cuando hay > 1 membresía activa y el usuario debe elegir manualmente.

---

## POST /api/sessions/select-tenant

Selecciona la empresa activa **al inicio de la sesión** (cuando el JWT central aún no trae `active_tenant_id`).

**Authorization**: JWT con `purpose=tenant-select` (emitido por `/api/auth/login` cuando hay múltiples membresías activas) o `purpose=full` (caso edge en que el cliente re-llama tras alguna operación). Otro `purpose` → `403 Identity.WrongTokenPurpose`.

**Request**:

```json
{ "tenantPublicId": "..." }
```

**Responses**:

- `200 OK`:
  ```json
  {
    "accessToken": "eyJ... (con active_tenant_id)",
    "refreshToken": "eyJ...",
    "expiresInSeconds": 1800,
    "tenant": { "tenantPublicId": "...", "tenantName": "..." }
  }
  ```
- `403 Forbidden` con `Membership.NotActive` si el usuario no tiene membresía activa en ese tenant.

**Auditoría**: `Session.TenantSelected`.

---

## POST /api/sessions/switch-tenant

Cambia el tenant activo **dentro de una sesión ya autenticada** sin re-loguear (FR-019 a FR-022).

**Request**:

```json
{ "tenantPublicId": "..." }
```

**Responses**:

- `200 OK` (mismo formato que `select-tenant`).
- `403 Forbidden` con `Membership.NotActive` si la membresía con el destino no está activa.
- `403 Forbidden` con `Tenant.MfaPolicyEnforced` si el destino exige MFA y el usuario no la tiene.

**Side effects** servidor: emite evento `Session.TenantSwitched(centralUserId, fromTenantId, toTenantId)`.

**Side effects** cliente (responsabilidad del SPA Blazor): antes de invocar este endpoint, el cliente DEBE verificar `hasUnsavedChanges` y solicitar confirmación al usuario (FR-022). El backend NO ejecuta esa lógica.

**Auditoría**: `Session.TenantSwitched` con tenants origen y destino.

---

## PUT /api/profile/default-tenant

Configura (o limpia) la empresa por defecto del usuario para próximos logins (FR-016).

**Request**:

```json
{ "tenantPublicId": "..." }
```
o
```json
{ "tenantPublicId": null }
```

**Responses**:

- `204 No Content`.
- `400 Bad Request` con `Profile.DefaultTenant.NotActiveMembership` si se intenta fijar una empresa sin membresía activa.

**Auditoría**: `Profile.DefaultTenantChanged`.
