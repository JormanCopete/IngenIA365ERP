# Caja única y conteo del efectivo

2026-11-23, pedido del dueño. Quien factura en Comercial y además recauda abría y cerraba dos cajas: la del punto de venta y la de
Recaudo, cada una con su base y su arqueo. Ahora puede trabajar con **una sola**. Además, el efectivo se cuenta como prefiera la
cooperativa: con un solo valor o billete por billete.

## 1. Cómo se cuenta el efectivo

Parámetro **`Caja.FormaDeConteoDelEfectivo`** (en **Recaudo → Configuración → Parámetros**). Rige en las dos cajas, Recaudo y punto de
venta, al abrir y al arquear.

| Valor | Qué ve el cajero |
|---|---|
| `AElegir` (defecto) | Un selector: «Un solo valor» o «Billete por billete» |
| `ValorGlobal` | Un solo campo con el efectivo contado |
| `Denominaciones` | La tabla de billetes y monedas. El servidor rechaza el efectivo con un solo valor (`Receipts.Session.CountByDenominations`) |

Los movimientos con la bóveda (abastecer, retirar, trasladar) siguen por denominaciones, porque la bóveda lleva la cuenta de cada
billete.

## 2. El arqueo, más sencillo

Al «Arquear y cerrar» cada medio muestra solo su forma de contarlo, con una línea que dice para qué es:

- **Efectivo**: el selector anterior.
- **Tarjetas**: un renglón por cada cierre de lote del datáfono, con el número del lote y su valor.
- **Transferencias, consignaciones y cheques**: el total de los soportes. En el punto de venta se marcan uno por uno.
- **Medios que no se arquean**: no aparecen.

Sin arqueo ciego, cada medio dice mientras se cuenta: esperado, contado y diferencia. El **motivo** aparece solo cuando hay diferencia.
Con arqueo ciego no se ve lo esperado; si al registrar hay diferencia, la pantalla lo avisa y pide el motivo de cada medio.

El testigo y el acta aparecen solo cuando se exigen: sesión olvidada y arqueo sorpresivo.

## 3. Caja única

### Cómo se enciende

1. **Recaudo → Configuración → Cajas y cajeros** (`/recaudo/configuracion/cajas`): en la fila de la caja de Recaudo, el botón «Punto de
   venta», y elegir el punto y la caja del punto de venta donde vende la misma persona. La columna «Punto de venta» queda en «Unida». Una
   caja del punto de venta va con una sola caja de Recaudo. Ver la opción pide `Receipts.Setup.View`; el botón, `Receipts.Setup.Manage`.
2. **Contabilidad**: en la matriz de Recaudo, el rol **«Puente de la caja del punto de venta»** (`PuenteDelPuntoDeVenta`). Póngale
   la **misma cuenta** que tiene la **caja fuerte** en la matriz de Comercial. Así el efectivo del punto de venta sale de la caja de
   Comercial y entra a la de Recaudo sin descuadrar nada. Lo decide la contadora.
3. **Recaudo → Configuración → Parámetros** (`/recaudo/configuracion/parametros`, desde el 2026-11-25): «Cambiar» en `Caja.SesionUnica`, valor
   Sí, la fecha desde la que rige y el motivo. Apagado por defecto.

   **Es seguro**: no se deja encender mientras falte el paso 1 (ninguna caja unida) o el 2 (sin la cuenta puente, cuando la falta de regla
   bloquea, `Contabilidad.FaltaDeRegla` de Recaudo). El mensaje dice qué falta (`Receipts.Setup.SingleSessionNotReady`). Apagarla no pide nada:
   las cajas vuelven a abrir y cerrar por separado desde la próxima apertura.

### Cómo se trabaja

