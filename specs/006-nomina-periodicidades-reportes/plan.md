# Implementation Plan: Nómina — periodicidades, recurrencia, descarte y reportes

**Branch**: `006-nomina-periodicidades-reportes` | **Date**: 2026-09-12 | **Spec**: [spec.md](spec.md)

## Summary

Cuatro entregas independientes sobre la feature 005, en este orden porque cada una destraba
la siguiente y ninguna rompe lo existente: (1) descartar borrador; (2) sub-período y mes en el
período + regla «Aplica en» de las recurrentes; (3) periodicidades decadal y semanal en el
plan y el motor; (4) centro de reportes con exportación a Excel, PDF y Word.

## Technical Context

**Language**: C# / .NET 10 · **Frameworks**: Carter, MediatR, FluentValidation, EF Core
(PostgreSQL y SQL Server en par), Blazor WASM + SyncFusion 33.1.44 · **Reportes**: QuestPDF
(existe) + `ClosedXML` (Excel) + `DocumentFormat.OpenXml` (Word), sólo en el proyecto API
(Presentation), nunca en Application. **Pruebas**: xUnit + FluentAssertions; casos dorados
JSON en Domain.Tests; e2e por HTTP con Testcontainers.

## Constitution Check

- I Spec-first: este documento. II Clean Architecture: el motor sigue en Domain sin
  dependencias; los exportadores viven en API/Reports. IV Multi-tenancy: todo por la base de
  la cooperativa activa. VI PublicId en la API. IX sin `return Success` sin hacer nada. XI
  inmutabilidad: descartar no borra la corrida ni sus líneas. XII migraciones en par con
  header; ninguna destructiva (sólo columnas nuevas con default).
- `LaNominaNoTieneValoresLegalesFijos`: 7 y 10 entran como valores del enum
  `PayrollPeriodicity` (Domain/Enums), fuera de `Domain/Payroll`; la prueba admite los
  literales ya listados y el motor sólo usa `DaysInPeriod`.

## Project Structure

```text
src/Core/IngenIA365ERP.Domain/
├── Enums/Payroll/PayrollPeriodicity.cs        (+TenDay, +Weekly)
├── Enums/Payroll/RecurringApplyRule.cs         (nuevo)
├── Entities/Payroll/PayPeriod.cs               (+SubPeriodNumber, +ImputationYear/Month)
├── Entities/Payroll/PayrollRecurringNovelty.cs (+ApplyOn)
├── Entities/Payroll/Transactions/PayrollRun.cs (+DiscardedAt/By/Reason)
└── Payroll/Calculation/PeriodCalendar.cs       (nuevo: propone sub-período/mes, último del mes, valida duración)
src/Core/IngenIA365ERP.Application/Payroll/
├── Runs/DiscardPayrollRun/                     (nuevo comando)
├── PayPeriods/                                 (crear/editar con sub-período y mes; validación de duración)
├── Novelties/RecurringNovelties/               (ApplyOn en comando, DTO y materializador)
├── Plans/                                      (enum ampliado en validadores)
└── Reports/                                    (nuevo: cinco consultas que devuelven modelos tabulares)
src/Presentation/IngenIA365ERP.API/
├── Endpoints/Payroll/PayrollRunsEndpoints.cs   (POST /runs/{id}/discard)
├── Endpoints/Reports/PayrollReportsEndpoints.cs (GET .../payroll/{vista}?format=)
└── Reports/Exportadores/                       (TablaExportable → xlsx/docx/pdf)
src/Presentation/IngenIA365ERP.Shared/Pages/
├── Nomina/Liquidacion.razor                    (Descartar borrador)
├── Nomina/PeriodosPago.razor                   (sub-período y mes)
├── Nomina/Novedades.razor                      (Aplica en)
├── Nomina/PlanesNomina.razor + Empleados.razor (periodicidades nuevas)
└── Reportes/ReportesNomina.razor               (reemplaza ComprobanteNomina.razor)
tests/
├── Domain.Tests/Payroll/Calculation/Casos/09-semanal-*.json, 10-decadal-*.json
├── Domain.Tests/Payroll/PeriodCalendarTests.cs
├── Application.Tests/Payroll/Runs/DiscardPayrollRunTests.cs
├── Application.Tests/Payroll/Novelties/RecurringApplyRuleTests.cs
└── API.IntegrationTests/Payroll/ReportesNominaTests.cs
```

## Design Decisions

- **Descartar** reutiliza el estado `Superseded` (no se agrega un estado nuevo al enum de
  corrida para no tocar filtros y reportes existentes) y guarda motivo/autor/fecha en
  columnas propias. El período vuelve a `Open`; las novedades `Recurring` del período se
  anulan con `CancelReason = "Borrador descartado"` para que la próxima materialización las
  regenere (la cuota nunca se contó).
- **Sub-período**: `PeriodCalendar.Proponer(periodicidad, inicio, fin)` devuelve
  (subPeriodo, año, mes) — quincena por día de inicio (≤15 → 1), década por inicio
  (≤10, ≤20, resto), semana por `ceil(díaInicio / 7)` acotada a 5, mes = mes del inicio;
  `EsUltimoDelMes(periodicidad, subPeriodo, periodosDelMes)`: 1 mensual, 2 quincenal, 3
  decadal, y en semanal el mayor `SubPeriodNumber` de los períodos ya creados para ese
  mes/año o 5 si ninguno.
- **Duración**: `PeriodCalendar.ValidarDuracion` admite exactamente `DaysInPeriod` días
  (fin − inicio + 1) o, si el período termina en el último día del mes, entre
  `DaysInPeriod − 2` y `DaysInPeriod + 1` (28–31 para mensual, 13–16 quincenal, 8–11
  decadal). Semanal: exactamente 7.
- **Motor**: no cambia una línea de regla; `PeriodInput.DaysInPeriod` ya viene del plan.
  Se agregan casos dorados y se revisa que la retención mensualice con `30/DaysInPeriod`
  (ya lo hace `WithholdingBaseBuilder`).
- **Exportación**: un modelo `TablaExportable(Titulo, Subtitulo, Columnas, Filas, Totales)`
  producido por cada consulta de reporte; tres exportadores (`ExportadorExcel`,
  `ExportadorWord`, `ExportadorPdf`) lo convierten. El comprobante por empleado reutiliza el
  PDF existente (`PayslipReport`) y para xlsx/docx pasa por la tabla.
- **Menú**: «Comprobante Nómina» → «Reportes de nómina» (`/reportes/nomina`);
  `/reportes/comprobante-nomina` redirige.

## Migrations

Una migración en par `PeriodicidadesReglasYDescarte`: columnas nuevas con default
(`SubPeriodNumber` 1, `ImputationYear/Month` desde `StartDate` por SQL de datos,
`ApplyOn` 0, `DiscardedAt/By/Reason` nulos). Ninguna destructiva.
