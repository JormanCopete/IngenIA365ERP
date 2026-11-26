# 026 — Modelo de datos

Todo es aditivo. Migración par de cooperativa `CargosMasivos`.

## LND_BulkChargeRuns — la corrida (`BulkChargeRun`, `AuditableEntity`)

| Campo | Tipo | Nota |
|---|---|---|
| Id, PublicId | int, Guid | |
| Number | varchar(20) | consecutivo `CGM-000001` del numerador de Cartera |
| CreditLineId | int | línea marcada |
| CreditLineVersionId | int | la vigente a la fecha del cargo al confirmar (se vuelve a leer al confirmar) |
| Period | int | aaaamm |
| ChargeDate | date | fecha del cargo: valor y contable |
| Amount | decimal(18,2) | valor por defecto de las líneas |
| Installments | int | ≥ 1 |
| Periodicity | int | `RepaymentPeriodicity` |
| RepaymentChannelMode | int | `BulkChargeChannelMode`: `Fixed` = 1, `FromAssociate` = 2 |
| RepaymentChannel | int? | con `Fixed` |
| PaymentCycle | int? | `AccrualDiscountCycle` |
| FirstDueDate | date | |
| CounterpartMode | int | `BulkChargeCounterpart`: `Debtor` = 1, `Person` = 2 |
| CounterpartPersonId | int? | con `Person` |
| AmortizationSystem | int? | si la línea lo pide |
| Insurances | text (JSON) | códigos, si la línea los tiene |
| Source | int | `BulkChargeSource`: `Filters` = 1, `File` = 2 |
| FiltersJson | text | agencia, pagaduría, forma y período de descuento de la ficha |
| Status | int | `BulkChargeRunStatus` (abajo) |
| PreviewSha256 | char(64) | huella de la última vista previa |
| TotalLines, CreatedCount, SkippedCount, FailedCount, VoidedCount | int | |
| TotalAmount | decimal(18,2) | suma de lo creado vivo |
| ProcessRunId | int? | `LND_ProcessRuns` de la ejecución |
| VoidProcessRunId | int? | de la anulación entera |
| PreparedByUserId, ConfirmedByUserId | int? | `SEC_Users.Id` |
| ConfirmedAt, CompletedAt, VoidedAt | timestamp? | |
| VoidReason | varchar(500)? | |
| IntegrationBatchId | int? | lote contable de la corrida |

**Estados** (`BulkChargeRunStatus`): `Draft` 1 → `Queued` 2 → `Running` 3 → `Completed` 4 · `Draft` → `Discarded` 7 ·
`Completed` → `Voiding` 5 → `Voided` 6 (si queda algo vivo, vuelve a `Completed` con `VoidedCount`). Sólo el borrador se edita, recalcula o
descarta.

Índices: único `Number`; `(CreditLineId, Period)`; `Status`.

## LND_BulkChargeLines — una por asociado (`BulkChargeLine`, `AuditableEntity`)

| Campo | Tipo | Nota |
|---|---|---|
| Id, PublicId | int, Guid | |
| RunId | int | |
| PersonId | int | deudor |
| CreditLineId, Period | int, int | copiados de la corrida, para la unicidad |
| BranchId | int | agencia del asociado |
| Amount | decimal(18,2) | |
| Installments | int | |
| FirstDueDate | date | |
| RepaymentChannel | int | resuelto |
| PayrollAgreementId | int? | nómina propia o pagaduría |
| Status | int | `BulkChargeLineStatus`: `Pending` 1, `Created` 2, `Skipped` 3, `Failed` 4, `Voided` 5 |
| Reason | varchar(60)? | código del motivo (omisión, falla, anulación) |
| ReasonDetail | varchar(500)? | texto |
| Warning | varchar(60)? | aviso no bloqueante (forma de pago sustituida) |
| LoanId | int? | la obligación creada |
| SourceRow | int? | fila del plano |
| HoldsPeriod | bool | verdadero en `Pending` y `Created` |
| ProcessedAt | timestamp? | |

Índices: único filtrado `(CreditLineId, PersonId, Period) WHERE HoldsPeriod`; `(RunId, Status)`; `LoanId`.

**Motivos** (`MotivosDeCargoMasivo`): `Withdrawn`, `Deceased`, `WithdrawalInProgress`, `AlreadyCharged`, `NotAssociate`, `NoPayrollAgreement`,
`NoOwnPayrollAgreement`, `ChannelNotAllowed` (aviso), `LineRule` (falla de la línea, con detalle), `HasMovements` (anulación rechazada),
`Voided`.

## Cambios a tablas existentes

- `LND_CreditLineVersions`: `AllowsBulkCharge bool default false`, `BulkChargeDefaultAmount decimal(18,2) null`,
  `BulkChargeDefaultInstallments int null`, `BulkChargeReportsToBureaus bool default false`, `BulkChargeEntersCollection bool default false`.
- `LND_Loans`: `BulkChargeLineId int null` (índice). `Origin` admite `BulkCharge = 4`.
- `LND_LoanOperations.DisbursementForm` admite `Charge = 5`.
- `LND_LoanTermsVersions.Cause` admite `BulkCharge = 13`.
- `LND_ProcessRuns.Kind` admite `BulkCharge = 9`, `BulkChargeVoid = 10`.
- `LND_RegulatoryFileLines.ExclusionReason` admite `BulkChargeNotReported`.

## Semillas (sin migración)

- `voucher-types.json`: `CO` «Cargos masivos», `Module`, `CAR`.
- `lending-voucher-mappings.json`: `CargoMasivoRegistrado → CO`, `CargoMasivoAnulado → CO` (cruce `PG`).
- `LendingFactTypesSeeder`: `CargoMasivoRegistrado` (`CMS`) y `CargoMasivoAnulado` (`CMA`), modo de fábrica `PorLotes` (la corrida sella el suyo).
- Permisos `Lending.BulkCharges.View/Run/Void` (API al arrancar). Parámetro `Segregacion.CargoMasivoPorOtraPersona`.

## Mensajes (contratos de integración, versión 1)

- `CargoMasivoRegistradoV1`: `Loan`, `DebtorPersonPublicId`, `ValueDate`, `RegisteredAt`, `Dimensions`, `Amounts` (`Principal`),
  `RulesVersion`, `Plan`, `BulkChargeRunPublicId`, `CounterpartPersonPublicId`.
- `CargoMasivoAnuladoV1`: `Loan`, `DebtorPersonPublicId`, `ValueDate`, `RegisteredAt`, `Dimensions`, `Amounts`, `OriginalMessageId`,
  `BulkChargeRunPublicId`, `CounterpartPersonPublicId`, `Reason`.
