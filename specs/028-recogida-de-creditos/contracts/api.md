# API — 028 Recogida de créditos y productos en el desembolso

Base `/api/lending`. Enums por nombre o número (salen como número). Errores con el sobre de siempre. Escrituras con `Idempotency-Key`.
Fuera del alcance de oficina o línea: el mismo 404 que si no existiera (`Lending.Access.OutOfScope`).

## 1. Productos para recoger (nuevo)

`POST /api/lending/payoffs/candidates` — `Lending.Payoffs.Read`. No escribe; audita `Lending.Payoff.Viewed` por crédito liquidado.

```json
{
  "personPublicId": "…",
  "creditLinePublicId": "…",          // la línea del crédito nuevo: decide los productos admitidos
  "projectedDisbursementDate": "2026-12-05",
  "recordedAsOf": null,               // nulo = ahora
  "selected": [                        // opcional: para simular el neto con lo marcado
    { "kind": "Loan", "productPublicId": "…", "mode": "Total", "installmentTreatment": null }
  ],
  "newLoan": { "amount": 8000000, "monthlyInstallment": 412300 }   // opcional, para neto y capacidad
}
```

Respuesta `PayoffCandidatesDto`:

```json
{
  "date": "2026-12-05", "recordedAsOf": "2026-11-27T15:04:00Z",
  "products": [
    {
      "kind": "Loan", "productPublicId": "…", "number": "65-78925", "line": "PRESTOR18 · …", "origin": "Loan|Commercial|BulkCharge",
      "channel": "Cashier|OwnPayroll|PaymasterDeduction", "daysPastDue": 0, "installment": 303683.06,
      "eligible": true, "blockReason": null,                         // RequiresRestructuring, WrittenOff, Insolvency, NotAdmitted, NoBalance…
      "admittedModes": ["Total", "Partial"],
      "toPayOff": 5123456.78,
      "breakdown": { "principal": 0, "currentInterest": 0, "interestReturned": 0, "interestCharged": 0, "defaultInterest": 0,
                     "insurance": 0, "administration": 0, "otherCharges": 0, "advance": 0, "creditBalance": 0 },
      "interestMode": "Corrido", "interestModeReason": null,
      "currentInstallment": { "number": 7, "dueDate": "2026-11-30", "amount": 312400, "treatment": "Include", "choosable": false,
                              "sent": { "state": "SentWithoutResponse", "paymaster": "…", "file": "ADP-000123", "period": "2026-11",
                                        "lineAmount": 312400 } },
      "toPayOffExcludingCurrentInstallment": 4811056.78,
      "inOtherApplication": null,                                    // número de otra solicitud viva que lo recoge
      "fingerprint": "sha256:…",
      "warnings": ["…"],
      "interestExplanation": [                                       // E-10: el interés renglón a renglón, con los días de la reconstrucción
        { "kind": "Accrued|FullInstallment|Proportional|Returned|RecognizedReleased", "installment": 7, "days": 10, "amount": 104000,
          "text": "Se cobra el interés de 10 días de la cuota 7 hasta la fecha del desembolso (del 21/11/2026 al 30/11/2026): $104.000." }
      ],
      "recognizedReleased": 0                                       // lo reconocido y no causado que se libera (informativo)
    }
  ],
  "summary": { "selectedTotal": 5123456.78, "netToDisburse": 2876543.22, "releasedInstallments": 303683.06,
               "capacityAfter": { "available": 125000, "declared": true } }
}
```

## 2. Solicitud (cambios)

`POST /applications`, `PUT /applications/{id}`, `PUT /applications/{id}/submitted` — cuerpo `CreditApplicationInput` gana:

```json
"payoffs": [
  { "kind": "Loan", "productPublicId": "…", "mode": "Total|Partial", "amount": null, "installmentTreatment": "Include|Exclude|null" },
  { "kind": "Account", "productPublicId": "…", "mode": "Installments|Deposit", "amount": 200000 },
  { "kind": "CommercialReceivable", "productPublicId": null, "reference": "FE-1234", "mode": "Total|Partial", "amount": null }
],
"payoffProjection": { "projectedDisbursementDate": "2026-12-05", "recordedAsOf": "…" }
```

- `ownLoanPayoffs` (lo de hoy) sigue valiendo y se lee como `kind = Loan`; mandar los dos es 400 `Request.BodyInvalid`.
- Errores al guardar o radicar: `Lending.Payoff.NotOfApplicant`, `Lending.Payoff.ProductNotAdmitted`, `Lending.Payoff.RequiresRestructuring`,
  `Lending.Payoff.NotPayable` (castigado, insolvencia, sin saldo), `Lending.Payoff.ModeNotAdmitted`, `Lending.Payoff.NetNotPositive`.
