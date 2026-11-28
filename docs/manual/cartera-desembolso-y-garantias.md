# Cartera: desembolso, garantías y pagaré

> Feature 013 (cartera financiera), fase 7 (US3, entrega K1). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §6 (garantías y pagaré), §8.4 (anular un
> crédito pendiente de giro), §9 (desembolso, órdenes de giro y archivos) y §20 (dobles de prueba),
> [contracts/mensajes.md](../../specs/013-cartera-financiera/contracts/mensajes.md) §10 y §12.4, y
> [contracts/adaptadores-y-archivos.md](../../specs/013-cartera-financiera/contracts/adaptadores-y-archivos.md) §3 y §6.2.
> Aquellos son el contrato; esto es la receta para quien opera el desembolso (jefe de cartera), quien registra
> garantías y pagarés (asesor) y quien implanta (formatos de archivo, cuentas de origen, depósito de valores).

El desembolso es el momento en que una solicitud **aprobada y vigente** se vuelve un crédito. En una sola
transacción el sistema sella las condiciones (tasa, usura del día, sobretasa de cargos, versión de las reglas) y
el plan de pagos, recoge los productos que la solicitud autorizó (§5), emite la **orden de giro** para Tesorería
y deja los hechos para Contabilidad. Nada de eso se edita después: lo que sale mal se reversa desde su origen.

Prerrequisitos: la originación ([cartera-originacion.md](cartera-originacion.md)), la oficina vinculada a su
sucursal contable ([cartera-gobierno-y-seguridad.md](cartera-gobierno-y-seguridad.md)), la persona del maestro
asociada a quien desembolsa, el mes contable abierto y, si se paga por archivo, el formato del banco y la cuenta
bancaria de origen en el plan de cuentas.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Garantías de la solicitud (registrar, perfeccionar, extinguir, adjuntar) | zona «Garantías y pagaré» del detalle `/cartera/solicitudes/{id}` | `Lending.Guarantees.Read` · `.Manage` |
| Liberar una garantía (cancelación en firme) | ruta `/api/lending/collaterals/{id}/release` | `Lending.Guarantees.Release` |
| Pagaré físico o desmaterializado, «Actualizar estado», custodia | zona «Garantías y pagaré» | `Lending.PromissoryNotes.Manage` |
| Vista previa y desembolso (botón «Desembolsar» de la solicitud aprobada) | `/cartera/desembolsos` (pestaña «Desembolsar») | `Lending.Disbursements.Execute` (atribución con monto) |
| Órdenes de giro: lista y detalle | `/cartera/desembolsos` (pestaña «Órdenes de giro») | `Lending.Loans.Read` |
| Pedir a Tesorería que anule una orden | pestaña «Órdenes de giro» | `Lending.DisbursementOrders.Void` |
| Giro nuevo de un crédito pendiente de giro | pestaña «Órdenes de giro» | `Lending.Disbursements.Execute` |
| Anular un crédito pendiente de giro | pestaña «Órdenes de giro» | `Lending.Disbursements.Void` |
| Archivos de desembolsos (generar, descargar, anular) | `/cartera/desembolsos` (pestaña «Archivos de desembolsos») | `Lending.DisbursementFiles.Generate` |
| Tesorería de prueba (fuera de producción) | `/cartera/dobles-de-prueba` | `Lending.TestDoubles.Operate` |

Como en todo Cartera: sin el permiso, o fuera del alcance de oficina y línea, la respuesta es el mismo 404 que si
no existiera y la auditoría anota `Lending.Access.Denied`; toda escritura lleva `Idempotency-Key` (desembolsar dos
veces con la misma clave devuelve el mismo crédito, no dos). La vista previa del desembolso y la del archivo,
«Actualizar estado» del pagaré y los enlaces de descarga son consultas y van sin clave.

## 2. Garantías y pagaré

