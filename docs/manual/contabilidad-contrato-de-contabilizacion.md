# Cómo contabiliza un módulo: el contrato de contabilización

> Feature 009 (contabilidad NIIF), entrega E1. Complementa
> [specs/009-contabilidad-niif/contracts/contabilizacion.md](../../specs/009-contabilidad-niif/contracts/contabilizacion.md),
> que es el contrato formal; este documento es la receta para el equipo.

Hasta la feature 009 había dieciséis sitios que escribían en las tablas contables, doce de
ellos con cabecera y sin líneas, cada uno con su numeración y sin ninguna regla de cuenta.
Desde la 009 **hay un solo camino al libro**: `AccountingPoster`
(`Application/Accounting/Posting`). Nadie más instancia un `AccountingDocument` ni una
`JournalEntry`, ni toca `VoucherType.NextNumber`. Lo vigilan dos pruebas de arquitectura:
`NingunModuloEscribeMovimientosFueraDelContrato` y `PrincipioXI_ContableImmutable`.

## 1. La regla en una frase

**El módulo decide qué contabilizar (cuentas, valores, terceros); el contrato decide si se
puede y cómo queda escrito.** El módulo no abre transacciones, no numera, no escribe saldos
(no existen: todo saldo es una suma sobre `ACC_JournalEntries`) y no edita ni borra nada
contabilizado.

## 2. El molde (lo que hace Nómina)

```csharp
public sealed class MiModuloAccountingPoster(IApplicationDbContext db, AccountingPoster poster)
{
    public async Task<Result<AccountingDocument>> PostAsync(MiDocumento doc, DateOnly fecha, CancellationToken ct)
    {
        // 1. Cuentas: de la parametrización del módulo, resueltas por Id o por código.
        //    Si falta una, se devuelve un error del módulo y no se toca el contexto.
        // 2. Líneas: una por (cuenta, tercero, centro, documento cruce); débito o crédito, nunca ambos.
        var lineas = new List<PostingLine>
        {
            new() { AccountId = cuentaDebito.Id, Debit = valor, Detail = "…", PersonId = tercero, CostCenterId = centro },
            new() { AccountId = cuentaCredito.Id, Credit = valor, Detail = "…", PersonId = tercero },
        };
        // 3. Al contrato: tipo de comprobante DEL MÓDULO, fecha de la operación, origen (módulo, tipo y PublicId del documento).
        return await poster.PrepareAsync(
            new PostingRequest("XX", fecha, "Descripción", new AccountingOrigin(ModuloContable.MiModulo, "MiDocumento", doc.PublicId), lineas), ct);
    }

    public Task<Result<AccountingDocument>> ReverseAsync(AccountingDocument original, DateOnly fecha, string motivo, CancellationToken ct) =>
        poster.PrepareReversalAsync(original, fecha, motivo, new AccountingOrigin(ModuloContable.MiModulo, "MiDocumento", original.SourcePublicId ?? Guid.Empty), ct);
}
```

Y en el comando del módulo:

```csharp
var posting = await contabilizador.PostAsync(documento, fecha, ct);
if (posting.IsFailure) return Result.Failure(posting.Error);   // nada se agregó al contexto
documento.Estado = Aprobado;
await db.SaveChangesAsync(ct);                                  // operación + comprobante, o nada
```

El comando se marca `IReintentableAnteConcurrencia`: el consecutivo se toma en memoria y su
`RowVersion` convierte una carrera de numeración en un reintento del comando completo.

## 3. Qué comprueba el contrato (y qué error da)

En este orden; las de encabezado cortan, las de línea se acumulan y llegan juntas.

