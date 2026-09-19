# Tasks: Contabilidad NIIF — plan de cuentas, comprobantes, integración e informes

**Input**: Design documents from `/specs/009-contabilidad-niif/`
**Prerequisites**: plan.md, spec.md (13 historias), research.md (R1–R20), data-model.md,
contracts/api.md, contracts/contabilizacion.md, quickstart.md

**Tests**: incluidos — la spec los exige (SC-004, SC-006, SC-010, SC-013 «comprobado por pruebas
automáticas») y el plan los lista por historia. Convención de la casa: pruebas de handler en
`Application.Tests` (InMemory + NSubstitute), de arquitectura por escaneo de fuente, e2e sobre
`NominaE2E.PrepararAsync` (colección «Nomina e2e»).

**Organization**: por historia, agrupadas en las **cuatro entregas** del plan (R17). Cada entrega
termina con un checkpoint de merge a `develop` y verificación en QA (`quickstart.md`); la
siguiente arranca desde `develop`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralelizable (archivos distintos, sin dependencia de tareas incompletas)
- **[Story]**: US1…US13 según spec.md; Setup, Foundational, cierres de entrega y Polish sin etiqueta
- Rutas exactas en cada tarea; `Application` = `src/Core/IngenIA365ERP.Application`, `Domain` =
  `src/Core/IngenIA365ERP.Domain`, `Persistence` = `src/Infrastructure/IngenIA365ERP.Persistence`,
  `API` = `src/Presentation/IngenIA365ERP.API`, `Shared` = `src/Presentation/IngenIA365ERP.Shared`,
  `AppTests` = `tests/IngenIA365ERP.Application.Tests`, `ArchTests` =
  `tests/IngenIA365ERP.Architecture.Tests/Principles`, `E2E` = `tests/IngenIA365ERP.API.IntegrationTests`

---

# ENTREGA E1 — Núcleo + Nómina (rama `009-contabilidad-niif`)

## Phase 1: Setup (infraestructura compartida)

**Purpose**: piezas transversales que el módulo necesita y que no existen (R3, R11, R13) o que
hoy están rotas (409 de concurrencia).

- [X] T001 Agregar `Syncfusion.Blazor.TreeGrid` 33.2.8 en `src/Presentation/IngenIA365ERP.Shared/IngenIA365ERP.Shared.csproj` y `@using Syncfusion.Blazor.TreeGrid` en `src/Presentation/IngenIA365ERP.Shared/_Imports.razor`
- [X] T002 [P] Mover `TablaExportable`, `ColumnaExportable`, `FilaExportable`, `TipoDeColumna` a `Application/Common/Reports/TablaExportable.cs` (namespace `IngenIA365ERP.Application.Common.Reports`) y actualizar los `using` en `Application/Payroll/Reports/*.cs`, `API/Reports/Exportadores/ExportadorDeTablas.cs`, `API/Endpoints/Reports/PayrollReportsEndpoints.cs` y `Shared/Services/Nomina/NominaClient.Reportes.cs`
- [X] T003 [P] Extraer `Entregar` de `API/Endpoints/Reports/PayrollReportsEndpoints.cs` a `API/Reports/EntregaDeInformes.cs` (`static Task<IResult> EntregarAsync(Result<TablaExportable>, string? formato, string nombreBase)`) y usarlo desde nómina
- [X] T004 [P] Crear `ITabularFileReader` (+ `TablaLeida`, `FilaLeida`, `CeldaLeida`) en `Application/Common/Interfaces/Files/ITabularFileReader.cs` y `ClosedXmlTabularFileReader` (xlsx con ClosedXML; csv/txt con BCL, delimitador detectado, `HeaderRows`) en `API/Reports/Importadores/ClosedXmlTabularFileReader.cs`; registrar en `API/Program.cs`
- [X] T005 [P] Crear `IReintentableAnteConcurrencia` y `ReintentoPorConcurrenciaBehavior<,>` (hasta 5 intentos con jitter; `DescartarCambios()` entre intentos) en `Application/Common/Behaviors/ReintentoPorConcurrenciaBehavior.cs`; agregar `void DescartarCambios()` a `Application/Common/Interfaces/IApplicationDbContext.cs`, `Persistence/DbContext/ApplicationDbContext.cs` (`ChangeTracker.Clear()`) y `AppTests/Common/TestDbContextFactory.cs`; registrar el behavior después de `ValidationBehavior` en `Application/DependencyInjection.cs`
- [X] T006 [P] Mapear `ConcurrencyConflictException` a `409 { code: "Concurrency.StaleRowVersion" }` en el manejador global de `API/Program.cs` (hoy sale como 500 `Generic.Unexpected`)
- [X] T007 [P] Crear `AccountingPermissionCatalogSeeder` (30 códigos de `contracts/api.md` §1, filtro `Resource.StartsWith("Accounting.")`) en `src/Infrastructure/IngenIA365ERP.Identity/Seed/AccountingPermissionCatalogSeeder.cs`; llamarlo desde `PhaseZeroSecuritySeeder.cs` antes de `BuiltInRolesSeeder`; en `BuiltInRolesSeeder.cs` sumar `Accounting.Vouchers.Create` y `Accounting.Reports.Export` a Operator, `Accounting.Reports.Export` a Auditor, y `Accounting.Accounts.View`, `Accounting.VoucherTypes.View` a `LecturaDeMaestros`
- [X] T008 [P] Agregar a `Application/Common/Audit/AuditEventTypes.cs` las constantes `Accounting.Report.Exported`, `Accounting.Certificate.Sent`, `Accounting.Setup.Initialized`, `Accounting.Catalog.Validated`, `Navigation.Opened`; crear `AccountingAuditEmitter` (molde `PayrollAuditEmitter`, `Module = "Accounting"`) en `Application/Accounting/Reports/AccountingAuditEmitter.cs` y registrarlo en `Application/DependencyInjection.cs`
- [X] T009 [P] Verificar en el clúster la versión de MongoDB y que admita dos índices TTL sobre `occurredAt` con `partialFilterExpression` distintas (si no: un solo TTL de 10 años y purga programada de los demás módulos a 5, documentando cuál quedó); luego en `src/Infrastructure/IngenIA365ERP.Audit/Indexes/AuditIndexBootstrap.cs` crear el índice TTL parcial `ttl_occurredAt_10y_contable` (10 años, `partialFilterExpression: module in ["Accounting","Navigation"]`) y excluir esos módulos del índice de 5 años
- [X] T010 [P] Pruebas `ReintentoPorConcurrenciaBehaviorTests` (reintenta N veces, descarta cambios, propaga tras el último) en `AppTests/Common/ReintentoPorConcurrenciaBehaviorTests.cs`

---

## Phase 2: Foundational (bloqueante para todas las historias)

**Purpose**: retirar el modelo heredado, crear el dominio nuevo, la migración, las semillas, el
contrato único y dejar la solución compilando con los módulos conectados o en pausa explícita.

**⚠️ CRITICAL**: ninguna historia empieza hasta que T046 (build + pruebas) esté verde.

