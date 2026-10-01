# Catálogo de mensajes de negocio

**Feature**: 012 | **Fecha**: 2026-09-24 | **Base**: decisiones transversales
(`decisiones-transversales.md`, en la carpeta de la feature) §1.3, §1.4, §2.6, T7 a T12, T26, T29, T32; spec FR-036, FR-062, FR-066, FR-069 a FR-080, FR-084, FR-096 a FR-100 |
**Hermano**: `contabilidad.md` (cómo Contabilidad convierte cada mensaje en comprobante)

Un mensaje de negocio es un **hecho** que Inventario registra al confirmar un documento, o al cerrar,
reabrir o reclasificar. Lleva todo lo que el destino necesita para actuar **sin leer tablas de
Inventario** (FR-014, FR-069). Inventario no conoce cuentas: dice qué pasó, con qué grupo contable,
bodega, medio de pago e impuesto, y la matriz de Contabilidad decide las cuentas.

Tres reglas que no se negocian:

- **Nace con su documento.** `EmisorDeMensajes` agrega el mensaje, sus entregas y sus dependencias
  **sin guardar**, dentro del `SaveChanges` que confirma el documento (paso 9 del flujo canónico).
  No hay documento confirmado sin sus mensajes ni mensaje sin documento (FR-071). Desde I1: las
  entregas a Contabilidad esperan `Pending` hasta que I2 registre su consumidor.
- **No se modifica nunca.** `COR_IntegrationMessages` y `COR_IntegrationMessageDependencies` son
  hechos inmutables (`IHechoInmutable`, `PrincipioXI_ContableImmutable`), y `PayloadSha256` delata
  cualquier alteración del contenido. Lo que cambia es la **entrega** por destino (§11).
- **Un cambio incompatible es otra versión.** Todos nacen en v1 (§12).

Dónde vive: los records en `Application/Common/Integration/Contracts/Inventory/<Type>V1.cs` (también
los de Cartera), el sobre en `IntegrationEnvelopeV1`, los bloques comunes de §5 en la misma carpeta.
Los esquemas JSON (`<Type>.v1.schema.json`, por destino) se generan de los records durante la
implementación y se publican con ellos; no son artefactos de este plan, que no trae más archivos de
mensajes que éste. Este documento es la referencia legible: si un record difiere de
él, el defecto es del record.

## 1. Los veinte tipos

`Type` es la cadena guardada, sin tildes; la pantalla muestra el nombre con tilde («DevoluciónRegistrada»).

| # | `Type` | Destino · `Kind` | Lo emite | Operación de la matriz | Comprobante por defecto |
|---|---|---|---|---|---|
| 1 | `VentaFacturada` | Accounting · Business | factura, factura desde remisiones, documento equivalente POS, comprobante no electrónico | `Venta` | FV |
| 2 | `CostoDeVentaReconocido` | Accounting · Business | factura directa o desde pedido, documento equivalente POS, comprobante no electrónico, remisión | `CostoDeVenta` | FV (SI en la remisión, por mapeo del tipo) |
| 3 | `CompraRecibida` | Accounting · Business | recepción de compra | `Compra` | EI |
| 4 | `FacturaProveedorRegistrada` | Accounting · Business | factura y nota del proveedor, documento soporte y su nota de ajuste | `FacturaProveedor` | CP |
| 5 | `AjusteInventarioAprobado` | Accounting · Business | ajuste positivo y negativo, consumo interno, baja, ensamble, sobrante de traslado | `AjustePositivo`, `AjusteNegativo`, `ConsumoInterno`, `RetiroGravado`, `Baja`, `Ensamble` | EI / SI |
| 6 | `TrasladoDespachado` | Accounting · Business | despacho de traslado | `DespachoTraslado` | TR |
| 7 | `TrasladoRecibido` | Accounting · Business | recepción de traslado | `RecepcionTraslado` | TR |
| 8 | `DevolucionRegistrada` | Accounting · Business | devolución a proveedor; nota crédito, nota de ajuste POS o nota no electrónica con devolución | `DevolucionAProveedor` / `DevolucionDeCliente` | SI / NV |
| 9 | `DocumentoAnulado` | Accounting · el `Kind` del original | anulación con documento contrario (FR-006; casos b y c de FR-066) | las del original | el del original |
| 10 | `AjusteDeCostoReconocido` | Accounting · Business | ajuste de costo, diferencia de precio en factura o nota del proveedor, costos adicionales, devolución a proveedor y anulación de entradas ya promediadas; **uno por documento afectado** | `AjusteDeCosto` | AC |
| 11 | `NotaCreditoEmitida` | Accounting · Business | nota crédito, nota de ajuste POS, nota no electrónica | `NotaCredito` | NV |
| 12 | `NotaDebitoEmitida` | Accounting · Business | nota débito (I6) | `NotaDebito` | NV |
| 13 | `GrupoContableReclasificado` | Accounting · Business | operación `ChangeProductAccountingGroupCommand` (FR-027) | `Reclasificacion` | AC |
| 14 | `MovimientoDeCajaRegistrado` | Accounting · Business | movimiento de caja (FR-100) | `MovimientoDeCaja` | CJ |
| 15 | `DiferenciaDeArqueoAprobada` | Accounting · Business | arqueo con diferencia (FR-099) | `DiferenciaDeArqueo` | CJ |
| 16 | `SaldoInicialCargado` | Accounting · Informational | saldo inicial (FR-089) | — | — (sin comprobante) |
| 17 | `PeriodoInventarioCerrado` | Accounting · Informational | operación `CloseInventoryPeriodCommand` | — | — |
| 18 | `PeriodoInventarioReabierto` | Accounting · Informational | operación `ReopenInventoryPeriodCommand` | — | — |
| 19 | `VentaACreditoRegistrada` | Lending · Business | toda venta con un pago de clase `AssociateCredit` o `CustomerCredit` (uno por pago de crédito) | — | — |
| 20 | `AjusteDeVentaACredito` | Lending · Business | nota, devolución, anulación (incluidos casos b y c) o reemplazo que afecta la parte a crédito | — | — |

**Qué lleva cada uno** (FR-036, sin superposición cuando una clase emite dos):

| Contenido | Mensajes |
|---|---|
| base, descuentos, impuestos, retenciones y pagos por medio, **sin costo** | `VentaFacturada`, `NotaCreditoEmitida`, `NotaDebitoEmitida`, `FacturaProveedorRegistrada` |
| **sólo** cantidades y costo | `CostoDeVentaReconocido`, `DevolucionRegistrada`, `CompraRecibida`, `TrasladoDespachado`, `TrasladoRecibido`, `SaldoInicialCargado` |
| cantidades y costo, **más** base e IVA en el retiro gravado | `AjusteInventarioAprobado` |
| **sólo** la diferencia de costo, partida en existencia y vendido | `AjusteDeCostoReconocido` |
| el contenido del original **con signo contrario** (comercial, de costo o ambos) | `DocumentoAnulado` |
| valores por medio de pago, sin productos | `MovimientoDeCajaRegistrado`, `DiferenciaDeArqueoAprobada` |
| la parte a crédito, sin líneas de producto ni impuestos | `VentaACreditoRegistrada`, `AjusteDeVentaACredito` |

## 2. Qué emite cada clase de documento

La clase fija los mensajes; el tipo de documento no los cambia (FR-037). `+` = en el mismo evento de
confirmación, con otra `originEventKey` cuando la tabla de §10.1 lo indica.

| `DocumentClass` | Mensajes al confirmar |
|---|---|
| `PurchaseRequest`, `PurchaseOrder`, `SalesQuote`, `SalesOrder`, `PhysicalCount`, `LocationMove` | ninguno |
| `PurchaseReceipt` | `CompraRecibida` |
| `SupplierInvoice`, `SupplierNote` | `FacturaProveedorRegistrada` (la nota con su signo) + `AjusteDeCostoReconocido` por cada recepción afectada si el precio difiere |
| `SupportDocument` | `FacturaProveedorRegistrada` |
| `SupportDocumentAdjustmentNote` | `FacturaProveedorRegistrada` con su signo + `AjusteDeCostoReconocido` si cambia el precio de lo recibido (FR-036) |
| `LandedCost` | `AjusteDeCostoReconocido` por cada recepción afectada |
| `SupplierReturn` | `DevolucionRegistrada` + `AjusteDeCostoReconocido` por la diferencia con el promedio |
| `PositiveAdjustment`, `NegativeAdjustment`, `InternalConsumption`, `WriteOff`, `Assembly` | `AjusteInventarioAprobado` (+ `AjusteDeCostoReconocido` por cada documento afectado cuando el ajuste nace de un conteo aprobado y entra antes de salidas ya registradas) |
| `OpeningBalance` | `SaldoInicialCargado` (informativo) + `AjusteDeCostoReconocido` por cada documento afectado cuando entra antes de salidas ya registradas del ámbito de costo (excepción de puesta en marcha, §6.10) |
| `TransferDispatch` | `TrasladoDespachado` |
| `TransferReceipt` | `TrasladoRecibido` |
| `CostAdjustment` | `AjusteDeCostoReconocido` por cada documento afectado |
| `CashMovement` | `MovimientoDeCajaRegistrado` |
| `CashCountDifference` | `DiferenciaDeArqueoAprobada` |
| `Shipment` | `CostoDeVentaReconocido` |
| `SalesInvoice` (directa o desde pedido), `PosEquivalentDocument`, `NonElectronicSalesReceipt` | `VentaFacturada` + `CostoDeVentaReconocido` |
| `SalesInvoiceFromShipments` | `VentaFacturada` (el costo salió con las remisiones) |
| `CreditNote`, `PosAdjustmentNote`, `NonElectronicSalesNote` | `NotaCreditoEmitida` (+ `DevolucionRegistrada` si devuelve mercancía) |
| `DebitNote` | `NotaDebitoEmitida` |
| `Voiding` | `DocumentoAnulado` (+ `AjusteDeCostoReconocido` cuando anular cambia un costo ya promediado) |

