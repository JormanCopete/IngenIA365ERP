# Contratos HTTP: Nómina completa — prestaciones, retiro, procedimiento 2, PILA, nómina electrónica y dispersión

**Feature**: 010 | **Date**: 2026-09-20 | **Hermanos**: `servicio-nomina-electronica.md` (ERP → servicio
central), `archivos.md` (PILA, dispersión, consignación de cesantías)

Convenciones vigentes (las mismas de la 009): envelope `{ code, message, traceId, data? }`
(`ErrorEnvelopeFilter`); `Validation.*` → 400, `*.NotFound` → 404, `Concurrency.*` → 409, resto de
negocio → **422**; sin permiso → **404 `Generic.NotFound`**. Todo identificador que cruza es
`PublicId`. Fechas `yyyy-MM-dd`; montos `decimal` con dos decimales. Los enums **entran por nombre o
por número y salen como número** (`EnumPorNombreONumero`); aquí se escriben por nombre y se da el
número entre paréntesis la primera vez. Toda ruta de `/api/payroll` lleva `RequireAuthorization()` +
`AddEndpointFilter<ErrorEnvelopeFilter>()` + `RequirePermission(...)`; los endpoints sólo reenvían
al `ISender` (Principio III) y cada comando tiene su `AbstractValidator` (Principio VIII; los
namespaces nuevos entran a `PrincipioVIII_DualValidation.InScopeNamespacePrefixes` en el primer
commit, R12). Ningún valor legal viaja en el código: todo lo que abajo se llama «parámetro» vive en
`PAY_LegalParameters` o en `PAY_CompanyPolicies` con vigencia (R4).

**Lo que no se duplica.** Una liquidación especial **es una corrida** (`PAY_PayrollRuns` con
`Kind`, R2). Por eso su relación de pago, la marca de pagado, los comprobantes del empleado, el
detalle por empleado con explicación, el cuadre y la exportación son las rutas de la 005 sobre
`/api/payroll/runs/{runId}` (§2), sin alias. Sólo se crean rutas para lo que la ordinaria no tiene:
calcular por semestre/año/empleado, aprobar y reversar **con permiso propio por tipo**, la propuesta
de descuentos de Cartera, el documento para firma y la consignación por fondo.

## 1. Permisos

Recursos nuevos en `PayrollPermissionCatalogSeeder` (misma convención `Resource.Action`). El
reparto separa **cuatro liquidaciones en cuatro recursos** —la R12 proponía uno solo— para que una
cooperativa pueda dar prima y PILA a una persona sin darle definitivas.

| Recurso | Acciones | Notas |
|---|---|---|
| `Payroll.ServiceBonus` | View, Calculate, Approve, Reverse | prima de servicios |
| `Payroll.Severance` | View, Calculate, Approve, Reverse, MarkDeposited | cesantías e intereses anuales; `MarkDeposited` registra la consignación por fondo |
| `Payroll.Vacations` | View, Register, Calculate, Approve, Reverse | saldo, movimientos y liquidación; `Register` = preview y disfrute/compensación |
| `Payroll.Settlements` | View, Calculate, Approve, Reverse, AdjustDeduction, Manage | definitiva y terminación del contrato; `AdjustDeduction` baja el descuento de Cartera; `Manage` = catálogo de motivos de retiro |
| `Payroll.BenefitBalances` | View, Manage | saldos iniciales de prestaciones (FR-007) |
| `Payroll.WithholdingRate` | View, Calculate, Approve | porcentaje fijo del procedimiento 2 |
| `Payroll.Pila` | View, Generate, MarkUploaded, Manage | `Manage` = datos del aportante; la descarga del archivo es `View` |
| `Payroll.ElectronicPayroll` | View, Generate, Transmit, Manage | `Manage` = habilitación, rangos, set de pruebas |
| `Payroll.Disbursement` | View, Generate, MarkSent, Manage | `Manage` = formatos por banco |
| `Payroll.CompanyPolicies` | View, Manage | políticas por empresa con vigencia |
| `Payroll.Holidays` | View, Manage | calendario de festivos |

Patrones de rol (`BuiltInRolesSeeder.PermissionPatterns`): `CompanyAdmin` `*`; `Operator` suma
`Payroll.ServiceBonus.Calculate`, `Payroll.Severance.Calculate`, `Payroll.Vacations.Register`,
`Payroll.Vacations.Calculate`, `Payroll.Settlements.Calculate`, `Payroll.BenefitBalances.Manage`,
`Payroll.WithholdingRate.Calculate`, `Payroll.Pila.Generate`, `Payroll.ElectronicPayroll.Generate`,
`Payroll.Disbursement.Generate` (ve, registra, calcula y genera; **no** aprueba, reversa,
transmite, marca enviado/cargado/consignado, ajusta descuentos ni administra políticas, formatos ni
habilitación: segregación como la ordinaria, FR-006); `Auditor` y `ReadOnly` `*.View` (el auditor
ya exporta por `Payroll.Runs.Export`). La segregación `Payroll.AllowSameUserApproval` compara al
aprobador con `CalculatedBy` de la corrida (R12); si coinciden y la política no lo admite → 422
`Payroll.Settlement.SegregationViolation`, salvo `confirmWithoutSegregation` cuando la política lo
permite, igual que `ApprovePayrollRunCommand`.

Eventos de auditoría (R12): `Payroll.Settlement.Calculated/Approved/Reversed/Discarded/
DeductionAdjusted`, `Payroll.Employee.Terminated/Reinstated`, `Payroll.OpeningBalance.Changed`,
`Payroll.Vacation.Registered/Cancelled`, `Payroll.WithholdingRate.Calculated/Approved`,
`Payroll.Pila.Generated/Uploaded`, `Payroll.ElectronicPayroll.Generated/Transmitted/StatusChanged/
EnablementChanged`, `Payroll.Dispersion.Generated/Sent/Cancelled`, `Payroll.Severance.Deposited`,
`Payroll.CompanyPolicy.Changed`, `Payroll.Holiday.Changed`, `Payroll.Report.Exported`.

## 2. Rutas de corrida que se reutilizan — `/api/payroll/runs/{runId}` (005, sin cambios de forma)

