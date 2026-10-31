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
| Captaciones y aportes por hechos (§10) | roles y hechos en `Application/Accounting/Lending/Reglas/` (`RolesDeCuentaDeCartera`, `OperacionesDeCartera`), el constructor en `Application/Accounting/Lending/Contabilizacion/ConstructorDeLineasDeCartera.Captaciones.cs`, las sugerencias CUIF en `Application/Accounting/Lending/Plantilla/sugerencias-cuif-captaciones.json`, el lado de Cartera en `Application/Lending/Integration/` (`ValidacionPreviaDeCaptaciones`, `HechosRepresentativosDeCaptaciones`, `SaldosContablesDeCaptaciones`); e2e `tests/…API.IntegrationTests/Lending/Deposits/ContabilidadDeCaptacionesTests.cs` |

## 7a. Activos (E4, 2026-10-30)

Activos (`ACT`) contabiliza **en línea por el contrato**, como Nómina: la corrida mensual (`RunAssetDepreciationCommand`) y la baja
(`RetireFixedAssetCommand`) arman un `PostingRequest` con tipo `DP` y origen `ACT` (`SourceType` `AssetRun` o `FixedAsset`) y
guardan el comprobante con su operación en un solo `SaveChanges`. La reversión de la corrida la hace Activos por
`PrepareReversalAsync`; desde Comprobantes es `Accounting.Document.ModuleOwned`. Recaudo y Tesorería (015) y Captaciones (014)
contabilizan por hechos (§9, §10) y ningún comando de E3 quedó por migrar: E3 se cerró con las features 012 a 015.

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
Con `Contabilidad.FaltaDeRegla = Advertir` toda operación confirma, sella el resultado (`NotPostable`,
`NoResponse`) y levanta la alerta `Cartera.HechoNoContabilizable`; lo que no se contabilizó queda rechazado en la bandeja
y se recupera reprocesando. Con `Bloquear` —el defecto desde el 2026-10-14— **se detiene todo** (A8 = «Bloquear todo», respuesta del dueño del
2026-10-14): las pantallas de Cartera responden 422 `Lending.Prevalidation.NotPostable` con el hecho, el rol, la
combinación, la cuenta, la regla y **quién corrige**; el contrato de obligaciones rechaza el pago con ese código (Nómina
no aprueba: `Payroll.LoanDeductionNotPostable`); el consumidor de Comercial deja la venta en reproceso; y los procesos de
Cartera detienen su tanda, dejan la corrida fallida y alertan. Sin contabilidad iniciada o con el hecho en «no pasa» no
se bloquea nada. Sin respuesta y con `Contabilidad.PoliticaSinRespuesta = Bloquear`, sólo las pantallas responden 422
`Lending.Prevalidation.NoResponse`.

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

## 10. Captaciones y aportes: por hechos de la Cartera

> Feature 014 (ahorros, aportes y CDAT dentro de la Cartera), historia US8. Contrato formal:
> [specs/014-ahorros-aportes-cdt/contracts/contabilidad.md](../../specs/014-ahorros-aportes-cdt/contracts/contabilidad.md) (enmienda E-03 de
> la 009), corregido por [research.md](../../specs/014-ahorros-aportes-cdt/research.md) §7 (E24–E28) y §15 (E106–E114). Donde el
> contrato y esta sección no coinciden, manda esta sección, que sigue el código. El manual del producto para la contadora y el jefe
> de captaciones es [cartera-captaciones.md](cartera-captaciones.md); su integración contable, contada para la contadora, en
> [cartera-captaciones-integracion-contable.md](cartera-captaciones-integracion-contable.md).