Además, en toda clase de venta, nota o anulación: `VentaACreditoRegistrada` por cada pago de clase
crédito, o `AjusteDeVentaACredito` por cada pago de crédito del original que el documento afecta
(FR-036, FR-062). Un documento fiscal ya validado por la DIAN **nunca** emite `DocumentoAnulado`: se
corrige con su nota (FR-066). Las operaciones sin documento emiten `GrupoContableReclasificado`,
`PeriodoInventarioCerrado` y `PeriodoInventarioReabierto`.

## 3. Serialización

- `System.Text.Json`, propiedades en **camelCase**, en inglés.
- **Enums como texto** (`"Business"`, `"PosEquivalentDocument"`). Es otro contrato que el de la API,
  que saca los enums como número: un mensaje puede esperar meses y no puede depender de la
  numeración de un enum.
- **Decimales exactos** (`decimal` de .NET, nunca `double`): montos con 2 decimales (18,2), cantidades
  con 4 (18,4), costos unitarios con 6 (18,6). **Toda tarifa es fracción** (9,6): `0.19`, `0.00966`
  para 9,66 por mil (T19).
- Fechas de operación como `DateOnly` (`"2026-11-14"`), en la hora de Colombia (`HoyLocal`, T20).
  Instantes como `DateTimeOffset` en UTC con `Z`.
- Guid en formato `D` en minúsculas. Toda persona, sucursal, documento y centro de costo viaja por su
  `PublicId` (Principio VI); los catálogos del módulo y de Core, por su **código**, que no cambia una
  vez creado (T27).
- Los campos nulos se escriben (`null`), no se omiten: el esquema de cada versión es estable.
- `PayloadJson` se guarda como texto UTF-8 exacto y `PayloadSha256` es el SHA-256 de esos bytes. El
  consumidor nunca reserializa para verificar: compara contra lo guardado.
- **Signo de los importes.** Todo importe y toda cantidad llevan su signo. Positivo es el efecto
  natural del mensaje (lo que describe su tabla en `contabilidad.md` §3); **negativo es el efecto
  contrario**. Así una nota del proveedor viaja en negativo dentro de `FacturaProveedorRegistrada`, y
  `DocumentoAnulado` repite el contenido del original con los signos invertidos. Los pagos llevan
  `amount` positivo y el sentido en `direction`; en un `DocumentoAnulado` su `amount` también se
  invierte.

## 4. El sobre (`IntegrationEnvelopeV1`)

Es igual para los veinte tipos. Sus campos son columnas inmutables de `COR_IntegrationMessages`; el
contenido de §6 a §8 es `PayloadJson`. El consumidor recibe los dos juntos en `MensajeEntrante`
(§13).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `messageId` | Guid | sí | `COR_IntegrationMessages.PublicId`. Identificador único y clave de idempotencia del destino (FR-016). El `Id bigint` interno es el orden (§9) y no sale del módulo. |
| `type` | string | sí | Uno de los veinte de §1. |
| `version` | int | sí | `1` en todos. |
| `kind` | `IntegrationMessageKind` | sí | `Business` o `Informational`. `DocumentoAnulado` hereda el de su original. |
| `originModule` | string | sí | Siempre `"INV"` (`ModuloContable.Inventario`). |
| `originEventKey` | string | sí | Qué evento del origen lo produjo (§10.1). Con `origin.publicId` y `type` es la clave única de emisión; con `origin.publicId` y el destino, la **unidad de contabilización**. |
| `origin` | objeto | sí | Documento u operación que lo origina (tabla siguiente). |
| `related` | `DocumentRefV1` | no | Documento que éste **anula, corrige, devuelve o ajusta**: el original de una anulación, nota o devolución; el documento afectado de un `AjusteDeCostoReconocido`; el reemplazado en el caso b de FR-066. Nulo en un original. |
| `chainRootPublicId` | Guid | sí | Documento raíz de la cadena de relación: aquél cuyo modo de paso sellado heredó la entrega (FR-075, FR-079). En un original es él mismo; en un derivado, la raíz de su primer origen (todos comparten destino, FR-075). |
| `branchPublicId` | Guid | sí | Sucursal contable (`COR_Branches`) de la operación: la de la bodega o del punto de venta. En operaciones sin sucursal propia (reclasificación, cierre y reapertura), la sucursal principal; sus líneas traen la suya. |
| `costCenterPublicId` | Guid | no | Centro de costo del documento (`COR_CostCenters`), cuando el tipo lo pide o el usuario lo pone. |
| `warehouseCode` | string | no | Bodega principal del documento (la de origen en un traslado). Las líneas traen la suya: éste sólo sirve para filtrar la bandeja. |
| `personPublicId` | Guid | no | Tercero del documento: cliente, proveedor, asociado o, en una diferencia de arqueo, el cajero. En una venta sin identificar, la persona genérica «Consumidor final». Va también por si D-02 exige orden por persona (T32). |
| `currency` | string(3) | sí | `"COP"` (FR-018). |
| `exchangeRate` | decimal(18,6) | sí | `1`. |
| `originUser` | objeto | sí | `{ centralUserId: Guid?, name: string }`: quien confirmó el documento (con aprobación, quien dio la última) u ordenó la operación. Es **dato**, nunca actor, y el procesador no usa sus permisos (FR-083). |
| `emittedAt` | DateTimeOffset | sí | Instante UTC del `SaveChanges` que lo emitió. |

`origin`:

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `kind` | string | sí | `Document` u `Operation`. |
| `documentClass` | `DocumentClass` | con `Document` | Clase fija del documento. |
| `documentTypeCode` | string | con `Document` | Código del tipo (`INV_DocumentTypes.Code`): con él Contabilidad elige tipo de comprobante (`contabilidad.md` §2.6) y agrupa los resumidos. |
| `number` | string | sí | Número visible con prefijo (`"PV01-1532"`). En una operación, su rótulo (`"2026-10"`, o el del cambio de grupo). Es el número del documento cruce de las líneas que lo exigen. |
| `publicId` | Guid | sí | Del documento (`INV_Documents`) o de la operación: `INV_Periods` en cierre y reapertura, `INV_ProductAccountingGroupChanges` en la reclasificación. En un origen `Operation`, qué operación es lo dice el `type`. |
| `operationDate` | DateOnly | sí | Fecha de operación. Es la fecha del comprobante, nunca la del lote (FR-077). |
| `fiscalUniqueCode` | string | no | CUFE, CUDE o CUDS del documento fiscal que emite la cooperativa. El CUFE de la factura de un proveedor va en el contenido. |

**Lo que no va en el sobre, y dónde está:**

- **Cooperativa.** Un mensaje nunca sale de la base de su cooperativa (Principio IV). La cooperativa es
  el contexto que fija `IEjecutorEnCooperativa` antes de entregar, no un campo del contenido; sin
  cooperativa resuelta, la entrega falla y nada se escribe en la auditoría global (FR-083).
- **Modo de paso sellado.** Vive en la **entrega** (`COR_IntegrationMessageDeliveries.Mode`,
  `ScheduleKey`, `BatchScopeKey`), copiado del `INV_Documents.PostingMode` sellado al confirmar o
  heredado del original (T9). El mensaje es el mismo pase o no pase; lo que cambia es su entrega
  (§11).
- **Resultado de la validación previa.** Columna `PrevalidationOutcome` del mensaje (`Postable`,
  `NoResponse`, `NotApplicable`), para medir SC-021. No es contenido de negocio.
- **Documentos de origen de un derivado.** En el contenido, `derivedFrom` (factura desde remisiones,
  factura del proveedor o documento soporte contra recepciones, recepción de traslado). Las aristas de
  orden están en `COR_IntegrationMessageDependencies` (§9).

## 5. Bloques comunes

Records en la misma carpeta, también en v1. Sus nombres los fija este contrato.

**`DocumentRefV1`** — `{ publicId: Guid, documentClass: DocumentClass, number: string }`.

**`UserRefV1`** — `{ centralUserId: Guid?, name: string }`. Usuarios como dato (cajero, aprobador).

**`SalesAmountLineV1`** — importes de venta o nota por grupo contable y bodega. Los precios de listas
que incluyen impuestos se separan antes: aquí todo va sin impuestos.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `accountingGroupCode` | string | sí | Grupo contable del producto (`INV_AccountingGroups.Code`). Dimensión exigida del rol `Ingreso`. |
| `warehouseCode` | string | no | Bodega de la que salió; nula en servicios. Dimensión opcional. |
| `grossAmount` | decimal(18,2) | sí | Cantidad × precio, antes de descuentos. |
| `discountAmount` | decimal(18,2) | sí | Descuentos no condicionados de línea, promociones y la prorrata del descuento por total (FR-054, FR-055). |
| `netAmount` | decimal(18,2) | sí | `grossAmount − discountAmount`: base gravable de esas líneas. |
| `documentLines` | [int] | sí | Números de línea del documento que suman aquí. Con ellos la validación previa dice «la línea» (FR-074). |