| Ruta existente | Permiso | Qué cambia para una liquidación especial |
|---|---|---|
| `GET /` (resumen) | Runs.View | `RunSummaryDto` suma `kind` (`PayrollRunKind`: `Ordinary` 0, `ServiceBonus` 1, `Severance` 2, `Vacation` 3, `Settlement` 4; viaja como texto), `cutoffDate`, `payDate`, `year?`, `semester?`, `employeePublicId?` (vacaciones y definitiva), `periodPublicId` **nullable** (el campo existente), `warnings: [{ code, message, data }]` (p. ej. `Payroll.Settlement.OpeningBalanceMissing` con los empleados) |
| `GET /employees`, `GET /employees/{employeeId}` | Runs.View | mismas líneas `CalculationLine` con explicación paso a paso; en la definitiva la línea `DESC_CARTERA` trae `data: { proposed, applied, reason, obligationPublicId }` |
| `GET /balance-check` | Runs.View | cuadra contra el comprobante de la liquidación; añade `provision: { accrued, consumed, released, difference }` por concepto |
| `GET /comparison` | Runs.View | compara con la versión anterior **de la misma corrida especial**; sin anterior → `{ previous: null }` |
| `GET /export` | Runs.Export | igual |
| `GET /payments`, `POST /payments`, `POST /payments/{employeeId}/revert` | Payments.* | la relación de pago propia de la liquidación (FR-011a); `PaidAt` es la fecha de pago propia; la marca bloquea la reversión como hoy (`Payroll.PaymentBlocksReversal`) |
| `GET /payslips/pdf`, `GET /payslips/{employeeId}/pdf`, `POST /payslips/send`, `GET /payslips/deliveries` | Payslips.* | comprobante con la etiqueta del tipo («Prima de servicios 2026-II», «Intereses a las cesantías 2026»…) vía `PayslipModelBuilder` |
| `POST /approve`, `POST /reverse`, `POST /discard` | Runs.* | **sólo `Kind = Ordinary`**; para cualquier otro tipo responden 422 `Payroll.Settlement.UseSettlementRoute` con `data: { kind, route }`. Sin esto, `Payroll.Runs.Approve` aprobaría primas y definitivas y los permisos del §1 serían decorativos |

Las rutas por período (`/pay-periods/{periodId}/runs*`) no aplican a las especiales (no tienen
período). Las consultas que hoy asumen período (`historico`, `comparison` de la ordinaria,
`balance-check` por período) filtran `Kind = Ordinary` (R2).

## 3. Liquidaciones especiales — `/api/payroll/settlements`

Cuatro subrecursos con el mismo ciclo: **calcular** (crea la corrida en `Draft`), **recalcular**
(versión nueva, la anterior `Superseded`), **aprobar** (contabiliza en la misma transacción por
`SettlementAccountingPoster` → `AccountingPoster`, único camino al libro; el ciclo común es
`SettlementRunWorkflow`), **reversar** (asiento
espejo) y **descartar** (borrador → `Superseded` sin contabilidad). Aprobar y reversar llevan el
permiso del tipo; el handler comprueba que el `Kind` de la corrida coincide con la ruta (422
`Payroll.Settlement.KindMismatch`).

### 3.1 Prima de servicios — `/service-bonus`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?year=&status=` | ServiceBonus.View | `[{ runPublicId, year, semester, version, status, employees, total, calculatedAt, calculatedBy, approvedAt?, paidCount, postedDocumentPublicId? }]` |
| `POST /` | ServiceBonus.Calculate | `{ year, semester: 1\|2, employeePublicIds?: [] }` → 201 `SettlementCalculatedDto` `{ runPublicId, version, kind, cutoffDate, employees, totals, blockers[], excluded: [{ employeePublicId, name, reasonCode, reason }], warnings[] }` (común a las cuatro). `excluded` explica a quien no tiene derecho (`SettlementReasonCodes`: `SalarioIntegral`, `AprendizLectiva`, `Pasante` (FR-009), `YaPagadaEnDefinitiva`, `SinDiasEnElSemestre`) |
| `POST /{runId}/recalculate` | ServiceBonus.Calculate | → 201 misma respuesta, versión N+1 |
| `POST /{runId}/approve` | ServiceBonus.Approve | `{ confirm: true, postingDate?, confirmEmpty?, confirmWithoutSegregation? }` → `{ runPublicId, documentPublicId, number, total, postingDate, approvedWithoutSegregation }`; `postingDate` por defecto la **fecha de corte** (D-04: la provisión se causa en el mes correcto; el pago lleva `payDate` propio) y sólo entre el corte y hoy (`SettlementAccountingPoster.ResolverFecha`); 422 `Payroll.Settlement.PostingDateInvalid` (`data: { postingDate, cutoffDate, today }`). Sin `confirm` → 422 `Payroll.Settlement.ConfirmationRequired` (el mismo código cuando falta `confirmEmpty` o `confirmWithoutSegregation`) |
| `POST /{runId}/reverse` | ServiceBonus.Reverse | `{ reason }` → `{ runPublicId, reversalDocumentPublicId, reversalNumber }`; 422 `Payroll.PaymentBlocksReversal` si hay pagos marcados |
| `POST /{runId}/discard` | ServiceBonus.Calculate | `{ reason }` |

### 3.2 Cesantías e intereses — `/severance`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?year=&status=` | Severance.View | como 3.1 más `interestTotal`, `severanceTotal`, `funds: [{ fundPublicId, name, employees, amount, depositedAt?, depositedBy?, reference? }]` |
| `POST /` | Severance.Calculate | `{ year, cutoffDate?, employeePublicIds?: [] }` (`cutoffDate` por defecto 31-12 del año) → 201 como 3.1; `excluded` con `SalarioIntegral`, `AprendizLectiva`, `Pasante`, `SinDiasEnElAnio` (el retirado con definitiva aprobada no entra a la población) |
| `POST /{runId}/recalculate` · `/approve` · `/reverse` · `/discard` | Severance.* | como 3.1; al aprobar el comprobante deja la cuenta por pagar **al fondo** por las cesantías y **al empleado** por los intereses (FR-011) |
| `GET /{runId}/deposit-schedule` | Severance.View | relación de consignación por fondo: `{ funds: [{ fundPublicId, fundName, fundNit, pilaCode, lines: [{ employeePublicId, documentType, document, name, hireDate, baseSalary, days, amount }], total, depositedAt? }], grandTotal, dueDate }` (`dueDate` = parámetro `CESANTIAS_FECHA_LIMITE_CONSIGNACION`) |
| `GET /{runId}/deposit-schedule/{fundId}/file?formatId=` | Severance.View | archivo plano del fondo con un formato parametrizable (ver `archivos.md` §3); 422 `Payroll.Severance.FundFormatMissing` si el fondo no tiene formato vigente |
| `POST /{runId}/funds/{fundId}/mark-deposited` | Severance.MarkDeposited | `{ depositedAt, reference }` → registra fecha por fondo (FR-012); 422 `Payroll.Severance.NotApproved`, `.AlreadyDeposited` |

