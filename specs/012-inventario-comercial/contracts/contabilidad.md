# Contrato contable de Inventario (enmienda de la 009)

**Feature**: 012 | **Fecha**: 2026-09-24 | **Entrega**: I2 | **Base**: decisiones transversales
(`decisiones-transversales.md`, en la carpeta de la feature) §1.4, §2.2 (`ACC_Inventory*`), §2.7, §2.9,
§2.16, T6, T11, T12, T27 a T31; spec FR-014, FR-069 a FR-083,
FR-090, FR-096 a FR-100, C1 a C3, C7, C8, D-01 | **Hermanos**: `mensajes.md` (qué llega);
`specs/009-contabilidad-niif/contracts/contabilizacion.md` (el contrato que se enmienda)

Lo que Contabilidad construye en esta feature para recibir a Inventario por mensajes. **El único
camino al libro sigue siendo `AccountingPoster`**: lo que cambia para Inventario es quién lo llama y
cuándo. En la 009 cada módulo llama al contrato dentro de su operación, y la operación y su
comprobante quedan juntos o no queda ninguno (FR-036 de la 009). Inventario no: confirma su documento
con sus mensajes (FR-071) y Contabilidad, después, convierte cada mensaje en comprobante con su
matriz. La atomicidad pasa a ser dos, una a cada lado:

- en Inventario, **documento + mensaje** en un `SaveChanges`;
- en Contabilidad, **comprobante + recibo del mensaje** (`ACC_InventoryPostings`) en un `SaveChanges`.

Lo que **no** cambia: las reglas 1 a 11 de `AccountingPoster` y `AccountLineRules`, la numeración, la
inmutabilidad y las pruebas `NingunModuloEscribeMovimientosFueraDelContrato`,
`PrincipioXI_ContableImmutable` y `LaContabilidadNoTieneCuentasEnCodigo`, que siguen verdes sin tocarse.

## 1. Piezas y frontera

| Pieza | Dónde | Qué hace |
|---|---|---|
| `ACC_InventoryPostingRules` · `InventoryPostingRule` | `Domain/Entities/Accounting/Inventory` | La matriz (§2). |
| `ACC_InventoryVoucherMappings` · `InventoryVoucherMapping` | ídem | Operación (y tipo de documento) → tipo de comprobante y de documento cruce (§2.6). |
| `ACC_InventoryPostings` · `InventoryPosting` (hecho inmutable) | `Domain/Entities/Accounting/Transactions` | Recibo único por mensaje procesado (§3.1). |
| `Reglas/{OperacionesDeInventario, RolesDeCuenta, ResolutorDeReglas}` | `Application/Accounting/Inventory` | Catálogos fijos y resolución. |
| `CreateInventoryPostingRuleCommand`, `AddInventoryPostingRuleVersionCommand`, `DeactivateInventoryPostingRuleCommand`, `ImportInventoryPostingRulesCommand`, `GetInventoryRulesTemplateQuery`, `SetInventoryVoucherMappingCommand` | ídem | Administración de la matriz. |
| `Contabilizacion/{ConstructorDeLineasDeInventario, DestinoContabilidad, VersionesAceptadas}`, `PostInventoryMessagesCommand`, `PostInventorySummaryGroupCommand` | ídem | El consumidor (§3, §5). |
| `Lotes/AgrupadorDeResumidos` | ídem | Grupos de un lote resumido (§5.3). |
| `Consultas/{EvaluateInventoryPostingQuery, InventoryAccountBalancesQuery, InventoryRulesCompletenessQuery, PreviewInventoryBatchQuery, PendingInventoryMessagesQuery}` y el adaptador `ContabilidadParaInventario` | ídem | Las cuatro consultas de FR-014 y el aviso de cierre (§4, §7, §8). |
| `IContabilidadParaInventario` | `Application/Common/Integration/Accounting` | Lo único de Contabilidad que Inventario conoce. |
| `IDimensionesDeInventario` | ídem (lo implementa Inventario) | Lo único de Inventario que Contabilidad conoce, además de la bandeja. |

**Frontera** (T31, `InventarioNoConoceContabilidadNiCartera`):

- Inventario no depende de `*.Accounting*` salvo `Application.Common.Integration*`, y la interfaz
  `IContabilidadParaInventario` declara **exactamente** los cuatro métodos de §4.1: es la
  comprobación automática que pide FR-014.
- `Application/Accounting/Inventory` no depende de `Domain.Entities.Inventory`. Lee los mensajes de la
  plataforma (`COR_Integration*`) por `IMensajesEntrantes`; los catálogos de Inventario (grupos
  contables, bodegas con su sucursal y comportamiento, puntos de venta, causas de ajuste, tipos de
  documento y combinaciones en uso) por `IDimensionesDeInventario`; y los de Core (`COR_PaymentMeans`,
  `COR_TaxRates`, `COR_Branches`, `COR_CostCenters`, `COR_People`) directamente.
- Los comandos de consumo no tienen ruta ni piden permisos (`LosComandosDeConsumoNoTienenRuta`).

## 2. La matriz de reglas (`ACC_InventoryPostingRules`)

Parametrizable sin programar y con vigencia (FR-073). Una sola tabla con `Role`: un motor de
resolución, una plantilla, una consulta de completitud y un solo punto de validación (T27).
Reemplaza a las cuentas de producto e IVA del módulo actual (`INV_ProductAccounts`, `INV_VatAccounts`),
que guardaban códigos de cuenta como texto sin validar.

### 2.1 Una regla

| Columna | Qué es |
|---|---|
| `Operation` | Código de `OperacionesDeInventario` (§2.2). |
| `Role` | Código de `RolesDeCuenta` (§2.2). |
| `AccountingGroupCode` | Grupo contable (`INV_AccountingGroups.Code`), o nulo (= cualquiera). |
| `WarehouseCode` | Bodega (`INV_Warehouses.Code`), o nulo. |
| `PointOfSaleCode` | Punto de venta (`INV_PointsOfSale.Code`), o nulo. |
| `PaymentMeansCode` | Medio de pago (`COR_PaymentMeans.Code`), o nulo. |
| `TaxRateCode` · `TaxRate` | Tarifa (`COR_TaxRates`) y su valor como fracción (9,6); nulos fuera de los roles de impuesto. |
| `ReasonCode` | Causa o motivo, según el rol (§2.2), o nulo. |
| `BranchId` | Sucursal (`COR_Branches`), o nulo. |
| `CostCenterId` | Centro de costo (`COR_CostCenters`), o nulo. |
| `AccountId` | Cuenta (`ACC_ChartOfAccounts`, `Restrict`). |
| `ValidFrom` · `ValidTo` | Vigencia; `ValidTo` nulo = abierta. |
| `Notes` | Quién decidió y por qué. |
| `DimensionKey` | Clave normalizada de operación, rol y las diez dimensiones, con `*` donde la regla no fija valor. Índice único filtrado `(DimensionKey, ValidFrom)` entre no borradas: un único con columnas nulas se comporta distinto en SQL Server y PostgreSQL. |

Las dimensiones de Inventario y de Core van **por código**, nunca con llave a tablas `INV_`: así
Contabilidad no acopla su esquema al de Inventario. Sólo sucursal y centro de costo, que son de Core,
llevan llave. Consecuencia para Inventario y Core: **los códigos de grupo contable, bodega, punto de
venta, medio de pago, tarifa y causa de ajuste no cambian una vez creados**; sus pantallas no los
ofrecen editables.

### 2.2 Operaciones, roles y dimensiones

Catálogos fijos en código (datos, no parámetros). Lado = el del importe positivo del mensaje; un
importe negativo lo invierte (`mensajes.md` §3).

| Operación | Mensaje | Débito | Crédito |
|---|---|---|---|
| `Venta` | `VentaFacturada` | `MedioDePago`, `Descuento`, `Retencion` (sufrida) | `Ingreso`, `Impuesto` (generado) |
| `CostoDeVenta` | `CostoDeVentaReconocido` | `Costo` | `Inventario` |
| `Compra` | `CompraRecibida` | `Inventario` | `MercanciaPorFacturar` |
| `FacturaProveedor` | `FacturaProveedorRegistrada` | `MercanciaPorFacturar` (bienes), `Contrapartida` (servicios), `Impuesto` (descontable) | `Retencion` (practicada), `CuentaPorPagar` |
| `DevolucionAProveedor` | `DevolucionRegistrada` | `MercanciaPorFacturar` | `Inventario` |
| `DevolucionDeCliente` | `DevolucionRegistrada` | `Inventario` | `Costo` |
| `NotaCredito` | `NotaCreditoEmitida` | `Devolucion`, `Impuesto` (reverso) | `Descuento` (reverso), `Retencion` (reverso), `MedioDePago` (reintegro) |
| `NotaDebito` | `NotaDebitoEmitida` | `MedioDePago`, `Descuento`, `Retencion` | `Ingreso`, `Impuesto` |
| `AjustePositivo` | `AjusteInventarioAprobado` | `Inventario` | `Contrapartida` |
| `AjusteNegativo`, `Baja` | ídem | `Contrapartida` | `Inventario` |
| `ConsumoInterno` | ídem | `Contrapartida` | `Inventario` |
| `RetiroGravado` | ídem | `Contrapartida` (costo y el IVA) | `Inventario`, `Impuesto` (generado) |
| `Ensamble` | ídem | `Inventario` (kit) | `Inventario` (componentes) |
| `DespachoTraslado` | `TrasladoDespachado` | `Transito` | `Inventario` |
| `RecepcionTraslado` | `TrasladoRecibido` | `Inventario` | `Transito` |
| `AjusteDeCosto` | `AjusteDeCostoReconocido` | `Inventario` y `Costo` (si la diferencia aumenta) | `Contrapartida` o `Redondeo` |
| `Reclasificacion` | `GrupoContableReclasificado` | `Inventario` (grupo nuevo) | `Inventario` (grupo anterior) |
| `MovimientoDeCaja` | `MovimientoDeCajaRegistrado` | según el tipo (§3.3) | según el tipo |
| `DiferenciaDeArqueo` | `DiferenciaDeArqueoAprobada` | `MedioDePago` (sobrante), `Faltante`, `GastoDeArqueo` | `Sobrante`, `MedioDePago` (faltante) |

En todo rol que nombre `Inventario`, una línea en bodega de tránsito (`warehouseBehavior = Transit`)
resuelve `Transito`.

