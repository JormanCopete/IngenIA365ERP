# Data Model: Contabilidad NIIF

**Feature**: 009 | **Date**: 2026-09-14

Esquema **nuevo** del módulo (`ACC_`), más las columnas que otros módulos ganan para vincularse.
Todas las entidades heredan de `AuditableEntity` (catálogos y configuración) o
`AuditableEntityLong` (documentos, líneas, extractos, corridas, cuotas): `PublicId`, soft-delete,
`RowVersion`, `CreatedAt/By`, `UpdatedAt/By`. Decimales `18,2` salvo donde se indica. Índices con
filtro se escriben en T-SQL (los traduce `ApplyPortableIndexFilters`). Nombres de entidad en
inglés; los que otros módulos referencian por FK **conservan su nombre**.

## 1. Configuración y catálogos

### `ACC_AccountingSetups` — `AccountingSetup` (una fila por empresa)

| Campo | Tipo | Regla |
|---|---|---|
| `CatalogId` | FK `ACC_AccountCatalogs` | catálogo con que se inició |
| `MovementLevel` | tinyint 5 \| 6 | FR-003 |
| `NiifGroup` | tinyint 1 \| 2 \| 3 | |
| `FirstFiscalYear` | int | |
| `ResultAccountId` | FK `ACC_ChartOfAccounts`, nullable | de movimiento; obligatoria al cerrar ejercicio |
| `MainBranchId` | FK `COR_Branches` | sucursal principal (R7) |
| `FourEyes` | bit | R9 |
| `ReconciliationDayTolerance` | int (3) | FR-057 |
| `TaxTolerance` | decimal (1.00) | pesos; FR-065 |
| `OpeningDocumentId` | FK `ACC_Documents`, nullable | FR-087 |
| `InitializedAt`, `InitializedBy` | | |
| `Locked` (calculado) | — | `∃ cuenta Origin=Company ∨ ∃ línea`: bloquea catálogo y nivel de movimiento (FR-004) |

### `ACC_AccountCatalogs` — `AccountCatalog`

`Code` (SOLIDARIO, COMERCIAL, o `PROPIO-{n}`), `Name`, `Version` (texto, p. ej. `2026.1`),
`Source` (Official \| Imported), `ImportedAt/By`, `ValidatedAt/By` (validación del contador,
FR-001), `EntryCount`. Único `Code` filtrado `[IsDeleted] = 0`.

### `ACC_AccountCatalogEntries` — `AccountCatalogEntry`

`CatalogId`, `Code` (1/2/4/6 dígitos según `Level`), `Name`, `Level` (1..4), `Nature` (`D`/`C`),
`NiifItemCode` (→ `ACC_FinancialStatementItems.Code`), `ParentCode`. Único `(CatalogId, Code)`.
Reglas del importador y de la prueba del JSON: padre existente, longitud por nivel, naturaleza
válida, rubro existente, sin duplicados.

### `ACC_FinancialStatementItems` — `FinancialStatementItem` (rubros NIIF, semilla por grupo)

`NiifGroup`, `Code`, `Name`, `Statement` (ESF \| ERI \| ECP \| EFE), `Section`, `Order`, `Sign`
(+1/−1), `ParentCode`. Único `(NiifGroup, Code)`.

### `ACC_ChartOfAccounts` — `ChartOfAccount` (plan de la empresa)