Las captaciones (ahorro a la vista, CDAT, ahorro contractual y ahorro permanente) y los aportes sociales **no tienen un lado contable
propio**: son hechos de la Cartera, del mismo emisor `CAR`, y todo lo del §9 vale igual. Entran por el mismo y único consumidor
`DestinoContabilidad` → `PostLendingFactsCommand` (o `PostLendingSummaryGroupCommand` en un resumido), se resuelven en la **misma matriz**
`ACC_LendingPostingRules`, pasan por el mismo `AccountingPoster`, se validan con la misma validación previa, se concilian con la misma
conciliación y los vigila la misma guarda de cierre. **No hay un tercer emisor.** Lo que la 014 sumó son datos en los catálogos fijos
(hechos, roles, dimensiones, tipos de comprobante, un tipo de documento cruce) y cinco columnas en la regla.

La Cartera sigue sin conocer cuentas: ningún hecho de captaciones lleva una cuenta contable (`LosHechosDeCarteraNoLlevanCuentas` recorre
también `Contracts/Lending/Deposits`; allí «cuenta» es la **cuenta del asociado**, `MemberAccountRefV1`, no una del plan).

### 10.1 Cómo arma el comprobante

El constructor (`ConstructorDeLineasDeCartera.Captaciones.cs`, sin IO) toma, de cada hecho de cuenta, **el cambio del saldo de cada
concepto** de la cuenta del asociado (positivo = la cooperativa le debe más al titular) y lleva cada concepto a su rol por su lado natural:

| Concepto de la cuenta | Rol |
|---|---|
| Capital | `CapitalDeCaptacion` (o `CapitalDeCaptacionInactiva` si la cuenta está inactiva) |
| Interés por pagar y bonificación provisionada | `InteresPorPagar` |
| Retención | `RetencionPorPagar` |
| GMF | `GmfPorPagar` |
| Aportes mínimos, ordinarios, extraordinarios y revalorizados | `AportesSociales`, con la clase de aporte de cada uno |
| Cargo por cobrar | `CargoDeCuentaPorCobrar` |
| No reclamados | `NoReclamadosPorPagar` |

Lo que no cuadra va a la **contrapartida del hecho**: el puente del recaudador en una consignación, un aporte o un retiro por el contrato;
la devolución por pagar en un retiro por giro o una devolución; el gasto por intereses en la causación; el ingreso por cargos; el puente
`OwnLoanPayoff` en el débito automático y en el cruce al retiro; la pérdida absorbida; los embargos por consignar; la penalidad; la
bonificación; los fondos de la asamblea. Un hecho que mueve saldo y no dice a dónde va su contrapartida responde
`Accounting.LendingFact.CounterpartUnknown` (defecto del emisor, no de la matriz).

Cada línea lleva el **número de la cuenta del asociado** como documento cruce `CTA` («Cuenta de captación o aportes») cuando la cuenta
contable exige documento, y el titular como tercero cuando la exige. En las cuentas contables que exigen tercero, cruce o base, va **una
línea por cuenta del asociado**, también en el resumido. En una unidad que junta hechos de las dos naturalezas (el cruce al retiro con el
pago del crédito, §10.3) cada línea toma el cruce de su propio hecho: el pagaré en las del crédito, la cuenta en las de captaciones.

### 10.2 Dimensiones nuevas

Cinco dimensiones al final de la clave de la regla (`DimensionDeCartera` 14 a 18; columnas nulas = cualquiera). **No pesan**: se exigen
por igualdad cuando el rol las declara y van en `*` cuando no. Los pesos del §9 no cambian.

| Dimensión | En la API y la plantilla | Valores |
|---|---|---|
| Naturaleza de la línea | `lineNature` (plantilla: `naturaleza`) | `Deposit` (captación), `Contribution` (aportes) |
| Clase de captación | `depositClass` (`claseDeCaptacion`) | `Demand` (a la vista), `Contractual`, `Permanent`, `TermDeposit` (CDAT) |
| Plazo de emisión | `issueTermBand` (`plazoDeEmision`) | `UnderSixMonths`, `SixToTwelve`, `TwelveToEighteen`, `EighteenOrMore` |
| Clase de aporte | `contributionClass` (`claseDeAporte`) | `Minimum`, `Ordinary`, `Extraordinary`, `Revalued` |
| Marca de la cuenta | `accountFlag` (`marcaDeCuenta`) | `Standard`, `GmfExempt` (exenta del GMF), `Garnished` (embargada) |