### 3.3 Vacaciones — `/vacations`

La liquidación nace de un movimiento (`PAY_VacationMovements`, R6): registrar el disfrute o la
compensación **crea el movimiento en `Pending` y la corrida `Vacation` en `Draft`** en la misma
acción; aprobar la corrida confirma el movimiento y deja la novedad `VACACIONES` (`NoveltyOrigin`
`VacationLeave`) en cada período que cubre; reversar o descartar anula el movimiento y las
novedades no consumidas. El saldo y la vista previa de días viven en `/api/payroll/vacations` (§5).

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?employeeId=&year=&status=` | Vacations.View | `[{ runPublicId, movementPublicId, employee…, kind, from?, to?, workingDays, calendarDays, compensatedDays?, amount, status }]` |
| `POST /` | Vacations.Calculate | `{ employeePublicId, kind: Enjoyment (0) \| Compensation (1), from?, to?, compensationDays?, paymentDate? }` → 201 `{ runPublicId, movementPublicId, workingDays, calendarDays, skipped: [{ date, reason }], amount, novelties: [{ periodPublicId, days, retroactive }] }`. `Enjoyment` exige `from`/`to`; `Compensation` exige `compensationDays` |
| `POST /{runId}/recalculate` · `/approve` · `/reverse` · `/discard` | Vacations.* | como 3.1; `approve` acepta `postingDate?` (por defecto la fecha de corte de la corrida, D-04; rango [corte, hoy]) |

Errores propios: `Payroll.Vacation.DatesInvalid`, `.NoWorkingDays` (todo festivo o domingo),
`.NoBalance` (`data: { pendingDays }`), `.CompensationOverMax` (`data: { requestedDays, maxDays,
accruedDays, policyCode: "VACACIONES_COMPENSABLE_PCT" }`, FR-016), `.PeriodApproved` (`data:
{ periodPublicId, retroactiveTargetPeriodPublicId }` — la novedad se ofrece como ajuste
retroactivo, Edge Cases), `.Overlaps` (`data: { movementPublicId }`), `.EmployeeTerminated`.

### 3.4 Terminación y liquidación definitiva — `/terminations`

Registrar la terminación **crea la corrida `Settlement` de un solo empleado en `Draft`** (R7). La
ficha se cierra **al aprobar** (`Status`, `TerminationDate`, `Person.IsEmployee = false`) y la
reversión la reabre en la misma transacción (FR-020). La ruta heredada
`POST /api/payroll/employees/{id}/terminate` (motivo como texto libre) **se retira sin alias**: la
ficha llama a esta.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?year=&status=&employeeId=` | Settlements.View | `[{ terminationPublicId, runPublicId, employee…, terminationDate, reasonCode, reasonName, generatesSeverancePay, contractType, status, net, approvedAt? }]` |
| `POST /` | Settlements.Calculate | `{ employeePublicId, terminationDate, reasonCode, contractType: Indefinite (1) \| FixedTerm (2) \| Work (3) \| Apprenticeship (4) \| Occasional (5), contractEndDate?, notes? }` → 201 `{ terminationPublicId, runPublicId, lines[], deductions: [...] (como `GET /{runId}/deductions`), warnings[] }`. `contractEndDate` obligatorio en `FixedTerm`/`Work` cuando el motivo genera indemnización (el tiempo faltante) |
| `GET /{runId}/deductions` | Settlements.View | propuesta desde Cartera (FR-018a): `{ net, items: [{ obligationPublicId, kind: Loan (0) \| Payroll deduction to third party (1), description, capitalBalance, interestBalance, defaultBalance, pendingInstallments, causedNotDeducted?, proposed, applied, reason?, adjustedBy?, adjustedAt?, remainingAfter }], totalProposed, totalApplied, netAfterDeductions }` |
| `PUT /{runId}/deductions/{obligationId}` | Settlements.AdjustDeduction | `{ applied, reason }` → la fila actualizada; recalcula la línea `DESC_CARTERA` sin nueva versión. 422 `Payroll.Settlement.DeductionAboveProposed` (`data: { proposed }`), `.DeductionReasonRequired`, `.NotDraft` |
| `POST /{runId}/recalculate` | Settlements.Calculate | versión nueva; vuelve a leer Cartera y **conserva los ajustes** cuyo `proposed` no cambió (los demás vuelven al saldo y se avisa) |
| `POST /{runId}/approve` | Settlements.Approve | `{ confirm, postingDate?, confirmWithoutSegregation? }` → `{ documentPublicId, number, net, portfolioPayments: [{ obligationPublicId, paymentPublicId, applied, remaining }] }`; cada descuento se aplica con `ProcessPaymentCommand` (Cartera contabiliza el recaudo; la línea de nómina `AffectsAccounting = false`, R7); `postingDate` por defecto `terminationDate` (= fecha de corte, D-04) |
| `POST /{runId}/reverse` | Settlements.Reverse | `{ reason }` → asiento espejo, ficha reabierta (`Payroll.Employee.Reinstated`), pagos de Cartera **no** se reversan solos: la respuesta lista `portfolioPayments` para que Cartera los reverse con su propio flujo, y el mensaje lo dice |
| `POST /{runId}/discard` | Settlements.Calculate | `{ reason }` → borrador descartado y terminación `Cancelled`; la ficha nunca se tocó |
| `GET /{runId}/document` | Settlements.View | PDF para firma (`SettlementDocumentModel`, QuestPDF): empresa, empleado, cargo, fechas, motivo, cada rubro con base y días, deducciones con propuesto/aplicado, neto, firmas. Antes de aprobar sale con marca «BORRADOR» |
| `GET /{runId}/late-payment-penalty?asOf=` | Settlements.View | **informativo** (CST art. 65): `{ asOf, daysLate, dailyRate, penalty, note }`; nunca línea automática (R7) |
| `GET /reasons` · `POST /reasons` · `PUT /reasons/{id}` · `POST /reasons/{id}/deactivate` | Settlements.View / Manage | catálogo `PAY_TerminationReasons`: `{ publicId, code, name, generatesSeverancePay, requiresContractEndDate, isSeeded, isActive }`. Los sembrados no cambian `generatesSeverancePay` ni `code` (422 `Payroll.Termination.ReasonSeeded`); la cooperativa agrega motivos propios |

