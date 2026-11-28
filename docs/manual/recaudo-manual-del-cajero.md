# Recaudo: manual del cajero (R0 y R1)

> Feature 015 (Recaudo y Tesorería), entrega R0. Receta para **el cajero** (plantilla de rol `recaudo.cajero`). El contrato
> está en [contracts/api.md](../../specs/015-recaudo-tesoreria/contracts/api.md) y en
> [spec.md](../../specs/015-recaudo-tesoreria/spec.md) (US2, US3, US14); esto es cómo se trabaja en la ventanilla.

La caja de Recaudo recibe el dinero de las personas y lo **entrega** a los módulos dueños de cada obligación: Cartera (créditos) y
Captaciones (aportes y cuentas). Recaudo no decide cuánto se debe ni cómo se imputa un pago a una cuota: lo pregunta a cada módulo
por su contrato y le aplica lo que la persona eligió. Un recibo puede pagar varias obligaciones de varios titulares a la vez.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Abrir la sesión, ver lo esperado y arquear al cierre | `/recaudo/sesiones` | `Receipts.Session.Open` · `.Close` |
| La caja: buscar, elegir, cobrar y confirmar | `/recaudo/caja` | `Receipts.Receipt.Register` |
| Los recibos emitidos y su reimpresión | `/recaudo/recibos` | `Receipts.Receipt.View` · `.Print` |
| Pedir la anulación de un recibo | `/recaudo/anulaciones` | `Receipts.Voiding.Request` |
| Digitar los recibos del talonario de contingencia | `/recaudo/contingencia` | `Receipts.Receipt.RegisterContingency` |

El cajero sólo ve las cajas que tiene asignadas (alcance por caja o por punto) y necesita el **segundo factor inscrito**: es una
condición de la lista de go/no-go (`/admin/go-no-go`, condición `REC.SecondFactorCashiersAndSupervisors`).

## 2. Abrir la sesión

> 2026-11-23: la base y el arqueo se pueden contar con un solo valor o billete por billete (`Caja.FormaDeConteoDelEfectivo`), y con
> `Caja.SesionUnica` esta sesión abre y cierra también la caja del punto de venta. Ver [caja-unica.md](caja-unica.md).

1. En `/recaudo/sesiones`, elija su caja y abra la sesión con la base que le entregó el supervisor. Con `Caja.UnaSesionPorCajero`
   (por defecto sí) no puede tener dos sesiones abiertas.
2. La sesión pertenece al **día del punto**: si el supervisor ya cerró el día, no se abre otra hasta el día siguiente.
3. Con `Caja.ArqueoCiego` (por defecto sí) usted cuenta sin ver lo esperado; lo esperado lo ve quien tenga `Receipts.Session.ViewExpected`.

## 3. La caja en tres pasos

1. **Buscar a la persona**: por documento, nombre o el número de la obligación. Si quien paga no es el titular, registre al
   **pagador** (quien entrega el dinero) y al titular: el sistema marca el recibo como pagado por un tercero.
2. **Elegir qué paga**: la caja muestra lo que cada módulo dice que la persona puede pagar (cuotas exigibles, aportes, cuentas). La
   persona elige el valor de cada línea y, si paga de más en un crédito, **qué hacer con el excedente** (abono a capital con menor
   cuota o menor plazo, según lo que ofrezca Cartera): esa elección viaja al módulo tal como la dijo el deudor.
3. **Cobrar**: registre los medios (efectivo, transferencia y los demás ofrecidos en su punto) hasta cubrir lo aplicado; las vueltas
   sólo salen del efectivo. Los medios se imputan a las aplicaciones en el orden de `Recibo.OrdenDeImputacion` (inmediatos primero y
   el cheque al final). Confirme: el recibo toma su número **después** de validar todo y se entrega impreso.

Atajos: cada medio puede tener su tecla rápida (la define el supervisor en la configuración del medio).

## 4. Efectivo grande: declaración y quien efectúa

Desde `Sarlaft.UmbralDeclaracionOrigenFondos` (5.000.000 por operación, **pendiente de validar** por el oficial, P7) la caja exige la
**declaración de origen de fondos** con su soporte, una vez por recibo, y el documento y nombre de **quien efectúa** la entrega. Sin
ellas no se confirma (`Receipts.Compliance.DeclarationRequired`, `.DeliveredByRequired`).

