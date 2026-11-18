# Costo de venta al cierre del mes

2026-11-18, pedido del dueño para COOFLOPAL. En un almacén que vende sin existencia y registra las compras después, la venta y la
compra deben pasar a contabilidad en línea, pero el costo de lo vendido se calcula y contabiliza al final del mes, cuando ya entraron
todas las compras.

## Cómo se configura

En **Inventario › Parámetros** (`/inventario/parametros`), el parámetro `Contabilidad.CostoDeVenta` tiene dos valores:

| Valor | Qué hace |
|---|---|
| `ConElDocumento` (defecto) | Como siempre: cada venta lleva su costo en su comprobante. |
| `AlCierreDelPeriodo` | La venta pasa en línea **sin su costo**. El costo espera el cierre del mes de inventario, que lo contabiliza en un solo comprobante. |

Se configura para toda la cooperativa o por tipo de documento (por ejemplo, sólo el POS). Queda fijado en cada documento al
confirmarlo: un cambio rige para lo que se confirme desde su fecha. Lo ya confirmado no se toca.

Se combina con dos parámetros que ya existían:

- `Existencias.StockNegativoPermitido` permite vender sin existencia. La venta sale al último costo, y la compra que llega después le
  ajusta el costo (regularización).
- `Costeo.RetroactivosPermitidos` y `Costeo.RetroactivosDiasMaximos` permiten registrar la compra con su fecha real, aunque sea anterior
  a una venta ya registrada. Esa compra recalcula el costo de las ventas posteriores. La fecha «Desde» tiene que ser anterior a la del
  documento atrasado, y los días máximos no pueden quedar en 0.

## Qué pasa durante el mes

- **La venta, el POS, las notas y la caja** contabilizan en línea: ingreso, IVA, medios de pago y cartera.
- **Las compras** siguen igual: entrada al inventario y cuenta por pagar, en línea.
- **El costo de esas ventas no pasa**, ni los ajustes que lo corrigen: regularización de lo vendido sin existencia, compra con fecha
  atrasada y diferencia de precio de la factura del proveedor. Quedan guardados para el cierre.
- **Anular una venta** deja también su costo en espera, con signo contrario, y en el cierre se cancelan entre sí.
- **Una remisión** (sólo costo, sin parte comercial) sigue llevando su costo con ella.

## Qué pasa al cerrar el mes

En **Inventario › Períodos** (`/inventario/periodos`), cerrar el mes suma todo el costo que espera de ese mes y emite un solo mensaje,
`CostoDelPeriodoReconocido`. Contabilidad lo convierte en **un comprobante de costo de ventas** fechado el último día del mes:

- con el tipo de comprobante de la operación `CostoDeVenta` (en COOFLOPAL, `FV`);
- con las mismas cuentas de la matriz de inventario que habrían usado las ventas una a una;
- agrupado por grupo contable del producto, bodega, sucursal y centro de costo, y los ajustes además por motivo.

Lo que cambia es **cuándo** pasa el costo, no el asiento: la suma es la misma que la de los comprobantes uno a uno.

**Reabrir el mes** reversa ese comprobante, con la misma fecha, y deja el costo otra vez en espera. Volver a cerrarlo genera el
comprobante nuevo con lo que haya entrado entre tanto.

**El cierre contable del mes no pasa** mientras quede costo de ese mes, o de uno anterior, sin enviar. El aviso es
`Accounting.Period.InventoryCostPending` y dice qué meses faltan. Primero se cierra el mes de inventario y después el contable.

## Lo que no es

No es un promedio ponderado mensual con un solo costo para todas las ventas del mes. Cada venta toma el promedio a su fecha, y se
recalcula si entra una compra con fecha anterior. Al cierre, el total queda muy cerca del promedio mensual, pero no es idéntico.

## Por dentro

- `INV_Documents.CostAtPeriodClose` sella la decisión al confirmar; la anulación la copia del anulado.
- `INV_DeferredCosts` guarda cada contenido de costo tal cual habría viajado (JSON), con la sucursal y el centro de su sobre:
  - `CostDocumentId` es el documento cuyo costo es;
  - `PeriodCostPostingId` es el envío que lo incluyó, nulo mientras espera.
- `INV_PeriodCostPostings` registra cada envío del cierre y su reversión, y es el origen del mensaje.
- El código vive en tres sitios:
  - `CostoAlCierre` (`Application/Inventory/Integration`): sella, separa, guarda, suma, cierra y reabre.
  - `ConfirmacionDeDocumento`: llama a `CostoAlCierre` antes de emitir los mensajes.
  - `CostoPendienteParaElCierre`: es el `IEstadoOperativoParaElCierre` de Inventario.
- Contabilidad desglosa cada parte del mensaje como su tipo original, igual que el espejo de un `DocumentoAnulado`
  (`ConstructorDeLineasDeInventario.Desglosar`).
- Migración aditiva `CostoDeVentaAlCierre`.
- Pruebas: `CostoAlCierreTests` y `CostoAlCierreEnLaConfirmacionTests`.
