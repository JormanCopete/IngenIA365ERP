# Contratos HTTP: Contabilidad NIIF

**Feature**: 009 | **Date**: 2026-09-14

Convenciones vigentes: envelope `{ code, message, traceId, data? }` (`ErrorEnvelopeFilter`);
`Validation.*` → 400, `*.NotFound` → 404, `Concurrency.*` → 409 (y desde esta feature también
`ConcurrencyConflictException` no atrapada), resto de negocio → **422**; sin permiso → **404
`Generic.NotFound`**. Todo identificador que cruza es `PublicId`. Fechas `yyyy-MM-dd`. Todas las
rutas de `/api/accounting` llevan `RequireAuthorization()` + `AddEndpointFilter<ErrorEnvelopeFilter>()`
+ `RequirePermission(...)` (una prueba de arquitectura lo exige tramo por tramo). Las 20 rutas
heredadas de `Endpoints/Accounting` **desaparecen** (FR-083); las de abajo son el módulo entero.

## 1. Permisos

| Recurso | Acciones | Notas de rol |
|---|---|---|
| `Accounting.Setup` | View, Manage | Manage sólo CompanyAdmin (patrón `*`) |
| `Accounting.Accounts` | View, Manage | `View` entra a `LecturaDeMaestros` (todo rol, también personalizados) |
| `Accounting.VoucherTypes` | View, Manage | `View` en `LecturaDeMaestros` |
| `Accounting.Vouchers` | View, Create, Post, Void | Operator += Create |
| `Accounting.Periods` | View, Close, Reopen, CloseYear | |
| `Accounting.Opening` | Manage | |
| `Accounting.Reports` | View, Export | Operator y Auditor += Export |
| `Accounting.Reconciliation` | View, Manage | |
| `Accounting.Budget` | View, Manage | |
| `Accounting.Taxes` | View, Manage, Certificates | |
| `Accounting.Exogenous` | View, Manage, Export | |
| `Accounting.Assets` | View, Manage, Run | |
| `AuditLog` | View, Export (existentes) | consulta de accesos |

`*.View` lo reciben los cuatro roles integrados por patrón; lo demás lo asigna el administrador.

## 2. Configuración y catálogos — `/api/accounting/setup`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /` | Setup.View | `{ initialized, catalogCode, movementLevel, level5Length, level6Length, niifGroup, firstFiscalYear, resultAccountPublicId, mainBranchPublicId, fourEyes, reconciliationDayTolerance, taxTolerance, locked, lockReason, openingDocumentPublicId }` |
| `POST /initialize` | Setup.Manage | `{ catalogCode, movementLevel, level5Length, level6Length, niifGroup, firstFiscalYear, mainBranchPublicId, fourEyes }` → 201 `{ accounts, periods }`; 422 `Accounting.Setup.AlreadyInitialized`, `Accounting.Setup.CatalogNotFound`, `Accounting.Setup.LengthsInvalid` |
| `PUT /` | Setup.Manage | mismos campos; catálogo/nivel/longitudes sólo si `!locked` (422 `Accounting.Setup.Locked` con `data: { companyAccounts, firstMovementAt }`); `fourEyes`, `resultAccountPublicId`, tolerancias siempre |
| `GET /catalogs` | Setup.View | `[{ code, name, version, source, entryCount, validatedAt }]` |
| `GET /catalogs/{code}/entries?level=` | Setup.View | entradas |
| `POST /catalogs/import` (multipart: archivo, `name`) | Setup.Manage | 201 `{ code, entryCount }` · 422 `Accounting.Catalog.Invalid` con `data: { errors: [{ row, column, code, message }] }` (nada a medias) |
| `POST /catalogs/{code}/validate` | Setup.Manage | registra la validación del contador (`validatedAt/By`) |
| `GET /catalogs/updates` · `POST /catalogs/updates/adopt` | Setup.View / Manage | cuentas nuevas de una versión de catálogo para la empresa iniciada (FR-006) |

## 3. Plan de cuentas — `/api/accounting/accounts`