- [X] T011 Borrar el modelo heredado: las 33 clases de `Domain/Entities/Accounting/*.cs` (AccountBalance, AccountGroup, AccountSubgroup, AccountingDocument, AccountingPeriod, Amortization, AuxiliaryDocument, BankReconciliation, BankReconciliationFlat, BankReconciliationMaster, Budget, ChartOfAccount, Depreciation, DianReportFormat, ExchangeRateHistory, FinancialReport, FinancialReportParam, FinancialReportValue, FiscalPeriod, GmfTaxLine, GroupName, IcaTaxLine, IncomeTaxLine, JournalEntry, JournalEntryItem, RiskCategory, StampTax, SubgroupName, TaxFormCode, ThirdPartyAccount, VatTaxLine, VoucherType, WithholdingTaxLine), sus configuraciones en `Persistence/Configurations/Accounting/`, sus DbSets en `Application/Common/Interfaces/IApplicationDbContext.cs` y `Persistence/DbContext/ApplicationDbContext.cs`, las carpetas `Application/Accounting/*` (todas), `API/Endpoints/Accounting/*.cs` (20), `API/Endpoints/Reports/AccountingReportsEndpoints.cs`, `API/Reports/{BalanceSheetReport,IncomeStatementReport,GeneralLedgerReport}.cs`, `Shared/Pages/Contabilidad/*.razor` (22) y `src/Infrastructure/IngenIA365ERP.Legacy/Adapters/AccountingLegacyAdapter.cs`, y las pruebas que dependen de ese modelo: `AppTests/Accounting/**` (todas) y `E2E/Accounting/CreateDocumentEndpointTests.cs`
- [X] T012 [P] Crear enums en `Domain/Entities/Accounting/Enums/`: `AccountingModules` (flags: Accounting=1, Payroll=2, Lending=4, Inventory=8, Treasury=16, Savings=32, Assets=64), `DocumentStatus`, `DocumentKind`, `VoucherUsage`, `TaxKind`, `AccountOrigin`, `PeriodStatus`, `CatalogSource`
- [X] T013 [P] Crear `AccountingSetup`, `AccountCatalog`, `AccountCatalogEntry`, `FinancialStatementItem` en `Domain/Entities/Accounting/` según data-model.md §1
- [X] T014 [P] Crear `ChartOfAccount` (reescrita: Code, Name, Level, Nature, ParentId, NiifItemCode, Origin, IsMovement, IsActive, FirstMovementAt, EnabledModules, Requires*, BankId, BankAccountNumber, TaxKind, TaxConceptCode, RequiresTaxBase) y `AccountTaxRate` en `Domain/Entities/Accounting/`
- [X] T015 [P] Crear `VoucherType` (reescrita: Code, Name, Usage, ModuleCode, NextNumber, IsActive, IsSeeded) y `CrossDocumentType` en `Domain/Entities/Accounting/`
- [X] T016 [P] Crear `FiscalYear` y `AccountingPeriod` (reescrita) en `Domain/Entities/Accounting/`
- [X] T017 [P] Crear `AccountingDocument` y `JournalEntry` en `Domain/Entities/Accounting/Transactions/` (namespace `IngenIA365ERP.Domain.Entities.Accounting.Transactions`, cubierto por `PrincipioXI`) según data-model.md §3
- [X] T018 [P] Agregar columnas en otros módulos: `Branch.TenantBranchPublicId` (`Domain/Entities/Core/Branch.cs`), `PersonId` en `Domain/Entities/Core/Bank.cs` y en `Domain/Entities/Payroll/{HealthInsuranceProvider,WorkRiskProvider,PensionProvider,SeveranceProvider,FamilyCompensationFund}.cs`, `DebitAccountId`/`CreditAccountId` en `Domain/Entities/Treasury/TreasuryConcept.cs`, `ProvisionExpenseAccountCode`/`ProvisionAccountCode` en `Domain/Entities/Lending/CreditLineParameter.cs`, `InterestExpenseAccount` en `Domain/Entities/Lending/SavingsParameter.cs` y `Domain/Entities/CDT/CdtParameter.cs`, `AccountingDocumentId` en `Domain/Entities/Inventory/InventoryDocument.cs`, con sus configuraciones en `Persistence/Configurations/{Core,Payroll,Treasury,Lending,CDT,Inventory}/`
- [X] T019 Crear las configuraciones EF de las entidades nuevas en `Persistence/Configurations/Accounting/*.cs` (tablas, índices y únicos filtrados en sintaxis T-SQL —`Code` de cuentas y de catálogos únicos sólo entre no eliminadas, `[IsDeleted] = 0`—, precisión 9,4 en tarifas, `Restrict` en `JournalEntry → AccountingDocument`) según data-model.md
- [X] T020 [P] Crear las entidades de E4 según data-model.md §4–§8 en `Domain/Entities/Accounting/` (clases completas desde ahora para que la migración las incluya de una vez): `BankStatementColumnMap`, `BankReconciliation`, `BankStatementLine`, `Budget`, `BudgetLine`, `WithholdingCertificate`, `WithholdingCertificateLine`, `TaxForm`, `TaxFormLine`, `ExogenousFormat`, `ExogenousConcept`, `ExogenousConceptAccount`, `ExogenousRun`, `ExogenousRunLine`, `FixedAsset`, `FixedAssetInstallment`, `AssetRun` (+ configuraciones en `Persistence/Configurations/Accounting/`)
- [X] T021 Declarar los DbSets nuevos en `Application/Common/Interfaces/IApplicationDbContext.cs` y `Persistence/DbContext/ApplicationDbContext.cs` (AccountingSetups, AccountCatalogs, AccountCatalogEntries, FinancialStatementItems, ChartOfAccounts, AccountTaxRates, VoucherTypes, CrossDocumentTypes, FiscalYears, AccountingPeriods, AccountingDocuments, JournalEntries, BankStatementColumnMaps, BankReconciliations, BankStatementLines, Budgets, BudgetLines, WithholdingCertificates, WithholdingCertificateLines, TaxForms, TaxFormLines, ExogenousFormats, ExogenousConcepts, ExogenousConceptAccounts, ExogenousRuns, ExogenousRunLines, FixedAssets, FixedAssetInstallments, AssetRuns) — depende de T012–T020
- [X] T022 Generar la migración en par `ContabilidadNiif` con `tools/scripts/add-migration.ps1 -Name ContabilidadNiif -Context Application` y editar a mano `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/Application/*_ContabilidadNiif.cs` y `…Migrations.SqlServer/Application/*_ContabilidadNiif.cs`: cabecera con `MIGRACION-DESTRUCTIVA-APROBADA`, referencia del respaldo y segundo revisor; `Up()` empieza con la guarda (`RAISE EXCEPTION`/`THROW` si `ACC_Documents` o `ACC_JournalEntries` tienen filas); `DropForeignKey` de `PAY_PayrollRuns` y `PAY_PayrollConceptDefinitionAccounts`; `DropTable` de las 33; `CreateTable`; `AddForeignKey`; columnas de T018; `Down()` documentado (recrea el esquema anterior vacío); `tools/scripts/check-migration-parity.ps1` verde
- [X] T023 [P] Transcribir `puc-comercial.json` (Decreto 2650: clases, grupos, cuentas y subcuentas con código, nombre, nivel, naturaleza y rubro NIIF) en `Persistence/Seeding/Parametric/Data/puc-comercial.json`
- [X] T024 [P] Transcribir `puc-solidario.json` (Catálogo Único de Información Financiera de la Supersolidaria bajo NIIF, hasta nivel 4, mismo formato) en `Persistence/Seeding/Parametric/Data/puc-solidario.json`
- [X] T025 [P] Crear `rubros-niif.json` (rubros de ESF, ERI, cambios en el patrimonio y flujo de efectivo indirecto para grupos 1, 2 y 3: código, nombre, estado, sección, orden, signo, padre) en `Persistence/Seeding/Parametric/Data/rubros-niif.json`
- [X] T026 [P] Crear `voucher-types.json` (CG manual; NM, DS, RC, CA, PV, DN, AH, CD, FV, EI, SI, CH, FP, CB reservados por módulo —CB = cuenta por cobrar de Tesorería; FV sólo Inventario; un código, un módulo—; DP activos; AP apertura; CI cierre) y `cross-document-types.json` (FV, FC, CC, NC, ND, CT, PG, OT) en `Persistence/Seeding/Parametric/Data/`
- [X] T027 Marcar los JSON como `EmbeddedResource` en `Persistence/IngenIA365ERP.Persistence.csproj`; crear `RecursoJson.Leer<T>(nombre)` en `Persistence/Seeding/Parametric/RecursoJson.cs`; crear `AccountCatalogsSeeder` (Order 58), `FinancialStatementItemsSeeder` (59), `VoucherTypesSeeder` (60, reemplaza y borra `PayrollVoucherTypeSeeder.cs`; actualizar `Persistence/Seeding/PayrollSeedApplier.cs` —«reaplicar semilla» de Conceptos— para invocarlo), `CrossDocumentTypesSeeder` (61) en `Persistence/Seeding/Parametric/` (idempotentes por clave natural, un `SELECT` de claves + un `SaveChanges`); borrar `ChartOfAccountsSeeder` de `Persistence/Seeding/Parametric/CatalogSeeders.cs`; registrar en `Persistence/DependencyInjection.cs`
- [X] T028 [P] Prueba `LasSemillasJsonSonCoherentes` (cada JSON: padre existente, longitud 1/2/4/6 por nivel, naturaleza D/C, rubro existente en `rubros-niif.json`, códigos únicos, conteo > 0; `voucher-types.json` con usos válidos) en `AppTests/Infrastructure/LasSemillasJsonSonCoherentes.cs`
- [X] T029 [P] Documentar la convención en `docs/manual/semillas-json.md` (formato, cómo revisa el contador, cómo se versiona un catálogo)
- [X] T030 [P] Crear `AccountingErrors` (todos los códigos de `contracts/contabilizacion.md` §2 y `contracts/api.md`, con mensajes en español, `data` de línea y `ErrorDeLinea` con `Severidad` (Error | Aviso)) en `Application/Accounting/Posting/AccountingErrors.cs`
- [X] T031 Crear `AccountLineRules` (clase única: recibe `CuentaParaReglas` —cuenta con reglas y tarifa vigente—, módulo origen, línea, configuración y alcance de sucursal; devuelve `IReadOnlyList<ErrorDeLinea>`; regla 10 con aviso dentro de la tolerancia y error fuera) en `Application/Accounting/Rules/AccountLineRules.cs`
- [X] T032 [P] Pruebas `AccountLineRulesTests` (una por regla de `contracts/contabilizacion.md` §2 filas 4–10, con su código) en `AppTests/Accounting/Rules/AccountLineRulesTests.cs`
- [X] T033 [P] Crear `IUserBranchScope` (`Task<AlcanceDeSucursales> ObtenerAsync(ct)`: sin restricción, o Ids + sucursal por defecto) en `Application/Common/Interfaces/Security/IUserBranchScope.cs` e implementarlo en `API/Services/UserBranchScope.cs` (lee `SEC_UserBranchAssignments` del usuario y traduce por `COR_Branches.TenantBranchPublicId`; con asignaciones sin vínculo → alcance vacío); registrar en `API/Program.cs`
- [X] T034 Crear `PostingRequest`, `PostingLine`, `AccountingOrigin` y `AccountingPoster` (`PrepareAsync`, `PrepareReversalAsync`: comprobaciones 1–12 del contrato, número desde `VoucherType.NextNumber`, `FirstMovementAt`, `Date`/`IsPosted` desnormalizados, alcance de sucursal sólo para origen `CNT`, sin guardar) en `Application/Accounting/Posting/`; registrar Scoped en `Application/DependencyInjection.cs`
- [X] T035 [P] Pruebas `AccountingPosterTests` (agrega documento y líneas sin `SaveChanges`; descuadre → nada agregado; tipo de otro módulo rechazado; período cerrado; número consecutivo; reversión invertida y referenciada; `Opening` sólo con fecha = primer período − 1) en `AppTests/Accounting/Posting/AccountingPosterTests.cs`
- [X] T036 Registrar en `AppTests/Common/TestDbContextFactory.cs` los DbSets contables nuevos (quitar `Ignore<ChartOfAccount>()`, `Ignore("RowVersion")` por entidad, navegaciones que colisionen)
- [X] T037 Crear `AccountEligibility` (`ResolverAsync(codigo|publicId, módulo) → Result<ChartOfAccount>` con `Accounting.NotInitialized`, `Account.NotEligible {accountCode, module, rule}`) en `Application/Accounting/Accounts/AccountEligibility.cs`; registrar Scoped
- [X] T038 Reescribir `Application/Payroll/Services/PayrollAccountingPoster.cs` sobre `AccountingPoster` (construye `PostingLine`s por concepto y centro, origen `("NOM","PayrollRun",run.PublicId)`, sucursal del empleado o principal; `ReverseAsync` → `PrepareReversalAsync`) y marcar `ApprovePayrollRunCommand`/`ReversePayrollRunCommand` como `IReintentableAnteConcurrencia` en `Application/Payroll/Runs/Commands/`; adaptar al modelo nuevo (documento con `Status`/`Number`/`Kind`, líneas por `DocumentId`) `AppTests/Payroll/Services/PayrollAccountingPosterTests.cs`, `AppTests/Payroll/Runs/ApprovePayrollRunCommandHandlerTests.cs` y `ReversePayrollRunCommandHandlerTests.cs` para que T046 pase
- [X] T039 Quitar la escritura contable muerta (cabeceras sin líneas) de `Application/CDT/InterestLiquidation/Commands/LiquidateCDTInterest/LiquidateCDTInterestCommand.cs`, `Application/Inventory/Documents/Commands/{CreateInventoryDocument,CreateInvoice,VoidInventoryDocument}/*.cs`, `Application/Lending/{Accruals,Classifications,PayrollDeductions,SavingsInterest}/Commands/**/*.cs`, `Application/Treasury/{Checks,Invoices}/Commands/**/*.cs`, dejando el marcador `// E3 (feature 009): contabilización por AccountingPoster pendiente` y sin ningún acceso a `AccountingDocuments`/`JournalEntries`
- [X] T040 Pasar `Application/Lending/LoanApplications/Commands/DisburseLoan/DisburseLoanCommand.cs` y `Application/Lending/Payments/Commands/ProcessPayment/ProcessPaymentCommand.cs` por `AccountingPoster` (cuentas de `CreditLineParameter` resueltas con `AccountEligibility`; banco desde `COR_Banks.AccountingAccountCode` del banco elegido; sin la cuenta «11100501»; falta → `Accounting.Parameterization.Missing`; `IReintentableAnteConcurrencia`; origen `LoanApplication`/`LoanPayment`)
- [X] T041 [P] Pruebas de arquitectura nuevas en `ArchTests/`: `NingunModuloEscribeMovimientosFueraDelContrato.cs` (fuera de `Application/Accounting/Posting` nadie hace `AccountingDocuments.Add`, `JournalEntries.Add`, `new AccountingDocument`, `new JournalEntry`) y `LaContabilidadNoTieneCuentasEnCodigo.cs` (ningún literal `"\d{4,12}"` en `Application/**` salvo `Seeding`, pruebas y una lista explícita)
- [X] T042 [P] Ampliar pruebas de arquitectura existentes: `ArchTests/PrincipioVIII_DualValidation.cs` (`InScopeNamespacePrefixes` += `IngenIA365ERP.Application.Accounting`, `IngenIA365ERP.Application.Audit`), renombrar `ArchTests/LosMaestrosDePersonaExigenPermiso.cs` → `LosEndpointsProtegidosExigenPermiso.cs` con el glob `Endpoints/Accounting/*.cs` y `Endpoints/Reports/AccountingReportsEndpoints.cs`, `ArchTests/LasPantallasDicenQueEstanCargando.cs` (`ModulosMigrados` += `"Contabilidad"`)
- [X] T043 [P] Crear `Shared/Services/Contabilidad/ContabilidadDtos.cs` (espejos de `contracts/api.md`: configuración, catálogo, cuenta, reglas, tipo de comprobante, período, documento y línea, errores de línea) y `Shared/Services/Contabilidad/ContabilidadClient.cs` (partial; `EnviarAsync`, `DescargarAsync`, molde `NominaClient`); registrar en `src/Presentation/IngenIA365ERP.Web/Program.cs` y `src/Presentation/IngenIA365ERP.Web.Client/Program.cs`
- [X] T044 [P] Reescribir el grupo Contabilidad de `Shared/Layout/NavMenu.razor` con las rutas de E1 (`/contabilidad/configuracion`, `/contabilidad/catalogos`, `/contabilidad/plan-de-cuentas`, `/contabilidad/tipos-de-comprobante`, `/contabilidad/periodos`, `/contabilidad/comprobantes`, `/contabilidad/borradores`, `/contabilidad/parametrizaciones-invalidas`) y agregar en `Shared/wwwroot/css/componentes.css` las clases `.grilla-lineas`, `.celda-error`, `.celda-aviso`, `.pie-de-totales`, `.arbol-cuentas`, `.guia-de-pasos`, `.marca-origen`
- [X] T045 [P] Mover `src/Presentation/IngenIA365ERP.Web.Client/Components/AttachmentList.razor` a `Shared/Components/Shared/AttachmentList.razor` (y `downloadFromStream` a `Shared/wwwroot/js/descargas.js` referenciado con `@Assets` en `App.razor`) para que los comprobantes lo usen
- [X] T046 Compuerta: `dotnet build IngenIA365ERP.CI.slnf -c Release` sin errores; `dotnet test tests/IngenIA365ERP.Application.Tests` y `tests/IngenIA365ERP.Architecture.Tests` verdes; `tools/IngenIA365ERP.DbMigrator` aplica `ContabilidadNiif` en local y `SEC_Permissions` tiene 30 códigos `Accounting.*`

