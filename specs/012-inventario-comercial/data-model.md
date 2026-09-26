# Data Model: Inventario comercial renovado

**Feature**: 012 | **Date**: 2026-09-24 | **Rama**: `012-inventario-comercial`

Las §0 a §13 cubren las tablas `INV_` del núcleo del módulo —catálogo, bodegas,
kardex y sus proyecciones, documento genérico, tipos y numeración, períodos, conteos, traslados,
compras y puesta en marcha— más `COR_ParameterVersions`, donde el módulo guarda sus parámetros con
vigencia, `COR_BackgroundLeases` y las columnas que I1 agrega a tablas existentes (§0.1, §0.2), y el
retiro de las 23 tablas heredadas. Lo demás (mensajería, idempotencia, aprobaciones,
alertas, auditoría encadenada, impuestos y medios de pago de Core, POS, caja, precios, facturación
electrónica y el lado contable) va en las §14 a §27; en las primeras sólo se nombra cuando una tabla
de ellas lo referencia.

Los nombres son los de `decisiones-transversales.md` §2 (en la carpeta de la feature,
`specs/012-inventario-comercial/decisiones-transversales.md`), literales; las «T» y las preguntas al
dueño («D1», «E6»…) que se citan son las de ese documento y de `research.md` («Lo que falta del
dueño»). Donde este modelo agrega algo que allí no está —una columna, un índice, un código de error—
lo dice con **(nuevo)**; los nombres que agregan las §14 a §23 se listan en la §26.

---

## 0. Convenciones

- **Entidades**. Toda entidad hereda `AuditableEntity`: `int Id`, `PublicId` (uniqueidentifier, UK
  `UK_{Tabla}_PublicId`), `IsDeleted`/`DeletedAt`/`DeletedBy`, `RowVersion`, `CreatedAt/By`,
  `UpdatedAt/By`. `AuditableEntityLong` (`bigint Id`) sólo en los hechos de alto volumen:
  `KardexEntry` y `LayerConsumption`. Las columnas heredadas no se repiten en las tablas de abajo.
  `INV_DocumentLines` queda en `int`: a 5.000 documentos diarios de 10 líneas son 18 millones al año,
  más de un siglo antes del tope.
- **PublicId** (Principio VI). La API, los mensajes y las pantallas sólo ven `PublicId`. Entre
  módulos se referencia por `PublicId` o por código (la matriz contable), nunca por `Id`.
- **Borrado lógico** (Principio VII). Toda configuración declara `HasQueryFilter(e => !e.IsDeleted)`.
  Los catálogos con código llevan UK filtrado `[IsDeleted] = 0`. Las **proyecciones** nunca se dan
  de baja y sus UK van **sin** filtro de borrado (admiten `INSERT … ON CONFLICT`, T18). Los **hechos**
  (`IHechoInmutable`) no se modifican ni se borran: lo rechaza `ApplicationDbContext.SaveChangesAsync`.
- **Documentos confirmados** (`IInmutableTrasConfirmar`, T17). Después de `Confirmed` sólo cambian
  `Status` (→ `Voided`), `VoidedByDocumentId`, `FiscalNumberReleased` y la auditoría. Sus líneas y
  satélites quedan igual de fijos, salvo lo que cada tabla dice expresamente.
- **Decimales** (`Persistence/Configurations/Inventory/PrecisionDeInventario`, T19; prueba
  `LasCantidadesYCostosTienenSuPrecision`). La convención global (18,2) no cambia y en PostgreSQL
  `numeric(18,2)` **redondea en silencio**: por eso cada propiedad declara la suya. En las tablas se
  escribe el alias:

  | Alias | Tipo | Dónde |
  |---|---|---|
  | `Cantidad` | decimal(18,4) | cantidad de la línea y en unidad base, existencias, reservas, capas, conteos, mínimos y máximos |
  | `Factor` | decimal(18,6) | conversión de unidades |
  | `CostoUnitario` | decimal(18,6) | costo unitario del kardex, de la línea, del estado de costo y de las capas |
  | `PrecioUnitario` | decimal(18,6) | precio unitario de la línea de compra o venta |
  | `Monto` | decimal(18,2) | pesos: totales, costo total, valor del estado de costo, impuestos, valorizado |
  | `Tarifa` | decimal(9,6) | toda tarifa, como fracción (0,19; 0,00966) |

  Redondeo `MidpointRounding.AwayFromZero` según `Redondeo.Montos`; el residuo, según
  `Redondeo.Residuo` y siempre visible (FR-017).
- **Fechas** (T20). La fecha de negocio (`OperationDate`, cortes, vigencias) es `date` y se propone con
  `IDateTimeService.HoyLocal` (America/Bogota). Los instantes (`…At`) son `datetime` en UTC.
- **Enumeraciones**. Se guardan por su **valor numérico** (int), con los valores de §2.5 de las
  decisiones transversales, y los índices filtrados usan el número. Contabilidad guarda por nombre; aquí
  se sigue a nómina porque varios valores son códigos (`Receipt030 = 30`) y la API ya los devuelve como
  número.
- **Personas que actúan**. Todo `…ByUserId` es `SEC_Users.Id` resuelto por `IActorActual` (T6), nunca
  el entero del token ni el correo. `CreatedBy`/`UpdatedBy` (texto) siguen como en toda entidad.
- **Códigos** (`CodigoDeCatalogo`: mayúsculas, `^[A-Za-z0-9._-]+$`, 10 caracteres; 20 en producto,
  D1). Duplicado: `Catalogo.CodigoDuplicado` con el nombre del existente. **No cambian una vez
  creados** los que la matriz contable usa como dimensión (T27): grupo contable, bodega, causa de
  ajuste y tipo de documento. La pantalla no los ofrece editables.
- **Claves foráneas**: `Restrict` siempre. Nada se borra en cascada.
- **Concurrencia**. `RowVersion` en toda entidad (`xmin` en PostgreSQL, `rowversion` en SQL Server).
  En la confirmación manda además el **cerrojo pesimista** en orden canónico (T15,
  `ICerrojoDeInventario`): `INV_Setup` (compartido) → `INV_Warehouses` (compartido, por Id) →
  `INV_CostStates` → `INV_StockBalances` → `INV_StockDetails` (exclusivos, por Id) → fila de numeración
  al final. `RowVersion` es la segunda defensa (cubre la reconstrucción y todo camino que no bloquea).
  Las filas de proyección que faltan las crea el propio cerrojo con `INSERT … ON CONFLICT DO NOTHING`
  (PostgreSQL) o `INSERT … WHERE NOT EXISTS` (SQL Server), fuera de EF y del
  `AuditableEntityInterceptor`; por eso ese INSERT escribe él mismo `PublicId` (`gen_random_uuid()` /
  `NEWID()`), `CreatedAt` = UTC, `CreatedBy` = nombre del actor (`IActorActual`), `IsDeleted` = 0 y
  ceros en las cantidades y valores. Lo prueba `ConcurrenciaDeExistenciasTests` en los dos motores.
- **Escritores únicos**. Kardex y proyecciones: sólo `RegistroDeKardex`
  (`NadieEscribeElKardexFueraDelRegistro`). `Number`, `NextValue` y `LastIssuedNumber`: sólo
  `Numerador`/`NumeradorFiscal` (`SoloElNumeradorNumera`). Ninguna propiedad se llama `NextNumber`.
- **Auditoría**. El `AuditableEntityInterceptor` ampliado guarda antes y después de toda entidad salvo
  las marcadas `[SinDiffDeAuditoria]`; en §0–§13: `KardexEntry`, `LayerConsumption` y las
  proyecciones. El documento es la referencia de lo que el kardex registra (T36).
- **Entregas** (columna `E`). Migración en que nace cada tabla o columna: I1 `InventarioComercialNucleo`
  (las `COR_` de plataforma, en `PlataformaParaInventario`), I3 `VentasYPuntoDeVenta`, I5
  `ComprasYCosteoAvanzado`, I6 `ComercioAmpliado`. Una columna que apunta a una tabla de una entrega
  posterior nace con esa entrega, **salvo** `LotId` y `SerialId` (§3.0).
- **Tipos**. Se escriben en T-SQL (`nvarchar`, `bit`, `datetime`, `uniqueidentifier`); EF los traduce
  en PostgreSQL. Los filtros de índice van en T-SQL y los traduce `ProviderModelConventions`; no se usa
  `IN` ni `OR` en un filtro (se parte en dos índices).

### Mapa

```text
COR_Branches ─< INV_Warehouses ─< INV_WarehouseLocations          (una de tránsito por sucursal)
                     └─< INV_ReorderPolicies >─ INV_Products
INV_ProductCategories ─< INV_Products >─ INV_UnitsOfMeasure (unidad base)
INV_Brands ────────────────┘ │  ├─< INV_ProductUnits, INV_ProductBarcodes
INV_AccountingGroups ────────┘  ├─< INV_ProductTaxes >─ COR_TaxDefinitions (+ código de COR_TaxRates)
                                └─< INV_ProductAccountingGroupChanges
INV_DocumentTypes ─< INV_DocumentSequences, INV_DocumentTypeWarehouses
       │
INV_Documents ─< INV_DocumentLines ─< INV_KardexEntries ──▶ proyecciones: INV_StockBalances,
   │                    └─< INV_DocumentLineLinks            INV_StockDetails, INV_CostStates,
   ├─< INV_DocumentLinks, INV_DocumentPartySnapshots,        INV_CostLayers (I5)
   │   INV_DocumentTaxLines
   ├── 1:1 INV_SupplierInvoiceDetails,  ─< INV_SupplierInvoiceEvents
   └─< INV_TransferDiscrepancies, INV_CountSnapshotLines, INV_CountCaptures,
       INV_PurchaseMatchLines (I5), INV_LandedCostAllocations (I5)
INV_Setup ─ INV_Periods ─< INV_PeriodClosingBalances
INV_WarehouseActivations, INV_LegacyFigures, COR_ParameterVersions, INV_Salespeople (se conserva)
COR_BackgroundLeases; columnas nuevas en COR_People, COR_Cities, COR_Branches (§0.1, §0.2)
```

### 0.1 `COR_BackgroundLeases` — `BackgroundLease` (I1, `PlataformaParaInventario`; Principio IV, FR-083)

Arrendamiento de un trabajo de fondo **en la base de cada cooperativa**: cada réplica de la API toma el
trabajo de esa cooperativa con `UPDATE … WHERE LeaseUntil < ahora OR Owner = yo` y lo renueva mientras
corre, así que un trabajo nunca corre dos veces a la vez en la misma cooperativa. Lo lee y escribe sólo
`IArrendamientos` (`Application/Common/Execution`).

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Name` | nvarchar(100) | no | UK `UK_COR_BackgroundLeases_Name`. Nombres fijos: `integration.dispatch` (despachador de mensajes), `audit.forward` (reenviador de auditoría), `einvoicing.process` (procesador DIAN), `scheduled.tasks` (`ProgramadorDeTareas`), `email.dispatch` (correo) |
| `Owner` | nvarchar(150) | sí | identidad de la réplica que lo tiene (máquina, proceso y un Guid por arranque); nulo = libre |
| `LeaseUntil` | datetime | no | instante UTC en que vence; vencido, cualquiera lo toma |

`RowVersion` como en toda entidad (segunda defensa si dos réplicas toman a la vez). La fila de cada
nombre la siembra la migración; nunca se da de baja. `[SinDiffDeAuditoria]`: la renovación no es un
cambio que auditar.

### 0.2 Columnas nuevas en tablas existentes (I1, `PlataformaParaInventario`; FR-011, FR-013, FR-050)

| Tabla | Columna | Tipo | Nulo | Notas |
|---|---|---|---|---|
| `COR_People` | `IsVatResponsible`, `IsSelfWithholder`, `IsVatWithholdingAgent`, `IsSimpleTaxRegime`, `IsIncomeTaxFiler`, `IsObligatedToInvoice` | bit | no | `DEFAULT 0`, aditivas. Perfil tributario de la persona (condiciones del motor tributario por régimen de las partes, FR-013; `IsObligatedToInvoice = 0` es el vendedor del documento soporte). Se escriben **sólo** por `PersonInput`/`PersonaDialog` (feature 008, un solo sitio); se reutilizan las existentes `IsLargeContributor`, `WithholdingExempt`, `IcaWithholdingExempt` y `CiiuCode` |
| `COR_Cities` | `DaneCode` | nvarchar(5) | sí | código DIVIPOLA del municipio. UK `UK_COR_Cities_DaneCode` filtrado `[DaneCode] IS NOT NULL AND [IsDeleted] = 0`. Lo llena `DivipolaSeeder` (Order 82, `Data/divipola.json`), que actualiza por nombre las ciudades existentes y agrega las que faltan |
| `COR_Branches` | `MunicipalityDaneCode` | nvarchar(5) | sí | municipio de la sucursal, referido **por código** a `COR_Cities.DaneCode` (sin FK: el código es el dato). Lo escriben `CreateBranch`/`UpdateBranch` de la 009 (`municipalityDaneCode`, validado contra `COR_Cities.DaneCode`: 422 `Branch.MunicipalityUnknown`). Lo propone a `INV_Documents.OperationMunicipalityDaneCode` para ReteICA en compras (FR-050, T24) y es requisito de la carga de plantillas |
| `CMP_HabeasDataConsents` (`HabeasDataConsent`) | `Action` | nvarchar (existente) | no | nuevo valor de texto `Declined` (la persona no autoriza el tratamiento, FR-011) junto a `Accepted` y `Revoked`; sin cambio de esquema |

---

## 1. Catálogo

### 1.1 `INV_UnitsOfMeasure` — `UnitOfMeasure` (I1; FR-017, FR-025)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Code` | nvarchar(10) | no | `CodigoDeCatalogo`; UK filtrado `[IsDeleted] = 0` |
| `Name` | nvarchar(60) | no | |
| `Symbol` | nvarchar(10) | sí | lo que imprime la tirilla («und», «kg») |
| `AllowedDecimals` | tinyint | no | 0..4. Decimales que admite una cantidad expresada en esta unidad |
| `DianUnitCode` | nvarchar(3) | sí | UN/ECE Rec. 20 (`94`, `KGM`, `LTR`…). Obligatorio para vender en un documento electrónico: el canónico lo exige y `GuardiaDeEmisionFiscal` lo reporta |
| `IsSeeded` | bit | no | sembrada por `InventoryUnitsSeeder` (Order 77, `Data/inventario-unidades.json`) |
| `IsActive` | bit | no | inactiva: no se ofrece en productos nuevos |

**Reglas**. `AllowedDecimals` de una unidad usada en líneas confirmadas **sólo sube** (bajarlo haría
inválidas cantidades ya registradas). Una unidad sembrada no se elimina; se inactiva. Una unidad
usada como base o alterna de un producto vivo no se inactiva.

### 1.2 `INV_ProductCategories` — `ProductCategory` (I1; FR-024)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Code` | nvarchar(10) | no | UK filtrado `[IsDeleted] = 0` |
| `Name` | nvarchar(120) | no | |
| `ParentId` | int FK self | sí | nulo = raíz |
| `Level` | tinyint | no | 1..5; `= padre.Level + 1` |
| `Path` | nvarchar(100) | no | **(nuevo)** ruta materializada de Ids (`/3/17/42/`). La usan el conteo por categoría (incluye subcategorías) y los informes por categoría; se reescribe en los descendientes al mover una rama |
| `IsActive` | bit | no | |

Índices: `IX_INV_ProductCategories_ParentId`, `IX_INV_ProductCategories_Path`. **Reglas**: nivel
máximo 5 contando el descendiente más profundo de la rama que se mueve (`Inventory.Category.TooDeep`,
**nuevo**); sin ciclos; con productos o hijas vivas no se elimina, se inactiva.

### 1.3 `INV_Brands` — `Brand` (I1; FR-024)

`Code` nvarchar(10) (UK filtrado), `Name` nvarchar(120), `IsActive` bit. Renombrar una marca recalcula
`SearchText` de sus productos en el mismo `SaveChanges` (la búsqueda incluye la marca).

### 1.4 `INV_AccountingGroups` — `AccountingGroup` (I1; FR-027, FR-073)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Code` | nvarchar(10) | no | UK filtrado; **inmutable** (la matriz lo usa como `AccountingGroupCode`, T27) |
| `Name` | nvarchar(120) | no | |
| `Description` | nvarchar(300) | sí | qué agrupa, para la contadora |
| `IsActive` | bit | no | no se inactiva mientras algún producto vivo lo use |

Es la clasificación que Inventario envía en cada mensaje y que la matriz traduce a cuentas. Inventario
no guarda cuentas.

### 1.5 `INV_SalesChannels` — `SalesChannel` (I1)

`Code` nvarchar(10) (UK filtrado), `Name` nvarchar(80), `IsActive` bit. Lo referencian el tipo de
documento, el documento y, en §14–§15, el punto de venta y las listas de precios.

### 1.6 `INV_Products` — `Product` (I1; FR-020, FR-023 a FR-030)

| Columna | Tipo | Nulo | E | Notas |
|---|---|---|---|---|
| `Code` | nvarchar(20) | no | I1 | `CodigoDeCatalogo.LargoLargo` (D1); UK filtrado `[IsDeleted] = 0`. Búsqueda exacta |
| `Name` | nvarchar(200) | no | I1 | |
| `ShortName` | nvarchar(40) | sí | I1 | **(nuevo)** nombre para la tirilla de 80 mm; vacío = `Name` recortado |
| `Description` | nvarchar(1000) | sí | I1 | |
| `Kind` | int (`ProductKind`) | no | I1 | no cambia después del primer movimiento. En I1 sólo `Inventoriable` y `Service`; `Combo`, `Kit`, `Template` y `Variant` se habilitan en I6 |
| `Status` | int (`ProductStatus`) | no | I1 | por defecto `Active` |
| `CategoryId` | int FK `INV_ProductCategories` | no | I1 | obligatoria: sin ella el conteo por categoría y el margen por categoría dejan huecos |
| `BrandId` | int FK `INV_Brands` | sí | I1 | |
| `BaseUnitId` | int FK `INV_UnitsOfMeasure` | no | I1 | el kardex va siempre en ella; **no cambia** si el producto tiene movimientos (FR-025) |
| `AccountingGroupId` | int FK `INV_AccountingGroups` | sí | I1 | ver reglas |
| `VatSaleTreatment` | int (`VatSaleTreatment`) | no | I1 | `Taxed`, `Exempt`, `Excluded` (FR-027; entra al orden de `IvaDescontable`, FR-044) |
| `WithholdingConceptId` | int FK `COR_WithholdingConcepts` | sí | I1 | concepto de retención en compras (FR-027) |
| `Reference` | nvarchar(60) | sí | I1 | referencia del fabricante o del proveedor; entra a la búsqueda |
| `Weight` | decimal(18,4) | sí | I1 | kg por unidad base; prorrateo por peso (I5) |
| `Volume` | decimal(18,4) | sí | I1 | litros por unidad base; prorrateo por volumen (I5) |
| `TracksLot`, `TracksSerial`, `TracksExpiry` | bit | no | I1 | por defecto 0 (FR-026) |
| `IsPurchasable`, `IsSellable` | bit | no | I1 | **(nuevo)** por defecto 1. Un servicio de flete es sólo de compra; el POS no ofrece lo que no se vende |
| `SearchText` | nvarchar(400) | no | I1 | normalizado por `NormalizadorDeBusqueda` (minúsculas, sin tildes): código, nombre, nombre corto, referencia y marca |
| `ParentProductId` | int FK self | sí | I6 | variante → su plantilla |
| `VariantKey` | nvarchar(200) | sí | I6 | combinación normalizada (`COLOR=AZUL;TALLA=M`) |

**Índices**. `UK_INV_Products_Code` filtrado `[IsDeleted] = 0`; `IX` por `CategoryId`, `BrandId`,
`AccountingGroupId`, `Status`; `UK_INV_Products_Parent_VariantKey (ParentProductId, VariantKey)`
filtrado `[ParentProductId] IS NOT NULL AND [IsDeleted] = 0` (I6). Búsqueda mientras se escribe
(T43): en PostgreSQL `CREATE EXTENSION IF NOT EXISTS pg_trgm` e índice GIN `gin_trgm_ops` sobre
`SearchText`; en SQL Server índice no agrupado sobre `SearchText` con `INCLUDE (Code, Name, Status)`
(el `LIKE '%término%'` recorre un índice angosto en vez de la tabla).

**Reglas**.
- **Grupo contable**: obligatorio en todo producto que se mueve o se vende, es decir en todos salvo
  `Template`. FR-027 lo exige a los inventariables; a servicios y combos se les exige también porque la
  matriz asigna la cuenta de ingreso por `AccountingGroupCode` y sin él la validación previa detendría
  la venta (decisión de este modelo).
- **Concepto de retención**: obligatorio salvo en `Template` y `Combo` (no se compran).
- **Seguimiento**: `TracksLot`, `TracksSerial` y `TracksExpiry` se rechazan encendidos hasta I6;
  `TracksExpiry` exige `TracksLot`; ninguno cambia con existencia distinta de cero ni con borradores que
  citen el producto.
- **Clases de producto**: un `Service` nunca produce kardex ni tiene política de reorden. Un `Template`
  no entra a documentos. Un `Combo` (I6) no tiene existencia propia: su venta saca los componentes. Un
  `Variant` se comporta como inventariable.
- **Estados** (FR-028): `Inactive` no se ofrece en documentos nuevos (los borradores que ya lo tienen
  pueden confirmarse); `Blocked` rechaza todo movimiento salvo el conteo y la recepción de un traslado
  ya despachado (Edge Cases), con `Inventory.Product.Blocked` **(nuevo)**. Pasar a `Blocked` exige
  motivo (`IConMotivo`).
- **Borrado**: un producto con historia (líneas, kardex, códigos de barras usados) no se borra
  (`Inventory.Product.HasHistory`, **nuevo**); se inactiva.
- **Unidad base**: cambiarla con movimientos responde `Inventory.Product.BaseUnitLocked` **(nuevo)**.
- **Imágenes** (FR-029, T41): no hay columna. Son `COR_Attachments` con `OwnerEntityType =
  "InventoryProduct"` y `OwnerEntityPublicId = Product.PublicId`; sube quien tiene
  `Inventory.Catalog.Manage` por la subida directa de la feature 011 (`image/jpeg`, `image/png`,
  `image/webp`), se muestran en orden de `CreatedAt` y borrarlas queda auditado. Las reglas del dueño
  viven en `AdjuntosDeModulo.DeModulo`.
- **Importación** (FR-030): `ImportProductsCommand` con `ModoDeImportacion { Review, Apply }`, todo o
  nada, reglas compartidas con el alta unitaria; cambiar por plantilla el grupo de un producto con
  existencia pasa por §1.10.

### 1.7 `INV_ProductUnits` — `ProductUnit` (I1; FR-017, FR-025)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ProductId` | int FK `INV_Products` | no | |
| `UnitId` | int FK `INV_UnitsOfMeasure` | no | distinta de la unidad base del producto (la base tiene factor 1 implícito) |
| `Factor` | `Factor` | no | unidades base por una de ésta (caja ×12 → 12); > 0 |
| `UsedForPurchase`, `UsedForSale` | bit | no | al menos uno |
| `IsDefaultPurchase`, `IsDefaultSale` | bit | no | la que proponen compras y POS |

Índices: `UK (ProductId, UnitId)` filtrado `[IsDeleted] = 0`; `UK (ProductId)` filtrado
`[IsDefaultPurchase] = 1 AND [IsDeleted] = 0`; ídem para `IsDefaultSale`. **Regla**: la línea del
documento copia `Factor` al guardarse, así que cambiarlo sólo afecta lo que venga (queda auditado).

### 1.8 `INV_ProductBarcodes` — `ProductBarcode` (I1; FR-020, FR-024)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ProductId` | int FK | no | |
| `Barcode` | nvarchar(48) | no | recortado y en mayúsculas; GS1 o interno |
| `ProductUnitId` | int FK `INV_ProductUnits` | sí | empaque que identifica; nulo = unidad base. La lectura propone esa unidad y su factor |
| `IsPrimary` | bit | no | el que imprime la etiqueta |

Índice `UK_INV_ProductBarcodes_Barcode` filtrado `[IsDeleted] = 0`: único en la cooperativa entre vivos
(T43). Duplicado: `Inventory.Barcode.Duplicate` nombrando al producto que lo tiene. `UK (ProductId)`
filtrado `[IsPrimary] = 1 AND [IsDeleted] = 0`.

### 1.9 `INV_ProductTaxes` — `ProductTax` (I1; FR-013, FR-027)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ProductId` | int FK | no | |
| `TaxDefinitionId` | int FK `COR_TaxDefinitions` | no | IVA, INC, impuesto por unidad… |
| `TaxRateCode` | nvarchar(10) | sí | la tarifa por su **código estable**; el motor toma la vigencia de ese código a la fecha del documento. Nulo = el motor la elige por condiciones |
| `AppliesTo` | int (`TaxAppliesTo`) | no | `Purchases`, `Sales`, `Both`: el «tratamiento» del vínculo |
| `TaxableUnitsPerBaseUnit` | `Factor` | sí | sólo en impuestos `AmountPerUnit` (bolsa = 1; bebida de 1,5 L con IBUA por 100 ml = 15) |

`UK (ProductId, TaxDefinitionId)` filtrado `[IsDeleted] = 0`. La tarifa va por código y no por FK a
la fila porque cada vigencia de `COR_TaxRates` es una fila nueva: un Id quedaría apuntando a la vencida
(mismo criterio de la matriz, T27). **Reglas**: `VatSaleTreatment = Taxed` exige exactamente un
`ProductTax` de `Kind = Iva` con `TaxRateCode`; `Exempt` y `Excluded` no llevan fila de IVA (el
canónico emite el exento como IVA 0 por el tratamiento). El INC en compras va siempre al costo
(FR-044). Las retenciones no se vinculan aquí: salen de `WithholdingConceptId`.

### 1.10 `INV_ProductAccountingGroupChanges` — `ProductAccountingGroupChange` (I1; FR-027)

Historial del grupo contable. Lo escribe sólo `ChangeProductAccountingGroupCommand` (permiso
`Inventory.Catalog.ReclassifyAccountingGroup`, motivo obligatorio) y es el origen de
«GrupoContableReclasificado».

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ProductId` | int FK | no | |
| `FromAccountingGroupId` | int FK | no | |
| `ToAccountingGroupId` | int FK | no | distinto de `From` |
| `EffectiveDate` | date | no | por defecto `HoyLocal`; en período abierto y **no anterior** al último movimiento del producto (afecta sólo lo futuro) |
| `Quantity` | `Cantidad` | no | existencia total que cambia de grupo a esa fecha |
| `Value` | `Monto` | no | su valor |
| `DetailJson` | nvarchar(max) | no | `[{ warehouseId, warehouseCode, quantity, value }]`, el desglose por bodega que viaja en el mensaje |
| `Reason` | nvarchar(500) | no | |

Índice `(ProductId, EffectiveDate)`. **Reglas**: el comando toma el cerrojo de `INV_Setup` (compartido)
y de los `INV_CostStates` del producto para leer cantidad y valor coherentes, actualiza
`Product.AccountingGroupId` y agrega la fila en el mismo `SaveChanges`; con existencia distinta de cero
emite el mensaje (modo de paso general, T9), con existencia cero sólo deja la fila. **El grupo de un
producto a una fecha D** es el `To` del último cambio con `EffectiveDate ≤ D`; si D es anterior al
primer cambio, su `From`; sin cambios, el actual. Así el valorizado por grupo a una fecha pasada no
depende del grupo de hoy. Se trata como hecho: una equivocación se corrige con otro cambio (propuesta:
implementa `IHechoInmutable`, que T18 no lista).

### 1.11 Catálogo avanzado (I6; FR-023, FR-026, US15)

**`INV_ProductComponents` — `ProductComponent`**: `ProductId` (combo o kit), `ComponentProductId`,
`Quantity` (`Cantidad`, en la unidad base del componente, > 0). `UK (ProductId, ComponentProductId)`
filtrado. Un componente es `Inventoriable` o `Variant` (ni combo, ni kit, ni plantilla, ni servicio);
sin ciclos. El combo vendido saca sus componentes a su costo (costo de venta = suma); el kit entra por
el documento `Assembly` al costo de lo consumido.

**`INV_VariantAttributes` — `VariantAttribute`**: `Code` nvarchar(10) (UK filtrado), `Name`
nvarchar(60), `IsActive`. **`INV_VariantAttributeValues` — `VariantAttributeValue`**:
`VariantAttributeId`, `Code` nvarchar(10), `Name` nvarchar(60), `SortOrder` int; `UK
(VariantAttributeId, Code)` filtrado. **`INV_ProductVariantValues` — `ProductVariantValue`**:
`ProductId` (la variante), `VariantAttributeId` (desnormalizado para el índice),
`VariantAttributeValueId`; `UK (ProductId, VariantAttributeId)` filtrado. Todas las variantes de una
plantilla llevan valores para los mismos atributos, y `VariantKey` impide repetir la combinación. Cada
variante es un producto con código, códigos de barras y existencia propios (US15-1).

**`INV_Lots` — `Lot`**: `ProductId`, `Code` nvarchar(40) (el número del proveedor o del fabricante),
`ExpiryDate` date (obligatoria si `TracksExpiry`), `ManufactureDate` date nula. `UK (ProductId, Code)`
filtrado. Nace con la primera entrada que lo cita; las salidas proponen el que vence primero y
`Ventas.LoteVencido` decide si uno vencido se bloquea o se advierte.

**`INV_Serials` — `Serial`**: `ProductId`, `SerialNumber` nvarchar(60), `LotId` nulo;
`UK (ProductId, SerialNumber)` filtrado. Proyección propia (reconstruible desde el kardex):
`InStockWarehouseId` y `InStockLocationId` (nulos si no está en existencia). Una entrada con una serie
que ya tiene `InStockWarehouseId` se rechaza (`Inventory.Serial.AlreadyInStock`, **nuevo**; US15-5). En
I6 la fila de la serie entra al cerrojo después de `INV_StockDetails` (propuesta para
`ICerrojoDeInventario`).

---

## 2. Bodegas

### 2.1 `INV_WarehouseTypes` — `WarehouseType` (I1; FR-032)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Code` | nvarchar(10) | no | UK filtrado |
| `Name` | nvarchar(60) | no | |
| `Behavior` | int (`WarehouseBehavior`) | no | `Operational` o `Transit`; comportamiento fijo del sistema. No cambia si el tipo tiene bodegas |
| `IsSeeded`, `IsActive` | bit | no | |

Semilla `WarehouseTypesSeeder` (Order 78): principal, punto de venta, averías, cuarentena
(`Operational`) y tránsito (`Transit`). Que el comportamiento sea fijo impide que un tipo parametrizado
convierta una bodega de ventas en tránsito.

### 2.2 `INV_Warehouses` — `Warehouse` (I1; FR-032 a FR-034, FR-089 a FR-091)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Code` | nvarchar(10) | no | UK filtrado; **inmutable** (dimensión `WarehouseCode` de la matriz) |
| `Name` | nvarchar(120) | no | |
| `BranchId` | int FK `COR_Branches` | no | sucursal contable de la 009 |
| `WarehouseTypeId` | int FK | no | |
| `Behavior` | int (`WarehouseBehavior`) | no | **(nuevo)** copia inmutable del tipo, para el índice de abajo y para las reglas de clase sin unir tablas |
| `ActivationStatus` | int (`WarehouseActivationStatus`) | no | `NotActivated` (defecto) o `Active` |
| `CutoffDate` | date | sí | fecha de corte: la víspera de la activación; fecha del saldo inicial |
| `ActivatedAt` | datetime | sí | |
| `ActivatedByUserId` | int FK `SEC_Users` | sí | |
| `Address` | nvarchar(200) | sí | |
| `IsActive` | bit | no | |

**Índices**. `UK_INV_Warehouses_Code` filtrado `[IsDeleted] = 0`; `IX (BranchId)`;
`UK_INV_Warehouses_Branch_Transit (BranchId)` filtrado `[Behavior] = 2 AND [IsDeleted] = 0`: una sola
bodega de tránsito por sucursal.

**Bodega de tránsito** (FR-032, FR-039; research R18). Se crea con la primera bodega operativa de la
sucursal, en la misma transacción; su código lo propone el sistema (`TR` + código de la sucursal) y lo
puede fijar quien crea: el campo opcional `transitWarehouse { code, name }` de `POST /warehouses`, o
una fila de tipo tránsito en la plantilla de bodegas (el código es nomenclatura de la cooperativa,
nunca un identificador del sistema, y como dimensión de la matriz no cambia después, T27). No se crea
sola fuera de ese momento ni se da de alta aparte: un tipo `Transit` en cualquier otro alta responde
`Inventory.WarehouseType.TransitIsSystem`. Desde ella no se
vende ni se despacha; sólo recibe despachos y admite recepciones de traslado, devoluciones al origen y
bajas (regla de clase en `ClasesDeDocumento`). Su contenido cuenta en el valorizado y no en la
existencia física de ninguna bodega operativa. Pasa a `Active` en la misma transacción que la primera
bodega activa de su sucursal (no lleva saldo inicial). No se inactiva con existencia.