| Ruta | Permiso | Notas |
|---|---|---|
| `GET /tree?parent=&onlyActive=` | Accounts.View | hijos de un nodo (raíz si `parent` vacío) para el `TreeGrid` |
| `GET /search?q=&module=&onlyMovement=true` | Accounts.View | buscador: código o nombre; sólo movimiento/activas/habilitadas para `module` (FR-015) |
| `GET /{id}` | Accounts.View | ficha completa + reglas + `firstMovementAt` + `references[]` (dónde está parametrizada) |
| `GET /{id}/history` | Accounts.View | eventos de auditoría de la cuenta (FR-018) |
| `POST /` | Accounts.Manage | `{ code, name, parentPublicId, enabledModules[], requiresThirdParty, requiresCrossDocument, requiresCostCenter, requiresBranch, bank?: { bankPublicId, accountNumber }, tax?: { kind, conceptCode, requiresTaxBase, rates: [{ validFrom, rate }] } }` → 201; 422 `Accounting.Account.CodeInvalid` (prefijo/longitud), `Accounting.Account.LevelNotAllowed`, `Catalogo.CodigoDuplicado` |
| `PUT /{id}` | Accounts.Manage | mismos campos; con movimientos en el ejercicio sólo `name`/`isActive` (422 `Accounting.Account.Locked`, `data: { firstMovementAt }`) |
| `POST /{id}/deactivate` · `/activate` | Accounts.Manage | |
| `DELETE /{id}` | Accounts.Manage | 422 `Accounting.Account.HasMovements` · `Accounting.Account.Referenced` con `data: { references }` |
| `GET /invalid-parameterizations` | Accounts.View | FR-017 y vínculos institucionales faltantes (FR-088) |
| `GET /api/catalogos/cuentas/codigo/{codigo}` | sesión | arma de `BuscarCodigoDeCatalogoQuery` para `CampoCodigo` |

## 4. Tipos de comprobante y documentos cruce

`/api/accounting/voucher-types` (`GET /`, `GET /{id}`, `POST /`, `PUT /{id}`, `POST /{id}/deactivate`)
con `VoucherTypes.View/Manage`; los sembrados no cambian de `usage` ni se eliminan (422
`Accounting.VoucherType.Seeded`). `/api/accounting/cross-document-types` igual.

## 5. Períodos — `/api/accounting/periods`

| Ruta | Permiso | Notas |
|---|---|---|
| `GET /?year=` | Periods.View | ejercicio + 12 períodos con estado |
| `POST /years` | Periods.CloseYear | abre el ejercicio `{ year }` (anterior cerrado o primero) |
| `POST /{year}/{month}/close` | Periods.Close | 422 `Accounting.Period.HasDrafts` con `data: { drafts[] }` |
| `POST /{year}/{month}/reopen` | Periods.Reopen | `{ reason }` obligatorio; marca conciliaciones `Outdated` |
| `POST /years/{year}/close` | Periods.CloseYear | 422 `Accounting.FiscalYear.PeriodsOpen`, `.PreviousOpen`, `.ResultAccountMissing`; → `{ closingDocumentPublicId }` |
| `POST /years/{year}/reopen` | Periods.CloseYear | `{ reason }`; reversa el cierre |