**`TaxLineV1`** — un renglón por impuesto y tarifa (y por concepto y municipio en retenciones), con
la foto del motor tributario (`INV_DocumentTaxLines`).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `taxCode` | string | sí | `COR_TaxDefinitions.Code`. |
| `taxKind` | `TaxKind` | sí | `Iva`, `Inc`, `ReteFuente`, `ReteIva`, `ReteIca`, `Ica`, `Other`. |
| `taxRateCode` | string | sí | Código de la tarifa (`COR_TaxRates`). Dimensión exigida de los roles `Impuesto` y `Retencion`. |
| `rate` | decimal(9,6) | no | Tarifa como fracción; nula en los de valor por unidad. La matriz la compara con la de su regla y con la de la cuenta (C8). |
| `amountPerUnit` | decimal(18,2) | no | Valor por unidad (impuesto de bolsas y similares). |
| `taxableUnits` | decimal(18,4) | no | Unidades gravables, en los de valor por unidad. |
| `treatment` | `TaxTreatment` | sí | `Generated`, `Deductible`, `AddedToCost`, `WithholdingApplied`, `WithholdingSuffered`. Decide el lado (`contabilidad.md` §3.5). |
| `taxableBase` | decimal(18,2) | sí | Base (en ReteIVA, el IVA sobre el que se calcula). |
| `amount` | decimal(18,2) | sí | Valor. |
| `withholdingConceptCode` | string | no | Concepto de retención (`COR_WithholdingConcepts.Code`). |
| `municipalityDaneCode` | string(5) | no | Municipio de ICA y ReteICA (T24). |
| `documentLines` | [int] | sí | Líneas del documento que forman la base. |

**`PaymentLineV1`** — un renglón por pago del documento (`INV_DocumentPayments`); el mismo medio
puede repetirse con referencias distintas (FR-097).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `paymentPublicId` | Guid | sí | El pago. |
| `lineNumber` | int | sí | Número del pago en el documento. |
| `paymentMeansCode` | string | sí | `COR_PaymentMeans.Code`. Dimensión exigida del rol `MedioDePago` (FR-098). |
| `paymentMeansClass` | `PaymentMeansClass` | sí | Copia de la clase al confirmar. |
| `direction` | `PaymentDirection` | sí | `Received` en ventas y notas débito; `Refunded` en reintegros de notas. |
| `amount` | decimal(18,2) | sí | Lo aplicado, positivo. En efectivo, sin las vueltas. |
| `reference` | string | no | Referencia normalizada: aprobación, comprobante, consignación, número de bono o cheque. |
| `thirdPartyPersonPublicId` | Guid | no | Tercero natural del pago: la persona del adquirente en tarjetas, la del banco en consignaciones y transferencias, el cliente en créditos. Nulo en efectivo y bonos. |
| `cardNetworkCode`, `cardAcquirerCode`, `cardTerminalCode`, `batchNumber` | string | no | Sólo tarjetas: franquicia, adquirente, datáfono de cobro y lote (FR-101). Nunca el número de la tarjeta. |
| `pendingValidation` | bool | sí | `true` sólo en un pago de crédito provisional (F2). |

**`CostLineV1`** — cantidades y costo por grupo contable y bodega, sumados desde el kardex.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `accountingGroupCode` | string | sí | Grupo contable del producto. |
| `warehouseCode` | string | sí | Bodega del movimiento. |
| `warehouseBehavior` | `WarehouseBehavior` | sí | `Operational` o `Transit`. Una línea en bodega de tránsito resuelve el rol `Transito` en lugar de `Inventario` (Contabilidad no lee `INV_Warehouses`). |
| `branchPublicId` | Guid | no | Sucursal de la bodega, cuando difiere de la del sobre. |
| `movement` | `KardexEntryKind` | sí | `Entry` o `Exit`: la dirección del movimiento en esa bodega. |
| `quantityBase` | decimal(18,4) | sí | Cantidad en unidad base (informativa para Contabilidad). |
| `cost` | decimal(18,2) | sí | Valor al costo (Σ `TotalCost` de las líneas de kardex). |
| `documentLines` | [int] | sí | Líneas del documento. |

**`TransferCostLineV1`** — un traslado mueve valor de una bodega a otra. Rol de cada punta: el que
corresponde a su comportamiento (`Operational` → `Inventario`, `Transit` → `Transito`).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `accountingGroupCode` | string | sí | Grupo contable. |
| `fromWarehouseCode`, `fromWarehouseBehavior`, `fromBranchPublicId` | string, enum, Guid | sí | Punta que sale. |
| `toWarehouseCode`, `toWarehouseBehavior`, `toBranchPublicId` | string, enum, Guid | sí | Punta que entra. |
| `quantityBase` | decimal(18,4) | sí | Cantidad. |
| `cost` | decimal(18,2) | sí | Valor al costo de la línea de despacho (T18). |
| `documentLines` | [int] | sí | Líneas del documento. |

**`CostDifferenceLineV1`** — diferencia de costo por grupo y bodega, partida en existencia y vendido.
Positivo aumenta el valor.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `accountingGroupCode` | string | sí | Grupo contable. |
| `warehouseCode`, `warehouseBehavior` | string, enum | sí | Bodega del kardex afectado. |
| `inventoryAmount` | decimal(18,2) | sí | Diferencia sobre lo que sigue en existencia. |
| `soldAmount` | decimal(18,2) | sí | Diferencia sobre lo ya vendido o consumido (va al costo). |

## 6. Mensajes a Contabilidad

Todo contenido de negocio a Contabilidad lleva `operation` (FR-070, «tipo de operación»): un código
del catálogo fijo `OperacionesDeInventario`. Inventario lo conoce porque la clase lo fija; qué cuentas
le corresponden, no.

### 6.1 `VentaFacturada` v1

Lo emiten `SalesInvoice`, `SalesInvoiceFromShipments`, `PosEquivalentDocument` y
`NonElectronicSalesReceipt` al confirmar. `originEventKey`: `Confirmation`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"Venta"`. |
| `salesChannelCode` | string | no | Canal (`INV_SalesChannels.Code`). |
| `pointOfSaleCode` | string | no | Punto de venta. Dimensión opcional del rol `MedioDePago` (caja de cada punto, FR-098). |
| `cashRegisterCode` | string | no | Caja. |
| `cashSessionPublicId` | Guid | no | Sesión de caja; es la `BatchScopeKey` del disparador `CierreDeTurno`. |
| `derivedFrom` | [`DocumentRefV1`] | sí | Remisiones de una factura desde remisiones; vacío en las demás. |
| `lines` | [`SalesAmountLineV1`] | sí | Ingresos por grupo y bodega. |
| `taxes` | [`TaxLineV1`] | sí | Impuestos `Generated` y retenciones que practica el comprador, `WithholdingSuffered` (T26). Puede ir vacía. |
| `payments` | [`PaymentLineV1`] | sí | Pagos por medio (FR-097, FR-098), todos `Received`. |
| `totals` | objeto | sí | `{ subtotal, discountTotal, taxTotal, withholdingTotal, total, amountDue }` (decimal(18,2)). |

Invariantes que el emisor garantiza y el consumidor verifica (si fallan:
`Accounting.InventoryMessage.Unbalanced`, defecto de Inventario):
`Σ lines.grossAmount = subtotal`; `Σ lines.discountAmount = discountTotal`;
`Σ taxes(Generated).amount = taxTotal`; `Σ taxes(WithholdingSuffered).amount = withholdingTotal`;
`total = subtotal − discountTotal + taxTotal`; `amountDue = total − withholdingTotal`;
`Σ payments.amount = amountDue` (FR-056 se evalúa contra `amountDue`). No lleva costo: el costo viaja
en `CostoDeVentaReconocido`, del mismo evento.

### 6.2 `CostoDeVentaReconocido` v1

Lo emiten `SalesInvoice` (directa o desde pedido), `PosEquivalentDocument`,
`NonElectronicSalesReceipt` y `Shipment`. `originEventKey`: `Confirmation`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"CostoDeVenta"`. |
| `pointOfSaleCode` | string | no | Punto de venta. |
| `cashSessionPublicId` | Guid | no | Sesión de caja. |
| `lines` | [`CostLineV1`] | sí | `movement = Exit`, al costo con que salió cada línea. |

No lleva base ni impuestos. En una factura con el mismo evento, los dos mensajes forman **una**
unidad y salen en **un** comprobante (T11).

### 6.3 `CompraRecibida` v1

Lo emite `PurchaseReceipt` (también la mitad de recepción de la compra directa). `originEventKey`:
`Confirmation`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"Compra"`. |
| `supplierDeliveryReference` | string | no | Remisión o guía del proveedor. |
| `lines` | [`CostLineV1`] | sí | `movement = Entry`, al costo de entrada: neto de descuentos y de impuestos descontables, más los impuestos al costo (FR-044). |

La recepción no conoce la factura: su contrapartida es la cuenta puente de mercancía por facturar
(`contabilidad.md` §3.6), que la factura cancela.

### 6.4 `FacturaProveedorRegistrada` v1

Lo emiten `SupplierInvoice`, `SupplierNote`, `SupportDocument` y `SupportDocumentAdjustmentNote`.
`originEventKey`: `Confirmation`. Una nota crédito del proveedor o una nota de ajuste que reduce
viaja con importes **negativos** (FR-036, «con su signo»).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"FacturaProveedor"`. |
| `supplierDocument` | objeto | sí | `{ kind: Invoice \| CreditNote \| DebitNote \| SupportDocument \| SupportDocumentAdjustmentNote, prefix, number, uniqueCode, issueDate, dueDate, paymentForm: Cash \| Credit, isElectronic }`. En la factura del proveedor, su número y CUFE; en el documento soporte, el número y el CUDS propios. `prefix`, `uniqueCode` y `dueDate` pueden ser nulos. |
| `derivedFrom` | [`DocumentRefV1`] | sí | Recepciones contra las que se registra (FR-075); vacío si no hay recepción (sólo servicios). |
| `lines` | [objeto] | sí | Por grupo, bodega y tipo de línea: `{ accountingGroupCode, warehouseCode, lineKind: Goods \| Service, grossAmount, discountAmount, netAmount, taxAddedToCost, documentLines }`. `taxAddedToCost` es el IVA o INC que fue al costo de esas líneas (FR-044). |
| `taxes` | [`TaxLineV1`] | sí | `Deductible`, `AddedToCost` (informativa: su valor ya está en `taxAddedToCost` de la línea) y `WithholdingApplied`. |
| `totals` | objeto | sí | `{ subtotal, discountTotal, taxTotal, withholdingTotal, total, amountPayable }`. `taxTotal` suma descontables y al costo. |