| Campo | Tipo | Regla |
|---|---|---|
| `Code` | nvarchar(12), único **filtrado `[IsDeleted] = 0`** | prefijo del padre; longitud por nivel: 7–9 dígitos en el 5, 10–12 en el 6 (FR-008); reinicializar con otro catálogo y recrear un código eliminado insertan de nuevo: una eliminada nunca colisiona |
| `Name` | nvarchar(150) | editable siempre |
| `Level` | tinyint 1..6 | |
| `Nature` | `D`/`C` | heredada (FR-011) |
| `ParentId` | FK self, null en nivel 1 | |
| `NiifItemCode` | | heredado |
| `Origin` | Catalog \| Company | catálogo: no se edita ni elimina (FR-007) |
| `IsMovement` | bit | `Level == Setup.MovementLevel`; lo escribe el handler |
| `IsActive` | bit | inactivar siempre permitido |
| `FirstMovementAt` | date, nullable | lo escribe el poster al primer movimiento; base de FR-012 |
| `EnabledModules` | int (flags) | Accounting=1, Payroll=2, Lending=4, Inventory=8, Treasury=16, Savings=32, Assets=64 |
| `RequiresThirdParty`, `RequiresCrossDocument`, `RequiresCostCenter`, `RequiresBranch` | bit | reglas de línea; `RequiresBranch` = elegir explícitamente (R7) |
| `BankId`, `BankAccountNumber` | FK `COR_Banks` nullable, nvarchar(30) | cuenta bancaria (FR-055) |
| `TaxKind` | tinyint nullable | None, Withholding, Vat, Ica, Gmf, IncomeTax (FR-065) |
| `TaxConceptCode` | nvarchar(20) nullable | |
| `RequiresTaxBase` | bit | |

Índices: `(ParentId)`, `(IsMovement, IsActive)`, `(BankId)` filtrado `[BankId] IS NOT NULL`.
**Bloqueo (FR-012)**: si `FirstMovementAt` cae en el ejercicio en curso, sólo `Name` e `IsActive`
cambian. **Eliminación (FR-013)**: sólo si `FirstMovementAt IS NULL` y ninguna parametrización
(`PAY_PayrollConceptDefinitionAccounts`, `LND_CreditLineParameters`, `INV_ProductAccounts`,
`INV_VatAccounts`, `CDT_Parameters`, `TRS_Concepts`, `COR_Banks`, `ACC_FixedAssets`,
`ACC_BudgetLines`, `ACC_ExogenousConceptAccounts`) la referencia.
**Reemplazo del plan (US1 esc. 2, FR-004)**: sólo con `Locked = false`; marca `IsDeleted` todas las
cuentas `Origin = Catalog`, copia el catálogo nuevo y actualiza `AccountingSetup.CatalogId`; queda
auditado con el catálogo anterior y el nuevo. Por eso el único de `Code` es filtrado.

### `ACC_AccountTaxRates` — `AccountTaxRate`

`AccountId`, `ValidFrom` (date), `Rate` (decimal 9,4). Único `(AccountId, ValidFrom)`.

### `ACC_VoucherTypes` — `VoucherType`

`Code` (`CodigoDeCatalogo`, 10, único), `Name`, `Usage` (Manual \| Module \| Closing \| Opening \|
Assets), `ModuleCode` (nvarchar(3), nullable: NOM, CAR, INV, TES, CDT, ACT), `NextNumber` (bigint,
1), `IsActive`, `IsSeeded` (bit: no se elimina ni cambia de uso). Semilla (`voucher-types.json`,
fuente única): `CG` manual; reservados por módulo `NM` Nómina; `DS`, `RC`, `CA`, `PV`, `DN` Cartera;
`AH` Ahorros; `CD` CDT; `FV`, `EI`, `SI` Inventario; `CH`, `FP`, `CB` Tesorería; `DP` activos; `AP`
apertura; `CI` cierre. Un código pertenece a un solo módulo (`ModuleCode`).

### `ACC_CrossDocumentTypes` — `CrossDocumentType`

`Code` (10, único), `Name`, `IsActive`, `IsSeeded`. Semilla: FV, FC, CC, NC, ND, CT, PG, OT.

## 2. Ejercicio y períodos

### `ACC_FiscalYears` — `FiscalYear`

`Year` (único), `Status` (Open \| Closed), `ClosingDocumentId` (FK nullable), `ClosedAt/By`,
`ReopenedAt/By/Reason`.

### `ACC_AccountingPeriods` — `AccountingPeriod`

`FiscalYearId`, `Month` (1..12), `StartDate`, `EndDate`, `Status` (Open \| Closed),
`ClosedAt/By`, `ReopenedAt/By/Reason`. Único `(FiscalYearId, Month)`.