- El **plazo de emisión** se exige sólo en el capital (`CapitalDeCaptacion` y `CapitalDeCaptacionInactiva`) de las clases `TermDeposit` y
  `Contractual`, porque el CUIF separa 2110 y 2125 por plazo; en el ahorro a la vista y el permanente va en `*`.
- La **marca** la exige sólo `CapitalDeCaptacion`. La inactividad no es marca: cambia el rol (`CapitalDeCaptacionInactiva`).
- `ConceptCode` admite los conceptos de la cuenta; `ReasonCode` admite el tipo de cargo en `IngresoPorCargos` (`MaintenanceFee`,
  `ExtraOperation`, `ExtraDocument`, `MediumReplacement`, `Inactivity`, `Other`) y el motivo de la orden en `DevolucionPorPagar`
  (`DepositWithdrawal`, `MembershipRefund`, `ContributionRefund`, `DeceasedDelivery`, `TermDepositInterest`, `TermDepositRedemption`,
  `ContractualMaturity`, `UnclaimedPayment`, `GarnishmentConsignment`, `SurplusPayment`). `CollectorClass` admite además `Treasury`.
- Las reglas de crédito que ya existían no cambian de resolución: la migración `CaptacionesAportes` les agregó `|*|*|*|*|*` al final de la
  clave.

Errores de la regla: una dimensión exigida en `*`, `Accounting.LendingRule.DimensionRequired`; un valor fuera de su lista,
`Accounting.LendingRule.DimensionCodeUnknown`; un rol de captaciones con `lineNature = Placement`,
`Accounting.LendingRule.LineNatureMismatch` (el contrato los llamaba `RequiredDimension`, `InvalidDimensionValue` y `NatureMismatch`).

### 10.3 Los roles nuevos

Diecisiete roles, en español, al final de los 26 del crédito. Familias nuevas «Captaciones» y «Aportes y fondos» (`Captacion`,
`Patrimonio`). Lín = línea (peso 512), Suc = sucursal (4), Hecho = el hecho (2, sólo en los de resultado).

| Rol | Nat. | Familia | Exige | Opcionales | Tercero | Cruce |
|---|---|---|---|---|---|---|
| `CapitalDeCaptacion` | C | Captaciones | clase, marca; plazo en CDAT y contractual | Lín, Suc | titular | cuenta |
| `CapitalDeCaptacionInactiva` | C | Captaciones | clase; plazo en CDAT y contractual | Lín, Suc | titular | cuenta |
| `InteresPorPagar` | C | Captaciones | clase | Lín, Suc | titular | cuenta |
| `GastoPorIntereses` | D | Resultado | clase | Hecho, Lín, Suc | titular | — |
| `RetencionPorPagar` | C | Pasivo | clase | Suc | — | — |
| `GmfPorPagar` | C | Pasivo | clase | Suc | — | — |
| `GastoPorBonificacion` | D | Resultado | clase | Hecho, Lín, Suc | — | — |
| `IngresoPorCargos` | C | Resultado | motivo (tipo de cargo) | Hecho, Lín, Suc | titular | — |
| `CargoDeCuentaPorCobrar` | D | Saldo | — | Lín, Suc | titular | cuenta |
| `AportesSociales` | C | Aportes y fondos | clase de aporte | Lín, Suc | titular | cuenta |
| `FondoDeRevalorizacion` | D | Aportes y fondos | — | Suc | — | — |
| `FondoDeExcedentes` | D | Aportes y fondos | — | Suc | — | — |
| `DevolucionPorPagar` | C | Puente | naturaleza, motivo (de la orden) | Lín, Suc | beneficiario | — |
| `NoReclamadosPorPagar` | C | Pasivo | naturaleza | Suc | titular | cuenta |
| `EmbargosPorConsignar` | C | Puente | — | Suc | beneficiario (el juzgado) | — |
| `IngresoPorPenalidad` | C | Resultado | clase | Hecho, Lín, Suc | titular | — |
| `PerdidaAbsorbida` | C | Aportes y fondos | — | Suc | — | — |

