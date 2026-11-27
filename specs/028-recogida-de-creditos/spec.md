# 028 — Recogida de créditos y productos en el desembolso

**Rama**: `028-recogida-de-creditos` · **Creada**: 2026-11-27 · **Estado**: aprobada; decisiones D1–D13 del dueño = las recomendadas (2026-11-27, research §3)
**Base**: la 013 (originación, desembolso, contrato de obligaciones, libranza K2), la 014 (captaciones y contrato de cuentas), la 015 (Recaudo),
la 024/025 (causación configurable y por período) y la 026 (cargos masivos).

## Problema

El dueño pide que un **crédito nuevo recoja** —cancele o abone con su propio desembolso— otros créditos del mismo asociado y, si la cooperativa
lo permite, otros productos suyos, como lo hacía su herramienta anterior.

- **Hoy se hace en dos pasos**: se desembolsa el crédito nuevo y después, en caja, se cancela el viejo con «Todo para cancelar (F7)». El dinero
  sale por Tesorería y vuelve por la caja, el asesor no puede mostrarle al asociado cuánto recibirá neto y nada compara lo que se le ofreció con
  lo que finalmente se canceló.
- **El motor ya sabe recoger créditos, pero ninguna pantalla lo ofrece.** La solicitud guarda los créditos autorizados para recoger y el
  desembolso los paga por el contrato de obligaciones en la misma transacción, con su vista previa y su huella. Pero:
  - la pantalla de la solicitud no tiene dónde elegirlos y se niega a corregir una solicitud que los traiga;
  - la pantalla de desembolsos sólo muestra un letrero y nunca los manda;
  - ni el simulador, ni el expediente que ve quien decide, ni los DTO de la interfaz los conocen.
- **Las validaciones de la solicitud cuentan como vivos los créditos que se van a recoger.** Capacidad de pago, capacidad de descuento,
  endeudamiento consolidado, cupo y concentración suman la cuota y el capital del crédito viejo y además los del nuevo: una solicitud que recoge
  puede quedar bloqueada sin razón.
- **La cuota ya enviada a la pagaduría no la mira nadie** — ni la recogida, ni la caja (F7), ni ninguna cancelación (ver H1).

## Hallazgos que esta feature corrige

- **H1 — Cuota enviada a la pagaduría.** Cuando un crédito por libranza se cancela (en caja, por recogida o por cualquier cancelación) después de
  que su cuota salió en el archivo de descuentos:
  1. el valor para cancelar incluye esa cuota, aunque ya se le haya descontado al asociado o se le vaya a descontar;
  2. la respuesta de la pagaduría se consume igual y deja un «pendiente de giro» sobre un crédito cancelado, que **nunca se levanta**: la tarea
     de giros atrasados lo sigue alertando, avisa al deudor y lo ve la proyección a centrales;
  3. cuando la pagaduría gira, el pago se rechaza porque el crédito ya no es pagable y el dinero queda como **saldo a favor de la pagaduría**, que
     sólo se le devuelve a ella o se aplica a un recibo: no llega ni al asociado ni a su crédito nuevo;
  4. no existe la forma de cancelar «todo menos la cuota que ya va a pagar la pagaduría».
  En la nómina propia no hay doble cobro: si el crédito se canceló antes de aprobar la corrida, el descuento baja a cero con aviso.
- **H2 — Validaciones de originación.** Ver arriba: cuentan los recogidos como si siguieran vivos.
- **H3 — Recogida parcial.** El abono parcial de hoy cubre lo exigible y deja el resto como **anticipo**, no como abono a capital.
- **H4 — Neto de la orden de giro.** La orden se emite por el bruto menos lo **evaluado** antes del cerrojo y no por lo **aplicado**: si la
  liquidación cambia entre los dos momentos, la orden y las recogidas no cuadran. Hay una e2e que recorre una recogida por la API, pero ninguna
  prueba mira su contabilidad ni Tesorería.
- **H5 — Contabilidad que no netea.** El comprobante del desembolso (DS) acredita el **bruto** a «desembolso por girar» y cada recogida sale en
  un recibo (RC) que debita el puente de la recogida propia. El neto sólo cuadra si la contadora lleva ese puente a la misma cuenta que
  «desembolso por girar», y aun así no cuadra en giro a tercero, con regla por línea o con documento cruce (el del crédito nuevo frente al del
  recogido). La conciliación de Cartera espera ese puente en cero y «desembolso por girar» igual al neto de las órdenes abiertas: con recogidas
  no se cumple.
- **H6 — Giro nuevo.** Cuando Tesorería rechaza el giro y se emite uno nuevo, el hecho del desembolso sale sin las recogidas (neto = bruto)
  mientras la orden lleva el neto.
- **H7 — Garantías del recogido.** El cruce interno queda en firme al aplicarse y libera enseguida las garantías y el pagaré del crédito
  recogido; si el crédito nuevo se anula antes del giro, el viejo vuelve a estar vivo sin ellas.

## Principio

- Un crédito nuevo recoge, **en su desembolso y en la misma transacción**, productos vivos del **mismo deudor**. Si una recogida no queda
  aplicada, el desembolso no existe.