**Reglas**.
- **Activación** (FR-091): una bodega `NotActivated` sólo admite documentos `OpeningBalance` y su
  `Voiding` (`Inventory.Warehouse.NotActive`). Una `Active` sólo admite documentos con `OperationDate
  > CutoffDate`, salvo su saldo inicial. `NotActivated → Active` es la única transición y no tiene
  vuelta: la hace `ActivateWarehouseCommand` (§6.4), que toma la fila en exclusivo; la confirmación la
  toma compartida, así que no hay documento a medias durante la activación.
- **Inactivar** exige existencia cero en todas sus ubicaciones, ningún borrador ni despacho abierto que
  la cite (`Inventory.Warehouse.HasStock`, **nuevo**).
- **Alcance** (T35): los usuarios la operan por `INV_UserWarehouseScopes` (§21);
  la de tránsito se ve a través de los traslados de las bodegas del alcance.
- **Stock negativo** (FR-034): parámetro `Existencias.StockNegativoPermitido`, general con excepción
  por bodega (§4).

### 2.3 `INV_WarehouseLocations` — `WarehouseLocation` (I1; FR-032, FR-039)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `WarehouseId` | int FK | no | |
| `Code` | nvarchar(20) | no | `UK (WarehouseId, Code)` filtrado |
| `Name` | nvarchar(80) | no | |
| `IsDefault` | bit | no | `UK (WarehouseId)` filtrado `[IsDefault] = 1 AND [IsDeleted] = 0` |
| `IsActive` | bit | no | |

Toda bodega nace con su ubicación por defecto (la pantalla propone `GENERAL`), que es la que toma una
línea sin ubicación: por eso el kardex lleva `LocationId` obligatorio. La ubicación por defecto no se
elimina ni se inactiva; ninguna se inactiva con existencia.

### 2.4 `INV_ReorderPolicies` — `ReorderPolicy` (I1; FR-035)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ProductId` | int FK | no | inventariable o variante |
| `WarehouseId` | int FK | no | operativa |
| `MinimumQuantity` | `Cantidad` | no | ≥ 0 |
| `MaximumQuantity` | `Cantidad` | no | ≥ mínimo |
| `ReorderPoint` | `Cantidad` | no | entre 0 y el máximo |

`UK (ProductId, WarehouseId)` filtrado. Nada se guarda de lo calculado: **posición** = disponible + en
tránsito hacia la bodega + por recibir (órdenes aprobadas no recibidas; 0 hasta I5); alerta
`Inventario.Reorden` cuando posición ≤ punto; **sugerido** = máximo − posición, sólo para ésos;
**quiebre** (`Inventario.Quiebre`) cuando disponible < mínimo. Lo evalúa `ProgramadorDeTareas`.

---

## 3. Existencias, kardex y costo

### 3.0 Lote y serie en el núcleo

`LotId` y `SerialId` nacen en I1 como columnas nulas **sin FK** en `INV_KardexEntries`,
`INV_DocumentLines`, `INV_StockDetails` e `INV_CountSnapshotLines`; `ComercioAmpliado` (I6) crea
`INV_Lots`/`INV_Serials` y agrega las FK. Así los índices únicos de las proyecciones no se reescriben en
I6. Hasta I6 van siempre nulas.

### 3.1 `INV_KardexEntries` — `KardexEntry` (I1; hecho, `AuditableEntityLong`; FR-001 a FR-004, FR-042 a FR-046)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentId` | int FK `INV_Documents` | no | todo movimiento nace de un documento confirmado (FR-001) |
| `DocumentLineId` | int FK `INV_DocumentLines` | no | una línea produce una o varias entradas (el despacho: salida del origen y entrada al tránsito; el combo: una por componente) |
| `ProductId` | int FK | no | |
| `WarehouseId` | int FK | no | |
| `LocationId` | int FK `INV_WarehouseLocations` | no | |
| `LotId`, `SerialId` | int | sí | §3.0 |
| `OperationDate` | date | no | la del documento; en un ajuste por retroactivo, la de la salida afectada |
| `RegisteredAt` | datetime | no | instante real de la escritura |
| `Kind` | int (`KardexEntryKind`) | no | `Entry`, `Exit`, `CostAdjustment` |
| `Reason` | int (`KardexReason`) | no | `Normal`, `Retroactive`, `PriceDifference`, `LandedCost`, `NegativeRegularization`, `VoidDifference`, `RoundingResidue`, `MethodChange` |
| `QuantityBase` | `Cantidad` | no | con signo: + entrada, − salida, **0** en `CostAdjustment` |
| `UnitCost` | `CostoUnitario` | no | ≥ 0; costo con que se registró |
| `TotalCost` | `Monto` | no | con signo; en `CostAdjustment` es la diferencia |
| `CostScopeWarehouseId` | int | no | ámbito de costo: 0 = cooperativa, o el Id de la bodega. Sin FK (0 es centinela) |
| `CostMethod` | int (`CostMethod`) | no | sellado al escribir |
| `ReversesEntryId` | bigint FK self | sí | en la línea de un `Voiding`: la entrada que revierte, a su mismo costo |
| `AffectsEntryId` | bigint FK self | sí | en `CostAdjustment`: la entrada cuyo costo corrige |

**Índices**. `IX (ProductId, CostScopeWarehouseId, OperationDate, Id)` (motor de costo y
retroactivos); `IX (ProductId, WarehouseId, OperationDate, Id)` (kardex por bodega y valorizado a una
fecha, SC-017); `IX (OperationDate)`; `IX (DocumentId)`; `IX (AffectsEntryId)` filtrado `[AffectsEntryId]
IS NOT NULL`. El orden del kardex es `(OperationDate, Id)`; nunca se ordena por `RegisteredAt` ni por
`PublicId`.

**Invariantes**. Sólo inserción (`IHechoInmutable`) y un solo escritor (`RegistroDeKardex`).
`QuantityBase = 0 ⇔ Kind = CostAdjustment`; el signo concuerda con `Kind`. En `Entry`/`Exit`,
`TotalCost = round(QuantityBase × UnitCost)` salvo la línea `RoundingResidue`, que lleva el residuo
para que el valor quede en 0 cuando la cantidad llega a 0 (FR-017). El saldo acumulado no se guarda: el
informe de kardex lo calcula en orden. Anular, retroactivos, prorrateos, diferencias de precio y
negativos regularizados **agregan** líneas (FR-002). ~9 millones de filas al año a la escala de
referencia.

**Reglas de costo que el kardex deja escritas** (`MotorDeCosteo`, casos dorados 01..17):
- compras: al costo neto de descuentos no condicionados más los impuestos `AddedToCost` (IVA no
  descontable según `IvaDescontable.Determinar`, INC) (FR-044);
- salidas: al promedio del ámbito (o por capas en PEPS, I5); con existencia cero el promedio conserva
  `LastUnitCost`; con negativo permitido la salida toma el último costo y la entrada que llega agrega la
  diferencia como `NegativeRegularization`;
- devolución de cliente: al costo con que salió (la línea de venta enlazada); devolución a proveedor: al
  costo de la línea de recepción enlazada, y la diferencia con el promedio como `CostAdjustment` con
  `VoidDifference` (el mismo motivo que la anulación de una entrada ya promediada, FR-075);
- traslados: salen del origen al costo de origen y la salida del tránsito va al costo de la línea de
  despacho (identificación específica: el tránsito queda en cero exacto al resolverse);
- anulación (`Voiding`): cada línea revierte su entrada con `ReversesEntryId` y el costo del original;
  si la entrada anulada ya había entrado al promedio, la diferencia contra el promedio vigente es
  `VoidDifference`;
- diferencia de precio (factura contra recepción), prorrateo y retroactivo: líneas `CostAdjustment`
  con `AffectsEntryId`, bajo el documento que las causa (la factura, el `LandedCost`, el documento
  retroactivo), separadas en la porción en existencia (a inventario) y la vendida (a costo de venta),
  una por documento afectado para el mensaje «AjusteDeCostoReconocido» (FR-045, FR-046, FR-075);
- **retroactivo**: un documento es retroactivo si deja un movimiento con fecha anterior a otro ya
  registrado del mismo producto y ámbito. Se rechaza mientras `Costeo.RetroactivosPermitidos = false`
  (siempre hasta I5) nombrando el movimiento posterior, **salvo dos clases que el sistema admite desde
  I1 sin mirar el parámetro** (precisión aplicada a la spec sobre FR-045; preguntas D8 y D9, con esta
  excepción como propuesta por defecto): el `OpeningBalance` (y su `Voiding`) de una bodega
  `NotActivated` (§10) y los ajustes que genera un conteo aprobado (§8). Para ellas I1 entrega un
  **retroactivo mínimo**: el motor inserta sus entradas en el orden `(OperationDate, Id)`, verifica
  que ningún saldo intermedio del ámbito quede negativo (con el negativo prohibido), recalcula las
  salidas posteriores cuando el promedio cambia (líneas `CostAdjustment` `Retroactive` con
  `AffectsEntryId`, separadas en existencia y vendido) y registra un «AjusteDeCostoReconocido» por
  documento afectado. El documento retroactivo general (cualquier otra clase, con el parámetro y sus
  días máximos) sigue siendo de I5;
- cambio de método o de ámbito (I5): un documento `CostAdjustment` generado por el sistema con líneas
  `Reason = MethodChange`, `QuantityBase = 0`, que marcan desde dónde la reconstrucción usa el método
  nuevo.

### 3.2 `INV_StockBalances` — `StockBalance` (I1; proyección; FR-003, FR-004, FR-033)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ProductId` | int FK | no | |
| `WarehouseId` | int FK | no | incluidas las de tránsito |
| `Physical` | `Cantidad` | no | = Σ `QuantityBase` del kardex de (producto, bodega) |
| `Reserved` | `Cantidad` | no | = Σ (`QuantityBase` − `ConsumedQuantityBase`) de las `INV_Reservations` con `Status = Active` (I6, §3.6); 0 hasta entonces |
| `LastMovementDate` | date | sí | **(nuevo)** fecha del último movimiento; alimenta «sin movimiento» y el tablero |

`UK_INV_StockBalances_Product_Warehouse (ProductId, WarehouseId)` **sin filtro**. Nunca se da de baja.
**Disponible** = `Physical − Reserved` (no se guarda); con el negativo prohibido, una salida que lo deja
bajo cero se rechaza con `Inventory.Stock.Insufficient` y `data.available` (FR-004). **En tránsito
hacia la bodega W** (no se guarda): Σ de las líneas de despachos confirmados no anulados con
`DestinationWarehouseId = W`, menos lo recibido en ellas (recepción ordinaria y tardía), menos lo
devuelto al origen y lo dado de baja desde el tránsito. Fila exclusiva en el cerrojo; `[SinDiffDeAuditoria]`.

### 3.3 `INV_StockDetails` — `StockDetail` (I1; proyección)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ProductId`, `WarehouseId`, `LocationId` | int FK | no | |
| `LotId` | int | sí | §3.0 |
| `Quantity` | `Cantidad` | no | Σ kardex de la combinación |

Dos índices únicos (PostgreSQL trata los nulos como distintos): `UK (ProductId, WarehouseId,
LocationId)` filtrado `[LotId] IS NULL` y `UK (ProductId, WarehouseId, LocationId, LotId)` filtrado
`[LotId] IS NOT NULL`. El filtro es por lote, no por borrado: la proyección nunca se da de baja (el
`ON CONFLICT` del cerrojo nombra el índice parcial). **Invariante**: Σ `Quantity` por (producto,
bodega) = `StockBalance.Physical`.

### 3.4 `INV_CostStates` — `CostState` (I1; proyección; FR-042, FR-043)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ProductId` | int FK | no | |
| `ScopeWarehouseId` | int | no | 0 = cooperativa; o la bodega (el tránsito tiene el suyo en ámbito bodega) |
| `Method` | int (`CostMethod`) | no | el vigente para el ámbito |
| `Quantity` | `Cantidad` | no | Σ `QuantityBase` del ámbito |
| `Value` | `Monto` | no | Σ `TotalCost` del ámbito, ajustes incluidos |
| `AverageCost` | `CostoUnitario` | no | `Value / Quantity` si `Quantity > 0`; si no, conserva `LastUnitCost` |
| `LastUnitCost` | `CostoUnitario` | no | último costo de entrada: lo usa la salida en negativo y la existencia cero |

`UK (ProductId, ScopeWarehouseId)` sin filtro. **Valor por bodega** = cantidad de la bodega × promedio
de su ámbito, con el residuo asignado por `Redondeo.Residuo`; nunca Σ `TotalCost` por bodega (en ámbito
cooperativa eso deriva: una bodega que recibió a 1.000 y vende al promedio de 1.150 quedaría negativa).
Con `Quantity = 0`, `Value = 0` siempre (lo asegura la línea `RoundingResidue`).

### 3.5 `INV_CostLayers` — `CostLayer` e `INV_LayerConsumptions` — `LayerConsumption` (I5; FR-043, US16)

**`CostLayer`** (proyección): `ProductId`, `ScopeWarehouseId`, `EntryKardexEntryId` (bigint FK: la
entrada que creó la capa; en el cambio de método, la línea `MethodChange` con la existencia al
promedio), `OperationDate` date, `OriginalQuantity` y `RemainingQuantity` (`Cantidad`), `UnitCost`
(`CostoUnitario`). `UK (EntryKardexEntryId)`; `IX (ProductId, ScopeWarehouseId, OperationDate,
EntryKardexEntryId)` es el orden PEPS. Una devolución de cliente crea capa a su costo de salida.

**`LayerConsumption`** (hecho, `AuditableEntityLong`): `ExitKardexEntryId` bigint FK, `LayerId` FK,
`Quantity` (`Cantidad`, negativa en la corrección de un retroactivo), `UnitCost`. `IX
(ExitKardexEntryId)`, `IX (LayerId)`.

**Invariantes**: `0 ≤ RemainingQuantity = OriginalQuantity − Σ Quantity` de sus consumos; Σ consumos de
una salida = |`QuantityBase`| de la salida. Retroactivos con PEPS: la propuesta por defecto (D6) los
restringe al promedio ponderado.

### 3.6 `INV_Reservations` — `Reservation` (I6; FR-052, US14-1)

Definida en §14 (`INV_Reservations`, I6), que es su única definición. `INV_StockBalances.Reserved`
= Σ (`QuantityBase` − `ConsumedQuantityBase`) de las reservas con `Status = Active`; la escribe la
misma transacción de la reserva (fila exclusiva del cerrojo); `ProgramadorDeTareas` vence las
expiradas.

### 3.7 Verificación y reconstrucción

No hay tablas. `VerificacionDeIntegridad` compara por `GROUP BY` cada proyección con el kardex
(`Physical`, `Quantity` por ubicación y lote, `Quantity`/`Value` por ámbito, capas contra consumos,
`Reserved` contra reservas) y levanta `Inventario.IncidenteDeIntegridad` por diferencia (FR-003,
SC-006). `RebuildInventoryProjectionsCommand` (`Inventory.Integrity.Rebuild`) las recalcula desde el
kardex bajo el cerrojo; nunca toca el kardex.

---

## 4. Parámetros con vigencia del módulo

### 4.1 `COR_ParameterVersions` — `ParameterVersion` (I1, `PlataformaParaInventario`; FR-012)

Tabla de plataforma (T21); la usa primero este módulo. Único lector: `LectorDeParametros`; único
escritor: `AddParameterVersionCommand`.

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Module` | nvarchar(10) | no | `INV`, `TAX`, `EINV` |
| `Key` | nvarchar(80) | no | de un catálogo cerrado (`ParametrosDeInventario`, `ParametrosTributarios`, `ParametrosDeFacturacionElectronica`) |
| `ScopeKind` | int (`ParameterScopeKind`) | no | `None` = general |
| `ScopeId` | int | no | 0 si `None`; si no, el Id de la bodega, tipo de documento, punto, caja o tipo de tercero. Sin FK (polimórfico): lo valida el comando |
| `Value` | nvarchar(2000) | no | texto; el lector lo tipa según `DefinicionDeParametro` |
| `ValidFrom` | date | no | |
| `ValidTo` | date | sí | |
| `Reason` | nvarchar(500) | no | motivo obligatorio (`IConMotivo`) |
| `LegalSource` | nvarchar(200) | sí | norma o acta que lo respalda (plazo de contingencia, cambio de método) |

`UK (Module, Key, ScopeKind, ScopeId, ValidFrom)` filtrado `[IsDeleted] = 0`.

**Reglas**. Valor fuera de lo admitido: `Parameters.ValueNotAllowed` (nunca se cae al defecto). Dos
vigencias de la misma clave y ámbito no se cruzan (`Parameters.Overlaps`); la nueva cierra la anterior
la víspera. Una vigencia que ya empezó no se edita ni se borra; una futura se retira con motivo (baja
lógica, auditada) y la anterior recupera su `ValidTo`. Lectura a una fecha: ámbito → general → defecto
seguro de la definición. **Sellado**: el modo de paso, el modo de emisión y la numeración se leen a la
fecha de **confirmación** y se copian al documento; lo demás, a su `OperationDate` (FR-012). Reglas por
clave: `Costeo.Metodo` y `Costeo.Ambito` sólo desde el primer día de un período abierto sin movimientos
posteriores y con `Inventory.Costing.Manage` (`Parameters.RequiresPeriodStart`); `Contabilidad.ModoDePaso`
por tipo se declara para **todos** los tipos de su cadena (`Inventory.PostingMode.ChainMismatch`) y
dejar sin paso un tipo fiscal —propio o heredado— exige `Inventory.DocumentTypes.DisableFiscalPosting` y
una confirmación que nombra esos tipos (`Inventory.PostingMode.FiscalRequiresConfirmation`).

### 4.2 Catálogo de claves (`INV`, `TAX`, `EINV`; el de `decisiones-transversales.md` §2.8 más una clave nueva)

Catálogo cerrado y completo de las claves del módulo, de las dos partes: lo que no está aquí no se
puede registrar (`Parameters.KeyNotFound`). «Sellado» = se fija en el documento al confirmar. Un valor
no admitido es `Parameters.ValueNotAllowed`, nunca el defecto.

| Module | Key | Valores | Defecto | Ámbitos | E | FR |
|---|---|---|---|---|---|---|
| INV | `Costeo.Metodo` | `PromedioPonderado`, `Peps` | PromedioPonderado | None (sólo al inicio de un período sin movimientos posteriores) | I1 (Peps I5) | 042, 043 |
| INV | `Costeo.Ambito` | `Cooperativa`, `Bodega` | Cooperativa | None (misma regla) | I1 | 042 |
| INV | `Costeo.RetroactivosPermitidos` | bool | false | None | I5 | 045 (no alcanza al saldo inicial de una bodega no activa ni a los ajustes de conteo, §3.1) |
| INV | `Costeo.RetroactivosDiasMaximos` | int | 0 | None | I5 | 045 |
| INV | `Existencias.StockNegativoPermitido` | bool | false | None, Warehouse | I1 | 034 |
| INV | `Redondeo.Montos` | `Centavo`, `Peso` | Centavo | None | I1 | 017 |
| INV | `Redondeo.Residuo` | `MayorValor`, `UltimaLinea` | MayorValor | None | I1 | 017 |
| INV | `Conteo.BloquearMovimientos` | bool | true | None, Warehouse | I1 | 040 |
| INV | `Conteo.FechaDelAjuste` | `Foto`, `Aprobacion` | Foto | None | I1 | 041 |
| INV | `Conteo.ToleranciaReconteoPorcentaje` · `Conteo.ToleranciaReconteoUnidades` | decimal | 0 · 0 | None, Warehouse | I1 | 040 |
| INV | `Compras.DiasAlertaEventosRadian` | int | 3 | None | I1 | 050 |
| INV | `Compras.ToleranciaCantidadPorcentaje` · `…CantidadValor` · `…PrecioPorcentaje` · `…PrecioValor` | decimal | 0 | None | I5 | 049, 050 |
| INV | `Compras.ReglaDeTolerancia` | `AmbasCondiciones`, `CualquieraDeLas` | AmbasCondiciones | None | I5 | 050 |
| INV | `Contabilidad.ModoDePaso` | `EnLinea`, `PorLotes`, `NoPasa` | EnLinea (sellado) | None, DocumentType (por cadena) | I1 (efecto I2) | 075 |
| INV | `Contabilidad.Granularidad` | `PorDocumento`, `Resumido` | PorDocumento | None, DocumentType | I2 | 075, 077 |
| INV | `Contabilidad.DisparadorDeLote` | `HoraDiaria`, `CierreDeTurno`, `CierreDePeriodo` | HoraDiaria | None, DocumentType | I2 | 077 |
| INV | `Contabilidad.HoraDeLote` | `HH:mm` hora de Colombia | 23:00 | None, DocumentType | I2 | 077 |
| INV | `Contabilidad.PoliticaSinRespuesta` | `ConfirmarConPendiente`, `Bloquear` | ConfirmarConPendiente | None | I2 | 074 |
| INV | `Contabilidad.ValidacionPreviaSegundos` | int | 3 | None | I2 | 074 |
| INV | `Ventas.BajoCosto` | `Alertar`, `Bloquear` | Alertar | None | I3 | 012 |
| INV | `Ventas.PersonaInactivaDeContado` | `Permitir`, `Bloquear` | Permitir | None | I3 | 011, 012 |
| INV | `Ventas.LoteVencido` | `Bloquear`, `Advertir` | Bloquear | None | I6 | 012, 026 |
| INV | `Ventas.ReservaDiasVencimiento` | int | 15 | None | I6 | 012, 052 |
| INV | `Ventas.RemisionDiasMaximosSinFacturar` | int | 30 | None | I6 | 012, 052 |
| INV | `Caja.BaseModo` | `FondoFijo`, `BaseDelDia` | FondoFijo | None, CashRegister | I3 | 099 |
| INV | `Caja.TratamientoFaltante` | `Gasto`, `CargoAlCajero` | Gasto | None, PointOfSale | I3 | 099 |
| INV | `Caja.ArqueoCiego` | bool | false | None, PointOfSale | I3 | 099 |
| INV | `Caja.UnaSesionPorCajero` | bool | true | None | I3 | 058, 099 |
| INV | `Cartera.CuentaPorCobrarRegistradaPor` | `Contabilidad`, `Cartera` | Contabilidad (sellado; forzado mientras IC pendiente) | None | I3 | 062, 084 |
| INV | `Cartera.IntegracionHabilitadaDesde` | fecha o vacío | vacío = IC pendiente | None | IC | 084, 085 |
| INV | `Cartera.PoliticaSinRespuesta` | `Bloquear`, `PermitirConAprobacion` | Bloquear (sólo con IC; en el provisional rige PermitirConAprobacion) | None, ThirdPartyKind | IC | 061 |
| INV | `Cartera.ConsultaSegundos` | int | 5 | None | IC | 085 |
| INV | `Informes.DeterioroPorcentajeGastosVenta` | decimal (fracción) | 0 | None | I3 | 012, 086 |
| INV | `Informes.DiasSinMovimiento` · `Informes.DiasProximoAVencer` · `Informes.UmbralesAbc` | int · int · texto | 90 · 30 · `80/15/5` | None | I6 | 086, 088 |
| INV | `Informes.TopeFaltantesPorcentaje` **(nuevo)** | decimal (fracción), exige `LegalSource` | 0 | None | I6 | 086 (tope de faltantes y mermas del informe) |
| TAX | `Tributario.ResponsableIva` | bool | true | None | I1 | 013 |
| TAX | `Tributario.GranContribuyente` · `.AgenteRetencionIva` · `.Autorretenedor` | bool | false | None | I1 | 013 |
| TAX | `Tributario.RegimenTributarioEspecial` | bool | true | None | I1 | 013 |
| TAX | `Tributario.RedondeoUvtAPesos` | `Peso`, `Centena`, `Mil` | Peso | None | I1 | 013 |
| EINV | `Dian.ObligadaAFacturar` | bool | true | None | I3 (lo lee la guardia desde I3) | 012, 064 |
| EINV | `Dian.EsperaMaximaPosSegundos` | int | 15 | None, PointOfSale | I4 | 063, 067 |
| EINV | `Dian.PlazoContingenciaHoras` | int, con `LegalSource` | 48 | None | I4 | 012, 067 |
| EINV | `Dian.AlertaHorasAntesDelPlazo` | int | 6 | None | I4 | 022, 067 |
| EINV | `Dian.MinutosAlertaSinValidar` | int | 10 | None | I4 | 022, 063 |
| EINV | `Dian.UmbralFallasCircuito` | int | 3 | None | I4 | 067 |
| EINV | `Dian.AvisoResolucionPorcentaje` · `Dian.AvisoResolucionDias` | decimal · int | 0.90 · 30 | None | I4 | 065 |
| EINV | `Dian.EntregaCorreo` | `Erp`, `Canal` | Erp | None | I4 | 063 |
| EINV | `DocumentoSoporte.Generacion` | `PorOperacion`, `Semanal` | PorOperacion | None | I4 | 063 |

Notas de clave. `Informes.TopeFaltantesPorcentaje` es la única clave que este modelo agrega al §2.8:
el tope de faltantes que usa el informe de mermas (`shrinkage-cap`), con la norma o el acta que lo
respalda. `Dian.UmbralFallasCircuito` cuenta como falla del canal **también** la espera del POS que se
agota (`Dian.EsperaMaximaPosSegundos`) sin respuesta: cumplido el umbral, `CircuitoDeCanal` se abre y
las ventas nuevas salen en contingencia 03 con su representación en papel de una vez (SC-004).

Fuera de `COR_ParameterVersions` y también con vigencia (FR-012): prefijos y consecutivos
(`INV_DocumentSequences`, §5.9), políticas de aprobación (`COR_ApprovalPolicies`) y montos máximos
(`SEC_PermissionAmountLimits`), impuestos (`COR_TaxRates`), listas de precios, topes de descuento,
alertas y configuración de emisión (§14–§22). La **tolerancia de arqueo** y la **comisión esperada**
de cada medio de pago (`COR_PaymentMeans`, §16) no llevan vigencia propia: cambiarlas exige
permiso y motivo (`IConMotivo`, auditado) y cada línea de arqueo y cada pago **copian** el valor al
usarlo, así que un cambio sólo afecta a las sesiones que se cierran después.

---

## 5. Documento genérico

Una cabecera y una línea para las 34 clases de `DocumentClass`, más satélites (T17). El comportamiento
de cada clase —efecto, si es fiscal, mensajes, cadena, grupo, bodegas admitidas— vive en código
(`Domain/Inventory/Documents/ClasesDeDocumento`, una estrategia por clase en
`Application/Inventory/Documents/Efectos`); el tipo sólo elige su clase y parametriza (FR-036, FR-037).

### 5.1 `INV_Documents` — `InventoryDocument` (I1)

| Columna | Tipo | Nulo | E | Notas |
|---|---|---|---|---|
| `Class` | int (`DocumentClass`) | no | I1 | fija; su `DocumentClassGroup` decide ruta y familia de permisos |
| `DocumentTypeId` | int FK `INV_DocumentTypes` | no | I1 | de la misma clase |
| `Prefix` | nvarchar(10) | no | I1 | `''` en borrador; lo fija el numerador (no fiscal: el de la secuencia vigente; fiscal: `FiscalPrefix` del tipo, ≤ 4) |
| `Number` | bigint | sí | I1 | nulo en borrador y en descartado |
| `Status` | int (`DocumentStatus`) | no | I1 | `Draft` por defecto |
| `OperationDate` | date | no | I1 | fecha de operación; `HoyLocal` propuesta |
| `ConfirmedAt` | datetime | sí | I1 | |
| `CreatedByUserId` | int FK `SEC_Users` | no | I1 | segregación y «mis borradores» |
| `ConfirmedByUserId` | int FK `SEC_Users` | sí | I1 | quien confirmó o dio la última aprobación |
| `DiscardedAt` · `DiscardedByUserId` · `DiscardReason` | datetime · int FK · nvarchar(300) | sí | I1 | **(nuevo)** el descarte queda registrado (FR-005) |
| `WarehouseId` | int FK `INV_Warehouses` | sí | I1 | bodega de origen (ver §5.2) |
| `DestinationWarehouseId` | int FK | sí | I1 | traslados: destino final |
| `TransitWarehouseId` | int FK | sí | I1 | traslados: tránsito de la sucursal de origen |
| `BranchId` | int FK `COR_Branches` | no | I1 | la de la bodega; en caja, la del punto |
| `CostCenterId` | int FK `COR_CostCenters` | sí | I1 | obligatorio si el tipo lo pide; siempre en `InternalConsumption` |
| `CounterpartyPersonId` | int FK `COR_People` | sí | I1 | proveedor o cliente |
| `SalespersonId` | int FK `INV_Salespeople` | sí | I1 | |
| `SalesChannelId` | int FK `INV_SalesChannels` | sí | I1 | |
| `PointOfSaleId` · `CashRegisterId` · `CashSessionId` | int FK | sí | I3 | tablas de §15 |
| `IsSuspended` · `SuspendedAt` · `SuspendedLabel` | bit · datetime · nvarchar(60) | no · sí · sí | I3 | borrador POS suspendido |
| `ExternalReference` | nvarchar(60) | sí | I1 | obligatorio si el tipo lo pide |
| `Notes` | nvarchar(1000) | sí | I1 | **(nuevo)** observaciones impresas |
| `Currency` | nchar(3) | no | I1 | `COP` (FR-018) |
| `ExchangeRate` | decimal(18,6) | no | I1 | 1 (FR-018) |
| `PostingMode` | int (`PostingMode`) | sí | I1 | sellado al confirmar (§5.3) |
| `Reason` | nvarchar(500) | sí | I1 | obligatorio en anulación, en los tipos que lo piden y en las bajas |
| `VoidsDocumentId` | int FK self | sí | I1 | en el `Voiding`: el anulado |
| `VoidedByDocumentId` | int FK self | sí | I1 | en el anulado: su `Voiding` |
| `FiscalNumberReleased` | bit | no | I1 | 0; pasa a 1 al anular el rechazado del caso b de FR-066, para que el reemplazo reuse el número |
| `OperationMunicipalityDaneCode` | nvarchar(5) | sí | I1 | compras: municipio de la operación para ReteICA; propuesto desde `COR_Branches.MunicipalityDaneCode` de la sucursal de la bodega que recibe (T24) |
| `Subtotal` | `Monto` | no | I1 | Σ `GrossAmount` de las líneas |
| `DiscountTotal` | `Monto` | no | I1 | Σ descuentos no condicionados |
| `TaxTotal` | `Monto` | no | I1 | Σ impuestos (`Generated`, `Deductible`, `AddedToCost`) |
| `WithholdingTotal` | `Monto` | no | I1 | Σ retenciones (practicadas por la cooperativa en compras; sufridas en ventas) |
| `Total` | `Monto` | no | I1 | `Subtotal − DiscountTotal + TaxTotal` |
| `AmountDue` | `Monto` | no | I1 | `Total − WithholdingTotal`: lo que se paga o se cobra (T26) |
| `CostTotal` | `Monto` | no | I1 | **(nuevo)** Σ del costo de las líneas; es el monto que evalúan la aprobación y el monto máximo en ajustes, bajas, traslados y saldo inicial (FR-009) |
| `CountKind` · `CountScope` | int (`CountKind`, `CountScope`) | sí | I1 | **(nuevo)** sólo `PhysicalCount` (§8) |
| `CountScopeJson` | nvarchar(4000) | sí | I1 | **(nuevo)** Ids de categorías, ubicaciones o productos; en I6 la clase ABC |
| `IsBlindCount` | bit | no | I1 | **(nuevo)** |
| `CountSnapshotAt` | datetime | sí | I1 | **(nuevo)** instante de la foto (abierto = no nulo) |
| `CountSnapshotKardexEntryId` | bigint | sí | I1 | **(nuevo)** mayor Id del kardex al tomar la foto: lo posterior se suma al teórico si se admiten movimientos |
| `CountRound` | tinyint | sí | I1 | **(nuevo)** ronda en curso (1 = primer conteo) |
| `AllocationMethod` | int (`LandedCostAllocationMethod`) | sí | I5 | **(nuevo)** sólo `LandedCost` (§9.7) |
| `BalanceClosedAt` · `BalanceClosedByUserId` · `BalanceClosedReason` | datetime · int (`SEC_Users.Id`) · nvarchar(500) | sí | I5 | **(nuevo)** sólo `PurchaseOrder`: cierre del saldo pendiente de recibir (§9.8) |
| `ExpectedDate` | date | sí | I5 | **(nuevo)** solicitud: para cuándo; orden: entrega esperada |
| `ValidUntil` | date | sí | I1 | **(nuevo)** vigencia de la cotización; vencimiento del pedido. Nace con la tabla y se usa desde I6 |
| `DueDate` | date | sí | I1 | vencimiento final de una venta con parte a crédito; reglas en §14 |
| `ReturnsGoods` | bit | no | I1 | sólo notas de venta: la nota devuelve mercancía; `DEFAULT 0`; reglas en §14 |
| `IsFullReversal` | bit | no | I1 | nota por el total del documento; `DEFAULT 0`; reglas en §14 |
| `CorrectionConceptCode` | nvarchar(2) | sí | I1 | concepto de corrección DIAN de la nota; reglas en §14 |

No lleva clave de idempotencia (va en `COR_OperationKeys`, T13) ni la copia fiscal en columnas (va en
`INV_DocumentPartySnapshots`, T52).

**Índices**.
- `UK_INV_Documents_Type_Prefix_Number (DocumentTypeId, Prefix, Number)` filtrado `[Number] IS NOT NULL
  AND [FiscalNumberReleased] = 0` (T16). La unicidad fiscal entre vigentes vive además en
  `COR_ElectronicDocuments (Environment, Prefix, Consecutive)` sin filtro (§18).
- `UK_INV_Documents_VoidsDocumentId (VoidsDocumentId)` filtrado `[VoidsDocumentId] IS NOT NULL AND
  [Status] <> 4`: un documento se anula una sola vez (el descartado no cuenta).
- `IX (Class, Status, OperationDate)`; `IX (WarehouseId, OperationDate)`; `IX (DestinationWarehouseId)`
  filtrado `[DestinationWarehouseId] IS NOT NULL`; `IX (CounterpartyPersonId)`; `IX (Status,
  OperationDate)` (cierre de período); `IX (CashSessionId)` filtrado no nulo (I3).

**Transiciones** (FR-005, FR-010, T33).

```text
Draft ── confirmar, sin niveles ──────────────────────────────▶ Confirmed ── su Voiding ──▶ Voided
Draft ── confirmar, con niveles ──▶ PendingApproval ── última aprobación ──▶ Confirmed
                                    PendingApproval ── rechazar (motivo) o retirar ──▶ Draft
