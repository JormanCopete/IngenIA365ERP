# Tasks: Novedades y Liquidación Periódica de Nómina

**Input**: Design documents from `/specs/005-nomina-novedades-liquidacion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, contracts/permissions.md, contracts/ui.md, quickstart.md

**Tests**: Incluidas donde la spec o la constitución las exigen, no como adorno:
casos dorados del motor (SC-001 «coincide al peso con la liquidación de la contadora»),
validadores por comando (Principio VIII, verificado por `PrincipioVIII_DualValidation`),
pruebas de arquitectura (X, XI, y la nueva «sin valores legales fijos») y el recorrido
HTTP de extremo a extremo (quickstart §3). Los handlers de flujo llevan pruebas con
`TestApplicationDbContext` (InMemory) y NSubstitute, como el resto de `Application.Tests`.

**Organization**: por historia de usuario de la spec. US1 y US2 son P1 y la spec las
declara inseparables para pagar una nómina: el MVP es Phase 1 + 2 + 3 + 4.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralelizable (archivos distintos, sin dependencia de una tarea incompleta)
- **[Story]**: US1…US7 según spec.md
- Rutas exactas en cada tarea. Raíces abreviadas:
  `DOM` = `src/Core/IngenIA365ERP.Domain`, `APP` = `src/Core/IngenIA365ERP.Application`,
  `PER` = `src/Infrastructure/IngenIA365ERP.Persistence`, `IDN` = `src/Infrastructure/IngenIA365ERP.Identity`,
  `STO` = `src/Infrastructure/IngenIA365ERP.Storage`, `API` = `src/Presentation/IngenIA365ERP.API`,
  `SHR` = `src/Presentation/IngenIA365ERP.Shared`, `TST` = `tests`.

## Path Conventions

Monorepo .NET existente (Clean Architecture en cuatro capas). No se crea ningún
proyecto. Migraciones **en par** con `tools/scripts/add-migration.ps1 -Context Application`.
Identificadores en inglés; textos de usuario en español; ningún color literal ni
`<style>` en las pantallas.

---

## Phase 1: Setup (enums, constantes y cambios transversales pequeños)

**Purpose**: lo que todas las capas necesitan compilado antes de escribir entidades.

- [X] T001 [P] Crear enums `PayrollPeriodicity` (Monthly=30, Biweekly=15), `PayPeriodStatus` (Open=0, Calculated=1, Approved=2, Reversed=3) y `EmployeeClass` (Standard, IntegralSalary, Apprentice, Intern, Pensioner) en `DOM/Enums/Payroll/PayrollPeriodicity.cs`, `PayPeriodStatus.cs`, `EmployeeClass.cs`
- [X] T002 [P] Crear enums `ConceptNature`, `CalculationKind`, `CalculationBase`, `UnitKind`, `ConceptOrigin`, `LegalParameterKind`, `TaxDeductionKind` en `DOM/Enums/Payroll/` (un archivo por enum), con los valores de data-model.md §2.6, §2.8, §2.5
- [X] T003 [P] Crear enums `NoveltyStatus`, `NoveltyOrigin`, `PayrollRunStatus`, `RunEmployeeFlag` ([Flags]), `PaymentMethod`, `DeliveryStatus` en `DOM/Enums/Payroll/` (un archivo por enum), valores de data-model.md §2.9, §2.11, §2.12, §2.14, §2.15
- [X] T004 [P] Crear `LegalParameterCodes` (constantes de los 27 códigos requeridos de data-model.md §2.8, con `Required` como lista) en `DOM/Payroll/Calculation/LegalParameterCodes.cs`
- [X] T005 [P] Extender `EmailMessage` con `IReadOnlyList<EmailAttachment>? Attachments = null` y crear `EmailAttachment(string FileName, string ContentType, byte[] Content)` en `APP/Common/Interfaces/Notifications/IEmailSender.cs`; adjuntarlos en `STO/Services/SmtpEmailSender.cs` (MimeKit `BodyBuilder.Attachments`); compilar y comprobar que los llamadores existentes no cambian
- [X] T006 [P] Añadir los namespaces `IngenIA365ERP.Application.Payroll.Plans`, `.Novelties`, `.Runs`, `.Payments`, `.Payslips`, `.Concepts`, `.LegalParameters`, `.EmployeeTax` a `InScopeNamespacePrefixes` en `TST/IngenIA365ERP.Architecture.Tests/Principles/PrincipioVIII_DualValidation.cs` (falla hasta que cada comando nuevo tenga validador: es la intención)
- [X] T007 [P] Crear prueba de arquitectura `LaNominaNoTieneValoresLegalesFijos` en `TST/IngenIA365ERP.Architecture.Tests/Principles/LaNominaNoTieneValoresLegalesFijos.cs`: ningún archivo bajo `DOM/Payroll/` ni `APP/Payroll/` (excluyendo `*Seeder*.cs` y pruebas) contiene literales decimales con sufijo `m` distintos de `0m`, `1m`, `30m`, `15m`, `100m`, `240m` (constantes de calendario y porcentaje), ni las cadenas `0.04`, `0.085`, `0.12`; el mensaje nombra archivo y línea

**Checkpoint**: la solución compila; VIII y la prueba nueva fallan o pasan vacuamente hasta que exista código de nómina.

---

## Phase 2: Foundational (entidades, esquema, semillas, permisos, motor, planes)

**Purpose**: todo lo que las siete historias comparten. **Bloquea** las historias.

### Entidades (Domain)

- [X] T008 [P] Crear `PayrollPlan : AuditableEntity` (Code, Name, Periodicity, IsDefault, IsActive) en `DOM/Entities/Payroll/PayrollPlan.cs`
- [X] T009 Extender `PayPeriod` (PayrollPlanId, PayrollPlan, Status → `PayPeriodStatus`, ApprovedAt, ApprovedBy, RunPublicId) en `DOM/Entities/Payroll/PayPeriod.cs` y `Employee` (PayrollPlanId, PayrollPlanEffectiveFrom, WithholdingProcedure byte, EmployeeClass) en `DOM/Entities/Payroll/Employee.cs`; corregir los usos de `Status` como `int` que dejen de compilar
- [X] T010 [P] Crear `PayrollConceptDefinition` y `PayrollConceptDefinitionAccount` (campos de data-model.md §2.6–2.7) en `DOM/Entities/Payroll/PayrollConceptDefinition.cs` y `PayrollConceptDefinitionAccount.cs`
- [X] T011 [P] Crear `PayrollLegalParameter` y `PayrollLegalParameterRange` (data-model.md §2.8) en `DOM/Entities/Payroll/PayrollLegalParameter.cs` y `PayrollLegalParameterRange.cs`
- [X] T012 [P] Crear `PayrollNovelty` y `PayrollRecurringNovelty` (data-model.md §2.9–2.10) en `DOM/Entities/Payroll/PayrollNovelty.cs` y `PayrollRecurringNovelty.cs`
- [X] T013 [P] Crear `EmployeeWithholdingRate` y `EmployeeTaxDeduction` (data-model.md §2.4–2.5) en `DOM/Entities/Payroll/EmployeeWithholdingRate.cs` y `EmployeeTaxDeduction.cs`
- [X] T014 [P] Crear `PayrollRun`, `PayrollRunEmployee` y `PayrollRunLine` (data-model.md §2.11–2.13) en `DOM/Entities/Payroll/Transactions/PayrollRun.cs`, `PayrollRunEmployee.cs`, `PayrollRunLine.cs` — namespace `IngenIA365ERP.Domain.Entities.Payroll.Transactions` (protegido por `PrincipioXI_ContableImmutable`)
- [X] T015 [P] Crear `PayrollPayment` y `PayslipDelivery` (data-model.md §2.14–2.15) en `DOM/Entities/Payroll/PayrollPayment.cs` y `PayslipDelivery.cs`

### Configuraciones EF y contexto

- [X] T016 [P] `PayrollPlanConfiguration` (`PAY_PayrollPlans`, UK Code, UX IsDefault filtrado con `ProviderModelConventions`) en `PER/Configurations/Payroll/PayrollPlanConfiguration.cs`
- [X] T017 [P] Actualizar `PayPeriodConfiguration` (FK PayrollPlanId, conversión del enum Status, índice `(PayrollPlanId, StartDate)`) y `EmployeeConfiguration` (FK PayrollPlanId, WithholdingProcedure, EmployeeClass) en `PER/Configurations/Payroll/PayPeriodConfiguration.cs` y `EmployeeConfiguration.cs`
- [X] T018 [P] `PayrollConceptDefinitionConfiguration` (`PAY_ConceptDefinitions`, UK `(Code, ValidFrom)`, IX `(Code, ValidTo)`, precisiones) y `PayrollConceptDefinitionAccountConfiguration` (`PAY_ConceptDefinitionAccounts`, UK `(ConceptCode, CostCenterId)`, FKs a `ChartOfAccounts`) en `PER/Configurations/Payroll/`
- [X] T019 [P] `PayrollLegalParameterConfiguration` (`PAY_LegalParameters`, UK `(Code, ValidFrom)`) y `PayrollLegalParameterRangeConfiguration` (`PAY_LegalParameterRanges`) en `PER/Configurations/Payroll/`
- [X] T020 [P] `PayrollNoveltyConfiguration` (`PAY_Novelties`, índices de §2.9, FKs Restrict) y `PayrollRecurringNoveltyConfiguration` (`PAY_RecurringNovelties`) en `PER/Configurations/Payroll/`
- [X] T021 [P] `EmployeeWithholdingRateConfiguration` (`PAY_EmployeeWithholdingRates`) y `EmployeeTaxDeductionConfiguration` (`PAY_EmployeeTaxDeductions`) en `PER/Configurations/Payroll/`
- [X] T022 [P] `PayrollRunConfiguration` (`PAY_PayrollRuns`, UK `(PayPeriodId, Version)`), `PayrollRunEmployeeConfiguration` (`PAY_PayrollRunEmployees`, UK `(PayrollRunId, EmployeeId)`) y `PayrollRunLineConfiguration` (`PAY_PayrollRunLines`, IX por RunEmployee, `ExplanationJson` como texto largo) en `PER/Configurations/Payroll/`
- [X] T023 [P] `PayrollPaymentConfiguration` (`PAY_PayrollPayments`, UX un pago vigente por RunEmployee filtrado `IsReverted = 0`) y `PayslipDeliveryConfiguration` (`PAY_PayslipDeliveries`) en `PER/Configurations/Payroll/`
- [X] T024 Añadir los 13 `DbSet` nuevos a `APP/Common/Interfaces/IApplicationDbContext.cs` y a `PER/DbContext/ApplicationDbContext.cs`; todas las configuraciones declaran `HasQueryFilter(!IsDeleted)` (Principio VII)
- [X] T025 Añadir los mismos `DbSet` a `TST/IngenIA365ERP.Application.Tests/Common/TestDbContextFactory.cs` (`TestApplicationDbContext`) para que los handlers se prueben con InMemory

### Migraciones (secuenciales, en par)

> **Ejecutado como UNA migración** `NominaNovedadesYLiquidacion` (2026-09-05) que cubre
> T026–T029: `dotnet ef migrations add` diferencia contra el snapshot completo y
> partirla en cuatro exigía fabricar a mano tres snapshots intermedios. El relleno
> idempotente del plan `DEFAULT` va dentro (ver encabezado de la migración).
> Verificada con `AutoMigrate` local sobre la base operativa y dos cooperativas;
> `Feature004_MigrationParity` y `PrincipioXII_*` en verde.

- [X] T026 Generar `NominaPlanesYPeriodos` con `.\tools\scripts\add-migration.ps1 -Name NominaPlanesYPeriodos -Context Application`; editar ambas migraciones para incluir, tras crear `PAY_PayrollPlans` y las columnas, un `UPDATE` idempotente que cree el plan `DEFAULT` si no existe y rellene `PayrollPlanId` en `PAY_PayPeriods` y `PAY_Employees` donde sea NULL; `Down` reversible; documentar en el header
- [X] T027 Generar `NominaConceptosYParametros` (`PAY_ConceptDefinitions`, `PAY_ConceptDefinitionAccounts`, `PAY_LegalParameters`, `PAY_LegalParameterRanges`, `PAY_EmployeeWithholdingRates`, `PAY_EmployeeTaxDeductions`) con `tools/scripts/add-migration.ps1 -Name NominaConceptosYParametros -Context Application`; revisar el par generado en `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/Application/` y `…Migrations.SqlServer/Application/`
- [X] T028 Generar `NominaNovedades` (`PAY_Novelties`, `PAY_RecurringNovelties`) con `tools/scripts/add-migration.ps1 -Name NominaNovedades -Context Application`; revisar el par generado en `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/Application/` y `…Migrations.SqlServer/Application/`
- [X] T029 Generar `NominaLiquidacion` (`PAY_PayrollRuns`, `PAY_PayrollRunEmployees`, `PAY_PayrollRunLines`, `PAY_PayrollPayments`, `PAY_PayslipDeliveries`) con el script en par; ejecutar `dotnet test tests/IngenIA365ERP.Architecture.Tests --filter Feature004_MigrationParity` y arrancar la API local para verificar `AutoMigrate`

### Semillas y permisos

- [X] T030 [P] Crear `PayrollPlansSeeder : IDataSeeder` (Parametric, Tenant, Order 60: plan `DEFAULT` mensual si no existe) en `PER/Seeding/Parametric/PayrollPlansSeeder.cs`
- [X] T031 [P] Crear `PayrollConceptDefinitionsSeeder : IDataSeeder` (Parametric, Tenant, Order 70) con los ≈28 conceptos estándar de research.md D-10 —código, nombre, naturaleza, forma, bases, clases, `IsAutomatic`, `Origin = Seed`, `ValidFrom = 2026-01-01`, incluido `AJUSTE_REDONDEO`— idempotente por `(Code, ValidFrom)`, en `PER/Seeding/Parametric/PayrollConceptDefinitionsSeeder.cs`
- [X] T032 [P] Crear `PayrollLegalParametersSeeder : IDataSeeder` (Parametric, Tenant, Order 71) con los 27 códigos de `LegalParameterCodes` con vigencia 2026 y sus rangos (tabla de retención en UVT, FSP) tomados de la normativa vigente y con `Source`; idempotente por `(Code, ValidFrom)`, en `PER/Seeding/Parametric/PayrollLegalParametersSeeder.cs`
- [X] T033 [P] Crear `PayrollVoucherTypeSeeder : IDataSeeder` (Parametric, Tenant, Order 72: `VoucherType` `NM` «Nómina» si falta) en `PER/Seeding/Parametric/PayrollVoucherTypeSeeder.cs` y añadir los `SystemSetting` `Payroll.Rounding=Peso`, `Payroll.VariationThresholdPercent=10`, `Payroll.AllowSameUserApproval=false` (ModulePrefix `PAY`) al `SystemParametersSeeder` existente en `PER/Seeding/Parametric/CatalogSeeders.cs`
- [X] T034 Registrar los cuatro seeders en `PER/DependencyInjection.cs` junto a los existentes (`services.AddScoped<Seeding.IDataSeeder, …>()`)
- [X] T035 [P] Crear `PayrollPermissionCatalogSeeder` con los 22 permisos de contracts/permissions.md en `IDN/Seed/PayrollPermissionCatalogSeeder.cs`, invocarlo donde corre `DomainPermissionCatalogSeeder`, y añadir la asignación a los roles integrados (Administrador de Cooperativa: todos; Operador, Auditor, Solo Lectura según la tabla) en `IDN/Seed/BuiltInRolesSeeder.cs`

### Motor de cálculo (Domain, puro)

- [ ] T036 [P] Crear `CalculationInput`, `CalculationResult`, `CalculationLine`, `Explanation` (`Form`, `Steps[{Label, Value}]`, `Parameter{Code, ValidFrom, Value}`, `Range`, `NoveltyPublicId`) y `CalculationRefusedException(missingCodes)` en `DOM/Payroll/Calculation/CalculationInput.cs`, `CalculationResult.cs`, `Explanation.cs`
- [ ] T037 [P] Crear `SalaryTranches` (tramos por fecha de efecto, días por tramo sobre base 30/15, ingreso y retiro dentro del período) en `DOM/Payroll/Calculation/Bases/SalaryTranches.cs`
- [ ] T038 [P] Crear `ICalculationRule` y `FixedAmountRule`, `PercentOfBaseRule` en `DOM/Payroll/Calculation/Rules/ICalculationRule.cs`, `FixedAmountRule.cs`, `PercentOfBaseRule.cs` (cada regla devuelve `CalculationLine` con su `Explanation` en sus propios términos)
- [ ] T039 [P] Crear `QuantityTimesUnitRule` (hora ordinaria = salario/`HORAS_MES`, día = salario/30, factor de recargo) y `RangeTableRule` (tramo, tarifa y fijo; base en UVT o SMMLV según el parámetro) en `DOM/Payroll/Calculation/Rules/QuantityTimesUnitRule.cs`, `RangeTableRule.cs`
- [ ] T040 [P] Crear `CompositeOfConceptsRule` y `ConceptDependencyGraph` (orden topológico; detección de ciclo con el camino) en `DOM/Payroll/Calculation/Rules/CompositeOfConceptsRule.cs`, `DOM/Payroll/Calculation/ConceptDependencyGraph.cs`
- [ ] T041 [P] Crear `BaseBuilder` (bases salarial, de aportes con tope de 25 SMMLV desde parámetro, prestacional, de auxilio de transporte con tope de 2 SMMLV) y `WithholdingBaseBuilder` (depuración: aportes obligatorios, deducciones declaradas con topes, renta exenta 25 % con tope, conversión a UVT; procedimiento 2 con porcentaje vigente o rechazo) en `DOM/Payroll/Calculation/Bases/BaseBuilder.cs`, `WithholdingBaseBuilder.cs`
- [ ] T042 Crear `PayrollCalculationEngine` (orden de evaluación de data-model.md §3, automáticos, novedades, deducciones de ley y autorizadas con tope `MAX_DEDUCCION_SALARIO_PCT` y saldo diferido, aportes y provisiones, banderas, redondeo con `AJUSTE_REDONDEO`) e `InputsHasher` (SHA-256 sobre el input normalizado) en `DOM/Payroll/Calculation/PayrollCalculationEngine.cs`, `InputsHasher.cs`
- [ ] T043 Crear el arnés de casos dorados (`CasoDorado.cs` que carga JSON `{input, expectedLines, expectedTotals}` y compara al peso) y los primeros cinco casos —tiempo completo mes entero, ingreso día 10, salario integral, aprendiz, dos salarios mínimos con auxilio— en `TST/IngenIA365ERP.Domain.Tests/Payroll/Calculation/CasoDorado.cs`, `CasosDoradosTests.cs`, `Casos/*.json`; más `RepetibilidadTests.cs` (mismo input → mismo hash y mismas líneas)

### Servicios compartidos de Application y cliente

- [ ] T044 Crear `CalculationInputLoader` (carga en ≤ 6 consultas: período y plan, empleados del plan vigentes, tramos desde `SalaryChanges`, novedades `Active`, versiones de concepto y parámetros vigentes a `EndDate`, deducciones y tasas del empleado, políticas de `SystemSettings`; construye `CalculationInput`) en `APP/Payroll/Services/CalculationInputLoader.cs`
- [ ] T045 [P] Crear `PayrollAccountingPoster` (valida período contable `CNT` abierto y `VoucherType` `NM`; agrupa líneas `AffectsAccounting` por concepto y centro de costo; débitos y créditos según naturaleza usando `PAY_ConceptDefinitionAccounts`; crea `AccountingDocument` + `JournalEntry` cuadrados; método `Reverse(original)`) en `APP/Payroll/Services/PayrollAccountingPoster.cs`
- [ ] T046 [P] Crear las interfaces `INoveltyFileParser`, `IPayslipPdfRenderer`, `IPayslipEmailDispatcher`, `IPayrollRunStaleMarker` en `APP/Payroll/Services/Interfaces.cs`, e implementar `PayrollRunStaleMarker` (marca `Stale` la corrida `Draft` del período afectado) registrándolo en `APP/DependencyInjection.cs`
- [ ] T047 [P] Crear `NominaClient` (patrón `ParametrosClient`: token desde `CentralAuthClient`, `EnviarAsync<T>`) con sus DTOs en `SHR/Services/Nomina/NominaClient.cs` y `NominaDtos.cs`; registrarlo en `src/Presentation/IngenIA365ERP.Web/Program.cs` y `src/Presentation/IngenIA365ERP.Web.Client/Program.cs` junto a `ParametrosClient`
- [ ] T048 [P] Crear componente `SelectorDePeriodo` (plan → período; oculta el plan si hay uno; emite `PeriodoCambiado`) en `SHR/Components/Nomina/SelectorDePeriodo.razor`

### Planes y períodos

- [ ] T049 Crear `CreatePayrollPlanCommand`, `UpdatePayrollPlanCommand`, `ChangeEmployeePlanCommand` con validadores y handlers, y `ListPayrollPlansQuery` con `PayrollPlanDto` en `APP/Payroll/Plans/` (una carpeta por comando); errores `Payroll.PlanCodeDuplicate`, `Payroll.PlanHasEmployees`, `Payroll.PlanNotFound`
- [ ] T050 Extender `PayPeriods` existente: `planPublicId` obligatorio al crear, validación de no superposición (`Payroll.PeriodOverlaps`), `PayPeriodDto` con plan y `status` textual, filtro `?planId&status` en `APP/Payroll/PayPeriods/Commands/` y `Queries/`
- [ ] T051 Crear `PayrollPlansEndpoints` (`/api/payroll/plans`, `.RequirePermission("Payroll.Plans.View|Manage")`) en `API/Endpoints/Payroll/PayrollPlansEndpoints.cs` y ajustar `API/Endpoints/Payroll/PayPeriodsEndpoints.cs` a los cambios de T050
- [ ] T052 [P] Crear pantalla `PlanesNomina.razor` (`/nomina/planes`, maestro con aviso de plan único) en `SHR/Pages/Nomina/PlanesNomina.razor` y ampliar `SHR/Pages/Nomina/PeriodosPago.razor` con plan y estado
- [ ] T053 [P] Añadir «Planes de nómina» y «Parámetros legales» al grupo Nómina de `SHR/Layout/NavMenu.razor` y las guías `planes-de-nomina` y `parametros-legales` a `SHR/Services/Manual/ManualCatalogo.cs`
- [X] T054 Retirar `APP/Payroll/PayrollProcessing/Commands/ProcessPayroll/ProcessPayrollCommand.cs` y `Commands/RegisterPayrollEntry/RegisterPayrollEntryCommand.cs`; en `API/Endpoints/Payroll/PayrollProcessingEndpoints.cs` eliminar `POST /process` y `POST /entries` y convertir `summary|detail|payslip` en redirecciones 308 a las rutas de contracts/api.md §4–5 (que se implementan en US2/US4/US5)

**Checkpoint**: compila; migraciones aplicadas en local; semilla deja plan, conceptos, parámetros, `NM` y permisos (quickstart §2); casos dorados iniciales en verde; VIII y XI en verde.

---

## Phase 3: User Story 1 — Registrar las novedades del período (Priority: P1) 🎯 MVP

**Goal**: registrar, listar, corregir y anular novedades de un período abierto, con cálculo de días y traslados, y cambios de salario con fecha de efecto.

**Independent Test**: con un período abierto y dos empleados, registrar cinco novedades de tipos distintos, verificar lista con valores estimados, corregir una, anular otra, y comprobar historial con usuario y fecha (spec US1).

### Tests for User Story 1

- [ ] T055 [P] [US1] Pruebas de `RegisterNoveltyCommandValidator` y handler (período no abierto, empleado no vigente, concepto no aplicable, duplicado sin repetición, días en período y traslado de incapacidad que cruza) en `TST/IngenIA365ERP.Application.Tests/Payroll/Novelties/RegisterNoveltyCommandHandlerTests.cs`
- [ ] T056 [P] [US1] Pruebas de `CorrectNoveltyCommand` (crea versión, marca `Superseded` con motivo, exige motivo) y `CancelNoveltyCommand` (marca `Cancelled`, rechaza si período aprobado con `retroactiveTargetPeriodPublicId`) en `TST/IngenIA365ERP.Application.Tests/Payroll/Novelties/CorrectAndCancelNoveltyTests.cs`
- [ ] T057 [P] [US1] Pruebas de `RegisterSalaryChangeCommand` (crea `SalaryChange`, espeja `Employee.Salary` si la fecha ya pasó, rechaza fecha dentro de período aprobado, marca `Stale` el borrador) en `TST/IngenIA365ERP.Application.Tests/Payroll/Novelties/RegisterSalaryChangeCommandHandlerTests.cs`

### Implementation for User Story 1

- [ ] T058 [US1] Crear `RegisterNoveltyCommand` + `Validator` + handler (FR-001..003: resolver PublicIds, concepto vigente a `EndDate` y aplicable a la clase, `RequiresDates|Quantity|Amount`, repetición, topes, `DaysInPeriod`/`CarryOverDays`, `Origin = Manual`, llamada a `IPayrollRunStaleMarker`) en `APP/Payroll/Novelties/RegisterNovelty/RegisterNoveltyCommand.cs`
- [ ] T059 [US1] Crear `CorrectNoveltyCommand` y `CancelNoveltyCommand` + validadores + handlers (FR-005; en período aprobado responder `Payroll.PeriodApproved` con el período abierto siguiente del plan) en `APP/Payroll/Novelties/CorrectNovelty/CorrectNoveltyCommand.cs` y `CancelNovelty/CancelNoveltyCommand.cs`
- [ ] T060 [US1] Crear `RegisterSalaryChangeCommand` + validador + handler (FR-004; evento explícito `IAuditAppendOnlyWriter` con salario anterior y nuevo — Principio X) en `APP/Payroll/Novelties/RegisterSalaryChange/RegisterSalaryChangeCommand.cs`
- [ ] T061 [P] [US1] Crear `ListNoveltiesQuery` (filtros de contracts/api.md §3, `NoveltyDto` con `estimatedAmount` calculado con el motor sobre el concepto y el salario vigente), `GetNoveltyHistoryQuery` y `ListSalaryChangesQuery` en `APP/Payroll/Novelties/Queries/NoveltyQueries.cs`
- [ ] T062 [P] [US1] Crear `CarryOverNoveltiesService` (al crear una novedad con `CarryOverDays > 0`, generar la novedad `Origin = CarryOver` en el siguiente período abierto del plan cuando exista, con `CarriedFromNoveltyId`) en `APP/Payroll/Novelties/CarryOverNoveltiesService.cs` e invocarlo desde T058
- [ ] T063 [US1] Crear `PayrollNoveltiesEndpoints` (`/api/payroll/pay-periods/{periodId}/novelties`, `/api/payroll/novelties/{id}`, `/cancel`, `/history`, `/api/payroll/employees/{id}/salary-changes`; permisos `Payroll.Novelties.*`) en `API/Endpoints/Payroll/PayrollNoveltiesEndpoints.cs`
- [ ] T064 [US1] Añadir a `NominaClient` los métodos de novedades y cambios de salario (`ListarNovedadesAsync`, `RegistrarNovedadAsync`, `CorregirNovedadAsync`, `AnularNovedadAsync`, `HistorialNovedadAsync`, `RegistrarCambioDeSalarioAsync`, `HistorialSalariosAsync`) en `SHR/Services/Nomina/NominaClient.cs`
- [ ] T065 [US1] Reescribir `Novedades.razor` (`/nomina/novedades`: `SelectorDePeriodo`, pastilla de estado, barra con buscar/filtros/«Nuevo»/«Cambio de salario», grilla con lápiz y papelera con motivo, diálogo de novedad con `PersonSearchPicker` filtrado a empleados, lista de conceptos aplicables agrupada por naturaleza, campos según el concepto y valor estimado al vuelo, diálogo de cambio de salario con historial; modelos con `DataAnnotations`; `_saving`) en `SHR/Pages/Nomina/Novedades.razor`
- [ ] T066 [P] [US1] Actualizar la guía `novedades-de-nomina` en `SHR/Services/Manual/ManualCatalogo.cs` con los pasos reales de la pantalla (Nuevo, corregir, anular, cambio de salario, ajuste retroactivo)
- [ ] T067 [US1] Prueba de integración HTTP: crear período, registrar, corregir, anular y listar novedades; sin permiso `Payroll.Novelties.Create` → 404 indistinguible en `TST/IngenIA365ERP.API.IntegrationTests/Payroll/NoveltiesEndpointsTests.cs`

**Checkpoint**: US1 verificable sola: la cooperativa deja las novedades en hojas de cálculo.

---

## Phase 4: User Story 2 — Liquidar el período: calcular, revisar, aprobar (Priority: P1) 🎯 MVP

**Goal**: calcular la nómina de un plan y período en borrador, recalcular, y aprobar generando el comprobante contable en la misma transacción.

**Independent Test**: tres empleados (tiempo completo, ingreso día 10, salario > 2 SMMLV) y las novedades de US1; calcular, comparar cada línea con la liquidación manual, cambiar una novedad, recalcular (sólo cambian las líneas afectadas), aprobar, verificar período cerrado, segundo cálculo rechazado, comprobante cuadrado (spec US2).

### Tests for User Story 2

- [ ] T068 [P] [US2] Casos dorados 6–12 (ingreso y retiro en el mismo período, dos cambios de salario, incapacidad con dos días al 66,67 % del empleador, FSP por tabla, tope de aportes 25 SMMLV, deducciones sobre el máximo con saldo diferido, neto negativo) en `TST/IngenIA365ERP.Domain.Tests/Payroll/Calculation/Casos/`
- [ ] T069 [P] [US2] Pruebas de `CalculatePayrollRunCommandHandler` (lock ocupado → `Payroll.RunInProgress`; parámetro sin vigencia → `Payroll.LegalParameterMissing` y nada persistido; corrida previa `Draft` → `Superseded` y `Version + 1`; `changedEmployees`; período → `Calculated`; `IDistributedLock` sustituido) en `TST/IngenIA365ERP.Application.Tests/Payroll/Runs/CalculatePayrollRunCommandHandlerTests.cs`
- [ ] T070 [P] [US2] Pruebas de `ApprovePayrollRunCommandHandler` (corrida `Stale` → `Payroll.RunStale`; bloqueos sin excepción → `Payroll.ApprovalBlocked`; excepción sin permiso → `Payroll.ExceptionNotAuthorized`; concepto sin cuentas → `Payroll.ConceptWithoutAccounts`; período contable cerrado; segregación con y sin `Payroll.AllowSameUserApproval`; éxito → `Approved`, `AccountingDocument` `NM` cuadrado, novedades congeladas) en `TST/IngenIA365ERP.Application.Tests/Payroll/Runs/ApprovePayrollRunCommandHandlerTests.cs`

### Implementation for User Story 2

- [ ] T071 [US2] Crear `CalculatePayrollRunCommand` + validador + handler (FR-007, 008, 011, 014, 015: lock `payroll:run:{tenant}:{period}` TTL 5 min; `CalculationInputLoader`; incluir descuentos de Cartera del período como novedades `LoanDeduction` `AffectsAccounting = false` (D-08); motor por empleado; crear `PayrollRun` v+1 con `InputsHash`, `RunEmployee` con `ChangedFromPreviousRun`, `RunLine` con `ExplanationJson`; marcar anterior `Superseded`; período `Calculated`; un solo `SaveChangesAsync`) en `APP/Payroll/Runs/CalculatePayrollRun/CalculatePayrollRunCommand.cs`
- [ ] T072 [P] [US2] Crear `GetCurrentRunQuery`, `ListRunsQuery`, `GetRunSummaryQuery` (`RunSummaryDto` con `byConcept`, `blockers`, `changedEmployees`) y `ListRunEmployeesQuery` (filtros `flag`, `changed`) en `APP/Payroll/Runs/Queries/RunQueries.cs`
- [ ] T073 [US2] Crear `ApprovePayrollRunCommand` + validador + handler (FR-019..023: bloqueos y excepciones con `Payroll.Runs.AuthorizeException`; segregación FR-020/021 con `Payroll.SegregationOfDuties` y segunda confirmación; `PayrollAccountingPoster`; período `Approved`; `RunPublicId`; `ExceptionsJson`; `ApprovedWithoutSegregation`; evento explícito de auditoría con totales y documento — Principio X) en `APP/Payroll/Runs/ApprovePayrollRun/ApprovePayrollRunCommand.cs`
- [ ] T074 [US2] Crear `PayrollRunsEndpoints` (`POST/GET /api/payroll/pay-periods/{id}/runs`, `/runs/current`, `GET /api/payroll/runs/{id}`, `/employees`, `POST /approve`; permisos `Payroll.Runs.*`) en `API/Endpoints/Payroll/PayrollRunsEndpoints.cs`
- [ ] T075 [US2] Añadir a `NominaClient` (`CalcularAsync`, `CorridaActualAsync`, `ResumenAsync`, `EmpleadosDeCorridaAsync`, `AprobarAsync`) en `SHR/Services/Nomina/NominaClient.cs`
- [ ] T076 [P] [US2] Crear componente `PanelDeAprobacion` (resumen, bloqueos con casilla «Autorizar excepción» + motivo visible sólo con permiso, texto de confirmación explícito, segunda confirmación sin segregación) en `SHR/Components/Nomina/PanelDeAprobacion.razor`
- [ ] T077 [US2] Reescribir `Liquidacion.razor` (`/nomina/liquidacion`: `SelectorDePeriodo`, tarjeta de estado con Calcular/Recalcular/Aprobar según estado y permiso, pestañas Resumen y Empleados con banderas y «cambió», `PanelDeAprobacion`) en `SHR/Pages/Nomina/Liquidacion.razor`
- [ ] T078 [P] [US2] Actualizar la guía `liquidacion-de-nomina` en `SHR/Services/Manual/ManualCatalogo.cs`
- [ ] T079 [US2] Prueba de integración HTTP del ciclo: período → novedades → calcular → recalcular → aprobar → segundo cálculo rechazado → `ACC_AccountingDocuments` `NM` cuadrado en `TST/IngenIA365ERP.API.IntegrationTests/Payroll/PayrollRunsEndpointsTests.cs`

**Checkpoint**: MVP completo: una cooperativa paga una nómina con el sistema (US1 + US2).

---

## Phase 5: User Story 3 — Parametrizar conceptos y reglas sin tocar el programa (Priority: P2)

**Goal**: la administradora crea y revisa conceptos por forma de cálculo, mantiene parámetros legales con vigencia, configura retención por empleado y prueba conceptos en seco.

**Independent Test**: crear «Bonificación por antigüedad» (2 % del básico, no prestacional, afecta retención), registrar el auxilio de transporte 2027 con vigencia 1 de enero, probar en seco sobre un empleado, calcular diciembre y enero y ver cada vigencia (spec US3).

### Tests for User Story 3

- [ ] T080 [P] [US3] Pruebas de `CreateConceptDefinitionCommandValidator`/`ReviseConceptDefinitionCommand` (campos por forma → `Payroll.ConceptFormIncomplete`, referencia inexistente, ciclo A→B→A → `Payroll.ConceptCycle` con el camino, revisión crea versión y cierra la anterior, semilla protegida, borrador → `Stale`) en `TST/IngenIA365ERP.Application.Tests/Payroll/Concepts/ConceptDefinitionCommandsTests.cs`
- [ ] T081 [P] [US3] Pruebas de `AddLegalParameterVersionCommand` (solape → `Payroll.LegalParameterOverlap`, rangos con huecos → `Payroll.RangeTableInvalid`, cierra la vigencia anterior) y `SetEmployeeWithholdingCommand` (solape de tasas) en `TST/IngenIA365ERP.Application.Tests/Payroll/LegalParameters/LegalParameterCommandsTests.cs`

### Implementation for User Story 3

- [ ] T082 [US3] Crear `CreateConceptDefinitionCommand`, `ReviseConceptDefinitionCommand`, `DeactivateConceptDefinitionCommand` + validadores + handlers (FR-027..029; `ConceptDependencyGraph` para ciclos; `Origin = Custom`; `Payroll.ConceptSeedProtected`; `IPayrollRunStaleMarker` sobre períodos abiertos del plan) en `APP/Payroll/Concepts/CreateConceptDefinition/`, `ReviseConceptDefinition/`, `DeactivateConceptDefinition/`
- [ ] T083 [P] [US3] Crear `SetConceptAccountsCommand` + validador + handler (FK a `ChartOfAccounts` por `PublicId`, centro de costo opcional) en `APP/Payroll/Concepts/SetConceptAccounts/SetConceptAccountsCommand.cs`
- [ ] T084 [P] [US3] Crear `ListConceptDefinitionsQuery` (`asOf`, `includeInactive`), `ListConceptVersionsQuery`, `ListLegacyConceptsQuery` (sólo lectura de `PayrollConcepts`) y `DryRunConceptQuery` (definición sin guardar + empleado + período → línea con explicación vía motor) en `APP/Payroll/Concepts/Queries/ConceptQueries.cs`
- [ ] T085 [P] [US3] Crear `ReapplyConceptSeedCommand` + validador + handler (ejecuta `PayrollConceptDefinitionsSeeder` y `PayrollLegalParametersSeeder` vía `IDataSeedRunner` para la cooperativa activa) en `APP/Payroll/Concepts/ReapplyConceptSeed/ReapplyConceptSeedCommand.cs`
- [ ] T086 [P] [US3] Crear `AddLegalParameterVersionCommand` + validador + handler y `ListLegalParametersQuery`, `ListLegalParameterVersionsQuery` en `APP/Payroll/LegalParameters/AddLegalParameterVersion/AddLegalParameterVersionCommand.cs`, `Queries/LegalParameterQueries.cs`
- [ ] T087 [P] [US3] Crear `SetEmployeeWithholdingCommand` + validador + handler (procedimiento, tasas con vigencia, deducciones declaradas; evento explícito de auditoría) y `GetEmployeeWithholdingQuery` en `APP/Payroll/EmployeeTax/SetEmployeeWithholding/SetEmployeeWithholdingCommand.cs`, `Queries/EmployeeWithholdingQueries.cs`
- [ ] T088 [US3] Crear `PayrollConceptDefinitionsEndpoints` y `PayrollLegalParametersEndpoints` (contracts/api.md §6; permisos `Payroll.Concepts.*`, `Payroll.LegalParameters.*`) en `API/Endpoints/Payroll/`, y añadir `GET/PUT /api/payroll/employees/{id}/withholding` a `API/Endpoints/Payroll/EmployeesEndpoints.cs`
- [ ] T089 [US3] Añadir a `NominaClient` los métodos de conceptos, parámetros legales y retención del empleado en `SHR/Services/Nomina/NominaClient.cs`
- [ ] T090 [P] [US3] Crear componente `TablaDeRangos` (filas desde/hasta/tarifa/fijo con validación de huecos y solapes en cliente) en `SHR/Components/Nomina/TablaDeRangos.razor`
- [ ] T091 [US3] Reescribir `Conceptos.razor` (`/nomina/conceptos`: pestañas Definiciones, Versiones, Catálogo heredado con «Traducir a definición» prellenando sólo lo traducible; formulario que cambia campos según la forma; cuentas con `AccountSearchDialog`; «Probar en seco»; aviso de borrador desactualizado) en `SHR/Pages/Nomina/Conceptos.razor`
- [ ] T092 [P] [US3] Crear `ParametrosLegales.razor` (`/nomina/parametros-legales`: grilla por código con vigencias, «Nueva vigencia» con `TablaDeRangos`, alerta de códigos requeridos sin vigencia para el año en curso y el siguiente) en `SHR/Pages/Nomina/ParametrosLegales.razor`
- [ ] T093 [P] [US3] Añadir pestaña «Retención y plan» (procedimiento, tasas con vigencia, deducciones declaradas, plan y fecha de efecto) a `SHR/Pages/Nomina/EmpleadoDetalle.razor`
- [ ] T094 [P] [US3] Actualizar las guías `conceptos-de-nomina` y `parametros-legales` en `SHR/Services/Manual/ManualCatalogo.cs`

**Checkpoint**: la cooperativa es autónoma ante un cambio legal (SC-003).

---

## Phase 6: User Story 4 — Comprobar y explicar cada valor (Priority: P2)

**Goal**: detalle por empleado con explicación por línea, comparativo con el período anterior, verificación de cuadre y exportación.

**Independent Test**: abrir la liquidación de un empleado con incapacidad y retención, reconstruir a mano cada línea desde la explicación; comparativo con variaciones resaltadas; exportar y comprobar las mismas líneas (spec US4).

### Tests for User Story 4

- [ ] T095 [P] [US4] Casos dorados 13–20 centrados en explicación (retención P1 con deducciones declaradas y renta exenta, P2 con tasa vigente, P2 sin tasa → rechazo, redondeo con `AJUSTE_REDONDEO`, comisión sobre base salarial, hora extra con recargo, aporte del empleador, provisión de prima) en `TST/IngenIA365ERP.Domain.Tests/Payroll/Calculation/Casos/`
- [ ] T096 [P] [US4] Pruebas de `GetRunComparisonQuery` (variación, umbral, empleado nuevo y retirado) y `GetRunBalanceCheckQuery` (las tres verificaciones, documento cuadrado o nulo) en `TST/IngenIA365ERP.Application.Tests/Payroll/Runs/ComparisonAndBalanceQueriesTests.cs`

### Implementation for User Story 4

- [ ] T097 [P] [US4] Crear `GetRunEmployeeDetailQuery` (`RunEmployeeDetailDto` con tramos y líneas con `explanation` deserializada) en `APP/Payroll/Runs/Queries/GetRunEmployeeDetailQuery.cs`
- [ ] T098 [P] [US4] Crear `GetRunComparisonQuery` (contra la última corrida `Approved` del período anterior del mismo plan; umbral de `Payroll.VariationThresholdPercent`) y `GetRunBalanceCheckQuery` en `APP/Payroll/Runs/Queries/ComparisonAndBalanceQueries.cs`
- [ ] T099 [P] [US4] Crear `ExportRunQuery` (CSV UTF-8 con `;`: empleado, concepto, cantidad, base, factor, parámetro y vigencia, novedad, valor, explicación en texto; evento explícito de auditoría de la exportación) en `APP/Payroll/Runs/Queries/ExportRunQuery.cs`
- [ ] T100 [US4] Añadir `GET /runs/{id}/employees/{employeeId}`, `/comparison`, `/balance-check`, `/export` (permisos `Payroll.Runs.View|Export`) a `API/Endpoints/Payroll/PayrollRunsEndpoints.cs`, y `NominaClient` (`DetalleEmpleadoAsync`, `ComparativoAsync`, `CuadreAsync`, `ExportarAsync`) en `SHR/Services/Nomina/NominaClient.cs`
- [ ] T101 [P] [US4] Crear componente `ExplicacionDeLinea` (forma, pasos etiqueta→valor, parámetro con vigencia, enlace a la novedad) en `SHR/Components/Nomina/ExplicacionDeLinea.razor`
- [ ] T102 [US4] Añadir a `Liquidacion.razor` el diálogo de detalle del empleado (tramos + líneas con `ExplicacionDeLinea` + botón «Comprobante PDF» que se activa en US5), las pestañas Comparativo (filtro «sólo variaciones») y Cuadre (✓/✗), y el botón Exportar en `SHR/Pages/Nomina/Liquidacion.razor`

**Checkpoint**: cada peso es reconstruible por una persona ajena al cálculo (SC-002).

---

## Phase 7: User Story 5 — Contabilizar y disponer el pago (Priority: P2)

**Goal**: relación de pago con marca de pagado por empleado, comprobantes de pago en PDF (uno o todos) y envío manual por correo con registro. El asiento contable ya nace en la aprobación (US2).

**Independent Test**: aprobar un período; verificar comprobante `NM` cuadrado por concepto y cuenta; un concepto sin cuenta impide aprobar; relación de pago con neto y cuenta por empleado; marcar pagados; descargar PDF; enviar a dos sin correo → listados para entrega manual (spec US5).

### Tests for User Story 5

- [ ] T103 [P] [US5] Pruebas de `MarkPaymentsCommand` (todos o algunos, `PaidAt/Method/Reference`, ya marcado → `Payroll.PaymentAlreadyMarked`, corrida no aprobada) y `RevertPaymentMarkCommand` (permiso, motivo obligatorio, `IsReverted`) en `TST/IngenIA365ERP.Application.Tests/Payroll/Payments/PaymentCommandsTests.cs`
- [ ] T104 [P] [US5] Pruebas de `PayrollAccountingPoster` (líneas agrupadas por concepto y centro de costo, débitos = créditos, `AffectsAccounting = false` excluidas, concepto sin cuentas → error con lista, período `CNT` cerrado, `Reverse` con referencia) en `TST/IngenIA365ERP.Application.Tests/Payroll/Services/PayrollAccountingPosterTests.cs`
- [ ] T105 [P] [US5] Pruebas de `SendPayslipsCommand` (sin correo → `withoutEmail`, fallo del `IEmailSender` → `PayslipDelivery` `Failed` y `failures`, éxito → `Sent`, ambiente sin correo → `Payroll.EmailNotConfigured`) en `TST/IngenIA365ERP.Application.Tests/Payroll/Payslips/SendPayslipsCommandHandlerTests.cs`

### Implementation for User Story 5

- [ ] T106 [P] [US5] Crear `GetPaymentRegisterQuery` (neto, medio de pago, banco, tipo y número de cuenta desde `Employee`, nombre y documento desde `Person`, estado de pago) en `APP/Payroll/Payments/Queries/GetPaymentRegisterQuery.cs`
- [ ] T107 [US5] Crear `MarkPaymentsCommand` y `RevertPaymentMarkCommand` + validadores + handlers (FR-040; evento explícito de auditoría) en `APP/Payroll/Payments/MarkPayments/MarkPaymentsCommand.cs`, `RevertPaymentMark/RevertPaymentMarkCommand.cs`
- [ ] T108 [P] [US5] Reescribir `GetPayslipQuery` (`PayslipDto` desde `PayrollRunEmployee` + líneas; datos de la persona) y crear `ListPayslipDeliveriesQuery` en `APP/Payroll/Payslips/Queries/PayslipQueries.cs`; retirar el `GetPayslipQuery` legado de `APP/Payroll/PayrollProcessing/Queries/PayrollQueries.cs`
- [ ] T109 [P] [US5] Implementar `PayslipPdfRenderer : IPayslipPdfRenderer` sobre `PayslipReport.Generate` (uno y todos en un PDF) en `API/Reports/PayslipPdfRenderer.cs`, registrarlo en `API/Program.cs`, y adaptar `API/Reports/PayslipReport.cs` al `PayslipDto` nuevo (tramos, explicación resumida por línea)
- [ ] T110 [US5] Crear `SendPayslipsCommand` + validador + handler y `PayslipEmailDispatcher : IPayslipEmailDispatcher` (plantilla `PayslipEmail.html` en `IIdentityEmailTemplates`; PDF adjunto vía `EmailMessage.Attachments`; un `PayslipDelivery` por intento; nunca automático) en `APP/Payroll/Payslips/SendPayslips/SendPayslipsCommand.cs`, `APP/Payroll/Payslips/PayslipEmailDispatcher.cs`; registrar en `APP/DependencyInjection.cs`
- [ ] T111 [US5] Crear `PayrollPaymentsEndpoints` y `PayrollPayslipsEndpoints` (contracts/api.md §5; permisos `Payroll.Payments.*`, `Payroll.Payslips.*`) en `API/Endpoints/Payroll/`, y reemplazar el `TODO` de `GET /api/reports/payroll/payslip/…/pdf` en `API/Endpoints/Reports/PayrollReportsEndpoints.cs` por la implementación real
- [ ] T112 [US5] Añadir a `NominaClient` (`RelacionDePagoAsync`, `MarcarPagadosAsync`, `RetirarMarcaAsync`, `ComprobantePdfAsync`, `ComprobantesPdfAsync`, `EnviarComprobantesAsync`, `EnviosAsync`) en `SHR/Services/Nomina/NominaClient.cs`
- [ ] T113 [US5] Añadir a `Liquidacion.razor` la pestaña «Relación de pago» (grilla, «Marcar pagados» con fecha/medio/referencia, «Retirar marca» con motivo, descargar uno/todos, «Enviar por correo» con resultado y pestaña de envíos) y activar «Comprobante PDF» en el detalle en `SHR/Pages/Nomina/Liquidacion.razor`
- [ ] T114 [P] [US5] Ampliar la guía `liquidacion-de-nomina` (relación de pago, marca de pagado, comprobantes y envío) en `SHR/Services/Manual/ManualCatalogo.cs`
- [ ] T115 [US5] Prueba de integración HTTP: aprobar → relación de pago → marcar pago → PDF descarga con `application/pdf` en `TST/IngenIA365ERP.API.IntegrationTests/Payroll/PaymentsAndPayslipsEndpointsTests.cs`

**Checkpoint**: la nómina llega a contabilidad y a tesorería, y el empleado recibe su comprobante.

---

## Phase 8: User Story 6 — Novedades recurrentes y carga masiva (Priority: P3)

**Goal**: recurrentes por cuotas o fecha final que se materializan en cada período, y carga por archivo validada completa antes de aplicar.

**Independent Test**: libranza a 3 cuotas aparece en tres períodos y no en el cuarto; archivo de 20 filas con dos erróneas se rechaza señalándolas; corregidas, se aplica (spec US6).

### Tests for User Story 6

- [ ] T116 [P] [US6] Pruebas de `ImportNoveltiesCommand` (dos filas inválidas → 422, `applied = 0`, nada persistido, errores con fila y columna; archivo válido → novedades `Origin = Import` con el mismo historial que el registro manual; > 5 MB rechazado) en `TST/IngenIA365ERP.Application.Tests/Payroll/Novelties/ImportNoveltiesCommandHandlerTests.cs`
- [ ] T117 [P] [US6] Pruebas de materialización de recurrentes en `CalculatePayrollRunCommandHandler` (cuotas 1..3 con `InstallmentNumber`, no en la cuarta, no duplica si ya existe, `InstallmentsIssued` sólo al aprobar) en `TST/IngenIA365ERP.Application.Tests/Payroll/Novelties/RecurringNoveltiesTests.cs`

### Implementation for User Story 6

- [ ] T118 [P] [US6] Crear `CreateRecurringNoveltyCommand`, `DeactivateRecurringNoveltyCommand` + validadores + handlers y `ListRecurringNoveltiesQuery` en `APP/Payroll/Novelties/CreateRecurringNovelty/`, `DeactivateRecurringNovelty/`, `Queries/RecurringNoveltyQueries.cs`
- [ ] T119 [US6] Materializar recurrentes activas del plan en `CalculationInputLoader` (crear `PayrollNovelty(Origin = Recurring, InstallmentNumber)` si no existe para el período) e incrementar `InstallmentsIssued` en `ApprovePayrollRunCommand` en `APP/Payroll/Services/CalculationInputLoader.cs`, `APP/Payroll/Runs/ApprovePayrollRun/ApprovePayrollRunCommand.cs`
- [ ] T120 [P] [US6] Implementar `CsvNoveltyFileParser : INoveltyFileParser` con CsvHelper (`;`, UTF-8, encabezados en español: documento, concepto, cantidad, valor, desde, hasta, observación) y `GenerateTemplate()` en `STO/Payroll/CsvNoveltyFileParser.cs`; añadir el paquete CsvHelper a `STO/IngenIA365ERP.Storage.csproj` y registrar en `STO/DependencyInjection.cs`
- [ ] T121 [US6] Crear `ImportNoveltiesCommand` (stream + nombre) + validador + handler (parsear, resolver documento → empleado del plan, validar cada fila con las reglas de `RegisterNovelty`, aplicar todo o nada con `ImportBatchId`) en `APP/Payroll/Novelties/ImportNovelties/ImportNoveltiesCommand.cs`
- [ ] T122 [US6] Añadir `GET /novelties/import-template`, `POST /pay-periods/{id}/novelties/import` (multipart, límite 5 MB) y las rutas de recurrentes (permisos `Payroll.Novelties.Import|Create|Cancel|View`) a `API/Endpoints/Payroll/PayrollNoveltiesEndpoints.cs`
- [ ] T123 [US6] Añadir a `NominaClient` (`PlantillaImportacionAsync`, `ImportarNovedadesAsync` multipart, `ListarRecurrentesAsync`, `CrearRecurrenteAsync`, `DesactivarRecurrenteAsync`) y a `Novedades.razor` el diálogo «Importar» (descarga de plantilla, selector de archivo, tabla de errores) y la pestaña «Recurrentes» en `SHR/Services/Nomina/NominaClient.cs`, `SHR/Pages/Nomina/Novedades.razor`
- [ ] T124 [P] [US6] Ampliar la guía `novedades-de-nomina` (recurrentes e importación) en `SHR/Services/Manual/ManualCatalogo.cs`
- [ ] T125 [US6] Prueba de integración HTTP de importación (lote inválido → 422 sin cambios; lote válido → novedades listadas) en `TST/IngenIA365ERP.API.IntegrationTests/Payroll/ImportNoveltiesEndpointsTests.cs`

**Checkpoint**: menos transcripción, mismos controles.

---

## Phase 9: User Story 7 — Reversión controlada de un período aprobado (Priority: P3)

**Goal**: reversar con permiso y motivo, generando el asiento reverso, reabriendo el período y conservando la corrida reversada; bloqueada por pagos marcados o período contable cerrado.

**Independent Test**: aprobar, reversar con motivo, verificar comprobante de reversión, período «Abierto» con novedades intactas, corrida reversada consultable; un período con pagos marcados no se deja reversar (spec US7).

### Tests for User Story 7

- [ ] T126 [P] [US7] Pruebas de `ReversePayrollRunCommandHandler` (pago vigente → `Payroll.PaymentBlocksReversal` con la lista; período `CNT` cerrado → `Payroll.AccountingPeriodClosedForReversal`; sin motivo → `Payroll.ReasonRequired`; éxito → asiento reverso con referencia, corrida `Reversed`, período `Open`, novedades sin cambios, `InstallmentsIssued` decrementado) en `TST/IngenIA365ERP.Application.Tests/Payroll/Runs/ReversePayrollRunCommandHandlerTests.cs`

### Implementation for User Story 7

- [ ] T127 [US7] Crear `ReversePayrollRunCommand` + validador + handler (FR-032; `PayrollAccountingPoster.Reverse`; `ReversalAccountingDocumentId`; período `Reversed` → `Open` en el mismo acto; evento explícito de auditoría con motivo) en `APP/Payroll/Runs/ReversePayrollRun/ReversePayrollRunCommand.cs`
- [ ] T128 [US7] Añadir `POST /api/payroll/runs/{id}/reverse` (permiso `Payroll.Runs.Reverse`) a `API/Endpoints/Payroll/PayrollRunsEndpoints.cs` y `ReversarAsync` a `SHR/Services/Nomina/NominaClient.cs`
- [ ] T129 [US7] Añadir a `Liquidacion.razor` el botón y diálogo «Reversar» (motivo obligatorio, advertencia, bloqueado si hay pagos con la lista visible) y el historial de corridas (versiones, reversadas) en `SHR/Pages/Nomina/Liquidacion.razor`
- [ ] T130 [P] [US7] Ampliar la guía `liquidacion-de-nomina` (reversión y cuándo no) en `SHR/Services/Manual/ManualCatalogo.cs`

**Checkpoint**: las siete historias son verificables por separado.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [ ] T131 [P] Escribir el runbook `docs/operaciones/nomina-primer-periodo.md` (qué debe dejar la semilla y cómo comprobarlo contra la base del ambiente, cómo se carga la vigencia de un año nuevo, qué hacer ante `Payroll.LegalParameterMissing`, cómo reaplicar la semilla) e indexarlo en `docs/INDICE-DOCUMENTACION.md`
- [ ] T132 [P] Prueba de rendimiento en `TST/IngenIA365ERP.Domain.Tests/Payroll/Calculation/RendimientoTests.cs`: 200 empleados sintéticos con 8 novedades cada uno se calculan en < 10 s en el motor puro (SC-004 deja margen a la persistencia); y una prueba de integración marcada con `RUN_PERF_TESTS` que mide el ciclo completo < 60 s
- [ ] T133 [P] Verificar que `PrincipioXI_ContableImmutable` ya no pasa vacuamente (el namespace `Entities/Payroll/Transactions` existe) y que `PrincipioVIII_DualValidation` cubre los 24 comandos; ejecutar `dotnet test tests/IngenIA365ERP.Architecture.Tests` y corregir lo que salga
- [ ] T134 Recorrer `specs/005-nomina-novedades-liquidacion/quickstart.md` §3 completo con `curl` contra la API local y anotar en el propio quickstart cualquier desvío
- [ ] T135 [P] Actualizar `CLAUDE.md`: sección Stack (nómina: motor puro, corridas inmutables, semilla de conceptos y parámetros con vigencia, retiro del cálculo preliminar) y la tabla de Totales remedida con los comandos indicados
- [ ] T136 Retirar las redirecciones 308 de `API/Endpoints/Payroll/PayrollProcessingEndpoints.cs` si ningún consumidor las usa (buscar en `SHR/` y MAUI), o documentar su fecha de retiro en `contracts/api.md` §7

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: sin dependencias.
- **Phase 2 (Foundational)**: depende de Phase 1; **bloquea** todas las historias. Dentro:
  entidades (T008–T015) → configuraciones (T016–T023) → contexto (T024–T025) →
  migraciones en orden (T026 → T027 → T028 → T029); semillas y permisos (T030–T035)
  tras T024; motor (T036–T043) sólo depende de Phase 1; servicios (T044–T048) tras
  T024 y T042; planes (T049–T053) tras T024 y T047; retiro del legado (T054) al final.
- **Phase 3 (US1)** y **Phase 4 (US2)**: dependen de Phase 2. US2 usa las novedades de
  US1 en su prueba independiente, pero no depende de su código (el motor calcula sin
  novedades).
- **Phase 5 (US3)**, **Phase 6 (US4)**: dependen de Phase 2; US4 depende de US2
  (necesita corridas para explicar y comparar).
- **Phase 7 (US5)**: depende de US2 (corrida aprobada) y de T005 (adjuntos).
- **Phase 8 (US6)**: depende de US1 (novedades) y de US2 para la materialización
  (T119 toca `CalculationInputLoader` y `ApprovePayrollRunCommand`).
- **Phase 9 (US7)**: depende de US2 y US5 (el bloqueo por pagos).
- **Phase 10**: al final.

### User Story Dependencies

- **US1 (P1)**: sólo Phase 2.
- **US2 (P1)**: sólo Phase 2 (aprueba sin novedades si no hay).
- **US3 (P2)**: sólo Phase 2 (la semilla ya provee conceptos; US3 los edita).
- **US4 (P2)**: US2.
- **US5 (P2)**: US2.
- **US6 (P3)**: US1 y US2.
- **US7 (P3)**: US2 y US5.

### Within Each User Story

- Pruebas de handler antes del handler (fallan primero).
- Comandos y queries → endpoints → `NominaClient` → pantalla → guía del manual →
  prueba de integración.
- Cada historia termina en su checkpoint antes de la siguiente.

### Parallel Opportunities

- Phase 1: T001–T007 todas en paralelo.
- Phase 2: T008–T015 en paralelo; T016–T023 en paralelo; T030–T033 y T035 en paralelo;
  T036–T041 en paralelo (T042 los integra); T045–T048 en paralelo con T044.
- Tras Phase 2, con varias personas: A → US1, B → US2, C → US3 a la vez; después
  A → US6, B → US4, C → US5; y US7 al cierre.
- Dentro de cada historia, las tareas marcadas [P] (pruebas, queries, componentes,
  guías) van en paralelo con la tarea de comando principal.

---

## Parallel Example: User Story 2

```bash
# Pruebas primero, en paralelo:
Task: "T068 Casos dorados 6–12 en tests/IngenIA365ERP.Domain.Tests/Payroll/Calculation/Casos/"
Task: "T069 CalculatePayrollRunCommandHandlerTests.cs"
Task: "T070 ApprovePayrollRunCommandHandlerTests.cs"

# Luego, en paralelo con T071 (comando de cálculo):
Task: "T072 RunQueries.cs"
Task: "T076 PanelDeAprobacion.razor"
Task: "T078 guía liquidacion-de-nomina en ManualCatalogo.cs"

# Secuencial al final: T073 (aprobar) → T074 (endpoints) → T075 (cliente) → T077 (pantalla) → T079 (integración)
```

---

## Implementation Strategy

### MVP First (US1 + US2)

1. Phase 1 y Phase 2 completas (compila, migra, siembra, casos dorados iniciales,
   VIII y XI en verde).
2. Phase 3 (US1) → verificar sola.
3. Phase 4 (US2) → verificar el ciclo completo con `curl` (quickstart §3 hasta la
   aprobación) y con la contadora frente a los casos dorados.
4. **Parar y validar** con una nómina real de la cooperativa de prueba en QA antes de
   seguir: es el momento de descubrir un caso que la spec no previó.

### Incremental Delivery

1. MVP (US1 + US2) → DEV/QA.
2. US3 (autonomía ante cambios legales) y US4 (explicación y comparativo) → DEV/QA.
3. US5 (relación de pago, PDF, envío) → DEV/QA.
4. US6 y US7 → DEV/QA.
5. `develop → release` con aprobación manual en Argo cuando QA convenza; antes, el
   runbook de T131 sobre la base de producción (semilla, `NM`, permisos, `CREATEDB`).

### Parallel Team Strategy

Con tres personas: todos en Phase 1 + 2 (repartiendo entidades/configuraciones,
motor y semillas); luego A → US1 → US6, B → US2 → US4 → US7, C → US3 → US5.

---

## Notes

- Ningún porcentaje ni tope legal en código: si una tarea necesita uno, es un
  parámetro con vigencia en la semilla (T032) y un código en `LegalParameterCodes`.
- Nada se borra: novedades se sustituyen o anulan; corridas se reemplazan; pagos se
  revierten con motivo.
- Todo comando nuevo lleva validador o el test de arquitectura VIII lo caza.
- Cada pantalla nueva o reescrita actualiza su guía en `ManualCatalogo`.
- Verificar lo desplegado contra la base del ambiente, no contra el repositorio.
