# Contrato: plantillas de importación de la parametrización (FR-095)

**Feature**: 012 | **Date**: 2026-09-24 | **Base**: decisiones transversales T49 (plantillas), T22,
T25, T27, T21, T13 (`decisiones-transversales.md`, en la carpeta de la feature); investigación `nucleo` (decisión 12), `compras_impuestos_cartera` (1, 2, 6),
`ventas_pos` (1b a 3b), `contabilidad` (2) | **Hermanos**: `api.md` (rutas), `data-model.md`
(entidades que cada plantilla escribe), `dian.md`

Requisitos que cubre: FR-030 (revisión previa, todo o nada, fila y columna), FR-031 (vendedores),
FR-089 (saldo inicial), FR-091 (cifras de SOLIDO), FR-095 (plantillas primero), FR-096 (medios de
pago), FR-073 (matriz); US1-1, US1-6, US4-1; SC-016.

**Por qué este contrato existe antes que las pantallas**: COOFLOPAL diligencia estas plantillas desde
ya, en lugar de parametrizar en las pantallas del módulo actual (D-07). Una columna que cambie después
invalida lo ya diligenciado, así que **el modelo de cada catálogo se congela antes de publicar su
plantilla** (T49). Cambiar una columna de este documento después de publicada exige una versión nueva
de la plantilla que siga leyendo la anterior.

## 0. Reglas comunes

### 0.1 Las dieciséis plantillas y su orden de carga

El orden es el de FR-095: cada plantilla sólo cita lo que cargaron las anteriores (o lo que ya existe
en el ERP, §0.2). Todas publican su `GET template.xlsx` en **I1**; la columna «Importa desde» dice
cuándo existe su `POST …/import`.

| # | Plantilla | Hojas de datos | Ruta base | Comando | Importa desde |
|---|---|---|---|---|---|
| 1 | Impuestos, retenciones y conceptos | `Conceptos`, `Impuestos`, `Tarifas` | `/api/core/taxes` | `ImportTaxCatalogCommand` | I1 |
| 2 | Grupos contables | `Datos` | `/api/inventory/accounting-groups` | `ImportAccountingGroupsCommand` | I1 |
| 3 | Unidades de medida | `Datos` | `/api/inventory/units` | `ImportUnitsOfMeasureCommand` | I1 |
| 4 | Marcas | `Datos` | `/api/inventory/brands` | `ImportBrandsCommand` | I1 |
| 5 | Categorías | `Datos` | `/api/inventory/product-categories` | `ImportProductCategoriesCommand` | I1 |
| 6 | Productos (con códigos de barras y conversiones) | `Productos`, `CodigosDeBarras`, `Unidades`, `ImpuestosAdicionales` | `/api/inventory/products` | `ImportProductsCommand` | I1 |
| 7 | Bodegas y ubicaciones | `Bodegas`, `Ubicaciones` | `/api/inventory/warehouses` | `ImportWarehousesCommand` | I1 |
| 8 | Tipos de documento | `TiposDeDocumento`, `NivelesDeAprobacion` | `/api/inventory/document-types` | `ImportDocumentTypesCommand` | I1 |
| 9 | Vendedores | `Datos` | `/api/inventory/salespeople` | `ImportSalespeopleCommand` | I1 |
| 10 | Puntos de venta y cajas | `PuntosDeVenta`, `Cajas` | `/api/inventory/points-of-sale` | `ImportPointsOfSaleCommand` | I3 |
| 11 | Medios de pago (con franquicias, adquirentes y datáfonos) | `Franquicias`, `Adquirentes`, `Datafonos`, `MediosDePago` | `/api/core/payment-means` | `ImportPaymentMeansCommand` (nombre propuesto; ver §11) | I3 |
| 12 | Listas de precios | `Listas`, `Precios` | `/api/inventory/price-lists` | `ImportPriceListsCommand` | I3 |
| 13 | Topes de descuento | `Datos` | `/api/inventory/discount-caps` | `ImportDiscountCapsCommand` | I3 |
| 14 | Saldo inicial | `Datos` | `/api/inventory/opening-balances` | `ImportOpeningBalanceCommand` | I1 |
| 15 | Cifras de SOLIDO | `Datos` | `/api/inventory/legacy-figures` | `ImportLegacyFiguresCommand` | I1 |
| 16 | Matriz de reglas contables | `Datos` | `/api/accounting/inventory/rules` | `ImportInventoryPostingRulesCommand` (+ `GetInventoryRulesTemplateQuery`) | I2 (su `GET` también es de I2, T49) |

En cada ruta base: `GET {base}/template.xlsx` y `POST {base}/import?mode=review|apply`.
`GET /api/inventory/templates` (`Inventory.Catalog.View`) devuelve esta tabla para la pantalla
`/inventario/plantillas`, con `canDownload` y `canImport` según los permisos de quien pregunta y
«se importa con I3» cuando la importación todavía no existe.

### 0.2 Lo que no tiene plantilla y debe existir antes

Sucursales contables (`COR_Branches`, con `MunicipalityDaneCode`, que se escribe por las rutas
existentes `POST/PUT /api/core/branches` (pantalla Maestros › Agencias) con `municipalityDaneCode`, validado contra
`COR_Cities.DaneCode`: 422 `Branch.MunicipalityUnknown`) y centros de costo de la 009;
personas del maestro (diálogo único de la 008: la importación **no crea ni modifica personas**,
FR-031); bancos (`COR_Banks`); canales de venta (`/ventas/canales`); roles de la cooperativa
(`/admin/roles`); plan de cuentas (009, que ya tiene su propia carga masiva); la UVT vigente
(`PAY_LegalParameters`, T23). Lo sembrado (unidades, tipos de bodega, causas de ajuste, un tipo de
documento por clase, catálogo tributario, `EFECTIVO`, denominaciones) se **actualiza** con la
plantilla: su fila existe y el código es la llave.

### 0.3 El archivo

- Libro `.xlsx` generado por `PlantillaDeImportacion`: **una hoja de datos por sección, con sólo los
  encabezados en la fila 1**, y una última hoja **«Instrucciones»** con título, subtítulo, una línea
  por columna (tipo, obligatoria, reglas y valores admitidos) y ejemplos. El lector no mira
  «Instrucciones». Las plantillas de una sola sección llaman a su hoja `Datos` y también aceptan
  `.csv` (la primera fila es el encabezado).
- **Extensión necesaria de la mecánica de la 009** (hoy lee sólo la primera hoja): `ITabularFileReader`
  gana la lectura de una hoja por nombre (sin nombre, la primera, como hoy) y `ErrorDeFila`
  (movido a `Application/Common/Imports`, T49) gana `Sheet`, nulo en las plantillas de una hoja.
  `data-model.md` y el plan deben recogerlo.
- Encabezados en `camelCase` español, como `PlantillaDeCuentas` de la 009. Se comparan sin
  mayúsculas, tildes, espacios ni guiones bajos (`TablaLeida.Normalizar`). El orden de las columnas no
  importa. Una columna desconocida se ignora con un aviso (`Import.Column.Unknown`), para detectar
  errores de digitación. Falta una columna obligatoria → 400 `Archivo.ColumnaFaltante`; falta una
  hoja obligatoria → 400 `Archivo.HojaFaltante` (nuevo).
- Las filas vacías se ignoran. El número de fila de un error es el de Excel (el encabezado es la 1).
- Formato de celda que pone la plantilla: texto (`@`) en códigos y documentos, para que Excel no se
  coma los ceros a la izquierda; `0.00` en montos; `0.0000` en cantidades; `0.000000` en costos y
  factores (los dos formatos que T49 agrega a `TipoDeColumna`); `0.0000` en porcentajes de tarifa.
- Tope: 60.000 filas por hoja y 16 MB por archivo (límite de la ruta). La revisión carga los
  catálogos citados en bloque (diccionarios código → Id), nunca una consulta por fila.

### 0.4 Tipos de valor