| # | Comprobación | Error |
|---|---|---|
| 1 | Contabilidad iniciada | `Accounting.NotInitialized` |
| 2 | Tipo de comprobante activo y del uso correcto (manual sólo desde Contabilidad; de módulo sólo desde su módulo) | `Accounting.VoucherType.NotFound` / `.Inactive` / `.NotAllowedForModule` |
| 3 | Fecha en un período existente y abierto; «posterior a hoy» sólo se rechaza al digitar (un módulo fecha según su operación) | `Accounting.Period.NotFound` / `.Closed` / `Accounting.Date.InFuture` |
| 4 | Al menos dos líneas | `Accounting.Document.TooFewLines` |
| 5 | Cuenta existe, es de movimiento, activa y habilitada para el módulo | `Accounting.Line.AccountNotFound` / `.AccountNotMovement` / `.AccountInactive` / `.AccountNotEnabledForModule` |
| 6 | Importe: débito o crédito, mayor que cero, nunca ambos | `Accounting.Line.AmountInvalid` |
| 7 | Sucursal: toda línea lleva una (la propuesta si no viene); «exige sucursal» pide una explícita; con alcance restringido (sólo al digitar) tiene que ser permitida | `Accounting.Line.BranchRequired` / `.BranchInvalid` / `.BranchOutOfScope` |
| 8 | Tercero si la cuenta lo exige; vigente si viene | `Accounting.Line.ThirdPartyRequired` / `.ThirdPartyInvalid` |
| 9 | Documento cruce si la cuenta lo exige; tipo activo | `Accounting.Line.CrossDocumentRequired` / `.CrossDocumentTypeInvalid` |
| 10 | Centro de costo: un módulo lo manda siempre y la cuenta decide si lo conserva; al digitar, ponerlo en una cuenta que no lo maneja es error | `Accounting.Line.CostCenterRequired` / `.CostCenterNotAllowed` / `.CostCenterInvalid` |
| 10b | Base gravable y valor ≈ base × tarifa vigente (con la tolerancia de la empresa); la diferencia pequeña es aviso, la grande error | `Accounting.Line.TaxBaseRequired` / `.TaxAmountDiffers` (aviso) / `.TaxAmountMismatch` / `.TaxRateMissing` |
| 11 | Cuadre | `Accounting.Document.Unbalanced` |
| 12 | Número y construcción: líneas con fecha y `IsPosted`; `FirstMovementAt` de cada cuenta | — |

Un solo error de línea llega tal cual; varios llegan como `Accounting.Document.Invalid` con
`data.errors[]` (`lineNumber`, `field`, `code`, `message`, `severity`). Los avisos no
bloquean. El `ErrorEnvelopeFilter` serializa `data` para que la pantalla señale la celda.

## 4. Terceros (FR-088)

El tercero de una línea es una **persona** (`COR_People`). En Nómina: el empleado en
devengos y deducciones; en aportes y provisiones, la persona vinculada a la EPS, ARL, fondo
o caja del empleado (`TercerosDeNomina`). Las entidades institucionales se vinculan a su
persona en su propia pantalla («Persona que la representa como tercero»); si una cuenta
exige tercero y la entidad no tiene vínculo, la aprobación falla nombrándola
(`Accounting.Line.ThirdPartyRequired` con `data.entity`) y la parametrización de cuentas por
concepto lo rechaza antes (`Accounting.Parameterization.InstitutionalLinkMissing`).
`Contabilidad › Parametrizaciones inválidas` lista lo que va a fallar.

## 5. Reversión

Lo contabilizado no se edita ni se borra (Principio XI). La corrección es
`PrepareReversalAsync`: un documento `Reversal` del mismo tipo con las líneas invertidas y la
referencia en ambos sentidos; el original queda `Reversed`. **Sólo el módulo dueño reversa lo
suyo**: desde Contabilidad, un `NM` responde `Accounting.Document.ModuleOwned` con
`data.origin`, y la pantalla ofrece «Ver en Nómina». Si la fecha cae en un mes cerrado, la
reversión se fecha en el primer período abierto y lo dice en la descripción.

**Excepción: Inventario** (feature 012, D-01). Lo nacido de Inventario **no se reversa**:
`PrepareReversalAsync` rechaza, antes de cualquier otra comprobación, un original de origen `INV`
o una petición de origen `INV` con `Accounting.Document.InventoryCorrectsWithNewVoucher`. Cada
anulación, nota o ajuste de Inventario llega como un **comprobante nuevo** de su propio mensaje,
con su fecha, enlazado al original, y el original nunca se marca `Reversed` (ver §8). Lo vigila
`LoDeInventarioNoSeReversa`.