Errores propios: `Payroll.Termination.PeriodApproved` (`data: { periodPublicId, periodName,
openPeriodPublicId? }`; FR-021: reversar el período o liquidar con fecha en el abierto),
`.DateBeforeHire`, `.DateInFuture`, `.EmployeeAlreadyTerminated` (`data: { terminationPublicId }`),
`.PendingSettlement` (ya hay una definitiva en borrador o aprobada; `data: { runPublicId }`),
`.ReasonNotFound`, `.ContractEndDateRequired`, `Payroll.Settlement.PortfolioUnavailable` (Cartera
no respondió: la propuesta sale vacía con aviso, no se bloquea).

### 3.5 Errores comunes a las cuatro

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Payroll.Settlement.Duplicate` | 422 | ya existe una del mismo tipo, período/corte y empleado en `Draft` o `Approved` (FR-005) | `{ runPublicId, status }` |
| `Payroll.Settlement.NoEligibleEmployees` | 422 | nadie con derecho en el período | `{ excluded[] }` |
| `Payroll.Settlement.ParametersMissing` | 422 | falta un código de `SettlementParameterCodes.Required` vigente a la fecha de corte (R4) | `{ codes: [{ code, asOf }] }` |
| `Payroll.Settlement.ConceptAccountsMissing` | 422 | al aprobar: un concepto de la liquidación sin cuentas en `PAY_ConceptDefinitionAccounts` | `{ conceptCodes[] }` |
| `Payroll.Settlement.AccountingNotInitialized` | 422 | la contabilidad de la cooperativa no está iniciada | |
| `Payroll.Settlement.NotDraft` / `.NotApproved` / `.AlreadyReversed` | 422 | transición inválida | `{ status }` |
| `Payroll.Settlement.KindMismatch` | 422 | la corrida no es del tipo de la ruta | `{ kind }` |
| `Payroll.Settlement.SegregationViolation` | 422 | aprueba quien calculó y la política no lo admite | `{ calculatedBy }` |
| `Payroll.Settlement.ConfirmationRequired` | 422 | falta `confirm`, `confirmEmpty` (corrida sin empleados) o `confirmWithoutSegregation` | |
| `Payroll.Settlement.ApprovalBlocked` | 422 | algún empleado quedó con bandera bloqueante (no aviso) en el cálculo | |
| `Payroll.Settlement.NothingToPost` | 422 | ninguna línea afecta contabilidad | |
| `Payroll.Settlement.CalculationRefused` | 422 | el motor se negó a liquidar a un empleado y dice por qué | |
| `Payroll.Settlement.UseSettlementRoute` | 422 | `approve`/`reverse`/`discard` de `/runs/{runId}` sobre `Kind ≠ Ordinary` (§2) | `{ kind, route }` |
| `Payroll.Settlement.OpeningBalanceMissing` | — | **aviso**, no error: empleado con ingreso anterior al arranque y sin saldo inicial (Edge Cases) | `{ employeePublicIds[] }` en `warnings` |
| `Payroll.PaymentBlocksReversal` | 422 | existente (005) | |
| `Payroll.Run.NotFound` | 404 | | |

## 4. Saldos iniciales de prestaciones — `/api/payroll/benefit-balances`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?asOf=&onlyMissing=&search=` | BenefitBalances.View | `[{ employeePublicId, name, hireDate, asOfDate?, pendingVacationDays?, accruedSeverance?, accruedSeveranceInterest?, accruedServiceBonus?, consumedBy: [{ runPublicId, kind }], updatedAt?, updatedBy? }]`; `onlyMissing=true` lista a quien ingresó antes del arranque y no tiene saldo |
| `GET /{employeeId}` | BenefitBalances.View | la fila vigente + `history[]` |
| `PUT /{employeeId}` | BenefitBalances.Manage | `{ asOfDate, pendingVacationDays, accruedSeverance, accruedSeveranceInterest, accruedServiceBonus, notes? }` → crea o reemplaza mientras nada la consumió |
| `POST /{employeeId}/adjustments` | BenefitBalances.Manage | `{ asOfDate, …mismos campos…, reason }` → fila nueva con vigencia cuando la anterior ya fue consumida (R3) |

422 `Payroll.BenefitBalance.Consumed` (`data: { runPublicIds[] }`: use `adjustments`),
`.AsOfAfterFirstRun` (la fecha es posterior a la primera corrida aprobada del empleado),
`.NegativeValue`, `.NoBalanceToAdjust` (ajuste sin saldo previo: use el `PUT`), `.AsOfDuplicate`
(ya hay un ajuste con esa fecha de corte). Auditoría `Payroll.OpeningBalance.Changed` con antes/después.

## 5. Vacaciones: saldo y movimientos — `/api/payroll/vacations`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /balances?asOf=&search=` | Vacations.View | `[{ employeePublicId, name, hireDate, accruedDays, openingDays, enjoyedDays, compensatedDays, adjustedDays, pendingDays, lastEnjoymentTo? }]` (saldo **derivado**, nunca almacenado) |
| `GET /employees/{employeeId}/balance?asOf=` | Vacations.View | lo anterior + `explanation[]` (días trabajados, suspensiones descontadas, parámetro `VACACIONES_DIAS_ANIO` con vigencia, saldo inicial digitado por quién y cuándo) |
| `GET /employees/{employeeId}/movements` | Vacations.View | `[{ movementPublicId, kind: Enjoyment (0) \| Compensation (1) \| Adjustment (2), from?, to?, workingDays, calendarDays, amount?, runPublicId?, status: Pending (0) \| Confirmed (1) \| Cancelled (2), createdBy, createdAt }]` |
| `POST /working-days` | Vacations.Register | `{ from, to, employeePublicId? }` → `{ workingDays, calendarDays, workWeek: MondayToSaturday (0) \| MondayToFriday (1), skipped: [{ date, reason: Sunday \| Holiday:<nombre> \| Saturday }] }` — la vista previa obligatoria antes de guardar (FR-015) |
| `POST /employees/{employeeId}/adjustments` | Vacations.Register | `{ days (±), reason }` → movimiento `Adjustment` (p. ej. días reconocidos por acuerdo); auditado |
| `POST /movements/{movementPublicId}/cancel` | Vacations.Register | `{ reason }`; sólo `Pending` sin corrida aprobada (422 `Payroll.Vacation.MovementConfirmed`: reverse la corrida) |