- **Abrir.** El cajero abre su caja en **Recaudo → Mi caja**, con una sola base. La caja del punto de venta se abre sola,
  vinculada y con base cero. Si no se pudo abrir (por ejemplo, porque ya tenía una abierta por su cuenta), la sesión lo dice y ofrece
  «Abrir la caja del punto de venta». Con `Caja.SoloCajerosAsignados` (encendido de fábrica, 2026-11-25) la abren sólo los cajeros de la caja de
  Recaudo y los propios de la caja del punto de venta: basta con asignarlos en Recaudo (ver
  [mi-caja-y-arqueo.md](mi-caja-y-arqueo.md#quién-abre-cada-caja)).
- **Durante el día.** Vende en el punto de venta y recauda en la Caja sin abrir nada más.
- **Cerrar.** Un solo «Arquear y cerrar» en Recaudo, con todo el dinero del cajón. Sin arqueo ciego, lo esperado de cada medio ya incluye
  lo vendido. Al registrar:
  1. **primero se comprueba** el arqueo con lo vendido, sin escribir nada (desde el 2026-11-25). Si le falta algo (un motivo, el conteo
     por billetes, quién puede cerrar la olvidada), lo dice y **la caja del punto de venta sigue abierta**;
  2. la caja del punto de venta se cierra sola y **cuadrada**, y su efectivo va a la caja fuerte con el retiro de cierre;
  3. lo que **contó** ese cierre entra a la sesión de Recaudo como «ingreso del punto de venta», un movimiento por medio;
  4. se hace **un solo arqueo**. Cualquier diferencia queda en Recaudo, con sus reglas: tolerancia, aprobación y faltante al cajero o
     al gasto (con el arqueo sencillo, al gasto).

  Repetir el arqueo es seguro: si el paso 2 o 3 ya se hizo, no se repite ni se pide dos veces.
- **El punto de venta** muestra «Cerrar en Recaudo» en vez de «Cerrar la caja». Si se intenta cerrar desde allí, responde
  `Inventory.CashSession.ClosesWithReceipts`.
- El **cierre del día** del punto de venta sigue igual: las sesiones vinculadas ya quedaron cerradas. Su efectivo **no se remesa desde
  el punto de venta**, porque ya pasó a Recaudo (la remesa del cierre del día sólo puede llevar el efectivo de las sesiones propias).
- La **sesión olvidada** la cierra el supervisor desde «Cajas del día» de Recaudo, con la caja del punto de venta incluida, aunque no
  tenga permiso para ver las sesiones ajenas del punto de venta.

### Lo que hay que saber

- Para cerrar no debe haber **ventas suspendidas ni borradores** en el punto de venta. Si hay, el arqueo responde lo mismo que el cierre
  del punto de venta y no se escribe nada.
- Un **medio que Recaudo no recibe** (no está en los medios del punto de recaudo) se queda cerrado y cuadrado en el punto de venta: no
  entra al arqueo único.
- **Solo el efectivo lleva asiento** al entrar a Recaudo (`Caja` ← `PuenteDelPuntoDeVenta`). Las tarjetas y transferencias del punto de
  venta entran al arqueo para contarlas juntas, pero su contabilidad sigue en las cuentas de Comercial.
- El **arqueo sorpresivo** de una caja única cuenta **el cajón entero**: lo esperado incluye lo que la caja del punto de venta lleva
  vendido (desde el 2026-11-25; antes la pantalla lo sumaba y el servidor no, y salía una diferencia falsa).
- Una vez que la caja del punto de venta se cerró con el arqueo, **no se abre otra en la misma sesión** de Recaudo
  (`Receipts.SingleSession.PointOfSaleAlreadyClosed`): lo de esa segunda caja nunca entraba a Recaudo. Termine el arqueo.
- Si en el punto de venta un medio quedó **en negativo** (salió más efectivo del que entró, por ejemplo una devolución), el arqueo lo
  rechaza antes de cerrar nada (`Receipts.SingleSession.PointOfSaleNegative`): registre en el punto de venta un ingreso desde la caja
  fuerte por ese valor y vuelva a arquear.
- La caja del punto de venta de una sesión la abre **su cajero**; la consulta exige que la sesión esté en el alcance de quien pregunta.
- Quien solo vende (un punto sin Recaudo) sigue con la caja del punto de venta como siempre.

## Por dentro

- Migración aditiva `CajaUnica`: `RCD_CashBoxes.PosCashRegisterPublicId` e `INV_CashSessions.LinkedSessionPublicId`.
- **Coordinador.** `CajaUnica` (`Application/Receipts/Sessions/CajaUnica.cs`) coordina sin transacción propia. Cada paso es un comando
  con su propia clave, derivada de la de la petición, así que repetir es inocuo:
  - abrir la del punto de venta (`OpenCashSessionCommand` con `LinkedSessionPublicId`);
  - cerrarla cuadrada (`CloseCashSessionCommand` con `DesdeLaCajaUnica`);
  - entrar lo recibido (`RegisterPointOfSaleIntakeCommand`, `CashMovementKindR.FromPointOfSale`).
- **Rutas.**
  - `GET /api/receipts/sessions/cash-count-mode`
  - `GET /api/receipts/sessions/{id}/point-of-sale`
  - `POST /api/receipts/sessions/{id}/point-of-sale/open`
  - `PUT /api/receipts/cash-boxes/{id}/point-of-sale-register`
- Caso dorado: `tests/IngenIA365ERP.Domain.Tests/Receipts/Casos/arqueo-caja-unica-01-punto-de-venta-y-valor-global.json`.
