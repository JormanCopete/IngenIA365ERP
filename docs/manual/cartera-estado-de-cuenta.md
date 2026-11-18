# Cartera: estado de cuenta de la persona

Pantalla `/cartera/estado-de-cuenta` (menú Cartera › Estado de cuenta), permiso `Lending.Loans.Read`. Muestra en un solo lugar los créditos de la
persona —como deudora y como codeudora—, sus aportes y sus ahorros, con lo que debe, lo que tiene y su cupo. Pedido del dueño del 2026-11-13,
rediseñado el 2026-11-16.

## 1. Buscar y leer

- **F2** busca a la persona por documento, nombre o apellido. La fecha de corte es opcional (sin ella, hoy). «Incluir cancelados y cerrados»
  suma lo que ya terminó.
- Arriba van los datos de la persona y el resumen: saldo de créditos, total para cancelar, vencido, mora máxima, aportes, ahorros y **cupo
  disponible** (pase el ratón por encima para ver el asignado). Debajo, **lo que debe por concepto**: capital, interés corriente, interés de mora,
  seguro, otros y el total.
- La tabla de productos se recorre con clic o con **Ctrl+↑ / Ctrl+↓**; el elegido queda resaltado. La información se desplaza por dentro y la
  barra de botones de abajo queda siempre a la vista.

## 2. La vista de cada usuario

**Vista (Ctrl+Alt+C)** abre un popup donde cada usuario elige qué columnas ve, el **tamaño del texto** (pequeño, normal, grande) y si los
valores llevan **centavos**. Se guarda para su usuario en cualquier equipo (preferencias `cartera.estado-de-cuenta.columnas`, `.tamano-texto`
y `.decimales`). Los valores van sin símbolo de moneda. El mismo tamaño y centavos se usan en el detalle de una obligación en la caja.

## 3. Mirar un producto por dentro

Todo se abre en un popup que se cierra con **Esc**:

| Tecla | Qué abre |
|---|---|
| F3 / doble clic | Cuotas pendientes del producto elegido: de un crédito, o de una cuenta de aportes o de ahorro permanente o contractual |
| Shift+F3 | Cuotas pendientes de **todas** las obligaciones vigentes |
| F4 | Cuotas anticipadas de un crédito (cubiertas por anticipo) o pagadas de una cuenta con cuotas |
| F6 | Cuotas extras |
| F7 | Plan de pagos |
| F8 | Movimientos (auxiliar con filtro por fechas, rubro y débito o crédito; Excel) |
| F9 | El producto completo, con todas sus pestañas y acciones |

En **F9** de un crédito, la pestaña **«Documentos firmados»** descarga la copia del pagaré, la carta de instrucciones y la libranza para
reimprimirlas. El sistema no genera esos documentos: son las copias firmadas que se adjuntaron al registrar el pagaré o como soportes de la
solicitud (decisión del dueño del 2026-11-16). La libranza se reconoce porque su requisito dice «libranza» o «autorización de descuento».

## 4. Información de la persona

Botones de la barra, cada uno con su permiso:

| Tecla | Qué muestra | Permiso |
|---|---|---|
| Ctrl+Alt+K | Circulares y avisos de cobro enviados | `Lending.DebtorMessages.Read` |
| Ctrl+Alt+G | Gestiones de cobranza y promesas | `Lending.Collections.Read` |
| Ctrl+Alt+O | Solicitudes de crédito | `Lending.Applications.Read` |
| Ctrl+Alt+N | Novedades y descuentos no aplicados por libranza o nómina | `Lending.Novelties.Read` |
| Ctrl+Alt+U | Cupo de crédito y su disponibilidad | `Lending.CreditLimits.Read` |

Cada una se puede imprimir desde su popup.

## 5. Imprimir, PDF, Excel y correo

- **Ctrl+P** imprime con el membrete de la cooperativa (logotipo de Cartera, nombre y NIT), los datos de la persona, el resumen, la tabla con las
  columnas elegidas y el resumen por concepto.
- **Ctrl+Alt+D** baja el mismo estado de cuenta en **PDF**; **Ctrl+Alt+E** en **Excel** (con el resumen por concepto y el cupo en las notas).
  Piden los permisos de exportar informes de Cartera con datos personales.
- **Ctrl+Alt+M** lo envía en PDF al correo registrado de la persona (`Lending.Documents.Issue`).

## 6. Simular la cancelación

**Ctrl+Alt+S** abre la simulación: elija las deudas (por defecto la elegida, o todas) y la fecha en que pagaría —hoy o después— y pulse
**Simular**; cambie la fecha y vuelva a simular cuantas veces quiera. Muestra por crédito el capital, el interés corriente, el de mora, el seguro,
los otros cargos, el anticipo que se descuenta y el total. Se imprime, se baja en PDF o se envía al correo. No aplica nada.

El interés de la cuota que está corriendo depende del parámetro de Cartera **`Pagos.InteresesAlPagarAntesDelVencimiento`** (general o por línea):

- `Corrido` (de fábrica): se cobra el interés causado día a día hasta la fecha de pago; lo que falta de la cuota en curso no se cobra.
- `CuotaCompleta`: se cobra todo el interés de la cuota en curso aunque se cancele antes de su vencimiento.