**Dimensiones por rol.** Las **exigidas** se comparan por igualdad exacta y la regla nunca las deja en
`*`; las **opcionales** coinciden por igualdad o con `*`, y suman el peso de especificidad de T27:
bodega o punto 16, centro 8, sucursal 4, grupo 2.

| Rol | Exigidas | Opcionales |
|---|---|---|
| `Inventario`, `Transito`, `Costo`, `Ingreso` | `AccountingGroupCode` | bodega, centro, sucursal |
| `Impuesto`, `Retencion` | `TaxRateCode` + `TaxRate` | centro, sucursal |
| `MedioDePago` | `PaymentMeansCode` | punto de venta, sucursal |
| `Sobrante`, `Faltante`, `GastoDeArqueo` | `ReasonCode` = tratamiento (`Surplus`, `ShortageToCashier`, `ShortageToExpense`) | punto de venta, centro, sucursal |
| `CajaDestino` | `ReasonCode` = la otra punta (`Safe`, `Deposit`) | punto de venta, sucursal |
| `Contrapartida` | en `AjusteNegativo` y `Baja`, `ReasonCode` = causa (`INV_AdjustmentCauses.Code`); en `AjusteDeCosto`, `ReasonCode` = razón (`KardexReason`) | grupo, bodega, centro, sucursal |
| `Descuento`, `Devolucion`, `MercanciaPorFacturar`, `Redondeo` | — | grupo, bodega, centro, sucursal |
| `CuentaPorPagar` | — | sucursal |

`Redondeo` es la contrapartida de los ajustes de costo por residuo de redondeo
(`KardexReason.RoundingResidue`), que dejan en cero el valor de un producto cuya cantidad llegó a cero.

### 2.3 Resolución (`ResolutorDeReglas`)

Para cada renglón del mensaje y cada rol con **importe distinto de cero** (una venta sin descuento no
pide regla de `Descuento`):

1. **Fecha de las reglas**: `origin.operationDate`; en un `AjusteDeCostoReconocido`, su
   `effectiveDate`; en un `DocumentoAnulado`, la `operationDate` de cada contenido anulado (la del
   original, T29).
2. **Valores buscados**: grupo y bodega del renglón; punto de venta, causa, razón, tratamiento o
   destino del contenido; medio del pago; tarifa del impuesto; sucursal del renglón o, si no trae, la
   del sobre; centro de costo del sobre. Los `PublicId` se traducen a `Id` de Core.
3. **Candidatas**: reglas no borradas de la operación y el rol, vigentes en la fecha, cuyas exigidas
   son iguales y cuyas opcionales son iguales o `*`.
4. **Gana la de mayor peso.** No hay empate posible: dos candidatas con el mismo peso fijan el mismo
   conjunto de dimensiones (los pesos son potencias de dos) y, si ambas coinciden, tienen la misma
   `DimensionKey`, cuyas vigencias no se cruzan.
5. **Sin candidata**: `Accounting.InventoryRule.Missing` con `data: { operation, role, values, date,
   lineNumbers }`.
6. **Tarifas** (C8): en `Impuesto` y `Retencion`, si la regla tiene la misma `TaxRateCode` pero otro
   `TaxRate`, o si la tarifa vigente de la cuenta en esa fecha (`CuentaParaReglas.TarifaVigenteDe`)
   difiere de la del mensaje, `Accounting.InventoryRule.TaxRateMismatch` con el impuesto, la cuenta y
   las dos tarifas. Los impuestos de valor por unidad no tienen tarifa y no se comparan.

### 2.4 Vigencia y cambios

- **Crear** abre una clave nueva; **agregar versión** cierra la anterior la víspera (molde de
  `AddPolicyVersionCommand`); **desactivar** fija `ValidTo` con motivo. Nada se borra: la regla vieja
  hace falta para reproducir el espejo de una anulación (§6).
- Dos versiones de la misma clave no se cruzan: `Accounting.InventoryRule.Overlaps`.
- **Sin retroactividad sobre lo contabilizado** (G3): una versión nueva no puede empezar en o antes
  de la fecha de operación más reciente ya contabilizada de su operación
  (`Accounting.InventoryRule.RetroactiveOverPosted`, con esa fecha y el documento). Una regla **nueva**
  (clave que no existía) sí puede empezar antes, si ningún mensaje ya contabilizado de esa operación
  desde su `ValidFrom` tiene un renglón que coincida con su clave; `ResolutorDeReglas` lo comprueba
  sobre el contenido de esos mensajes. Así se rescatan mensajes rechazados o enviados después (FR-078)
  sin cambiar el resultado de nada ya contabilizado. Un mapeo equivocado ya contabilizado se corrige
  con un comprobante manual `CG` de reclasificación.

### 2.5 Qué valida guardar una regla

- La cuenta, con `AccountEligibility.Verificar(cuenta, ModuloContable.Inventario)`: de movimiento,
  activa y habilitada para INV (FR-016 y FR-017 de la 009). Si no, `Accounting.Account.NotEligible`.
- En `Impuesto` y `Retencion`, una cuenta de impuesto de un tipo compatible con el `TaxKind` de la
  tarifa (tabla fija en `RolesDeCuenta`: IVA con `Iva` y `ReteIva`; retención en la fuente con
  `ReteFuente`; ICA con `Ica` y `ReteIca`; `Inc` y `Other` con cualquiera).
- `TaxRate` igual a la tarifa del catálogo para esa `TaxRateCode` en `ValidFrom`. Si la cuenta tiene
  otra tarifa vigente en algún tramo, se guarda con **aviso** y la completitud lo lista (C8).
- Las exigidas del rol presentes; las que el rol no admite, ausentes (el validador responde 400).
- Los códigos existen: los de Inventario, por `IDimensionesDeInventario`; los de Core, en Core; los
  `ReasonCode` fijos (tratamientos, destinos, razones), en su catálogo.

### 2.6 Tipos de comprobante (`ACC_InventoryVoucherMappings`)

Cada fila: `Operation`, `InventoryDocumentTypeCode` opcional (excepción por tipo de documento),
`VoucherTypeId` y `CrossDocumentTypeId`. El tipo de comprobante debe ser `Usage = Module`,
`ModuleCode = INV` y activo; si no, la validación previa y el consumidor responden
`Accounting.VoucherType.NotAllowedForModule`.

- **Resolución**: primero `(operación, código del tipo del documento)`, después `(operación, nulo)`.
  Sin fila, `Accounting.VoucherType.NotFound`.
- **El tipo de la unidad lo da su mensaje principal**, el primero emitido: el comercial antes que el
  de costo (T28). `VentaFacturada` + `CostoDeVentaReconocido` salen en un FV; `NotaCreditoEmitida` +
  `DevolucionRegistrada`, en un NV. `DocumentoAnulado` usa el del original (operación de su primer
  contenido y `voidedDocumentTypeCode`).
- La semilla `InventoryVoucherMappingsSeeder` (Order 84) deja las filas por operación de §9. La
  remisión (I6) necesita su fila por tipo hacia `SI`: la agrega la semilla de I6 que crea su tipo por
  defecto.

### 2.7 Plantilla

`GET /api/accounting/inventory/rules/template.xlsx` (`GetInventoryRulesTemplateQuery`) y
`POST /api/accounting/inventory/rules/import?mode=review|apply` (`ImportInventoryPostingRulesCommand`,
con `ModoDeImportacion`), con la mecánica común de `plantillas.md` §0 (la de `ImportAccountsCommand`
extendida: encabezados en la fila 1 e instrucciones en otra hoja con `PlantillaDeImportacion`, todo o
nada, `ImportResultDto`, 422 `Import.Invalid`), y **las mismas reglas que crear una a una**, porque
reusa las de los comandos de §2.4 y §2.5. Columnas: las de `plantillas.md` §16 (`operacion`, `rol`,
`grupoContable`, `bodega`, `puntoDeVenta`, `medioDePago`, `tarifa`, `tarifaPorcentaje` —en puntos,
guardada como fracción—, `causa`, `sucursal`, `centroDeCosto`, `cuenta`, `vigenteDesde`,
`vigenteHasta`, `notas`). Una fila con una clave que ya existe y una fecha posterior es una versión nueva. COOFLOPAL la
diligencia con su contadora (FR-095).

### 2.8 Pantallas y permisos

`/contabilidad/inventario/matriz` (pestañas por familia de rol: inventario y costo, ventas, compras,
impuestos y retenciones, medios de pago, caja, traslados y puentes; al elegir una cuenta muestra sus
reglas, FR-016 de la 009), `/contabilidad/inventario/tipos-de-comprobante`,
`/contabilidad/inventario/completitud`, `/contabilidad/inventario/lotes` y
`/contabilidad/inventario/lotes/{id:guid}`; aviso de pendientes en `/contabilidad/periodos` (§8).
Permisos nuevos en `AccountingPermissionCatalogSeeder`: `Accounting.InventoryRules.View` y `.Manage`
(matriz, mapeo, plantilla, completitud), `Accounting.InventoryBatches.View` y `.Run` (ver,
previsualizar y ordenar lotes). Todas las pantallas con `PermissionGate` e `IndicadorDeCarga`.

## 3. De mensaje a comprobante

### 3.1 La unidad y el comando

**Unidad de contabilización**: los mensajes de un mismo evento de origen (`origin.publicId` +
`originEventKey`) con destino Contabilidad → **un comprobante por documento** (FR-077, T11).

`PostInventoryMessagesCommand(IReadOnlyList<Guid> MessagePublicIds, Guid? BatchPublicId)`, con
`IReintentableAnteConcurrencia` y validador hermano, sin ruta y sin permisos. Lo envía el despachador
en un ámbito DI propio por unidad. **Dentro** del handler (lo exige el reintento, que descarta el
contexto entero):

1. Lee los mensajes por `IMensajesEntrantes` y verifica que forman una unidad.
2. `VersionesAceptadas` (hoy v1 de los dieciocho tipos a Contabilidad): si no, `Rejected` con
   `Integration.VersionNotAccepted`.
3. Moneda `COP` y tasa 1: si no, `Rejected` con `Accounting.InventoryMessage.CurrencyNotSupported`.
4. Si ya hay recibo de alguno: `AlreadyProcessed` con su referencia.
5. Si depende de un original (`related`, `derivedFrom` o documento afectado) cuyo mensaje a
   Contabilidad no está `Processed`: `Retry` con `Accounting.InventoryMessage.WaitingForOriginal`. Es
   una defensa: las dependencias ya lo impiden (`mensajes.md` §9).
