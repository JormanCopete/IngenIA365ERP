# Contrato de API — 023

Sobre PublicId (Principio VI); sobre de error de siempre; toda escritura con `Idempotency-Key`.

## 1. Cobro del POS — `POST /api/inventory/pos/drafts/{id}/checkout` (cambia el comportamiento, no la forma)

- Después del commit espera el documento electrónico **leyendo la fila** a lo sumo `Dian.EsperaMaximaPosSegundos` (defecto 2).
- `ticket` **siempre** viene cuando la venta queda `Confirmed`:
  - validado dentro de la espera → tirilla con `uniqueCode` y `qrContent`;
  - no validado → tirilla **sin** código único ni QR y `legend = "Documento electrónico pendiente de validación DIAN"`;
  - contingencia → la leyenda de contingencia de hoy.
- `electronic` gana campos (todos opcionales, los clientes viejos los ignoran):

```json
"electronic": {
  "electronicDocumentPublicId": "…", "kind": 1, "status": 1, "uniqueCode": null, "qrContent": null,
  "contingencyType": null, "waitedMs": 2003, "deliverable": false, "pendingDelivery": true, "messages": [],
  "displayStatus": "PendingValidation",
  "transientFailures": 0, "attemptLimit": 12, "nextAttemptAt": null, "deadAt": null
}
```

`displayStatus`: `PendingSend | Sent | Retrying | Validated | ValidatedWithNotices | Rejected | Dead | Contingency | PendingValidation`
(la última = la tirilla salió sin CUDE). Lo mismo en el detalle de la venta (`GET /api/inventory/pos/drafts/{id}` y el detalle del documento) y en la bandeja de documentos electrónicos.

## 2. Reimpresión — `POST /api/inventory/documents/{id}/reprint` (existente)

Refleja el estado del momento: validado → con CUDE/QR y sin leyenda de pendiente.

## 3. Confirmación de factura en oficina (existente)

Responde sin intentar la emisión; `electronic.displayStatus = PendingSend`.

## 4. Mensajes muertos (nuevo) — grupo `/api/platform/dead-letters`

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /api/platform/dead-letters?kind=&from=&to=&page=&size=` | `Platform.DeadLetters.View` | Lista paginada: documentos muertos, seguimientos muertos y entregas muertas. |
| `GET /api/platform/dead-letters/{kind}/{publicId}` | `Platform.DeadLetters.View` | Detalle con historia de intentos (transmisiones o intentos de entrega) y último motivo. |
| `POST /api/platform/dead-letters/reprocess` | `Platform.DeadLetters.Reprocess` | Reprocesa una lista. |
| `GET /api/platform/dead-letters/summary` | `Platform.DeadLetters.View` | Conteos por tipo y el más viejo (tarjeta del tablero). |

`kind`: `ElectronicDocument | ElectronicFollowUp | IntegrationDelivery`.

Fila de la lista:

```json
{ "kind": "ElectronicDocument", "publicId": "…", "reference": "SETP990000123", "counterparty": "Consumidor final",
  "amount": 125000.00, "deadAt": "…", "attemptLimit": 12, "attempts": 12, "lastError": "Canal no disponible (503)",
  "module": "INV", "destination": null }
```

Reprocesar:

```json
POST /api/platform/dead-letters/reprocess
{ "items": [ { "kind": "ElectronicDocument", "publicId": "…" } ], "reason": "El proveedor volvió a responder" }
→ 200 { "reprocessed": 1, "skipped": [ { "publicId": "…", "code": "DeadLetters.NotDead" } ] }
```

Validación (`ReprocessDeadLettersCommandValidator`): 1–200 ítems, `reason` 10–500. Errores: `DeadLetters.NotDead` (ya no está muerto:
se omite, no falla la petición), `DeadLetters.NotFound` (404 si ninguno existe).

## 5. Caja (existente) — `POST|PUT /api/inventory/points-of-sale/{id}/cash-registers[/{registerId}]`

Campo nuevo `directPrint: bool` (defecto `false`), junto a `printCopies` y `receiptWidthMm`. Viaja también en la caja que el POS ya lee al abrir la sesión, para que la pantalla sepa si imprime directo.

## 6. Parámetros

`GET/PUT` de parámetros existentes muestran `Dian.TopeDeIntentos` e `Integracion.TopeDeIntentos` y la nueva descripción de
`Dian.EsperaMaximaPosSegundos`.

## 7. Métricas (no pública)

`GET :9464/metrics` (puerto aparte, sin Ingress): formato Prometheus. Ver research R8.
