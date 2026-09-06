# Contratos HTTP — Nómina: novedades y liquidación (Feature 005)

Convenciones heredadas del repositorio: `/api/payroll/{recurso-plural-kebab}`;
identificadores **siempre `PublicId`** (Guid); JWT central con `active_tenant_id`
resuelto por `TenantResolutionMiddleware` (ninguna ruta de nómina es exenta);
`Result` → envelope de error `{ errorCode, message, traceId }` vía
`ErrorEnvelopeFilter`; todo endpoint `.RequireAuthorization().RequirePermission(…)`;
sin permiso responde **404** indistinguible. Fechas ISO-8601 (`yyyy-MM-dd` para
fechas civiles). Montos `decimal` con dos decimales.

Los endpoints Carter sólo mapean y reenvían a `ISender` (Principio III). Cada comando
tiene su `AbstractValidator` (Principio VIII; namespaces cubiertos por el test de
arquitectura).

---

## 1. Planes de nómina — `/api/payroll/plans`

| Método | Ruta | Comando / Query | Permiso |
|---|---|---|---|
| GET | `/api/payroll/plans` | `ListPayrollPlansQuery` | `Payroll.Plans.View` |
| POST | `/api/payroll/plans` | `CreatePayrollPlanCommand { code, name, periodicity }` | `Payroll.Plans.Manage` |
| PUT | `/api/payroll/plans/{planId}` | `UpdatePayrollPlanCommand { name, isActive }` | `Payroll.Plans.Manage` |
| POST | `/api/payroll/employees/{employeeId}/plan` | `ChangeEmployeePlanCommand { planId, effectiveFrom }` | `Payroll.Plans.Manage` |

`PayrollPlanDto { publicId, code, name, periodicity: "Monthly"\|"Biweekly", isDefault, isActive, employeeCount }`

Errores: `Payroll.PlanCodeDuplicate`, `Payroll.PlanHasEmployees` (desactivar),
`Payroll.PlanChangeInsideOpenPeriod` (la fecha de efecto cae dentro de un período
abierto del plan actual: se acepta pero rige desde el siguiente período; el mensaje lo
dice), `Payroll.PlanNotFound`.

## 2. Períodos — `/api/payroll/pay-periods` (existente, extendido)

`PayPeriodDto` añade `planPublicId, planCode, status: "Open"|"Calculated"|"Approved"|"Reversed",
currentRunPublicId?, approvedAt?, approvedBy?`. `POST` exige `planPublicId` y valida no
superposición (`Payroll.PeriodOverlaps`). `GET /api/payroll/pay-periods?planId=&status=`.

## 3. Novedades

| Método | Ruta | Comando / Query | Permiso |
|---|---|---|---|
| GET | `/api/payroll/pay-periods/{periodId}/novelties?employeeId&conceptCode&status&origin` | `ListNoveltiesQuery` | `Payroll.Novelties.View` |
| POST | `/api/payroll/pay-periods/{periodId}/novelties` | `RegisterNoveltyCommand` | `Payroll.Novelties.Create` |
| PUT | `/api/payroll/novelties/{noveltyId}` | `CorrectNoveltyCommand` (crea versión nueva) | `Payroll.Novelties.Update` |
| POST | `/api/payroll/novelties/{noveltyId}/cancel` | `CancelNoveltyCommand { reason }` | `Payroll.Novelties.Cancel` |
| GET | `/api/payroll/novelties/{noveltyId}/history` | `GetNoveltyHistoryQuery` | `Payroll.Novelties.View` |
| POST | `/api/payroll/employees/{employeeId}/salary-changes` | `RegisterSalaryChangeCommand { newSalary, effectiveFrom, reason }` | `Payroll.Novelties.Create` |
| GET | `/api/payroll/employees/{employeeId}/salary-changes` | `ListSalaryChangesQuery` | `Payroll.Novelties.View` |
| GET | `/api/payroll/novelties/import-template` | archivo CSV de plantilla | `Payroll.Novelties.Import` |
| POST | `/api/payroll/pay-periods/{periodId}/novelties/import` (multipart `file`) | `ImportNoveltiesCommand` | `Payroll.Novelties.Import` |
| GET | `/api/payroll/recurring-novelties?employeeId&active` | `ListRecurringNoveltiesQuery` | `Payroll.Novelties.View` |
| POST | `/api/payroll/recurring-novelties` | `CreateRecurringNoveltyCommand` | `Payroll.Novelties.Create` |
| POST | `/api/payroll/recurring-novelties/{id}/deactivate` | `DeactivateRecurringNoveltyCommand { reason }` | `Payroll.Novelties.Cancel` |