**Checkpoint**: dominio, migración, semillas y contrato listos; Nómina y Cartera (desembolso/recaudo) contabilizan por el contrato; los demás módulos en pausa explícita hasta E3.

---

## Phase 3: User Story 1 — Iniciar la contabilidad con el PUC elegido (Priority: P1) 🎯 MVP

**Goal**: configuración inicial por empresa; plan hasta nivel 4 desde el catálogo; importador de
catálogo propio; bloqueo tras la primera auxiliar.

**Independent Test**: iniciar una cooperativa de prueba con Solidario y nivel 6; el plan tiene
todas las cuentas del catálogo, ninguna de movimiento; cambiar el nivel tras crear una auxiliar
es rechazado; importar un catálogo con una fila sin padre falla señalándola.

- [X] T047 [P] [US1] Crear `InitializeAccountingCommand` (+ validador; crea `AccountingSetup`, copia el catálogo a `ACC_ChartOfAccounts` en bloque, crea `FiscalYear` y 12 períodos; `Accounting.Setup.AlreadyInitialized`, `.CatalogNotFound`, `.LengthsInvalid`) en `Application/Accounting/Setup/InitializeAccountingCommand.cs`
- [X] T048 [P] [US1] Crear `UpdateAccountingSetupCommand` (+ validador; catálogo/nivel/longitudes sólo si no está bloqueada —cambiar de catálogo = soft-delete de las cuentas `Origin = Catalog` + copia nueva + `CatalogId`, auditado— → `Accounting.Setup.Locked` con `companyAccounts`, `firstMovementAt`; cuatro ojos, cuenta de resultado, sucursal principal y tolerancias siempre) en `Application/Accounting/Setup/UpdateAccountingSetupCommand.cs`
- [X] T049 [P] [US1] Crear `GetAccountingSetupQuery`, `ListAccountCatalogsQuery`, `GetCatalogEntriesQuery`, `ListCatalogUpdatesQuery` (+ validadores) en `Application/Accounting/Setup/SetupQueries.cs`
- [X] T050 [P] [US1] Crear `ImportAccountCatalogCommand` (+ validador; `ITabularFileReader`; reglas de `AccountCatalogEntry`; errores por fila `Accounting.Catalog.Invalid { errors[] }`; nada a medias; `Source = Imported`, `Code = PROPIO-{n}`), `ValidateCatalogCommand` (`ValidatedAt/By`, evento `Accounting.Catalog.Validated`) y `AdoptCatalogUpdatesCommand` en `Application/Accounting/Setup/`
- [X] T051 [P] [US1] Pruebas `InitializeAccountingCommandHandlerTests` (catálogo completo; ninguna de movimiento; períodos; ya iniciada), `UpdateAccountingSetupCommandHandlerTests` (bloqueo con auxiliar; con movimiento; cuatro ojos siempre; cambiar de catálogo dos veces y volver al primero no viola el único de `Code`) e `ImportAccountCatalogCommandHandlerTests` (fila sin padre; longitud; duplicado; nada a medias) en `AppTests/Accounting/Setup/`
- [X] T052 [US1] Crear `API/Endpoints/Accounting/SetupEndpoints.cs` (`/api/accounting/setup`: GET, POST initialize, PUT, catalogs, entries, import multipart, validate, updates, adopt) con `RequirePermission("Accounting.Setup.View|Manage")` por tramo y `ErrorEnvelopeFilter`
- [X] T053 [P] [US1] Crear `Shared/Services/Contabilidad/ContabilidadClient.Configuracion.cs` (obtener, iniciar, actualizar, catálogos, entradas, importar archivo, validar, novedades, adoptar)
- [X] T054 [US1] Crear `Shared/Pages/Contabilidad/ConfiguracionInicial.razor` (`/contabilidad/configuracion`: formulario con catálogo, nivel, longitudes, grupo NIIF, ejercicio, sucursal principal, cuatro ojos; estado «Iniciada con…»; motivo del bloqueo; `PermissionGate Required="Accounting.Setup.View" MostrarAviso`; `IndicadorDeCarga` + `EstadoDeCarga` en carga y guardado; `_saving`)
- [X] T055 [US1] Crear `Shared/Pages/Contabilidad/Catalogos.razor` (`/contabilidad/catalogos`: lista de catálogos con versión, conteo y validación; entradas por nivel en `SfTreeGrid`; diálogo de importación con errores por fila; botón «Validar» para el contador; novedades de versión con «Incorporar»)
- [ ] T056 [US1] Actualizar `E2E/Payroll/NominaE2E.cs` (`PrepararAsync`: `POST /api/accounting/setup/initialize` con Solidario y nivel 6, creación de auxiliares de movimiento con reglas para nómina, períodos ya no se crean a mano) y crear `E2E/Accounting/ContabilidadNiifTests.cs` con la parte de US1 (inicializar; segundo intento 422; bloqueo tras auxiliar; importar catálogo con error de fila → 422 con `row`; importar un catálogo generado de 2 000 filas termina en < 1 min, SC-001)

**Checkpoint**: una cooperativa nueva queda con su plan hasta nivel 4 en menos de 2 minutos (SC-001).

---

## Phase 4: User Story 2 — Parametrizar las auxiliares y sus reglas (Priority: P1)

**Goal**: auxiliares de nivel 5/6 con reglas, bloqueo por movimientos, eliminación protegida,
buscador que sólo ofrece cuentas de movimiento habilitadas, parametrizaciones inválidas.

**Independent Test**: crear una auxiliar sólo para Nómina que exige tercero; Cartera no puede
elegirla y Nómina sí; tras un movimiento sus reglas no cambian; eliminar una referenciada muestra
dónde.