Si la persona aparece en una **lista vinculante**, la operación queda bloqueada y no se recibe su dinero: no insista, el oficial de
cumplimiento ya recibió la alerta. En una lista **no vinculante**, según `Sarlaft.ListasNoVinculantes.Efecto`:
«Sigue» confirma con alerta; «Retiene» pide **retener** el efectivo en custodia de la sesión (botón «Retener»); el oficial lo
libera o lo devuelve por orden de pago.

## 5. Si el sistema dice «no contabilizable»

Con la matriz contable incompleta y la política `Contabilidad.FaltaDeRegla = Bloquear`, la caja no confirma
(`Receipts.Accounting.NotPostable`) y dice qué hecho y qué rol faltan. Avise al supervisor: lo completa Contabilidad en
`/contabilidad/recaudo/matriz`.

## 6. Contingencia (sin sistema)

Si la ventanilla se queda sin sistema, se recibe en el **talonario de contingencia** registrado para el punto. Al volver, digite cada
recibo en `/recaudo/contingencia` con la fecha valor del talonario, el número del talonario y **la imagen del recibo en papel**
(obligatoria). El informe de la pantalla muestra los números por digitar y los sin usar; la tarea diaria alerta los que faltan
(`Recaudo.ContingenciaPorDigitar`).

## 7. Cerrar la sesión

Cuente el efectivo por denominación y los demás medios en `/recaudo/sesiones` → «Arquear y cerrar». Una diferencia la aprueba el
supervisor (otra persona); un faltante aprobado se trata según `Caja.TratamientoFaltante` (por defecto, al gasto desde el 2026-11-26; con el arqueo sencillo, siempre al gasto).

## 8. Anular un recibo

Pida la anulación en `/recaudo/anulaciones` con su motivo; la aprueba otra persona con `Receipts.Voiding.Approve`. La anulación es
un documento nuevo: el recibo original no se borra ni se edita, y cada módulo reversa lo que había aplicado.

- **Cómo se pide**: «Anular» en la lista de Recibos (`/recaudo/recibos`), o «Pedir su anulación» en la caja tras confirmar el recibo, abre la
  pantalla con el **número** del recibo ya puesto; también se escribe a mano («5» encuentra el «000005»). La pantalla muestra el recibo y lo
  que aplicó antes de pedirla.
- **Quién aprueba**: otra persona con `Receipts.Voiding.Approve`, distinta de quien la pidió y del cajero que hizo el recibo. La pantalla y el
  menú los ve también quien sólo aprueba.
- **Anular sin segunda persona**: quien tenga `Receipts.Voiding.Direct` («Anular recibos sin una segunda persona», pensado para la contadora)
  anula cualquier recibo en un solo clic —el botón dice «Anular el recibo»—, también los que cobró o pidió ella misma, y aprueba las
  anulaciones que pidieron otros. El administrador de la cooperativa **no** lo recibe por su rol: se da con un rol propio (Seguridad › Roles)
  a la persona que corresponda. La auditoría marca esas anulaciones con `withoutSecondPerson`.
- **Qué queda**: lo pagado vuelve a quedar por pagar en cada crédito o cuenta (cuotas, intereses y saldos como antes del recibo). Si la sesión
  de caja sigue abierta y es el mismo día, el dinero sale de la caja; si ya cerró, queda como **saldo a favor** del pagador
  (`/recaudo/saldos-a-favor`), que se aplica en otro recibo o se devuelve por Tesorería.

## 9. Lo que trae R1

- **Medios de pago**: tarjeta débito y crédito (datáfono del adquirente, lote y aprobación; **nunca** escriba el número de la tarjeta),
  bonos por referencia y «otro» con su referencia. El arqueo de tarjetas se cuenta por lote.
- **Recibo completo**: «Todo lo exigible (F4)» trae de una vez lo que la persona debe hoy; «También como codeudor» suma los créditos donde
  es codeudora. Si Cartera o Captaciones todavía no se activaron y la persona está en la carga, use «Dejar pendiente»: el valor queda como
  **recaudo pendiente de aplicar** y se aplica el día de la activación. «Enviar por correo» manda el recibo sólo si la persona autorizó el
  canal.