**Garantías reales y de fondo** (prenda, hipoteca, fondo de garantías, aportes) se registran sobre la solicitud
**antes de decidirla**: una solicitud ya decidida responde `Lending.Collateral.OwnerLocked`. Sólo se admiten las
clases que la regla de garantías de la línea admite para el monto y el plazo (`Lending.Collateral.KindNotAdmitted`
con la lista de admitidas). Un constituyente distinto del deudor necesita su autorización de tratamiento para
crédito. Cada garantía nace **constituida** y sigue `Constituida → Perfeccionada → Extinguida | Ejecutada`; sus
documentos (avalúo, póliza, certificado de registro) se suben directo al almacén como adjuntos de la garantía y se
bloquean cuando la garantía deja de estar viva. Se **libera** sólo cuando el crédito está pagado **y en firme**
(pasados los días de firmeza del último pago sin reverso): antes responde `Lending.Collateral.NotFinal` con la fecha. Si lo canceló el
desembolso de otro crédito, además tiene que estar **girado** ese crédito nuevo (§5, `Lending.Collateral.PendingNewLoanDisbursement`).
La tarea `cartera.garantias-por-vencer` alerta las garantías y pólizas que vencen en los próximos días.

**El pagaré** es uno vivo por solicitud o crédito:

- **Físico**: se crea con su número, se adjunta el pagaré firmado y escaneado y se registra la custodia (dónde
  queda guardado). Queda en custodia y perfeccionado.
- **Desmaterializado**: se pide al **depósito de valores** configurado en *Administración › Canales externos*
  (clase *Depósito de valores*). El canal queda **sellado** en el pagaré: si después cambia el canal vigente, ese
  pagaré se sigue consultando por el suyo. Nace pendiente de firma; «Actualizar estado» consulta al depósito y, cuando
  el deudor firma, lo deja en custodia. La tarea `cartera.pagares` hace lo mismo cada hora y alerta los que pasan la
  vigencia de la aprobación sin firma.

**El depósito simulado en producción (T73).** Mientras no haya contrato con un depósito real, el canal disponible
es `SIMULADO`. Sirve para probar el flujo completo fuera de producción, pero **en producción un pagaré simulado
nunca cuenta como perfeccionado**: desembolsar responde `Lending.Adapter.SimulatedInProduction` y dice qué
configurar. En producción, hasta tener el depósito real, se usa el pagaré físico con su custodia. En el depósito
simulado la respuesta depende del último dígito del documento del deudor: 1 rechaza la firma, 3 la deja en proceso
y firma a la tercera consulta, 5 simula el canal caído y cualquier otro firma en la consulta siguiente.

## 3. Lo que se revisa antes de desembolsar

La vista previa (`POST /api/lending/applications/{id}/disbursement-preview`) y el desembolso hacen **la misma
evaluación**; la vista previa no escribe nada y devuelve una huella (`previewSha256`) que el desembolso puede
exigir para no confirmar algo distinto de lo que se vio. Impiden desembolsar:

| Qué | Código |
|---|---|
| La solicitud no está aprobada · la aprobación venció | `Lending.Application.NotApproved` · `Lending.Application.ApprovalExpired` (con la fecha) |
| Quien decidió el expediente no puede desembolsarlo | `Lending.Segregation.Conflict` |
| La tasa con cargos supera la **usura del día del desembolso** (no la de la aprobación) | `Lending.Usury.Exceeded` |
| Garantías o pagaré sin perfeccionar · faltan codeudores · la cobertura no alcanza | `Lending.Disbursement.GuaranteesPending` (con la lista) · `Lending.Collateral.CoDebtorsInsufficient` · `Lending.Collateral.CoverageInsufficient` |
| Coincidencia en listas sin liberar · bloqueo o embargo vigente | `Compliance.RestrictiveList.Match` · `Lending.Debtor.Blocked` |
| Límite de concentración · sin patrimonio técnico vigente | `Lending.Concentration.LimitExceeded` · `.TechnicalCapitalUnavailable` |
| El tramo supera el monto operativo de quien desembolsa | `Lending.AmountLimit.Exceeded` |
| La oficina no tiene sucursal contable · la transferencia no tiene cuenta destino | `Lending.Branch.AccountingBranchMissing` · `Lending.Disbursement.BeneficiaryAccountMissing` |
| Un crédito a recoger está en mora · la solicitud no autorizó esa recogida · lo recogido no deja neto | `Lending.Payoff.RequiresRestructuring` · `Lending.Payoff.NotRequested` · `Lending.Disbursement.NetNotPositive` |
| Un producto a recoger está castigado, en insolvencia o pendiente de giro · la lista no es la de la solicitud · una diferencia pasa de la tolerancia (feature 028) | `Lending.Payoff.NotPayable` · `Lending.Payoff.MustMatchApplication` · `Lending.Payoff.DifferenceAboveTolerance` |
| Contabilidad no tiene regla y la cooperativa eligió bloquear | `Lending.Prevalidation.NotPostable` |