Draft ── descartar (motivo) ──▶ Discarded
```

- `Draft`: se edita (líneas, cabecera, totales) y no consume número. `PendingApproval`: congelado
  (`COR_ApprovalRequests` con `SourceType = "InventoryDocument"` y `SourcePublicId`; su `ContentSha256`
  guarda la huella de lo aprobado); sin número.
- `→ Confirmed`: `ConfirmInventoryDocumentCommand` en el flujo canónico de §1.3 de las decisiones
  (validaciones, aprobación, validación previa fuera del cerrojo, cerrojo, costo y kardex, número,
  mensajes, un `SaveChanges`). La última aprobación vuelve a entrar por ese flujo en la transacción del
  aprobador.
- `→ Voided`: sólo lo pone la confirmación de su `Voiding` (FR-006): documento contrario, fechado en su
  propia `OperationDate` (nunca la del original), en período abierto, que revierte al costo del
  original. Con dependientes vigentes se rechaza nombrándolos (`Inventory.Document.HasDependents`,
  `data.dependents`); dos veces, `Inventory.Document.AlreadyVoided`. Un documento fiscal emitido y
  validado no se anula así (`Inventory.Document.FiscalUseCorrection`): se corrige con su nota (FR-066,
  §18). El registro de una factura o nota **recibida** del proveedor sí se anula con contrario.
- `Discarded`: sólo desde `Draft`; suelta vínculos y reservas del borrador (baja lógica de sus
  `INV_DocumentLinks`/`INV_DocumentLineLinks`).
- **Período** (FR-047): se rechaza una `OperationDate` anterior a `INV_Setup.StartDate`
  (`Inventory.Document.DateBeforeCutoff`, **nuevo**), menor o igual a `LastClosedDate` (`Inventory.Period.Closed`
  nombrando el período) o posterior a `HoyLocal` si el tipo no tiene `AllowsFutureDate`.

### 5.2 Cómo usa cada clase la cabecera (las de §5 a §13; las de venta en §14)

| Clase (E operable) | `WarehouseId` / destino / tránsito | Contraparte | Satélites y vínculos |
|---|---|---|---|
| `PurchaseRequest` (I5) | bodega que pide | — | líneas sin precio; → orden con `FromOrder` |
| `PurchaseOrder` (I5) | bodega que recibe | proveedor | `ExpectedDate`; → recepción con `FromOrder` |
| `PurchaseReceipt` (I1) | bodega que recibe (entrada) | proveedor | líneas de impuesto `AddedToCost`; `FromOrder` (I5) |
| `SupplierInvoice`, `SupplierNote` (I1) | la de sus recepciones si todas comparten una; si no, nula | proveedor | `INV_SupplierInvoiceDetails`, `INV_SupplierInvoiceEvents` (factura a crédito), impuestos, copia fiscal; `InvoiceOfReceipt` / `NoteOf` |
| `SupportDocument`, `SupportDocumentAdjustmentNote` (I4) | como la factura | vendedor no obligado | documento electrónico en `COR_ElectronicDocuments`; `InvoiceOfReceipt` / `NoteOf` |
| `LandedCost` (I5) | nula | proveedor del flete | `INV_LandedCostAllocations`; `LandedCostOf` |
| `SupplierReturn` (I1) | bodega de la que sale | proveedor | `ReturnOf` a la recepción; si hubo documento soporte, su nota de ajuste (I4) |
| `PositiveAdjustment`, `NegativeAdjustment`, `InternalConsumption`, `WriteOff` (I1) | la bodega | — | causa por línea en negativos y bajas; adjuntos `InventoryAdjustmentSupport`; `CountAdjustmentOf` si nace de un conteo |
| `Assembly` (I6) | la bodega | — | una línea por kit; los componentes salen por `INV_ProductComponents` |
| `OpeningBalance` (I1) | bodega **no activa** | — | fecha = `CutoffDate` (§10) |
| `TransferDispatch`, `TransferReceipt` (I1) | origen / destino / tránsito, **iguales en los dos** | — | `ReceiptOf`; `INV_TransferDiscrepancies` (§7) |
| `LocationMove` (I1) | la bodega | — | `LocationId` → `ToLocationId` en cada línea; sin mensajes |
| `PhysicalCount` (I1) | la bodega | — | columnas `Count*`, `INV_CountSnapshotLines`, `INV_CountCaptures` (§8) |
| `CostAdjustment` (I1; I5) | nula | — | lo genera el sistema (cambio de método o de ámbito); sin alta manual |
| `Voiding` (I1) | las del original | la del original | `Voids`; líneas copia de las del original |

Un documento **sin bodega** (factura del proveedor de varias bodegas, costos adicionales, ajuste de
costo, caja) se filtra por alcance a través de sus vínculos: visible si alguna bodega de sus orígenes
está en el alcance, operable si todas lo están (`FiltroDeAlcance`, T35).

### 5.3 Modo de paso sellado (FR-075, FR-079, T9)

`PostingMode` se sella al confirmar en toda clase que emite mensajes a Contabilidad: el valor de
`Contabilidad.ModoDePaso` vigente a la fecha de confirmación, del tipo si su cadena tiene excepción o el
general. Un cambio posterior del parámetro no lo toca, aunque se registre con vigencia anterior. Los
**derivados** (factura del proveedor, documento soporte o devolución contra una recepción; recepción de
traslado; factura desde remisiones) y los **relacionados** (anulación, notas, ajustes de costo) no leen
el parámetro: copian el modo de la entrega de su origen u original. Un derivado no reúne orígenes con
destinos distintos (`Inventory.PostingMode.ChainMismatch`). Queda nulo en las clases sin mensajes a
Contabilidad (`LocationMove`, `PhysicalCount`, `PurchaseRequest`, `PurchaseOrder`, cotización y pedido)
y en las informativas (`OpeningBalance` y su anulación): esos mensajes se entregan siempre.

### 5.4 `INV_DocumentLines` — `InventoryDocumentLine` (I1)

| Columna | Tipo | Nulo | E | Notas |
|---|---|---|---|---|
| `DocumentId` | int FK | no | I1 | |
| `LineNumber` | int | no | I1 | `UK (DocumentId, LineNumber)` |
| `ProductId` | int FK | no | I1 | ni plantilla; bloqueado sólo en conteo y recepción de traslado |
| `UnitId` | int FK `INV_UnitsOfMeasure` | no | I1 | la base o una de `INV_ProductUnits` con el uso de la clase |
| `Quantity` | `Cantidad` | no | I1 | > 0 (el signo lo pone la clase); respeta `AllowedDecimals` de `UnitId` (`Inventory.Unit.DecimalsNotAllowed`) |
| `Factor` | `Factor` | no | I1 | copiado de la unidad al guardar; 1 en la base |
| `QuantityBase` | `Cantidad` | no | I1 | `round(Quantity × Factor)` a los decimales de la unidad base |
| `RoundingQuantity` | `Cantidad` | no | I1 | `Quantity × Factor − QuantityBase`, visible (FR-017) |
| `UnitPrice` | `PrecioUnitario` | no | I1 | precio de compra o de venta; 0 en clases sin precio |
| `ListPrice` · `PriceListId` | `PrecioUnitario` · int FK `INV_PriceLists` | sí | I3 | precio de lista y lista aplicada (FR-053) |
| `ListPriceIncludesTaxes` | bit | no | I1 | copia de `INV_PriceLists.IncludesTaxes` de la lista aplicada; `DEFAULT 0`; se usa desde I3; reglas en §14 |
| `GrossAmount` | `Monto` | no | I1 | **(nuevo)** `round(Quantity × UnitPrice)` |
| `DiscountAmount` | `Monto` | no | I1 | **(nuevo)** descuento no condicionado de la línea; en ventas, Σ de sus `INV_DocumentLineDiscounts` (§14) |
| `NetAmount` | `Monto` | no | I1 | **(nuevo)** `GrossAmount − DiscountAmount`: base gravable |
| `UnitCost` | `CostoUnitario` | sí | I1 | entradas: el de entrada (compra neta + impuestos al costo; saldo inicial: el cargado; ajuste positivo: el vigente, o el digitado con `Inventory.Adjustments.SetUnitCost`); salidas: lo escribe la confirmación con el costo aplicado |
| `TotalCost` | `Monto` | sí | I1 | **(nuevo)** costo total de la línea, sin signo |
| `LocationId` | int FK | sí | I1 | nulo en borrador = la ubicación por defecto; obligatoria al confirmar |
| `ToLocationId` | int FK | sí | I1 | `LocationMove` y ubicación de entrada en la recepción de traslado |
| `LotId` · `SerialId` | int | sí | I1 (FK I6) | §3.0 |
| `AdjustmentCauseId` | int FK `INV_AdjustmentCauses` | sí | I1 | **(nuevo)** obligatoria en `NegativeAdjustment`, `WriteOff` y en los ajustes de un conteo |
| `Description` | nvarchar(200) | sí | I1 | texto de un servicio o del documento soporte |
| `AffectsCost` | bit | no | I1 | **(nuevo, US9 T342)** sólo en `SupplierNote`: la línea cambia el precio de lo recibido y deja `PriceDifference` sobre la recepción; `DEFAULT 0` |

`IX (ProductId)`. Un servicio no produce kardex; el resto de la línea no cambia al confirmar salvo
`UnitCost`/`TotalCost` de las salidas y `LocationId` por defecto, que se escriben en la misma transacción
de la confirmación. Después, inmutable.

### 5.5 `INV_DocumentLinks` — `DocumentLink` y `INV_DocumentLineLinks` — `DocumentLineLink` (I1)

**`DocumentLink`**: `SourceDocumentId` (el original u origen), `TargetDocumentId` (el nuevo o derivado),
`Kind` (`DocumentLinkKind`). `UK (SourceDocumentId, TargetDocumentId, Kind)` filtrado; `IX
(TargetDocumentId)`.

| `Kind` | Target ← Source |
|---|---|
| `Voids` | `Voiding` ← anulado |
| `FromOrder` | recepción ← orden de compra; orden ← solicitud; pedido ← cotización y factura ← pedido (I6) |
| `FromShipment` | factura desde remisiones ← remisión (I6) |
| `NoteOf` | nota del proveedor ← su factura; nota de ajuste del documento soporte ← documento soporte; notas de venta ← su documento (§14) |
| `ReceiptOf` | recepción de traslado ← despacho |
| `InvoiceOfReceipt` | factura del proveedor o documento soporte ← recepción |
| `ReturnOf` | devolución a proveedor ← recepción |
| `DispatchOf` | remisión ← pedido (I6) |
| `CountAdjustmentOf` | ajuste positivo o negativo ← conteo |
| `ReplacementOf` | reemplazo ← rechazado del caso b de FR-066 (§18); factura ← documento equivalente POS cuando el comprador pide factura después (FR-063; esa factura no escribe kardex) |
| `LandedCostOf` | costos adicionales ← recepción, y ← la factura del flete |

**`DocumentLineLink`**: `DocumentLinkId` FK (da el `Kind`), `SourceLineId`, `TargetLineId`,
`QuantityBase` (`Cantidad` > 0). `UK (SourceLineId, TargetLineId)` filtrado; `IX (TargetLineId)`.

**Pendientes** (no se guardan contadores): lo pendiente de una línea origen = su `QuantityBase` − Σ
`QuantityBase` de los vínculos cuyo destino está `PendingApproval` o `Confirmed` y no `Voided`. Reglas:
mismo producto en las dos puntas; Σ ≤ origen (lo recibido de más contra una orden, sólo dentro de la
tolerancia, I5); devuelto ≤ recibido − ya devuelto; facturado ≤ recibido − ya facturado (FR-050).
**Concurrencia**: dos derivados que consumen el mismo origen a la vez podrían pasarse de lo pendiente
sin tocar las mismas filas de existencia; se propone que la confirmación bloquee en exclusivo las filas
`INV_Documents` de los orígenes (sin modificarlas), después de `INV_Warehouses` en el orden del
cerrojo. Es un paso que el orden canónico de T15 no tiene.

### 5.6 `INV_DocumentPartySnapshots` — `DocumentPartySnapshot` (I1; hecho; FR-011, T52)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentId` | int FK | no | |
| `Version` | int | no | 1 al confirmar; la corrección del caso a de FR-066 agrega la siguiente. Vigente = la mayor |
| `PersonId` | int FK `COR_People` | no | |
| `DianOrganizationType` | nvarchar(2) | no | persona natural o jurídica (código DIAN) |
| `DianIdTypeCode` | nvarchar(3) | no | tipo de identificación DIAN, derivado por `CatalogoDian` |
| `TaxId` · `CheckDigit` | nvarchar(20) · nchar(1) | no · sí | |
| `LegalName` | nvarchar(300) | no | razón social o nombre completo como se emitió |
| `FirstName` · `LastName` | nvarchar(150) | sí | persona natural |
| `Address` | nvarchar(200) | sí | |
| `MunicipalityDaneCode` · `CityName` · `DepartmentName` · `CountryCode` | nvarchar(5) · (100) · (100) · nchar(2) | sí | |
| `Email` · `Phone` | nvarchar(200) · (50) | sí | |
| `DianResponsibilities` | nvarchar(100) | sí | `O-13;O-15…`, derivadas de las marcas (T24) |
| `DianTaxSchemeCode` | nvarchar(10) | sí | tributo (`01`, `ZZ`) |
| `IsVatResponsible`, `IsLargeContributor`, `IsSelfWithholder`, `IsVatWithholdingAgent`, `IsSimpleTaxRegime`, `IsIncomeTaxFiler`, `WithholdingExempt`, `IcaWithholdingExempt` | bit | no | el perfil con que corrió el motor tributario; las notas y devoluciones lo reutilizan |
| `CiiuCode` | nvarchar(10) | sí | actividad para ReteICA |
| `ChangeReason` | nvarchar(300) | sí | obligatorio desde la versión 2 |

`UK (DocumentId, Version)`. Sólo inserción. Se escribe al confirmar todo documento con
`CounterpartyPersonId`. Reimpresiones y representación gráfica usan la vigente o el archivo firmado.

### 5.7 `INV_DocumentTaxLines` — `DocumentTaxLine` (I1; hecho; FR-013, FR-044, T22)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentId` | int FK | no | |
| `DocumentLineId` | int FK | sí | nulo = renglón del documento (retenciones) |
| `TaxDefinitionId` | int FK `COR_TaxDefinitions` | no | |
| `TaxRateId` | int FK `COR_TaxRates` | no | la **fila** (vigencia) que aplicó |
| `TaxRateCode` | nvarchar(10) | no | copia |
| `Kind` | int (`TaxKind`) | no | copia |
| `Treatment` | int (`TaxTreatment`) | no | `Generated`, `Deductible`, `AddedToCost`, `WithholdingApplied`, `WithholdingSuffered` |
| `WithholdingConceptId` | int FK | sí | retenciones |
| `MunicipalityDaneCode` | nvarchar(5) | sí | ICA y ReteICA |
| `Rate` | `Tarifa` | sí | fracción |
| `AmountPerUnit` · `TaxableUnits` | `Monto` · `Cantidad` | sí | impuestos por unidad |
| `Base` | `Monto` | no | |
| `Amount` | `Monto` | no | redondeado por línea; el total es la suma |
| `DianTaxCode` | nvarchar(3) | sí | copia, para el canónico |
| `ExplanationJson` | nvarchar(max) | no | pasos del motor: UVT y fecha, base mínima en pesos, condición que decidió |

`IX (DocumentId)`, `IX (TaxDefinitionId, DocumentId)`. **Reglas**: se escriben **al confirmar** (el
borrador los calcula al vuelo y guarda sólo los totales de la cabecera). La recepción guarda sólo los
`AddedToCost`, que forman su costo; la factura del proveedor guarda todos, separando descontable y al
costo para que la matriz cancele la cuenta puente con el mismo valor que cargó «CompraRecibida»
(research R20). Notas y devoluciones usan la foto del original y no vuelven a probar la base
mínima. El `Voiding` no tiene renglones propios: su mensaje se arma con los del original.

### 5.8 `INV_DocumentTypes` — `InventoryDocumentType` y `INV_DocumentTypeWarehouses` — `DocumentTypeWarehouse` (I1; FR-037)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Code` | nvarchar(10) | no | UK filtrado; **inmutable** (`ACC_InventoryVoucherMappings` puede mapear por él, T28) |
| `Name` | nvarchar(120) | no | |
| `Class` | int (`DocumentClass`) | no | inmutable |
| `FiscalPrefix` | nvarchar(4) | sí | **(nuevo)** sólo en tipos fiscales con resolución (factura, DEE POS, documento soporte y sus tipos de contingencia): el prefijo con que `NumeradorFiscal` busca la resolución vigente por (`Kind`, `Prefix`, `Environment`), sin FK a la resolución (T16) |
| `IsContingency` | bit | no | **(nuevo)** tipo de contingencia del facturador (resolución `Contingency`) |
| `RequiresCounterparty`, `RequiresCostCenter`, `RequiresReason`, `RequiresExternalReference` | bit | no | campos obligatorios (FR-037) |
| `SalesChannelId` | int FK | sí | canal del tipo |
| `IsTaxableWithdrawal` | bit | no | sólo `InternalConsumption`: retiro gravado (base = lista general vigente, IVA del producto) |
| `VatNonDeductible` | bit | no | sólo clases de compra: el IVA va al costo (FR-044, paso 2) |
| `AllowsFutureDate` | bit | no | |
| `AllWarehouses` | bit | no | **(nuevo)** 1 = cualquier bodega que admita la clase; 0 = sólo las de `INV_DocumentTypeWarehouses` |
| `IsActive` | bit | no | inactivo: no se usa en documentos nuevos |

`IX (Class)`. **`DocumentTypeWarehouse`**: `DocumentTypeId`, `WarehouseId`; `UK (DocumentTypeId,
WarehouseId)` filtrado. **Reglas**: las marcas sólo valen en su clase; un tipo fiscal con resolución
exige `FiscalPrefix` (mayúsculas y dígitos, ≤ 4); la política de aprobación (`COR_ApprovalPolicies` por
`DocumentTypePublicId`) y el modo de paso y su granularidad (`COR_ParameterVersions` con `ScopeKind =
DocumentType`) no son columnas del tipo. Semilla `InventoryDocumentTypesSeeder` (Order 80): un tipo por
clase de I1, incluido `Voiding`, con su secuencia.

### 5.9 `INV_DocumentSequences` — `DocumentSequence` (I1; FR-038, T16)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentTypeId` | int FK | no | tipos no fiscales y **todas las notas** (crédito, débito, de ajuste POS, de ajuste del documento soporte, las no electrónicas) |
| `Prefix` | nvarchar(10) | no | puede ser `''` |
| `NextValue` | bigint | no | ≥ 1. Se fija al crear (para continuar la numeración de SOLIDO) y después sólo lo cambia `Numerador` |
| `ValidFrom` | date | no | |
| `ValidTo` | date | sí | |

`UK (DocumentTypeId, Prefix)` filtrado `[IsDeleted] = 0`. **Reglas**: a lo sumo una secuencia vigente
por tipo a una fecha (compartida por todas las cajas que usan el tipo, FR-038); cambiar de prefijo es
una fila nueva que cierra la anterior la víspera; volver a un prefijo ya usado reabre su fila (continúa
su consecutivo, porque el número es único por tipo y prefijo). El número se asigna al confirmar, con la
secuencia vigente ese día, bloqueada al final del cerrojo e incrementada por EF en la misma transacción:
sin huecos ni repetidos, y el borrador no consume. Los tipos fiscales con resolución numeran en
`COR_DianNumberingResolutions.LastIssuedNumber` (§18).

### 5.10 `INV_AdjustmentCauses` — `AdjustmentCause` (I1; FR-037, FR-039)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Code` | nvarchar(10) | no | UK filtrado; **inmutable** (`ReasonCode` de la matriz en `Baja`/`AjusteNegativo`) |
| `Name` | nvarchar(80) | no | |
| `AllowsPositive`, `AllowsNegative` | bit | no | la diferencia de conteo vale en los dos sentidos |
| `AllowsTransitWriteOff` | bit | no | daño, hurto, reclamación al transportador |
| `RequiresAttachment` | bit | no | exige soporte (acta de destrucción, denuncia) antes de confirmar; por defecto 0 |
| `IsSeeded`, `IsActive` | bit | no | |

Semilla `AdjustmentCausesSeeder` (Order 79): merma o faltante, daño, vencimiento, hurto, diferencia de
conteo, destrucción y reclamación al transportador. **Soportes**: `COR_Attachments` con `OwnerEntityType
= "InventoryAdjustmentSupport"` y `OwnerEntityPublicId` = el documento; sube quien lo crea mientras está
en borrador; confirmado, no se borran (T41).

---

## 6. Períodos, puesta en marcha y activación

### 6.1 `INV_Setup` — `InventorySetup` (I1; FR-047)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `StartDate` | date | no | primer día del mes del primer corte; antes no se admite ningún documento |
| `LastClosedDate` | date | sí | último día del último mes cerrado; nulo = ninguno |
| `StartedAt` · `StartedByUserId` | datetime · int FK | no | |

Fila única. La crea el primer registro de una fecha de corte (o `ImportOpeningBalanceCommand` si no
existe); después, una fecha de corte anterior a `StartDate` se rechaza. **Cerrojo**: compartido al confirmar,
exclusivo al cerrar o reabrir: el cierre espera las confirmaciones en vuelo y detiene las nuevas
mientras dura. Como los meses cierran en orden y sólo se reabre el último, «¿período cerrado?» se
reduce a comparar con `LastClosedDate`.

### 6.2 `INV_Periods` — `InventoryPeriod` (I1; FR-047)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Year` · `Month` | smallint · tinyint | no | `UK (Year, Month)` filtrado |
| `Status` | int (`InventoryPeriodStatus`) | no | |
| `CloseVersion` | int | no | 1 en el primer cierre; +1 en cada recierre |
| `ClosedAt` · `ClosedByUserId` | datetime · int FK | sí | |
| `CloseWarningsJson` | nvarchar(max) | sí | borradores, tránsitos sin resolver y mensajes pendientes, en lote o rechazados con fecha en el mes, tal como se aceptaron |
| `UnbilledShipmentsJson` | nvarchar(max) | sí | remisiones sin facturar con su valor por facturar (I6; el contador las causa con un comprobante de la 009) |
| `UnbilledShipmentsAcceptedByUserId` · `UnbilledShipmentsAcceptedReason` | int FK · nvarchar(500) | sí | `Inventory.Periods.AcceptUnbilledShipments` |
| `ReopenedAt` · `ReopenedByUserId` · `ReopenReason` | datetime · int FK · nvarchar(500) | sí | |

Un mes sin fila entre `StartDate` y hoy está abierto; la fila nace con el primer cierre. **Transiciones**:
`(abierto) → Closed` con `CloseInventoryPeriodCommand` (`Inventory.Periods.Close`): exige el mes
anterior cerrado, **se bloquea** con conteos abiertos con foto en el mes, avisa sin bloquear
de lo de `CloseWarningsJson`, exige aceptar las remisiones sin facturar, fija el valorizado versión
`CloseVersion`, pone `LastClosedDate`, emite «PeriodoInventarioCerrado» y ordena el lote `PeriodClose`
(§20). `Closed → Open` con `ReopenInventoryPeriodCommand` (`Inventory.Periods.Reopen`, motivo): sólo
el último cerrado; marca `Superseded` su valorizado, devuelve `LastClosedDate` al mes anterior y emite
«PeriodoInventarioReabierto».

### 6.3 `INV_PeriodClosingBalances` — `PeriodClosingBalance` (I1; FR-047, SC-017)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `PeriodId` | int FK | no | |
| `Version` | int | no | = `CloseVersion` del cierre que la fijó |
| `ProductId` · `WarehouseId` | int FK | no | incluidas las de tránsito |
| `AccountingGroupId` | int FK | no | el grupo del producto a la fecha de cierre (§1.10) |
| `Quantity` · `Value` | `Cantidad` · `Monto` | no | valor = cantidad × promedio del ámbito, residuo por regla |
| `Superseded` | bit | no | lo único que cambia después: 1 al reabrir |

`UK (PeriodId, Version, ProductId, WarehouseId)`; `IX (PeriodId, Superseded)`. No se guardan filas en
cero. Σ `Value` por ámbito = `CostState.Value` a esa fecha. El valorizado a una fecha pasada parte del
último cierre vigente anterior y suma el kardex posterior (SC-017).

### 6.4 `INV_WarehouseActivations` — `WarehouseActivation` (I1, usa I2; FR-090, SC-018)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `WarehouseId` | int FK | no | `UK (WarehouseId)` filtrado: se activa una sola vez |
| `CutoffDate` | date | no | copia de la bodega |
| `ComparisonJson` | nvarchar(max) | no | por grupo contable y conjunto de cuentas mapeadas: valorizado de las bodegas activas y de la que se activa, cifras de SOLIDO a la fecha de corte de las no activas que comparten cuentas —que **suman al valorizado del conjunto** y se guardan aparte para identificarlas—, saldo contable (`IContabilidadParaInventario.SaldosDeCuentasMapeadasAsync`) y diferencia |
| `TotalDifference` | `Monto` | no | |
| `IsBalanced` | bit | no | diferencia 0 en todo conjunto |
| `DifferenceAcceptedByUserId` · `AcceptanceReason` | int FK · nvarchar(500) | sí | `Inventory.Warehouses.AcceptActivationDifference` |
| `ActivatedAt` · `ActivatedByUserId` | datetime · int FK | no | |

**Regla**: `ActivateWarehouseCommand` (`Inventory.Warehouses.Activate`) compara a la fecha de corte;
sin cuadre y sin aceptación se rechaza sin dejar fila (el intento queda en la auditoría); con cuadre o
aceptación escribe esta fila, pasa la bodega a `Active` y, si es la primera de la sucursal, también su
tránsito. El saldo inicial es opcional: una bodega nueva que arranca vacía también se compara. Las
bodegas no activas que comparten cuentas entran al conjunto con sus cifras de SOLIDO a la fecha de
corte (`INV_LegacyFigures`): la diferencia no se les atribuye, porque el saldo contable de esas
cuentas también las contiene (FR-081 y SC-005, precisión aplicada a la spec).

**Antes de I2** no hay matriz ni consulta de saldos. Fuera de producción, para ensayar I1, la
activación se admite con la diferencia aceptada, el permiso especial y motivo («sin comparación
contable»), y la fila guarda `ComparisonJson` sin conjuntos. **En producción ese camino no existe**:
el comando responde 422 `Inventory.Activation.AccountingUnavailable` mientras la consulta de saldos no
exista, así que ninguna bodega de producción se activa sin la comparación de FR-090.

### 6.5 `INV_LegacyFigures` — `LegacyFigure` (I1; FR-090, FR-091, SC-018)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `ImportBatchPublicId` | uniqueidentifier | no | el lote de `ImportLegacyFiguresCommand` |
| `AsOfDate` | date | no | fecha de la cifra (saldo) o fin del rango (movimiento) |
| `FromDate` | date | sí | con valor, la fila trae movimientos del rango para el comparativo de kardex |
| `ProductCodeRaw` · `WarehouseCodeRaw` | nvarchar(40) · (20) | no | tal como vienen de SOLIDO |
| `ProductId` · `WarehouseId` | int FK | sí | resueltos si existen; lo no resuelto se lista en la revisión previa |
| `Quantity` · `Value` | `Cantidad` · `Monto` | sí | saldo a `AsOfDate` |
| `QuantityIn` · `QuantityOut` · `ValueIn` · `ValueOut` | `Cantidad` · `Monto` | sí | movimientos del rango |
| `SourceFileName` | nvarchar(260) | no | |

`IX (AsOfDate, WarehouseId)`, `IX (ImportBatchPublicId)`. Nunca mueven existencia (FR-001): sólo
alimentan la activación, la conciliación y las vistas `legacy-comparison-kardex` y
`legacy-comparison-valuation`. Un lote nuevo de la misma fecha y bodega da de baja lógica al anterior
(auditado).

---

## 7. Traslados (I1; FR-039, US10)

Dos documentos, `TransferDispatch` y `TransferReceipt`, con las tres bodegas iguales en ambos (§5.2):
`WarehouseId` origen, `DestinationWarehouseId` destino y `TransitWarehouseId` el tránsito de la
sucursal de **origen**.

- **Despacho** (`DispatchTransferCommand`): salida del origen y entrada al tránsito, al costo de
  origen. El origen no puede ser un tránsito.
- **Recepción** (`ReceiveTransferCommand`): una recepción ordinaria por despacho; sus líneas se enlazan
  (`ReceiptOf`) a las del despacho y sacan del tránsito, **como máximo**, lo pendiente, al costo de la
  línea de despacho, para entrarlo al destino. Es derivada: sigue el modo de paso del despacho. Lo que
  no llega queda en tránsito como faltante; lo que llega de más queda como sobrante, fuera de la
  existencia.
- **Anulación**: un despacho sin recepción se anula y la mercancía vuelve al origen; una recepción no se
  anula: un traslado recibido se corrige con otro en sentido contrario (US10-3).
- **Períodos**: la recepción se fecha en período abierto aunque el despacho sea de un mes cerrado; el
  tránsito aparece en el valorizado de los dos cortes.

### 7.1 `INV_TransferDiscrepancies` — `TransferDiscrepancy`

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DispatchDocumentId` · `ReceiptDocumentId` | int FK | no | |
| `DispatchLineId` | int FK | no | |
| `ProductId` · `LotId` | int | no · sí | |
| `Kind` | int (`TransferDiscrepancyKind`) | no | `Shortage`, `Surplus` |
| `QuantityBase` | `Cantidad` | no | > 0 |
| `UnitCost` | `CostoUnitario` | sí | faltante: el de la línea de despacho; sobrante: el vigente al resolver |
| `Resolution` | int (`TransferDiscrepancyResolution`) | sí | pedida |
| `ResolutionRequestedAt` · `ResolutionRequestedByUserId` · `ResolutionReason` | datetime · int FK · nvarchar(500) | sí | |
| `AdjustmentCauseId` | int FK | sí | obligatoria en `WriteOffFromTransit` y `SurplusAdjustment` |
| `ResolutionDocumentId` | int FK `INV_Documents` | sí | el documento que la resuelve |
| `ResolvedAt` | datetime | sí | nulo = pendiente |

`UK (ReceiptDocumentId, DispatchLineId, Kind)` filtrado; `IX (ResolvedAt)` filtrado nulo.

**Transiciones** (`ResolveTransferDiscrepancyCommand`, con aprobación por la política
`Subject = TransferDiscrepancy` del tipo de la **recepción de traslado** —`COR_ApprovalPolicies`, parte
2, que es donde se fija—, sea cual sea la clase del documento que la resuelve;
`Inventory.Transfers.Approve`): `Pending` (sin `Resolution`) → resolución pedida (se crea el documento
en borrador y pasa por aprobación) → `Resolved` cuando ese documento se confirma. Documentos: faltante
`ReturnToOrigin` = recepción de traslado del tránsito al origen; `WriteOffFromTransit` = `WriteOff` sobre
el tránsito con su causa; `LateReceipt` = recepción del tránsito al destino; sobrante
`SurplusAdjustment` = `PositiveAdjustment` en el destino al costo vigente, con su causa («AjusteInventarioAprobado»).
Si la aprobación se rechaza, vuelve a pendiente. El faltante no desaparece hasta resolverse (US10-2), y
el cierre de período avisa de los pendientes.

---

## 8. Conteos (I1; FR-040, FR-041, US11)

El conteo es un documento `PhysicalCount` (columnas `Count*` de §5.1) con dos satélites. Ciclo:

```text
Draft (alcance definido)
   │ OpenPhysicalCountCommand (congela la foto; sin número)
   ▼
Draft + CountSnapshotAt (abierto: foto fija) ◀─┐ CapturePhysicalCountCommand, una o más rondas
   │ ClosePhysicalCountCommand                  ┘
   ▼
