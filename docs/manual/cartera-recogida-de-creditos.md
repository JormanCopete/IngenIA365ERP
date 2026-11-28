# Cartera: recogida de créditos y productos en el desembolso

> Feature 028 (pedido del dueño del 2026-11-27). Especificación: `specs/028-recogida-de-creditos/` (decisiones D1–D13 en `research.md` §3,
> enmiendas E-01 a E-16 en §4). Para **el asesor** que vende, **el analista** que decide, **quien desembolsa**, **la contadora** y **el
> administrador** de la cooperativa. Complementa [cartera-originacion.md](cartera-originacion.md),
> [cartera-desembolso-y-garantias.md](cartera-desembolso-y-garantias.md), [cartera-libranza-y-pagadurias.md](cartera-libranza-y-pagadurias.md) y
> [cartera-integracion-contable.md](cartera-integracion-contable.md).

**Recoger** es que un crédito nuevo **cancele o abone, con su propio desembolso y en la misma operación**, otros productos vivos del mismo
deudor: sus créditos y, si la cooperativa lo permite, sus aportes, sus ahorros y sus facturas a crédito de Comercial. Si una recogida no queda
aplicada, el desembolso no existe: no hay medio desembolso.

Hasta esta feature se hacía en dos pasos: se desembolsaba el crédito nuevo y después, en caja, se cancelaba el viejo con «Todo para cancelar
(F7)». El dinero salía por Tesorería y volvía por la caja, el asesor no podía mostrarle al asociado cuánto recibiría neto y nada comparaba lo
ofrecido con lo que finalmente se canceló. Ahora:

- al **vender**, el asesor ve todos los productos del asociado y simula cuánto cuesta recoger cada uno a una fecha;
- la solicitud **guarda esa proyección**, y las validaciones miden la situación **después de recoger**;
- al **desembolsar** se **vuelve a calcular todo** y se muestra la diferencia con lo proyectado, con su causa, antes de confirmar;
- la orden de giro sale por el **bruto menos lo realmente aplicado**, al peso, y la contabilidad **cuadra sola**.

**De fábrica nada cambia**: todos los parámetros nuevos arrancan con el comportamiento de antes (§12).

## 1. Qué se recoge y qué no

| Producto | Cómo se recoge | De fábrica |
|---|---|---|
| Créditos del deudor (también los nacidos de una venta de Comercial o de un cargo masivo) | **Total** o **parcial** | Sí |
| Aportes sociales | Sus **cuotas** pendientes o un **abono** (aporte extraordinario) | No |
| Ahorro a la vista y ahorro permanente | Un **abono** (consignación) | No |
| Ahorro contractual | Sus **cuotas** pendientes | No |
| Facturas a crédito de Comercial (servicios que no son cartera) | **Total** o **parcial** | No |

**No se recoge**:

- un crédito **en mora**: recogerlo es una **reestructuración** y se tramita en Novedades como una refinanciación (decisión K3, confirmada por el
  dueño en D10). También dentro de la tolerancia de saldo vencido;
- un crédito **castigado**, uno cuyo deudor está en **insolvencia que negocia**, uno **pendiente de giro** o uno sin saldo;
- un crédito donde el asociado es **codeudor**: sólo se recogen los productos donde es deudor o titular;
- un **CDAT**: no es una deuda, y ni siquiera aparece en la lista;
- los conceptos varios y los anticipos de Recaudo: tampoco son deudas;
- créditos de otra entidad (compra de cartera externa).

**Palabras que usa la pantalla**:

| Palabra | Qué es |
|---|---|
| Crédito nuevo | El que se vende y se desembolsa. |
| Producto recogido | Lo que el desembolso cancela o abona. |
| Fecha proyectada de desembolso | La fecha a la que se liquida cada producto. Por defecto, la fecha esperada de desembolso de la simulación. |
| Información hasta | Hasta cuándo se tomaron los pagos, la causación y la mora. Por defecto, el momento en que se simula. |
| Cuota causada | La cuota del período en curso: su período ya empezó y todavía no vence. |
| Cuota enviada | La cuota causada que ya salió en el archivo de descuentos a la pagaduría, o que la nómina propia ya está descontando. |
| Neto a girar | El valor del crédito nuevo menos lo recogido. |

## 2. Pantallas y permisos

No hay permisos nuevos.

| Qué | Dónde | Permiso |
|---|---|---|
| Ver los productos del asociado, simular la recogida e imprimir la simulación | Solicitud nueva o corrección, zona «Productos que recoge» (`/cartera/solicitudes/nueva`, `/cartera/solicitudes/{id}/corregir`) | `Lending.Payoffs.Read` |
| Radicar o corregir una solicitud que recoge | Las mismas pantallas | `Lending.Applications.Create` (y lo de siempre para corregir o editar la radicada) |
| Ver «Lo que recoge» en el expediente | Detalle de la solicitud `/cartera/solicitudes/{id}` | El de la solicitud |
| Desembolsar con recogidas | `/cartera/desembolsos`, pestaña «Desembolsar» | `Lending.Disbursements.Execute` |
| Informe «Recogidas» | `/cartera/informes?vista=payoffs` (o Centro de reportes → «Recogidas») | `Lending.Reports.View`, más `Lending.Loans.Read`; al exportar, `Lending.Reports.Export` y `Lending.Reports.ExportPersonalData` |
| Parámetros de la recogida | Parámetros de Cartera (`/cartera/parametros`, «Nueva vigencia») | Los de los parámetros de Cartera |
| Un solo comprobante para el desembolso y sus recogidas | Administración → Parámetros del sistema (módulo `ACC`) | El de los parámetros del sistema |

Sin `Lending.Payoffs.Read` la zona «Productos que recoge» no se ve: la solicitud se radica sin recogidas o, al corregir, conserva las que ya
tenía.

## 3. Vender: los productos del asociado y la simulación (asesor)