- **Facturas comerciales**: en «qué paga» aparecen las facturas con saldo de ventas a crédito; «Cobrar» la agrega con su factura. No se
  cobra más que el saldo ni como concepto vario.
- **Egresos de caja** (`/recaudo/egresos`): retiro de ahorro, cancelación de cuenta con entrega, saldo a favor de un crédito o de un pagador y
  egreso vario (si la cooperativa lo encendió, con aprobador presente). Verifique la identidad y las firmas y, si recibe otra persona,
  regístrela: se cruza con las listas. Si la caja no tiene el efectivo, el sistema lo dice **antes** de pedir la autorización; abastézcala
  desde la bóveda. El documento del egreso se imprime y lo firman quien entrega y quien recibe. Anular un egreso sólo se puede si el dinero
  vuelve a esta misma caja, con aprobador.
- **Consignación reportada** (`/recaudo/consignaciones-reportadas`): registre la consignación que trae el asociado con su soporte; por
  defecto queda **por verificar** hasta que aparezca en el extracto.

## 10. La caja desde el 2026-11-16 (pedido del dueño)

- **Lo que puede pagar sale agrupado**: Cartera (créditos), aportes y ahorros, Contabilidad y Comercial (facturas) y conceptos varios. Cada crédito
  tiene «Detalle» (o **F3** sobre el marcado): lo exigible, lo vencido, lo que vale cancelarlo, el anticipo, los saldos por concepto y sus cuotas,
  con el tamaño de texto y los centavos que cada usuario eligió en el estado de cuenta.
- **Al elegir la persona ya no entra solo lo exigible** (desde el 2026-11-23): la caja muestra lo que puede pagar y lo exigible entra con
  **«Lo exigible (F4)»** o con **«Todo para cancelar (F7)»**. Si la cooperativa quiere el comportamiento anterior, enciende
  `Caja.CargarExigibleAlElegir` en los parámetros de Recaudo.
- **Todo para cancelar (F7)** (2026-11-18): reemplaza «Qué paga» por todo lo que la persona debe: cada crédito por su valor de cancelación, las
  cuotas pendientes de sus cuentas y el saldo de sus facturas. Los conceptos varios y las consignaciones libres no entran.
- **Con cuotas vencidas sólo se paga lo exigible o se cancela** (`Caja.ConMoraSoloExigible`, de fábrica Sí): sobre esa obligación no se abona a
  capital ni se anticipan cuotas (`Receipts.Receipt.OverdueOnlyDue`). Desde el 2026-11-26, «vencidas» son las cuotas cuya fecha ya pasó o que
  vencen hoy. Por período calendario (`Causacion.Periodo = PeriodoCalendario`), el interés ya causado de la cuota del mes en curso es exigible, pero
  **no es mora**: el asociado al día paga la cuota del período completa, anticipa o abona a capital, y **F4** trae la cuota del período completa,
  no sólo ese interés.
- **Lo vencido y además anticipar o abonar en el mismo recibo** (`Caja.ConMoraPermiteAnticipar`, de fábrica No; 2026-11-26): con la regla anterior
  encendida, este parámetro deja que el recibo pague lo vencido y además use cualquier modalidad, siempre que lo vencido se pague primero:
  - «Cuota del ciclo», «Anticipo», «Valor libre» y «Abono a capital» ya pagan primero lo exigible, así que se admiten tal cual.
  - Para anticipar cuotas, en «Detalle» se marcan como siempre. La caja pone antes un ítem «Lo exigible» por todo lo exigible y después las
    cuotas.
  - Si el mes en curso tiene interés ya causado, la cuota de ese período entra por su valor menos ese interés, que ya paga «Lo exigible».
  - Una cuota futura sin «Lo exigible» antes, en el mismo recibo, se rechaza con el valor vencido.
  - La cuota se anticipa aunque la línea no deje pagar cuotas fuera de orden: el recibo cubre primero las anteriores.
  - La simulación de cada ítem de un crédito se hace sobre lo que deja el ítem anterior del mismo crédito, y al confirmar se aplican en ese
    orden.