Confirmed (numerado al cerrar, sin kardex) ──▶ ajustes en borrador (CountAdjustmentOf) ──▶ aprobación ──▶ Confirmed
   │ anular (motivo)
   ▼
Voided ◀── su Voiding (documento contrario sin kardex ni mensajes, `Voids` al conteo)

Draft, abierto o no ── descartar con motivo ──▶ Discarded (sin número; libera los productos bloqueados)
```

- **Número**: el conteo no consume número al abrir; lo recibe al cerrar, cuando pasa a `Confirmed`
  (FR-038: sin huecos, el número se asigna al confirmar). Un conteo abierto que no sigue se
  **descarta** con motivo; no se anula.
- **Anular** sólo alcanza a un conteo cerrado (`Confirmed`): como todo confirmado (FR-006,
  `IInmutableTrasConfirmar`), con un `Voiding` que lo referencia (`Voids`) y que no produce kardex ni
  mensajes, porque el conteo no los produjo. Con ajustes del conteo ya confirmados se rechaza
  nombrándolos (`Inventory.Document.HasDependents`): se anulan primero los ajustes.

- **Abrir** toma en exclusivo la fila de la bodega (espera las confirmaciones en vuelo), copia la foto de
  `INV_StockDetails` del alcance a `INV_CountSnapshotLines` y guarda `CountSnapshotKardexEntryId`. Con
  `Conteo.BloquearMovimientos = true` (defecto, por bodega), toda confirmación que mueva un producto del
  alcance en esa bodega se rechaza mientras esté abierto (`Inventory.Count.ProductsLocked`, **nuevo**); con
  `false`, lo movido después de la foto se suma al teórico al cerrar.
- **Cerrar** calcula contado y diferencia, confirma el conteo (sin kardex) y genera hasta dos
  documentos en borrador, `PositiveAdjustment` con los sobrantes y `NegativeAdjustment` con los
  faltantes, con causa «diferencia de conteo», fechados según `Conteo.FechaDelAjuste` (la de la foto o la
  de la aprobación; si ese mes ya cerró, el primer día abierto) y valorizados al promedio vigente en esa
  fecha (FR-041). Se confirman sólo después de aprobados (`/counts/{id}/adjustment`,
  `Inventory.Counts.Approve`); la aprobación excluye a quien abrió y a todo contador (FR-010). Un
  ajuste fechado en la foto con movimientos posteriores del producto en su ámbito es retroactivo, y
  se admite siempre desde I1 sin depender de `Costeo.RetroactivosPermitidos`: FR-041 lo fecha en la
  foto y los ajustes que el sistema genera desde un conteo aprobado quedan fuera de FR-045 (precisión
  aplicada a la spec; pregunta D9, con esto como propuesta por defecto). Con promedio ponderado, un
  ajuste valorizado al promedio de su fecha **no cambia** el costo de las salidas posteriores (entra o
  sale al promedio, así que el promedio no se mueve): el retroactivo mínimo de I1 (§3.1) sólo inserta
  sus entradas en el orden `(OperationDate, Id)` y verifica que ningún saldo intermedio quede negativo
  con el negativo prohibido; si lo quedara, el ajuste se rechaza nombrando la salida
  (`Inventory.Stock.Insufficient`, `data.available`).
- El cierre de período no se completa con conteos abiertos con foto en el mes (FR-047).

### 8.1 `INV_CountSnapshotLines` — `CountSnapshotLine`

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentId` | int FK | no | el conteo |
| `ProductId` · `LocationId` | int FK | no | |
| `LotId` | int | sí | §3.0 |
| `TheoreticalQuantity` | `Cantidad` | no | la foto |
| `SnapshotUnitCost` | `CostoUnitario` | no | promedio del ámbito al abrir (informativo) |
| `AddedDuringCapture` | bit | no | producto encontrado que no estaba en la foto (teórico 0) |
| `MovementsAfterSnapshot` | `Cantidad` | sí | al cerrar, si se admitían movimientos |
| `CountedQuantity` · `Difference` | `Cantidad` | sí | al cerrar; manda la última ronda |
| `RecountRequired` | bit | no | la diferencia de la ronda supera la tolerancia |
| `LastRound` | tinyint | no | |

Dos UK como en `INV_StockDetails`: `(DocumentId, ProductId, LocationId)` filtrado `[LotId] IS NULL` y
`(DocumentId, ProductId, LocationId, LotId)` filtrado `[LotId] IS NOT NULL`. Fija desde que se abre
salvo las columnas del cierre y `RecountRequired`/`LastRound`.

### 8.2 `INV_CountCaptures` — `CountCapture`

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentId` · `SnapshotLineId` | int FK | no | |
| `Round` | tinyint | no | 1 = primer conteo; 2… = reconteos |
| `CounterUserId` | int FK `SEC_Users` | no | |
| `Quantity` | `Cantidad` | no | suma de las lecturas de la tanda (cada lectura suma); negativa = corrección del mismo contador |
| `Reads` | int | no | lecturas del lector en la tanda |
| `IsCorrection` | bit | no | |
| `CapturedAt` | datetime | no | |

`IX (DocumentId, Round, SnapshotLineId)`. Sólo inserción mientras el conteo está abierto. Contado de
una línea en una ronda = Σ de las capturas de todos los contadores en esa ronda (los contadores se
reparten ubicaciones; un segundo conteo independiente es una ronda nueva). **Reconteo**: al terminar
una ronda, las líneas cuya diferencia supera `Conteo.ToleranciaReconteoPorcentaje` o `…Unidades`
quedan `RecountRequired` y se abre la ronda siguiente sólo para ellas; no se cierra con reconteos
pendientes, y después del reconteo manda la última ronda. En conteo ciego la captura no devuelve el
teórico.

---

## 9. Compras

Clases del documento común (FR-036, research R20). En I1: `PurchaseReceipt`, `SupplierInvoice`,
`SupplierNote`, `SupplierReturn` y la compra directa (`ConfirmDirectPurchaseCommand`: recepción y
factura, dos documentos en una transacción, la factura derivada de la recepción). En I4 operan
`SupportDocument` y su nota. En I5, `PurchaseRequest`, `PurchaseOrder`, recepción contra orden, cruce a
tres vías, `LandedCost` y la emisión de eventos RADIAN.

### 9.1 Recepción (FR-049, FR-044)

Entra al kardex al costo neto de descuentos no condicionados más los impuestos `AddedToCost` y emite
«CompraRecibida». En I5 se enlaza por línea a la orden (`FromOrder`), total o parcial, y lo recibido de
más sólo entra dentro de la tolerancia.

### 9.2 `INV_SupplierInvoiceDetails` — `SupplierInvoiceDetail` (I1; FR-050)

1:1 con la factura o la nota **del proveedor** (`SupplierInvoice`, `SupplierNote`). El documento
soporte no la usa: lo emite la cooperativa y su número y CUDS viven en `COR_ElectronicDocuments`.

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentId` | int FK | no | `UK (DocumentId)` |
| `DocumentClass` | int (`DocumentClass`) | no | copia, para el índice |
| `SupplierPersonId` | int FK `COR_People` | no | copia de `CounterpartyPersonId`, para el índice |
| `SupplierPrefix` | nvarchar(10) | no | `''` si no tiene |
| `SupplierNumber` | nvarchar(20) | no | |
| `Cufe` | nvarchar(96) | sí | CUFE (factura) o CUDE (nota), 96 hexadecimales en minúscula; obligatorio si `IsElectronic` |
| `IssueDate` | date | no | ≤ `HoyLocal` |
| `DueDate` | date | sí | obligatoria si `IsCredit`; ≥ `IssueDate` |
| `IsCredit` | bit | no | forma de pago: crédito o contado |
| `IsElectronic` | bit | no | |
| `IsDebitNote` | bit | no | sólo `SupplierNote`: 1 débito, 0 crédito; da el signo de «FacturaProveedorRegistrada» |
| `IsReleased` | bit | no | 1 cuando el documento se anula o se descarta: su número vuelve a estar libre |

**Índices**: `UK (SupplierPersonId, DocumentClass, SupplierPrefix, SupplierNumber)` filtrado
`[IsReleased] = 0 AND [IsDeleted] = 0` y `UK (Cufe)` filtrado `[Cufe] IS NOT NULL AND [IsReleased] = 0
AND [IsDeleted] = 0`: la misma factura no se registra dos veces (`Inventory.SupplierInvoice.Duplicate`,
**nuevo**, nombrando el registro existente). `IsReleased` es lo único que cambia tras confirmar, junto
con el estado de la cabecera. **Reglas**: se registra contra sus recepciones (`InvoiceOfReceipt`, líneas
enlazadas; facturado ≤ recibido no facturado). Una diferencia de precio con la recepción agrega
`CostAdjustment` `PriceDifference` bajo la factura, separada en existencia y vendido, y emite
«AjusteDeCostoReconocido» (en I1 no se retiene, E6). Impuestos y retenciones, por el motor con el
concepto de cada producto, el perfil de las partes y `OperationMunicipalityDaneCode`. Puede prellenarse
leyendo el XML del proveedor, que no se guarda (E10). Anular el registro (contrario, FR-006) no toca
los eventos ya registrados o emitidos.

