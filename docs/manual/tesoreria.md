# Tesorería: índice de manuales y runbooks

> Feature 015 (Recaudo y Tesorería), fase 47 (T0480). Tesorería es la dueña de la plata que **sale** y de los bancos: cuentas propias,
> fondo de liquidez, bandeja de órdenes, lotes y archivos de pago, cheques girados, remesas y conciliación bancaria. Se construyó por
> **entregas**: T0 es el mínimo con el que COOFLOPAL sale en vivo el 01/12/2026 junto con R0; T1 completa, T2 amplía, y después de T2 se
> retira la Tesorería anterior. Este índice enlaza, por entrega, los manuales de uso y los procedimientos de operación. La especificación
> está en [specs/015-recaudo-tesoreria/](../../specs/015-recaudo-tesoreria/spec.md); su par es [recaudo.md](recaudo.md).

## Cómo está armada Tesorería, en cinco líneas

1. **Tesorería no decide a quién ni cuánto se paga**: lo dice la orden que llega por la bandeja (`OrdenDeGiroEmitida` de Cartera,
   Captaciones, Recaudo o Comercial, o una orden manual aprobada), y a cada origen le responde el resultado del giro.
2. **Una sola bandeja, un solo camino al banco**: orden → lote con forma de pago → aprobación por monto (y por firmantes desde T1) →
   archivo o portal → resultado; la cuenta destino y el archivo generado se verifican antes de pagar.
3. **Tesorería no conoce cuentas ni escribe asientos**: el vínculo de cada cuenta propia con su auxiliar lo escribe Contabilidad, y lo que
   cruza va en hechos por la bandeja de mensajes.
4. **El extracto bancario se guarda una vez**, en Tesorería; Recaudo identifica sobre sus líneas y la conciliación cruza contra lo
   contabilizado de la cuenta.
5. **Lo enviado no se edita**: un archivo enviado no se anula, una conciliación cerrada se reabre en una versión nueva y la Tesorería
   anterior se retira con una migración destructiva con guarda, irreversible.

## T0 — giros, cheques, remesas y conciliación mínima (salida en vivo)

| Tema | Manual | Operación |
|---|---|---|
| Cuentas propias, fondo de liquidez, de la orden al pago, cheques, remesas y conciliación mínima | [tesoreria-manual-del-tesorero.md](tesoreria-manual-del-tesorero.md) §1–§5 | — |
| Contabilización: matriz, vínculo de cuentas, puentes, validación previa | [recaudo-y-tesoreria-contabilizacion.md](recaudo-y-tesoreria-contabilizacion.md) | — |
| Formatos bancarios (archivos de pago y de liquidación) | [dispersion-bancaria.md](dispersion-bancaria.md) | — |
| Puesta en marcha, cuadre de apertura y activación | [plantillas-de-importacion.md](plantillas-de-importacion.md) | [recaudo-tesoreria-salida-en-vivo-cooflopal.md](../operaciones/recaudo-tesoreria-salida-en-vivo-cooflopal.md) |
| Despliegue, paralelo en QA y go/no-go | — | [recaudo-tesoreria-despliegue.md](../operaciones/recaudo-tesoreria-despliegue.md), [recaudo-tesoreria-paralelo-en-qa.md](../operaciones/recaudo-tesoreria-paralelo-en-qa.md), [recaudo-tesoreria-go-no-go.md](../operaciones/recaudo-tesoreria-go-no-go.md) |

## T1 — traslados, cheques al cierre, órdenes manuales y firmantes

| Tema | Manual | Operación |
|---|---|---|
| Traslados entre cuentas propias, cheques al cierre del mes, remesa del punto de venta, órdenes manuales, firmantes y tokens, comprobante de pago | [tesoreria-manual-del-tesorero.md](tesoreria-manual-del-tesorero.md) §6 | [recaudo-tesoreria-despliegue.md](../operaciones/recaudo-tesoreria-despliegue.md) §5 |

## T2 — conciliación completa, proveedores, liquidez y adquirentes

| Tema | Manual | Operación |
|---|---|---|
| Conciliación completa con reglas y notas bancarias, pagos a proveedores y cuentas por pagar, posición de liquidez, flujo de caja, tablero y adquirentes | [tesoreria-manual-del-tesorero.md](tesoreria-manual-del-tesorero.md) §7 | — |
| Informes de T2 (`reconciliations`, `liquidity`, `cash-flow`) | [tesoreria-manual-del-tesorero.md](tesoreria-manual-del-tesorero.md) §7 | — |

## Después de T2 — el retiro de la Tesorería anterior

| Tema | Manual | Operación |
|---|---|---|
| Exportación para archivo, carga de pendientes y reemplazos de cada pantalla retirada | [tesoreria-manual-del-tesorero.md](tesoreria-manual-del-tesorero.md) §8 | [tesoreria-retiro-heredado.md](../operaciones/tesoreria-retiro-heredado.md) (destructivo con guarda; lo aprueba el dueño por base) |

## Pendiente del dueño

Las decisiones con su defecto vigente (cobro tardío de cheques, facturas de contado, firmantes, fondo de liquidez, layouts de las redes…)
están en [specs/015-recaudo-tesoreria/RETOMAR.md](../../specs/015-recaudo-tesoreria/RETOMAR.md); el retiro espera el diagnóstico, el respaldo
y la aprobación del dueño como segundo revisor.