Los **avisos** (Contabilidad sin regla con la política «advertir», o sin respuesta a tiempo) no bloquean: la
pantalla los muestra y pide reconocerlos antes de confirmar. Las **diferencias de lo que se recoge** (feature 028) se reconocen con su propia
casilla (§5).

## 4. Desembolsar y los tramos

La forma del giro es **transferencia** (a la cuenta del deudor o de un tercero), **cheque** o **giro a un tercero**
(proveedor o vendedor, persona del maestro). La cuenta destino se propone desde la persona y se puede corregir; queda
**copiada en la orden** y un cambio posterior en el maestro no la toca. El desembolso en especie sólo nace de una venta
de Comercial (US6).

Si la línea admite **tramos**, el primer desembolso puede ser por menos de lo aprobado y los siguientes amplían el
mismo crédito (`loanPublicId` + `trancheAmount`); cada tramo es una operación con su número (`DS-…`) y **regenera el
plan** sobre lo desembolsado. Sin tramos en la línea, desembolsar menos de lo aprobado responde
`Lending.Disbursement.TrancheNotAllowed`; pasarse de lo aprobado, `Lending.Disbursement.ExceedsApproved`.

El resultado trae el número del crédito (`CR-…`), el de la operación, el neto a girar y la orden (`OG-…`). La solicitud
pasa a *Desembolsada* y su pagaré y garantías pasan al crédito.

## 5. Recogida de créditos y productos en el desembolso

> Reescrita con la feature 028 (2026-11-27). La receta completa —vender, radicar, desembolsar, la cuota causada, los intereses, los
> parámetros y los códigos— es [cartera-recogida-de-creditos.md](cartera-recogida-de-creditos.md). Aquí, lo que cambia para quien desembolsa.

La solicitud puede autorizar **recoger** con el desembolso productos vivos del mismo deudor: sus créditos (total o parcial) y, si la
cooperativa lo parametriza (`Recogida.ProductosAdmitidos`), cuotas o abonos de aportes y ahorros y facturas a crédito de Comercial. Cada
recogida se paga **por el contrato de siempre** —los créditos por el contrato de obligaciones (clase de recaudador «recogida propia», clave
`CAR:{desembolso}:{obligación}`), las cuentas por su registro y las facturas con un recibo de Recaudo sin caja— dentro de la transacción del
desembolso: si una no se aplica, el desembolso no existe.

- **Las recogidas salen de la solicitud**, todas: no se eligen en el desembolso. Si el cuerpo trae otra lista responde
  `Lending.Payoff.MustMatchApplication`; para cambiar lo que se recoge, se devuelve la solicitud. Sólo el primer tramo recoge.
- **La vista previa recalcula** cada recogida a la fecha valor y la compara con lo proyectado en la solicitud: por producto, lo proyectado, lo
  actual, la diferencia y su causa (pagos, causación, mora, cuota causada, parámetros, ya cancelado). Un producto ya cancelado se **omite** y el
  neto sube. Con una diferencia o un omitido, desembolsar exige marcar «Revisé las diferencias de lo que se recoge»
  (`Lending.Payoff.DifferencesNotAcknowledged`); con `Recogida.ToleranciaDeDiferencia` y una diferencia mayor, se bloquea
  (`Lending.Payoff.DifferenceAboveTolerance`) y hay que devolver la solicitud.