| Tipo | Cómo se escribe | Ejemplo |
|---|---|---|
| Código de catálogo | `CodigoDeCatalogo`: `^[A-Za-z0-9._-]+$`, se guarda en mayúsculas; largo 10 salvo que la columna diga otro (producto 20) | `ABARROTES` |
| Texto | libre, se recorta; largo máximo según la columna | `ARROZ 500 G` |
| Entero | sin decimales ni separador de miles | `24` |
| Monto | hasta 2 decimales, punto o coma decimal, sin separador de miles | `2100,50` |
| Cantidad | hasta 4 decimales (y no más de los que admite la unidad) | `12,5000` |
| Costo / factor | hasta 6 decimales | `1234,567890` |
| Porcentaje | en puntos, hasta 4 decimales: `19` es 19 %; `0,966` es 9,66 por mil. **Se guarda como fracción (9,6)** (T19): 0,190000; 0,009660 | `19` |
| Fecha | `yyyy-MM-dd` o fecha de Excel | `AAAA-MM-01` |
| Hora | `HH:mm`, hora de Colombia | `23:00` |
| Sí/no | `sí`, `si`, `s`, `x`, `1` = sí; `no`, `n`, `0` o vacío = no (`PlantillaDeCuentas.Booleano`) | `sí` |
| Sí/no/indiferente | como sí/no, pero **vacío = no importa** (condiciones de tarifa) | vacío |
| Lista | valores separados por coma; `*` = todos | `B01, B02` |
| Enumeración | el nombre del enum en inglés o la etiqueta en español de la columna, sin mayúsculas ni tildes | `Inventoriable` o `inventariable` |
| Persona | documento de identidad sin dígito de verificación; debe existir y estar viva en el maestro | `16000111` |
| Sucursal | su código (`LegacyCode`) o su nombre | `01` |
| Centro de costo | su código (`LegacyCode`) o su nombre | `ADM` |
| Banco | su nombre o su código (`COR_Banks`) | `BANCOLOMBIA` |
| Cuenta contable | código completo de la auxiliar de movimiento | `24080501` |

**Las fechas de los ejemplos son ilustrativas** (`AAAA-MM-01` para una vigencia, `AAAA-MM-DD` para la
fecha de corte de una bodega): no anuncian ningún plan. La vigencia y el corte los fija el dueño (Q1).

Un valor que no cumple su tipo es `Import.Cell.Format`; uno obligatorio vacío, `Import.Cell.Required`;
una referencia que no existe, `Import.Cell.NotFound` (el mensaje nombra lo que se buscó y dónde se
crea); la misma llave dos veces en el archivo, `Import.Row.Duplicate` (nombra la otra fila); un valor
de una entrega que todavía no existe (p. ej. controlar lote antes de I6),
`Import.Cell.NotYetAvailable`; una columna que exige un permiso que la persona no tiene,
`Import.Cell.PermissionRequired` (nombra el permiso). Las reglas de negocio responden con **el mismo
código que el alta unitaria** (`Catalogo.CodigoDuplicado`, `Inventory.Barcode.Duplicate`,
`Inventory.Unit.DecimalsNotAllowed`, `Inventory.PostingMode.ChainMismatch`,
`Accounting.InventoryRule.Overlaps`…): no hay una segunda puerta con reglas propias.

### 0.5 Revisión y aplicación

- `mode` es obligatorio (sin él, 400 `Import.ModeRequired`). Cuerpo `multipart/form-data` con el
  archivo y, cuando la revisión lo pide, `reason` (hasta 400 caracteres).
- **`review`** corre exactamente las mismas reglas que `apply` y **no guarda nada** (FR-030). Responde
  200 aunque haya errores, con `valid = false`. Con `format=xlsx` devuelve además el mismo libro con
  dos columnas al final de cada hoja, `resultado` (Crear, Actualizar, Sin cambio) y `errores`, para
  corregir sobre el mismo archivo.
- **`apply`** es **todo o nada**: una transacción (`TransaccionExplicita`), un `SaveChanges`. Con un
  solo error no se guarda nada y responde 422 `Import.Invalid` con el mismo cuerpo en `data`. Si otra
  persona creó al mismo tiempo algo que choca con un índice único, la aplicación falla completa con
  ese error nombrado y se vuelve a revisar.
- **Llave y actualización**: el código (o la llave compuesta que diga cada plantilla) identifica la
  fila. Si existe, **actualiza**; si queda igual, «sin cambio». Así el archivo es idempotente: se
  corrige y se vuelve a subir sin duplicar nada.
- **La plantilla nunca borra**: lo que no viene en el archivo se deja como está. Inactivar es poner
  `activo = no` donde la entidad lo admite; quitar un código de barras o una ubicación se hace en
  pantalla.
- **Los códigos no se renombran**: grupo contable, bodega, punto de venta, medio de pago, tarifa y
  causa de ajuste son dimensiones de la matriz por código y no cambian una vez creados (T27). Un
  código distinto es otra fila.
- **Motivo**: la revisión devuelve `requiresReason = true` cuando el archivo crea o cierra una vigencia
  (parámetro, política de aprobación, tope, tarifa, lista de precios, medio de pago), cambia la
  tolerancia o la comisión esperada de un medio existente, o cambia el grupo contable de un producto
  con existencia; entonces `apply` exige `reason`, que se copia a cada vigencia y al evento de
  auditoría (`IConMotivo`). Sin él, 422 `Import.Invalid` con un error `Import.Cell.Required` sin fila y
  con `column = "reason"`; una columna que exige un permiso ausente es `Import.Cell.PermissionRequired`.
  No hay códigos de importación propios de cada área (`Inventory.Import.*`,
  `Accounting.InventoryRules.Invalid`, `Inventory.PriceLists.Invalid`, `Core.PaymentMeans.Invalid`): todas
  responden `Import.*`.
- **Idempotencia** (T13): cada llamada lleva `Idempotency-Key`. Revisar y aplicar son dos operaciones:
  cada una con su clave (misma clave con otro modo es otro contenido → 422 `Operation.KeyReused`).
- **Auditoría**: se registra la importación (plantilla, modo, nombre y SHA-256 del archivo, conteos,
  quién, IP, canal); en `apply`, además, el antes y después de cada entidad por el interceptor. La
  descarga con datos se audita como exportación. El archivo no se guarda.

Respuesta (`ImportResultDto`):

```jsonc
{
  "template": "inventory.products",
  "mode": "Review",                       // ModoDeImportacion: Review | Apply
  "valid": true, "applied": false,
  "fileName": "productos-cooflopal.xlsx", "fileSha256": "9c1e…",
  "requiresReason": false,
  "sheets": [ { "sheet": "Productos", "rows": 1250, "created": 1200, "updated": 40, "unchanged": 10 } ],
  "changes": [                            // hasta 5.000; el resto en format=xlsx (changesTruncated)
    { "sheet": "Productos", "row": 7, "key": "ARZ-001", "action": "Update",
      "fields": [ { "column": "nombre", "before": "ARROZ 500", "after": "ARROZ 500 G" } ] }
  ],
  "changesTruncated": false,
  "warnings": [ { "sheet": "CodigosDeBarras", "row": 12, "column": "codigoDeBarras", "code": "Import.Barcode.CheckDigit", "message": "…" } ],
  "errors":   [ { "sheet": "Productos", "row": 9, "column": "grupoContable", "code": "Import.Cell.NotFound", "message": "No hay un grupo contable «ABARROTE». Créelo en la plantilla de grupos o en Inventario → Grupos contables." } ],
  "totalErrors": 1,                       // se devuelven hasta 1.000, ordenados por hoja, fila y columna
  "extra": { }                            // propio de cada plantilla (§8, §14, §15)
}
```

### 0.6 Descarga con datos

`GET {base}/template.xlsx?withData=true` devuelve la plantilla llena con lo que hoy tiene la
cooperativa, en el mismo formato: así se exporta, se corrige y se vuelve a importar (FR-030). Exige,
además del permiso de consulta del área, el de exportar (`Inventory.Catalog.Export` en Inventario;
en Core basta el de consulta, que no trae datos personales; con datos personales —vendedores, listas
por cliente, adquirentes con tercero— además `Inventory.Reports.ExportPersonalData`). Se audita.

### 0.7 Permisos por plantilla

| # | Descargar vacía | Importar | Permisos adicionales por columna |
|---|---|---|---|
| 1 | `Core.Taxes.View` | `Core.Taxes.Manage` | — |
| 2–5 | `Inventory.Catalog.View` | `Inventory.Catalog.Import` | — |
| 6 | `Inventory.Catalog.View` | `Inventory.Catalog.Import` | cambiar el grupo de un producto con existencia: `Inventory.Catalog.ReclassifyAccountingGroup` |
| 7 | `Inventory.Warehouses.View` | `Inventory.Warehouses.Manage` | `stockNegativo`: `Inventory.Parameters.Manage` |
| 8 | `Inventory.DocumentTypes.View` | `Inventory.DocumentTypes.Manage` | hoja `NivelesDeAprobacion`: `Inventory.ApprovalPolicies.Manage`; modo de paso y lotes: `Inventory.Parameters.Manage`; tipo fiscal sin paso: `Inventory.DocumentTypes.DisableFiscalPosting` |
| 9 | `Inventory.Salespeople.View` | `Inventory.Salespeople.Manage` | — |
| 10 | `Inventory.PointsOfSale.View` | `Inventory.PointsOfSale.Manage` | — |
| 11 | `Core.PaymentMeans.View` | `Core.PaymentMeans.Manage` | columnas de disponibilidad (`puntos`, `canales`, `tiposDeDocumento`): `Inventory.PointsOfSale.Manage` |
| 12 | `Inventory.Prices.View` | `Inventory.Prices.Manage` | — |
| 13 | `Inventory.Prices.View` | `Inventory.DiscountCaps.Manage` | — |
| 14 | `Inventory.Warehouses.View` | `Inventory.OpeningBalance.Load` | confirmar el documento: `Inventory.OpeningBalance.Approve` (fuera de la plantilla) |
| 15 | `Inventory.Warehouses.View` | `Inventory.LegacyFigures.Import` | — |
| 16 | `Accounting.InventoryRules.View` | `Accounting.InventoryRules.Manage` | — |

