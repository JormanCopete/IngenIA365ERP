# Mi caja, Cajas del día y el arqueo

Desde el 2026-11-24 (pedido del dueño), la caja de **Recaudo** y la del **punto de venta** se abren, se consultan y se arquean igual. Las piezas
son comunes (`Components/Comunes`): `AperturaDeCaja`, `MiCajaAbierta`, `ArqueoDeCaja`, `ResultadoDelArqueo` y `TablaDeCajas`.

## Dos opciones en cada módulo

| Opción | Recaudo | Punto de venta | Para quién |
|---|---|---|---|
| **Mi caja** | `/recaudo/sesiones` | `/pos/sesiones` | El cajero. Sin caja abierta, la abre: el punto (si hay más de uno), la caja libre y la base. Con la caja abierta ve el día, la base, lo movido y lo que debería haber, y tiene tres botones: cobrar o vender, mover efectivo, arquear y cerrar. |
| **Cajas del día** | `/recaudo/cajas` | `/pos/cajas` | El supervisor. Las cajas del día elegido y las pendientes de días anteriores, con un filtro. Desde aquí se cierra la caja de otra persona; en Recaudo también se aprueban diferencias y se hacen el arqueo sorpresivo y el ajuste. |

Si la sesión abierta es de un día anterior, «Mi caja» y las pantallas de cobro lo avisan (`AvisoSesionDeOtroDia`).

## Quién abre cada caja

Desde el 2026-11-25 (pedido del dueño), cada caja sólo la abren sus cajeros. Así nadie abre por error la caja que usa otra persona. Lo rige el
parámetro **`Caja.SoloCajerosAsignados`** (`RCD`), que **viene encendido**. Se cambia en **Recaudo → Configuración → Parámetros**.

| Dónde | Quién la abre |
|---|---|
| **Recaudo** | Sólo sus cajeros vigentes. Se asignan en **Recaudo → Configuración → Cajas y cajeros → «Cajeros»**. Así funcionaba ya desde la feature 015. |
| **Punto de venta** | Sus cajeros vigentes. Se asignan en **Ventas → Puntos de venta → «Cajas» → «Cajeros»**. Una caja **sin** cajeros la abre cualquiera con acceso al punto, para que nada se detenga mientras se configura. |
| **Caja única** | La caja del punto de venta unida a una caja de Recaudo también la abren los cajeros de esa caja de Recaudo. Basta con asignarlos en Recaudo; el diálogo del punto de venta los muestra de sólo lectura. |

- En «Mi caja», las cajas de otros cajeros se ven pero **no se pueden elegir**: dicen de quién son, por ejemplo «(de Leidy Agudelo)». Las propias van
  primero.
- Si alguien intenta abrir una caja que no es suya, el servidor responde `Inventory.CashSession.CashierNotAssigned` en el punto de venta y
  `Receipts.Session.NotCashierOfBox` en Recaudo. El mensaje dice quiénes la abren.
- Asignar a un cajero rige desde hoy. «Retirar» cierra su vigencia ayer, y nada se borra.
- Con el parámetro apagado no se mira la asignación al abrir: en Recaudo abre quien tenga la caja en su alcance, y en el punto de venta, cualquiera
  del punto.
- La lista de cada caja del punto de venta (`INV_CashRegisterCashiers`) sale de
  `GET/POST /api/inventory/points-of-sale/{id}/cash-registers/{registerId}/cashiers` y de `POST …/cashiers/{userId}/retire`. Consultarla pide
  `Inventory.PointsOfSale.View`, y cambiarla, `Inventory.PointsOfSale.Manage`. Quién abre lo decide un solo sitio, `CajerosDeLaCaja`. La tabla
  llegó con la migración aditiva `CajerosDeLaCajaDelPuntoDeVenta`.

## Ver el detalle en una ventana

Desde el 2026-11-25 (pedido del dueño), el detalle se abre en una **ventana** con su resumen. Antes aparecía al final de la página o sólo se podía
descargar. Los faltantes salen en rojo, los sobrantes en ámbar, y cada medio que difiere va en su propia fila con el motivo. La ventana se cierra con
**Esc** o con «Cerrar».

- **Cierre del día del punto de venta** (`/pos/cierre-del-dia` → «Ver», y al cerrar el día): la ventana tiene estas partes.
  - El resumen: punto, fecha, versión, estado con el motivo de la reapertura, y quién cerró y cuándo.
  - Las cifras: sesiones, esperado, contado y diferencia.
  - El detalle por medio de pago, y el de tarjetas por adquirente y datáfono, que avisa si no se registró el lote.
  - **Sesiones y diferencias.** Cada sesión con su caja, cajero, horario, base, esperado, contado, diferencia y quién arqueó. Debajo de cada sesión
    van los medios que difieren, con su tratamiento (sobrante, a cargo del cajero o al gasto) y el motivo que escribió el cajero. Así se ve a quién
    corresponde cada descuadre sin abrir sesión por sesión.
  - La sesión unida a Recaudo (caja única) dice «se arquea en Recaudo».
  - Desde la ventana se imprime (la hoja también lleva las diferencias por sesión) y se exporta a PDF o Excel.
  - Los datos salen de `GET /api/inventory/day-closes/{id}`, cuyas `sessions` traen `openingBase`, `expected`, `counted`, `difference`,
    `countedByName`, `linkedToCollection` y `differences`.
- **Cajas del día de Recaudo** (`/recaudo/cajas` → «Ver»): la ventana tiene estas partes.
  - El resumen: punto, caja, cajero, día, estado, horario y base.
  - Las cifras del último arqueo y lo que falta por aprobar.
  - Lo esperado por medio: base, recibos, anulados, entradas, salidas y esperado. Con arqueo ciego no se muestra hasta contar.
  - Cada arqueo (cierre, sorpresivo o cierre de la caja olvidada) con sus medios, diferencias, tratamiento, motivo y si espera aprobación.
  - El botón «Informe (PDF)» de la caja cerrada se pasó a la ventana.