**Transiciones**: `Open → Closed` (sin borradores del período; permiso `Periods.Close`);
`Closed → Open` (permiso `Periods.Reopen`, motivo, marca `Outdated` las conciliaciones cerradas
del período); ejercicio `Open → Closed` (12 cerrados, anterior cerrado, `ResultAccountId`
definido; crea documento `Closing`); `Closed → Open` (reversa el cierre; permiso
`Periods.CloseYear`).

## 3. Comprobantes (`Domain/Entities/Accounting/Transactions`, Principio XI)

### `ACC_Documents` — `AccountingDocument`

| Campo | Tipo | Regla |
|---|---|---|
| `VoucherTypeId` | FK | |
| `Number` | bigint, nullable | null en borrador; único `(VoucherTypeId, Number)` filtrado `[Number] IS NOT NULL` |
| `Date` | date | en período abierto (salvo `Opening`) y ≤ hoy |
| `Description` | nvarchar(200) | |
| `Status` | Draft \| Posted \| Reversed | |
| `Kind` | Regular \| Opening \| Closing \| Reversal | |
| `OriginModule` | nvarchar(3) | CNT, NOM, CAR, INV, TES, CDT, ACT |
| `SourceType`, `SourcePublicId` | nvarchar(60), uniqueidentifier, nullable | documento origen (FR-037); índice `(OriginModule, SourcePublicId)` |
| `PeriodId` | FK nullable | null sólo en `Opening` |
| `TotalDebit`, `TotalCredit` | | iguales al contabilizar |
| `RegisteredByUserId`, `RegisteredBy` | int, nvarchar(100) | |
| `PostedByUserId`, `PostedBy`, `PostedAt` | nullable | |
| `ReversesDocumentId`, `ReversedByDocumentId` | FK self, nullable | referencia en ambos sentidos (FR-030) |
| `ReversalReason` | nvarchar(200) nullable | obligatorio en reversión |

Índices: `(Date)`, `(Status)`, `(PeriodId)`. **Transiciones**: `Draft → Posted` (cuadra, líneas
válidas, período abierto, cuatro ojos, asigna número) · `Posted → Reversed` (crea el documento
`Reversal`, mismo tipo, líneas invertidas, fecha en período abierto) · un `Reversal` no se
reversa · un `Draft` se descarta (soft-delete: es lo único que se borra, y no es un movimiento).

### `ACC_JournalEntries` — `JournalEntry`

| Campo | Tipo | Regla |
|---|---|---|
| `DocumentId` | FK `ACC_Documents` (cascade no: restrict) | |
| `LineNumber` | int | |
| `AccountId` | FK | de movimiento, activa, habilitada para `OriginModule` |
| `BranchId` | FK `COR_Branches`, **obligatoria** | R7 |
| `CostCenterId` | FK nullable | según `RequiresCostCenter` |
| `PersonId` | FK `COR_People` nullable | según `RequiresThirdParty`; vigente |
| `CrossDocumentTypeId`, `CrossDocumentNumber` | FK nullable, nvarchar(30) | según `RequiresCrossDocument` |
| `Debit`, `Credit` | | exactamente uno > 0 |
| `Description` | nvarchar(200) | |
| `TaxBase` | nullable | obligatoria si `RequiresTaxBase`; `|Debit+Credit − TaxBase×Rate| ≤ TaxTolerance` |
| `Date`, `IsPosted` | date, bit | **desnormalizados** del documento al contabilizar (R4) |
| `ReconciledAt` | datetime nullable | conciliación (FR-057) |

Índices: `(AccountId, Date)`, `(PersonId, AccountId, Date)`, `(BranchId, Date)`, `(DocumentId)`,
`(CrossDocumentTypeId, CrossDocumentNumber, PersonId)`, `(CostCenterId, Date)`.

### `ACC_DocumentAttachments`

No existe: los soportes usan `COR_Attachments` con `OwnerEntityType = "AccountingDocument"`.

## 4. Conciliación bancaria

- `ACC_BankStatementColumnMaps` — `BankStatementColumnMap`: `AccountId` (único), `DateColumn`,
  `DateFormat`, `ReferenceColumn`, `DescriptionColumn`, `AmountColumn` o `DebitColumn`+`CreditColumn`,
  `SignConvention` (DebitPositive \| CreditPositive), `HeaderRows`, `Delimiter`.