En la solicitud nueva, después de simular el crédito y pulsar «Usar en la solicitud», el paso 2 muestra **«Productos que recoge»**:

1. **La tabla** lista todos los productos vivos del solicitante, liquidados a la fecha proyectada de desembolso. Por cada uno: el producto
   (con su origen, si es una venta de Comercial o un cargo masivo), la línea, la forma de pago (caja, nómina propia, libranza, débito
   automático), los días de mora, la cuota, lo que cuesta recogerlo y el modo. Si la cuota del período ya va en la libranza o en la nómina,
   la forma de pago dice «· cuota enviada».
2. **Lo que no se puede recoger** sale sin casilla, con el motivo: «En mora: requiere reestructuración», «Castigado», «En insolvencia», «Sin
   saldo», «No está vigente», «La línea no lo admite», «No se pudo liquidar», «La cooperativa no los recoge» (aportes y ahorros de fábrica),
   «La cooperativa no las recoge» (facturas de fábrica) o «Recaudo no está listo para recogerla».
3. **Marque** lo que se recoge y elija el modo: total o parcial (con su valor) en créditos y facturas; cuotas o abono (con su valor) en
   cuentas. Un parcial mayor que lo que cuesta recoger se toma por el total. Si el crédito tiene cuota causada y la cooperativa deja elegir
   (§6), aparece una segunda lista: «Incluir la cuota» o «Dejarla a su canal».
4. **Cambie la fecha proyectada** las veces que quiera y pulse **«Simular»**: todo se vuelve a liquidar a esa fecha con la información de ese
   momento. Mientras no pulse «Simular» la pantalla avisa que los valores son de la fecha anterior. La fecha no puede ser anterior a hoy.
5. **Al pie**: total recogido, **neto a girar**, cuotas que se liberan y la cuota del crédito nuevo, para compararlas.
6. **«Detalle»** de cada producto despliega el desglose por concepto (capital, interés corriente, interés devuelto o cobrado por
   proporcionales, mora, seguro, administración, otros cargos, anticipo a favor), el interés explicado renglón a renglón con sus días (§7), la
   cuota causada con su envío y qué pasará con ella, y los avisos.
7. **«Imprimir simulación»** baja un PDF con el membrete: el asociado, los productos marcados con su desglose, el neto y la fecha proyectada,
   como la simulación de cancelación del estado de cuenta. Queda en la auditoría de exportaciones.

**Avisos que no bloquean**: un crédito que ya está en otra solicitud viva para recogerlo dice «Está en la solicitud N» (o «en otra solicitud en
borrador»): se puede marcar igual, y la que se desembolse después lo encontrará cancelado y lo omitirá. **Lo que sí bloquea**: si lo
marcado deja el neto a girar en cero o negativo, la pantalla lo avisa y la solicitud no se radica así (suba el monto o recoja menos).

Ver liquidaciones queda en la auditoría (`Lending.Payoff.Viewed`, uno por crédito liquidado) cada vez que se pulsa «Simular»; marcar y
desmarcar no vuelve a consultar.

## 4. Radicar, corregir y decidir

**Lo que guarda la solicitud.** Por cada producto: el tipo, el modo, el valor pedido (parcial o abono), el tratamiento de la cuota causada, el
modo de intereses, lo proyectado con su desglose, la cuota causada y su envío, la fecha proyectada y hasta cuándo se tomó la información.
Al radicar se vuelve a proyectar con la fecha guardada y la información de ese momento. Una fecha proyectada que ya pasó se lleva a hoy.

**Las validaciones miden la situación después de recoger.** Capacidad de pago, capacidad de descuento (nómina propia y libranza), endeudamiento
consolidado, cupo y concentración ya no cuentan dos veces lo mismo:

- un crédito recogido **total** deja de contar con su cuota y su capital;
- uno **parcial** libera de capital el valor pedido (topado en su capital), pero **su cuota sigue contando**;
- el detalle de cada validación dice cuánto liberó la recogida;
- las cuentas de aportes y ahorros no cambian ninguna validación: no son exposición ni liberan cuotas de crédito.

**Quién decide no cambia por recoger.** El nivel de atribución se sigue calculando sobre el valor bruto del crédito nuevo: más exposición
significa un nivel más alto, y la huella del expediente no se mueve con cada proyección.

**Al guardar o radicar se rechaza** lo que no es del solicitante como deudor o titular, lo que la línea del crédito nuevo no admite, el modo
no admitido, lo que no se puede recoger y el neto que no es positivo (códigos en §13).

**Corregir.** Una solicitud con recogidas se corrige como cualquier otra (antes la pantalla se negaba). Lo que se recoge es el grupo
**«Créditos que recoge»** de `Originacion.GruposEditablesAlDevolver`: si está entre los editables, la tabla se puede cambiar; si no, se ve de sólo
lectura y la solicitud las conserva tal como estaban. «Qué cambió desde la devolución» las compara como cualquier otro dato.

**El expediente.** Quien decide ve **«Lo que recoge»**: cada producto con su modo, valor pedido, lo proyectado y su desglose, la cuota causada
y qué pasa con ella; el total recogido, el neto a girar y la fecha proyectada. Si otro crédito vivo lo recoge también, lo dice.

**Solicitudes de antes de la 028.** Las que ya autorizaban recoger créditos se copiaron como recogidas **sin proyección**: el desembolso no tiene
con qué compararlas y no pide reconocer diferencias por ellas.

## 5. Desembolsar (quien desembolsa)

**Las recogidas salen de la solicitud.** En `/cartera/desembolsos` la solicitud aprobada trae **todas** sus recogidas autorizadas, sin elegirlas
de nuevo (D9). Para quitar una, se devuelve la solicitud para corregirla. Sólo el **primer tramo** recoge; los siguientes no.

