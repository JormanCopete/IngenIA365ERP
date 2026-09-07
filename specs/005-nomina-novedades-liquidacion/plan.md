# Implementation Plan: Novedades y Liquidación Periódica de Nómina

**Branch**: `005-nomina-novedades-liquidacion` | **Date**: 2026-09-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-nomina-novedades-liquidacion/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Reemplazar las dos pantallas vacías de **Nómina → Novedades** y **Nómina →
Liquidación**, y el cálculo preliminar con porcentajes fijos que hay detrás, por un
proceso completo y parametrizable: novedades por período con historial, cálculo de
toda la nómina de un plan mediante un **motor puro en Domain** que aplica cinco formas
de cálculo predefinidas sobre conceptos y parámetros legales **con vigencia**,
borrador revisable con explicación línea a línea, aprobación que en la misma
transacción genera el comprobante contable, relación de pago con marca de pagado,
comprobantes en PDF con envío manual, y reversión controlada. Cada cálculo crea una
corrida nueva (las líneas son transacciones inmutables, Principio XI); la primera
nómina arranca con una semilla curada de conceptos y parámetros 2026 (`IDataSeeder`),
y el catálogo heredado queda de consulta.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0.5 (SDK anclado 10.0.302).

**Primary Dependencies**: Carter (Minimal APIs), MediatR + FluentValidation (CQRS,
behaviors `Validation/Audit/Logging/Performance`), EF Core 10 multi-proveedor
(Npgsql + SqlClient, migraciones en par), Blazor InteractiveWebAssembly (Web +
Web.Client) con SyncFusion 33.1.44, QuestPDF (comprobante, `API/Reports/PayslipReport`),
CsvHelper (importación, en `Storage`), Redis (`IDistributedLock`), MongoDB
(`IAuditAppendOnlyWriter`), MailKit vía `IEmailSender` (adjuntos: extensión de
`EmailMessage`).

**Storage**: PostgreSQL (SaaS) / SQL Server (on-premise), **una base por
cooperativa**; tablas nuevas `PAY_*` (ver [data-model.md](./data-model.md)); auditoría
en la base Mongo de la cooperativa; lock de cálculo en la ranura Redis de la
cooperativa.

**Testing**: xUnit + FluentAssertions + NSubstitute. Casos dorados del motor en
`Domain.Tests` (JSON legibles por la contadora), handlers y validadores en
`Application.Tests` (InMemory `TestApplicationDbContext`), recorrido HTTP en
`API.IntegrationTests` (Testcontainers), y `Architecture.Tests` (VIII ampliado, XI
vigente, nuevo «sin valores legales fijos en Domain/Payroll»).

**Target Platform**: Kubernetes (k3s) tras Cloudflare; navegador (WASM) para las
pantallas; MAUI comparte `Shared` sin trabajo adicional en esta feature.

**Project Type**: web-service + web-app (API Carter + Blazor), monorepo existente.

**Performance Goals**: calcular un período de 200 empleados en < 60 s (SC-004); el
motor en memoria con insumos cargados en ≤ 6 consultas y un solo `SaveChanges`
(~10 000 filas). Pantallas de borrador paginadas por empleado.

**Constraints**: ningún porcentaje ni tope legal en código (FR-010, verificado por
prueba de arquitectura); repetibilidad byte a byte (`InputsHash`); cálculo atómico
(una transacción) y exclusivo (lock + índice único); líneas de liquidación nunca se
borran ni actualizan; aprobación y asiento en la misma unidad de trabajo; segregación
de funciones por defecto; correo del empleado sólo desde `COR_People`.