- `ACC_BankReconciliations` — `BankReconciliation`: `AccountId`, `PeriodId`,
  `StatementOpeningBalance`, `StatementClosingBalance`, `BookBalanceAtClose`, `Status`
  (Open \| Closed \| Outdated), `ClosedAt/By`, `ReopenedAt/By/Reason`. Único `(AccountId, PeriodId)`.
- `ACC_BankStatementLines` — `BankStatementLine`: `ReconciliationId`, `LineNumber`, `Date`,
  `Reference`, `Description`, `Amount` (con signo), `Fingerprint` (SHA-256 de
  `date|ref|desc|amount|ocurrencia`, único por conciliación: detecta repetidos sin impedir dos
  movimientos idénticos legítimos), `JournalEntryId` (nullable, único filtrado), `MatchKind`
  (Auto \| Manual), `MatchedAt/By`, `DraftDocumentId` (borrador creado desde la partida).

## 5. Presupuesto

- `ACC_Budgets` — `Budget`: `FiscalYearId`, `Version` (int), `Status` (Draft \| Approved \|
  Superseded), `ApprovedAt/By`, `ChangeReason`. Único `(FiscalYearId, Version)`; una sola
  `Approved` por ejercicio.
- `ACC_BudgetLines` — `BudgetLine`: `BudgetId`, `AccountId` (de movimiento), `BranchId`
  nullable, `CostCenterId` nullable, `Month`, `Amount`. Único `(BudgetId, AccountId, BranchId,
  CostCenterId, Month)`.

## 6. Impuestos, certificados y formularios

- `ACC_WithholdingCertificates` — `WithholdingCertificate`: `PersonId`, `TaxKind`, `Year`,
  `PeriodFrom`, `PeriodTo` (nullable: anual), `Number` (consecutivo por `TaxKind`), `IssuedAt/By`,
  `Status` (Current \| Outdated \| Voided), `VoidedByCertificateId`, `LedgerFingerprint` (R20),
  `LastSentAt`, `LastSentTo`.
- `ACC_WithholdingCertificateLines`: `CertificateId`, `AccountId`, `ConceptCode`, `Base`, `Rate`,
  `Amount`.
- `ACC_TaxForms` — `TaxForm`: `Code` (350, 300, ICA…), `Name`, `IsActive`;
  `ACC_TaxFormLines` — `TaxFormLine`: `FormId`, `LineCode`, `Description`, `Selector`
  (Base \| Amount), `AccountPrefixes` (nvarchar(400), lista), `Sign`. Sin semilla obligatoria.

## 7. Exógena

- `ACC_ExogenousFormats` — `ExogenousFormat`: `TaxYear`, `FormatCode`, `FormatVersion`, `Name`,
  `Applies` (bit), `Origin` (Seed \| Custom), `MinAmount`, `MinorAmountsRule` (bit),
  `MinorAmountsTaxId` (`222222222`). Único `(TaxYear, FormatCode)`.
- `ACC_ExogenousConcepts` — `ExogenousConcept`: `FormatId`, `ConceptCode`, `Name`.
- `ACC_ExogenousConceptAccounts`: `ConceptId`, `ValueField` (nvarchar(40): columna del formato,
  p. ej. `PagoDeducible`, `RetencionPracticada`, `IvaDescontable`), `AccountPrefix` o `AccountId`,
  `Selector` (Debit \| Credit \| Net \| Base).
- `ACC_ExogenousRuns` — `ExogenousRun`: `TaxYear`, `FormatId`, `Version`, `Status` (Working \|
  Exported), `GeneratedAt/By`, `ExportedAt/By`, `XmlAttachmentPublicId`, `IssueCount`,
  `BlockingIssueCount`. Único `(FormatId, Version)`.
- `ACC_ExogenousRunLines`: `RunId`, `PersonId` (nullable si menores cuantías), `ConceptId`,
  `IdType`, `IdNumber`, `CheckDigit`, `Surname1`, `Surname2`, `Name1`, `Name2`, `BusinessName`,
  `Address`, `MunicipalityCode`, `DepartmentCode`, `Value1..Value10`, `IssuesJson`.