Sin el permiso de la ruta, el 404 genérico.

---

## 1. Impuestos, retenciones y conceptos de retención (Core, T22)

Escribe `COR_WithholdingConcepts`, `COR_TaxDefinitions` y `COR_TaxRates`. La semilla
(`TaxCatalogSeeder`, marcada «pendiente de validar por la contadora») ya deja IVA 19/5/exento/excluido,
INC, bolsas por unidad, ReteFuente por concepto con base en UVT y ReteIVA; ReteICA no se siembra (es
municipal). Descargar con datos es el camino para ajustarla. La UVT no va aquí (T23).

### Hoja `Conceptos`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 120 | sí | |
| `activo` | sí/no | no (sí) | un concepto usado por productos o tarifas vigentes no se inactiva |

| codigo | nombre | activo |
|---|---|---|
| COMPRAS | Compras generales | sí |
| SERVICIOS | Servicios generales | sí |
| HONORARIOS | Honorarios | sí |

### Hoja `Impuestos`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 120 | sí | |
| `clase` | `TaxKind`: `Iva`, `Inc`, `ReteFuente`, `ReteIva`, `ReteIca`, `Ica`, `Other` | sí | no cambia si tiene tarifas usadas |
| `formaDeCalculo` | `TaxCalculationForm`: `PercentOfBase` (porcentaje sobre la base), `PercentOfTax` (sobre otro impuesto), `AmountPerUnit` (valor por unidad) | sí | |
| `calculadoSobre` | código de otro impuesto | con `PercentOfTax` | debe ser de clase `Iva` (ReteIVA) |
| `esRetencion` | sí/no | sólo con `Other` | en las demás clases se deriva (`ReteFuente`, `ReteIva`, `ReteIca` = sí) |
| `codigoDian` | texto 2 | sí | tributo del anexo vigente de `CatalogoDian` (p. ej. 01 IVA, 04 INC, 22 bolsas, 05 ReteIVA, 06 ReteFuente, 07 ReteICA, ZZ otros) |
| `activo` | sí/no | no (sí) | |

| codigo | nombre | clase | formaDeCalculo | calculadoSobre | codigoDian |
|---|---|---|---|---|---|
| IVA | IVA | Iva | PercentOfBase | | 01 |
| RETEIVA | Retención de IVA | ReteIva | PercentOfTax | IVA | 05 |
| BOLSAS | Impuesto nacional al consumo de bolsas plásticas | Inc | AmountPerUnit | | 22 |

### Hoja `Tarifas`

Llave: `codigo` + `vigenteDesde`. El código es estable entre vigencias (la matriz lo cita junto con la
tarifa, T27): una tarifa nueva de la misma clave es otra fila con otra `vigenteDesde`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | |
| `impuesto` | código de la hoja `Impuestos` o existente | sí | |
| `nombre` | texto 120 | sí | «IVA 19 %» |
| `tarifaPorcentaje` | porcentaje | con `PercentOfBase`/`PercentOfTax` | 0 a 100; se guarda como fracción (9,6) |
| `valorPorUnidad` | monto | con `AmountPerUnit` | excluye a `tarifaPorcentaje` |
| `conceptoRetencion` | código de `Conceptos` | en `ReteFuente`; opcional en `ReteIca` | |
| `municipio` | texto 5 (DANE) | en `Ica` y `ReteIca` | debe existir en `COR_Cities.DaneCode` (DIVIPOLA) |
| `actividad` | texto 4 (CIIU) o `*` | no (`*`) | sólo `Ica`/`ReteIca`; `*` = tarifa general del municipio |
| `baseMinimaUvt` | cantidad | no | ≥ 0; excluye a `baseMinimaPesos`; procede si la base es **igual o superior** al mínimo |
| `baseMinimaPesos` | monto | no | sólo donde el municipio la fija en pesos |
| `aplicaA` | `TaxAppliesTo`: `Purchases` (compras), `Sales` (ventas), `Both` (ambas) | sí | |
| `prioridad` | entero ≥ 0 | no (0) | desempata tarifas del mismo impuesto y concepto que coinciden a la vez; gana la mayor |
| `sujetoTipoPersona` | `Natural`, `Juridica` o vacío | no | condición sobre quien sufre la retención o paga el impuesto |
| `sujetoDeclarante` · `sujetoResponsableIva` · `sujetoGranContribuyente` · `sujetoAutorretenedor` · `sujetoRegimenSimple` | sí/no/indiferente | no | condiciones tipadas sobre las marcas de `COR_People` (T24) |
| `agenteGranContribuyente` · `agenteRetenedorIva` | sí/no/indiferente | no | condiciones sobre quien retiene |
| `vigenteDesde` | fecha | sí | |
| `vigenteHasta` | fecha | no | ≥ `vigenteDesde` |
| `norma` | texto 200 | sí | la norma que la respalda (`LegalSource`) |
| `notas` | texto 400 | no | |

Reglas: dos vigencias del mismo código no se cruzan; una tarifa usada por un documento confirmado sólo
admite cambiar `vigenteHasta`, `nombre` y `notas` (para otro valor se cierra y se agrega otra fila);
la condición `sujeto…`/`agente…` sólo se admite en clases de retención; `actividad` distinta de `*`
exige `municipio`. Los nombres de las columnas de condición son el modelo congelado de
`COR_TaxRates` (§ condiciones de `data-model.md`).

| codigo | impuesto | nombre | tarifaPorcentaje | valorPorUnidad | conceptoRetencion | municipio | baseMinimaUvt | aplicaA | sujetoDeclarante | vigenteDesde | norma |
|---|---|---|---|---|---|---|---|---|---|---|---|
| IVA19 | IVA | IVA 19 % | 19 | | | | | Both | | 2017-01-01 | ET art. 468 |
| BOLSA | BOLSAS | Bolsa plástica | | 66,00 | | | | Sales | | 2026-01-01 | ET art. 512-15 (valor por cotejar) |
| RFCOMP25 | RETEFUENTE | ReteFuente compras 2,5 % | 2,5 | | COMPRAS | | 27 | Purchases | sí | 2026-01-01 | DUR 1625/2016 |

(Los valores de ejemplo son ilustrativos; los de la cooperativa los valida la contadora.)

## 2. Grupos contables

Escribe `INV_AccountingGroups`. Inventario envía el grupo; la matriz de Contabilidad lo traduce a
cuentas (FR-027, FR-073).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave; no cambia nunca (T27) |
| `nombre` | texto 120 | sí | |
| `descripcion` | texto 400 | no | |
| `activo` | sí/no | no (sí) | un grupo con productos activos no se inactiva (el error dice cuántos) |

| codigo | nombre | descripcion |
|---|---|---|
| ABARROTES | Abarrotes | Víveres secos |
| AGRO | Insumos agropecuarios | |
| SERVICIOS | Servicios | Ingresos por servicios (sin existencia) |

## 3. Unidades de medida

Escribe `INV_UnitsOfMeasure`. La semilla (`inventario-unidades.json`) trae las básicas.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 60 | sí | |
| `decimales` | entero 0 a 4 | sí | `AllowedDecimals`; bajarlo en una unidad con movimientos se rechaza |
| `codigoDian` | texto 3 | sí | UN/ECE Rec. 20 validado contra la tabla vigente de `CatalogoDian` (lo exige la factura electrónica) |
| `activo` | sí/no | no (sí) | |

| codigo | nombre | decimales | codigoDian |
|---|---|---|---|
| UND | Unidad | 0 | 94 |
| KG | Kilogramo | 3 | KGM |
| LT | Litro | 2 | LTR |

## 4. Marcas

Escribe `INV_Brands`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 120 | sí | |
| `activo` | sí/no | no (sí) | |