**Excepción: Cartera** (feature 013, enmienda E-01). Lo nacido de Cartera **tampoco se reversa**:
`PrepareReversalAsync` rechaza, antes de cualquier otra comprobación (también antes de pedir el motivo), un original o
una petición de origen `CAR` con `Accounting.Document.LendingCorrectsWithNewVoucher`, y reversar desde Comprobantes un
comprobante de Cartera responde `Accounting.Document.ModuleOwned` con un mensaje propio (en un resumido, cuántos
créditos reúne). Se corrige en Cartera y llega como comprobante nuevo (ver §9). Lo vigila
`LoDeCarteraSeCorrigeConComprobanteNuevo`.

## 6. Qué NO hacer

- No hacer `db.AccountingDocuments.Add(...)` ni `new JournalEntry` fuera de `Accounting/Posting`.
  La prueba de arquitectura lo rechaza; el borrador manual también nace en el contrato
  (`NuevoBorrador`, `NuevaLineaDeBorrador`).
- No tocar `VoucherType.NextNumber` (ni en pruebas de módulo): sólo el contrato numera.
- No usar `Remove`/`RemoveRange` sobre documentos o líneas: las sobrantes de un borrador se
  marcan `IsDeleted`.
- No guardar saldos: se calculan sobre `ACC_JournalEntries` con `IsPosted = true`.
- No inventar un tipo de comprobante en código: el módulo pide el suyo por código (`NM`,
  `DS`, `RC`…) y falla claro si no existe o está inactivo.
- No hacer `SaveChangesAsync` dentro del contabilizador del módulo: quien llama guarda
  todo junto.

## 7. Dónde está cada cosa

| | |
|---|---|
| Contrato | `Application/Accounting/Posting/AccountingPoster.cs`, `PostingRequest.cs`, `AccountingErrors.cs` |
| Reglas puras | `Application/Accounting/Rules/AccountLineRules.cs` |
| Elegibilidad de una cuenta para un módulo | `Application/Accounting/Accounts/AccountEligibility.cs` |
| Molde de módulo | `Application/Payroll/Services/PayrollAccountingPoster.cs` |
| Digitación manual | `Application/Accounting/Documents/` |
| Pruebas del contrato | `tests/…Application.Tests/Accounting/Posting/AccountingPosterTests.cs` |
| Pruebas de arquitectura | `tests/…Architecture.Tests/Principles/NingunModuloEscribeMovimientosFueraDelContrato.cs`, `PrincipioXI_ContableImmutable.cs` |
| Inventario por mensajes (§8) | `Application/Accounting/Inventory/` (`Reglas`, `Contabilizacion`, `Lotes`, `Consultas`), `Application/Common/Integration/`, `API/Integration/DespachadorDeMensajes.cs`; e2e `tests/…API.IntegrationTests/Accounting/ContabilizacionPorMensajesTests.cs` |
| Cartera por hechos (§9) | `Application/Accounting/Lending/` (`Reglas`, `Contabilizacion`, `Lotes`, `Consultas`), lo común de los dos emisores en `Application/Accounting/Integration/` (`DestinoContabilidad`, `ConjuntosDeCuentas`, `RecibosDeContabilidad`), el lado de Cartera en `Application/Lending/Integration/`; e2e `tests/…API.IntegrationTests/Lending/ContabilizacionDeCarteraPorMensajesTests.cs` |

## 8. Inventario: por mensajes

> Feature 012 (inventario comercial), entrega I2. Contrato formal:
> [specs/012-inventario-comercial/contracts/contabilidad.md](../../specs/012-inventario-comercial/contracts/contabilidad.md).
> Es una enmienda de la 009 (D-01): el molde del §2 **no** aplica a Inventario.

Inventario **no escribe asientos ni conoce cuentas**. Al confirmar un documento guarda, en la
misma transacción, el documento y sus **mensajes** (`COR_IntegrationMessages`, por
`EmisorDeMensajes`). Contabilidad los recibe después con su consumidor,
`PostInventoryMessagesCommand` (o `PostInventorySummaryGroupCommand` para un resumido), que arma
las líneas con la matriz y las pasa por el **mismo** `AccountingPoster.PrepareAsync`; comprobante
y recibo (`ACC_InventoryPostings`, único por mensaje) se guardan juntos. Así hay dos unidades
atómicas —documento + mensaje en Inventario, comprobante + recibo en Contabilidad— y sigue
habiendo un solo camino al libro. Un mensaje entregado dos veces no hace un segundo comprobante:
la segunda vez responde «ya procesado». Los mensajes los mueve `DespachadorDeMensajes`, un
trabajo de fondo por cooperativa que escribe sólo enviando comandos.