- **Cierre del día de Recaudo** (`/recaudo/cierre-del-dia`): al cerrar el día, el resultado (código de verificación, cifras y detalle por medio) se
  abre en la ventana, con el informe del cierre.

## El arqueo en dos pasos

`/recaudo/sesiones/{id}/cierre` y `/pos/sesiones/{id}/cierre`:

1. **Cuente.** Un renglón por medio de pago con un solo valor. El efectivo se cuenta con un valor o billete por billete, según
   `Caja.FormaDeConteoDelEfectivo`. Al lado de cada medio se ven lo esperado y la diferencia mientras se cuenta.
2. **Revise y cierre.** Un resumen por medio. Se pide el motivo donde hace falta, y cada pantalla agrega lo suyo: en Comercial, el retiro de cierre;
   en Recaudo, el testigo y el acta.

En Recaudo la misma pantalla sirve para el arqueo sorpresivo (`?modo=sorpresivo`) y para cerrar la caja olvidada de otro (`?modo=olvidada`).

## El parámetro `Caja.ArqueoEstricto`

Es un parámetro de `RCD` que leen las dos cajas; se cambia en **Recaudo → Configuración → Parámetros** (desde el 2026-11-25). **De fábrica
viene apagado**, y entonces el arqueo es sencillo:

- La base es lo que el cajero cuenta al abrir, con un valor o billete por billete (la suma de los billetes). En el punto de venta con
  `Caja.BaseModo = FondoFijo`, hasta el 2026-11-25 la sesión heredaba en silencio la base de la sesión anterior de la caja: se abría con 30.000
  y el arqueo esperaba 100.000. Con el estricto encendido se sigue heredando el fondo de la caja (sin contar las sesiones de la caja única, que
  abren en cero) y lo digitado distinto sólo se avisa.
- No hay arqueo ciego, aunque `Caja.ArqueoCiego` esté encendido.
- El motivo se pide sólo si la diferencia pasa la tolerancia del medio.
- La diferencia queda resuelta al cerrar, sin que otra persona la apruebe. El faltante va **al gasto** (decisión del dueño). En Recaudo, la línea
  queda aprobada por quien cerró con el motivo «arqueo sencillo».
- Tarjetas y transferencias se cuentan con un total, sin lote de datáfono ni cotejo pago por pago.
- La caja olvidada la cierra su cajero, o un supervisor sin testigo.
- El movimiento con la bóveda no pide segundo custodio ni sello. El acta del sorpresivo y los soportes del ajuste y de la reclasificación son
  opcionales; cuando se suben, se suben como archivo (tipo `ReceiptsCashSupport`, dueño: la sesión).

**Encendido**, todo funciona como antes del 2026-11-24, con sus parámetros: `Caja.ArqueoCiego`, `Caja.DobleCustodia`, `Caja.TratamientoFaltante`,
`Caja.CajeroCierraSuSesionOlvidada` y `Caja.CajeroApruebaSuDiferencia`.

El parámetro **no se sella al abrir**: se lee al momento de cada operación, así que cambiarlo rige también para las cajas que ya están abiertas.
La pantalla lo pregunta en `GET /api/receipts/sessions/count-policy`.

Lo vigilan `ArqueoSencilloTests` (Recaudo) y las pruebas del arqueo sencillo en `CashCommandsTests` (punto de venta). Las e2e de la caja de Recaudo
encienden el estricto en su cooperativa.

## Lo que cambió el 2026-11-26 (revisión de cajas y cierres)

Pedido del dueño: «en Mi caja no me cuenta las ventas que hice por factura de venta, sólo las del POS».

- **Lo vendido cuenta la factura de venta.** La factura que se hace en Ventas y se cobra con un medio que se arquea queda en la caja por su
  pago, no por el documento. «Vendido» (Mi caja, Cajas del día, el arqueo y el detalle de la sesión) y la lista de documentos de la sesión ya
  la cuentan, con la misma regla del informe «Ventas por sesión». La cifra es **neta de notas crédito**, y lo vendido a crédito va aparte en la
  misma línea: «3 venta(s) por 250.000 (a crédito 80.000)».
- **No se cierra con una factura pendiente.** Una factura en borrador o esperando aprobación con un pago en la caja impide cerrarla, como la
  venta suspendida del POS. Antes la caja cerraba con ese dinero como sobrante y confirmar la factura después fallaba.
- **La caja de un día anterior ya no vende.** Como la «olvidada» de Recaudo, la caja del punto de venta abierta desde otro día no abre
  ventas, no retoma, no cobra y la factura no la toma: se cierra en Mi caja y se abre la de hoy. Descartar ventas y cerrar sí se puede.
- **La base contada vale también en Recaudo.** Con el arqueo sencillo, la caja de fondo fijo de Recaudo abre con la base que cuenta el cajero,
  como el punto de venta; con el estricto sigue exigiendo el fondo.
- **Un solo parámetro para las dos cajas.** `Caja.UnaSesionPorCajero` y `Caja.TratamientoFaltante` son los de Recaudo (`RCD`) y los lee
  también el punto de venta, como `Caja.ArqueoEstricto` y `Caja.SoloCajerosAsignados`. Los de Inventario se retiraron. El faltante va al gasto
  por defecto y siempre con el arqueo sencillo.
- **El cierre del día del punto de venta** no se hace con diferencias de arqueo esperando aprobación (`Inventory.DayClose.DifferencesPending`).
- **El cierre del día de Recaudo** cuenta los recibos vigentes y, aparte, los anulados (antes las anulaciones salían siempre en cero).