**La vista previa recalcula todo** a la fecha valor con la información de ese momento. La tabla **«Lo que recoge»** muestra por producto:

| Columna | Qué dice |
|---|---|
| Modo | Total, parcial (abono a capital), cuotas o abono; «sin la cuota N» si deja la cuota causada |
| Proyectado | Lo que se proyectó en la solicitud («Sin proyección» en las de antes de la 028) |
| Actual | Lo que cuesta hoy |
| Diferencia | Actual menos proyectado. **En ámbar con «+»**: recoger cuesta más y el asociado recibe menos. **En verde con «−»**: cuesta menos y el neto sube |
| Causa | Por qué cambió (abajo) |
| Se aplica | Lo que se va a aplicar |
| Estado | Si se recoge, se omite o bloquea |
| Detalle | La diferencia por concepto, la cuota causada y los avisos |

**Las causas de una diferencia**:

| Causa | Cuándo |
|---|---|
| Pagos recibidos | El producto recibió un pago, un reverso o una resolución de saldo a favor después de la proyección; también un anticipo mayor |
| Causación | Corrieron días: interés, seguro o administración cambiaron con el capital igual; en una cuenta, vencieron más cuotas |
| Mora | Cambió el interés de mora |
| Cuota causada | Cambió la cuota causada (por ejemplo, empezó un período nuevo) |
| Parámetros | Cambió el modo de intereses que se guardó |
| Ya cancelado | El producto se canceló por otro camino |
| Otros movimientos | Otra cosa (por ejemplo, una factura que subió) |

**Lo que se omite y lo que bloquea.** Un producto ya cancelado, anulado, novado o sin saldo (una cuenta cancelada o sin cuotas pendientes, una
factura ya pagada) **se omite**: no se recoge, el neto sube y hay que reconocerlo. Un crédito que entró en mora después de la aprobación bloquea
el desembolso con «requiere reestructuración»; uno castigado, en insolvencia o pendiente de giro, también.

**Reconocer.** Si hay una diferencia en cualquier concepto o un producto omitido —aunque el total sea el mismo— aparece la casilla **«Revisé las
diferencias de lo que se recoge»** y sin ella no se desembolsa. Quien la marca queda en la auditoría junto con lo proyectado, lo actual, lo
aplicado y la causa.

**Recalcular o mantener lo proyectado** (`Recogida.AlDesembolsar`, por línea del crédito nuevo):

- **Recalcular** (de fábrica): se aplica lo de ahora.
- **MantenerLoProyectado**: se aplica el menor entre lo proyectado y lo de ahora. Si con eso el producto no queda cancelado, se aplica como
  abono a capital y **el producto queda con el saldo que falta**; la vista previa lo dice en el producto y en los avisos.

**La tolerancia** (`Recogida.ToleranciaDeDiferencia`, en pesos). Vacía de fábrica: ninguna diferencia bloquea, sólo se avisa y se reconoce. Con
valor, una diferencia (a favor o en contra) mayor que ese valor **bloquea** y pide devolver la solicitud para volver a mostrarle al asociado lo
que recibe. No cuenta para los omitidos ni para las recogidas sin proyección.

**Recogida parcial de un crédito.** Cubre primero lo exigible y abona el resto **a capital** con la preferencia de abono de la solicitud
(reducir cuota o plazo; sin preferencia, plazo). Ya no queda como anticipo (D7). Si el valor alcanza para todo, es una cancelación total.

**Confirmar.** Al pulsar «Desembolsar» todo se vuelve a liquidar bajo el cerrojo. Si algo cambió desde la vista previa (un pago llegó en ese
momento), la respuesta pide otra vista previa (`Lending.Disbursement.PreviewChanged`) y nada queda hecho. Si lo aplicado no fuera lo liquidado,
el desembolso se detiene (`Lending.Payoff.AppliedDiffers`).

**El resultado** dice bruto, recogido y **neto girado en la orden**, la tabla «Lo que se aplicó» (con la cuota dejada a su canal y el recibo de
las facturas) y «Lo que no se recogió» con el motivo. **La orden de giro es el bruto menos lo aplicado a cada producto, al peso**: la vista
previa, el resultado y la orden dicen lo mismo.

**Contabilidad antes de confirmar.** Si falta la regla de la matriz para las recogidas (el puente de la recogida propia) y
`Contabilidad.FaltaDeRegla` está en «Bloquear», la vista previa lo dice y no se desembolsa.

**Giro nuevo.** Si Tesorería rechaza el giro y se emite uno nuevo, el giro nuevo lleva las mismas recogidas vivas y el mismo neto que la orden.

**Anular el crédito nuevo antes del giro.** Cada recogida se reversa en la misma transacción —el pago del crédito, la consignación de la
cuenta, el recibo de la factura— y los productos recogidos vuelven a su estado, con sus codeudores.

**Garantías y pagaré del crédito recogido.** Se liberan, como siempre, con su acto y su motivo, pero **sólo cuando el crédito nuevo quedó
girado** (D12). Mientras tanto la garantía y el pagaré dicen «Por liberar al girar el crédito N» y liberarlos responde
`Lending.Collateral.PendingNewLoanDisbursement`. Así, si el crédito nuevo se anula antes del giro, el viejo vuelve vivo con sus garantías.

## 6. La cuota causada y la cuota enviada

### 6.1 Dónde se ve

La solicitud, el detalle, el expediente, la vista previa del desembolso y la caja dicen, por crédito, si su cuota del período ya está en su
canal:

| Estado | Qué significa |
|---|---|
| Enviada a la pagaduría, sin respuesta | Salió en el archivo de descuentos y la pagaduría no ha respondido |
| Descontada por la pagaduría, pendiente de giro | La pagaduría la descontó y no la ha girado |
| No descontada por la pagaduría | La pagaduría respondió que no la descontó |
| En la nómina propia (novedad generada) | La nómina tiene la novedad del período, sin calcular |
| En la nómina propia (corrida en borrador) | La nómina ya la calculó y no la ha aprobado |
| Aplicada por la nómina propia | La nómina se aprobó: la cuota ya está pagada |