**La matriz** (`ACC_InventoryPostingRules`, pantalla `/contabilidad/inventario/matriz`, permisos
`Accounting.InventoryRules.View/Manage`): cada regla dice, para una operación y un rol de cuenta
(inventario, costo, ingreso, impuesto, contrapartida…), qué cuenta usar, y puede afinarse por
grupo contable, bodega, sucursal, causa, tipo de documento o punto de venta. Gana la regla más
específica vigente a la fecha del documento (`ResolutorDeReglas`); cambiar una regla es crear una
versión con vigencia, no editarla. Al guardar se valida la cuenta con las mismas reglas de la 009
(de movimiento, activa, habilitada para Inventario). Se carga en bloque con la **plantilla 16**
(`GET /api/accounting/inventory/rules/template.xlsx`, `POST …/rules/import`, todo o nada con fila
y columna del error). El tipo de comprobante por operación y tipo de documento se elige en
`/contabilidad/inventario/tipos-de-comprobante` (`FV`, `EI`, `SI`, `NV`, `CP`, `TR`, `AC`, `CJ`).
`/contabilidad/inventario/completitud` dice qué combinaciones en uso no tienen regla y qué cuenta
de impuesto no tiene tarifa vigente: se revisa **antes** de activar el paso de un tipo.

**Validación previa y quién corrige.** Antes de confirmar, Inventario pregunta a Contabilidad si
el documento es contabilizable (`IContabilidadParaInventario.EvaluarAsync`, que usa
`AccountingPoster.ValidarVariosAsync`: el mismo análisis sin numerar ni guardar). Si no lo es, la
confirmación responde 422 `Inventory.Prevalidation.NotPostable` **sin número ni mensajes**, y
cada error dice la línea del documento, la cuenta, la regla incumplida y **quién corrige** (el
módulo, la página y el permiso: una regla que falta la corrige Contabilidad en la matriz; un
tercero que falta, Inventario en el documento). Si Contabilidad no responde a tiempo
(`Contabilidad.ValidacionPreviaSegundos`), manda la política `Contabilidad.PoliticaSinRespuesta`:
bloquear o confirmar con el mensaje pendiente (aviso `Inventory.Prevalidation.NoResponse`). Una
cooperativa sin contabilidad iniciada no confirma nada que deba pasar en línea
(`Accounting.NotInitialized`): mientras no la inicie, el tipo va en «no pasa».

**Modos de paso** (`Contabilidad.ModoDePaso` por tipo de documento, con vigencia; se **sella** en
cada documento al confirmarlo y cambiarlo después no mueve lo ya confirmado):

- **En línea** (`EnLinea`): el despachador lo procesa en su siguiente pasada; un comprobante por
  documento.
- **Por lotes** (`PorLotes`): espera a un lote, que se dispara a la `Contabilidad.HoraDeLote`
  (hora de Colombia), al cerrar el período de inventario o a mano (`/contabilidad/inventario/lotes`,
  permiso `Accounting.InventoryBatches.Run`, con vista previa). `Contabilidad.Granularidad` elige
  un comprobante por documento o **resumido**: uno por fecha, tipo de comprobante y sucursal, que
  suma sin netear y conserva el detalle de tercero, documento cruce y base; su origen es el lote,
  que lista sus documentos. Un lote que no corre a su hora levanta la alerta
  `Integracion.LoteNoCorrio`.
- **No pasa** (`NoPasa`): el mensaje queda `NotApplicable`; se puede mandar después con
  `POST /api/inventory/messages/send-not-applicable` (permiso `Inventory.Messages.SendNotApplicable`),
  con sus anulaciones en orden.

**Anulaciones, notas y ajustes: comprobantes nuevos.** Anular un documento de Inventario es otro
documento con su fecha; su mensaje produce un comprobante nuevo enlazado al original (y al
resumido, si el original fue parte de uno), que queda intacto. Lo mismo las notas del proveedor y
los ajustes de costo. Nunca `PrepareReversalAsync` (§5).

