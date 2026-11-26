# 026 — Contrato HTTP

Grupo `/api/lending/bulk-charges` (`LendingGroup.Grupo`). Toda escritura lleva `Idempotency-Key` (`ConClaveDeOperacion`). Errores con el sobre
de siempre.

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /options` | View | líneas marcadas vigentes con sus valores por defecto, formas de pago admitidas, convenios |
| `GET /?creditLineId=&period=&status=&page=` | View | lista de corridas |
| `GET /{id}` | View | encabezado y totales |
| `GET /{id}/lines?status=&page=` | View | líneas |
| `GET /{id}/export?format=xlsx\|pdf` | View | detalle exportado |
| `POST /` | Run | crea el borrador (`CreateBulkChargeRunCommand`) y calcula la vista previa → 201 |
| `PUT /{id}` | Run | cambia los datos del borrador y recalcula |
| `POST /{id}/preview` | Run | recalcula la vista previa (sin cambiar datos) |
| `POST /{id}/confirm` | Run | `{ expectedPreviewSha256 }` → 202; si la vista previa cambió, 409 `Lending.BulkCharge.PreviewChanged` con la nueva |
| `POST /{id}/discard` | Run | descarta el borrador |
| `POST /{id}/void` | Void | `{ reason }` anula lo vivo de la corrida en segundo plano → 202 |
| `POST /{id}/lines/{lineId}/void` | Void | `{ reason }` anula el cargo de un asociado |
| `GET /{id}/template.xlsx` | Run | plantilla del plano |
| `POST /{id}/import?mode=review\|apply` | Run | carga las líneas del borrador desde el plano |

## Cuerpo de crear y cambiar

```json
{
  "creditLinePublicId": "…", "period": 202612, "chargeDate": "2026-12-01",
  "amount": 13557, "installments": 1, "periodicity": "Monthly",
  "channelMode": "Fixed", "repaymentChannel": "Cashier", "paymentCycle": null,
  "firstDueDate": "2026-12-31",
  "counterpartMode": "Debtor", "counterpartPersonPublicId": null,
  "amortizationSystem": null, "insurances": [],
  "source": "Filters",
  "filters": { "branchPublicIds": [], "deductionTypes": ["C","N"], "deductionPeriods": [], "employerPublicIds": [] }
}
```

## Vista previa (en el detalle)

```json
{
  "status": "Draft", "previewSha256": "…",
  "included": 455, "skipped": 4, "totalAmount": 6168435,
  "accounting": {
    "debit": { "accountCode": "14…", "accountName": "…" },
    "credit": { "accountCode": "26…", "accountName": "…", "requiresThirdParty": true },
    "voucherType": "CO", "outcome": "Ready | Warning | Blocked | NotPosted", "messages": []
  },
  "skippedByReason": [ { "reason": "WithdrawalInProgress", "count": 3 } ]
}
```

## Errores nuevos

`Lending.BulkCharge.LineNotEnabled`, `.OutOfRange`, `.FirstDueDateOutOfCycle`, `.CounterpartRequired`, `.NotDraft`, `.PreviewChanged`,
`.SamePerson` (segregación), `.PeriodClosed`, `.HasMovements`, `.NothingToCharge`, `Lending.CreditLine.BulkChargeIncompatible`.