- [X] T057 [P] [US2] Crear `CreateAccountCommand` (+ validador; prefijo del padre, longitud por nivel, nivel ≤ movimiento, `IsMovement`, `CodigoDeCatalogo`, herencia de naturaleza y rubro), `UpdateAccountCommand` (bloqueo FR-012 → `Accounting.Account.Locked { firstMovementAt }`), `SetAccountActiveCommand`, `DeleteAccountCommand` (`Accounting.Account.HasMovements`, `.Referenced { references }` recorriendo las tablas de data-model.md §1); `UpdateAccountCommand` y `DeleteAccountCommand` rechazan cuentas `Origin = Catalog` con `Accounting.Account.FromCatalog` (sólo `SetAccountActiveCommand` las toca, FR-007) en `Application/Accounting/Accounts/`
- [X] T058 [P] [US2] Crear `GetAccountTreeQuery` (hijos por padre), `SearchAccountsQuery` (código o nombre; sólo movimiento, activas y habilitadas para `module`), `GetAccountByPublicIdQuery` (ficha, reglas, tarifas, referencias), `GetAccountHistoryQuery` (eventos de auditoría de la cuenta vía `IAuditService`), `ListInvalidParameterizationsQuery` (cuentas de agrupación/inactivas/no habilitadas en `PAY_PayrollConceptDefinitionAccounts` y vínculos institucionales faltantes; las demás tablas se suman en E3) en `Application/Accounting/Accounts/AccountQueries.cs`
- [X] T059 [P] [US2] Agregar el arma `"cuentas"` (por `Code`) a `Application/Common/Catalogos/BuscarCodigoDeCatalogoQuery.cs`
- [X] T060 [P] [US2] Pruebas `CreateAccountCommandHandlerTests` (nivel 6 con movimiento 5 rechazado; prefijo; longitud; duplicado; herencia; editar o eliminar una cuenta del catálogo → `FromCatalog`), `UpdateAccountCommandHandlerTests` (con movimiento en el ejercicio sólo nombre/activa), `DeleteAccountCommandHandlerTests` (referenciada por concepto de nómina), `SearchAccountsQueryHandlerTests` (no ofrece agrupación ni inactivas ni de otro módulo) en `AppTests/Accounting/Accounts/`
- [X] T061 [US2] Crear `API/Endpoints/Accounting/AccountsEndpoints.cs` (`/api/accounting/accounts`: tree, search, {id}, history, POST, PUT, deactivate/activate, DELETE, invalid-parameterizations) con `Accounting.Accounts.View|Manage` por tramo
- [X] T062 [US2] Validar la cuenta al guardar cuentas por concepto de nómina con `AccountEligibility` en el comando de `Application/Payroll/Concepts/` que escribe `PayrollConceptDefinitionAccount` (agrupación/inactiva/no habilitada → `Accounting.Account.NotEligible`) y devolver las reglas de la cuenta en su consulta para que la pantalla las muestre
- [X] T063 [P] [US2] Crear `Shared/Services/Contabilidad/ContabilidadClient.Cuentas.cs` y los componentes `Shared/Components/Contabilidad/BuscadorDeCuenta.razor` (`SfComboBox` con búsqueda remota por módulo, muestra código y nombre, expone `Cuenta` con sus reglas), `ReglasDeCuenta.razor` (sección del formulario: módulos, exige tercero/documento/centro/sucursal, banco, impuesto) y `ArbolDeCuentas.razor` (`SfTreeGrid` con hijos por demanda, filtro activas, acciones por nodo)
- [X] T064 [US2] Crear `Shared/Pages/Contabilidad/PlanDeCuentas.razor` (`/contabilidad/plan-de-cuentas`: árbol, diálogo crear/editar con `CampoCodigo Catalogo="cuentas"` y `ReglasDeCuenta`, bloqueo explicado, historial de cambios, inactivar/reactivar, eliminar con referencias; `PermissionGate` en botones; `IndicadorDeCarga` en árbol y diálogo) y `Shared/Pages/Contabilidad/ParametrizacionesInvalidas.razor` (`/contabilidad/parametrizaciones-invalidas`)
- [X] T065 [US2] Mostrar las reglas de la cuenta elegida (`ReglasDeCuenta` en modo lectura) y usar `BuscadorDeCuenta` con `Modulo="Payroll"` en la pantalla de cuentas por concepto de `Shared/Pages/Nomina/` (la que hoy pide códigos de cuenta a mano)
- [ ] T066 [US2] Ampliar `E2E/Accounting/ContabilidadNiifTests.cs` con US2 (auxiliar sólo Nómina: parametrizar en nómina OK; buscador con `module=Lending` no la devuelve; tras contabilizar un comprobante, `PUT` de reglas → 422 `Accounting.Account.Locked`; `DELETE` referenciada → 422 con `references`)

**Checkpoint**: las reglas viven en la cuenta y las hace cumplir el contrato (SC-002 para Nómina y digitación).

---

## Phase 5: User Story 3 — Digitar comprobantes manuales en una ventana guiada (Priority: P1)

**Goal**: tipos de comprobante y períodos mensuales; borradores, validación campo a campo,
contabilizar con número y cuatro ojos, reversión, impresión, soportes, teclado de principio a fin.

**Independent Test**: registrar 20 líneas sólo con teclado con cuentas que exigen tercero,
documento y centro; ninguna incompleta pasa; diferencia en vivo; al contabilizar no se edita;
dos usuarios contabilizan a la vez y reciben números consecutivos.

- [X] T067 [P] [US3] Crear comandos y consultas de tipos de comprobante y documentos cruce (`CreateVoucherTypeCommand`, `UpdateVoucherTypeCommand`, `SetVoucherTypeActiveCommand`, `ListVoucherTypesQuery`; ídem `CrossDocumentType`; sembrados no cambian de uso → `Accounting.VoucherType.Seeded`) en `Application/Accounting/VoucherTypes/` y `Application/Accounting/CrossDocumentTypes/`; armas `"tipos-comprobante"` y `"documentos-cruce"` en `BuscarCodigoDeCatalogoQuery.cs`
- [X] T068 [P] [US3] Crear `ListPeriodsQuery`, `ClosePeriodCommand` (sin borradores → `Accounting.Period.HasDrafts { drafts[] }`), `ReopenPeriodCommand` (motivo; marca conciliaciones `Outdated`) y `OpenFiscalYearCommand` (+ validadores) en `Application/Accounting/Periods/`
- [X] T069 [P] [US3] Crear `SaveDraftDocumentCommand` (crea/actualiza borrador con errores permitidos; sólo tipos `Manual`; `RegisteredByUserId`; actualiza líneas por `LineNumber` y marca `IsDeleted` las sobrantes, nunca `Remove` —lo vigila `PrincipioXI`—), `DiscardDraftCommand` (soft-delete del borrador), `ValidateDraftQuery` (misma `AccountLineRules`, sin guardar; totales y diferencia), `PostDocumentCommand` (`AccountingPoster.PrepareAsync` sobre las líneas del borrador; cuatro ojos → `Accounting.Document.FourEyes`; `IReintentableAnteConcurrencia`), `ReverseDocumentCommand` (motivo; `Accounting.Document.NotPosted|AlreadyReversed|IsReversal|ModuleOwned`; fecha en período abierto o primer abierto) en `Application/Accounting/Documents/`
- [X] T070 [P] [US3] Crear `GetDocumentQuery` (cabecera, líneas, origen con enlace por `SourceType`, reversión, adjuntos), `ListDocumentsQuery` (filtros y alcance de sucursal), `GetDocumentBySourceQuery`, `GetMyDraftsQuery`, `PrintDocumentQuery` (+ validadores) en `Application/Accounting/Documents/DocumentQueries.cs`; reescribir `API/Reports/VoucherPrintReport.cs` (origen, quién registró y contabilizó, reversión)
- [X] T071 [P] [US3] Pruebas `SaveDraftDocumentCommandHandlerTests` (tipo de módulo rechazado; guarda con errores), `ValidateDraftQueryHandlerTests` (errores con `lineNumber` y `field`), `PostDocumentCommandHandlerTests` (cuatro ojos activo mismo usuario → rechazo; otro usuario OK con `PostedByUserId`; descuadre; período cerrado; número), `ReverseDocumentCommandHandlerTests` (dos veces; reversión de reversión; de módulo), `ClosePeriodCommandHandlerTests` (con borradores) en `AppTests/Accounting/Documents/` y `AppTests/Accounting/Periods/`
- [X] T072 [US3] Crear `API/Endpoints/Accounting/VoucherTypesEndpoints.cs`, `CrossDocumentTypesEndpoints.cs`, `PeriodsEndpoints.cs` (`/api/accounting/periods`: GET, years, close, reopen) y `DocumentsEndpoints.cs` (`/api/accounting/documents`: list, {id}, drafts POST/PUT/DELETE, validate, post, reverse, print, by-source, my-drafts) con permisos por tramo según `contracts/api.md`
- [X] T073 [P] [US3] Crear `Shared/Services/Contabilidad/ContabilidadClient.Documentos.cs` y `ContabilidadClient.Periodos.cs`; agregar el parámetro `Compacto` a `Shared/Components/Shared/PersonSearchPicker.razor` (una línea, sin ayuda, para celdas de grilla)
- [X] T074 [US3] Crear `Shared/Components/Contabilidad/LineasDeComprobante.razor` (tabla `.grilla-lineas`; por celda `BuscadorDeCuenta`, `PersonSearchPicker Compacto`, `SfComboBox` documento cruce/centro/sucursal, `SfNumericTextBox` débito/crédito/base, detalle; habilita/deshabilita campos por reglas de la cuenta; validación al salir del campo con debounce vía `/validate`; `@onkeydown` Tab/Enter/Ctrl+D duplicar/Ctrl+B balancear/F2 ayuda; repetir tercero/centro/sucursal de la línea anterior; foco por `ElementReference`; errores por celda `.celda-error`; avisos con `.celda-aviso` que no bloquean)
- [X] T075 [US3] Crear `Shared/Pages/Contabilidad/Comprobante.razor` (`/contabilidad/comprobantes/nuevo` y `/{id:guid}`: cabecera, `LineasDeComprobante`, pie de totales y diferencia, panel de ayuda por campo y guía de pasos, lista de errores con enlace a la línea, «Guardar borrador», «Contabilizar» (`PermissionGate Accounting.Vouchers.Post`), «Anular» con motivo (`Accounting.Vouchers.Void`), «Imprimir», `AttachmentList` con `OwnerEntityType="AccountingDocument"`; modo sólo lectura con «Ver en {módulo}» cuando el origen es un módulo; `IndicadorDeCarga`; `_saving`)
- [X] T076 [US3] Crear `Shared/Pages/Contabilidad/Comprobantes.razor` (`/contabilidad/comprobantes`: lista con filtros de fecha, tipo, estado, origen, número; `IndicadorDeCarga`), `Shared/Pages/Contabilidad/Borradores.razor` (`/contabilidad/borradores`), `Shared/Pages/Contabilidad/TiposDeComprobante.razor` (`/contabilidad/tipos-de-comprobante`, con `CampoCodigo Catalogo="tipos-comprobante"` y documentos cruce en pestaña) y `Shared/Pages/Contabilidad/Periodos.razor` (`/contabilidad/periodos`: ejercicio, doce meses, cerrar/reabrir con motivo)
- [ ] T077 [US3] Ampliar `E2E/Accounting/ContabilidadNiifTests.cs` con US3 (`/validate` marca tercero faltante; borrador descuadrado se guarda y no se contabiliza; dos clientes contabilizan en paralelo → números consecutivos distintos; cuatro ojos rechaza al mismo usuario; reversar → neto cero; reversar dos veces → 422; cerrar mes con borrador → 422 con lista; tipo `NM` en digitación manual → 422)

**Checkpoint**: la operación diaria funciona de punta a punta con teclado (SC-003) y numeración sin huecos.

---

## Phase 6: User Story 4 — Nómina contabiliza en línea por el contrato (Priority: P1, parte 1)

**Goal**: aprobar/reversar nómina produce comprobantes `NM` contabilizados con terceros
(empleado y entidades vinculadas), visibles de sólo lectura en Contabilidad.

**Independent Test**: vincular la EPS a una persona, aprobar una liquidación y ver el `NM` con la
EPS como tercero en aportes; un concepto a cuenta de agrupación hace fallar la aprobación sin
dejar nada; reversar desde Nómina genera la reversión referenciada.