Invariantes: `Σ netAmount = subtotal − discountTotal`; `total = subtotal − discountTotal + taxTotal`;
`amountPayable = total − withholdingTotal`; `Σ taxAddedToCost = Σ taxes(AddedToCost).amount`. No lleva
costo: una diferencia de precio con la recepción viaja en `AjusteDeCostoReconocido`
(`reason = PriceDifference`), uno por recepción afectada.

### 6.5 `AjusteInventarioAprobado` v1

Lo emiten `PositiveAdjustment`, `NegativeAdjustment`, `InternalConsumption`, `WriteOff` y `Assembly`.
Son de esta familia el ajuste de un conteo, el sobrante aprobado de un traslado (ajuste positivo en
destino) y la baja de un faltante desde tránsito (líneas con `warehouseBehavior = Transit`).
`originEventKey`: `Confirmation`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `AjustePositivo`, `AjusteNegativo`, `ConsumoInterno`, `RetiroGravado` (consumo interno de un tipo con `IsTaxableWithdrawal`), `Baja` o `Ensamble`. |
| `causeCode` | string | con `AjusteNegativo` y `Baja` | Causa (`INV_AdjustmentCauses.Code`, FR-037). Es la `ReasonCode` de la matriz. |
| `sourceDocument` | `DocumentRefV1` | no | Conteo o traslado que lo originó. Informativo: no es dependencia. |
| `lines` | [`CostLineV1`] | sí | `Entry` o `Exit`; el ensamble trae las dos (componentes que salen, kit que entra). |
| `taxableWithdrawal` | objeto | con `RetiroGravado` | `{ priceListPublicId, base, taxes: [TaxLineV1] }`: base al precio de la lista general vigente e IVA `Generated` del producto (FR-037). |

El consumo interno y el retiro gravado exigen centro de costo: va en el sobre.

### 6.6 `TrasladoDespachado` v1

Lo emite `TransferDispatch`. `originEventKey`: `Confirmation`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"DespachoTraslado"`. |
| `transitWarehouseCode` | string | sí | Bodega de tránsito de la sucursal de origen (Supuesto 15). |
| `destinationWarehouseCode` | string | sí | Bodega de destino (informativa). |
| `lines` | [`TransferCostLineV1`] | sí | De la bodega de origen (`Operational`) a la de tránsito (`Transit`). |

### 6.7 `TrasladoRecibido` v1

Lo emite `TransferReceipt`, también la recepción tardía y la devolución al origen con que se resuelve
un faltante (FR-039). Es un derivado de su despacho: no sella modo propio. `originEventKey`:
`Confirmation`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"RecepcionTraslado"`. |
| `derivedFrom` | [`DocumentRefV1`] | sí | El despacho (uno). |
| `lines` | [`TransferCostLineV1`] | sí | De tránsito (`Transit`) a destino (`Operational`), sólo lo recibido. |

### 6.8 `DevolucionRegistrada` v1

Lo emiten `SupplierReturn` y las notas de venta con devolución (`CreditNote`, `PosAdjustmentNote`,
`NonElectronicSalesNote`). `originEventKey`: `Confirmation`. `related`: la recepción o factura
devuelta (a proveedor) o la venta (de cliente).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `DevolucionAProveedor` o `DevolucionDeCliente`. |
| `lines` | [`CostLineV1`] | sí | A proveedor: `Exit` al costo con que entró; de cliente: `Entry` al costo con que salió (FR-044). |

La diferencia entre el costo de entrada y el promedio vigente, en la devolución a proveedor, viaja en
`AjusteDeCostoReconocido` (`reason = VoidDifference`).

### 6.9 `DocumentoAnulado` v1

Lo emite `Voiding` al anular con documento contrario: documentos no fiscales (FR-006), el registro de
documentos recibidos de proveedores, y los casos b y c de FR-066. `originEventKey`: `Confirmation`.
`related`: el anulado. `kind`: el del original (informativo si anula un saldo inicial).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `reason` | string | sí | Motivo de la anulación. |
| `fiscalCase` | string | no | `DianRejectionReplaced` (caso b), `DianRejectionCancelled` (caso c) o nulo. |
| `voidedDocument` | `DocumentRefV1` | sí | Igual a `related`. |
| `voidedDocumentTypeCode` | string | sí | Tipo del anulado: con él Contabilidad elige el tipo de comprobante del original (T28). |
| `voidedContents` | [objeto] | sí | Uno por cada mensaje a Contabilidad del evento `Confirmation` del anulado: `{ messageId, type, version, operationDate, content }`. `operationDate` es la del original (fecha de las reglas, T29); `content` es el contenido original con **todos** los importes y cantidades con signo contrario. |

El documento de anulación copia la sucursal, el centro de costo y el tercero del anulado, así que el
sobre de los dos coincide salvo en `origin`, `related` y `originUser`. Sólo lo emite un documento
cuyo original emitió mensajes a Contabilidad. No repite los
`AjusteDeCostoReconocido` del original: el efecto de costo de anular viaja como
`AjusteDeCostoReconocido` nuevo del documento de anulación (`reason = VoidDifference`). El pago
anulado de un crédito viaja además a Cartera (`AjusteDeVentaACredito`).

### 6.10 `AjusteDeCostoReconocido` v1

Lo emiten `CostAdjustment` (retroactivo, negativo regularizado, residuo de redondeo), `SupplierInvoice`
y `SupplierNote` (diferencia de precio), `SupportDocumentAdjustmentNote`, `LandedCost`,
`SupplierReturn` y `Voiding`; y, con `reason = Retroactive` desde I1, la **excepción de puesta en
marcha** (precisión aplicada a la spec; preguntas D8 y D9 al dueño, propuesta por defecto): el
`OpeningBalance` de una bodega todavía `NotActivated` (y su `Voiding`) y los ajustes que genera un
conteo aprobado no dependen de `Costeo.RetroactivosPermitidos`; el motor inserta sus entradas en orden
(`OperationDate`, `Id`), recalcula las salidas posteriores cuando cambia el promedio y emite una parte
por cada documento afectado. El retroactivo general sigue siendo de I5. **Uno por documento afectado** (FR-045, FR-046, FR-075): cada parte
sigue el destino del mensaje de ese documento. `originEventKey`:
`Confirmation:{affectedDocumentPublicId:N}`. `related`: el documento afectado.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"AjusteDeCosto"`. |
| `reason` | `KardexReason` | sí | `Retroactive`, `PriceDifference`, `LandedCost`, `NegativeRegularization`, `VoidDifference` o `RoundingResidue`. Es la `ReasonCode` de su contrapartida. |
| `effectiveDate` | DateOnly | sí | Fecha de las líneas de ajuste del kardex, y la del comprobante: la de la salida afectada en un retroactivo (T18); la del documento que lo causa en los demás. |
| `affectedDocument` | `DocumentRefV1` | sí | Igual a `related`. |
| `lines` | [`CostDifferenceLineV1`] | sí | Diferencia en existencia y en lo vendido. |

Si `Σ (inventoryAmount + soldAmount)` no es cero, el resto es la contrapartida del ajuste (la cuenta
puente en una diferencia de precio o en costos adicionales; `contabilidad.md` §3.3). El cambio de
método de costeo (I5) no emite: el valorizado es el mismo en la fecha del cambio y el efecto
retrospectivo lo registra Contabilidad (FR-043).

### 6.11 `NotaCreditoEmitida` v1

Lo emiten `CreditNote`, `PosAdjustmentNote` y `NonElectronicSalesNote`. `originEventKey`:
`Confirmation`. `related`: la venta que corrige.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"NotaCredito"`. |
| `isTotalVoid` | bool | sí | Marca de anulación total (FR-066). |
| `withReturn` | bool | sí | Si devuelve mercancía; con devolución, el mismo evento emite `DevolucionRegistrada`. |
| `pointOfSaleCode`, `cashSessionPublicId` | string, Guid | no | Dónde se reintegra (una devolución en efectivo sale de la sesión donde se devuelve). |
| `lines` | [`SalesAmountLineV1`] | sí | Lo que se acredita, por grupo y la bodega de la venta. |
| `taxes` | [`TaxLineV1`] | sí | Reverso de impuestos y retenciones con la foto del original, en proporción y sin volver a probar la base mínima (T22). |
| `payments` | [`PaymentLineV1`] | sí | Reintegros por medio, `direction = Refunded` (por defecto el medio de la venta; otro exige permiso, FR-097). Un reintegro a un medio de crédito reduce esa cuenta por cobrar. |
| `totals` | objeto | sí | Como en `VentaFacturada`; `Σ payments.amount = amountDue`. |

La nota de una factura desde remisiones no mueve existencia (FR-066): va sin `DevolucionRegistrada`.

### 6.12 `NotaDebitoEmitida` v1 (I6)

Lo emite `DebitNote`. `originEventKey`: `Confirmation`. `related`: la venta. Mismo contenido que
`VentaFacturada` (`operation = "NotaDebito"`, sin `derivedFrom`): lo que se carga, sus impuestos y
cómo se cobra (`Received`, habitualmente un medio de crédito).

### 6.13 `GrupoContableReclasificado` v1