- **La orden de giro es el bruto menos lo realmente aplicado**, al peso; la vista previa, el resultado y la orden dicen lo mismo. Antes se
  emitía por lo evaluado antes del cerrojo y podía no cuadrar con las recogidas.
- **La recogida parcial** de un crédito cubre lo exigible y abona el resto **a capital** con la preferencia de abono de la solicitud (antes
  quedaba como anticipo).
- **Un crédito en mora** no se recoge: es una **reestructuración** y se tramita en Novedades como una refinanciación que lo recoge
  (`Lending.Payoff.RequiresRestructuring`). Castigado, en insolvencia que negocia o pendiente de giro: `Lending.Payoff.NotPayable`.
- **El tope legal del prepago.** La recogida no **sella** el tope del abono sin penalidad ni se bloquea porque falte el SMMLV o
  `PREPAGO_TOPE_SMMLV` (respuesta del dueño del 2026-10-14): no es un abono del deudor sino Cartera pagándose un crédito con el desembolso de
  otro. Pero el tope sí decide **el interés**: dentro del tope, o sin los valores para saberlo, se cobra el proporcional (y la cuota causada que
  se deja a la pagaduría lleva el interés proporcional). Ver la receta, §6.3 y §7.
- **Los créditos quedan enlazados**: el recogido dice «Recogido por el crédito …» y el nuevo «Recogió: …».

**Las garantías y el pagaré del crédito recogido se liberan al girar el crédito nuevo**, no al aplicarse la recogida (D12). Liberarlos sigue
siendo un acto de una persona, con su permiso y su motivo, pero mientras el crédito nuevo no esté girado la liberación responde
`Lending.Collateral.PendingNewLoanDisbursement` y el detalle del recogido los muestra «Por liberar al girar el crédito N». Así, si el crédito nuevo
se anula antes del giro, el viejo vuelve vivo con sus garantías. Un cheque girado que pasa a «no cobrado» vuelve a dejarlos pendientes hasta que
el giro nuevo quede pagado.

## 6. La orden de giro, Tesorería y el archivo de desembolsos

La orden nace con el desembolso y viaja a Tesorería en el mensaje `OrdenDeGiroEmitida`. Si la transferencia se paga
**por archivo de desembolsos de Cartera** (`PaidByFile`), la marca viaja en la orden para que Tesorería no la pague por
otra vía; el archivo no viaja en ningún mensaje.

**El archivo** se genera en la pestaña «Archivos de desembolsos» con el motor común de archivos planos (el mismo de
la dispersión de nómina):

1. Formato del ámbito *Desembolsos de crédito* (en *Maestros › Formatos bancarios*). Puede ser de un banco o genérico.
   El layout real lo aporta el banco; el contrato trae de ejemplo un CSV genérico (`CSV-DESEMBOLSOS`) que no se siembra.
2. Cuenta bancaria de origen: una cuenta del plan con banco; debe ser del banco del formato, salvo que el formato sea
   genérico (`Lending.DisbursementFile.BankMismatch`).
3. Vista previa: las líneas como quedarían y las órdenes que **no entran** con su motivo (banco sin código ACH, sin
   cuenta destino, ya en otro archivo vivo, o el campo obligatorio que falta).
4. Generar: el archivo se guarda como adjunto con su SHA-256 **antes** de guardar el registro, numerado (`ARD-…`), y
   cada orden queda con la referencia del archivo.
5. Descargar: enlace firmado de 60 segundos, auditado.
6. Anular: sólo mientras **ninguna** orden del archivo tenga resultado de Tesorería
   (`Lending.DisbursementFile.HasResults`). Las órdenes quedan libres para otro archivo y siguen pagándose por archivo.