- Se recoge **por los contratos de siempre**: los pagos entran a Cartera sólo por el contrato de obligaciones y a las cuentas sólo por su
  registro; nadie escribe un libro fuera de su escritor.
- Lo que se muestra al vender es una **proyección**; al desembolsar **manda el recálculo**, y la diferencia se ve antes de confirmar.
- **De fábrica nada cambia**: todo parámetro nuevo arranca con el comportamiento de hoy.
- **Cartera no conoce cuentas**: la contabilidad sale de la matriz, con los hechos de siempre.

## Vocabulario

- **Crédito nuevo**: el que se vende y se desembolsa.
- **Producto recogido**: lo que el desembolso cancela o abona: un crédito del deudor (también las obligaciones que nacen de ventas de Comercial y
  de cargos masivos) o, si se parametriza, un aporte, un ahorro o una factura a crédito.
- **Fecha de proyección (R)**: hasta cuándo se toma la información (pagos, causación, mora). Por defecto, el momento en que se simula.
- **Fecha proyectada de desembolso (D)**: la fecha a la que se liquida cada producto. Por defecto, la fecha esperada de desembolso de la
  solicitud.
- **Cuota causada**: la cuota del período en curso a la fecha de la recogida, cuyo período ya empezó y que aún no vence. Con causación por
  período calendario su interés ya está causado completo.
- **Cuota enviada**: la cuota causada que ya salió en un archivo de descuentos a la pagaduría (enviada sin respuesta, o descontada y pendiente de
  giro), o que la nómina propia ya está descontando.
- **Neto a girar**: el valor del crédito nuevo menos lo recogido.

## Historias de usuario

### HU1 — Ver los productos del asociado y simular la recogida al vender (P1)

El asesor arma la solicitud. Al elegir al asociado, la pantalla carga **todos sus productos vivos** a la fecha de proyección: por cada crédito,
el número, la línea, la forma de pago (caja, nómina, libranza), el saldo, la cuota, los días de mora, si tiene una cuota enviada a la pagaduría y
**si se puede recoger o por qué no**. El asesor marca lo que se recoge, total o parcial, cambia la fecha proyectada de desembolso cuantas veces
quiera y ve, para cada fecha: lo que costaría recoger cada producto (capital, interés, mora, seguro, administración, otros, anticipo a favor y lo
que se devuelve o se cobra por intereses proporcionales), la cuota causada y qué se hace con ella, el total recogido, el **neto a girar** y la
cuota nueva frente a las cuotas que se liberan.

**Por qué primero**: es la «venta dinámica y segura» que pidió el dueño; sin esto nadie puede ofrecer el producto.

**Prueba independiente**: un asociado con dos créditos al día y uno en mora: la pantalla muestra los tres, deja marcar los dos al día, el de mora
sale con «requiere reestructuración»; al cambiar la fecha proyectada de desembolso del 28/11 al 05/12 el valor a recoger y el neto cambian.

**Escenarios**:
1. **Dado** un crédito al día por caja, **cuando** se marca para recoger total, **entonces** se ve su valor a la fecha proyectada, desglosado por
   concepto, y el neto a girar baja en ese valor.
2. **Dado** un crédito en mora (o castigado, o en insolvencia), **cuando** se listan los productos, **entonces** sale sin poder marcarse, con el
   motivo.
3. **Dado** un crédito por libranza cuya cuota de noviembre ya salió en el archivo de descuentos, **cuando** se marca, **entonces** la pantalla
   dice «La cuota 7 por $312.400 ya se envió a la pagaduría COLPENSIONES en el archivo ADP-000123 de noviembre» y explica qué pasará con ella
   según `Recogida.CuotaCausada` (HU4).
4. **Dado** la fecha proyectada de desembolso cambiada, **cuando** se vuelve a simular, **entonces** todos los valores se recalculan a esa fecha
   con la información hasta la fecha de proyección.
5. **Dado** una recogida que deja el neto en cero o negativo, **cuando** se simula, **entonces** se avisa y no se puede radicar así.
6. **Dado** un crédito que ya está en otra solicitud en curso para recogerlo, **cuando** se marca, **entonces** se avisa con el número de esa
   solicitud.
7. **Dado** que se pide, **cuando** el asesor imprime la simulación, **entonces** sale un PDF con los productos, el neto y la fecha proyectada,
   como la simulación de cancelación del estado de cuenta.

### HU2 — Radicar, corregir y decidir una solicitud que recoge (P1)

La solicitud guarda lo que se recoge **con su proyección**: producto, modo, valor pedido, valor proyectado y su desglose, las fechas de
proyección y de desembolso, y el tratamiento de la cuota causada y de los intereses. Las validaciones de la solicitud miden la **situación
después de recoger**. Quien decide ve en el expediente lo que se recoge, lo proyectado y el neto. Una solicitud con recogidas se puede corregir
como cualquier otra.

**Por qué primero**: sin radicación con recogidas no hay nada que desembolsar.