Precisiones:

- **El tercero y el cruce se ponen sólo si la cuenta contable los exige**: la columna dice de dónde salen, no que siempre vayan.
- `GmfPorPagar` lleva la clase del **producto que genera el gravamen**, no la de la cuenta debitada: la redención de un CDAT abonada en ahorro
  debita el capital a la vista y acredita el GMF «sobre CDAT»; en un traslado, la clase de la cuenta de origen.
- `DevolucionPorPagar` es el **puente con Tesorería** de todas las órdenes de giro de captaciones y aportes (como `DesembolsoPorGirar` en los
  créditos): Tesorería paga contra la misma cuenta. Conviene que esa cuenta **no exija documento cruce** (la completitud lo avisa).
- `PerdidaAbsorbida` es de naturaleza crédito porque recibe el **abono** que extingue la pérdida (la cuenta de pérdidas es débito; abonarla la
  reduce). Pendiente de validar.
- Los aportes **no causan intereses**: ningún hecho mueve `GastoPorIntereses` ni `InteresPorPagar` con naturaleza aportes.
- La **retención y el GMF por pagar no se concilian** contra la Cartera (en el libro de la cuenta sólo crecen; la cuenta contable baja al
  pagar a la DIAN): se concilian con la declaración.

### 10.4 Los hechos

Treinta y tres hechos (`OperacionesDeCartera.DeCaptaciones`, semilla `LND_FactTypes`): treinta de negocio, con comprobante, y tres
informativos, con recibo y sin comprobante. El débito y el crédito son los del importe positivo; un negativo los invierte. Todos cuadran
solos. «Lotes» = por lotes resumido, disparado al terminar la causación del día (un `CC` por día con la sucursal en cada línea).