**Bandeja y reproceso.** Lo que Contabilidad rechaza después de confirmado (una regla cambiada, un
período cerrado) no deshace el documento: queda `Rejected` en la **bandeja de mensajes**
(`/inventario/bandeja-de-mensajes`, `Inventory.Messages.View`) con el motivo y quién corrige.
Corregido, se reprocesa (`POST /api/inventory/messages/reprocess`, `Inventory.Messages.Reprocess`)
con la **fecha original** del documento. Un mensaje que falla por algo transitorio se reintenta
con espera creciente y, si no sale, alerta `Integracion.MensajeSinEntregar`.

**Aviso de cierre.** Cerrar un mes contable con mensajes de Inventario pendientes, en lote o
rechazados con fecha en ese mes responde `Accounting.Period.InventoryPending` con la lista;
cerrarlo igual exige reconocerlo (`AcknowledgeInventoryPending`), y queda auditado. Lo que quede
se recupera reabriendo el mes según la 009 y reprocesando.

**Conciliación** (`/inventario/conciliacion`): compara el valorizado de Inventario con los saldos
de las cuentas de la matriz (`SaldosDeCuentasMapeadasAsync`), por conjunto de cuentas, incluido el
saldo inicial; los pendientes, rechazados y lo que no pasa se muestran como partidas aparte. Con
todo procesado, la diferencia es cero. La misma comparación decide la **activación** de una bodega
(`/inventario/activacion`): una diferencia sólo se acepta con
`Inventory.Warehouses.AcceptActivationDifference` y motivo.

Lo que **no** hay que hacer desde Inventario: leer o escribir tablas `ACC_`, instanciar
`AccountingPoster` o pedir una reversión. Lo vigilan `InventarioNoConoceContabilidadNiCartera`,
`LoDeInventarioNoSeReversa` y `LosComandosDeConsumoNoTienenRuta` (los consumidores no tienen ruta
HTTP: sólo los envía el despachador).

## 9. Cartera: por hechos

> Feature 013 (cartera financiera), fase 11 (US7, entrega K1). Contrato formal:
> [specs/013-cartera-financiera/contracts/contabilidad.md](../../specs/013-cartera-financiera/contracts/contabilidad.md).
> Es la enmienda E-01 de la 009: el molde del §2 **no** aplica a Cartera. La receta para la contadora (cómo llenar la
> matriz, los asientos de ejemplo, la conciliación y lo pendiente de validar) es
> [cartera-integracion-contable.md](cartera-integracion-contable.md).

Cartera, como Inventario, **no escribe asientos ni conoce cuentas**. Cada operación (desembolso, pago, reverso,
causación, cierre…) guarda, en la misma transacción que sus transacciones del libro, sus **hechos** (`CAR → Accounting`
en la bandeja). Contabilidad los recibe por el **mismo y único** consumidor del destino `Accounting`,
`DestinoContabilidad`, que ahora es un **enrutador de dos emisores**: lo de `INV` va a los comandos de Inventario sin
cambios; lo de `CAR`, a `PostLendingFactsCommand` (o a `PostLendingSummaryGroupCommand` en un resumido). El constructor
(`ConstructorDeLineasDeCartera`, sin IO) arma las líneas con la matriz y las pasa por el **mismo**
`AccountingPoster.PrepareAsync`; comprobante y recibo (`ACC_LendingPostings`, único por hecho) se guardan juntos. Un
hecho entregado dos veces no hace un segundo comprobante. Ningún hecho lleva una cuenta
(`LosHechosDeCarteraNoLlevanCuentas`).

**La unidad.** Una operación y su modo forman **un** comprobante. Los acompañantes de un hecho principal (el anticipo,
el cobro de suspenso o el saldo a favor que deja un pago) viajan con él, sellan su modo y salen en su comprobante. El
tipo de comprobante lo da el hecho principal por el mapeo de `/contabilidad/cartera/tipos-de-comprobante`: `DS`
desembolsos y ajustes desde Comercial; `RC` pagos (`DN` cuando el recaudador es Nómina); `CA` causación; `PV`
deterioro, reclasificación y castigo; `CN` (nuevo) novedades, cambios de dimensión y contingentes. El documento cruce
es `PG` + número del crédito, salvo en los roles de Comercial, que cruzan con la factura (`FV`). La fecha del
comprobante es la **fecha contable** que Cartera calculó al emitir (si el mes de la fecha valor está cerrado, el primer
día del primer mes abierto); Contabilidad nunca la corre.