6. Informativo: agrega su recibo sin comprobante y termina (§3.9).
7. Arma el `PostingRequest` con `ConstructorDeLineasDeInventario` (§3.2 a §3.7) y verifica los
   invariantes del contenido (`mensajes.md` §6); si fallan, `Rejected` con
   `Accounting.InventoryMessage.Unbalanced`.
8. Sin líneas distintas de cero: recibo «sin comprobante (valor cero)».
9. `AccountingPoster.PrepareAsync`: valida con las reglas 1 a 11, numera y **agrega sin guardar**. Un
   fallo es `Rejected` con el código de la 009 y sus errores de línea, traducidos a líneas del
   documento.
10. Agrega una fila de `ACC_InventoryPostings` por mensaje: tipo y versión, documento origen y
    relacionado, fecha de operación, operación, `AccountingDocumentId`, `BatchPublicId`, usuario de
    origen y actor.
11. **Un** `SaveChangesAsync`. Si choca contra el índice único de `MessagePublicId` (otra réplica
    ganó), la colisión se traduce a `AlreadyProcessed` (molde `PersonFactory.EsColisionDeDocumento`).

El consumidor **no toca** tablas de la plataforma: el despachador, en otro ámbito, registra el
resultado con `RegisterDeliveryResultCommand` sobre todas las entregas de la unidad.
`ResultadoDeConsumo.Processed` lleva `(Guid AccountingDocumentPublicId, string VoucherTypeCode, string
VoucherNumber)`, y la plataforma los guarda en la entrega (`ResultReference`, `ResultVoucherTypeCode`,
`ResultVoucherNumber` de `COR_IntegrationMessageDeliveries`): así la bandeja de Inventario muestra el
comprobante sin leer ninguna tabla `ACC_` (FR-014).

| Situación | `ResultadoDeConsumo` | Código |
|---|---|---|
| Comprobante guardado | `Processed(PublicId del comprobante, código del tipo, número)` | — |
| Informativo o valor cero | `Processed(PublicId del recibo, nulo, nulo)` | — |
| Recibo existente o colisión del índice | `AlreadyProcessed(la misma referencia)` | — |
| Versión no aceptada | `Rejected` | `Integration.VersionNotAccepted` |
| Moneda o tasa distintas | `Rejected` | `Accounting.InventoryMessage.CurrencyNotSupported` |
| Invariantes del contenido | `Rejected` | `Accounting.InventoryMessage.Unbalanced` |
| Sin regla, tarifa distinta | `Rejected` | `Accounting.InventoryRule.Missing` · `.TaxRateMismatch` |
| Tipo de comprobante sin mapeo o de otro módulo | `Rejected` | `Accounting.VoucherType.NotFound` · `.NotAllowedForModule` |
| Reglas de la 009: contabilidad sin iniciar, período cerrado o inexistente, cuenta, tercero, cruce, centro, base, cuadre | `Rejected` | `Accounting.NotInitialized`, `Accounting.Period.Closed`, `Accounting.Period.NotFound`, `Accounting.Line.*`, `Accounting.Document.Unbalanced`, con `data.errors` |
| Original aún sin contabilizar | `Retry` | `Accounting.InventoryMessage.WaitingForOriginal` |
| Excepción, base no disponible, concurrencia que sobrevive a los reintentos | `Retry` | — |

### 3.2 Reglas comunes del `PostingRequest`

- **`VoucherTypeCode`**: el del mapeo (§2.6).
- **`Date`**: `origin.operationDate` (en `AjusteDeCostoReconocido`, `effectiveDate`). Nunca la del lote
  ni la de hoy (FR-077). Un módulo fecha según su operación (regla 3 de la 009): la fecha futura sólo
  la admite el tipo de documento que la permite.
- **`Description`**: «{clase} {número}» (p. ej. «Documento equivalente POS PV01-1532»); en un
  resumido, «Lote {n} · {tipo} · {sucursal} · {k} documentos».
- **`Origin`**: `AccountingOrigin("INV", "InventoryDocument", origin.publicId)` por documento;
  `AccountingOrigin("INV", "InventoryPostingBatch", lote.PublicId)` en un resumido; en la
  reclasificación, `AccountingOrigin("INV", "InventoryProduct", productPublicId)`.
- **`Kind`**: siempre `Regular`, también en anulaciones, notas y ajustes (T29).
- **`RegistradoPor`**: `UsuarioDeOrigen(originUser.CentralUserId, originUser.Name)` por documento;
  nulo en un resumido, cuyos usuarios de origen quedan en `ACC_InventoryPostings` (§10).
- **Sucursal** de cada línea: la del renglón o la del sobre (`BranchPublicId` → `COR_Branches.Id`).
- **Centro de costo**: el del sobre, sólo en las líneas cuya cuenta lo maneja; si la cuenta lo exige y
  el documento no lo trae, error de la regla 9 que corrige el documento.
- **Tercero**: el natural del rol (tabla siguiente) en toda línea cuya cuenta lo exige, y siempre en
  `MedioDePago`, `CuentaPorPagar`, `Impuesto`, `Retencion`, `MercanciaPorFacturar` y `Faltante`. Si la
  cuenta lo exige y el rol no tiene tercero natural, el del sobre; si tampoco hay, error de la regla 7.
- **Documento cruce**, sólo si la cuenta lo exige: tipo `CrossDocumentTypeId` del mapeo y número
  `origin.number` (en `FacturaProveedorRegistrada`, el número del proveedor). Las líneas que
  **saldan otro documento** cruzan contra él: los reintegros a un medio de crédito de una nota, las
  notas del proveedor y todo el espejo de un `DocumentoAnulado` llevan el tipo y el número del
  original, para que el saldo por tercero y documento (`pending-documents` de la E2) se cancele.
- **`TaxBase`**: en las líneas de impuesto cuya cuenta la exige, la `taxableBase` del renglón.
- **`Detail`**: «{número} · {grupo, medio o tarifa}».
- **Importes**: dos decimales, como vienen; un negativo invierte el lado; se descartan los ceros. Las
  líneas con la misma cuenta, lado, sucursal, centro, tercero y documento cruce se suman; las de
  impuesto, no: una por renglón, con su base.
- **Mapa de líneas**: cada línea contable guarda los `documentLines` de su renglón, para que un error
  diga la línea del documento (FR-074).

| Rol | Tercero natural |
|---|---|
| `MedioDePago` | `thirdPartyPersonPublicId` del pago (adquirente, banco o el cliente del crédito) |
| `CuentaPorPagar`, `MercanciaPorFacturar`, `Impuesto`, `Retencion` | persona del sobre (proveedor o cliente) |
| `Faltante` | `cashier.personPublicId` |
| los demás | ninguno; el del sobre si la cuenta lo exige |

### 3.3 Líneas por mensaje

| Mensaje · operación | Débito | Crédito |
|---|---|---|
| `VentaFacturada` · `Venta` | `MedioDePago` por cada pago; `Descuento` por renglón (`discountAmount`); `Retencion` por cada `WithholdingSuffered` | `Ingreso` por renglón (`grossAmount`); `Impuesto` por cada `Generated`, con base |
| `CostoDeVentaReconocido` · `CostoDeVenta` | `Costo` por renglón | `Inventario` por renglón |
| `CompraRecibida` · `Compra` | `Inventario` por renglón | `MercanciaPorFacturar` |
| `FacturaProveedorRegistrada` · `FacturaProveedor` | `MercanciaPorFacturar` por renglón `Goods` y `Contrapartida` por renglón `Service` (`netAmount + taxAddedToCost`); `Impuesto` por cada `Deductible` | `Retencion` por cada `WithholdingApplied`; `CuentaPorPagar` por `amountPayable` |
| `DevolucionRegistrada` · `DevolucionAProveedor` | `MercanciaPorFacturar` | `Inventario` |
| `DevolucionRegistrada` · `DevolucionDeCliente` | `Inventario` | `Costo` |
| `NotaCreditoEmitida` · `NotaCredito` | `Devolucion` por renglón (`grossAmount`); `Impuesto` por cada `Generated` | `Descuento` por renglón; `Retencion` por cada `WithholdingSuffered`; `MedioDePago` por cada reintegro |
| `NotaDebitoEmitida` · `NotaDebito` | como `Venta` | como `Venta` |
| `AjusteInventarioAprobado` · `AjustePositivo` | `Inventario` | `Contrapartida` |
| ídem · `AjusteNegativo`, `Baja` | `Contrapartida` (con la causa) | `Inventario` |
| ídem · `ConsumoInterno` | `Contrapartida` (con el centro) | `Inventario` |
| ídem · `RetiroGravado` | `Contrapartida` por el costo y por el IVA | `Inventario` por el costo; `Impuesto` por el IVA, con base al precio de lista |
| ídem · `Ensamble` | `Inventario` por los renglones `Entry` | `Inventario` por los renglones `Exit` |
| `TrasladoDespachado`, `TrasladoRecibido` | el rol de la punta `to` (`Inventario` o `Transito`), con su sucursal | el rol de la punta `from`, con su sucursal |
| `AjusteDeCostoReconocido` · `AjusteDeCosto` | `Inventario` por `inventoryAmount` y `Costo` por `soldAmount` (positivos) | la contrapartida por `−Σ(inventoryAmount + soldAmount)`: `Contrapartida` con `ReasonCode = reason`, o `Redondeo` si es `RoundingResidue` |
| `GrupoContableReclasificado` · `Reclasificacion` | `Inventario` del grupo nuevo, por bodega y sucursal | `Inventario` del grupo anterior |
| `DiferenciaDeArqueoAprobada` · `DiferenciaDeArqueo` | sobrante: `MedioDePago`; faltante: `GastoDeArqueo` (`ShortageToExpense`) o `Faltante` (`ShortageToCashier`, con el cajero de tercero) | sobrante: `Sobrante`; faltante: `MedioDePago` |

`MovimientoDeCajaRegistrado` · `MovimientoDeCaja`, por tipo (cada `MedioDePago` con su punto de venta):