### 9.3 `INV_SupplierInvoiceEvents` — `SupplierInvoiceEvent` (I1; FR-050, US9-4, T42)

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentId` | int FK | no | la factura del proveedor |
| `EventCode` | int (`SupplierInvoiceEventCode`) | no | `Receipt030 = 30` (acuse de recibo), `GoodsReceived032 = 32` (recibo del bien) |
| `Status` | int (`SupplierInvoiceEventStatus`) | no | |
| `EventDate` | date | sí | |
| `Source` | nvarchar(20) | sí | `DianPortal`, `SupplierPortal` o `Erp` (lista cerrada) |
| `Cude` | nvarchar(96) | sí | |
| `RegisteredByUserId` | int FK | sí | |
| `RegisteredAt` | datetime2 | sí | **(nuevo, US9 T336)** cuándo se registró (o corrigió) el evento externo; lo devuelve §14.8 como `registeredAt` |
| `EvidenceAttachmentPublicId` | uniqueidentifier | sí | constancia opcional |
| `ElectronicDocumentPublicId` | uniqueidentifier | sí | I5: el `COR_ElectronicDocuments` de `Kind = RadianEvent030/032` |
| `Notes` | nvarchar(300) | sí | |

`UK (DocumentId, EventCode)` filtrado. **Transiciones**: al confirmar la factura nacen las dos filas,
`Pending` si `IsCredit` y `NotApplicable` si es de contado. `Pending → RegisteredExternally` con
`RegisterExternalRadianEventCommand` (`Inventory.Purchases.RegisterRadianEvent`; corregir un registro
externo lo reescribe con antes y después auditados). Desde I5, `Pending → Emitted` al validarse el
documento de `EmitRadianEventCommand` (`Inventory.Purchases.EmitRadianEvent`), o `→ Rejected` y de
nuevo `Pending` al reintentar. El 032 exige el 030. La alerta `Compras.EventosRadianFaltantes` sale a
los `Compras.DiasAlertaEventosRadian` días de `IssueDate` con alguno `Pending` y se atiende sola cuando
ninguno lo está.

### 9.4 Notas del proveedor y devolución (FR-050, FR-051)

- **Nota del proveedor** (`SupplierNote`): contra su factura (`NoteOf`, líneas enlazadas), con su
  `SupplierInvoiceDetail` (`IsDebitNote`); emite «FacturaProveedorRegistrada» con su signo y, si cambia
  el precio de lo recibido, «AjusteDeCostoReconocido». Sus impuestos y retenciones, con la foto de la
  factura y en proporción, sin volver a probar la base mínima (E9).
- **Devolución a proveedor** (`SupplierReturn`): línea enlazada (`ReturnOf`) a la de recepción; sale al
  costo de esa línea; la diferencia con el promedio, `VoidDifference`; emite «DevolucionRegistrada».
  Devuelto ≤ recibido − ya devuelto y con disponible suficiente. Si la compra se soportó con documento
  soporte, la misma transacción crea su `SupportDocumentAdjustmentNote` (I4).

### 9.5 Documento soporte (I4; FR-063, FR-051)

`SupportDocument` es un documento de esta tabla con `CounterpartyPersonId` = el vendedor no obligado
(`COR_People.IsObligatedToInvoice = false`), enlazado a sus recepciones (`InvoiceOfReceipt`) o sin
recepción si es un servicio. Su prefijo y número salen de la resolución (`FiscalPrefix`,
`NumeradorFiscal`) y su parte electrónica es un `COR_ElectronicDocuments` con `SourceModule = "INV"` y
`SourceDocumentPublicId` = el `PublicId` del documento (§18). Por `DocumentoSoporte.Generacion =
Semanal`, uno solo puede reunir las recepciones de la semana. Se corrige con su nota de ajuste, nunca
con contrario (FR-066).

### 9.6 `INV_PurchaseMatchLines` — `PurchaseMatchLine` (I5; FR-050, US13)

Resultado del cruce a tres vías (`CruceDeCompra`) por línea de factura, calculado en cada intento de
confirmar.

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `InvoiceDocumentId` · `InvoiceLineId` | int FK | no | |
| `OrderLineId` | int FK | sí | |
| `OrderedQuantity` · `ReceivedNotInvoicedQuantity` · `InvoicedQuantity` | `Cantidad` | no | en unidad base |
| `OrderedUnitPrice` · `ReceivedUnitCost` · `InvoicedUnitPrice` | `PrecioUnitario` | sí | |
| `QuantityDifference` | `Cantidad` | no | |
| `PriceDifferenceAmount` · `PriceDifferenceRate` | `Monto` · `Tarifa` | no | |
| `ExceedsTolerance` | bit | no | según `Compras.Tolerancia*` y `Compras.ReglaDeTolerancia` vigentes a la fecha |
| `ToleranceJson` | nvarchar(1000) | no | los valores de tolerancia que se usaron |
| `ApprovalRequestPublicId` | uniqueidentifier | sí | la solicitud de la excepción (política «Diferencia de cruce») |
| `Status` | int (`PurchaseMatchStatus`) | sí | **(nuevo)** `Held=1, Approved=2, Rejected=3` (api.md §14.9); nulo si la línea no excede |
| `Reasons` | nvarchar(40) | sí | **(nuevo)** motivos de la diferencia separados por coma: `Quantity`, `Price` |

`IX (InvoiceDocumentId)`. **Reglas**: una línea que excede queda retenida mientras su solicitud está
`Pending`; aprobada, la diferencia de precio ajusta el costo de lo existente y el costo de venta de lo
vendido (US13-2); rechazada, la factura vuelve a borrador. Las filas de un intento anterior quedan de
baja lógica al recalcular. Facturar más de lo recibido nunca se aprueba por tolerancia (US13-1).

### 9.7 `INV_LandedCostAllocations` — `LandedCostAllocation` (I5; FR-046, US13-3)

El documento `LandedCost` enlaza la factura del flete o el seguro (otra factura del proveedor, de
servicio, con su retención de transporte) y las recepciones (`LandedCostOf`); su `Subtotal` es lo que se
reparte y `AllocationMethod` (**nuevo** enum `LandedCostAllocationMethod { Value = 1, Quantity = 2,
Weight = 3, Volume = 4, Manual = 5 }`, que §2.5 no tiene) el método.

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `DocumentId` | int FK | no | el `LandedCost` |
| `ReceiptDocumentId` · `ReceiptLineId` | int FK | no | |
| `ProductId` | int FK | no | |
| `Basis` | decimal(18,6) | no | pesos, cantidad, kg o litros de la línea según el método; en `Manual`, el valor digitado |
| `AllocatedAmount` | `Monto` | no | |
| `RoundingResidue` | `Monto` | no | 0 salvo en la línea que recibió el residuo |
| `ExistingRatio` | `Tarifa` | no | `mín(1, existencia actual / cantidad recibida)` del ámbito (D5, a validar por la contadora) |
| `ExistingAmount` · `SoldAmount` | `Monto` | no | a inventario y a costo de venta |

`UK (DocumentId, ReceiptLineId)`. **Invariantes**: Σ `AllocatedAmount` = `Subtotal` del documento,
exacto, con el residuo por `Redondeo.Residuo` y visible; `ExistingAmount + SoldAmount =
AllocatedAmount`. Al confirmar escribe `CostAdjustment` `LandedCost` con `AffectsEntryId` y un
«AjusteDeCostoReconocido» por recepción afectada. Antes de I5, un flete facturado aparte va al gasto
(E7).

### 9.8 Solicitud y orden (I5; FR-048)

`PurchaseRequest` (bodega, `ExpectedDate`, líneas sin precio, aprobación por su política) y
`PurchaseOrder` (proveedor, precios, `ExpectedDate`, condiciones en `Notes`, aprobación por montos,
envío al proveedor en PDF o por correo, auditado). La orden aprobada no recibida cuenta como «por
recibir» en la posición de reorden. Lo pendiente de recibir se calcula por los vínculos.

**Cerrar el saldo** (decisión del dueño: se adopta; `ClosePurchaseOrderBalanceCommand`, `POST
/purchases/orders/{id}/close-balance`, `Purchases.Confirm`, motivo obligatorio): una orden confirmada con
saldo pendiente anota `BalanceClosedAt`, `BalanceClosedByUserId` y `BalanceClosedReason` en
`INV_Documents` (§5.1). Desde ese momento no admite recepciones (`Inventory.PurchaseOrder.NotOpen`) y su
saldo deja de contar como «por recibir». No mueve kardex ni genera mensaje; lo recibido y facturado no
cambia. Una orden sin confirmar o con el saldo ya cerrado responde `Inventory.PurchaseOrder.NotOpen`. Queda
en la auditoría con su motivo.

---

## 10. Saldo inicial (I1; FR-089 a FR-091, SC-016)

No tiene tablas propias: es la clase `OpeningBalance` más `INV_Warehouses.CutoffDate`.

- `ImportOpeningBalanceCommand` (`Inventory.OpeningBalance.Load`, `ModoDeImportacion`): plantilla con
  producto, bodega, ubicación, cantidad y costo unitario (lote, serie y vencimiento en I6); revisión
  previa completa y todo o nada. Genera documentos `OpeningBalance` por bodega de hasta **4.000
  líneas** cada uno (T15: SQL Server escala el bloqueo de fila a tabla con más), fechados en la
  `CutoffDate` de la bodega (que fija el primer documento si estaba vacía).
- Cada documento se confirma con aprobación (`Inventory.OpeningBalance.Approve`) y entra al costo
  cargado. Emite «SaldoInicialCargado», **informativo**: no pasa por la validación previa ni por el modo
  de paso (`PostingMode` nulo). Su anulación también es informativa.
- Sólo en bodegas `NotActivated`; tras activar, `Inventory.OpeningBalance.WarehouseActive` (**nuevo**).
  Recargar antes de activar = anular los documentos anteriores y volver a importar.
- La cantidad es la del conteo más o menos lo movido en SOLIDO entre el conteo y el corte, o la bodega
  deja de operar en SOLIDO desde el conteo (FR-089).
- **Excepción de puesta en marcha** (FR-089 a FR-091, SC-016; precisión aplicada a la spec sobre
  FR-045; pregunta D8, con esta excepción como propuesta por defecto). Con el ámbito de costo por
  defecto (`Costeo.Ambito = Cooperativa`), la segunda bodega y las siguientes cargan su saldo inicial
  fechado en su propia `CutoffDate`, cuando las ya activas tienen movimientos posteriores de los mismos
  productos: ese `OpeningBalance` es retroactivo. Un `OpeningBalance` (y su `Voiding`) de una bodega
  `NotActivated` **no está sujeto** a `Costeo.RetroactivosPermitidos`. Si deja movimientos posteriores
  en su ámbito, el motor aplica el retroactivo mínimo que I1 entrega sólo para esta clase y para los
  ajustes de conteo (§3.1): inserta sus entradas en el orden `(OperationDate, Id)`, recalcula el costo
  de las salidas posteriores de las otras bodegas del ámbito (el saldo entra al costo cargado y mueve
  el promedio de la cooperativa) y registra un «AjusteDeCostoReconocido» por documento afectado, que
  sí sigue el modo de paso de ese documento. Caso dorado 17: «segunda bodega activada después de
  ventas en la primera, ámbito cooperativa».

---

## 11. Vendedores: `INV_Salespeople` — `Salesperson` (se conserva; FR-031, FR-092)

Sin columnas nuevas (`PersonId`, `SalespersonType`, `AppliesCommission`). Un cambio de índice en I1:
`UK_INV_Salespeople_PersonId` pasa a filtrado `[IsDeleted] = 0` (con el índice sin filtro, volver a
asignar el rol a una persona retirada respondía 500). La operación se muda y endurece **antes** del
retiro de las tablas: `CreateSalespersonCommand` restaura la fila dada de baja de esa persona si existe
(mismo `PublicId`: los documentos que la citan siguen apuntando a ella) o crea una, y pone
`Person.IsSalesperson = true` en el mismo `SaveChanges`; `DeleteSalespersonCommand` da de baja la fila y
apaga la marca. Permisos `Inventory.Salespeople.View/Manage`, rutas `/api/inventory/salespeople` (+
`template.xlsx`, `import`). `ImportSalespeopleCommand` cita a la persona por su documento; si no existe,
rechaza la fila nombrándola: la importación no crea ni modifica personas (FR-031, feature 008).

---

## 12. Retiro del módulo heredado (I1; FR-092, T4)

Migración par **destructiva con guarda** `RetiroDelInventarioHeredado`, en su propio commit y **antes**
de `InventarioComercialNucleo` (que reutiliza tres nombres). Suelta estas 23 tablas:

`INV_CommissionParameters`, `INV_CommissionPriceParams`, `INV_DiscountTypes`, `INV_Discounts`,
`INV_Documents`, `INV_Invoices`, `INV_Transactions`, `INV_TransactionTypes`, `INV_Locations`,
`INV_OrderDocuments`, `INV_OrderTransactions`, `INV_PhysicalInventory`, `INV_Prices`,
`INV_PriceListTypes`, `INV_PrimaryGroups`, `INV_ProductAccounts`, `INV_Products`, `INV_ProductGroups`,
`INV_SalesPoints`, `INV_SecondaryGroups`, `INV_Shifts`, `INV_VatAccounts`, `INV_Warehouses`.

- **Guarda**: cuenta las filas de las 23 y se niega nombrando tabla y cantidad (PostgreSQL `DO $$ …
  RAISE EXCEPTION`; SQL Server `THROW 50012`) salvo que la base tenga `COR_SystemSettings.SettingKey =
  'INV.RetiroHeredado.Aprobado'` con referencia al respaldo (`pg_dump`), segundo revisor y fecha,
  insertada a mano después del respaldo. Cabecera `MIGRACION-DESTRUCTIVA-APROBADA`. `Down()` las recrea
  vacías. Antes de fusionar se corre `specs/012-inventario-comercial/diagnostico-inventario-heredado.sql`
  en DEV, QA y PDN.
- **Nombres reutilizados** después, con otro esquema: `INV_Documents`, `INV_Products`,
  `INV_Warehouses`. Por eso el retiro deja un estado intermedio del modelo sin las 23 entidades ni las
  nuevas, y la paridad de proveedores se revisa en los dos pasos.
- **Se conserva** `INV_Salespeople` (Principio V) con su operación de §11. **No se tocan ni se usan**
  `COR_PaymentMethods`, `COR_Sequences`, `DEB_PosTerminals` ni `COR_Companies.Dian*`.
- Con las tablas salen sus 23 entidades, los archivos de `Application/Inventory` salvo `Salespeople`, las
  rutas de `Endpoints/Inventory`, el `InventoryReportsEndpoints` actual, `InventoryValuationReport`, las
  20 páginas, `NavMenu` 94-111, sus tarjetas del Centro de Reportes, sus temas de `ManualCatalogo` y su
  uso en `IdentitySeedData`; `AccountReferenceFinder` deja de mirar `ProductAccounts`/`VatAccounts`. Las
  tareas de la 009 T120 (parte Inventario), T125, T128 y T129 (Inventario) quedan «reemplazadas por la
  012».

---

## 13. Códigos de error nuevos de §0 a §12

Todos de la familia `Inventory.*` (§2.17 lista las principales): `Inventory.Category.TooDeep`,
`Inventory.Product.Blocked`, `Inventory.Product.HasHistory`, `Inventory.Product.BaseUnitLocked`,
`Inventory.Serial.AlreadyInStock`, `Inventory.WarehouseType.TransitIsSystem`,
`Inventory.Warehouse.HasStock`, `Inventory.Document.DateBeforeCutoff`,
`Inventory.Count.ProductsLocked`, `Inventory.SupplierInvoice.Duplicate`,
`Inventory.OpeningBalance.WarehouseActive`. Lo que falla por alcance o permiso responde el 404
genérico (FR-009), **salvo** los permisos que dependen del cuerpo cuando el recurso ya es visible
(costo digitado `Inventory.Adjustments.SetUnitCost`, aceptar la diferencia de activación, aceptar
remisiones sin facturar, dejar sin paso un tipo fiscal, el permiso de una clave de parámetro): ésos
responden 422 con código propio, porque no revelan existencia (precisión aplicada a la spec sobre
FR-009; pregunta C10).

---

<!-- Secciones 14 a 27: ventas, caja, medios de pago, impuestos, DIAN, mensajería, lado contable, seguridad, alertas y auditoría -->

Las §0 a §13 traen parámetros con vigencia, catálogo, bodegas, kardex y proyecciones, documento
genérico, compras, traslados, conteos, períodos y puesta en marcha. Desde aquí va lo que el módulo necesita
para **vender y cobrar**, los dos catálogos de Core que comparte con otros módulos (impuestos y medios
de pago), la **facturación electrónica** de plataforma, la **bandeja de mensajes** hacia Contabilidad y
Cartera con la idempotencia de pantalla, el **lado contable** de la enmienda a la 009 y las piezas de
**seguridad, alertas y auditoría** que el módulo estrena.

Los nombres de tabla, entidad, enumeración, mensaje, parámetro, permiso y componente son los de
`decisiones-transversales.md` §2 (en la carpeta de la feature, `specs/012-inventario-comercial/`), al
pie de la letra. Lo que estas secciones tuvieron que agregar (una enumeración, un valor, una columna que la §2 no
nombra) está listado en la §26, para llevarlo a ese documento antes de `tasks.md`.

Convenciones (las de la §0, en una línea cada una):

- Toda entidad hereda `AuditableEntity` (`int Id`, `PublicId`, borrado lógico, `RowVersion`,
  `CreatedAt/By`, `UpdatedAt/By`) salvo donde se dice `AuditableEntityLong` (`bigint Id`); toda
  configuración declara el filtro `!IsDeleted`; FK con `Restrict` siempre.
- Índices filtrados escritos en T-SQL y traducidos por `ProviderModelConventions.ApplyPortableIndexFilters`;
  «vivos» = `[IsDeleted] = 0`. Donde una unicidad involucra columnas anulables se usa una **clave
  normalizada en texto** (`ScopeKey`, `DimensionKey`, `DetailKey`, `PolicyKey`): SQL Server trata dos
  NULL como iguales en un índice único y PostgreSQL como distintos.
- Precisión (`PrecisionDeInventario`, T19): cantidad (18,4), factor (18,6), costo y precio unitario
  (18,6), monto (18,2), **tarifa, porcentaje y tope como fracción (9,6)** (0,19 = 19 %).
- Enumeraciones: `int` con el valor numérico (§0); donde una columna dice `int (EnumX)`, se
  guarda el número. La FK a la unidad de medida se llama `UnitId` (como en §1).
- Fechas de negocio `date` en hora de Colombia (`IDateTimeService.HoyLocal`, T20); instantes `datetime`
  en UTC.
- Marcas: **(H)** hecho inmutable (`IHechoInmutable`, sólo inserción; lo hace cumplir
  `ApplicationDbContext.SaveChangesAsync`); **[SinDiff]** = `[SinDiffDeAuditoria]` en la entidad (su
  historia es su bitácora, no el diff del interceptor); **[NoAuditar]** en la propiedad; **E** = entrega
  en que nace la tabla o la columna.
- Una columna marcada «copia» se escribe al confirmar y no vuelve a cambiar: es la historia cuando el
  catálogo cambia después (Principio XI). Nadie la «refresca».

---

## 14. Ventas: documentos, precios, descuentos y promociones

Las ventas **no tienen tabla de cabecera propia**: son clases de `INV_Documents` / `INV_DocumentLines`
(T17, §5). Esta sección fija qué usa cada clase de venta, las pocas columnas de cabecera que sólo
usan las ventas y los satélites que son de ventas.

### Las clases de venta sobre el documento genérico

| Clase (`DocumentClass`) | E | Número | Efecto en inventario | Nace de (`DocumentLinkKind`) | Mensajes (§2.6) | Lo propio de la clase |
|---|---|---|---|---|---|---|
| `SalesQuote` | I6 | `INV_DocumentSequences` | ninguno | — | ninguno | `ValidUntil` obligatorio; vencida no se convierte |
| `SalesOrder` | I6 | secuencia | reserva (`INV_Reservations`) | `FromOrder` (opcional, desde cotización) | ninguno | `ValidUntil` = fecha + `Ventas.ReservaDiasVencimiento` (sellado); disponible = físico − reservado |
| `Shipment` | I6 | secuencia | salida al promedio | `DispatchOf` (opcional, desde pedido; consume la reserva) | `CostoDeVentaReconocido` | cadena `Sales`; lo pendiente de facturar se calcula con `INV_DocumentLineLinks`; alerta `Inventario.RemisionSinFacturar` |
| `SalesInvoice` | I3 | resolución `Invoice` (`NumeradorFiscal`) | salida; libera la reserva si viene de pedido | `FromOrder` (opcional, I6) | `VentaFacturada` + `CostoDeVentaReconocido` (+ `VentaACreditoRegistrada`) | confirma sólo si `GuardiaDeEmisionFiscal` responde `Electronic` |
| `SalesInvoiceFromShipments` | I6 | resolución `Invoice` | ninguno | `FromShipment` (una o varias remisiones) | `VentaFacturada` (+ Cartera) | derivado: no sella modo, copia el de sus remisiones (T9); todas con el mismo destino, o `Inventory.PostingMode.ChainMismatch` |
| `PosEquivalentDocument` | I3 | resolución `PosEquivalent` | salida | — | `VentaFacturada` + `CostoDeVentaReconocido` (+ Cartera) | exige sesión, caja y punto; guardia `Electronic` |
| `NonElectronicSalesReceipt` | I3 | secuencia | salida | `FromOrder` (I6) | `VentaFacturada` + `CostoDeVentaReconocido` (+ Cartera) | sólo con guardia `NonElectronic` (cooperativa no obligada) |
| `NonElectronicSalesNote` | I3 | secuencia | entrada al costo con que salió, o ninguno | `NoteOf`; `ReturnOf` por línea | `NotaCreditoEmitida` (+ `DevolucionRegistrada`) (+ `AjusteDeVentaACredito`) | sólo sobre `NonElectronicSalesReceipt` |
| `CreditNote` | I3 | secuencia (T16: las notas no usan resolución) | ídem | `NoteOf` sobre `SalesInvoice` o `SalesInvoiceFromShipments`; `ReturnOf` por línea | ídem | `CorrectionConceptCode`; la nota de una factura desde remisiones no mueve existencia y deja esas remisiones otra vez pendientes de facturar |
| `PosAdjustmentNote` | I3 | secuencia | ídem | `NoteOf` sobre `PosEquivalentDocument` | ídem | ídem |
| `DebitNote` | I6 | secuencia | ninguno | `NoteOf` | `NotaDebitoEmitida` (+ `AjusteDeVentaACredito` si la original fue a crédito) | fiscal; `CorrectionConceptCode` |
| `Voiding` de una venta | I3 | secuencia del tipo `Voiding` | la contraria del original, al costo del original | `Voids` | `DocumentoAnulado` (+ `AjusteDeCostoReconocido`) (+ `AjusteDeVentaACredito`) | sólo sobre `NonElectronicSalesReceipt`, `NonElectronicSalesNote`, `Shipment`, `SalesOrder` (libera la reserva, sin mensajes) y `SalesQuote` (sin efecto). Un documento fiscal emitido nunca se anula así: se corrige con su nota (FR-066); los casos b y c de un rechazo por la DIAN usan este `Voiding` desde `ReplaceRejectedDocumentCommand` / `CancelRejectedDocumentCommand` |

Reglas que atraviesan las clases:

- **Contraparte**: `CounterpartyPersonId` siempre; la venta sin identificar usa la persona «Consumidor
  final» que siembra `ConsumidorFinalSeeder` (Order 87). La copia fiscal va en
  `INV_DocumentPartySnapshots` (§5, T52).
- **Totales** (T26): `Total` = `Subtotal` − `DiscountTotal` + `TaxTotal`; `AmountDue` = `Total` −
  `WithholdingTotal` (las retenciones que practica un comprador agente retenedor, renglones
  `WithholdingSuffered` de `INV_DocumentTaxLines`). La suma de los pagos recibidos iguala `AmountDue`
  exactamente, o `Payments.TotalMismatch`.
- **Vendedor**: `SalespersonId` → `INV_Salespeople`, opcional (FR-057).
- **Canal**: en el POS lo fija el punto (`INV_PointsOfSale.SalesChannelId`); en oficina, el tipo de
  documento.
- **POS**: `PointOfSaleId`, `CashRegisterId` y `CashSessionId` son obligatorios en toda venta del POS y
  en `PosEquivalentDocument`. En una factura de oficina, la cabecera no los exige: basta que cada **pago
  que se arquea** lleve su sesión (T50, `INV_DocumentPayments.CashSessionId`).
- **Borrador del POS** (T50): vive en el servidor desde la primera lectura, ligado a la sesión y sin
  número. `IsSuspended`, `SuspendedAt`, `SuspendedLabel` sólo existen en borradores; recuperar una venta
  suspendida en otra caja del mismo punto la pasa a la sesión de quien la recupera (auditado). Una
  sesión no cierra con borradores propios, suspendidos o no.

### Columnas de `INV_Documents` e `INV_DocumentLines` que sólo usan las ventas

La §5 define la cabecera y la línea, y lista estas columnas en sus tablas (§5.1 y §5.4) con E = I1:
nacen con la tabla en `InventarioComercialNucleo` (anulables o con defecto) para no alterar
`INV_Documents` en I3, y se usan desde I3/I6. Aquí van sus reglas.

| Tabla | Columna | Tipo | Regla |
|---|---|---|---|
| `INV_Documents` | `ValidUntil` | date, nullable | vigencia de la cotización; vencimiento de la reserva del pedido (sellado al confirmar) |
| `INV_Documents` | `DueDate` | date, nullable | vencimiento final de una venta con parte a crédito (lo necesita el UBL: forma de pago crédito); = el mayor `FinalDueDate` de sus pagos de crédito |
| `INV_Documents` | `ReturnsGoods` | bit NOT NULL DEFAULT 0 | sólo notas de venta: la nota devuelve mercancía (sus líneas con `ReturnOf` entran al costo con que salieron y emiten `DevolucionRegistrada`); falso = rebaja, descuento o ajuste de precio sin movimiento. Siempre falso en la nota de una `SalesInvoiceFromShipments` |
| `INV_Documents` | `IsFullReversal` | bit NOT NULL DEFAULT 0 | nota por el total del documento: `NotaCreditoEmitida` lleva la marca de anulación total (FR-066) y `AjusteDeVentaACredito.AdjustmentClass = CreditNote` por el total |
| `INV_Documents` | `CorrectionConceptCode` | nvarchar(2), nullable | concepto de corrección DIAN de la nota (tabla de `CatalogoDian`: devolución parcial, anulación, rebaja, ajuste de precio…). Obligatorio en `CreditNote`, `PosAdjustmentNote`, `DebitNote` y en la nota de ajuste del documento soporte; `IsFullReversal = 1` exige el concepto de anulación |
| `INV_DocumentLines` | `ListPriceIncludesTaxes` | bit NOT NULL DEFAULT 0 | copia de `INV_PriceLists.IncludesTaxes` de la lista aplicada. `ListPrice` queda como está en la lista; `UnitPrice` (18,6) es siempre **sin** impuestos: con lista que los incluye, `UnitPrice` = `ListPrice` ÷ (1 + Σ tarifas porcentuales generadas), y el residuo de redondeo se asigna por `Redondeo.Residuo`, visible (FR-017) |

Lo que la línea de venta ya trae de la §5 y cómo lo usan las ventas: `PriceListId` (la lista que
resolvió `ResolutorDeListaDePrecios`; se vuelve a resolver al cambiar el cliente), `ListPrice`,
`UnitPrice`, `UnitCost` (costo de salida, el que usará la devolución). Vender bajo costo se decide al
agregar la línea contra `INV_CostStates.AverageCost` del ámbito: `Ventas.BajoCosto = Alertar` levanta
`Inventario.VentaBajoCosto`; `Bloquear` responde `Inventory.Sales.BelowCost`.

### `INV_DocumentLineDiscounts` — `DocumentLineDiscount` (I3)

Uno o varios descuentos por línea. El descuento por total del documento se **prorratea** a las líneas
(una fila por línea con `FromDocumentDiscount = 1`) para que baje la base gravable línea a línea.

| Columna | Tipo | Regla |
|---|---|---|
| `DocumentLineId` | int, FK `INV_DocumentLines` | |
| `DocumentId` | int, FK `INV_Documents` | desnormalizado para la vista `discount-approvals` |
| `Sequence` | tinyint | orden de aplicación en la línea |
| `Source` | int (`DiscountSource`: `Manual=1`, `Promotion=2`) | con una promoción en la línea no entra un manual (no acumulables, F9) |
| `FromDocumentDiscount` | bit | la fila es la parte de la línea de un descuento por total |
| `IsPriceOverride` | bit | el cajero cambió el precio de lista: el descuento es (`ListPrice` − precio digitado) × cantidad y respeta el tope (T51) |
| `Rate` | 9,6, nullable | fracción del descuento cuando se expresó en porcentaje |
| `Amount` | 18,2 | valor del descuento en la línea; la suma de las partes de un descuento por total iguala el total exactamente (residuo por `Redondeo.Residuo`) |
| `CapRateApplied` | 9,6 | copia del tope del usuario al aplicarlo (el mayor de sus roles en `INV_DiscountCaps` a la fecha; sin fila, 0) |
| `RequiresApproval` | bit | `Rate` efectivo sobre el tope |
| `ApprovalRequestId` | int, nullable, FK `COR_ApprovalRequests` | la solicitud con sujeto `DiscountOverCap` |
| `ApprovedByUserId` | int, nullable, FK `SEC_Users` | copia del aprobador (informe) |
| `ApprovalMethod` | int, nullable (`ApprovalMethod`) | `OwnSession`, `InPersonPasskey`, `InPersonTotp` |
| `Reason` | nvarchar(200), nullable | obligatorio si `RequiresApproval` |
| `PromotionId` | int, nullable, FK `INV_Promotions` | **I6** (entra con `ComercioAmpliado`); obligatorio si `Source = Promotion` |

Único `(DocumentLineId, Sequence)` filtrado vivos. **Invariantes**: la aprobación vale mientras la línea
no cambie: la solicitud guarda `ContentSha256` de (línea, producto, cantidad, `ListPrice`, `UnitPrice`,
`Amount`) y cualquier cambio la deja sin efecto (T33). Cambiar el precio de lista a mano **es** un
descuento: no hay otra forma de vender por debajo de la lista. En un documento confirmado, inmutable.

### `INV_Reservations` — `Reservation` (I6)

Cantidad comprometida por un pedido vigente. `INV_StockBalances.Reserved` (§3) es su proyección:
Σ (`QuantityBase` − `ConsumedQuantityBase`) de las reservas `Active`, reconstruible.

| Columna | Tipo | Regla |
|---|---|---|
| `DocumentId` | int, FK `INV_Documents` | el `SalesOrder` |
| `DocumentLineId` | int, FK `INV_DocumentLines` | |
| `ProductId`, `WarehouseId` | int, FK | |
| `QuantityBase` | 18,4 | reservado en unidad base |
| `ConsumedQuantityBase` | 18,4 | lo que ya salió por factura o remisión desde el pedido |
| `ExpiresOn` | date | copia de `INV_Documents.ValidUntil` |
| `Status` | int (`ReservationStatus`: `Active=1`, `Consumed=2`, `Released=3`, `Expired=4`) | |
| `ReleasedAt` | datetime, nullable | |
| `ReleasedByDocumentId` | int, nullable, FK `INV_Documents` | la anulación del pedido, o la factura que consumió la última parte |
| `ReleaseReason` | nvarchar(200), nullable | |

Índices: `(ProductId, WarehouseId, Status)`, `(ExpiresOn)` filtrado `[Status] = 1`. **Invariantes**:
crear una reserva pasa por el cerrojo (T15) sobre `INV_StockBalances` y exige disponible ≥ cantidad;
consumirla ocurre en la misma transacción de la salida; la vence `ProgramadorDeTareas` (tarea «liberar
reservas vencidas», actor proceso). Es estado, no hecho: su historia es el diff de auditoría.

### `INV_PriceLists` — `PriceList` y `INV_PriceListItems` — `PriceListItem` (I3)

| Columna (`INV_PriceLists`) | Tipo | Regla |
|---|---|---|
| `Code` | nvarchar(10) | `CodigoDeCatalogo`, slug `listas-de-precios`; único filtrado vivos |
| `Name` | nvarchar(80) | |
| `IncludesTaxes` | bit | el precio de la lista trae los impuestos incluidos |
| `PersonId` | int, nullable, FK `COR_People` | ámbito cliente |
| `Segment` | nvarchar(4), nullable | ámbito segmento = `COR_Associates.AssociateClass`, validado contra los valores existentes (F10) |
| `SalesChannelId` | int, nullable, FK `INV_SalesChannels` | ámbito canal |
| `BranchId` | int, nullable, FK `COR_Branches` | ámbito sucursal |
| `ScopeKey` | nvarchar(80) | `P:{PersonId\|-}\|S:{Segment\|-}\|C:{SalesChannelId\|-}\|B:{BranchId\|-}`; la general es `P:-\|S:-\|C:-\|B:-` |
| `DimensionCount` | tinyint | cuántas dimensiones no nulas (0..4); ordena la resolución |
| `ValidFrom` | date | |
| `ValidTo` | date, nullable | |
| `Currency` | char(3) | `COP` (FR-018) |
| `IsActive` | bit | |
| `Notes` | nvarchar(300), nullable | |

Único `(ScopeKey, ValidFrom)` filtrado vivos. **Dos listas del mismo `ScopeKey` no se cruzan en el
tiempo**: el comando lo verifica tomando bloqueo de actualización sobre las listas de ese `ScopeKey`, y
responde `Inventory.PriceList.Overlaps` nombrando la otra. Resolución (T51, pura, casos dorados):
candidatas = vigentes a la fecha con todas sus dimensiones no nulas coincidentes; primero la de más
dimensiones; en empate cliente > segmento > canal > sucursal; la general al final; si la elegida no
trae el producto, la siguiente candidata que lo traiga (F8).

| Columna (`INV_PriceListItems`) | Tipo | Regla |
|---|---|---|
| `PriceListId` | int, FK | |
| `ProductId` | int, FK `INV_Products` | no admite `ProductKind.Template` |
| `UnitId` | int, FK `INV_UnitsOfMeasure` | la unidad base del producto o una alterna de venta (`INV_ProductUnits`) |
| `Price` | 18,2 | en pesos (T19); con o sin impuestos según la lista |

Único `(PriceListId, ProductId, UnitId)` filtrado vivos. Cambiar un precio de una lista vigente
queda en el diff de auditoría; un cambio programado es una lista nueva del mismo ámbito con su
`ValidFrom`. Los documentos no dependen de la lista después de confirmar: la línea guarda lista, precio
de lista y precio. La lista **general** vigente es obligatoria para el retiro gravado (FR-037) y para la
vista `impairment`; sin precio, esos documentos dicen qué precio falta.

### `INV_DiscountCaps` — `DiscountCap` (I3)

| Columna | Tipo | Regla |
|---|---|---|
| `RoleId` | int, FK `SEC_Roles` | rol de la cooperativa |
| `MaxLineRate` | 9,6 | tope por línea (fracción) |
| `MaxDocumentRate` | 9,6 | tope por total |
| `ValidFrom`, `ValidTo` | date, date nullable | |
| `Reason` | nvarchar(300) | obligatorio (`IConMotivo`) |

Único `(RoleId, ValidFrom)` filtrado vivos; sin cruces del mismo rol (`Inventory.DiscountCap.Overlaps`).
Tope de un usuario = el mayor de los topes vigentes de sus roles activos; sin fila, 0 (T51).

### Promociones (I6): `INV_Promotions`, `INV_PromotionScopes`, `INV_PromotionTiers`

`MotorDePromociones` (puro) produce **descuentos no condicionados** por línea (`Source = Promotion`),
nunca líneas a precio cero; entre promociones no acumulables gana la de mayor descuento.

| Columna (`INV_Promotions` — `Promotion`) | Tipo | Regla |
|---|---|---|
| `Code`, `Name` | nvarchar(10), nvarchar(80) | |
| `Kind` | int (`PromotionKind`: `Percent=1`, `Amount=2`, `BuyNPayM=3`, `QuantityPrice=4`, `BundlePrice=5`) | |
| `Rate` | 9,6, nullable | `Percent` |
| `Amount` | 18,2, nullable | `Amount`: descuento por unidad vendida |
| `BuyQuantity`, `PayQuantity` | 18,4, nullable | `BuyNPayM` («3×2»: 3 y 2; el descuento es el precio de una, repartido en las 3) |
| `BundlePrice` | 18,2, nullable | `BundlePrice`: precio del paquete completo |
| `IsCumulative` | bit NOT NULL DEFAULT 0 | |
| `ValidFrom`, `ValidTo` | date | |
| `IsActive` | bit | |
| `Notes` | nvarchar(300), nullable | |

| Columna (`INV_PromotionScopes` — `PromotionScope`) | Tipo | Regla |
|---|---|---|
| `PromotionId` | int, FK | |
| `ScopeKind` | int (`PromotionScopeKind`: `Product=1`, `Category=2`, `Segment=3`, `Channel=4`) | una sola columna destino llena por fila, la de su clase |
| `ProductId` | int, nullable, FK `INV_Products` | |
| `ProductCategoryId` | int, nullable, FK `INV_ProductCategories` | incluye sus descendientes |
| `Segment` | nvarchar(4), nullable | |
| `SalesChannelId` | int, nullable, FK `INV_SalesChannels` | |
| `RequiredQuantity` | 18,4, nullable | sólo `BundlePrice`: cuántas unidades del producto forman el paquete |

Semántica: dentro de una misma `ScopeKind`, cualquiera (O); entre clases distintas, todas (Y). Sin
filas de una clase, esa clase no restringe.

| Columna (`INV_PromotionTiers` — `PromotionTier`) | Tipo | Regla |
|---|---|---|
| `PromotionId` | int, FK | sólo `QuantityPrice` |
| `MinQuantity` | 18,4 | desde esta cantidad… |
| `UnitPrice` | 18,2 | …este precio unitario |

Único `(PromotionId, MinQuantity)` filtrado vivos.

---

## 15. Punto de venta y caja

### `INV_PointsOfSale` — `PointOfSale` (I3)

| Columna | Tipo | Regla |
|---|---|---|
| `Code` | nvarchar(10) | `CodigoDeCatalogo`; único filtrado vivos; **no cambia** una vez creado (es dimensión `PointOfSaleCode` de la matriz, T27) |
| `Name` | nvarchar(80) | |
| `BranchId` | int, FK `COR_Branches` | sucursal contable |
| `SalesChannelId` | int, FK `INV_SalesChannels` | el punto fija el canal (FR-058) |
| `PosEnabled` | bit | habilita la pantalla `/pos`; un punto sin POS igual tiene cajas y sesiones para el cobro de oficina en efectivo (T50) |
| `DefaultWarehouseId` | int, FK `INV_Warehouses` | de la misma sucursal |
| `Address` | nvarchar(120), nullable | ubicación que pide el documento equivalente POS |
| `IsActive` | bit | |

### `INV_CashRegisters` — `CashRegister` (I3)

| Columna | Tipo | Regla |
|---|---|---|
| `PointOfSaleId` | int, FK | |
| `Code` | nvarchar(10) | único `(PointOfSaleId, Code)` filtrado vivos |
| `Name` | nvarchar(60) | |
| `WarehouseId` | int, FK `INV_Warehouses` | bodega de la caja (propuesta: la del punto); de la sucursal del punto |
| `DefaultCardTerminalId` | int, nullable, FK `COR_CardTerminals` | datáfono que se propone al cobrar con tarjeta (la relación vive aquí para que Core no apunte al módulo) |
| `DianCashRegisterPlate` | nvarchar(30), nullable | placa o serial de la caja que pide el DEE POS |
| `DianCashRegisterTypeCode` | nvarchar(10), nullable | tipo de caja del catálogo DIAN (`CatalogoDian`) |
| `ReceiptWidthMm` | smallint | 58 o 80 |
| `PrintCopies` | tinyint DEFAULT 1 | |
| `IsActive` | bit | |

### `INV_CashRegisterDocumentTypes` — `CashRegisterDocumentType` (I3)

| Columna | Tipo | Regla |
|---|---|---|
| `CashRegisterId` | int, FK | |
| `Role` | int (`CashRegisterDocumentRole`: `PosSale=1`, `InvoiceOnRequest=2`, `PosAdjustmentNote=3`, `InvoiceCreditNote=4`, `PosSaleContingency=5`, `InvoiceContingency=6`) | |
| `DocumentTypeId` | int, FK `INV_DocumentTypes` | |

Único `(CashRegisterId, Role)` filtrado vivos. Una caja que expide el documento equivalente POS y
también factura a solicitud configura las dos notas y las dos contingencias (FR-058, FR-067).
Compatibilidad rol ↔ clase del tipo (la valida el comando): `PosSale` → `PosEquivalentDocument` o
`NonElectronicSalesReceipt`; `InvoiceOnRequest` → `SalesInvoice`; `PosAdjustmentNote` →
`PosAdjustmentNote` o `NonElectronicSalesNote` (las notas no electrónicas van en este rol);
`InvoiceCreditNote` → `CreditNote`; `PosSaleContingency` → un tipo fiscal cuyo prefijo tiene una
resolución `Kind = Contingency` con `BacksUpKind = PosEquivalent`; `InvoiceContingency` → uno cuyo
prefijo tiene una resolución `Kind = Contingency` con `BacksUpKind = Invoice`. En contingencia 03, la
venta usa el tipo del rol de contingencia que respalda al rol de la venta. Cambiar el tipo de un rol da
de baja la fila y crea otra (queda en el diff); los documentos ya emitidos guardan su tipo.

### Disponibilidad de medios: `INV_PaymentMeansPointsOfSale`, `INV_PaymentMeansChannels`, `INV_PaymentMeansDocumentTypes` (I3)

Tres conjuntos independientes (T25). Cada uno es `(PaymentMeansId → COR_PaymentMeans, PointOfSaleId |
SalesChannelId | DocumentTypeId)` con único filtrado vivos sobre el par. Un conjunto vacío **no**
significa «todos»: «todos» es la marca del medio (`OfferedAtAllPointsOfSale`, `OfferedInAllChannels`,
`OfferedForAllDocumentTypes`). Un medio se ofrece si está activo y vigente, aparece en los tres
conjuntos (o su marca «todos») y su clase es compatible con el caso (`DisponibilidadDeMedio`, pura,
la usan pantalla y servidor). Cobrar con un medio no disponible: `Payments.MeansNotAvailable`.

### `INV_CashSessions` — `CashSession` (I3)

La sesión es el turno de un cajero en una caja: reemplaza a `INV_Shifts` y separa el estado del día de
la configuración del punto.

| Columna | Tipo | Regla |
|---|---|---|
| `CashRegisterId` | int, FK | |
| `PointOfSaleId` | int, FK | copia desnormalizada (alcance, cierre del día) |
| `CashierUserId` | int, FK `SEC_Users` | de `IActorActual` (T6), nunca el entero del token |
| `CashierPersonId` | int, nullable, FK `COR_People` | copia de `SEC_Users.PersonId` al abrir; obligatoria si el tratamiento del faltante es `CargoAlCajero` |
| `CashierName` | nvarchar(150) | copia |
| `Label` | nvarchar(30), nullable | rótulo opcional («mañana», «tarde») |
| `OperatingDate` | date | fecha local de la apertura (T20); la fecha fiscal de cada venta es la real |
| `OpenedAt` | datetime | |
| `BaseMode` | nvarchar(12) | copia de `Caja.BaseModo` (`FondoFijo` \| `BaseDelDia`) |
| `OpeningBase` | 18,2 | con fondo fijo, la base que dejó la sesión anterior de la caja; con base del día o si cambia, un movimiento `BaseIncome` |
| `BaseIncomeDocumentId` | int, nullable, FK `INV_Documents` | ese movimiento |
| `ShortageTreatment` | nvarchar(14) | copia de `Caja.TratamientoFaltante` (`Gasto` \| `CargoAlCajero`) con ámbito punto |
| `IsBlindCount` | bit | copia de `Caja.ArqueoCiego` |
| `ExclusiveCashier` | bit | copia de `Caja.UnaSesionPorCajero` |
| `Status` | int (`CashSessionStatus`: `Open=1`, `Closed=2`) | |
| `LastActivityAt` | datetime | se **escribe** en cada cobro y movimiento de la sesión (ver «Concurrencia») |
| `ClosedAt` | datetime, nullable | |
| `ClosedByUserId` | int, nullable, FK `SEC_Users` | |

Índices únicos: `UK_INV_CashSessions_Register_Open (CashRegisterId)` filtrado `[Status] = 1 AND
[IsDeleted] = 0` (una sesión abierta por caja) y `UK_INV_CashSessions_Cashier_Open (CashierUserId)`
filtrado `[Status] = 1 AND [ExclusiveCashier] = 1 AND [IsDeleted] = 0` (un cajero, una sesión, cuando el
parámetro lo pide; la columna sellada deja que el índice sea fijo aunque el parámetro cambie). Más
`(PointOfSaleId, OperatingDate)`.

**Abrir** (`OpenCashSessionCommand`) exige: caja activa, alcance del usuario sobre el punto (T35), que
no haya un `INV_DayCloses` cerrado para `(punto, OperatingDate)`, persona vinculada si el faltante va a
cargo del cajero (`Inventory.CashSession.CashierWithoutPerson`, con cómo vincularla) y consulta
`GuardiaDeEmisionFiscal` como aviso temprano. **Cerrar** (`CloseCashSessionCommand`) es inmediato:
exige que no queden borradores de la sesión (`Inventory.CashSession.HasOpenDrafts`, `data.drafts`)
ni movimientos en aprobación (`Inventory.CashSession.HasPendingMovements`); escribe el arqueo, crea el
documento de diferencia si alguna línea difiere, y crea el lote `CashSessionClose` cuando corresponde
(T12).

**Concurrencia**. Cobrar, registrar un movimiento o un pago de oficina en efectivo hace
`UPDATE INV_CashSessions SET LastActivityAt = … WHERE Id = @id AND Status = 1` **antes** del orden
canónico del cerrojo (§1.3 de las decisiones) y exige una fila afectada; cerrar hace lo mismo al pasar
a `Closed`. Así un cierre espera al cobro en curso y lo incluye, o el cobro llega tarde y responde
`Inventory.CashSession.NotOpen`. Sólo toca sesiones el POS y la caja; el movimiento a otra caja toma las
dos sesiones por `Id` ascendente. Abrir sesión y cerrar el día serializan sobre la fila del punto
(bloqueo de actualización) para que no se abra una sesión sobre un día que se está cerrando.

### Movimientos de caja: clase `CashMovement` + `INV_CashMovementDetails` — `CashMovementDetail` (I3)

El movimiento es un documento (T50): su tipo trae consecutivo y política de aprobación; su cabecera
lleva `CashSessionId`, `PointOfSaleId`, `CashRegisterId` y `Reason` (obligatorio). Sólo lo confirmado
cuenta en el esperado. Al confirmar emite `MovimientoDeCajaRegistrado`; se imprime con
`CashMovementReceiptReport` (firmas).

| Columna | Tipo | Regla |
|---|---|---|
| `DocumentId` | int, FK `INV_Documents`, único | 1:1 |
| `CashSessionId` | int, FK | sesión de origen |
| `Kind` | int (`CashMovementKind`: `WithdrawalToSafe=1`, `WithdrawalToRegister=2`, `WithdrawalForDeposit=3`, `BaseIncome=4`, `ReclassificationBetweenMeans=5`) | |
| `SourcePaymentMeansId` | int, FK `COR_PaymentMeans` | el medio que sale de la sesión (en `BaseIncome`, el que entra: el efectivo) |
| `TargetPaymentMeansId` | int, nullable, FK | sólo `ReclassificationBetweenMeans`: el medio correcto |
| `Destination` | int, nullable (`CashMovementDestination`: `Safe=1`, `Register=2`, `Deposit=3`) | la otra punta: a dónde va en los retiros; de dónde viene en `BaseIncome` (`Safe`); nulo en la reclasificación |
| `DestinationCashRegisterId` | int, nullable, FK `INV_CashRegisters` | `WithdrawalToRegister` |
| `DestinationCashSessionId` | int, nullable, FK `INV_CashSessions` | la sesión abierta de esa caja, resuelta al confirmar (`Inventory.CashMovement.DestinationRegisterClosed` si no hay); el mismo documento resta en el origen y suma en el destino |
| `DepositBankId` | int, nullable, FK `COR_Banks` | `WithdrawalForDeposit`: banco al que se lleva (dato; la consignación es de Tesorería) |
| `Amount` | 18,2 | > 0 |
| `DenominationsJson` | nvarchar(max), nullable | `[{ denominationPublicId, quantity, amount }]` informativo |
| `ReclassifiedPaymentId` | int, nullable, FK `INV_DocumentPayments` | el pago registrado con el medio equivocado (nunca se edita: FR-005) |
| `TargetReference`, `TargetAuthorizationCode` | nvarchar(40), nvarchar(20), nullable | referencia y autorización del medio correcto |
| `TargetCardTerminalId` | int, nullable, FK `COR_CardTerminals` | |

### Arqueo: `INV_CashCounts`, `INV_CashCountLines`, `INV_CashCountDenominations`, `INV_CashCountTerminalBatches`, `INV_CashCountReferenceChecks` (I3)

Un arqueo por sesión, con una línea por medio usado o con saldo esperado. El esperado lo calcula
`CalculadoraDeEsperado` (pura, casos dorados): por medio, base (sólo el efectivo) + recibido − reintegrado
± movimientos confirmados (salen del origen, entran al destino, `BaseIncome` entra, la reclasificación
pasa de un medio al otro). Se edita mientras su documento de diferencia está en borrador (reconteo
tras un rechazo, auditado); después, inmutable.

| Columna (`INV_CashCounts` — `CashCount`) | Tipo | Regla |
|---|---|---|
| `CashSessionId` | int, FK, único | |
| `CountedAt` | datetime | |
| `CountedByUserId` | int, FK `SEC_Users` | |
| `IsBlind` | bit | copia de la sesión |
| `TotalExpected`, `TotalCounted`, `TotalDifference` | 18,2 | |
| `DifferenceDocumentId` | int, nullable, FK `INV_Documents` | el `CashCountDifference`, si alguna línea difiere |

| Columna (`INV_CashCountLines` — `CashCountLine`) | Tipo | Regla |
|---|---|---|
| `CashCountId` | int, FK | |
| `PaymentMeansId` | int, FK | único `(CashCountId, PaymentMeansId)` |
| `CountMethod` | int (`CashCountMethod`) | copia del medio |
| `ExpectedAmount`, `CountedAmount`, `DifferenceAmount` | 18,2 | diferencia = contado − esperado; en `None` (créditos) contado = esperado |
| `PaymentCount` | int | pagos de la sesión con ese medio |
| `ToleranceAmount` | 18,2 | copia de `COR_PaymentMeans.ToleranceAmount` |
| `WithinTolerance` | bit | |
| `Reason` | nvarchar(300), nullable | obligatorio si la diferencia ≠ 0 |
| `Treatment` | int, nullable (`CashDifferenceTreatment`: `Surplus=1`, `ShortageToCashier=2`, `ShortageToExpense=3`) | sobrante siempre `Surplus`; faltante según `ShortageTreatment` de la sesión |

| Columna (`INV_CashCountDenominations` — `CashCountDenomination`) | Tipo | Regla |
|---|---|---|
| `CashCountLineId` | int, FK | sólo líneas `PhysicalCount` |
| `CashDenominationId` | int, FK `COR_CashDenominations` | único `(CashCountLineId, CashDenominationId)` |
| `DenominationValue` | 18,2 | copia |
| `Quantity` | int | |
| `Amount` | 18,2 | su suma iguala `CountedAmount` cuando se contó por denominaciones |

| Columna (`INV_CashCountTerminalBatches` — `CashCountTerminalBatch`) | Tipo | Regla |
|---|---|---|
| `CashCountLineId` | int, FK | líneas `VoucherTotal` |
| `CardTerminalId` | int, FK `COR_CardTerminals` | |
| `BatchNumber` | nvarchar(20) | lote del cierre del datáfono |
| `BatchTotal` | 18,2 | total del lote según el voucher de cierre |
| `VoucherCount` | int | |
| `ExpectedTotal` | 18,2 | Σ pagos de la sesión con ese medio y ese datáfono |

Único `(CashCountLineId, CardTerminalId, BatchNumber)`. El lote se asocia a los pagos de la sesión y
datáfono sin editarlos (FR-101): el informe `card-payments` los une por sesión y datáfono.

| Columna (`INV_CashCountReferenceChecks` — `CashCountReferenceCheck`) | Tipo | Regla |
|---|---|---|
| `CashCountLineId` | int, FK | líneas `ByReference` (y cotejo opcional de tarjetas) |
| `DocumentPaymentId` | int, FK `INV_DocumentPayments` | único `(CashCountLineId, DocumentPaymentId)` |
| `IsVerified` | bit | |
| `Note` | nvarchar(200), nullable | |

### Diferencias: clase `CashCountDifference` + `INV_CashDocumentLines` — `CashDocumentLine` (I3)

`CloseCashSessionCommand` crea el documento en la misma transacción del cierre. Si todas las líneas
están dentro de la tolerancia, se confirma ahí mismo con los motivos dados (sin aprobación); si alguna
la supera, queda en borrador y pasa por la política de su tipo con `Amount` = Σ |diferencia| de las
líneas por encima de la tolerancia; el cajero es participante excluido (nunca aprueba). Rechazar lo
devuelve a borrador para recontar. Al confirmarse emite **un** `DiferenciaDeArqueoAprobada` con todas
sus líneas (también las aceptadas dentro de la tolerancia, F3).

| Columna | Tipo | Regla |
|---|---|---|
| `DocumentId` | int, FK `INV_Documents` | |
| `LineNumber` | smallint | único `(DocumentId, LineNumber)` |
| `CashCountLineId` | int, FK | |
| `PaymentMeansId` | int, FK | |
| `Sign` | smallint | +1 sobrante, −1 faltante |
| `Amount` | 18,2 | > 0 |
| `Treatment` | int (`CashDifferenceTreatment`) | la matriz lo usa como `ReasonCode` (§2.7) |
| `WithinTolerance` | bit | |
| `CashierUserId` | int, FK `SEC_Users` | |
| `CashierPersonId` | int, nullable, FK `COR_People` | obligatorio con `ShortageToCashier` (tercero de la cuenta por cobrar al cajero) |
| `Reason` | nvarchar(300) | |

### `INV_DayCloses` — `DayClose` y `INV_DayCloseLines` — `DayCloseLine` (I3)

Consolidado del día por punto; no emite mensajes. Exige todas las sesiones del punto con esa
`OperatingDate` cerradas (`Inventory.DayClose.SessionsOpen`, `data.sessions`); después no se abren
sesiones con esa fecha en el punto. Se reabre con `Inventory.DayClose.Reopen` y motivo (F12): la fila
queda `Reopened` con sus líneas como evidencia y el siguiente cierre es otra fila con `Version` + 1.

| Columna (`INV_DayCloses`) | Tipo | Regla |
|---|---|---|
| `PointOfSaleId` | int, FK | |
| `OperatingDate` | date | |
| `Version` | smallint | |
| `Status` | int (`DayCloseStatus`: `Closed=1`, `Reopened=2`) | |
| `ClosedAt`, `ClosedByUserId` | datetime, int FK `SEC_Users` | |
| `SessionCount` | smallint | |
| `TotalExpected`, `TotalCounted`, `TotalDifference` | 18,2 | |
| `ReopenedAt`, `ReopenedByUserId`, `ReopenReason` | datetime, int, nvarchar(300), nullable | |

Únicos: `UK_INV_DayCloses_Point_Date_Closed (PointOfSaleId, OperatingDate)` filtrado `[Status] = 1 AND
[IsDeleted] = 0` y `(PointOfSaleId, OperatingDate, Version)`.

| Columna (`INV_DayCloseLines`) | Tipo | Regla |
|---|---|---|
| `DayCloseId` | int, FK | |
| `PaymentMeansId` | int, FK | |
| `CardAcquirerId`, `CardTerminalId` | int, nullable, FK Core | detalle de tarjetas; ambos nulos = total del medio |
| `DetailKey` | nvarchar(40) | `M:{medio}\|A:{adquirente\|-}\|T:{datáfono\|-}`; único `(DayCloseId, DetailKey)` |
| `ExpectedAmount`, `CountedAmount`, `DifferenceAmount` | 18,2 | |
| `PaymentCount` | int | |
| `BatchNumbersJson` | nvarchar(400), nullable | lotes del datáfono en el día |

---

## 16. Medios de pago y pagos

### `COR_PaymentMeans` — `PaymentMeans` (Core, I3)

Catálogo configurable sin programar (FR-096). La **clase** es lo único que el código conoce (vueltas,
crédito, forma de pago DIAN, arqueo por defecto); todo lo demás es dato. `COR_PaymentMethods`
(`sys_forpago`) no se toca ni se usa.

| Columna | Tipo | Regla |
|---|---|---|
| `Code` | nvarchar(10) | `CodigoDeCatalogo`, slug `medios-de-pago`; único filtrado vivos; **no cambia** (dimensión `PaymentMeansCode` de la matriz, T27) |
| `Name` | nvarchar(60) | «Visa Redeban», «Bono Navidad» |
| `DisplayOrder` | smallint | |
| `QuickKey` | nvarchar(3), nullable | tecla rápida en el panel de cobro; única entre activos (regla del comando) |
| `Class` | int (`PaymentMeansClass`: `Cash=1`, `CreditCard=2`, `DebitCard=3`, `AssociateCredit=4`, `CustomerCredit=5`, `BankDeposit=6`, `Transfer=7`, `Voucher=8`, `Check=9`, `Other=99`) | no cambia una vez tiene pagos |
| `CardNetworkId` | int, nullable, FK `COR_CardNetworks` | obligatorio en tarjetas |
| `CardAcquirerId` | int, nullable, FK `COR_CardAcquirers` | obligatorio en tarjetas; el medio admite los datáfonos de su adquirente |
| `BankId` | int, nullable, FK `COR_Banks` | consignación y transferencia |
| `DestinationAccountNumber` | nvarchar(25), nullable | cuenta de la cooperativa, como **dato** (no cuenta contable) |
| `DestinationAccountType` | tinyint, nullable | 1 ahorros, 2 corriente (el código que ya usa `PAY_Employees.PayrollBankAccountType`) |
| `RequiresReference` | bit | |
| `ReferenceKind` | int, nullable (`PaymentReferenceKind`: `Approval=1`, `Receipt=2`, `Deposit=3`, `VoucherNumber=4`, `CheckNumber=5`, `Other=99`) | obligatorio si `RequiresReference` |
| `ReferenceMinLength`, `ReferenceMaxLength` | tinyint, nullable | |
| `AllowsChange` | bit | sólo si `Class = Cash` |
| `AllowsPartial` | bit | |
| `UniqueReference` | bit | un número no se usa dos veces (bonos: verdadero por defecto) → `INV_VoucherRedemptions` |
| `CountMethod` | int (`CashCountMethod`: `PhysicalCount=1`, `VoucherTotal=2`, `ByReference=3`, `None=4`) | por defecto según la clase; combinaciones inválidas se rechazan (un crédito no se cuenta físicamente; el efectivo sí se arquea) |
| `RequiresTerminalBatchAtClose` | bit | el arqueo exige el lote de cada datáfono |
| `ToleranceAmount` | 18,2 | tolerancia de arqueo del medio (FR-012); se **copia** a cada `INV_CashCountLines.ToleranceAmount` al cerrar, así que un cambio sólo afecta las sesiones que cierren después |
| `ExpectedCommissionRate`, `ExpectedCommissionFixed` | 9,6, 18,2, nullable | informativos (FR-101); se copian al pago (`INV_DocumentPayments.ExpectedCommissionAmount`) al confirmar |
| `DianPaymentMeansCode` | nvarchar(3) | dato validado por la contadora (10, 48, 49, 42, 47, 20, 71…; A8) |
| `OfferedAtAllPointsOfSale`, `OfferedInAllChannels`, `OfferedForAllDocumentTypes` | bit | la opción «todos» de cada conjunto |
| `DefaultTermDays`, `MaxTermDays` | smallint, nullable | sólo créditos: plazo propuesto y máximo |
| `DefaultInstallments`, `MaxInstallments` | smallint, nullable | |
| `InstallmentPeriodDays` | smallint, nullable | periodicidad de las cuotas |
| `SuggestedCreditLineCode` | nvarchar(20), nullable | código de línea de Cartera sugerida, como texto (sin llave a `LND_*`, T32) |
| `IsActive` | bit | |
| `ValidFrom`, `ValidTo` | date, date nullable | |
| `Notes` | nvarchar(300), nullable | |

**Invariantes**: modificar un medio exige motivo (`IConMotivo`, FR-012, FR-096); el historial es el
diff de auditoría con ese motivo (cada pago y cada línea de arqueo guardan copia de lo que importa); un
medio con pagos no se borra, se inactiva (`Core.PaymentMeans.InUse`). La forma de pago DIAN (contado o
crédito) se deriva: un documento con algún pago de clase crédito es a crédito.

### `COR_CardNetworks`, `COR_CardAcquirers`, `COR_CardTerminals`, `COR_CashDenominations` (Core, I3)

| Tabla — entidad | Columnas | Unicidad y reglas |
|---|---|---|
| `COR_CardNetworks` — `CardNetwork` | `Code` nvarchar(10), `Name` nvarchar(60), `CardKind` int (`CardKind`: `Credit=1`, `Debit=2`, `Both=3`), `IsActive` | `Code` único filtrado vivos |
| `COR_CardAcquirers` — `CardAcquirer` | `Code` nvarchar(10), `Name` nvarchar(80), `PersonId` int nullable FK `COR_People` (tercero de la cuenta por cobrar a la red, como `COR_Banks.PersonId` en la 009), `IsActive` | `Code` único filtrado vivos |
| `COR_CardTerminals` — `CardTerminal` | `CardAcquirerId` FK, `Code` nvarchar(20) (el TER del voucher), `Serial` nvarchar(40) nullable, `Description` nvarchar(80) nullable, `IsActive` | único `(CardAcquirerId, Code)` filtrado vivos. En pantalla, «Datáfonos de cobro» (no es `DEB_PosTerminals`) |
| `COR_CashDenominations` — `CashDenomination` | `Currency` char(3), `Value` 18,2, `Kind` int (`CashDenominationKind`: `Bill=1`, `Coin=2`), `DisplayOrder` smallint, `IsActive`, `ValidFrom`, `ValidTo` | único `(Currency, Kind, Value)` filtrado vivos; lo siembra `CashDenominationsSeeder` (Order 85) |

### `INV_DocumentPayments` — `DocumentPayment` (I3)

N pagos por documento, reemplazo de las columnas fijas de SOLIDO (FR-097). En borrador de oficina se
editan; el POS los manda con `CheckoutPosDraftCommand` y entran en la transacción que confirma. En un
documento confirmado, inmutables: un medio mal registrado se corrige con un movimiento
`ReclassificationBetweenMeans`.

| Columna | Tipo | Regla |
|---|---|---|
| `DocumentId` | int, FK `INV_Documents` | |
| `LineNumber` | smallint | único `(DocumentId, LineNumber)` filtrado vivos |
| `PaymentMeansId` | int, FK `COR_PaymentMeans` | el mismo medio puede repetirse con referencias distintas |
| `Direction` | int (`PaymentDirection`: `Received=1`, `Refunded=2`) | ventas `Received`; notas y anulaciones `Refunded` |
| `Amount` | 18,2 | > 0, lo aplicado |
| `AmountTendered` | 18,2, nullable | sólo con `AllowsChange` |
| `ChangeGiven` | 18,2, nullable | = entregado − aplicado |
| `Reference` | nvarchar(40), nullable | obligatoria según el medio (`Payments.ReferenceRequired`); se rechaza si parece un número de tarjeta (13 a 19 dígitos: `Payments.CardNumberNotAllowed`) |
| `NormalizedReference` | nvarchar(40), nullable | mayúsculas, sin espacios ni guiones; la usa `INV_VoucherRedemptions` |
| `AuthorizationCode` | nvarchar(20), nullable | aprobación de la red |
| `CardTerminalId` | int, nullable, FK `COR_CardTerminals` | del adquirente del medio; se propone el de la caja |
| `TerminalBatchNumber` | nvarchar(20), nullable | opcional al cobrar (el lote se exige al cerrar si el medio lo pide) |
| `Last4` | char(4), nullable | **nunca** el número completo (`LosPagosNoGuardanElNumeroDeTarjeta`) |
| `CashSessionId` | int, nullable, FK `INV_CashSessions` | obligatorio si el medio se arquea (`CountMethod ≠ None`), también en oficina (T50); la devolución en efectivo sale de la sesión donde se devuelve |
| `MeansCode`, `MeansName` | nvarchar(10), nvarchar(60) | copia |
| `MeansClass` | int (`PaymentMeansClass`) | copia |
| `CardNetworkCode`, `CardAcquirerCode` | nvarchar(10), nullable | copia |
| `CardAcquirerPersonId` | int, nullable | copia del tercero de la cuenta por cobrar a la red |
| `BankId` | int, nullable | copia |
| `DianPaymentMeansCode` | nvarchar(3) | copia |
| `ExpectedCommissionAmount` | 18,2, nullable | `Amount` × tasa + fijo del medio al confirmar (informativo, FR-101) |
| `RefundsPaymentId` | int, nullable, FK self | en una nota: el pago de la venta que se reintegra; por defecto el mismo medio, otro exige `Inventory.Sales.RefundOtherMeans` y queda auditado |
| `CreditTermDays`, `InstallmentCount`, `InstallmentPeriodDays` | smallint, nullable | sólo clases crédito, dentro de los máximos del medio |
| `FirstDueDate`, `FinalDueDate` | date, nullable | |
| `SuggestedCreditLineCode` | nvarchar(20), nullable | copia del medio |
| `PendingValidation` | bit | mientras IC esté pendiente, siempre verdadero en crédito |
| `CreditOrigin` | int, nullable (`CreditOrigin`: `ProvisionalCredit=1`, `LendingNoResponse=2`, `Validated=3`) | el mismo texto que viaja en `VentaACreditoRegistrada` |
| `AccountsReceivableRecordedBy` | nvarchar(12), nullable | copia sellada de `Cartera.CuentaPorCobrarRegistradaPor` (`Contabilidad` forzado mientras IC está pendiente) |
| `ApprovalRequestId` | int, nullable, FK `COR_ApprovalRequests` | la aprobación del crédito provisional (sujeto `ProvisionalCredit`) |

Índices: `(CashSessionId, PaymentMeansId)` (arqueo), `(CardTerminalId, TerminalBatchNumber)` (lotes),
`(PaymentMeansId, NormalizedReference)` filtrado `[NormalizedReference] IS NOT NULL` (búsqueda).
**Reglas del servidor** al confirmar, en una unidad (`ValidadorDePagos`, puro): Σ recibido = `AmountDue`
exacto; vueltas sólo en medios que las admiten y entregado − aplicado = vueltas; referencia según el
medio; crédito sólo con persona identificada y activa (asociado: `IsAssociate` y sin retiro; cliente:
`IsCustomer`), con aprobación de alguien con monto suficiente que no sea el cajero (T32).

### `INV_VoucherRedemptions` — `VoucherRedemption` (I3)

| Columna | Tipo | Regla |
|---|---|---|
| `PaymentMeansId` | int, FK | |
| `NormalizedNumber` | nvarchar(40) | |
| `DocumentPaymentId` | int, FK `INV_DocumentPayments` | |
| `DocumentId` | int, FK `INV_Documents` | la venta que lo usó |
| `Status` | int (`VoucherRedemptionStatus`: `Active=1`, `Released=2`) | |
| `RedeemedAt` | datetime | |
| `ReleasedByDocumentId` | int, nullable, FK | la anulación o la nota con reintegro del bono |
| `ReleasedAt`, `ReleaseReason` | datetime, nvarchar(200), nullable | |

`UK_INV_VoucherRedemptions_Means_Number_Active (PaymentMeansId, NormalizedNumber)` filtrado `[Status] =
1 AND [IsDeleted] = 0`: la base garantiza la unicidad entre cajas (SC-002). Se inserta en la
transacción que confirma la venta; la violación del índice se traduce (molde
`PersonFactory.EsColisionDeDocumento`) a `Payments.VoucherAlreadyUsed` con `data { documentType,
documentNumber, documentPublicId }`. Liberar es un cambio de estado, nunca un borrado; el bono vuelve a
quedar disponible (F11). Sólo para medios con `UniqueReference = 1`.

---

## 17. Catálogo de impuestos y retenciones (Core, I1)

En Core y no en Inventario (T22): lo reutilizan Tesorería, Cartera y Activos, y Contabilidad no se
lee (FR-014). Lo escribe `Core.Taxes.Manage` desde `/maestros/impuestos` o la plantilla
(`ImportTaxCatalogCommand`); lo lee un solo lector, `LectorDeCatalogoTributario`, que arma la foto
`TaxCatalogSnapshot` a una fecha para `MotorTributario`. Ningún valor legal está en el código
(`ElComercioNoTieneValoresLegalesFijos`): todo sale de estas filas, de la UVT (`LectorDeUvt`, T23) y
de los parámetros `TAX`. La foto de lo aplicado en cada documento es `INV_DocumentTaxLines` y la
relación producto → impuesto es `INV_ProductTaxes` (§1).

### `COR_TaxDefinitions` — `TaxDefinition`

| Columna | Tipo | Regla |
|---|---|---|
| `Code` | nvarchar(10) | `CodigoDeCatalogo`; único filtrado vivos |
| `Name` | nvarchar(120) | el largo de la plantilla (contracts/plantillas.md §1; antes decía 80) |
| `Kind` | int (`TaxKind`: `Iva=1`, `Inc=2`, `ReteFuente=3`, `ReteIva=4`, `ReteIca=5`, `Ica=6`, `Other=99`) | `Ica` es informativo (declaración por municipio) |
| `CalculationForm` | int (`TaxCalculationForm`: `PercentOfBase=1`, `PercentOfTax=2`, `AmountPerUnit=3`) | |
| `TaxedOnDefinitionId` | int, nullable, FK self | obligatorio si `PercentOfTax` (ReteIVA sobre el IVA) |
| `IsWithholding` | bit | verdadero en `ReteFuente`, `ReteIva`, `ReteIca`; en `Other`, lo dice quien lo crea |
| `DianTaxCode` | nvarchar(4), nullable | tributo DIAN (01 IVA, 04 INC, 22 bolsas, 05 ReteIVA, 06 ReteFuente, 07 ReteICA, ZZ…); obligatorio en lo que va a un documento electrónico |
| `IsActive` | bit | |
| `Notes` | nvarchar(400), nullable | el largo de la plantilla (antes decía 300) |

### `COR_WithholdingConcepts` — `WithholdingConcept`

| Columna | Tipo | Regla |
|---|---|---|
| `Code` | nvarchar(10) | único filtrado vivos |
| `Name` | nvarchar(120) | compras, servicios, honorarios, arrendamientos, transporte, otros |
| `IsActive` | bit | |
| `Notes` | nvarchar(400), nullable | |

### `COR_TaxRates` — `TaxRate`

Una tarifa con vigencia, sus condiciones y su norma. Una vigencia nueva es una fila nueva con el mismo
`Code` (el patrón de `PAY_LegalParameters`).

| Columna | Tipo | Regla |
|---|---|---|
| `TaxDefinitionId` | int, FK | |
| `Code` | nvarchar(10) | lo que la matriz contable usa como `TaxRateCode` (T27); **no cambia** |
| `Name` | nvarchar(120) | «IVA 19 %», «ReteFuente compras declarante» |
| `Rate` | 9,6, nullable | **fracción** (0,19; 0,00966 para 9,66 ‰); obligatorio salvo `AmountPerUnit` |
| `AmountPerUnit` | 18,2, nullable | valor por unidad gravable; la unidad gravable por unidad base del producto la da `INV_ProductTaxes.TaxableUnitsPerBaseUnit` |
| `WithholdingConceptId` | int, nullable, FK | obligatorio en `ReteFuente` |
| `MunicipalityDaneCode` | nvarchar(5), nullable | DIVIPOLA; obligatorio en `Ica` y `ReteIca` |
| `ActivityCode` | nvarchar(6), nullable | CIIU, o `*` = tarifa general del municipio |
| `MinimumBaseUvt` | 18,4, nullable | base mínima en UVT |
| `MinimumBasePesos` | 18,2, nullable | sólo donde el municipio la fija en pesos; excluyente con la de UVT |
| `SubjectPersonType` | nvarchar(2), nullable | condición sobre quien soporta el impuesto o la retención: `01` natural, `02` jurídica (el código de `COR_People.PersonType`); nula = cualquiera |
| `SubjectIsIncomeTaxFiler`, `SubjectIsVatResponsible`, `SubjectIsLargeContributor`, `SubjectIsSelfWithholder`, `SubjectIsSimpleTaxRegime` | bit, nullable | condiciones tipadas sobre el sujeto; nula = no importa |
| `AgentIsLargeContributor`, `AgentIsVatWithholdingAgent` | bit, nullable | condiciones sobre quien retiene |
| `AppliesTo` | int (`TaxAppliesTo`: `Purchases=1`, `Sales=2`, `Both=3`) | |
| `Priority` | smallint | desempata candidatas |
| `ValidFrom` | date | |
| `ValidTo` | date, nullable | |
| `LegalSource` | nvarchar(200) | la norma (FR-013) |
| `ReviewPending` | bit NOT NULL DEFAULT 0 | la semilla la deja en 1: «pendiente de validar por la contadora» (A8); la baja quien tiene `Core.Taxes.Manage`, auditado |
| `Notes` | nvarchar(400), nullable | |

Único `(Code, ValidFrom)` filtrado vivos; dos vigencias del mismo `Code` no se cruzan
(`Core.TaxRate.Overlaps`). **Una tarifa no se edita desde que entra en vigencia**: tarifa, valor por
unidad, bases, condiciones y alcance quedan fijos (`Core.TaxRate.InEffect`); se cierra su `ValidTo` y se
crea otra. Así Core no necesita leer los documentos para saber si una tarifa «ya se usó», y la foto de
cada documento sigue valiendo. El sujeto exento (`COR_People.WithholdingExempt`,
`IcaWithholdingExempt`) lo resuelve el motor, no una condición de tarifa.

**Selección** (en `MotorTributario`): candidatas = tarifas vigentes a la fecha del documento de las
definiciones del producto o del concepto, con `AppliesTo` compatible con la perspectiva, municipio y
actividad exactos o, si no hay, la fila `*` del municipio, y cuyas condiciones no nulas se cumplen con
`PerfilTributario` de las dos partes. Gana la de mayor `Priority`; a igual prioridad, la de más
condiciones no nulas; si aún empatan, el documento no se confirma (`Core.TaxRate.Ambiguous`, que nombra
las dos). Guardar una tarifa ya rechaza el empate evidente (misma definición, concepto, municipio,
actividad, condiciones y prioridad con vigencias cruzadas). El empate evidente sólo se mira en las definiciones de retención (el motor
elige esas tarifas por condiciones; los impuestos del producto se citan por código en `INV_ProductTaxes`, así que IVA 19 e
IVA 5 conviven sin empate). Un código de tarifa pertenece a una sola definición. Una retención procede con base **≥** el
mínimo convertido a pesos con `Tributario.RedondeoUvtAPesos`; las notas usan la foto del original y no
vuelven a probar el mínimo.

---

## 18. Facturación electrónica DIAN (plataforma, I4)

Todo vive en la base de la cooperativa (Principio IV) y en `COR_`, porque es de plataforma
(`ElectronicInvoicing`) y la usarán otros emisores. La plataforma **no lee tablas `INV_`**: el
contenido le llega por `IFuenteDeDocumentoElectronico` (Inventario lo implementa con
`FuenteDeEmisionDeInventario`). En factura, **un número rechazado se reusa** (casos a y b); en la
nómina electrónica de la 010, no: no se copia su modelo.

### `COR_ElectronicEmissionSettings` — `ElectronicEmissionSetting`

Configuración de emisión por cooperativa, con vigencia sin cruces. `ConfigureEmissionCommand` crea la
fila nueva y cierra la anterior la víspera, con motivo.

| Columna | Tipo | Regla |
|---|---|---|
| `Mode` | int (`EmissionMode`: `TechnologyProvider=1`, `OwnSoftware=2`) | |
| `ChannelCode` | nvarchar(40) | clave del adaptador (`SIMULADO`, el del proveedor, `SERVICIO-CENTRAL`), en mayúsculas, la de `ICanalDeEmisionElectronica.ChannelCode` |
| `Environment` | int (`DianEnvironment`: `Production=1`, `Testing=2`) | trasladado a `Domain/Enums/Dian` con los mismos valores |
| `SoftwareId` | nvarchar(36), nullable | modo propio |
| `TestSetId` | nvarchar(36), nullable | set de pruebas |
| `TestSetAcceptedAt` | datetime, nullable | en producción, sin él la guardia bloquea (modo propio) |
| `CredentialKey` | nvarchar(120), nullable, **[NoAuditar]** | sólo el **nombre** de la clave del Secret `erp-fe-credenciales` (`{tenantPublicId}.{channelCode}.json`); si no coincide con la ruta derivada de la cooperativa resuelta: `ElectronicInvoicing.CredentialMismatch` |
| `CredentialVerifiedAt` | datetime, nullable | lo escribe `VerifyChannelCredentialCommand` |
| `EmailDeliveryBy` | int (`EmailDeliveryBy`: `Erp=1`, `Channel=2`) | `Channel` sólo si `CapacidadesDelCanal` lo ofrece (ver «Dudas») |
| `IssuerTaxId`, `IssuerCheckDigit` | nvarchar(15), nvarchar(1) | se proponen desde `COR_Companies` y se confirman aquí |
| `IssuerBusinessName` | nvarchar(200) | |
| `IssuerAddress` | nvarchar(150) | |
| `IssuerMunicipalityDaneCode` | nvarchar(5) | DIVIPOLA |
| `IssuerEmail` | nvarchar(150) | |
| `IsEnabled` | bit | |
| `ValidFrom`, `ValidTo` | date, date nullable | |
| `Reason` | nvarchar(300) | |

Único `(ValidFrom)` filtrado vivos; sin cruces (`ElectronicInvoicing.Settings.Overlaps`). El canal nuevo
sólo se admite si hay, para cada tipo usado, una resolución vigente asociada a ese canal o software.
Ningún token, usuario, contraseña, certificado ni PIN se guarda aquí ni en ninguna tabla
(`LasCredencialesDeFacturacionNoTocanLaBase`). Las responsabilidades tributarias del emisor salen de los
parámetros `TAX` (T24).

### `COR_DianNumberingResolutions` — `DianNumberingResolution`

| Columna | Tipo | Regla |
|---|---|---|
| `Kind` | int (`ResolutionKind`: `Invoice=1`, `PosEquivalent=2`, `SupportDocument=3`, `Contingency=4`) | |
| `BacksUpKind` | int, nullable (`ResolutionKind`) | sólo `Contingency`: a qué tipo respalda |
| `ResolutionNumber` | nvarchar(30) | |
| `ResolutionDate` | date | |
| `Prefix` | nvarchar(4) | alfanumérico (Res. 165 art. 11 num. 4) |
| `RangeFrom`, `RangeTo` | bigint | |
| `ValidFrom`, `ValidTo` | date | |
| `Environment` | int (`DianEnvironment`) | los de pruebas no valen en producción |
| `LastIssuedNumber` | bigint | nace en `RangeFrom − 1`; **no** se llama `NextNumber` (T16); lo incrementa sólo `NumeradorFiscal`, bajo el cerrojo, como última fila del orden canónico |
| `IsActive` | bit | |
| `Notes` | nvarchar(300), nullable | |

Único `(Environment, Prefix, ResolutionNumber)` filtrado vivos; índice `(Kind, Environment, Prefix,
ValidFrom)`. **Reglas**: dos resoluciones del mismo `(Environment, Prefix)` no se cruzan en vigencia ni
en rango (`ElectronicInvoicing.Resolution.Overlaps`); nunca se numera fuera de una resolución vigente y
asociada al canal sellado (`ElectronicInvoicing.Resolution.Exhausted` / `.Expired`,
`Inventory.Numbering.ResolutionUnavailable`); avisos `Dian.ResolucionPorAgotar` (consumo ≥
`Dian.AvisoResolucionPorcentaje`) y `Dian.ResolucionPorVencer` (faltan `Dian.AvisoResolucionDias`). El
tipo de documento de Inventario declara su prefijo sin FK a la resolución (T16); un prefijo de notas
(que numeran en `INV_DocumentSequences`) no puede coincidir con el de una resolución, porque comparten
la unicidad de `COR_ElectronicDocuments`.

### `COR_DianResolutionChannels` — `DianResolutionChannel`

Asociación prefijo ↔ canal o software desde una fecha (FR-064, FR-065).

| Columna | Tipo | Regla |
|---|---|---|
| `ResolutionId` | int, FK | |
| `ChannelCode` | nvarchar(40) | |
| `SoftwareId` | nvarchar(36), nullable | |
| `ValidFrom`, `ValidTo` | date, date nullable | una sola asociación vigente por resolución |
| `TechnicalKey` | nvarchar(100), nullable, **[NoAuditar]** | sólo en resoluciones `Invoice`; se responde enmascarada (últimos 4) |

Único `(ResolutionId, ValidFrom)` filtrado vivos; sin cruces por resolución.

### `COR_ElectronicDocuments` — `ElectronicDocument`

**Uno por número fiscal**. La identidad es el número: el rechazado y su reemplazo (caso b) comparten la
fila; las versiones van aparte.

| Columna | Tipo | Regla |
|---|---|---|
| `SourceModule` | nvarchar(3) | `INV` |
| `SourceDocumentPublicId` | uniqueidentifier | el documento comercial **vigente**; en el caso b pasa al reemplazo (el original queda en la versión 1) |
| `SourceDocumentTypeCode` | nvarchar(10) | para la bandeja sin leer `INV_` |
| `Kind` | int (`ElectronicDocumentKind`: `Invoice=1`, `CreditNote=2`, `DebitNote=3`, `PosEquivalent=4`, `PosAdjustmentNote=5`, `SupportDocument=6`, `SupportDocumentAdjustmentNote=7`, `RadianEvent030=8`, `RadianEvent032=9`) | |
| `DianDocumentTypeCode` | nvarchar(2) | 01, 03, 04, 05, 20, 91, 92, 94, 95, 96 (los del DEE se cotejan con el anexo 1.0) |
| `ResolutionId` | int, nullable, FK | nulo en notas y eventos |
| `Prefix` | nvarchar(10) | |
| `Consecutive` | bigint | |
| `Number` | nvarchar(20) | prefijo + consecutivo |
| `Environment` | int (`DianEnvironment`) | |
| `EmissionSettingId` | int, FK | **sellado** al numerar |
| `Mode` | int (`EmissionMode`) | sellado |
| `ChannelCode` | nvarchar(40) | sellado: reintentos, casos a y b y transmisión de contingencia salen por aquí (FR-064) |
| `SoftwareId` | nvarchar(36), nullable | sellado |
| `IssuedAt` | datetime | instante de expedición (se transmite con −05:00) |
| `IssueDate` | date | fecha local |
| `CounterpartyTaxId`, `CounterpartyName` | nvarchar(20), nvarchar(200), nullable | copia de la contraparte para la bandeja; cambia sólo con el caso a |
| `TotalAmount` | 18,2 | |
| `UniqueCode` | nvarchar(96), nullable | CUFE, CUDE o CUDS |
| `UniqueCodeKind` | int, nullable (`UniqueCodeKind`: `Cufe=1`, `Cude=2`, `Cuds=3`) | |
| `QrContent` | nvarchar(1000), nullable | tal como lo devuelve el canal |
| `Status` | int (`ElectronicDocumentStatus`: `Pending=0`, `Sent=1`, `Validated=2`, `ValidatedWithNotices=3`, `Rejected=4`, `IssuerContingency=5`, `DianContingency=6`, `CancelledWithoutReplacement=7`) | |
| `ContingencyType` | int, nullable (`ContingencyType`: `Issuer03=3`, `Dian04=4`) | queda aunque después se valide |
| `ContingencyEventId` | int, nullable, FK `COR_DianContingencyEvents` | |
| `SentAt`, `ValidatedAt`, `DeliveredAt` | datetime, nullable | |
| `EmailDeliveryBy` | int (`EmailDeliveryBy`) | sellado de la configuración |
| `EmailSentAt` | datetime, nullable | |
| `CurrentVersionId` | int, nullable, FK `COR_ElectronicDocumentVersions` | |
| `CorrectsDocumentId` | int, nullable, FK self | notas y notas de ajuste: el documento que corrigen |
| `RejectedBy` | int, nullable (`RejectedBy`: `Dian=1`, `Channel=2`) | |
| `RejectionReason` | nvarchar(500), nullable | motivo del caso c |
| `CancelledByUserId` | int, nullable, FK `SEC_Users` | responsable del caso c |
| `AttemptCount` | int | **[NoAuditar]** |
| `NextAttemptAt` | datetime, nullable | **[NoAuditar]**; espera 15 s, 1, 2, 5, 15, 30, 60 min y luego cada hora hasta el plazo |
| `LeaseUntil`, `LeaseOwner` | datetime, nvarchar(100), nullable | **[NoAuditar]**; arrendamiento por fila: el intento en línea del POS y `ProcesadorDeDocumentosElectronicos` no toman el mismo documento |
| `TransmissionDeadline` | datetime, nullable | contingencias: cierre del evento + `Dian.PlazoContingenciaHoras`, contado según el tipo (`PlazoDeContingencia`) |
| `LastOutcome` | int, nullable (`ChannelOutcome`) | |
| `LastMessagesJson` | nvarchar(max), nullable | mensajes traducidos del último intento, para la pantalla |

Índices: único `(Environment, Prefix, Consecutive)` **sin filtro** (un número nunca se duplica, ni
contra filas dadas de baja: estas filas no se borran); único `(SourceModule, SourceDocumentPublicId,
Kind)`; `(Status, NextAttemptAt)`; `(UniqueCode)` filtrado no nulo; `(IssueDate)`;
`(ContingencyEventId)`. **Invariantes**: nunca `Validated` sin código único **y** respuesta de
validación; un `Sent` no se corrige ni se anula hasta tener respuesta; un documento numerado con la
numeración normal nunca se renumera a contingencia. Las columnas de reintento y arrendamiento van con
`[NoAuditar]` no por secretas sino por ruido: su historia es `COR_ElectronicDocumentTransmissions`.

**Transiciones** (`TransicionesDelDocumentoElectronico`, pura, con prueba de todas las prohibidas):

| Desde | Operación | Hacia |
|---|---|---|
| — | confirmar el documento comercial | `Pending`; `IssuerContingency` si hay contingencia 03 abierta (numera con la resolución `Contingency`) |
| `Pending` | `Emit` → resultado del canal | `Validated`, `ValidatedWithNotices`, `Rejected`, `Sent` (ambiguo o asíncrono), `DianContingency` (el canal dijo `DianUnavailable`); `ChannelUnavailable` deja `Pending` con la siguiente espera |
| `Sent` | `QueryStatus` | cualquiera de los finales; `NotFound` → `Pending` y se reenvía la **misma** versión con el **mismo** número |
| `IssuerContingency`, `DianContingency` | `TransmitContingency` | `Sent`, `Validated`, `ValidatedWithNotices`, `Rejected` |
| `Rejected` (confirmado) | caso a (`CorrectRejectedDocumentCommand`) o b (`ReplaceRejectedDocumentCommand`) | `Pending` con versión n + 1 |
| `Rejected` (confirmado) | caso c (`CancelRejectedDocumentCommand`) | `CancelledWithoutReplacement` (final; el número queda ligado al documento anulado y no es hueco) |

### `COR_ElectronicDocumentVersions` — `ElectronicDocumentVersion` (H)

| Columna | Tipo | Regla |
|---|---|---|
| `ElectronicDocumentId` | int, FK | |
| `VersionNumber` | smallint | único `(ElectronicDocumentId, VersionNumber)` |
| `SourceDocumentPublicId` | uniqueidentifier | el documento comercial de esta versión |
| `Reason` | int (`DocumentVersionReason`: `Initial=1`, `CaseA=2`, `CaseB=3`) | |
| `CanonicalSchemaVersion` | smallint | 1 (`DocumentoElectronicoCanonico` v1) |
| `CanonicalSha256` | char(64) | huella del canónico construido en la transacción de confirmación |
| `EconomicFingerprint` | char(64) | huella económica (tercero, líneas, bases, impuestos, retenciones, totales) que usa `ReglaDeCorreccionFiscal` para separar el caso a del b |
| `CorrectionReason` | nvarchar(500), nullable | casos a y b |
| `ChangedFieldsJson` | nvarchar(max), nullable | caso a: antes y después de lo que cambió (incluida la copia fiscal, T52) |
| `CanonicalAttachmentPublicId` | uniqueidentifier, nullable | **escritura única** |
| `SignedXmlAttachmentPublicId` | uniqueidentifier, nullable | **escritura única** |
| `AttachedDocumentAttachmentPublicId` | uniqueidentifier, nullable | **escritura única** |
| `GraphicPdfAttachmentPublicId` | uniqueidentifier, nullable | **escritura única**; representación del ERP (`RepresentacionGraficaReport`, carta y 80 mm) |

Los artefactos llegan **después** del commit (T40: el canónico se sube tras confirmar; el XML firmado y
el AttachedDocument, con la respuesta del canal; el PDF, al validarse). Por eso las cuatro columnas de
adjunto son de **escritura única**: la guarda de `IHechoInmutable` acepta un `Modified` sólo si toda
propiedad modificada lleva `[EscrituraUnica]` y su valor original es nulo; cualquier otro cambio o
borrado se rechaza. Los adjuntos son de dueño `ElectronicSalesDocument` o `ElectronicPurchaseDocument`
(T41, no borrables, sin subidas) con `OwnerEntityPublicId` = el `PublicId` del documento electrónico.

### `COR_ElectronicDocumentTransmissions` — `ElectronicDocumentTransmission` (H)

Un intento contra el canal. Se inserta en la transacción que registra el resultado, después de la
llamada (la llamada nunca va dentro de la transacción de confirmación).

| Columna | Tipo | Regla |
|---|---|---|
| `ElectronicDocumentId` | int, FK | |
| `VersionId` | int, FK | |
| `AttemptNumber` | int | único `(ElectronicDocumentId, AttemptNumber)` |
| `Operation` | int (`TransmissionOperation`: `Emit=1`, `TransmitContingency=2`, `QueryStatus=3`, `Event=4`, `Download=5`) | |
| `ChannelCode` | nvarchar(40) | |
| `RequestedAt`, `CompletedAt` | datetime | |
| `DurationMs` | int | |
| `RequestedByKind` | int (`ActorKind`: `Person=1`, `Process=2`) | |
| `RequestedByUserId` | int, nullable, FK `SEC_Users` | |
| `RequestedByName` | nvarchar(150) | «Proceso de integración» o la persona |
| `IdempotencyKey` | nvarchar(120) | `{tenantPublicId}:{ambiente}:{prefijo}{consecutivo}:v{versión}` |
| `RequestSha256` | char(64) | |
| `HttpStatus` | smallint, nullable | |
| `Outcome` | int (`ChannelOutcome`: `Validated=1`, `ValidatedWithNotices=2`, `Rejected=3`, `InProcess=4`, `NotFound=5`, `DianUnavailable=6`, `ChannelUnavailable=7`, `InvalidData=8`) | |
| `ProviderCode` | nvarchar(20), nullable | |
| `DianStatusCode` | nvarchar(10), nullable | |
| `IsValid` | bit, nullable | |
| `RawMessagesJson`, `TranslatedMessagesJson` | nvarchar(max), nullable | `[{ regla, tipo, texto, traducción }]` |
| `ExternalReference` | nvarchar(100), nullable | trackId, zipKey o el identificador del proveedor |
| `ApplicationResponseAttachmentPublicId` | uniqueidentifier, nullable | |
| `CorrelationId` | nvarchar(64), nullable | |

Si el proceso cae entre la llamada y este registro, el siguiente intento **consulta** antes de
reenviar; reenviar la misma versión es seguro porque el número es la clave de idempotencia (el canal
traduce «ya existe» a una consulta, nunca a un error).

### `COR_DianContingencyEvents` — `DianContingencyEvent`

| Columna | Tipo | Regla |
|---|---|---|
| `Type` | int (`ContingencyType`) | `Dian04` sólo lo declara el canal; `Issuer03` el circuito (`Dian.UmbralFallasCircuito`) o una persona con `ElectronicInvoicing.Contingencies.Declare` |
| `ChannelCode` | nvarchar(40) | |
| `StartedAt`, `EndedAt` | datetime, datetime nullable | |
| `DetectedByKind` | int (`ActorKind`) | |
| `DetectedByUserId` | int, nullable, FK `SEC_Users` | |
| `DetectedByName` | nvarchar(150) | |
| `Reason` | nvarchar(500) | |
| `Status` | int (`ContingencyEventStatus`: `Open=1`, `Closed=2`) | |
| `DeadlineHoursApplied` | smallint | copia de `Dian.PlazoContingenciaHoras` a la fecha del cierre |
| `LegalSource` | nvarchar(200) | copia de la norma del parámetro |
| `DeadlineAt` | datetime, nullable | cierre + plazo (el de cada documento lo lleva su `TransmissionDeadline`) |
| `DeclaredToDianAt` | datetime, nullable | constancia presentada |
| `DeclaredToDianReference` | nvarchar(60), nullable | |
| `ClosedByKind`, `ClosedByUserId`, `ClosedByName` | int (`ActorKind`), int, nvarchar(150), nullable | |
| `CloseReason` | nvarchar(300), nullable | |

Único `(Type, ChannelCode)` filtrado `[Status] = 1 AND [IsDeleted] = 0`: un solo evento abierto por
tipo y canal; los documentos se unen al abierto. El conteo del circuito (`CircuitoDeCanal`) vive en
memoria del procesador (por cooperativa y canal) y **cuenta como falla también la espera del POS que se
cumple** (`Dian.EsperaMaximaPosSegundos` sin respuesta): pasado `Dian.UmbralFallasCircuito`, las ventas
nuevas abren la contingencia 03 y reciben su representación en papel de inmediato (SC-004). Lo que queda
en la base es el evento. Evidencias: adjuntos de dueño
`DianContingencyEvent` (ver §26).

### Eventos RADIAN y lo que no es tabla

- Desde I1, el estado del 030 y el 032 de cada factura de proveedor a crédito vive en
  `INV_SupplierInvoiceEvents` (§9, T42). Desde I5 la **emisión** es una fila de
  `COR_ElectronicDocuments` con `Kind = RadianEvent030 | RadianEvent032`, `DianDocumentTypeCode = 96`,
  sin resolución, `SourceDocumentPublicId` = el registro de la factura del proveedor, y el enlace de
  vuelta es `INV_SupplierInvoiceEvents.ElectronicDocumentPublicId`.
- «Obligada a facturar electrónicamente» es el parámetro `Dian.ObligadaAFacturar` (`COR_ParameterVersions`,
  §4); la decisión la toma sólo `GuardiaDeEmisionFiscal.Evaluar` (`Electronic` / `NonElectronic` /
  `Blocked` con motivos) y no se guarda: la consultan la confirmación, la apertura de caja y
  `GET /api/electronic-invoicing/readiness`.
- Credenciales: fuera de la base (Secret `erp-fe-credenciales`, T40). Catálogos DIAN: JSON embebidos y
  versionados (`CatalogoDian`), no tablas.

---

## 19. Mensajería e idempotencia (plataforma)

Bandeja de salida (T7) en la base de cada cooperativa, escrita por `EmisorDeMensajes` **dentro** del
`SaveChanges` del documento, y leída por `DespachadorDeMensajes`. El arrendamiento del despachador es
`COR_BackgroundLeases` (plataforma de ejecución, §0.1). Contabilidad puede leer estas tablas;
Inventario no lee las de Contabilidad (T31).

### `COR_IntegrationMessages` — `IntegrationMessage` (H, `AuditableEntityLong`, I1)

| Columna | Tipo | Regla |
|---|---|---|
| `Id` | bigint identity | **es el orden**: un relacionado sólo se confirma después de su original; nunca se ordena por Guid. No sale del módulo ni aparece en JSON (Principio VI): la bandeja devuelve los mensajes en este orden, sin exponerlo |
| `PublicId` | uniqueidentifier | = `MessageId`; único |
| `Type` | nvarchar(60) | `VentaFacturada`, `CostoDeVentaReconocido`… (§2.6) |
| `Version` | smallint | 1 |
| `Kind` | int (`IntegrationMessageKind`: `Business=1`, `Informational=2`) | |
| `OriginModule` | nvarchar(3) | `INV` |
| `OriginKind` | int (`MessageOriginKind`: `Document=1`, `Operation=2`) | operación = cierre, reapertura, reclasificación |
| `OriginPublicId` | uniqueidentifier | el documento o la operación |
| `OriginDocumentClass` | nvarchar(40), nullable | el nombre del `DocumentClass` como texto: la plataforma no depende de los tipos de Inventario y mañana emiten otros módulos |
| `OriginDocumentTypeCode` | nvarchar(10), nullable | |
| `OriginNumber` | nvarchar(30), nullable | |
| `OriginEventKey` | nvarchar(80) | `Confirmation` (todo mensaje de un documento); `Confirmation:{affectedDocumentPublicId:N}` (cada parte de `AjusteDeCostoReconocido`); `Confirmation:{paymentPublicId:N}` (`VentaACreditoRegistrada` y `AjusteDeVentaACredito`); `Close:{closingVersion}` · `Reopen:{closingVersion}`; `Reclassification` — ver contracts/mensajes.md §10.1 |
| `FiscalUniqueCode` | nvarchar(96), nullable | CUFE, CUDE o CUDS si el origen es fiscal y ya lo tiene |
| `RelatedPublicId` | uniqueidentifier, nullable | anulado, devuelto o corregido |
| `RelatedDocumentClass` | nvarchar(40), nullable | ídem |
| `RelatedNumber` | nvarchar(30), nullable | |
| `ChainRootPublicId` | uniqueidentifier | raíz de la cadena del documento |
| `OperationDate` | date | la del documento: es la fecha del comprobante |
| `BranchPublicId` | uniqueidentifier | sucursal contable |
| `CostCenterPublicId` | uniqueidentifier, nullable | |
| `WarehouseCode` | nvarchar(10), nullable | |
| `PersonPublicId` | uniqueidentifier, nullable | tercero; va siempre por si D-02 exige orden por persona (H1) |
| `Currency` | char(3) | `COP` |
| `ExchangeRate` | 18,6 | 1 |
| `PayloadJson` | nvarchar(max) / text | el record `<Type>V1` serializado (camelCase, enums como texto, decimales exactos, tarifas como fracción); texto y no `jsonb`, por paridad |
| `PayloadSha256` | char(64) | detecta alteraciones |
| `PrevalidationOutcome` | int, nullable (`PrevalidationOutcome`: `Postable=1`, `NoResponse=2`, `NotApplicable=3`) | sellado para medir SC-021 |
| `OriginUserCentralId` | uniqueidentifier | usuario que originó: dato, nunca actor |
| `OriginUserName` | nvarchar(150) | |
| `EmittedAt` | datetime | |

Índices: único `PublicId`; único `(OriginPublicId, Type, OriginEventKey)` (defensa contra doble
emisión); `(ChainRootPublicId, Id)`; `(OperationDate)`; `(Type, OperationDate)`; `(PersonPublicId)`
filtrado no nulo. La inmutabilidad la cumplen tres cosas: la guarda de `IHechoInmutable`,
`PrincipioXI_ContableImmutable` sobre `Entities/Integration/Transactions` y `PayloadSha256`.

### `COR_IntegrationMessageDependencies` — `IntegrationMessageDependency` (H, I1)

| Columna | Tipo | Regla |
|---|---|---|
| `MessageId` | bigint, FK | |
| `DependsOnMessageId` | bigint, FK | el último mensaje de cada cadena de la que depende (su documento, el original que anula o corrige, los orígenes de un derivado, cada documento afectado por un ajuste de costo) |

Único `(MessageId, DependsOnMessageId)`; índice `(DependsOnMessageId)`. Siempre `DependsOnMessageId <
MessageId` (prueba: el orden causal es el del `Id`).

### `COR_IntegrationMessageDeliveries` — `IntegrationMessageDelivery` (I1, [SinDiff])

Estado por destino, mutable con `RowVersion`. Nace en la misma transacción que el mensaje.

| Columna | Tipo | Regla |
|---|---|---|
| `MessageId` | bigint, FK | |
| `Destination` | nvarchar(20) | `IntegrationDestinations.Accounting` = `Accounting`, `.Lending` = `Lending` |
| `Mode` | int (`DeliveryMode`: `Online=1`, `Batch=2`, `NotPosted=3`, `Always=4`) | sellado; relacionados y derivados lo copian de la entrega de su original (T9) |
| `Status` | int (`DeliveryStatus`: `Pending=0`, `InBatch=1`, `Processed=2`, `Rejected=3`, `NotApplicable=4`, `ValidationFailed=5`) | |
| `ScheduleKey` | nvarchar(60), nullable | `{DocumentTypeCode raíz}\|{DisparadorDeLote}\|{HoraDeLote}\|{Granularidad}`, sellados al confirmar (el tipo es el del original de la cadena) |
| `BatchScopeKey` | nvarchar(60), nullable | `CashSession:{publicId}` (`CierreDeTurno`) · `Period:{aaaa-mm}` (`CierreDePeriodo`) |
| `BatchId` | int, nullable, FK `COR_IntegrationBatches` | la columna nace en I1; la FK entra con la tabla de lotes en I2 |
| `Attempts` | int | |
| `NextAttemptAt` | datetime, nullable | `min(15 s·2^(n−1), 15 min)` + jitter |
| `LastAttemptAt` | datetime, nullable | |
| `LastErrorCode` | nvarchar(80), nullable | |
| `LastErrorMessage` | nvarchar(1000), nullable | |
| `LastErrorDataJson` | nvarchar(max), nullable | `data.errors[] { lineNumber, account, rule, whoFixes }` tal como lo respondió el destino |
| `ProcessedAt` | datetime, nullable | |
| `ResultReference` | nvarchar(100), nullable | `PublicId` del comprobante, o «sin comprobante (valor cero)» / «informativo» |
| `ResultVoucherTypeCode` | nvarchar(10), nullable | tipo del comprobante generado, tal como lo devolvió el destino en `ResultadoDeConsumo.Processed`; lo escribe `RegisterDeliveryResultCommand` |
| `ResultVoucherNumber` | nvarchar(30), nullable | número del comprobante, ídem. La bandeja de Inventario lee `result` **sólo** de esta fila: no consulta tablas `ACC_` (FR-014) |

Único `(MessageId, Destination)`; `(Destination, NextAttemptAt, MessageId)` filtrado `[Status] = 0`
(elegibles); `(ScheduleKey, MessageId)` filtrado `[Status] = 1`; `(BatchId)`.

Estados de la entrega:

| Desde | Evento | Hacia |
|---|---|---|
| — (emisión) | Contabilidad `Online`; informativos y Cartera (`Always`) | `Pending` |
| — (emisión) | Contabilidad `Batch` | `InBatch` (con `ScheduleKey`, sin `BatchId`) |
| — (emisión) | Contabilidad `NotPosted` | `NotApplicable` |
| `InBatch` | el lote la toma (`BatchId` asignado) y el destino responde | `Processed` \| `Rejected`; ante `Retry` sigue `InBatch` con el mismo `BatchId` y su espera, y el lote sigue `Running` hasta que no le quede ninguna |
| `Pending` | consumidor `Processed` / `AlreadyProcessed` | `Processed` |
| `Pending` | consumidor `Retry` | `Pending` con la siguiente espera (alerta `Integracion.MensajeSinEntregar` a los 3 intentos o 15 min) |
| `Pending` | consumidor `Rejected` | `Rejected` (alerta `Integracion.MensajeRechazado`) |
| `Rejected` | `ReprocessMessagesCommand` (persona, motivo) | `InBatch` (con el `BatchId` del lote `Reprocess`), con sus dependientes bloqueados |
| `NotApplicable` | `SendNotApplicableMessagesCommand` (FR-078) | `InBatch` (lote `SendNotApplicable`), en bloque sobre la clausura de dependencias |
| `Processed` (Lending, con IC) | validación negativa (Cartera valida después de procesar) | `ValidationFailed` (alerta `Integracion.ValidacionFallida`) |

Elegible = `Pending`, `NextAttemptAt` vencido y ninguna dependencia con entrega **al mismo destino** en
{`Pending`, `InBatch`, `Rejected`}. Un destino sin consumidor registrado (Cartera mientras IC esté
pendiente o `Cartera.IntegracionHabilitadaDesde` vacío) se salta sin intentos ni alertas: la bandeja
dice «Pendiente — destino aún no disponible (IC)». No hay estado «en proceso» persistido.

### `COR_IntegrationDeliveryAttempts` — `IntegrationDeliveryAttempt` (H, `AuditableEntityLong`, I2, [SinDiff])

| Columna | Tipo | Regla |
|---|---|---|
| `DeliveryId` | int, FK | |
| `MessageId` | bigint | desnormalizado |
| `AttemptNumber` | int | único `(DeliveryId, AttemptNumber)` |
| `StartedAt`, `FinishedAt` | datetime | |
| `DurationMs` | int | |
| `Outcome` | int (`DeliveryAttemptOutcome`: `Processed=1`, `AlreadyProcessed=2`, `Rejected=3`, `Retry=4`) | el `ResultadoDeConsumo` del destino |
| `ErrorCode`, `ErrorMessage` | nvarchar(80), nvarchar(1000), nullable | |
| `ActorKind` | int (`ActorKind`) | proceso, o la persona que ordenó el lote o el reproceso |
| `ActorUserId` | int, nullable, FK `SEC_Users` | |
| `ActorName` | nvarchar(150) | |
| `BatchId` | int, nullable, FK | |
| `Instance` | nvarchar(100) | réplica que lo procesó |

### `COR_IntegrationBatches` — `IntegrationBatch` y `COR_IntegrationBatchCounters` — `IntegrationBatchCounter` (I2)

El lote es de plataforma (T12) para que Inventario lo muestre sin leer `ACC_`; Contabilidad planea sus
grupos. Es a la vez **orden** (lote manual, reproceso, envío posterior) y **registro**.

| Columna (`COR_IntegrationBatches`) | Tipo | Regla |
|---|---|---|
| `Number` | bigint | de `COR_IntegrationBatchCounters.NextValue`; único |
| `Destination` | nvarchar(20) | |
| `Trigger` | int (`BatchTrigger`: `Scheduled=1`, `CashSessionClose=2`, `PeriodClose=3`, `Manual=4`, `Reprocess=5`, `SendNotApplicable=6`) | |
| `ScheduleKey` | nvarchar(60), nullable | |
| `ScheduledFor` | datetime, nullable | hora **local** de Colombia de la franja (`Contabilidad.HoraDeLote`) |
| `CashSessionPublicId` | uniqueidentifier, nullable | `CashSessionClose` |
| `PeriodYear`, `PeriodMonth` | smallint, tinyint, nullable | `PeriodClose` |
| `CutoffMessageId` | bigint, nullable | `Manual` y `SendNotApplicable`: se procesa exactamente lo que mostró la vista previa. Es interno: la petición trae `cutoffMessagePublicId` (el `PublicId` del último mensaje listado) y el comando lo resuelve a este `Id` |
| `DateFrom`, `DateTo` | date, nullable | rango de fechas de operación |
| `Granularity` | int, nullable (`PostingGranularity`: `PerDocument=1`, `Summarized=2`) | sellada al crear, con el parámetro `Contabilidad.Granularidad` del tipo de la `ScheduleKey`; nula en un lote manual que mezcla tipos (cada grupo usa la de su tipo) |
| `Status` | int (`BatchStatus`: `Requested=0`, `Running=1`, `Completed=2`, `CompletedWithRejections=3`, `Empty=4`) | `Empty` prueba que corrió sin mensajes |
| `RequestedByKind` | int (`ActorKind`) | |
| `RequestedByUserId` | int, nullable, FK `SEC_Users` | |
| `RequestedByCentralUserId` | uniqueidentifier, nullable | |
| `RequestedByName`, `RequestedByEmail`, `RequestedByIp` | nvarchar(150), nvarchar(150), nvarchar(45), nullable | capturados en la petición que creó la orden: el despachador la ejecuta con esa persona como actor |
| `Reason` | nvarchar(500), nullable | obligatorio en `Manual`, `Reprocess`, `SendNotApplicable` |
| `RequestedAt`, `StartedAt`, `FinishedAt` | datetime | |
| `MessageCount`, `DocumentCount`, `ProcessedCount`, `RejectedCount`, `VoucherCount` | int | |
| `TotalDebit`, `TotalCredit` | 18,2 | de los comprobantes generados |
| `ResultSummaryJson` | nvarchar(max), nullable | grupos, comprobantes y rechazos con motivo |

Únicos: `Number`; `UK_COR_IntegrationBatches_Schedule (ScheduleKey, ScheduledFor)` filtrado
`[Trigger] = 1 AND [IsDeleted] = 0` (dos réplicas no crean la misma franja);
`(CashSessionPublicId)` filtrado `[Trigger] = 2 AND [IsDeleted] = 0` (un lote por cierre de sesión).
`COR_IntegrationBatchCounters`: fila única (`NextValue` bigint, `RowVersion`), creada en el primer uso.

### `COR_OperationKeys` — `OperationKey` (I1, [SinDiff])

Idempotencia de las operaciones de pantalla (T13). `IdempotencyBehavior` abre `TransaccionExplicita`,
inserta la fila, ejecuta el comando y, si tiene éxito, guarda el resultado y confirma; si falla, revierte
y la clave no queda.

| Columna | Tipo | Regla |
|---|---|---|
| `Key` | uniqueidentifier | la cabecera `Idempotency-Key`; **único** |
| `Operation` | nvarchar(120) | nombre del comando |
| `RequestSha256` | char(64) | misma clave con otro contenido → 422 `Operation.KeyReused` |
| `CentralUserId` | uniqueidentifier | |
| `UserId` | int, nullable, FK `SEC_Users` | de `IActorActual` |
| `ActorName` | nvarchar(150) | |
| `ResultJson` | nvarchar(max), nullable | el `Result` serializado; se escribe en la misma transacción antes del commit |

Nunca se borra ni cambia después del commit (B3: retención indefinida). Una repetición devuelve el mismo
resultado con `Idempotent-Replayed: true` y el evento `Operation.Replayed`; los mensajes no usan esta
tabla (su `MessageId` y el recibo único del destino bastan).

---

## 20. Lado contable (enmienda de la 009, I2)

Tablas `ACC_` en `Domain/Entities/Accounting/Inventory` (y el recibo en
`Entities/Accounting/Transactions`). Inventario no las lee ni las nombra
(`InventarioNoConoceContabilidadNiCartera`); las dimensiones que vienen del módulo o de Core se guardan
**por código**, nunca con FK a `INV_` (T27). Por eso esos códigos **no cambian** una vez creados: grupo
contable, bodega, punto de venta, medio de pago, tarifa y causa de ajuste.

### `ACC_InventoryPostingRules` — `InventoryPostingRule`

| Columna | Tipo | Regla |
|---|---|---|
| `Operation` | nvarchar(30) | catálogo fijo `OperacionesDeInventario` (§2.7) |
| `Role` | nvarchar(30) | catálogo fijo `RolesDeCuenta` (§2.7) |
| `AccountingGroupCode` | nvarchar(10), nullable | exigido por `Inventario`, `Costo`, `Ingreso`, `Transito` |
| `WarehouseCode` | nvarchar(10), nullable | opcional en esos roles |
| `PointOfSaleCode` | nvarchar(10), nullable | opcional en `MedioDePago` |
| `PaymentMeansCode` | nvarchar(10), nullable | exigido por `MedioDePago` |
| `TaxRateCode` | nvarchar(10), nullable | exigido por `Impuesto` y `Retencion` |
| `TaxRate` | 9,6, nullable | la tarifa (fracción) que la regla espera; va con `TaxRateCode` y es la que compara C8 |
| `ReasonCode` | nvarchar(40), nullable | `DiferenciaDeArqueo`: `Surplus` \| `ShortageToCashier` \| `ShortageToExpense`; `Baja` y `AjusteNegativo`: el código de `INV_AdjustmentCauses`; `CajaDestino`: la otra punta (`CashMovementDestination`: `Safe`, `Deposit`); `Contrapartida` en `AjusteDeCosto`: el nombre de `KardexReason` |
| `BranchId` | int, nullable, FK `COR_Branches` | |
| `CostCenterId` | int, nullable, FK `COR_CostCenters` | |
| `AccountId` | int, FK `ACC_ChartOfAccounts` | pasa por `AccountEligibility.Verificar(…, ModuloContable.Inventario)`; la de `Impuesto`/`Retencion`, además, con `TaxKind` compatible |
| `DimensionKey` | nvarchar(200) | `{Operation}\|{Role}\|G:{…\|*}\|W:{…\|*}\|P:{…\|*}\|M:{…\|*}\|T:{código@tarifa\|*}\|R:{…\|*}\|B:{Id\|*}\|C:{Id\|*}` |
| `SpecificityWeight` | smallint | bodega o punto 16, centro 8, sucursal 4, grupo 2 (suma); ordena la resolución sin empates |
| `ValidFrom` | date | |
| `ValidTo` | date, nullable | |
| `Notes` | nvarchar(300) | quién decidió y por qué |

`UK_ACC_InventoryPostingRules_DimensionKey_ValidFrom (DimensionKey, ValidFrom)` filtrado vivos; índice
`(Operation, Role, ValidFrom)`. **Reglas** (molde `AddPolicyVersionCommand`): una versión nueva cierra la
anterior la víspera; dos versiones de la misma clave no se cruzan (`Accounting.InventoryRule.Overlaps`);
una versión que empiece en o antes de la fecha de operación del último mensaje ya contabilizado de esa
operación se rechaza (`Accounting.InventoryRule.RetroactiveOverPosted`: los errores pasados se corrigen
con un comprobante manual). Desactivar cierra `ValidTo`; nunca se borra una regla usada. Resolución
(`ResolutorDeReglas`): entre las vigentes a la fecha de operación que coinciden, la de mayor
`SpecificityWeight`. `DocumentoAnulado` resuelve con las reglas vigentes a la fecha **del original**.

### `ACC_InventoryVoucherMappings` — `InventoryVoucherMapping`

| Columna | Tipo | Regla |
|---|---|---|
| `Operation` | nvarchar(30) | `OperacionesDeInventario` |
| `InventoryDocumentTypeCode` | nvarchar(10), nullable | excepción por tipo de documento de Inventario |
| `VoucherTypeId` | int, FK `ACC_VoucherTypes` | `Usage = Module`, `ModuleCode = INV`, activo (`FV`, `EI`, `SI`, `NV`, `CP`, `TR`, `AC`, `CJ`) |
| `CrossDocumentTypeId` | int, nullable, FK `ACC_CrossDocumentTypes` | cruce de las líneas de cuentas por cobrar y por pagar (`FV`, `NC`, `ND`, `FC`) |
| `MappingKey` | nvarchar(50) | `{Operation}\|{InventoryDocumentTypeCode\|*}`; único filtrado vivos |

Sin vigencia: un cambio vale para lo que se contabilice después y queda en el diff. El tipo de
comprobante de una unidad lo da su **mensaje principal** (el primero emitido: el comercial antes que el
de costo); `DocumentoAnulado` usa el de su original (T28). Lo siembra `InventoryVoucherMappingsSeeder`
(Order 84) desde `inventory-voucher-mappings.json`.

### `ACC_InventoryPostings` — `InventoryPosting` (H, `AuditableEntityLong`, `Entities/Accounting/Transactions`)

Recibo único por mensaje procesado, escrito en el **mismo** `SaveChanges` que el comprobante (T11). Es
también el vínculo comprobante ↔ documentos.

| Columna | Tipo | Regla |
|---|---|---|
| `MessagePublicId` | uniqueidentifier | **único** (`UK_ACC_InventoryPostings_MessagePublicId`): la colisión se traduce a `AlreadyProcessed` |
| `MessageType`, `MessageVersion` | nvarchar(60), smallint | |
| `MessageKind` | int (`IntegrationMessageKind`) | |
| `SourceModule` | nvarchar(3) | `INV` |
| `SourcePublicId` | uniqueidentifier | el documento, o la operación (cierre, reapertura, reclasificación) |
| `SourceDocumentClass` | nvarchar(40), nullable | el texto del mensaje (Contabilidad no depende de los tipos de Inventario) |
| `SourceDocumentTypeCode`, `SourceDocumentNumber` | nvarchar(10), nvarchar(30), nullable | |
| `RelatedDocumentPublicId` | uniqueidentifier, nullable | el original anulado, devuelto o corregido |
| `OperationDate` | date | la fecha del comprobante |
| `AccountingDocumentId` | int, nullable, FK `ACC_Documents` | nulo en informativos y en valor cero |
| `NoVoucherReason` | nvarchar(20), nullable | `Informational` \| `ZeroValue` |
| `BatchPublicId` | uniqueidentifier, nullable | el `COR_IntegrationBatches` por el que pasó |
| `OriginUserCentralId`, `OriginUserName` | uniqueidentifier, nvarchar(150) | usuario que originó el documento (dato) |
| `ActorKind` | int (`ActorKind`) | |
| `ActorUserId` | int, nullable, FK `SEC_Users` | |
| `ActorName` | nvarchar(150) | «Proceso de integración» o quien ordenó |
| `ProcessedAt` | datetime | |

Índices: `(SourcePublicId)`, `(RelatedDocumentPublicId)`, `(AccountingDocumentId)`, `(BatchPublicId)`,
`(OperationDate)`. Crece del orden de 3 a 4 millones de filas al año por cooperativa: por eso `bigint`.

### Vínculos comprobante ↔ documentos

- Por documento: `ACC_Documents` con `AccountingOrigin("INV", "InventoryDocument", documento.PublicId)`;
  resumido: `AccountingOrigin("INV", "InventoryPostingBatch", lote.PublicId)` (un comprobante por grupo,
  varios por lote: el índice de origen de `ACC_Documents` no es único). Los documentos de un resumido son
  las filas de `ACC_InventoryPostings` con ese `AccountingDocumentId`.
- Una anulación, nota o ajuste es un comprobante **nuevo** con su propio mensaje; su enlace al original es
  `RelatedDocumentPublicId`, y el comprobante del original se encuentra por el `MessagePublicId` de su
  mensaje. El original nunca se marca `Reversed` (T29). `EnlacesDeOrigen` (Application y Shared) suma
  `InventoryDocument` e `InventoryPostingBatch`.
- `PostingRequest.RegistradoPor` lleva el usuario de origen al `RegisteredBy` del comprobante (por
  documento); en un resumido, `RegisteredBy` es el actor y los usuarios quedan en el recibo.

### Cambio en una tabla existente

`ACC_AccountTaxRates.Rate` pasa de (9,4) a (9,6), ampliación no destructiva en
`IntegracionContableDeInventario`, para que C8 compare la tarifa del catálogo con la de la cuenta sin
redondear (las de ICA por mil no caben en cuatro decimales de fracción).

---

## 21. Seguridad y aprobaciones

### `INV_UserWarehouseScopes` — `UserWarehouseScope` (I1) e `INV_UserPointOfSaleScopes` — `UserPointOfSaleScope` (I3)

| Columna | Tipo | Regla |
|---|---|---|
| `UserId` | int, FK `SEC_Users` | |
| `WarehouseId` / `PointOfSaleId` | int, FK `INV_Warehouses` / `INV_PointsOfSale` | |
| `IsDefault` | bit | la bodega o el punto que se propone |

Únicos: `(UserId, WarehouseId)` [o `(UserId, PointOfSaleId)`] filtrado vivos, y `(UserId)` filtrado
`[IsDefault] = 1 AND [IsDeleted] = 0` (un solo defecto). Los administra `Inventory.Scopes.Manage`
(pestaña «Alcance comercial» de `/admin/usuarios`). **Falla cerrado**: sin filas, ninguna bodega ni
punto, salvo `Inventory.Scope.AllWarehouses` / `AllPointsOfSale` (T35). `IAlcanceDeInventario` lo lee
una vez por petición; en segundo plano el alcance es total. La bodega de tránsito se ve por los
traslados de las bodegas del alcance.

### `SEC_PermissionAmountLimits` — `PermissionAmountLimit` (I1)

| Columna | Tipo | Regla |
|---|---|---|
| `RoleId` | int, FK `SEC_Roles` | |
| `PermissionCode` | nvarchar(100) | validado contra el catálogo `SEC_Permissions`; se usa en `Inventory.Purchases.Confirm`, `Adjustments.Confirm`, `Sales.SellOnCredit`, notas y órdenes |
| `MaxAmount` | 18,2 | |
| `Currency` | char(3) | `COP` |
| `ValidFrom`, `ValidTo` | date, date nullable | |
| `Reason` | nvarchar(300) | |

Único `(RoleId, PermissionCode, ValidFrom)` filtrado vivos; sin cruces
(`Security.PermissionAmountLimit.Overlaps`). Monto efectivo (`ILimitesPorPermiso.MontoMaximoAsync`) = el
mayor de los roles activos que conceden el permiso; un rol que lo concede sin fila = sin límite (C4).
Por encima: nivel 1 de la política del tipo más los niveles cuyo umbral se alcanza; sin política,
`Inventory.Approval.AmountExceedsLimit` con `data.maxAmount`. Los administra
`Inventory.ApprovalPolicies.Manage` para los códigos `Inventory.*`.

### `COR_ApprovalPolicies`, `COR_ApprovalPolicyLevels`, `COR_ApprovalRequests`, `COR_ApprovalDecisions` (I1)

Motor de plataforma (T33): `EvaluadorDePolitica` (puro, casos dorados) + `MotorDeAprobaciones`.

| Columna (`COR_ApprovalPolicies` — `ApprovalPolicy`) | Tipo | Regla |
|---|---|---|
| `Module` | nvarchar(20) | `Inventory` |
| `Subject` | nvarchar(40) | qué se aprueba (`ApprovalSubjects`, tabla de abajo) |
| `DocumentTypePublicId` | uniqueidentifier, nullable | el tipo de documento (sin FK: la plataforma no apunta al módulo); nulo = todos los tipos del sujeto |
| `PolicyKey` | nvarchar(100) | `{Module}\|{Subject}\|{DocumentTypePublicId\|*}` |
| `Version` | int | |
| `ValidFrom`, `ValidTo` | date, date nullable | |
| `Reason` | nvarchar(300) | |

Único `(PolicyKey, ValidFrom)` filtrado vivos; sin cruces (`Approvals.Policy.Overlaps`). Gana la de un
tipo sobre la de todos.

| Columna (`COR_ApprovalPolicyLevels` — `ApprovalPolicyLevel`) | Tipo | Regla |
|---|---|---|
| `PolicyId` | int, FK | |
| `Order` | tinyint | 1..n; único `(PolicyId, Order)` |
| `Threshold` | 18,2 | no decrece con el orden |
| `PermissionCode` | nvarchar(100) | cualquier código del catálogo (C6); `Inventory.Approvals.Supervisor` y `.Management` son los sugeridos |

| Columna (`COR_ApprovalRequests` — `ApprovalRequest`) | Tipo | Regla |
|---|---|---|
| `Module`, `Subject` | nvarchar(20), nvarchar(40) | |
| `SourceType` | nvarchar(60) | `InventoryDocument`, `DocumentLineDiscount`, `DocumentPayment`, `TransferDiscrepancy`, `PurchaseMatchLine` |
| `SourcePublicId` | uniqueidentifier | |
| `SourceLabel` | nvarchar(80) | «AJ-000123», para la bandeja sin leer el módulo |
| `ScopeWarehousePublicId`, `ScopePointOfSalePublicId` | uniqueidentifier, nullable | el aprobador también necesita alcance (T35) |
| `Amount` | 18,2 | |
| `Currency` | char(3) | |
| `OperationDate` | date | la política vigente a esta fecha es la que se sella |
| `PolicyId` | int, nullable, FK | nulo en las reglas fijas de un nivel |
| `RequiredLevelsJson` | nvarchar(2000) | niveles sellados: `[{ order, threshold, permission }]` |
| `CreatedByUserId` | int, FK `SEC_Users` | creador del documento |
| `RequestedByUserId` | int, FK `SEC_Users` | |
| `ExcludedUserIdsJson` | nvarchar(400) | creador, solicitante y participantes declarados (quien abrió o capturó el conteo, el cajero); se suman quienes aprueban cada nivel |
| `Status` | int (`ApprovalRequestStatus`: `Pending=0`, `Approved=1`, `Rejected=2`, `Cancelled=3`) | |
| `CurrentLevel` | tinyint | |
| `ContentSha256` | char(64) | huella de lo que se aprueba; si cambia, la solicitud se cancela |
| `RequestedAt`, `DecidedAt` | datetime, datetime nullable | |

Único `(SourceType, SourcePublicId, Subject)` filtrado `[Status] = 0 AND [IsDeleted] = 0` (una solicitud
pendiente por fuente y sujeto); índice `(Status, Module, CurrentLevel)`.

| Columna (`COR_ApprovalDecisions` — `ApprovalDecision`, H) | Tipo | Regla |
|---|---|---|
| `RequestId` | int, FK | |
| `Level` | tinyint | |
| `Decision` | int (`ApprovalDecisionKind`: `Approve=1`, `Reject=2`) | |
| `DecidedByUserId` | int, FK `SEC_Users` | `SEC_Users.Id` de `IActorActual`, nunca el entero del token ni el correo (`LaSegregacionNoUsaUserIdDelToken`) |
| `DecidedByName` | nvarchar(150) | |
| `DecidedAt` | datetime | |
| `Method` | int (`ApprovalMethod`: `OwnSession=1`, `InPersonPasskey=2`, `InPersonTotp=3`) | nunca contraseña |
| `CredentialPublicId` | uniqueidentifier, nullable | la passkey usada en `InPersonPasskey` |
| `PermissionCodeUsed` | nvarchar(100) | |
| `ContentSha256` | char(64) | la huella al decidir |
| `Reason` | nvarchar(500), nullable | obligatorio al rechazar |

Único `(RequestId, Level)` filtrado `[Decision] = 1` (una aprobación por nivel). Rechazar cierra la
solicitud en `Rejected` y devuelve el documento a borrador; volver a enviarlo crea otra solicitud. La
última aprobación confirma el documento en la misma transacción (ahí se numera).

Qué pide aprobación (`ApprovalSubjects`, constantes). Esta tabla es la **única fuente** de qué política
rige cada sujeto; las rutas (`/approval-policies` con `subject`, el crédito provisional con
`Subject = ProvisionalCredit` y `SourceType = DocumentPayment`, la resolución de diferencias de traslado)
remiten a ella. El `Module` es siempre `Inventory`:

| `Subject` | `SourceType` | Niveles | Quién queda excluido |
|---|---|---|---|
| `DocumentConfirmation` | `InventoryDocument` | la política del tipo; y el nivel 1 forzado si el monto supera el máximo del permiso (T34). Cubre ajustes, bajas, consumos, conteos, saldo inicial, compras, notas, movimientos de caja y `CashCountDifference` | creador, solicitante, participantes (el cajero en arqueo y movimientos; quien abrió o capturó el conteo) |
| `DiscountOverCap` | `DocumentLineDiscount` | regla fija de un nivel: `Inventory.Discounts.Authorize` y tope del aprobador ≥ el descuento; si la cooperativa registra una política para el sujeto, rige la política | el cajero que lo pidió |
| `ProvisionalCredit` | `DocumentPayment` | regla fija de un nivel: `Inventory.Sales.SellOnCredit` con monto máximo ≥ el valor a crédito (o política del sujeto) | el cajero (T32) |
| `TransferDiscrepancy` | `TransferDiscrepancy` | la política del tipo del documento que la resuelve (el que crea `ResolveTransferDiscrepancyCommand`: recepción hacia el origen o el destino, baja desde el tránsito o ajuste positivo); sin política, un nivel con `Inventory.Transfers.Approve` | quien despachó, quien recibió y quien propone la resolución |
| `PurchaseMatchException` (I5) | `PurchaseMatchLine` | la política del tipo de la factura del proveedor | quien registró la factura |

---

## 22. Alertas (plataforma, I1)

### `COR_AlertTypes` — `AlertType`

Configuración por cooperativa, con vigencia, de cada tipo de §2.13.

| Columna | Tipo | Regla |
|---|---|---|
| `TypeCode` | nvarchar(60) | catálogo cerrado (§2.13) |
| `Module` | nvarchar(20) | |
| `RecipientPermissions` | nvarchar(1000) | JSON: códigos del catálogo de permisos; ninguno de acción `View` (el glob `*.View` los repartiría a todos los roles) |
| `Channels` | int (`AlertChannels` [Flags]: `InApp=1`, `Email=2`) | |
| `Severity` | int (`AlertSeverity`: `Info=1`, `Warning=2`, `Critical=3`) | |
| `ThresholdsJson` | nvarchar(2000), nullable | umbrales propios del tipo |
| `IsEnabled` | bit | |
| `ValidFrom`, `ValidTo` | date, date nullable | |
| `Reason` | nvarchar(300) | cambiar exige `Inventory.Alerts.Manage` y motivo |

Único `(TypeCode, ValidFrom)` filtrado vivos; sin cruces (`Alerts.AlertType.Overlaps`). Defecto de la
semilla (`AlertTypesSeeder`, Order 83), editable:

| `TypeCode` | Destinatarios por defecto | Canal | Severidad |
|---|---|---|---|
| `Inventario.Reorden` | `Inventory.Purchases.Create` | InApp | Info |
| `Inventario.Quiebre` | `Inventory.Purchases.Create`, `Inventory.Warehouses.Manage` | InApp | Warning |
| `Inventario.IncidenteDeIntegridad` | `Inventory.Integrity.Rebuild` | InApp + Email | Critical |
| `Compras.EventosRadianFaltantes` | `Inventory.Purchases.RegisterRadianEvent` | InApp + Email | Warning |
| `Aprobaciones.Pendiente` | el permiso del nivel pendiente (no usa la lista) | InApp | Info |
| `Integracion.MensajeSinEntregar` | `Inventory.Messages.Reprocess`, `Accounting.InventoryBatches.Run` | InApp + Email | Warning |
| `Integracion.MensajeRechazado` | `Inventory.Messages.Reprocess`, `Accounting.InventoryRules.Manage` | InApp + Email | Critical |
| `Integracion.LoteNoCorrio` | `Accounting.InventoryBatches.Run`, `Inventory.Messages.Reprocess` | InApp + Email | Warning |
| `Inventario.VentaBajoCosto` | `Inventory.Prices.Manage` | InApp | Warning |
| `Integracion.ValidacionFallida` (IC) | `Inventory.Sales.Approve` | InApp + Email | Warning |
| `Dian.DocumentoSinValidar` | `ElectronicInvoicing.Documents.Transmit` | InApp | Warning |
| `Dian.DocumentoRechazado` | `ElectronicInvoicing.Documents.Correct` | InApp + Email | Critical |
| `Dian.PlazoDeContingencia` | `ElectronicInvoicing.Contingencies.Declare`, `ElectronicInvoicing.Documents.Transmit` | InApp + Email | Critical |
| `Dian.ContingenciaAbierta` | `ElectronicInvoicing.Contingencies.Declare` | InApp + Email | Warning |
| `Dian.ResolucionPorAgotar`, `Dian.ResolucionPorVencer` | `ElectronicInvoicing.Resolutions.Manage` | InApp + Email | Warning |
| `Inventario.ProximoAVencer`, `Inventario.RemisionSinFacturar` (I6) | `Inventory.Warehouses.Manage`; `Inventory.Sales.Confirm` | InApp | Info |
| `Personas.SinPoliticaDeDatos` (I3) | `Compliance.HabeasData.RecordConsent` | InApp + Email | Warning |

`Personas.SinPoliticaDeDatos` se levanta cuando se da de alta una persona (POS o Compras) sin política
de tratamiento de datos publicada: el alta procede dejando «sin política vigente» (FR-011, FR-022).

### `COR_Alerts` — `Alert` (`AuditableEntityLong`)

Estado **compartido**: una alerta la atiende una persona y queda atendida para todos (lo que
`COR_Notifications`, una fila por destinatario, no modela).

| Columna | Tipo | Regla |
|---|---|---|
| `TypeCode` | nvarchar(60) | |
| `AlertTypeId` | int, FK `COR_AlertTypes` | la versión vigente al levantarla |
| `Module` | nvarchar(20) | |
| `Severity` | int (`AlertSeverity`) | |
| `Subject` | nvarchar(200) | |
| `Body` | nvarchar(2000) | qué pasó y qué hacer (FR-020) |
| `EntityType` | nvarchar(60), nullable | |
| `EntityPublicId` | uniqueidentifier, nullable | |
| `ScopeWarehousePublicId`, `ScopePointOfSalePublicId` | uniqueidentifier, nullable | sólo reciben quienes tienen alcance |
| `DedupKey` | nvarchar(200) | `{TypeCode}:{entidad}[:{bodega}]` |
| `Status` | int (`AlertStatus`: `Pending=0`, `Attended=1`) | |
| `RaisedAt` | datetime | |
| `RaisedByKind`, `RaisedByName` | int (`ActorKind`), nvarchar(150) | |
| `OccurrenceCount` | int | una repetición con la misma `DedupKey` pendiente suma aquí, no crea otra |
| `LastOccurredAt` | datetime | |
| `AttendedAt` | datetime, nullable | |
| `AttendedByKind` | int, nullable (`ActorKind`) | `Process` cuando se cierra sola (p. ej. llegaron el 030 y el 032) |
| `AttendedByUserId` | int, nullable, FK `SEC_Users` | |
| `AttendedByName` | nvarchar(150), nullable | |
| `AttendNote` | nvarchar(1000), nullable | |
| `RecipientCount` | int | |
| `WithoutRecipient` | bit | nadie activo tenía el permiso: se enrutó a los titulares de `CompanyAdmin` (SC-022) |

Único `(DedupKey)` filtrado `[Status] = 0 AND [IsDeleted] = 0`; índice `(Status, TypeCode, RaisedAt)`.
La entrega va por `SendNotificationCommand`: una fila de `COR_Notifications` por destinatario con
`Type = "Alert"` y la columna nueva `AlertPublicId` (uniqueidentifier, nullable) para abrir la alerta
desde la notificación; el correo, por `NotificationEmailDispatcher` con el arrendamiento
`email.dispatch`.

---

## 23. Auditoría garantizada y sello de integridad (plataforma, I1)

Para los **módulos encadenados** (`AuditoriaEncadenada.Modulos`: `Inventory`, `ElectronicInvoicing`,
`Integration`, `Approvals`, `Alerts`, `Parameters`, `Taxes`, `PaymentMeans`, `Navigation`), el evento se
escribe en SQL en la misma transacción del cambio y un reenviador lo lleva a Mongo (T37, T38).
`Accounting` queda fuera en esta feature (C1).

### `COR_AuditOutbox` — `AuditOutboxEntry` (`AuditableEntityLong`, [SinDiff])

| Columna | Tipo | Regla |
|---|---|---|
| `EventId` | uniqueidentifier | **único**; en Mongo es el `_id` (un duplicado se trata como hecho) |
| `Stream` | nvarchar(60) | `{tenantPublicId:N}:10y` |
| `Module` | nvarchar(30) | |
| `OccurredAt` | datetime | |
| `PayloadJson` | nvarchar(max), nullable | el `AuditDocumentSchema`; se vacía (`UPDATE … = NULL`) tras reenviar |
| `Forwarded` | bit | |
| `ForwardedAt` | datetime, nullable | |
| `ForwardAttempts` | int | |
| `LastForwardError` | nvarchar(500), nullable | |
| `Seq` | bigint, nullable | lo asigna el reenviador, en el orden en que reenvía |
| `PrevHash` | char(64), nullable | |
| `Hash` | char(64), nullable | SHA-256(`PrevHash` ‖ JSON canónico de `SelloDeIntegridad`) |

Únicos: `EventId`; `(Stream, Seq)` filtrado `[Seq] IS NOT NULL`. Índice `(Id)` filtrado `[Forwarded] =
0` (pendientes, por `Id` y nunca por un «último Id»: una transacción puede confirmar tarde). **Nunca
DELETE** (Principio VII): lo único que cambia, y sólo lo cambia `AuditOutboxForwarder`, es el reenvío, el
sello y el vaciado del contenido; queda la fila delgada (`Seq`, `Hash`) como testigo en SQL (C3). Las
diferencias de entidad las agrega `AuditableEntityInterceptor` en `SavingChanges`; el evento del comando,
`AuditBehavior`; los rechazos, un contexto aparte (`ITenantDbContextFactory`) que sobrevive al rollback.

### `COR_AuditChainHeads` — `AuditChainHead` ([SinDiff])

| Columna | Tipo | Regla |
|---|---|---|
| `Stream` | nvarchar(60) | único |
| `LastSeq` | bigint | |
| `LastHash` | char(64) | |
| `UpdatedAt` | datetime | |

Se actualiza con `RowVersion` junto con las filas que sella: dos réplicas no bifurcan la cadena aunque
coincidan (la segunda choca y vuelve a leer la cabeza).

### `COR_AuditAnchors` — `AuditAnchor` (H, [SinDiff])

| Columna | Tipo | Regla |
|---|---|---|
| `Stream` | nvarchar(60) | |
| `Kind` | int (`AuditAnchorKind`: `Genesis=1`, `EveryN=2`, `Daily=3`) | génesis al activar; cada 1.000 eventos; diaria |
| `Seq` | bigint | |
| `Hash` | char(64) | |
| `AnchorDate` | date, nullable | sólo `Daily` |
| `Hmac` | nvarchar(128) | HMAC de `stream\|seq\|hash\|anchoredAt` con la clave de `AuditSignature` en versión propia (A2: nunca `dev-v1`) |
| `KeyVersion` | nvarchar(20) | |
| `AnchoredAt` | datetime | |

Únicos: `(Stream, Kind, Seq)`; `(Stream, AnchorDate)` filtrado `[Kind] = 3`. En Mongo, cada evento lleva
`chain { stream, seq, prevHash, hash, alg: "SHA-256", v }`. `VerifyAuditIntegrityQuery` recorre por
`seq`, recalcula, valida anclas e informa alterado, eliminado, intercalado, ancla inválida y «purgado
por retención»; la verificación misma queda como `AuditLog.IntegrityVerified` (no es tabla).
`AuditRetention.ModulosDeDiezAnios` suma los módulos encadenados salvo `Navigation`, que ya estaba.

---

## 24. Resumen: índices únicos de §14 a §23

| Tabla | Columnas | Filtro |
|---|---|---|
| `INV_DocumentLineDiscounts` | `DocumentLineId, Sequence` | vivos |
| `INV_PriceLists` | `Code`; `ScopeKey, ValidFrom` | vivos |
| `INV_PriceListItems` | `PriceListId, ProductId, UnitId` | vivos |
| `INV_DiscountCaps` | `RoleId, ValidFrom` | vivos |
| `INV_Promotions` · `INV_PromotionTiers` | `Code` · `PromotionId, MinQuantity` | vivos |
| `INV_PointsOfSale` · `INV_CashRegisters` | `Code` · `PointOfSaleId, Code` | vivos |
| `INV_CashRegisterDocumentTypes` | `CashRegisterId, Role` | vivos |
| `INV_PaymentMeans{PointsOfSale,Channels,DocumentTypes}` | `PaymentMeansId` + destino | vivos |
| `INV_CashSessions` | `CashRegisterId` | `[Status] = 1 AND [IsDeleted] = 0` |
| `INV_CashSessions` | `CashierUserId` | `[Status] = 1 AND [ExclusiveCashier] = 1 AND [IsDeleted] = 0` |
| `INV_CashMovementDetails` · `INV_CashCounts` | `DocumentId` · `CashSessionId` | — |
| `INV_CashCountLines` | `CashCountId, PaymentMeansId` | — |
| `INV_CashCountDenominations` · `…TerminalBatches` · `…ReferenceChecks` | línea + denominación · línea + datáfono + lote · línea + pago | — |
| `INV_CashDocumentLines` | `DocumentId, LineNumber` | — |
| `INV_DayCloses` | `PointOfSaleId, OperatingDate` | `[Status] = 1 AND [IsDeleted] = 0` |
| `INV_DayCloses` · `INV_DayCloseLines` | `PointOfSaleId, OperatingDate, Version` · `DayCloseId, DetailKey` | — |
| `INV_DocumentPayments` | `DocumentId, LineNumber` | vivos |
| `INV_VoucherRedemptions` | `PaymentMeansId, NormalizedNumber` | `[Status] = 1 AND [IsDeleted] = 0` |
| `COR_PaymentMeans` · `COR_CardNetworks` · `COR_CardAcquirers` | `Code` | vivos |
| `COR_CardTerminals` · `COR_CashDenominations` | `CardAcquirerId, Code` · `Currency, Kind, Value` | vivos |
| `COR_TaxDefinitions` · `COR_WithholdingConcepts` | `Code` | vivos |
| `COR_TaxRates` | `Code, ValidFrom` | vivos |
| `COR_ElectronicEmissionSettings` | `ValidFrom` | vivos |
| `COR_DianNumberingResolutions` | `Environment, Prefix, ResolutionNumber` | vivos |
| `COR_DianResolutionChannels` | `ResolutionId, ValidFrom` | vivos |
| `COR_ElectronicDocuments` | `Environment, Prefix, Consecutive`; `SourceModule, SourceDocumentPublicId, Kind` | **sin filtro** |
| `COR_ElectronicDocumentVersions` · `…Transmissions` | documento + versión · documento + intento | — |
| `COR_DianContingencyEvents` | `Type, ChannelCode` | `[Status] = 1 AND [IsDeleted] = 0` |
| `COR_IntegrationMessages` | `PublicId`; `OriginPublicId, Type, OriginEventKey` | — |
| `COR_IntegrationMessageDependencies` | `MessageId, DependsOnMessageId` | — |
| `COR_IntegrationMessageDeliveries` | `MessageId, Destination` | — |
| `COR_IntegrationDeliveryAttempts` | `DeliveryId, AttemptNumber` | — |
| `COR_IntegrationBatches` | `Number`; `ScheduleKey, ScheduledFor`; `CashSessionPublicId` | —; `[Trigger] = 1 AND [IsDeleted] = 0`; `[Trigger] = 2 AND [IsDeleted] = 0` |
| `COR_OperationKeys` | `Key` | — |
| `ACC_InventoryPostingRules` | `DimensionKey, ValidFrom` | vivos |
| `ACC_InventoryVoucherMappings` | `MappingKey` | vivos |
| `ACC_InventoryPostings` | `MessagePublicId` | — |
| `INV_UserWarehouseScopes` · `INV_UserPointOfSaleScopes` | usuario + bodega o punto; `UserId` con `[IsDefault] = 1` | vivos |
| `SEC_PermissionAmountLimits` | `RoleId, PermissionCode, ValidFrom` | vivos |
| `COR_ApprovalPolicies` · `COR_ApprovalPolicyLevels` | `PolicyKey, ValidFrom` · `PolicyId, Order` | vivos |
| `COR_ApprovalRequests` | `SourceType, SourcePublicId, Subject` | `[Status] = 0 AND [IsDeleted] = 0` |
| `COR_ApprovalDecisions` | `RequestId, Level` | `[Decision] = 1` |
| `COR_AlertTypes` · `COR_Alerts` | `TypeCode, ValidFrom` · `DedupKey` | vivos · `[Status] = 0 AND [IsDeleted] = 0` |
| `COR_AuditOutbox` | `EventId`; `Stream, Seq` | —; `[Seq] IS NOT NULL` |
| `COR_AuditChainHeads` · `COR_AuditAnchors` | `Stream` · `Stream, Kind, Seq`; `Stream, AnchorDate` | —; `[Kind] = 3` |

## 25. Tablas de §14 a §23 por migración y semillas

| Migración (§2.15) | Tablas y columnas de §14 a §23 |
|---|---|
| `PlataformaParaInventario` (I1) | `COR_OperationKeys`, `COR_ApprovalPolicies`, `COR_ApprovalPolicyLevels`, `COR_ApprovalRequests`, `COR_ApprovalDecisions`, `COR_AlertTypes`, `COR_Alerts`, `COR_AuditOutbox`, `COR_AuditChainHeads`, `COR_AuditAnchors`, `SEC_PermissionAmountLimits`, `COR_TaxDefinitions`, `COR_TaxRates`, `COR_WithholdingConcepts`, `COR_IntegrationMessages`, `COR_IntegrationMessageDependencies`, `COR_IntegrationMessageDeliveries` (con `BatchId` sin FK); `COR_Notifications.AlertPublicId` |
| `InventarioComercialNucleo` (I1) | `INV_UserWarehouseScopes`; las columnas de venta de `INV_Documents` / `INV_DocumentLines` (`ValidUntil`, `DueDate`, `ReturnsGoods`, `IsFullReversal`, `CorrectionConceptCode`, `ListPriceIncludesTaxes`) |
| `IntegracionContableDeInventario` (I2) | `COR_IntegrationDeliveryAttempts`, `COR_IntegrationBatches`, `COR_IntegrationBatchCounters` (+ FK de `COR_IntegrationMessageDeliveries.BatchId`), `ACC_InventoryPostingRules`, `ACC_InventoryVoucherMappings`, `ACC_InventoryPostings`, `ACC_AccountTaxRates.Rate` (9,6) |
| `VentasYPuntoDeVenta` (I3) | `COR_PaymentMeans`, `COR_CardNetworks`, `COR_CardAcquirers`, `COR_CardTerminals`, `COR_CashDenominations`, `INV_PointsOfSale`, `INV_CashRegisters`, `INV_CashRegisterDocumentTypes`, `INV_PaymentMeansPointsOfSale`, `INV_PaymentMeansChannels`, `INV_PaymentMeansDocumentTypes`, `INV_CashSessions`, `INV_CashMovementDetails`, `INV_CashCounts`, `INV_CashCountLines`, `INV_CashCountDenominations`, `INV_CashCountTerminalBatches`, `INV_CashCountReferenceChecks`, `INV_CashDocumentLines`, `INV_DayCloses`, `INV_DayCloseLines`, `INV_DocumentPayments`, `INV_VoucherRedemptions`, `INV_DocumentLineDiscounts` (sin `PromotionId`), `INV_PriceLists`, `INV_PriceListItems`, `INV_DiscountCaps`, `INV_UserPointOfSaleScopes` |
| `DocumentosElectronicos` (I4) | `COR_ElectronicEmissionSettings`, `COR_DianNumberingResolutions`, `COR_DianResolutionChannels`, `COR_ElectronicDocuments`, `COR_ElectronicDocumentVersions`, `COR_ElectronicDocumentTransmissions`, `COR_DianContingencyEvents` |
| `ComercioAmpliado` (I6) | `INV_Reservations`, `INV_Promotions`, `INV_PromotionScopes`, `INV_PromotionTiers`, `INV_DocumentLineDiscounts.PromotionId` (+ FK) |

Semillas (§2.14) que llenan tablas de §14 a §23: `TaxCatalogSeeder` (81, `COR_Tax*` con `ReviewPending
= 1`), `AlertTypesSeeder` (83), `InventoryVoucherMappingsSeeder` (84), `VoucherTypesSeeder` (60, suma
`NV`, `CP`, `TR`, `AC`, `CJ` y registra colisiones en el log), `CashDenominationsSeeder` (85),
`DefaultPaymentMeansSeeder` (86, sólo `EFECTIVO`, clase `Cash`, `CountMethod = PhysicalCount`,
`DianPaymentMeansCode` sugerido `10`), `ConsumidorFinalSeeder` (87). Los permisos no son semilla
paramétrica: los siembra la API al arrancar (T48).

## 26. Nombres que las §14 a §23 agregan

No están en `decisiones-transversales.md` §2 y las §14 a §23 los necesitan. Propuesta: llevarlos a la §2
antes de `tasks.md`.

- **Enumeraciones nuevas** — en `Domain/Enums/Inventory`: `CreditOrigin { ProvisionalCredit=1,
  LendingNoResponse=2, Validated=3 }` (mismo texto que el contrato de `VentaACreditoRegistrada`),
  `ReservationStatus { Active=1, Consumed=2, Released=3, Expired=4 }`, `PromotionScopeKind { Product=1,
  Category=2, Segment=3, Channel=4 }`, `DayCloseStatus { Closed=1, Reopened=2 }`. En `Domain/Enums/Core`:
  `CashDenominationKind { Bill=1, Coin=2 }`. En `Domain/Enums/ElectronicInvoicing`: `UniqueCodeKind {
  Cufe=1, Cude=2, Cuds=3 }` (la §2.2 nombra la columna, no el tipo), `ContingencyEventStatus { Open=1,
  Closed=2 }`. En `Domain/Enums/Integration`: `MessageOriginKind { Document=1, Operation=2 }` (el
  `Origin.Kind` del sobre), `DeliveryAttemptOutcome { Processed=1, AlreadyProcessed=2, Rejected=3,
  Retry=4 }` (el `ResultadoDeConsumo` guardado). En `Domain/Enums/Audit`: `AuditAnchorKind { Genesis=1,
  EveryN=2, Daily=3 }`.
- **Constantes**: `ApprovalSubjects` (`DocumentConfirmation`, `DiscountOverCap`, `ProvisionalCredit`,
  `TransferDiscrepancy`, `PurchaseMatchException`) y la columna `COR_ApprovalPolicies.Subject` con su
  `PolicyKey`: la clave `(Module, DocumentTypePublicId)` de la §2.2 no distingue aprobar un documento de
  aprobar un descuento o un crédito del mismo tipo.
- **Atributo** `EscrituraUnicaAttribute` (`Domain/Common`), que la guarda de `IHechoInmutable` respeta
  (columnas de adjunto de `COR_ElectronicDocumentVersions`).
- **Columnas** no listadas en la §2.2/§2.3: las de venta de `INV_Documents` e `INV_DocumentLines` (arriba),
  `INV_CashRegisters.DefaultCardTerminalId` (el datáfono por defecto va en la caja y no en Core),
  `INV_CashSessions.ExclusiveCashier` y `LastActivityAt`, `COR_Notifications.AlertPublicId`,
  `COR_TaxRates.ReviewPending`, los `Issuer*` de `COR_ElectronicEmissionSettings`,
  `COR_IntegrationMessageDeliveries.ResultVoucherTypeCode` y `ResultVoucherNumber`.
- **Valores de `CashRegisterDocumentRole`**: `PosSale=1`, `InvoiceOnRequest=2`, `PosAdjustmentNote=3`,
  `InvoiceCreditNote=4`, `PosSaleContingency=5`, `InvoiceContingency=6` (reemplazan `CreditNote` y
  `Contingency` de la §2.5: una caja puede necesitar las dos notas y las dos contingencias).
- **`ResultadoDeConsumo.Processed`** lleva `(Guid AccountingDocumentPublicId, string VoucherTypeCode,
  string VoucherNumber)`: el destino los devuelve y la plataforma los guarda en la entrega, para que la
  bandeja de Inventario no lea tablas `ACC_` (FR-014).
- **Dueño de adjuntos** `DianContingencyEvent` en `AdjuntosDeModulo.DeModulo`: lectura
  `ElectronicInvoicing.Contingencies.View`, subida `ElectronicInvoicing.Contingencies.Declare`, no
  borrable (evidencias de la constancia ante la DIAN).
- **Comando** `ReplacePosDocumentWithInvoiceCommand` (factura después del documento equivalente, FR-063;
  contrato en `contracts/api.md` §18.3.1).
- **Códigos de error**: `Inventory.Sales.InvoiceInsteadNotApplicable`, `Inventory.PriceList.Overlaps`, `Inventory.DiscountCap.Overlaps`,
  `Inventory.Sales.BelowCost`, `Inventory.CashSession.{NotOpen, CashierWithoutPerson, HasOpenDrafts,
  HasPendingMovements}`, `Inventory.CashMovement.DestinationRegisterClosed`,
  `Inventory.DayClose.SessionsOpen`, `Payments.{ReferenceRequired, CardNumberNotAllowed, MeansNotAvailable}`,
  `Core.PaymentMeans.InUse`, `Core.TaxRate.{Overlaps, InEffect, Ambiguous}`,
  `ElectronicInvoicing.Settings.Overlaps`, `ElectronicInvoicing.Resolution.Overlaps`,
  `Approvals.Policy.Overlaps`, `Security.PermissionAmountLimit.Overlaps`, `Alerts.AlertType.Overlaps`.

## 27. Dudas para el plan

1. **Correo al comprador en dos sitios**: `COR_ElectronicEmissionSettings.EmailDeliveryBy` (§2.2) y el
   parámetro `Dian.EntregaCorreo` (§2.8). Propuesta: manda la configuración, porque depende de lo que el
   canal ofrece; el parámetro sólo propone el valor al crear una configuración. Si se prefiere el
   parámetro, se retira la columna.
2. **La sesión de caja se toca antes del orden canónico del cerrojo** (`LastActivityAt`), para que un
   cierre no deje fuera un cobro en curso. Es una fila más en el orden de §1.3 de las decisiones y hay
   que agregarla allí con la prueba de concurrencia (`UnaSesionPorCajaTests`).
3. **Unicidad «un cajero, una sesión» con parámetro**: un índice no se enciende con un parámetro; por eso
   la sesión sella `ExclusiveCashier` y el filtro lo incluye. Con el parámetro apagado, el comando no
   bloquea nada más.
4. **Cierre del día reabierto**: la §2.2 dice único `(PointOfSaleId, OperatingDate)`; aquí va filtrado a
   `Closed` más `Version`, para que reabrir y volver a cerrar deje la evidencia del primer cierre.
5. **Escritura única en un hecho inmutable** (`COR_ElectronicDocumentVersions`): es la forma más simple de
   que los artefactos lleguen después del commit sin volver mutable la versión. La alternativa es
   registrar cada artefacto en la transmisión que lo produjo y dejar la versión sin adjuntos.
6. **Una tarifa no se edita desde que entra en vigencia** (en vez de «si ya se usó»), porque Core no puede
   leer los documentos de Inventario para saberlo. Pedir a la contadora que lo acepte.
7. **Notas sobre facturas expedidas fuera del ERP** (ventas de COOFLOPAL anteriores a la salida): la
   spec no lo cubre y `COR_ElectronicDocuments.CorrectsDocumentId` sólo apunta a documentos del ERP.
   Hace falta decidir si se admite una nota con referencia externa (número, CUFE y fecha digitados) o si
   esas devoluciones se hacen por fuera.
8. **Numeración de los eventos RADIAN** que emite el ERP (I5): la §2 no dice de dónde sale su
   consecutivo; se decide en el plan de I5.
9. **Prefijos de notas y resoluciones** comparten la unicidad `(Environment, Prefix, Consecutive)` sin
   filtro: el comando que crea la secuencia de un tipo de nota debe rechazar un prefijo que ya sea de una
   resolución (y viceversa).
10. **Reglas fijas de aprobación** para `DiscountOverCap` y `ProvisionalCredit` cuando la cooperativa no
    registra política: se propone un nivel con el permiso y el tope o monto del aprobador; confirmar que
    no se quiere obligar a registrar una política.
11. **Lo que las §14 a §23 dan por definido en §0 a §13**: `COR_ParameterVersions`, `COR_BackgroundLeases`
    (plataforma de ejecución), `INV_Documents` e `INV_DocumentLines` completos (con las columnas de
    venta en E = I1), `INV_DocumentLinks`, `INV_DocumentLineLinks`, `INV_DocumentPartySnapshots`,
    `INV_DocumentTaxLines`, `INV_ProductTaxes`, `INV_SalesChannels`, `INV_SupplierInvoiceEvents`, el
    catálogo completo de claves de parámetro (§4.2) y las columnas nuevas de `COR_People`, `COR_Cities` y
    `COR_Branches` (sección de columnas nuevas en tablas existentes, migración `PlataformaParaInventario`).