Lo emite la operación `ChangeProductAccountingGroupCommand` (FR-027): no pertenece a un documento y
sigue el **valor general** de `Contabilidad.ModoDePaso` (FR-069). `origin.kind = Operation`,
`origin.publicId` = `INV_ProductAccountingGroupChanges.PublicId`. `originEventKey`: `Reclassification`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"Reclasificacion"`. |
| `productPublicId`, `productCode` | Guid, string | sí | Producto. |
| `fromAccountingGroupCode`, `toAccountingGroupCode` | string | sí | Grupo que deja y grupo que toma. |
| `reason` | string | sí | Motivo del cambio. |
| `lines` | [objeto] | sí | Por bodega: `{ warehouseCode, warehouseBehavior, branchPublicId, quantityBase, value }`, a la fecha efectiva (`origin.operationDate`). |

### 6.14 `MovimientoDeCajaRegistrado` v1

Lo emite `CashMovement` (FR-100). `originEventKey`: `Confirmation`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"MovimientoDeCaja"`. |
| `movementKind` | `CashMovementKind` | sí | `WithdrawalToSafe`, `WithdrawalToRegister`, `WithdrawalForDeposit`, `BaseIncome` o `ReclassificationBetweenMeans`. |
| `paymentMeansCode`, `paymentMeansClass` | string, enum | sí | Medio que se mueve (normalmente efectivo); en una reclasificación, el medio que sale. |
| `destinationPaymentMeansCode` | string | con reclasificación | Medio al que se reclasifica. |
| `pointOfSaleCode`, `cashRegisterCode`, `cashSessionPublicId` | string, string, Guid | sí | Caja y sesión donde se registra. |
| `destination` | `CashMovementDestination` | no | La otra punta: `Safe`, `Register` o `Deposit`. En `BaseIncome` nombra de dónde sale la base (`Safe`). Nula en la reclasificación. Es la `ReasonCode` del rol `CajaDestino`. |
| `destinationPointOfSaleCode`, `destinationCashRegisterCode`, `destinationCashSessionPublicId` | string, string, Guid | con `Register` | Caja y sesión que reciben un retiro a otra caja (un solo documento, dos sesiones). |
| `amount` | decimal(18,2) | sí | Valor. |
| `reason` | string | sí | Motivo (FR-100). |

### 6.15 `DiferenciaDeArqueoAprobada` v1

Lo emite `CashCountDifference` al confirmarse: también la aceptada dentro de la tolerancia, sólo con
motivo (T50). Un mensaje por arqueo, con una línea por medio con diferencia. `originEventKey`:
`Confirmation`. `origin.operationDate` es la fecha operativa de la sesión arqueada; `personPublicId`,
la persona del cajero; `originUser`, quien confirmó (la última aprobación, si la hubo).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `operation` | string | sí | `"DiferenciaDeArqueo"`. |
| `pointOfSaleCode`, `cashRegisterCode`, `cashSessionPublicId` | string, string, Guid | sí | Punto, caja y sesión. |
| `cashier` | objeto | sí | `{ centralUserId, personPublicId, name }`. `personPublicId` es obligatorio si alguna línea es `ShortageToCashier` (T50). |
| `approval` | objeto | no | `{ approvalRequestPublicId, approvedBy: UserRefV1, level, method: ApprovalMethod, reason, decidedAt }`; nulo si todo quedó dentro de la tolerancia. |
| `lines` | [objeto] | sí | `{ paymentMeansCode, paymentMeansClass, countMethod: CashCountMethod, expected, counted, difference, toleranceAmount, withinTolerance, treatment: CashDifferenceTreatment, reason }`. `difference = counted − expected`: positivo es sobrante, negativo faltante. `treatment` es `Surplus` en todo sobrante y, en un faltante, el de `Caja.TratamientoFaltante` vigente (`ShortageToCashier` o `ShortageToExpense`). |

## 7. Mensajes informativos

Contabilidad los registra **sin comprobante** y se entregan siempre (`DeliveryMode.Always`), sin
depender del modo de paso ni de validación previa (FR-069, FR-089).

### 7.1 `SaldoInicialCargado` v1

Lo emite `OpeningBalance`. `originEventKey`: `Confirmation`. No contabiliza porque su valor ya está en
los libros (Supuesto 2); sirve a la conciliación y a la activación (FR-081, FR-090). Su anulación es
un `DocumentoAnulado` también informativo. Lo que su inserción cambie en salidas ya registradas de
otras bodegas del ámbito de costo **sí** contabiliza, como `AjusteDeCostoReconocido` de negocio (§6.10).

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `cutoffDate` | DateOnly | sí | Fecha de corte de la bodega (la víspera de su activación). |
| `lines` | [`CostLineV1`] | sí | `Entry`, por grupo y bodega. |

### 7.2 `PeriodoInventarioCerrado` v1

Lo emite `CloseInventoryPeriodCommand` (FR-047). `origin.kind = Operation`, `origin.publicId` =
`INV_Periods.PublicId`, `origin.number` = `"aaaa-mm"`, `origin.operationDate` = último día del mes.
`originEventKey`: `Close:{closingVersion}`, porque un mes puede cerrarse, reabrirse y volver a
cerrarse.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `year`, `month` | int | sí | Período. |
| `closingVersion` | int | sí | Versión del valorizado fijado (`INV_PeriodClosingBalances`). |
| `valuation` | [objeto] | sí | `{ accountingGroupCode, warehouseCode, warehouseBehavior, branchPublicId, quantityBase, value }` fijado al cierre. |
| `unbilledShipments` | [objeto] | sí | Remisiones sin facturar aceptadas: `{ document: DocumentRefV1, personPublicId, amountToInvoice }`, para que el contador cause el ingreso por facturar (Supuesto 12). Vacía si no hay. |
| `acknowledgedPending` | objeto | sí | `{ pending, inBatch, rejected }`: mensajes con fecha en el mes que quedaban sin procesar al cerrar (FR-047 avisa, no bloquea). |

### 7.3 `PeriodoInventarioReabierto` v1

Lo emite `ReopenInventoryPeriodCommand`. Mismo `origin` que el cierre. `originEventKey`:
`Reopen:{closingVersion}`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `year`, `month` | int | sí | Período. |
| `reopenedClosingVersion` | int | sí | Versión del valorizado que queda `Superseded`. |
| `reason` | string | sí | Motivo. |

## 8. Mensajes a Cartera

Destino `Lending`, `DeliveryMode.Always`, `Status = Pending` desde que se emiten, con dependencias
hacia el original (T32). Mientras la entrega IC esté pendiente no hay consumidor registrado: el
despachador los salta sin intentos ni alertas, y la bandeja los muestra «Pendiente — destino aún no
disponible (IC)». Cuando exista Cartera, los recibe en orden y sin duplicar (SC-023). Inventario no
guarda saldos, cuotas ni obligaciones (FR-062).

### 8.1 `VentaACreditoRegistrada` v1

Lo emite toda clase de venta con un pago de clase `AssociateCredit` o `CustomerCredit`, **uno por
pago de crédito** (cada uno será una obligación). `originEventKey`: `Confirmation:{paymentPublicId:N}`.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `thirdPartyKind` | string | sí | `Associate` o `Customer`. |
| `person` | objeto | sí | Foto de la contraparte al confirmar: `{ taxIdType, taxId, name }`. La persona es `personPublicId` del sobre. |
| `creditPayment` | objeto | sí | `{ paymentPublicId, lineNumber, paymentMeansCode, paymentMeansClass, amount }`: el pago de crédito y el valor financiado. |
| `documentTotal`, `amountDue` | decimal(18,2) | sí | Total y total a pagar del documento. |
| `terms` | objeto | sí | `{ termUnit: Days \| Months, term, installments, periodicity: Weekly \| Biweekly \| Monthly \| SinglePayment, firstDueDate, finalDueDate }`. Mientras IC esté pendiente son datos del medio de pago (T32). |
| `creditLineCode` | string | no | Línea de crédito de Cartera; nula mientras IC esté pendiente. |
| `suggestedCreditLineCode` | string | no | Línea sugerida por el medio (texto, sin llave a `LND_*`). |
| `pendingValidation` | bool | sí | Marca «pendiente de validar» (FR-061). Siempre `true` en crédito provisional. |
| `origin` | string | sí | Por qué quedó así: `ProvisionalCredit`, `LendingNoResponse` o `Validated`. No confundir con `origin` del sobre. |
| `approval` | objeto | no | `{ approvalRequestPublicId, approvedBy: UserRefV1, level, method, reason, decidedAt }` cuando la hubo (crédito provisional o Cartera sin respuesta). Nunca el cajero (FR-010). |
| `consultationEvidence` | string | no | Identificador del evento de auditoría con la respuesta de Cartera (FR-060), cuando se consultó (IC). |
| `accountsReceivableRecordedBy` | string | sí | Valor de `Cartera.CuentaPorCobrarRegistradaPor` sellado al confirmar: `Contabilidad` (forzado mientras IC esté pendiente) o `Cartera`. Dice a Cartera si contabiliza la cuenta por cobrar o sólo reclasifica la provisional (FR-062). |
| `pointOfSaleCode`, `salesChannelCode` | string | no | Dónde se vendió. |

### 8.2 `AjusteDeVentaACredito` v1

Lo emite toda nota, devolución, anulación o reemplazo que cambia la parte a crédito de una venta, uno
por pago de crédito afectado. `originEventKey`: `Confirmation:{originalCreditPaymentPublicId:N}`.
`related`: la venta original.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `adjustmentClass` | string | sí | `CreditNote` (nota sin devolución), `Return` (nota con devolución), `DebitNote`, `Voiding` (anulación con documento contrario, FR-006), `VoidingByDianRejection` (casos b y c de FR-066) o `Replacement` (reemplazo del caso b). |
| `amount` | decimal(18,2) | sí | Efecto con signo sobre el valor financiado: negativo reduce (notas crédito, devoluciones, anulaciones), positivo aumenta (nota débito, reemplazo). |
| `originalMessageId` | Guid | sí | `messageId` de la `VentaACreditoRegistrada` que ajusta. |
| `originalCreditPaymentPublicId` | Guid | sí | Pago de crédito del original. |
| `originalDocument` | `DocumentRefV1` | sí | Igual a `related`. |
| `paymentMeansCode` | string | sí | Medio del pago de crédito. |
| `terms` | objeto | no | En `DebitNote` y `Replacement`, las condiciones del valor nuevo (mismo formato que 8.1). |
| `reason` | string | no | Motivo de la nota o de la anulación. |
| `accountsReceivableRecordedBy` | string | sí | El mismo sello del original: nunca cambia dentro de una cadena. |