| `movementKind` | Débito | Crédito |
|---|---|---|
| `WithdrawalToSafe`, `WithdrawalForDeposit` | `CajaDestino` (`ReasonCode` = `Safe` o `Deposit`) | `MedioDePago` del punto de origen |
| `WithdrawalToRegister` | `MedioDePago` del punto de destino | `MedioDePago` del punto de origen |
| `BaseIncome` | `MedioDePago` del punto | `CajaDestino` (`Safe`) |
| `ReclassificationBetweenMeans` | `MedioDePago` del medio de destino | `MedioDePago` del medio de origen |

Cuadre por construcción: en `Venta`, débitos = `amountDue + discountTotal + withholdingTotal` =
`subtotal + taxTotal` = créditos; en `FacturaProveedor`, débitos = `total` = `withholdingTotal +
amountPayable`. En `AjusteDeCosto` la contrapartida absorbe la suma; en los demás, cada renglón tiene
sus dos lados por el mismo importe.

### 3.4 Pagos por medio de pago (FR-098)

Cada pago llega solo a su cuenta: la matriz la asigna por `PaymentMeansCode` y, si se quiere, por
punto de venta. Un medio activo sin regla vigente aparece en la completitud y **detiene la validación
previa** (SC-024).

| Clase del medio | Cuenta que le asigna la contadora | Tercero | Documento cruce |
|---|---|---|---|
| `Cash`, `Check` | la caja del punto (por punto de venta) | — | — |
| `CreditCard`, `DebitCard` | cuenta por cobrar a la red o al adquirente | persona del adquirente | si la cuenta lo exige |
| `BankDeposit`, `Transfer` | el banco | persona del banco | si la cuenta lo exige |
| `AssociateCredit`, `CustomerCredit` | cartera o cuenta por cobrar **provisional**: exige tercero y documento cruce `FV` (T32) | el cliente | `FV` + número de la venta |
| `Voucher` | la cuenta del bono | — | — |
| `Other` | la que se defina | el del pago, si hay | si la cuenta lo exige |

`Received` va al débito y `Refunded` al crédito. Mientras IC esté pendiente, la cuenta por cobrar la
registra Contabilidad desde `VentaFacturada` (FR-062, primera opción; sello
`accountsReceivableRecordedBy = Contabilidad`). Si D-02 eligiera después que la registre Cartera, la
regla `MedioDePago` de los créditos apuntaría a la cuenta puente que indique la matriz, desde una
fecha, sin cambiar el mensaje. El recaudo del crédito provisional va por fuera, con un comprobante de
la 009 contra el mismo tercero y documento cruce.

### 3.5 Impuestos y retenciones

| `treatment` | Rol | Lado (importe positivo) | Dónde |
|---|---|---|---|
| `Generated` | `Impuesto` | crédito (débito en la nota crédito) | venta, nota débito, retiro gravado |
| `Deductible` | `Impuesto` | débito | factura y notas del proveedor, documento soporte |
| `AddedToCost` | — | no se contabiliza aparte: su valor va en `taxAddedToCost` del renglón | compras |
| `WithholdingApplied` | `Retencion` | crédito (retención por pagar) | compras |
| `WithholdingSuffered` | `Retencion` | débito (anticipo de impuestos) | venta a un agente retenedor (T26) |

- Una línea por renglón de impuesto, con `TaxBase` cuando la cuenta la exige, y la regla 10 de la 009
  (`|importe − base × tarifa de la cuenta| ≤ TaxTolerance`) sin cambios.
- Tercero: la persona del sobre (la exógena lo pide en IVA descontable y retenciones).
- **Impuestos por unidad** (bolsas y similares): su cuenta va **sin** «exige base gravable», porque
  base × tarifa no aplica (pregunta E11 del dueño, propuesta por defecto). La completitud avisa si no.
- **ReteICA y varias tarifas de retención**: cada municipio, actividad o tarifa es una `TaxRateCode`
  distinta, así que la contadora puede llevar cada una a su auxiliar (una tarifa vigente por cuenta y
  fecha, como exige la 009).

### 3.6 Tránsito y cuentas puente

- **Tránsito.** La mercancía despachada queda en la bodega de tránsito de la sucursal de origen, y su
  valor en la cuenta del rol `Transito`: el despacho la carga, la recepción la descarga, la baja de un
  faltante desde tránsito la descarga contra su causa. Su saldo es lo que está en camino, y la
  conciliación lo muestra como columna propia (FR-081). Un traslado entre sucursales deja cada línea
  en la sucursal de su bodega.
- **Mercancía por facturar.** `CompraRecibida` la acredita al costo; la factura del proveedor la
  debita por su valor neto más lo que fue al costo; la devolución a proveedor la debita; la nota del
  proveedor, en negativo, la acredita; la diferencia de precio (`AjusteDeCostoReconocido`,
  `PriceDifference`) la cancela por su contrapartida, que la contadora mapea a esta misma cuenta. Su
  saldo es lo recibido y no facturado. Conviene que **no exija documento cruce**: la recepción no
  conoce el número de la factura.
- **Costos por distribuir.** El flete o seguro llega en una factura del proveedor de servicio: su
  renglón `Service` va a la `Contrapartida` del grupo del servicio, que la contadora mapea a una
  cuenta de costos por distribuir; el prorrateo (`LandedCost`, I5) la cancela con su contrapartida
  (`ReasonCode = LandedCost`).
- **Puente de Cartera**: sólo si D-02 elige que Cartera registre la cuenta por cobrar (§3.4).

### 3.7 El espejo de `DocumentoAnulado`

Por cada contenido anulado (`voidedContents`), el constructor arma las líneas **como las del tipo
original**, con las reglas vigentes en la fecha del original, y como los importes vienen con signo
contrario, cada lado se invierte. Tercero, sucursal, centro y documento cruce son los del original.
Así las cuentas de balance (inventario, tránsito, puentes, cuentas por cobrar) quedan en cero aunque
la matriz haya cambiado después (G2). El comprobante lleva el tipo del original y **la fecha del
documento de anulación**; si esa fecha cae en período cerrado, el mensaje se rechaza a la bandeja: la
fecha nunca se mueve (§6).

### 3.8 Ejemplo: la venta de `mensajes.md` §14

Una unidad (`VentaFacturada` + `CostoDeVentaReconocido`) → un comprobante FV, fecha 2026-11-14,
origen `INV · InventoryDocument · 3f0c9d42…`, registrado por «Cajero de ensayo», contabilizado por
«Proceso de integración». Los nombres de cuenta son de ejemplo: los decide la matriz.

| # | Rol · dimensión | Cuenta (ejemplo) | Débito | Crédito | Tercero | Cruce | Base |
|---|---|---|---|---|---|---|---|
| 1 | `MedioDePago` · EFECTIVO · PTO01 | Caja del punto PTO01 | 60.000,00 | | | | |
| 2 | `MedioDePago` · VISARB | Por cobrar a Redeban | 49.250,00 | | Redeban | | |
| 3 | `MedioDePago` · CREDASOC | Por cobrar provisional a asociados | 50.000,00 | | el asociado | FV PV01-1532 | |
| 4 | `Descuento` · ABARROTES | Descuentos en ventas | 5.000,00 | | | | |
| 5 | `Ingreso` · ABARROTES | Ventas de abarrotes | | 100.000,00 | | | |
| 6 | `Ingreso` · ASEO | Ventas de aseo | | 50.000,00 | | | |
| 7 | `Impuesto` · IVA05 · 0,05 | IVA generado 5 % | | 4.750,00 | el asociado | | 95.000,00 |
| 8 | `Impuesto` · IVA19 · 0,19 | IVA generado 19 % | | 9.500,00 | el asociado | | 50.000,00 |
| 9 | `Costo` · ABARROTES | Costo de ventas abarrotes | 72.400,00 | | | | |
| 10 | `Inventario` · ABARROTES · B01PV | Inventario de abarrotes | | 72.400,00 | | | |
| 11 | `Costo` · ASEO | Costo de ventas aseo | 31.180,50 | | | | |
| 12 | `Inventario` · ASEO · B01PV | Inventario de aseo | | 31.180,50 | | | |
| | | **Sumas** | **267.830,50** | **267.830,50** | | | |

Si el tipo `DEPOS` pasara **por lotes resumidos**, las líneas 1, 2, 4, 5, 6 y 9 a 12 se sumarían con
las de las demás ventas del día, tipo y sucursal; la 3 (tercero y cruce) y las 7 y 8 (base) quedarían
con el detalle de este documento (§5.3).

### 3.9 Mensajes informativos

`SaldoInicialCargado`, `PeriodoInventarioCerrado`, `PeriodoInventarioReabierto` y el `DocumentoAnulado`
de un saldo inicial dejan su recibo en `ACC_InventoryPostings` con `AccountingDocumentId` nulo, sin
validación de reglas ni comprobante. El saldo inicial no contabiliza porque su valor ya está en los
libros por la apertura (Supuesto 2). Lo que sí contabiliza es lo que su inserción provoque: por la
excepción de puesta en marcha (el saldo inicial de una bodega `NotActivated` y su anulación no dependen
de `Costeo.RetroactivosPermitidos`; lo mismo los ajustes de un conteo aprobado, fechados en la foto), el
motor recalcula las salidas posteriores del ámbito de costo y emite un `AjusteDeCostoReconocido` por
documento afectado, que llega aquí como cualquier otro (§3.3, fecha `effectiveDate`). El cierre de período se muestra en `/contabilidad/periodos` con el
valorizado por grupo y la lista de remisiones sin facturar, para que el contador cause el ingreso por
facturar con un `CG` (Supuesto 12).

## 4. Validación previa: «¿es contabilizable?»

### 4.1 Contrato

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Accounting;

public interface IContabilidadParaInventario
{
    // FR-074: ¿las líneas que generarían estos mensajes cumplen las reglas y está abierto el período?
    Task<Result<ResultadoDeContabilizacionDto>> EvaluarAsync(
        IReadOnlyList<MensajeContableDto> mensajes, CancellationToken ct);

    // FR-081, FR-090: saldo contable de las cuentas mapeadas, por conjunto, a una fecha.
    Task<Result<IReadOnlyList<ConjuntoDeCuentasDto>>> SaldosDeCuentasMapeadasAsync(
        DateOnly corte, CancellationToken ct);