**Prueba independiente**: un asociado cuyo ingreso alcanza para la cuota nueva sólo si se libera la del crédito viejo: la solicitud que lo recoge
pasa la capacidad de pago; la misma sin recogerlo no pasa.

**Escenarios**:
1. **Dado** una solicitud que recoge un crédito total, **cuando** se radica, **entonces** la capacidad de pago, la de descuento por nómina o
   libranza, el endeudamiento consolidado, el cupo y la concentración ya no cuentan ese crédito.
2. **Dado** una recogida parcial, **cuando** se radica, **entonces** las validaciones cuentan el saldo que queda.
3. **Dado** una solicitud con recogidas devuelta para corregir, **cuando** se abre en la pantalla de corrección, **entonces** se pueden cambiar las
   recogidas si el grupo «Recogidas» está entre los editables, y lo demás no se pierde.
4. **Dado** el expediente de una solicitud con recogidas, **cuando** lo abre quien decide, **entonces** ve cada producto con su valor proyectado,
   el total, el neto a girar y la fecha proyectada.
5. **Dado** un producto que no es del solicitante como deudor, **cuando** se intenta radicar, **entonces** se rechaza.

### HU3 — Desembolsar recalculando todo y viendo las diferencias (P1)

En la pantalla de desembolsos, la solicitud aprobada trae **sus recogidas autorizadas** sin que nadie las elija de nuevo. La vista previa
**recalcula todo** a la fecha valor con la información de ese momento y, por cada producto, muestra lo proyectado, lo de ahora y **la diferencia
con su causa**: pagos recibidos desde la proyección, causación, mora, cambio de la cuota causada o enviada, novedades, parámetros que cambiaron. El
neto a girar se recalcula. Quien desembolsa reconoce las diferencias y confirma; la orden de giro sale por el **neto realmente aplicado**.

**Por qué primero**: es el pedido 3 del dueño y lo que reemplaza el desembolso más F7.

**Prueba independiente**: una solicitud proyectada el 20/11 al 28/11; el asociado paga una cuota en caja el 25/11; al desembolsar el 28/11 la
vista previa muestra la recogida menor, el pago como causa y el neto mayor; confirmado, el crédito viejo queda cancelado y la orden de giro es por
el neto mostrado.

**Escenarios**:
1. **Dado** una diferencia entre lo proyectado y lo de ahora, **cuando** se pide la vista previa, **entonces** se muestra por producto y por
   concepto, y confirmar exige reconocerla.
2. **Dado** `Recogida.AlDesembolsar` en «Recalcular», **cuando** se desembolsa, **entonces** se recoge lo de ahora.
3. **Dado** `Recogida.AlDesembolsar` en «MantenerLoProyectado» y una diferencia en contra del asociado, **cuando** se desembolsa, **entonces** se
   aplica lo proyectado y el producto queda con el saldo que falta, como lo dijo la vista previa.
4. **Dado** un producto que ya se canceló por otro camino, **cuando** se pide la vista previa, **entonces** sale «ya cancelado, no se recoge», el
   neto sube y confirmar exige reconocerlo.
5. **Dado** un producto que entró en mora después de la aprobación, **cuando** se pide la vista previa, **entonces** el desembolso se bloquea con
   «requiere reestructuración», como hoy.
6. **Dado** una diferencia mayor que `Recogida.ToleranciaDeDiferencia`, **cuando** se pide la vista previa, **entonces** el desembolso se bloquea
   y pide devolver la solicitud para corregirla (si el parámetro tiene valor; vacío = sólo avisar).
7. **Dado** un desembolso confirmado, **cuando** se mira la orden de giro, **entonces** su valor es el bruto menos lo aplicado de verdad a cada
   producto, al peso.
8. **Dado** el crédito nuevo, **cuando** se anula antes del giro, **entonces** cada recogida se reversa y los productos recogidos vuelven a su
   estado, como hoy.

### HU4 — La cuota causada y la cuota enviada a la pagaduría (P1)

La cooperativa decide (`Recogida.CuotaCausada`) si la recogida cancela **todo, incluida la cuota causada**, o **todo menos la cuota causada**,
que queda en el crédito viejo para que la pague su canal de siempre (la pagaduría que ya la descontó, la nómina, la caja). La pantalla lo
**muestra y lo explica** en la simulación, en el expediente, en la vista previa del desembolso y en el comprobante. Lo mismo se ofrece en la
caja (F7) y en toda cancelación (H1), con su propio parámetro. Lo que la pagaduría gire después por un crédito ya recogido se trata según
`Recogida.GiroPosteriorDeCreditoRecogido`.

**Por qué primero**: es el parámetro 1 del dueño y corrige H1, que hoy ya afecta a la caja.

**Prueba independiente**: un crédito por libranza con la cuota 7 enviada; con «todo menos la cuota causada», la recogida cancela el resto y el
crédito viejo queda vivo sólo con la cuota 7; cuando la pagaduría la gira, el crédito queda cancelado sin saldo a favor ni alertas.