| Hecho | Sigla | Comp. | Modo | Débito | Crédito |
|---|---|---|---|---|---|
| `ConsignacionEnCuentaAplicada` | CCN | `MA` (`DN` si recauda Nómina) | En línea | `RecaudoPorAplicar` | `CapitalDeCaptacion` |
| `AporteRecibido` | APR | `AO` (`DN` si recauda Nómina) | En línea | `RecaudoPorAplicar` | `AportesSociales` |
| `RetiroDeCuentaAplicado` | RCT | `MA` | En línea | capital (activo o inactivo) | `RecaudoPorAplicar` o `DevolucionPorPagar` (retiro por giro) |
| `TrasladoEntreCuentasAplicado` | TRC | `MA` | En línea | saldo de la cuenta origen (capital o aportes) | saldo de la cuenta destino |
| `GmfRetenido` | GMF | el del principal | el del principal | capital | `GmfPorPagar` |
| `InteresDeCaptacionCausado` | ICC | `CC` | Lotes | `GastoPorIntereses` | `InteresPorPagar` |
| `AjusteDeCausacionDeCaptacion` | AJC | `CC` | Lotes (o el del principal) | `GastoPorIntereses` | `InteresPorPagar` (con signo) |
| `InteresDeCaptacionLiquidado` | ILQ | `CC` | Lotes si es del proceso | `InteresPorPagar` | capital |
| `RetencionPracticada` | RTF | `CC` o el del principal | Lotes o el del principal | capital o `InteresPorPagar` | `RetencionPorPagar` |
| `CdatConstituido` | CDC | `TD` | En línea | `RecaudoPorAplicar` o capital de la cuenta de ahorro origen | capital del CDAT |
| `InteresesDeCdatPagados` | CDP | `TD` | En línea | `InteresPorPagar` | capital de la cuenta destino o `DevolucionPorPagar` |
| `CdatProrrogado` | CDR | `TD` | En línea | `InteresPorPagar` (lo capitalizado) | capital del CDAT (plazo nuevo) |
| `CdatRenovado` | CDN | `TD` | En línea | `InteresPorPagar`, `RecaudoPorAplicar` o capital | capital, `DevolucionPorPagar` o `RecaudoPorAplicar` |
| `CdatRedimido` | CDD | `TD` | En línea | capital del CDAT, `InteresPorPagar` | capital destino, `DevolucionPorPagar` o `RecaudoPorAplicar` |
| `CdatCancelado` | CDX | `TD` | En línea | capital del CDAT, `InteresPorPagar` | ídem |
| `PenalidadAplicada` | PEN | `TD` | En línea | `InteresPorPagar` o capital | `IngresoPorPenalidad` |
| `CaptacionReclasificada` | RCC | `CN` | En línea (lotes si la emite el proceso) | cada saldo con la combinación anterior | el mismo saldo con la nueva |
| `BonificacionReconocida` | BON | `CC` | En línea | `GastoPorBonificacion` | `InteresPorPagar` (provisión) o capital (abono) |
| `CargoDeCuentaCausado` | CGA | `MA` | En línea | capital (cobrado) o `CargoDeCuentaPorCobrar` (pendiente) | `IngresoPorCargos` |
| `CargoPendienteCobrado` | CGP | `MA` | el del principal | capital | `CargoDeCuentaPorCobrar` |
| `CargoDeCuentaCondonado` | CGN | `MA` | En línea | `IngresoPorCargos` | `CargoDeCuentaPorCobrar` o capital (reintegro) |
| `RevalorizacionDeAportesAplicada` | RVA | `AO` | En línea | `FondoDeRevalorizacion` | `AportesSociales` (revalorizados) |
| `ExcedentesDistribuidos` | EXD | `AO` | En línea | `FondoDeExcedentes` | `AportesSociales`, capital o `DevolucionPorPagar` |
| `AportesAmortizados` | AMZ | `AO` | En línea | `AportesSociales` | `DevolucionPorPagar` |
| `CruceAlRetiroAplicado` | CRR | `RA` | En línea | aportes o capital | `RecaudoPorAplicar` (`OwnLoanPayoff`) |
| `PerdidaAplicadaAlRetiro` | PDR | `RA` | el del principal | `AportesSociales` | `PerdidaAbsorbida` |
| `DevolucionOrdenada` | DVO | `RA` | En línea | aportes, capital o `NoReclamadosPorPagar` | `DevolucionPorPagar` o capital de otra cuenta |
| `SaldoTrasladadoANoReclamados` | SNR | `MA` | En línea | `DevolucionPorPagar` o capital | `NoReclamadosPorPagar` |
| `EmbargoConsignado` | EMC | `MA` | En línea | capital | `EmbargosPorConsignar` |
| `OperacionDeCuentaReversada` | RVC | el del original | hereda el del original | espejo de lo que acreditó el original | espejo de lo que debitó |
| `DevolucionGirada` | DVG | — (informativo) | siempre | — | — |
| `CaptacionesActivadas` | CAV | — (informativo) | siempre | — | — |
| `CargaDeCaptacionesAplicada` | CAC | — (informativo) | siempre | — | — |

Reglas que valen para todos:

- **La unidad** es la del §9: una operación y su modo, un comprobante, con el tipo del hecho principal. Acompañan a su principal y salen en su
  comprobante: el GMF, la retención, el ajuste de la causación por fecha valor, el cargo pendiente que cobra una consignación, la penalidad y
  la pérdida al retiro.
- **El débito automático netea el puente**: el retiro acredita `RecaudoPorAplicar` con la clase `OwnLoanPayoff` y el pago del crédito lo debita
  en la misma transacción, con su propio `RC`. El puente `OwnLoanPayoff` debe dar cero al cierre de cada día.
