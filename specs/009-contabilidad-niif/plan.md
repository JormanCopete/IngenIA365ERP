# Implementation Plan: Contabilidad NIIF — plan de cuentas, comprobantes, integración e informes

**Branch**: `009-contabilidad-niif` | **Date**: 2026-09-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/009-contabilidad-niif/spec.md` (13 historias, 88
requisitos, 8 decisiones del dueño en Clarifications, sesión 2026-09-14).

## Summary

Se **reemplaza** el módulo de contabilidad heredado de SOLIDO (33 tablas `ACC_`, libros vacíos en
los cuatro ambientes) por un núcleo NIIF: catálogos PUC Solidario y Comercial hasta nivel 4 como
datos (JSON embebido) más importador de catálogo propio; configuración por empresa (catálogo,
nivel de movimiento 5 o 6, longitudes, grupo NIIF, sucursal principal, cuatro ojos); plan de la
empresa con auxiliares que llevan **reglas** (módulos habilitados, exige tercero, documento cruce,
centro, sucursal, base gravable) bloqueadas en cuanto la cuenta tiene movimientos; **un solo
contrato de contabilización** (`AccountingPoster`, agrega sin guardar; una sola clase de reglas)
por el que pasan la digitación manual, Nómina, Cartera, Inventario, Tesorería, CDT y los procesos
propios; documentos inmutables con reversión referenciada, numeración al contabilizar con
reintento por concurrencia; **saldos derivados** (sin tablas de saldos); períodos por ejercicio,
apertura y cierre; libro auxiliar con profundización (`SfTreeGrid`), libros oficiales y estados
financieros desde rubros NIIF, todo exportable a Excel/PDF/Word y auditado; permisos
`Accounting.*` en toda ruta; auditoría del ingreso a cada opción del ERP; y los satélites
reconstruidos sobre el mismo contrato (conciliación con mapeo de extracto por cuenta,
presupuesto versionado, impuestos con tarifas vigentes y certificados, exógena por año con
semilla completa, activos con corrida única por período). Se entrega en **cuatro etapas** (R17).

## Technical Context

**Language/Version**: .NET 10 (global.json 10.0.3xx), C# 14; Blazor Web App
InteractiveWebAssembly (Web + Web.Client) y MAUI Hybrid; Syncfusion 33.2.8 por componente
(**se agrega `Syncfusion.Blazor.TreeGrid`**, misma versión, en `Shared.csproj`)
**Primary Dependencies**: MediatR + FluentValidation (Application); EF Core 10 en par
PostgreSQL/SQL Server; MongoDB (auditoría: `AuditBehavior` + `IAuditAppendOnlyWriter`); Redis
(caché de permisos); Carter; ClosedXML/OpenXML/QuestPDF (exportador existente, y ClosedXML como
lector de archivos tabulares detrás de `ITabularFileReader`); `System.Xml` (BCL) para el XML de
exógena; adjuntos cifrados existentes (`COR_Attachments`); `IEmailSender` con adjuntos
**Storage**: esquema `ACC_` **nuevo** (data-model.md, 30 tablas) que sustituye a las 33 heredadas
mediante la migración destructiva `ContabilidadNiif` (guarda: falla si hay libros; marcador
`MIGRACION-DESTRUCTIVA-APROBADA`); columnas nuevas en `COR_Branches` (`TenantBranchPublicId`),
`COR_Banks` y los cinco catálogos institucionales de nómina (`PersonId`), `TRS_Concepts`
(cuentas), `LND_CreditLineParameters`/`LND_SavingsParameters`/`CDT_Parameters` (cuentas de
provisión/gasto de intereses), `INV_Documents` (`AccountingDocumentId`); semillas JSON en cada
base de cooperativa; sin datos históricos que convertir
**Testing**: Application.Tests (InMemory + NSubstitute; `TestApplicationDbContext` registra los
DbSets contables nuevos y deja de ignorar `ChartOfAccount`), Architecture.Tests (3 pruebas nuevas,
4 ampliadas), API.IntegrationTests (`CentralIdentityApiFixture`, colección «Nomina e2e» sobre
`NominaE2E.PrepararAsync`, que ya deja plan mínimo, períodos 2026 y cuentas por concepto), prueba
de coherencia de los JSON de semilla, verificación manual en QA por rol y por el contador;
prevalidador de la DIAN en QA para el XML
**Target Platform**: API en contenedor linux-x64 (k3s, Argo CD; DEV/QA auto, PDN manual);
cliente WebAssembly detrás de Cloudflare; MAUI comparte `Shared`
**Project Type**: web-service (Minimal APIs) + web-app (Blazor) en un repositorio, Clean
Architecture de cuatro capas
**Performance Goals**: SC-008 — libro auxiliar de una cuenta por un mes, balance de prueba y
ejecución presupuestal < 5 s sobre un libro de 1 000 000 de líneas (agregación sobre índices
`(AccountId, Date)`, `(PersonId, AccountId, Date)`, `(BranchId, Date)`); exportación de 50 000
líneas < 60 s; validación de una línea (`/documents/validate`) < 300 ms percibidos (debounce
en cliente); importación de catálogo de 2 000 filas < 1 min y de apertura de 5 000 filas < 2 min;
contabilización con dos usuarios concurrentes sin duplicados ni huecos (reintento en servidor)
**Constraints**: sin `BeginTransaction` en `IApplicationDbContext` → atomicidad por un solo
`SaveChangesAsync` (el contrato agrega sin guardar, como `PayrollAccountingPoster`); numeración
sin `SEQUENCE` → `RowVersion` + índice único + `ReintentoPorConcurrenciaBehavior` +
`IApplicationDbContext.DescartarCambios()`; ningún valor de negocio en código (códigos de cuenta,
rubros, tarifas, formatos: todo dato; prueba `LaContabilidadNoTieneCuentasEnCodigo`); `Shared`
no referencia Application (DTOs espejo en `ContabilidadDtos`); sin permiso → 404 indistinguible;
Domain sin infraestructura (el lector de archivos y el renderizador PDF son interfaces de
Application implementadas en API); Principio XI: documento y líneas en
`Entities.Accounting.Transactions`, nunca `Remove`
**Scale/Scope**: ~30 entidades nuevas y 33 tablas retiradas; ~95 comandos/consultas; ~110 rutas
(20 heredadas retiradas); 30 códigos de permiso; 22 pantallas heredadas reemplazadas por ~24
nuevas y ~9 componentes; 16 escritores de módulo re-encaminados; 4 semillas JSON (2 catálogos,
rubros NIIF, exógena 2026) + tipos de comprobante y documentos cruce; 1 migración destructiva
en par; cooperativas con miles de terceros y hasta un millón de líneas al año

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec clarificada (8 decisiones) → este plan → tasks; rama propia; entrega por etapas documentada (R17) |
| II   | Clean Architecture | PASS | entidades y enums en Domain sin dependencias; `AccountingPoster`, reglas, informes y XML en Application (BCL); ClosedXML/QuestPDF sólo en API detrás de `ITabularFileReader`/renderizadores; `Shared` no referencia Application |
| III  | CQRS + MediatR | PASS | todo comando con validador hermano (`PrincipioVIII` pasa a cubrir `Application.Accounting`); endpoints sólo reenvían; el contrato de contabilización es un servicio de Application que usan los handlers, no un endpoint |
| IV   | Multi-tenancy | PASS | todo bajo la conexión de la cooperativa; los catálogos se siembran **en cada base** (no hay base compartida); el vínculo sucursal ↔ oficina administrativa es por `PublicId`, sin FK entre bases; la auditoría de accesos va a la base Mongo de la cooperativa |
| V    | Person centralizada | PASS | el tercero es siempre `COR_People`; las entidades institucionales se **vinculan** a una persona (`PersonId`), no duplican datos; exógena toma identificación y dirección de Personas |
| VI   | PublicId | PASS | contratos sólo con `PublicId`; `AccountRef` por código de cuenta (nomenclatura, no identificador); `SourcePublicId` en el origen |
| VII  | Soft-delete + auditoría | PASS | todas las entidades `AuditableEntity/Long`; los rubros y tipos de documento cruce también (no se justifica lookup sin auditoría); el único borrado de usuario es el descarte de un **borrador** (soft) |
| VIII | Validación dual | PASS | `AccountLineRules` en servidor para toda línea; `POST /documents/validate` da a la ventana los mismos errores por campo; DataAnnotations en modelos del cliente |
| IX   | Errores visibles | PASS | el contrato nunca omite en silencio (`Accounting.Parameterization.Missing` reemplaza el salto de `DisburseLoan`); el registro de accesos reintenta, registra y avisa una vez; los `catch` de las pantallas heredadas desaparecen con ellas |
| X    | Trazabilidad | PASS | cada comando (incluido `RegisterOptionAccess`) pasa por `AuditBehavior`; cambios de cuenta y configuración con antes/después; exportaciones y envíos por `AccountingAuditEmitter`; retención contable 10 años: índice TTL propio `ttl_occurredAt_10y` para `Module in (Accounting, Navigation)` en `AuditIndexBootstrap` (ver Complexity) |
| XI   | Inmutabilidad contable | PASS | documento contabilizado inmutable; corrección sólo por reversión referenciada; documento y líneas en `Entities.Accounting.Transactions` (la prueba prohíbe `Remove`); no existen tablas de saldos que «corregir» |
| XII  | Migraciones | **PASS con justificación** | `ContabilidadNiif` es destructiva (33 `DropTable`): guarda de datos en `Up()`, marcador `MIGRACION-DESTRUCTIVA-APROBADA`, respaldo y segundo revisor documentados, `Down` recrea el esquema anterior vacío; en par; DDL separado de las semillas (JSON en seeders idempotentes) |
| UI   | Indicador de carga | PASS | toda pantalla nueva con `IndicadorDeCarga` + `EstadoDeCarga`; `Contabilidad` entra a `ModulosMigrados` en E1 porque las 22 heredadas se retiran; sin colores literales ni `<style>` |

**Post-design re-check (Phase 1)**: sin cambios de estado. El diseño de `data-model.md` no cruza
bases ni expone `int Id`; `contracts/api.md` exige permiso en cada tramo (prueba de arquitectura);
`contracts/contabilizacion.md` deja un único escritor. Tres decisiones se justifican abajo.

## Complexity Tracking

| Violation / desviación | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Migración destructiva (33 `DropTable`) | el modelo heredado (saldos por columna-mes y por día, cabeceras sin líneas, 50 columnas muertas por tabla) contradice FR-046 y no tiene datos | alterar tabla por tabla: migración ilegible, `Down` imposible, y se conservaría el diseño que la spec prohíbe; los libros están vacíos (verificado el 2026-09-14) y la guarda de `Up()` lo vuelve a comprobar en cada base |
| `IApplicationDbContext.DescartarCambios()` y `ReintentoPorConcurrenciaBehavior` (contrato transversal nuevo) | numeración consecutiva sin huecos bajo concurrencia sin transacciones explícitas (FR-020) | `SEQUENCE` por tipo (DDL por proveedor desde handlers, huecos en fallo); 409 al usuario (viola el escenario 9 de US3) |
| Semillas como `EmbeddedResource` JSON (convención nueva en Persistencia) | ~1.500 filas por catálogo × 2, rubros y 69 formatos de exógena deben ser revisables por el contador | arreglos C# (patrón actual) ilegibles a esa escala; tabla global compartida (prohibida por IV) |
| TTL de auditoría de 10 años para contabilidad y accesos (hoy 5 años global) | FR-052: retención contable de diez años — sujeto a verificar que la versión de MongoDB del clúster admita dos índices TTL parciales sobre la misma clave (T009); si no, un solo TTL de 10 años y purga programada | subir todo a 10 años dobla el almacenamiento de auditoría de módulos que la norma no exige; un índice TTL parcial por `module` mantiene los 5 años del resto |

## Decisiones

| Decisión | Alternativa descartada | Por qué |
|---|---|---|
| Reemplazar el núcleo `ACC_` con guarda de datos y marcador (R1) | adaptar columna a columna; tablas nuevas junto a las viejas | libros vacíos; las FK de Nómina siguen válidas al conservar nombres de entidad |
| `AccountingPoster.PrepareAsync` agrega sin guardar; una sola `AccountLineRules` (R2) | `Send` anidado; eventos de dominio | atomicidad con un `SaveChanges`; «mismas reglas» por construcción |
| Número al contabilizar + `RowVersion` + índice único + reintento en servidor (R3) | `SEQUENCE`; 409 al usuario | sin huecos ni duplicados; cierra el 500 de `ConcurrencyConflictException` |
| Sin tablas de saldos; líneas con `Date`/`IsPosted` desnormalizados e índices (R4) | saldos mantenidos por el poster | ningún saldo puede divergir del libro (FR-046) |
| Catálogos y rubros en JSON embebido, sembrados por base; importador propio (R5, R6) | arreglos C#; base compartida | revisables por el contador; Principio IV |
| Sucursal = `COR_Branches`; principal en la configuración; alcance por `TenantBranchPublicId` (R7) | reapuntar la asignación existente | sin FK entre bases; no rompe `AssignBranch` |
| `PersonId` en catálogos institucionales y bancos (R8) | crear persona automática; no exigir tercero | decisión del dueño; Principio V |
| Cuatro ojos por `RegisteredByUserId` vs `UserId` (R9) | comparar nombres | identidad, no texto |
| `RegisterOptionAccessCommand` + `LocationChanged` en `MainLayout` con cola y reintento (R10) | `LogAccessAsync` (sin consulta ni TTL); middleware HTTP | pasa por `AuditBehavior`, consola y permisos existentes |
| `TablaExportable` a `Common.Reports`; `Entregar()` compartido; export auditado en el handler (R11) | comando de exportación aparte | precedente `ExportRunQuery`; sin duplicar consultas |
| `SfTreeGrid` para plan y libro auxiliar; grilla de líneas propia con teclado (R12) | `SfGrid` editable; `SfTreeView` | FR-031 y FR-042 exactos; no hay precedente que reutilizar |
| `ITabularFileReader` (Application) con ClosedXML en API (R13) | ClosedXML en Application | Principio II; tres importadores, un lector |
| Exógena como datos por año, XML con BCL, XSD en el prevalidador (R14) | validar XSD en el ERP | los XSD cambian cada año; el prevalidador es la autoridad |
| Activos: línea recta, calendario prospectivo, `ACC_AssetRuns.PeriodId` único (R15) | comprobar «ya corrió» en código | la unicidad es de la base |
| Módulos con su parametrización existente y `Accounting.Parameterization.Missing` (R16) | diseñar la contabilidad de cada módulo aquí | cada módulo es su propia feature; nada se omite en silencio |
| Cuatro entregas E1–E4 (R17) | un solo PR | 13 historias no caben en una revisión |

Detalle y alternativas en [research.md](research.md).

## Entregas

| Etapa | Historias | Contenido | Rama |
|---|---|---|---|
| **E1 núcleo** | US1, US2, US3, US4 (Nómina), US7 | migración, dominio y contrato, catálogos y semillas, configuración, plan y auxiliares, tipos y períodos (abrir/cerrar mes), digitación con borradores/contabilizar/reversar, Nómina por el contrato con terceros institucionales, permisos, auditoría de accesos, pantallas nuevas y retiro de las 22 heredadas, pruebas | `009-contabilidad-niif` |
| **E2 consultas, cierres y apertura** | US5, US6, US13 | libro auxiliar con profundización, balance de prueba, libros, estados financieros, saldo diario promedio, exportación auditada, cierre y reapertura de ejercicio, apertura importada | `009-e2-consultas-cierres` |
| **E3 módulos restantes** | US4 (escenario 7) | Cartera, Inventario, Tesorería, CDT/Ahorros por el contrato; parametrizaciones validadas; anulaciones por vínculo; consulta de parametrizaciones inválidas | `009-e3-modulos` |
| **E4 satélites** | US8–US12 | conciliación bancaria, presupuesto, impuestos y certificados, exógena, activos | `009-e4-satelites` |

Cada etapa se mergea a `develop`, se despliega a DEV/QA y se verifica según `quickstart.md`
antes de abrir la siguiente. Producción sólo con autorización expresa y respaldo previo.

## Project Structure

### Documentation (this feature)

```text
specs/009-contabilidad-niif/
├── spec.md                      # 13 historias, 88 requisitos, 8 decisiones
├── plan.md                      # este archivo
├── research.md                  # R1–R20: hallazgos de los cuatro mapas, norma, decisiones
├── data-model.md                # 30 tablas ACC_, columnas en otros módulos, migración
├── contracts/api.md             # rutas, permisos, cuerpos, errores
├── contracts/contabilizacion.md # contrato interno único y mapa módulo → líneas
├── quickstart.md                # pruebas, QA por etapa y por rol, producción
├── diagnostico-libros.sql       # guarda previa a la migración
├── checklists/requirements.md
└── tasks.md                     # Phase 2 (/speckit-tasks), por etapas E1–E4
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/Entities/Accounting/
  AccountingSetup.cs, AccountCatalog.cs, AccountCatalogEntry.cs, FinancialStatementItem.cs
  ChartOfAccount.cs (reescrita), AccountTaxRate.cs, VoucherType.cs (reescrita), CrossDocumentType.cs
  FiscalYear.cs, AccountingPeriod.cs (reescrita)
  Transactions/AccountingDocument.cs, Transactions/JournalEntry.cs        # Principio XI por namespace
  BankStatementColumnMap.cs, BankReconciliation.cs, BankStatementLine.cs   # E4
  Budget.cs, BudgetLine.cs, WithholdingCertificate.cs, WithholdingCertificateLine.cs, TaxForm.cs, TaxFormLine.cs
  ExogenousFormat.cs, ExogenousConcept.cs, ExogenousConceptAccount.cs, ExogenousRun.cs, ExogenousRunLine.cs
  FixedAsset.cs, FixedAssetInstallment.cs, AssetRun.cs
  Enums/AccountingModules.cs (flags), DocumentStatus.cs, DocumentKind.cs, VoucherUsage.cs, TaxKind.cs, …
  (se borran las 33 clases heredadas: AccountBalance, AccountGroup, …, WithholdingTaxLine)