**Escenarios**:
1. **Dado** «Incluir» (el defecto), **cuando** se recoge un crédito con cuota enviada, **entonces** se cancela todo, como hoy, y la pantalla
   avisa que lo que la pagaduría gire por esa cuota se tratará según `Recogida.GiroPosteriorDeCreditoRecogido`.
2. **Dado** «Excluir», **cuando** se recoge, **entonces** se cancela todo menos la cuota causada (capital, interés, seguro y administración de esa
   cuota); el crédito viejo queda vivo con sólo esa cuota y sale de los archivos de descuentos siguientes.
3. **Dado** «Preguntar», **cuando** se marca el crédito en la solicitud, **entonces** el asesor elige por crédito y la elección queda en la
   solicitud.
4. **Dado** un crédito cancelado con una cuota enviada sin respuesta, **cuando** llega la respuesta de la pagaduría, **entonces** no se le avisa al
   deudor como si estuviera en mora, no se alerta giro atrasado y queda marcada «descuento de un crédito cancelado».
5. **Dado** «AlCreditoNuevo» y el giro de la pagaduría por un crédito ya recogido, **cuando** se aplica el giro, **entonces** el valor entra al
   crédito que lo recogió como abono, con su comprobante. Con «SaldoDeLaPagaduria» (el defecto) queda como hoy.
6. **Dado** la caja con «Todo para cancelar (F7)» y un crédito con cuota enviada, **cuando** se arma la lista, **entonces** la caja lo avisa y,
   si `Pagos.CuotaEnviadaAlCancelar` es «Excluir», propone cancelar todo menos esa cuota.
7. **Dado** «Excluir» y una cuota que la pagaduría al final no descuenta, **cuando** llega el «no descontado», **entonces** sigue la regla del
   convenio como cualquier cuota no descontada y se avisa al asesor que la recogió.

### HU5 — Intereses proporcionales en la recogida (P2)

La cooperativa decide (`Recogida.InteresesProporcionales`) cómo se cobra el interés del crédito recogido a la fecha del desembolso: **Sí**: si
ya hay interés causado más allá de esa fecha (causación al inicio del ciclo o por período calendario), **se devuelve** el de los días que faltan;
si no se ha causado hasta esa fecha, **se cobra** el de los días que faltan. **No**: se cobra completo el interés de la cuota en curso. **Como al
cancelar** (el defecto): lo que diga `Pagos.InteresesAlPagarAntesDelVencimiento`, como hoy. En vivienda y dentro del tope legal del prepago
siempre se cobra el proporcional.

**Prueba independiente**: un crédito con causación por período calendario y la cuota de noviembre causada completa el 1/11, recogido el 20/11
con «Sí»: se devuelven los 10 días restantes; con «No» se cobra la cuota completa; con «Como al cancelar», lo que diga el parámetro de la caja.

**Escenarios**:
1. **Dado** «Sí» y lo causado hasta el 30/11, **cuando** se recoge el 20/11, **entonces** el desglose muestra «interés devuelto: 10 días» y el
   valor a recoger lo descuenta.
2. **Dado** «Sí» y la causación hecha sólo hasta el 15/11, **cuando** se recoge el 20/11, **entonces** se cobra el interés del 16 al 20.
3. **Dado** «No» y un crédito dentro del tope legal del prepago, **cuando** se recoge, **entonces** se cobra el proporcional y la pantalla dice
   por qué.
4. **Dado** «Excluir» la cuota causada y «Sí», **cuando** se recoge, **entonces** la cuota causada queda **tal como se envió** (capital, interés
   del período completo, seguro y administración: D13) y la devolución proporcional sólo alcanza lo causado de las cuotas posteriores; la
   pantalla explica que el interés de esa cuota se cobra completo porque la pagaduría la paga tal cual.

### HU6 — Recoger aportes y ahorros (P2)

Si la cooperativa lo parametriza (`Recogida.ProductosAdmitidos`), el crédito nuevo también paga **cuotas pendientes de aportes** o **abona**
(aporte extraordinario, consignación a un ahorro), parcial o total, aunque no haya cuotas causadas. Lo hace por el registro de cuentas, en la
misma transacción, con las reglas de cada línea (mínimo no reducible, límite de participación, extraordinario admitido, embargos, topes).

**Prueba independiente**: con aportes admitidos, un crédito nuevo de $5.000.000 paga $150.000 de cuotas de aportes atrasadas y abona
$200.000 de aporte extraordinario; el neto a girar es $4.650.000 menos lo demás recogido; el estado de cuenta de aportes muestra los dos
movimientos «por desembolso del crédito …».

**Escenarios**:
1. **Dado** de fábrica, **cuando** se listan los productos, **entonces** sólo los créditos se pueden marcar; los aportes y ahorros se ven sin
   poder marcarse, con «la cooperativa no los recoge».
2. **Dado** una línea de aportes que no admite extraordinarios, **cuando** se pide un abono libre, **entonces** se rechaza con el motivo de la
   línea.
3. **Dado** un ahorro contractual, **cuando** se pide recogerlo, **entonces** sólo admite sus cuotas pendientes.
4. **Dado** un CDAT, **cuando** se listan los productos, **entonces** no aparece: no es una deuda.

