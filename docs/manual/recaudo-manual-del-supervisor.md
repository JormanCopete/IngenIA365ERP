# Recaudo: manual del supervisor de caja (R0)

> Feature 015, entrega R0. Receta para **el supervisor de caja** (plantilla `recaudo.supervisor`). Contrato:
> [contracts/api.md](../../specs/015-recaudo-tesoreria/contracts/api.md); historias US1, US2, US4 parte R0 y US5 de la
> [spec](../../specs/015-recaudo-tesoreria/spec.md).

El supervisor configura y vigila los puntos de recaudo de su oficina: abre y cierra el día, aprueba diferencias y anulaciones,
custodia la bóveda y resuelve el dinero que los módulos devuelven. Lo que aprueba siempre lo registró **otra** persona (segregación).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Sesiones de las cajas, lo esperado, arqueo sorpresa, sesión olvidada, ajustes | `/recaudo/sesiones` | `Receipts.Session.View` · `.ViewExpected` · `.SurpriseCount` · `.CloseForgotten` · `.Adjust` |
| Aprobar la diferencia de un arqueo (con monto) | `/recaudo/sesiones` | `Receipts.Count.ApproveDifference` |
| Cierre del día del punto | `/recaudo/cierre-del-dia` | `Receipts.Day.Close` |
| Bóveda: abastecer cajas, recibir su efectivo, despachar remesas | `/recaudo/boveda` | `Receipts.Vault.View` · `.Move` · `.Remit` |
| Aprobar anulaciones (con monto) | `/recaudo/anulaciones` | `Receipts.Voiding.Approve` |
| Aplicaciones reversadas por Cartera o Captaciones | `/recaudo/reversos-de-modulos` | `Receipts.Reversals.View` · `.Resolve` |
| Saldos a favor de pagadores | `/recaudo/saldos-a-favor` | `Receipts.CreditBalances.View` · `.Resolve` |
| Informes (cierres, recaudo del día, excepciones) | `/recaudo/informes` | `Receipts.Reports.View` · `.Export` |

Un supervisor necesita `Receipts.Count.ApproveDifference`, `Receipts.Day.Close` y `Receipts.Voiding.Approve` (FR-176) y el segundo
factor inscrito; sin un supervisor así, Recaudo no se activa (`SupervisorWithPermissions`).

## 2. El día del punto

- Cada punto tiene su día. El cierre del día exige todas las sesiones cerradas y deja, medio por medio, lo esperado, lo contado y la
  diferencia (informe `day-close`).
- La hora de corte (`Recaudo.HoraDeCorte`, 18:00 por defecto) cae antes del ciclo diario de Cartera. Tras el corte, la tarea
  `recaudo.lote-del-dia` contabiliza los recibos de caja de las sesiones cerradas en un comprobante `RR` por día y punto; un punto con
  el día abierto no detiene a los demás: sus recibos entran al lote siguiente (alerta `Recaudo.DiaDelPuntoAbierto`).
- Una sesión que quedó abierta la marca la tarea `recaudo.sesiones-olvidadas`; ciérrela con arqueo diferido y un testigo.

## 3. Diferencias de arqueo

La diferencia la aprueba el supervisor con su motivo y, si pasa del monto de su atribución, quien tenga el monto. El faltante va a
cuenta por cobrar al cajero o a gasto según `Caja.TratamientoFaltante`; el sobrante espera `Caja.Sobrantes.DiasAntesDeIngreso` días
antes de pasar a ingreso (sin valor, no pasa solo; **pendiente de la contadora**, P8).

## 4. Bóveda y remesas

Con `Caja.DobleCustodia` (por defecto sí) cada movimiento de bóveda lleva dos custodios. La remesa al banco se despacha desde
`/recaudo/boveda` con el sello de la bolsa y su acta; Tesorería la confirma contra el extracto (`/tesoreria/remesas`).

## 5. Anulaciones y reversos

- **Anular** un recibo es un documento nuevo, aprobado por otra persona; cada módulo reversa su aplicación y el registro de efectivo
  escribe la fila con signo contrario.
- **Reversos de los módulos**: si Cartera o Captaciones reversan una aplicación, el dinero vuelve a Recaudo y queda en
  `/recaudo/reversos-de-modulos`: aplíquelo a otra obligación o déjelo como saldo a favor del pagador.
- **Saldos a favor**: se aplican en un recibo posterior o se devuelven por **orden de pago** a Tesorería. Si Tesorería no la gira
  (rechazada, anulada o cheque no cobrado), el saldo se reabre solo (hecho `DevolucionNoGirada`). Lo mismo pasa con el dinero retenido
  en custodia por una lista no vinculante: la custodia se reabre.

## 6. Lo que vigila la tarea diaria

`recaudo.antiguedad` alerta: partidas por identificar por escalón de antigüedad, saldos a favor viejos, extractos sin cargar, recibos de
contingencia por digitar, la política contable en «advertir» y, tras la salida en vivo, **el cuadre de apertura provisional** pendiente
(`Recaudo.CuadreProvisionalPendiente`, que escala al vencer su fecha límite).