src/Core/IngenIA365ERP.Domain/Entities/{Core/Branch.cs, Core/Bank.cs, Payroll/*Provider.cs, Payroll/FamilyCompensationFund.cs,
  Treasury/TreasuryConcept.cs, Lending/CreditLineParameter.cs, Lending/SavingsParameter.cs, CDT/CdtParameter.cs, Inventory/InventoryDocument.cs}
src/Core/IngenIA365ERP.Application/
  Common/Reports/TablaExportable.cs (movido desde Payroll/Reports)
  Common/Interfaces/Files/ITabularFileReader.cs
  Common/Interfaces/Security/IUserBranchScope.cs
  Common/Interfaces/IApplicationDbContext.cs (+ DescartarCambios, DbSets nuevos)
  Common/Behaviors/ReintentoPorConcurrenciaBehavior.cs (+ IReintentableAnteConcurrencia)
  Common/Audit/AuditEventTypes.cs (+ Accounting.*, Navigation.Opened)
  Audit/RegisterOptionAccess/RegisterOptionAccessCommand.cs
  Accounting/
    Rules/AccountLineRules.cs                     # UNA clase de reglas
    Posting/PostingRequest.cs, AccountingPoster.cs, AccountingErrors.cs
    Setup/{InitializeAccounting, UpdateAccountingSetup, ImportAccountCatalog, ValidateCatalog, AdoptCatalogUpdates}Command.cs, GetAccountingSetupQuery.cs, ListAccountCatalogsQuery.cs
    Accounts/{CreateAccount, UpdateAccount, SetAccountActive, DeleteAccount}Command.cs, {GetAccountTree, SearchAccounts, GetAccountByPublicId, GetAccountHistory, ListInvalidParameterizations}Query.cs, AccountEligibility.cs
    VoucherTypes/*, CrossDocumentTypes/*
    Periods/{OpenFiscalYear, ClosePeriod, ReopenPeriod, CloseFiscalYear, ReopenFiscalYear}Command.cs, ListPeriodsQuery.cs
    Documents/{SaveDraftDocument, DiscardDraft, PostDocument, ReverseDocument}Command.cs, {ValidateDraft, GetDocument, ListDocuments, GetDocumentBySource, PrintDocument}Query.cs
    Opening/ImportOpeningBalancesCommand.cs                                     # E2
    Reports/{Ledger, TrialBalance, Journal, GeneralLedger, ThirdPartyStatement, PendingDocuments, VoucherList,
             FinancialPosition, IncomeStatement, EquityChanges, CashFlow, DailyAverage}Query.cs, AccountingAuditEmitter.cs   # E2
    Reconciliation/*, Budgets/*, Taxes/*, Exogenous/*, Assets/*                # E4
  Payroll/Services/PayrollAccountingPoster.cs (sobre AccountingPoster; PersonId en líneas)
  Lending/*, Inventory/*, Treasury/*, CDT/* (los 12 escritores, sobre el contrato)          # E3
src/Infrastructure/IngenIA365ERP.Persistence/
  Configurations/Accounting/*.cs (reescritas), Configurations/{Core,Payroll,Treasury,Lending,CDT,Inventory}/… (columnas nuevas)
  Seeding/Parametric/Data/{puc-comercial,puc-solidario,rubros-niif,voucher-types,cross-document-types,exogena-2026}.json (EmbeddedResource)
  Seeding/Parametric/{AccountCatalogsSeeder, FinancialStatementItemsSeeder, VoucherTypesSeeder, CrossDocumentTypesSeeder, ExogenousSeeder}.cs
  DbContext/ApplicationDbContext.cs (DescartarCambios), DependencyInjection.cs
src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/*_ContabilidadNiif.cs (+ una por etapa si hace falta)
src/Infrastructure/IngenIA365ERP.Identity/Seed/AccountingPermissionCatalogSeeder.cs, BuiltInRolesSeeder.cs, PhaseZeroSecuritySeeder.cs
src/Infrastructure/IngenIA365ERP.Audit/Indexes/AuditIndexBootstrap.cs (TTL parcial 10 años)
src/Presentation/IngenIA365ERP.API/
  Endpoints/Accounting/{Setup, Accounts, VoucherTypes, CrossDocumentTypes, Periods, Documents, Opening, Reconciliations, Budgets, Taxes, Exogenous, Assets}Endpoints.cs (los 20 heredados se borran)
  Endpoints/Reports/AccountingReportsEndpoints.cs (reescrito), Endpoints/AuditLogModule.cs (+ /access), Endpoints/CatalogosEndpoints.cs
  Reports/EntregaDeInformes.cs (Entregar compartido), Reports/Importadores/ClosedXmlTabularFileReader.cs, Reports/VoucherPrintReport.cs (reescrito), Reports/WithholdingCertificateReport.cs
  Services/UserBranchScope.cs, Program.cs (registros; 409 para ConcurrencyConflictException)
src/Presentation/IngenIA365ERP.Shared/
  Services/Contabilidad/ContabilidadClient.cs (+ .Cuentas, .Documentos, .Periodos, .Informes, .Conciliacion, .Presupuesto, .Impuestos, .Exogena, .Activos), ContabilidadDtos.cs
  Services/Auditoria/RegistroDeAccesos.cs
  Components/Contabilidad/{LineasDeComprobante, BuscadorDeCuenta, ArbolDeCuentas, ReglasDeCuenta, LibroAuxiliar, EncabezadoDeInforme, MapeoDeExtracto, ConciliadorDePartidas, CuotasDeActivo}.razor
  Components/Shared/PersonSearchPicker.razor (+ Compacto), Components/Shared/AttachmentList.razor (movido desde Web.Client)
  Pages/Contabilidad/{ConfiguracionInicial, Catalogos, PlanDeCuentas, TiposDeComprobante, Periodos, Comprobantes, Comprobante, Borradores, Apertura,
                      LibroAuxiliar, Informes, EstadosFinancieros, Conciliacion, Presupuesto, Impuestos, Certificados, Exogena, Activos, ParametrizacionesInvalidas}.razor
  (se borran las 22 heredadas: BalancePrueba, CategoriasRiesgo, …, TiposComprobante)
  Layout/NavMenu.razor (grupo Contabilidad nuevo), Layout/MainLayout.razor (LocationChanged → RegistroDeAccesos)
  wwwroot/css/componentes.css (.grilla-lineas, .arbol-cuentas, .celda-error, .partida-conciliada…)
  IngenIA365ERP.Shared.csproj (+ Syncfusion.Blazor.TreeGrid)
src/Presentation/IngenIA365ERP.Web/Program.cs, IngenIA365ERP.Web.Client/Program.cs (registros)
docs/manual/contabilidad-contrato-de-contabilizacion.md, docs/manual/semillas-json.md, docs/operaciones/contabilidad-primer-ejercicio.md
tests/IngenIA365ERP.Application.Tests/Accounting/** , Audit/RegisterOptionAccessCommandTests.cs, Common/TestDbContextFactory.cs, Infrastructure/{RepartoDePermisosTests, LasSemillasJsonSonCoherentes}.cs
tests/IngenIA365ERP.Architecture.Tests/Principles/{NingunModuloEscribeMovimientosFueraDelContrato, LaContabilidadNoTieneCuentasEnCodigo, LosEndpointsProtegidosExigenPermiso (renombrada), PrincipioVIII_DualValidation, LasPantallasDicenQueEstanCargando, PrincipioXI_ContableImmutable}.cs
tests/IngenIA365ERP.API.IntegrationTests/Accounting/{ContabilidadNiif, Apertura, CierreDeEjercicio, Informes, Conciliacion, Presupuesto, Certificados, Exogena, Activos}Tests.cs, Payroll/ContabilidadDeNominaTests.cs, {Lending,Inventory,Treasury,CDT}/ContabilizacionTests.cs
```

**Structure Decision**: Clean Architecture de cuatro capas existente; el módulo vive en
`Application/Accounting` por área funcional (Rules, Posting, Setup, Accounts, …), con
`Transactions` separado en Domain para que el Principio XI lo cubra por namespace. Las pantallas
reemplazan a las heredadas bajo `Pages/Contabilidad` (mismo módulo, rutas nuevas) para que
`LasPantallasDicenQueEstanCargando` lo exija desde E1.

## Riesgos y mitigaciones

- **Migración destructiva en producción**: guarda en `Up()`, `diagnostico-libros.sql` antes y
  después, `pg_dump` aunque esté vacío, marcador con respaldo y revisor, despliegue sólo con
  autorización expresa.
- **Contenido de los catálogos y de la exógena 2026**: son transcripciones de norma; la
  validación del contador en QA (`POST /catalogs/{code}/validate`) es tarea explícita y
  bloqueante antes de producción; los JSON tienen prueba de coherencia estructural, no de
  contenido.
- **Módulos con contabilidad inexistente hoy**: E3 los conecta con lo que tienen; donde falta
  parametrización, la operación falla con mensaje (nunca en silencio); Tesorería, Cartera
  (provisión) y CDT/Ahorros ganan columnas de cuentas. Cada uno queda documentado en
  `contracts/contabilizacion.md` §4 y puede necesitar su propia feature contable después.
- **Rendimiento con saldos derivados**: índices definidos en `data-model.md`; prueba de carga
  `RUN_PERF_TESTS=1` con un millón de líneas en E2; resumen materializado diferido (R4).
- **Concurrencia de numeración**: reintento en servidor probado con dos clientes en la e2e;
  el 409 residual ya no es 500.
- **Grilla con teclado sin precedente**: componente propio con prueba manual en QA
  (`quickstart.md` §4.4); sin pruebas de navegador en el repo (regla de la casa).
- **`ChartOfAccount` en el contexto de pruebas**: hoy está `Ignore`d por su grafo; el modelo
  nuevo tiene pocas navegaciones (padre, reglas), se registra con `Ignore("RowVersion")`.
- **Tamaño**: cuatro etapas; `tasks.md` por etapa; ninguna etapa toca lo que otra ya entregó
  salvo por contrato.