Desde el 2026-11-16 el parámetro lo aplica **el motor de pagos**, no sólo la simulación: la simulación muestra exactamente lo que cobrará la caja.

- **Dónde se aplica:** en toda **cancelación** —el pago total en caja (`FullPayoff`), el abono extraordinario que alcanza el capital por vencer y
  termina cancelando, el **saldo a favor aplicado como abono** que termina cancelando (decisión del dueño del 2026-11-16; lo que no usa sigue como
  saldo a favor), la recogida de un crédito al desembolsar otro, el descuento de nómina que cancela y el cruce de deudas en el retiro del asociado—.
  El listado y el detalle de la caja, la liquidación del crédito y lo que se recoge en un desembolso ya ofrecen el valor con la cuota completa.
- **Dónde no:** dentro del **tope legal del prepago sin penalidad** (Ley 1555 de 2012 y Ley 2032 de 2020: el crédito cuyo monto no pasa de
  `PREPAGO_TOPE_SMMLV` veces el salario mínimo del desembolso) y en los créditos de **vivienda** (Ley 546 de 1999) se cobra siempre el corrido,
  aunque la cooperativa elija `CuotaCompleta`; la liquidación y la simulación dicen por qué. Si faltan el salario mínimo o el tope a la fecha del
  desembolso tampoco se cobra la cuota completa. La **novación** y la **refinanciación** (traslado de saldo al crédito nuevo) no lo miran, ni los pagos
  que no cancelan (cuota, vencido, anticipo, valor libre).
- **Cuánto:** lo que la causación diaria todavía no ha causado de la cuota en curso —la primera que vence después de la fecha de pago, si ya empezó—
  desde el día siguiente hasta su vencimiento, con las mismas reglas de siempre (capital no vencido, tasa del día topada por la usura, suspenso).
  La usura queda en la vigente el día del pago: una usura que rija después no cambia lo cobrado. El día del vencimiento de la cuota anterior no hay
  cuota en curso que cobrar.
- **Cómo queda:** el pago lo **causa** en su fecha valor —el comprobante dice «se cobró el interés completo de la cuota en curso»— y lo cubre; el
  crédito queda en cero. Contablemente es causación (interés por cobrar contra el ingreso; el suspenso, a orden) en el mismo comprobante del pago. Sólo
  se cobra si lo pagado cubre todo lo demás: si no, el pago es parcial como siempre. Si cubre todo lo demás pero no alcanza la cuota completa, lo que
  falte queda debiéndose en esa cuota.
- **Reverso y pagos fechados atrás:** reversar la cancelación deshace también ese interés; un pago fechado antes de la cancelación la vuelve a
  calcular con la misma regla. Los pagos aplicados antes de elegir `CuotaCompleta` siguen como se cobraron (el modo queda sellado en cada pago).
- **Orden de los rubros:** lo que cubre un pago que no alcanza para todo sigue el orden de rubros de la empresa (`Pagos.PrelacionDeRubros`, ver
  [cartera-pagos-y-contrato-de-obligaciones.md](cartera-pagos-y-contrato-de-obligaciones.md) §4.1). La simulación de cancelación cubre todo, así que el
  orden no cambia su total.

## 7. La cuota reconocida al inicio del ciclo

Sólo si la cooperativa reconoce la cuota al empezar su período (`Causacion.Momento = InicioDelCiclo`, feature 024, fase 4; ver
[cartera-causacion-y-liquidacion.md](cartera-causacion-y-liquidacion.md) §9.6):

- La columna elegible **«Cuota reconocida por causar»** (en **Vista**) dice, por crédito, lo que se reconoció de la cuota del período y todavía
  no se causó: con lo causado es la cuota completa. No es saldo: no suma a lo que debe por concepto, al total para cancelar ni a lo vencido.
- La **mora** se mide como siempre, desde el vencimiento; la cuota reconocida no está vencida.
- En la **simulación de cancelación**, lo reconocido que no se cause hasta la fecha del pago no se cobra con `Corrido` (se devuelve en
  contabilidad ese día) y con `CuotaCompleta` el interés de la cuota en curso se cobra entero, como en §6.
- Pagar la cuota reconocida antes de su vencimiento deja el valor como **anticipo** de esa cuota (F4, «Cuotas anticipadas») que se aplica al
  vencer; el capital se abona ese día.

## Desde el 2026-11-17

- **Interés aparte de la mora.** Las cuotas pendientes —las del crédito (F3), las de todas las obligaciones (Shift+F3) y su impresión— traen
  el interés corriente y el interés de mora en columnas aparte. Antes salían juntos en «Interés» y la mora parecía interés del plan.
  «Debe» es lo que se debe de la cuota sin el anticipo.
- **Cuotas de las cuentas.** En una cuenta de aportes o de ahorro permanente o contractual, F3 abre sus cuotas pendientes y F4 las
  pagadas: período, vencimiento, estado (pendiente, pagada en parte, no descontada), valor, reparto entre aporte y ahorro permanente,
  pagado y lo que debe. Antes los dos botones se apagaban en las cuentas.