```jsonc
// RegisterNoveltyCommand (body)
{ "employeePublicId": "…", "conceptCode": "HEX_NOCTURNA", "quantity": 6, "amount": null,
  "startDate": null, "endDate": null, "notes": "Turno del 12" }

// NoveltyDto
{ "publicId": "…", "periodPublicId": "…", "employeePublicId": "…", "employeeName": "…",
  "conceptCode": "HEX_NOCTURNA", "conceptName": "Hora extra nocturna", "nature": "Earning",
  "quantity": 6, "amount": null, "startDate": null, "endDate": null,
  "daysInPeriod": 0, "carryOverDays": 0, "estimatedAmount": 31250.00,
  "status": "Active", "statusReason": null, "origin": "Manual",
  "installmentNumber": null, "installmentTotal": null,
  "createdAt": "…", "createdBy": "…", "supersedesPublicId": null }

// ImportNoveltiesResultDto
{ "applied": 18, "batchId": "…", "errors": [ { "row": 7, "column": "cantidad", "message": "…" } ] }
// Con errores: HTTP 422, applied = 0, ningún cambio persistido.
```

Errores: `Payroll.PeriodNotOpen`, `Payroll.PeriodApproved` (ofrece
`retroactiveTargetPeriodPublicId` en el envelope), `Payroll.EmployeeNotActiveInPeriod`,
`Payroll.EmployeeNotInPlan`, `Payroll.ConceptNotFound`, `Payroll.ConceptNotApplicable`
(clase de empleado), `Payroll.ConceptRequiresDates|Quantity|Amount`,
`Payroll.NoveltyDuplicate` (incluye `existingPublicId`), `Payroll.NoveltyOverMax`,
`Payroll.NoveltyNotActive`, `Payroll.ReasonRequired`, `Payroll.ImportInvalid`,
`Payroll.ImportFileTooLarge` (> 5 MB), `Payroll.SalaryChangeInApprovedPeriod`.

## 4. Liquidación (corridas)

| Método | Ruta | Comando / Query | Permiso |
|---|---|---|---|
| POST | `/api/payroll/pay-periods/{periodId}/runs` | `CalculatePayrollRunCommand` | `Payroll.Runs.Calculate` |
| GET | `/api/payroll/pay-periods/{periodId}/runs/current` | `GetCurrentRunQuery` | `Payroll.Runs.View` |
| GET | `/api/payroll/pay-periods/{periodId}/runs` | `ListRunsQuery` (historial de versiones) | `Payroll.Runs.View` |
| GET | `/api/payroll/runs/{runId}` | `GetRunSummaryQuery` (totales por concepto y bloqueos) | `Payroll.Runs.View` |
| GET | `/api/payroll/runs/{runId}/employees?flag&changed` | `ListRunEmployeesQuery` | `Payroll.Runs.View` |
| GET | `/api/payroll/runs/{runId}/employees/{employeeId}` | `GetRunEmployeeDetailQuery` (líneas + explicación) | `Payroll.Runs.View` |
| GET | `/api/payroll/runs/{runId}/comparison` | `GetRunComparisonQuery` (contra el período anterior aprobado) | `Payroll.Runs.View` |
| GET | `/api/payroll/runs/{runId}/balance-check` | `GetRunBalanceCheckQuery` | `Payroll.Runs.View` |
| POST | `/api/payroll/runs/{runId}/approve` | `ApprovePayrollRunCommand { confirm: true, exceptions: [{ employeePublicId, flag, reason }] }` | `Payroll.Runs.Approve` (+ `Payroll.Runs.AuthorizeException` si hay excepciones) |
| POST | `/api/payroll/runs/{runId}/reverse` | `ReversePayrollRunCommand { reason }` | `Payroll.Runs.Reverse` |
| GET | `/api/payroll/runs/{runId}/export` | CSV de líneas con explicación | `Payroll.Runs.Export` |