### HU7 — Recoger facturas a crédito de Comercial (P3)

Si la cooperativa lo parametriza, el crédito nuevo paga facturas a crédito de Comercial del asociado (los servicios que no son cartera, como en
COOFLOPAL), total o parcial, por un recibo de Recaudo sin caja que nace del desembolso, para que Contabilidad, Recaudo y Transporte se enteren
igual que con un pago en caja.

**Prueba independiente**: con facturas admitidas, el crédito nuevo paga una factura de $73.425; Recaudo tiene el recibo sin caja, la factura
queda en cero en el informe de cuentas por cobrar y el cargo de Transporte queda pagado.

**Escenarios**:
1. **Dado** una factura con saldo, **cuando** se recoge parcial, **entonces** el recibo aplica ese valor y la factura queda con el resto.
2. **Dado** que Recaudo no puede emitir el recibo, **cuando** se desembolsa, **entonces** el desembolso no existe y se dice por qué.

### HU8 — Informes y seguimiento de recogidas (P2)

Un informe de recogidas por período, oficina, línea y asesor: crédito nuevo, productos recogidos, proyectado, aplicado, diferencia y su causa,
cuota causada dejada, intereses devueltos o cobrados, quién vendió, quién decidió y quién desembolsó. El crédito recogido muestra «recogido por
el crédito …» en su detalle y en el estado de cuenta; el nuevo, lo que recogió. Las cuotas dejadas a la pagaduría (HU4) se siguen hasta que se
giran.

**Prueba independiente**: después de tres desembolsos con recogidas, el informe de noviembre los lista con sus diferencias y se exporta a Excel y
PDF.

## Parámetros

Todos con vigencia, por cooperativa y, donde se indica, por línea. **De fábrica ninguno cambia lo que hay hoy.**

| Clave | Pregunta | Valores | De fábrica | Ámbito |
|---|---|---|---|---|
| `Recogida.CuotaCausada` | ¿Qué se recoge de la cuota causada? | `Incluir` (todo, incluida la cuota causada), `Excluir` (todo menos la cuota causada), `Preguntar` (el asesor elige por crédito) | `Incluir` | General y línea del crédito que se recoge (D1) |
| `Recogida.InteresesProporcionales` | ¿Se cobran o devuelven los intereses proporcionales a la fecha de la recogida? | `ComoAlCancelar` (lo de `Pagos.InteresesAlPagarAntesDelVencimiento`), `Si`, `No` | `ComoAlCancelar` | General y línea del crédito que se recoge |
| `Recogida.ProductosAdmitidos` | ¿Qué productos y de qué forma recoge un crédito nuevo? | Por producto: `Creditos` (`Total`, `Parcial`), `Aportes` (`Cuotas`, `Abono`), `AhorroALaVista` (`Abono`), `AhorroPermanente` (`Abono`), `AhorroContractual` (`Cuotas`), `FacturasComerciales` (`Total`, `Parcial`) | `Creditos: Total, Parcial` | General y línea del crédito nuevo |
| `Recogida.AlDesembolsar` | ¿Los cambios entre la proyección y el desembolso se recalculan o se mantienen? | `Recalcular`, `MantenerLoProyectado` | `Recalcular` | General y línea del crédito nuevo |
| `Recogida.ToleranciaDeDiferencia` | Diferencia (pesos) desde la cual el desembolso pide devolver la solicitud | Pesos; vacío = nunca, sólo se avisa | vacío | General |
| `Recogida.GiroPosteriorDeCreditoRecogido` | ¿Qué se hace con lo que la pagaduría gira por un crédito ya recogido? | `SaldoDeLaPagaduria` (hoy), `AlCreditoNuevo`, `AFavorDelAsociado` | `SaldoDeLaPagaduria` | General |
| `Pagos.CuotaEnviadaAlCancelar` | ¿Una cancelación en caja cobra la cuota ya enviada a la pagaduría? | `Incluir`, `Excluir`, `Preguntar` | `Incluir` | General y línea |
| `Contabilidad.RecogidaEnUnComprobante` | ¿El desembolso y sus recogidas salen en un solo comprobante? | Sí / No | No | General (Contabilidad) |

**Relación con `Pagos.InteresesAlPagarAntesDelVencimiento`** («¿Devuelve los días no corridos al cancelar?»): `Recogida.InteresesProporcionales =
Si` es el mismo cálculo que su valor «Corrido» y `No` el de «CuotaCompleta», aplicados sólo a la recogida. Con `ComoAlCancelar` la recogida
cobra exactamente lo que cobraría la caja, que es lo que pasa hoy. Las excepciones legales (vivienda, tope del prepago) rigen en los dos.

## Casos límite

- **El crédito recogido recibe un pago entre la vista previa y la confirmación**: confirmar vuelve a liquidar bajo el cerrojo; si cambia la
  huella, se pide otra vista previa, como hoy.
- **Dos solicitudes recogen el mismo crédito**: se avisa al radicar la segunda; la que se desembolsa después lo encuentra cancelado y lo omite
  con aviso.