## 6. Comprobantes — `/api/accounting/documents`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?from=&to=&voucherType=&status=&origin=&number=&page=&pageSize=` | Vouchers.View | lista (respeta alcance de sucursal) |
| `GET /{id}` | Vouchers.View | cabecera + líneas + `origin { module, sourceType, sourcePublicId, link }` + `reversal { reversesPublicId, reversedByPublicId }` + `attachments[]` |
| `POST /drafts` | Vouchers.Create | `{ voucherTypeCode, date, description, lines: [{ accountCode, branchPublicId?, costCenterPublicId?, personPublicId?, crossDocumentType?, crossDocumentNumber?, debit, credit, detail?, taxBase? }] }` → 201 `{ publicId, errors[] }` (se guarda aunque tenga errores; sólo tipos manuales) |
| `PUT /drafts/{id}` | Vouchers.Create | ídem; 409 si el borrador cambió (`Concurrency.StaleRowVersion`) |
| `DELETE /drafts/{id}` | Vouchers.Create | descarta (soft) |
| `POST /validate` | Vouchers.Create | mismo cuerpo, sin guardar → `{ errors: [{ lineNumber, field, code, message, severity }], totalDebit, totalCredit, difference }` (`severity`: `Error` bloquea, `Aviso` no) (la ventana lo llama al salir de cada campo con debounce) |
| `POST /{id}/post` | Vouchers.Post | → `{ number }`; 422 `Accounting.Document.Unbalanced`, `Accounting.Document.Invalid` (`data.errors`), `Accounting.Document.FourEyes`, `Accounting.Period.Closed`; reintento interno ante concurrencia de numeración |
| `POST /{id}/reverse` | Vouchers.Void | `{ reason, date? }` → `{ reversalPublicId, number }`; 422 `Accounting.Document.NotPosted`, `.AlreadyReversed`, `.IsReversal`, `.ModuleOwned` (`data.origin`) |
| `GET /{id}/print` | Vouchers.View | PDF (`VoucherPrintReport` reescrito con origen y firmas) |
| `GET /by-source?module=&sourcePublicId=` | Vouchers.View | el comprobante de un documento de módulo |
| `GET /my-drafts` | Vouchers.Create | |

Soportes: `POST /api/attachments` con `ownerEntityType = "AccountingDocument"` (permisos de
adjuntos existentes); se listan en `GET /{id}`.

## 7. Apertura — `/api/accounting/opening`

`GET /template.xlsx` (Opening.Manage): plantilla con columnas `cuenta, tercero (documento),
tipoDocumento, numeroDocumento, centroCosto, sucursal, debito, credito, detalle` ·
`POST /import` (multipart) → 201 `{ draftPublicId, lines, errors[] }` (borrador tipo `AP`, o 422
`Accounting.Opening.Invalid` con errores por fila; nada a medias) · contabilizar y reversar por
`/documents`. 422 `Accounting.Opening.AlreadyExists` con `data: { openingDocumentPublicId }`.

## 8. Informes — `/api/reports/accounting/{vista}?format=json|xlsx|pdf|docx&…filtros`

Vistas: `ledger` (libro auxiliar, con `node=` para profundizar: `class|group|account|subaccount|
auxiliary|person|document|voucher`), `trial-balance`, `journal`, `general-ledger`,
`third-party-statement`, `pending-documents`, `voucher-list`, `financial-position`,
`income-statement` (`includeClosing=`), `equity-changes`, `cash-flow`, `daily-average`,
`budget-execution`, `withholdings`, `vat`, `ica`, `gmf`, `tax-form/{code}`, `reconciliation`,
`assets`. Filtros comunes: `from`, `to`, `accountFrom`, `accountTo`, `accountPublicId`, `person`,
`crossDocument`, `costCenter`, `branch`, `voucherType`, `origin`, `user`, `level`,
`withThirdParties`. `Reports.View` para `json`; `Reports.Export` para los demás (emite
`Accounting.Report.Exported`). Encabezado obligatorio en toda tabla: empresa, NIT, filtros,
período, usuario, fecha (FR-045).

## 9. Conciliación — `/api/accounting/reconciliations`

`GET /accounts` (cuentas bancarias) · `GET /{accountId}/map` · `PUT /{accountId}/map` (mapeo,
FR-056) · `POST /{accountId}/{year}/{month}/statement` (multipart: archivo; `preview=true`
devuelve las 10 primeras filas interpretadas) · `POST /{accountId}/{year}/{month}/lines` (digitar)
· `GET /{accountId}/{year}/{month}` (partidas por lado, propuestas, saldos) · `POST …/auto-match` ·
`POST …/match` `{ statementLinePublicId, journalEntryPublicId }` · `POST …/unmatch` `{ …, reason }`
· `POST …/draft-from-line` → `{ draftPublicId }` · `POST …/close` · `POST …/reopen` `{ reason }`.
Permisos `Reconciliation.View/Manage`. 422 `Accounting.Reconciliation.MapMissing`,
`.FileDoesNotMatchMap` (`data: { row, column }`), `.Closed`, `.Outdated`.

