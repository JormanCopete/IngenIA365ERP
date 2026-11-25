# Mi caja, Cajas del día y el arqueo

Desde el 2026-11-24 (pedido del dueño), la caja de **Recaudo** y la del **punto de venta** se abren, se consultan y se arquean igual. Las piezas
son comunes (`Components/Comunes`): `AperturaDeCaja`, `MiCajaAbierta`, `ArqueoDeCaja`, `ResultadoDelArqueo` y `TablaDeCajas`.

## Dos opciones en cada módulo

| Opción | Recaudo | Punto de venta | Para quién |
|---|---|---|---|
| **Mi caja** | `/recaudo/sesiones` | `/pos/sesiones` | El cajero. Sin caja abierta, la abre: el punto (si hay más de uno), la caja libre y la base. Con la caja abierta ve el día, la base, lo movido y lo que debería haber, y tiene tres botones: cobrar o vender, mover efectivo, arquear y cerrar. |
| **Cajas del día** | `/recaudo/cajas` | `/pos/cajas` | El supervisor. Las cajas del día elegido y las pendientes de días anteriores, con un filtro. Desde aquí se cierra la caja de otra persona; en Recaudo también se aprueban diferencias y se hacen el arqueo sorpresivo y el ajuste. |

Si la sesión abierta es de un día anterior, «Mi caja» y las pantallas de cobro lo avisan (`AvisoSesionDeOtroDia`).

## El arqueo en dos pasos

`/recaudo/sesiones/{id}/cierre` y `/pos/sesiones/{id}/cierre`:

1. **Cuente.** Un renglón por medio de pago con un solo valor. El efectivo se cuenta con un valor o billete por billete, según
   `Caja.FormaDeConteoDelEfectivo`. Al lado de cada medio se ven lo esperado y la diferencia mientras se cuenta.
2. **Revise y cierre.** Un resumen por medio. Se pide el motivo donde hace falta, y cada pantalla agrega lo suyo: en Comercial, el retiro de cierre;
   en Recaudo, el testigo y el acta.

En Recaudo la misma pantalla sirve para el arqueo sorpresivo (`?modo=sorpresivo`) y para cerrar la caja olvidada de otro (`?modo=olvidada`).

## El parámetro `Caja.ArqueoEstricto`

Es un parámetro de `RCD` que leen las dos cajas. **De fábrica viene apagado**, y entonces el arqueo es sencillo:

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