    // FR-082: combinaciones sin regla, medios sin cuenta, cuentas no elegibles, tarifas distintas.
    Task<Result<CompletitudDeLaMatrizDto>> CompletitudAsync(DateOnly fecha, CancellationToken ct);

    // FR-077: los comprobantes que generaría un lote, sin numerar ni guardar.
    Task<Result<VistaPreviaDeLoteDto>> PrevisualizarLoteAsync(
        IReadOnlyList<Guid> messagePublicIds, CancellationToken ct);
}

public sealed record MensajeContableDto(IntegrationEnvelopeV1 Envelope, object Payload);

public sealed record ResultadoDeContabilizacionDto(
    bool IsPostable,
    IReadOnlyList<HallazgoContableDto> Errors,
    IReadOnlyList<HallazgoContableDto> Warnings);

public sealed record HallazgoContableDto(
    string MessageType, IReadOnlyList<int> DocumentLines, string? AccountCode,
    string Rule, string Message, QuienCorrigeDto WhoFixes);

public sealed record QuienCorrigeDto(string Module, string? Page, string? Permission);
```

`MensajeContableDto` lleva el sobre y el contenido **tal como se emitirían**: `EmisionDeInventario` arma
los mensajes una sola vez, los evalúa y, si el documento se confirma, emite esos mismos objetos con los
costos definitivos. Lo evaluado y lo emitido son el mismo contrato, y el consumidor los construye con
el mismo `ConstructorDeLineasDeInventario`: por eso SC-021 se cumple por construcción. El `messageId`
de la evaluación es provisional y no se guarda.

### 4.2 Qué evalúa

`EvaluateInventoryPostingQuery` (detrás de `EvaluarAsync`):

1. Agrupa los mensajes en unidades y arma cada `PostingRequest` con el mismo constructor del consumidor
   (§3).
2. Los somete a `AccountingPoster.ValidarVariosAsync`: el mismo análisis de `ValidarAsync` (reglas 1 a
   11: contabilidad iniciada, tipo de comprobante, período existente y abierto, importes, cuenta de
   movimiento activa y habilitada para INV, sucursal, tercero, documento cruce, centro de costo, base
   gravable y tarifa, cuadre), para varios a la vez, con carga en bloque de configuración, tipos,
   períodos, cuentas con sus tarifas, sucursales, terceros, centros y cruces, **sin seguimiento** y
   **sin numerar ni agregar nada**.
3. Suma las comprobaciones de la matriz: regla faltante, tarifa distinta en la regla o en la cuenta
   (C8), tipo de comprobante sin mapear o inactivo, invariantes del contenido y más de dos decimales.

Evalúa sólo lo que pasaría a Contabilidad: mensajes de negocio de un documento cuyo modo sellado o
heredado es en línea o por lotes. No evalúa `NotPosted`, informativos ni mensajes a Cartera. Los
costos de la evaluación son **provisionales** (el promedio leído sin bloqueo); las reglas de cuenta no
dependen de ellos, salvo el caso del valor cero.

### 4.3 Respuesta y quién corrige

Si hay errores, Inventario no confirma y responde `Inventory.Prevalidation.NotPostable` (422) con
`data.errors[] { lineNumber, account, rule, whoFixes }`. Los avisos (`Accounting.Line.TaxAmountDiffers`)
se muestran y no impiden confirmar.

| Código | Módulo | Pantalla | Permiso |
|---|---|---|---|
| `Accounting.InventoryRule.Missing` | Contabilidad | `/contabilidad/inventario/matriz` | `Accounting.InventoryRules.Manage` |
| `Accounting.InventoryRule.TaxRateMismatch` (la regla) | Contabilidad | `/contabilidad/inventario/matriz` | `Accounting.InventoryRules.Manage` |
| `Accounting.InventoryRule.TaxRateMismatch` (la cuenta, C8) | Contabilidad | `/contabilidad/plan-de-cuentas` | `Accounting.Taxes.Manage` (o el catálogo, `/maestros/impuestos`, `Core.Taxes.Manage`) |
| `Accounting.VoucherType.NotFound` · `.NotAllowedForModule` | Contabilidad | `/contabilidad/inventario/tipos-de-comprobante` | `Accounting.InventoryRules.Manage` |
| `Accounting.Line.AccountNotMovement` · `.AccountInactive` · `.AccountNotEnabledForModule` · `.CostCenterNotAllowed` · `.TaxBaseRequired` · `.TaxAmountMismatch` | Contabilidad | `/contabilidad/plan-de-cuentas` | `Accounting.Accounts.Manage` |
| `Accounting.Line.ThirdPartyRequired` (la clase lleva tercero) · `.CostCenterRequired` | Inventario | el documento | el de crear de su grupo (p. ej. `Inventory.Sales.Create`) |
| `Accounting.Line.ThirdPartyRequired` (la clase no lleva tercero) · `.CrossDocumentRequired` | Contabilidad | `/contabilidad/inventario/matriz` | `Accounting.InventoryRules.Manage` |
| `Accounting.Line.ThirdPartyInvalid` | Core | Personas | `Core.People.Update` |
| `Accounting.Line.CrossDocumentTypeInvalid` | Contabilidad | `/contabilidad/inventario/tipos-de-comprobante` | `Accounting.InventoryRules.Manage` |
| `Accounting.Period.Closed` · `.NotFound` | Contabilidad | `/contabilidad/periodos` | `Accounting.Periods.Reopen` (`.CloseYear` si falta el ejercicio) |
| `Accounting.NotInitialized` | Contabilidad | `/contabilidad/configuracion` | `Accounting.Setup.Manage` |
| `Accounting.InventoryMessage.Unbalanced`, más de dos decimales | Inventario | — | defecto del emisor: soporte |

Una cooperativa sin contabilidad iniciada pone `Contabilidad.ModoDePaso = NoPasa`; si no, toda
confirmación que pase se detiene en `Accounting.NotInitialized`.

### 4.4 Tiempo y política

La validación corre **en proceso** (sin HTTP), **fuera del cerrojo** de existencias (paso 5 del flujo
canónico) y con un tiempo máximo de `Contabilidad.ValidacionPreviaSegundos` (3 s por defecto). Una
excepción o el tiempo agotado es «no responde», y Inventario aplica `Contabilidad.PoliticaSinRespuesta`:
`ConfirmarConPendiente` (por defecto, R6) o `Bloquear`. El resultado queda sellado en
`IntegrationMessage.PrevalidationOutcome`: `Postable`, `NoResponse` o `NotApplicable`.

**SC-021.** Un mensaje evaluado `Postable` sólo puede rechazarse por algo que cambió después de
confirmar: una regla, una cuenta o el cierre del período. Lo confirmado con `NoResponse` y lo enviado
por FR-078 se miden aparte. La bandeja compara `PrevalidationOutcome` con el resultado de la entrega.

## 5. En línea, por lotes y resumido

El documento sella su modo al confirmar y la entrega lo copia (`mensajes.md` §11). El mismo comando
contabiliza en línea y por lotes con granularidad por documento; sólo cambia cuándo lo dispara el
despachador.

### 5.1 En línea

El despachador toma las entregas `Pending` elegibles apenas `ISenalDeMensajes` avisa del commit (o en
el sondeo de 5 s) y envía un `PostInventoryMessagesCommand` por unidad. Actor: el proceso de
integración. Meta: el 95 % antes de un minuto (SC-011).

### 5.2 Por lotes, un comprobante por documento

Un lote (`COR_IntegrationBatches`, de la plataforma, para que Inventario lo muestre sin leer `ACC_`) toma
las entregas `InBatch` de su `ScheduleKey` hasta su corte. `IDestinoDeMensajes.PlanearLote` devuelve
una unidad por documento, y el despachador envía un `PostInventoryMessagesCommand(…, BatchPublicId)` por
unidad, cada una en su ámbito DI y en pasadas que sólo toman lo que ya tiene sus dependencias
satisfechas. El origen de cada comprobante es su documento; la fecha, la de operación del documento.

### 5.3 Por lotes resumido

Un comprobante por **fecha de operación, tipo de documento de Inventario, tipo de comprobante y
sucursal** (y centro de costo, si el documento lo trae) (FR-077). `AgrupadorDeResumidos` arma los
grupos y el despachador envía un `PostInventorySummaryGroupCommand(BatchPublicId, GroupKey,
MessagePublicIds)` por grupo:

1. Arma el `PostingRequest` de cada documento del grupo como si fuera por documento y los valida
   todos con `ValidarVariosAsync`.
2. **Excluye** los que fallan, con su motivo (`Rejected`), y a sus relacionados del mismo lote, que
   quedan esperando: un documento malo no tumba el resumen de 5.000 ventas.
3. Suma las líneas válidas por **cuenta, lado, sucursal y centro** (este último sólo si la cuenta lo
   maneja), **débitos y créditos por separado, sin netear**.
4. **No suma** las líneas cuya cuenta exige tercero, documento cruce o base gravable: conservan su
   tercero, cruce y base por documento (FR-077).
5. Un solo `PrepareAsync` por grupo, fechado en la fecha de operación del grupo (nunca la del lote),
   con origen `AccountingOrigin("INV", "InventoryPostingBatch", lote.PublicId)` y `RegistradoPor`
   nulo.
6. Una fila de `ACC_InventoryPostings` por mensaje del grupo, todas con el mismo `AccountingDocumentId`
   y el `BatchPublicId`: es la lista de documentos y usuarios de origen del comprobante (Principio X).
7. Un `SaveChanges` por grupo.

Un `DocumentoAnulado`, una nota o un ajuste de costo en un lote resumido van en el grupo de **su**
tipo de documento y **su** fecha: nunca se mezclan con los del original.

### 5.4 Disparadores y ciclo del lote

| `BatchTrigger` | Cuándo | Actor | Quién lo crea |
|---|---|---|---|
| `Scheduled` | la hora de `Contabilidad.HoraDeLote` (hora de Colombia) de cada `ScheduleKey` con `HoraDiaria` | proceso | `ScheduleIntegrationBatchesCommand` (sin ruta), que el despachador envía por `ISender` dentro de `IEjecutorEnCooperativa`; índice único `(ScheduleKey, ScheduledFor)` para que dos réplicas no lo dupliquen |
| `CashSessionClose` | al cerrar la sesión de caja (`CierreDeTurno`), con las entregas de esa sesión | proceso | `CloseCashSessionCommand`, en su `SaveChanges` |
| `PeriodClose` | al cerrar el período de inventario (`CierreDePeriodo`) | proceso | `CloseInventoryPeriodCommand`, en su `SaveChanges` |
| `Manual` | cuando alguien lo ordena, tras la vista previa | la persona | `OrderIntegrationBatchCommand` (`POST /api/accounting/inventory/batches`, `Accounting.InventoryBatches.Run`, motivo y clave de operación), responde 202 |
| `Reprocess` | reproceso de rechazados y sus dependientes | la persona | `ReprocessMessagesCommand` (Inventario) |
| `SendNotApplicable` | envío posterior de «no aplica» (FR-078) | la persona | `SendNotApplicableMessagesCommand` (Inventario) |

Estados: `Requested` → `Running` (`StartIntegrationBatchCommand`) → `Completed`, `CompletedWithRejections` o `Empty` (`CloseIntegrationBatchCommand`, con sus totales; los dos sin ruta y enviados por el despachador, que no escribe por sí mismo) (un lote sin
mensajes queda registrado, para poder probar que corrió). Número de `COR_IntegrationBatchCounters.NextValue`.
Un lote programado que no corrió a su hora más la tolerancia técnica levanta `Integracion.LoteNoCorrio` (el despachador la envía con `RaiseAlertCommand`),
dirigida a quienes tienen `Accounting.InventoryBatches.Run` y a quienes tienen
`Inventory.Messages.Reprocess` (FR-080: responsables de los dos lados).
El orden manual trae el `cutoffMessagePublicId` de su vista previa (el `PublicId` del último mensaje
listado); el handler lo traduce al `Id` interno y lo guarda en `COR_IntegrationBatches.CutoffMessageId`,
que nunca sale por HTTP (Principio VI): se procesa exactamente lo previsto.
Procesar otra vez un lote o un mensaje no crea nada (recibo único; SC-020).

### 5.5 Vista previa

`PreviewInventoryBatchQuery` (`POST /api/accounting/inventory/batches/preview`,
`Accounting.InventoryBatches.View`) y, para la bandeja de Inventario, `PrevisualizarLoteAsync`:
devuelven los grupos con sus comprobantes propuestos (tipo, fecha, sucursal, líneas y totales), los
documentos de cada grupo y los errores de cada documento que quedaría fuera, más el
`cutoffMessagePublicId` (el del último mensaje incluido; el `Id` interno no se expone).
No numeran, no agregan ni guardan nada.

### 5.6 Rendimiento

SC-020: 5.000 documentos en menos de 10 minutos. El lote carga una vez por lote las reglas vigentes,
las cuentas con sus tarifas, los tipos de comprobante, los períodos, sucursales, centros y terceros
que usa, y los reutiliza en cada grupo (`ValidarVariosAsync` sin seguimiento). La prueba de volumen
reporta Skip explícito sin `RUN_PERF_TESTS=1`.

## 6. Anulaciones, notas y ajustes: comprobantes nuevos

Lo que decidió el dueño (C7, Supuesto 5), con la regla de T29:

- **Nada de Inventario usa `PrepareReversalAsync`.** La guarda del poster rechaza un original o un
  origen INV con `Accounting.Document.InventoryCorrectsWithNewVoucher`; `ReverseDocumentCommand`
  conserva `Accounting.Document.ModuleOwned` con mensaje propio para INV («se corrige anulando el
  documento o con su nota en Inventario; llega como comprobante nuevo»; en un resumido dice cuántos
  documentos reúne). Lo vigila `LoDeInventarioNoSeReversa`.
- Cada anulación (`DocumentoAnulado`), nota (`NotaCreditoEmitida`, `NotaDebitoEmitida`), devolución o
  ajuste de costo es un comprobante **nuevo**, `Kind = Regular`, de **su propio mensaje** y **su
  propia fecha**, por documento o dentro de un resumido.
- El comprobante original **nunca** se marca `Reversed` ni se toca. El vínculo vive en
  `ACC_InventoryPostings` (`RelatedDocumentPublicId`, y el recibo del original por su `messageId`):
  desde el comprobante nuevo se llega al original y a su comprobante, aunque sea un resumido.
- Si la fecha de la corrección cae en un período contable cerrado, el mensaje se **rechaza** a la
  bandeja; no se corre de fecha. Se recupera reabriendo el mes (y el ejercicio, si ya se cerró) según
  las reglas de la 009 y reprocesando (US7-6).
- Contabilidad nunca corrige por su cuenta un comprobante nacido de Inventario (C3, FR-038 de la 009).
  Un `CG` manual de reclasificación del contador sigue siendo posible, como hoy.

Ejemplo (US7-8): una factura validada que iba en el FV resumido del 14 se anula el 20 con su nota
crédito total. Llegan `NotaCreditoEmitida` y `DevolucionRegistrada` de la nota, fechadas el 20; sale un
NV del 20 enlazado a la factura y a su FV resumido, y el resumido del 14 queda como estaba.

## 7. Consultas: completitud y conciliación

### 7.1 Completitud de la matriz (FR-082)

`InventoryRulesCompletenessQuery` (`GET /api/accounting/inventory/completeness?date=`, y
`CompletitudAsync` para Inventario). Recibe de `IDimensionesDeInventario` las combinaciones en uso
(operación × grupo × bodega de los tipos que pasan a contabilidad), los puntos de venta y las causas;
lee de Core los medios de pago activos y las tarifas vigentes. Devuelve:

- combinaciones y roles exigidos **sin regla vigente**;
- medios de pago activos sin regla `MedioDePago` vigente;
- reglas cuya cuenta **dejó de ser elegible** (`AccountEligibility.Reparo`: inactiva, de agrupación o
  sin INV habilitado);
- impuestos cuya cuenta tiene, en algún tramo de la vigencia, **otra tarifa o ninguna** (C8);
- operaciones sin tipo de comprobante mapeado, o mapeadas a uno inactivo;
- avisos: cuentas de impuesto por unidad que exigen base; la cuenta de mercancía por facturar si exige
  documento cruce; en ámbito de costeo por cooperativa, un grupo cuyas bodegas van a cuentas de
  inventario distintas (D2).

Se integra además en `ListInvalidParameterizationsQuery` (FR-017 de la 009) y en
`AccountReferenceFinder`: una cuenta de la matriz no se elimina, e inactivarla avisa (G7). D-07 pide
dejar este reporte vacío antes del ensayo.

### 7.2 Saldos de las cuentas mapeadas (FR-081, FR-090)

`InventoryAccountBalancesQuery` (`SaldosDeCuentasMapeadasAsync`):

1. Toma las reglas vigentes al corte con rol `Inventario` o `Transito`.
2. Arma **conjuntos de cuentas** como componentes conexos del grafo grupo ↔ cuenta: dos grupos que
   comparten una cuenta van en el mismo conjunto, y un grupo cuya bodega tiene una regla propia junta
   las dos cuentas.
3. Lee el saldo de cada cuenta y sucursal a la fecha **sólo** por `MovimientosContables` (un solo punto
   de lectura), con la sobrecarga nueva de `PrepararAsync` con alcance explícito
   `AlcanceDeSucursales.SinRestriccion` (la cifra se compara contra el valorizado total; el permiso lo
   pone la ruta de Inventario, G5): `HastaInclusive(Base(...))`. Incluye la apertura `AP`, que es el
   saldo inicial; el cierre `CI` no toca cuentas de clase 1.

```csharp
public sealed record ConjuntoDeCuentasDto(
    IReadOnlyList<string> AccountingGroupCodes,
    IReadOnlyList<(string AccountingGroupCode, string WarehouseCode)> Pairs,   // "*" = todas
    IReadOnlyList<CuentaDelConjuntoDto> Accounts,
    decimal Balance);