```jsonc
// RunSummaryDto
{ "publicId": "…", "periodPublicId": "…", "version": 2, "status": "Draft",
  "calculatedAt": "…", "calculatedBy": "…", "approvedAt": null, "approvedBy": null,
  "employeeCount": 47, "totals": { "earnings": 0, "deductions": 0, "employerContributions": 0,
  "provisions": 0, "net": 0, "roundingAdjustment": 0 },
  "byConcept": [ { "code": "SALARIO", "name": "…", "nature": "Earning", "employees": 47, "amount": 0 } ],
  "blockers": [ { "employeePublicId": "…", "employeeName": "…", "flag": "NegativeNet", "detail": "…" } ],
  "changedEmployees": 3, "inputsHash": "…", "accountingDocumentPublicId": null }

// RunEmployeeDetailDto
{ "employeePublicId": "…", "employeeName": "…", "document": "…", "employeeClass": "Standard",
  "daysWorked": 21, "salaryTranches": [ { "from": "2026-09-10", "to": "2026-09-30", "days": 21, "salary": 2000000 } ],
  "lines": [ { "conceptCode": "SALARIO", "conceptName": "Salario básico", "nature": "Earning",
               "quantity": 21, "base": 2000000, "factor": null, "amount": 1400000,
               "affectsAccounting": true, "noveltyPublicId": null,
               "explanation": { "form": "QuantityTimesUnit", "steps": [
                  { "label": "Salario vigente (tramo 10–30 sep)", "value": "2.000.000" },
                  { "label": "Valor día = salario / 30", "value": "66.666,67" },
                  { "label": "Días liquidados", "value": "21" },
                  { "label": "Resultado", "value": "1.400.000" } ] } } ],
  "totals": { "earnings": 0, "deductions": 0, "employerContributions": 0, "provisions": 0, "net": 0 },
  "flags": [] }

// ComparisonDto
{ "previousPeriodPublicId": "…", "thresholdPercent": 10,
  "rows": [ { "employeePublicId": "…", "employeeName": "…", "previousNet": 0, "currentNet": 0,
              "variationPercent": 0, "overThreshold": false, "newEmployee": false, "leftEmployee": false } ] }

// BalanceCheckDto
{ "earningsMinusDeductionsEqualsNet": true, "employerAndProvisionsOutsideNet": true,
  "accountingDocumentBalanced": null, "details": [ … ] }
```

Errores: `Payroll.PeriodNotOpen`, `Payroll.PeriodApproved`, `Payroll.RunInProgress`
(lock), `Payroll.NoEmployeesInPlan` (200 con `employeeCount = 0`, no error; aprobar
un período vacío exige `confirmEmpty: true`), `Payroll.LegalParameterMissing`
(`details: [{ code, requiredAt }]`), `Payroll.RunNotFound`, `Payroll.RunStale`,
`Payroll.RunNotDraft`, `Payroll.ApprovalBlocked` (`blockers[]`),
`Payroll.ExceptionNotAuthorized`, `Payroll.ConceptWithoutAccounts`
(`concepts[]`, `employees[]`), `Payroll.AccountingPeriodClosed`,
`Payroll.VoucherTypeMissing` (`NM`), `Payroll.SegregationOfDuties`,
`Payroll.ConfirmationRequired`, `Payroll.RunNotApproved`,
`Payroll.PaymentBlocksReversal` (`payments[]`), `Payroll.AccountingPeriodClosedForReversal`.

## 5. Relación de pago y comprobantes

| Método | Ruta | Comando / Query | Permiso |
|---|---|---|---|
| GET | `/api/payroll/runs/{runId}/payments` | `GetPaymentRegisterQuery` (neto, banco, cuenta, estado de pago) | `Payroll.Payments.View` |
| POST | `/api/payroll/runs/{runId}/payments` | `MarkPaymentsCommand { employeePublicIds: [] \| null (=todos), paidAt, method, reference }` | `Payroll.Payments.Mark` |
| POST | `/api/payroll/runs/{runId}/payments/{employeeId}/revert` | `RevertPaymentMarkCommand { reason }` | `Payroll.Payments.Unmark` |
| GET | `/api/payroll/runs/{runId}/payslips/{employeeId}/pdf` | `GetPayslipQuery` → `IPayslipPdfRenderer` | `Payroll.Payslips.View` |
| GET | `/api/payroll/runs/{runId}/payslips/pdf` | todos en un PDF | `Payroll.Payslips.View` |
| POST | `/api/payroll/runs/{runId}/payslips/send` | `SendPayslipsCommand { employeePublicIds: [] \| null }` | `Payroll.Payslips.Send` |
| GET | `/api/payroll/runs/{runId}/payslips/deliveries` | `ListPayslipDeliveriesQuery` | `Payroll.Payslips.View` |