**Scale/Scope**: 10–200 empleados por cooperativa, 1–3 planes, 12–24 períodos por
año; ≈ 40 líneas por empleado y período. 5 pantallas, ≈ 45 endpoints, 15 entidades
(10 nuevas + 2 extendidas + 3 de apoyo), 4 migraciones en par, 3 seeders, 22
permisos.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | Constitution → spec (clarificada, 7 decisiones) → este plan → tasks. Toca cinco capas y el modelo de datos: la secuencia es obligatoria y se cumple. |
| II   | Clean Architecture | PASS | Motor de cálculo en **Domain** sin dependencias; Application sólo referencia Domain y abstracciones (`IDistributedLock`, `IEmailSender`, `INoveltyFileParser`, `IPayslipPdfRenderer`); CsvHelper vive en Storage; QuestPDF sigue en API (raíz de composición) implementando la interfaz de Application. Presentation no toca Persistence. |
| III  | CQRS + MediatR | PASS | Todo es Command/Query; los endpoints Carter sólo reenvían. La contabilización no envía otro comando: replica el patrón dentro del mismo handler para una sola transacción (D-07). Cada Command lleva validador hermano (namespaces añadidos a `PrincipioVIII_DualValidation`). |
| IV   | Multi-tenancy | PASS | Todas las tablas en la base de la cooperativa; ninguna consulta cruzada; el lock usa la ranura Redis de la cooperativa; el seeder corre por cooperativa vía `SeedOrchestrator`; ninguna ruta de nómina se exime en `TenantResolutionMiddleware`. |
| V    | Person centralizada | PASS | Nombre, documento y correo del empleado salen de `COR_People`; `PAY_Employees` sólo gana columnas laborales (plan, procedimiento de retención, clase). |
| VI   | PublicId externo | PASS | Rutas y DTOs sólo con `PublicId`; los `int Id` se resuelven en handlers. Las referencias internas entre corrida, línea y novedad usan `Id`; hacia afuera, `PublicId`. |
| VII  | Soft-delete + auditoría | PASS | Toda entidad nueva hereda `AuditableEntity` con `HasQueryFilter`; novedades se sustituyen o anulan, nunca se borran; corridas se marcan `Superseded`. |
| VIII | Validación dual | PASS | `DataAnnotations` en los modelos de las pantallas + FluentValidation en cada Command; mensajes en español con código `Payroll.*`. |
| IX   | Errores visibles | PASS | Ningún `catch` vacío; fallos de correo se registran en `PAY_PayslipDeliveries` y se muestran; fallo de asiento aborta la aprobación con su causa. |
| X    | Trazabilidad SIPLA/SARLAFT | PASS | `AuditBehavior` para todos los comandos + `IAuditAppendOnlyWriter` explícito con `EntityPublicId` y antes/después en aprobación, reversión, pagos, envíos, cambios de salario y retención (Principio X exige la traza de cambios de salario). |
| XI   | Inmutabilidad contable | PASS | `PayrollRun`, `PayrollRunEmployee` y `PayrollRunLine` en `Entities/Payroll/Transactions` (protegidas por el test); corrección = corrida nueva o asiento reverso con referencia; `PAY_PayrollTransactions` legado no se toca. |
| XII  | Migraciones idempotentes y reversibles | PASS | Cuatro migraciones EF en par (DDL), reversibles, sin datos; la semilla va por `IDataSeeder` idempotente; el relleno del plan por defecto en filas existentes se hace en la migración con `UPDATE … WHERE PayrollPlanId IS NULL` (idempotente) y se documenta. Nada destructivo. |

**Resultado del gate**: 12/12 PASS. Sin entradas en Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/005-nomina-novedades-liquidacion/
├── plan.md              # este archivo
├── spec.md              # especificación clarificada (7 decisiones)
├── research.md          # Phase 0: 14 hallazgos verificados + 16 decisiones
├── data-model.md        # Phase 1: 15 entidades, estados, migraciones
├── quickstart.md        # Phase 1: levantar, sembrar, recorrer, probar
├── contracts/
│   ├── api.md           # ≈45 endpoints, DTOs, códigos de error
│   ├── permissions.md   # 22 permisos, roles integrados, segregación
│   └── ui.md            # 5 pantallas, componentes, menú
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root — layout existente; lo nuevo marcado con +)