## 6. Porcentaje fijo del procedimiento 2 — `/api/payroll/withholding-rates`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?year=&semester=&status=&employeeId=` | WithholdingRate.View | `[{ calculationPublicId, employee…, year, semester, monthsUsed, averageBase, averageBaseUvt, theoreticalWithholding, percentage, status: Calculated (0) \| Approved (1) \| Superseded (2), validFrom?, validTo?, calculatedAt, approvedAt? }]` |
| `POST /calculate` | WithholdingRate.Calculate | `{ year, semester: 1\|2, employeePublicIds?: [] }` → 201 `{ batchPublicId, items: [...como arriba...], skipped: [{ employeePublicId, reason }] }`. Semestre 1 se calcula en junio y rige julio–diciembre; semestre 2 en diciembre y rige enero–junio del año siguiente |
| `GET /{calculationPublicId}` | WithholdingRate.View | explicación mes a mes: `{ months: [{ year, month, grossTaxable, mandatoryContributions, declaredDeductions, exemptIncome, depurated, sourceRuns: [{ runPublicId, kind }] }], divisor, divisorSource: "RETEFTE_P2_DIVISOR", sequence: DepurateThenDivide (0) \| DivideThenDepurate (1), averageBase, uvt, averageBaseUvt, table: { code, validFrom, ranges[] }, theoreticalWithholding, percentage, rounding }` |
| `POST /{calculationPublicId}/approve` | WithholdingRate.Approve | → `{ validFrom, validTo, previousClosedAt? }`; cierra la vigencia anterior en `PAY_EmployeeWithholdingRates` (no la borra, R8) |
| `POST /approve` | WithholdingRate.Approve | `{ calculationPublicIds[] }` → lote; respuesta por ítem |

Errores: `Payroll.WithholdingRate.NoProcedure2Employees`, `.NoHistory` (`data: { employeePublicId,
firstApprovedMonth }`), `.SemesterIncomplete` (**aviso**: falta aprobar corridas del último mes),
`.AlreadyApproved` (`data: { calculationPublicId }`), `.NotCalculated`, `.TableMissing`.

## 7. PILA — `/api/payroll/pila`

Datos del aportante en `PAY_PilaSettings` (fila única por cooperativa), por empleado en la ficha
(§12) y `pilaCode` en los catálogos institucionales (§12). El layout es dato versionado
(`at2-v30-2026-07-24.json` embebido, R9).

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /settings` | Pila.View | `{ contributorType, contributorClass, presentationForm: Single (U) \| Branch (S), branchCode?, branchName?, arlPilaCode, economicActivityCode, divipolaDepartment, divipolaMunicipality, operatorCode, nitLastTwoDigits, complete, missing[] }` |
| `PUT /settings` | Pila.Manage | mismos campos → auditado |
| `GET /layouts` | Pila.View | `[{ code, version, validFrom, validTo?, type1Fields, type1Length, type2Fields, type2Length, source }]` |
| `GET /{year}/{month}/due-date` | Pila.View | `{ dueDate, rule: "PILA_PLAZO_PAGO_POR_NIT", nitDigits }` |
| `POST /{year}/{month}/validate` | Pila.Generate | → `{ canGenerate, blocking, warnings, issues: [{ severity: Blocking (0) \| Warning (1), code, field?, message, employeePublicId?, employeeName?, link }], sources: [{ runPublicId, kind, status }] }` (FR-025). Sin guardar |
| `POST /{year}/{month}/generate` | Pila.Generate | `{ acknowledgeWarnings: true }` → 201 `{ generationPublicId, version, fileName, contributors, lines, totals: { pension, health, arl, ccf, sena, icbf, fsp, total }, reconciliation: { bySubsystem: [{ subsystem, fileTotal, ledgerTotal, difference }], balanced }, issues[] }`. Regenerar deja la anterior `Superseded` (FR-026) |
| `GET /?year=&month=` | Pila.View | versiones: `[{ generationPublicId, period, version, status: Validated (0) \| Generated (1) \| Uploaded (2) \| Superseded (3), layoutCode, generatedAt, generatedBy, totals, balanced, uploadedAt?, operatorReference? }]` |
| `GET /{generationPublicId}` | Pila.View | detalle: cabecera, `lines: [{ lineNumber, employeePublicId, contributorType, subtype, novelties[], days: { pension, health, arl, ccf }, ibc: {…}, contributions: {…}, exempt, fields: { "40": "...", … } }]`, `issues[]`, `reconciliation`, `explanations` por línea |
| `GET /{generationPublicId}/lines/{lineNumber}/explanation` | Pila.View | por campo: valor, origen (`Constant`/`Profile`/`Calculation`/`Blank`), fuente (línea de corrida, parámetro con vigencia, política) |
| `GET /{generationPublicId}/file?acknowledgeDifference=` | Pila.View | `text/plain; charset=us-ascii`, `.txt`, CRLF; 422 `Payroll.Pila.Unreconciled` (`data: reconciliation`) si `balanced = false` y no viene `acknowledgeDifference=true` (FR-027: la diferencia se muestra **antes** de descargar) |
| `POST /{generationPublicId}/mark-uploaded` | Pila.MarkUploaded | `{ uploadedAt, operatorReference, paidAt? }` → `Uploaded` |

Errores: `Payroll.Pila.BlockingIssues` (`data: { issues[] }`), `.NoApprovedRuns`,
`.LayoutMissing` (sin layout vigente para el período), `.ParametersMissing` (`data: { codes[] }`,
`PilaParameterCodes.Required`), `.SettingsIncomplete` (`data: { missing[] }`),
`.WarningsNotAcknowledged`, `.AlreadyUploaded` (regenerar exige que la vigente no esté cargada;
`data: { generationPublicId }`), `.NotGenerated`.

## 8. Nómina electrónica — `/api/payroll/electronic-payroll`

Todo lo de la cooperativa vive en **su** base (`PAY_ElectronicPayrollSettings`, `…NumberingRanges`,
`…Documents`, `…Transmissions`); el ERP construye el XML, lo entrega al servicio central **sin
estado** (`servicio-nomina-electronica.md`) que lo completa (CUNE, SoftwareSC), valida, firma y
transmite, y guarda lo que vuelve. Los secretos (certificado, PIN) **no pasan por aquí**: la
habilitación referencia el **nombre** del Secret de Kubernetes que el dueño creó con
`tools/scripts/crear-secreto-nomina-electronica.ps1`.

### 8.1 Habilitación

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /settings` | ElectronicPayroll.View | `{ nit, dv, businessName, mode: OwnSoftware (0) \| TechnologyProvider (1), environment: Habilitacion (2) \| Produccion (1) (números de la DIAN), softwareId, testSetId?, testSetStatus: NotStarted (0) \| InProgress (1) \| Accepted (2) \| Rejected (3), secretName, certificate?: { subject, issuer, thumbprint, validTo, daysToExpiry } (lo devuelve el servicio al inspeccionar), daneDepartment, daneMunicipality, payrollPeriodCode, ranges: [{ rangePublicId, documentType: 102 \| 103, environment, prefix?, from, to, validFrom, validTo?, next, used }], complete, missing[], deadlineComputation: Calendar (0) \| WorkingDays (1) }` |
| `PUT /settings` | ElectronicPayroll.Manage | mismos campos salvo `certificate`, `testSetStatus`, `complete`, `missing` → auditado `EnablementChanged` (sin el nombre del secreto en el diff público) |
| `POST /settings/ranges` · `PUT /settings/ranges/{id}` · `POST /settings/ranges/{id}/close` | ElectronicPayroll.Manage | rangos internos por tipo y ambiente (R10: no hay rango autorizado por la DIAN); 422 `Payroll.ElectronicPayroll.RangeOverlaps`, `.RangeInUse` (sólo `to` y `validTo` cambian si ya emitió) |
| `POST /settings/inspect-certificate` | ElectronicPayroll.Manage | pide al servicio que inspeccione el certificado del `secretName` → `certificate`; 422 `.SecretNotFound`, `.CertificateExpired`, `.ServiceUnavailable` |
| `POST /test-set/send` | ElectronicPayroll.Manage | `{ documentPublicIds[] }` (documentos generados en ambiente `Habilitacion`) → `{ zipKey, results[] }`; sólo en modo `Habilitacion` (422 `.EnvironmentMismatch`) |
| `POST /test-set/refresh` | ElectronicPayroll.Manage | consulta el estado del set por `zipKey` → `testSetStatus` y detalle |