- **El cruce al retiro y el pago del crédito van en un solo comprobante `RA`** (decisión del dueño del 2026-10-18): el cruce de cada crédito es
  el principal y el pago del crédito su acompañante; el puente `OwnLoanPayoff` se anula dentro del comprobante. El excedente a devolver va por
  `DevolucionOrdenada` al puente de Tesorería. El **reverso** del cruce sigue saliendo en comprobantes separados (cada uno cuadra por el
  puente) hasta que el dueño decida juntarlos.
- **Interés anticipado del CDAT**: el pago debita `InteresPorPagar` antes de causarlo; esa cuenta queda con saldo débito durante el período y
  la causación la lleva a cero. Pendiente de validar (si la contadora prefiere un diferido, es un rol nuevo).
- **Penalidad por reliquidación** de un CDAT o un contractual: un ajuste a la baja de la causación (`AjusteDeCausacionDeCaptacion`), no
  `PenalidadAplicada`; la penalidad de descuento fijo sí va a `IngresoPorPenalidad`. Pendiente de validar.
- **IVA de los cargos**: la Cartera cobra el cargo **sin IVA**; la factura del servicio la deja lista Comercial por su flujo de ventas.
- **La activación no emite hechos contables**: los saldos iniciales ya están en Contabilidad y la carga se cuadra contra ellos (§10.8).

### 10.5 Tipos de comprobante y documento cruce

La semilla suma cinco tipos (`Usage = Module`, `Module = CAR`) y su mapeo hecho → tipo, editable por la cooperativa en
`/contabilidad/cartera/tipos-de-comprobante`:

| Código | Nombre | Hechos por defecto |
|---|---|---|
| `MA` | Movimientos de cuentas de captación y aportes | consignación, retiro, traslado, GMF, cargos, no reclamados, embargo consignado, reverso (si el original no tiene mapeo) |
| `CC` | Causación de captaciones | causación, ajuste de causación, liquidación, retención, bonificación |
| `TD` | Certificados de depósito de ahorro a término | constitución, pago de intereses, prórroga, renovación, redención, cancelación y penalidad del CDAT |
| `AO` | Aportes sociales y asamblea | aporte recibido, revalorización, excedentes, amortización |
| `RA` | Retiro de asociados | cruce al retiro, pérdida al retiro, devolución ordenada |

Además: la consignación y el aporte que recauda Nómina van a `DN`; la reclasificación, a `CN`; el reverso toma el tipo de su original. Los
tipos `AH` («Intereses de ahorros») y `CD` («Liquidación de intereses de CDT») de la 009 quedan **inactivos**: la semilla los inactiva en
una cooperativa que los tenía sin comprobantes, y si ya tenían comprobantes los deja y lo anota en el log
(`[Semilla.TipoDeComprobanteRetiradoEnUso]`). El documento cruce de todos los hechos de captaciones es `CTA` con el número de la cuenta.

### 10.6 Modo de paso y validación previa

- **Modo** (`Contabilidad.ModoDePaso`, ámbito tipo de hecho; se sella en cada hecho al emitirse): en línea todo, salvo la causación, su ajuste,
  la liquidación del proceso y su retención (por lotes resumido) y los informativos (siempre, sin comprobante); los reversos heredan el del
  original. Sin contabilidad iniciada y sin un modo guardado, «no pasa».
- **Validación previa**: rige lo mismo que en los créditos (A8 = «Bloquear todo»). Con `Contabilidad.FaltaDeRegla = Bloquear` —el
  defecto—, si falta la regla **bloquean la pantalla** (422 `Lending.Prevalidation.NotPostable`: retiro por giro, traslado, cancelación,
  constitución y redención del CDAT, liquidación del retiro del asociado…), **el contrato de cuentas** (rechaza la consignación o el retiro
  de Recaudo, Nómina o Tesorería con ese código) y **los procesos** (la tanda no avanza, sus cuentas quedan en los errores de la corrida y sale
  la alerta `Cartera.HechoNoContabilizable`). Con `Advertir` todo se aplica, se sella y se alerta. La pregunta se hace con hechos
  representativos con las dimensiones vigentes de la cuenta, su titular y su número.