Una nota que reintegra sólo por medios de contado no afecta la parte a crédito y no emite este
mensaje.

## 9. Orden y dependencias

- **El orden es el `Id bigint`** de `COR_IntegrationMessages` (identidad, se asigna al insertar). Un
  relacionado sólo se confirma después de su original, así que el `Id` es un orden topológico válido.
  Nunca se ordena por Guid ni por fecha. Ese `Id` **nunca sale por HTTP** (Principio VI): la bandeja
  devuelve los mensajes ya en ese orden y muestra `emittedAt`, y el corte de un lote o de un envío
  posterior viaja como `cutoffMessagePublicId` (el `PublicId` del último mensaje listado), que el
  handler traduce al `Id` interno.
- **Unidad.** Los mensajes con el mismo `origin.publicId`, la misma `originEventKey` y el mismo destino
  son una unidad: nacen en el mismo `SaveChanges`, tienen las mismas dependencias, se entregan juntos
  y dan **un** comprobante (T11). `VentaFacturada` + `CostoDeVentaReconocido` son una;
  `NotaCreditoEmitida` + `DevolucionRegistrada`, otra; cada parte de `AjusteDeCostoReconocido` es la
  suya.
- **Aristas** (`COR_IntegrationMessageDependencies`, T9). Al emitir, cada mensaje apunta al último
  mensaje ya emitido de cada cadena de la que depende:
  - su propio documento (eventos anteriores del mismo origen);
  - el original que anula, corrige, devuelve o reemplaza (`related`);
  - cada origen de un derivado (`derivedFrom`);
  - en `AjusteDeCostoReconocido`, el documento afectado.
- **Elegible para un destino** = entrega `Pending` con `NextAttemptAt` vencido y **ninguna**
  dependencia con entrega a ese mismo destino en `Pending`, `InBatch` o `Rejected`. `Processed`,
  `NotApplicable` y `ValidationFailed` no bloquean; una dependencia sin entrega a ese destino tampoco.
  Los elegibles se procesan por `Id` ascendente. Dentro de un lote vale lo mismo para las entregas
  `InBatch` con el `BatchId` del lote, en pasadas sucesivas: cada pasada toma sólo lo que ya tiene
  sus dependencias satisfechas (T12).
- **Qué detiene un fallo** (FR-071): sólo los posteriores de su documento y los de sus relacionados y
  derivados, transitivamente. Entre documentos no relacionados no hay orden: cada comprobante lleva la
  fecha de su documento.
- **Destinos independientes.** Una arista sólo bloquea dentro del destino: una venta rechazada en
  Contabilidad no detiene su `VentaACreditoRegistrada`.
- La bandeja muestra «espera al mensaje X», calculado, no guardado.

## 10. Idempotencia

### 10.1 `originEventKey`

| Valor | Mensajes |
|---|---|
| `Confirmation` | todo mensaje de un documento, salvo los dos siguientes |
| `Confirmation:{affectedDocumentPublicId:N}` | cada parte de `AjusteDeCostoReconocido` |
| `Confirmation:{paymentPublicId:N}` | `VentaACreditoRegistrada` (el pago de crédito) y `AjusteDeVentaACredito` (el pago de crédito del original) |
| `Close:{closingVersion}` · `Reopen:{closingVersion}` | `PeriodoInventarioCerrado` · `PeriodoInventarioReabierto` |
| `Reclassification` | `GrupoContableReclasificado` |

### 10.2 Cuatro capas

1. **Pantalla.** La operación que confirma lleva su clave (`COR_OperationKeys`, `IdempotencyBehavior`,
   T13): repetirla devuelve el mismo resultado y no vuelve a emitir.
2. **Emisión.** Índice único `(OriginPublicId, Type, OriginEventKey)`: una doble emisión es un
   defecto y hace fallar el `SaveChanges` entero.
3. **Entrega.** `RowVersion` de `COR_IntegrationMessageDeliveries`: si dos réplicas registran el
   resultado de la misma entrega, una pierde y relee. El arrendamiento `integration.dispatch` evita
   el trabajo doble, pero la exactitud no depende de él (T10).
4. **Destino.** Recibo único por `messageId`: en Contabilidad, `ACC_InventoryPostings` con índice único
   `MessagePublicId`, escrito en el mismo `SaveChanges` que el comprobante; la colisión del índice se
   traduce a `AlreadyProcessed` con la misma referencia. Cartera tendrá el suyo (IC). Procesar tres
   veces el mismo mensaje produce un solo efecto (SC-002).

Las órdenes de lote manual, reproceso y envío posterior llevan su propia clave de operación.

## 11. Estados de entrega por destino

Una fila de `COR_IntegrationMessageDeliveries` por mensaje y destino (UK `(MessageId, Destination)`;
destinos `IntegrationDestinations.Accounting` y `IntegrationDestinations.Lending`). El mensaje no
cambia; la entrega sí.

**Estado inicial**, según el modo sellado (T9):

| Destino · mensaje | `Mode` | `Status` inicial |
|---|---|---|
| Accounting · de negocio, documento en línea | `Online` | `Pending` |
| Accounting · de negocio, documento por lotes | `Batch` | `InBatch`, con `ScheduleKey` y `BatchScopeKey` (sesión de caja en `CierreDeTurno`, período en `CierreDePeriodo`); `BatchId` nulo hasta que un lote lo toma |
| Accounting · de negocio, documento que no pasa | `NotPosted` | `NotApplicable` |
| Accounting · informativo | `Always` | `Pending` |
| Lending · todos | `Always` | `Pending` |

La `ScheduleKey` identifica disparador, hora y granularidad vigentes al sellar, de modo que todas las
entregas de una clave comparten horario y granularidad. Formato:
`{DocumentTypeCode raíz}|{DisparadorDeLote}|{HoraDeLote}|{Granularidad}`, sellados al confirmar. La
`BatchScopeKey` es `CashSession:{publicId}` (`CierreDeTurno`) o `Period:{aaaa-mm}` (`CierreDePeriodo`).
Los
relacionados y derivados **no leen el parámetro**: copian `Mode` y `ScheduleKey` de la entrega de su
original u origen (FR-075, FR-079; también el reemplazo del caso b, que comparte número fiscal con el
reemplazado). `GrupoContableReclasificado` usa el valor general.

**Estados y transiciones:**

| `DeliveryStatus` | Qué significa | Llega desde | Pasa a |
|---|---|---|---|
| `Pending` | Espera entrega. | emisión (`Online`, `Always`); resultado `Retry`, con `NextAttemptAt` | `Processed`, `Rejected`, o sigue `Pending` |
| `InBatch` | Espera su lote o corre en uno (`BatchId`). | emisión por lotes; orden de lote, reproceso o envío posterior | `Processed`, `Rejected`, o sigue `InBatch` con `NextAttemptAt` si el resultado es `Retry` |
| `Processed` | El destino lo procesó; `ResultReference` es el comprobante (o el recibo de un informativo), y `ResultVoucherTypeCode`/`ResultVoucherNumber` su tipo y número, que da el consumidor. | consumo | `ValidationFailed` (sólo Lending) |
| `Rejected` | Regla de negocio; guarda código y motivo. No se reintenta solo. | consumo | `InBatch` por `ReprocessMessagesCommand` |
| `NotApplicable` | Modo `NoPasa` sellado: se conserva para trazabilidad y conciliación, no se entrega. | emisión | `InBatch` por `SendNotApplicableMessagesCommand` (FR-078) |
| `ValidationFailed` | Sólo Lending: Cartera validó la venta pendiente en contra (FR-061). La venta sigue confirmada. | IC | — |

Bloquean a sus dependientes `Pending`, `InBatch` y `Rejected`. Quién escribe la entrega: el emisor
(al nacer), `RegisterDeliveryResultCommand` (resultado del consumo y la bitácora
`COR_IntegrationDeliveryAttempts`), `OrderIntegrationBatchCommand`, `ReprocessMessagesCommand` y
`SendNotApplicableMessagesCommand`. **El consumidor nunca toca tablas de la plataforma** (T11).

- **Reproceso** (`ReprocessMessagesCommand`, `Inventory.Messages.Reprocess`, con motivo): crea un
  `IntegrationBatch` con `Trigger = Reprocess` y pasa a `InBatch` las entregas rechazadas elegidas
  **y sus dependientes bloqueados**, que se reprocesan juntos (FR-079). Corre con la persona que lo
  ordenó como actor (FR-083).
- **Envío posterior** (`SendNotApplicableMessagesCommand`, `Inventory.Messages.SendNotApplicable`,
  con motivo): `Trigger = SendNotApplicable`; toma los `NotApplicable` de un rango de fechas **y toda
  su clausura de relacionados** (anulaciones, notas, ajustes, derivados y los relacionados de éstos),
  sean de la fecha que sean, con su fecha de operación original (FR-078). Si uno cae en período
  contable cerrado, se rechaza el documento completo a la bandeja.
- **Reintentos** de un fallo transitorio (`Retry`): sin límite, con espera
  `min(15 s·2^(n−1), 15 min)` más dispersión aleatoria; alerta `Integracion.MensajeSinEntregar` a los 3
  intentos o 15 minutos (valores técnicos en `Integration:Retries`). Un `Rejected` levanta
  `Integracion.MensajeRechazado` para los responsables de Inventario y de Contabilidad (FR-080); un
  lote programado que no corrió, `Integracion.LoteNoCorrio`, también para los dos lados
  (`Accounting.InventoryBatches.Run` e `Inventory.Messages.Reprocess`); un `ValidationFailed`,
  `Integracion.ValidacionFallida`.