### 8.2 Documentos del mes

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /{year}/{month}/summary` | ElectronicPayroll.View | `{ dueDate, deadlineComputation, byStatus: { generated, transmitted, accepted, rejected, inProcess }, employeesPaid, employeesWithoutDocument: [{ employeePublicId, reason }], sourceRuns: [{ runPublicId, kind, paidAt? }] }` |
| `POST /{year}/{month}/generate` | ElectronicPayroll.Generate | `{ employeePublicIds?: [], acknowledgeUnpaid?: false }` → 201 `{ generated: [{ documentPublicId, employeePublicId, number, totals }], skipped: [{ employeePublicId, reasonCode, reason, missing[] }] }`. Un documento (`TipoXML` 102) por empleado con pago en el mes, sumando ordinarias y especiales **pagadas** (FR-011a, FR-028); `FechasPagos` desde las marcas de pago; sin marca de pago → se omite salvo `acknowledgeUnpaid` (usa la fecha de aprobación y lo avisa). Un empleado ya con documento aceptado en el mes no se regenera: va por nota (§8.3) |
| `GET /documents?year=&month=&status=&employeeId=&type=` | ElectronicPayroll.View | `[{ documentPublicId, type: 102 \| 103, noteType?: Replace (1) \| Delete (2), number, employee…, year, month, status: Generated (0) \| Signed (1) \| Transmitted (2) \| Accepted (3) \| Rejected (4) \| InProcess (5), cune?, environment, statusCode?, lastAttemptAt?, replacesPublicId?, replacedByPublicId? }]` |
| `GET /documents/{id}` | ElectronicPayroll.View | detalle: `{ …, generatedAt, generationTime, totals: { earnings, deductions, total }, xmlAvailable: { unsigned, signed, applicationResponse }, errors: [{ rule, kind: Rejection (0) \| Notification (1), message, plain }], attempts: [{ attemptPublicId, at, operation, statusCode, statusDescription, isValid, durationMs, outcome }], sourceRuns[] }` |
| `GET /documents/{id}/xml?signed=` | ElectronicPayroll.View | `application/xml`; `signed=true` sólo si existe (404 `Payroll.ElectronicPayroll.SignedXml.NotFound`) |
| `GET /documents/{id}/application-response` | ElectronicPayroll.View | `application/xml` (el `ApplicationResponse` firmado por la DIAN) |
| `GET /documents/{id}/pdf` | ElectronicPayroll.View | representación gráfica (QuestPDF + QR con `CodigoQR`); antes de ser aceptado sale con marca «SIN VALIDAR» y sin CUNE (R10: no es un desprendible; el comprobante del empleado sigue en `/runs/{runId}/payslips`) |
| `POST /documents/transmit` | ElectronicPayroll.Transmit | `{ documentPublicIds[] }` → `{ results: [{ documentPublicId, status, cune?, statusCode?, errors[] }] }`. **Acción explícita** (FR-029); uno por uno contra el servicio (`SendNominaSync`, 60 s); nunca `Accepted` sin CUNE **y** `ApplicationResponse` |
| `POST /documents/{id}/refresh-status` · `POST /{year}/{month}/refresh-status` | ElectronicPayroll.Generate | consulta `GetStatus(CUNE)` vía el servicio (para `InProcess` o `Transmitted`) → estado actualizado, por documento o por mes. **Manual** (D-11): en esta feature no hay trabajo de fondo; el backoff de R10 queda para cuando exista uno por cooperativa |
| `POST /documents/{id}/regenerate` | ElectronicPayroll.Generate | reconstruye el XML **con el mismo número** cuando está `Generated` o `Rejected` (la DIAN nunca lo registró); conserva `attempts` (FR-029 «conserva su historial»); 422 `.AlreadyAccepted`, `.InProcess` |
| `POST /documents/{id}/void-number` | ElectronicPayroll.Manage | `{ reason }`: anula un número que nunca fue aceptado y que no se volverá a usar (queda como hueco documentado en el rango) |

### 8.3 Notas de ajuste

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /{year}/{month}/adjustments/pending` | ElectronicPayroll.View | empleados cuyo mes aceptado cambió (reversión, corrección, liquidación especial pagada después): `[{ employeePublicId, acceptedDocumentPublicId, acceptedTotals, currentTotals, proposedNoteType: Replace \| Delete, reason }]` |
| `POST /{year}/{month}/adjustments` | ElectronicPayroll.Generate | `{ employeePublicIds?: [] }` → 201 `{ generated: [{ documentPublicId, noteType, replacesPublicId, number }], skipped[] }`. `Replace` con los totales actuales; `Delete` si el mes quedó en cero; el número reemplazado no se reutiliza; una nota puede ajustar a otra nota (R10) |

Transmisión, consulta y descarga de notas por las mismas rutas de §8.2 (`type = 103`).