| codigo | nombre |
|---|---|
| DIANA | Arroz Diana |
| GENERICO | Sin marca |

## 5. Categorías

Escribe `INV_ProductCategories`, jerárquica hasta 5 niveles (FR-024).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 120 | sí | |
| `padre` | código de categoría | no | vacío = raíz; puede venir en otra fila del mismo archivo, en cualquier orden |
| `activo` | sí/no | no (sí) | |

Reglas: sin ciclos (el error nombra la cadena); ninguna categoría queda en un nivel mayor que 5, también
al mover una rama; una categoría con productos activos no se inactiva.

| codigo | nombre | padre |
|---|---|---|
| ALIM | Alimentos | |
| GRANOS | Granos | ALIM |
| ARROZ | Arroz | GRANOS |

## 6. Productos, con códigos de barras y conversiones

Escribe `INV_Products`, `INV_ProductBarcodes`, `INV_ProductUnits` e `INV_ProductTaxes`. Reglas de
FR-023 a FR-030. Las cuatro hojas se revisan y se aplican juntas.

### Hoja `Productos`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código **20** | sí | llave |
| `nombre` | texto 200 | sí | |
| `tipo` | `ProductKind`: `Inventoriable` (inventariable), `Service` (servicio) | sí | `Combo`, `Kit`, `Template`, `Variant` → `Import.Cell.NotYetAvailable` hasta I6 |
| `categoria` | código de categoría | sí | |
| `marca` | código de marca | no | |
| `unidadBase` | código de unidad | sí | no cambia si el producto tiene movimientos (FR-025) |
| `grupoContable` | código de grupo | en inventariables | en servicios es opcional, pero sin grupo su venta no encontrará cuenta de ingreso en la matriz (aviso) |
| `estado` | `ProductStatus`: `Active` (activo), `Inactive` (inactivo), `Blocked` (bloqueado) | no (Active) | |
| `controlaLote` · `controlaSerie` · `controlaVencimiento` | sí/no | no (no) | «sí» → `Import.Cell.NotYetAvailable` hasta I6; no cambian con movimientos |
| `tratamientoIva` | `VatSaleTreatment`: `Taxed` (gravado), `Exempt` (exento), `Excluded` (excluido) | sí | |
| `tarifaIva` | código de tarifa de un impuesto de clase `Iva` | si es gravado | vacío si exento o excluido |
| `conceptoRetencion` | código de concepto | sí | concepto de retención en compras (FR-027) |
| `referencia` | texto 60 | no | entra a la búsqueda (`SearchText`) |
| `peso` · `volumen` | cantidad | no | kg y litros; sirven al prorrateo (I5) |
| `motivoCambioDeGrupo` | texto 400 | cuando la fila cambia el grupo de un producto con existencia | exige además `Inventory.Catalog.ReclassifyAccountingGroup`; el cambio rige desde la fecha de operación de la importación, afecta sólo lo futuro y emite `GrupoContableReclasificado` (FR-027, FR-030) |

| codigo | nombre | tipo | categoria | marca | unidadBase | grupoContable | tratamientoIva | tarifaIva | conceptoRetencion |
|---|---|---|---|---|---|---|---|---|---|
| ARZ-001 | ARROZ DIANA 500 G | Inventoriable | ARROZ | DIANA | UND | ABARROTES | Excluded | | COMPRAS |
| FERT-50 | FERTILIZANTE 15-15-15 X 50 KG | Inventoriable | AGROINS | GENERICO | UND | AGRO | Taxed | IVA5 | COMPRAS |
| SRV-TRANS | SERVICIO DE TRANSPORTE | Service | SERV | | UND | SERVICIOS | Taxed | IVA19 | SERVICIOS |

### Hoja `CodigosDeBarras`

Llave: `codigoDeBarras`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `producto` | código de producto (de la hoja o existente) | sí | |
| `codigoDeBarras` | texto 50, `^[A-Za-z0-9-]+$` | sí | único en la cooperativa entre los vivos; si lo tiene otro producto, `Inventory.Barcode.Duplicate` nombrando a ese producto (FR-024) |
| `unidad` | código de unidad | no (la base) | la base o una unidad alterna del producto: el código identifica ese empaque |

Aviso `Import.Barcode.CheckDigit` si un código numérico de 8, 12, 13 o 14 dígitos no cumple el dígito
de control EAN/UPC (no bloquea: hay códigos internos).

| producto | codigoDeBarras | unidad |
|---|---|---|
| ARZ-001 | 7702001000014 | |
| ARZ-001 | 17702001000011 | PACA25 |

### Hoja `Unidades` (conversiones)

Llave: `producto` + `unidad`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `producto` | código de producto | sí | |
| `unidad` | código de unidad | sí | distinta de la base |
| `factor` | costo/factor (18,6) | sí | > 0; cuántas unidades base tiene (paca de 25 → 25); no cambia si la unidad tiene movimientos |
| `uso` | `Purchase` (compra), `Sale` (venta), `Both` (ambas) | sí | |

| producto | unidad | factor | uso |
|---|---|---|---|
| ARZ-001 | PACA25 | 25 | Both |
| ACEITE-1L | CAJA12 | 12 | Purchase |

Una unidad con factor fraccionario (vender por kilo un producto cuya base es el bulto) sólo sirve si
la unidad base admite los decimales que resultan; si no, la conversión se rechaza al digitar (FR-017).
Es preferible que la base sea la unidad más pequeña que se vende.

### Hoja `ImpuestosAdicionales`

Otros impuestos del producto además del IVA (INC, bolsas, ICUI o IBUA si la contadora los confirma).
Llave: `producto` + `tarifa`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `producto` | código de producto | sí | |
| `tarifa` | código de tarifa | sí | no de clase `Iva` (ése va en `tarifaIva`) ni de retención |
| `unidadesGravables` | costo/factor | con impuestos por unidad | `TaxableUnitsPerBaseUnit`: bolsa = 1; bebida de 1,5 L con IBUA por 100 ml = 15 |

| producto | tarifa | unidadesGravables |
|---|---|---|
| BOLSA-01 | BOLSA | 1 |

## 7. Bodegas y ubicaciones

Escribe `INV_Warehouses` e `INV_WarehouseLocations` (FR-032, FR-034).

### Hoja `Bodegas`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave; no cambia nunca (T27) |
| `nombre` | texto 120 | sí | |
| `sucursal` | sucursal | sí | no cambia si la bodega tiene movimientos; si la sucursal no tiene `MunicipalityDaneCode`, aviso (la ReteICA de compras lo necesita) |
| `tipo` | código o nombre del tipo de bodega | sí | sembrados: principal, punto de venta, averías, cuarentena, tránsito |
| `stockNegativo` | sí/no/indiferente | no | vacío = hereda el general; con valor crea una vigencia de `Existencias.StockNegativoPermitido` con ámbito bodega (pide `reason`) |
| `activa` | sí/no | no (sí) | |

Reglas: toda bodega nace **no activa** (`NotActivated`); la plantilla nunca activa (eso es
`ActivateWarehouseCommand`, con su comparación contra Contabilidad, FR-090). **Bodega de tránsito**
(regla única, la misma de `POST /warehouses` con su campo opcional `transitWarehouse { code, name }`):
se crea con la primera bodega operativa de la sucursal; su código lo propone el sistema (`TR` + código
de la sucursal) y lo puede fijar quien crea, aquí con una fila de tipo tránsito de esa sucursal en el
mismo archivo. Si el archivo no la trae, la aplicación la crea con el código propuesto y la revisión lo
anuncia. No se crea sola fuera de ese momento: una fila de tipo tránsito para una sucursal que ya tiene
la suya, o sin bodega operativa nueva que la acompañe, es `Inventory.WarehouseType.TransitIsSystem`.
Como su código es dimensión de la matriz (T27), conviene fijarlo aquí.

| codigo | nombre | sucursal | tipo |
|---|---|---|---|
| B01 | BODEGA PRINCIPAL FLORIDA | 01 | principal |
| PV01 | ALMACÉN FLORIDA | 01 | punto de venta |
| TR01 | TRÁNSITO FLORIDA | 01 | tránsito |

### Hoja `Ubicaciones`

Llave: `bodega` + `codigo`. Cada bodega nace con su ubicación por defecto.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `bodega` | código de bodega | sí | no de tránsito |
| `codigo` | código 10 | sí | único en su bodega |
| `nombre` | texto 60 | sí | |
| `porDefecto` | sí/no | no (no) | al terminar, cada bodega tiene exactamente una por defecto |
| `activa` | sí/no | no (sí) | la por defecto no se inactiva |

