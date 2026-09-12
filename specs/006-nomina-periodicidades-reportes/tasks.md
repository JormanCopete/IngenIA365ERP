# Tasks: Nómina — periodicidades, recurrencia, descarte y reportes

**Input**: [spec.md](spec.md), [plan.md](plan.md)

## Phase 1: Modelo y migración (bloqueante)

- [x] T001 Ampliar `PayrollPeriodicity` con `TenDay = 10` y `Weekly = 7` en src/Core/IngenIA365ERP.Domain/Enums/Payroll/PayrollPeriodicity.cs
- [x] T002 [P] Crear `RecurringApplyRule` {EveryPeriod, FirstOfMonth, LastOfMonth} en src/Core/IngenIA365ERP.Domain/Enums/Payroll/RecurringApplyRule.cs
- [x] T003 [P] Agregar `SubPeriodNumber`, `ImputationYear`, `ImputationMonth` a src/Core/IngenIA365ERP.Domain/Entities/Payroll/PayPeriod.cs
- [x] T004 [P] Agregar `ApplyOn` a src/Core/IngenIA365ERP.Domain/Entities/Payroll/PayrollRecurringNovelty.cs
- [x] T005 [P] Agregar `DiscardedAt/By/Reason` a src/Core/IngenIA365ERP.Domain/Entities/Payroll/Transactions/PayrollRun.cs
- [x] T006 `PeriodCalendar` (proponer, último del mes, validar duración) en src/Core/IngenIA365ERP.Domain/Payroll/Calculation/PeriodCalendar.cs con pruebas en tests/IngenIA365ERP.Domain.Tests/Payroll/PeriodCalendarTests.cs
- [x] T007 Migración en par `PeriodicidadesReglasYDescarte` con `tools/scripts/add-migration.ps1`; SQL de datos para `ImputationYear/Month` desde `StartDate`; header explicativo

## Phase 2: US1 — Descartar borrador

- [x] T010 [US1] `DiscardPayrollRunCommand` (+validador, handler, auditoría) en src/Core/IngenIA365ERP.Application/Payroll/Runs/DiscardPayrollRun/
- [x] T011 [US1] `POST /api/payroll/runs/{runId}/discard` con permiso `Payroll.Runs.Calculate` en src/Presentation/IngenIA365ERP.API/Endpoints/Payroll/PayrollRunsEndpoints.cs
- [x] T012 [US1] Botón y diálogo «Descartar borrador» en src/Presentation/IngenIA365ERP.Shared/Pages/Nomina/Liquidacion.razor; versiones muestran motivo
- [x] T013 [US1] Pruebas: tests/IngenIA365ERP.Application.Tests/Payroll/Runs/DiscardPayrollRunTests.cs (período vuelve a Open, recurrentes anuladas, manuales intactas, Aprobado rechazado, motivo obligatorio)

## Phase 3: US3 + US2 — Sub-período/mes y «Aplica en»

- [x] T020 [US3] Crear/editar período con sub-período y mes (propuesta desde `PeriodCalendar`, validación de duración) en src/Core/IngenIA365ERP.Application/Payroll/PayPeriods/
- [x] T021 [US3] DTO de período con `SubPeriodNumber`, `ImputationYear/Month`, `EtiquetaSubPeriodo`; pantalla src/Presentation/IngenIA365ERP.Shared/Pages/Nomina/PeriodosPago.razor (campos propuestos y editables, columnas, filtro por mes)
- [x] T022 [US2] `ApplyOn` en `CreateRecurringNoveltyCommand`, DTO y `RecurringNoveltiesMaterializer` (regla contra `PeriodCalendar.EsUltimoDelMes`) en src/Core/IngenIA365ERP.Application/Payroll/Novelties/RecurringNovelties/RecurringNovelties.cs
- [x] T023 [US2] Pantalla de recurrentes: selector «Aplica en» con explicación y columna en el grid en src/Presentation/IngenIA365ERP.Shared/Pages/Nomina/Novedades.razor
- [x] T024 [US2] Pruebas: tests/IngenIA365ERP.Application.Tests/Payroll/Novelties/RecurringApplyRuleTests.cs (quincenal primero/último/cada, mensual indiferente, semanal 5 semanas)

## Phase 4: US4 — Periodicidades decadal y semanal

- [x] T030 [US4] Validadores y textos de periodicidad (planes, períodos, empleados, liquidación) en Application y Shared
- [x] T031 [US4] Casos dorados `09-semanal-mes-entero.json` y `10-decadal-tercera-decada.json` en tests/IngenIA365ERP.Domain.Tests/Payroll/Calculation/Casos/ (valores calculados a mano en el propio JSON)
- [x] T032 [US4] Validación de duración al crear período (`Payroll.PeriodLengthMismatch`) con las excepciones de calendario

## Phase 5: US5 — Centro de reportes

- [x] T040 [US5] Modelo `TablaExportable` y cinco consultas en src/Core/IngenIA365ERP.Application/Payroll/Reports/
- [x] T041 [US5] Paquetes `ClosedXML` y `DocumentFormat.OpenXml` en la API; exportadores xlsx/docx/pdf en src/Presentation/IngenIA365ERP.API/Reports/Exportadores/
- [x] T042 [US5] Endpoints `GET /api/reports/payroll/{vista}` con `format=json|xlsx|pdf|docx` en src/Presentation/IngenIA365ERP.API/Endpoints/Reports/PayrollReportsEndpoints.cs
- [x] T043 [US5] Pantalla `ReportesNomina.razor` (`/reportes/nomina`) con las cinco vistas y botones de exportación; `ComprobanteNomina.razor` redirige; menú actualizado
- [x] T044 [US5] e2e tests/IngenIA365ERP.API.IntegrationTests/Payroll/ReportesNominaTests.cs: cada vista en json y los tres formatos descargan; totales iguales a la corrida

## Phase 6: Cierre

- [x] T050 Docs: `docs/operaciones/nomina-primer-periodo.md` (periodicidades, sub-períodos, descartar), `CLAUDE.md`, contratos en specs/006/contracts/api.md
- [ ] T051 Suites en verde (Domain, Application, Architecture, Shared, e2e nómina); merge a `develop`
