# Modelo de datos — 028 Recogida de créditos y productos en el desembolso

Todo en la base de cada cooperativa. Entidades `AuditableEntity` con soft-delete y filtro (Principio VII), `PublicId` hacia afuera (VI),
configuración `ComoEntidadDeCartera`. Montos `decimal(18,2)`; los de UVR no aplican (la recogida de un crédito en UVR va en pesos).

## Enums nuevos (Domain/Enums/Lending)

- `PayoffProductKind`: `Loan = 1`, `Account = 2`, `CommercialReceivable = 3`.
- `PayoffMode`: `Total = 1`, `Partial = 2`, `Installments = 3`, `Deposit = 4` (abono a un ahorro o aporte extraordinario).
- `InstallmentTreatment` (cuota causada): `Include = 1`, `Exclude = 2`.
- `PayoffDifferenceCause` (banderas): `Payments = 1`, `Accrual = 2`, `Arrears = 4`, `CurrentInstallment = 8`, `Parameters = 16`,
  `ProductClosed = 32`, `Other = 64`.
- `SentInstallmentState`: `None = 0`, `SentWithoutResponse = 1`, `DeductedPendingTransfer = 2`, `NotDeducted = 3`, `OwnPayroll = 4`.

En el contrato de obligaciones (`Application/Common/Integration/Lending/EnumsDelContratoDeCartera.cs`): `ModalidadDePago.PayoffExceptCurrentInstallment = 8`.

## `LND_CreditApplicationPayoffs` — recogida proyectada (E1)

Una fila por solicitud y producto. Se reescribe entera al guardar la solicitud mientras es editable (las filas anteriores quedan dadas de baja,
Principio XI no aplica: no es un movimiento).

| Columna | Tipo | Nota |
|---|---|---|
| `Id`, `PublicId` | int, uuid | |
| `CreditApplicationId` | int FK `LND_CreditApplications` | |
| `ProductKind` | `PayoffProductKind` | |
| `LoanId` | int? FK `LND_Loans` | con `Loan` |
| `AccountId` | int? FK `LND_MemberAccounts` | con `Account` |
| `CommercialReceivableReference` | varchar(60)? | con `CommercialReceivable`: documento cruce de la factura |
| `ProductNumber` | varchar(40) | número visible (crédito, cuenta, factura), copiado para el informe |
| `Mode` | `PayoffMode` | |
| `RequestedAmount` | decimal? | parcial o abono |
| `InstallmentTreatment` | `InstallmentTreatment`? | sólo créditos; el resuelto (parámetro o elección del asesor) |
| `InterestMode` | varchar(20) | `Corrido` / `CuotaCompleta` resuelto a D, con su motivo legal si lo hubo |
| `ProjectedDate` (D) | date | |
| `RecordedAsOf` (R) | timestamp | |
| `ProjectedAmount` | decimal | lo que se recogería a D con R |
| `ProjectedBreakdown` | json | por concepto: capital, interés corriente, devuelto/cobrado por proporcionales, mora, seguro, administración, otros, anticipo, saldo a favor |
| `CurrentInstallmentNumber`, `CurrentInstallmentAmount` | int?, decimal? | la cuota causada |
| `SentInstallmentState` | `SentInstallmentState` | con su `PayrollDeductionFileId?` |
| `ProjectionSha256` | char(71) | huella de la liquidación |
| `Order` | int | orden de aplicación (el de la pantalla) |

Índices: `(CreditApplicationId, ProductKind, LoanId, AccountId, CommercialReceivableReference)` único filtrado a vivas; `LoanId` para el aviso de
«ya está en otra solicitud» (FR-009).

Validación (al radicar): el producto es del solicitante como deudor (créditos) o titular (cuentas, facturas por tercero); vivo; admitido por
`Recogida.ProductosAdmitidos` de la línea de la solicitud; `RequestedAmount > 0` en parcial y abono; neto positivo.

**Migración de datos**: cada `LND_CreditApplications.PayoffRequest` no vacío se copia a filas `Loan` con `Mode` según su `Mode` y sin
proyección (`ProjectedAmount = 0`, `ProjectionSha256` vacío); la columna se conserva y deja de escribirse.