| bodega | codigo | nombre | porDefecto |
|---|---|---|---|
| B01 | A-01 | PASILLO A ESTANTE 1 | sí |
| B01 | PATIO | PATIO DE CARGUE | no |

## 8. Tipos de documento

Escribe `INV_DocumentTypes`, `INV_DocumentTypeWarehouses`, `INV_DocumentSequences`, las vigencias de
modo de paso en `COR_ParameterVersions` (ámbito tipo de documento) y las políticas de aprobación en
`COR_ApprovalPolicies`/`COR_ApprovalPolicyLevels` (`Module = Inventory`, `Subject =
DocumentConfirmation`; FR-010, FR-037, FR-038, FR-075). Se pueden
registrar tipos de clases de entregas posteriores (p. ej. los del POS antes de I3): quedan listos, y
confirmar un documento de esa clase espera a su entrega.

### Hoja `TiposDeDocumento`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 120 | sí | |
| `clase` | `DocumentClass` (tabla abajo) | sí | no cambia si el tipo tiene documentos |
| `prefijo` | texto | no | clases fiscales: hasta 4 alfanuméricos (en las numeradas por resolución, el de la resolución, `dian.md` §9); demás: hasta 10. Cambiar el prefijo crea una fila nueva de secuencia desde `vigenciaDelPrefijo` (T16) |
| `siguienteNumero` | entero ≥ 1 | no (1) | sólo en tipos con consecutivo propio (no fiscales y notas); no baja de lo ya emitido; en clases numeradas por resolución debe ir vacío |
| `vigenciaDelPrefijo` | fecha | no (fecha de la importación) | |
| `exigeTercero` · `exigeCentroDeCosto` · `exigeMotivo` · `exigeReferenciaExterna` | sí/no | no | la clase puede forzar alguno (consumo interno exige centro de costo, FR-037) |
| `bodegas` | lista de códigos de bodega o `*` | no (`*`) | bodegas admitidas por la clase (`ClasesDeDocumento`; nunca de tránsito salvo las clases de traslado) |
| `canal` | código de canal de venta | no | sólo clases de venta |
| `retiroGravado` | sí/no | no | sólo consumo interno (FR-037) |
| `ivaNoDescontable` | sí/no | no | sólo clases de compra (FR-044) |
| `permiteFechaFutura` | sí/no | no (no) | |
| `modoDePaso` | `EnLinea`, `PorLotes`, `NoPasa` o vacío | no | vacío = hereda el general. Vigencia de `Contabilidad.ModoDePaso` para el tipo. En clases sin mensajes contables se ignora (aviso) |
| `granularidad` | `PorDocumento`, `Resumido` o vacío | no | `Contabilidad.Granularidad`; sólo tiene efecto con `PorLotes` |
| `disparadorDeLote` | `HoraDiaria`, `CierreDeTurno`, `CierreDePeriodo` o vacío | no | `Contabilidad.DisparadorDeLote` |
| `horaDeLote` | hora | no | `Contabilidad.HoraDeLote` |
| `vigenciaDelModo` | fecha | no (fecha de la importación) | desde cuándo rigen las cuatro anteriores; sin cruces, cierra la vigencia anterior la víspera (T21) |
| `activo` | sí/no | no (sí) | |

Reglas del archivo completo (se evalúan sobre lo que quedaría: archivo + lo existente):

- **Cadenas** (FR-075, T17): todos los tipos de una cadena tienen el mismo `modoDePaso` a la misma
  fecha, o todos lo heredan. Si no, `Inventory.PostingMode.ChainMismatch` nombrando la cadena y los
  tipos. Cadenas: compras (`PurchaseReceipt`, `SupplierInvoice`, `SupplierNote`, `SupportDocument`,
  `SupportDocumentAdjustmentNote`, `SupplierReturn`), ventas (`Shipment`, `SalesInvoice`,
  `SalesInvoiceFromShipments`, `PosEquivalentDocument`, `NonElectronicSalesReceipt`,
  `NonElectronicSalesNote`, `CreditNote`, `PosAdjustmentNote`, `DebitNote`), traslados
  (`TransferDispatch`, `TransferReceipt`).
- **Tipos fiscales sin paso**: dejar `NoPasa` un tipo de clase fiscal (emitida o recibida) exige
  `Inventory.DocumentTypes.DisableFiscalPosting` y una confirmación explícita: la revisión devuelve
  `extra.fiscalTypesWithoutPosting[]` con sus códigos, y `apply` exige repetirlos en
  `confirmFiscalWithoutPosting` (lista separada por comas). Si falta o no coincide, 422
  `Inventory.PostingMode.FiscalRequiresConfirmation`. Queda auditado.
- `SaldoInicial` (`OpeningBalance`) es informativo: su modo de paso se ignora (FR-089).
- `CostAdjustment` lo genera el sistema: su tipo sembrado se puede renombrar o cambiar de prefijo, no
  usar para altas manuales.

Clases (`DocumentClass`) y la etiqueta que acepta la plantilla:

| Valor | Etiqueta | Entrega | Numeración |
|---|---|---|---|
| `PurchaseRequest` · `PurchaseOrder` | Solicitud de compra · Orden de compra | I5 | propia |
| `PurchaseReceipt` | Recepción de compra | I1 | propia |
| `SupplierInvoice` · `SupplierNote` | Factura del proveedor · Nota del proveedor | I1 | propia (el número del proveedor va en el documento) |
| `SupportDocument` | Documento soporte | I4 | resolución DIAN |
| `SupportDocumentAdjustmentNote` | Nota de ajuste al documento soporte | I4 | propia |
| `LandedCost` | Costos adicionales | I5 | propia |
| `SupplierReturn` | Devolución a proveedor | I1 | propia |
| `PositiveAdjustment` · `NegativeAdjustment` | Ajuste positivo · Ajuste negativo | I1 | propia |
| `InternalConsumption` · `WriteOff` · `Assembly` | Consumo interno · Baja · Ensamble (I6) | I1 / I6 | propia |
| `OpeningBalance` | Saldo inicial | I1 | propia |
| `TransferDispatch` · `TransferReceipt` · `LocationMove` | Despacho de traslado · Recepción de traslado · Movimiento entre ubicaciones | I1 | propia |
| `PhysicalCount` · `CostAdjustment` | Conteo · Ajuste de costo | I1 | propia |
| `CashMovement` · `CashCountDifference` | Movimiento de caja · Arqueo con diferencia | I3 | propia |
| `SalesQuote` · `SalesOrder` · `Shipment` | Cotización · Pedido · Remisión | I6 | propia |
| `SalesInvoice` · `SalesInvoiceFromShipments` | Factura de venta · Factura desde remisiones (I6) | I3/I4 | resolución DIAN |
| `PosEquivalentDocument` | Documento equivalente POS | I3/I4 | resolución DIAN |
| `NonElectronicSalesReceipt` · `NonElectronicSalesNote` | Comprobante de venta no electrónico · Nota sobre comprobante no electrónico | I3 | propia (sólo cooperativa no obligada) |
| `CreditNote` · `PosAdjustmentNote` · `DebitNote` | Nota crédito · Nota de ajuste del documento equivalente · Nota débito (I6) | I3/I4 | propia |
| `Voiding` | Anulación | I1 | propia |

### Hoja `NivelesDeAprobacion`

Llave: `tipoDeDocumento` + `nivel`. Las filas de un tipo **reemplazan** su política: si difieren de la
vigente, se crea una versión nueva desde `vigenteDesde` y se cierra la anterior la víspera (pide
`reason`); si son iguales, «sin cambio». Un tipo sin filas en esta hoja conserva su política.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `tipoDeDocumento` | código de tipo | sí | |
| `nivel` | entero 1..n | sí | consecutivos desde 1 |
| `umbral` | monto ≥ 0 | sí | estrictamente creciente con el nivel |
| `permiso` | código del catálogo de permisos | sí | cualquier código existente (C6); sugeridos `Inventory.Approvals.Supervisor` y `Inventory.Approvals.Management` |
| `vigenteDesde` | fecha | sí | la misma para todos los niveles del tipo en el archivo |

La segregación no es parametrizable y no va aquí: no aprueba el creador ni quien ya aprobó otro nivel
(FR-010).

| tipoDeDocumento | nivel | umbral | permiso | vigenteDesde |
|---|---|---|---|---|
| AJN | 1 | 0 | Inventory.Approvals.Supervisor | AAAA-MM-01 |
| AJN | 2 | 5000000 | Inventory.Approvals.Management | AAAA-MM-01 |

## 9. Vendedores