El texto nombra la cuota, el valor, la pagaduría, el archivo y el período; por ejemplo: «La cuota 7 por $312.400 ya se envió a la pagaduría …
en el archivo … de noviembre y no ha respondido».

### 6.2 Qué se recoge de la cuota causada (`Recogida.CuotaCausada`)

Se lee con la línea **del crédito que se recoge** (D1), porque depende de cómo se paga ese crédito.

- **Incluir** (de fábrica): se cancela todo, incluida la cuota causada, como siempre. Lo que pase después lo dice la pantalla:
  - en libranza, lo que la pagaduría gire por esa cuota sigue `Recogida.GiroPosteriorDeCreditoRecogido` (§6.4);
  - en nómina propia con la corrida en borrador, al aprobar la nómina el descuento de ese crédito baja a cero con aviso y el neto del empleado
    sube: no hay doble cobro;
  - si la nómina ya se aprobó, la cuota ya está pagada.
- **Excluir**: se cancela **todo menos la cuota causada** (su capital, interés, seguro y administración). El crédito viejo queda vivo **sólo con
  esa cuota**, sale de los archivos de descuentos siguientes y queda cancelado, al peso, cuando su canal la paga. Su detalle dice «Cuota dejada a la
  pagaduría: cuota N por $X. El crédito queda cancelado cuando se pague.»
- **Preguntar**: el asesor elige por crédito, en la solicitud, «Incluir la cuota» o «Dejarla a su canal» (D2). La elección queda en la solicitud.

**Cuándo no se puede dejar**, y la pantalla dice por qué: un crédito de **vivienda** (en UVR o en pesos: la Ley 546 de 1999 no admite
penalidad por prepago), uno **sin cuota causada** a la fecha (la fecha es un vencimiento, está en gracia o el período de la próxima no ha
empezado), uno sin condiciones selladas, una recogida **parcial**, o una cuota **ya vencida** (descontada y pendiente de giro). En esos casos se
recoge completo.

Al desembolsar, el tratamiento se vuelve a resolver con el parámetro de la fecha valor: una solicitud proyectada a una fecha sin cuota causada,
con «Excluir», la deja si al desembolsar la hay (con la causa «Cuota causada»).

### 6.3 El interés de la cuota que se deja

La Ley 1555 de 2012 da el prepago sin penalidad con los intereses liquidados al día del pago. Por eso (D13 ajustada por el dueño):

- **Dentro del tope legal del prepago** (o si faltan los valores nacionales para saberlo: el SMMLV o `PREPAGO_TOPE_SMMLV` a la fecha del
  desembolso), la cuota se deja con el **interés proporcional**: sobre todo el saldo hasta el día de la recogida y, de ahí a su vencimiento,
  sólo sobre su propio capital.
- **Fuera del tope**, la cuota queda **tal como se envió**, con el interés del período completo: la pagaduría la paga exacta y el crédito
  cierra al peso.
- **Vivienda**: no se deja; se recoge completa.

Esto no depende de `Recogida.InteresesProporcionales`: sólo el motivo legal cambia la cuota dejada.

**Ejemplo.** Un crédito por libranza debe $800.000 de capital en dos cuotas de $400.000: la cuota 2 vence el 15 de mayo y la 3 el 15 de junio.
La tasa es 1,5 % mensual (0,05 % diario, año de 360 días). La cuota 2 ya salió en el archivo de descuentos por **$412.000** ($400.000 de capital y
$12.000 de interés del mes sobre $800.000). Un crédito nuevo lo recoge el **30 de abril** con «Excluir»:

| | Dentro del tope | Fuera del tope |
|---|---|---|
| Interés del 16 al 30 de abril, sobre $800.000 | $6.000 | — |
| Interés del 1 al 15 de mayo, sólo sobre los $400.000 de la cuota | $3.000 | — |
| Interés de la cuota dejada | $9.000 | $12.000 (el del período completo) |
| **La cuota queda en el crédito por** | **$409.000** | **$412.000** (tal como se envió) |
| Lo que cancela la recogida | $400.000 (la cuota 3, sin interés: su período no ha empezado) | $400.000 |

Dentro del tope, si la pagaduría gira los $412.000 que se le enviaron, los **$3.000 de más** siguen `Recogida.GiroPosteriorDeCreditoRecogido`
(§6.4); en nómina propia, vuelven al neto del empleado. La pantalla lo explica con estas mismas cifras.

### 6.4 Lo que la pagaduría gira después (`Recogida.GiroPosteriorDeCreditoRecogido`)

Cuando la pagaduría gira algo por un crédito que ya se recogió (el excedente del ejemplo, o una cuota que se incluyó en la recogida):

- **SaldoDeLaPagaduria** (de fábrica, como siempre): queda como saldo a favor de la pagaduría, que sólo se le devuelve o se aplica a un recibo
  suyo.
- **AlCreditoNuevo** (la recomendada para COOFLOPAL, D3; se configura, no es el defecto): se aplica al crédito que lo recogió como pago de la
  pagaduría con valor libre —cubre primero lo exigible y deja el resto como anticipo—, con la fecha del descuento (nunca antes del desembolso
  nuevo) y su comprobante.
- **AFavorDelAsociado**: queda como saldo a favor del asociado, que se resuelve en Saldos a favor de Recaudo.

Rige sólo para los créditos **recogidos**. Lo que la pagaduría gire por un crédito que se canceló **en caja** sigue quedando como saldo de la
pagaduría.

### 6.5 La respuesta de la pagaduría sobre un crédito cerrado

Si la respuesta de la pagaduría llega cuando el crédito ya está cancelado, anulado o novado, la línea queda marcada **«descuento de un crédito
cancelado»** y:

- no crea un no aplicado ni sigue la regla del convenio;
- no avisa al deudor como si estuviera en mora;
- no queda pendiente de giro, no alerta giro atrasado y no cuenta para centrales;
- el cuadre `payroll-deduction-reconciliation` lo muestra con ese motivo y con el pendiente de giro en cero.

Un crédito **castigado** no cuenta como cerrado: sigue recibiendo pagos. Si el crédito vuelve a estar vivo (se anuló el crédito nuevo y se
reversó la recogida), su pendiente de giro vuelve.

### 6.6 La cuota dejada que no se paga

Si la pagaduría responde que **no descontó** la cuota dejada, sigue la regla del convenio como cualquier cuota no descontada (pasa a caja, va a
cobranza o al archivo siguiente) y **se avisa a quien radicó** la solicitud del crédito nuevo, en la aplicación y por correo. Lo mismo si la
nómina propia no la descuenta. Si la cuota vence antes de que la pagaduría gire, sigue la regla de mora y de giro de pagaduría de siempre.

### 6.7 La caja: «Todo para cancelar (F7)» con cuota enviada (`Pagos.CuotaEnviadaAlCancelar`)

La caja **siempre avisa** en el ítem que la cuota del período ya va a la pagaduría o a la nómina, y la lista de lo que puede pagar dice «cuota N
enviada a la pagaduría/nómina». Qué hace la cancelación lo decide la línea del crédito:

- **Incluir** (de fábrica): la cancelación cobra todo, como siempre.
- **Excluir**: F7 propone la modalidad **«Todo menos la cuota enviada»**: cancela todo salvo esa cuota, que queda en el crédito (con el interés
  proporcional dentro del tope legal, tal como se envió fuera de él, §6.3) para que la pague su canal.
- **Preguntar**: una casilla en el ítem, «Dejar la cuota enviada para que la pague su canal».

Sólo se ofrece mientras la cuota siga en su canal sin respuesta (archivo sin resultado o nómina sin aprobar). El detalle está en
[recaudo-manual-del-cajero.md](recaudo-manual-del-cajero.md) §13.

## 7. Intereses proporcionales (`Recogida.InteresesProporcionales`)

Decide cómo se cobra el interés de la cuota en curso del crédito recogido a la fecha del desembolso. Se lee con la línea del crédito que se
recoge.

- **ComoAlCancelar** (de fábrica): exactamente lo que cobraría una cancelación en caja, según «¿Devuelve los días no corridos al cancelar?»
  (`Pagos.InteresesAlPagarAntesDelVencimiento`).
- **Sí**: el interés corrido. Se cobra lo causado hasta la fecha del desembolso; si la causación va adelantada (por período calendario, lo
  causado de una cuota cuyo período no ha empezado), **se devuelve**.
- **No**: se cobra completo el interés de la cuota en curso, también el de los días que faltan hasta su vencimiento.

**Siempre proporcional**, aunque diga «No»: en vivienda y dentro del tope legal del prepago (o sin los valores nacionales para saberlo). La
pantalla dice por qué.

**Ejemplo** (causación por período calendario, la cuota de noviembre causada completa el 1/11, recogida el 20/11): con «Sí» se devuelven los
10 días que faltan; con «No» se cobra la cuota completa; con «ComoAlCancelar», lo que diga el parámetro de la caja.

**El interés explicado.** El detalle del producto, la vista previa y el PDF muestran el interés renglón a renglón, con los días contados con la
convención del plan: lo **corrido** de la cuota en curso hasta la fecha, la **cuota completa** (los días que faltan hasta el vencimiento), el
**proporcional** por motivo legal, lo **devuelto** de un período sin empezar y lo **reconocido que se libera** (lo reconocido al inicio del ciclo
y no causado a la fecha: es informativo, no cambia el valor).

## 8. Aportes y ahorros

Se activan con `Recogida.ProductosAdmitidos` (general o por línea del crédito nuevo). El valor es un texto `Producto:modo,modo;Producto:modo`;
por ejemplo, para recoger créditos, cuotas y abonos de aportes y abonos al ahorro a la vista:

```
Creditos:Total,Parcial;Aportes:Cuotas,Abono;AhorroALaVista:Abono
```

| Producto | Modos posibles |
|---|---|
| `Creditos` | `Total`, `Parcial` |
| `Aportes` | `Cuotas`, `Abono` |
| `AhorroALaVista` | `Abono` |
| `AhorroPermanente` | `Abono` |
| `AhorroContractual` | `Cuotas` |
| `FacturasComerciales` | `Total`, `Parcial` |

Un texto vacío o mal escrito se lee como el defecto: sólo créditos, total o parcial.

- **Cuotas**: paga exactamente las cuotas pendientes que vencen a la fecha del desembolso o antes. Sin cuotas pendientes no hay nada que
  recoger.
- **Abono**: un aporte extraordinario (si la línea de aportes lo admite; si no, sólo por cuotas) o una consignación al ahorro.
- Rigen **las reglas de cada línea**: mínimo no reducible, límite de participación, extraordinario admitido, embargos y topes. Una cuenta se
  recoge si está activa o inactiva y su titular no figura como fallecido.
- El movimiento entra por el registro de cuentas, en la transacción del desembolso, y el libro de la cuenta dice «Por el desembolso … del
  crédito …».
- Al desembolsar, una cuenta cancelada o sin cuotas pendientes se omite; la diferencia de una cuenta es por pagos (menos) o por cuotas que
  vencieron desde la proyección (más, «Causación»).

## 9. Facturas a crédito de Comercial

El crédito nuevo puede pagar facturas a crédito de Comercial del asociado (los servicios que no son cartera, como en COOFLOPAL) con un
**recibo de Recaudo sin caja** que nace en la transacción del desembolso: un recibo por desembolso, una porción por factura. Como el recibo
emite el mismo hecho que un pago en caja, Contabilidad, Recaudo y Transporte se enteran igual (el cargo de Transporte queda pagado).

