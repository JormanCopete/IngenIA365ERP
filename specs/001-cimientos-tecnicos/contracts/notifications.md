# Contract — Notifications

**Base path**: `/api/notifications`
**Auth**: authenticated; el usuario solo ve sus propias notificaciones.

---

## GET `/api/notifications`

Lista las notificaciones del usuario autenticado.

**Query parameters**

| Name | Type | Notes |
|---|---|---|
| status | string? | `Unread`, `Read`, `Archived`, `All`. Default `Unread`. |
| from | datetime? | |
| to | datetime? | |
| page | int | Default 1. |
| pageSize | int | Default 50. |

**Response 200**

```json
{
  "items": [
    {
      "publicId": "Guid",
      "type": "AccountLocked",
      "subject": "Tu cuenta fue bloqueada",
      "body": "...",
      "createdAtUtc": "...",
      "status": "Unread",
      "deliveredAtUtc": "...",
      "readAtUtc": null
    }
  ],
  "unreadCount": 4
}
```

---

## POST `/api/notifications/{publicId}/read`

Marca como leída. Idempotente (devuelve 200 aunque ya estuviera leída).

**Response 200**: `{ "publicId": "...", "status": "Read", "readAtUtc": "..." }`.

---

## POST `/api/notifications/{publicId}/archive`

Archiva. `Status = Archived`, `ArchivedAtUtc` se rellena.

---

## POST `/api/notifications/read-all`

Marca todas las notificaciones no leídas como leídas. Útil para el botón "marcar todas como leídas".

---

## SignalR hub — `/hubs/notifications`

Conexión autenticada (JWT en query string o cookie). Eventos:

| Event | Payload |
|---|---|
| `notification.created` | `{ publicId, type, subject, createdAtUtc }` |
| `notification.updated` | `{ publicId, status }` |

Al conectar, el cliente recibe `unreadCount` actual.

---

## Internal API (no expuesta, solo MediatR)

El comando `SendNotificationCommand { RecipientUserPublicId, Type, Subject, Body, Channels }` es invocado por handlers internos (post-LoginAttempt lock, post-RoleAssigned, etc.). No tiene endpoint REST público.