## 10. Presupuesto — `/api/accounting/budgets`

`GET /?year=` · `POST /` `{ year, lines: [{ accountPublicId, branchPublicId?, costCenterPublicId?,
amounts[12] }] }` · `POST /{year}/copy-from/{previousYear}?adjustPercent=` · `POST /{year}/distribute`
`{ accountPublicId, total, mode: equal|manual|percent, values? }` · `PUT /{year}` (crea versión con
`reason` si está aprobado) · `POST /{year}/approve` · ejecución por `/api/reports/accounting/budget-execution`.
`Budget.View/Manage`. 422 `Accounting.Budget.AccountNotMovement`.

## 11. Impuestos y certificados — `/api/accounting/taxes`

`GET /accounts` (cuentas de impuesto y tarifas) · `PUT /accounts/{id}` (reglas y vigencias) ·
`GET /forms` · `PUT /forms/{code}` (renglones) · `POST /certificates/generate` `{ kind, year,
periodFrom?, periodTo? }` → `{ issued, outdated }` · `GET /certificates?kind=&year=&person=` ·
`GET /certificates/{id}/pdf` · `POST /certificates/{id}/send` (correo; 422
`Accounting.EmailNotConfigured`, `Accounting.Certificate.PersonWithoutEmail`) ·
`POST /certificates/{id}/reissue`. `Taxes.View/Manage/Certificates`.

## 12. Exógena — `/api/accounting/exogenous`

`GET /{year}/formats` · `PUT /{year}/formats/{code}` (aplica, cuantía, conceptos y cuentas) ·
`POST /{year}/copy-from/{previousYear}` · `POST /{year}/formats/{code}/generate` → `{ runPublicId,
lines, issues, blockingIssues }` · `GET /runs/{id}` (líneas e inconsistencias con
`personPublicId`) · `GET /runs/{id}/export?format=xml|xlsx` (xml: 422 `Accounting.Exogenous.BlockingIssues`)
· `GET /{year}/formats/{code}/runs` (versiones). `Exogenous.View/Manage/Export`.

## 13. Activos — `/api/accounting/assets`

`GET /?status=` · `GET /{id}` (ficha, cuotas, proyección) · `POST /` · `PUT /{id}` (vida útil y
residual: recalcula pendientes) · `POST /runs/{year}/{month}` → `{ documentPublicId, assets,
amount }` (422 `Accounting.Assets.AlreadyRun`, `Accounting.Period.Closed`) · `POST /runs/{year}/{month}/reverse`
`{ reason }` · `POST /{id}/retire` `{ date, reason, counterAccountPublicId }` → `{ documentPublicId }`.
`Assets.View/Manage/Run`.

## 14. Auditoría de accesos

`POST /api/audit/access` `{ route, title }` (sesión + cooperativa; sin código propio) → 202. Se
audita como `RegisterOptionAccess` (módulo `Navigation`). Consulta: `GET /api/audit/logs?Module=Navigation&UserId=&From=&To=`
y sus exportaciones (`AuditLog.View/Export`).

## 15. Catálogos institucionales (otros módulos)

`PUT /api/payroll/health-providers/{id}` (y ARL, pensiones, cesantías, cajas) y
`PUT /api/core/banks/{id}` aceptan `personPublicId` (FR-088). `PUT /api/treasury/concepts/{id}`
acepta `debitAccountPublicId`, `creditAccountPublicId`. Las parametrizaciones de Cartera,
Inventario y CDT validan la cuenta al guardar (422 `Accounting.Account.NotEligible` con
`data: { accountCode, module, rule }`).

## 16. Errores de concurrencia

`ConcurrencyConflictException` pasa a mapearse a `409 Concurrency.StaleRowVersion` en el
manejador global (hoy 500). Los comandos marcados `IReintentableAnteConcurrencia` reintentan
antes de llegar ahí.