## 8. Activos fijos y diferidos

- `ACC_FixedAssets` — `FixedAsset`: `Code` (`CodigoDeCatalogo`), `Name`, `Kind` (Asset \|
  Deferred), `AssetAccountId`, `AccumulatedAccountId` (nullable en diferidos),
  `ExpenseAccountId`, `BranchId`, `CostCenterId` nullable, `SupplierPersonId` nullable,
  `PurchaseDate`, `PurchaseDocument`, `Cost`, `ResidualValue`, `LifeMonths`, `StartDate`,
  `Status` (Active \| FullyDepreciated \| Retired), `RetiredAt`, `RetireReason`,
  `RetireDocumentId`, `RetireCounterAccountId`.
- `ACC_FixedAssetInstallments` — `FixedAssetInstallment`: `AssetId`, `PeriodId`, `Amount`,
  `Status` (Pending \| Posted \| Reversed), `DocumentId` nullable. Único `(AssetId, PeriodId)`.
  Se regenera sólo `Pending` (FR-078).
- `ACC_AssetRuns` — `AssetRun`: `PeriodId` (**único**), `DocumentId`, `ExecutedAt/By`, `Status`
  (Posted \| Reversed), `ReversalDocumentId`.

## 9. Columnas que ganan otros módulos

| Tabla | Columna | Para |
|---|---|---|
| `COR_Branches` | `TenantBranchPublicId` (uniqueidentifier, nullable, único filtrado) | R7 alcance por usuario |
| `PAY_HealthInsuranceProviders`, `PAY_WorkRiskProviders`, `PAY_PensionProviders`, `PAY_SeveranceProviders`, `PAY_FamilyCompensationFunds`, `COR_Banks` | `PersonId` (FK `COR_People`, nullable) | FR-088 |
| `TRS_Concepts` | `DebitAccountId`, `CreditAccountId` (FK nullable) | R16 |
| `LND_CreditLineParameters`, `LND_SavingsParameters`, `INV_ProductAccounts`, `INV_VatAccounts`, `CDT_Parameters`, `COR_Banks` | sin columnas nuevas: los códigos existentes se resuelven a `AccountId` al contabilizar y se validan al guardar (FR-016) | R16 |
| `INV_Documents` (inventario) | `AccountingDocumentId` (FK nullable) | vínculo para anular (bug de `VoidInventoryDocument`) |

## 10. Migraciones (par PostgreSQL / SQL Server)

1. **`ContabilidadNiif`** (destructiva, con `MIGRACION-DESTRUCTIVA-APROBADA`, respaldo y segundo
   revisor en la cabecera): (a) guarda `Sql`: si `ACC_JournalEntries` o `ACC_Documents` tienen
   filas → `RAISE EXCEPTION` / `THROW` y no continúa; (b) `DropForeignKey` de `PAY_PayrollRuns` y
   `PAY_PayrollConceptDefinitionAccounts` hacia `ACC_*`; (c) `DropTable` de las 33 `ACC_`;
   (d) `CreateTable` del esquema nuevo; (e) `AddForeignKey` de vuelta; (f) columnas de §9.
   `Down`: recrea las 33 tablas heredadas vacías a partir del snapshot anterior (irreversible en
   datos por definición: no había).
2. **`SemillaContableInicial`**: no es migración; `AccountCatalogsSeeder` (Order 58),
   `FinancialStatementItemsSeeder` (59), `VoucherTypesSeeder` reemplaza a
   `PayrollVoucherTypeSeeder` (60), `CrossDocumentTypesSeeder` (61), `ExogenousSeeder` (62),
   `AccountingPermissionCatalogSeeder` (en `PhaseZeroSecuritySeeder`). Idempotentes por clave
   natural; corren en el alta de cooperativa y al arrancar (`SeedOrchestrator`).

Diagnóstico previo (sólo lectura): `diagnostico-libros.sql` — cuenta filas de `ACC_Documents`,
`ACC_JournalEntries`, `ACC_ChartOfAccounts` con `LegacyCode`, y parametrizaciones por código sin
cuenta correspondiente.