- **Tramos**: las recogidas van con el primer tramo; un tramo siguiente no recoge.
- **Crédito nuevo en otra línea o de otra oficina**: se recoge igual si el crédito viejo está en el alcance de quien desembolsa.
- **Crédito recogido con anticipo o saldo a favor**: el valor para cancelar ya los descuenta; el desglose los muestra.
- **Crédito recogido en UVR**: se liquida en pesos a la UVR del día del desembolso, siempre con el proporcional (vivienda).
- **Crédito recogido con codeudores**: la cancelación libera a los codeudores, como hoy.
- **Fecha valor del desembolso en un mes cerrado** de Cartera o de Contabilidad: no se desembolsa, como hoy.
- **La cuota causada vence antes de que la pagaduría gire** («Excluir»): el crédito viejo sigue la regla de mora y de giro de pagaduría de
  siempre (umbral de mora de pagaduría, Ley 1527).
- **Recogida parcial que termina cubriendo todo**: se trata como total.
- **Neto a girar cero o negativo**: no se desembolsa, como hoy.

## Requisitos funcionales

### Productos recogibles y proyección (HU1)

- **FR-001** Una consulta lista los productos vivos del asociado **como deudor o titular**, dentro del alcance de quien consulta, a una fecha
  proyectada de desembolso D con la información hasta R: créditos (con su origen: crédito, venta de Comercial, cargo masivo), y —si
  `Recogida.ProductosAdmitidos` los admite para la línea del crédito nuevo— cuentas de aportes y ahorros con sus cuotas pendientes y facturas a
  crédito de Comercial.
- **FR-002** Por cada producto dice si se puede recoger y, si no, por qué: en mora (reestructuración), castigado, en insolvencia, bloqueado,
  producto no admitido, sin saldo, CDAT.
- **FR-003** Por cada crédito: valor a recoger a D y su desglose por concepto (capital, interés corriente, interés devuelto o cobrado por
  proporcionales, mora, seguro, administración, otros cargos, anticipo y saldo a favor que se descuentan), la cuota causada (número, vencimiento,
  valor) y si está enviada (pagaduría, archivo, período, estado: enviada sin respuesta, descontada pendiente de giro, en nómina propia).
- **FR-004** La simulación recibe el crédito nuevo y los productos marcados y devuelve el total recogido, el neto a girar, la cuota nueva, las
  cuotas que se liberan y la capacidad después de recoger. Simular no escribe; ver liquidaciones exige `Lending.Payoffs.Read` y queda auditado
  como hoy.
- **FR-005** La simulación se puede imprimir en PDF con el membrete.

### Solicitud y decisión (HU2)

- **FR-006** La solicitud guarda, por producto recogido: tipo, referencia, modo (total, parcial, cuotas, abono), valor pedido si es parcial,
  tratamiento de la cuota causada y de los intereses, valor proyectado con su desglose, D, R y la huella de la liquidación.
- **FR-007** Al radicar se valida que cada producto sea del solicitante como deudor o titular, esté vivo, se pueda recoger y el neto sea positivo.
- **FR-008** Capacidad de pago, capacidad de descuento (nómina propia y libranza), endeudamiento consolidado, cupo y concentración miden la
  situación **después de recoger**: sin la cuota ni el capital de lo recogido total, con el saldo que queda de lo parcial.
- **FR-009** Un crédito que ya está en otra solicitud viva para recogerlo produce un aviso con el número de esa solicitud; no bloquea.
- **FR-010** La pantalla de corrección admite solicitudes con recogidas y permite cambiarlas cuando el grupo «Recogidas» es editable; deja de
  rechazarlas.
- **FR-011** El expediente y el enrutamiento muestran las recogidas con lo proyectado y el neto; la foto de la solicitud registra sus cambios.

### Desembolso (HU3)

- **FR-012** El desembolso toma **todas** las recogidas autorizadas por la solicitud; no se elige un subconjunto (D9). Un producto ya cancelado
  se omite con aviso.
- **FR-013** La vista previa recalcula cada recogida a la fecha valor con la información del momento y muestra, por producto y concepto,
  proyectado, actual, diferencia y su causa.
- **FR-014** Con `Recogida.AlDesembolsar = Recalcular` se aplica lo actual; con `MantenerLoProyectado`, lo proyectado (topado en lo que cancela;
  si no alcanza, el producto queda con el saldo que falta y la vista previa lo dice).
- **FR-015** Toda diferencia u omisión entra en la huella de la vista previa y confirmar exige reconocerla. Con `Recogida.ToleranciaDeDiferencia`
  con valor, una diferencia mayor bloquea con `Lending.Payoff.DifferenceAboveTolerance`.
- **FR-016** La orden de giro sale por el bruto menos lo **aplicado** a cada producto (corrige H4); la vista previa, el resultado y la orden dicen
  lo mismo al peso.
- **FR-017** Una recogida parcial de un crédito cubre lo exigible y abona el resto **a capital** con la elección del deudor (reducir cuota o
  plazo), no como anticipo (corrige H3; D7).