Errores: `Payroll.ElectronicPayroll.SettingsIncomplete` (`data: { missing[] }`, FR-031),
`.NoPaidRuns`, `.RangeExhausted` (`data: { documentType, environment }`), `.RangeMissing`,
`.EmployeeDataMissing` (por documento, en `skipped`: `secondSurname`, `daneMunicipality`,
`workerType`, `contractTypeDian`, `paymentMethodDian`…), `.ConceptMappingMissing` (`data:
{ conceptCodes[] }`: concepto sin ruta en el XML), `.NotGenerated`, `.AlreadyAccepted`,
`.InProcess`, `.EnvironmentMismatch`, `.NothingToAdjust`, `.OriginalNotAccepted`,
`.ServiceUnavailable` (el documento queda `InProcess` si el servicio alcanzó a firmar y enviar;
`Generated` si no), `.ServiceRejected` (el servicio devolvió 4xx: `data: { serviceCode, message }`,
p. ej. XSD inválido), `.CertificateExpired`, `.SecretNotFound`.

## 9. Dispersión bancaria — `/api/payroll/disbursements`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /formats?bankId=&asOf=` | Disbursement.View | `[{ formatPublicId, code, name, bankPublicId, bankName, kind: FixedWidth (0) \| Delimited (1), validFrom, validTo?, isActive, usedBy }]` |
| `GET /formats/{id}` | Disbursement.View | la definición completa (`archivos.md` §2) |
| `POST /formats` | Disbursement.Manage | definición completa → 201; 422 `Payroll.Disbursement.FormatInvalid` (`data: { errors: [{ record, order, field, message }] }`), `.FormatOverlaps` (mismo banco y vigencia cruzada) |
| `PUT /formats/{id}` | Disbursement.Manage | si el formato ya generó archivos, sólo `name`, `validTo` e `isActive` (422 `.FormatInUse`: cree uno nuevo con `validFrom` posterior; los archivos anteriores conservan el suyo, Edge Cases) |
| `POST /formats/{id}/preview` | Disbursement.Manage | `{ runPublicId }` → `{ fileName, sample: [líneas 1-5], lineCount, total }` sin persistir |
| `POST /` | Disbursement.Generate | `{ runPublicId, formatPublicId?, paymentDate, sourceAccountPublicId?, reference?, employeePublicIds?: [] }` → 201 `{ filePublicId, fileName, formatCode, lines, total, excluded: [{ employeePublicId, name, reasonCode: NoBankAccount \| AlreadyPaid \| BankCodeMissing \| ZeroNet, net }], sha256 }`. `formatPublicId` por defecto el vigente del banco pagador de la empresa; `sourceAccountPublicId` la cuenta bancaria de la empresa (`ACC_*` bancaria de la 009) si el formato la exige |
| `GET /?runId=&status=&from=&to=` | Disbursement.View | `[{ filePublicId, runPublicId, runKind, runLabel, bankName, formatCode, lines, total, status: Generated (0) \| Sent (1) \| Cancelled (2), generatedAt, generatedBy, sentAt?, bankReference? }]` |
| `GET /{id}` | Disbursement.View | cabecera + `lines: [{ lineNumber, employeePublicId, name, document, bankCode, accountType, accountNumber, amount, paid }]` + `excluded[]` |
| `GET /{id}/file` | Disbursement.View | el archivo con `Content-Type` y codificación del formato (`text/plain; charset=…`), nombre según el formato |
| `POST /{id}/mark-sent` | Disbursement.MarkSent | `{ sentAt, bankReference, paidAt? }` → marca pagados a **todos** los empleados del archivo con `MarkPaymentsCommand(run, empleados, paidAt ?? sentAt, Transfer, reference = bankReference)` (FR-033; bloquea la reversión); → `{ marked, alreadyPaid: [] }` |
| `POST /{id}/cancel` | Disbursement.Generate | `{ reason }`; sólo `Generated` |

Errores: `Payroll.Disbursement.RunNotApproved`, `.NoFormat` (`data: { bankPublicId }`),
`.NoEmployeesWithAccount`, `.AllPaid`, `.AlreadySent` (`data: { filePublicId }`; regenerar exige
cancelar el anterior si no fue enviado), `.PaymentsAlreadyMarked` (`data: { employeePublicIds[] }`:
alguien fue marcado a mano entre generar y enviar; se excluyen y se avisa), `.SourceAccountRequired`,
`.LineTooLong` (`data: { lineNumber, field }`, ancho fijo desbordado), `.NotGenerated`.

## 10. Políticas por empresa y festivos

### 10.1 `/api/payroll/company-policies`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?asOf=` | CompanyPolicies.View | `[{ key, value, validFrom?, validTo?, allowed[], format?, description, source, publicId?, notes?, versionCount }]` con las **once** claves de `CompanyPolicyKeys` (data-model §2.1): `Exonerada114_1` (`true/false`), `SemanaLaboral` (`LunesASabado`/`LunesAViernes`), `VacacionesPagoAnticipado` (D-01), `CotizaArlEnVacaciones`, `RetefteTopesAnualesModo` (`Acumulado`/`Mensualizado`), `P2SecuenciaDepuracion` (`DepurarLuegoDividir`/`DividirLuegoDepurar`), `DianPlazoComputo` (`Calendario`/`Habiles`), `DianMedioPagoMapa` (JSON), `DeduccionAlRetiroModo` (`SaldoTotal`/`SoloCuotasCausadas`/`NoProponer`), `ArranqueNominaFecha` (`yyyy-MM-dd`), `AllowSameUserApproval` (migrada de `COR_SystemSettings`). `source` dice si el valor viene de `PAY_CompanyPolicies` o del defecto del catálogo |
| `GET /{key}/versions` | CompanyPolicies.View | historial: `[{ publicId, key, value, validFrom, validTo?, notes, createdBy, createdAt, isCurrent }]` |
| `POST /{key}/versions` | CompanyPolicies.Manage | `{ value, validFrom, validTo?, reason, closePrevious = true }` → 201 `{ publicId, warnings[] }`; cierra la vigente el día anterior si se solapa y `closePrevious = true`; si hay corridas aprobadas desde `validFrom`, `warnings[]` lo dice (el cambio no las recalcula). 422 `Payroll.CompanyPolicy.KeyUnknown`, `.ValueInvalid` (`data: { allowed[], format }`), `.VersionOverlaps`, `.RetroactiveNotAllowed` (sólo `Exonerada114_1`: cambia aportes ya contabilizados; `data: { approvedRuns, firstApprovedAt, validFrom }`). Toda vigencia nueva marca los borradores como desactualizados |

### 10.2 `/api/payroll/holidays`

