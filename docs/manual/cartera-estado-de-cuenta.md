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
| F3 / doble clic | Cuotas pendientes del producto elegido |
| Shift+F3 | Cuotas pendientes de **todas** las obligaciones vigentes |
| F4 | Cuotas anticipadas (cubiertas por anticipo) |
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
- `CuotaCompleta`: se cobra todo el interés de la cuota en curso aunque se pague antes de su vencimiento.

Hoy el parámetro sólo cambia la **simulación**; la cancelación real sigue cobrando lo corrido. Si la cooperativa elige `CuotaCompleta`, avise:
falta llevarlo al motor de pagos.