## `LND_DisbursementPayoffs` — recogida aplicada (E1; columnas de la cuota dejada en E2)

Una fila por operación de desembolso y producto.

| Columna | Tipo | Nota |
|---|---|---|
| `Id`, `PublicId` | int, uuid | |
| `DisbursementOperationId` | int FK `LND_LoanOperations` | la operación `LoanDisbursement` |
| `CreditApplicationPayoffId` | int? FK | la proyectada de la que viene |
| `ProductKind`, `LoanId`, `AccountId`, `CommercialReceivableReference`, `ProductNumber` | | como arriba |
| `Mode`, `InstallmentTreatment`, `InterestMode` | | los aplicados |
| `ProjectedAmount` | decimal | copia de la proyectada (0 si no había) |
| `CurrentAmount` | decimal | liquidado a la fecha valor |
| `AppliedAmount` | decimal | lo que aplicó el contrato o el registro |
| `Difference` | decimal | `CurrentAmount − ProjectedAmount` |
| `DifferenceCause` | `PayoffDifferenceCause` | |
| `DifferenceDetail` | json | por concepto |
| `Omitted` | bit | el producto ya estaba cancelado |
| `IdempotencyKey` | varchar(120) | `CAR:{op:N}:{producto:N}` |
| `PaymentOperationId` | int? FK `LND_LoanOperations` | la operación de pago del crédito recogido |
| `AccountOperationId` | int? | la operación de la cuenta (E3) |
| `ReceiptPublicId` | uuid? | el recibo de Recaudo (E4) |
| `ReversedAt`, `ReversalKey` | timestamp?, varchar? | al anular el crédito nuevo |
| `AcknowledgedBy`, `AcknowledgedAt` | varchar?, timestamp? | quien reconoció la diferencia |
| **E2** `LeftInstallmentNumber`, `LeftInstallmentAmount` | int?, decimal? | la cuota dejada a su canal |
| **E2** `LeftInstallmentSettledAt` | timestamp? | cuando la cuota dejada quedó pagada |

## Cambios en tablas existentes

- **E2** `LND_PayrollDeductionResults.LoanClosedAtResult` (bit, defecto falso): el crédito ya no estaba activo al llegar el resultado.
- `LND_CreditApplications.PayoffRequest`: se conserva, deja de escribirse.

## Mensajes

- `CreditoDesembolsadoV1.PayoffDeductions[]` (`PayoffDeductionV1`): gana `ProductKind` (opcional, ausente = `Loan`), `AccountRef` y
  `ReceivableRef` opcionales. Compatible hacia atrás.
- Rechazo del contrato `Lending.Payment.ObligationNotPayable` de un crédito recogido: `Data` gana `successorObligationPublicId` y `afterPayoff`
  (`SaldoDeLaPagaduria` | `AlCreditoNuevo` | `AFavorDelAsociado`).
- `ObligacionListadaDto` (contrato de obligaciones): gana `SentInstallment` (estado, cuota, valor, pagaduría, archivo, período).

## Parámetros (`COR_ParameterVersions`, sin esquema)

`Recogida.CuotaCausada`, `Recogida.InteresesProporcionales`, `Recogida.ProductosAdmitidos`, `Recogida.AlDesembolsar`,
`Recogida.ToleranciaDeDiferencia`, `Recogida.GiroPosteriorDeCreditoRecogido`, `Pagos.CuotaEnviadaAlCancelar` (Cartera) y
`Accounting.LendingDisbursementWithPayoffsInOneVoucher` (Contabilidad). Valores y defectos en la spec.

## Transiciones

- **Recogida proyectada**: viva mientras la solicitud es editable; se congela al aprobar; queda como referencia histórica.
- **Recogida aplicada**: `Applied` al desembolsar → `Reversed` al anular el crédito nuevo antes del giro (con su clave de reverso). Con cuota
  dejada (E2): `LeftInstallmentSettledAt` se llena cuando la cuota dejada se paga.
- **Resultado de libranza** (E2): `LoanClosedAtResult = true` lo saca de pendientes de giro, de la tarea de giros atrasados y de centrales.