- **Destino sin consumidor** (Lending hasta IC): se salta, sin intentos ni alertas.

## 12. Versiones

- Todo tipo nace en **v1**. El record `<Type>V1` y su esquema **no cambian** una vez publicados:
  agregar, quitar o renombrar un campo, cambiar un tipo, un signo, un valor admitido de un enum o el
  significado de un campo crea `<Type>V2` (FR-072).
- El emisor escribe una sola versión por tipo a la vez: la vigente en el código que confirma. Lo ya
  emitido **conserva su versión para siempre**; nunca se reescribe (FR-071).
- Cada destino declara lo que acepta (`IDestinoDeMensajes.Acepta(type, version)`; en Contabilidad,
  `VersionesAceptadas`). Una versión no aceptada queda `Rejected` con `Integration.VersionNotAccepted`.
- Al publicar una v2, el consumidor acepta v1 y v2 mientras quede alguna entrega v1 sin procesar (los
  mensajes a Cartera pueden esperar meses, SC-023). Retirar la aceptación de v1 exige cero entregas
  v1 en `Pending`, `InBatch`, `Rejected` o `NotApplicable`.
- Versiones aceptadas hoy: Contabilidad, v1 de los dieciocho tipos 1 a 18; Cartera, las declarará IC
  (se espera v1 de 19 y 20).

## 13. Qué hace cada consumidor

El despachador (`DespachadorDeMensajes`, arrendamiento `integration.dispatch` en la base de cada
cooperativa) entrega por `IEjecutorEnCooperativa`, en un ámbito DI nuevo por unidad, con el actor
«Proceso de integración» (o la persona que ordenó el lote, el reproceso o el envío). El consumidor
recibe `MensajeEntrante`: el sobre, el contenido, `PayloadSha256`, `PrevalidationOutcome` y los datos
de la entrega (`Destination`, `Mode`, `ScheduleKey`, `BatchScopeKey`, `BatchPublicId`, intentos). Lee
contenidos por `IMensajesEntrantes` y responde `ResultadoDeConsumo`: `Processed(ref, tipo de
comprobante, número)` —en Contabilidad, `(Guid AccountingDocumentPublicId, string VoucherTypeCode,
string VoucherNumber)`, nulos los dos últimos en un recibo sin comprobante—, `AlreadyProcessed(ref)`,
`Rejected(código, motivo)` o `Retry(motivo)`. En otro ámbito, el despachador registra el resultado con
`RegisterDeliveryResultCommand`, que guarda los tres en la entrega (`ResultReference`,
`ResultVoucherTypeCode`, `ResultVoucherNumber`): la bandeja de Inventario los lee de ahí y nunca de
tablas `ACC_` (FR-014).

**Contabilidad** (`DestinoContabilidad`, I2): verifica versión, moneda y que el original ya esté
contabilizado; arma las líneas con la matriz (`ConstructorDeLineasDeInventario`); prepara el
comprobante por `AccountingPoster.PrepareAsync` y escribe `ACC_InventoryPostings` en **un**
`SaveChanges` (`PostInventoryMessagesCommand`; en resumido, `PostInventorySummaryGroupCommand`). Los
informativos dejan su recibo sin comprobante. El detalle está en `contabilidad.md`.

**Cartera** (`Lending`, IC, pendiente de D-02). Lo que este contrato le exige, para la especificación
de Cartera:

- recibo único por `messageId` y procesamiento en el orden de §9;
- declarar sus versiones aceptadas;
- crear una obligación por `VentaACreditoRegistrada`, enlazada a la venta por `origin.publicId` y
  `origin.number`, visible desde las dos puntas (SC-003);
- con `accountsReceivableRecordedBy = Contabilidad`, no volver a contabilizar el ingreso ni la cuenta
  por cobrar: a lo sumo reclasificar la provisional (FR-062);
- con `pendingValidation = true`, evaluar estado y cupo **a la fecha de la venta**, sin contarla, y
  dejar el resultado para `IConsultasDeCartera.EstadoDeValidacionAsync`; si es negativo, la entrega
  pasa a `ValidationFailed` y nada se anula solo (FR-061);
- aplicar cada `AjusteDeVentaACredito` a la obligación de `originalMessageId` según su
  `adjustmentClass` (FR-084);
- correr por cooperativa con el actor de FR-083 y sin escribir en la auditoría global.

## 14. Ejemplos

Una venta en el POS el 14 de noviembre a las 8:12 p. m. (hora de Colombia; ya es 15 en UTC): cinco
productos de dos grupos, un descuento de 5.000, IVA del 5 % y del 19 %, pagada con efectivo, una Visa
de Redeban y crédito de asociado. El tipo `DEPOS` pasa **en línea**, así que las dos entregas a
Contabilidad nacen `Pending` y forman una unidad (un FV); la de Cartera nace `Pending`, modo `Always`,
y espera a IC.

### 14.1 `VentaFacturada`

```json
{
  "messageId": "8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4e51",
  "type": "VentaFacturada",
  "version": 1,
  "kind": "Business",
  "originModule": "INV",
  "originEventKey": "Confirmation",
  "origin": {
    "kind": "Document",
    "documentClass": "PosEquivalentDocument",
    "documentTypeCode": "DEPOS",
    "number": "PV01-1532",
    "publicId": "3f0c9d42-5a1b-4e7c-8d21-6b9f0e4a7c13",
    "operationDate": "2026-11-14",
    "fiscalUniqueCode": "9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a"
  },
  "related": null,
  "chainRootPublicId": "3f0c9d42-5a1b-4e7c-8d21-6b9f0e4a7c13",
  "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f",
  "costCenterPublicId": null,
  "warehouseCode": "B01PV",
  "personPublicId": "c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  "currency": "COP",
  "exchangeRate": 1,
  "originUser": { "centralUserId": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f70", "name": "Cajero de ensayo" },
  "emittedAt": "2026-11-15T01:12:09.418Z",
  "payload": {
    "operation": "Venta",
    "salesChannelCode": "MOSTRADOR",
    "pointOfSaleCode": "PTO01",
    "cashRegisterCode": "CAJA03",
    "cashSessionPublicId": "a9b8c7d6-e5f4-4a3b-9c2d-1e0f9a8b7c6d",
    "derivedFrom": [],
    "lines": [
      { "accountingGroupCode": "ABARROTES", "warehouseCode": "B01PV", "grossAmount": 100000.00, "discountAmount": 5000.00, "netAmount": 95000.00, "documentLines": [1, 2, 4] },
      { "accountingGroupCode": "ASEO", "warehouseCode": "B01PV", "grossAmount": 50000.00, "discountAmount": 0.00, "netAmount": 50000.00, "documentLines": [3, 5] }
    ],
    "taxes": [
      { "taxCode": "IVA", "taxKind": "Iva", "taxRateCode": "IVA05", "rate": 0.05, "amountPerUnit": null, "taxableUnits": null, "treatment": "Generated", "taxableBase": 95000.00, "amount": 4750.00, "withholdingConceptCode": null, "municipalityDaneCode": null, "documentLines": [1, 2, 4] },
      { "taxCode": "IVA", "taxKind": "Iva", "taxRateCode": "IVA19", "rate": 0.19, "amountPerUnit": null, "taxableUnits": null, "treatment": "Generated", "taxableBase": 50000.00, "amount": 9500.00, "withholdingConceptCode": null, "municipalityDaneCode": null, "documentLines": [3, 5] }
    ],
    "payments": [
      { "paymentPublicId": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a01", "lineNumber": 1, "paymentMeansCode": "EFECTIVO", "paymentMeansClass": "Cash", "direction": "Received", "amount": 60000.00, "reference": null, "thirdPartyPersonPublicId": null, "cardNetworkCode": null, "cardAcquirerCode": null, "cardTerminalCode": null, "batchNumber": null, "pendingValidation": false },
      { "paymentPublicId": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a02", "lineNumber": 2, "paymentMeansCode": "VISARB", "paymentMeansClass": "CreditCard", "direction": "Received", "amount": 49250.00, "reference": "482913", "thirdPartyPersonPublicId": "f0e1d2c3-b4a5-4968-8776-655443322110", "cardNetworkCode": "VISA", "cardAcquirerCode": "REDEBAN", "cardTerminalCode": "T0457", "batchNumber": null, "pendingValidation": false },
      { "paymentPublicId": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a03", "lineNumber": 3, "paymentMeansCode": "CREDASOC", "paymentMeansClass": "AssociateCredit", "direction": "Received", "amount": 50000.00, "reference": null, "thirdPartyPersonPublicId": "c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "cardNetworkCode": null, "cardAcquirerCode": null, "cardTerminalCode": null, "batchNumber": null, "pendingValidation": true }
    ],
    "totals": { "subtotal": 150000.00, "discountTotal": 5000.00, "taxTotal": 14250.00, "withholdingTotal": 0.00, "total": 159250.00, "amountDue": 159250.00 }
  }
}
```

Cuadra: `150.000 − 5.000 + 14.250 = 159.250` y `60.000 + 49.250 + 50.000 = 159.250`. El comprobante
que resulta está en `contabilidad.md` §3.8.

### 14.2 `CostoDeVentaReconocido`

Mismo sobre salvo `messageId` (`…4e52`) y `type`: misma unidad, mismo comprobante.