- [X] T078 [P] [US4] Aceptar `PersonPublicId` en los comandos de actualización de `Application/Payroll/Catalogs/` para EPS, ARL, pensiones, cesantías y cajas y en `Application/Core/Banks/` (validar persona vigente; DTOs de consulta devuelven `personPublicId` y nombre)
- [X] T079 [P] [US4] Agregar `PersonSearchPicker` («Persona que la representa como tercero», con creación en línea) a los diálogos de `Shared/Pages/Nomina/{Eps,Arl,FondosPension,FondosCesantias,CajasCompensacion}.razor` y `Shared/Pages/Maestros/Bancos.razor`, y `personPublicId` a sus DTOs en `Shared/Services/Nomina/NominaDtos.cs`
- [X] T080 [US4] En `Application/Payroll/Services/PayrollAccountingPoster.cs` enviar `PersonId` por línea: el empleado en devengos y deducciones; la persona vinculada a la EPS/ARL/fondo/caja del empleado en aportes y provisiones según la clase del concepto; sin vínculo y cuenta que exige tercero → `Accounting.Line.ThirdPartyRequired` con `data.entity` (FR-088); sucursal del empleado o principal
- [X] T081 [US4] En la validación de cuentas por concepto (T062) exigir el vínculo institucional cuando la cuenta exige tercero y el concepto es de aporte/provisión → `Accounting.Parameterization.InstitutionalLinkMissing { entity }`; incluirlo en `ListInvalidParameterizationsQuery`
- [X] T082 [P] [US4] Ampliar las pruebas ya adaptadas en T038 con los casos de tercero institucional: `AppTests/Payroll/Services/PayrollAccountingPosterTests.cs` (líneas con tercero; entidad sin vínculo rechazada; nada agregado si una regla falla) y `AppTests/Payroll/Runs/ApprovePayrollRunCommandHandlerTests.cs` (concepto a cuenta de agrupación → falla nombrándolo; sin corrida aprobada ni documento)
- [X] T083 [US4] Mapa `SourceType → ruta` para «Ver en {módulo}» en `Shared/Services/Contabilidad/EnlacesDeOrigen.cs` (`PayrollRun` → `/nomina/liquidacion/{id}`) y usarlo en `Comprobante.razor`
- [ ] T084 [US4] Crear `E2E/Payroll/ContabilidadDeNominaTests.cs` (aprobar → `NM` contabilizado con EPS como tercero; `GET /documents/by-source?module=NOM`; en Contabilidad `POST /{id}/reverse` → 422 `ModuleOwned`; concepto a agrupación → aprobación 422 y cero huérfanos en ambos sentidos; reversar desde Nómina → neto cero; nómina con fecha en mes cerrado → 422 `Accounting.Period.Closed`)

**Checkpoint**: Nómina en producción tiene contabilidad real (SC-004 para Nómina).

---

## Phase 7: User Story 7 — Todo queda auditado y cada acción tiene su permiso (Priority: P2)

**Goal**: auditoría del ingreso a cada opción del ERP, exportaciones y envíos auditados,
permisos por capacidad aplicados en servidor y ocultados en cliente.

**Independent Test**: un rol que sólo registra borradores no ve ni puede contabilizar (404
`Generic.NotFound`); abrir cinco opciones y verlas en `/admin/auditoria` con `Module=Navigation`;
cambiar una regla de cuenta muestra antes/después.

- [X] T085 [P] [US7] Crear `RegisterOptionAccessCommand(string Route, string Title)` (+ validador; sin efecto en SQL; el evento lo escribe `AuditBehavior`) en `Application/Audit/RegisterOptionAccess/RegisterOptionAccessCommand.cs` y la ruta `POST /api/audit/access` → 202 en `API/Endpoints/AuditLogModule.cs` (sesión + cooperativa); mapear `.Audit.RegisterOptionAccess` → `"Navigation"` en `InferModuleFromNamespace` de `Application/Common/Behaviors/AuditBehavior.cs`
- [X] T086 [P] [US7] Crear `Shared/Services/Auditoria/RegistroDeAccesos.cs` (Scoped: cola, deduplicación de la misma ruta consecutiva, envío con 3 reintentos y `backoff`, `ILogger` y un aviso por sesión si agota) y suscribirlo a `NavigationManager.LocationChanged` en `Shared/Layout/MainLayout.razor` (`@implements IDisposable`); registrar en `Web/Program.cs` y `Web.Client/Program.cs`
- [X] T087 [P] [US7] En `Shared/Pages/Administracion/AuditLogConsole.razor` agregar el módulo `Navigation` al filtro y la columna «Opción» (`newValuesJson.title`/`route`)
- [X] T088 [P] [US7] Pruebas `RegisterOptionAccessCommandTests` (validador; el evento queda con `Module = Navigation`) en `AppTests/Audit/RegisterOptionAccessCommandTests.cs` y `RegistroDeAccesosTests` (deduplica, reintenta, avisa una vez) en `tests/IngenIA365ERP.Shared.Tests/Auditoria/RegistroDeAccesosTests.cs`
- [X] T089 [P] [US7] Ampliar `AppTests/Infrastructure/RepartoDePermisosTests.cs` con el catálogo `Accounting.*` (Operator: `Vouchers.Create` y `Reports.Export` sí, `Vouchers.Post` no; ReadOnly sólo `View`; Auditor `Reports.Export`; `LecturaDeMaestros` incluye `Accounting.Accounts.View`; la prueba cuenta contra `AccountingPermissionCatalogSeeder.Catalog`, no contra un número fijo)
- [X] T090 [US7] Revisar que toda página nueva de `Shared/Pages/Contabilidad/` lleve `PermissionGate` de página con `MostrarAviso="true"` y gates por botón para Create/Post/Void/Manage/Close/Reopen
- [ ] T091 [US7] Ampliar `E2E/Accounting/ContabilidadNiifTests.cs` con US7 (`POST /api/audit/access` y el evento `RegisterOptionAccess` aparece en `/api/audit/logs?Module=Navigation` en < 30 s; sólo lectura: toda escritura → 404 `Generic.NotFound` igual que `POST /api/accounting/no-existe`, lecturas 200; Operador: `POST /drafts` 201 y `POST /post` 404; cambio de regla de cuenta con `oldValuesJson`/`newValuesJson`)

**Checkpoint**: SC-007 medido en la e2e.

---

## Phase 8: Cierre de E1

- [X] T092 [P] Escribir `docs/manual/contabilidad-contrato-de-contabilizacion.md` (cómo contabiliza un módulo, errores, reintento, qué no hacer) y `docs/operaciones/contabilidad-primer-ejercicio.md` (iniciar, auxiliares mínimas, vincular entidades, primer `NM`); enlazar ambos en `docs/INDICE-DOCUMENTACION.md`
- [X] T093 [P] Actualizar `CLAUDE.md` (párrafo «Contabilidad (feature 009)»: contrato único, saldos derivados, reglas en la cuenta, migración destructiva con guarda, entregas E1–E4; totales de rutas/páginas/pruebas remedidos) y `docs/operaciones/estado-y-pendientes.md` (E1 entregada; E2–E4 pendientes con sus ramas)
- [X] T094 Ejecutar `quickstart.md` §1–§3 completos (build, Application, Architecture, integración con Docker) y corregir lo que salga
  - Estado 2026-09-15: §1 verde (build 0 errores; 1.036 pruebas sin contenedores: 165 Domain, 743 Application, 74 Architecture, 52 Shared, 2 Load); §2 corrido con Docker: 152 de integración, 151 pasan y 1 omitida (`PasswordHashIntegrity`). Salieron y se corrigieron dos cosas: `ReintentoPorConcurrenciaBehavior` resolvía `IApplicationDbContext` en el constructor y tumbaba el login (sin cooperativa resuelta) con 500 —ahora lo pide por `IServiceProvider` sólo al reintentar—, y `Provisioning_SmokeTests` esperaba las 9 cuentas de la semilla vieja (ahora comprueba PUC, rubros, tipos y cruces). §3 hecho (migrador `migrate --scope cooperativas` contra el PostgreSQL local: coop_alfa y coop_beta en `ContabilidadNiif`). Diagnóstico de libros corrido en DEV y QA (`ingenia365erp`, `coop_prueba`): 0 documentos, 0 movimientos. `NominaE2E` ya usa `setup/initialize` + `accounts`. Las e2e nuevas T056/T066/T077/T084/T091 siguen por escribir.
- [X] T095 Confirmar la rama con el usuario, merge a `develop`, CI verde, Argo en DEV/QA; verificar en las bases (`ACC_AccountCatalogs` con SOLIDARIO/COMERCIAL y conteos, `ACC_FinancialStatementItems`, `ACC_VoucherTypes`, 30 permisos, roles con `Accounting.*.View`)
  - Hecho 2026-09-15: merge `6d0c89d` a `develop`, CI `35044468344` verde (7 jobs), GitOps `9c614c8`, Argo `Synced/Healthy` en erp-dev y erp-qa; en `ingenia365erp` (DEV y QA) y `coop_prueba` (QA): `ContabilidadNiif` aplicada, PUC-SOLIDARIO 695 y PUC-COMERCIAL 1.869, 205 rubros, 18 tipos, 8 cruces, 30 permisos `Accounting.*`, los 4 roles built-in con todas las lecturas, 29 tablas `ACC_*` y ninguna heredada. Segundo revisor de la migración: Jorman Copete (2026-09-17), anotado en las dos cabeceras.
- [ ] T096 QA manual `quickstart.md` §4 por rol (administrador, Operador, sólo lectura) y **validación de los catálogos por el contador** (`POST /catalogs/{code}/validate`); registrar resultado en `docs/operaciones/estado-y-pendientes.md` — bloqueante para producción. Nota 2026-09-18: `puc-solidario.json` pasó a ser el CUIF oficial (formato SIAC 2023-11-03, 2.110 cuentas; el anterior era una transcripción con 695 códigos); el contador lo valida sobre esa versión

**Checkpoint E1**: núcleo en `develop` y QA; producción sólo con autorización expresa y respaldo.

---

# ENTREGA E2 — Consultas, cierres y apertura (rama `009-e2-consultas-cierres`)

## Phase 9: User Story 5 — Consultar y profundizar del saldo a la línea (Priority: P2)

**Goal**: libro auxiliar con profundización, libros oficiales, estados financieros NIIF, estado de
cuenta por tercero, documentos pendientes, saldo diario promedio; todo exportable y auditado.

**Independent Test**: del balance de prueba en 1105 llegar a la línea de un comprobante en 4
clics; exportar la vista por terceros a Word con los mismos totales; el balance cuadra tras cada
operación.

