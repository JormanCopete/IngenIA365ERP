# Data model — 016

Todo vive en la **base de cada cooperativa** (Principio IV) y es **aditivo**: tablas nuevas `COR_*`, ninguna columna nueva en
tablas existentes, ningún índice retirado. La base administrativa no cambia: las invitaciones de la US7 usan las tablas de la 017
tal como quedan (`ADM_CentralUsers`, `ADM_Invitations`, `ADM_TenantIdentityPolicies`).

Una sola migración par, `CargueDeImplantacion` (PostgreSQL y SQL Server), generada **después** de mezclar 015 y 017 en `develop`,
sobre su último snapshot. No se regenera ninguna migración congelada (013, 014, 017).

Toda entidad hereda `AuditableEntity` (Id interno, `PublicId`, `Created*`, `Updated*`, `IsDeleted`…) con `HasQueryFilter`, salvo
las de sólo inserción que se indican (que igual heredan y no se borran nunca: no hay comando de baja).

## 1. Perfiles de lectura

### `COR_ReadProfiles`

| Columna | Tipo | Nota |
|---|---|---|
| `Code` | varchar(20) | `CodigoDeCatalogo`, único entre vivos |
| `Name` | varchar(120) | |
| `TargetTemplate` | varchar(60) | clave de la plantilla destino (`core.people`, `accounting.opening`, `lending.migration`…) |
| `TargetPart` | varchar(40), nulo | parte de una carga por partes (`loans`, `installments`…) |
| `IsActive` | bool | |
| `CurrentVersionNumber` | int | la que se usa por omisión |

Índice único (`Code`) filtrado a vivos. `TargetTemplate`/`TargetPart` se validan contra `CatalogoDeDestinosDePerfil` (código: la lista
de plantillas que aceptan perfil, con su definición de columnas).

### `COR_ReadProfileVersions`

| Columna | Tipo | Nota |
|---|---|---|
| `ReadProfileId` | int FK | |
| `VersionNumber` | int | 1, 2, … único por perfil |
| `SourceVersionNumber` | int, nulo | el número que traía al importarse la configuración (R12) |
| `FileKind` | int | `Delimited = 0`, `FixedWidth = 1` |
| `Delimiter` | varchar(4), nulo | `;`, `,`, `\t`, `|` |
| `TextQualifier` | varchar(1), nulo | `"` por omisión en delimitado |
| `HasHeader` | bool | sólo delimitado |
| `Encoding` | varchar(20) | `utf-8`, `windows-1252`, `iso-8859-1` |
| `SkipFirstLines` | int | 0 |
| `SkipPatterns` | varchar(1000), nulo | JSON: lista de `{ kind: "StartsWith"|"Contains"|"Regex", value, position? }` |
| `SkipEmptyLines` | bool | true |
| `LineLength` | int, nulo | ancho fijo: largo esperado; nulo = no se exige |
| `UsedAt` | timestamp, nulo | se sella en el primer `apply`; desde entonces la versión y sus mapeos son inmutables |
| `Notes` | varchar(500), nulo | |

### `COR_ReadProfileMappings`

| Columna | Tipo | Nota |
|---|---|---|
| `ReadProfileVersionId` | int FK | |
| `TargetColumn` | varchar(60) | nombre de la columna de la plantilla destino; único por versión |
| `SourceKind` | int | `ColumnByName = 0`, `ColumnByIndex = 1`, `Position = 2`, `Constant = 3` |
| `SourceColumnName` | varchar(120), nulo | |
| `SourceColumnIndex` | int, nulo | base 1 |
| `Start`, `Length` | int, nulo | ancho fijo, base 1 |
| `Constant` | varchar(200), nulo | |
| `Trim` | int | `Both = 0`, `None = 1`, `Left = 2`, `Right = 3` |
| `PadChar` | varchar(1), nulo | relleno que se quita (ceros a la izquierda de un código, por ejemplo) |
| `DateFormat` | varchar(30), nulo | patrón exacto (`dd/MM/yyyy`, `yyyyMMdd`) |
| `EmptyDateValues` | varchar(100), nulo | lista separada por coma que significa «sin fecha» (`00/00/0000`) |
| `ThousandsSeparator`, `DecimalSeparator` | varchar(1), nulo | |
| `ImpliedDecimals` | int, nulo | `0001234` con 2 → `12.34` |
| `SignStyle` | int | `Leading = 0`, `Trailing = 1`, `Parentheses = 2` |
| `CodeEquivalenceTableId` | int FK, nulo | |
| `PassThroughWhenMissing` | bool | el código sin equivalencia se toma tal cual (FR-008) |
| `DefaultWhenEmpty` | varchar(200), nulo | valor si viene vacía |
| `ClearWhenEmpty` | bool | sólo plantillas que actualizan: vacío borra el dato (FR-015) |

**Reglas al guardar una versión** (`Import.Profile.*`):