**Lo que tiene que preparar el administrador** (una sola vez):

1. **Admitir las facturas** en `Recogida.ProductosAdmitidos` (`FacturasComerciales:Total,Parcial`).
2. **Crear un tipo de documento de la clase «Recogida en el desembolso»** en Recaudo → Configuración → Tipos de documento
   (`/recaudo/configuracion/tipos-de-documento`), activo. **No se siembra**: cada cooperativa lo crea, con su prefijo y su consecutivo propio,
   que no usa ningún otro camino.
3. **El medio `RECOGIDA`** («Recogida en el desembolso», clase del mismo nombre) lo deja el sistema, activo. No mueve dinero, no se arquea y
   **no se ofrece** en ninguna caja ni punto de venta. No se crea otro ni se pasa otro medio a esa clase.
4. **La regla de Recaudo**: en la matriz de Recaudo (`/contabilidad/recaudo/matriz`), el rol **«Recogida en el desembolso»** del hecho «Recibo de
   caja» (y del «Recibo anulado») con **la misma cuenta que el puente de la recogida propia de Cartera** (§10). El documento cruce del «Recibo
   de caja» en los tipos de comprobante de Recaudo debe ser `FV`, para que la cuenta por cobrar salde contra la factura.

Sin el tipo de documento o sin el medio, la factura se ve en la lista con «Recaudo no está listo para recogerla» y el motivo.

**Al desembolsar**: Recaudo vuelve a leer el saldo de cada factura; si ya no alcanza para lo que se iba a aplicar, el desembolso se detiene y pide
revisar la vista previa. Si Recaudo no puede emitir el recibo, **el desembolso no existe** y se dice por qué. Una factura sin saldo se omite.

**Al anular el crédito nuevo** antes del giro, el recibo se anula primero (espejo exacto, sin saldo a favor) y después se reversa lo demás. Desde
la caja ese recibo **no se anula**: se anula con el crédito.

## 10. Contabilidad (la contadora)

Cartera sigue sin conocer cuentas: emite los hechos de siempre (desembolso, pago aplicado, consignación, recibo) y la matriz pone las cuentas.
Lo que cambió es **cómo reparte el comprobante del desembolso (`DS`)**:

| Línea del `DS` | Débito | Crédito |
|---|---|---|
| Capital del crédito nuevo | el bruto | |
| `DesembolsoPorGirar` (desembolsos por girar) | | **sólo el neto** que gira Tesorería |
| `RecaudoPorAplicar` de la clase `OwnLoanPayoff` (el puente de la recogida propia), una por producto recogido | | lo recogido, con el deudor como tercero, el producto recogido como documento cruce y la sucursal del producto recogido |

El recibo de cada recogida (`RC` del crédito, `AO`/`MA` de la cuenta) **debita ese mismo puente** con las mismas dimensiones. **El puente queda
en cero solo**, sin que nadie tenga que hacer coincidir dos reglas. Ejemplo (caso dorado 01): un crédito de $10.000.000 que recoge $2.300.000
lleva capital $10.000.000 al débito, desembolsos por girar $7.700.000 y el puente $2.300.000 al crédito; el `RC` del recogido debita el puente
por $2.300.000 contra su capital y sus intereses.

**Antes de la 028** el `DS` acreditaba el bruto a «desembolsos por girar» y sólo cuadraba si la regla del puente `OwnLoanPayoff` apuntaba a la misma
cuenta (lo que hace hoy COOFLOPAL), y aun así no cuadraba en giro a tercero, con regla por línea o con documento cruce. Ahora:

- **se puede dejar como está** (la misma cuenta de «desembolsos por girar»: cuadra con una línea por crédito) **o pasar a una cuenta puente
  propia**, que puede exigir tercero y documento cruce. La naturaleza de esa cuenta no importa y la completitud no avisa por ella;
- **si se cambia la regla**, la vigencia nueva debe empezar **después del último desembolso con recogidas ya contabilizado** (lo lista el
  diagnóstico de [recogida-de-creditos-despliegue.md](../operaciones/recogida-de-creditos-despliegue.md)).

**El puente con saldo** son recogidas que esperan un giro nuevo: si Tesorería rechaza o anula la orden, el reverso del desembolso (`DRV`) es el
espejo del `DS` —«desembolsos por girar» vuelve por el neto y el puente recibe lo recogido— mientras las recogidas siguen aplicadas. El giro nuevo
lo cierra; si se anula el crédito, lo cierran los reversos de las recogidas. La conciliación lo espera.

**«Desembolsos por girar» en la conciliación** es la suma de las órdenes **emitidas**. Una orden rechazada o un cheque no cobrado ya no cuentan:
su reverso ya le devolvió el valor a la cuenta. Antes se contaban y la conciliación descuadraba por el neto, con recogidas o sin ellas.

**Aportes y ahorros.** La consignación sale con su hecho de siempre (`AporteRecibido` o `ConsignacionEnCuentaAplicada`) y debita el mismo puente
con la clase `OwnLoanPayoff`; el `DS` usa el número de la cuenta como cruce. La completitud pide el mapeo de esos hechos con la clase
`OwnLoanPayoff` cuando la cooperativa admite recoger cuentas.

**Facturas: la única excepción al «cuadra solo».** El débito del puente lo pone el recibo de Recaudo con **su** matriz (rol «Recogida en el
desembolso»), y el crédito lo pone el `DS` con la de Cartera (el puente, con la factura de cruce y tipo `FV`). Son reglas de dos matrices: la
completitud de Cartera avisa `Accounting.LendingRule.InvoicePayoffBridgeMismatch` si falta la de Recaudo o las cuentas no coinciden. La
validación previa de la vista previa no pregunta por la regla de Recaudo; si falta y Recaudo está en «Bloquear», el desembolso no existe y dice
por qué.