public sealed record CuentaDelConjuntoDto(
    string AccountCode, string AccountName, string Role,                        // Inventario | Transito
    IReadOnlyList<(Guid BranchPublicId, decimal Balance)> BalanceByBranch);
```

Inventario suma su valorizado sobre esos pares (con el tránsito como columna propia); las bodegas no
activas que comparten cuentas con el conjunto **suman al valorizado del conjunto** con sus cifras de
SOLIDO a la fecha de corte (FR-090) y se muestran aparte para identificarlas (la diferencia no se les
atribuye: precisión aplicada a la spec en FR-081); y explica la diferencia con sus propios mensajes
pendientes, en lote, rechazados y «no aplica» (FR-081). Con cero mensajes sin procesar, la diferencia
es cero (SC-005).

Esta consulta llega en I2. Antes, la activación de una bodega sin comparación sólo existe fuera de
producción; en producción `ActivateWarehouseCommand` responde 422
`Inventory.Activation.AccountingUnavailable` mientras la consulta no exista.

### 7.3 Rutas del lado contable

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /api/accounting/inventory/rules` | `Accounting.InventoryRules.View` | Lista con filtros (operación, rol, fecha, cuenta). |
| `POST /api/accounting/inventory/rules` | `Accounting.InventoryRules.Manage` | `CreateInventoryPostingRuleCommand`. |
| `POST /api/accounting/inventory/rules/{id}/versions` | ídem | `AddInventoryPostingRuleVersionCommand`. |
| `POST /api/accounting/inventory/rules/{id}/deactivate` | ídem | `DeactivateInventoryPostingRuleCommand` (con motivo). |
| `GET /api/accounting/inventory/rules/template.xlsx` | `Accounting.InventoryRules.View` | Plantilla. |
| `POST /api/accounting/inventory/rules/import?mode=review\|apply` | `Accounting.InventoryRules.Manage` | Importación todo o nada. |
| `GET` · `PUT /api/accounting/inventory/voucher-mappings` | `.View` · `.Manage` | Mapeo de tipos de comprobante (`SetInventoryVoucherMappingCommand`). |
| `GET /api/accounting/inventory/completeness?date=` | `Accounting.InventoryRules.View` | §7.1. |
| `GET /api/accounting/inventory/batches` | `Accounting.InventoryBatches.View` | Lotes, con su estado y totales. |
| `POST /api/accounting/inventory/batches/preview` | `Accounting.InventoryBatches.View` | §5.5. |
| `POST /api/accounting/inventory/batches` | `Accounting.InventoryBatches.Run` | Orden de lote manual (202; `Idempotency-Key`). |
| `GET /api/accounting/inventory/batches/{id}` | `Accounting.InventoryBatches.View` | Documentos, comprobantes y rechazos del lote. |
| `GET /api/accounting/inventory/postings?document=` | `Accounting.Vouchers.View` | Recibos de un documento: mensajes, comprobantes y lote. |