**La matriz** (`ACC_LendingPostingRules`, pantalla `/contabilidad/cartera/matriz`, permisos
`Accounting.LendingRules.View/Manage`). Cada regla dice, para un hecho y un **rol** de cuenta (capital, interés por
cobrar, deterioro, suspenso en orden, puente de recaudo, ingreso…), qué cuenta usar, afinada por las dimensiones de
Cartera **por código**: línea, modalidad, sub-segmento, categoría contable, concepto, empleado, gracia, reestructurado,
por cuenta del asociado, clase de recaudador, motivo y sucursal. Las exigidas del rol se comparan por igualdad; las
opcionales casan por igualdad o `*` y suman peso (línea 512, empleado 256, gracia 128, sub-segmento 64, reestructurado
32, por cuenta del asociado 16, sucursal 4 y, **sólo en los roles de resultado**, el hecho 2): gana la de mayor peso
vigente a la fecha contable, y no hay empates. En los roles de saldo, orden, pasivo, puente, diferido y contingentes el
hecho va **siempre en `*`**: un saldo vive en una sola cuenta, lo mueva quien lo mueva. El centro de costo se **asigna**
a la línea desde la regla ganadora; no se compara ni pesa. Cambiar una regla es una versión con vigencia (nunca en o
antes de lo ya contabilizado: `Accounting.LendingRule.RetroactiveOverPosted`); desactivar pide motivo; nada se borra.
Al guardar se valida la cuenta con las reglas de la 009 (de movimiento, activa, habilitada para CAR). Se carga en bloque
con la plantilla de la matriz (`GET /api/accounting/lending/rules/template.xlsx`, `POST …/rules/import?mode=review|apply`,
todo o nada), que trae **filas sugeridas sin cuenta**; no se siembra ninguna regla. `/contabilidad/cartera/completitud`
lista lo que falta (combinaciones en uso sin regla, recaudadores sin puente, cuentas que dejaron de ser elegibles,
hechos sin tipo de comprobante) y los avisos; «¿a qué cuenta va?» (`POST /api/accounting/lending/rules/resolve`)
muestra la regla ganadora y las candidatas.

**Validación previa.** Antes de confirmar, Cartera pregunta si lo que va a emitir es contabilizable
(`IContabilidadParaCartera.EvaluarAsync`, que usa el mismo constructor y `AccountingPoster.ValidarVariosAsync`: sin
numerar ni guardar), una pregunta por combinación distinta, fuera del cerrojo y con `Contabilidad.ValidacionPreviaSegundos`.
**«Bloquear» sólo muerde en las pantallas de Cartera** (el desembolso; en K3, la novedad, la condonación y el castigo):
con `Contabilidad.FaltaDeRegla = Bloquear` responde 422 `Lending.Prevalidation.NotPostable` con el hecho, el rol, la
combinación, la cuenta, la regla y **quién corrige**; sin respuesta y con `Contabilidad.PoliticaSinRespuesta = Bloquear`,
422 `Lending.Prevalidation.NoResponse`. El contrato de obligaciones (Recaudo, Nómina, Aportes), el consumidor de las
ventas de Comercial y los procesos **nunca se detienen** por la matriz: aplican, sellan el resultado (`NotPostable`,
`NoResponse`) y levantan la alerta `Cartera.HechoNoContabilizable`. Un pago que ya ocurrió no se rechaza por un vacío
de parametrización: queda rechazado en la bandeja y se recupera.