- toda columna obligatoria de la plantilla destino tiene un mapeo (de archivo o constante) → `MissingRequiredColumn`;
- ningún `TargetColumn` repetido → `DuplicateTarget`; ninguno desconocido → `UnknownTarget`;
- en ancho fijo, `Start`/`Length` positivos y sin cruzarse → `OverlappingPositions`; en delimitado no se admite `Position`;
- `DateFormat` sólo en columnas de fecha; separadores sólo en numéricas; `ClearWhenEmpty` sólo si la plantilla actualiza;
- una versión con `UsedAt` no se modifica → `VersionSealed` (se crea la siguiente).

## 2. Equivalencias

### `COR_CodeEquivalenceTables`

| Columna | Tipo | Nota |
|---|---|---|
| `Code` | varchar(20) | único entre vivos |
| `Name` | varchar(120) | |
| `CodeKind` | int | `Account`, `Office`, `Branch`, `CreditLine`, `City`, `DocumentType`, `WithdrawalReason`, `AssociateGroup`, `Bank`, `Other` |

### `COR_CodeEquivalences`

| Columna | Tipo | Nota |
|---|---|---|
| `CodeEquivalenceTableId` | int FK | |
| `OldCode` | varchar(40) | normalizado: recortado, en mayúsculas |
| `NewCode` | varchar(40), nulo | nulo sólo en `NotMigrated` |
| `Kind` | int | `OneToOne = 0`, `ManyToOne = 1`, `OneToMany = 2`, `NotMigrated = 3` |
| `Note` | varchar(300), nulo | |

Índice único (`CodeEquivalenceTableId`, `OldCode`, `NewCode`) filtrado a vivos. Regla: un `OldCode` con más de una fila viva exige
que todas sean `OneToMany` (`Import.Equivalence.Conflicting`). Al traducir: `OneToOne`/`ManyToOne` dan `NewCode`; `OneToMany` →
`Import.Equivalence.OneToMany`; `NotMigrated` → `Import.Equivalence.NotMigrated`; sin fila → `Import.Equivalence.Missing` salvo
`PassThroughWhenMissing`.

## 3. Tablero

### Catálogos en código (no son tablas)

`PasosDeImplantacion` — código, nombre, orden por omisión, plantillas que lo alimentan, paso previo:

| Orden | Código | Plantillas | Totales de control (`TotalesDeControl`) |
|---|---|---|---|
| 1 | `EQUIVALENCIAS` | `implementation.equivalences*`, perfiles | `EQ_TABLAS`, `EQ_FILAS` |
| 2 | `USUARIOS` | `security.user-invitations` | `USU_INVITADOS`, `USU_ACTIVOS` |
| 3 | `PERSONAS` | `core.people` | `PER_TOTAL`, `PER_NATURALES`, `PER_JURIDICAS`, `PER_PROVEEDORES`, `PER_CLIENTES` |
| 4 | `ASOCIADOS` | `core.associates` | `ASO_VIGENTES`, `ASO_RETIRADOS` |
| 5 | `AUXILIARES` | `accounting.accounts` | `AUX_TOTAL` |
| 6 | `APERTURA` | `accounting.opening` | `APE_DEBITOS`, `APE_CREDITOS`, `APE_LINEAS` |
| 7 | `VINCULADOS_CUPO` | `lending.related-parties`, `lending.contribution-balances` | `VIN_TOTAL`, `CUP_SALDO` |
| 8 | `CARTERA` | `lending.migration` (partes) | `CAR_CREDITOS`, `CAR_CAPITAL`, `CAR_INTERES`, `CAR_MORA` (+ cuadre contable de la carga) |
| 9 | `APORTES` | `deposits.migration` (partes) | `APO_CUENTAS`, `APO_SALDO` (+ cuadre contable de la carga) |
| 10 | `INVENTARIO` | plantillas de la 012 y la puesta en marcha | `INV_PRODUCTOS`, `INV_VALOR` (los comparativos de la 012) |

Los totales con saldo comparan además con la contabilidad **reusando** el cuadre del módulo (`CuadreDeMigracion`,
`CuadreDeCaptaciones`) y, en la apertura, la suma de `MovimientosContables` del comprobante `AP`. Los códigos de la tabla son la
lista cerrada que decidió el dueño (research D7).

### `COR_ImplementationStepSettings`

| Columna | Tipo | Nota |
|---|---|---|
| `StepCode` | varchar(30) | de `PasosDeImplantacion`; único |
| `Order` | int | |
| `IsEnabled` | bool | |

Sin fila, el paso usa su orden por omisión y está habilitado (salvo `INVENTARIO`, que viene deshabilitado si la cooperativa no tiene
el módulo).

### `COR_ImplementationCutoffs`

| Columna | Tipo | Nota |
|---|---|---|
| `Name` | varchar(80) | «Corte del 27/11» |
| `CutoffDate` | date | |
| `IsCurrent` | bool | uno solo vigente (índice único filtrado) |

### `COR_ReferenceFigures`

| Columna | Tipo | Nota |
|---|---|---|
| `ImplementationCutoffId` | int FK | |
| `StepCode`, `TotalCode` | varchar(30) | `TotalCode` de `TotalesDeControl` del paso |
| `Value` | decimal(18,2) | conteos sin decimales |
| `Source` | varchar(200) | «Balance de prueba al 27/11 del sistema anterior» |