`GET /?year=` → `[{ holidayPublicId, date, name, origin, isSeeded, createdBy, createdAt }]` (View);
`origin` es `HolidayOrigin`: `Ley51Fixed` (1), `Ley51MovedToMonday` (2), `Ley51Easter` (3) —los
tres de la semilla—, `Decreed` (4), `Manual` (5). `POST /` `{ date, name, origin?: Manual \| Decreed }`
→ 201 `{ holidayPublicId }` (Manage; por defecto `Manual`; otro origen → 422
`Payroll.Holiday.OriginInvalid`; fecha repetida → `.DateDuplicate`) · `DELETE /{id}` soft, sólo
`Manual`/`Decreed` (Manage; 422 `Payroll.Holiday.Seeded`; `.NotFound`). Semilla 2026-2028 por la
Ley 51/1983. Auditoría `Payroll.Holiday.Changed`.

### 10.3 Parámetros legales (existente, `/api/payroll/legal-parameters`)

Sin rutas nuevas; entran los códigos de R4 con `Source` preciso. Se agrega
`GET /api/payroll/legal-parameters/missing?process=Ordinary|Settlements|Pila|WithholdingRates|ElectronicPayroll&asOf=`
(LegalParameters.View; `LegalParameterProcess`) → `{ process, asOf, missing: [{ code, description, source }] }`, la misma
lista que devuelve `*.ParametersMissing`, para que la pantalla de parámetros la muestre antes de
liquidar.

## 11. Centro de reportes — `/api/reports/payroll/{vista}?format=json|xlsx|pdf|docx&…`

Mismo mecanismo de la 006 (`TablaExportable` + `EntregaDeInformes`); encabezado obligatorio con
empresa, NIT, filtros, usuario y fecha. `json` y los archivos exigen el `.View` del recurso; toda
exportación a archivo emite `Payroll.Report.Exported`.

| Vista | Filtros | Permiso | Contenido |
|---|---|---|---|
| `liquidacion-especial-resumen` | `runId` | `.View` del tipo | un renglón por empleado: base, días, cada rubro, retención, neto |
| `liquidacion-especial-detalle` | `runId` | `.View` del tipo | empleado × concepto con explicación (como `detalle`) |
| `consignacion-cesantias` | `runId`, `fundId?` | Severance.View | la relación por fondo de §3.2 (FR-012) |
| `saldos-vacaciones` | `asOf` | Vacations.View | saldo por empleado con causado/inicial/disfrutado/compensado |
| `movimientos-vacaciones` | `employeeId?`, `desde`, `hasta` | Vacations.View | |
| `terminaciones` | `desde`, `hasta` | Settlements.View | retiros con motivo, indemnización, neto, estado |
| `saldos-iniciales-prestaciones` | `asOf?` | BenefitBalances.View | |
| `retencion-p2` | `calculationId` \| `year`,`semester` | WithholdingRate.View | mes a mes, promedio, retención teórica, porcentaje |
| `pila-lineas` | `generationId` | Pila.View | una fila por registro tipo 2 con los campos legibles |
| `pila-cuadre` | `generationId` | Pila.View | totales del archivo vs comprobantes `NM` por subsistema (FR-027) |
| `pila-inconsistencias` | `generationId` \| `year`,`month` | Pila.View | |
| `nomina-electronica-estado` | `year`, `month`, `status?` | ElectronicPayroll.View | documento por empleado: número, tipo, estado, CUNE, fecha, errores |
| `dispersion` | `fileId` | Disbursement.View | líneas del archivo y excluidos |

PDF propios (no `TablaExportable`): documento de liquidación definitiva (§3.4), representación
gráfica de la nómina electrónica (§8.2), comprobantes del empleado (§2).

## 12. Cambios en rutas existentes

- `PUT /api/payroll/employees/{id}`, `POST /api/payroll/employees[/with-person]` y
  `GET /api/payroll/employees/{id}` (`EmployeeDto`) suman los bloques `pila: { contributorType,
  contributorSubtype, divipolaDepartment, divipolaMunicipality, economicActivityCode, workCenter,
  salaryTypeCode: F|V|X, foreignNotPensionObligated, colombianAbroad, pensionTransitionRegime:
  Unknown (0)|Yes (1)|No (2), highRiskPension }` y `dian: { workerType, workerSubtype,
  contractTypeDian: 1..5, highRiskPension, paymentMethodCode?, workAddress? }` (R9, R10;
  `FichaPilaDian`), más `apprenticeStage?: Lective (1)|Practical (2)` y
  `disbursementBankPublicId?`. Un bloque que no viene **no toca** lo que había. `salaryTypeCode`
  se **deriva** de `SalaryType` (0/1/2 ↔ F/V/X; la clase `IntegralSalary` siempre es X) y al
  escribirlo se guarda ahí. Tipo y subtipo DIAN son las mismas columnas que el cotizante PILA
  (D-05): si vienen en los dos bloques manda `pila`. `paymentMethodCode` vacío se deriva de la
  forma de pago por la política `DianMedioPagoMapa`. `apprenticeStage` es **obligatoria** si la
  clase es `Apprentice` o `Intern` (422 `Payroll.Employee.ApprenticeStageRequired`). En el `PUT`,
  `disbursementBankPublicId` nulo = no cambia y `clearDisbursementBank: true` lo quita. El `GET`
  devuelve además `disbursementBankName`, `disbursementBankTransferCode`, `vacationBalance?`,
  `openingBalance?`, `currentWithholdingRate?`, `termination?`. La cuenta bancaria de nómina
  (`payrollBankId`, `payrollBankAccountType`, `payrollBankAccountNumber`) ya existe y no cambia.
- `PUT /api/payroll/health-providers/{id}`, `/pension-providers`, `/work-risk-providers`,
  `/severance-providers`, `/family-compensation-funds` aceptan `pilaCode` (6, distinto del `Code`
  de la cooperativa) y `GET /api/core/banks/{id}` expone `transferCode` (código ACH del banco
  destino, R11; **verificar** que el dato migrado sea el ACH).
- Segundo apellido y otros nombres separados se escriben en la persona (`PersonInput`,
  `Components/Personas/PersonaDialog`, único sitio que escribe la persona): `secondLastName`,
  `otherNames` (columnas `COR_People.SecondLastName/OtherNames`, D-06). Sin ellos el documento DIAN
  queda en `skipped`.
- `POST /api/payroll/employees/{id}/terminate` **se retira** (§3.4).
- `POST /api/payroll/runs/{runId}/approve|reverse|discard` rechazan `Kind ≠ Ordinary` (§2).
- No hay trabajo de fondo de consulta de estado DIAN en esta feature (D-11): la consulta es manual
  (§8.2). Si algún día existe, recorre el directorio de cooperativas y abre la conexión de cada
  una; nunca una consulta que barra todas (Principio IV).