Escribe `INV_Salespeople` y la marca `IsSalesperson` de la persona, **sólo** por la operación de
`CreateSalespersonCommand` (restaura o crea) y `DeleteSalespersonCommand` (FR-031, feature 008).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `documento` | persona | sí | llave; la persona debe existir y estar viva; si fue eliminada, el error dice que se restaura en Personas. **No se crean ni modifican personas** |
| `nombre` | texto | no | informativa: si no coincide con el maestro sale un aviso (documento mal digitado) |
| `tipoDeVendedor` | entero | no | `SalespersonType` |
| `aplicaComision` | sí/no | no (no) | |
| `activo` | sí/no | no (sí) | `no` retira el rol con la operación de retiro; `sí` sobre un rol retirado lo restaura (índice único filtrado a vivos) |

| documento | nombre | tipoDeVendedor | aplicaComision | activo |
|---|---|---|---|---|
| 16000111 | MARÍA PÉREZ | 1 | no | sí |
| 94500222 | JUAN GÓMEZ | | sí | no |

## 10. Puntos de venta y cajas

Escribe `INV_PointsOfSale`, `INV_CashRegisters` e `INV_CashRegisterDocumentTypes` (FR-058). Se
descarga en I1 y se importa en I3.

### Hoja `PuntosDeVenta`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave; no cambia nunca (T27) |
| `nombre` | texto 120 | sí | |
| `sucursal` | sucursal | sí | |
| `canal` | código de canal | sí | el punto fija el canal |
| `posHabilitado` | sí/no | no (sí) | el POS es opcional por punto |
| `bodegaPorDefecto` | código de bodega | sí | operativa y de la misma sucursal |
| `activo` | sí/no | no (sí) | un punto con sesiones abiertas no se inactiva |

### Hoja `Cajas`

Llave: `codigo` (único en la cooperativa, no sólo en su punto).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | |
| `punto` | código de punto | sí | no cambia si la caja tuvo sesiones |
| `nombre` | texto 60 | sí | |
| `bodega` | código de bodega | sí | operativa, de la sucursal del punto |
| `tipoVentaPos` | código de tipo | sí | clase `PosEquivalentDocument` en una cooperativa obligada a facturar electrónicamente a la fecha (`Dian.ObligadaAFacturar`); `NonElectronicSalesReceipt` en una no obligada |
| `tipoFactura` | código de tipo | no | clase `SalesInvoice` (para cuando el comprador pide factura) |
| `tipoNotaVentaPos` | código de tipo | no | nota sobre la venta POS: `PosAdjustmentNote` en una cooperativa obligada, `NonElectronicSalesNote` en una no obligada |
| `tipoNotaCreditoFactura` | código de tipo | con `tipoFactura` | clase `CreditNote`, para las facturas que la caja expide a pedido |
| `tipoContingenciaVentaPos` | código de tipo | no (obligatorio antes de operar I4 en una cooperativa obligada) | clase `PosEquivalentDocument` con el prefijo de una resolución de contingencia con `BacksUpKind = PosEquivalent` (`dian.md` §7.2) |
| `tipoContingenciaFactura` | código de tipo | con `tipoFactura` (antes de operar I4) | clase `SalesInvoice` con el prefijo de una resolución de contingencia con `BacksUpKind = Invoice` (`dian.md` §7.2) |
| `impresion` | `Tirilla80`, `Carta` | no (Tirilla80) | |
| `activa` | sí/no | no (sí) | |

Cada tipo citado debe admitir la bodega de la caja. Las seis columnas de tipo son los roles de
`CashRegisterDocumentRole`, uno por fila de `INV_CashRegisterDocumentTypes` (único por caja y rol):
`PosSale` (`tipoVentaPos`), `InvoiceOnRequest` (`tipoFactura`), `PosAdjustmentNote`
(`tipoNotaVentaPos`), `InvoiceCreditNote` (`tipoNotaCreditoFactura`), `PosSaleContingency`
(`tipoContingenciaVentaPos`) e `InvoiceContingency` (`tipoContingenciaFactura`). Así una caja que expide
el documento equivalente y también factura a pedido tiene nota y contingencia para cada uno.

| codigo | punto | nombre | bodega | tipoVentaPos | tipoFactura | tipoNotaVentaPos | tipoNotaCreditoFactura | tipoContingenciaVentaPos | tipoContingenciaFactura |
|---|---|---|---|---|---|---|---|---|---|
| CJ01 | PV01 | CAJA 1 | PV01 | POS | FV | NAPOS | NC | POSC | FVC |
| CJ02 | PV01 | CAJA 2 | PV01 | POS | FV | NAPOS | NC | POSC | FVC |

## 11. Medios de pago, con franquicias, adquirentes y datáfonos (Core, T25)

Escribe `COR_CardNetworks`, `COR_CardAcquirers`, `COR_CardTerminals`, `COR_PaymentMeans` y la
disponibilidad del módulo (`INV_PaymentMeansPointsOfSale`, `…Channels`, `…DocumentTypes`) (FR-096,
FR-097, FR-101). Se descarga en I1 y se importa en I3, después de puntos y cajas (los datáfonos
citan una caja). La semilla deja `EFECTIVO`; las denominaciones son semilla y no van aquí.

**Nombre y capa**: §2.16 de `decisiones-transversales.md` no nombra este comando;
`ImportPaymentMeansCommand` en `Application/Core/PaymentMeans` es la propuesta. Como la disponibilidad
es del módulo (T25) y viene en el mismo archivo, el comando la escribe en el mismo `SaveChanges` con el
permiso adicional de §0.7; si el plan prefiere que Core no escriba tablas `INV_`, la hoja de
disponibilidad pasa a la plantilla 10 y el orden de carga cambia a medios antes que cajas (con
`cajaPorDefecto` del datáfono en la plantilla 10).

### Hoja `Franquicias`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 60 | sí | |
| `tipoDeTarjeta` | `CardKind`: `Credit` (crédito), `Debit` (débito), `Both` (ambas) | sí | |
| `activa` | sí/no | no (sí) | |

### Hoja `Adquirentes`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 60 | sí | |
| `tercero` | persona | no | la persona del adquirente, para que la cuenta por cobrar lleve tercero |
| `activo` | sí/no | no (sí) | |

### Hoja `Datafonos` (datáfonos de cobro)

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave; el terminal (TER) del comprobante |
| `adquirente` | código de adquirente | sí | |
| `serial` | texto 40 | no | |
| `cajaPorDefecto` | código de caja | no | se propone al cobrar en esa caja |
| `activo` | sí/no | no (sí) | |

### Hoja `MediosDePago`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave; no cambia nunca (T27) |
| `nombre` | texto 60 | sí | «Visa Redeban», «Bono de mercado» |
| `orden` | entero | no (0) | orden en pantalla |
| `teclaRapida` | texto 1 (letra o dígito) | no | única entre los medios activos |
| `clase` | `PaymentMeansClass`: `Cash` (efectivo), `CreditCard`, `DebitCard`, `AssociateCredit` (crédito a asociado), `CustomerCredit` (crédito comercial), `BankDeposit` (consignación), `Transfer`, `Voucher` (bono o vale), `Check`, `Other` | sí | no cambia si el medio tiene pagos |
| `franquicia` | código de franquicia | en tarjetas | tipo compatible (débito no en `CreditCard`) |
| `adquirente` | código de adquirente | en tarjetas | |
| `banco` | banco | en consignación y transferencia | dato del medio, no cuenta contable |
| `cuentaDestino` · `tipoCuentaDestino` | texto 30 · `Ahorros`/`Corriente` | con `banco` | |
| `exigeReferencia` | sí/no | no (según la clase) | |
| `tipoReferencia` | `PaymentReferenceKind`: `Approval`, `Receipt`, `Deposit`, `VoucherNumber`, `CheckNumber`, `Other` | si exige referencia | |
| `largoMinimoReferencia` · `largoMaximoReferencia` | entero | no | |
| `admiteVueltas` | sí/no | no (no) | sólo `Cash` |
| `admitePagoParcial` | sí/no | no (sí) | |
| `referenciaUnica` | sí/no | no (sí en `Voucher`) | un número de bono no se usa dos veces por medio |
| `arqueo` | `CashCountMethod`: `PhysicalCount` (contado físico), `VoucherTotal` (total de comprobantes o lote), `ByReference` (por referencias), `None` | no (según la clase) | combinación validada: el efectivo se arquea, un crédito no se cuenta físicamente |
| `tolerancia` | monto ≥ 0 | no (0) | tolerancia de arqueo del medio; cambiarla en un medio existente pide `reason` (§0.5). Se copia a cada línea del arqueo (`INV_CashCountLines`) al usarse, así el cambio sólo afecta a las sesiones que se cierren después |
| `comisionEsperadaPorcentaje` · `comisionEsperadaValor` | porcentaje · monto | no | informativas (FR-101); se copian a cada pago al registrarse; cambiarlas en un medio existente pide `reason` |
| `codigoDian` | texto 3 | sí | medio de pago del anexo vigente (`CatalogoDian`); sugerido por clase y **pendiente de validar por la contadora** (A8): 10, 48, 49, 42, 47, 20, 71… |
| `plazoDias` · `cuotas` | entero | en clases de crédito | datos del crédito provisional (T32) |
| `periodicidad` | `Mensual`, `Quincenal`, `Semanal` | en clases de crédito | el enum lo fija `data-model.md` |
| `lineaSugerida` | texto 20 | no | línea de Cartera sugerida, sin llave a `LND_*` |
| `puntos` · `canales` · `tiposDeDocumento` | lista o `*` | no (`*`) | dónde se ofrece (`DisponibilidadDeMedio`); `*` fija «todos» en el medio |
| `vigenteDesde` · `vigenteHasta` | fecha | sí · no | |
| `activo` | sí/no | no (sí) | un medio con pagos no se borra: se inactiva |