Sin permiso, el mismo 404 que algo inexistente. La matriz y el mapeo siguen la mecánica de la 009:
sin clave de operación; la orden de lote la lleva (T13).

## 8. Aviso antes del cierre contable

`ClosePeriodCommand` gana `bool AcknowledgeInventoryPending = false`. Antes de cerrar consulta
`PendingInventoryMessagesQuery(desde, hasta)`: entregas a Contabilidad en `Pending`, `InBatch` o
`Rejected` (no las `NotApplicable`) con fecha de operación en el mes.

- Si hay y no vino el reconocimiento, falla con `Accounting.Period.InventoryPending` (422) y
  `data: { pending, inBatch, rejected, oldestOperationDate, types[] }`.
- La pantalla de Períodos ofrece **Procesar ahora** (ordena un lote manual del rango; exige
  `Accounting.InventoryBatches.Run`) o **Cerrar de todos modos** (reconoce; queda auditado).
- Lo que siga pendiente se rechazará con `Accounting.Period.Closed` y se recupera reabriendo el mes
  (y el ejercicio) según la 009, y reprocesando (D-01).
- Es un aviso, no un bloqueo (G6); molde de `GeneratePilaCommand.AcknowledgeWarnings`.
  `CloseFiscalYearCommand` no cambia: exige los doce meses cerrados, que ya pasaron por el aviso.

## 9. Tipos de comprobante de Inventario

Todos `Usage = Module`, `ModuleCode = INV`. `FV`, `EI` y `SI` ya existen; `NV`, `CP`, `TR`, `AC` y
`CJ` los agrega `voucher-types.json` (§12.6). `VoucherTypesSeeder` registra en el log la colisión con un
código que la cooperativa ya tenga con otro uso, en vez de saltarla en silencio, y el mapeo permite
llevar la operación a otro tipo. Pendiente de validar por la contadora (A8).

| Código | Nombre | Operaciones (mapeo por defecto) | Cruce por defecto |
|---|---|---|---|
| `FV` | Factura de venta | `Venta`, `CostoDeVenta` | `FV` |
| `EI` | Entrada de inventario | `Compra`, `AjustePositivo`, `Ensamble` | — |
| `SI` | Salida de inventario | `AjusteNegativo`, `ConsumoInterno`, `RetiroGravado`, `Baja`, `DevolucionAProveedor`; la remisión por su tipo | `FC` en `DevolucionAProveedor` |
| `NV` | Notas de venta | `NotaCredito`, `DevolucionDeCliente`, `NotaDebito` | `NC` · `ND` |
| `CP` | Compras | `FacturaProveedor` (factura y notas del proveedor, documento soporte y su nota) | `FC` |
| `TR` | Traslados | `DespachoTraslado`, `RecepcionTraslado` | — |
| `AC` | Ajustes de costo | `AjusteDeCosto`, `Reclasificacion` | — |
| `CJ` | Caja POS | `MovimientoDeCaja`, `DiferenciaDeArqueo` | — |

`FC`, `FV`, `NC` y `ND` son tipos de documento cruce que ya siembra la 009. Recuerde §3.2: las líneas que
saldan otro documento cruzan contra el original, no contra el cruce por defecto de su operación.

## 10. El actor

| Quién procesa | `Actor` | `PostedBy` del comprobante | `RegisteredBy` |
|---|---|---|---|
| En línea, lote programado, lote de cierre de turno o de período | `Kind = Process`, `Name = "Proceso de integración"`, canal `Process`, origen `Mensaje:{id}` o `Lote:{número}`, sin IP | «Proceso de integración» | por documento, el usuario de origen (`RegistradoPor`); en un resumido, el actor |
| Lote manual, reproceso, envío posterior | `Kind = Person`: la persona que lo ordenó, con su IP y motivo capturados en la orden | esa persona | ídem |

- El usuario de origen viaja en el mensaje y queda como **dato** en el comprobante y en
  `ACC_InventoryPostings`; nunca es actor y **el procesador no usa sus permisos** (FR-083). Los permisos
  se exigen en la petición que crea una orden manual.
- `ICurrentUserService.UserId` no cambia en esta feature (T6): `RegisteredByUserId` queda en 0 y
  `PostedByUserId` nulo, como hoy con todo usuario de identidad central. No se crea usuario técnico en
  `SEC_Users`.
- El cuatro ojos de la 009 no aplica: un comprobante de módulo se contabiliza al prepararse, no es un
  borrador.
- Auditoría: `AuditBehavior` registra el comando con el actor y el origen; `AccountingAuditEmitter`
  agrega `Accounting.Inventory.Posted`, `Accounting.Inventory.Rejected` y
  `Accounting.Inventory.BatchProcessed`, escritos por el `PublicId` de la cooperativa. Sin cooperativa
  resuelta, **lanza**: nunca cae a la base de auditoría global (T5, FR-083).