```jsonc
// SendPayslipsResultDto
{ "sent": 44, "failed": 1, "withoutEmail": [ { "employeePublicId": "…", "employeeName": "…" } ],
  "failures": [ { "employeePublicId": "…", "error": "…" } ] }
```

Errores: `Payroll.RunNotApproved`, `Payroll.PaymentAlreadyMarked`,
`Payroll.PaymentNotFound`, `Payroll.EmailNotConfigured` (el ambiente no tiene correo
saliente: se informa antes de intentar), `Payroll.ReasonRequired`.

## 6. Conceptos y parámetros

| Método | Ruta | Comando / Query | Permiso |
|---|---|---|---|
| GET | `/api/payroll/concept-definitions?asOf&includeInactive` | `ListConceptDefinitionsQuery` (versión vigente por código) | `Payroll.Concepts.View` |
| GET | `/api/payroll/concept-definitions/{code}/versions` | `ListConceptVersionsQuery` | `Payroll.Concepts.View` |
| POST | `/api/payroll/concept-definitions` | `CreateConceptDefinitionCommand` | `Payroll.Concepts.Manage` |
| PUT | `/api/payroll/concept-definitions/{code}` | `ReviseConceptDefinitionCommand { …, validFrom }` (nueva versión) | `Payroll.Concepts.Manage` |
| POST | `/api/payroll/concept-definitions/{code}/deactivate` | `DeactivateConceptDefinitionCommand { validTo }` | `Payroll.Concepts.Manage` |
| PUT | `/api/payroll/concept-definitions/{code}/accounts` | `SetConceptAccountsCommand { rows: [{ costCenterPublicId?, debitAccountPublicId, creditAccountPublicId }] }` | `Payroll.Concepts.Manage` |
| POST | `/api/payroll/concept-definitions/dry-run` | `DryRunConceptQuery { definition, employeePublicId, periodPublicId }` | `Payroll.Concepts.View` |
| POST | `/api/payroll/concept-definitions/seed` | `ReapplyConceptSeedCommand` | `Payroll.Concepts.Manage` |
| GET | `/api/payroll/concept-definitions/legacy` | `ListLegacyConceptsQuery` (sólo lectura) | `Payroll.Concepts.View` |
| GET | `/api/payroll/legal-parameters?asOf` | `ListLegalParametersQuery` | `Payroll.LegalParameters.View` |
| GET | `/api/payroll/legal-parameters/{code}/versions` | `ListLegalParameterVersionsQuery` | `Payroll.LegalParameters.View` |
| POST | `/api/payroll/legal-parameters/{code}/versions` | `AddLegalParameterVersionCommand { validFrom, value?, ranges?[], source }` | `Payroll.LegalParameters.Manage` |
| GET | `/api/payroll/employees/{employeeId}/withholding` | `GetEmployeeWithholdingQuery` | `Payroll.Employees.Read` |
| PUT | `/api/payroll/employees/{employeeId}/withholding` | `SetEmployeeWithholdingCommand { procedure, rates[], deductions[] }` | `Payroll.Employees.Update` |

Errores: `Payroll.ConceptCodeDuplicate`, `Payroll.ConceptCycle` (`cycle: ["A","B","A"]`),
`Payroll.ConceptReferenceNotFound`, `Payroll.ConceptFormIncomplete` (campo que falta
para la forma), `Payroll.ConceptSeedProtected` (borrar o cambiar `Code` de semilla),
`Payroll.LegalParameterOverlap`, `Payroll.RangeTableInvalid` (huecos o solapes),
`Payroll.WithholdingRateOverlap`.

## 7. Retirados

`POST /api/payroll/process/{periodId}` y `POST /api/payroll/entries` desaparecen (D-14).
`GET /api/payroll/summary|detail|payslip/…` iban a quedar como alias 308 durante una
version. Al implementar (T136, 2026-09-05) se buscaron consumidores en `SHR/` y MAUI y no
hubo ninguno —las pantallas heredadas eran marcadores—, asi que los alias se retiraron en
esta misma version: `PayrollProcessingEndpoints.cs` ya no existe. Las rutas heredadas de
reportes (`/api/reports/payroll/payslip/{employeeId}/{periodId}/pdf` y `/summary/{periodId}/pdf`)
si redirigen 308 a la corrida vigente del periodo, porque el comprobante de pago se emite
por corrida (seccion 5), no por periodo.
