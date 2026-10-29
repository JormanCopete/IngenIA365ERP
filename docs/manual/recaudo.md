# Recaudo: índice de manuales y runbooks

> Feature 015 (Recaudo y Tesorería), fase 47 (T0480). Recaudo es el dueño de la plata que **entra**: caja, medios de pago, recibo
> multiproducto, bancos, pagadurías, cheques recibidos, convenios y canales externos, y el SARLAFT del efectivo. Se construyó por
> **entregas**: R0 es el mínimo con el que COOFLOPAL sale en vivo el 01/12/2026 junto con T0; R1 completa y R2 amplía. Este índice enlaza,
> por entrega, los manuales de uso (`docs/manual`) y los procedimientos de operación (`docs/operaciones`). La especificación está en
> [specs/015-recaudo-tesoreria/](../../specs/015-recaudo-tesoreria/spec.md); su par es [tesoreria.md](tesoreria.md).

## Cómo está armado Recaudo, en cinco líneas

1. **Sólo por contratos**: Recaudo no aplica nada por su cuenta; paga obligaciones y abona cuentas llamando al **contrato de obligaciones**
   de Cartera y al **contrato de cuentas** de Captaciones dentro de su propia transacción, obligaciones antes que cuentas.
2. **Un solo camino al recibo** (`ConfirmacionDeRecibo`): listas restrictivas → validación previa contable → cerrojos en orden fijo →
   número → contratos → hechos → un solo guardado. Un rechazo no consume número; la misma operación confirmada dos veces da el mismo recibo.
3. **Recaudo no conoce cuentas ni escribe asientos**: emite hechos a Contabilidad por la bandeja de mensajes, en la misma transacción; la
   contadora parametriza la matriz.
4. **Recaudo y Tesorería se hablan por puertos**: el extracto bancario vive una sola vez, en Tesorería, y Recaudo identifica sobre sus
   líneas; ninguno toca las tablas del otro.
5. **Lo confirmado no se edita**: un recibo se anula o se reversa por porción con otro registro; un proceso por archivo va línea por línea,
   cada una con su clave, y un rechazo no tumba las demás.

## R0 — caja, recibo, bancos y pagadurías (salida en vivo)

| Tema | Manual | Operación |
|---|---|---|
| La caja: sesión, recibo en tres pasos, efectivo grande, contingencia, cierre y anulación | [recaudo-manual-del-cajero.md](recaudo-manual-del-cajero.md) | — |
| El día del punto, diferencias de arqueo, bóveda y remesas, anulaciones y reversos | [recaudo-manual-del-supervisor.md](recaudo-manual-del-supervisor.md) | — |
| Identificar abonos, partidas por identificar, pagadurías y cheques recibidos | [recaudo-manual-del-analista.md](recaudo-manual-del-analista.md) §1–§5 | — |
| SARLAFT básico: umbrales, declaración, quien efectúa, listas y señales | [recaudo-cumplimiento-sarlaft.md](recaudo-cumplimiento-sarlaft.md) | — |
| Contabilización: matriz, puentes, validación previa, cierre y cuadre | [recaudo-y-tesoreria-contabilizacion.md](recaudo-y-tesoreria-contabilizacion.md) | — |
| Plantillas de puesta en marcha y activación | [plantillas-de-importacion.md](plantillas-de-importacion.md) | [recaudo-tesoreria-salida-en-vivo-cooflopal.md](../operaciones/recaudo-tesoreria-salida-en-vivo-cooflopal.md) |
| Despliegue, paralelo en QA y go/no-go | — | [recaudo-tesoreria-despliegue.md](../operaciones/recaudo-tesoreria-despliegue.md), [recaudo-tesoreria-paralelo-en-qa.md](../operaciones/recaudo-tesoreria-paralelo-en-qa.md), [recaudo-tesoreria-go-no-go.md](../operaciones/recaudo-tesoreria-go-no-go.md) |
| El recaudador y la tesorería de prueba (dobles) que se retiran | — | [cartera-dobles-de-prueba.md](../operaciones/cartera-dobles-de-prueba.md) |

## R1 — Recaudo completo

| Tema | Manual | Operación |
|---|---|---|
| Lo que trae R1 a la caja (egresos varios, saldos a favor, referencias, más medios) | [recaudo-manual-del-cajero.md](recaudo-manual-del-cajero.md) §9 | — |
| Lo que trae R1 al supervisor | [recaudo-manual-del-supervisor.md](recaudo-manual-del-supervisor.md) §7 | — |
| Reglas de identificación, consignaciones reportadas y reporte de transacciones a la UIAF | [recaudo-manual-del-analista.md](recaudo-manual-del-analista.md) §6, [recaudo-cumplimiento-sarlaft.md](recaudo-cumplimiento-sarlaft.md) | [recaudo-tesoreria-despliegue.md](../operaciones/recaudo-tesoreria-despliegue.md) §5 |

## R2 — canje, convenios y canales externos

| Tema | Manual | Operación |
|---|---|---|
| Canje completo de cheques recibidos, convenios de recaudo, pasarelas y referencias de pago | [recaudo-manual-del-analista.md](recaudo-manual-del-analista.md) §7 | [canales-externos.md](../operaciones/canales-externos.md) |
| Informes de canales | [recaudo-manual-del-analista.md](recaudo-manual-del-analista.md) §7 | — |

## Pendiente del dueño

Las decisiones con su defecto vigente (umbrales, GMF, conservación, layout UIAF por cotejar, hecho de remesa del punto de venta…) están
en [specs/015-recaudo-tesoreria/RETOMAR.md](../../specs/015-recaudo-tesoreria/RETOMAR.md) y en la tabla P1–P19 de
[research.md](../../specs/015-recaudo-tesoreria/research.md).