- [ ] T097 [P] [US5] Crear `FiltrosDeInforme` (rango, cuenta/rama, tercero, documento, centro, sucursal, tipo, origen, usuario, nivel, con terceros, incluir cierre, formato) y `EncabezadoDeInforme` (empresa, NIT, filtros, período, usuario, fecha → `Notas` de `TablaExportable`) en `Application/Accounting/Reports/FiltrosDeInforme.cs`
- [ ] T098 [P] [US5] Crear `LedgerQuery` (nodos por demanda: class → group → account → subaccount → auxiliary → person → document → voucher → line, cada uno con saldo inicial, débitos, créditos y final; respeta `IUserBranchScope`) en `Application/Accounting/Reports/LedgerQuery.cs`
- [ ] T099 [P] [US5] Crear `TrialBalanceQuery` (nivel, con/sin terceros, con/sin cierre; consolidación en memoria; invariante débitos = créditos), `JournalQuery`, `GeneralLedgerQuery`, `VoucherListQuery` en `Application/Accounting/Reports/LibrosQueries.cs`
- [ ] T100 [P] [US5] Crear `ThirdPartyStatementQuery` (movimientos por cuenta y documentos con saldo), `PendingDocumentsQuery` (saldo por tercero y documento cruce), `DailyAverageBalanceQuery` (saldo diario desde movimientos, promedio del período) en `Application/Accounting/Reports/TercerosQueries.cs`
- [ ] T101 [P] [US5] Crear `FinancialPositionQuery`, `IncomeStatementQuery` (`IncluirCierre`), `EquityChangesQuery`, `CashFlowQuery` (indirecto) sobre `ACC_FinancialStatementItems` con comparativo del período anterior en `Application/Accounting/Reports/EstadosFinancierosQueries.cs`
- [ ] T102 [US5] En cada handler de informe de `Application/Accounting/Reports/*Queries.cs` (T098–T101) emitir `Accounting.Report.Exported` (informe, filtros, formato) vía `AccountingAuditEmitter` cuando `Formato != json`
- [ ] T103 [P] [US5] Pruebas `TrialBalanceQueryHandlerTests` (cuadra; saldo = suma de líneas; excluye borradores; incluye reversiones netas; cierre excluido por defecto), `LedgerQueryHandlerTests` (nodos y saldos por nivel; alcance de sucursal), `IncomeStatementQueryHandlerTests` (rubros del grupo 2) en `AppTests/Accounting/Reports/`
- [ ] T104 [US5] Crear `API/Endpoints/Reports/AccountingReportsEndpoints.cs` (`/api/reports/accounting/{vista}?format=`, un `MapGet` por vista con `EntregaDeInformes.EntregarAsync`; `Accounting.Reports.View` para `json`, `Accounting.Reports.Export` para el resto)
- [ ] T105 [P] [US5] Crear `Shared/Services/Contabilidad/ContabilidadClient.Informes.cs` (JSON y descarga por formato; `TablaReporteDto` reutilizado desde nómina) y el componente `Shared/Components/Contabilidad/EncabezadoDeInforme.razor`
- [ ] T106 [US5] Crear `Shared/Components/Contabilidad/LibroAuxiliar.razor` (`SfTreeGrid` con hijos por demanda desde `LedgerQuery`, columnas saldo inicial/débitos/créditos/final, exportar el nivel actual) y `Shared/Pages/Contabilidad/LibroAuxiliar.razor` (`/contabilidad/libro-auxiliar`: filtros combinables + árbol; abre el comprobante en `Comprobante.razor`)
- [ ] T107 [US5] Crear `Shared/Pages/Contabilidad/Informes.razor` (`/contabilidad/informes`: selector de vista, filtros, tabla en pantalla y tres botones de exportación) y `Shared/Pages/Contabilidad/EstadosFinancieros.razor` (`/contabilidad/estados-financieros`: ESF, ERI, cambios en el patrimonio, flujo de efectivo, comparativo, incluir cierre); agregar las rutas a `Shared/Layout/NavMenu.razor`
- [ ] T108 [US5] Crear `E2E/Accounting/InformesTests.cs` (`ElBalanceDePruebaCuadra` tras digitar, contabilizar, reversar, cerrar y reabrir; `ledger` de 1105 hasta la línea; los tres formatos con los mismos totales; `Accounting.Report.Exported` en auditoría; usuario con sucursal asignada sólo ve la suya, y una prueba recorre todas las vistas de `/api/reports/accounting` con ese usuario verificando que ninguna devuelve otra sucursal) y `E2E/Accounting/RendimientoDeInformesTests.cs` (`RUN_PERF_TESTS=1`: un millón de líneas, `ledger` y `trial-balance` < 5 s; SC-008)

**Checkpoint**: SC-005, SC-006 y SC-008 verificados.

---

## Phase 10: User Story 6 — Cerrar períodos y el ejercicio (Priority: P2)

**Goal**: cierre y reapertura del ejercicio con comprobante de cierre reservado; apertura del
ejercicio siguiente.

**Independent Test**: cerrar doce meses y el ejercicio; el balance del primer día siguiente
muestra sólo saldos de balance; reabrir reversa el cierre; cerrar con el anterior abierto es
rechazado.

- [ ] T109 [US6] Crear `CloseFiscalYearCommand` (12 meses cerrados, ejercicio anterior cerrado, `ResultAccountId` definido; comprobante `Kind = Closing` tipo `CI` por `AccountingPoster` cancelando rubros de resultado contra la cuenta de resultado; `IReintentableAnteConcurrencia`) y `ReopenFiscalYearCommand` (motivo; `PrepareReversalAsync` del cierre) en `Application/Accounting/Periods/`
- [ ] T110 [P] [US6] Pruebas `CloseFiscalYearCommandHandlerTests` (meses abiertos; anterior abierto; sin cuenta de resultado; comprobante de cierre cuadrado y con fecha 31/12) y `ReopenFiscalYearCommandHandlerTests` en `AppTests/Accounting/Periods/`
- [ ] T111 [US6] Agregar `POST /years/{year}/close` y `/reopen` a `API/Endpoints/Accounting/PeriodsEndpoints.cs` (`Accounting.Periods.CloseYear`) y las acciones de ejercicio a `Shared/Pages/Contabilidad/Periodos.razor` y `ContabilidadClient.Periodos.cs`
- [ ] T112 [US6] Crear `E2E/Accounting/CierreDeEjercicioTests.cs` (cerrar con borrador → 422; cerrar los doce; cerrar el año; balance del 1 de enero siguiente sin resultados; estado de resultados sin/con cierre; reabrir con motivo y auditoría; cerrar 2027 con 2026 abierto → 422)

---

## Phase 11: User Story 13 — Cargar los saldos de apertura (Priority: P2)

**Goal**: apertura única por empresa, importada o digitada, con las mismas reglas que cualquier
comprobante y tratada como saldo inicial.

**Independent Test**: importar 200 filas con dos errores, corregir, contabilizar; el balance del
primer día coincide con el archivo; el estado de cuenta de un tercero muestra sus documentos.

- [ ] T113 [US13] Crear `ImportOpeningBalancesCommand` (`ITabularFileReader`; plantilla `cuenta, tercero, tipoDocumento, numeroDocumento, centroCosto, sucursal, debito, credito, detalle`; errores por fila con `AccountLineRules`; crea borrador tipo `AP` `Kind = Opening`; `Accounting.Opening.AlreadyExists { openingDocumentPublicId }`, `.Invalid { errors[] }`) y `GetOpeningTemplateQuery` (tabla vacía con encabezados → xlsx por `ExportadorDeTablas`) en `Application/Accounting/Opening/`
- [ ] T114 [US13] En `PostDocumentCommand`/`AccountingPoster` cerrar las reglas de `Opening` (fecha = primer período − 1, única no reversada, guarda `AccountingSetup.OpeningDocumentId`) y en `TrialBalanceQuery`/`LedgerQuery` tratar sus líneas como saldo inicial
- [ ] T115 [P] [US13] Pruebas `ImportOpeningBalancesCommandHandlerTests` (fila con agrupación; tercero inexistente; documento faltante; más de dos decimales; nada a medias; segunda apertura rechazada) en `AppTests/Accounting/Opening/`
- [ ] T116 [US13] Crear `API/Endpoints/Accounting/OpeningEndpoints.cs` (`/api/accounting/opening`: `GET /template.xlsx`, `POST /import` multipart; `Accounting.Opening.Manage`), `Shared/Services/Contabilidad/ContabilidadClient.Apertura.cs` y `Shared/Pages/Contabilidad/Apertura.razor` (`/contabilidad/apertura`: descargar plantilla, importar con errores por fila, abrir el borrador en `Comprobante.razor`, estado de la apertura; ruta en `NavMenu.razor`)
- [ ] T117 [US13] Crear `E2E/Accounting/AperturaTests.cs` (plantilla; importar con errores → 422 con filas; importar bien → borrador `AP`; con `RUN_PERF_TESTS=1`, 5 000 filas generadas en < 2 min (SC-014); contabilizar; balance del 1 de enero = totales del archivo al centavo; documentos pendientes del tercero; segunda apertura → 422; reversar y cargar otra)

## Phase 12: Cierre de E2

- [ ] T118 Actualizar `docs/operaciones/contabilidad-primer-ejercicio.md` (apertura y cierre), `CLAUDE.md` (totales) y `docs/operaciones/estado-y-pendientes.md`; correr `quickstart.md` §1–§3; merge a `develop`, DEV/QA, QA manual `quickstart.md` §5 E2

**Checkpoint E2**: una cooperativa que viene de SOLIDO puede arrancar y cerrar su primer ejercicio.

---

# ENTREGA E3 — Módulos restantes (rama `009-e3-modulos`)

## Phase 13: User Story 4 — Cartera, Inventario, Tesorería y CDT por el contrato (Priority: P1, parte 2)

**Goal**: cada operación que contabiliza produce su comprobante por `AccountingPoster` con la
parametrización existente (ampliada donde faltaba), o falla nombrando qué configurar.

**Independent Test**: una operación por módulo → `GET /documents/by-source` devuelve un
comprobante cuadrado con el tercero de la operación; anular desde el módulo reversa ese
comprobante; sin parametrización → `Accounting.Parameterization.Missing`.