**Un solo comprobante** (`Accounting.LendingDisbursementWithPayoffsInOneVoucher`, «¿El desembolso y sus recogidas salen en un solo comprobante?»,
Administración → Parámetros del sistema, módulo `ACC`, **No** de fábrica). Con **Sí**, el `DS` lleva también el recibo de cada crédito recogido y
la consignación de cada cuenta: el puente entra y sale en el mismo comprobante y la descripción dice «· recoge N créditos». El recibo de las
facturas sigue aparte, en el lote de Recaudo. Si algo impide juntarlos, salen en comprobantes separados que cuadran igual.

**Giro nuevo.** El hecho del giro nuevo lleva las mismas recogidas vivas y el mismo neto que su orden.

## 11. Informe «Recogidas» y seguimiento

**El informe** (`/cartera/informes?vista=payoffs`, o Centro de reportes → «Recogidas») lista los productos que recogieron los desembolsos del
período. Filtros: fechas, sucursal y línea del crédito nuevo, «Producto recogido» (créditos, aportes y ahorros, facturas a crédito) y, por el
enlace del informe, el asesor.
Columnas: fecha, crédito nuevo, solicitud, deudor, oficina, línea, producto, tipo, modo, proyectado, aplicado, diferencia, causa, cuota causada
(incluida o «Dejada a su canal: cuota N por $X»), interés devuelto, interés cobrado, asesor, decisor, quién desembolsó y estado. El estado es
«Aplicada», «Reversada al anular el crédito nuevo» u omitida (el producto ya estaba cancelado); los totales no cuentan las reversadas. La
diferencia se muestra sólo si hubo proyección. Se exporta a Excel, PDF y Word.

**En el detalle del crédito**:

- el recogido dice «Recogido por el crédito N el dd/mm/aaaa», con el botón «Ver el crédito N», y, mientras no se pague, la cuota dejada a su
  canal;
- el nuevo dice «Recogió: crédito … (total, $…); …»;
- en el libro del crédito y en el informe de transacciones, la fila del pago dice «Recogido con el crédito …»;
- en el estado de cuenta de la persona aparece en los movimientos (F8);
- las garantías y el pagaré del recogido dicen «Por liberar al girar el crédito N» hasta el giro.

**La auditoría** del desembolso lleva por producto lo proyectado, lo actual, lo aplicado, la diferencia, su causa y quién la reconoció; la de la
solicitud, las recogidas pedidas.

## 12. Los parámetros

Todos con vigencia. Los de Cartera se cambian en Parámetros de Cartera (`/cartera/parametros`, «Nueva vigencia»). **De fábrica ninguno cambia
lo que había.**

| Clave | Pregunta | Valores | De fábrica | Ámbito |
|---|---|---|---|---|
| `Recogida.CuotaCausada` | ¿Qué se recoge de la cuota causada del crédito que se recoge? | `Incluir` (todo, incluida la cuota), `Excluir` (todo menos la cuota), `Preguntar` (el asesor elige por crédito) | `Incluir` | General y línea del **crédito que se recoge** |
| `Recogida.InteresesProporcionales` | ¿Se cobran o devuelven los intereses proporcionales a la fecha de la recogida? | `ComoAlCancelar`, `Si`, `No` | `ComoAlCancelar` | General y línea del **crédito que se recoge** |
| `Recogida.ProductosAdmitidos` | ¿Qué productos y de qué forma recoge un crédito nuevo? | Texto `Producto:modo,modo;…` (§8) | `Creditos:Total,Parcial` | General y línea del **crédito nuevo** |
| `Recogida.AlDesembolsar` | Al desembolsar, ¿se recoge lo de ahora o lo proyectado? | `Recalcular`, `MantenerLoProyectado` | `Recalcular` | General y línea del **crédito nuevo** |
| `Recogida.ToleranciaDeDiferencia` | Diferencia en pesos desde la cual el desembolso pide devolver la solicitud | Pesos; vacío = nunca bloquea | vacío | General |
| `Recogida.GiroPosteriorDeCreditoRecogido` | ¿Qué se hace con lo que la pagaduría gira después por un crédito ya recogido? | `SaldoDeLaPagaduria`, `AlCreditoNuevo`, `AFavorDelAsociado` | `SaldoDeLaPagaduria` | General |
| `Pagos.CuotaEnviadaAlCancelar` | ¿Una cancelación en caja cobra la cuota ya enviada a la pagaduría o a la nómina propia? | `Incluir`, `Excluir`, `Preguntar` (el cajero elige) | `Incluir` | General y línea del crédito que se cancela |
| `Accounting.LendingDisbursementWithPayoffsInOneVoucher` | ¿El desembolso y sus recogidas salen en un solo comprobante? | Sí / No | No | Parámetros del sistema, módulo `ACC` |

Otros que intervienen: `Pagos.InteresesAlPagarAntesDelVencimiento` («¿Devuelve los días no corridos al cancelar?», el que usa `ComoAlCancelar`),
`Originacion.GruposEditablesAlDevolver` (el grupo «Créditos que recoge») y `Contabilidad.FaltaDeRegla`.

**La relación entre los dos de intereses**: `Recogida.InteresesProporcionales = Si` hace el mismo cálculo que «Corrido» y `No` el de
«CuotaCompleta», aplicados sólo a la recogida. Con `ComoAlCancelar` la recogida cobra exactamente lo que cobraría la caja. Las excepciones
legales (vivienda, tope del prepago) rigen en los dos.

## 13. Códigos de error y qué hacer