- `GET /applications/{id}` gana `payoffs[]` con lo proyectado (como en §1, por producto) y `payoffSummary` (total, neto, D, R).
  `UsesAdvancedTerms` deja de encenderse por las recogidas.
- Las validaciones (`checks[]`) de capacidad, descuento, endeudamiento, cupo y concentración llevan en su detalle `releasedByPayoffs`.

## 3. Desembolso (cambios)

`POST /applications/{id}/disbursement-preview` y `POST /disbursements` — `DisbursementInput`:

- `ownLoanPayoffs` / `payoffs`: **opcional**. Las recogidas salen de la solicitud; si el cuerpo trae una lista distinta,
  `Lending.Payoff.MustMatchApplication`.
- Nuevo `acknowledgedPayoffDifferences: bool` (obligatorio en `/disbursements` si la vista previa tuvo diferencias u omitidos).

Respuesta de la vista previa, `payoffs[]` (reemplaza a la lista de hoy, que se conserva con sus campos):

```json
{
  "kind": "Loan", "productPublicId": "…", "number": "65-78925", "mode": "Total",
  "projected": 5123456.78, "current": 5098456.78, "toApply": 5098456.78, "difference": -25000,
  "causes": ["Payments"], "detail": { "principal": -25000 },
  "omitted": false, "requiresRestructuring": false,
  "currentInstallment": { "number": 7, "treatment": "Exclude", "left": 312400, "sent": { "…": "…" } },
  "toPayOff": 5098456.78, "daysPastDue": 0                        // campos de hoy
}
```

y `payoffDifferences: { "count": 1, "total": -25000, "requiresAcknowledgement": true, "aboveTolerance": false }`.

Respuesta de `/disbursements`: `payoffs[]` con `applied`, `receiptOperationPublicId` (créditos), `accountOperationPublicId` (cuentas),
`receiptPublicId` (facturas) y `leftInstallment`. `netToDisburse` = bruto − aplicado.

Errores nuevos: `Lending.Payoff.MustMatchApplication`, `Lending.Payoff.DifferencesNotAcknowledged`, `Lending.Payoff.DifferenceAboveTolerance`,
`Lending.Payoff.AppliedDiffers`.

## 4. Contrato de obligaciones (interno)

- `ModalidadDePago.PayoffExceptCurrentInstallment = 8` (sólo `CAR`/`OwnLoanPayoff` en E2; `Receipts` en la caja con
  `Pagos.CuotaEnviadaAlCancelar`).
- `ObligacionListadaDto.SentInstallment` (estado, cuota, valor, pagaduría, archivo, período).
- Rechazo `ObligationNotPayable` de un crédito recogido: `data.successorObligationPublicId`, `data.afterPayoff`.
- La prueba `LaCarteraSoloSeTocaPorSusInterfaces` (métodos del puerto) no cambia: no hay métodos nuevos.

## 5. Informe

`GET /api/reports/lending/payoffs?from=&to=&branch=&line=&advisor=&productKind=Loan|Account|CommercialReceivable&format=json|xlsx|pdf|docx` (E-10: `branch`, el filtro común) — `Lending.Reports.View` / `.Export`,
más `Lending.Loans.Read` (datos del deudor) y `Lending.Reports.ExportPersonalData` al exportar, como las demás vistas con datos personales.
Columnas: fecha, crédito nuevo, solicitud, deudor, oficina, línea, producto, tipo, modo, proyectado, aplicado, diferencia, causa, cuota dejada,
interés devuelto/cobrado, asesor, decisor, quien desembolsó, estado (aplicada, reversada u omitida); oculta `_credito` (el crédito nuevo).

`GET /api/lending/loans/{id}` gana `paidOffBy[]` («Recogido por el crédito … el …») y `paidOff[]` («Recogió: …»): `{ kind, loanPublicId, number, date, mode,
applied, reversed }`; `GET /loans/{id}/transactions` gana `description` («Recogido con el crédito …») en las filas del pago de la recogida (E-10).

## 6. Recaudo (E2 y E4)

- Caja: `GET /api/receipts/payables` gana por crédito `sentInstallment` (de §4) y `admitsPayoffExceptCurrentInstallment`.
- E4: puerto `IRecogidaEnRecaudo` (interno, sin ruta HTTP).