- [ ] T119 [P] [US4] Validar con `AccountEligibility` al guardar la parametrización de Cartera (`Application/Lending/CreditLines/Commands/*` sobre `CreditLineParameter`: `AccountCode`, `AccountInterestIncome`, `AccountInterestCxC`, `AccountInterestDefault`, `ProvisionExpenseAccountCode`, `ProvisionAccountCode`; `Application/Lending/SavingsParameters/*`: `TreasuryAccount`, `InterestExpenseAccount`) con `Modulo = Lending|Savings`
- [ ] T120 [P] [US4] Validar la parametrización de Inventario (`Application/Inventory/ProductAccounts/Commands/*`, `Application/Inventory/VatAccounts/Commands/*`) con `Modulo = Inventory`, la de CDT (`Application/CDT/Parameters/*`: `TreasuryAccount`, `InterestExpenseAccount`) con `Modulo = Savings`, y la de bancos (`Application/Core/Banks/*`: `AccountingAccountCode`) con `Modulo = Treasury`
- [ ] T121 [P] [US4] Agregar `DebitAccountPublicId`/`CreditAccountPublicId` a los comandos y consultas de `Application/Treasury/Concepts/*` (validados con `Modulo = Treasury`) y a `Shared/Pages/Tesoreria/Conceptos.razor` con `BuscadorDeCuenta Modulo="Treasury"` y `ReglasDeCuenta`
- [ ] T122 [US4] Ampliar `ListInvalidParameterizationsQuery` (`Application/Accounting/Accounts/AccountQueries.cs`) a todas las tablas de `contracts/contabilizacion.md` §4 y `DeleteAccountCommand.Referenced` a las mismas
- [ ] T123 [US4] Cartera por el contrato: `AccrueInterestCommand` (`CA`), `ClassifyPortfolioCommand` (`PV` con las cuentas de provisión nuevas), `ProcessPayrollDeductionCommand` (`DN`), `LiquidateSavingsInterestCommand` (`AH`) en `Application/Lending/**/*.cs` (tercero = asociado; documento cruce `PG`/`CT` + número; sucursal del crédito; `IReintentableAnteConcurrencia`; `SourceType` propio); completar `DisburseLoan`/`ProcessPayment` (documento cruce `PG`, sucursal del crédito)
- [ ] T124 [US4] CDT por el contrato: `LiquidateCDTInterestCommand` (`CD`; tercero = titular; documento `CT` + número) en `Application/CDT/InterestLiquidation/Commands/LiquidateCDTInterest/LiquidateCDTInterestCommand.cs`
- [ ] T125 [US4] Inventario por el contrato: `CreateInvoiceCommand` (`FV`: CxC cliente / ventas e IVA desde `ProductAccount`/`VatAccount`), `CreateInventoryDocumentCommand` (`EI`/`SI`), guardar `InventoryDocument.AccountingDocumentId`, y `VoidInventoryDocumentCommand` reversando **ese** documento por `PrepareReversalAsync` en `Application/Inventory/Documents/Commands/**/*.cs`
- [ ] T126 [US4] Tesorería por el contrato: `RegisterCheckCommand` (`CH`: concepto / banco), `RegisterTreasuryInvoiceCommand` (`FP`/`CB`), `ProcessCheckCommand` (anular → reversión; devolución) en `Application/Treasury/**/*.cs`; sin `MAX(DocumentNumber)+1`
- [ ] T127 [P] [US4] Pruebas por módulo en `AppTests/Lending/Contabilizacion/`, `AppTests/CDT/`, `AppTests/Inventory/Contabilizacion/`, `AppTests/Treasury/Contabilizacion/` (comprobante cuadrado con tercero y documento; parametrización faltante → `Accounting.Parameterization.Missing` sin guardar nada; anulación reversa el documento vinculado)
- [ ] T128 [P] [US4] Ampliar `Shared/Services/Contabilidad/EnlacesDeOrigen.cs` (`LoanApplication`, `LoanPayment`, `Invoice`, `InventoryDocument`, `Check`, `TreasuryInvoice`, `CdtLiquidation` → rutas) y mostrar «Ver comprobante» en las pantallas de esos módulos (`Shared/Pages/{CarteraFinanciera,Inventario,Tesoreria,CDT}/*.razor`) usando `GET /documents/by-source`
- [ ] T129 [US4] Crear `E2E/Lending/ContabilizacionDeCarteraTests.cs`, `E2E/Inventory/ContabilizacionDeInventarioTests.cs`, `E2E/Treasury/ContabilizacionDeTesoreriaTests.cs`, `E2E/CDT/ContabilizacionDeCdtTests.cs` (una operación → comprobante por `by-source`; anular → reversión; sin parametrización → 422 con qué configurar; período cerrado → 422; `ElBalanceDePruebaCuadra` después de todo)
- [ ] T130 Cierre de E3: actualizar `docs/manual/contabilidad-contrato-de-contabilizacion.md` (tabla por módulo), `CLAUDE.md`, `docs/operaciones/estado-y-pendientes.md`; `quickstart.md` §1–§3; merge a `develop`, DEV/QA, QA manual §5 E3

**Checkpoint E3**: SC-004 y SC-010 para los cinco módulos; ningún camino fuera del contrato.

---

# ENTREGA E4 — Satélites (rama `009-e4-satelites`)

## Phase 14: User Story 8 — Conciliar las cuentas bancarias (Priority: P3)

**Independent Test**: diez movimientos y un extracto con ocho coincidencias, una nota sin
contabilizar y un cheque no cobrado → informe con diferencia cero explicada.

- [ ] T131 [P] [US8] Crear `SaveStatementColumnMapCommand`, `PreviewStatementQuery` (10 filas interpretadas), `ImportStatementCommand` (mapeo; `Fingerprint`; `.FileDoesNotMatchMap { row, column }`; repetidos no se duplican), `AddStatementLineCommand` (digitar) en `Application/Accounting/Reconciliation/`
- [ ] T132 [P] [US8] Crear `AutoMatchCommand` (valor+referencia, o valor+fecha ± tolerancia), `MatchLineCommand`, `UnmatchLineCommand` (motivo), `CreateDraftFromStatementLineCommand` (borrador prellenado con cuenta bancaria, fecha, valor), `CloseReconciliationCommand`, `ReopenReconciliationCommand`, `GetReconciliationQuery` (partidas por lado, propuestas, saldos, diferencia explicada; respeta `IUserBranchScope`), `ListBankAccountsQuery` en `Application/Accounting/Reconciliation/`
- [ ] T133 [US8] Marcar `Outdated` desde `ReopenPeriodCommand` (`Application/Accounting/Periods/ReopenPeriodCommand.cs`) y actualizar `JournalEntry.ReconciledAt` al conciliar/desconciliar
- [ ] T134 [P] [US8] Pruebas en `AppTests/Accounting/Reconciliation/` (mapeo con débito/crédito separados y con signo; repetidos; auto-match por referencia y por tolerancia; dos movimientos idénticos no se emparejan dos veces; informe a cero; cerrar; desactualizada al reabrir el período)
- [ ] T135 [US8] Crear `API/Endpoints/Accounting/ReconciliationsEndpoints.cs` (`contracts/api.md` §9; `Accounting.Reconciliation.View|Manage`), `ContabilidadClient.Conciliacion.cs`, componentes `Shared/Components/Contabilidad/MapeoDeExtracto.razor` (definir columnas con vista previa) y `ConciliadorDePartidas.razor` (dos listas, propuestas, confirmar/rechazar en bloque), y `Shared/Pages/Contabilidad/Conciliacion.razor` (`/contabilidad/conciliacion`; ruta en `NavMenu.razor`); vista `reconciliation` en `AccountingReportsEndpoints.cs`
- [ ] T136 [US8] Crear `E2E/Accounting/ConciliacionTests.cs` (mapeo con vista previa; carga doble; auto-match; nota → borrador; informe a cero; cerrar; reabrir período → `Outdated`)

## Phase 15: User Story 9 — Presupuestar y seguir la ejecución (Priority: P3)

**Independent Test**: presupuestar dos cuentas de gasto, contabilizar en ellas y ver ejecución
mensual y acumulada con variación correcta y profundización.

- [ ] T137 [P] [US9] Crear `SaveBudgetCommand` (líneas por cuenta de movimiento; `Accounting.Budget.AccountNotMovement`; crea versión con motivo si está aprobado), `ApproveBudgetCommand`, `CopyBudgetCommand` (año anterior + %; redondeo a pesos), `DistributeBudgetCommand` (igual/manual/porcentual; diferencia en el último mes), `GetBudgetQuery` en `Application/Accounting/Budgets/`
- [ ] T138 [P] [US9] Crear `BudgetExecutionQuery` (presupuestado, ejecutado, variación y %, mes y acumulado, por cuenta y agregado por niveles; inicial y vigente; respeta `IUserBranchScope`) en `Application/Accounting/Reports/BudgetExecutionQuery.cs` y la vista `budget-execution` en `AccountingReportsEndpoints.cs`
- [ ] T139 [P] [US9] Pruebas en `AppTests/Accounting/Budgets/` (agrupación rechazada; versión con motivo; copia +5 % redondeada; distribución igual con resto al último mes; ejecución y agregación)
- [ ] T140 [US9] Crear `API/Endpoints/Accounting/BudgetsEndpoints.cs` (`Accounting.Budget.View|Manage`), `ContabilidadClient.Presupuesto.cs` y `Shared/Pages/Contabilidad/Presupuesto.razor` (`/contabilidad/presupuesto`: grilla cuenta × 12 meses, copiar, distribuir, aprobar, versiones, ejecución con profundización al libro auxiliar; ruta en `NavMenu.razor`)
- [ ] T141 [US9] Crear `E2E/Accounting/PresupuestoTests.cs`

## Phase 16: User Story 10 — Impuestos y certificados de retención (Priority: P3)

**Independent Test**: cuenta de retefuente al 4 %, tres líneas de dos terceros con base,
certificados del año con valores, consecutivos y PDF.

- [ ] T142 [P] [US10] Crear `SetAccountTaxRuleCommand` (tipo, concepto, exige base, tarifas con vigencia) y `ListTaxAccountsQuery` en `Application/Accounting/Taxes/`; `SaveTaxFormCommand`/`GetTaxFormQuery` (renglones con selector y prefijos) en `Application/Accounting/Taxes/Forms/`
- [ ] T143 [P] [US10] Crear `GenerateWithholdingCertificatesCommand` (por tercero y año/período desde el libro; consecutivo por tipo; `LedgerFingerprint`), `ListCertificatesQuery` (marca `Outdated` comparando huella), `ReissueCertificateCommand` (anula el anterior), `SendCertificateCommand` (`IEmailSender` con PDF; `Accounting.EmailNotConfigured`, `.Certificate.PersonWithoutEmail`; evento `Accounting.Certificate.Sent`), `GetCertificatePdfQuery` en `Application/Accounting/Taxes/Certificates/`; `IWithholdingCertificatePdfRenderer` en Application implementado en `API/Reports/WithholdingCertificateReport.cs`
- [ ] T144 [P] [US10] Crear `WithholdingsReportQuery`, `VatReportQuery`, `IcaReportQuery`, `GmfReportQuery`, `TaxFormSummaryQuery` (todas respetan `IUserBranchScope`) en `Application/Accounting/Reports/ImpuestosQueries.cs` y las vistas `withholdings`, `vat`, `ica`, `gmf`, `tax-form/{code}` en `AccountingReportsEndpoints.cs`
- [ ] T145 [P] [US10] Pruebas en `AppTests/Accounting/Taxes/` (tarifa vigente por fecha; `TaxAmountMismatch` según tolerancia; base obligatoria; certificados por tercero con consecutivo; reimpresión no cambia número; huella desactualizada tras reversión; reexpedir anula el anterior)
- [ ] T146 [US10] Crear `API/Endpoints/Accounting/TaxesEndpoints.cs` (`Accounting.Taxes.View|Manage|Certificates`), `ContabilidadClient.Impuestos.cs`, `Shared/Pages/Contabilidad/Impuestos.razor` (`/contabilidad/impuestos`: cuentas de impuesto y tarifas, formularios y renglones) y `Shared/Pages/Contabilidad/Certificados.razor` (`/contabilidad/certificados`: generar, lista con estado, PDF, enviar, reexpedir); rutas en `NavMenu.razor`
- [ ] T147 [US10] Crear `E2E/Accounting/CertificadosTests.cs` (línea sin base → 422; valor fuera de tolerancia → 422; generar; PDF; enviar con `CapturingEmailSender`; reversión → `Outdated`; reexpedir; con `RUN_PERF_TESTS=1`, 1 000 terceros generados en < 2 min, SC-012)