**Modos y lotes** (`Contabilidad.ModoDePaso` por tipo de hecho, en los parámetros de Cartera; se **sella** en cada hecho
al emitirse). Por defecto: en línea casi todo; **por lotes resumido** la causación (disparada al terminar la corrida
diaria: un `CA` por día con la sucursal en cada línea y una línea **por crédito** en las cuentas que exigen tercero o
cruce) y el deterioro y la reclasificación del cierre (disparados por el cierre de Cartera: un `PV`); los reversos
**heredan** el modo de su original; los tres contingentes, «no pasa»; sin contabilidad iniciada y sin un modo guardado,
«no pasa». Los lotes de Cartera se ven en `/contabilidad/cartera/lotes` (`Accounting.LendingBatches.View`), con vista
previa y lote manual (`.Run`, con motivo e `Idempotency-Key`); el origen de un resumido es el lote
(`LendingPostingBatch`), y su detalle (`/contabilidad/cartera/lotes/{id}`) lista hechos, créditos de cada parte,
comprobantes y rechazos. Lo que no pasaba se envía después desde la pestaña «Emitidos» de
`/cartera/bandeja-de-mensajes` («Enviar lo que no pasaba», `Lending.Messages.SendNotApplicable`), con su fecha contable
original, una vez cambiado el modo del tipo.

**Bandeja y reproceso.** Un rechazo después de confirmado (regla que falta, cuenta inactiva, período cerrado) no deshace
la operación: el hecho queda `Rejected` en la bandeja de Cartera con el motivo y quién corrige; corregida la causa, se
reprocesa (`POST /api/lending/messages/reprocess`, `Lending.Messages.Reprocess`) con su fecha contable original. Un mes
que no existe en Contabilidad no rechaza: reintenta y alerta `Contabilidad.PeriodoInexistente`. Un hecho cuyo original
todavía no está contabilizado espera (`Accounting.LendingFact.WaitingForOriginal`).

**Regla de cierre.** Contabilidad **no cierra** un mes que Cartera, activada, no cerró
(`Accounting.Period.LendingNotClosed`, sin reconocimiento posible): primero se cierra en `/cartera/cierres`. Además
avisa, con reconocimiento auditado, de los hechos de Cartera del mes pendientes, en lote o rechazados
(`Accounting.Period.LendingPending`, `AcknowledgeLendingPending`) y de una carga de Cartera aplicada que vencería
(`Accounting.Period.LendingLoadWillExpire`, `AcknowledgeLendingLoad`). Lo sabe por los recibos de los hechos
informativos de Cartera y por las entregas de la bandeja, sin leer tablas de Cartera. `/contabilidad/periodos` muestra
el estado de Cartera del mes y ofrece «Procesar ahora» y «Cerrar de todos modos».

**Comprobantes nuevos, nunca reversión.** Lo de Cartera no se reversa desde Comprobantes (§5): el reverso del pago, del
desembolso o la reapertura del cierre es un **hecho nuevo** (`PagoReversado`, `DesembolsoReversado`, `ReversoDeCierre`)
que produce un comprobante nuevo, con **su** fecha contable y las **dimensiones vigentes** del saldo que restituye: un
pago de un crédito en A reversado después de un cierre que lo pasó a C restituye en C. El original nunca se marca
reversado. Un mapeo equivocado ya contabilizado lo corrige la contadora con un `CG` manual de reclasificación.

**Conciliación** (`/cartera/conciliacion-contable`, `Lending.Reconciliation.View`): compara el saldo de Cartera por fecha
contable con el de las cuentas de la matriz, **por conjuntos de cuentas** (si la contadora lleva dos categorías a una
auxiliar, se comparan juntas), y explica cada diferencia hecho por hecho con lo pendiente, en lote, rechazado o que no
pasa; los puentes deben quedar en cero, y lo que los hechos no explican en ellos es «del otro lado». Con todo
procesado, la diferencia es cero.

**Nómina** (enmienda D-08 de la 010): `DESC_CARTERA` vuelve a contabilizar en el `NM` y en la definitiva, débito
salarios por pagar y crédito el puente «recaudos por nómina para Cartera»; Cartera contabiliza su lado en `DN` contra el
mismo puente.

Lo que **no** hay que hacer desde Cartera: leer o escribir tablas `ACC_`, llamar a `AccountingPoster`, elegir el tipo de
comprobante o pedir una reversión. Lo vigilan `CarteraNoConoceContabilidad` (en las dos direcciones),
`LosHechosDeCarteraNoLlevanCuentas`, `LoDeCarteraSeCorrigeConComprobanteNuevo`, `UnSoloConsumidorPorDestino` y
`LosComandosDeConsumoNoTienenRuta`.