| codigo | nombre | clase | franquicia | adquirente | arqueo | tolerancia | codigoDian | puntos | vigenteDesde |
|---|---|---|---|---|---|---|---|---|---|
| EFECTIVO | Efectivo | Cash | | | PhysicalCount | 2000 | 10 | * | AAAA-MM-01 |
| VISARB | Visa Redeban | CreditCard | VISA | REDEBAN | VoucherTotal | 0 | 48 | PV01 | AAAA-MM-01 |
| BONOMER | Bono de mercado | Voucher | | | ByReference | 0 | 71 | * | AAAA-MM-01 |

## 12. Listas de precios

Escribe `INV_PriceLists` e `INV_PriceListItems` (FR-053, T51). Se descarga en I1 y se importa en I3.

### Hoja `Listas`

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `codigo` | código 10 | sí | llave |
| `nombre` | texto 120 | sí | |
| `incluyeImpuestos` | sí/no | sí | |
| `cliente` | persona | no | dimensión de ámbito |
| `segmento` | texto | no | un valor existente de la clase del asociado (`AssociateClass`) |
| `canal` | código de canal | no | |
| `sucursal` | sucursal | no | |
| `vigenteDesde` · `vigenteHasta` | fecha | sí · no | |
| `activa` | sí/no | no (sí) | |

Las cuatro dimensiones vacías = lista general. Dos listas con el mismo ámbito exacto (`ScopeKey`) no
pueden tener vigencias que se crucen (edge case). El retiro gravado de consumo interno usa la general
vigente.

### Hoja `Precios`

Llave: `lista` + `producto` + `unidad`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `lista` | código de lista | sí | |
| `producto` | código de producto | sí | activo o inactivo (aviso si inactivo); no bloqueado |
| `unidad` | código de unidad | no (la base) | la base o una unidad de venta del producto |
| `precio` | monto ≥ 0 | sí | con o sin impuestos según la lista |

| lista | producto | unidad | precio |
|---|---|---|---|
| GENERAL | ARZ-001 | | 2100 |
| GENERAL | ARZ-001 | PACA25 | 50000 |
| ASOCIADOS | ARZ-001 | | 1990 |

## 13. Topes de descuento

Escribe `INV_DiscountCaps` (FR-054, T51). Se descarga en I1 y se importa en I3. Llave: `rol` +
`vigenteDesde`.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `rol` | código de `SEC_Roles` de la cooperativa | sí | |
| `topeLineaPorcentaje` | porcentaje 0 a 100 | sí | |
| `topeDocumentoPorcentaje` | porcentaje 0 a 100 | sí | |
| `vigenteDesde` · `vigenteHasta` | fecha | sí · no | una vigencia nueva cierra la anterior del rol la víspera; no se cruzan (pide `reason`) |

Un rol sin fila tiene tope 0; con varios roles, rige el mayor. Cambiar el precio de lista cuenta como
descuento.

| rol | topeLineaPorcentaje | topeDocumentoPorcentaje | vigenteDesde |
|---|---|---|---|
| CAJERO | 5 | 3 | AAAA-MM-01 |
| SUPERVISOR | 15 | 10 | AAAA-MM-01 |

## 14. Saldo inicial

Genera, por bodega, **borradores** de documento `OpeningBalance` fechados en su fecha de corte (FR-089,
US4-1, SC-016). La plantilla nunca confirma: el documento se confirma con aprobación
(`Inventory.OpeningBalance.Approve`). Emite `SaldoInicialCargado` **informativo** al confirmarse: no
genera comprobante, porque ese valor ya está en los libros.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `bodega` | código de bodega | sí | **no activa** (FR-091) y operativa (no de tránsito) |
| `fechaDeCorte` | fecha | sí | la misma en todas las filas de una bodega; queda como `CutoffDate` de la bodega (la víspera de su activación) y debe coincidir si ya estaba fijada |
| `producto` | código de producto | sí | inventariable, con grupo contable, no bloqueado |
| `ubicacion` | código de ubicación de la bodega | no (la por defecto) | |
| `cantidad` | cantidad > 0 | sí | **en unidad base**, con los decimales que admite la unidad |
| `costoUnitario` | costo ≥ 0 | sí | entra al costo cargado (FR-044); 0 se admite con aviso |
| `lote` · `vencimiento` · `serie` | texto 30 · fecha · texto 60 | no | `Import.Cell.NotYetAvailable` hasta I6 |

Reglas:

- Llave: `bodega` + `producto` + `ubicacion` (+ `lote`/`serie` desde I6); repetida en el archivo es
  error, no se suma.
- La cantidad es la del conteo, más o menos los movimientos que la bodega tuvo en SOLIDO entre el conteo
  y el corte (o la bodega deja de operar en SOLIDO desde el conteo hasta su activación; FR-089).
- Una bodega con saldo inicial **confirmado** vigente rechaza la fila (para reemplazarlo, se anula
  primero; la anulación también es informativa). Si tiene un borrador, **volver a importar reemplaza
  sus líneas** conservando el mismo borrador, como la apertura contable de la 009.
- Una bodega de más de 4.000 líneas se parte en varios documentos del mismo corte (T15), para que el
  bloqueo de SQL Server no escale a tabla.
- Valor de cada línea = cantidad × costo unitario, redondeado según `Redondeo.Montos`; el total es la
  suma exacta de las líneas.
- La revisión devuelve en `extra.byWarehouse[]` { bodega, fechaDeCorte, lineas, documentos,
  cantidadTotal, valorTotal, valorPorGrupo[] { grupoContable, valor } }: es el insumo de la comparación
  contra Contabilidad antes de activar (FR-090). Antes de I2 no hay consulta de saldos: la activación
  sin comparación existe sólo fuera de producción; en producción responde 422
  `Inventory.Activation.AccountingUnavailable` hasta que la consulta exista.
- **Excepción de puesta en marcha** (precisión aplicada a la spec; pregunta D8 al dueño, propuesta por
  defecto): el saldo inicial de una bodega todavía `NotActivated` (y su anulación) **no** está sujeto a
  `Costeo.RetroactivosPermitidos`, aunque su fecha de corte sea anterior a movimientos ya registrados
  de los mismos productos en otras bodegas del ámbito de costo (con `Costeo.Ambito = Cooperativa`, lo
  normal al activar bodega por bodega). El motor inserta sus entradas en orden (`OperationDate`, `Id`),
  recalcula las salidas posteriores cuando cambia el promedio y registra `AjusteDeCostoReconocido` por
  cada documento afectado; ese soporte retroactivo mínimo llega en I1 (el retroactivo general sigue en
  I5). Así, aunque `SaldoInicialCargado` sea informativo, los ajustes de costo que provoque sí se
  contabilizan. La revisión no los calcula: los muestra la confirmación.

| bodega | fechaDeCorte | producto | ubicacion | cantidad | costoUnitario |
|---|---|---|---|---|---|
| B01 | AAAA-MM-DD | ARZ-001 | A-01 | 1200 | 1850,000000 |
| B01 | AAAA-MM-DD | FERT-50 | PATIO | 85 | 142300,500000 |

## 15. Cifras de SOLIDO

