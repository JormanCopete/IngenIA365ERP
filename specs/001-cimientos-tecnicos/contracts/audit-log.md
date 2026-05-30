# Contract — Audit log

**Base path**: `/api/audit/events`
**Auth**: `AuditLog.View` (+ `AuditLog.Export` para exportar).
**Tenant scope**: implícito en el JWT — el query siempre se filtra por `tenantId` del usuario; nunca se acepta `tenantId` en query string.

---

## GET `/api/audit/events`

**Query parameters**

| Name | Type | Notes |
|---|---|---|
| from | datetime (ISO UTC) | Inicio del rango. Default = ahora − 7 días. |
| to | datetime (ISO UTC) | Fin del rango. Default = ahora. |
| userPublicId | Guid? | |
| entityType | string? | `User`, `Role`, `Attachment`, ... |
| entityPublicId | Guid? | |
| operationType | string? | `Create`, `Update`, ... |
| failureOnly | bool? | Default false. |
| q | string? | Filtro libre en `username` o `failureCode`. |
| page | int | Default 1. |
| pageSize | int | Default 50, max 200. |

**Response 200**

```json
{
  "page": 1,
  "pageSize": 50,
  "totalCount": 12345,
  "items": [
    {
      "publicId": "Guid",
      "occurredAt": "2026-05-28T14:23:45Z",
      "userId": "Guid",
      "username": "jcopete",
      "ip": "10.0.0.7",
      "userAgent": "Mozilla/5.0 ...",
      "operationType": "Update",
      "entityType": "Role",
      "entityPublicId": "Guid",
      "result": "Success",
      "failureCode": null,
      "changes": [ { "field": "Permissions", "before": "...", "after": "..." } ]
    }
  ]
}
```

**Errors**

- `AuditLog.RangeTooLarge` — ventana mayor a 6 meses requiere export.

**Performance contract**: p95 < 5 s para ventana de 1 mes con `totalCount <= 1_000_000` (SC-004).

---

## GET `/api/audit/events/export.csv`

**Permission**: `AuditLog.Export`

Mismos parámetros que `GET /events`. Responde streaming `text/csv; charset=utf-8` con BOM. Header `Content-Disposition: attachment; filename="audit_{tenantNit}_{from}_{to}.csv"`.

**Columnas**: `Timestamp UTC, Usuario, IP, Operacion, Entidad, IdentificadorPublico, Resultado, CodigoFallo, CambiosJson`.

**Limit**: 100.000 filas por export; más allá → `AuditLog.ExportTooLarge` (segmentar por rango).

---

## GET `/api/audit/events/export.pdf`

**Permission**: `AuditLog.Export`

Mismos parámetros. Responde `application/pdf` con PDF firmado.

**Contenido**:
- Portada: razón social, NIT, rango exportado, usuario solicitante, total registros, hash SHA-256 del payload, HMAC de firma.
- Cuerpo: tabla paginada con los eventos.
- Pie: huella criptográfica reproducible.

**Header**: `Content-Disposition: attachment; filename="audit_{tenantNit}_{from}_{to}.pdf"`.

**Limit**: 100.000 filas por export; más allá → `AuditLog.ExportTooLarge`.

---

## Verification of PDF signature (out-of-band)

Endpoint operativo para el operador SaaS (no para el cliente):

`POST /api/saas/audit/verify` — recibe el PDF, recalcula el HMAC con la clave activa o las claves históricas, devuelve `{ valid: true|false, keyVersion: "...", computedHmac: "..." }`. Permiso `Saas.AuditLog.Verify`.