## Phase 17: User Story 11 — Información exógena para la DIAN (Priority: P3)

**Independent Test**: formato de pagos con dos conceptos, cinco terceros (uno bajo cuantía, uno
sin dirección), generar, inconsistencias, exportar Excel y XML.

- [ ] T148 [P] [US11] Transcribir `exogena-2026.json` (todos los formatos y conceptos de la Resolución Única 000227 de 2025 y modificaciones 2026 vigentes para el año gravable 2026: código, versión, nombre, conceptos, columnas de valor, cuantía mínima, regla de menores cuantías, cuentas propuestas por prefijo donde el vínculo es natural) en `Persistence/Seeding/Parametric/Data/exogena-2026.json` y `ExogenousSeeder` (Order 62) en `Persistence/Seeding/Parametric/ExogenousSeeder.cs`; ampliar `LasSemillasJsonSonCoherentes`
- [ ] T149 [P] [US11] Crear `SaveExogenousFormatCommand` (aplica, cuantía, conceptos y cuentas), `CopyExogenousYearCommand`, `GenerateExogenousRunCommand` (valores por tercero y concepto desde el libro; instantánea de identificación desde `COR_People`; menores cuantías; inconsistencias bloqueantes y avisos), `GetExogenousRunQuery`, `ListExogenousRunsQuery` en `Application/Accounting/Exogenous/`
- [ ] T150 [P] [US11] Crear `ExportExogenousRunQuery` (`xlsx` por `ExportadorDeTablas`; `xml` con `System.Xml` según estructura `Dmuisca` y nombre de 33 caracteres, ISO 8859-1; bloqueantes → `Accounting.Exogenous.BlockingIssues`; guarda el XML como adjunto `OwnerEntityType="ExogenousRun"` y marca la versión `Exported`) en `Application/Accounting/Exogenous/ExportExogenousRunQuery.cs`
- [ ] T151 [P] [US11] Pruebas en `AppTests/Accounting/Exogenous/` (cuantía mínima agrupa; documento inválido bloquea; dirección faltante avisa; regenerar reemplaza sólo la de trabajo; XML bien formado con encabezado y filas; copiar año)
- [ ] T152 [US11] Crear `API/Endpoints/Accounting/ExogenousEndpoints.cs` (`Accounting.Exogenous.View|Manage|Export`), `ContabilidadClient.Exogena.cs` y `Shared/Pages/Contabilidad/Exogena.razor` (`/contabilidad/exogena`: formatos del año con «aplica», conceptos y cuentas, generar, inconsistencias con enlace a la persona, versiones, exportar Excel y XML; ruta en `NavMenu.razor`)
- [ ] T153 [US11] Crear `E2E/Accounting/ExogenaTests.cs` (semilla 2026 presente; generar 1001; menores cuantías; inconsistencia bloqueante impide XML; corregir persona y regenerar; XML exportado bien formado) y dejar en `quickstart.md` §5 el paso del prevalidador en QA (SC-012)

## Phase 18: User Story 12 — Activos fijos y diferidos (Priority: P3)

**Independent Test**: activo de 60 meses con residual; dos corridas; reversar la segunda;
cambiar vida útil recalcula sólo las pendientes; baja con comprobante.

- [ ] T154 [P] [US12] Crear `RegisterFixedAssetCommand` (+ calendario de cuotas lineal con residual), `UpdateFixedAssetCommand` (vida útil/residual → regenera sólo `Pending` sobre el valor neto), `RunAssetDepreciationCommand` (un documento `DP` por período vía `AccountingPoster` con todas las cuotas pendientes del período; `ACC_AssetRuns.PeriodId` único → `Accounting.Assets.AlreadyRun`; `IReintentableAnteConcurrencia`), `ReverseAssetRunCommand`, `RetireFixedAssetCommand` (comprobante de baja), `GetFixedAssetQuery`, `ListFixedAssetsQuery` en `Application/Accounting/Assets/`
- [ ] T155 [P] [US12] Pruebas en `AppTests/Accounting/Assets/` (cuotas y residual; corrida única por período; reversión deja cuotas pendientes; cambio prospectivo no toca contabilizadas; totalmente depreciado no genera; compra en período cerrado acumula en la primera cuota; baja cancela costo y acumulada)
- [ ] T156 [US12] Crear `API/Endpoints/Accounting/AssetsEndpoints.cs` (`Accounting.Assets.View|Manage|Run`), la vista `assets` en `AccountingReportsEndpoints.cs`, `ContabilidadClient.Activos.cs`, el componente `Shared/Components/Contabilidad/CuotasDeActivo.razor` y `Shared/Pages/Contabilidad/Activos.razor` (`/contabilidad/activos`: inventario con valor neto, ficha con `CampoCodigo Catalogo="activos"` y `BuscadorDeCuenta Modulo="Assets"`, correr período, reversar, baja; ruta en `NavMenu.razor`); arma `"activos"` en `BuscarCodigoDeCatalogoQuery.cs`
- [ ] T157 [US12] Crear `E2E/Accounting/ActivosTests.cs` (registrar; correr dos veces el mismo mes → una sola vez; reversar; cambiar vida útil; baja; `ElBalanceDePruebaCuadra`)

## Phase 19: Polish final

- [ ] T158 [P] Revisar rendimiento con `RUN_PERF_TESTS=1` sobre las vistas de E4 (ejecución presupuestal y conciliación) y ajustar índices en `Persistence/Configurations/Accounting/` si alguna supera SC-008
- [ ] T159 [P] Actualizar `docs/manual/contabilidad-contrato-de-contabilizacion.md`, `docs/operaciones/contabilidad-primer-ejercicio.md` (satélites), `docs/INDICE-DOCUMENTACION.md`, `CLAUDE.md` (párrafo final de la feature y totales remedidos) y `docs/operaciones/estado-y-pendientes.md` (feature cerrada; lo que quede pendiente por módulo)
- [ ] T160 Ejecutar `quickstart.md` completo; merge de E4 a `develop`; DEV/QA; QA manual §5 E4 incluido el prevalidador de la DIAN
- [ ] T161 Preparar la promoción a producción (`quickstart.md` §6): `pg_dump` por cooperativa, `diagnostico-libros.sql` antes/después, notas de release con los 30 permisos y la instrucción de vincular entidades institucionales; ejecutar **sólo** con autorización expresa del usuario

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)**: T011–T046 dependen de T001–T010 (paquete, `TablaExportable`, behavior, lector, permisos, emisor).
- **Foundational bloquea todo**: ninguna historia empieza antes de T046.
- **E1**: US1 (Phase 3) → US2 (Phase 4) → US3 (Phase 5) → US4-Nómina (Phase 6) en ese orden (cada una usa lo anterior: no hay auxiliares sin configuración, no hay comprobantes sin auxiliares ni períodos, no hay `NM` sin comprobantes); US7 (Phase 7) puede correr en paralelo desde el final de US1; Cierre E1 (Phase 8) al final.
- **E2** parte de `develop` con E1: US5, US6 y US13 son independientes entre sí tras Phase 9 T097 (filtros/encabezado); US13 necesita T114 que toca `TrialBalanceQuery` (T099).
- **E3** parte de `develop` con E2: Phase 13 depende de `AccountEligibility` (T037) y del contrato; los cuatro módulos son paralelos entre sí (T123–T126).
- **E4** parte de `develop` con E3: US8, US9, US10, US11, US12 son independientes entre sí; todas usan `AccountingReportsEndpoints` (T104) y `ContabilidadClient` (T043).

### User Story Dependencies

| Historia | Depende de | Independiente de |
|---|---|---|
| US1 | Foundational | todas |
| US2 | US1 (configuración) | US3+ |
| US3 | US1, US2 | US4+ |
| US4 (Nómina) | US1–US3 | US5+ |
| US7 | US1 (endpoints con permiso) | US2–US6 |
| US5 | E1 | US6, US13 |
| US6 | E1 | US5, US13 |
| US13 | E1, T099 | US6 |
| US4 (módulos) | E1 | E4 |
| US8–US12 | E1–E3 | entre sí |

### Parallel Opportunities

- Phase 1: T002–T010 en paralelo tras T001.
- Phase 2: T012–T018 (dominio) en paralelo; T023–T026 (JSON) en paralelo con T019–T022; T028–T033, T035, T041–T045 en paralelo.
- Cada historia: comandos/consultas [P] → pruebas [P] → endpoint → cliente/páginas → e2e.
- E4: las cinco historias en paralelo por personas distintas (archivos disjuntos).

## Parallel Example: Phase 2

```text
T012 enums | T013 setup/catálogos | T014 cuenta | T015 tipos | T016 períodos | T017 transacciones | T018 columnas de otros módulos
→ T019 configuraciones → T020 DbSets → T022 migración
T023 puc-comercial | T024 puc-solidario | T025 rubros | T026 tipos y documentos cruce → T027 seeders → T028 prueba JSON
T030 errores → T031 reglas → T034 poster (T032, T035 pruebas en paralelo) → T038 nómina, T040 cartera, T039 pausa explícita
T041 | T042 | T043 | T044 | T045 en paralelo → T046 compuerta
```

## Implementation Strategy

### MVP = E1 completa (US1 + US2 + US3 + US4-Nómina + US7)

1. Phases 1–2: infraestructura, dominio nuevo, migración con guarda, semillas, contrato.
2. Phase 3 (US1): iniciar una cooperativa con su PUC → **demostrable**.
3. Phase 4 (US2): auxiliares con reglas que el contrato hace cumplir.
4. Phase 5 (US3): digitación con borradores y contabilizar → operación diaria.
5. Phase 6 (US4): Nómina real en producción con contabilidad real.
6. Phase 7 (US7): permisos y auditoría de accesos → cumplimiento.
7. Phase 8: merge, QA por rol, **validación del contador** (bloqueante para producción).

### Entregas incrementales

- **E2** hace útil la contabilidad para el contador (consultas, cierres, apertura).
- **E3** conecta los módulos que hoy contabilizan a medias o nada.
- **E4** completa la norma (conciliación, presupuesto, impuestos, exógena, activos).

Cada entrega deja `develop` desplegable y QA verificado antes de abrir la siguiente.