- **Descuento por pago anticipado** (2026-11-26): si la línea tiene descuento (`Pagos.DescuentoPorPagoAnticipado`), «Detalle» muestra por cuota el
  descuento y lo que se paga, y el cajero decide con una casilla si lo aplica. El crédito recibe la cuota completa y la cooperativa asume el
  descuento. Ver [descuento-por-pago-anticipado.md](descuento-por-pago-anticipado.md).
- **Sin nada exigible se paga al menos una cuota** (`Caja.AnticipoMinimoUnaCuota`, de fábrica Sí; `Receipts.Receipt.BelowOneInstallment`). Para
  anticipar varias, en «Detalle» se marca hasta qué cuota: cada una entra a «Qué paga» con su valor completo (capital, interés, seguro y otros, menos
  lo que ya tenga anticipado). El plan ya calcula el interés de la siguiente sobre el saldo después de la anterior. Desde esta fecha se anticipa por
  número aunque la línea no permita pagar cuotas fuera de orden, porque sin nada exigible no hay cuota anterior que saltarse.
- **Valor que entrega**: escriba lo que la persona entrega y pulse **Repartir (F6)**: cubre primero lo exigible de cada producto en el orden de
  prelación de la cooperativa y después lo propuesto; lo que sobre lo escribe usted en el producto que la persona quiera abonar.
- **Prelación por clase** (`Caja.PrelacionPorClase`, vacía de fábrica = libre; por ejemplo `Aportes,Creditos,Ahorros,Facturas,Conceptos`): mientras
  haya algo exigible de una clase anterior sin cubrir, no se abona a una posterior (`Receipts.Receipt.PriorityNotRespected`, con la clase y el valor
  pendiente).
- **Conceptos varios** (p. ej. SOAT): cada concepto dice a nombre de quién queda —quien paga, un tercero fijo como la aseguradora, o el que elija el
  cajero— y puede traer un valor sugerido; la cuenta sale de la matriz de Recaudo y se ve en la configuración del concepto. Si el concepto exige
  documento cruce, la caja lo pide.
- El servidor vuelve a validar todo al simular y al confirmar; la modalidad que la obligación no admite (`ModalityNotAdmitted`) y la obligación
  bloqueada (`ObligationBlocked`) también se rechazan aunque se intenten por fuera de la pantalla.
- **Cuota reconocida al inicio del ciclo** (feature 024, fase 4; sólo si la cooperativa usa `Causacion.Momento = InicioDelCiclo`, ver
  `docs/manual/cartera-causacion-y-liquidacion.md` §9.6): la cuota del período ya reconocida se ve como causada aunque no haya vencido.
  En «Detalle» la obligación dice cuánto está reconocido y todavía por causar (columna «Reconocido») y la cuota del ciclo sale marcada
  «reconocida»; al elegir la persona y con **F4** entra como lo exigible de esa obligación, por la cuota del ciclo completa (capital,
  interés, seguro y administración), si no hay nada vencido. Pagada antes del vencimiento
  queda como anticipo de esa cuota y se aplica al vencer —el capital se abona ese día, como siempre— y ya no sale el aviso de que el
  anticipo no abona a capital: con la cuota reconocida es lo esperado. La **mora** sigue contando sólo desde el vencimiento.

## 11. La caja desde el 2026-11-17 (pedido del dueño)

**La pantalla cabe en la ventana y se reparte de dos formas.** Los botones «Apilada» y «Columnas» de la barra superior cambian la
distribución, y el navegador recuerda la última:

- **Apilada** (por defecto): a la izquierda, arriba la persona con lo que puede pagar y abajo «Qué paga»; a la derecha, los medios de pago
  con los comandos.
- **Columnas**: las tres columnas de antes.

Cada zona se desplaza por dentro. «Qué paga» ya no crece sin fin: su total queda abajo, siempre a la vista, igual que la barra de atajos
(ahora con botones). Bajo 900 px de ancho todo va en una sola columna.