- **FR-018** Anular el crédito nuevo antes del giro reversa cada recogida con su clave derivada, en la misma transacción, para todo tipo de
  producto.

### Cuota causada y cuota enviada (HU4)

- **FR-019** El contrato de obligaciones admite una cancelación **«todo menos la cuota causada»**: cubre todo lo demás y abona a capital las cuotas
  posteriores, dejando intacta la cuota causada con sus conceptos.
- **FR-020** La recogida usa esa cancelación según `Recogida.CuotaCausada` (o la elección del asesor con `Preguntar`). El crédito viejo queda vivo
  sólo con la cuota causada y no vuelve a salir en archivos de descuentos ni en la nómina después de esa cuota.
- **FR-021** Una consulta dice, por crédito, si su cuota del período está enviada sin respuesta, descontada pendiente de giro o en la nómina
  propia, con la pagaduría, el archivo y el período. La usan la solicitud, la vista previa del desembolso y la caja.
- **FR-022** Respuesta o giro de la pagaduría sobre un crédito cancelado: no avisa al deudor ni alerta giro atrasado ni cuenta para centrales; la
  línea queda «descuento de un crédito cancelado» y lo girado sigue `Recogida.GiroPosteriorDeCreditoRecogido` (corrige H1).
- **FR-023** La caja avisa la cuota enviada en «Todo para cancelar (F7)» y en la cancelación de un crédito, y con `Pagos.CuotaEnviadaAlCancelar`
  en `Excluir` o `Preguntar` ofrece cancelar todo menos esa cuota.

### Intereses (HU5)

- **FR-024** El valor a recoger aplica `Recogida.InteresesProporcionales` con las excepciones legales de siempre, en la proyección, en la vista
  previa y en la aplicación, por el mismo sitio que decide el interés al cancelar.

### Otros productos (HU6, HU7)

- **FR-025** Aportes y ahorros se recogen por el registro de cuentas en la transacción del desembolso, con la clase de recaudador de la recogida
  propia y las reglas de cada línea.
- **FR-026** Las facturas a crédito de Comercial se recogen con un recibo de Recaudo sin caja que nace en la transacción del desembolso, con su
  `ReciboEmitido`.

### Contabilidad, informes, auditoría y permisos

- **FR-027** La contabilidad del desembolso con recogidas sale por la matriz de Cartera con los hechos de siempre (desembolso, pago aplicado,
  consignación a cuenta, recibo) y **cuadra sola** (corrige H5): el DS acredita a «desembolso por girar» sólo el **neto** y lleva lo recogido
  al **puente de la recogida propia**, por producto, con el deudor como tercero y el producto recogido como documento cruce; cada RC debita
  ese mismo puente. El puente queda en cero sin que la contadora tenga que hacer coincidir dos reglas. La completitud de la matriz pide las
  reglas que falten para los productos admitidos. Cartera no conoce cuentas.
- **FR-027a** La validación contable previa de la vista previa del desembolso evalúa también las recogidas: si falta una regla y
  `Contabilidad.FaltaDeRegla` está en «Bloquear», se ve antes de confirmar y no al ejecutar.
- **FR-027b** Con `Contabilidad.RecogidaEnUnComprobante` (defecto: no, como hoy), el DS y los RC de sus recogidas salen en **un solo
  comprobante** (D11).
- **FR-027c** El giro nuevo de un crédito con recogidas emite el hecho del desembolso con las mismas recogidas y el mismo neto que la orden
  (corrige H6).
- **FR-027d** Las garantías y el pagaré de un crédito recogido se liberan cuando el crédito nuevo queda **girado** (o, sin giro, al
  desembolsarse); mientras tanto el crédito recogido los muestra «por liberar al girar el crédito …» (corrige H7).
- **FR-028** Vista `payoffs` («Recogidas») en el centro de informes de Cartera, exportable, con los campos de HU8.
- **FR-029** El detalle del crédito y el estado de cuenta muestran «recogido por el crédito …» y «recogió …».
- **FR-030** La auditoría del desembolso lleva lo proyectado, lo aplicado, las diferencias reconocidas y quién las reconoció; la de la solicitud,
  las recogidas pedidas.
- **FR-031** Sin permisos nuevos: radicar con recogidas es `Lending.Applications.Create`, ver liquidaciones `Lending.Payoffs.Read`, desembolsar
  `Lending.Disbursements.Execute`.
- **FR-032** Toda pantalla nueva o modificada lleva su indicador de carga por zona; ninguna pantalla, informe ni manual dice el nombre del
  sistema anterior.

## Entidades

- **Recogida proyectada** (una por solicitud y producto): tipo de producto, referencia, modo, valor pedido, tratamiento de cuota causada e
  intereses, valor proyectado con su desglose, cuota causada y su envío, D, R, huella.
- **Recogida aplicada** (una por desembolso y producto): la proyectada de la que viene, valor actual, aplicado, diferencia y su causa, reconocida
  por, operación o recibo del producto, clave.