### 10.7 La plantilla de la matriz y las cuentas CUIF sugeridas

La plantilla de la matriz (`GET /api/accounting/lending/rules/template.xlsx`) es una **versión nueva** que suma las columnas `naturaleza`,
`claseDeCaptacion`, `plazoDeEmision`, `claseDeAporte`, `marcaDeCuenta` y `cuentaSugerida`, y sigue leyendo los archivos de la versión
anterior (sin esas columnas, `*`). Trae **filas sugeridas sin cuenta** por cada combinación que las líneas activas de la cooperativa van a
necesitar. **No se siembra ninguna regla**: la columna `cuentaSugerida` es texto informativo que la importación **no aplica**; la contadora
escribe la cuenta en la columna de la cuenta. Las sugerencias son dato (`Accounting/Lending/Plantilla/sugerencias-cuif-captaciones.json`),
no código.

**Cuentas CUIF sugeridas — orientación para la contadora, todas pendientes de validar:**

| Rol | Clase / dimensión | Cuenta sugerida |
|---|---|---|
| `CapitalDeCaptacion` | a la vista | 210505 |
| `CapitalDeCaptacionInactiva` | a la vista | 210510 |
| `CapitalDeCaptacion` | CDAT menos de 6 meses · 6 a 12 · 12 a 18 · 18 o más | 211005 · 211010 · 211015 · 211020 |
| `CapitalDeCaptacion` | contractual de menos de 12 meses · de 12 o más | 212505 · 212515 |
| `CapitalDeCaptacionInactiva` | contractual de menos de 12 meses · de 12 o más | 212510 · 212520 |
| `CapitalDeCaptacion` | ahorro permanente | 213005 o 213010 (la contadora elige) |
| `InteresPorPagar` | a la vista · CDAT · contractual · permanente | 210595 · 211095 · 212595 · 213095 |
| `GastoPorIntereses` | a la vista · CDAT · contractual · permanente | 615005 · 615010 · 615015 · 615020 |
| `RetencionPorPagar` | todas las clases | 243535 |
| `GmfPorPagar` | a la vista · CDAT · contractual · permanente | 243005 · 243010 · 243015 · 243020 |
| `GastoPorBonificacion` | contractual | 615015 |
| `AportesSociales` | ordinarios · extraordinarios · mínimos no reducibles · revalorizados | 310505 · 310510 · 3110 · 310505 |
| `DevolucionPorPagar` | aportes (retiro y devolución de aportes) · captación (retiro) | 246505 · 246510 |
| `NoReclamadosPorPagar` | — | 246595 |
| `FondoDeRevalorizacion` | — | 3310 |
| `FondoDeExcedentes` | — | 3505 |
| `PerdidaAbsorbida` | — | 3510 |

Sin sugerencia (la contadora decide): `IngresoPorCargos`, `CargoDeCuentaPorCobrar`, `EmbargosPorConsignar`, `IngresoPorPenalidad`, el resto de
los motivos de `DevolucionPorPagar` (intereses y redención de CDAT, vencimiento del contractual, pago de excedentes, entrega por
fallecimiento, consignación de embargos) y el puente `RecaudoPorAplicar` de Tesorería. Con la marca: la plantilla sugiere para la cuenta
exenta y la embargada la **misma** cuenta que la estándar; si la contadora las lleva a cuentas distintas, la reclasificación al marcar o
desmarcar mueve saldo entre ellas. El gasto por intereses se sugiere con el hecho en `*` (la causación y su ajuste usan el mismo rol).