```json
{
  "messageId": "8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4e52",
  "type": "CostoDeVentaReconocido",
  "version": 1,
  "kind": "Business",
  "originModule": "INV",
  "originEventKey": "Confirmation",
  "origin": {
    "kind": "Document",
    "documentClass": "PosEquivalentDocument",
    "documentTypeCode": "DEPOS",
    "number": "PV01-1532",
    "publicId": "3f0c9d42-5a1b-4e7c-8d21-6b9f0e4a7c13",
    "operationDate": "2026-11-14",
    "fiscalUniqueCode": "9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a"
  },
  "related": null,
  "chainRootPublicId": "3f0c9d42-5a1b-4e7c-8d21-6b9f0e4a7c13",
  "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f",
  "costCenterPublicId": null,
  "warehouseCode": "B01PV",
  "personPublicId": "c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  "currency": "COP",
  "exchangeRate": 1,
  "originUser": { "centralUserId": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f70", "name": "Cajero de ensayo" },
  "emittedAt": "2026-11-15T01:12:09.418Z",
  "payload": {
    "operation": "CostoDeVenta",
    "pointOfSaleCode": "PTO01",
    "cashSessionPublicId": "a9b8c7d6-e5f4-4a3b-9c2d-1e0f9a8b7c6d",
    "lines": [
      { "accountingGroupCode": "ABARROTES", "warehouseCode": "B01PV", "warehouseBehavior": "Operational", "branchPublicId": null, "movement": "Exit", "quantityBase": 20.0000, "cost": 72400.00, "documentLines": [1, 2, 4] },
      { "accountingGroupCode": "ASEO", "warehouseCode": "B01PV", "warehouseBehavior": "Operational", "branchPublicId": null, "movement": "Exit", "quantityBase": 5.0000, "cost": 31180.50, "documentLines": [3, 5] }
    ]
  }
}
```

### 14.3 `VentaACreditoRegistrada`

Del pago 3 de la misma venta, con crédito provisional (IC pendiente) aprobado por un supervisor desde
su sesión.

```json
{
  "messageId": "8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4e53",
  "type": "VentaACreditoRegistrada",
  "version": 1,
  "kind": "Business",
  "originModule": "INV",
  "originEventKey": "Confirmation:e1f2a3b4c5d64e7f8a9b0c1d2e3f4a03",
  "origin": {
    "kind": "Document",
    "documentClass": "PosEquivalentDocument",
    "documentTypeCode": "DEPOS",
    "number": "PV01-1532",
    "publicId": "3f0c9d42-5a1b-4e7c-8d21-6b9f0e4a7c13",
    "operationDate": "2026-11-14",
    "fiscalUniqueCode": "9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a"
  },
  "related": null,
  "chainRootPublicId": "3f0c9d42-5a1b-4e7c-8d21-6b9f0e4a7c13",
  "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f",
  "costCenterPublicId": null,
  "warehouseCode": "B01PV",
  "personPublicId": "c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
  "currency": "COP",
  "exchangeRate": 1,
  "originUser": { "centralUserId": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f70", "name": "Cajero de ensayo" },
  "emittedAt": "2026-11-15T01:12:09.418Z",
  "payload": {
    "thirdPartyKind": "Associate",
    "person": { "taxIdType": "CC", "taxId": "1000000001", "name": "Asociado de ensayo" },
    "creditPayment": { "paymentPublicId": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a03", "lineNumber": 3, "paymentMeansCode": "CREDASOC", "paymentMeansClass": "AssociateCredit", "amount": 50000.00 },
    "documentTotal": 159250.00,
    "amountDue": 159250.00,
    "terms": { "termUnit": "Months", "term": 3, "installments": 3, "periodicity": "Monthly", "firstDueDate": "2026-12-14", "finalDueDate": "2027-02-14" },
    "creditLineCode": null,
    "suggestedCreditLineCode": "CONSUMOALM",
    "pendingValidation": true,
    "origin": "ProvisionalCredit",
    "approval": {
      "approvalRequestPublicId": "0b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d4e",
      "approvedBy": { "centralUserId": "5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a8b9", "name": "Supervisor de ensayo" },
      "level": 1,
      "method": "OwnSession",
      "reason": "Crédito provisional mientras la integración con Cartera está pendiente",
      "decidedAt": "2026-11-15T01:11:40Z"
    },
    "consultationEvidence": null,
    "accountsReceivableRecordedBy": "Contabilidad",
    "pointOfSaleCode": "PTO01",
    "salesChannelCode": "MOSTRADOR"
  }
}
```

### 14.4 `DiferenciaDeArqueoAprobada`

Cierre de la sesión de esa caja: en efectivo faltan 8.000 (tolerancia 5.000, así que exigió
aprobación; `Caja.TratamientoFaltante = Gasto`), y el lote de la Visa trae 270 de más porque un pago
se registró por 49.250 y el comprobante dice 49.520 (dentro de la tolerancia de 1.000). Un supervisor
lo aprobó en persona con su passkey a la mañana siguiente.

```json
{
  "messageId": "8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4e60",
  "type": "DiferenciaDeArqueoAprobada",
  "version": 1,
  "kind": "Business",
  "originModule": "INV",
  "originEventKey": "Confirmation",
  "origin": {
    "kind": "Document",
    "documentClass": "CashCountDifference",
    "documentTypeCode": "ARQDIF",
    "number": "AD-0045",
    "publicId": "7c8d9e0f-1a2b-4c3d-9e4f-5a6b7c8d9e0f",
    "operationDate": "2026-11-14",
    "fiscalUniqueCode": null
  },
  "related": null,
  "chainRootPublicId": "7c8d9e0f-1a2b-4c3d-9e4f-5a6b7c8d9e0f",
  "branchPublicId": "b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f",
  "costCenterPublicId": null,
  "warehouseCode": null,
  "personPublicId": "2a3b4c5d-6e7f-4a8b-9c0d-1e2f3a4b5c6d",
  "currency": "COP",
  "exchangeRate": 1,
  "originUser": { "centralUserId": "5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a8b9", "name": "Supervisor de ensayo" },
  "emittedAt": "2026-11-15T13:05:22.107Z",
  "payload": {
    "operation": "DiferenciaDeArqueo",
    "pointOfSaleCode": "PTO01",
    "cashRegisterCode": "CAJA03",
    "cashSessionPublicId": "a9b8c7d6-e5f4-4a3b-9c2d-1e0f9a8b7c6d",
    "cashier": { "centralUserId": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f70", "personPublicId": "2a3b4c5d-6e7f-4a8b-9c0d-1e2f3a4b5c6d", "name": "Cajero de ensayo" },
    "approval": {
      "approvalRequestPublicId": "4d5e6f7a-8b9c-4d0e-8f1a-2b3c4d5e6f7a",
      "approvedBy": { "centralUserId": "5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a8b9", "name": "Supervisor de ensayo" },
      "level": 1,
      "method": "InPersonPasskey",
      "reason": "Recontado dos veces; el faltante se confirma",
      "decidedAt": "2026-11-15T13:05:20Z"
    },
    "lines": [
      { "paymentMeansCode": "EFECTIVO", "paymentMeansClass": "Cash", "countMethod": "PhysicalCount", "expected": 1250000.00, "counted": 1242000.00, "difference": -8000.00, "toleranceAmount": 5000.00, "withinTolerance": false, "treatment": "ShortageToExpense", "reason": "Faltante sin explicación al cierre del turno" },
      { "paymentMeansCode": "VISARB", "paymentMeansClass": "CreditCard", "countMethod": "VoucherTotal", "expected": 820500.00, "counted": 820770.00, "difference": 270.00, "toleranceAmount": 1000.00, "withinTolerance": true, "treatment": "Surplus", "reason": "Pago registrado por 49.250; el comprobante del datáfono dice 49.520" }
    ]
  }
}
```

## 15. Lo que este contrato fija por su cuenta

`decisiones-transversales.md` no bajaba a este detalle. Lo que sigue lo decide este documento, en
coherencia con ellas; si `data-model.md` o `plan.md` dicen otra cosa, hay que reconciliar:

1. Los valores de `originEventKey` (§10.1). La consecuencia: cada parte de `AjusteDeCostoReconocido`
   es su propia unidad (un comprobante AC por documento afectado y por su modo), y cada pago de
   crédito, su propio mensaje a Cartera.
2. La convención de signo (§3), `movement` en `CostLineV1` y la forma «de → a» de `TransferCostLineV1`.
3. `operation` en todo contenido de negocio a Contabilidad (lectura literal de FR-070).
4. `documentLines` en los renglones, para que la validación previa nombre la línea (FR-074).
5. `effectiveDate` en `AjusteDeCostoReconocido`: el comprobante de un retroactivo se fecha en la
   salida afectada, como su línea de kardex.
6. `related` sólo para lo que se anula, corrige, devuelve o ajusta; `derivedFrom` para los orígenes de
   un derivado.
7. `DocumentoAnulado` repite los contenidos del evento `Confirmation` del original; los ajustes de
   costo de anular viajan como `AjusteDeCostoReconocido` nuevos (`VoidDifference`).
8. El reemplazo del caso b hereda el modo del reemplazado y emite `AjusteDeVentaACredito`
   (`Replacement`) con el valor nuevo.
9. `thirdPartyPersonPublicId` por pago (adquirente, banco o cliente).
10. En operaciones sin documento, el sobre lleva la sucursal principal y cada línea la suya.
11. La fecha de operación de un arqueo con diferencia es la fecha operativa de su sesión.
12. `periodicity` y `termUnit` del crédito como texto con valores cerrados (no son enums de §2.5).
13. `unbilledShipments` en `PeriodoInventarioCerrado`, para el ingreso por facturar del Supuesto 12.

**Sin resolver**: FR-027 exige grupo contable sólo al producto inventariable, y el rol `Ingreso` lo
exige como dimensión. Un **servicio sin grupo** no tendría regla de ingreso y la validación previa
detendría su venta. Propuesta para el dueño: exigir grupo contable también a los servicios que se
venden o se compran (es un dato del catálogo, sin efecto en el kardex).
