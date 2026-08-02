# Contract — Listado de solicitudes de MFA reset (aprobador)

## GET /api/auth/mfa/reset/requests

Autorización: admin del tenant activo o master admin — el mismo guard que
los endpoints existentes de request/approve (Fase 0). Corre bajo el tenant
context del middleware (entidad per-tenant).

### Query params

| Param | Tipo | Default | Nota |
|---|---|---|---|
| `status` | enum `Pending\|Approved\|Rejected\|Executed\|Expired` | `Pending` | Filtro por estado. |
| `page` | int | 1 | |
| `pageSize` | int | 20 | máx. 100 |

### Response 200

```json
{
  "items": [
    {
      "publicId": "9c2f…",
      "affectedUser": { "publicId": "…", "username": "luis.martinez", "email": "luis.martinez@coop.solidaria.test" },
      "requestedBy": { "publicId": "…", "username": "ana.perez" },
      "requestedAt": "2026-08-01T14:00:00Z",
      "reason": "Perdió el teléfono en visita a sucursal.",
      "hasEvidence": true,
      "status": "Pending",
      "firstApprovalAt": null,
      "secondApprovalAt": null,
      "expiresAt": "2026-08-02T14:00:00Z"
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

- Los DTOs exponen `PublicId` (nunca `int Id`, principio VI) y resuelven
  usernames/correos para que el aprobador no maneje GUIDs a mano (SC-106).
- Las reglas de aprobación NO cambian: doble aprobación, aprobadores
  distintos, sin auto-aprobación, ventana de 24 h — todo sigue en los
  handlers existentes de request/approve.

### Errores

| HTTP | code | Caso |
|---|---|---|
| 401 | `Session.TenantNotSelected` | Sin tenant activo (middleware, sin cambios). |
| 403 | `Identity.Forbidden` | No es admin del tenant ni master. |

## Consumidor de UI

`MfaResetApprovals.razor` (/security/mfa-reset-approvals, enlazada en la
navegación admin): tabla de pendientes con solicitante, afectado, motivo,
fechas y acciones **Aprobar / Rechazar** (endpoints existentes por
`publicId`), más el formulario de alta de solicitud (absorbe la página
`MfaResetRequests.razor`, que se retira). Una solicitud resuelta desaparece
del filtro Pending; el intento de aprobarla dos veces recibe el error del
handler existente.