## 11. Errores

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Accounting.InventoryRule.Missing` | 422 / bandeja | sin regla vigente para un rol con importe | `operation, role, values, date, lineNumbers` |
| `Accounting.InventoryRule.Overlaps` | 422 | vigencias cruzadas de una misma clave | la regla existente |
| `Accounting.InventoryRule.RetroactiveOverPosted` | 422 | versión que empieza antes de lo ya contabilizado | `lastPostedDate, document` |
| `Accounting.InventoryRule.TaxRateMismatch` | 422 / bandeja | tarifa de la regla o de la cuenta distinta de la del mensaje (C8) | `tax, account, ruleOrAccountRate, messageRate` |
| `Accounting.InventoryMessage.Unbalanced` | bandeja | invariantes del contenido; defecto del emisor | `difference` |
| `Accounting.InventoryMessage.CurrencyNotSupported` | bandeja | moneda ≠ COP o tasa ≠ 1 | `currency, exchangeRate` |
| `Accounting.InventoryMessage.WaitingForOriginal` | reintento | el original aún no está contabilizado | `originalMessageId` |
| `Integration.VersionNotAccepted` | bandeja | versión que el destino no acepta | `type, version` |
| `Accounting.Period.InventoryPending` | 422 | cierre de mes con pendientes sin reconocer | `pending, inBatch, rejected, oldestOperationDate, types` |
| `Accounting.Document.InventoryCorrectsWithNewVoucher` | 422 | `PrepareReversalAsync` sobre algo de INV | `origin` |
| `Accounting.Document.ModuleOwned` (existente) | 422 | reversar desde Comprobantes un comprobante INV, con el mensaje de §6 | `origin` |
| `Inventory.Prevalidation.NotPostable` (lado Inventario) | 422 | la validación previa encontró errores | `errors[] { lineNumber, account, rule, whoFixes }` |

## 12. Enmienda de la 009 (D-01)

Todo lo que cambia fuera de la 012 para que la 009 diga lo que el código hace (Principio I). Se
aplica en la entrega I2, en el mismo cambio que publica el consumidor.

### 12.1 `specs/009-contabilidad-niif/spec.md`

| Dónde | Hoy | Enmienda |
|---|---|---|
| Entrada (l.7) | «Recibe transacciones en línea desde … Inventario comercial …» | Nota al pie: «Inventario comercial llega por mensajes asíncronos con validación previa (feature 012, D-01)». |
| Alcance (l.129) | «… después Cartera, Inventario, Tesorería y CDT/Ahorros …» | «… después Cartera, Tesorería y CDT/Ahorros; Inventario, por mensajes con la matriz de reglas de la 012». |
| US4, introducción (l.323) | «si rechaza, la operación del módulo falla completa» | Agregar: «salvo Inventario, que pregunta antes de confirmar (validación previa) y, si Contabilidad rechaza después, deja el mensaje en su bandeja (012 FR-074)». |
| US4, escenario 5 (l.350) | período cerrado → el rechazo llega al usuario del módulo | Agregar: «No aplica a Inventario: su rechazo por período cerrado llega a la bandeja y se recupera reabriendo y reprocesando (012 US7-6)». |
| US4, escenario 7 (l.354) | «Cartera, Inventario, Tesorería y CDT» | Quitar Inventario; agregar «Inventario: 012 US7». |
| FR-016 (l.809) | «cuentas de producto e IVA» | «la matriz de reglas de Inventario (012 FR-073)». |
| FR-017 (l.812) | parametrizaciones inválidas | Agregar «incluidas las reglas de la matriz de Inventario». |
| FR-022 (l.828) | cierre mensual sin borradores | Agregar: «y MUST avisar, con reconocimiento auditado, de los mensajes de Inventario pendientes, en lote o rechazados con fecha en el período (012 D-01)». |
| FR-030 (l.857) | la única corrección es la reversión | Agregar: «Excepción: lo nacido de Inventario se corrige con un comprobante nuevo de su propio mensaje, con fecha propia, enlazado al original, que nunca se marca reversado (012 FR-079, C7)». |
| FR-036 (l.877) | operación y comprobante juntos o ninguno | Agregar: «Inventario no usa el contrato sincrónico: la atomicidad es documento + mensaje en Inventario y comprobante + recibo del mensaje en Contabilidad (012 FR-071, FR-074); el camino al libro sigue siendo este contrato». |
| FR-037 (l.880) | todo comprobante guarda su origen | Agregar: «El origen de un comprobante resumido de Inventario es el lote (`InventoryPostingBatch`), que lista sus documentos». |
| FR-038 (l.883) | corrección desde el módulo por reversión | Agregar: «En Inventario, por el comprobante nuevo de su anulación, nota o ajuste (FR-030)». |
| FR-039 (l.885) | los módulos contabilizan por el contrato | «Inventario contabiliza por el contrato a través de su consumidor de mensajes (`PostInventoryMessagesCommand`), no desde sus comandos». |
| FR-040 (l.889) | tipos reservados por módulo | Inventario: `FV`, `EI`, `SI`, `NV`, `CP`, `TR`, `AC`, `CJ`, mapeables por operación y tipo de documento; tercero y sucursal, los del mensaje. |
| FR-041 (l.894) | el rechazo nombra línea, concepto y regla | Agregar: «Hacia Inventario, en la validación previa (antes de confirmar) o en la bandeja (después), con la línea del documento, la cuenta, la regla y quién corrige». |
| FR-070 (l.993) | «IVA en Inventario» cumple FR-065 | «Los impuestos de Inventario, por la matriz y con el control de tarifa de la 012 (C8, FR-082)». |
| SC-004 (l.1127) | cero huérfanos por operación de cada módulo | Para Inventario: «cero documentos confirmados sin mensaje y cero comprobantes de origen INV sin recibo de mensaje; lo pendiente, rechazado o que no pasa se ve en la bandeja». |

### 12.2 `specs/009-contabilidad-niif/contracts/contabilizacion.md`

- §1: `PostingRequest` gana `UsuarioDeOrigen? RegistradoPor = null` (`UsuarioDeOrigen(Guid?
  CentralUserId, string Name)`; nulo conserva el comportamiento de Nómina y la digitación);
  `AccountingPoster` documenta `ValidarAsync` (existe) y la nueva `ValidarVariosAsync(IReadOnlyList<PostingRequest>, ct)`
  (mismo análisis, carga en bloque, sin seguimiento, sin numerar); el comentario de
  `PrepareReversalAsync` dice que rechaza lo de INV.
- §2: fila nueva «reversión de algo de INV → `Accounting.Document.InventoryCorrectsWithNewVoucher`».
- §3: nota «Inventario no usa este molde: ver `specs/012-inventario-comercial/contracts/contabilidad.md`».
- §4: las filas «Inventario · factura de venta», «Inventario · entradas/salidas» e «Inventario ·
  anular» se reemplazan por una: «**Inventario** · por mensajes | `FV`, `EI`, `SI`, `NV`, `CP`, `TR`,
  `AC`, `CJ` | por la matriz `ACC_InventoryPostingRules` | el del mensaje | según la cuenta | 012
  `contracts/contabilidad.md`; anular → comprobante nuevo».
- §5: «No permite editar ni borrar un documento contabilizado: sólo `PrepareReversalAsync`» →
  agregar «, salvo lo de Inventario, que no se reversa: se corrige con un comprobante nuevo».

### 12.3 `specs/009-contabilidad-niif/research.md`

- R2 (l.90): nota «Inventario (012) no llama al contrato desde su operación: la unidad atómica de
  Inventario es documento + mensaje; la de Contabilidad, comprobante + recibo (`ACC_InventoryPostings`)
  en el consumidor, con el mismo `PrepareAsync`».
- R16 (l.291): la entrada de Inventario («`ProductAccount`/`VatAccount` por código, hoy nunca leídos»)
  pasa a «Inventario: matriz `ACC_InventoryPostingRules` por mensajes (012); `INV_ProductAccounts` e
  `INV_VatAccounts` se retiran con `RetiroDelInventarioHeredado`».
- R17 (l.312): E3 sin Inventario.

### 12.4 `specs/009-contabilidad-niif/data-model.md`

- l.73-74 (referencias que impiden eliminar una cuenta): fuera `INV_ProductAccounts` e
  `INV_VatAccounts`; entra `ACC_InventoryPostingRules`.
- l.90 (semilla de tipos): `FV`, `EI`, `SI`, `NV`, `CP`, `TR`, `AC`, `CJ` Inventario.
- l.82 (`ACC_AccountTaxRates.Rate`): (9,4) → (9,6), ampliación no destructiva (T19).
- l.237: fuera `INV_ProductAccounts` e `INV_VatAccounts` de la fila.
- l.238 (`INV_Documents.AccountingDocumentId`): se elimina; el vínculo vive en `ACC_InventoryPostings`.
- Sección nueva con `ACC_InventoryPostingRules`, `ACC_InventoryVoucherMappings` y
  `ACC_InventoryPostings`, remitiendo a la 012.

### 12.5 `specs/009-contabilidad-niif/tasks.md` y `plan.md`

- T120 (parte Inventario), T125, T128 (entradas `InventoryDocument` e `Invoice`) y T129 (la parte
  `E2E/Inventory/ContabilizacionDeInventarioTests`): marcadas «reemplazadas por la 012».
- `plan.md` l.17 (Inventario en la lista del contrato): «Inventario por mensajes (012)»; l.42 (`INV_Documents`
  con `AccountingDocumentId`): se quita; l.137 (E3): sin Inventario.
- `contracts/api.md` §15 (l.236): quitar Inventario de «validan la cuenta al guardar»; la matriz valida
  en su propio comando.

### 12.6 Documentación y semillas

- `docs/manual/contabilidad-contrato-de-contabilizacion.md`: sección nueva «Inventario: por mensajes»
  (matriz, validación previa, modos, comprobantes nuevos) y la excepción en su §5.
- `CLAUDE.md`, párrafo de Contabilidad: E3 cubre cinco módulos; Inventario por mensajes (012), y sus
  escritores muertos con el marcador `E3 (feature 009)` desaparecen con el retiro del módulo actual.
  Las cifras «18 tipos de comprobante» y «30 permisos» pasan a 23 y 34.
- `Persistence/Seeding/Parametric/Data/voucher-types.json`: `NV`, `CP`, `TR`, `AC`, `CJ`.
- `Data/inventory-voucher-mappings.json` y `InventoryVoucherMappingsSeeder` (Order 84).

### 12.7 Código de la 009 que cambia

`AccountingPoster` (`ValidarVariosAsync` sin seguimiento; guarda INV en `PrepareReversalAsync`;
`RegisteredBy` desde `RegistradoPor`), `PostingRequest` (`RegistradoPor`), `AccountingErrors`
(`DocumentInventoryCorrectsWithNewVoucher`, `PeriodInventoryPending`, `InventoryRule*`,
`InventoryMessage*`), `ReverseDocumentCommand` (mensaje para INV), `ClosePeriodCommand`
(`AcknowledgeInventoryPending`), `MovimientosContables.PrepararAsync` (sobrecarga con alcance explícito),
`EnlacesDeOrigen` en Application y en Shared (`InventoryDocument`, `InventoryPostingBatch`,
`InventoryProduct`), `AccountReferenceFinder` (fuera `ProductAccounts`/`VatAccounts`, entra la matriz;
debe cambiar **en el mismo commit** del retiro o deja de compilar), `ListInvalidParameterizationsQuery`,
`VoucherTypesSeeder` (colisiones al log), `AccountingPermissionCatalogSeeder` (cuatro permisos),
`AccountingAuditEmitter` (sin caída a la base global), `IApplicationDbContext` (tres `DbSet`),
configuración de `ACC_AccountTaxRates.Rate` (9,6) en la migración `IntegracionContableDeInventario`.

### 12.8 Pruebas

| Prueba | Cambio |
|---|---|
| `AccountingPosterTests` | reversión de algo de INV rechazada; `RegistradoPor` llega a `RegisteredBy`; `ValidarVariosAsync` da lo mismo que `ValidarAsync` por cada comprobante y no deja nada rastreado |
| `PeriodCommandsTests` | cierre con pendientes: falla sin reconocimiento, cierra con él y lo audita |
| pruebas de `ReverseDocumentCommand` | mensaje propio para INV |
| pruebas que cuentan tipos de comprobante o permisos sembrados | 18 → 23 tipos; 30 → 34 permisos contables |
| `AccountLineRulesTests` | sin cambios |
| `NingunModuloEscribeMovimientosFueraDelContrato`, `PrincipioXI_ContableImmutable` (ya cubre `Entities/Accounting/Transactions`, donde vive `InventoryPosting`), `LaContabilidadNoTieneCuentasEnCodigo` | sin cambios, verdes |
| `InventarioNoConoceContabilidadNiCartera`, `LoDeInventarioNoSeReversa`, `LosComandosDeConsumoNoTienenRuta` | nuevas |
| `Application.Tests/Accounting/Inventory/*` | nuevas: `ResolutorDeReglas` (especificidad, vigencia, exigidas), constructor con casos dorados JSON por operación (incluidos espejo y signos), consumo idempotente con colisión del índice, agrupador de resumidos (detalle de tercero, cruce y base; sin netear), completitud con C8, conjuntos de cuentas conexos |
| `E2E/Inventory/ContabilizacionDeInventarioTests` (T129 de la 009) | reemplazada por `Accounting/ContabilizacionPorMensajesTests` de la 012: en línea; lote resumido de ajustes; reenvío sin duplicar; anulación de un ajuste de un resumido (el FV resumido y la factura anulada van en `ContabilizacionDeVentasPorMensajesTests`, I3); período cerrado → rechazo → reapertura → reproceso con la fecha original; huérfanos cero en los dos sentidos (comprobante INV sin recibo, recibo de negocio sin comprobante salvo valor cero); `ElBalanceDePruebaCuadra` al final. En cooperativa aislada (colección «Inventario e2e») |

## 13. Lo que este contrato fija por su cuenta

Detalle que `decisiones-transversales.md` no fijaba; si `data-model.md`, `research.md` o `plan.md`
dicen otra cosa, hay que reconciliar:

1. `ReasonCode` también en `Contrapartida` de `AjusteDeCosto` (la `KardexReason`) y en `CajaDestino` (la
   otra punta del movimiento, `CashMovementDestination`), además de los usos de §2.7 de
   `decisiones-transversales.md`. Las
   dimensiones exigidas no pesan en la especificidad; los pesos siguen siendo 16/8/4/2.
2. `Redondeo` como contrapartida de `RoundingResidue`.
3. Un rol sólo exige regla cuando el mensaje trae importe para él.
4. `RetroactiveOverPosted` con su excepción para claves nuevas (§2.4), para no impedir FR-078.
5. `AccountingOrigin("INV", "InventoryProduct", …)` para la reclasificación (tercer tipo de origen en
   `EnlacesDeOrigen`).
6. Las líneas que saldan otro documento cruzan contra el original (§3.2), por encima del cruce por
   defecto de T28 (`NC`, `ND`).
7. Los nombres de los DTO auxiliares de §4.1 y §7.2 (`HallazgoContableDto`, `QuienCorrigeDto`,
   `CompletitudDeLaMatrizDto`, `VistaPreviaDeLoteDto`, `CuentaDelConjuntoDto`) y la firma de
   `PrevisualizarLoteAsync` por identificadores de mensaje. Los cuatro métodos son los de T30 y T31.
8. `POST …/rules/{id}/deactivate` como ruta de desactivación y `Accounting.Vouchers.View` en
   `…/postings`.