| Código | Qué pasó | Qué hacer |
|---|---|---|
| `Lending.Payoff.RequiresRestructuring` | Un crédito a recoger está en mora | Tramitarlo en Novedades como una refinanciación que lo recoge |
| `Lending.Payoff.NotPayable` | Castigado, en insolvencia que negocia, sin saldo, pendiente de giro o no vigente; una cuenta que no recibe operaciones, de titular fallecido o sin cuotas pendientes; una factura que Recaudo no puede recoger | Quitarlo de lo que se recoge (al desembolsar, devolver la solicitud) |
| `Lending.Payoff.ProductNotAdmitted` | La línea del crédito nuevo no admite ese producto (`Recogida.ProductosAdmitidos`), o es un CDAT | Recoger otra cosa, u otra línea; el administrador decide el parámetro |
| `Lending.Payoff.ModeNotAdmitted` | La línea no admite ese modo; la línea de aportes no admite extraordinarios | Elegir uno de los modos que dice el mensaje |
| `Lending.Payoff.NotOfApplicant` | El producto no es del solicitante como deudor o titular (por ejemplo, es codeudor), o la factura no tiene saldo a su nombre | Recoger sólo sus propios productos |
| `Lending.Payoff.NetNotPositive` | Lo recogido deja el neto a girar en cero o negativo | Subir el monto o recoger menos |
| `Lending.Payoff.InstallmentTreatmentNotAdmitted` | Se pidió dejar la cuota causada y la línea del recogido dice «Incluir» | Incluirla, o que el administrador cambie el parámetro |
| `Lending.Payoff.DateInPast` | La fecha proyectada de desembolso es anterior a hoy | Poner hoy o una fecha futura |
| `Lending.Payoff.MustMatchApplication` | El desembolso trae una lista de recogidas distinta de la de la solicitud, o es un tramo siguiente | Desembolsar sin lista; para cambiar lo que recoge, devolver la solicitud |
| `Lending.Payoff.NotRequested` | Un producto autorizado ya no existe o ya no es del solicitante | Devolver la solicitud para corregir lo que recoge |
| `Lending.Payoff.DifferencesNotAcknowledged` | Hay diferencias u omitidos y no se marcó la casilla | Revisar y marcar «Revisé las diferencias de lo que se recoge» |
| `Lending.Payoff.DifferenceAboveTolerance` | Una diferencia pasa de `Recogida.ToleranciaDeDiferencia` | Devolver la solicitud, corregirla y mostrarle al asociado lo que recibe |
| `Lending.Payoff.AppliedDiffers` | Al confirmar se aplicó algo distinto de lo liquidado; nada quedó hecho | Pedir otra vista previa y confirmar |
| `Lending.Disbursement.PreviewChanged` | Algo cambió entre la vista previa y la confirmación (un pago, por ejemplo) | Pedir otra vista previa |
| `Lending.Disbursement.NetNotPositive` | Al desembolsar, lo recogido no deja neto por girar | Devolver la solicitud |
| `Lending.Prevalidation.NotPostable` | Falta una regla de la matriz (el puente `OwnLoanPayoff`, el mapeo de la consignación) y la política es «Bloquear» | La contadora completa la matriz (`/contabilidad/cartera/completitud`) |
| `Lending.Collateral.PendingNewLoanDisbursement` | Se intentó liberar una garantía o anular el pagaré del recogido antes del giro del crédito nuevo | Esperar a que el crédito nuevo quede girado |
| `Receipts.LoanPayoff.NotReady` | Recaudo no tiene el tipo de documento «Recogida en el desembolso» o el medio `RECOGIDA` activo | Crear el tipo de documento o activar el medio (§9) |
| `Receipts.LoanPayoff.InvoiceChanged` | El saldo de una factura cambió desde la vista previa | Volver a revisar el desembolso |
| `Receipts.LoanPayoff.NotApplied` | Recaudo no pudo emitir el recibo de las facturas | Leer el motivo del mensaje; el desembolso no se hizo |
| `Receipts.Prevalidation.NotPostable` | La matriz de Recaudo no tiene la regla del recibo de la recogida | Completar el rol «Recogida en el desembolso» |
| `Receipts.LoanPayoff.VoidWithTheLoan` | Se intentó anular desde la caja el recibo de una recogida | Anular el crédito nuevo antes del giro |
| `Receipts.Receipt.ModalityNotAdmitted` | En caja, «Todo menos la cuota enviada» sin cuota enviada o con la línea en «Incluir» | Cancelar completo |
| `Request.BodyInvalid` | Una integración mandó `ownLoanPayoffs` y `payoffs` a la vez | Mandar una sola lista |
| `Accounting.LendingRule.InvoicePayoffBridgeMismatch` | Aviso de completitud: la regla de Recaudo de la recogida de facturas falta o no coincide con el puente de Cartera | Ponerles la misma cuenta |
| `Accounting.LendingFact.PayoffKindNotSupported` | Defecto de quien emitió el hecho | Reportarlo a soporte |

**Avisos que no bloquean** (en el comprobante del pago y en la pantalla): `Lending.Payment.CurrentInstallmentLeft` (se dejó la cuota causada),
`Lending.Payment.LeftInstallmentProportional` (se dejó con el interés proporcional), `Lending.Payment.NoInstallmentToLeave` (no había cuota que
dejar: se canceló todo) y `Lending.Payment.HousingPaidOffInFull` (vivienda: se canceló todo).

## 14. Lo que queda pendiente

- **La contadora**: decidir si el puente `OwnLoanPayoff` sigue en la cuenta de «desembolsos por girar» o pasa a una cuenta propia, completar las
  reglas de los productos que la cooperativa admita y revisar el diagnóstico de desembolsos con recogidas contabilizados antes de la 028.
- **El dueño**: confirmar la vivienda en pesos (se recoge completa, sin dejar la cuota) y el valor de `Recogida.GiroPosteriorDeCreditoRecogido`
  para COOFLOPAL (la recomendada es `AlCreditoNuevo`).
- **Fuera de alcance, anterior a la 028**: el giro nuevo de una compra con descuento condicionado sale sin la parte del fondo.