**Antes de cobrar, la columna derecha dice qué falta**: valor en cero, modalidad que no admite la mora, cuota mínima, elección del deudor
en un abono a capital, aceptación del aviso del anticipo, tercero o documento cruce de un concepto, un ítem que el módulo rechazó, o que lo
entregado no alcanza. Lo que el servidor rechazaría deja apagado «Cobrar» (F10); lo demás es un aviso. Cuando no hay nada, dice «Todo
listo para cobrar».

**Cada ítem muestra lo que paga**: los rubros (capital, interés corriente, interés de mora, seguro…), las cuotas que toca, lo que queda
como anticipo o saldo a favor y cómo queda el crédito (para cancelar, vencido, próxima cuota o «queda cancelado»). En una cuenta, los
períodos que cubre y el saldo después.

**Cuotas de aportes**: el botón «Cuotas» (o F3) de una cuenta de aportes o de ahorro permanente o contractual muestra sus cuotas
pendientes. Se marcan en orden, de la más antigua a la más nueva, y entran a «Qué paga» por lo que se debe de ellas.

**El recibo impreso** (tirilla o carta) trae para el asociado:

- el detalle de cada pago por rubro o por período y cómo quedó el producto;
- las formas de pago con su nombre, su referencia y lo que cubrió cada una;
- lo recibido y las vueltas;
- en carta, las firmas.

El desglose sale de la simulación que se confirmó; un recibo anterior a este cambio se imprime sin él.

**Arreglos**:

- **«Ha ocurrido un error inesperado»:** cambiar un valor justo después de «Repartir» o de F4 tumbaba la pantalla. Ahora cada cambio
  busca el ítem por su identificador, y un error de una acción se avisa sin tumbar la pantalla.
- **Concurrencia:** dos simulaciones al tiempo chocaban en el servidor. Ahora va una a la vez.
- **Sesión vencida:** si la consulta de la caja falla por algo distinto de «no tiene caja abierta», la pantalla dice el motivo.

## 11. La caja desde el 2026-11-23 (pedido del dueño)

- **Un solo cobro a la vez.** Al pulsar F10 (o «Confirmar el recibo») el botón se apaga y se ve «Procesando el pago…» hasta que termina:
  simular, registrar los medios y confirmar son un solo paso. Una segunda pulsación, o F10 sostenido, no hace nada.
- **Lo exigible no entra solo** al elegir la persona (ver §10): se agrega con F4 o F7, o uno a uno desde la lista.
- **Quitar todo (Ctrl+Supr)** vacía «Qué paga» y conserva a la persona y la lista de lo que puede pagar. El mismo crédito o la misma cuenta
  no entra dos veces: si ya está, la caja lo dice y su valor se cambia en «Qué paga».
- **Pago registrado.** Al confirmar se abre una ventana con el número del recibo en grande, lo pagado, lo recibido y las vueltas, con los
  botones de tirilla, carta y nueva operación. Enter abre la siguiente operación, pero no en el primer segundo y medio, para que el Enter
  que confirmó no la cierre. «Ver el pago» la vuelve a abrir.
- **Impresión por usuario.** En la barra, «Al confirmar» elige cómo sale el recibo: «Sólo mostrar los botones» (de fábrica), «Imprimir
  tirilla» o «Imprimir carta». Se guarda para el usuario en esa cooperativa. Con tirilla o carta, al confirmar se abre el diálogo de
  impresión con el recibo, sin descargar el PDF; los botones Tirilla (Ctrl+P) y Carta (Ctrl+Shift+P) también imprimen en vez de
  descargar. Si el navegador no deja imprimir desde la pantalla, el recibo se descarga como antes.
  La tirilla sale a su tamaño real, 80 mm de ancho, también si la impresora tiene hoja carta: no se ajusta a la hoja.

## 12. La caja desde el 2026-11-24 (pedido del dueño)