```text
src/Core/IngenIA365ERP.Domain/
├── Entities/Payroll/
│   ├── + PayrollPlan.cs, PayrollConceptDefinition.cs, PayrollConceptDefinitionAccount.cs
│   ├── + PayrollLegalParameter.cs, PayrollLegalParameterRange.cs
│   ├── + PayrollNovelty.cs, PayrollRecurringNovelty.cs
│   ├── + EmployeeWithholdingRate.cs, EmployeeTaxDeduction.cs
│   ├── + PayrollPayment.cs, PayslipDelivery.cs
│   ├── ~ Employee.cs, PayPeriod.cs (columnas nuevas)
│   └── + Transactions/ PayrollRun.cs, PayrollRunEmployee.cs, PayrollRunLine.cs
├── Enums/Payroll/ + PayrollPeriodicity, PayPeriodStatus, EmployeeClass, ConceptNature,
│                   CalculationKind, CalculationBase, UnitKind, ConceptOrigin,
│                   NoveltyStatus, NoveltyOrigin, PayrollRunStatus, RunEmployeeFlag, …
└── + Payroll/Calculation/           # motor puro
    ├── PayrollCalculationEngine.cs, CalculationInput.cs, CalculationResult.cs
    ├── Rules/ ICalculationRule, FixedAmountRule, PercentOfBaseRule,
    │          QuantityTimesUnitRule, RangeTableRule, CompositeOfConceptsRule
    ├── Bases/ BaseBuilder.cs, WithholdingBaseBuilder.cs, SalaryTranches.cs
    ├── Explanation.cs, LegalParameterCodes.cs, ConceptDependencyGraph.cs
    └── InputsHasher.cs

src/Core/IngenIA365ERP.Application/Payroll/
├── + Plans/        (Commands: Create, Update, ChangeEmployeePlan · Queries: List)
├── + Novelties/    (Register, Correct, Cancel, RegisterSalaryChange, Import,
│                    CreateRecurring, DeactivateRecurring · Queries: List, History, Template)
├── + Runs/         (Calculate, Approve, Reverse · Queries: Current, List, Summary,
│                    Employees, EmployeeDetail, Comparison, BalanceCheck, Export)
├── + Payments/     (MarkPayments, RevertPaymentMark · Query: Register)
├── + Payslips/     (SendPayslips · Queries: Payslip, Deliveries) + IPayslipEmailDispatcher,
│                    IPayslipPdfRenderer
├── + Concepts/     (Create, Revise, Deactivate, SetAccounts, ReapplySeed · Queries:
│                    List, Versions, DryRun, Legacy)
├── + LegalParameters/ (AddVersion · Queries: List, Versions)
├── + EmployeeTax/  (SetEmployeeWithholding · Query: Get)
├── + Services/     CalculationInputLoader.cs (carga insumos → CalculationInput),
│                    PayrollAccountingPoster.cs (asiento NM dentro del handler),
│                    INoveltyFileParser.cs
├── ~ PayrollProcessing/ (retirado: ProcessPayroll, RegisterPayrollEntry; queries
│                    reescritas o redirigidas)
└── Common/Interfaces/IApplicationDbContext.cs (~ DbSets nuevos)

src/Infrastructure/IngenIA365ERP.Persistence/
├── Configurations/Payroll/ + 13 configuraciones; ~ Employee, PayPeriod
├── Seeding/Parametric/ + PayrollConceptDefinitionsSeeder.cs,
│                          PayrollLegalParametersSeeder.cs, PayrollVoucherTypeSeeder.cs
└── (migraciones en Persistence.Migrations.PostgreSql / .SqlServer, 4 pares)

src/Infrastructure/IngenIA365ERP.Identity/Seed/ + PayrollPermissionCatalogSeeder.cs
src/Infrastructure/IngenIA365ERP.Storage/
├── + Payroll/CsvNoveltyFileParser.cs
└── ~ Email/SmtpEmailSender.cs (adjuntos); ~ Application: EmailMessage.Attachments

src/Presentation/IngenIA365ERP.API/
├── Endpoints/Payroll/ + PayrollPlansEndpoints, PayrollNoveltiesEndpoints,
│                        PayrollRunsEndpoints, PayrollPaymentsEndpoints,
│                        PayrollPayslipsEndpoints, PayrollConceptDefinitionsEndpoints,
│                        PayrollLegalParametersEndpoints; ~ PayrollProcessingEndpoints (alias 308)
└── Reports/ ~ PayslipReport.cs; + PayslipPdfRenderer.cs (implementa IPayslipPdfRenderer)

src/Presentation/IngenIA365ERP.Shared/
├── Pages/Nomina/ ~ Novedades.razor, Liquidacion.razor, Conceptos.razor;
│                 + PlanesNomina.razor, ParametrosLegales.razor; ~ EmpleadoDetalle.razor
├── Components/Nomina/ + SelectorDePeriodo, ExplicacionDeLinea, PanelDeAprobacion, TablaDeRangos
├── Services/Nomina/ + NominaClient.cs (+ DTOs)
├── Layout/NavMenu.razor (~ grupo Nómina)
└── Services/Manual/ManualCatalogo.cs (~ guías de las 5 pantallas)

tests/
├── IngenIA365ERP.Domain.Tests/Payroll/Calculation/ + casos dorados (JSON) + reglas
├── IngenIA365ERP.Application.Tests/Payroll/ + validadores y handlers
├── IngenIA365ERP.API.IntegrationTests/Payroll/ + recorrido completo y permisos
└── IngenIA365ERP.Architecture.Tests/Principles/
    ├── ~ PrincipioVIII_DualValidation.cs (namespaces de nómina)
    └── + LaNominaNoTieneValoresLegalesFijos.cs
```