## 7. El resultado del giro, el reverso y la anulación

Tesorería informa por orden con `ResultadoDelGiro`: *girada*, *rechazada*, *cheque no cobrado* o *anulada*.

- **Girada**: no cambia el libro.
- **Rechazada**, **cheque no cobrado** o **anulada** (después de pedir la anulación): el desembolso se **reversa desde
  su origen** —una operación de reverso con un espejo por cada fila del tramo, enlazado a su original—, sale
  `DesembolsoReversado` y el crédito queda **pendiente de giro**. Con varios tramos sólo se reversa el capital de ese
  tramo y la diferencia de causación por fecha valor.
- Un resultado que no cuadra (valor distinto del neto, transición que no cabe) se rechaza con
  `Lending.DisbursementOrder.InvalidResult` y el motivo, y queda para reprocesar. Repetir el mismo mensaje no reversa dos
  veces.

**Pedir la anulación** de una orden emitida (`Lending.DisbursementOrders.Void`) emite `AnulacionDeOrdenDeGiro`; la orden
sigue *emitida* hasta que Tesorería responda. Una orden que va en un archivo vivo no se anula: primero se anula el archivo.

Con el crédito **pendiente de giro** hay dos caminos:

- **Giro nuevo**: otra orden del mismo tramo (que nombra a la anterior) y el crédito vuelve a *Activo*. Desde la feature 028 la orden y el
  hecho del giro nuevo llevan las mismas recogidas vivas y el mismo neto (bruto menos lo recogido).
- **Anular el crédito** (`Lending.Disbursements.Void`, con motivo): queda *Anulado* y, en la misma transacción, se
  reversan las recogidas del desembolso —los pagos de los créditos por el contrato, las consignaciones de las cuentas por su registro y el
  recibo de las facturas en Recaudo— y los productos recogidos vuelven a su estado.

La tarea `cartera.giros-sin-resultado` alerta las órdenes sin resultado pasados los días hábiles del parámetro
`Giros.DiasSinResultado`.

> Estado en K1: Cartera consume `ResultadoDelGiro` por la bandeja de mensajes con su destino `Lending`. Ese destino
> cuenta sólo cuando su consumidor acepta todo lo que adopta la fila `CAR` del registro de integración, y la semilla la
> deja adoptando también las ventas a crédito de Comercial, que llegan con la fase 10 (US6). Hasta entonces los
> resultados del giro quedan esperando en la bandeja, sin intentos ni rechazos, igual que las ventas a crédito.

## 8. Ambientes de prueba: la tesorería de prueba

Fuera de producción, con `Lending:TestDoubles:Enabled = true`, existe la **tesorería de prueba** (registrada como
participante de clase *Prueba*) y la pantalla `/cartera/dobles-de-prueba`, rotulada «AMBIENTE DE PRUEBAS»: muestra las
órdenes y anulaciones recibidas y emite el resultado de cada una como lo haría Tesorería, con la persona que prueba
como usuario de origen. En producción ni las rutas ni la pantalla existen, y la API no arranca si la configuración las
pide.

## 9. Qué queda en la auditoría y en la bandeja

- El desembolso deja el evento de la operación con la distribución completa por cuota y concepto, y la intervención
  *Desembolsar* en el expediente (segregación).
- La bandeja recibe `CreditoDesembolsado` (Contabilidad), `OrdenDeGiroEmitida` (Tesorería) y, si hubo recogidas,
  `PagoAplicado` por cada crédito recogido (la consignación de cada cuenta y el `ReciboEmitido` de las facturas, feature 028); el reverso,
  `DesembolsoReversado` con el mismo modo de paso del original y, desde la 028, con las mismas recogidas para que sea el espejo del `DS`.
- Desde la feature 028 el evento del desembolso lleva, por producto recogido, lo proyectado, lo actual, lo aplicado, la diferencia, su causa y
  quién la reconoció.
- La descarga del archivo de desembolsos queda auditada (`Lending.DisbursementFile.Downloaded`).