Lo que la contadora tiene que validar en especial: el ahorro permanente (213005 o 213010), la bonificación (615015), los revalorizados (310505),
la frontera de 12 meses del plazo de emisión (contado en meses de 30 días), el residuo de centavos entre la causación diaria y el interés
liquidado (queda en el interés por pagar contable), y la pérdida absorbida.

**Completitud** (`/contabilidad/cartera/completitud`, con filtro por naturaleza y la cuenta sugerida en cada fila): exige, por cada línea
activa, las combinaciones de los hechos de aportes y del ahorro (entregas K7 y K8) y **avisa** de una devolución o un embargo por pagar en una cuenta que exige cruce, de la causación que irá una línea por
cuenta, de la marca exenta o embargada en otra cuenta que la estándar y del puente de Tesorería sin regla. Lo de la asamblea, los no
reclamados, los embargos y Tesorería se sugiere pero no se exige. La activación de cada naturaleza pregunta, por cada línea activa,
por los hechos que emitirá desde el primer día —en aportes, el aporte, el cruce y la devolución del retiro; en el ahorro, la consignación,
el retiro por giro con su GMF y, en las clases que causan, la causación, la liquidación y la retención— (`LinesWithMatrix`).

### 10.8 Conciliación, cuadre de activación y cierre

- **Conciliación** (`/cartera/conciliacion-contable` y la vista `deposits-reconciliation`, `Lending.Reconciliation.View`, sin datos de
  titulares): suma el libro de cuentas por fecha contable por rol (capital, capital inactivo, interés por pagar, aportes por clase, cargo por
  cobrar), línea, naturaleza, clase, marca y oficina, y la compara **por conjuntos de cuentas** con los saldos de las cuentas que la matriz
  asigna, igual que los créditos. Cada diferencia se explica hecho por hecho (pendiente, en lote, rechazado, no pasa). Los puentes
  `OwnLoanPayoff` y, con Tesorería al día, `DevolucionPorPagar` y `EmbargosPorConsignar` deben quedar en cero.
- **Cuadre de la activación**: compara la carga de migración, al corte, con los mismos saldos contables y con la **misma** clave de la
  conciliación; las diferencias sólo se aceptan con motivo y punto de acta
  ([captaciones-puesta-en-marcha.md](../operaciones/captaciones-puesta-en-marcha.md)).
- **Cierre**: `Accounting.Period.LendingNotClosed` (sin reconocimiento) **cubre las captaciones**: con aportes o captaciones activados,
  Contabilidad no cierra un mes que la Cartera no cerró, y el mensaje dice desde qué día está pendiente la causación de captaciones
  (`data.depositAccrualPendingFrom`; nada si ninguna cuenta ha causado, como en una cooperativa sólo de aportes). Los avisos reconocibles
  `LendingPending` y `LendingLoadWillExpire` cuentan también los hechos y la carga de captaciones.

### 10.9 Cómo se corrige

Lo de captaciones y aportes **no se reversa desde Comprobantes**: `PrepareReversalAsync` responde `Accounting.Document.ModuleOwned` con el
mensaje «se corrige en la Cartera con el reverso de la operación de la cuenta» (y, si es un resumido, cuántas cuentas reúne). Se corrige
reversando la operación en la Cartera: sale `OperacionDeCuentaReversada`, un **comprobante nuevo** con su propia fecha contable, el tipo de su
original y las **dimensiones vigentes** del saldo que restituye (un retiro de una cuenta que después quedó inactiva se restituye en el capital
inactivo). El original nunca se marca reversado. Un mapeo equivocado ya contabilizado lo corrige la contadora con un `CG` manual de
reclasificación, como en los créditos.

Lo que **no** hay que hacer desde las captaciones: leer o escribir tablas `ACC_`, llamar a `AccountingPoster`, elegir el tipo de comprobante,
llevar una cuenta del plan en un hecho o pedir una reversión. Lo vigilan las mismas pruebas del §9.