**Structure Decision**: se respeta el layout existente (Clean Architecture en cuatro
capas, un solo repositorio). Lo nuevo entra por módulo `Payroll` en cada capa; el
motor de cálculo es la única pieza con carpeta propia en Domain porque es la que se
prueba con casos dorados y no puede depender de nada. No se crea ningún proyecto.

## Complexity Tracking

Sin violaciones que justificar: las doce compuertas pasan. Dos decisiones que podrían
parecer complejidad y no lo son, para que no se reabran en tasks:

| Decisión | Por qué no es una violación |
|---|---|
| Una corrida nueva por cada cálculo (no se edita el borrador) | Es la consecuencia directa del Principio XI sobre `Entities/Payroll/Transactions`; editar in-place exigiría `Remove`, prohibido. Además da el «qué cambió» gratis. |
| El asiento se genera dentro del handler de aprobación y no vía `CreateDocumentCommand` | Principio III permite lógica en handlers; lo que prohíbe es lógica en endpoints. Dos comandos serían dos transacciones y FR-023 exige una. |

## Phase 0 — Outline & Research

Completado en [research.md](./research.md): catorce hallazgos verificados contra el
código (el más importante: `PayPeriod.PlanId` legado es número de planilla, y el
Principio XI obliga a corridas inmutables) y dieciséis decisiones (D-01 a D-16) con
alternativas. El Technical Context no tiene incógnitas pendientes: cada valor está
verificado en el código o decidido en la investigación.

## Phase 1 — Design & Contracts

### Data model

[data-model.md](./data-model.md): 10 entidades nuevas (`PayrollPlan`,
`PayrollConceptDefinition`, `PayrollConceptDefinitionAccount`,
`PayrollLegalParameter` + rangos, `PayrollNovelty`, `PayrollRecurringNovelty`,
`EmployeeWithholdingRate`, `EmployeeTaxDeduction`, `PayrollRun`,
`PayrollRunEmployee`, `PayrollRunLine`, `PayrollPayment`, `PayslipDelivery`), dos
extendidas (`PayPeriod`, `Employee`), el modelo en memoria del motor, los estados con
sus transiciones e invariantes, y las cuatro migraciones.

### Contracts

- [contracts/api.md](./contracts/api.md): siete grupos de endpoints con DTOs y
  códigos de error `Payroll.*`; retiro del cálculo preliminar con alias 308.
- [contracts/permissions.md](./contracts/permissions.md): 22 permisos, asignación a
  roles integrados, reglas de segregación y de auditoría explícita.
- [contracts/ui.md](./contracts/ui.md): cinco pantallas, el detalle del empleado, los
  cuatro componentes nuevos y el menú.

### Quickstart

[quickstart.md](./quickstart.md): levantar, qué debe dejar la semilla y cómo
comprobarlo, el recorrido completo con `curl` y sus verificaciones en base, las
pruebas por nivel, las migraciones en par y el despliegue.

### Agent context update

`CLAUDE.md`, bloque `<!-- SPECKIT START -->…<!-- SPECKIT END -->`: apunta a este plan
y sus artefactos (antes apuntaba a la 004).

## Constitution Re-check (post Phase 1 design)

Repetido sobre el diseño concreto:

- **II**: `IPayslipPdfRenderer` se implementa en API y `INoveltyFileParser` en
  Storage; Application no referencia QuestPDF ni CsvHelper. `Domain/Payroll/Calculation`
  no referencia nada fuera de Domain. PASS.
- **III/VIII**: 24 comandos, cada uno con validador; el test de arquitectura VIII
  incorpora los ocho namespaces nuevos, así que un comando sin validador rompe la
  compilación de pruebas. PASS.
- **IV**: `CalculateRunCommand` obtiene el lock con la clave prefijada por
  `tenantPublicId`; ninguna tabla nueva es `ADM_*`. PASS.
- **VI**: `ExplanationJson` guarda `PublicId` de novedad y código de parámetro, nunca
  `Id`. PASS.
- **X**: los siete comandos con efecto financiero o sobre personas escriben evento
  explícito en Mongo además del behavior. PASS.
- **XI**: la prueba `PrincipioXI_ContableImmutable` deja de ser vacua al aparecer
  `Entities/Payroll/Transactions`; el diseño no llama `Remove` sobre ellas. PASS.
- **XII**: el relleno de `PayrollPlanId` en filas existentes es un `UPDATE`
  idempotente dentro de la migración de DDL; si la política del repo lo exige, se
  separa en migración propia de datos (`NominaPlanesRelleno`). PASS.

**Resultado**: 12/12 PASS. Listo para `/speckit-tasks`.
