# Contratos de API — feature 006

Todas las rutas van bajo la cooperativa activa (Bearer con `active_tenant_id`) y devuelven el
sobre de error de siempre (`{code, errorCode, message, traceId}`).

## Descartar borrador

`POST /api/payroll/runs/{runId}/discard` · permiso `Payroll.Runs.Calculate`

Body: `{ "reason": "texto ≤ 300" }` → `200 { runPublicId, periodPublicId, recurrentesAnuladas }`.

Errores: `Payroll.RunNotFound`, `Payroll.RunNotDraft` (sólo un borrador se descarta; una
aprobada se reversa), `Payroll.ReasonRequired`, `Payroll.RunInProgress`.

## Períodos de pago

`POST /api/payroll/pay-periods` y `PUT /api/payroll/pay-periods/{id}` aceptan además
`subPeriodNumber` (1..N según la periodicidad), `imputationYear`, `imputationMonth`; si faltan,
se proponen desde la fecha de inicio. Nuevos errores: `Payroll.PeriodLengthMismatch`,
`Payroll.SubPeriodOutOfRange`, `Payroll.ImputationMonthInvalid`. El DTO devuelve
`subPeriodNumber`, `imputationYear`, `imputationMonth`, `subPeriodLabel` («Quincena 2»).

## Recurrentes

`POST /api/payroll/recurring-novelties` acepta `applyOn` ∈ `EveryPeriod` (por defecto),
`FirstOfMonth`, `LastOfMonth`. El DTO devuelve `applyOn`.

## Planes

`Periodicity` admite `Monthly`, `Biweekly`, `TenDay`, `Weekly`. `PUT /api/payroll/plans/{id}`
acepta `periodicity` mientras el plan no tenga períodos ni liquidaciones
(`Payroll.PlanPeriodicityLocked`).

## Reportes de nómina

`GET /api/reports/payroll/{vista}?...&format=json|xlsx|pdf|docx` · permiso `Payroll.Runs.View`

| vista | parámetros |
|---|---|
| `comprobante` | `runId`, `employeeId` |
| `resumen` | `runId` |
| `detalle` | `runId` |
| `novedades` | `periodId`, `incluirAnuladas` (true) |
| `historico` | `employeeId`, `desde`, `hasta` |

`json` devuelve `TablaExportable { titulo, subtitulo, columnas[{nombre, tipo, clave}],
filas[{valores[], seccion, resaltada}], totales, notas[] }`; los demás formatos devuelven el
archivo (`Content-Disposition` con nombre). `Reportes.FormatoInvalido` para otro formato.
