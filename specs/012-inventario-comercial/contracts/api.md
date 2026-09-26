# Contratos HTTP: Inventario comercial (permisos, convenciones, catálogo, bodegas, documentos de inventario, compras y plataforma del módulo)

**Feature**: 012 | **Date**: 2026-09-24 | Secciones 1 a 17: permisos, convenciones, catálogo, bodegas,
documentos de inventario, compras y plataforma; 18 a 32: ventas, precios y descuentos, POS, caja, medios
de pago, crédito provisional, facturación electrónica, bandeja de mensajes, lado contable, informes,
tablero, cambios en rutas existentes, catálogo tributario (§30) y vendedores (§31). **Hermanos**: `contracts/mensajes.md`,
`contracts/contabilidad.md`, `contracts/dian.md`, `contracts/plantillas.md`.
**Nombres**: todos salen de las decisiones transversales T1–T52 de `decisiones-transversales.md` (en la
carpeta de la feature, §2); aquí no se inventa ninguno que allí tenga nombre. Donde este contrato
necesita un nombre que allí no está (un enum de cuerpo, una ruta auxiliar), lo dice en §17.

Convenciones vigentes (las de la 009 y la 010, detalladas en §2): sobre `{ code, message, traceId,
data? }` (`ErrorEnvelopeFilter`); `Validation.*` → 400, `*.NotFound` → 404, `Concurrency.*` → 409,
resto de negocio → **422**; sin permiso o fuera de alcance → **el mismo 404 que algo inexistente**.
Todo identificador que cruza es `PublicId`. Toda ruta de `/api/inventory` lleva `RequireAuthorization()`
+ `AddEndpointFilter<ErrorEnvelopeFilter>()` + `RequirePermission(...)` (lo vigila
`LosEndpointsProtegidosExigenPermiso`, ampliada a `Endpoints/Inventory/*.cs`); los endpoints sólo
reenvían al `ISender` (Principio III) y cada comando tiene su `AbstractValidator` (Principio VIII). Todo
comando con ruta exige la cabecera `Idempotency-Key` (§2.3). Ningún valor legal viaja en el código: lo
que abajo se llama «parámetro» vive en `COR_ParameterVersions` con vigencia (§7), y lo tributario en el
catálogo de Core (§30).

**Lo que no se duplica.** Hay **un** modelo de documento (`INV_Documents` + `INV_DocumentLines` +
satélites, T17) y **un** ciclo (borrador → en aprobación → confirmado → anulado, más descartado). Cada
grupo de clases (`DocumentClassGroup`) tiene su prefijo de ruta y su familia de permisos, pero el ciclo lo
hacen siempre los mismos comandos: `SaveInventoryDraftCommand`, `DiscardInventoryDraftCommand`,
`ConfirmInventoryDocumentCommand(DocumentPublicId, ExpectedGroup)` y `VoidInventoryDocumentCommand`; los
propios de traslados, conteos, compra directa y puesta en marcha los envuelven, no los reemplazan. La
forma común del documento está en §9 y cada grupo sólo agrega lo propio. Las aprobaciones no tienen rutas por documento: todas pasan por
`/api/inventory/approvals` (§15).

## 1. Permisos

### 1.1 Catálogo `Inventory.*`

Los siembra la API al arrancar (`InventoryPermissionCatalogSeeder`, invocado desde
`PhaseZeroSecuritySeeder` **antes** de `BuiltInRolesSeeder`), nunca el DbMigrator. Formato
`Inventory.Recurso.Acción`. La columna E dice desde qué entrega tiene efecto una ruta que lo exija; el
código se siembra entero en I1 para que las plantillas de rol no cambien de forma.

| Recurso | Acciones | Qué habilita | E |
|---|---|---|---|
| `Inventory.Catalog` | View, Manage, Import, Export, ReclassifyAccountingGroup | ver y administrar unidades, categorías, marcas, grupos contables, productos (unidades alternas, códigos de barras, impuestos, imágenes), causas de ajuste y canales de venta; `Import` descarga plantillas y las carga; `Export` descarga el catálogo en su plantilla; `ReclassifyAccountingGroup` cambia el grupo contable de un producto que ya tuvo movimientos (FR-027) | I1 |
| `Inventory.Salespeople` | View, Manage | rol vendedor (FR-031; rutas en §31: `/api/inventory/salespeople`) | I1 |
| `Inventory.Warehouses` | View, Manage, Activate, AcceptActivationDifference | tipos de bodega, bodegas, ubicaciones y políticas de reorden; `Activate` pide la comparación de FR-090 y activa con cuadre; `AcceptActivationDifference` activa **con** diferencia y motivo (permiso especial) | I1 (activar: I2) |
| `Inventory.Stock` | View | existencia física, reservada, disponible y en tránsito, **sin** valores | I1 |
| `Inventory.Costs` | Read | ver costo unitario, costo total, promedio y valorizado en **cualquier** respuesta del módulo; sin él esos campos salen `null` y los informes de valor responden 404. Acción distinta de `View` a propósito (el glob `*.View` no la concede) | I1 |
| `Inventory.Costing` | Manage | registrar vigencias de `Costeo.*` (método, ámbito, retroactividad); permiso especial de FR-043 | I1 (PEPS y retroactivos generales: I5; los de puesta en marcha y conteo, I1, §13.1 y §12) |
| `Inventory.Integrity` | Verify, Rebuild | verificar kardex contra proyecciones (FR-003); reconstruir proyecciones | I1 |
| `Inventory.Periods` | View, Close, Reopen, AcceptUnbilledShipments | ver y cerrar períodos; `Reopen` es el permiso especial de FR-047; `AcceptUnbilledShipments` acepta con motivo las remisiones sin facturar al cerrar | I1 (remisiones: I6) |
| `Inventory.DocumentTypes` | View, Manage, DisableFiscalPosting | tipos de documento y consecutivos; `DisableFiscalPosting` deja sin paso a contabilidad un tipo fiscal (FR-075), además de la confirmación explícita | I1 |
| `Inventory.Documents` | View, Reprint | lista y detalle genéricos de documentos (el grupo exige además su `View`); `Reprint` (§18) | I1 |
| `Inventory.Parameters` | View, Manage | ver y registrar vigencias de parámetros (las claves con permiso propio lo exigen además, §7) | I1 |
| `Inventory.ApprovalPolicies` | View, Manage | políticas de aprobación por tipo de documento y montos máximos por rol de los permisos `Inventory.*` | I1 |
| `Inventory.Approvals` | View, Supervisor, Management | `View` abre la bandeja y la ruta de decisión (decidir exige además el permiso del nivel); `Supervisor` y `Management` son permisos de nivel genéricos sugeridos para los niveles altos (C6) | I1 |
| `Inventory.Scopes` | Manage | asignar bodegas y puntos de venta a usuarios | I1 |
| `Inventory.Scope` | AllWarehouses, AllPointsOfSale | alcance total sin filas de asignación | I1 / I3 |
| `Inventory.Adjustments` | View, Create, Confirm, Approve, Void, SetUnitCost | ajustes positivos y negativos, consumos internos, bajas, ensamble (I6) y movimientos entre ubicaciones; `SetUnitCost` indica el costo de un ajuste positivo (FR-044) | I1 |
| `Inventory.Transfers` | View, Create, Dispatch, Receive, Approve, Void | traslados en dos pasos y resolución de diferencias | I1 |
| `Inventory.Counts` | View, Open, Capture, Close, Approve | conteos físicos y aprobación de su ajuste | I1 |
| `Inventory.Purchases` | View, Create, Confirm, Approve, Void, RegisterRadianEvent, EmitRadianEvent | recepciones, compra directa, facturas y notas del proveedor, devoluciones, documento soporte (I4), solicitudes, órdenes, cruce y costos adicionales (I5); `RegisterRadianEvent` anota un 030/032 emitido por fuera; `EmitRadianEvent` los emite desde el ERP | I1 (Emit: I5) |
| `Inventory.Sales` | View, Create, Confirm, Approve, Void, SellOnCredit, RefundOtherMeans | §18 | I3 |
| `Inventory.Pos` | Sell | §20 | I3 |
| `Inventory.Discounts` | Authorize | §19 | I3 |
| `Inventory.Prices` | View, Manage | §19 | I3 |
| `Inventory.DiscountCaps` | Manage | §19 | I3 |
| `Inventory.PointsOfSale` | View, Manage | §20 | I3 |
| `Inventory.CashSessions` | View, ViewAll, Open, Close | §21 | I3 |
| `Inventory.CashMovements` | Create, Approve | §21 | I3 |
| `Inventory.CashDifferences` | Approve | §21 | I3 |
| `Inventory.DayClose` | Execute, Reopen | §21 | I3 |
| `Inventory.OpeningBalance` | Load, Approve | importar, confirmar y anular el saldo inicial; aprobarlo | I1 |
| `Inventory.LegacyFigures` | Import | importar y consultar cifras de SOLIDO (FR-091) | I1 |
| `Inventory.Messages` | View, Reprocess, SendNotApplicable | bandeja de mensajes (§25); en §1–§17 sólo decide si el detalle de un documento muestra el estado de sus mensajes | I1 (ver) / I2 |
| `Inventory.Reconciliation` | View | conciliación (§26 y vista `reconciliation` de §27) | I2 |
| `Inventory.Dashboard` | View | tablero (§28) | I6 |
| `Inventory.Reports` | View, Export, ExportPersonalData | informes (§27) | I1 |
| `Inventory.Alerts` | View, Attend, Manage | bandeja de alertas, atender, configurar tipos | I1 |

Códigos de otros módulos que esta feature usa (se siembran en su seeder de siempre):

| Código | Dónde se usa |
|---|---|
| `Core.Taxes.View`, `Core.Taxes.Manage` | catálogo tributario (§30) y claves `TAX` de parámetros (§7) |
| `Core.PaymentMeans.View`, `Core.PaymentMeans.Manage` | medios de pago (§22) |
| `Core.People.Create` | alta de persona desde Compras con el aviso de Habeas Data (`PersonaDialog`, T46) |
| `Accounting.InventoryRules.View/Manage`, `Accounting.InventoryBatches.View/Run` | matriz y lotes (§26) |
| `ElectronicInvoicing.Settings.View/Manage`, `.Resolutions.View/Manage`, `.Documents.View/Transmit/Correct/TransmitByCurrentChannel`, `.Contingencies.View/Declare` | facturación electrónica (§24); `Settings.Manage` también gobierna las claves `EINV` (§7) |
| `AuditLog.VerifyIntegrity` | `POST /api/audit/integrity/verify` (contrato en §29); el auditor integrado lo recibe por `AuditLog.*` |
| `Security.Roles.Create` | crear un rol desde un perfil sugerido (§1.5) |

### 1.2 Cómo se aplican

- **El permiso es fijo por ruta** y la ruta la decide el grupo de la clase (`DocumentClassGroup`): no hay
  permisos dinámicos por tipo de documento (T48). Un documento de otro grupo pedido por una ruta ajena
  responde 404 (el comando recibe `ExpectedGroup`).
- **Crear, confirmar y aprobar son acciones distintas** (segregación). La regla «quien crea no aprueba»
  la hace cumplir el motor de aprobaciones con `SEC_Users.Id` de `IActorActual`, nunca con el entero del
  token ni con el correo (T6), y **no es parametrizable**.
- **Lecturas sensibles con acción distinta de `View`**: `Costs.Read`, `Scope.AllWarehouses`,
  `Scope.AllPointsOfSale`, `CashSessions.ViewAll`, `Reports.Export`, `Reports.ExportPersonalData`. Los
  roles integrados `Operator`, `ReadOnly` y `Auditor` reciben todo `*.View` por su glob y **nada más**
  del módulo: ven documentos y existencias de las bodegas que tengan asignadas, sin costos. A ningún rol
  integrado se le agrega escritura de inventario; `CompanyAdmin` tiene `*`.
- **Permisos que revisa el handler además del de la ruta** (la ruta no puede declararlos porque dependen
  del cuerpo): `Adjustments.SetUnitCost` si el cuerpo trae `unitCost`; `Warehouses.AcceptActivationDifference` si trae `acceptDifference`;
  `Periods.AcceptUnbilledShipments` si trae `acceptUnbilledShipments`; `Costing.Manage`,
  `DocumentTypes.DisableFiscalPosting`, `Core.Taxes.Manage` o `ElectronicInvoicing.Settings.Manage`
  según la clave de parámetro (§7); `Catalog.ReclassifyAccountingGroup` en una importación que cambia el
  grupo de un producto con movimientos. Faltar uno de éstos responde **422 con código propio** (no 404):
  el recurso ya es visible para el usuario, así que no revela la existencia de nada, y la pantalla
  necesita saber qué falta. Es una precisión aplicada a la spec sobre FR-009 («cuando el recurso ya es
  visible y el permiso faltante depende del cuerpo, la respuesta es 422 con código propio») y queda
  como pregunta al dueño C10. Las pantallas los esconden con
  `PermissionGate` a partir de `GET /api/admin/permissions/mine`. La excepción es el permiso del nivel en
  una decisión de aprobación: sin él la solicitud no existe para el usuario (404, §15.2).
- **Alcance** (T35): falla cerrado. Sin filas en `INV_UserWarehouseScopes` (o
  `INV_UserPointOfSaleScopes`, I3) el usuario no ve ni opera ninguna bodega, salvo con
  `Scope.AllWarehouses` (`AllPointsOfSale`). Toda consulta pasa por `FiltroDeAlcance` (documentos por
  bodega de origen **o** de destino; existencias y kardex por bodega); todo comando valida cada bodega,
  ubicación y punto que toca, también en las líneas, y responde el 404 de la entidad si está fuera. El
  aprobador también necesita alcance sobre la bodega del documento. La bodega de tránsito se ve a través
  de los traslados de las bodegas del alcance; su existencia propia sólo con alcance total o asignación
  explícita.

### 1.3 Montos máximos por permiso

`SEC_PermissionAmountLimits` (rol, código, monto, moneda, vigencia, motivo; rutas en §15.3). Admiten
límite sólo los permisos de acciones con valor (FR-009): `Inventory.Purchases.Confirm` (compras,
órdenes y notas del proveedor), `Inventory.Adjustments.Confirm`, `Inventory.Sales.Confirm` (sólo notas)
y `Inventory.Sales.SellOnCredit`. El límite efectivo es **el mayor** de los roles activos del usuario que
conceden el permiso; un rol que lo concede **sin fila** significa sin límite (C4).

El monto que se compara, por grupo: compras, notas y órdenes, el `Total` del documento (antes de
retenciones); ajustes, consumos y bajas, el valor al costo de las líneas (Σ |costo total|, con el promedio
leído sin bloqueo al evaluar); saldo inicial, el valor cargado. Si el monto supera el límite, el
documento exige **al menos el nivel 1** de la política de su tipo aunque no alcance su umbral, más los
niveles cuyo umbral alcance; si el tipo no tiene política, 422 `Inventory.Approval.AmountExceedsLimit`
con `data: { amount, maxAmount, currency, permissionCode }`.

### 1.4 Perfiles sugeridos (FR-093, T48)

Plantillas en código (`PerfilesSugeridos`), no roles integrados: crear un rol desde una produce un rol
normal y editable (`IsBuiltIn = false`) con la intersección entre la plantilla y el catálogo sembrado.
Traducción de la propuesta de seguridad a los códigos de §1.1 (lo que allá era `Inventory.Taxes`,
`Inventory.PaymentMethods` e `Inventory.Dian` hoy es `Core.Taxes`, `Core.PaymentMeans` y
`ElectronicInvoicing`).

| Plantilla | Actor | Permisos | Alcance y montos |
|---|---|---|---|
| `inventario.administrador` | Administrador del módulo | `Inventory.*`, `Core.Taxes.*`, `Core.PaymentMeans.*`, `ElectronicInvoicing.*` | alcance total (trae los `Scope.*`); sin límite de monto |
| `inventario.jefe` | Jefe de inventario | `Inventory.Catalog.*`, `Inventory.Warehouses.*`, `Inventory.Stock.View`, `Inventory.Costs.Read`, `Inventory.Costing.Manage`, `Inventory.Periods.*`, `Inventory.Adjustments.*`, `Inventory.Transfers.*`, `Inventory.Counts.*`, `Inventory.OpeningBalance.*`, `Inventory.Integrity.Verify`, `Inventory.Reports.View`, `Inventory.Reports.Export`, `Inventory.Alerts.*`, `Inventory.Approvals.View`, `Inventory.Approvals.Supervisor`, `Inventory.Documents.View`, `Inventory.DocumentTypes.View`, `Inventory.Parameters.View`, `Inventory.Scope.AllWarehouses` | todas las bodegas; límite en `Adjustments.Confirm` si la cooperativa lo fija |
| `inventario.bodeguero` | Bodeguero | `Inventory.Stock.View`, `Inventory.Catalog.View`, `Inventory.Warehouses.View`, `Inventory.DocumentTypes.View`, `Inventory.Documents.View`, `Inventory.Transfers.{View,Create,Dispatch,Receive}`, `Inventory.Counts.{View,Capture}`, `Inventory.Adjustments.{View,Create}`, `Inventory.Purchases.{View,Create}` (recepción), `Inventory.Alerts.View` | sus bodegas (filas de alcance) |
| `inventario.comprador` | Comprador | `Inventory.Purchases.{View,Create,Confirm,Void,RegisterRadianEvent,EmitRadianEvent}`, `Inventory.Catalog.View`, `Inventory.Warehouses.View`, `Inventory.DocumentTypes.View`, `Inventory.Documents.View`, `Inventory.Stock.View`, `Inventory.Prices.View`, `Inventory.Costs.Read`, `Inventory.Reports.View`, `Inventory.Alerts.View`, `Core.People.Create` | sus bodegas; monto máximo en `Purchases.Confirm` |
| `inventario.cajero` | Vendedor o cajero | `Inventory.Sales.{View,Create,Confirm,SellOnCredit}`, `Inventory.Pos.Sell`, `Inventory.CashSessions.{View,Open,Close}`, `Inventory.CashMovements.Create`, `Inventory.PointsOfSale.View`, `Inventory.Catalog.View`, `Inventory.Prices.View`, `Inventory.Stock.View`, `Inventory.Documents.{View,Reprint}`, `Inventory.Approvals.View` (pedir la aprobación presencial y seguir su estado), `Inventory.Alerts.View`, `Core.People.Create` | sus puntos de venta (y la bodega de su caja); monto máximo en `Sales.SellOnCredit` |
| `inventario.aprobador` | Aprobador | `Inventory.Adjustments.Approve`, `Inventory.Transfers.Approve`, `Inventory.Counts.Approve`, `Inventory.Purchases.Approve`, `Inventory.Sales.Approve`, `Inventory.CashMovements.Approve`, `Inventory.CashDifferences.Approve`, `Inventory.OpeningBalance.Approve`, `Inventory.Discounts.Authorize`, `Inventory.Approvals.{View,Supervisor}`, `Inventory.*.View`; **sin** `Create` ni `Confirm` | las bodegas y puntos que aprueba (el aprobador también necesita alcance) |
| `inventario.contador` | Contador (sólo consulta) | `Inventory.*.View`, `Inventory.Costs.Read`, `Inventory.Reports.{View,Export}`, `Inventory.Reconciliation.View`, `Inventory.Scope.AllWarehouses` | todas las bodegas; la matriz y los lotes (`Accounting.InventoryRules.*`, `Accounting.InventoryBatches.*`) van en su rol contable, no aquí |
| `inventario.auditor` | Auditor (sólo lectura) | `Inventory.*.View`, `Inventory.Costs.Read`, `Inventory.Reports.View`, `Inventory.Integrity.Verify`, `Inventory.CashSessions.ViewAll`, `Inventory.Scope.AllWarehouses`, `Inventory.Scope.AllPointsOfSale`, `AuditLog.View`, `AuditLog.Export`, `AuditLog.VerifyIntegrity` | todo, sin escritura (verificar la integridad levanta alertas pero no cambia datos) |

`Inventory.*.View` en una plantilla se expande a los códigos `View` del catálogo sembrado al crear el
rol; las acciones sensibles se listan una a una.

### 1.5 Rutas de plantillas de rol (plataforma)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /api/admin/roles/templates?module=Inventory` | Security.Roles.Create | `[{ key, actor, description, permissionCodes[], notes }]` con los códigos ya expandidos contra el catálogo de la cooperativa |
| `POST /api/admin/roles/from-template` | Security.Roles.Create | `{ templateKey, code, name, description? }` → 201 `{ rolePublicId, code, name, permissionCodes[], omitted[] }` (`CreateRoleFromTemplateCommand`, con las reglas de `CreateRoleCommand`; `omitted` = códigos de la plantilla que no existen en el catálogo). 422 `Security.Roles.CodeAlreadyExists`; plantilla inexistente: 404 `Generic.NotFound` |

### 1.6 Auditoría

Todo comando lo registra `AuditBehavior` con el módulo inferido del espacio de nombres (`Inventory`,
`Approvals`, `Alerts`, `Parameters`, `Taxes`…), actor (`IActorActual`), origen (`IOrigenDeLaPeticion`:
IP, User-Agent, canal `web`/`app` por la cabecera `X-Canal`, `pos`, `proceso`), el `reason` de los
comandos `IConMotivo`, la clave de idempotencia y `ActorKind` en metadata; un `Result.IsFailure` queda como
`Rejected` con su `Error.Code`. Las diferencias antes/después las agrega el `AuditableEntityInterceptor`
ampliado a `COR_AuditOutbox` en la misma transacción (T36, T37). Eventos explícitos que nacen en esta
parte (las lecturas y exportaciones no pasan por un comando): `Operation.Replayed` (§2.3),
`Inventory.Catalog.Exported` (§3.9), `Inventory.Integrity.Verified` (§6.2), `AuditLog.IntegrityVerified`
(plataforma). El ingreso a cada pantalla lo registra `RegistroDeAccesos` con módulo `Navigation`.

## 2. Convenciones

### 2.1 Sobre de error y estado HTTP

Una falla sale siempre como `{ code, message, traceId, data? }`. El estado lo infiere
`ErrorEnvelopeFilter` del código:

| Código | HTTP | Cuándo |
|---|---|---|
| `Validation.Invalid` | 400 | el validador del comando (FluentValidation) rechazó el cuerpo; `message` lista los campos |
| `Request.BodyInvalid` | 400 | el cuerpo no se pudo leer (un enum inexistente, un Guid mal formado); el mensaje dice el valor y lo admitido |
| `Operation.KeyRequired` | 400 | comando sin `Idempotency-Key` (§2.3) |
| `Archivo.Vacio`, `Archivo.ColumnaFaltante`, `Archivo.HojaFaltante`, `Archivo.Ilegible` | 400 | importaciones (§2.9; contracts/plantillas.md §0) |
| `*.NotFound`, `Generic.NotFound` | 404 | no existe, **o** no hay permiso, **o** está fuera del alcance (§2.2) |
| `Concurrency.StaleRowVersion` | 409 | el borrador cambió desde que se leyó (se envía `rowVersion`, §9.2) |
| cualquier otro | 422 | regla de negocio; `data` lleva lo necesario para que la pantalla diga qué hacer |

Un `2xx` con cuerpo devuelve el DTO; una acción sin valor, 204. Un POST que crea responde 201.

### 2.2 Sin permiso o fuera de alcance: el mismo 404

- Ruta sin su permiso: 404 `Generic.NotFound`, igual que una ruta inexistente (FR-009, SC-014).
- Recurso fuera del alcance del usuario (una bodega que no tiene, un documento cuyas bodegas no tiene,
  una solicitud de aprobación cuyo nivel no le toca): el **mismo** 404 que el recurso inexistente
  (`Inventory.Warehouse.NotFound`, `Inventory.Document.NotFound`, `Approvals.Request.NotFound`), con el
  mismo mensaje. Nunca 403.
- Las listas filtran en silencio: lo que está fuera del alcance no aparece y no cuenta en `totalCount`.
- Un identificador de otra entidad dentro del cuerpo (la bodega destino de un traslado, la ubicación de
  una línea) sigue la misma regla: fuera de alcance = no existe.

### 2.3 Idempotencia (FR-016, T13)

- **Cabecera** `Idempotency-Key: <uuid>` obligatoria en todo POST, PUT o DELETE que envía un comando
  (`IOperacionIdempotente`) de `Application/Inventory`, de la plataforma invocada desde la pantalla
  (parámetros, aprobaciones, alertas, montos, alcances) y de Core. Las consultas (todo GET, y los POST
  que son consultas: `integrity/verify`, `purchases/supplier-invoices/prefill`,
  `documents/{id}/prevalidate`, `documents/{id}/cost-impact` (§9.3), `sales/credit-evaluations` y
  `/api/accounting/inventory/batches/preview`) no la llevan. La
  ruta la copia a `OperationKey` del comando; `LosComandosDeInventarioLlevanClave` lo vigila.
- **El cliente** genera el UUID al **iniciar** la operación (abrir el diálogo, cargar el borrador), lo
  conserva en todo reintento (`RenovacionDeSesionHandler` ya clona las cabeceras) y lo renueva sólo tras
  un éxito o si cambia el contenido. Revisar una importación y aplicarla son dos operaciones: dos claves.
- **Primera vez**: `IdempotencyBehavior` abre `TransaccionExplicita`, inserta la fila de
  `COR_OperationKeys` (`Key`, `Operation`, `RequestSha256` = huella de ruta + cuerpo canónico,
  `CentralUserId`, `ActorName`) y guarda el resultado serializado **sólo si hubo éxito**, en la misma
  transacción. Si falla, se revierte y la clave no queda: reintentar con la misma clave vuelve a ejecutar.
- **Repetición** (doble clic, reintento de red, dos pestañas): mismo estado y mismo cuerpo que la primera
  vez + cabecera `Idempotent-Replayed: true` + evento de auditoría `Operation.Replayed`. Un duplicado que
  llega mientras el primero corre espera en el índice único y después recibe la respuesta guardada.
- **Misma clave, otro contenido** (otra ruta, otro cuerpo u otro usuario): 422 `Operation.KeyReused` con
  `data: { operation, firstUsedAt }`.
- La clave **no** es columna de ningún documento; las filas de `COR_OperationKeys` no se borran (B3).

### 2.4 Paginación y orden

Las listas que pueden crecer (productos, documentos, existencias, capturas, alertas, aprobaciones,
cifras de SOLIDO) reciben `?page=&pageSize=` y responden `PagedResult { items, page, pageSize,
totalCount, totalPages }` (`Application/Common/Paging`): `page` desde 1, `pageSize` por defecto 20,
máximo 200 (`PageRequest.MaxPageSize`; un valor mayor se recorta, no falla). Los catálogos chicos
(unidades, marcas, categorías, grupos, tipos de bodega, causas, canales, tipos de documento, bodegas,
ubicaciones, políticas) responden un arreglo sin paginar. Cada lista declara su orden fijo; no hay
`sort` libre. Las exportaciones y los informes no paginan (§27).

### 2.5 Sólo `PublicId` (Principio VI)

Todo identificador que entra o sale es el `PublicId` (Guid) de la entidad; las rutas usan `{id:guid}`. Los
`Id` internos (`int`/`bigint`, también `SEC_Users.Id`) nunca cruzan: un usuario sale como
`{ userPublicId, name }`. Los **códigos** de catálogo (`code`) son la nomenclatura de la cooperativa, para
personas y plantillas: las plantillas identifican por código, y la búsqueda de productos resuelve código
y código de barras (§3.5), pero ningún comando recibe un código donde espera una entidad. Excepciones de
clave natural: los períodos van por `{year}/{month}` (como `years/{year}` de la 009), las claves de
parámetro por `{module}/{key}` y los tipos de alerta por `{typeCode}`.

### 2.6 Enums, fechas, decimales y moneda

- **Enums**: entran por nombre o por número y salen como número (`EnumPorNombreONumero`). Aquí se
  escriben por nombre; los números son los de `data-model.md`. `ModoDeImportacion` viaja en la query
  como `review` o `apply` (sin distinguir mayúsculas).
- **Fechas de operación** `yyyy-MM-dd`, fecha **local de Colombia** (`IDateTimeService.HoyLocal`, T20);
  la que se propone al crear un borrador es la de hoy local. **Instantes** en UTC ISO-8601 con `Z`.
- **Decimales** exactos en JSON (número, nunca cadena): cantidades hasta 4 decimales (los que admita la
  unidad), factores y costos unitarios hasta 6, precios unitarios de línea hasta 6, montos en pesos 2,
  **tarifas como fracción** (0.19; 0.00966). Un valor con más decimales de los admitidos es
  `Validation.Invalid`, nunca se redondea en silencio.
- **Moneda**: todo documento lleva `currency` (`"COP"`) y `exchangeRate` (`1`) (FR-018); se pueden
  omitir y el servidor los pone. Otro valor → 422 `Inventory.Currency.NotSupported`.

### 2.7 Nombres de los códigos de error

- `Área.Recurso.Motivo`, en inglés y PascalCase (`Inventory.Stock.Insufficient`,
  `Inventory.Transfer.ReceiveExceedsDispatched`). Áreas de esta parte: `Inventory`, `Approvals`,
  `Alerts`, `Parameters`, `Operation` y `Taxation` (la UVT); se propagan tal cual los de
  `ElectronicInvoicing`, `Core` y `Attachments` donde la confirmación o un adjunto los producen, y se
  conservan los existentes `Catalogo.CodigoDuplicado`, `Archivo.*`, `Validation.Invalid`,
  `Concurrency.StaleRowVersion`, `Security.Roles.*` y `Generic.NotFound`.
- El recurso va en **singular** (`Inventory.Document.*`, `Inventory.Warehouse.*`), aunque la ruta sea
  plural. `*.NotFound` es 404; lo demás, 422 salvo `Validation.*`.
- `data` va en camelCase y trae **lo que la persona necesita para corregir**: la línea (`lineNumber`), el
  producto (`productPublicId`, `productCode`), la cantidad disponible, el período, los dependientes, el
  monto máximo, quién puede corregir (`whoFixes`). El `message` dice en español qué pasó y qué hacer
  (FR-020).
- Las tablas de cada sección son el catálogo de códigos de esta parte; un código que emita
  `Application/Inventory` y no figure en este contrato es un defecto del contrato.

### 2.8 Avisos, reconocimientos y motivo

- **Avisos**: una respuesta exitosa puede traer `warnings: [{ code, message, data }]`; no detienen nada
  (p. ej. `Inventory.Stock.BelowReorderPoint`, `Inventory.Prevalidation.NoResponse` con la política
  «confirmar con pendiente»).
- **Reconocimiento**: cuando una operación exige que alguien acepte un aviso (cerrar un período con
  borradores, activar con diferencia), el primer intento responde 422 con el código del aviso y `data`, y
  el segundo lleva la bandera nombrada (`acknowledgeWarnings`, `acceptDifference`,
  `confirmFiscalWithoutPosting`) y el `reason` cuando la acción lo exige. Se audita lo aceptado.
- **Motivo**: los comandos `IConMotivo` (anular, descartar, rechazar, reabrir, cambiar un parámetro,
  aceptar una diferencia, resolver una diferencia de traslado, atender una alerta con nota) exigen
  `reason` no vacío de hasta 500 caracteres; sin él, 400 `Validation.Invalid`. Se copia al evento de
  auditoría.

### 2.9 Plantillas de importación y exportación (FR-030, T49)

Mecánica común a todo catálogo y a las cargas de puesta en marcha (la de la 009 E2 más la revisión
previa). **Una sola fuente**: mecánica, `ImportResultDto`, `?withData=true`, códigos `Import.*` y
`Archivo.HojaFaltante`: ver contracts/plantillas.md §0. Este contrato no repite ni cambia nada de allí.
En resumen, para ubicar las rutas: en cada ruta base `GET {base}/template.xlsx` (con `?withData=true`
es la exportación: la plantilla llena para corregir y volver a importar; audita
`Inventory.Catalog.Exported`) y `POST {base}/import?mode=review|apply`; `review` no guarda nada y
`apply` es todo o nada (422 `Import.Invalid`). Un código que ya existe **actualiza** con las mismas
reglas que la edición unitaria (comparten el método de reglas del comando unitario, como
`ImportAccountsCommand` reusa `CreateAccountCommandHandler`), y las reglas de negocio responden con el
mismo código que el alta unitaria. Cada importación es un comando `Import…Command(ModoDeImportacion)`
con clave de idempotencia.

## 3. Catálogo — `/api/inventory` (`CatalogEndpoints.cs`, I1)

Reglas de todo catálogo del módulo:
- **Código** por `CodigoDeCatalogo`: alfanumérico, en mayúsculas, sin espacios, único en su tabla; 10
  caracteres, **20 en productos** (D1). Un duplicado responde 422 `Catalogo.CodigoDuplicado` con
  `data: { existingPublicId, existingName }`, y la pantalla lo consulta antes con
  `GET /api/catalogos/{catalogo}/codigo/{codigo}` (componente `CampoCodigo`; §17 lista los nombres de
  catálogo nuevos).
- **El código no se edita** en ningún catálogo del módulo: identifica la fila en las plantillas
  (importar actualiza por código) y en la matriz contable (T27: grupo contable, bodega, causa de ajuste…).
  Un código mal digitado se corrige inactivando esa fila y creando otra.
- **Nada con historia se borra**: los catálogos se inactivan (`/deactivate`) y se reactivan
  (`/reactivate`) con `reason`; sólo un producto sin ningún documento admite `DELETE` (FR-028), y es un
  borrado lógico (Principio VII).
- Leer exige `Inventory.Catalog.View`; escribir, `Inventory.Catalog.Manage`; salvo donde se diga.

### 3.1 Unidades de medida — `/units`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?includeInactive=` | Catalog.View | `[UnitOfMeasureDto { publicId, code, name, allowedDecimals, dianUnitCode, isSeeded, isActive, productsUsing }]`, por código |
| `POST /` | Catalog.Manage | `{ code, name, allowedDecimals (0..4), dianUnitCode }` → 201 `UnitOfMeasureDto`. `dianUnitCode` se valida contra `CatalogoDian` (Rec. 20): 422 `Inventory.Unit.DianCodeUnknown` |
| `PUT /{id}` | Catalog.Manage | `{ name, allowedDecimals, dianUnitCode }` → `UnitOfMeasureDto`. Bajar `allowedDecimals` por debajo de lo ya usado en líneas o kardex: 422 `Inventory.Unit.DecimalsInUse` (`data: { maxDecimalsUsed }`) |
| `POST /{id}/deactivate` · `/{id}/reactivate` | Catalog.Manage | `{ reason }`. Unidad base o alterna de un producto activo: 422 `Inventory.Unit.InUse` (`data: { products, examples[] }`) |

### 3.2 Categorías — `/product-categories`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?includeInactive=` | Catalog.View | `[CategoryDto { publicId, code, name, parentPublicId?, level, path, isActive, products }]`, en orden de árbol (`path` = «ABARROTES › GRANOS») |
| `POST /` | Catalog.Manage | `{ code, name, parentPublicId? }` → 201. Más de 5 niveles: 422 `Inventory.Category.TooDeep` (`data: { maxLevel: 5 }`) |
| `PUT /{id}` | Catalog.Manage | `{ name, parentPublicId? }` (mover una rama). 422 `Inventory.Category.Cycle` (el padre es un descendiente), `.TooDeep` (algún descendiente pasaría del nivel 5) |
| `POST /{id}/deactivate` · `/reactivate` | Catalog.Manage | `{ reason }`. Con hijas activas o productos activos: 422 `Inventory.Category.InUse` (`data: { children, products }`) |

### 3.3 Marcas — `/brands`

`GET /?includeInactive=` → `[{ publicId, code, name, isActive, products }]`; `POST /` `{ code, name }`;
`PUT /{id}` `{ name }`; `POST /{id}/deactivate` · `/reactivate` `{ reason }` (una marca con productos
activos se puede inactivar: los productos la conservan).

### 3.4 Grupos contables — `/accounting-groups`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?includeInactive=` | Catalog.View | `[{ publicId, code, name, isActive, products }]` |
| `POST /` · `PUT /{id}` | Catalog.Manage | `{ code, name }` / `{ name }`. El código lo usa la matriz contable (`AccountingGroupCode`, T27) y por eso nunca cambia |
| `POST /{id}/deactivate` · `/reactivate` | Catalog.Manage | `{ reason }`. Con productos inventariables activos: 422 `Inventory.AccountingGroup.InUse` |

Qué cuentas le corresponden a cada grupo lo dice la matriz de Contabilidad (§26), nunca este módulo.

### 3.5 Productos — `/products`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?search=&categoryPublicId=&brandPublicId=&accountingGroupPublicId=&kind=&status=&page=&pageSize=` | Catalog.View | `PagedResult<ProductListItemDto { publicId, code, name, kind, status, category { publicId, code, name }, brand?, baseUnitCode, accountingGroupCode?, primaryBarcode?, hasMovements }>`, por código. `categoryPublicId` incluye las subcategorías |
| `GET /{id}` | Catalog.View | `ProductDto` (abajo) |
| `GET /search?q=&warehousePublicId=&kinds=&includeInactive=&take=` | Catalog.View | `SearchProductsQuery` (T43) → `{ exact?: ProductSearchItemDto, items: ProductSearchItemDto[] }` (abajo) |
| `POST /` | Catalog.Manage | `CreateProductRequest` → 201 `ProductDto` |
| `PUT /{id}` | Catalog.Manage | `UpdateProductRequest` → `ProductDto` |
| `POST /{id}/status` | Catalog.Manage | `{ status: Active \| Inactive \| Blocked, reason }` → `ProductDto`. Mismo estado: 422 `Inventory.Product.StatusUnchanged` |
| `DELETE /{id}` | Catalog.Manage | 204 si el producto nunca estuvo en un documento (ni en borrador) ni en una lista de precios. Con historia: 422 `Inventory.Product.HasHistory` (`data: { alternatives: ["Inactive", "Blocked"] }`, US1-3) |

**`CreateProductRequest`**: `{ code, name, kind, categoryPublicId, brandPublicId?, baseUnitPublicId,
accountingGroupPublicId?, vatSaleTreatment, withholdingConceptPublicId?, reference?, weight?, volume?,
tracksLot?, tracksSerial?, tracksExpiry?, units?: [{ unitPublicId, factor, usage }], barcodes?: [{
barcode, unitPublicId? }], taxes?: [{ taxDefinitionPublicId, taxRatePublicId?, taxableUnitsPerBaseUnit?
}] }`. En I1 `kind` admite `Inventoriable` y `Service`; `Combo`, `Kit`, `Template` y `Variant`, y las
marcas de lote, serie y vencimiento, responden 422 `Inventory.Product.KindNotAvailable` /
`.TrackingNotAvailable` (`data: { availableIn: "I6" }`) hasta I6. `accountingGroupPublicId` es obligatorio
si es inventariable (422 `Inventory.Product.AccountingGroupRequired`) y no se admite en un servicio. Las
unidades, los códigos y los impuestos del alta siguen las mismas reglas de sus subrecursos (§3.6), en la
misma transacción.

**`UpdateProductRequest`**: lo mismo sin `code`, `kind`, `units`, `barcodes` ni `taxes` (tienen sus
rutas). `baseUnitPublicId` y `accountingGroupPublicId` sólo cambian si el producto **nunca** tuvo
movimientos: si los tuvo, 422 `Inventory.Product.BaseUnitLocked` (FR-025) o
`Inventory.Product.UseReclassifyAccountingGroup` (el grupo cambia por §3.6.4).

**`ProductDto`**: `{ publicId, code, name, kind, status, category { publicId, code, name, path }, brand?,
baseUnit { publicId, code, name, allowedDecimals }, accountingGroup? { publicId, code, name },
vatSaleTreatment, withholdingConcept? { publicId, code, name }, reference?, weight?, volume?, tracksLot,
tracksSerial, tracksExpiry, units[], barcodes[], taxes[], images: [{ attachmentPublicId, fileName }],
hasMovements, createdAt, createdBy, updatedAt? }` (las listas con la forma de §3.6).

**Búsqueda** (FR-020, T43, SC-009):
- `q` de al menos 2 caracteres (si no, 400 `Validation.Invalid`); `take` por defecto 20, máximo 50.
- Primero la lectura **exacta**: igualdad contra los códigos de barras vivos y contra el código. Si
  acierta, sale en `exact`, con `packUnit` cuando el código es de un empaque (US1-5).
- Después, mientras se escribe: cada término normalizado (`NormalizadorDeBusqueda`: sin tildes, en
  mayúsculas, espacios colapsados) debe aparecer en `INV_Products.SearchText` (código, nombre, referencia,
  marca y códigos de barras); orden: prefijo de código, prefijo de nombre, resto por nombre.
- Sólo activos, salvo `includeInactive=true` (consultas); los bloqueados salen con su `status` para que la
  pantalla los marque.
- `ProductSearchItemDto { publicId, code, name, reference?, brandName?, baseUnitCode, status,
  matchedBarcode?, packUnit? { productUnitPublicId, unitCode, factor }, available? }`. `available` sólo
  si viene `warehousePublicId` y el usuario tiene `Stock.View`; una bodega fuera del alcance es 404
  `Inventory.Warehouse.NotFound`.
- El cliente espera 200 ms desde la última tecla y cancela la búsqueda anterior; el lector de código de
  barras en modo teclado llega como una sola ráfaga terminada en Enter y usa `exact`.

### 3.6 Lo que cuelga del producto

#### 3.6.1 Unidades alternas — `/products/{id}/units`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /` | Catalog.View | `[{ productUnitPublicId, unit { publicId, code, name, allowedDecimals }, factor, usage: Purchase \| Sale \| Both }]` (el factor convierte a la unidad base: caja × 12 = 12) |
| `POST /` | Catalog.Manage | `{ unitPublicId, factor, usage }` → 201. Factor > 0 con hasta 6 decimales. La unidad base: 422 `Inventory.ProductUnit.IsBaseUnit`; ya está: `.Duplicate` |
| `PUT /{productUnitId}` | Catalog.Manage | `{ factor, usage }`. Cambiar el factor de una unidad con movimientos: 422 `Inventory.ProductUnit.FactorLocked` (el kardex conserva el de cada línea; se crea otra unidad) |
| `DELETE /{productUnitId}` | Catalog.Manage | 204 (baja lógica). En un borrador, con códigos de barras vivos o en una lista de precios: 422 `Inventory.ProductUnit.InUse` |

#### 3.6.2 Códigos de barras — `/products/{id}/barcodes`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /` | Catalog.View | `[{ barcodePublicId, barcode, productUnitPublicId?, unitCode }]` |
| `POST /` | Catalog.Manage | `{ barcode, productUnitPublicId? }` (hasta 50 caracteres imprimibles, sin espacios; el empaque es una unidad alterna del producto) → 201. Ya asignado a un código vivo: 422 `Inventory.Barcode.Duplicate` con `data: { productPublicId, productCode, productName }` (FR-024, US1-4) |
| `DELETE /{barcodeId}` | Catalog.Manage | 204; el código queda libre para otro producto |

#### 3.6.3 Impuestos del producto — `/products/{id}/taxes`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /` | Catalog.View | `ProductTaxesDto { vatSaleTreatment, withholdingConcept?, taxes: [{ taxDefinition { publicId, code, name, kind }, taxRate? { publicId, code, rate?, amountPerUnit?, validFrom, validTo? }, taxableUnitsPerBaseUnit? }] }` |
| `PUT /` | Catalog.Manage | reemplaza el conjunto: `{ vatSaleTreatment: Taxed \| Exempt \| Excluded, withholdingConceptPublicId?, taxes: [{ taxDefinitionPublicId, taxRatePublicId?, taxableUnitsPerBaseUnit? }] }` → `ProductTaxesDto` |

Errores: `Inventory.ProductTax.VatRateRequired` (gravado sin tarifa de IVA), `.VatRateNotAllowed` (exento
o excluido con tarifa distinta de cero), `.RateNotOfDefinition`, `.WithholdingNotAllowed` (una retención
no es impuesto del producto: va por el concepto), `.UnitsRequired` (impuesto por unidad sin
`taxableUnitsPerBaseUnit`), `.Duplicate`; `Core.Tax.NotFound` / `Core.TaxRate.NotFound` /
`Core.WithholdingConcept.NotFound` (404). El cambio rige para lo que se confirme después; lo confirmado
conserva su foto en `INV_DocumentTaxLines` (T22).

#### 3.6.4 Cambio de grupo contable — `/products/{id}/accounting-group`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /` | Catalog.View | historial `[{ changePublicId, from { code, name }, to { code, name }, effectiveDate, reason, changedBy, changedAt, quantity, value?, messagePublicId? }]` (`value` con `Costs.Read`) |
| `POST /` | Catalog.ReclassifyAccountingGroup | `{ accountingGroupPublicId, effectiveDate, reason }` → 201 `{ changePublicId, from, to, effectiveDate, byWarehouse: [{ warehousePublicId, warehouseCode, quantity, value? }], messagePublicId? }` (`ChangeProductAccountingGroupCommand`, FR-027) |

Rige desde `effectiveDate` para los movimientos siguientes. Si a esa fecha hay existencia, emite
`GrupoContableReclasificado` con cantidad y valor por bodega (modo de paso general, T9; sin existencia
no hay mensaje). Errores: `Inventory.Product.NotInventoriable`, `.AccountingGroupUnchanged`,
`Inventory.AccountingGroup.Inactive`, `Inventory.Period.Closed` (fecha en período cerrado),
`Inventory.Document.DateInFuture`, `Inventory.Product.MovementsAfterEffectiveDate` (`data: {
lastMovementDate }`: la fecha efectiva no puede dejar movimientos ya registrados en el grupo equivocado).

#### 3.6.5 Imágenes

Son adjuntos con dueño `InventoryProduct` (T41) por las rutas de la feature 011
(`POST /api/attachments/uploads`, `/confirm`, `/download-link`, `GET /api/attachments/by-owner`): leer con
`Inventory.Catalog.View`, subir y borrar con `Inventory.Catalog.Manage`, sólo tipos de imagen; el borrado
queda auditado. No hay rutas propias en el módulo.

### 3.7 Causas de ajuste — `/adjustment-causes`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?includeInactive=` | Catalog.View | `[{ publicId, code, name, isSeeded, isActive }]` (semilla: merma o faltante, daño, vencimiento, hurto, diferencia de conteo, destrucción, reclamación al transportador) |
| `POST /` · `PUT /{id}` | Catalog.Manage | `{ code, name }` / `{ name }`. El código lo usa la matriz (`ReasonCode` en `Baja` y `AjusteNegativo`) |
| `POST /{id}/deactivate` · `/reactivate` | Catalog.Manage | `{ reason }`. La causa que usa el ajuste de conteo («diferencia de conteo»): 422 `Inventory.AdjustmentCause.RequiredBySystem` |

### 3.8 Canales de venta — `/sales-channels`

`GET /?includeInactive=` → `[{ publicId, code, name, isActive }]`; `POST /` `{ code, name }`; `PUT /{id}`
`{ name }`; `POST /{id}/deactivate` · `/reactivate` `{ reason }` (en uso por un punto de venta, un tipo de
documento o una lista de precios vigentes: 422 `Inventory.SalesChannel.InUse`). Los usan tipos de
documento (§8), puntos y listas (§19–§20).

### 3.9 Plantillas de parametrización (FR-030, FR-095, T49)

`GET /api/inventory/templates` (Catalog.View) → la tabla de contracts/plantillas.md §0.1 (orden de
carga, plantilla, hojas, ruta base, comando, «importa desde»), con `canDownload` y `canImport` según los
permisos de quien pregunta: la lista que la pantalla `/inventario/plantillas` muestra. Todas se
**descargan desde I1** (el modelo de cada catálogo se congela antes de publicar su plantilla); algunas
se **importan** después, y su ruta de importación no existe hasta entonces.

Orden, hojas y columnas: contracts/plantillas.md §0.1 y §1–§16; permisos por plantilla y por columna:
§0.7 de ese contrato. Esta parte implementa los comandos
`Import{UnitsOfMeasure,ProductCategories,Brands,AccountingGroups,Products,Warehouses,DocumentTypes}Command`
y los de la puesta en marcha (§13.1, §13.2); los del catálogo tributario y de vendedores están en la
§30 y §31.

Reglas propias de esta parte, que la plantilla ya recoge: cambiar por importación el grupo de un
producto **con movimientos** exige además `Catalog.ReclassifyAccountingGroup` (sin él, error de celda
`Import.Cell.PermissionRequired`) y el `reason` de la importación (`requiresReason`; sin él,
`Import.Cell.Required`), y se aplica como §3.6.4 con fecha efectiva de hoy. En tipos de documento, el
modo de paso, la granularidad, el disparador y la hora de lote y los niveles de aprobación se escriben
como vigencias (parámetros del tipo, §7, y política, §15) desde `vigenteDesde`; la regla de cadenas se
evalúa sobre el archivo entero (todos los tipos de una cadena con el mismo modo, o error de fila
`Inventory.PostingMode.ChainMismatch`), y dejar sin paso un tipo fiscal exige
`DocumentTypes.DisableFiscalPosting` (error de fila `Inventory.PostingMode.FiscalRequiresConfirmation`;
en la importación la confirmación es `confirmFiscalWithoutPosting` más el permiso, contracts/plantillas.md §8).

La exportación de cada catálogo es `GET …/template.xlsx?withData=true` (contracts/plantillas.md §0.6):
exige además `Inventory.Catalog.Export` y deja el evento `Inventory.Catalog.Exported`
(`{ catalog, rows }`); ninguna de las de esta parte lleva datos personales.

### 3.10 Errores del catálogo

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Catalogo.CodigoDuplicado` | 422 | código repetido en su tabla | `{ existingPublicId, existingName }` |
| `Inventory.{Unit,Category,Brand,AccountingGroup,Product,ProductUnit,Barcode,AdjustmentCause,SalesChannel}.NotFound` | 404 | no existe o está dado de baja | |
| `Inventory.Unit.DianCodeUnknown` · `.DecimalsInUse` · `.InUse` | 422 | §3.1 | `{ maxDecimalsUsed }` · `{ products, examples[] }` |
| `Inventory.Category.TooDeep` · `.Cycle` · `.InUse` | 422 | §3.2 | `{ maxLevel }` · — · `{ children, products }` |
| `Inventory.AccountingGroup.InUse` · `.Inactive` | 422 | §3.4, §3.6.4 | |
| `Inventory.Product.KindNotAvailable` · `.TrackingNotAvailable` | 422 | clase de producto o control de lote/serie antes de I6 | `{ availableIn }` |
| `Inventory.Product.WithholdingConceptRequired` **(nuevo, T217)** | 422 | producto sin concepto de retención en compras (obligatorio salvo plantillas y combos, data-model §1.6) | |
| `Inventory.Product.AccountingGroupRequired` · `.BaseUnitLocked` · `.UseReclassifyAccountingGroup` · `.HasHistory` · `.StatusUnchanged` · `.NotInventoriable` · `.AccountingGroupUnchanged` · `.MovementsAfterEffectiveDate` | 422 | §3.5, §3.6.4 | `{ alternatives[] }` en `HasHistory`; `{ lastMovementDate }` |
| `Inventory.ProductUnit.IsBaseUnit` · `.Duplicate` · `.FactorLocked` · `.InUse` | 422 | §3.6.1 | |
| `Inventory.Barcode.Duplicate` | 422 | FR-024 | `{ productPublicId, productCode, productName }` |
| `Inventory.ProductTax.VatRateRequired` · `.VatRateNotAllowed` · `.RateNotOfDefinition` · `.WithholdingNotAllowed` · `.UnitsRequired` · `.Duplicate` | 422 | §3.6.3 | |
| `Inventory.AdjustmentCause.RequiredBySystem` · `Inventory.SalesChannel.InUse` | 422 | §3.7, §3.8 | |
| `Import.Invalid` | 422 | `mode=apply` con errores de fila (contracts/plantillas.md §0.5) | el `ImportResultDto` completo |

## 4. Bodegas, ubicaciones y reorden — `/api/inventory` (`WarehousesEndpoints.cs`, I1)

Estructura cooperativa → sucursal contable (`COR_Branches`, la de la 009) → bodega → ubicación (FR-032).
Toda lectura pasa por el alcance (§1.2).

### 4.1 Tipos de bodega — `/warehouse-types`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?includeInactive=` | Warehouses.View | `[{ publicId, code, name, behavior: Operational \| Transit, isSeeded, isActive }]` (semilla: principal, punto de venta, averías, cuarentena, tránsito) |
| `POST /` · `PUT /{id}` | Warehouses.Manage | `{ code, name, behavior }` / `{ name }`. El comportamiento es fijo del sistema y no cambia; `Transit` sólo lo tiene el tipo sembrado: 422 `Inventory.WarehouseType.TransitIsSystem` |
| `POST /{id}/deactivate` · `/reactivate` | Warehouses.Manage | `{ reason }`. Con bodegas activas: 422 `Inventory.WarehouseType.InUse` |

### 4.2 Bodegas — `/warehouses`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?branchPublicId=&typePublicId=&activationStatus=&includeTransit=&includeInactive=` | Warehouses.View | `[WarehouseDto]` del alcance, por sucursal y código |
| `GET /{id}` | Warehouses.View | `WarehouseDto` + `locations[]` + `openingBalance? { documentPublicId, number?, status, cutoffDate }` + `activation?` (§13.3) |
| `POST /` | Warehouses.Manage | `{ code, name, branchPublicId, warehouseTypePublicId, notes?, transitWarehouse?: { code, name } }` → 201 `{ warehouse: WarehouseDto, transitWarehouseCreated?: { publicId, code, name } }`. `transitWarehouse` sólo se admite (y sólo se usa) cuando es la primera bodega operativa de la sucursal; en otro caso, 400 `Validation.Invalid` |
| `PUT /{id}` | Warehouses.Manage | `{ name, warehouseTypePublicId, notes? }` → `WarehouseDto`. La sucursal no cambia; el tipo sólo por otro del mismo comportamiento (422 `Inventory.Warehouse.BehaviorLocked`) |
| `POST /{id}/deactivate` · `/reactivate` | Warehouses.Manage | `{ reason }`. Con existencia en cualquier ubicación: 422 `Inventory.Warehouse.HasStock` (`data: { products, quantity }`); tránsito con existencia o despachos abiertos: `.TransitHasStock` |

**`WarehouseDto`**: `{ publicId, code, name, branch { publicId, code, name }, type { publicId, code, name,
behavior }, isTransit, transitWarehouse? { publicId, code }, activationStatus: NotActivated \| Active,
cutoffDate?, activatedAt?, activatedBy?, isActive, isDefault, negativeStockAllowed }` — `isDefault` es la
bodega por defecto **del usuario** (`INV_UserWarehouseScopes.IsDefault`); `negativeStockAllowed` es el
valor de `Existencias.StockNegativoPermitido` vigente hoy para la bodega.

La bodega de tránsito de una sucursal **se crea con la primera bodega operativa de la sucursal**, en la
misma transacción (tipo sembrado): su código lo propone el sistema (`TR` + código de la sucursal) y lo
puede fijar quien crea (campo opcional `transitWarehouse { code, name }` del `POST`, o una fila de tipo
tránsito en la plantilla de bodegas, contracts/plantillas.md §7); **no se crea sola fuera de ese
momento**. Como es dimensión de la matriz (T27), su código sigue las reglas de todo catálogo y no se
edita. Cada bodega nace con su ubicación por defecto `GENERAL`. Una bodega nueva nace `NotActivated`: sólo admite su saldo inicial hasta activarse
(§13.3); la de tránsito se considera activa cuando lo está alguna bodega de su sucursal. Crearla no la
asigna a nadie: quien no tiene alcance total la ve después de que `Scopes.Manage` se la asigne. Si la
sucursal no tiene municipio (`COR_Branches.MunicipalityDaneCode`, que se escribe por `POST/PUT
/api/core/branches`: §29), la respuesta trae el aviso
`Inventory.Branch.MunicipalityMissing`: las compras de esa bodega no tendrán municipio propuesto para la
ReteICA. Fuera de ese momento una bodega de tránsito no se crea a mano (un `POST` con un tipo `Transit`: 422
`Inventory.WarehouseType.TransitIsSystem`).

### 4.3 Ubicaciones — `/warehouses/{id}/locations`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?includeInactive=` | Warehouses.View | `[{ publicId, code, name, isDefault, isActive }]` |
| `POST /` | Warehouses.Manage | `{ code, name, isDefault? }` → 201 (código único dentro de la bodega: `Catalogo.CodigoDuplicado`). En tránsito: 422 `Inventory.Location.TransitHasOnlyDefault` |
| `PUT /{locationId}` | Warehouses.Manage | `{ name, isDefault }`: marcar otra por defecto desmarca la anterior |
| `POST /{locationId}/deactivate` · `/reactivate` | Warehouses.Manage | `{ reason }`. Con existencia: 422 `Inventory.Location.HasStock`; la de por defecto: `.IsDefault` |

### 4.4 Mínimos, máximos y punto de reorden — `/reorder-policies`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?warehousePublicId=&productPublicId=&belowReorderPoint=&page=&pageSize=` | Warehouses.View | `PagedResult<{ publicId, product { publicId, code, name }, warehouse { publicId, code }, minimum, maximum, reorderPoint, position?, available? }>` (`position` y `available` con `Stock.View`) |
| `PUT /` | Warehouses.Manage | alta o cambio por (producto, bodega): `{ productPublicId, warehousePublicId, minimum, maximum, reorderPoint }` → la fila. Exige `0 ≤ minimum ≤ reorderPoint ≤ maximum`: 422 `Inventory.ReorderPolicy.Invalid`; en tránsito: `.TransitNotAllowed`; servicio: `Inventory.Product.NotInventoriable` |
| `DELETE /{id}` | Warehouses.Manage | 204 |

Posición = disponible + en tránsito hacia la bodega + por recibir (órdenes aprobadas no recibidas; 0
hasta I5) (FR-035). Las alertas `Inventario.Reorden` (posición ≤ punto) e `Inventario.Quiebre`
(disponible < mínimo) las levanta la confirmación de una salida y la tarea nocturna, una pendiente por
producto y bodega (`DedupKey`).

### 4.5 Errores de bodegas

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Inventory.{WarehouseType,Warehouse,Location,ReorderPolicy}.NotFound` · `Inventory.Branch.NotFound` | 404 | no existe o fuera del alcance | |
| `Inventory.WarehouseType.TransitIsSystem` · `.InUse` | 422 | §4.1 | |
| `Inventory.Warehouse.BehaviorLocked` · `.HasStock` · `.TransitHasStock` | 422 | §4.2 | `{ products, quantity }` |
| `Inventory.Location.TransitHasOnlyDefault` · `.HasStock` · `.IsDefault` | 422 | §4.3 | |
| `Inventory.ReorderPolicy.Invalid` · `.TransitNotAllowed` | 422 | §4.4 | `{ minimum, reorderPoint, maximum }` |
| `Inventory.Branch.MunicipalityMissing` | aviso | §4.2 | `{ branchPublicId }` |

## 5. Existencias — `/api/inventory/stock` (`StockEndpoints.cs`, I1)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?warehousePublicId=&productPublicId=&categoryPublicId=&locationPublicId=&search=&onlyWithStock=&belowReorderPoint=&page=&pageSize=` | Stock.View | `PagedResult<StockRowDto { product { publicId, code, name, baseUnitCode, status }, warehouse { publicId, code, name, isTransit }, physical, reserved, available, inTransitTo, averageCost?, value?, minimum?, reorderPoint?, maximum?, position? }>`, por código de producto y bodega |
| `GET /{productId}` | Stock.View | `ProductStockDto { product, totals { physical, reserved, available, inTransit, value? }, byWarehouse: [{ warehouse, physical, reserved, available, inTransitTo, value? }], byLocation: [{ warehouse, location { publicId, code }, lot?, quantity }], inTransit: [{ transferPublicId, dispatchNumber, from, to, quantity, dispatchedOn }], costState?: { scope: Cooperative \| Warehouse, method: WeightedAverage \| Fifo, quantity, averageCost, lastUnitCost, value } }` |

- **Física** es la proyección `INV_StockBalances` (y por ubicación y lote, `INV_StockDetails`), que
  coincide siempre con la suma del kardex (FR-003); **reservada** son las reservas de pedidos vigentes (0
  hasta I6); **disponible** = física − reservada; **en tránsito hacia** una bodega = lo despachado hacia
  ella que ni se recibió ni se resolvió (FR-033). Un documento confirmado se ve en la consulta siguiente.
- **Valores** (`averageCost`, `value`, `costState`) sólo con `Inventory.Costs.Read`; sin él salen `null`.
  El valor de una bodega es su cantidad × el promedio de su ámbito de costeo, nunca la suma de los costos
  totales del kardex de esa bodega (T18).
- Cantidades en unidad base. Sin `warehousePublicId`, suma las bodegas del alcance; la existencia propia
  de una bodega de tránsito sólo con alcance total o asignación explícita (T35).
- El valorizado a una fecha pasada, el kardex exportable y los comparativos con SOLIDO son vistas de
  informe (`valuation`, `kardex`, `legacy-comparison-*`; §27).

## 6. Kardex e integridad

### 6.1 Consulta del kardex

No hay ruta de kardex bajo `/api/inventory`: la pantalla `/inventario/kardex` usa la vista `kardex` de
`/api/reports/inventory/kardex?format=json` (como `/contabilidad/libro-auxiliar` usa `ledger` en la 009),
y la misma vista exporta con `format=xlsx|pdf` (convenciones de informes y permisos de exportación en
§27).

| Parámetro | Obligatorio | Nota |
|---|---|---|
| `productPublicId` | sí | un producto por consulta |
| `warehousePublicId`, `locationPublicId`, `lot` | no | sin bodega, las del alcance |
| `from`, `to` | sí | rango de fechas de operación de hasta 5 años; la primera fila es el saldo al día anterior a `from` |
| `includeCostAdjustments` | no (sí por defecto) | muestra las líneas de ajuste de costo (`Kind = CostAdjustment`) |

Columnas (`TablaExportable`, columnas ocultas con `_`): fecha de operación, fecha de registro, documento
(clase, tipo y número; `_documento` con su `PublicId` para ir a la pantalla del documento), tipo
(entrada, salida, ajuste de costo) y motivo (`KardexReason`), bodega, ubicación, lote, entrada, salida,
saldo en cantidad, costo unitario, costo total, saldo en valor, promedio y usuario. Orden
`(OperationDate, Id)`. La ruta exige `Inventory.Reports.View` (exportar, `Reports.Export`); las columnas de costo y
valor sólo con `Inventory.Costs.Read`. En ámbito de costeo cooperativa y con bodega, el saldo en valor es
cantidad de la bodega × promedio del ámbito (T18).

### 6.2 Verificación y reconstrucción — `/api/inventory/integrity`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `POST /verify` | Integrity.Verify | `{ productPublicIds?: [], warehousePublicIds?: [] }` (vacío = todo lo del alcance) → 200 `IntegrityReportDto { verifiedAt, checked: { stockBalances, stockDetails, costStates, costLayers }, incidents: [{ kind: StockBalance \| StockDetail \| CostState \| CostLayer, product { publicId, code }, warehouse?, location?, lot?, field, expected, actual, difference }], alertPublicId? }` (`VerifyInventoryIntegrityQuery`, FR-003). Es consulta: sin clave de idempotencia |
| `POST /rebuild` | Integrity.Rebuild | `{ productPublicIds?: [], warehousePublicIds?: [], reason }` → 200 `{ rebuiltAt, rows: { stockBalances, stockDetails, costStates, costLayers }, corrected: [{ kind, product, warehouse?, field, before, after }] }` (`RebuildInventoryProjectionsCommand`) |

Verificar compara las sumas del kardex con cada proyección; si encuentra diferencias levanta la alerta
`Inventario.IncidenteDeIntegridad` (una pendiente por alcance verificado) y deja el evento
`Inventory.Integrity.Verified` con el resultado; la tarea nocturna (`ProgramadorDeTareas`) corre la misma
consulta sobre todo. Reconstruir recalcula las proyecciones desde el kardex **sin tocar el kardex**, por
producto y en transacciones separadas, tomando el cerrojo de las filas de ese producto (sus
confirmaciones esperan); sin filtros puede tardar minutos y la pantalla espera con su indicador.

### 6.3 Errores

| Código | HTTP | Cuándo |
|---|---|---|
| `Inventory.Product.NotFound` · `Inventory.Warehouse.NotFound` | 404 | filtro inexistente o fuera del alcance |

## 7. Parámetros — `/api/inventory/parameters` (`ParametersEndpoints.cs`, I1)

Una ruta para todas las claves con vigencia de `COR_ParameterVersions` (módulos `INV`, `TAX`, `EINV`; el
catálogo de claves, valores admitidos, defectos seguros y ámbitos está en `data-model.md`, tomado de
`decisiones-transversales.md` §2.8). Las definiciones (`DefinicionDeParametro`) viven en código, en los catálogos cerrados
`ParametrosDeInventario`, `ParametrosTributarios` y `ParametrosDeFacturacionElectronica`; la tabla sólo
guarda vigencias. Leer un valor en el servidor es sólo `LectorDeParametros` (T21).

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?module=&key=&asOf=` | Parameters.View | `[ParameterDto { module, key, description, type: Bool \| Int \| Decimal \| Text \| Date \| Time \| Choice, allowedValues?, defaultValue, allowedScopes[], sealedOnConfirm, requiredPermission?, availableFrom, current: { value, source: Version \| Default, validFrom?, validTo?, reason?, legalSource?, changedBy?, changedAt? }, overrides: [{ scopeKind, scope { publicId, code, name }, value, validFrom, validTo? }], scheduled: [{ scopeKind, scope?, value, validFrom }] }]` a la fecha `asOf` (por defecto hoy) |
| `GET /{module}/{key}/history?scopeKind=&scopePublicId=` | Parameters.View | `[{ versionPublicId, scopeKind, scope?, value, validFrom, validTo?, reason, legalSource?, createdBy, createdAt }]`, la más reciente primero |
| `POST /{module}/{key}/versions` | Parameters.Manage (+ el de la clave) | `{ scopeKind, scopePublicId?, chain?, value, validFrom, reason, legalSource?, confirmFiscalWithoutPosting? }` → 201 `{ versionPublicIds[], previousClosedOn?, affectedDocumentTypes?: [{ publicId, code, name }] }` (`AddParameterVersionCommand`) |

`ParameterDto.type` es texto de presentación, no un enum de dominio. Reglas del alta:

- **Valor**: texto en el formato del tipo (`true`/`false`, decimal con punto, `HH:mm` hora de Colombia,
  `yyyy-MM-dd`, o uno de los valores admitidos: `EnLinea`, `PromedioPonderado`…). Un valor no admitido
  es 422 `Parameters.ValueNotAllowed` con `data: { allowed[] }`, **nunca** el defecto.
- **Ámbito**: `scopeKind` (`ParameterScopeKind`: `None`, `Warehouse`, `DocumentType`, `PointOfSale`,
  `CashRegister`, `ThirdPartyKind`) debe estar entre los de la clave (422 `Parameters.ScopeNotAllowed`,
  `data: { allowed[] }`); `scopePublicId` es la entidad del ámbito (404 si no existe o está fuera del
  alcance) y se omite con `None`.
- **Vigencia**: la nueva cierra la anterior del mismo (clave, ámbito) la víspera de `validFrom`; si ya
  hay una vigencia que empieza después, 422 `Parameters.Overlaps` (`data: { existingValidFrom }`).
  `validFrom` puede ser pasada pero no dentro de un período de inventario cerrado (422
  `Parameters.ValidFromInClosedPeriod`, `data: { lastClosedDate }`): lo ya confirmado no cambia, y lo
  sellado al confirmar (modo de paso, modo de emisión, numeración) nunca se recalcula (FR-012, FR-075).
- **Permiso de la clave** además de `Parameters.Manage`: `Costeo.*` exige `Inventory.Costing.Manage`;
  las `TAX`, `Core.Taxes.Manage`; las `EINV`, `ElectronicInvoicing.Settings.Manage`. Sin él, 422
  `Parameters.PermissionRequired` (`data: { permissionCode }`).
- **`Costeo.Metodo` y `Costeo.Ambito`**: sólo desde el primer día de un período abierto sin movimientos
  posteriores (422 `Parameters.RequiresPeriodStart`, `data: { earliestAllowed? }`); `Peps` responde
  `Parameters.ValueNotAllowed` hasta I5.
- **`Contabilidad.ModoDePaso` por tipo de documento** (FR-075): si el tipo pertenece a una cadena
  (`PostingChain` `Purchases`, `Sales`, `Transfers`), el cuerpo trae `chain` en lugar de `scopePublicId` y
  el comando escribe una vigencia por cada tipo activo de la cadena, en la misma transacción
  (`affectedDocumentTypes`); un `scopePublicId` de un tipo encadenado responde 422
  `Inventory.PostingMode.ChainMismatch` con `data: { chain, documentTypes: [{ publicId, code, name }] }`.
  Los tipos sin cadena (ajustes, conteos…) van uno a uno.
- **Dejar sin paso un tipo fiscal** —`NoPasa` sobre un tipo o una cadena con tipos fiscales, o sobre el
  valor general cuando un tipo fiscal lo hereda— exige `Inventory.DocumentTypes.DisableFiscalPosting`
  (si no, `Parameters.PermissionRequired`) **y** `confirmFiscalWithoutPosting: true`; sin la confirmación,
  422 `Inventory.PostingMode.FiscalRequiresConfirmation` con `data: { fiscalDocumentTypes: [{ publicId,
  code, name, class }] }`, que la pantalla muestra para confirmar. Queda auditado con esa lista.
- **Stock negativo** (`Existencias.StockNegativoPermitido`): general con excepción por bodega (FR-034).
- Cada alta queda auditada con antes y después y el `reason` (FR-012, US12-3).

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Parameters.KeyNotFound` | 404 | clave inexistente en el catálogo del módulo | |
| `Parameters.ValueNotAllowed` · `.ScopeNotAllowed` | 422 | valor o ámbito fuera de lo admitido | `{ allowed[] }` |
| `Parameters.Overlaps` · `.ValidFromInClosedPeriod` · `.RequiresPeriodStart` · `.PermissionRequired` | 422 | ver reglas | |
| `Inventory.PostingMode.ChainMismatch` · `.FiscalRequiresConfirmation` | 422 | FR-075 | ver reglas |

## 8. Tipos de documento — `/api/inventory/document-types` (`DocumentTypesEndpoints.cs`, I1)

Las **clases** son fijas del sistema (`DocumentClass`, comportamiento en `ClasesDeDocumento`); los
**tipos** eligen su clase y parametrizan consecutivo, campos obligatorios, bodegas y canal (FR-036,
FR-037). La política de aprobación del tipo se administra en §15.1 y el modo de paso, su granularidad y
su horario son parámetros con ámbito `DocumentType` (§7); la pantalla `/inventario/tipos-de-documento`
los compone.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /classes` | DocumentTypes.View | `[{ class, group, effectDescription, isFiscal, fiscalDirection?: Received \| Emitted, messages[], chain, numberedBy: Sequence \| DianResolution, availableFrom, operable }]` (`operable` = disponible en este despliegue) |
| `GET /?group=&class=&includeInactive=` | DocumentTypes.View | `[DocumentTypeDto]`, por clase y código |
| `GET /{id}` | DocumentTypes.View | `DocumentTypeDto` + `sequences: [{ publicId, prefix, nextValue, validFrom, validTo? }]` (historial) |
| `POST /` | DocumentTypes.Manage | `{ code, name, class, requiresCounterparty, requiresCostCenter, requiresReason, requiresExternalReference, warehousePublicIds[], salesChannelPublicId?, isTaxableWithdrawal?, vatNonDeductible?, allowsFutureDate?, prefix, firstNumber?, validFrom }` → 201 `DocumentTypeDto` |
| `PUT /{id}` | DocumentTypes.Manage | lo mismo sin `code`, `class`, `prefix`, `firstNumber` ni `validFrom` |
| `POST /{id}/sequences` | DocumentTypes.Manage | `{ prefix, nextValue, validFrom, reason }` → 201: cambiar de prefijo o de número es una fila nueva de `INV_DocumentSequences` que cierra la anterior la víspera |
| `POST /{id}/deactivate` · `/reactivate` | DocumentTypes.Manage | `{ reason }` |

**`DocumentTypeDto`**: `{ publicId, code, name, class, group, isFiscal, numberedBy, prefix,
requiredFields { counterparty, costCenter, reason, externalReference }, warehouses: [{ publicId, code, name
}] (vacío = todas las operativas), salesChannel?, isTaxableWithdrawal, vatNonDeductible, allowsFutureDate,
currentSequence? { publicId, prefix, nextValue, validFrom, validTo? }, approvalPolicy? { publicId,
version, levels: [{ order, threshold, permissionCode }] }, posting { mode, modeInherited, chain,
granularity, batchTrigger, batchTime }, isSeeded, isActive }` — `posting` y `approvalPolicy` son los
vigentes hoy, de sólo lectura aquí.

Reglas:
- La clase se elige al crear y no cambia. Una clase que todavía no se entrega: 422
  `Inventory.DocumentClass.NotAvailable` (`data: { class, availableIn }`).
- `isTaxableWithdrawal` sólo en `InternalConsumption`; `vatNonDeductible` sólo en clases de compras;
  `allowsFutureDate` sólo en clases no fiscales: 422 `Inventory.DocumentType.FlagNotApplicable`
  (`data: { flag, class }`).
- Una bodega de tránsito no es bodega permitida de ningún tipo (el tránsito lo manejan los traslados):
  422 `Inventory.DocumentType.TransitNotAllowed`.
- **Numeración** (FR-038, T16): los no fiscales y las notas numeran con `INV_DocumentSequences` por
  (tipo, prefijo), compartido por todas las cajas del tipo, asignado al confirmar. Prefijo de hasta 4
  caracteres alfanuméricos en mayúsculas (puede ir vacío). `nextValue` no puede quedar en o por debajo de
  un número ya emitido con ese prefijo (422 `Inventory.Sequence.NumberAlreadyIssued`, `data: { lastIssued
  }`); dos vigencias del mismo tipo no se cruzan (`Inventory.Sequence.Overlaps`). Las clases que numeran
  con resolución DIAN (factura, factura desde remisiones, documento equivalente POS, documento soporte)
  declaran el prefijo de su resolución y no tienen consecutivo propio: `firstNumber` o `POST /sequences`
  sobre ellas responde 422 `Inventory.DocumentType.NumberedByResolution` (resoluciones en §24).
- Inactivar un tipo con borradores o documentos en aprobación: 422 `Inventory.DocumentType.HasOpenDocuments`
  (`data: { drafts, pendingApproval }`); el último tipo activo de una clase que el sistema genera solo
  (`Voiding`, `CostAdjustment`, el ajuste de conteo): `.RequiredBySystem`.
- La semilla (`InventoryDocumentTypesSeeder`) deja un tipo por clase de I1, incluido `Voiding`, con su
  consecutivo, `isSeeded = true`; el de `OpeningBalance` con una política de un nivel, umbral 0, permiso
  `Inventory.OpeningBalance.Approve` (§13.1).

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Inventory.DocumentType.NotFound` | 404 | | |
| `Inventory.DocumentClass.NotAvailable` | 422 | clase de una entrega futura | `{ class, availableIn }` |
| `Inventory.DocumentType.FlagNotApplicable` · `.TransitNotAllowed` · `.NumberedByResolution` · `.HasOpenDocuments` · `.RequiredBySystem` | 422 | ver reglas | |
| `Inventory.Sequence.NumberAlreadyIssued` · `.Overlaps` | 422 | consecutivo | `{ lastIssued }` |

## 9. Documentos de inventario: forma común y ciclo

### 9.1 Grupos, rutas y permisos (T17, T48)

| Grupo (`DocumentClassGroup`) | Clases | Ruta | Permisos | Sección |
|---|---|---|---|---|
| `Adjustments` | PositiveAdjustment, NegativeAdjustment, InternalConsumption, WriteOff, Assembly (I6), LocationMove | `/api/inventory/adjustments` | `Inventory.Adjustments.*` | §10 |
| `Transfers` | TransferDispatch, TransferReceipt | `/api/inventory/transfers` | `Inventory.Transfers.*` | §11 |
| `Counts` | PhysicalCount | `/api/inventory/counts` | `Inventory.Counts.*` | §12 |
| `OpeningBalance` | OpeningBalance | `/api/inventory/opening-balances` | `Inventory.OpeningBalance.*` | §13.1 |
| `Purchases` | PurchaseRequest, PurchaseOrder, PurchaseReceipt, SupplierInvoice, SupplierNote, SupportDocument, SupportDocumentAdjustmentNote, LandedCost, SupplierReturn | `/api/inventory/purchases/*` | `Inventory.Purchases.*` | §14 |
| `Costing` | CostAdjustment | sin alta: lo genera el sistema (retroactivos, diferencias de precio, prorrateos, negativos regularizados, anulaciones de entradas promediadas); se ve por `/documents` | `Inventory.Documents.View` + `Inventory.Costs.Read` | — |
| `Sales`, `Cash` | … | §18, §20, §21 | | |
| (anulación) | Voiding | `POST …/{id}/void` en la ruta del grupo del original | `{Grupo}.Void` | §9.5 |

### 9.2 Lista y detalle genéricos — `/api/inventory/documents` (`DocumentsEndpoints.cs`, sin escritura)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?group=&class=&documentTypePublicId=&status=&from=&to=&warehousePublicId=&counterpartyPersonPublicId=&number=&search=&page=&pageSize=` | Documents.View | `PagedResult<DocumentSummaryDto>`: sólo de los grupos cuyo `View` tiene el usuario (`Costing` exige `Costs.Read`) y de las bodegas del alcance (origen **o** destino). Orden: fecha de operación y número, descendente |
| `GET /{id}` | Documents.View | `InventoryDocumentDto`; exige además el `View` del grupo (si no, 404) |

Cada grupo repite la lista en su propia ruta (`GET /api/inventory/adjustments?…`) con los mismos filtros y
su permiso `View`.

**`DocumentSummaryDto`**: `{ publicId, class, group, documentType { publicId, code, name }, prefix,
number?, displayNumber?, status, operationDate, warehouse { publicId, code, name },
destinationWarehouse?, counterparty? { personPublicId, name, documentNumber }, total?, costValue?,
createdBy, createdAt, confirmedAt?, confirmedBy?, postingMode?, voids? { publicId, displayNumber },
voidedBy? { publicId, displayNumber }, pendingApproval? { requestPublicId, currentLevel } }` —
`displayNumber` es `{prefijo}{número}`; `costValue` con `Costs.Read`.

**`InventoryDocumentDto`**: la cabecera `{ publicId, class, group, documentType, prefix, number?,
displayNumber?, status, operationDate, createdBy, createdAt, confirmedAt?, confirmedBy?, warehouse,
destinationWarehouse?, transitWarehouse?, branch { publicId, code, name }, costCenter?, counterparty?,
salesperson?, externalReference?, reason?, adjustmentCause?, notes?, currency, exchangeRate,
postingMode?, voids?, voidedBy?, rowVersion }` más:
- `lines: [{ linePublicId, lineNumber, product { publicId, code, name }, unit { publicId, code }, quantity,
  factor, quantityBase, roundingQuantity, unitPrice?, discountAmount?, lineSubtotal?, unitCost?,
  totalCost?, location?, toLocation?, lot?, serial?, expiryDate?, links: [{ kind, documentPublicId,
  displayNumber, linePublicId, quantityBase }], notes? }]` — `roundingQuantity` es la diferencia visible
  de una conversión que no dio exacta (FR-017); `unitCost` y `totalCost` con `Costs.Read`;
- `links: [{ kind, documentPublicId, class, displayNumber, status }]` (`DocumentLinkKind`);
- `taxLines: [{ lineNumber?, tax { code, name, kind }, rate { code, rate?, amountPerUnit? }, base, amount,
  treatment, concept?, municipalityDaneCode?, explanation[] }]` (foto de `INV_DocumentTaxLines`, compras
  y ventas);
- `totals { subtotal, discountTotal, taxTotal, withholdingTotal, total, amountDue, costTotal? }` (T26);
- `party?` — la copia fiscal vigente de la contraparte (`INV_DocumentPartySnapshots`: nombre o razón
  social, tipo y número de documento, dirección, régimen), sólo en documentos confirmados;
- `approval? { requestPublicId, status, currentLevel, levels[], decisions[] }` (§15.2);
- `messages?: [{ messagePublicId, type, destination, deliveryStatus, mode }]`, sólo con
  `Inventory.Messages.View`;
- `attachments: [{ attachmentPublicId, fileName, contentType, sizeBytes, uploadedBy, uploadedAt,
  canDelete }]`;
- `allowedActions: [Edit, Discard, Confirm, Void, Dispatch, Receive, …]` calculado por el servidor con el
  estado, los permisos y el alcance del usuario (la pantalla no lo deduce, como `canDelete` en la 011);
- `warnings[]`.

### 9.3 El ciclo común

| Ruta (sobre la del grupo) | Permiso | Cuerpo / respuesta |
|---|---|---|
| `POST /` | `{Grupo}.Create` | `SaveInventoryDraftRequest` → 201 `InventoryDocumentDto` en `Draft`, sin número (`SaveInventoryDraftCommand`) |
| `PUT /{id}` | `{Grupo}.Create` | `SaveInventoryDraftRequest` con `rowVersion`: reemplaza cabecera y líneas del borrador → `InventoryDocumentDto`. Cambió desde que se leyó: 409 `Concurrency.StaleRowVersion`; no es borrador: 422 `Inventory.Document.NotDraft` |
| `POST /{id}/discard` | `{Grupo}.Create` | `{ reason }` → 204; queda `Discarded` (se conserva y se audita, FR-005). No consume número |
| `POST /{id}/confirm` | `{Grupo}.Confirm` | `{ rowVersion }` → 200 `ConfirmationResultDto` (`ConfirmInventoryDocumentCommand(DocumentPublicId, ExpectedGroup)`) |
| `POST /{id}/void` | `{Grupo}.Void` | `{ reason, operationDate? }` → 201 `VoidResultDto` (`VoidInventoryDocumentCommand`, §9.5) |
| `POST /api/inventory/documents/{id}/cost-impact` | `{Grupo}.Create` + `Costs.Read` | sin cuerpo → 200 `{ retroactive: bool, affected: [{ documentPublicId, displayNumber, product { publicId, code, name }, inventoryAmount, soldAmount }], total }` (FR-045): lo calcula `MotorDeCosteo.SimularImpacto` **sin guardar**. Es consulta: sin clave de idempotencia. I5, con los retroactivos generales (el soporte mínimo de I1 para saldo inicial y conteos, §13.1 y §12, no pasa por aquí: no depende de `Costeo.RetroactivosPermitidos`) |

(Los traslados despachan con `/dispatch` y reciben con `/receive`; los conteos tienen su propio ciclo;
§11, §12.)

**`SaveInventoryDraftRequest`**: `{ documentTypePublicId, operationDate?, warehousePublicId,
destinationWarehousePublicId?, costCenterPublicId?, counterpartyPersonPublicId?, externalReference?,
reason?, adjustmentCausePublicId?, notes?, currency?, exchangeRate?, rowVersion?, lines: [{
linePublicId?, productPublicId, unitPublicId, quantity, unitPrice?, discountPercent?, discountAmount?,
unitCost?, locationPublicId?, toLocationPublicId?, sourceLinePublicId?, lotCode?, serialNumber?,
expiryDate?, notes? }] }`. Cada grupo dice qué campos usa; un campo que su clase no admite es
`Validation.Invalid`. `operationDate` por defecto es hoy (hora de Colombia). `linePublicId` en un `PUT`
conserva la línea; sin él, la línea es nueva. Hasta 4.000 líneas por documento (422
`Inventory.Document.TooManyLines`, `data: { max }`), para no escalar el bloqueo en SQL Server (T15).

**Guardar un borrador** valida la forma (validador), que lo referenciado exista y esté en el alcance
(404), el tipo contra el grupo de la ruta y las unidades y decimales (422 `Inventory.Unit.*`). Lo que hoy
impediría confirmarlo (existencia, período, bodega no activa, campos obligatorios del tipo) **no**
bloquea el guardado: vuelve en `warnings[]` con el mismo código que daría la confirmación, para que la
pantalla lo marque al salir de cada campo.

**Confirmar** recorre, en este orden (flujo canónico de `decisiones-transversales.md` §1.3):
1. relee el borrador; clase ↔ ruta (`ExpectedGroup`); alcance; bodega activa; período
   (`INV_Setup.LastClosedDate`); reglas de la clase y del tipo;
2. aprobaciones: política vigente a la fecha de operación y monto máximo (§1.3); con niveles el
   documento queda `PendingApproval`, congelado y sin número, y la respuesta lo dice —la última
   aprobación lo confirma (§15.2)—;
3. guardia fiscal (sólo clases fiscales; §24);
4. validación previa contable, si el modo que se sellará no es `NotPosted` y hay consumidor (desde I2),
   **fuera del cerrojo** y con tiempo máximo `Contabilidad.ValidacionPreviaSegundos` (T30);
5. cerrojo en orden canónico, costeo y kardex (`MotorDeCosteo`, `RegistroDeKardex`), disponibilidad,
   numeración (`Numerador`), mensajes (`EmisorDeMensajes`) y un solo `SaveChanges` con la auditoría.

**`ConfirmationResultDto`**: `{ publicId, status: Confirmed \| PendingApproval, number?, displayNumber?,
operationDate, confirmedAt?, postingMode?, approval? { requestPublicId, reason: Policy \| AmountLimit,
levels: [{ order, threshold, permissionCode, status }] }, prevalidation? { outcome: Postable \|
NoResponse \| NotApplicable, warnings[] }, messages?: [{ messagePublicId, type, destination,
deliveryStatus }], warnings[] }`. `prevalidation.outcome` es `NotApplicable` en modo `NotPosted`, en los
mensajes informativos y antes de I2; `messages` sólo con `Inventory.Messages.View`. Avisos típicos:
`Inventory.Stock.BelowReorderPoint` (`data: { productCode, warehouseCode, position, reorderPoint }`) y
`Inventory.Prevalidation.NoResponse` cuando la política es «confirmar con pendiente».

Validación previa: si Contabilidad dice que no es contabilizable, 422 `Inventory.Prevalidation.NotPostable`
con `data: { errors: [{ lineNumber, account, rule, message, whoFixes: { module, page, permission } }] }` y
el documento sigue en borrador (FR-074); si no responde y la política es `Bloquear`, 422
`Inventory.Prevalidation.NoResponse`.

### 9.4 Reglas que comparten todas las clases

- **Fecha**: `operationDate` ≤ hoy salvo tipos con `allowsFutureDate` (422
  `Inventory.Document.DateInFuture`, `data: { operationDate, today }`); nunca antes del corte de la
  bodega ni del inicio del módulo (`Inventory.Document.DateBeforeCutoff`, `data: { cutoffDate }`); nunca en
  un período cerrado (`Inventory.Period.Closed`, `data: { year, month, lastClosedDate }`).
- **Bodega**: activa para la clase (`Inventory.Warehouse.NotActive`, `data: { warehousePublicId,
  warehouseCode, allowedClasses: ["OpeningBalance", "Voiding"] }`, FR-091) y no inactiva
  (`Inventory.Warehouse.Inactive`); entre las permitidas del tipo (`Inventory.Document.WarehouseNotAllowedForType`);
  el tránsito nunca es origen de una salida ni de un despacho (`Inventory.Document.TransitNotAllowed`).
- **Campos obligatorios del tipo**: tercero, centro de costo, motivo, referencia externa, causa (422
  `Inventory.Document.FieldRequired`, `data: { field }`).
- **Producto**: inventariable (`Inventory.Product.NotInventoriable`), activo en documentos nuevos
  (`.Inactive`), no bloqueado salvo conteo y recepción de lo que ya estaba en tránsito (`.Blocked`); la
  unidad es la base o una alterna del producto (`Inventory.Unit.NotForProduct`); la cantidad en unidad
  base respeta los decimales de la unidad (`Inventory.Unit.DecimalsNotAllowed`, `data: { lineNumber,
  unitCode, allowedDecimals, quantityBase }`); la ubicación es de la bodega
  (`Inventory.Location.NotInWarehouse`). Todos con `lineNumber` y `productCode` en `data`.
- **Conteo abierto** que bloquea movimientos (`Conteo.BloquearMovimientos`): 422
  `Inventory.Count.ProductsLocked` con `data: { countPublicId, displayNumber, products[] }`.
- **Existencia** (FR-004): con el stock negativo prohibido, una salida que no cabe responde 422
  `Inventory.Stock.Insufficient` con `data: { lineNumber, productPublicId, productCode, warehousePublicId,
  locationPublicId?, requested, available, lines: [ …una entrada por línea que falla… ] }`. Dos
  confirmaciones simultáneas del último disponible: una pasa y la otra recibe este error con lo que
  realmente queda (SC-001).
- **Numeración** (FR-038): el número se asigna al confirmar, dentro de la transacción; sin consecutivo
  vigente para el tipo a la fecha, 422 `Inventory.Numbering.SequenceMissing` (`data: { documentTypeCode,
  operationDate }`).

### 9.5 Anulación (FR-006)

**`VoidResultDto`**: `{ voidingDocumentPublicId, displayNumber?, status: Confirmed \| PendingApproval,
operationDate, costAdjustments?: [{ product, difference }], messages? }`.

- Crea un documento de clase `Voiding` del tipo de anulación del grupo, **con su propia fecha** (hoy, o la
  que admita el tipo) en un período abierto; nunca hereda la del original. Referenciado en los dos
  sentidos (`VoidsDocumentId` / `VoidedByDocumentId`) y con `reason` obligatorio.
- Revierte las cantidades al costo del original; si la entrada anulada ya entró al promedio, la
  diferencia es un ajuste de costo (`AjusteDeCostoReconocido`). Emite `DocumentoAnulado` (informativo si
  el original lo era: saldo inicial). La anulación sigue el destino del mensaje del original (FR-079).
- Si el tipo de anulación tiene política, la anulación pasa por aprobación como cualquier documento.
- Una sola vez (422 `Inventory.Document.AlreadyVoided`, `data: { voidingDocumentPublicId, displayNumber
  }`); sólo confirmados (`Inventory.Document.NotConfirmed`, `data: { status }`: un documento en
  aprobación se retira con §15.2); una anulación no se anula (`Inventory.Document.VoidingNotVoidable`).
- **Dependientes vigentes** (la factura de una recepción, la recepción de un despacho, la nota de una
  factura, el ajuste de un conteo): 422 `Inventory.Document.HasDependents` con `data: { dependents: [{
  publicId, class, displayNumber, status }] }`; hay que anularlos primero (casos borde «anulaciones
  encadenadas»).
- Anular una entrada cuyas unidades ya salieron y dejaría disponible negativo: 422
  `Inventory.Stock.Insufficient` con `data.suggestion` (`SupplierReturn` o `NegativeAdjustment`).
- Un documento fiscal que emitió la cooperativa no se anula así: 422
  `Inventory.Document.FiscalUseCorrection` (notas y corrección DIAN, §24). El **registro** de una
  factura o nota del proveedor sí se anula con documento contrario (corrige el ERP, no el documento del
  proveedor).

### 9.6 Errores comunes del ciclo

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Inventory.Document.NotFound` | 404 | no existe, es de otro grupo o está fuera del alcance | |
| `Inventory.Document.NotDraft` · `.NotConfirmed` | 422 | transición inválida | `{ status }` |
| `Inventory.Document.TypeNotForRoute` | 422 | el tipo es de una clase de otro grupo | `{ class, group }` |
| `Inventory.DocumentType.Inactive` | 422 | tipo inactivo en un documento nuevo | |
| `Inventory.Document.Empty` · `.TooManyLines` | 422 | sin líneas / más de 4.000 | `{ max }` |
| `Inventory.Document.FieldRequired` · `.WarehouseNotAllowedForType` · `.TransitNotAllowed` | 422 | §9.4 | `{ field }` / `{ warehouseCode, documentTypeCode }` |
| `Inventory.Document.DateInFuture` · `.DateBeforeCutoff` | 422 | §9.4 | `{ operationDate, today }` / `{ cutoffDate }` |
| `Inventory.Period.Closed` | 422 | fecha en período cerrado | `{ year, month, lastClosedDate }` |
| `Inventory.Warehouse.NotActive` · `.Inactive` | 422 | §9.4 | `{ warehousePublicId, warehouseCode, allowedClasses[] }` |
| `Inventory.Product.NotInventoriable` · `.Inactive` · `.Blocked` | 422 | §9.4 | `{ lineNumber, productCode }` |
| `Inventory.Unit.NotForProduct` · `.DecimalsNotAllowed` · `Inventory.Location.NotInWarehouse` | 422 | §9.4 | `{ lineNumber, … }` |
| `Inventory.Count.ProductsLocked` | 422 | conteo abierto | `{ countPublicId, displayNumber, products[] }` |
| `Inventory.Stock.Insufficient` | 422 | FR-004 | `{ lineNumber, productPublicId, productCode, warehousePublicId, requested, available, lines[], suggestion? }` |
| `Inventory.Costing.RetroactiveNotAllowed` **(nuevo, T285)** | 422 | FR-045: el documento deja un movimiento con fecha anterior a otro ya registrado del mismo producto y ámbito de costo (salvo el saldo inicial de una bodega `NotActivated` y el ajuste de un conteo; hasta I5, sin mirar `Costeo.RetroactivosPermitidos`) | `{ lineNumber, productCode, laterMovement { documentPublicId, displayNumber, operationDate } }` |
| `Inventory.Approval.AmountExceedsLimit` | 422 | §1.3 | `{ amount, maxAmount, currency, permissionCode }` |
| `Inventory.Prevalidation.NotPostable` · `.NoResponse` | 422 | §9.3 | `{ errors[] }` |
| `Inventory.Numbering.SequenceMissing` | 422 | §9.4 | `{ documentTypeCode, operationDate }` |
| `Inventory.Currency.NotSupported` | 422 | moneda distinta de COP o tasa distinta de 1 | |
| `Inventory.Document.AlreadyVoided` · `.VoidingNotVoidable` · `.HasDependents` · `.FiscalUseCorrection` | 422 | §9.5 | |
| `Concurrency.StaleRowVersion` | 409 | borrador cambiado | |
| `Operation.KeyRequired` · `Operation.KeyReused` | 400 · 422 | §2.3 | |

## 10. Ajustes, consumos, bajas, ensamble y movimientos entre ubicaciones — `/api/inventory/adjustments`

`AdjustmentsEndpoints.cs`, grupo `Adjustments`, ciclo de §9.3 con `Inventory.Adjustments.{View, Create,
Confirm, Void}`; el monto que se compara con el límite de `Adjustments.Confirm` es el valor al costo.

| Clase | Efecto | Campos propios | Costo | Mensaje (operación de la matriz) | E |
|---|---|---|---|---|---|
| `PositiveAdjustment` | entrada | `adjustmentCausePublicId?`; `lines[].unitCost?` sólo con `Adjustments.SetUnitCost` | costo vigente (promedio del ámbito, o el último costo si la existencia es cero) o el indicado | `AjusteInventarioAprobado` (`AjustePositivo`) | I1 |
| `NegativeAdjustment` | salida | `adjustmentCausePublicId` obligatoria; soportes adjuntos | promedio vigente | `AjusteInventarioAprobado` (`AjusteNegativo`, `ReasonCode` = código de la causa) | I1 |
| `InternalConsumption` | salida | `costCenterPublicId` obligatorio; si el tipo es **retiro gravado**, base = precio de la lista general vigente e IVA del producto | promedio vigente | `AjusteInventarioAprobado` (`ConsumoInterno`, o `RetiroGravado` con base e IVA) | I1 (retiro gravado: I3) |
| `WriteOff` | salida | `adjustmentCausePublicId` obligatoria (daño, vencimiento, hurto, destrucción…); soportes (acta, denuncia) | promedio vigente | `AjusteInventarioAprobado` (`Baja`, `ReasonCode` = causa) | I1 |
| `Assembly` | salida de componentes y entrada del kit | `assembly: { kitProductPublicId, quantity }`; las líneas de componentes las propone el servidor desde `INV_ProductComponents` | el kit entra a la suma del costo de sus componentes | `AjusteInventarioAprobado` (`Ensamble`) | I6 |
| `LocationMove` | dentro de una bodega, un paso | `lines[].locationPublicId` → `lines[].toLocationPublicId` | no cambia | ninguno | I1 |

Reglas y errores propios:
- `unitCost` sin `Adjustments.SetUnitCost`: 422 `Inventory.Adjustment.UnitCostNotAllowed` (`data: {
  permissionCode }`); en una clase que no es entrada: `.UnitCostOnlyOnEntries`.
- Retiro gravado antes de I3 (no hay lista general): 422 `Inventory.Adjustment.TaxableWithdrawalNotAvailable`;
  un producto sin precio en la lista general vigente: `Inventory.Adjustment.PriceMissing` (`data: {
  lineNumber, productCode, priceList }`, caso borde).
- Ensamble antes de I6: `Inventory.DocumentClass.NotAvailable`; un producto que no es kit:
  `Inventory.Assembly.NotAKit`; sin componentes: `.ComponentsMissing`.
- Movimiento con la misma ubicación de origen y destino: `Inventory.Location.Same`.
- Ningún ajuste sale del tránsito ni entra a él: las diferencias de un traslado se resuelven en §11.
- **Soportes**: adjuntos con dueño `InventoryAdjustmentSupport` y el documento como `ownerPublicId`, por
  las rutas de la 011: sube quien tiene `Adjustments.Create` mientras el documento está en borrador o en
  aprobación; se leen con `Adjustments.View`; después de confirmar no se borran (`Attachments.OwnerLocked`)
  (T41).
- Los ajustes de un conteo (§12) son documentos de este grupo (enlazados `CountAdjustmentOf`) que nacen
  en aprobación; se anulan con `Adjustments.Void`.

## 11. Traslados en dos pasos — `/api/inventory/transfers` (`TransfersEndpoints.cs`, I1)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?state=&originWarehousePublicId=&destinationWarehousePublicId=&from=&to=&page=&pageSize=` | Transfers.View | `PagedResult<TransferSummaryDto { dispatchPublicId, displayNumber?, state, operationDate, origin, destination, transit, lines, quantityDispatched, quantityReceived, pendingDiscrepancies, value? }>`; alcance por origen **o** destino. `state` es texto derivado: `Draft`, `PendingApproval`, `InTransit`, `Received`, `ReceivedWithDiscrepancies`, `Voided` |
| `GET /{id}` | Transfers.View | `TransferDto { dispatch: InventoryDocumentDto, receipts: [InventoryDocumentDto], lines: [{ dispatchLinePublicId, product, unit, dispatchedBase, receivedBase, shortageBase, surplusBase, pendingBase }], discrepancies: [TransferDiscrepancyDto] }` |
| `POST /` · `PUT /{id}` · `POST /{id}/discard` | Transfers.Create | borrador del despacho: `{ documentTypePublicId, operationDate?, warehousePublicId (origen), destinationWarehousePublicId, reason?, externalReference?, lines: [{ productPublicId, unitPublicId, quantity, locationPublicId? }] }` |
| `POST /{id}/dispatch` | Transfers.Dispatch | `{ rowVersion }` → `ConfirmationResultDto` (`DispatchTransferCommand`) |
| `POST /{id}/receive` | Transfers.Receive | `{ operationDate?, documentTypePublicId?, lines: [{ dispatchLinePublicId, receivedQuantity, unitPublicId?, toLocationPublicId?, surplusQuantity? }], notes? }` → 201 `{ receiptPublicId, displayNumber, operationDate, shortages: [{ discrepancyPublicId, product, quantityBase }], surpluses: [{ discrepancyPublicId, product, quantityBase }] }` (`ReceiveTransferCommand`) |
| `GET /discrepancies?status=&warehousePublicId=&page=&pageSize=` | Transfers.View | `PagedResult<TransferDiscrepancyDto { publicId, kind: Shortage \| Surplus, transfer { dispatchPublicId, displayNumber }, product, quantityBase, value?, state, resolution?, resolvingDocument? { publicId, displayNumber, status }, createdAt }>`; `state` texto: `Pending`, `InApproval`, `Resolved` |
| `POST /discrepancies/{id}/resolve` | Transfers.Receive | `{ resolution, quantity?, adjustmentCausePublicId?, operationDate?, toLocationPublicId?, reason }` → 201 `{ discrepancyPublicId, resolution, documentPublicId, documentClass, status: PendingApproval, approvalRequestPublicId }` (`ResolveTransferDiscrepancyCommand`) |
| `POST /{id}/void` | Transfers.Void | `{ reason }` → 201 `VoidResultDto`: sólo un despacho **no recibido** |

**Despacho** (FR-039): sale del origen y entra a la bodega de tránsito de la **sucursal de origen**, al
costo de origen; emite `TrasladoDespachado` y sella el modo de la cadena `Transfers`. El destino ve la
mercancía como «en tránsito» y no la puede vender (US10-1). El despacho exige alcance sobre el origen;
el destino puede ser cualquier bodega operativa y activa de la cooperativa, y la pantalla la elige de
`GET /api/inventory/warehouses?purpose=TransferDestination`, que para quien tiene `Transfers.Create`
devuelve todas las operativas activas con sólo `{ publicId, code, name, branch }` (§17.2). Origen igual
al destino: 422 `Inventory.Transfer.SameWarehouse`; destino no activo: `Inventory.Warehouse.NotActive`.

**Recepción**: exige alcance sobre el destino; crea **y** confirma la recepción en una operación
(FR-019). Saca del tránsito, como máximo, lo despachado, al costo de la línea de despacho, y lo entra al
destino; sigue el destino del mensaje de su despacho (no sella modo, FR-075) y emite `TrasladoRecibido`.
- Una recepción por despacho (422 `Inventory.Transfer.AlreadyReceived`, `data: { receiptPublicId }`); lo
  que no llega queda en tránsito como **faltante** pendiente; lo que llega de más va en `surplusQuantity`
  y queda como **sobrante** pendiente, fuera de la existencia.
- `receivedQuantity` por encima de lo despachado: 422 `Inventory.Transfer.ReceiveExceedsDispatched`
  (`data: { lineNumber, dispatchedBase }`: el exceso se declara como sobrante).
- Despacho sin confirmar o anulado: `Inventory.Transfer.NotInTransit`; fecha anterior al despacho:
  `Inventory.Transfer.ReceiptBeforeDispatch`. Si el mes del despacho ya se cerró, la recepción se fecha
  en el período abierto y el tránsito queda en el valorizado de los dos cortes (caso borde).
- Un producto bloqueado con existencia en tránsito sí se recibe (caso borde).

**Resolver una diferencia** siempre pasa por aprobación (FR-039): el comando crea el documento que la
resuelve y abre una solicitud con `Subject = TransferDiscrepancy` y `SourceType = TransferDiscrepancy`,
con **la política del sujeto `TransferDiscrepancy` para el tipo de la recepción de traslado** (o la del
sujeto para todos los tipos); si no hay ninguna, se exige un nivel con `Inventory.Transfers.Approve`. La
regla vive en un solo sitio, la tabla de `ApprovalSubjects` de `data-model.md`; el tipo del documento
que resuelve no aporta política. Quien despachó, quien recibió y quien propone quedan excluidos. Al
aprobarse el documento se confirma y la diferencia queda resuelta (o por la cantidad aprobada, si fue
parcial).

| `resolution` | Para | Documento que crea | Efecto |
|---|---|---|---|
| `ReturnToOrigin` | faltante | recepción de traslado hacia el origen (`ReturnOf`) | sale del tránsito y vuelve al origen al costo del despacho |
| `WriteOffFromTransit` | faltante | baja desde el tránsito con `adjustmentCausePublicId` (daño, hurto, reclamación al transportador) | sale del tránsito; `AjusteInventarioAprobado` (`Baja`) |
| `LateReceipt` | faltante | recepción de traslado hacia el destino | sale del tránsito y entra al destino |
| `SurplusAdjustment` | sobrante | ajuste positivo en el destino con su causa, al costo vigente | entra al destino; `AjusteInventarioAprobado` |

Errores: `Inventory.TransferDiscrepancy.NotPending` (ya en aprobación o resuelta),
`.ResolutionNotAllowed` (`data: { kind, allowed[] }`), `.QuantityExceeds` (`data: { pendingBase }`),
`Inventory.Document.FieldRequired` (causa).

**Anulación**: un despacho no recibido se anula y la mercancía vuelve del tránsito al origen
(`DocumentoAnulado`). Con recepción: 422 `Inventory.Document.HasDependents`; lo recibido sólo se corrige
con un traslado contrario (US10-3), y anular una recepción responde 422
`Inventory.Transfer.UseReverseTransfer`. El movimiento entre ubicaciones de una bodega es de un paso
(`LocationMove`, §10).

## 12. Conteos físicos — `/api/inventory/counts` (`CountsEndpoints.cs`, I1)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?state=&warehousePublicId=&from=&to=&page=&pageSize=` | Counts.View | `PagedResult<CountSummaryDto { publicId, displayNumber?, state, kind, scope, warehouse, snapshotAt?, lines, linesWithDifference?, differenceValue? }>`; `state` texto: `Draft`, `Open`, `Closed`, `AdjustmentPending`, `Adjusted`, `Discarded`, `Voided` |
| `GET /{id}` | Counts.View | `PhysicalCountDto` (abajo) |
| `POST /` · `PUT /{id}` | Counts.Open | `{ documentTypePublicId, warehousePublicId, kind: Total \| Cyclic, scope: All \| Category \| Location \| Selection \| AbcClass, categoryPublicIds?, locationPublicIds?, productPublicIds?, abcClass?, blind, counterUserPublicIds?, notes? }` |
| `POST /{id}/open` | Counts.Open | `{ rowVersion }` → `{ publicId, snapshotAt, snapshotDate, lines, blocksMovements }` (`OpenPhysicalCountCommand`): congela la foto (sin número; el documento sigue en `Draft` con `CountSnapshotAt`) |
| `POST /{id}/discard` | Counts.Open | `{ reason }` → 204: descarta un conteo en borrador **o abierto** (sin número, no consume consecutivo) y libera sus productos; queda `Discarded` y auditado (FR-005) |
| `POST /{id}/captures` | Counts.Capture | `{ round: 1 \| 2, reads: [{ barcode?, productPublicId?, unitPublicId?, quantity?, locationPublicId?, lotCode?, readAt }] }` → 200 `{ accepted, rejected: [{ index, code, message }], lines: [{ productPublicId, productCode, locationPublicId?, counted? }] }` (`CapturePhysicalCountCommand`) |
| `GET /{id}/captures?counterUserPublicId=&productPublicId=&page=&pageSize=` | Counts.View | `PagedResult<{ capturePublicId, counter { userPublicId, name }, round, product, unit, quantity, quantityBase, location?, lot?, readAt, registeredAt }>` |
| `POST /{id}/close` | Counts.Close | `{ rowVersion }` → `{ state: Closed, number, displayNumber, lines, withDifference, differenceValue? }` (`ClosePhysicalCountCommand`): el conteo queda `Confirmed` y **se numera aquí** (FR-038) |
| `GET /{id}/adjustment` | Counts.View | vista previa: `{ adjustmentDate, dateRule: Foto \| Aprobacion, lines: [{ product, location, lot?, theoretical, counted, difference, unitCost?, value? }], positiveValue?, negativeValue? }` |
| `POST /{id}/adjustment` | Counts.Close | `{ notes? }` → 201 `{ documents: [{ documentPublicId, class: PositiveAdjustment \| NegativeAdjustment, lines, value?, status: PendingApproval, approvalRequestPublicId }] }` |
| `POST /{id}/void` | Counts.Close | `{ reason }` → 201 `VoidResultDto`: sólo un conteo **cerrado** (`Confirmed`) sin ajuste en curso ni vigente (`Inventory.Document.HasDependents`). Crea un documento `Voiding` que referencia el conteo (FR-006), **sin kardex ni mensajes** porque el conteo no movió existencia. Un conteo abierto se descarta, no se anula (`Inventory.Document.NotConfirmed`) |

**`PhysicalCountDto`**: `{ publicId, displayNumber?, state, kind, scope, warehouse, criteria {
categories[], locations[], products[], abcClass? }, blind, blocksMovements, snapshotAt?, snapshotDate?,
openedBy?, counters: [{ userPublicId, name }], lines: [{ product, location, lot?, theoretical?,
countedRound1, countedRound2?, counted, difference?, differenceValue?, needsRecount }], adjustments: [{
documentPublicId, class, status }] }`. En un conteo **ciego**, `theoretical`, `difference` y
`differenceValue` salen `null` para quien sólo tiene `Counts.Capture`; los valores exigen `Costs.Read`.

Reglas (FR-040, FR-041):
- **Abrir** congela la foto teórica por producto × ubicación × lote del alcance del conteo
  (`INV_CountSnapshotLines`) y sella `Conteo.BloquearMovimientos`: si bloquea (defecto), todo documento que
  toque esos productos en esa bodega responde `Inventory.Count.ProductsLocked`; si no, los movimientos
  posteriores a la foto se suman al teórico al comparar. Un producto sólo puede estar en un conteo
  abierto por bodega (422 `Inventory.Count.Overlaps`, `data: { countPublicId, products[] }`); alcance
  vacío: `.EmptyScope`; `AbcClass` antes de I6: `.ScopeNotAvailable`.
- **Capturar**: cada lectura suma (`quantity` por defecto 1 en la unidad del código leído; tres lecturas
  del mismo código = 3, US11-2); varias personas capturan a la vez; una lectura equivocada se corrige con
  otra de cantidad negativa (las capturas sólo se agregan). Cada lectura se acepta o se rechaza sola en
  `rejected[]`: código desconocido (`Inventory.Barcode.NotFound`), producto fuera del criterio del conteo
  (`Inventory.Count.ProductNotInScope`; en conteos `All` o por ubicación, un producto sin teórico entra
  con teórico cero). Si el conteo declara contadores, sólo ellos capturan
  (`Inventory.Count.CounterNotAssigned`); conteo no abierto: `Inventory.Count.NotOpen`.
- **Cerrar** termina la captura, compara y confirma el conteo con su número (el número se asigna al
  confirmar, sin huecos: un conteo abierto que se descarta no consume ninguno). Una diferencia por encima de la tolerancia
  (`Conteo.ToleranciaReconteoPorcentaje` / `…Unidades`) sin segunda ronda responde 422
  `Inventory.Count.RecountRequired` con `data: { lines: [{ product, location, theoretical, counted,
  difference }] }`; se captura `round = 2` de esas líneas y se cierra otra vez (vale la segunda ronda).
- **Ajuste**: sólo después de cerrar; crea uno o dos documentos (positivo y negativo) con la causa
  «diferencia de conteo», enlazados `CountAdjustmentOf`, que nacen **en aprobación** (FR-041): el tipo de
  ajuste de conteo trae sembrada una política de un nivel, umbral 0, permiso `Inventory.Counts.Approve`.
  No pueden aprobar quien abrió el conteo ni quien capturó en él. Fecha según `Conteo.FechaDelAjuste`
  (`Foto`: la de la foto; `Aprobacion`: la de la última aprobación); si ese período se cerró, el primer
  día abierto. Valor: el costo promedio vigente en esa fecha. Sin diferencias no se crea nada y el
  conteo queda `Adjusted`. Ya hay un ajuste en curso: `Inventory.Count.AdjustmentInProgress`.
- **El ajuste de un conteo aprobado no depende de `Costeo.RetroactivosPermitidos`** (precisión aplicada a
  la spec sobre FR-045; pregunta al dueño D9, con esta excepción como defecto propuesto): aunque su fecha
  deje movimientos posteriores en el ámbito de costeo, se confirma. El motor lo inserta en orden
  `(OperationDate, Id)` y verifica que no aparezca existencia negativa (si aparece, 422
  `Inventory.Stock.Insufficient`). En promedio ponderado un ajuste valorado al promedio de su fecha no
  cambia el promedio, así que normalmente no cambia el costo de las salidas posteriores; si alguna
  cambia, el motor la recalcula y registra `AjusteDeCostoReconocido` por documento afectado. Ese soporte
  mínimo de retroactivos se entrega en **I1** (el mismo de §13.1); los retroactivos generales siguen en
  I5.
- Un conteo abierto con foto en un mes impide cerrar ese mes (§13.4).

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Inventory.Count.NotFound` | 404 | | |
| `Inventory.Count.Overlaps` · `.EmptyScope` · `.ScopeNotAvailable` · `.NotOpen` · `.CounterNotAssigned` · `.RecountRequired` · `.AdjustmentInProgress` | 422 | ver reglas | `{ countPublicId, products[] }` · `{ lines[] }` |
| `Inventory.Count.ProductNotInScope` · `Inventory.Barcode.NotFound` | — | lectura rechazada, en `rejected[]` | `{ index }` |

## 13. Puesta en marcha y períodos

### 13.1 Saldo inicial — `/api/inventory/opening-balances` (`GoLiveEndpoints.cs`, I1)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /template.xlsx` | Warehouses.View | la plantilla 14 de contracts/plantillas.md (§14: hoja `Datos` con `bodega`, `fechaDeCorte`, `producto`…; permisos en su §0.7) |
| `POST /import?mode=review` | OpeningBalance.Load | multipart con el archivo (la fecha de corte viene en la columna `fechaDeCorte`) → 200 `ImportResultDto` (contracts/plantillas.md §0.5) con `extra.byWarehouse[]` (§14 de ese contrato: el insumo de la comparación de FR-090) |
| `POST /import?mode=apply` | OpeningBalance.Load | igual → 200 `ImportResultDto` (`applied: true`) con `extra.documents: [{ documentPublicId, warehouse { publicId, code }, lines, value, status: Draft, replaced }]` (`ImportOpeningBalanceCommand`); con errores 422 `Import.Invalid` y nada guardado |
| `GET /?warehousePublicId=&status=` | OpeningBalance.Load | `[DocumentSummaryDto]` de clase `OpeningBalance` del alcance |
| `GET /{id}` | OpeningBalance.Load | `InventoryDocumentDto` |
| `POST /{id}/confirm` | OpeningBalance.Load | `{ rowVersion }` → `ConfirmationResultDto`, normalmente `PendingApproval` |
| `POST /{id}/discard` | OpeningBalance.Load | `{ reason }` → 204 |
| `POST /{id}/void` | OpeningBalance.Load | `{ reason }` → 201 `VoidResultDto`, sólo mientras la bodega no esté activa |

Reglas (FR-089, US4):
- **Un documento de saldo inicial por bodega**, fechado en su `cutoffDate` (la víspera de su activación),
  partido en varios si pasa de 4.000 líneas (T15). La fecha de corte se guarda en la bodega
  (`INV_Warehouses.CutoffDate`); no puede ser futura ni caer en un período cerrado.
- Sólo en bodegas **no activas** (422 `Inventory.OpeningBalance.WarehouseActive`) y nunca en tránsito
  (`.TransitNotAllowed`). Si la bodega ya tiene un saldo inicial confirmado: 422
  `Inventory.OpeningBalance.AlreadyConfirmed` (`data: { documents[] }`): se anula primero.
- **Todo o nada** (US4-1: 20.000 líneas con un producto y una bodega inexistentes no guardan nada y listan
  los dos errores). Errores de fila con los códigos de contracts/plantillas.md §0.4 (`Import.Cell.NotFound`
  para un producto, una bodega —inexistente o fuera del alcance— o una ubicación que no existen,
  `Import.Cell.Format` para cantidad o costo negativos o con más decimales, `Import.Row.Duplicate` para el
  mismo producto, bodega, ubicación y lote dos veces) y los de negocio iguales al alta unitaria
  (`Inventory.Product.NotInventoriable`, `.Blocked`, `Inventory.Unit.DecimalsNotAllowed`).
- **Volver a importar** reemplaza las líneas de los borradores de esas bodegas conservando el mismo
  documento (`replaced: true`; las líneas viejas quedan de baja, Principio XI), como la apertura de la 009.
- **Confirmar** pasa por aprobación: el tipo sembrado trae una política de un nivel, umbral 0, permiso
  `Inventory.OpeningBalance.Approve`, y la política de ese tipo no se puede vaciar
  (`Approvals.Policy.RequiredForClass`). El costo es el cargado (FR-044).
- Emite `SaldoInicialCargado` **informativo**: sin comprobante, sin validación previa y sin modo de paso
  (FR-089); su anulación también es informativa.
- `INV_Setup` (y su `StartDate`, primer día del mes del primer corte) la crea el primer registro de una
  fecha de corte o `ImportOpeningBalanceCommand` (data-model §6.1); los meses anteriores no existen como
  períodos.
- **Excepción de puesta en marcha** (FR-089, FR-091, FR-095, SC-016; precisión aplicada a la spec sobre
  FR-045 y pregunta al dueño D8, con esta excepción como defecto propuesto): un `OpeningBalance` —y su
  `Voiding`— de una bodega `NotActivated` **no está sujeto a `Costeo.RetroactivosPermitidos`**. Con el
  ámbito de costeo por defecto (`Cooperativa`), la segunda bodega y las siguientes cargan su saldo a su
  propia fecha de corte cuando las ya activas tienen movimientos posteriores de los mismos productos:
  el motor inserta sus entradas en orden `(OperationDate, Id)`, recalcula las salidas posteriores del
  ámbito cuando el costo cargado cambia el promedio y registra `AjusteDeCostoReconocido` por documento
  afectado (T9). Ese soporte mínimo de retroactivos (esta clase y los ajustes de conteo, §12) se entrega
  en **I1**; los documentos retroactivos generales siguen en I5. Lo fija el caso dorado «segunda bodega
  activada después de ventas en la primera, ámbito cooperativa».

### 13.2 Cifras de SOLIDO — `/api/inventory/legacy-figures` (`GoLiveEndpoints.cs`, I1)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /template.xlsx` | Warehouses.View | la plantilla 15 de contracts/plantillas.md (§15: hoja `Datos`, códigos de producto **tal como vienen de SOLIDO**; permisos en su §0.7) |
| `POST /import?mode=review` | LegacyFigures.Import | multipart con el archivo → 200 `ImportResultDto` (contracts/plantillas.md §0.5) con `extra.byDateWarehouseGroup[]` (§15 de ese contrato) |
| `POST /import?mode=apply` | LegacyFigures.Import | igual → 200 `ImportResultDto` (`applied: true`) con `extra: { batchPublicId, supersededBatchPublicIds[] }` (`ImportLegacyFiguresCommand`); con errores 422 `Import.Invalid` |
| `GET /?asOf=&warehouseCode=` | LegacyFigures.Import | `[{ batchPublicId, asOf, rows, resolvedRows, unresolvedRows, quantity, value, importedAt, importedBy, superseded }]` |
| `GET /{batchId}/rows?unresolvedOnly=&page=&pageSize=` | LegacyFigures.Import | `PagedResult<{ warehouseCode, productCode, warehousePublicId?, productPublicId?, quantity, value }>` |

Las cifras son dato para comparar, no movimientos: nunca tocan el kardex. Un código de producto de SOLIDO
que no existe en el catálogo nuevo **no** es error: queda sin resolver, como aviso de fila
(`Inventory.LegacyFigures.CodeUnresolved`), exige `grupoContable` y los comparativos lo muestran aparte;
la bodega sí debe existir en el ERP (activa o no). Columnas y reglas: contracts/plantillas.md §15. Reimportar la misma (fecha, bodega) deja la carga anterior `superseded`, sin
borrarla. Los comparativos por producto y bodega son las vistas `legacy-comparison-kardex` y
`legacy-comparison-valuation` (§27, US4-6).

### 13.3 Activación de una bodega y su cuadre contable — `/api/inventory/warehouses/{id}/activation` (I2)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?cutoffDate=` | Warehouses.Activate | `ActivationPreviewDto` (abajo); si la bodega ya está activa, `activation` con lo guardado al activarla |
| `POST /` | Warehouses.Activate | `{ cutoffDate, acceptDifference?, reason? }` → 200 `ActivationResultDto { activationPublicId, warehousePublicId, activatedAt, activatedBy, cutoffDate, totalDifference, differenceAccepted, reason? }` (`ActivateWarehouseCommand`) |

**`ActivationPreviewDto`**: `{ warehouse { publicId, code, name }, cutoffDate, openingBalance: {
confirmed, value, documents: [{ publicId, displayNumber?, status, value }] }, sets: [{ accountingGroups:
[{ code, name }], accounts: [{ code, name, role: Inventario \| Transito, ledgerBalance }], ledgerBalance,
valuation: { thisWarehouse, activeWarehouses: [{ warehousePublicId, code, value }], legacyWarehouses: [{
warehouseCode, warehousePublicId?, value, figuresAsOf, batchPublicId }], total }, difference, explanation:
{ pending, inBatch, rejected, notPosted } }], totalDifference, blockers: [{ code, message, data }],
canActivate, requiresAcceptance }`.

Cómo se arma (FR-090, US4-2, SC-018):
- Contabilidad responde por la consulta definida `IContabilidadParaInventario.SaldosDeCuentasMapeadasAsync(corte)`
  los **conjuntos de cuentas**: grupos contables y cuentas de rol `Inventario` y `Transito` vigentes al
  corte, unidos cuando dos grupos comparten cuenta, con el saldo contable de cada cuenta a la fecha (sin
  el alcance de sucursal del usuario, G5; la apertura de la 009 cuenta como saldo).
- Por cada conjunto se suma el valorizado de **todas** las bodegas que usan esas cuentas: la que se
  activa y las ya activas, con el valorizado del módulo nuevo al corte; las no activas **entran al
  valorizado del conjunto** con las cifras de SOLIDO a esa misma fecha (§13.2), y se muestran aparte
  (`legacyWarehouses`) para identificarlas. Así la primera bodega no muestra como diferencia lo que
  sigue en SOLIDO.
- `explanation` cuenta los mensajes de Inventario que explican parte de la diferencia (pendientes, en
  lote, rechazados, de tipos que no pasan). El saldo inicial es informativo: se cuenta como incluido en
  el saldo contable.

Bloqueos (en `blockers[]` del GET; como 422 en el POST): `Inventory.Activation.OpeningBalanceNotConfirmed`,
`.CutoffMismatch` (`cutoffDate` distinta de la fecha del saldo inicial), `.LegacyFiguresMissing` (`data:
{ warehouseCodes[] }`: bodegas no activas que comparten cuentas sin cifras a esa fecha), `.RulesMissing`
(`data: { accountingGroups[] }`: grupos de la bodega sin regla vigente de rol `Inventario`),
`.AccountingUnavailable` (la consulta no respondió), `.AlreadyActive`, `Inventory.Period.Closed`.

Activar **vuelve a calcular** (nunca confía en la vista previa):
- cuadre (diferencia cero en todos los conjuntos): activa;
- con diferencia y sin `acceptDifference`: 422 `Inventory.Activation.Difference` con `data: { sets: [{
  accountingGroups, difference }], totalDifference }`;
- `acceptDifference: true` exige `Inventory.Warehouses.AcceptActivationDifference` (si no, 422
  `Inventory.Activation.AcceptDifferenceNotAllowed`, `data: { permissionCode }`, US4-3) y `reason`.

Al activar: `ActivationStatus = Active`, `CutoffDate`, `ActivatedAt/By`, la fila de
`INV_WarehouseActivations` con la comparación entera, la diferencia, quién la aceptó y el motivo; la
bodega de tránsito de la sucursal queda activa con la primera. Desde ese momento sólo el módulo nuevo
opera la bodega (FR-091). Se audita.

Antes de I2 no hay matriz ni consulta de saldos: la ruta existe, el GET trae `sets: []` y el bloqueo
`.AccountingUnavailable` como aviso. **Fuera de producción** el POST sólo activa con
`acceptDifference: true`, el permiso especial y un motivo que queda auditado («sin comparación
contable»): es lo que permite ensayar I1 en una cooperativa de ensayo. **En producción** (ambiente
`Production`) el POST responde 422 `Inventory.Activation.AccountingUnavailable` mientras la consulta de
saldos no exista, así que ninguna bodega se activa sin la comparación de FR-090: COOFLOPAL activa con I2
(§17.2).

La conciliación permanente (inventario valorizado contra saldo contable por conjunto de cuentas, FR-081)
es la vista `reconciliation` con `Inventory.Reconciliation.View` (§27), con la misma regla: las
bodegas no activas que comparten cuentas suman al valorizado del conjunto con sus cifras de SOLIDO a la
fecha de corte y se muestran aparte; la diferencia no se les atribuye.

### 13.4 Períodos — `/api/inventory/periods` (`PeriodsEndpoints.cs`, I1)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?year=` | Periods.View | `{ startDate?, lastClosedDate?, periods: [{ year, month, status: Open \| Closed, closedAt?, closedBy?, reopenedAt?, reopenedBy?, reopenReason?, unbilledShipmentsAccepted? { by, at, reason, count, value }, closingVersion? }] }` desde el mes de `startDate` hasta el actual |
| `GET /{year}/{month}/close` | Periods.View | vista previa `PeriodCloseCheckDto { year, month, canClose, blockers: { openCounts: [{ countPublicId, displayNumber, warehouse, snapshotDate }], notNext?, notEnded? }, warnings: { drafts, pendingApproval, unresolvedTransfers: [{ dispatchPublicId, displayNumber, pendingBase }], messages: { pending, inBatch, rejected } }, unbilledShipments: [{ documentPublicId, displayNumber, customer, value, valueSource: Order \| PriceList }], unbilledTotal }` |
| `POST /{year}/{month}/close` | Periods.Close | `{ acknowledgeWarnings?, acceptUnbilledShipments?, reason? }` → 200 `{ year, month, closedAt, closingVersion, valuation: [{ accountingGroup { code, name }, warehouse { publicId, code }, quantity, value? }], total?, messagePublicId, batchPublicId? }` (`CloseInventoryPeriodCommand`) |
| `POST /{year}/{month}/reopen` | Periods.Reopen | `{ reason }` → 200 `{ year, month, reopenedAt, supersededClosingVersion, messagePublicId }` (`ReopenInventoryPeriodCommand`) |

Reglas (FR-047, US3-3..5):
- Para toda la cooperativa y en orden: sólo el mes siguiente al último cerrado (422
  `Inventory.Period.NotNext`, `data: { nextToClose }`) y sólo un mes que ya terminó en hora de Colombia
  (`Inventory.Period.NotEnded`). Sin `INV_Setup` (el módulo no arrancó): 422 `Inventory.Period.NotStarted` **(nuevo, T289)**.
- **Bloquea**: conteos abiertos con foto en el período (422 `Inventory.Period.OpenCounts`, `data: { counts[]
  }`).
- **Avisa**: borradores y documentos en aprobación, traslados o diferencias sin resolver, mensajes
  pendientes, en lote o rechazados con fecha en el período. El primer intento responde 422
  `Inventory.Period.WarningsNotAcknowledged` con esos avisos en `data`; con `acknowledgeWarnings: true`
  cierra y audita lo aceptado.
- **Remisiones sin facturar** (I6): la lista con su valor (al precio del pedido o de la lista vigente);
  sólo se cierra con `acceptUnbilledShipments: true`, `reason` y el permiso
  `Inventory.Periods.AcceptUnbilledShipments` (422 `Inventory.Period.UnbilledShipmentsNotAccepted` con la
  lista, o `.AcceptUnbilledNotAllowed`).
- Cerrar toma el bloqueo **exclusivo** de `INV_Setup` (espera a las confirmaciones en vuelo y detiene las
  nuevas mientras dura), fija `LastClosedDate` al último día del mes, guarda el valorizado por producto ×
  bodega con su grupo (`INV_PeriodClosingBalances`, versión nueva) y emite `PeriodoInventarioCerrado`
  (informativo). Si algún tipo tiene `Contabilidad.DisparadorDeLote = CierreDePeriodo`, ordena su lote en
  el mismo `SaveChanges` (`batchPublicId`, T12). Desde entonces todo documento con fecha en el mes se
  rechaza con `Inventory.Period.Closed`, nombrando el período.
- **Reabrir**: permiso especial, `reason`, sólo el último cerrado (422 `Inventory.Period.NotLastClosed`,
  `data: { lastClosed }`; no cerrado: `.NotClosed`); deja la versión del valorizado `Superseded` (no la
  borra), retrocede `LastClosedDate` y emite `PeriodoInventarioReabierto`. Se audita.
- El período contable es otro: Contabilidad avisa antes de cerrar su mes si hay mensajes de Inventario
  sin procesar (§26).
- Los valores del valorizado exigen `Costs.Read`; sin él salen `null`.

## 14. Compras — `/api/inventory/purchases` (`PurchasesEndpoints.cs`)

### 14.1 Lo común a toda compra

- **Grupo `Purchases`**: ciclo de §9.3 con `Inventory.Purchases.{View, Create, Confirm, Void}`; límite de
  monto sobre `Purchases.Confirm` comparado con el `Total` (§1.3).
- **Proveedor**: una persona del maestro (`supplierPersonPublicId`; en la cabecera, `CounterpartyPersonId`).
  Si no existe, se crea desde `PersonaDialog` con el aviso de Habeas Data (`Core.People.Create`, T46); el
  módulo no escribe personas. Al confirmar se guarda la copia fiscal (`INV_DocumentPartySnapshots`).
- **Impuestos y retenciones** (FR-013, FR-050): los calcula el servidor con `MotorTributario` sobre el
  catálogo vigente a la fecha de operación, según el concepto de retención de cada producto, el perfil
  tributario del proveedor (`COR_People`) y el de la cooperativa (parámetros `TAX`), la base mínima en UVT
  (`LectorDeUvt`) y el municipio de la operación. Cada guardado del borrador los recalcula y los devuelve
  en `taxLines` y `totals` como vista previa; al confirmar queda la foto. Las retenciones que practica la
  cooperativa salen con `treatment = WithholdingApplied`. Sin UVT vigente: 422 `Taxation.Uvt.Missing`
  («No hay UVT vigente al {fecha}; regístrela en Parámetros legales»); un impuesto del producto sin tarifa
  vigente: `Inventory.ProductTax.RateNotInForce` (`data: { lineNumber, taxCode, date }`).
- **Municipio**: `operationMunicipalityDaneCode` se propone desde la sucursal de la bodega que recibe y se
  puede cambiar en el borrador (T24); un código DANE inexistente: 422 `Inventory.Purchase.MunicipalityUnknown`.
- **Costo de entrada** (FR-044, `CostoDeEntrada`): precio neto de descuentos no condicionados, más el IVA
  no descontable (`IvaDescontable.Determinar`: la cooperativa no responsable de IVA, el tipo con
  `vatNonDeductible`, o el producto de venta excluida) y el INC, que siempre va al costo. Se ve en
  `lines[].unitCost` con `Costs.Read`.
- **Cadena de modo de paso `Purchases`** (FR-075): recepción, factura y notas del proveedor, documento
  soporte y su nota, devolución. La factura, el documento soporte o la devolución **contra una recepción**
  siguen el destino del mensaje de esa recepción; no se pueden reunir recepciones con destinos distintos
  (422 `Inventory.Purchase.MixedPostingDestinations`, `data: { receipts: [{ publicId, displayNumber, mode
  }] }`).

### 14.2 Recepciones — `/purchases/receipts` (`PurchaseReceipt`)

Ciclo de §9.3. Cuerpo: `{ documentTypePublicId, operationDate?, warehousePublicId, supplierPersonPublicId,
externalReference? (remisión o guía del proveedor), operationMunicipalityDaneCode?, notes?, lines: [{
productPublicId, unitPublicId, quantity, unitPrice, discountPercent?, discountAmount?, locationPublicId?,
orderLinePublicId? (I5), lotCode?, expiryDate?, serialNumber? (I6) }] }`. Entra al kardex al costo de
entrada y emite `CompraRecibida`. El detalle agrega por línea `pendingToInvoice` y `returned` (se calculan
desde `INV_DocumentLineLinks`, no se guardan). Recibir 3 cajas × 12 registra 36 unidades y el documento
muestra 3 cajas (US1-2). Anular una recepción con factura, devolución, documento soporte o costos
adicionales vigentes: `Inventory.Document.HasDependents`.

### 14.3 Compra directa — `POST /purchases/direct` (I1)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `POST /purchases/direct` | Purchases.Confirm | `{ receipt: <cuerpo de §14.2>, invoice: { documentTypePublicId, supplier: { prefix?, number, cufe?, issueDate, dueDate?, paymentForm: Cash \| Credit, isElectronic } } }` → 201 `DirectPurchaseResultDto { status: Confirmed \| PendingApproval, receipt { publicId, displayNumber? }, supplierInvoice { publicId, displayNumber?, supplierNumber }, approval?, totals, taxLines[], radianEvents[], messages?, warnings[] }` (`ConfirmDirectPurchaseCommand`) |

Crea y confirma la recepción y la factura del proveedor en **una** transacción (FR-019: abrir, leer
productos, confirmar), con las mismas líneas y precios, la misma cadena y el mismo modo sellado; emite
`CompraRecibida` y `FacturaProveedorRegistrada`. Si hace falta aprobación (política del tipo de recepción
o monto sobre el límite de `Purchases.Confirm`, medido con el total de la factura), las dos quedan
guardadas —la recepción en aprobación, la factura en borrador enlazado— y la última aprobación confirma
las dos en la transacción del aprobador. Errores: los de §9.6, §14.2 y §14.4.

### 14.4 Facturas del proveedor — `/purchases/supplier-invoices` (`SupplierInvoice`)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?supplierPersonPublicId=&status=&paymentForm=&radianPending=&from=&to=&page=&pageSize=` | Purchases.View | `PagedResult<DocumentSummaryDto + { supplier { prefix, number, cufe? }, dueDate?, paymentForm, radian { receipt030, goodsReceived032 } }>` |
| `GET /{id}` | Purchases.View | `InventoryDocumentDto` + `supplierInvoice { prefix, number, cufe?, issueDate, dueDate?, paymentForm, isElectronic }` + `radianEvents[]` + `match?` (I5) |
| `POST /` · `PUT /{id}` · `POST /{id}/discard` | Purchases.Create | `{ documentTypePublicId, operationDate?, supplierPersonPublicId, supplier: { prefix?, number, cufe?, issueDate, dueDate?, paymentForm, isElectronic }, operationMunicipalityDaneCode?, notes?, lines: [{ receiptLinePublicId?, productPublicId?, unitPublicId?, quantity, unitPrice, discountPercent?, discountAmount?, costCenterPublicId? }] }` |
| `POST /{id}/confirm` | Purchases.Confirm | `{ rowVersion }` → `ConfirmationResultDto` + `costAdjustments: [{ receiptPublicId, product, difference, toInventory, toCostOfSales }]` (con `Costs.Read`) + `radianEvents[]` |
| `POST /{id}/void` | Purchases.Void | `{ reason }` → `VoidResultDto`: anula el **registro** (`DocumentoAnulado`) |
| `POST /prefill` | Purchases.Create | multipart `archivo` (XML UBL de la factura, `AttachedDocument` o ZIP) → 200 `{ supplier { documentNumber, name, personPublicId? }, prefix, number, cufe, issueDate, dueDate?, paymentForm, lines: [{ supplierItemCode, description, quantity, unitCode, unitPrice, discount, taxes: [{ code, rate, amount }], suggestedProductPublicId? }], totals }`. Es consulta (sin clave); el archivo **no se guarda** (T41, E10). 422 `Inventory.SupplierInvoice.FileUnreadable`, `.NotAnInvoice` |

Reglas:
- **Contra las recepciones** (FR-050): las líneas con `receiptLinePublicId` facturan lo recibido; las
  demás son servicios (producto `Service`: flete, honorarios) sin recepción. Recepción de otro proveedor
  (`Inventory.Purchase.ReceiptFromOtherSupplier`) o sin confirmar (`.ReceiptNotConfirmed`); cantidad por
  encima de lo recibido, menos lo ya facturado y lo devuelto: 422 `Inventory.Purchase.InvoiceExceedsReceived`
  (`data: { lineNumber, received, invoiced, returned, available }`).
- **Dos vías (I1)**: un precio distinto del de la recepción no se retiene (E6): al confirmar genera la
  diferencia de costo, repartida entre lo que sigue en existencia y lo ya vendido y dividida por documento
  afectado (`AjusteDeCostoReconocido`). Las tres vías con tolerancias llegan en I5 (§14.9).
- **Unicidad** entre registros no anulados: (proveedor, prefijo, número) → 422
  `Inventory.SupplierInvoice.Duplicate` (`data: { documentPublicId, displayNumber }`); CUFE →
  `.CufeDuplicate`. Una factura electrónica exige CUFE (`.CufeRequired`); vencimiento anterior a la
  emisión: `.DueDateInvalid`.
- **A crédito** (`paymentForm = Credit`): al confirmar nacen sus eventos 030 y 032 en `Pending` (§14.8);
  de contado, `NotApplicable`.
- Emite `FacturaProveedorRegistrada` (base, descuentos, IVA descontable e IVA al costo por separado,
  retenciones) y, si hubo diferencia de precio, `AjusteDeCostoReconocido`.
- Con notas vigentes no se anula (`Inventory.Document.HasDependents`).

### 14.5 Notas del proveedor — `/purchases/supplier-notes` (`SupplierNote`)

Ciclo de §9.3. Cuerpo: `{ documentTypePublicId, operationDate?, supplierInvoicePublicId, noteKind: Credit \|
Debit, supplier: { prefix?, number, cufe?, issueDate, isElectronic }, reason, lines: [{ invoiceLinePublicId,
quantity?, unitPrice?, amount?, affectsCost }] }`.
- Contra una factura confirmada del mismo proveedor; una nota crédito no puede pasar de lo que queda de
  la factura tras las otras notas (422 `Inventory.SupplierNote.ExceedsInvoice`, `data: { remaining }`).
- Impuestos y retenciones con la **foto de la factura original**, en proporción y sin volver a probar la
  base mínima (T22, E9).
- Emite `FacturaProveedorRegistrada` con su signo; si cambia el precio de lo recibido (`affectsCost`),
  además `AjusteDeCostoReconocido`. La nota no mueve existencia: si el proveedor recoge mercancía, la
  salida es una devolución (§14.6) enlazada.
- Misma unicidad que la factura (`Inventory.SupplierInvoice.Duplicate`); se anula con documento contrario.
  El límite de monto de `Purchases.Confirm` también rige para notas.

### 14.6 Devoluciones a proveedor — `/purchases/returns` (`SupplierReturn`)

Ciclo de §9.3. Cuerpo: `{ documentTypePublicId, operationDate?, warehousePublicId, supplierPersonPublicId,
reason, adjustmentCausePublicId?, lines: [{ receiptLinePublicId, quantity, unitPublicId?, locationPublicId?
}] }`.
- Toda línea va enlazada a una línea de recepción (422 `Inventory.Return.ReceiptLineRequired`) y no pasa de
  lo recibido menos lo ya devuelto (`Inventory.Return.ExceedsReceived`, `data: { received,
  alreadyReturned }`).
- Sale al **costo con que entró** (el de la línea de recepción); la diferencia con el promedio vigente es
  ajuste de costo (FR-044, FR-051). Emite `DevolucionRegistrada` (+ `AjusteDeCostoReconocido`) y sigue el
  destino del mensaje de su recepción.
- Si la compra se soportó con documento soporte (I4), la respuesta trae
  `supportDocumentAdjustmentNoteRequired: true` y la nota de ajuste se emite en §14.7.

### 14.7 Documento soporte y su nota de ajuste — `/purchases/support-documents` (I4)

Mismo ciclo y mismas reglas de impuestos que la factura del proveedor, para compras a quien no está
obligado a facturar; la nota de ajuste se crea en la misma ruta con un tipo de clase
`SupportDocumentAdjustmentNote` y `supportDocumentPublicId`. Diferencias: son **fiscales emitidos** (CUDS);
numeran al confirmar dentro de su resolución (`NumeradorFiscal`; sin resolución vigente: 422
`Inventory.Numbering.ResolutionUnavailable`); la confirmación consulta `GuardiaDeEmisionFiscal` (422
`ElectronicInvoicing.NotReady` con `data.missing[]`) y deja el documento electrónico en `Pending` con su
versión 1. Su emisión, su estado, su corrección y sus contingencias viven en
`/api/electronic-invoicing/documents/*` (§24). Emiten `FacturaProveedorRegistrada`. La generación por
operación o semanal la decide `DocumentoSoporte.Generacion`. Un documento soporte validado no se anula
(`Inventory.Document.FiscalUseCorrection`): se corrige con su nota de ajuste.

### 14.8 Eventos RADIAN de una factura a crédito — `/purchases/supplier-invoices/{id}/radian-events`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /` | Purchases.View | `[{ eventCode: Receipt030 \| GoodsReceived032, status: Pending \| RegisteredExternally \| Emitted \| Rejected \| NotApplicable, date?, source?: DianPortal \| SupplierPortal \| Erp, cude?, registeredBy?, registeredAt?, notes?, electronicDocumentPublicId? }]` |
| `POST /` | Purchases.RegisterRadianEvent | `{ eventCode, date, source: DianPortal \| SupplierPortal, cude?, notes?, correct? }` → la lista actualizada (`RegisterExternalRadianEventCommand`, I1). `correct: true` **(nuevo, US9)** corrige un registro externo ya hecho (antes y después en la auditoría, `Inventory.RadianEvent.Corrected`); sin él, un evento hecho responde `.AlreadyRegistered` |
| `POST /emit` | Purchases.EmitRadianEvent | `{ eventCodes: [Receipt030, GoodsReceived032] }` → 202 `{ events: [{ eventCode, electronicDocumentPublicId, status: Pending }] }` (`EmitRadianEventCommand`, I5) |

Reglas (FR-050, US9-4, US13-4, T42):
- Sólo facturas a crédito (422 `Inventory.RadianEvent.NotApplicable`); el 032 exige el 030 registrado o
  emitido (`.OutOfOrder`); fecha entre la emisión de la factura y hoy (`.DateInvalid`); un evento ya
  registrado o emitido: `.AlreadyRegistered`.
- **Hasta I5**, la cooperativa emite los eventos en el portal de su proveedor o de la DIAN y el ERP anota
  quién dijo haberlo hecho, cuándo y con qué fuente. **Desde I5**, el ERP los emite por el mismo canal de
  facturación electrónica: cada evento es un `COR_ElectronicDocuments` de `Kind = RadianEvent030/032`
  enlazado por `ElectronicDocumentPublicId`, procesado por el procesador de §24; el 032 exige una
  recepción confirmada enlazada a la factura (`.ReceiptNotConfirmed`) y el canal listo
  (`ElectronicInvoicing.NotReady`).
- La alerta `Compras.EventosRadianFaltantes` sale a los `Compras.DiasAlertaEventosRadian` días si falta
  alguno y se atiende sola cuando están los dos.

### 14.9 Compras completas (I5): solicitudes, órdenes, cruce a tres vías y costos adicionales

**Solicitudes** — `/purchases/requests` (`PurchaseRequest`): ciclo de §9.3 con aprobación por la política
del tipo; sin efecto en inventario. Cuerpo `{ documentTypePublicId, operationDate?, warehousePublicId,
neededBy, requestedByPersonPublicId?, notes?, lines: [{ productPublicId, unitPublicId, quantity, notes? }]
}`; el detalle agrega por línea `pendingToOrder`.

**Órdenes** — `/purchases/orders` (`PurchaseOrder`): ciclo de §9.3 con límite de monto y política
(FR-048). Cuerpo `{ documentTypePublicId, operationDate?, supplierPersonPublicId, warehousePublicId,
expectedDate, paymentTerms?, notes?, lines: [{ productPublicId, unitPublicId, quantity, unitPrice,
discountPercent?, discountAmount?, requestLinePublicId? }] }`; el detalle agrega `pendingToReceive`. Rutas
propias:

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /purchases/orders/{id}/pdf` | Purchases.View | la orden en PDF (QuestPDF) |
| `POST /purchases/orders/{id}/send` | Purchases.Confirm | `{ email? }` (por defecto el correo del proveedor en el maestro) → 204; envía el PDF por `IEmailSender`. 422 `Inventory.PurchaseOrder.NotConfirmed`, `.SupplierEmailMissing` |
| `POST /purchases/orders/{id}/close-balance` | Purchases.Confirm | `{ reason }` → 204: cierra lo pendiente de recibir; ya no admite recepciones |

Una recepción contra orden lleva `orderLinePublicId` en sus líneas; de otro proveedor:
`Inventory.Purchase.OrderFromOtherSupplier`; orden sin confirmar o con saldo cerrado:
`Inventory.PurchaseOrder.NotOpen`; lo recibido de más sólo dentro de la tolerancia
(`Compras.ToleranciaCantidad*`): si no, 422 `Inventory.Purchase.OverReceiptBeyondTolerance` (`data: {
lineNumber, ordered, received, tolerance }`).

**Cruce a tres vías** — `/purchases/matches`:

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /purchases/matches?status=&supplierPersonPublicId=&page=&pageSize=` | Purchases.View | `PagedResult<PurchaseMatchLineDto { publicId, supplierInvoice { publicId, displayNumber, supplierNumber }, lineNumber, product, ordered, received, invoiced, orderPrice, receiptPrice, invoicePrice, quantityVariance, priceVariance, reasons: [Quantity, Price], status: Held \| Approved \| Rejected, approvalRequestPublicId? }>` |
| `GET /purchases/supplier-invoices/{id}/match` | Purchases.View | el cruce de esa factura, línea por línea |

Al confirmar una factura contra recepciones con orden, `CruceDeCompra` compara por línea lo ordenado, lo
recibido y lo facturado, en cantidad y precio, con `Compras.Tolerancia{Cantidad,Precio}{Porcentaje,Valor}`
y `Compras.ReglaDeTolerancia`. Las líneas que exceden quedan `Held` y la factura pasa a
`PendingApproval` (política del tipo; si no tiene, un nivel con `Inventory.Purchases.Approve`). Aprobada:
la diferencia de precio ajusta el costo de lo existente y el costo de venta de lo vendido, con sus
mensajes (US13-2); rechazada: la factura vuelve a borrador.

**Costos adicionales** — `/purchases/landed-costs` (`LandedCost`): ciclo de §9.3. Cuerpo `{
documentTypePublicId, operationDate?, supplierInvoicePublicId (la factura del flete o del seguro, de
servicio), receiptPublicIds[], amount?, method: Value \| Quantity \| Weight \| Volume \| Manual,
manualAllocations?: [{ receiptLinePublicId, amount }] }`. El borrador devuelve `allocations: [{
receiptLine, product, basis, allocated, toInventory, toCostOfSales }]` y `roundingResidue` (`Prorrateo`:
suma exacta por residuo mayor y diferencia visible; US13-3: $100.000 por valor sobre $600.000 y $400.000
reparte $60.000 y $40.000). Confirmar emite un `AjusteDeCostoReconocido` por documento afectado. Errores:
`Inventory.LandedCost.BasisMissing` (`data: { products[] }`: sin peso o volumen),
`.ManualNotBalanced` (`data: { amount, allocated }`), `.ExceedsInvoice` (`data: { available }`: más de lo
que queda sin repartir de la factura), `.InvoiceNotService`, `Inventory.Purchase.ReceiptNotConfirmed`.

### 14.10 Errores de compras

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Inventory.Purchase.MunicipalityUnknown` · `.MixedPostingDestinations` | 422 | §14.1 | `{ receipts[] }` |
| `Inventory.Purchase.ReceiptFromOtherSupplier` · `.ReceiptNotConfirmed` · `.InvoiceExceedsReceived` | 422 | §14.4 | `{ lineNumber, received, invoiced, returned, available }` |
| `Inventory.Purchase.OrderFromOtherSupplier` · `.OverReceiptBeyondTolerance` · `Inventory.PurchaseOrder.NotOpen` · `.NotConfirmed` · `.SupplierEmailMissing` | 422 | §14.9 | `{ lineNumber, ordered, received, tolerance }` |
| `Inventory.SupplierInvoice.Duplicate` · `.CufeDuplicate` · `.CufeRequired` · `.DueDateInvalid` · `.FileUnreadable` · `.NotAnInvoice` | 422 | §14.4 | `{ documentPublicId, displayNumber }` |
| `Inventory.SupplierNote.ExceedsInvoice` | 422 | §14.5 | `{ remaining }` |
| `Inventory.Purchase.GoodsWithoutReceipt` · `Inventory.SupplierInvoice.IssueDateInvalid` · `Inventory.SupplierNote.InvoiceFromOtherSupplier` · `.InvoiceNotConfirmed` · `.InvoiceLineRequired` **(nuevos, US9)** | 422 | §14.4, §14.5 | `{ lineNumber, productCode }` · `{ today }` · `{ invoicePublicId, displayNumber }` · `{ lineNumber }` |
| `Inventory.Return.ReceiptLineRequired` · `.ExceedsReceived` | 422 | §14.6 | `{ received, alreadyReturned }` |
| `Inventory.RadianEvent.NotApplicable` · `.OutOfOrder` · `.DateInvalid` · `.AlreadyRegistered` · `.ReceiptNotConfirmed` | 422 | §14.8 | |
| `Inventory.LandedCost.BasisMissing` · `.ManualNotBalanced` · `.ExceedsInvoice` · `.InvoiceNotService` | 422 | §14.9 | |
| `Inventory.ProductTax.RateNotInForce` · `Taxation.Uvt.Missing` | 422 | §14.1 | `{ lineNumber, taxCode, date }` · `{ date }` |
| `Inventory.Numbering.ResolutionUnavailable` · `ElectronicInvoicing.NotReady` | 422 | §14.7, §14.8 | `{ missing[] }` |

## 15. Aprobaciones — `/api/inventory/{approval-policies, approvals, amount-limits}` (`ApprovalsEndpoints.cs`, I1)

Un solo motor de plataforma (`IMotorDeAprobaciones`, `EvaluadorDePolitica` puro, tablas `COR_Approval*`;
T33) para todo lo que pide aprobación: tipos de documento, ajustes de conteo, saldo inicial, diferencias
de traslado, cruce de compras (I5), y en §18–§23 descuentos sobre tope, diferencias de arqueo,
movimientos de caja y crédito provisional. No hay rutas de aprobación por documento.

### 15.1 Políticas por sujeto y tipo de documento — `/approval-policies`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?subject=&documentTypePublicId=&asOf=&includeHistory=` | ApprovalPolicies.View | `[ApprovalPolicyDto { publicId, module: "Inventory", subject, documentType? { publicId, code, name, class }, version, validFrom, validTo?, reason, levels: [{ order, threshold, permissionCode }], createdBy, createdAt }]` |
| `POST /` | ApprovalPolicies.Manage | `{ subject, documentTypePublicId?, validFrom, reason, levels: [{ order, threshold, permissionCode }] }` → 201 `ApprovalPolicyDto` (`SaveApprovalPolicyCommand`) |

- `subject` es una de las constantes `ApprovalSubjects` (`DocumentConfirmation`, `DiscountOverCap`,
  `ProvisionalCredit`, `TransferDiscrepancy`, `PurchaseMatchException`; otro valor: 400
  `Validation.Invalid`). `documentTypePublicId` nulo = todos los tipos del sujeto; la de un tipo gana
  sobre la de todos (`PolicyKey` = `{Module}|{Subject}|{DocumentTypePublicId|*}`). El módulo es
  `Inventory` en todo el contrato y en la tabla.

- Cada alta es una **versión nueva** del mismo (sujeto, tipo) que cierra la anterior la víspera de `validFrom`; `levels: []`
  significa «sin aprobación» desde esa fecha. Una versión posterior ya registrada: 422
  `Approvals.Policy.Overlaps`; `validFrom` en un período de inventario cerrado:
  `Approvals.Policy.ValidFromInClosedPeriod`.
- Niveles con `order` 1..n consecutivos y umbrales no decrecientes y ≥ 0 (422
  `Approvals.Policy.LevelsInvalid`, `data: { reason }`); `permissionCode` de cualquier código del catálogo
  (C6) —los sugeridos son el `Approve` del grupo, `Inventory.Approvals.Supervisor` y
  `Inventory.Approvals.Management`— (422 `Approvals.Policy.PermissionUnknown`, `data: { permissionCode }`).
- Un documento exige, en orden, **todos** los niveles cuyo umbral alcanza su monto; por debajo del primero
  se confirma sin aprobación (FR-010). La política vigente en la **fecha de operación** se sella en la
  solicitud: cambiarla después no altera una solicitud pendiente.
- Los tipos que siempre se aprueban —el de saldo inicial y los de ajuste de conteo, sembrados con un
  nivel de umbral 0— no admiten una política vacía: 422 `Approvals.Policy.RequiredForClass` (`data: {
  class }`). La resolución de una diferencia de traslado se aprueba con la política del sujeto
  `TransferDiscrepancy` o, sin ella, con un nivel fijo (§11).

### 15.2 Bandeja y decisión — `/approvals`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?status=&mine=&documentClass=&warehousePublicId=&page=&pageSize=` | Approvals.View | `PagedResult<ApprovalRequestDto>` (`ListMyPendingApprovalsQuery`). `mine=true` (defecto): las que el usuario puede decidir **ahora** (tiene el permiso del nivel actual y alcance, y no está excluido); `mine=false`: las del alcance en cualquier estado, incluidas las que pidió (seguimiento) |
| `GET /{id}` | Approvals.View | `ApprovalRequestDto` + `decisions: [{ level, decision, decidedBy, decidedAt, reason?, method }]`. El POS consulta cada 2 s mientras espera |
| `POST /{id}/decide` | Approvals.View (+ el permiso del nivel) | `{ decision: Approve \| Reject, reason?, method: OwnSession \| InPersonPasskey \| InPersonTotp, presence?: { challengePublicId, assertion?, totpCode? }, expectedContentSha256 }` → 200 `DecisionResultDto { requestPublicId, status, currentLevel, source: { publicId, class, status, displayNumber? } }` (`DecideApprovalCommand`) |
| `POST /{id}/presence-challenge` | Approvals.View | `{ approverEmail }` → 200 `{ challengePublicId, expiresAt, methods: [Passkey, Totp], publicKeyOptions? }` (base64url, para `wwwroot/js/webauthn.js`) |
| `POST /{id}/withdraw` | Approvals.View | `{ reason }` → 204: el solicitante o el creador retiran la solicitud (`Cancelled`) y el documento vuelve a borrador para corregirlo |

**`ApprovalRequestDto`**: `{ publicId, module, subject, sourceType, source: { publicId, class, documentType {
code, name }, displayNumber?, operationDate, warehouse?, pointOfSale?, summary, route }, amount, currency,
status: Pending \| Approved \| Rejected \| Cancelled, currentLevel, levels: [{ order, threshold,
permissionCode, state, decidedBy?, decidedAt?, method? }], createdBy, requestedBy, requestedAt,
contentSha256, canDecide, excludedReason? }` — `route` es la pantalla del documento; `state` de cada
nivel es texto (`Waiting`, `Pending`, `Approved`, `Rejected`); `excludedReason` explica por qué el
usuario no puede decidir (`Creator`, `Requester`, `Participant`, `PreviousLevel`).

Reglas (FR-010, T33):
- La ruta exige `Approvals.View`; **decidir** exige además el `permissionCode` del nivel actual y alcance
  sobre las bodegas o el punto del documento. Sin ellos, 404 `Approvals.Request.NotFound`, igual que una
  solicitud inexistente.
- **Segregación fija, no parametrizable**: no deciden el creador del documento, quien pidió la
  aprobación, los participantes que declara el documento (quien abrió o capturó el conteo, quien despachó
  o recibió el traslado, el cajero) ni quien ya aprobó otro nivel: 422 `Approvals.SelfApprovalForbidden`
  con `data: { reason }`. La identidad es `SEC_Users.Id` de `IActorActual`.
- `expectedContentSha256` debe ser el `contentSha256` que el aprobador vio; si el documento cambió (un
  descuento recalculado), 422 `Approvals.Request.ContentChanged` (`data: { currentSha256 }`) y la
  solicitud queda invalidada.
- **Rechazar** exige `reason` (400 si falta), deja la solicitud `Rejected` y devuelve el documento a
  borrador.
- **Aprobar** registra la decisión (`COR_ApprovalDecisions`, sólo inserción). Si quedan niveles, pasa al
  siguiente (y alerta `Aprobaciones.Pendiente` a sus titulares). La **última** aprobación confirma el
  documento en la transacción del aprobador, repitiendo la confirmación de §9.3 (existencia, período,
  validación previa, numeración): si algo falla, **la decisión no queda** y la respuesta es ese error
  (p. ej. 422 `Inventory.Stock.Insufficient`); la solicitud sigue pendiente para que el creador la
  retire y corrija.
- Solicitud que ya no está pendiente: 422 `Approvals.Request.NotPending` (`data: { status }`).
- **Métodos**: `OwnSession` es el aprobador desde su sesión (bandeja `/inventario/aprobaciones`).
  `InPersonPasskey` e `InPersonTotp` son el aprobador **presente** en el equipo de quien pidió (el
  supervisor junto a la caja): la sesión debe ser la del solicitante (si no, 422
  `Approvals.Presence.NotRequester`); primero `presence-challenge` con el correo del aprobador y luego
  `decide` con la aserción WebAuthn de **sus** credenciales (`IWebAuthnService`) o un código TOTP de un
  solo uso. El aprobador así identificado cumple las mismas reglas (permiso, alcance, segregación). Una
  prueba fallida responde 422 `Approvals.Presence.Invalid`, **nunca 401**: un 401 haría que
  `RenovacionDeSesionHandler` renovara y reintentara la sesión del cajero. Otros: `.Expired` (el desafío
  vence a los 2 minutos), `.TotpReused`. Nunca se pide ni se guarda una contraseña.
- `withdraw`: sólo el solicitante o el creador (si no, 404) y sólo pendientes (`Approvals.Request.NotPending`).
- `decide`, `presence-challenge` y `withdraw` llevan `Idempotency-Key`.

### 15.3 Montos máximos por permiso — `/amount-limits`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?rolePublicId=&permissionCode=&asOf=` | ApprovalPolicies.View | `[{ publicId, role { publicId, name }, permissionCode, maxAmount?, currency, validFrom, validTo?, reason, createdBy, createdAt }]` |
| `POST /` | ApprovalPolicies.Manage | `{ rolePublicId, permissionCode, maxAmount, validFrom, reason }` → 201 (`SetPermissionAmountLimitCommand`); `maxAmount: null` = sin límite desde `validFrom` |

Sólo los permisos limitables de §1.3 (422 `Approvals.AmountLimit.NotLimitable`, `data: { limitable[] }`);
el rol debe conceder ese permiso (`Approvals.AmountLimit.RoleLacksPermission`); vigencias sin cruces
(`Approvals.AmountLimit.Overlaps`); moneda `COP`. Rol inexistente: 404 `Generic.NotFound`. Rige con
el límite vigente en la fecha de operación del documento.

### 15.4 Errores de aprobaciones

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Approvals.Request.NotFound` · `Approvals.Policy.NotFound` | 404 | inexistente, sin el permiso del nivel o fuera del alcance | |
| `Approvals.Policy.Overlaps` · `.ValidFromInClosedPeriod` · `.LevelsInvalid` · `.PermissionUnknown` · `.RequiredForClass` | 422 | §15.1 | |
| `Approvals.SelfApprovalForbidden` | 422 | segregación | `{ reason }` |
| `Approvals.Request.NotPending` · `.ContentChanged` | 422 | §15.2 | `{ status }` · `{ currentSha256 }` |
| `Approvals.Presence.NotRequester` · `.Invalid` · `.Expired` · `.TotpReused` | 422 | aprobación presencial | |
| `Approvals.AmountLimit.NotLimitable` · `.RoleLacksPermission` · `.Overlaps` | 422 | §15.3 | `{ limitable[] }` |

## 16. Alertas y alcance

### 16.1 Bandeja de alertas — `/api/inventory/alerts` (`AlertsEndpoints.cs`, I1)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?status=&typeCode=&module=&severity=&from=&to=&page=&pageSize=` | Alerts.View | `PagedResult<AlertDto { publicId, typeCode, module, severity: Info \| Warning \| Critical, subject, body, entity? { type, publicId, label, route }, status: Pending \| Attended, raisedAt, attendedAt?, attendedBy?, attendNote?, withoutRecipient }>`, la más reciente primero. El contador de la campana es `totalCount` con `status=Pending&pageSize=1` |
| `GET /{id}` | Alerts.View | `AlertDto` |
| `POST /{id}/attend` | Alerts.Attend | `{ note }` → `AlertDto` (`AttendAlertCommand`). Ya atendida: 422 `Alerts.Alert.AlreadyAttended` (`data: { attendedBy, attendedAt }`) |

- Cada usuario ve las alertas que lo alcanzan: tiene uno de los permisos destinatarios del tipo y alcance
  sobre la bodega o el punto de la alerta; las demás son 404 `Alerts.Alert.NotFound`.
- Las levanta `RaiseAlertCommand` (confirmaciones, `ProgramadorDeTareas`, despachadores) con `DedupKey`:
  mientras una está pendiente, la misma condición no levanta otra. Se entregan como `COR_Notifications`
  (`NotificationType.Alert`) y, si el tipo tiene el canal, por correo. Sin destinatario activo, van a los
  titulares de `CompanyAdmin` y salen con `withoutRecipient: true` (SC-022).
- El estado es **compartido** (FR-022): la atiende una persona, con nota, y queda quién y cuándo. Algunas
  se atienden solas cuando desaparece su causa (los dos eventos RADIAN registrados, la diferencia de
  traslado resuelta), con el proceso como actor. Atender no corrige la causa: si sigue, la próxima
  revisión levanta otra.
- Tipos que nacen en esta parte: `Inventario.Reorden`, `Inventario.Quiebre`,
  `Inventario.IncidenteDeIntegridad`, `Compras.EventosRadianFaltantes`, `Aprobaciones.Pendiente` (I1); los
  demás de `decisiones-transversales.md` §2.13, en §18–§28.

### 16.2 Tipos de alerta — `/api/inventory/alert-types`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /` | Alerts.View | `[AlertTypeDto { typeCode, module, description, severity, recipientPermissions[], channels: [InApp, Email], thresholds?, validFrom, validTo?, reason?, activeRecipients, withoutRecipient, availableFrom }]` (`activeRecipients` = usuarios activos que hoy la recibirían) |
| `GET /{typeCode}/history` | Alerts.View | las vigencias, la más reciente primero |
| `POST /{typeCode}/versions` | Alerts.Manage | `{ recipientPermissions[], channels, thresholds?, validFrom, reason }` → 201 `AlertTypeDto` (`SaveAlertTypeCommand`) |

Errores: 404 `Alerts.Type.NotFound` (el `typeCode` es del catálogo cerrado de `decisiones-transversales.md` §2.13; la
cooperativa configura, no inventa tipos); 422 `Alerts.Type.PermissionUnknown` (`data: { permissionCode
}`), `.RecipientsRequired`, `.InAppRequired` (la notificación en la aplicación siempre va; el correo es
opcional), `.ThresholdsInvalid` (`data: { key }`), `.Overlaps`.

### 16.3 Alcance comercial de un usuario — `/api/inventory/scopes/users/{userPublicId}` (`ScopesEndpoints.cs`)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET` | Scopes.Manage | `{ user { publicId, name, email }, hasAllWarehouses, hasAllPointsOfSale, warehouses: [{ warehousePublicId, code, name, isDefault }], pointsOfSale: [{ pointOfSalePublicId, code, name, isDefault }] }` |
| `PUT` | Scopes.Manage | `{ warehouses: [{ warehousePublicId, isDefault }], pointsOfSale?: [{ pointOfSalePublicId, isDefault }] }` → el mismo DTO |

Reemplaza las asignaciones (las retiradas quedan de baja lógica; todo auditado). A lo sumo una por
defecto de cada clase (422 `Inventory.Scope.DefaultDuplicate`); se puede asignar una bodega de tránsito
explícitamente; `pointsOfSale` desde I3. Quien administra sólo asigna bodegas y puntos de su propio
alcance (lo demás es 404). Es la pestaña «Alcance comercial» de `/admin/usuarios`.

## 17. Lo que este contrato agrega a los nombres canónicos, y lo que decide

### 17.1 Nombres nuevos (no están en `decisiones-transversales.md` §2; `data-model.md` los adopta o los cambia)

- **Enums de cuerpo**: `ProductUnitUsage { Purchase=1, Sale=2, Both=3 }`; `PurchasePaymentForm { Cash=1,
  Credit=2 }`; `SupplierNoteKind { Credit=1, Debit=2 }`; `RadianEventSource { DianPortal=1,
  SupplierPortal=2, Erp=3 }`; `LandedCostAllocationMethod { Value=1, Quantity=2, Weight=3, Volume=4,
  Manual=5 }`; `PurchaseMatchStatus { Held=1, Approved=2, Rejected=3 }`. Los estados derivados (`state` de traslados, conteos, diferencias y niveles de aprobación,
  `ParameterDto.type`) son **texto de presentación**, no enums de dominio.
- **Rutas auxiliares** que no enumera `decisiones-transversales.md` §2.9: `GET /document-types/classes`,
  `POST /document-types/{id}/sequences`, `GET /products/{id}/accounting-group` (historial),
  `POST /products/{id}/status`, `…/reactivate` en los catálogos, `GET /warehouses?purpose=TransferDestination`,
  `GET /transfers/discrepancies`, `GET /counts/{id}/captures`, `POST /counts/{id}/void` (anulación con documento `Voiding`), `POST /counts/{id}/discard` (también de un conteo abierto),
  `POST /api/inventory/documents/{id}/cost-impact` (I5),
  `GET /periods/{year}/{month}/close` (vista previa), `GET /legacy-figures/{batchId}/rows`,
  `POST /purchases/supplier-invoices/prefill`, `GET /purchases/supplier-invoices/{id}/match`,
  `POST /purchases/supplier-invoices/{id}/radian-events/emit`, `GET|POST /purchases/orders/{id}/{pdf,send,close-balance}`,
  `GET /approvals/{id}`, `POST /approvals/{id}/presence-challenge`, `POST /approvals/{id}/withdraw`,
  `GET /alert-types/{typeCode}/history`, `POST /alert-types/{typeCode}/versions`,
  `GET /parameters/{module}/{key}/history`, `POST /parameters/{module}/{key}/versions`,
  `POST /api/inventory/integrity/verify|rebuild` con cuerpo de filtros.
- **Eventos de auditoría**: `Inventory.Catalog.Exported`, `Inventory.Integrity.Verified`.
- **Reporte**: la orden de compra en PDF (I5) necesita una clase `PurchaseOrderReport`, que no está en la
  lista de reportes de las decisiones.
- **Área de error** `Taxation.*` (`Taxation.Uvt.Missing`), por `Application/Common/Taxation/LectorDeUvt`;
  el catálogo tributario de §30 usa la misma familia.
- **Nombres de catálogo** para `BuscarCodigoDeCatalogoQuery` (`GET /api/catalogos/{catalogo}/codigo/{codigo}`):
  `unidades`, `categorias`, `marcas`, `grupos-contables`, `productos`, `tipos-de-bodega`, `bodegas`,
  `tipos-de-documento`, `causas-de-ajuste`, `canales-de-venta` (sin prefijo de módulo, como los
  existentes `eps`, `cuentas`, `agencias`, `centros-costo` y los de §18–§31).

### 17.2 Decisiones que toma este contrato (a confirmar al fundir las dos partes)

1. **Activación antes de I2**: la ruta existe en I1; **sólo fuera de producción**, sin matriz ni saldos
   contables, activa con `acceptDifference`, el permiso especial y motivo («sin comparación contable»),
   para poder ensayar I1. En producción el POST responde 422 `Inventory.Activation.AccountingUnavailable`
   mientras la consulta de saldos no exista: COOFLOPAL activa con la comparación de I2 (FR-090).
2. **Destino de un traslado fuera del alcance de quien despacha**: se admite (`purpose=TransferDestination`
   devuelve todas las operativas activas, sin existencias ni valores); la recepción sí exige alcance
   sobre el destino. Lectura de T35: «tocar» una bodega es moverle existencia, y el despacho no mueve la
   del destino.
3. **Permisos que dependen del cuerpo** (`SetUnitCost`, `AcceptActivationDifference`,
   `AcceptUnbilledShipments`, `DisableFiscalPosting`, el de una clave de parámetro): 422 con código propio,
   no 404, porque el recurso ya es visible, no revelan la existencia de nada y la pantalla necesita saber
   qué falta. Precisión aplicada a la spec sobre FR-009; pregunta al dueño C10.
4. **`POST /approvals/{id}/decide` exige `Approvals.View`** (y el permiso del nivel se revisa adentro),
   para que la aprobación presencial funcione desde la sesión del cajero; por eso la plantilla
   `inventario.cajero` incluye `Inventory.Approvals.View`.
5. **Las diferencias de traslado, los ajustes de conteo y el saldo inicial se aprueban siempre**, aunque
   no haya política (FR-039, FR-041, FR-089): el motor exige un nivel con el `Approve` del grupo. La
   diferencia de traslado toma la política del sujeto `TransferDiscrepancy` del tipo de la recepción de
   traslado (§11).
6. **`INV_Setup`** (y su `StartDate`, primer día del mes del primer corte) la crea el primer registro de
   una fecha de corte o `ImportOpeningBalanceCommand` (data-model §6.1).
7. **Claves `TAX` y `EINV`** se registran por la misma ruta de parámetros con su permiso propio; si
   §24 o §30 las exponen desde `/api/electronic-invoicing/settings` o `/maestros/impuestos`, usa el mismo
   `AddParameterVersionCommand`, no otro camino.
8. **Una sola recepción por despacho**: lo que llega después es una resolución `LateReceipt` del
   faltante, no una segunda recepción.
9. **Retroactivos mínimos en I1** (precisión aplicada a la spec sobre FR-045; preguntas al dueño D8 y
   D9, con esta excepción como defecto propuesto): el `OpeningBalance` (y su `Voiding`) de una bodega
   `NotActivated` y los ajustes que genera un conteo aprobado no dependen de
   `Costeo.RetroactivosPermitidos`; el motor los inserta en orden `(OperationDate, Id)`, recalcula las
   salidas posteriores cuando hace falta y registra `AjusteDeCostoReconocido` por documento afectado
   (§12, §13.1). Los retroactivos generales siguen en I5.
10. **Conteo**: abrir congela la foto sin numerar; cerrar confirma y numera; un conteo abierto se
   descarta; uno cerrado se anula con documento `Voiding` sin kardex ni mensajes (§12, FR-038, FR-006).
11. **Plantillas**: una sola fuente, contracts/plantillas.md; este contrato no repite su mecánica ni sus
   columnas (§2.9, §3.9, §13.1, §13.2).

### 17.3 Lo que no tiene ruta

Los comandos de consumo de mensajes (`PostInventoryMessagesCommand`, `RegisterDeliveryResultCommand`) y los del ciclo del lote que envía el despachador (`ScheduleIntegrationBatchesCommand`, `StartIntegrationBatchCommand`, `CloseIntegrationBatchCommand`; lo
vigila `LosComandosDeConsumoNoTienenRuta`); los documentos `CostAdjustment` (los genera el sistema);
`RaiseAlertCommand` (lo llaman los procesos); la emisión de mensajes (`EmisorDeMensajes`, dentro de cada
confirmación).

---


## 18. Ventas — `/api/inventory/sales`

**Convenciones**: §2 (sobre de error y estados, sin permiso o fuera de alcance = 404
`Generic.NotFound`, `PublicId`, fechas, decimales, enums, idempotencia, paginación `PagedResult` con
`totalPages`) y, para toda plantilla, contracts/plantillas.md §0 (`GET …/template.xlsx[?withData=true]`,
`POST …/import?mode=review|apply`, `ImportResultDto`, errores `{ row, sheet, column, code, message }`,
`Import.Invalid`). Los nombres canónicos están en `decisiones-transversales.md`, en esta carpeta. Lo que
esta parte agrega o precisa:

- **En este documento** los enums se escriben por nombre y, la primera vez, con su número entre
  paréntesis. En cada tabla los permisos se escriben sin el prefijo del módulo: `Sales.View` es
  `Inventory.Sales.View`; los de otros módulos se escriben completos.
- **Clave de operación (T13)**: la llevan todas las rutas que escriben de las secciones 18 a 25, 30 y
  31; en la sección 26 sólo ordenar un lote. Las consultas que viajan por `POST` (`documents/{id}/prevalidate`,
  `sales/credit-evaluations`, `/api/accounting/inventory/batches/preview`,
  `/api/audit/integrity/verify`) no la llevan; la lista completa es la de la §2.3.
- **Canal `pos`**: los comandos de la venta en el POS y de la caja (§20.2 y §21) implementan
  `IOperacionDePuntoDeVenta`, así que `AuditBehavior` los registra con canal `pos` (T36).
- **Permisos que dependen del cuerpo** (reintegrar por otro medio sin `RefundOtherMeans`,
  `Payments.RefundMeansNotAllowed`; pagar con un medio de crédito sin `SellOnCredit`,
  `Payments.MeansNotAvailable`): el recurso ya es visible, así que faltar el permiso responde **422 con código
  propio**, no 404, porque no revela existencia (§1.2). Es una precisión aplicada a la spec
  (FR-009) y pregunta al dueño C10.

**Ruta y clase.** Las ventas son documentos de `INV_Documents` del grupo `Sales` (T17). La ruta fija la
familia de permisos: `Inventory.Sales.{View, Create, Confirm, Approve, Void, SellOnCredit,
RefundOtherMeans}`. La clase la fija el **tipo de documento** elegido, y cada ruta sólo acepta las
clases que le corresponden; otra clase responde 422 `Inventory.Document.TypeNotForRoute`, con
`data: { class, group }` (el mismo código de la §9.6). Un documento de venta confirmado no se edita (`IInmutableTrasConfirmar`).
Se anula con un documento contrario si no es fiscal electrónico, o con su documento de corrección si
lo es (FR-066).

### 18.1 Consulta de documentos de venta

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /documents?class=&documentType=&status=&from=&to=&pointOfSale=&cashRegister=&cashSession=&person=&salesperson=&number=&electronicStatus=&pendingDelivery=&pendingValidation=` | Sales.View | paginado de `SalesDocumentSummaryDto` `{ documentPublicId, class, documentTypeCode, prefix, number?, status, operationDate, pointOfSaleCode?, cashRegisterCode?, counterpartyName, salespersonName?, total, amountDue, payments: [{ meansCode, meansClass, amount }], postingMode?, electronicStatus?, pendingDelivery, pendingValidation }`. Se filtra por alcance: bodega del documento o punto de venta. `pendingDelivery=true&cashSession=` es la lista «pendientes de entrega» de la caja (§20.3) |
| `GET /documents/{id}` | Sales.View | `SalesDocumentDto` (abajo) |
| `POST /documents/{id}/deliver` | Sales.View | primera entrega al comprador, §20.3 |
| `GET /documents/{id}/credit` | Sales.View | pagos a crédito y su estado frente a Cartera, §23.2 |

`SalesDocumentDto`:

- **Cabecera**: `{ documentPublicId, class, documentType: { publicId, code, name, role? }, prefix,
  number?, status, operationDate, confirmedAt?, warehouse, branch, pointOfSale?, cashRegister?,
  cashSessionPublicId?, salesChannel, postingMode? }`. `status` es `DocumentStatus`: `Draft` (0),
  `PendingApproval` (1), `Confirmed` (2), `Voided` (3), `Discarded` (4).
- **Contraparte**: `counterparty: { personPublicId, isFinalConsumer, name, idType, idNumber,
  address?, email? }`. En un documento confirmado es la **copia fiscal vigente**
  (`INV_DocumentPartySnapshots`, T52), no el maestro de hoy. `salesperson?: { salespersonPublicId,
  name }`.
- **Líneas**: `lines: [{ linePublicId, lineNumber, product: { publicId, code, name }, unit: {
  publicId, code }, factor, quantity, quantityBase, roundingQuantity, listPrice, unitPrice,
  includesTaxes, priceList?: { publicId, code, name }, discounts: [{ source: Manual (1) \| Promotion
  (2), percent?, amount, promotionPublicId?, approval?: { approvalRequestPublicId, status,
  approvedByName?, method? } }], taxes: [{ taxRateCode, kind, rate?, amountPerUnit?, base, amount,
  treatment }], subtotal, total, belowCost }]`. `belowCost` no expone el costo, que exige
  `Inventory.Costs.Read`.
- **Retenciones y totales**: `withholdings: [{ taxRateCode, kind, rate, base, amount, treatment:
  WithholdingSuffered }]` y `totals: { subtotal, discountTotal, taxTotal, withholdingTotal, total,
  amountDue, roundingResidue }`. `amountDue = total − retenciones sufridas` (T26).
- **Pagos**: `payments: [DocumentPaymentDto]` (§22.4).
- **Integración**: `integration: { messages: [{ messagePublicId, type, destination,
  deliveryStatus, batchNumber? }] }`, para ver en qué va cada mensaje sin ir a la bandeja.
- **Bloque electrónico**: `electronic?: { electronicDocumentPublicId, kind, status, uniqueCode?,
  uniqueCodeKind?, qrContent?, contingencyType?, rejectedBy?, deliverable, deliveredAt? }`. Es el
  resumen de §24.4.
- **Vínculos y trazas**: `links: { origin?, voids?, voidedBy?, notes: [], replacementOf?,
  replacedBy? }`, `createdBy`, `confirmedBy?` e `issues: [{ code, message, lineNumber? }]` (lo que
  impediría confirmar un borrador).

### 18.2 Facturas y comprobantes de venta no electrónicos — `/invoices`

Clases que admite: `SalesInvoice` (26) y `NonElectronicSalesReceipt` (29). En I6 suma
`SalesInvoiceFromShipments` (27), con `originPublicIds` de remisiones, y la factura desde pedido.
Admite `PosEquivalentDocument` (28) **sólo** en un borrador de reemplazo del caso b (§24.5).

Cuál de las dos clases se puede confirmar no lo decide la pantalla: lo decide
`GuardiaDeEmisionFiscal.Evaluar`.

- Si responde `Electronic`, la cooperativa está obligada y lista, y sólo confirman los tipos de clase
  electrónica.
- Si responde `NonElectronic` (parámetro `Dian.ObligadaAFacturar` apagado), sólo confirman los de
  comprobante no electrónico.
- Si responde `Blocked`, no confirma ninguna venta fiscal: 422 `ElectronicInvoicing.NotReady` con
  `data.missing[]` (§24.3).
- Un tipo de la otra clase → 422 `Inventory.Sales.FiscalClassMismatch`, con `data: { verdict,
  allowedClasses }`.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `POST /` | Sales.Create | `SalesDraftInput` → 201 `SalesDocumentDto` en `Draft`, sin número (`SaveInventoryDraftCommand`). El borrador se guarda aunque tenga problemas de negocio; `issues[]` los lista |
| `PUT /{id}` | Sales.Create | `SalesDraftInput` → `SalesDocumentDto`. Sólo en `Draft`; en otro estado → 422 `Inventory.Document.NotDraft` (`data: { status }`) |
| `POST /{id}/confirm` | Sales.Confirm | `{ expectedAmountDue }` → `SalesConfirmationDto` (`ConfirmInventoryDocumentCommand(DocumentPublicId, ExpectedGroup = Sales)`, flujo canónico, `decisiones-transversales.md` §1.3). Si `expectedAmountDue` no coincide con lo que el servidor calcula → 422 `Inventory.Document.TotalChanged` (`data: { amountDue }`) |
| `POST /{id}/void` | Sales.Void | `{ reason, operationDate?, cashSessionPublicId? }` → `{ voidingDocumentPublicId, number, messages[] }` (`VoidInventoryDocumentCommand`). Sólo un `NonElectronicSalesReceipt` se anula con documento contrario (FR-066). Un documento fiscal electrónico responde 422 `Inventory.Document.FiscalUseCorrection`, con `data: { correctionClass: CreditNote \| PosAdjustmentNote, route: "/api/inventory/sales/credit-notes", totalVoid: true }`. Si fue enviado sin respuesta → 422 `ElectronicInvoicing.Document.AwaitingResponse`. Si fue rechazado → 422 `Inventory.Document.FiscalUseCorrection`, con `data.route` en los casos de §24.5. El reintegro de un pago en efectivo sale de `cashSessionPublicId`, una sesión abierta del usuario (T50) |
| `POST /{id}/discard` | Sales.Create | `{ reason? }` → `Discarded` (`DiscardInventoryDraftCommand`) |

`SalesDraftInput`:

```
{ documentTypePublicId, operationDate?, warehousePublicId, counterpartyPersonPublicId?,
  salespersonPublicId?, costCenterPublicId?, externalReference?, dueDate?, notes?,
  lines: [{ lineNumber?, productPublicId, unitPublicId, quantity, unitPrice?,
            discount?: { percent? | amount? }, notes? }],
  documentDiscount?: { percent? | amount? },
  payments: [DocumentPaymentInput],
  originPublicIds?: [] }
```

- **Contraparte**: sin `counterpartyPersonPublicId` la venta es a **consumidor final** (la persona que
  siembra `ConsumidorFinalSeeder`). La persona se da de alta sólo en `PersonaDialog` (FR-011).
- **Canal y sucursal**: salen del tipo de documento y de la bodega; no se escriben.
- **Precio**: sin `unitPrice`, lo resuelve `ResolutorDeListaDePrecios` (§19.2). Un `unitPrice`
  distinto del de la lista cuenta como descuento y respeta el tope (§19.3).
- **Venta bajo costo**: con `Ventas.BajoCosto = Alertar`, la línea queda con `belowCost = true` y la
  confirmación levanta la alerta `Inventario.VentaBajoCosto`. Con `Bloquear` → 422
  `Inventory.Sales.BelowCost` (`data: { lineNumber }`), al guardar y al confirmar.
- **Persona inactiva**: de contado se admite o se bloquea según `Ventas.PersonaInactivaDeContado`; si
  se bloquea → 422 `Inventory.Sales.PersonInactive`. A crédito nunca se admite (§23).
- **Pagos**: los valida `ValidadorDePagos` (§22.4). La suma de lo aplicado iguala `amountDue`
  (FR-056, T26). Todo pago cuyo medio se arquea exige una sesión de caja abierta del usuario, también
  en la oficina (T50).

`SalesConfirmationDto`:

```
{ documentPublicId, class, prefix?, number?, status: Confirmed | PendingApproval,
  approvalRequestPublicId?, postingMode?, prevalidation: { outcome: Postable | NoResponse | NotApplicable },
  total, amountDue, change,
  messages: [{ messagePublicId, type, destination, deliveryStatus }],
  electronic?: { electronicDocumentPublicId, kind, status, uniqueCode?, qrContent?, contingencyType?, deliverable },
  warnings: [{ code, message, data? }] }
```

- **Con niveles de aprobación** (política del tipo, monto sobre el límite del permiso o crédito
  provisional, §23): el documento queda `PendingApproval`, sin número. La última aprobación lo
  confirma en su propia transacción, por la bandeja de aprobaciones (§15). Sin política y por
  encima del límite → 422 `Inventory.Approval.AmountExceedsLimit` (`data: { maxAmount }`).
- **Emisión electrónica**: la factura de oficina **no espera** a la DIAN. Queda
  `electronic.status = Pending`, el procesador la emite (T40) y la pantalla consulta
  `GET /documents/{id}` hasta que sea `deliverable`. Sólo el POS espera (§20.2).
- **Validación previa** (FR-074, §25.1): si falla → 422 `Inventory.Prevalidation.NotPostable`, con
  `data.errors[] { messageType, lineNumber, account, rule, message, whoFixes: { module, page,
  permission } }`. Si Contabilidad no responde a tiempo y `Contabilidad.PoliticaSinRespuesta =
  Bloquear` → 422 `Inventory.Prevalidation.NoResponse`. Con `ConfirmarConPendiente` confirma y
  `prevalidation.outcome = NoResponse`.

### 18.3 Notas de venta — `/credit-notes`

Esta ruta admite tres clases. Cada una va con la clase de su documento original; si no coinciden →
422 `Inventory.CreditNote.ClassMismatch` (`data: { originClass, expectedClass }`).

| Clase de la nota | Documento original |
|---|---|
| `CreditNote` (31) | `SalesInvoice`, `SalesInvoiceFromShipments` |
| `PosAdjustmentNote` (32) | `PosEquivalentDocument` |
| `NonElectronicSalesNote` (30) | `NonElectronicSalesReceipt` |

La nota es un documento nuevo, con su número propio de `INV_DocumentSequences` (sin resolución, T16),
su propia fecha y su propio código único, y sale por el canal de emisión vigente (FR-064).

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `POST /` | Sales.Create | `CreditNoteDraftInput` → 201 `SalesDocumentDto` precargado con lo que queda por acreditar de cada línea |
| `PUT /{id}` | Sales.Create | `CreditNoteDraftInput`, sólo en `Draft` |
| `POST /{id}/confirm` | Sales.Confirm | `{ expectedAmountDue }` → `SalesConfirmationDto` |
| `POST /{id}/void` | Sales.Void | sólo `NonElectronicSalesNote` (documento contrario). Una nota electrónica validada → 422 `Inventory.Document.FiscalUseCorrection` (su corrección es una nota débito, I6) |
| `POST /{id}/discard` | Sales.Create | `{ reason? }` |

`CreditNoteDraftInput`:

```
{ originDocumentPublicId, documentTypePublicId?, operationDate?, correctionConceptCode, reason,
  totalVoid: bool, withReturn: bool, returnWarehousePublicId?,
  lines: [{ originLinePublicId, quantity?, amount? }],
  refunds: [DocumentPaymentInput] }
```

- **Motivo DIAN**: `correctionConceptCode` es el concepto de corrección del catálogo DIAN
  (`CatalogoDian`). Se exige en las clases electrónicas.
- **Anulación total** (`totalVoid = true`): acredita todo lo que queda. Es la «anulación» de un
  documento fiscal validado (FR-066): el mensaje `NotaCreditoEmitida` lleva la marca de anulación
  total.
- **Con devolución** (`withReturn`): la mercancía vuelve **al costo con que salió** a
  `returnWarehousePublicId` (por defecto, la bodega del original) y se emite `DevolucionRegistrada`.
  Sin devolución (descuento posterior) no se toca la existencia. Una nota de servicios sólo emite
  `NotaCreditoEmitida`. En I6, la nota de una factura desde remisiones no mueve existencia y deja
  sus remisiones sin facturar.
- **Tope**: cada línea acredita a lo sumo lo que queda (original − notas confirmadas, calculado por
  `INV_DocumentLineLinks`). Si se pasa → 422 `Inventory.CreditNote.ExceedsRemaining`, con
  `data: { lines: [{ originLinePublicId, remainingQuantity, remainingAmount }] }`.
- **Reintegros** (`refunds`, con `direction = Refunded`): por defecto van por el mismo medio de la
  venta y en proporción. Otro medio exige `Inventory.Sales.RefundOtherMeans`; sin él → 422
  `Payments.RefundMeansNotAllowed`, y con él queda auditado. La suma de los reintegros iguala el
  `amountDue` de la nota (`Payments.TotalMismatch`). Un reintegro en efectivo sale de una sesión
  abierta del usuario, aunque la venta haya sido en otra caja (T50).
- **Bono**: un bono reintegrado vuelve a estar disponible. Su `INV_VoucherRedemptions` pasa a
  `Released`, con el documento que lo liberó.
- **Crédito**: el reintegro a un medio de crédito disminuye la parte financiada y emite
  `AjusteDeVentaACredito` con `AdjustmentClass = CreditNote` hacia Cartera (§23.4).
- **Original sin respuesta de la DIAN**: una nota sobre un original enviado sin respuesta → 422
  `ElectronicInvoicing.Document.AwaitingResponse`. Sobre uno rechazado → 422
  `Inventory.Document.FiscalUseCorrection` con `data.route` en §24.5: un rechazado no está expedido
  y no se corrige con nota. Sobre uno expedido en contingencia se admite; su transmisión espera a la
  del original (FR-066).

#### 18.3.1 Factura después del documento equivalente (I4, FR-063)

Si el comprador pide factura cuando el documento equivalente POS ya se emitió, la norma exige anularlo
con su nota de ajuste y emitir después la factura. Es una sola acción para el cajero:

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `POST /api/inventory/sales/documents/{id}/invoice-instead` | Sales.Confirm | `ReplacePosDocumentWithInvoiceCommand`: `{ buyerPersonPublicId, reason, expectedAmountDue }` → `{ adjustmentNotePublicId, invoicePublicId, messages[] }` |

- En **una** transacción confirma dos documentos nuevos. El primero es la `PosAdjustmentNote` de
  anulación total (`totalVoid = true`, sin devolución, concepto de corrección de anulación). El segundo
  es la `SalesInvoice`, con las mismas líneas, precios, impuestos, vendedor y bodega, a nombre de
  `buyerPersonPublicId`. La factura toma el número del rol `InvoiceOnRequest` de la caja del original.
- **Los pagos no se mueven.** La nota no reintegra y la factura no cobra: los pagos del original pasan
  a la factura (`INV_DocumentPayments` nuevos con los mismos medios y montos, y el original queda
  saldado por la nota). La caja no registra ni entrada ni salida. Un bono no se libera. Un medio de
  crédito se traslada como `AjusteDeVentaACredito` (§23.4).
- La factura se transmite **después** de la nota. Su `INV_DocumentLinks` es `ReplacementOf` hacia el
  original, y la nota es `NoteOf`. Si la nota es rechazada, la factura no sale: queda `Pending` con la
  alerta del rechazo, y se resuelve como cualquier rechazo (§24.5).
- Errores (422):
  - `Inventory.Sales.InvoiceInsteadNotApplicable`: el original no es un `PosEquivalentDocument` expedido,
    o ya tiene notas.
  - `ElectronicInvoicing.Document.AwaitingResponse`: el original está sin respuesta.
  - `Payments.TotalMismatch`: `expectedAmountDue` no coincide.
  - Los datos DIAN que le falten al comprador responden como en la confirmación de una factura.
- Emite `NotaCreditoEmitida` (anulación total, sin devolución) y `VentaConfirmada` de la factura. La
  mercancía no se mueve dos veces: la nota no devuelve (`ReturnsGoods = 0`), y una `SalesInvoice` con
  vínculo `ReplacementOf` hacia un `PosEquivalentDocument` no escribe kardex, porque la salida ya la
  hizo el original. Es la misma regla de la `SalesInvoiceFromShipments`, que factura lo ya
  despachado. Contabilidad recibe los asientos de ingreso e impuestos, que se cancelan y se rehacen,
  sin costo de venta nuevo.

### 18.4 Cotización, pedido, remisión y nota débito (I6)

Son las mismas cinco operaciones de borrador de §18.2 (`POST /`, `PUT /{id}`, `/{id}/confirm`,
`/{id}/void`, `/{id}/discard`), con los mismos permisos `Sales.*`.

| Prefijo | Clase | Diferencias |
|---|---|---|
| `/quotes` | `SalesQuote` (23) | sin efecto ni mensajes; lleva `validUntil`. `POST /quotes/{id}/to-order` crea el borrador del pedido, vinculado con `FromOrder` |
| `/orders` | `SalesOrder` (24) | al confirmar **reserva** (`INV_Reservations`) hasta `operationDate + Ventas.ReservaDiasVencimiento`, y la reserva se libera sola (`ProgramadorDeTareas`); anular libera. No emite mensajes (FR-075: la factura desde pedido sella su propio modo) |
| `/shipments` | `Shipment` (25) | descarga existencia y emite `CostoDeVentaReconocido` (tipo `SI` por mapeo); `originPublicIds` = pedidos, vinculados con `DispatchOf`. Si pasa `Ventas.RemisionDiasMaximosSinFacturar` sin facturarse, alerta `Inventario.RemisionSinFacturar` |
| `/debit-notes` | `DebitNote` (33) | sobre una factura; emite `NotaDebitoEmitida`. Si la venta original fue a crédito: consulta a Cartera en IC; en el crédito provisional, aprobación (§23) y `AjusteDeVentaACredito` con `AdjustmentClass = DebitNote` |

La factura desde remisiones usa `POST /invoices` con un tipo de clase `SalesInvoiceFromShipments` y
`originPublicIds`. No vuelve a descargar lo remisionado. Si las remisiones tienen destinos de mensaje
distintos → 422 `Inventory.PostingMode.ChainMismatch` (FR-075, «un derivado no reúne orígenes con
destinos distintos»).

### 18.5 Errores de ventas

Los canónicos de `decisiones-transversales.md` §2.17: `Inventory.Stock.Insufficient` (`data.available`),
`Inventory.Period.Closed`, `Inventory.Warehouse.NotActive`, `Inventory.Document.HasDependents`
(`data.dependents`), `Inventory.Document.AlreadyVoided`, `Inventory.Document.FiscalUseCorrection`,
`Inventory.Approval.AmountExceedsLimit`, `Inventory.Unit.DecimalsNotAllowed`,
`Inventory.Numbering.ResolutionUnavailable` (`data: { kind, prefix, environment, reasonCode:
ElectronicInvoicing.Resolution.Exhausted \| ElectronicInvoicing.Resolution.Expired \|
ElectronicInvoicing.Resolution.NotLinkedToChannel }`), `Inventory.Prevalidation.NotPostable` y los
de pagos (§22.5).

Los de esta sección: `Inventory.Document.TypeNotForRoute` (`data: { class, group }`), `.NotDraft`, `.TotalChanged`,
`Inventory.Sales.FiscalClassMismatch`, `.BelowCost`, `.PersonInactive`, `.InvoiceInsteadNotApplicable` (§18.3.1),
`Inventory.Prevalidation.NoResponse`, `Inventory.CreditNote.ClassMismatch`, `.ExceedsRemaining`,
`Payments.RefundMeansNotAllowed`.

## 19. Precios, descuentos y promociones — `/api/inventory`

Permisos: `Inventory.Prices.View`, `Inventory.Prices.Manage` e `Inventory.DiscountCaps.Manage`.
Aprobar un descuento sobre el tope usa el motor de aprobaciones con el permiso
`Inventory.Discounts.Authorize` como permiso de nivel.

### 19.1 Listas de precios — `/price-lists`

Una lista tiene un **ámbito** hecho de dimensiones opcionales: persona, segmento, canal y sucursal. El
ámbito se normaliza en `ScopeKey`. Dos listas con el mismo `ScopeKey` no pueden tener vigencias que se
crucen.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?asOf=&scope=&active=&search=` | Prices.View | paginado de `PriceListDto` `{ priceListPublicId, code, name, includesTaxes, scope: { personPublicId?, personName?, segment?, salesChannelPublicId?, branchPublicId? }, scopeKey, specificity, validFrom, validTo?, isActive, itemCount }` |
| `GET /{id}` | Prices.View | `PriceListDto` + `items` paginados `[{ itemPublicId, productPublicId, productCode, productName, unitPublicId, unitCode, price }]` (`?search=&page=`) |
| `POST /` | Prices.Manage | `{ code, name, includesTaxes, scope: {…}, validFrom, validTo?, reason }` → 201 `{ priceListPublicId }`. Errores: 422 `Inventory.PriceList.Overlaps` (`data: { priceListPublicId, code, validFrom, validTo }`) y `Catalogo.CodigoDuplicado`. Un segmento que no existe en `Associate.AssociateClass` → 422 `Inventory.PriceList.SegmentUnknown` (`data: { allowed[] }`, T51) |
| `PUT /{id}` | Prices.Manage | `{ name, validTo?, isActive, reason }`. El ámbito, el código e `includesTaxes` no cambian (otra lista con otra vigencia); si se intenta → 422 `Inventory.PriceList.ScopeLocked` |
| `PUT /{id}/items` | Prices.Manage | `{ items: [{ productPublicId, unitPublicId, price }], removeProductPublicIds?: [], reason }`: agrega o reemplaza por (producto, unidad). Un precio en uso se cambia creando otra lista o cerrando la vigencia; la línea de cada documento ya guardó su precio |
| `GET /template.xlsx[?withData=true]` | Prices.View | hojas y columnas: contracts/plantillas.md §12 (T49: la plantilla se publica en I1) |
| `POST /import?mode=review\|apply` | Prices.Manage | `ImportPriceListsCommand` (I3), con la mecánica de contracts/plantillas.md §0.5 → `ImportResultDto`; si `apply` falla, 422 `Import.Invalid` |

### 19.2 Resolver el precio — `GET /prices/resolve`

`GET /prices/resolve?product=&unit=&person=&salesChannel=&branch=&date=` · Prices.View ·
`ResolvePriceQuery`, sobre `ResolutorDeListaDePrecios` (puro).

Respuesta: `ResolvedPriceDto { price, includesTaxes, priceList: { publicId, code, name,
matchedDimensions: [Person, Segment, Channel, Branch] }, fallbackUsed, candidates: [{ priceListPublicId,
code, matchedDimensions[], hasProduct }] }`.

- **Qué lista gana**: entre las vigentes, la que coincide en más dimensiones. En empate: cliente, luego
  segmento, luego canal, luego sucursal; al final, la general.
- **Respaldo por producto**: si la lista ganadora no trae el producto, se toma de la siguiente lista
  aplicable y `fallbackUsed = true` (F8).
- **Sin precio en ninguna** → 422 `Inventory.Price.NotFound` (`data: { product, unit }`). También lo
  usa el retiro gravado cuando falta el precio en la lista general.

El POS y la factura resuelven el precio en el servidor al agregar la línea. Esta ruta sirve para la
consulta de precio y para la pantalla de listas.

### 19.3 Topes de descuento — `/discount-caps`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?role=&asOf=` | Prices.View | `[{ discountCapPublicId, rolePublicId, roleName, maxLinePercent, maxDocumentPercent, validFrom, validTo? }]` |
| `GET /mine?date=` | Prices.View | el tope efectivo del usuario: el mayor entre sus roles; un rol sin fila da 0 (T51). `{ maxLinePercent, maxDocumentPercent, fromRoles[] }` |
| `POST /` | DiscountCaps.Manage | `{ rolePublicId, maxLinePercent, maxDocumentPercent, validFrom, validTo?, reason }` → 201. Cierra la vigencia anterior la víspera. Si se cruza → 422 `Inventory.DiscountCap.Overlaps` |
| `GET /template.xlsx` · `POST /import?mode=review\|apply` | Prices.View (descarga) · DiscountCaps.Manage (importación) | `ImportDiscountCapsCommand` (plantilla en I1, importación en I3); columnas en contracts/plantillas.md §13 |

**Cómo se aplica.** El descuento se aplica en la línea (`PATCH /pos/drafts/{id}/lines/{lineId}` o el
borrador de factura). Cambiar a mano el precio de lista cuenta como descuento. Si el porcentaje supera
el tope del usuario, no se rechaza: la línea queda con el descuento
`approval.status = Pending` y se crea una `COR_ApprovalRequests` con `Subject = DiscountOverCap`,
`SourceType = DocumentLineDiscount` y `ContentSha256` de la línea (módulo `Inventory`).

- **Quién aprueba**: alguien con `Inventory.Discounts.Authorize`, con tope suficiente y alcance sobre el
  punto, y que no sea quien pidió la aprobación (T33). Aprueba en la bandeja de aprobaciones
  (§15), desde su sesión o en persona en el terminal, con su passkey o su TOTP de un solo uso (nunca
  con contraseña).
- **Si la línea cambia**: la huella deja de coincidir y la aprobación caduca.
- **Confirmar con una aprobación pendiente o rechazada** → 422 `Inventory.Discount.ApprovalPending`
  (`data: { lines[] }`).
- **Descuento por total**: se prorratea a las líneas, con el residuo de redondeo visible (FR-017), y se
  compara con `maxDocumentPercent`.

### 19.4 Promociones — `/promotions` (I6)

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?asOf=&active=` | Prices.View | `[PromotionDto { promotionPublicId, code, name, kind, validFrom, validTo?, cumulative, isActive, scopes: [{ productPublicId? , categoryPublicId?, segment?, salesChannelPublicId? }], tiers: [{ minQuantity?, buyQuantity?, payQuantity?, percent?, amount?, price? }] }]` |
| `GET /{id}` | Prices.View | `PromotionDto` |
| `POST /` | Prices.Manage | `PromotionDto` sin id, con `reason` → 201. `kind` es `PromotionKind`: `Percent` (1), `Amount` (2), `BuyNPayM` (3), `QuantityPrice` (4), `BundlePrice` (5) |
| `PUT /{id}` | Prices.Manage | sólo `name`, `validTo`, `isActive`; si ya se aplicó en un documento confirmado, lo demás no cambia (422 `Inventory.Promotion.InUse`) |

`MotorDePromociones` corre al precificar. Aplica la promoción como descuento **no condicionado** en la
línea (`source = Promotion`, `promotionPublicId`), nunca como línea a precio cero. Entre promociones
no acumulables gana la de mayor descuento, y un descuento manual no se suma a una promoción (F9).

### 19.5 Errores

`Inventory.PriceList.Overlaps`, `.SegmentUnknown`, `.ScopeLocked`, `Import.Invalid` (importación),
`Inventory.Price.NotFound`, `Inventory.DiscountCap.Overlaps`, `Inventory.Discount.ApprovalPending`,
`Inventory.Promotion.InUse`, `Catalogo.CodigoDuplicado`, `Approvals.SelfApprovalForbidden`.

## 20. Punto de venta — `/api/inventory/points-of-sale` y `/api/inventory/pos`

Permisos: `Inventory.PointsOfSale.{View, Manage}` para la configuración e `Inventory.Pos.Sell` para
vender. El alcance por punto (`INV_UserPointOfSaleScopes`) se asigna en la ruta de alcances
(§16). `Inventory.Scope.AllPointsOfSale` lo omite.

### 20.1 Puntos de venta y cajas

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /points-of-sale?branch=&active=` | PointsOfSale.View | `[PointOfSaleDto { pointOfSalePublicId, code, name, branchPublicId, salesChannelPublicId, posEnabled, defaultWarehousePublicId, isActive, cashRegisters: n }]` |
| `GET /points-of-sale/{id}` | PointsOfSale.View | `PointOfSaleDto` + `cashRegisters: [CashRegisterDto]` |
| `POST /points-of-sale` | PointsOfSale.Manage | `{ code, name, branchPublicId, salesChannelPublicId, posEnabled, defaultWarehousePublicId, reason? }` → 201. El código no cambia después (lo usa la matriz, T27) |
| `PUT /points-of-sale/{id}` | PointsOfSale.Manage | `{ name, salesChannelPublicId, posEnabled, defaultWarehousePublicId, isActive }`. Con una sesión abierta no se desactiva (422 `Inventory.PointOfSale.HasOpenSessions`) |
| `GET /points-of-sale/{id}/cash-registers` | PointsOfSale.View | `[CashRegisterDto { cashRegisterPublicId, code, name, warehousePublicId, defaultCardTerminalPublicId?, printFormat: Ticket80 \| Ticket58 \| Letter, isActive, documentTypes: [{ role, documentTypePublicId, documentTypeCode, class, prefix, resolution?: { publicId, prefix, status } }], openSession?: { cashSessionPublicId, cashierName, openedAt } }]` |
| `POST /points-of-sale/{id}/cash-registers` | PointsOfSale.Manage | `{ code, name, warehousePublicId, defaultCardTerminalPublicId?, printFormat, documentTypes: [{ role, documentTypePublicId }] }` → 201 |
| `PUT /points-of-sale/{id}/cash-registers/{registerId}` | PointsOfSale.Manage | lo mismo, sin `code` |
| `GET /points-of-sale/template.xlsx` · `POST /points-of-sale/import?mode=review\|apply` | PointsOfSale.View (descarga) · PointsOfSale.Manage (importación) | puntos y cajas con sus tipos por rol (`ImportPointsOfSaleCommand`; plantilla en I1, importación en I3); columnas en contracts/plantillas.md §10 |

**Tipos de documento de la caja.** Cada caja declara a lo sumo uno por `CashRegisterDocumentRole`
(índice único `(CashRegisterId, Role)`): `PosSale` (1), `InvoiceOnRequest` (2), `PosAdjustmentNote`
(3), `InvoiceCreditNote` (4), `PosSaleContingency` (5) e `InvoiceContingency` (6). Así una caja que
expide el documento equivalente POS y la factura a petición del comprador tiene la nota de cada uno y
la contingencia de cada uno (FR-058, FR-067). La clase de cada tipo tiene que ser compatible con su
rol; si no → 422 `Inventory.CashRegister.RoleClassMismatch`.

| Rol | Clases admitidas |
|---|---|
| `PosSale` | `PosEquivalentDocument` o `NonElectronicSalesReceipt` |
| `InvoiceOnRequest` | `SalesInvoice` |
| `PosAdjustmentNote` | `PosAdjustmentNote`, o `NonElectronicSalesNote` (las notas no electrónicas van en este rol) |
| `InvoiceCreditNote` | `CreditNote` |
| `PosSaleContingency` | `PosEquivalentDocument` con resolución de `ResolutionKind.Contingency` y `BacksUpKind = PosEquivalent` |
| `InvoiceContingency` | `SalesInvoice` con resolución de `ResolutionKind.Contingency` y `BacksUpKind = Invoice` |

En contingencia 03, la venta toma el tipo del rol de contingencia que respalda a su rol:
`PosSaleContingency` para `PosSale`, `InvoiceContingency` para `InvoiceOnRequest`. Sin ese tipo, la
guardia fiscal responde `Blocked` con `ElectronicInvoicing.Readiness.DocumentTypeMissing`.

La bodega de la caja tiene que ser de la sucursal del punto; si no → 422
`Inventory.CashRegister.WarehouseBranchMismatch`.

**Punto sin POS (FR-058).** Con `posEnabled = false` el punto conserva sus cajas y sesiones para el cobro
de oficina en efectivo, pero `POST /pos/drafts`, `GET /pos/lookup` y `POST /pos/drafts/{id}/resume` con una
sesión de ese punto responden 422 `Inventory.Pos.NotEnabled`. `CashSessionDto.pointOfSale` trae
`posEnabled` para que la pantalla oculte el enlace a `/pos`.

### 20.2 La venta en el POS — `/pos`

**Cómo se vende.** La venta del POS es un documento de venta en `Draft` que vive en el servidor desde
la primera lectura, ligado a la sesión y sin número (T50). Las lecturas del lector no son pasos. Los
pasos de FR-019 son tres:

1. **Cobrar** (F10): abre el panel de cobro. Puede llamar la validación previa (§25.1) para avisar
   antes, sin bloquear.
2. **Confirmar** (F10): `POST /pos/drafts/{id}/checkout`.
3. **Entregar**: la tirilla viene en la respuesta del cobro, o sale después por `deliver` (§20.3).

Cada lectura es **una** petición. Una lectura repetida del mismo producto suma cantidad. «3*» antes de
leer multiplica.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /pos/lookup?code=&cashSession=` | Pos.Sell | `LookupPosProductQuery`: búsqueda **exacta** por código de barras (el de empaque devuelve su unidad y factor) o por código de producto → `{ product: { publicId, code, name, status }, unit: { publicId, code }, factor, price, priceList, available }`. Sin coincidencia → 404 `Inventory.Product.NotFound`, y la pantalla abre la búsqueda (`GET /api/inventory/products/search`, parte 1) con el texto |
| `GET /pos/drafts?cashSession=&pointOfSale=&suspended=` | Pos.Sell | `[PosDraftSummaryDto { draftPublicId, cashSessionPublicId, cashierName, lines, total, suspended?: { label, suspendedAt, suspendedByName } }]`. Con `suspended=true` salen las ventas suspendidas **del punto**, que cualquier cajero con alcance puede recuperar |
| `POST /pos/drafts` | Pos.Sell | `{ cashSessionPublicId, firstLine?: { code, quantity? } }` → 201 `PosDraftDto` (`CreatePosDraftCommand`). La sesión debe estar abierta y ser del usuario; si no → 422 `Inventory.CashSession.NotOpen`. El tipo es el del rol `PosSale` de la caja |
| `GET /pos/drafts/{id}` | Pos.Sell | `PosDraftDto`. Después de confirmar responde `status: Confirmed` y el mismo `draftPublicId`, que es el `documentPublicId` de §18.1 |
| `PATCH /pos/drafts/{id}` | Pos.Sell | `UpdatePosDraftCommand`: `{ customerPersonPublicId?, clearCustomer?, salespersonPublicId?, role?: PosSale \| InvoiceOnRequest, documentDiscount?: { percent? \| amount? }, notes? }` → `PosDraftDto`. Cambiar el cliente vuelve a precificar. `role = InvoiceOnRequest` (factura porque el comprador la pide) cambia al tipo de ese rol y exige un cliente identificado (422 `Inventory.Pos.InvoiceRequiresCustomer`) |
| `POST /pos/drafts/{id}/lines` | Pos.Sell | `AddPosLineCommand`: `{ code? \| productPublicId?, unitPublicId?, quantity? (1 por defecto) }` → `PosDraftDto` con `lastLine`. Si el producto ya está con la misma unidad, precio y descuento, suma cantidad |
| `PATCH /pos/drafts/{id}/lines/{lineId}` | Pos.Sell | `UpdatePosLineCommand`: `{ quantity?, unitPrice?, discount?: { percent? \| amount? } }` → `PosDraftDto`. El precio manual y el descuento se auditan uno a uno. Sobre el tope, la aprobación de §19.3 |
| `DELETE /pos/drafts/{id}/lines/{lineId}` | Pos.Sell | `RemovePosLineCommand` → `PosDraftDto`. Queda auditado (FR-059) |
| `POST /pos/drafts/{id}/suspend` | Pos.Sell | `{ label }` → `IsSuspended = true`, con fecha y rótulo |
| `POST /pos/drafts/{id}/resume` | Pos.Sell | `{ cashSessionPublicId }`: la liga a la sesión abierta del usuario, en cualquier caja del mismo punto. Si cambió la fecha operativa, se vuelve a precificar y la respuesta avisa `Inventory.Pos.Repriced`. Queda auditado. Una venta que no está suspendida → 422 `Inventory.Pos.NotSuspended` |
| `POST /pos/drafts/{id}/discard` | Pos.Sell | `{ reason }` → `Discarded`, auditado |
| `POST /pos/drafts/{id}/checkout` | Pos.Sell | `CheckoutPosDraftCommand`: `{ payments: [DocumentPaymentInput], expectedAmountDue, sendEmailTo? }` → `CheckoutResultDto` (abajo) |

`PosDraftDto`:

- **Cabecera**: `{ draftPublicId, status, class, documentType: { publicId, code, role },
  cashSessionPublicId, pointOfSale, cashRegister, warehouse }`.
- **Cliente y vendedor**: `customer: { personPublicId, name, isFinalConsumer, segment? }` y
  `salesperson?`.
- **Líneas**: `lines: [PosLineDto]` y `lastLine?`.
  `PosLineDto = { linePublicId, lineNumber, product, unit, factor, quantity, quantityBase,
  roundingQuantity, listPrice, unitPrice, includesTaxes, priceList, discounts[], taxes[], total,
  available, belowCost }`.
- **Totales**: `documentDiscount?` y `totals: { subtotal, discountTotal, taxTotal, withholdingTotal,
  total, amountDue }`.
- **Medios disponibles**: `availablePaymentMeans: [{ paymentMeansPublicId, code, name, class,
  quickKey?, requiresReference, referenceKind?, allowsChange, allowsPartial, countMethod,
  cardTerminals: [{ publicId, code }], defaultCardTerminalPublicId?, creditDefaults? }]`. Salen de
  `DisponibilidadDeMedio` para el punto, el canal, el tipo y el cliente: sin cliente identificado no
  aparecen los de crédito, y sin `Inventory.Sales.SellOnCredit` tampoco.
- **Estado**: `suspended?`, `pendingApprovals: [{ approvalRequestPublicId, subject, sourceType, lineNumber?,
  status }]` y `warnings: [{ code, message }]`. Un aviso típico es `Inventory.Sales.BelowCost` con
  `Ventas.BajoCosto = Alertar`.

`CheckoutResultDto`:

```
{ documentPublicId, status: Confirmed | PendingApproval, approvalRequestPublicId?,
  class, prefix?, number?, total, amountDue, change, postingMode?,
  electronic?: { electronicDocumentPublicId, kind, status, uniqueCode?, qrContent?,
                 contingencyType?, waitedMs, deliverable, pendingDelivery,
                 messages?: [{ rule, kind, text, translation }] },
  ticket?: TicketDto,
  warnings: [{ code, message, data? }] }
```

Qué hace el cobro:

- **Orden**: registra los pagos (`INV_DocumentPayments`) y confirma por el flujo canónico. En orden: la
  aprobación, la guardia fiscal, la validación previa fuera del cerrojo, el cerrojo, el kardex, el
  número, los mensajes y el `COR_ElectronicDocuments` en `Pending`, todo en un `SaveChanges`.
- **Espera de la DIAN**: después del commit intenta la emisión en línea (`EmitElectronicDocumentCommand`)
  y espera hasta `Dian.EsperaMaximaPosSegundos` (15 por defecto, por punto).
- **Si se valida** (`Validated` o `ValidatedWithNotices`): `ticket` viene listo y la entrega queda
  registrada.
- **Si se cumple la espera**: la venta sigue confirmada, `pendingDelivery = true`, no hay `ticket` y la
  caja queda libre. El documento sale en la lista de pendientes de entrega (§20.3). Esa venta **no** se
  renumera: se entrega al validarse.
- **La espera cumplida cuenta como falla del canal**: cada venta del POS cuya espera se cumple suma una
  falla en `CircuitoDeCanal`, igual que un error del canal. Al llegar a `Dian.UmbralFallasCircuito`, el
  circuito abre la contingencia 03 (`detectedBy = Process`, §24.6) y las ventas **nuevas** se numeran
  con el tipo de contingencia y reciben la representación de papel en la misma respuesta del cobro. Así
  el comprador siempre recibe, a tiempo, el documento validado o el de contingencia (SC-004).
- **En contingencia**: 03 numera con el tipo del rol de contingencia que respalda al rol de la venta
  (`PosSaleContingency` o `InvoiceContingency`, §20.1) e imprime la representación de papel con su
  leyenda; 04 entrega sin validar, con la leyenda «pendiente de validación de la DIAN». En los
  dos casos `deliverable = true` y hay `ticket` (FR-067).
- **Si la DIAN rechaza**: la venta sigue confirmada, `electronic.status = Rejected` con los motivos
  traducidos y sin `ticket`. La corrige quien tenga `ElectronicInvoicing.Documents.Correct` (§24.5).
- **Comprobante no electrónico**: `electronic` es nulo y `ticket` viene siempre.
- **Con aprobación pendiente**: el crédito provisional (§23) o un nivel de la política del tipo dejan
  `status = PendingApproval`, sin número ni emisión. El POS consulta `GET /pos/drafts/{id}` cada 2 s
  hasta `Confirmed` (o `Draft` si se rechaza, con el motivo) y entonces pide la entrega (§20.3). El
  aprobador en persona decide en el terminal por la bandeja de aprobaciones (§15).
- **Reintentos**: un cobro repetido con la misma `Idempotency-Key` (se perdió la conexión) devuelve el
  mismo resultado, nunca una segunda venta (SC-002).

`TicketDto`, el modelo de la tirilla que pinta `TirillaDeVenta` y se imprime por
`IImpresionDeDocumentos`:

```
{ format: Ticket80 | Ticket58, copy: bool,
  header: { companyName, nit, branchName, address, resolutionText?, regimeText },
  document: { classLabel, prefix, number, issuedAt, cashRegisterCode, cashierName, salespersonName? },
  party: { name, idType, idNumber },
  lines: [{ code, description, quantity, unitCode, unitPrice, discount, total, taxMark }],
  taxes: [{ label, base, amount }], withholdings: [{ label, amount }],
  totals: { subtotal, discountTotal, taxTotal, total, amountDue },
  payments: [{ meansName, amount, reference?, last4? }], change,
  electronic?: { uniqueCodeKind, uniqueCode, qrContent, legend? },
  footer: [] }
```

`party` sale de la copia fiscal (T52). `legend` es el texto de contingencia o «SIN VALIDEZ FISCAL» en
el ambiente de pruebas.

### 20.3 Entrega y reimpresión

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /api/inventory/sales/documents?pendingDelivery=true&cashSession=` | Sales.View | las ventas de la caja cuya entrega espera la validación de la DIAN |
| `POST /api/inventory/sales/documents/{id}/deliver` | Sales.View | `{ format: Ticket80 \| Ticket58 \| Letter, sendEmail?, email? }`: **primera** entrega, sin la marca «COPIA», auditada como `Inventory.Document.Delivered`. Ticket → `TicketDto`. `Letter` de un documento electrónico → `{ direct, url, expiresAt }`, el enlace firmado de 60 s (feature 011) a la representación gráfica guardada. `Letter` de un comprobante no electrónico → `application/pdf` generado al vuelo (`SalesDocumentReport`). `sendEmail` manda por `IEmailSender` el PDF y el AttachedDocument al correo de la copia fiscal, o a `email`. Si todavía no es entregable → 422 `ElectronicInvoicing.Document.NotDeliverable` (`data: { status }`). Si ya se entregó en ese formato → 422 `Inventory.Document.AlreadyDelivered` (se usa la reimpresión) |
| `POST /api/inventory/documents/{id}/reprint` | Documents.Reprint | `ReprintDocumentCommand`: `{ format, reason? }` → lo mismo que `deliver`, con `copy = true` y la marca «COPIA». Usa la copia fiscal vigente o el archivo firmado (FR-011). Queda auditado. Sirve para cualquier grupo; las ventas usan `format` de tirilla o carta |

El correo al comprador sale **solo** después de la validación cuando `Dian.EntregaCorreo = Erp` y la
copia fiscal trae correo. `sendEmail` sirve para reenviarlo.

### 20.4 Errores del POS

`Inventory.CashSession.NotOpen`, `Inventory.Product.NotFound`, `Inventory.Pos.NotEnabled` (§20.1, punto sin POS), `Inventory.Pos.InvoiceRequiresCustomer`,
`.NotSuspended`, `Inventory.Document.TotalChanged` (`expectedAmountDue` ≠ el calculado),
`Inventory.Discount.ApprovalPending`, `Inventory.Sales.BelowCost`, `Inventory.Stock.Insufficient`,
`Inventory.Prevalidation.NotPostable` y `.NoResponse`, `ElectronicInvoicing.NotReady`,
`Inventory.Numbering.ResolutionUnavailable`, `Inventory.PointOfSale.HasOpenSessions`,
`Inventory.CashRegister.RoleClassMismatch`, `.WarehouseBranchMismatch`,
`ElectronicInvoicing.Document.NotDeliverable`, `Inventory.Document.AlreadyDelivered` y los de pagos
(§22.5). El aviso `Inventory.Pos.Repriced` no es un error. Una resolución agotada o vencida a mitad del
turno detiene la venta con `ResolutionUnavailable` y la alerta `Dian.ResolucionPorAgotar` o
`Dian.ResolucionPorVencer` para quien las administra. Las notas no se afectan.

## 21. Caja — sesiones, movimientos, arqueo y cierre del día — `/api/inventory`

Permisos: `Inventory.CashSessions.{View, ViewAll, Open, Close}`, `Inventory.CashMovements.{Create,
Approve}`, `Inventory.CashDifferences.Approve` e `Inventory.DayClose.{Execute, Reopen}`. `ViewAll`
permite ver y cerrar las sesiones de otros cajeros del punto. Sin él, sólo las propias; las ajenas
responden 404.

### 21.1 Sesiones — `/cash-sessions`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /cash-sessions?mine=&pointOfSale=&cashRegister=&status=&from=&to=&cashier=` | CashSessions.View | paginado de `CashSessionDto { cashSessionPublicId, pointOfSale, cashRegister, cashier: { userPublicId, name, personPublicId? }, operatingDate, openedAt, openingBase, baseMode, status: Open (1) \| Closed (2), closedAt?, closedByName?, salesCount, salesTotal, differenceDocument?: { documentPublicId, status } }`. `mine=true&status=Open` es la sesión abierta del usuario, con la que arranca el POS |
| `GET /cash-sessions/{id}` | CashSessions.View | `CashSessionDto` + `movements[]`, `suspendedDrafts`, `openDrafts`, `pendingDeliveries`, `count?` (§21.2) |
| `POST /cash-sessions` | CashSessions.Open | `OpenCashSessionCommand`: `{ cashRegisterPublicId, openingBase?, denominations?: [{ cashDenominationPublicId, quantity }] }` → 201 `CashSessionDto` + `baseIncomeDocument?` + `warnings[]` |
| `GET /cash-sessions/{id}/expected` | CashSessions.View | `GetCashSessionExpectedQuery` → `CashSessionExpectedDto` (§21.2) |
| `POST /cash-sessions/{id}/close` | CashSessions.Close | `CloseCashSessionCommand`, §21.2 |
| `POST /cash-sessions/{id}/recount` | CashSessions.Close | `RecountCashSessionCommand`: recontar cuando rechazaron la diferencia, §21.2 |
| `GET /cash-sessions/{id}/count-report` | CashSessions.View | `application/pdf`: `CashCountReport`, el arqueo por medio con denominaciones, lotes y firmas del cajero y del supervisor |

Al abrir:

- **Una sesión por caja y por cajero**: índices únicos filtrados a `Status = Open` (T50). Con
  `Caja.UnaSesionPorCajero = true`, un cajero no abre otra en toda la cooperativa. Si la caja ya tiene
  una → 422 `Inventory.CashSession.RegisterBusy` (`data: { cashierName, openedAt }`); si el cajero ya
  tiene una → 422 `Inventory.CashSession.CashierBusy` (`data: { cashRegisterCode }`). La colisión del
  índice bajo concurrencia se traduce al mismo error.
- **Base**: con `Caja.BaseModo = FondoFijo`, la sesión hereda el fondo fijo de la caja y no emite
  mensaje; `openingBase` es sólo informativo, y si difiere del fondo sale el aviso
  `Inventory.CashSession.BaseDiffers`. Con `BaseDelDia`, `openingBase` es obligatorio y la apertura crea
  un movimiento `BaseIncome` (§21.3) que sigue su política; cuenta en el esperado cuando se confirma.
- **Faltante a cargo del cajero**: con `Caja.TratamientoFaltante = CargoAlCajero`, un usuario sin
  `SEC_Users.PersonId` no abre sesión → 422 `Inventory.CashSession.CashierWithoutPerson`. El mensaje
  dice cómo vincular la persona.
- **Día cerrado**: sobre un día cuyo cierre ya se hizo en el punto → 422
  `Inventory.CashSession.DayClosed`.
- **Aviso fiscal temprano**: la apertura consulta `GuardiaDeEmisionFiscal`. Si responde `Blocked`, la
  sesión **abre** con el aviso `ElectronicInvoicing.NotReady` (`data.missing[]`) y la venta fiscal se
  detendrá al cobrar.

### 21.2 Esperado, arqueo por medio de pago y cierre de la sesión

`CashSessionExpectedDto`:

```
{ blind: bool,
  lines: [{ paymentMeans: { publicId, code, name, class, countMethod },
            openingBase, sales, refunds, movementsIn, movementsOut,
            reclassificationsIn, reclassificationsOut, expected, tolerance, paymentsCount,
            terminals?: [{ cardTerminalPublicId, code, acquirerCode, expected, paymentsCount }],
            references?: [{ documentPaymentPublicId, documentNumber, reference, amount }] }],
  total }
```

El esperado lo calcula `CalculadoraDeEsperado` (pura): base (sólo efectivo) + ventas − devoluciones ±
movimientos confirmados ± reclasificaciones. Con `Caja.ArqueoCiego = true` y sin `ViewAll`,
`blind = true` y las cifras de `expected` salen nulas: el cajero cuenta sin verlas.

**Cerrar la sesión.** Cuerpo:

```
{ counts: [{ paymentMeansPublicId,
             countedTotal?,
             denominations?: [{ cashDenominationPublicId, quantity }],
             terminalBatches?: [{ cardTerminalPublicId, batchNumber, total, count }],
             referenceChecks?: [{ documentPaymentPublicId, checked }],
             reason? }],
  closingWithdrawal?: { destination: Safe | Deposit } }
```

Cómo se cuenta cada medio depende de su `CashCountMethod`. Un dato que no corresponde al método → 422
`Inventory.CashSession.CountMethodMismatch` (`data: { paymentMeansCode, countMethod }`).

| Método | Qué se captura |
|---|---|
| `PhysicalCount` (1) | denominaciones o un total |
| `VoucherTotal` (2) | un lote por datáfono; el lote queda asociado a los pagos de ese datáfono en la sesión, sin editarlos (FR-101) |
| `ByReference` (3) | la marca de cada referencia |
| `None` (4) | nada: los créditos cuentan lo esperado |

Respuesta:

```
{ cashSessionPublicId, status: Closed,
  lines: [{ paymentMeansCode, expected, counted, difference, tolerance, withinTolerance, treatment? }],
  differenceDocument?: { documentPublicId, number?, status, approvalRequestPublicId? },
  closingWithdrawalDocument?: { documentPublicId, number?, status },
  batch?: { batchPublicId, number, trigger: CashSessionClose } }
```

Reglas del cierre (T50):

- **Inmediato**: la sesión pasa a `Closed` y la caja puede abrir otra.
- **Ventas en curso**: no se cierra con ventas suspendidas o borradores abiertos → 422
  `Inventory.CashSession.HasOpenDrafts` (`data: { drafts: [{ draftPublicId, label?, total }] }`).
- **Movimientos en curso**: tampoco con movimientos de caja de la sesión en `Draft` o
  `PendingApproval` → 422 `Inventory.CashSession.HasPendingMovements` (`data: { movements: [{
  documentPublicId, kind, amount, status }] }`): se confirman, se descartan o se anulan antes.
- **Motivo**: toda diferencia distinta de cero exige `reason` en su medio; si falta → 422
  `Inventory.CashSession.ReasonRequired` (`data: { paymentMeansCodes[] }`).
- **Documento de diferencia**: con alguna diferencia, el cierre crea un documento `CashCountDifference`
  (22) con una línea por medio (`INV_CashDocumentLines`: signo, valor, tratamiento, cajero).
  - `treatment` sale del parámetro: `Surplus` (1), o para el faltante `ShortageToCashier` (2) o
    `ShortageToExpense` (3), según `Caja.TratamientoFaltante`.
  - Dentro de la tolerancia del medio, se confirma con el motivo y sin aprobación.
  - Por encima, pasa a `PendingApproval` según la política del tipo. El nivel exige
    `Inventory.CashDifferences.Approve` y **nunca** es el cajero.
  - Al confirmarse emite `DiferenciaDeArqueoAprobada`, también cuando la diferencia está dentro de la
    tolerancia.
- **Rechazo**: devuelve el documento a `Draft` y se recuenta con `POST /cash-sessions/{id}/recount`, con
  el mismo cuerpo `counts`. El recuento reemplaza las líneas del borrador, vuelve a pedir aprobación y
  queda auditado con antes y después. Recontar una sesión sin diferencia rechazada → 422
  `Inventory.CashSession.NothingToRecount`.
- **Retiro de cierre**: `closingWithdrawal` genera el movimiento `WithdrawalToSafe` o
  `WithdrawalForDeposit` por lo contado en efectivo menos el fondo fijo.
- **Lote**: si algún tipo de la sesión pasa a contabilidad por lotes con disparador
  `CierreDeTurno`, el cierre crea la orden `COR_IntegrationBatches` (`Trigger = CashSessionClose`) en
  el mismo `SaveChanges` (T12).

### 21.3 Movimientos de caja — `/cash-movements`

Son documentos de la clase `CashMovement` (21), del grupo `Cash`, con su tipo, su consecutivo y su
política de aprobación (FR-100). Los datos propios van en el satélite `INV_CashMovementDetails`.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /cash-movements?cashSession=&pointOfSale=&kind=&status=&from=&to=` | CashSessions.View | paginado de `CashMovementDto { documentPublicId, number?, status, cashSessionPublicId, kind, sourcePaymentMeansCode, targetPaymentMeansCode?, destination?, destinationCashRegisterCode?, amount, reason, createdByName, approvedByName? }` |
| `POST /cash-movements` | CashMovements.Create | `{ documentTypePublicId?, cashSessionPublicId, kind, sourcePaymentMeansPublicId, targetPaymentMeansPublicId?, destination?, destinationCashRegisterPublicId?, amount, denominations?: [{ cashDenominationPublicId, quantity }], reason }` → 201 `CashMovementDto` en `Draft` |
| `PUT /cash-movements/{id}` | CashMovements.Create | el mismo cuerpo, sólo en `Draft` |
| `POST /cash-movements/{id}/confirm` | CashMovements.Create | `ConfirmInventoryDocumentCommand(ExpectedGroup = Cash)` → `{ documentPublicId, number?, status: Confirmed \| PendingApproval, approvalRequestPublicId? }`. Los niveles exigen `Inventory.CashMovements.Approve` y excluyen a quien lo creó. Al confirmarse emite `MovimientoDeCajaRegistrado` |
| `POST /cash-movements/{id}/void` | CashMovements.Approve | `{ reason }`, sólo con la sesión todavía abierta; el documento contrario emite `DocumentoAnulado`. Con la sesión cerrada → 422 `Inventory.CashMovement.SessionClosed` |
| `POST /cash-movements/{id}/discard` | CashMovements.Create | `{ reason? }` |
| `GET /cash-movements/{id}/receipt` | CashSessions.View | `application/pdf`: `CashMovementReceiptReport`, con firmas de quien entrega y quien recibe |

`kind` es `CashMovementKind`, y cada tipo tiene sus campos:

| `kind` | Campos que exige |
|---|---|
| `WithdrawalToSafe` (1) | `destination = Safe` |
| `WithdrawalToRegister` (2) | `destination = Register` y `destinationCashRegisterPublicId`. Es **un** documento: resta en la sesión de origen y suma en la sesión abierta de la caja destino; si no hay → 422 `Inventory.CashMovement.DestinationRegisterClosed` |
| `WithdrawalForDeposit` (3) | `destination = Deposit` |
| `BaseIncome` (4) | ingresa efectivo a la sesión desde la caja fuerte |
| `ReclassificationBetweenMeans` (5) | `targetPaymentMeansPublicId`, distinto del origen. Corrige un pago registrado con el medio equivocado sin tocar la venta confirmada |

Si el retiro supera lo esperado del medio → 422 `Inventory.CashMovement.ExceedsExpected`
(`data: { expected }`). Sólo lo confirmado cuenta en el esperado.

### 21.4 Cierre del día — `/day-closes`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /day-closes?pointOfSale=&from=&to=` | CashSessions.View | `[DayCloseDto { dayClosePublicId, pointOfSale, operatingDate, closedAt, closedByName, reopenedAt?, reopenReason?, total }]` |
| `GET /day-closes/{id}` | CashSessions.View | `DayCloseDto` + `lines: [{ paymentMeansCode, expected, counted, difference }]`, `cards: [{ acquirerCode, cardTerminalCode, batchNumber?, total, count }]` y `sessions[]` |
| `POST /day-closes` | DayClose.Execute | `ExecuteDayCloseCommand`: `{ pointOfSalePublicId, operatingDate }` → 201 `DayCloseDto`. Todas las sesiones del punto con esa fecha operativa deben estar cerradas; si no → 422 `Inventory.DayClose.SessionsOpen` (`data.sessions[]`). Si ya existe → 422 `Inventory.DayClose.AlreadyClosed`. No emite mensajes |
| `POST /day-closes/{id}/reopen` | DayClose.Reopen | `ReopenDayCloseCommand`: `{ reason }`. Queda auditado. Deja volver a abrir sesiones con esa fecha |

La consolidación antes de cerrar es la vista `day-close` de §27 con `pointOfSale` y `operatingDate`.

### 21.5 Errores de caja

`Inventory.CashSession.NotOpen`, `.RegisterBusy`, `.CashierBusy`, `.CashierWithoutPerson`,
`.DayClosed`, `.HasOpenDrafts`, `.HasPendingMovements`, `.ReasonRequired`, `.CountMethodMismatch`,
`.NothingToRecount`;
`Inventory.CashMovement.SessionClosed`, `.DestinationRegisterClosed`, `.ExceedsExpected`;
`Inventory.DayClose.SessionsOpen`, `.AlreadyClosed`; `Approvals.SelfApprovalForbidden`. El aviso
`Inventory.CashSession.BaseDiffers` no es un error.

## 22. Medios de pago — `/api/core` y disponibilidad en `/api/inventory`

El catálogo es de Core (T25), con permisos `Core.PaymentMeans.View` y `Core.PaymentMeans.Manage`. Dónde
se ofrece cada medio es del módulo comercial. Los códigos no cambian una vez creados, porque la matriz
contable los usa (T27). Un medio con pagos no se borra: se inactiva.

### 22.1 Medios — `/api/core/payment-means`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?class=&active=&asOf=` | Core.PaymentMeans.View | `[PaymentMeansDto]` |
| `GET /{id}` | Core.PaymentMeans.View | `PaymentMeansDto` + `paymentsCount` + `availability` (§22.3) |
| `POST /` | Core.PaymentMeans.Manage | `PaymentMeansInput` → 201 `{ paymentMeansPublicId }` |
| `PUT /{id}` | Core.PaymentMeans.Manage | `PaymentMeansInput` sin `code` ni `class`, con `reason` **obligatorio** (`IConMotivo`; sin él, 400 `Validation.Invalid`), porque la tolerancia y la comisión esperada son parámetros del medio (FR-012, FR-096). Con pagos, `class`, la red, el adquirente y `countMethod` no cambian (422 `Core.PaymentMeans.InUse`, `data: { payments }`): se inactiva y se crea otro. `isActive = false` con `validTo` lo retira hacia adelante |
| `DELETE /{id}` | Core.PaymentMeans.Manage | retiro suave de un medio **sin** pagos. Con pagos → 422 `Core.PaymentMeans.InUse` |
| `GET /template.xlsx` · `POST /import?mode=review\|apply` | Core.PaymentMeans.View (descarga) · Core.PaymentMeans.Manage (importación) | `ImportPaymentMeansCommand` (plantilla en I1, importación en I3); hojas y columnas en contracts/plantillas.md §11. Si `apply` falla, 422 `Import.Invalid` |

`PaymentMeansInput` / `PaymentMeansDto`:

- **Identificación**: `{ code, name, displayOrder, quickKey?, class }`. `class` es `PaymentMeansClass`:
  `Cash` (1), `CreditCard` (2), `DebitCard` (3), `AssociateCredit` (4), `CustomerCredit` (5),
  `BankDeposit` (6), `Transfer` (7), `Voucher` (8), `Check` (9), `Other` (99).
- **Tarjetas**: `cardNetworkPublicId?` y `cardAcquirerPublicId?`, que se exigen en las clases de
  tarjeta.
- **Consignación y transferencia**: `bankPublicId?`, `destinationAccountNumber?` y
  `destinationAccountType?`, como **dato**, no como cuenta contable.
- **Captura**: `requiresReference`, `referenceKind?` (`PaymentReferenceKind`), `referenceMinLength?`,
  `referenceMaxLength?`, `allowsChange` (sólo `Cash`), `allowsPartial` y `uniqueReference` (verdadero
  por defecto en `Voucher`).
- **Arqueo**: `countMethod` (`CashCountMethod`; si no viene, sale de la clase) y `toleranceAmount`.
  La tolerancia se **copia** a cada línea del arqueo (`INV_CashCountLines`) al cerrar la sesión, así
  que un cambio sólo afecta a las sesiones que se cierran después; el historial queda en la auditoría
  del `PUT`, con su motivo.
- **Datos informativos**: `expectedCommissionRate?` (fracción, 0,025 = 2,5 %) y `expectedCommissionFixed?` (FR-101), que se
  **copian** a cada pago (`INV_DocumentPayments.ExpectedCommissionAmount`) al confirmar, y `dianPaymentMeansCode` (dato
  validado contra `CatalogoDian`).
- **Crédito**: `creditDefaults?: { maxInstallments, termDays, periodicityDays, suggestedLineCode? }`,
  sólo en las clases de crédito. Plazo, cuotas y línea sugerida son datos del medio, sin llave a
  `LND_*` (T32).
- **Disponibilidad y vigencia**: `offeredAtAllPoints`, `offeredOnAllChannels`,
  `offeredForAllDocumentTypes`, `isActive`, `validFrom` y `validTo?`.

Las combinaciones que no admite se validan: vueltas en un medio que no es efectivo, un crédito con
arqueo físico, una tarjeta sin red o un efectivo sin arqueo → 422 `Core.PaymentMeans.Invalid`, con
`data: { field, rule }`, en el alta y la edición una a una. En la importación la misma regla sale como
un error de fila dentro de `Import.Invalid` (contracts/plantillas.md §0.5). Un código repetido → `Catalogo.CodigoDuplicado`.

### 22.2 Franquicias, adquirentes, datáfonos de cobro y denominaciones

Todas con `GET` en `Core.PaymentMeans.View` y escritura en `Core.PaymentMeans.Manage`.

| Prefijo | Cuerpo | Notas |
|---|---|---|
| `/api/core/card-networks` | `{ code, name, cardKind: Credit (1) \| Debit (2) \| Both (3), isActive }` | Visa, Mastercard, débito de cada red… |
| `/api/core/card-acquirers` | `{ code, name, personPublicId?, isActive }` | `personPublicId` es el tercero de la cuenta por cobrar a la red, como `Bank.PersonId` |
| `/api/core/card-terminals` | `{ code, cardAcquirerPublicId, serial?, isActive }` | datáfono **de cobro**; no es `DEB_PosTerminals`. El código es el TER del voucher. Se propone al cobrar desde `defaultCardTerminalPublicId` de la caja (§20.1) |
| `/api/core/cash-denominations` | `{ kind, value, validFrom, validTo? }` | sembradas por `CashDenominationsSeeder`; `kind` es `CashDenominationKind`: `Bill` (1) · `Coin` (2). Se agregan billetes nuevos y se cierra la vigencia de los retirados |

Cada una tiene `GET /`, `GET /{id}`, `POST /`, `PUT /{id}` y `DELETE /{id}`, este último sólo sin uso;
si está en uso → 422 `Core.CardNetwork.InUse`, `Core.CardAcquirer.InUse` o `Core.CardTerminal.InUse`.

### 22.3 Dónde se ofrece cada medio — `/api/inventory/payment-means/{id}/availability`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET` | PointsOfSale.View | `{ offeredAtAllPoints, pointOfSalePublicIds[], offeredOnAllChannels, salesChannelPublicIds[], offeredForAllDocumentTypes, documentTypePublicIds[] }` |
| `PUT` | PointsOfSale.Manage | `SetPaymentMeansAvailabilityCommand`: los tres conjuntos (`INV_PaymentMeansPointsOfSale`, `…Channels`, `…DocumentTypes`) |

Las marcas «todos» viven en el medio de Core y se escriben en §22.1. Un conjunto explícito junto con
«todos» en la misma dimensión → 422 `Inventory.PaymentMeans.AvailabilityConflict`. La pantalla
`/maestros/medios-de-pago` guarda las dos cosas. La regla que decide si un medio se ofrece es
`DisponibilidadDeMedio`, pura, y la usan el POS, la factura y el servidor al cobrar.

### 22.4 El pago dentro de un documento

Esta forma la comparten la factura (§18.2), la nota (§18.3) y el cobro del POS (§20.2).

`DocumentPaymentInput`:

```
{ paymentMeansPublicId, amount, tendered?, reference?, authorizationCode?,
  cardTerminalPublicId?, batchNumber?, last4?, cashSessionPublicId?,
  credit?: { installments, termDays, periodicityDays, firstDueDate?, suggestedLineCode? } }
```

`DocumentPaymentDto` es lo mismo más:

```
{ documentPaymentPublicId, lineNumber, direction: Received (1) | Refunded (2), change,
  meansCode, meansClass, networkCode?, acquirerCode?, bankPublicId?,
  pendingValidation, creditOrigin?: ProvisionalCredit | LendingNoResponse | Validated,
  voucherRedemptionStatus?: Active | Released }
```

Reglas (`ValidadorDePagos`, puro, más el servidor al confirmar):

- **Suma exacta**: lo aplicado suma exactamente `amountDue`, en decimal. Si no → 422
  `Payments.TotalMismatch` (`data: { amountDue, paid, missing, excess }`).
- **Vueltas**: sólo en medios con `allowsChange`, y `tendered − amount = change`. Si no → 422
  `Payments.ChangeNotAllowed`.
- **Referencia**: obligatoria según el medio → 422 `Payments.ReferenceRequired` (`data: {
  paymentMeansCode, referenceKind }`); fuera de la longitud del medio → `Payments.ReferenceInvalid`. El
  mismo medio puede repetirse con referencias distintas; con la misma → `Payments.DuplicateReference`.
- **Tarjeta**: `last4` son exactamente 4 dígitos. Una referencia o un campo con forma de número de
  tarjeta completo se rechaza con 422 `Payments.CardNumberNotAllowed`
  (`LosPagosNoGuardanElNumeroDeTarjeta`). El lote es opcional al cobrar y obligatorio en el arqueo si el
  medio lo exige.
- **Bono de número único**: el número se normaliza y se inserta en `INV_VoucherRedemptions` en la misma
  transacción. Si choca, bajo concurrencia también, → 422 `Payments.VoucherAlreadyUsed`, con `data: {
  documentPublicId, documentClass, prefix, number }` de la venta que lo usó.
- **Disponibilidad**: un medio que no se ofrece en ese punto, canal, tipo o cliente → 422
  `Payments.MeansNotAvailable` (`data: { paymentMeansCode }`). También cuando es un crédito y el usuario
  no tiene `Inventory.Sales.SellOnCredit`.
- **Pago parcial**: un medio sin `allowsPartial` sólo puede cubrir todo lo que falta → 422
  `Payments.PartialNotAllowed`.
- **Sesión**: todo medio que se arquea (`countMethod ≠ None`) exige `cashSessionPublicId` de una sesión
  abierta del usuario → 422 `Payments.CashSessionRequired`. En el POS lo pone el servidor.
- **Copias**: el pago guarda copias inmutables del código, la clase, la red, el adquirente y el banco
  del medio.
- **Forma de pago DIAN**: si hay un pago de clase crédito, la venta es a crédito y lleva vencimiento.

### 22.5 Errores

`Core.PaymentMeans.Invalid`, `.InUse`, `Core.CardNetwork.InUse`, `Core.CardAcquirer.InUse`,
`Core.CardTerminal.InUse`, `Inventory.PaymentMeans.AvailabilityConflict`, `Catalogo.CodigoDuplicado`;
`Payments.TotalMismatch`, `.ChangeNotAllowed`, `.ReferenceRequired`, `.ReferenceInvalid`,
`.DuplicateReference`, `.CardNumberNotAllowed`, `.VoucherAlreadyUsed`, `.MeansNotAvailable`,
`.PartialNotAllowed`, `.CashSessionRequired`, `.RefundMeansNotAllowed`.

## 23. Crédito provisional y consultas a Cartera

**Mientras IC esté pendiente rige el crédito provisional** (F2, T32). No hay consulta de cupo, y la
comunicación con Cartera espera la especificación de Cartera (D-02). Lo marcado **(IC)** existe en el
contrato, pero sólo funciona cuando `Cartera.IntegracionHabilitadaDesde` tenga fecha **y** exista un
destino `Lending` registrado. Hasta entonces `IConsultasDeCartera` resuelve a `CarteraNoHabilitada`.

### 23.1 Evaluar una venta a crédito — `POST /api/inventory/sales/credit-evaluations`

Permiso `Sales.SellOnCredit` · `EvaluateSaleCreditQuery`, una consulta sin clave de operación.

La pantalla la llama al elegir un medio de crédito, y el servidor la repite al confirmar.

Cuerpo:

```
{ personPublicId, paymentMeansPublicId, amount, operationDate?,
  pointOfSalePublicId?, documentTypePublicId? }
```

Respuesta:

```
{ lendingEnabled: bool,
  origin: ProvisionalCredit | Validated | LendingNoResponse,
  person: { publicId, name, isAssociate, associateActive?, isCustomer, eligible,
            reasons: [{ code, message }] },
  requiresApproval: bool,
  approval?: { levels: [{ order, permissionCode, threshold }], approverMaxAmount? },
  creditDefaults: { maxInstallments, termDays, periodicityDays, suggestedLineCode? },
  lending?: { status, availableQuota, lines: [{ code, name, maxTermMonths, maxInstallments }], evidenceId } }
```

En el crédito provisional:

- `lendingEnabled` es `false`, `origin` es `ProvisionalCredit`, `requiresApproval` es siempre `true` y
  `lending` es nulo.
- **Elegibilidad** (T32): la persona está identificada (no es el consumidor final) y activa en el
  maestro. Con `AssociateCredit` además `IsAssociate` y el asociado sin retiro; con `CustomerCredit`,
  `IsCustomer`.
- Si la persona no es elegible, `eligible = false` y `reasons[]` usa los mismos códigos de error que el
  cobro: `Inventory.Credit.PersonNotIdentified`, `.PersonInactive`, `.NotAssociate` o `.NotCustomer`.

**(IC)** Con Cartera habilitada:

- `lending` trae lo que responda `EstadoCrediticioAsync`, y la respuesta queda **sólo en la auditoría**
  como evidencia (`evidenceId`). Inventario no la guarda como dato (FR-060).
- **Cartera responde tarde**: después de `Cartera.ConsultaSegundos` se aplica
  `Cartera.PoliticaSinRespuesta`, por tipo de tercero. Con `Bloquear` → 422
  `Inventory.Credit.LendingNoResponse`. Con `PermitirConAprobacion` → `origin = LendingNoResponse` y
  `requiresApproval = true`.
- **Estado de la persona**: en mora o bloqueada → 422 `Inventory.Credit.LendingBlocked` (`data: { status
  }`); sin cupo suficiente → 422 `Inventory.Credit.QuotaExceeded` (`data: { availableQuota }`). La
  pantalla ofrece pagar la diferencia de contado.

### 23.2 El crédito en la venta

No hay ruta propia: el crédito es un pago de clase `AssociateCredit` o `CustomerCredit` dentro de
`payments` (§22.4), en la factura o en el cobro del POS. Al confirmar:

1. **Evaluación**: el servidor repite la evaluación de §23.1. Una persona no elegible → 422 con el código
   de `reasons`.
2. **Aprobación**: crea una solicitud del motor con `Subject = ProvisionalCredit` y `SourceType =
   DocumentPayment` (módulo `Inventory`) por el monto financiado.
   - El nivel 1 exige `Inventory.Sales.SellOnCredit`, con un monto máximo
     (`SEC_PermissionAmountLimits`) igual o mayor que lo financiado. Los niveles siguientes, la política
     del tipo.
   - Se excluyen el cajero y quien creó el documento (T33).
   - El documento queda `PendingApproval`, y la última aprobación lo confirma.
   - Si nadie en la cooperativa tiene un límite suficiente → 422 `Inventory.Approval.AmountExceedsLimit`
     (`data: { maxAmount }`).
3. **Pago pendiente de validar**: el pago queda con `pendingValidation = true` y `creditOrigin =
   ProvisionalCredit`.
4. **Mensajes**:
   - `VentaFacturada` lleva el pago de crédito por su medio, y la matriz lo manda a una cuenta que exige
     tercero y documento cruce `FV`. La cuenta por cobrar la registra Contabilidad
     (`Cartera.CuentaPorCobrarRegistradaPor = Contabilidad`, sellado y forzado mientras IC esté
     pendiente).
   - Se emite `VentaACreditoRegistrada`, con entrega `Destination = Lending`, `Mode = Always` y `Status =
     Pending` (§23.4).

| Ruta | Permiso | Respuesta |
|---|---|---|
| `GET /api/inventory/sales/documents/{id}/credit` | Sales.View | `{ payments: [{ documentPaymentPublicId, meansCode, meansClass, amount, installments, termDays, periodicityDays, firstDueDate, finalDueDate, suggestedLineCode?, pendingValidation, creditOrigin, approval?: { approvalRequestPublicId, approvedByName, level, decidedAt } }], accountsReceivableRecordedBy, lendingMessages: [{ messagePublicId, type, deliveryStatus, destinationAvailable, validation: { status: Pending \| Validated \| Failed, evaluatedAt?, reason? } }] }` |

Mientras IC esté pendiente, `destinationAvailable = false` y `validation.status = Pending`, con el
texto «Pendiente: el destino aún no está disponible (IC)». **(IC)** `validation` sale de
`EstadoDeValidacionAsync`. Un resultado negativo deja la entrega en `ValidationFailed` y levanta la
alerta `Integracion.ValidacionFallida`. La venta sigue confirmada: nada se anula solo (FR-061).

### 23.3 Interfaz de consultas a Cartera (IC) — en proceso, sin HTTP

`Application/Common/Integration/Lending/IConsultasDeCartera`. Tiene exactamente dos métodos, y la prueba
`InventarioNoConoceContabilidadNiCartera` los vigila (FR-014, FR-085):

| Método | Entrada | Salida |
|---|---|---|
| `EstadoCrediticioAsync` | `personPublicId`, clase del medio (asociado o cliente), `amount`, `operationDate`, canal, con `CancellationToken` de `Cartera.ConsultaSegundos` | `EstadoCrediticioDto { status, availableQuota, lines[], evidenceId }`. Si no hay destino, devuelve la marca `CarteraNoHabilitada` |
| `EstadoDeValidacionAsync` | `messagePublicId` de la `VentaACreditoRegistrada` | `EstadoDeValidacionDto { status: Pending \| Validated \| Failed, evaluatedAt?, reason? }` |

Los valores de `status` de la persona (activo, en mora, bloqueado…) y la forma de `lines` los fija la
especificación de Cartera (D-02). Mientras tanto son provisionales y no se persisten en Inventario. Una
respuesta que llega después del tiempo máximo no cambia una venta ya resuelta; queda en la auditoría.

### 23.4 Mensajes para Cartera

Toda venta con pago de crédito emite `VentaACreditoRegistrada`. Sus notas, devoluciones y anulaciones
—incluidos los casos b y c de la DIAN— emiten `AjusteDeVentaACredito`, con `AdjustmentClass` en
`CreditNote`, `DebitNote`, `Return`, `Voiding`, `VoidingByDianRejection` o `Replacement`. Todas las
entregas son `Destination = Lending` y `Mode = Always`, dependen del mensaje original y nacen en el
`SaveChanges` del documento (`decisiones-transversales.md` §2.6).

Se ven en la bandeja (§25.2) con `destination=Lending`. Mientras IC esté pendiente, el despachador las
salta sin intentos ni alertas. Cuando IC exista, se entregan en orden de `Id`, respetando las
dependencias, y Cartera deduplica por `MessageId`.

## 24. Facturación electrónica — `/api/electronic-invoicing`

Es un módulo de plataforma (T3, T40) con permisos `ElectronicInvoicing.{Settings.View, Settings.Manage,
Resolutions.View, Resolutions.Manage, Documents.View, Documents.Transmit, Documents.Correct,
Documents.TransmitByCurrentChannel, Contingencies.View, Contingencies.Declare}`. En esta sección se
escriben sin el prefijo `ElectronicInvoicing.`.

Los parámetros `EINV` (`Dian.ObligadaAFacturar`, `Dian.EsperaMaximaPosSegundos`,
`Dian.PlazoContingenciaHoras`, `Dian.AlertaHorasAntesDelPlazo`, `Dian.MinutosAlertaSinValidar`,
`Dian.UmbralFallasCircuito`, `Dian.AvisoResolucionPorcentaje`, `Dian.AvisoResolucionDias`,
`Dian.EntregaCorreo`, `DocumentoSoporte.Generacion`) se leen y cambian por la ruta de parámetros con
vigencia (§7).

**Ninguna llamada al canal va dentro de la transacción de confirmación.** El número lo asigna el ERP
al confirmar, y cada documento sella el canal con que se numeró: sus reenvíos, sus correcciones a y b y
su transmisión desde contingencia salen por ese mismo canal (FR-064).

### 24.1 Configuración de emisión — `/settings`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /settings` | Settings.View | `{ current?: ElectronicEmissionSettingDto, history: [ElectronicEmissionSettingDto], availableChannels: [{ channelCode, name, capabilities: { documentKinds[], events[], acceptsErpNumber, isAsync, returnsPdf, canSendEmail } }], credential: { key, configured, verifiedAt? } }`. `ElectronicEmissionSettingDto = { settingPublicId, mode: TechnologyProvider (1) \| OwnSoftware (2), channelCode, environment: Production (1) \| Testing (2), softwareId?, testSetId?, emailDeliveryBy: Erp (1) \| Channel (2), isEnabled, credentialVerifiedAt?, validFrom, validTo?, reason, createdByName }` |
| `POST /settings` | Settings.Manage | `ConfigureEmissionCommand`: `{ mode, channelCode, environment, softwareId?, testSetId?, emailDeliveryBy, isEnabled, validFrom, reason }` → 201. Crea una vigencia nueva y cierra la anterior la víspera, sin cruces. `CredentialKey` no viaja: se **deriva** (`{tenantPublicId}.{channelCode}.json`) y queda fuera del diff |
| `POST /settings/verify-credential` | Settings.Manage | `VerifyChannelCredentialCommand`: `{ channelCode? }` → `{ verified, verifiedAt?, outcome, messages: [{ rule, kind, text, translation }] }` (`ICanalDeEmisionElectronica.ProbarAsync`). Sólo sella `CredentialVerifiedAt` |

Al guardar una vigencia nueva se valida:

- que el canal esté registrado → si no, 422 `ElectronicInvoicing.Settings.ChannelUnknown`;
- que el canal acepte el número del ERP → si no, `ElectronicInvoicing.Settings.ChannelRejectsErpNumber`;
- que el modo `OwnSoftware` tenga `softwareId` → si no, `ElectronicInvoicing.Settings.SoftwareIdRequired`;
- que haya al menos una resolución vigente de cada tipo en uso asociada a ese canal → si no, 422
  `ElectronicInvoicing.Settings.NoResolutionForChannel` (`data: { kinds[] }`).

Si el archivo de credenciales no está en el Secret o no coincide con la ruta derivada → 422
`ElectronicInvoicing.CredentialMismatch`.

### 24.2 Resoluciones de numeración — `/resolutions`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /resolutions?kind=&environment=&status=` | Resolutions.View | `[DianNumberingResolutionDto { resolutionPublicId, kind, backsUpKind?, resolutionNumber, resolutionDate, prefix, rangeFrom, rangeTo, validFrom, validTo, environment, lastIssuedNumber?, consumedFraction, daysToExpire, status: Active \| NotYetValid \| Expired \| Exhausted, channels: [{ channelCode, softwareId?, validFrom, technicalKeyMasked? }] }]`. `status` es un texto calculado, no un enum guardado |
| `GET /resolutions/{id}` | Resolutions.View | `DianNumberingResolutionDto` + los documentos numerados por mes |
| `POST /resolutions` | Resolutions.Manage | `RegisterNumberingResolutionCommand`: `{ kind: Invoice (1) \| PosEquivalent (2) \| SupportDocument (3) \| Contingency (4), backsUpKind?, resolutionNumber, resolutionDate, prefix, rangeFrom, rangeTo, validFrom, validTo, environment, reason }` → 201 |
| `PUT /resolutions/{id}` | Resolutions.Manage | corrige datos de digitación mientras `lastIssuedNumber` sea nulo. Con números emitidos, sólo `validTo` hacia atrás (retirarla) → 422 `ElectronicInvoicing.Resolution.InUse` |
| `POST /resolutions/{id}/channels` | Resolutions.Manage | `LinkResolutionToChannelCommand`: `{ channelCode, softwareId?, validFrom, technicalKey?, fetchTechnicalKeyFromChannel?, reason }` → la asociación del prefijo con el canal desde esa fecha. La clave técnica es sólo de factura (`[NoAuditar]`) y se devuelve enmascarada (`••••ab12`). `fetchTechnicalKeyFromChannel` usa la consulta de rangos del canal cuando `capabilities` la ofrece: verifica la resolución y propone la clave, nunca crea resoluciones |

- **Prefijo**: hasta 4 caracteres alfanuméricos → si no, `ElectronicInvoicing.Resolution.PrefixInvalid`.
- **Rango**: `rangeFrom ≤ rangeTo` → si no, `.RangeInvalid`. Dos resoluciones del mismo tipo, prefijo y
  ambiente con rangos o vigencias que se cruzan → 422 `.Overlaps`.
- **Contingencia**: una resolución `Contingency` exige `backsUpKind` → si no,
  `.BackedKindRequired`.
- **Clave técnica**: en una resolución que no es de factura → 422 `.TechnicalKeyNotAllowed`.
- **Avisos**: las alertas `Dian.ResolucionPorAgotar` y `Dian.ResolucionPorVencer` salen según
  `Dian.AvisoResolucionPorcentaje` y `Dian.AvisoResolucionDias`.

### 24.3 Preparación — `GET /readiness`

`GET /readiness?asOf=&documentType=&cashRegister=` · Settings.View · `GetDianReadinessQuery`, que es la
misma decisión de `GuardiaDeEmisionFiscal.Evaluar` que usan la confirmación y la apertura de caja.

Respuesta:

```
{ asOf, obligated,
  verdict: Electronic | NonElectronic | Blocked,
  channel?: { channelCode, mode, environment },
  resolutions: [{ kind, prefix, status, consumedFraction, daysToExpire }],
  openContingency?: { contingencyPublicId, type },
  missing: [{ code, message, whoFixes: { page, permission } }] }
```

Los códigos de `missing[]` son también los de `data.missing[]` de `ElectronicInvoicing.NotReady`:

| Código | Qué falta |
|---|---|
| `ElectronicInvoicing.Readiness.NoSettings` | no hay configuración de emisión vigente |
| `.Disabled` | la emisión está desactivada |
| `.CredentialNotVerified` | la credencial del canal no está verificada |
| `.NoResolution` | no hay resolución vigente |
| `.ResolutionNotLinked` | la resolución no está asociada al canal |
| `.TestSetPending` | falta el set de pruebas en producción |
| `.NoContingencyResolution` | hay contingencia 03 abierta y no hay resolución de contingencia |
| `.DocumentTypeMissing` | la caja no tiene el tipo del rol (también el de contingencia que respalda al rol de la venta, §20.1) |
| `.EventsNotSupported` | el canal vigente no ofrece eventos RADIAN (§24.7) |
| `.CompanyDataIncomplete` | faltan NIT, DIVIPOLA, responsabilidades o correo del emisor |

### 24.4 Documentos electrónicos — `/documents`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /documents?status=&kind=&from=&to=&prefix=&number=&sourceModule=&contingency=&overdue=` | Documents.View | paginado de `ElectronicDocumentDto { electronicDocumentPublicId, kind, dianDocumentTypeCode, prefix, consecutive, number, environment, channelCode, source: { module, documentPublicId, documentClass, route }, counterpartyName, issuedAt, total, status, contingencyType?, rejectedBy?, uniqueCode?, uniqueCodeKind?, attemptCount, nextAttemptAt?, transmissionDeadline?, lastMessage? }`. `overdue=true` trae los que llevan más de `Dian.MinutosAlertaSinValidar` sin validar |
| `GET /documents/{id}` | Documents.View | `ElectronicDocumentDetailDto` (abajo) |
| `POST /documents/{id}/retry` | Documents.Transmit | `EmitElectronicDocumentCommand`, un intento **ahora** con el arrendamiento de la fila → `{ status, outcome, messages[] }`. Sólo en `Pending`, `IssuerContingency` o `DianContingency`. En `Sent` → 422 `ElectronicInvoicing.Document.AwaitingResponse` (se consulta con `query-status`); en un estado final → 422 `ElectronicInvoicing.Document.Final`; con otro proceso trabajándolo → 422 `ElectronicInvoicing.Document.InProgress` (`data: { leaseUntil }`) |
| `POST /documents/{id}/query-status` | Documents.Transmit | `QueryElectronicDocumentStatusCommand` → `{ status, outcome, messages[] }`. Ante un resultado ambiguo **siempre** se consulta antes de reenviar: si el canal responde `NotFound`, el documento vuelve a `Pending` y se reenvía la misma versión con el mismo número |
| `POST /documents/{id}/transmit-by-current-channel` | Documents.TransmitByCurrentChannel | `TransmitByCurrentChannelCommand`: `{ reason }`. Sólo si el canal sellado se retiró **y** la resolución con que se numeró está asociada al canal vigente; si no → 422 `ElectronicInvoicing.Document.ChannelNotLinked`. Queda auditado |
| `POST /documents/{id}/download-link` | la del dueño del adjunto (`Inventory.Sales.View` o `Inventory.Purchases.View`); sin ella, 404 | `?artifact=Canonical\|SignedXml\|AttachedDocument\|ApplicationResponse\|GraphicRepresentation&version=&transmission=` → `{ direct, url, expiresAt }`, el enlace firmado de 60 s (feature 011, auditado). El permiso es sólo la regla del dueño del adjunto (`decisiones-transversales.md` §2.10, FR-068, T41): `ElectronicSalesDocument` exige `Inventory.Sales.View` y `ElectronicPurchaseDocument` exige `Inventory.Purchases.View`; no se exige además `Documents.View`. `artifact` es texto, no un enum guardado |

`ElectronicDocumentDetailDto` es `ElectronicDocumentDto` más:

```
{ resolution?: { publicId, number, prefix },
  qrContent?, issuedAt, validatedAt?, deliveredAt?,
  versions: [{ versionNumber, reason: Initial (1) | CaseA (2) | CaseB (3), sourceDocumentPublicId,
               canonicalSha256, createdAt,
               artifacts: [{ artifact, attachmentPublicId, fileName }],
               partySnapshotChange?: { before, after } }],
  transmissions: [{ operation, channelCode, requestedAt, requestedBy: { kind: Person | Process, name },
                    durationMs, outcome, providerCode?, dianStatusCode?,
                    messages: [{ rule, kind: Rejection | Notice, text, translation }],
                    externalReference?, applicationResponseAttachmentPublicId? }],
  correctsDocument?: { electronicDocumentPublicId, number, uniqueCode },
  correctedBy: [{ electronicDocumentPublicId, kind, number, status }],
  contingency?: { contingencyPublicId, type, startedAt, endedAt?, deadlineAt },
  cancellation?: { reason, byName, at } }
```

- `operation` es `TransmissionOperation` y `outcome` es `ChannelOutcome`.
- `status` es `ElectronicDocumentStatus`: `Pending` (0), `Sent` (1), `Validated` (2),
  `ValidatedWithNotices` (3), `Rejected` (4), `IssuerContingency` (5), `DianContingency` (6) y
  `CancelledWithoutReplacement` (7).
- Nunca queda `Validated` sin código único y sin la respuesta de validación. Las notificaciones solas no
  rechazan.

### 24.5 Documentos rechazados: casos a, b y c

Se aplican **sólo** a un documento `Rejected`. En otro estado → 422
`ElectronicInvoicing.Document.NotRejected`. Los casos b y c además exigen un rechazo **confirmado**, por
la respuesta definitiva o por la consulta de estado; si no → 422
`ElectronicInvoicing.Document.RejectionNotConfirmed`. Ninguno se aplica a `Sent`, `Validated` ni a un
`Pending` sin respuesta. **A diferencia de la nómina (010), el número rechazado se reutiliza**, porque
el documento rechazado no quedó expedido.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `POST /documents/{id}/correct` | Documents.Correct | caso a, `CorrectRejectedDocumentCommand`: `{ reason, partySnapshotChanges?: { name?, address?, cityDaneCode?, email?, phone?, responsibilities? } }`. Vuelve a armar el canónico con los datos corregidos en el maestro y compara la **huella económica** (`ReglaDeCorreccionFiscal`): tercero (tipo, número y DV), líneas, bases, impuestos, retenciones y totales. Si coincide, crea la versión n+1 (`CaseA`), agrega una versión a la copia fiscal si cambió (antes y después auditados, FR-011) y vuelve a `Pending` con el mismo canal y número, **sin** mensajes de negocio → `{ versionNumber, status }`. Si la huella difiere → 422 `ElectronicInvoicing.Document.EconomicFootprintChanged` (`data: { fields[] }`) y se sigue por el caso b |
| `POST /documents/{id}/replacement-draft` | Documents.Correct | preparación del caso b: el módulo dueño, por `IFuenteDeDocumentoElectronico`, crea un borrador de la **misma clase**, precargado con las líneas y la contraparte del rechazado y vinculado con `ReplacementOf`, sin número → 201 `{ replacementDraftPublicId, sourceModule, editRoute }`. El borrador se edita por la ruta de su grupo: `PUT /api/inventory/sales/invoices/{id}` para factura y documento equivalente POS, `/sales/credit-notes/{id}` para notas, y la ruta del documento soporte en §14. Si ya hay un borrador de reemplazo vivo → 422 `ElectronicInvoicing.Document.ReplacementDraftExists` (`data.replacementDraftPublicId`) |
| `POST /documents/{id}/replace` | Documents.Correct | caso b, `ReplaceRejectedDocumentCommand`: `{ replacementDocumentPublicId, reason }`. En una transacción: anula el documento del ERP con un documento contrario **sin efecto fiscal** (`Voiding`, que emite `DocumentoAnulado`, la devolución a existencia y el `AjusteDeVentaACredito` `VoidingByDianRejection` si hubo crédito), confirma el reemplazo tomando el **mismo** número fiscal (`FiscalNumberReleased` en el anulado) y crea la versión n+1 (`CaseB`) → `{ voidingDocumentPublicId, replacementDocumentPublicId, versionNumber, status: Pending }`. El reemplazo pasa por la validación previa y el cerrojo como cualquier confirmación |
| `POST /documents/{id}/cancel` | Documents.Correct | caso c, `CancelRejectedDocumentCommand`: `{ reason }`. Anula igual que el caso b, sin reemplazo; el número queda como `CancelledWithoutReplacement`, con motivo y responsable, y no cuenta como hueco |

### 24.6 Contingencias — `/contingencies`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /contingencies?isOpen=&type=&from=&to=` | Contingencies.View | `[DianContingencyEventDto { contingencyPublicId, type: Issuer03 (3) \| Dian04 (4), channelCode, startedAt, endedAt?, isOpen, detectedBy: { kind: Person \| Process, name }, reason, deadlineAt?, documents: { total, pending, transmitted, rejected } }]` |
| `GET /contingencies/{id}` | Contingencies.View | `DianContingencyEventDto` + `documents[]` (`ElectronicDocumentDto`) + `evidence: [{ at, byName, note }]` |
| `POST /contingencies` | Contingencies.Declare | `OpenContingencyCommand`: `{ type: Issuer03, reason, startedAt?, evidenceNote? }` → 201. Mientras esté abierta, las ventas fiscales **nuevas** toman el consecutivo del tipo del rol de contingencia que respalda al rol de la venta en la caja: `PosSaleContingency` para `PosSale` e `InvoiceContingency` para `InvoiceOnRequest` (§20.1). La 04 la declara **sólo** el canal (`DianUnavailable`) → 422 `ElectronicInvoicing.Contingency.Dian04OnlyByChannel`. Con una 03 ya abierta → 422 `ElectronicInvoicing.Contingency.AlreadyOpen` |
| `POST /contingencies/{id}/close` | Contingencies.Declare | `CloseContingencyCommand`: `{ reason, endedAt?, evidenceNote? }`. Fija el plazo `deadlineAt = endedAt + Dian.PlazoContingenciaHoras`, contado según el tipo de documento, y el procesador transmite en orden, referido al número de papel. La alerta `Dian.PlazoDeContingencia` sale `Dian.AlertaHorasAntesDelPlazo` horas antes del plazo |

El circuito por canal (`CircuitoDeCanal`, umbral `Dian.UmbralFallasCircuito`) abre la 03 solo, con
`detectedBy = Process`. Cuentan como fallas los errores del canal y también las esperas del POS que se
cumplen sin respuesta (§20.2). Un documento ya numerado con resultado ambiguo **nunca** se renumera a
contingencia.

### 24.7 Eventos RADIAN que emite la cooperativa (I5)

Registrar un evento hecho por fuera (I1) es de compras, en §14. La emisión desde el ERP es un
`COR_ElectronicDocuments` de `Kind = RadianEvent030` (8) o `RadianEvent032` (9), por el mismo puerto y
la misma máquina de estados (T42).

Ruta, cuerpo y errores: §14.8 (`POST /api/inventory/purchases/supplier-invoices/{id}/radian-events/emit`,
`EmitRadianEventCommand`, permiso `Inventory.Purchases.EmitRadianEvent`), con los errores
`Inventory.RadianEvent.NotApplicable`, `.OutOfOrder`, `.ReceiptNotConfirmed`, `.DateInvalid` y
`.AlreadyRegistered`. Lo que añade esta sección:

- Si el canal vigente no ofrece eventos → 422 `ElectronicInvoicing.NotReady`
  (`data.missing[] = ElectronicInvoicing.Readiness.EventsNotSupported`).
- El evento sale por el canal vigente al emitirlo, porque es un documento nuevo.

### 24.8 Errores de facturación electrónica

Canónicos: `ElectronicInvoicing.NotReady` (`data.missing[]`), `.CredentialMismatch`,
`.Document.NotRejected`, `.Document.AwaitingResponse`, `.Resolution.Exhausted`,
`.Resolution.Expired`.

De esta sección:

- `ElectronicInvoicing.Settings.ChannelUnknown`, `.ChannelRejectsErpNumber`, `.SoftwareIdRequired`,
  `.NoResolutionForChannel`.
- `ElectronicInvoicing.Resolution.PrefixInvalid`, `.RangeInvalid`, `.Overlaps`,
  `.BackedKindRequired`, `.TechnicalKeyNotAllowed`, `.InUse`, `.NotLinkedToChannel`.
- `ElectronicInvoicing.Document.Final`, `.InProgress`, `.ChannelNotLinked`, `.RejectionNotConfirmed`,
  `.EconomicFootprintChanged` (`data.fields[]`), `.ReplacementDraftExists`, `.NotDeliverable`.
- `ElectronicInvoicing.Document.MissingData` (422, `data.missing[] { field, where, permission }`): el
  documento no tiene un dato que el canónico exige (contracts/dian.md); bloquea la confirmación.
- `ElectronicInvoicing.Contingency.Dian04OnlyByChannel`, `.AlreadyOpen`.
- Los de RADIAN (`Inventory.RadianEvent.NotApplicable`, `.OutOfOrder`, `.ReceiptNotConfirmed`,
  `.DateInvalid`, `.AlreadyRegistered`) son de la §14.8.

## 25. Integración — validación previa, bandeja, reproceso y envío posterior — `/api/inventory`

Permisos: `Inventory.Messages.{View, Reprocess, SendNotApplicable}` e `Inventory.Documents.View`. La
bandeja, el reproceso y el envío posterior son de Inventario. El lote manual y su vista previa son de
Contabilidad (§26.4, T3).

### 25.1 Validación previa — `POST /documents/{id}/prevalidate`

Permiso `Documents.View`. Es una consulta sin clave de operación: no guarda nada ni toma el cerrojo.
Llama a `IContabilidadParaInventario.EvaluarAsync` con los mensajes que emitiría el documento tal como
está.

La usan la factura de oficina al salir de cada sección, el panel de cobro del POS y las pantallas de
compras, ajustes y traslados antes de «Confirmar». El saldo inicial no la necesita: su mensaje es
informativo (FR-089).

- **Cuerpo**: `{ payments?: [DocumentPaymentInput] }`. Los pagos propuestos se usan cuando el borrador
  todavía no los tiene (el POS), porque `VentaFacturada` los lleva por medio.
- **Respuesta**:

```
PrevalidationResultDto {
  applies: bool, postingMode: Online | Batch | NotPosted, responded: bool, isPostable?: bool,
  errors: [{ messageType, lineNumber?, account?: { code, name }, rule, message,
             whoFixes: { module, page, permission } }],
  warnings: [{ messageType, code, message }],
  elapsedMs }
```

- **Sin paso**: con el modo que se sellaría igual a `NotPosted`, o si el documento sólo emite mensajes
  informativos, `applies = false`.
- **Sin respuesta**: si Contabilidad no responde en `Contabilidad.ValidacionPreviaSegundos`,
  `responded = false`. La pantalla muestra la política vigente (`ConfirmarConPendiente` o `Bloquear`).
- **Los mismos errores que la confirmación**: los de la matriz (`Accounting.InventoryRule.Missing`,
  `Accounting.InventoryRule.TaxRateMismatch`, tipo de comprobante sin mapear, mensaje descuadrado) y las
  reglas de cuenta de la 009 (tercero, documento cruce, centro de costo, sucursal, base y tarifa, período
  contable abierto). La confirmación repite la evaluación y responde 422
  `Inventory.Prevalidation.NotPostable` con los mismos `errors[]`.
- **El costo es provisional**: los montos de costo se leen sin bloqueo. Sólo afectan a la regla de
  «valor cero».

### 25.2 Bandeja de mensajes — `/messages`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /messages?status=&destination=&type=&document=&documentNumber=&documentType=&batch=&from=&to=&closure=&prevalidationOutcome=` | Messages.View | `ListIntegrationMessagesQuery` → paginado de `IntegrationMessageDto` (abajo) + `countsByStatus: { Pending, InBatch, Processed, Rejected, NotApplicable, ValidationFailed }` |
| `GET /messages/{id}` | Messages.View | `IntegrationMessageDto` + `payload` (JSON del contrato `…V1`, inmutable, con su `payloadSha256`) + `attempts: [{ startedAt, finishedAt, outcome, code?, message?, actor: { kind, name }, batchNumber?, instance }]` + `dependsOn: [{ messagePublicId, type, status }]` + `dependents: [{ messagePublicId, type, status }]` |
| `POST /messages/reprocess` | Messages.Reprocess | `ReprocessMessagesCommand`: `{ messagePublicIds[], reason }` → **202** `{ batchPublicId, number, trigger: Reprocess, messages, dragged }` |
| `POST /messages/send-not-applicable` | Messages.SendNotApplicable | `SendNotApplicableMessagesCommand`: `{ from, to, documentTypePublicIds?, cutoffMessagePublicId, reason }` → **202** `{ batchPublicId, number, trigger: SendNotApplicable, documents, messages }` (FR-078) |

`IntegrationMessageDto`:

```
{ messagePublicId, emittedAt, type, version, kind: Business (1) | Informational (2),
  destination: Accounting | Lending, deliveryStatus, mode: Online (1) | Batch (2) | NotPosted (3) | Always (4),
  scheduleKey?, batch?: { batchPublicId, number },
  attempts, nextAttemptAt?, lastError?: { code, message, whoFixes? },
  processedAt?, result?: { voucherTypeCode, number, accountingDocumentPublicId, route } | { withoutVoucher: Informational | ZeroValue },
  origin: { kind: Document | Operation, documentClass?, documentTypeCode?, number?, publicId, operationDate },
  related?: { publicId, documentClass, number },
  originUserName, prevalidationOutcome?, blockedBy: [{ messagePublicId, type, deliveryStatus }],
  destinationAvailable, pendingValidation? }
```

- La lista sale en orden de emisión; `emittedAt` lo hace visible. El `Id` `bigint` de
  `COR_IntegrationMessages` no sale del módulo (Principio VI; contracts/mensajes.md §4).
- `deliveryStatus` es `DeliveryStatus`: `Pending` (0), `InBatch` (1), `Processed` (2), `Rejected` (3),
  `NotApplicable` (4), `ValidationFailed` (5).
- `destinationAvailable = false` es el destino `Lending` mientras IC esté pendiente: la fila se muestra
  como «Pendiente: el destino aún no está disponible (IC)».
- `result.route` lleva al comprobante: `/contabilidad/comprobantes/{id}`, o `/contabilidad/inventario/lotes/{id}` si es un resumido.
- `result` se lee **sólo** de `COR_IntegrationMessageDeliveries` (`ResultReference`,
  `ResultVoucherTypeCode`, `ResultVoucherNumber`), que escribe `RegisterDeliveryResultCommand` con lo que
  devuelve el consumidor en `ResultadoDeConsumo.Processed(AccountingDocumentPublicId, VoucherTypeCode,
  VoucherNumber)`. Inventario no consulta tablas `ACC_` (FR-014, `InventarioNoConoceContabilidadNiCartera`).

**Reproceso** (FR-080). Sólo mensajes `Rejected`; otro estado → 422 `Integration.Message.NotRejected`
(`data.messagePublicIds[]`).

- **Lote**: el reproceso crea un `COR_IntegrationBatches` con `Trigger = Reprocess` y las entregas pasan
  de `Rejected` a `InBatch` con ese lote.
- **Arrastre**: arrastra, en orden, a los dependientes que esperaban (T10), y `dragged` dice cuántos.
- **Actor y fecha**: el actor es la persona que ordena (FR-083) y cada mensaje se procesa con su fecha de
  operación original, nunca con otra.
- **Otra versión del contrato**: un mensaje de una versión que el destino ya no acepta sigue
  rechazado con `Integration.VersionNotAccepted`. Nunca se reescribe.

**Envío posterior de «no aplica»** (FR-078):

- **Vista previa**: es la misma bandeja con `status=NotApplicable&destination=Accounting&from=&to=
  &documentType=&closure=true`. Con `closure=true` la respuesta suma **toda la clausura de
  dependencias**: anulaciones, notas, ajustes y documentos derivados de cualquier fecha, con sus
  relacionados. También trae `cutoffMessagePublicId`, el `PublicId` del último mensaje listado (el más
  reciente en orden de emisión), para que se envíe exactamente lo previsualizado; el handler lo resuelve
  al `Id` interno.
- **Modo del tipo**: si algún tipo del rango sigue en `NotPosted` → 422
  `Integration.SendNotApplicable.ModeStillNotPosted` (`data: { documentTypeCodes[] }`).
- **Qué hace**: crea un lote `COR_IntegrationBatches` con `Trigger = SendNotApplicable` y pasa en bloque
  las entregas `NotApplicable` → `InBatch` con ese lote, con la persona como actor y el motivo.
- **Período cerrado**: un documento cuya fecha cae en un período contable cerrado se rechaza completo a
  la bandeja; no es un error de la petición.

### 25.3 Lotes

El lote es de la plataforma (`COR_IntegrationBatches`, T12). Su vista previa, orden y detalle son de
Contabilidad (§26.4). Inventario los ve con la bandeja (`?batch=`) y con la vista `accounting-batches`
de §27. La alerta `Integracion.LoteNoCorrio` sale cuando un lote programado no corrió a su hora más la
tolerancia técnica.

### 25.4 Errores

`Integration.Message.NotRejected`, `Integration.SendNotApplicable.ModeStillNotPosted`,
`Integration.VersionNotAccepted` (motivo visible en la bandeja, no en la petición),
`Inventory.Prevalidation.NotPostable` y `.NoResponse` (en las confirmaciones).

## 26. Lado contable (enmienda de la 009) — `/api/accounting/inventory`

Permisos: `Accounting.InventoryRules.{View, Manage}` y `Accounting.InventoryBatches.{View, Run}`. Siguen
las convenciones de la 009. Sólo ordenar un lote lleva `Idempotency-Key`.

**Lo que no tiene ruta.** Los comandos de consumo (`PostInventoryMessagesCommand`,
`PostInventorySummaryGroupCommand`) no tienen ruta; lo vigila `LosComandosDeConsumoNoTienenRuta`. Las
cuatro consultas de `IContabilidadParaInventario` corren en proceso, sin HTTP.

### 26.1 Matriz de reglas — `/rules`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /rules/catalog` | InventoryRules.View | `{ operations: [{ code, requiredRoles[] }], roles: [{ code, requiredDimensions[], allowedDimensions[] }], dimensions: { accountingGroups[], warehouses: [{ code, name, branchCode }], pointsOfSale[], paymentMeans[], taxRates: [{ code, name, kind, rate?, amountPerUnit?, validFrom, validTo? }], reasons[] } }`. Los catálogos fijos son `OperacionesDeInventario` y `RolesDeCuenta`; los valores de dimensión los entrega `IDimensionesDeInventario` |
| `GET /rules?operation=&role=&asOf=&accountingGroupCode=&warehouseCode=&pointOfSaleCode=&paymentMeansCode=&taxRateCode=&reasonCode=&account=&onlyCurrent=` | InventoryRules.View | paginado de `InventoryPostingRuleDto` (abajo) |
| `GET /rules/{id}` | InventoryRules.View | `InventoryPostingRuleDto` |
| `GET /rules/{id}/versions` | InventoryRules.View | las versiones de la misma `DimensionKey`, de la más nueva a la más vieja |
| `POST /rules` | InventoryRules.Manage | `CreateInventoryPostingRuleCommand`: `{ operation, role, dimensions: {…}, accountPublicId, validFrom, notes, reason }` → 201 `{ rulePublicId }` |
| `POST /rules/{id}/versions` | InventoryRules.Manage | `AddInventoryPostingRuleVersionCommand`: `{ accountPublicId, validFrom, notes, reason }`. Cierra la vigente la víspera |
| `POST /rules/{id}/deactivate` | InventoryRules.Manage | `DeactivateInventoryPostingRuleCommand`: `{ validTo, reason }` |
| `GET /rules/template.xlsx[?withData=true]` | InventoryRules.View | `GetInventoryRulesTemplateQuery`. Encabezados en la fila 1 (instrucciones en otra hoja): las columnas de contracts/plantillas.md §16 (`operacion`, `rol`, `grupoContable`, `bodega`, `puntoDeVenta`, `medioDePago`, `tarifa`, `tarifaPorcentaje`, `causa`, `sucursal`, `centroDeCosto`, `cuenta`, `vigenteDesde`, `vigenteHasta`, `notas`) |
| `POST /rules/import?mode=review\|apply` | InventoryRules.Manage | `ImportInventoryPostingRulesCommand`, con la mecánica de contracts/plantillas.md §0.5 (todo o nada, `ImportResultDto`, las mismas reglas que el alta una a una). Si `apply` falla → 422 `Import.Invalid`. Queda auditado como `Accounting.InventoryRules.Imported` |

`InventoryPostingRuleDto`:

```
{ rulePublicId, operation, role,
  dimensions: { accountingGroupCode?, warehouseCode?, pointOfSaleCode?, paymentMeansCode?,
                taxRateCode?, taxRate?, reasonCode?, branch?: { publicId, code },
                costCenter?: { publicId, code } },
  account: { publicId, code, name, isEligible },
  validFrom, validTo?, notes, dimensionKey, specificity, isCurrent, createdByName }
```

Qué se valida al guardar (T27):

- **Dimensiones**: la operación y el rol pertenecen a los catálogos fijos. Cada rol lleva sus
  dimensiones exigidas y sólo las admitidas: `Impuesto` y `Retencion` → `taxRateCode` + `taxRate`;
  `MedioDePago` → `paymentMeansCode` y, opcional, `pointOfSaleCode`; `Inventario`, `Costo`, `Ingreso` y
  `Transito` → `accountingGroupCode` y, opcional, `warehouseCode`; `Sobrante`, `Faltante` y
  `GastoDeArqueo` → `reasonCode`. Los errores son 422 `Accounting.InventoryRule.DimensionRequired`,
  `.DimensionNotAllowed` y `.DimensionCodeUnknown`, este último para un código que no existe según
  `IDimensionesDeInventario`.
- **Cuenta**: pasa `AccountEligibility.Verificar(…, ModuloContable.Inventario)`, con los errores
  `Accounting.Account.*` de la 009. La de impuesto tiene que ser de un `TaxKind` compatible.
- **Tarifa**: la del rol `Impuesto` o `Retencion` debe coincidir con la vigente de la cuenta → si no, 422
  `Accounting.InventoryRule.TaxRateMismatch` (`data: { taxRateCode, catalogRate, account, accountRate
  }`, C8).
- **Vigencia**: dos versiones de la misma clave no se cruzan → 422 `Accounting.InventoryRule.Overlaps`.
  Una versión no puede empezar en la fecha de operación del último mensaje ya contabilizado de esa
  operación, ni antes → 422 `.RetroactiveOverPosted` (`data: { lastPostedDate }`). Los errores pasados
  se corrigen con un comprobante manual (CG).
- **Resolución**: gana la regla más específica, con pesos: bodega o punto 16, centro 8, sucursal 4 y
  grupo 2.

### 26.2 Tipos de comprobante — `/voucher-mappings`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /voucher-mappings` | InventoryRules.View | `[InventoryVoucherMappingDto { mappingPublicId, operation, inventoryDocumentTypeCode?, voucherType: { publicId, code, name }, crossDocumentType?: { publicId, code }, isSeeded }]`. La semilla es la de `decisiones-transversales.md` §2.6: `FV`, `EI`, `SI`, `NV`, `CP`, `TR`, `AC` y `CJ` |
| `PUT /voucher-mappings` | InventoryRules.Manage | `SetInventoryVoucherMappingCommand`: `{ operation, inventoryDocumentTypeCode?, voucherTypePublicId, crossDocumentTypePublicId?, reason }`, que agrega o reemplaza por (operación, tipo). El tipo de comprobante debe ser `Usage = Module`, `ModuleCode = INV` y estar activo → si no, 422 `Accounting.VoucherType.NotAllowedForModule` (contracts/contabilidad.md §2.6) |

El tipo del comprobante de una unidad lo da su mensaje principal: el comercial antes que el de costo.
`DocumentoAnulado` usa el tipo de su original.

### 26.3 Completitud — `GET /completeness?date=`

Permiso `InventoryRules.View` · `InventoryRulesCompletenessQuery` (FR-082, SC-024).

Respuesta:

```
{ date,
  missingRules: [{ operation, role, accountingGroupCode?, warehouseCode?, pointOfSaleCode?,
                   paymentMeansCode?, taxRateCode?, reasonCode?, usedByDocumentTypes[], lastUsedAt? }],
  paymentMeansWithoutAccount: [{ paymentMeansCode, name, pointOfSaleCode? }],
  ineligibleRules: [{ rulePublicId, operation, role, account, reason }],
  taxRateMismatches: [{ taxRateCode, catalogRate, account, accountRate?, from, to }],
  unmappedOperations: [{ operation, inventoryDocumentTypeCode? }],
  summary: { total, byKind } }
```

Las combinaciones en uso (operación × grupo × bodega de los tipos que pasan, medios activos e impuestos
con vigencia) las entrega Inventario por `IDimensionesDeInventario`: Contabilidad no lee tablas `INV_`.
La consulta de parametrizaciones inválidas de la 009 suma las reglas de la matriz.

### 26.4 Lotes de contabilización — `/batches`

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /batches?from=&to=&status=&trigger=&destination=` | InventoryBatches.View | paginado de `IntegrationBatchDto` (abajo) |
| `POST /batches/preview` | InventoryBatches.View | `PreviewIntegrationBatchQuery`, que delega en `PreviewInventoryBatchQuery`: `{ from, to, documentTypeCodes?, scheduleKey?, branchPublicId? }` → `InventoryBatchPreviewDto` (abajo). No guarda nada |
| `POST /batches` | InventoryBatches.Run | `OrderIntegrationBatchCommand`, con `Idempotency-Key`: `{ cutoffMessagePublicId, from, to, documentTypeCodes?, scheduleKey?, branchPublicId?, reason }` → **202** `{ batchPublicId, number, status: Requested }`. Procesa exactamente lo previsualizado hasta `cutoffMessagePublicId` (el handler lo resuelve al `Id` interno, que sólo guarda `COR_IntegrationBatches.CutoffMessageId`), en segundo plano, con la persona como actor. Si ya hay un lote en curso con el mismo alcance → 422 `Accounting.InventoryBatch.AlreadyRunning` (`data: { batchPublicId, number }`) |
| `GET /batches/{id}` | InventoryBatches.View | `IntegrationBatchDto` + `documents[]` + `vouchers: [{ accountingDocumentPublicId, voucherTypeCode, number, operationDate, branch, costCenter?, granularity, documentsCount }]` + `rejected: [{ messagePublicId, documentNumber, code, message, whoFixes }]` |

`IntegrationBatchDto`:

```
{ batchPublicId, number, destination, trigger, scheduleKey?, scheduledFor?, cashSessionPublicId?,
  period?: { year, month }, dateFrom?, dateTo?, granularity, status,
  requestedBy: { kind: Person | Process, name }, requestedAt, startedAt?, finishedAt?,
  totals: { messages, documents, vouchers, rejected, debit, credit }, late }
```

- `trigger` es `BatchTrigger`: `Scheduled` (1), `CashSessionClose` (2), `PeriodClose` (3), `Manual`
  (4), `Reprocess` (5), `SendNotApplicable` (6).
- `status` es `BatchStatus`: `Requested` (0), `Running` (1), `Completed` (2),
  `CompletedWithRejections` (3), `Empty` (4).
- `granularity` es `PostingGranularity`: `PerDocument` (1) o `Summarized` (2).

`InventoryBatchPreviewDto`:

```
{ cutoffMessagePublicId,
  documents: [{ documentPublicId, class, documentTypeCode, number, operationDate, branch, total }],
  vouchers: [{ voucherTypeCode, operationDate, branch, costCenter?, granularity, documentsCount,
               lines: [{ account: { code, name }, debit, credit, thirdParty?, crossDocument?, taxBase? }],
               totals: { debit, credit } }],
  excluded: [{ documentPublicId, number, errors: [{ lineNumber, account, rule, whoFixes }] }] }
```

La vista previa valida documento por documento con `AccountingPoster.ValidarVariosAsync`, y excluye los
que fallan junto con sus relacionados. En el modo resumido suma débitos y créditos por clave, sin
netear, y conserva el detalle de las líneas cuya cuenta exige tercero, documento cruce o base gravable
(FR-077). Todo comprobante lleva la fecha de operación de sus documentos, nunca la del lote.

### 26.5 Recibos de contabilización — `GET /postings`

`GET /postings?document=&message=&batch=&from=&to=` · `Accounting.Vouchers.View` (el permiso de la 009
para ver comprobantes, contracts/contabilidad.md) → paginado de
`InventoryPostingDto`:

```
{ messagePublicId, type, version, sourceDocumentPublicId, relatedDocumentPublicId?, operationDate,
  accountingDocumentPublicId?, voucher?: { typeCode, number },
  withoutVoucher?: Informational | ZeroValue,
  batchPublicId?, originUserName, actor: { kind, name }, postedAt }
```

Es el vínculo documento ↔ comprobante, que sirve también para los resumidos y para el comprobante nuevo
de una anulación (T29).

### 26.6 Conciliación y saldos

Contabilidad no expone una ruta de saldos. `SaldosDeCuentasMapeadasAsync` (sobre
`InventoryAccountBalancesQuery`) se llama en proceso desde:

- la vista `reconciliation` de §27 (FR-081), con `Inventory.Reports.View` e
  `Inventory.Reconciliation.View`;
- la activación de bodegas (§13, FR-090). Mientras esta consulta no exista (antes de I2), en
  producción la activación responde 422 `Inventory.Activation.AccountingUnavailable`; el camino sin
  comparación existe sólo fuera de producción (§13.3).

Los conjuntos de cuentas son componentes conexos grupo ↔ cuenta de los roles `Inventario` y
`Transito`. El saldo sale **sólo** de `MovimientosContables`, con alcance explícito sin restricción de
sucursal, e incluye la apertura (`AP`). La diferencia la explican los mensajes de Inventario.

### 26.7 Errores del lado contable

Canónicos: `Accounting.InventoryRule.{Missing, Overlaps, RetroactiveOverPosted, TaxRateMismatch}`,
`Accounting.InventoryMessage.{Unbalanced, CurrencyNotSupported, WaitingForOriginal}` (llegan a la
bandeja, no a una petición), `Accounting.Period.InventoryPending` y
`Accounting.Document.InventoryCorrectsWithNewVoucher`.

De esta sección: `Accounting.InventoryRule.DimensionRequired`, `.DimensionNotAllowed`,
`.DimensionCodeUnknown`, `Accounting.VoucherType.NotAllowedForModule` y
`Accounting.InventoryBatch.AlreadyRunning`; la importación responde `Import.Invalid`.

## 27. Informes — `/api/reports/inventory/{vista}?format=json|xlsx|pdf|docx`

**Una ruta por vista** en `Endpoints/Reports/InventoryReportsEndpoints.cs`, reescrito (T44), para que
`LosEndpointsProtegidosExigenPermiso` revise cada una.

- **Permisos**: cada ruta lleva `.RequirePermission("Inventory.Reports.View")` y
  `.RequirePermissionWhenExporting("Inventory.Reports.Export")`. Las vistas marcadas **(PD)** exigen
  además `Inventory.Reports.ExportPersonalData` al exportar (FR-011, FR-087).
- **Entrega**: `TablaExportable` por `EntregaDeInformes.EntregarAsync`, con encabezado de empresa, NIT,
  filtros, usuario y fecha. Los errores salen con el sobre y el estado de la convención general.
- **Auditoría**: cada exportación a archivo emite `Inventory.Report.Exported` por
  `InventoryAuditEmitter`, con el PublicId de la cooperativa y la cantidad de filas.
- **Alcance**: se aplica en la consulta (bodega de origen o destino, punto de venta).
- **Costos**: sin `Inventory.Costs.Read`, las columnas de costo y valor salen vacías y la cabecera lo
  dice. La vista `valuation` exige `Inventory.Costs.Read`.
- **Columnas ocultas**: las que empiezan con `_` (`_documento`, `_producto`, `_bodega`, `_mensaje`,
  `_lote`, `_sesion`) las omiten la pantalla y los exportadores; sirven para profundizar.
- **Filtros comunes**: `from`, `to`, `asOf`, `branch`, `warehouse`, `pointOfSale`, `cashRegister`,
  `session`, `product`, `category`, `accountingGroup`, `person` y `documentType`. El rango es de hasta 5
  años, como en la 009.
- **Pantallas**: `/inventario/informes?vista=` y `/ventas/informes?vista=`.
- **Registro de vistas** (nuevo, T182): `GET /api/reports/inventory` con `Inventory.Reports.View` devuelve las vistas
  publicadas `[{ key, name, description, fileName, filters[], ownFilters?[], personalData, personalDataWhen?,
  requiredPermission? }]`, sin las que exigen un permiso adicional que quien pregunta no tiene. De ahí arma su selector la
  pantalla; cada vista se publica con `MapVistaDeInventario`, que además valida el rango (422
  `Inventory.Report.RangeInvalid` / `.RangeTooLong`) y audita la exportación con las filas.

| Vista | E | Filtros propios | Permiso adicional | Columnas (· ocultas) |
|---|---|---|---|---|
| `kardex` | I1 | `product` (obligatorio), `warehouse?`, `location?`, `lot?`, `from`, `to` | Costs.Read para valores | Fecha de operación, Registrado, Documento (clase, tipo, número), Motivo, Bodega, Ubicación, Lote/serie, Entrada, Salida, Saldo (cantidad), Costo unitario, Valor entrada, Valor salida, Saldo (valor), Costo promedio · `_documento`, `_producto`, `_bodega` |
| `valuation` | I1 | `asOf`, `warehouse?`, `accountingGroup?`, `category?`, `includeTransit` | Costs.Read | Grupo contable, Bodega, Producto, Unidad, Cantidad, Costo promedio, Valor, En tránsito (cantidad), En tránsito (valor) · `_producto`, `_bodega` |
| `stock` | I1 | `warehouse?`, `category?`, `onlyWithStock` | — | Bodega, Producto, Unidad, Físico, Reservado, Disponible, En tránsito hacia la bodega, Mínimo, Punto de reorden, Máximo · `_producto`, `_bodega` |
| `documents` | I1 | `class?`, `documentType?`, `status?`, `person?` | (PD) si trae contraparte | Fecha, Clase, Tipo, Número, Estado, Bodega, Bodega destino, Contraparte, Total, Modo de paso, Estado del mensaje, Creado por, Confirmado por · `_documento` |
| `count-differences` | I1 | `count?`, `warehouse?` | Costs.Read para valores | Conteo (número, fecha de la foto), Bodega, Ubicación, Producto, Teórico, Contado, Reconteo, Diferencia (cantidad), Costo unitario, Diferencia (valor), Ajuste · `_documento`, `_producto` |
| `reorder-alerts` | I1 | `warehouse?`, `category?` | — | Bodega, Producto, Disponible, En tránsito, Por recibir, Posición, Mínimo, Punto de reorden, Máximo, Sugerido (máximo − posición), Quiebre · `_producto`, `_bodega` |
| `radian-events` | I1 | `supplier?`, `status?` | — | Proveedor, Factura (prefijo y número), CUFE, Emisión, Vencimiento, Forma de pago, 030 (estado, fecha, fuente), 032 (estado, fecha, fuente), Días sin eventos · `_documento` |
| `legacy-comparison-kardex` | I1 | `warehouse`, `product?` | Costs.Read para valores | Producto, Bodega, Fecha, Cantidad SOLIDO, Cantidad módulo, Diferencia (cantidad), Valor SOLIDO, Valor módulo, Diferencia (valor) · `_producto` |
| `legacy-comparison-valuation` | I1 | `asOf`, `warehouse?`, `accountingGroup?` | Costs.Read | Grupo, Bodega, Producto, Cantidad SOLIDO, Valor SOLIDO, Cantidad módulo, Valor módulo, Diferencias · `_producto`, `_bodega` |
| `messages` | I2 | `status?`, `destination?`, `type?`, `batch?` | — | Número, Tipo, Versión, Destino, Estado, Modo, Documento, Fecha de operación, Lote, Intentos, Último error, Procesado, Comprobante · `_mensaje`, `_documento` |
| `accounting-batches` | I2 | `status?`, `trigger?` | — | Lote, Disparador, Programado para, Estado, Rango, Granularidad, Documentos, Mensajes, Comprobantes, Rechazados, Solicitado por, Inicio, Fin, Tarde |
| `reconciliation` | I2 | `asOf` | Reconciliation.View | Sección 1 (por conjunto): Conjunto, Grupos, Cuentas, Valorizado en bodegas, En tránsito, Valorizado total, Saldo contable, Diferencia, Pendientes, En lote, Rechazados, Sin explicar. Sección 2: detalle por bodega (informativo). Sección 3: lo movido por tipos que no pasan. Sección 4: ventas a crédito de esos tipos. Sección 5: bodegas no activas que comparten cuentas, con sus cifras de SOLIDO a la fecha de corte; **suman al valorizado del conjunto**, como en FR-090, y se muestran aparte para identificarlas (la diferencia no se les atribuye: entran con sus cifras importadas; precisión aplicada a la spec, FR-081) · `_mensaje` |
| `sales-by-session` | I3 | `session?`, `cashier?` | (PD) con `groupBy=customer` | Sesión, Punto, Caja, Cajero, Fecha operativa, Documentos, Ventas brutas, Descuentos, Impuestos, Devoluciones, Neto · `_sesion` |
| `sales-by-register` | I3 | `cashRegister?`, `groupBy=day\|customer` | (PD) con `groupBy=customer` | Punto, Caja, Día, Documentos, Ventas brutas, Descuentos, Impuestos, Devoluciones, Neto |
| `sales-by-payment-means` | I3 | `paymentMeans?`, `class?` | (PD) con `groupBy=customer` | Medio, Clase, Punto, Caja, Pagos, Recibido, Reintegrado, Neto |
| `cash-session` | I3 | `session` (obligatorio) | CashSessions.ViewAll si la sesión no es propia | Sección por medio: Base, Ventas, Devoluciones, Movimientos, Reclasificaciones, Esperado, Contado, Diferencia, Tolerancia, Tratamiento, Motivo. Sección de tarjetas: Adquirente, Datáfono, Lote, Pagos, Total. Sección de movimientos · `_documento`, `_sesion` |
| `day-close` | I3 | `pointOfSale`, `operatingDate` o `dayClose` | CashSessions.ViewAll para ver sesiones ajenas | Medio, Esperado, Contado, Diferencia; Adquirente, Datáfono, Lote, Total; Sesiones · `_sesion` |
| `card-payments` | I3 | `acquirer?`, `terminal?`, `network?` | (PD) | Fecha, Documento, Punto, Caja, Cliente, Medio, Franquicia, Adquirente, Datáfono, Lote, Aprobación, Últimos 4, Valor, Comisión esperada · `_documento` |
| `cash-movements` | I3 | `kind?` | — | Fecha, Documento, Sesión, Tipo, Medio origen, Medio destino, Destino, Caja destino, Valor, Motivo, Estado, Aprobado por · `_documento`, `_sesion` |
| `cash-differences` | I3 | `cashier?`, `treatment?` | — | Sesión, Cajero, Medio, Esperado, Contado, Diferencia, Tolerancia, Tratamiento, Motivo, Aprobado por, Documento, Estado · `_documento`, `_sesion` |
| `voucher-redemptions` | I3 | `paymentMeans?`, `status?` | (PD) | Medio, Número de bono, Documento, Fecha, Cliente, Valor, Estado, Liberado por · `_documento` |
| `discount-approvals` | I3 | `approver?` | — | Fecha, Documento, Producto, Precio de lista, Descuento (%), Descuento (valor), Tope de quien pidió, Pidió, Aprobó, Método, Estado · `_documento` |
| `impairment` | I3 | `asOf`, `warehouse?` | Costs.Read | Producto, Bodega, Cantidad, Costo unitario, Precio de la lista general, Gastos de venta (`Informes.DeterioroPorcentajeGastosVenta`), Valor neto realizable, Indicio (valor), Motivo (costo sobre VNR, vencido o próximo a vencer (I6), sin movimiento (I6)) · `_producto` |
| `dian-documents` | I4 | `status?`, `kind?`, `contingency?` | — | Tipo, Prefijo, Número, Fecha, Contraparte, Total, Estado, Contingencia, Código único, Intentos, Plazo, Canal, Último mensaje · `_documento` |
| `purchase-matches` | I5 | `supplier?`, `status?` | — | Orden, Recepción, Factura, Producto, Pedido, Recibido, Facturado, Precio pedido, Precio facturado, Diferencia (cantidad), Diferencia (precio), Dentro de tolerancia, Estado · `_documento` |
| `method-change-valuation` | I5 | `asOf`, `comparativeFrom` | Costs.Read | Grupo contable, Fecha, Valor promedio ponderado, Valor PEPS, Diferencia, Nota (por qué no se pudo calcular) |
| `margin` | I6 | `by=product\|category\|salesperson\|customer\|point` | Costs.Read; (PD) con `by=customer` | Dimensión, Ventas netas, Costo de venta, Margen, Margen (%) |
| `turnover` | I6 | `by=product\|category\|accountingGroup` | Costs.Read | Dimensión, Costo de venta del período, Inventario promedio, Rotación, Días de inventario |
| `abc` | I6 | `basis=sales\|consumption` | — | Producto, Valor, Participación (%), Acumulado (%), Clase (con `Informes.UmbralesAbc`) · `_producto` |
| `no-movement` | I6 | `days?` (por defecto `Informes.DiasSinMovimiento`) | Costs.Read para valores | Producto, Bodega, Último movimiento, Días sin movimiento, Cantidad, Valor · `_producto`, `_bodega` |
| `expiring` | I6 | `days?` (por defecto `Informes.DiasProximoAVencer`) | Costs.Read para valores | Producto, Lote, Vence, Días, Bodega, Cantidad, Valor · `_producto`, `_lote`, `_bodega` |
| `purchase-suggestion` | I6 | `warehouse?`, `supplier?` | Costs.Read para el último costo | Producto, Bodega, Posición, Mínimo, Punto de reorden, Máximo, Sugerido, Último costo, Proveedor habitual · `_producto` |
| `shrinkage-cap` | I6 | `year` | Costs.Read | Inventario inicial, Compras del año, Base, Tope legal (%), Tope, Faltantes y mermas, Exceso |

«Posición» = disponible + en tránsito + por recibir. El sugerido es máximo − posición cuando la
posición es menor o igual al punto de reorden. «Quiebre» es disponible por debajo del mínimo (US17).

`shrinkage-cap` es opcional, para el régimen ordinario de renta. Su porcentaje sale del parámetro
`Informes.TopeFaltantesPorcentaje` (fracción, por defecto 0, con vigencia y `LegalSource`; data-model
§4.2); con 0 la vista dice que el tope no está parametrizado.

Documentos QuestPDF propios, que no son `TablaExportable`:

- `SalesDocumentReport`: el comprobante no electrónico en carta (§20.3).
- `CashCountReport` y `CashMovementReceiptReport` (§21).
- `RepresentacionGraficaReport`, en carta y en 80 mm (I4), que se guarda como adjunto y se baja por
  enlace.

## 28. Tablero — `GET /api/inventory/dashboard` (I6)

`GET /api/inventory/dashboard?branch=&warehouse=&asOf=` · `Inventory.Dashboard.View` ·
`GetInventoryDashboardQuery`. El alcance por bodega aplica.

Respuesta:

```
InventoryDashboardDto {
  asOf, scope: { branches[], warehouses[] },
  tiles: [{ key, label, value, previousValue?, unit: Money | Quantity | Days | Percent | Count,
            severity?: Info | Warning | Critical, link: { page, query } }],
  withoutRecipient: [{ alertTypeCode }] }
```

`unit` y `severity` son textos de presentación; `severity` usa los nombres de `AlertSeverity`.

| `key` | Qué muestra | Lleva a |
|---|---|---|
| `inventoryValue` | valor del inventario, con tránsito | `valuation` |
| `turnover`, `inventoryDays` | rotación y días de inventario | `turnover` |
| `grossMargin` | margen bruto del mes | `margin?by=category` |
| `salesToday`, `salesMonth` | ventas del día y del mes, con el período anterior en `previousValue` | `sales-by-register` |
| `belowReorder`, `stockouts` | bajo punto de reorden y quiebres | `reorder-alerts` |
| `expiringSoon` | próximos a vencer | `expiring` |
| `messagesPending`, `messagesRejected` | mensajes pendientes y rechazados | `/inventario/bandeja-de-mensajes?status=` |
| `lateBatches` | lotes que no corrieron a su hora | `accounting-batches` |
| `dianPending`, `dianRejected` | documentos DIAN pendientes y rechazados | `/ventas/documentos-electronicos?status=` |
| `fiscalTypesNotPosted` | tipos fiscales configurados para no pasar a contabilidad (FR-088) | `/inventario/parametros` |
| `alertsPending` | alertas pendientes | `/inventario/alertas` |

Las fichas de valor y margen exigen `Inventory.Costs.Read`; sin él no salen. `withoutRecipient` lista
los tipos de alerta levantados sin destinatario activo, que se enrutaron a `CompanyAdmin` (SC-022).

## 29. Cambios en rutas existentes

- `POST /api/accounting/periods/{year}/{month}/close` (`Accounting.Periods.Close`) acepta un cuerpo
  opcional `{ acknowledgeInventoryPending }` (`ClosePeriodCommand.AcknowledgeInventoryPending`).
  - Si hay entregas a Contabilidad `Pending`, `InBatch` o `Rejected` con fecha de operación en el mes y
    no viene el reconocimiento → 422 `Accounting.Period.InventoryPending`, con `data: { pending,
    inBatch, rejected, oldestOperationDate, types[] }` (contracts/contabilidad.md).
  - La pantalla ofrece «Procesar ahora», que ordena el lote de §26.4, o «Cerrar de todos modos», que
    queda auditado.
- `POST /api/accounting/documents/{id}/reverse` (009): un comprobante de origen `INV` sigue
  respondiendo 422 `Accounting.Document.ModuleOwned`, con un mensaje propio. Lo de Inventario se corrige
  anulando el documento o con su nota, y llega como comprobante nuevo; en un resumido, el mensaje dice
  cuántos documentos reúne. `PrepareReversalAsync` rechaza el origen `INV` con
  `Accounting.Document.InventoryCorrectsWithNewVoucher`.
- `GET /api/accounting/documents/{id}` (009): el enlace de origen (`EnlacesDeOrigen`) resuelve
  `InventoryDocument` a la página del documento en Inventario, e `InventoryPostingBatch` a
  `/contabilidad/inventario/lotes/{id}`.
- Rutas de tarifas de cuentas de impuesto (009): `rate` admite hasta 6 decimales
  (`ACC_AccountTaxRates.Rate` pasa a (9,6), T19).
- Eliminar una cuenta que usa la matriz responde el error de referencias de la 009, que ahora nombra la
  matriz (`AccountReferenceFinder`). `ListInvalidParameterizationsQuery` suma las reglas cuyas cuentas
  dejaron de ser elegibles.
- `/api/reports/inventory/valuation/pdf` y `/api/reports/inventory/invoice/{id}/pdf` se retiran **sin
  alias**. `/api/reports/inventory/valuation` pasa a ser la vista de §27.
- `GET /api/catalogos/{catalogo}/codigo/{codigo}` suma los slugs `medios-de-pago`, `franquicias`,
  `adquirentes`, `datafonos`, `puntos-de-venta`, `cajas`, `listas-de-precios` y `promociones` (I6).
- `GET /api/admin/permissions/mine` devuelve los códigos nuevos de `decisiones-transversales.md` §2.10 sin
  cambio de forma.
- **Personas (FR-011, enmienda de la 008)**: `POST/PUT /api/core/people` y los compuestos `with-person`
  (`/api/payroll/employees/with-person`, `/api/core/associates/with-person` y los que se sumen) reciben
  en `PersonInput` el perfil tributario: `isVatResponsible`, `isSelfWithholder`,
  `isVatWithholdingAgent`, `isSimpleTaxRegime`, `isIncomeTaxFiler`, `isObligatedToInvoice`,
  `isLargeContributor`, `withholdingExempt`, `icaWithholdingExempt` y `ciiuCode`. Las seis marcas
  nuevas se escriben **sólo** por `PersonInput`/`PersonaDialog`, como el resto de la persona. Las diez son
  anulables en el contrato: nula = no cambia (un `PUT` sin ellas no las borra; al crear, nula es falso), y
  `ciiuCode` vacío quita el CIIU (4 a 6 dígitos). Además
  aceptan `authorization?: { decision: Accepted | Declined, policyVersionPublicId, channel }`, la
  autorización de tratamiento de datos capturada al crear (`AutorizacionAlCrear`), que se escribe en el
  mismo `SaveChanges` que la persona (`HabeasDataConsent.Action = Declined` cuando se niega).
- `GET /api/compliance/habeas-data/policies/current` (`Core.People.Create`) → `{ policyVersionPublicId,
  version, text, publishedAt }`, la política que el POS y Compras muestran al crear una persona. Sin
  política publicada → 404 `Generic.NotFound`, y la pantalla muestra el aviso «sin política vigente»
  y deja crear la persona sin autorización.
- `POST/PUT /api/core/branches` (`Endpoints/Core/BranchesEndpoints.cs`; el contrato decía `/api/accounting/branches`,
  que no existe: corregido en T178) aceptan `municipalityDaneCode`, el municipio DIVIPOLA de la
  sucursal, que ReteICA de compras propone (FR-050) y que contracts/plantillas.md §0.2 exige antes de
  cargar. Se valida contra `COR_Cities.DaneCode` → si no existe, 422 `Branch.MunicipalityUnknown`. En el `PUT`,
  `municipalityDaneCode` nulo no cambia el municipio y vacío lo quita; los fallos de estas dos rutas responden con el
  sobre canónico (antes el `PUT` respondía 404 a cualquier fallo).
- `POST /api/audit/integrity/verify` (`AuditLog.VerifyIntegrity`; `VerifyAuditIntegrityQuery`, una
  consulta sin clave de operación, FR-008). Cuerpo `{ from, to, stream? }`: sin `stream`, la cadena de
  10 años de la cooperativa. Respuesta `{ stream, fromSeq, toSeq, checked, anchorsChecked, incidents:
  [{ kind: Altered | Deleted | Interleaved | AnchorInvalid | PurgedByRetention, seq, eventId?,
  occurredAt? }] }`. La verificación misma queda registrada con el evento `AuditLog.IntegrityVerified` y
  su resultado. Un rango de más de 10 años → 400 `Validation.Invalid`.

## 30. Catálogo tributario — `/api/core` (`TaxesEndpoints.cs`, I1)

Es de Core (T22) y lo reutilizan compras, ventas, la DIAN y los módulos que vengan. Permisos
`Core.Taxes.View` (consulta y plantilla vacía) y `Core.Taxes.Manage` (escritura e importación); aquí se
escriben completos. Lo lee un solo lector, `LectorDeCatalogoTributario`, y ningún valor legal está en
el código (FR-013). Es la plantilla 1 del orden de carga (contracts/plantillas.md §1), la primera que
llena la cooperativa.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /taxes?kind=&active=` | Core.Taxes.View | `[TaxDefinitionDto { taxPublicId, code, name, kind, calculationForm, taxedOnTaxPublicId?, isWithholding, dianTaxCode?, isActive, notes? }]`. `kind` es `TaxKind`: `Iva` (1), `Inc` (2), `ReteFuente` (3), `ReteIva` (4), `ReteIca` (5), `Ica` (6), `Other` (99); `calculationForm` es `TaxCalculationForm`: `PercentOfBase` (1), `PercentOfTax` (2), `AmountPerUnit` (3) |
| `GET /taxes/{id}` | Core.Taxes.View | `TaxDefinitionDto` + `rates: [TaxRateDto]` (todas las vigencias) |
| `POST /taxes` | Core.Taxes.Manage | `{ code, name, kind, calculationForm, taxedOnTaxPublicId?, isWithholding, dianTaxCode?, notes?, reason }` → 201 `{ taxPublicId }`. `PercentOfTax` exige `taxedOnTaxPublicId`. Código repetido → `Catalogo.CodigoDuplicado` |
| `PUT /taxes/{id}` | Core.Taxes.Manage | `{ name, dianTaxCode?, isActive, notes?, reason }`. El código, la clase y la forma de cálculo no cambian |
| `GET /tax-rates?tax=&asOf=&municipality=&reviewPending=&onlyCurrent=` | Core.Taxes.View | `[TaxRateDto { taxRatePublicId, taxPublicId, code, name, rate?, amountPerUnit?, withholdingConceptPublicId?, municipalityDaneCode?, activityCode?, minimumBaseUvt?, minimumBasePesos?, conditions: { subjectPersonType?, subjectIsIncomeTaxFiler?, subjectIsVatResponsible?, subjectIsLargeContributor?, subjectIsSelfWithholder?, subjectIsSimpleTaxRegime?, agentIsLargeContributor?, agentIsVatWithholdingAgent? }, appliesTo, priority, validFrom, validTo?, legalSource, reviewPending, notes? }]`. `rate` es fracción; `appliesTo` es `TaxAppliesTo`: `Purchases` (1), `Sales` (2), `Both` (3) |
| `GET /tax-rates/{id}` | Core.Taxes.View | `TaxRateDto` + las otras vigencias del mismo `code` |
| `POST /tax-rates` | Core.Taxes.Manage | `TaxRateDto` sin id ni `reviewPending`, con `reason` → 201 `{ taxRatePublicId }`. Una vigencia nueva de una tarifa existente es **otra fila** con el mismo `code`; si se cruza con otra del mismo código → 422 `Core.TaxRate.Overlaps` (`data: { taxRatePublicId, validFrom, validTo }`). Si empata con otra candidata (misma definición, concepto, municipio, actividad, condiciones y prioridad, con vigencias cruzadas) → 422 `Core.TaxRate.Ambiguous`, que nombra las dos |
| `PUT /tax-rates/{id}` | Core.Taxes.Manage | corrige una tarifa que todavía no entra en vigencia (mismo cuerpo). Desde `validFrom` no se edita → 422 `Core.TaxRate.InEffect`: se cierra y se crea otra |
| `POST /tax-rates/{id}/close` | Core.Taxes.Manage | `CloseTaxRateCommand`: `{ validTo, reason }`: cierra la vigencia. `validTo` no puede quedar antes de `validFrom` (400 `Validation.Invalid`) |
| `POST /tax-rates/{id}/review` | Core.Taxes.Manage | `ReviewTaxRateCommand`: `{ reason }`: baja `ReviewPending` («pendiente de validar por la contadora», A8), auditado con el motivo. No cambia la tarifa |
| `GET /withholding-concepts?active=` | Core.Taxes.View | `[{ withholdingConceptPublicId, code, name, isActive, notes? }]` |
| `POST /withholding-concepts` | Core.Taxes.Manage | `{ code, name, notes?, reason }` → 201. Código repetido → `Catalogo.CodigoDuplicado` |
| `PUT /withholding-concepts/{id}` | Core.Taxes.Manage | `{ name, isActive, notes?, reason }`. Un concepto usado por tarifas vigentes no se inactiva → 422 `Core.WithholdingConcept.InUse` |
| `GET /taxes/template.xlsx[?withData=true]` | Core.Taxes.View | hojas `Conceptos`, `Impuestos` y `Tarifas`, columnas en contracts/plantillas.md §1 |
| `POST /taxes/import?mode=review\|apply` | Core.Taxes.Manage | `ImportTaxCatalogCommand`, con la mecánica de contracts/plantillas.md §0.5 y las mismas reglas que el alta una a una → `ImportResultDto`; si `apply` falla, 422 `Import.Invalid` |

- **Una tarifa no se edita desde que entra en vigencia**: tarifa, valor por unidad, bases, condiciones y
  alcance quedan fijos. Core no lee documentos para saber si una tarifa «ya se usó»; la foto de lo
  aplicado está en cada documento (`INV_DocumentTaxLines`).
- **Códigos**: el `code` de la tarifa es la dimensión `TaxRateCode` de la matriz contable (T27) y no
  cambia; un código distinto es otra tarifa.
- **Referencias**: una definición, tarifa o concepto que no existe → 404 `Core.Tax.NotFound`,
  `Core.TaxRate.NotFound` o `Core.WithholdingConcept.NotFound`.

Errores: `Core.Tax.NotFound`, `Core.TaxRate.{NotFound, Overlaps, InEffect, Ambiguous}`,
`Core.WithholdingConcept.{NotFound, InUse}`, `Catalogo.CodigoDuplicado`, `Import.Invalid`.

## 31. Vendedores — `/api/inventory/salespeople` (`SalespeopleEndpoints.cs`, I1)

El rol vendedor (FR-031, FR-092) se crea y se retira **sólo** por la operación que escribe a la vez la
fila de `INV_Salespeople` y la marca `IsSalesperson` de la persona (feature 008): desde esta pantalla o
desde la plantilla 9 (contracts/plantillas.md §9). La persona no se crea ni se modifica aquí: se da de
alta en `PersonaDialog`.

| Ruta | Permiso | Cuerpo / respuesta |
|---|---|---|
| `GET /?search=&includeRetired=` | Salespeople.View | paginado de `SalespersonDto { salespersonPublicId, person: { personPublicId, name, idNumber }, salespersonType, appliesCommission, isActive }` |
| `POST /` | Salespeople.Manage | `CreateSalespersonCommand`: `{ personPublicId, salespersonType?, appliesCommission?, reason? }`. Si la persona tiene una fila retirada, la **restaura** con el mismo `salespersonPublicId` (los documentos que la citan siguen apuntando a ella); si no, crea una. Pone `IsSalesperson = true` en el mismo `SaveChanges` → 201 `{ salespersonPublicId, restored }`. Si ya tiene el rol vivo → 422 `Inventory.Salesperson.AlreadyActive`; persona inexistente o eliminada → 404 `Core.Person.NotFound` |
| `PUT /{id}` | Salespeople.Manage | `{ salespersonType, appliesCommission, reason? }` |
| `POST /{id}/retire` | Salespeople.Manage | `DeleteSalespersonCommand`: `{ reason }`. Da de baja la fila (retiro suave) y apaga `IsSalesperson` en el mismo `SaveChanges` |
| `GET /template.xlsx[?withData=true]` | Salespeople.View | columnas en contracts/plantillas.md §9; con datos exige además `Inventory.Reports.ExportPersonalData` |
| `POST /import?mode=review\|apply` | Salespeople.Manage | `ImportSalespeopleCommand`: cita a la persona por su documento y rechaza la fila si no existe; `activo = no` retira con `DeleteSalespersonCommand` y `sí` sobre un rol retirado lo restaura con `CreateSalespersonCommand`. Si `apply` falla, 422 `Import.Invalid` |

Errores: `Inventory.Salesperson.AlreadyActive`, `Core.Person.NotFound`, `Import.Invalid`.

## 32. Rutas que este contrato agrega a la tabla de prefijos

La tabla de prefijos es `decisiones-transversales.md` §2.9: nombra cada prefijo y su archivo de
endpoints. Las acciones que cuelgan de esos prefijos y que la tabla no enumera son éstas; la lista
completa de rutas es este contrato, no la tabla:

- **Ventas**:
  - `sales/documents/{id}/deliver`
  - `sales/documents/{id}/invoice-instead` (I4, §18.3.1)
  - `sales/documents/{id}/credit`
  - `sales/credit-evaluations`
  - `sales/quotes/{id}/to-order` (I6)
- **POS**:
  - `pos/drafts/{id}` (`GET`/`PATCH`, `UpdatePosDraftCommand`)
  - `pos/drafts` (`GET`, suspendidas del punto)
- **Caja**:
  - `cash-sessions/{id}/recount`
  - `cash-sessions/{id}/count-report`
  - `cash-movements/{id}/{confirm, void, discard, receipt}`
  - `day-closes/{id}`
- **Precios**:
  - `price-lists/{id}/items`
  - `discount-caps/mine`
- **Puntos de venta y medios**:
  - `points-of-sale/{id}/cash-registers/{registerId}`
  - `points-of-sale/template.xlsx|import`
  - `payment-means/{id}/availability` (en `/api/inventory`)
  - `payment-means/template.xlsx|import` (en `/api/core`)
- **Documentos**: `documents/{id}/prevalidate`.
- **Facturación electrónica**:
  - `resolutions/{id}` (`GET`/`PUT`)
  - `contingencies/{id}` (`GET`)
  - `documents/{id}/replacement-draft`
  (la emisión RADIAN es de compras: §14.8, `radian-events/emit`)
- **Contabilidad**:
  - `rules/catalog`
  - `rules/{id}/deactivate`
- **Tablero**: `/api/inventory/dashboard`.
- **Catálogo tributario** (§30, en `/api/core`, `TaxesEndpoints.cs`): `taxes`, `taxes/{id}`,
  `tax-rates`, `tax-rates/{id}`, `tax-rates/{id}/{close, review}`, `withholding-concepts`,
  `withholding-concepts/{id}`, `taxes/template.xlsx|import`.
- **Vendedores** (§31, `SalespeopleEndpoints.cs`): `salespeople`, `salespeople/{id}`,
  `salespeople/{id}/retire`, `salespeople/template.xlsx|import`.
- **Rutas existentes** (§29): `municipalityDaneCode` en `/api/core/branches` no agrega ruta;
  `/api/compliance/habeas-data/policies/current` y `/api/audit/integrity/verify` ya están en la tabla.

Nombres nuevos que no están en `decisiones-transversales.md` §2.16:

- **Comandos y consultas**: `UpdatePosDraftCommand`, `RecountCashSessionCommand`,
  `EvaluateSaleCreditQuery`, `SetPaymentMeansAvailabilityCommand`, `ImportPaymentMeansCommand`,
  `GetInventoryDashboardQuery` y, del catálogo tributario, `CloseTaxRateCommand` y
  `ReviewTaxRateCommand`.
- **DTOs**: `TaxDefinitionDto`, `TaxRateDto`, `SalespersonDto`.
- **Puerto**: un método de `IFuenteDeDocumentoElectronico` para crear el borrador de reemplazo.

Los errores nuevos siguen la forma `Área.Recurso.Motivo` y se enumeran al final de cada sección.