Único (corte, paso, total) entre vivos. Cambiar el valor deja la fila anterior de baja (historia).

### `COR_AcceptedDifferences` (sólo inserción)

| Columna | Tipo | Nota |
|---|---|---|
| `ImplementationCutoffId` | int FK | |
| `StepCode`, `TotalCode` | varchar(30) | |
| `Comparison` | int | `ReferenceFigure = 0`, `Accounting = 1` |
| `CalculatedValue`, `ExpectedValue` | decimal(18,2) | |
| `Reason` | varchar(500) | obligatorio |
| `AcceptedAt` | timestamp | |

Una diferencia aceptada vale mientras los dos valores sean los mismos; si cambia cualquiera, el total vuelve a «diferencia».

## 4. Cargues

### `COR_ImplementationLoads`

| Columna | Tipo | Nota |
|---|---|---|
| `StepCode` | varchar(30) | |
| `TargetTemplate`, `TargetPart` | varchar(60), varchar(40) nulo | |
| `ReadProfileVersionId` | int FK, nulo | nulo = archivo con la plantilla de siempre |
| `ImplementationCutoffId` | int FK, nulo | |
| `Mode` | int | `Review = 0`, `Apply = 1`, `Preview = 2` |
| `Status` | int | `Queued`, `Running`, `Succeeded`, `Invalid`, `Failed`, `Cancelled` |
| `AttachmentPublicId` | uuid | adjunto del archivo (dueño `ImplementationLoad`, R3) |
| `FileName` | varchar(260) | |
| `FileSha256` | char(64) | |
| `TotalLines`, `ProcessedLines` | int | avance |
| `Valid`, `Applied` | bool | |
| `ResultJson` | text | `ImportResultDto` con sus topes |
| `ModuleLoadPublicId` | uuid, nulo | la carga de migración de Cartera o de aportes, cuando la hay |
| `TargetParameters` | varchar(500), nulo | JSON: `replace`, fecha de la apertura, motivo |
| `OperationKey` | uuid | idempotencia (`Idempotency-Key`) |
| `RequestedByUserId` | int | `SEC_Users.Id` interno; sale como nombre |
| `StartedAt`, `FinishedAt` | timestamp, nulo | |
| `FailureMessage` | varchar(1000), nulo | error inesperado, ya en Serilog |

Transiciones: `Queued → Running → Succeeded | Invalid | Failed`; `Queued → Cancelled`. Un `Running` que se queda sin
arrendamiento (`COR_BackgroundLeases`) vuelve a `Queued` una vez y luego `Failed`. Un `apply` con `Succeeded` sella la versión del
perfil (`UsedAt`).

## 5. Parámetros (`COR_ParameterVersions`, ya existentes)

`Implantacion.MaximoDeBytesPorArchivo`, `Implantacion.MaximoDeLineasPorCargue`, `Implantacion.FilasDeVistaPrevia`,
`Invitaciones.MaximoDeFilasPorArchivo` (defectos en research R14), registrados en `ReglasDeParametrosPorModulo`.

## 6. Permisos (los siembra la API al arrancar)

| Código | Roles | Uso |
|---|---|---|
| `Implementation.Profiles.Manage` | CompanyAdmin | perfiles y tablas de equivalencias |
| `Implementation.Loads.Review` | CompanyAdmin, Operator | revisar y vista previa |
| `Implementation.Dashboard.View` | CompanyAdmin, Operator, Auditor, ReadOnly | ver el tablero |
| `Implementation.ReferenceFigures.Manage` | CompanyAdmin | cortes, cifras, ajustes de pasos |
| `Implementation.Differences.Accept` | CompanyAdmin | aceptar diferencias |
| `Implementation.Configuration.Export` | CompanyAdmin | exportar e importar la configuración |
| `Implementation.Loads.ExportPersonalData` | CompanyAdmin | libro de revisión con datos personales cuando el área del destino no tiene su propio permiso de exportar datos personales (personas, asociados) |
| `Security.Users.ImportInvitations` | CompanyAdmin | US7 |

El libro de revisión con datos personales exige **el permiso de exportar datos personales del área** del destino (FR-024):
`Lending.Reports.ExportPersonalData` en Cartera y aportes, `Accounting.Reports.Export` en apertura y auxiliares, y
`Implementation.Loads.ExportPersonalData` en personas y asociados, que hoy no tienen uno propio.

Aplicar un cargue exige, además, **el permiso de importar de la plantilla destino** (`Core.People.Create`, `Core.Associates.Create`,
`Accounting.Opening.Manage`, `Lending.Migration.Load`, …), comprobado al pedir y otra vez en el trabajo con los permisos capturados.

## 7. Lo que llega de la 017 y se usa sin cambiar

- `COR_People.Username` (único, incluye eliminadas) y el tipo `PT`: llave de `core.people` y `core.associates`.
- `COR_Positions`: el catálogo de cargos que escribe la hoja «Cargos» (`LegacyCode` = código).
- `ADM_TenantIdentityPolicies` (tipos aceptados, intentos) y el servicio de invitación con documento.