- **Ayuda de cada modalidad.** Al pasar el cursor por «Modalidad» (o por cada opción de la lista) y con **F1** sobre la lista, la caja dice
  cómo funciona la modalidad y cuándo usarla:
  - **Cancelación total**: paga todo lo que debe el crédito a hoy y lo deja cancelado; usa primero el anticipo.
  - **Lo vencido**: sólo las cuotas vencidas con su mora.
  - **Cuota del ciclo**: lo vencido y la cuota del período; si no ha vencido, queda como anticipo de esa cuota.
  - **Anticipo**: lo vencido y el resto guardado para pagar las cuotas al vencer; no reduce intereses.
  - **Abono extraordinario a capital**: lo vencido y el resto baja el capital, reduciendo el plazo o la cuota (lo elige el deudor); sí
    reduce intereses.
  - **Cuota específica**: sólo la cuota indicada.
  - **Valor libre**: lo vencido por la prelación y el resto como anticipo.
- **Avisos en español.** Bajo el producto, la simulación ya no muestra códigos como `Lending.Payment.AdvanceDoesNotReduceInterest` o
  `Lending.Payment.TreatedAsPayoff`, sino lo que significan y qué hacer (por ejemplo: «El anticipo queda guardado y paga cada cuota cuando
  vence: no reduce intereses. Si el asociado quiere pagar menos intereses, use «Abono extraordinario a capital».»). Los textos son los
  mismos de la simulación de pago del crédito en Cartera.
- **Recibos por pagador.** En Recibos (`/recaudo/recibos`), el campo **«Pagador (documento o nombre)»** busca la persona y lista sus
  recibos. Las fechas siguen filtrando; bórrelas para ver todos los recibos de esa persona.

## 13. La caja desde el 2026-11-27 (feature 028): la cuota ya enviada a la pagaduría o a la nómina

Un crédito por libranza o por nómina propia puede tener **la cuota del período ya en su canal**: salió en el archivo de descuentos y la
pagaduría no ha respondido, o la nómina propia ya la tiene (novedad o corrida en borrador). Si en caja se cancela todo, esa cuota se le cobra
también al asociado aunque la pagaduría o la nómina ya se la vayan a descontar.

- **La caja siempre avisa.** La lista de lo que puede pagar dice «cuota N enviada a la pagaduría» (o «a la nómina») junto al valor para cancelar,
  y al cancelar ese crédito (con **F7** o desde la lista) el ítem explica dónde va la cuota y qué pasará con ella.
- **Qué hace la cancelación lo decide la línea del crédito**, con el parámetro de Cartera `Pagos.CuotaEnviadaAlCancelar` (Parámetros de Cartera,
  general o por línea):
  - **Incluir** (de fábrica): se cancela todo, como siempre. Lo que la pagaduría gire después por esa cuota queda como saldo de la pagaduría;
    en nómina propia, si el crédito queda cancelado antes de aprobar la nómina, su descuento baja a cero con aviso.
  - **Excluir**: F7 propone la modalidad **«Todo menos la cuota enviada»**: cancela todo lo demás y la cuota queda en el crédito para que la pague
    su canal. El crédito queda cancelado cuando la pagaduría la gira o la nómina la aplica.
  - **Preguntar**: el ítem trae la casilla **«Dejar la cuota enviada para que la pague su canal»**. Marcada, se cancela todo menos esa cuota;
    sin marcar, la cancelación la cobra.
- **Cuánto queda en la cuota.** Dentro del tope legal del prepago sin penalidad (Ley 1555 de 2012), la cuota queda con el **interés
  proporcional**: el aviso dice el valor y qué pasa con lo de más si su canal la paga completa (en la pagaduría, saldo de la pagaduría; en la
  nómina, vuelve al neto del empleado). Fuera del tope, queda **tal como se envió**. El ejemplo con cifras está en
  [cartera-recogida-de-creditos.md](cartera-recogida-de-creditos.md) §6.3.
- **Cuándo no se ofrece**: si la cuota ya no está en su canal sin respuesta (la pagaduría ya respondió o la nómina ya se aprobó), si el crédito
  es de vivienda o si no hay cuota del período en curso. En esos casos se cancela completo. Si se intenta por fuera de la pantalla, se rechaza con
  `Receipts.Receipt.ModalityNotAdmitted`.
- **El recibo de una recogida en el desembolso** (el que paga facturas a crédito cuando un crédito nuevo las recoge) no se anula desde la caja:
  responde `Receipts.LoanPayoff.VoidWithTheLoan` y se anula anulando el crédito nuevo antes del giro.
