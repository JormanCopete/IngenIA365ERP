# Canal digital — reverso de un pago en línea (feature 021, US7)

Para el administrador de la cooperativa (permiso **`Channel.Payments.Reverse`**). Un pago que el asociado hizo en el canal
digital y que hay que devolver —pago doble, error del asociado, solicitud de devolución— se reversa desde el ERP con un
motivo. El ERP decide el camino; el funcionario no lo elige.

Hoy se pide por la API (`POST /api/channel/payments/{referencia}/reverse`, cuerpo `{ "reason": "…" }`); el botón llega
con la pantalla de Atención (`/canal/atencion`, fase 11).

## Los dos caminos

| Cuándo | Qué pasa | Qué ve el asociado |
|---|---|---|
| **Antes del corte**: el mismo día de la aprobación (hora de Colombia) y antes de la hora de corte del destino (Destinos de pago; 20:00 por defecto) | El canal pide el reverso a la pasarela. Si la pasarela lo reversa, Recaudo **anula el recibo** «en línea» por los contratos del módulo dueño (crédito, aportes o ahorro), con un documento de anulación numerado y su **asiento espejo** contra la cuenta del medio: el dinero no llega al banco | «Reversado»: la pasarela le devuelve el dinero al medio con que pagó |
| **Después del corte** (o el pago es de un día anterior) | **Devolución por Tesorería**: Recaudo anula el recibo y el valor queda como **saldo a favor** del asociado, que sale de inmediato en una **orden de pago** a Tesorería (transferencia; la cuenta del asociado se puede indicar o Tesorería la pide al girar) | «Devuelto»: la cooperativa le devuelve el dinero |

Además:

- Si el pago **dejó un saldo a favor** (parte que ningún producto admitió), va siempre por Tesorería y la orden devuelve
  también ese saldo: es dinero del asociado que ya está en Recaudo.
- Si el canal responde que el corte ya pasó (su reloj manda), el ERP sigue por Tesorería sin preguntar.
- Si la pasarela **no** lo reversa, no se cambia nada y el ERP muestra el motivo de la pasarela.
- Si el recibo ya se anuló en Recaudo, el reverso no se hace desde el canal.
- Repetir el reverso no hace nada dos veces. Si la llamada se cortó después de que la pasarela reversó, repetirlo termina
  la anulación en Recaudo, aunque ya haya pasado el corte.

## Conciliación

Un pago reversado por la pasarela que aparezca aprobado en el reporte diario **no se aplica**: la línea queda como
partida por identificar con el motivo «pago del canal reversado» (`Receipts.GatewayFile.ChannelPaymentReversed`). Si el
dinero llegó, se devuelve desde la partida. La devolución por Tesorería sí se concilia: el dinero entró, el recibo está
anulado y la orden de pago lo devuelve (ver `canal-digital-conciliacion.md`).

## Auditoría

Queda `Channel.Payment.Reversed` en el ERP (referencia, modo, motivo, recibo, anulación, órdenes) y
`Canal.Pago.Reversado` en el canal, con un evento `reversal` en la historia del intento.
