# Contract: Memberships

**Módulo Carter**: `MembershipsModule.cs`
**Rutas**: `/api/tenants/{tenantPublicId}/members/*`
**Autorización**: `[RequireTenantAdmin]` (admin del tenant en cuestión) o `[RequireMasterAdmin]` (master, puede operar en cualquier tenant).

---

## GET /api/tenants/{tenantPublicId}/members

Lista los miembros (todas las membresías, incluidas suspendidas y revocadas) del tenant.

**Query params**: `status` (opcional, filtro), `page`, `pageSize`.

**Response** `200 OK`:

```json
{
  "items": [
    {
      "membershipPublicId": "...",
      "centralUserPublicId": "...",
      "email": "ana@coop.coop",
      "displayName": "Ana Pérez",
      "status": "Active",
      "isTenantAdmin": true,
      "invitedAt": "...",
      "activatedAt": "..."
    }
  ],
  "totalCount": 23,
  "page": 1,
  "pageSize": 25
}
```

---

## POST /api/tenants/{tenantPublicId}/members/{membershipPublicId}/suspend

Suspende una membresía activa (Active → Suspended).

**Authorization**: admin del tenant o master.

**Request**: `{ "reason": "Texto opcional" }`.

**Responses**:
- `204 No Content`.
- `409 Conflict` con `Membership.InvalidTransition` si la membresía no está Active.
- `403 Forbidden` con `Membership.LastAdminProtected` si suspender este miembro dejaría al tenant sin ningún admin activo.
- `403 Forbidden` con `Membership.SelfSuspendBlocked.LastAdmin` si el actor intenta suspenderse a sí mismo siendo el único admin.

**Side effects**: invalida la caché de membresías del usuario (Redis), forzando que la próxima petición del usuario suspendido sea rechazada.

**Auditoría**: `Membership.Suspended`.

---

## POST /api/tenants/{tenantPublicId}/members/{membershipPublicId}/activate

Reactiva una membresía suspendida (Suspended → Active).

**Authorization**: admin del tenant o master.

**Response**: `204 No Content`.

**Errors**:
- `409 Conflict` con `Membership.InvalidTransition` si la membresía no está Suspended.

**Side effects**: invalida caché Redis.

**Auditoría**: `Membership.Activated.FromSuspension`.

---

## POST /api/tenants/{tenantPublicId}/members/{membershipPublicId}/revoke

Revoca una membresía (terminal — para reactivar requiere nueva invitación).

**Authorization**: admin del tenant o master.

**Request**: `{ "reason": "Texto" }`.

**Responses**:
- `204 No Content`.
- `403 Forbidden` con `Membership.LastAdminProtected` (idéntica salvaguarda).

**Side effects**:
- Marca `SEC_Users.IsActive = false` en el tenant correspondiente (soft-delete del registro del tenant).
- Invalida caché Redis.

**Auditoría**: `Membership.Revoked`.

---

## POST /api/tenants/{tenantPublicId}/members/{membershipPublicId}/promote-admin

Promueve un miembro regular a admin del tenant (FR-040a).

**Authorization**: admin del tenant (peer-admin model) o master.

**Response**: `204 No Content`.

**Errors**:
- `409 Conflict` con `Membership.AlreadyAdmin`.
- `409 Conflict` con `Membership.NotActive` si el destinatario no tiene membresía Active.

**Auditoría**: `Membership.PromotedToAdmin`.

---

## POST /api/tenants/{tenantPublicId}/members/{membershipPublicId}/demote-admin

Degrada un admin del tenant a miembro regular (FR-040a + salvaguarda FR-040).

**Authorization**: admin del tenant (peer-admin model) o master.

**Response**: `204 No Content`.

**Errors**:
- `403 Forbidden` con `Membership.LastAdminProtected` si la operación dejaría al tenant sin admin.
- `403 Forbidden` con `Membership.SelfDemoteBlocked.LastAdmin`.

**Auditoría**: `Membership.DemotedFromAdmin`.
