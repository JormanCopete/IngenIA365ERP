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
supervisor (otra persona); un faltante aprobado se trata según `Caja.TratamientoFaltante` (por defecto, cuenta por cobrar al cajero).

## 8. Anular un recibo

Pida la anulación en `/recaudo/anulaciones` con su motivo; la aprueba otra persona con `Receipts.Voiding.Approve`. La anulación es
un documento nuevo: el recibo original no se borra ni se edita, y cada módulo reversa lo que había aplicado.

- **Cómo se pide**: «Anular» en la lista de Recibos (`/recaudo/recibos`), o «Pedir su anulación» en la caja tras confirmar el recibo, abre la
  pantalla con el **número** del recibo ya puesto; también se escribe a mano («5» encuentra el «000005»). La pantalla muestra el recibo y lo
  que aplicó antes de pedirla.
- **Quién aprueba**: otra persona con `Receipts.Voiding.Approve`, distinta de quien la pidió y del cajero que hizo el recibo. La pantalla y el
  menú los ve también quien sólo aprueba.
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
- **Al elegir la persona entra solo lo exigible** (lo causado y vencido); F4 lo vuelve a traer si se quitó.
- **Todo para cancelar (F7)** (2026-11-18): reemplaza «Qué paga» por todo lo que la persona debe: cada crédito por su valor de cancelación, las
  cuotas pendientes de sus cuentas y el saldo de sus facturas. Los conceptos varios y las consignaciones libres no entran.
- **Con cuotas vencidas sólo se paga lo exigible o se cancela** (`Caja.ConMoraSoloExigible`, de fábrica Sí): sobre esa obligación no se abona a
  capital ni se anticipan cuotas (`Receipts.Receipt.OverdueOnlyDue`).
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