Escribe `INV_LegacyFigures`: existencias y valores de SOLIDO a una fecha, **sólo informativos**
(FR-091). No mueven existencias ni costo (FR-001). Sirven al ensayo, a los comparativos
(`legacy-comparison-kardex`, `legacy-comparison-valuation`), a la comparación de activación de las
bodegas que siguen en SOLIDO (FR-090) y a la conciliación (FR-081): las bodegas no activas que comparten
cuentas con las activas **entran al valorizado del conjunto** con sus cifras a la fecha de corte, y se
muestran aparte para identificarlas (la diferencia no se les atribuye).

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `fecha` | fecha | sí | fecha de corte de la cifra; un archivo puede traer varias |
| `bodega` | código de bodega | sí | debe existir en el ERP (activa o no) |
| `producto` | código de producto | sí | se guarda tal como viene; si no existe en el catálogo nuevo, la fila exige `grupoContable` y queda «sin producto en el catálogo» (cuenta en el valorizado por grupo, no en el comparativo por producto) |
| `cantidad` | cantidad | sí | en unidad base de SOLIDO; puede ser negativa (se muestra así en el comparativo) |
| `valor` | monto | sí | valor total de la existencia |
| `grupoContable` | código de grupo | si el producto no existe | si el producto existe y el grupo difiere del suyo, aviso |

Reglas: llave `fecha` + `bodega` + `producto`. Cada importación es un lote (`lote de importación`);
importar de nuevo un par (`fecha`, `bodega`) deja la cifra anterior como historia y los comparativos
usan la del lote más reciente. La revisión devuelve `extra.byDateWarehouseGroup[]` con cantidad y valor
por (fecha, bodega, grupo).

| fecha | bodega | producto | cantidad | valor | grupoContable |
|---|---|---|---|---|---|
| AAAA-MM-DD | B02 | ARZ-001 | 640 | 1184000,00 | |
| AAAA-MM-DD | B02 | OBSOL-77 | 3 | 45000,00 | ABARROTES |

## 16. Matriz de reglas contables (Contabilidad, enmienda de la 009)

Escribe `ACC_InventoryPostingRules` con las mismas reglas que crear una a una
(`CreateInventoryPostingRuleCommand` / `AddInventoryPostingRuleVersionCommand`), como
`ImportAccountsCommand` reutiliza las de la cuenta (FR-073, T27). COOFLOPAL la diligencia con su
contadora (D-07) hasta dejar vacío el reporte de completitud (FR-082). Se descarga e importa en I2.

| Columna | Tipo | Oblig. | Reglas |
|---|---|---|---|
| `operacion` | `OperacionesDeInventario`: `Venta`, `CostoDeVenta`, `Compra`, `FacturaProveedor`, `DevolucionAProveedor`, `DevolucionDeCliente`, `NotaCredito`, `NotaDebito`, `AjustePositivo`, `AjusteNegativo`, `ConsumoInterno`, `RetiroGravado`, `Baja`, `Ensamble`, `DespachoTraslado`, `RecepcionTraslado`, `AjusteDeCosto`, `Reclasificacion`, `MovimientoDeCaja`, `DiferenciaDeArqueo` | sí | |
| `rol` | `RolesDeCuenta`: `Inventario`, `Transito`, `Costo`, `Ingreso`, `Descuento`, `Devolucion`, `Impuesto`, `Retencion`, `MedioDePago`, `MercanciaPorFacturar`, `CuentaPorPagar`, `Contrapartida`, `CajaDestino`, `Sobrante`, `Faltante`, `GastoDeArqueo`, `Redondeo` | sí | debe ser un rol de la operación |
| `grupoContable` | código de grupo o `*` | según el rol | obligatorio en `Inventario`, `Costo`, `Ingreso`, `Transito` |
| `bodega` | código de bodega o `*` | no | opcional en los roles de grupo |
| `puntoDeVenta` | código de punto o `*` | no | opcional en `MedioDePago` |
| `medioDePago` | código de medio | según el rol | obligatorio en `MedioDePago` |
| `tarifa` | código de tarifa (`TaxRateCode`) | según el rol | obligatorio en `Impuesto` y `Retencion` |
| `tarifaPorcentaje` | porcentaje | con `tarifa` | se guarda como fracción (9,6) en `TaxRate`; debe ser una tarifa de ese código en el catálogo de Core |
| `causa` | código | según el rol | obligatorio en `Sobrante`, `Faltante`, `GastoDeArqueo` (`Surplus`, `ShortageToCashier`, `ShortageToExpense`); en `Baja` y `AjusteNegativo`, el código de la causa de ajuste (`INV_AdjustmentCauses`) |
| `sucursal` | sucursal o `*` | no | |
| `centroDeCosto` | centro de costo o `*` | no | |
| `cuenta` | cuenta contable | sí | auxiliar de movimiento, activa y habilitada para `INV` (`AccountEligibility.Verificar`); en `Impuesto`/`Retencion`, de clase de impuesto compatible |
| `vigenteDesde` | fecha | sí | |
| `vigenteHasta` | fecha | no | |
| `notas` | texto 400 | no | quién decidió y por qué |

Vacío y `*` son lo mismo: comodín. Los códigos de dimensión se validan contra lo que Inventario publica
por `IDimensionesDeInventario` (Contabilidad no lee tablas `INV_`, T31). Especificidad al resolver:
bodega o punto 16, centro 8, sucursal 4, grupo 2 (§2.7 de `decisiones-transversales.md`).

Reglas:

- Llave: la clave de dimensiones normalizada (`DimensionKey`) + `vigenteDesde`. La misma clave con una
  `vigenteDesde` posterior crea una versión nueva y cierra la anterior la víspera; dos versiones que se
  crucen → `Accounting.InventoryRule.Overlaps`.
- Una versión que empiece en o antes de la fecha del último mensaje ya contabilizado de esa operación
  se rechaza (`Accounting.InventoryRule.RetroactiveOverPosted`): lo pasado se corrige con un
  comprobante manual.
- Una cuenta de impuesto con otra tarifa vigente en esa fecha deja el aviso
  `Accounting.InventoryRule.TaxRateMismatch` (C8); la validación previa lo convierte en bloqueo al
  confirmar un documento.
- Una dimensión que el rol no admite es error nombrando el rol y las dimensiones que sí admite.

La hoja «Instrucciones» se genera de `OperacionesDeInventario` y `RolesDeCuenta` (qué roles exige cada
operación y qué dimensiones admite cada rol), no de este texto.

| operacion | rol | grupoContable | bodega | puntoDeVenta | medioDePago | tarifa | tarifaPorcentaje | causa | cuenta | vigenteDesde |
|---|---|---|---|---|---|---|---|---|---|---|
| Venta | Ingreso | ABARROTES | * | | | | | | 41350501 | AAAA-MM-01 |
| Venta | Impuesto | | | | | IVA19 | 19 | | 24080501 | AAAA-MM-01 |
| Venta | MedioDePago | | | PV01 | EFECTIVO | | | | 11050501 | AAAA-MM-01 |
| CostoDeVenta | Costo | ABARROTES | | | | | | | 61350501 | AAAA-MM-01 |
| CostoDeVenta | Inventario | ABARROTES | | | | | | | 14350101 | AAAA-MM-01 |
| DiferenciaDeArqueo | Faltante | | | | | | | ShortageToExpense | 53959501 | AAAA-MM-01 |

(Los códigos de cuenta de los ejemplos son ilustrativos: salen del plan de cuentas de la cooperativa.)

---

## 17. Lo que este contrato fija y deben recoger los demás artefactos

- `ITabularFileReader` lee una hoja por nombre; `ErrorDeFila` gana `Sheet` (§0.3).
- Códigos nuevos: `Import.ModeRequired`, `Import.Invalid`, `Archivo.HojaFaltante`,
  `Import.Cell.{Required, Format, NotFound, NotYetAvailable, PermissionRequired}`,
  `Import.Row.Duplicate`, avisos `Import.Column.Unknown` e `Import.Barcode.CheckDigit`.
- `ImportResultDto` (§0.5), `?withData=true` (§0.6), `format=xlsx` en la revisión y
  `confirmFiscalWithoutPosting` (§8).
- `ImportPaymentMeansCommand` y su capa (§11), sin nombre en `decisiones-transversales.md`.
- Columnas de condición de `COR_TaxRates` (§1), código de caja único en la cooperativa (§10), los seis
  roles de `CashRegisterDocumentRole` (§10), `periodicidad` de los medios de crédito (§11) y la regla
  única de la bodega de tránsito con su código propuesto (§7): `data-model.md` los congela con estos
  nombres o esta plantilla se ajusta **antes** de publicarse.
- Esta plantilla es la **única fuente** del orden de carga, las hojas, las columnas y la mecánica de
  importación (`ImportResultDto`, `?withData=true`, códigos `Import.*`): `api.md` la cita y no la repite.