- **Cuota dejada a la pagaduría**: el crédito viejo, la cuota, el crédito nuevo que lo recogió; se cierra cuando la cuota se paga.

## Criterios de éxito

- **SC-001** Un asesor arma una solicitud que recoge dos créditos y ve el neto a girar en menos de 3 minutos, sin pasar por la caja.
- **SC-002** En el 100 % de los desembolsos con recogidas, la orden de giro es el bruto menos lo aplicado, al peso.
- **SC-003** Toda diferencia entre lo proyectado y lo aplicado se ve antes de confirmar y queda reconocida en la auditoría.
- **SC-004** Ningún crédito cancelado (por recogida o en caja) genera alertas de giro atrasado ni avisos de mora por una cuota enviada.
- **SC-005** Con los parámetros de fábrica, las suites existentes de Cartera, Recaudo y libranza pasan sin cambiar sus resultados.
- **SC-006** Con «todo menos la cuota causada», el crédito viejo queda cancelado al peso cuando la pagaduría gira la cuota.

## Supuestos

- La recogida exige que el producto sea del **mismo deudor**; no se recogen créditos donde el asociado es codeudor.
- Recoger un crédito **en mora** sigue siendo reestructuración y queda bloqueado (decisión K3: «refinanciar en mora sólo por novedad»).
- La lista de productos respeta el alcance de oficina y línea de quien consulta.
- La recogida va con el primer tramo.
- Los conceptos varios y los anticipos de Recaudo no se recogen: no son deudas.
- El CDAT no se recoge: no es una deuda.

## Decisiones para el dueño

Cada una con la recomendación. Las respuestas se anotan aquí y en `research.md`.

- **D1 — Ámbito de `Recogida.CuotaCausada`.** A: línea del **crédito que se recoge** (es propiedad de cómo se paga: libranza, nómina, caja).
  B: línea del crédito nuevo. **Recomendada: A.**
- **D2 — ¿`Preguntar` en `Recogida.CuotaCausada`?** A: sí, el asesor elige por crédito. B: no, sólo el parámetro. **Recomendada: A.**
- **D3 — Lo que la pagaduría gira después por un crédito ya recogido.** Parámetro `Recogida.GiroPosteriorDeCreditoRecogido` con defecto
  «como hoy» (saldo de la pagaduría). ¿Qué valor quiere para COOFLOPAL? **Recomendada: `AlCreditoNuevo`.**
- **D4 — ¿La caja (F7) y la cancelación también ofrecen «todo menos la cuota enviada»?** A: sí, con `Pagos.CuotaEnviadaAlCancelar` (defecto
  `Incluir`) y siempre con el aviso. B: sólo el aviso. **Recomendada: A.**
- **D5 — `MantenerLoProyectado`.** Si el producto cuesta más al desembolsar, queda con saldo. A: ofrecerlo así. B: no ofrecerlo (sólo
  `Recalcular`). **Recomendada: A**, con el aviso explícito.
- **D6 — Tolerancia.** ¿Alguna diferencia obliga a devolver la solicitud a decisión? A: no, sólo se avisa y se reconoce (vacío). B: desde un
  valor. **Recomendada: A** de fábrica, configurable.
- **D7 — Recogida parcial de un crédito.** A: abono a capital con la elección del deudor. B: como hoy (anticipo). **Recomendada: A.**
- **D8 — Facturas a crédito de Comercial (HU7).** Toca Recaudo, Contabilidad y Transporte. A: en esta feature, como última entrega. B: en una
  feature aparte. **Recomendada: A.**
- **D9 — ¿Se puede desembolsar sin una de las recogidas autorizadas?** A: no; quitarla exige devolver la solicitud. B: sí, con aviso.
  **Recomendada: A.**
- **D10 — Créditos en mora.** Confirmar que recogerlos sigue bloqueado (reestructuración), también dentro de `Mora.ToleranciaDeSaldoVencido`.
  **Recomendada: confirmar.**
- **D11 — Un solo comprobante.** ¿El desembolso y sus recogidas salen en un solo comprobante (como se decidió para el cruce del retiro, D45)?
  A: parámetro `Contabilidad.RecogidaEnUnComprobante`, defecto no (como hoy), y la contadora decide. B: siempre uno. **Recomendada: A.**
- **D12 — Garantías del crédito recogido.** A: se liberan al girar el crédito nuevo (FR-027d). B: como hoy, al aplicarse la recogida.
  **Recomendada: A.**
- **D13 — El interés de la cuota que se deja («Excluir»).** A: la cuota queda tal como se envió, con el interés del período completo sobre todo
  el saldo; la pagaduría la paga exacta y el crédito cierra al peso. B: se recalcula sobre el capital que queda y lo que la pagaduría gire de
  más se le devuelve al asociado. **Recomendada: A** (B deja un excedente por cada recogida).

## Fuera de alcance

- Recoger créditos de otra entidad (compra de cartera externa con giro a un tercero).
- Reestructurar créditos en mora por la recogida.
- Constituir un CDAT con el desembolso.
- Cambiar el cálculo de la cancelación en caja más allá de la cuota enviada (H1).
