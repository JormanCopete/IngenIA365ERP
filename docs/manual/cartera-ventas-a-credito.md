# Cartera: ventas a crédito de Comercial

> Feature 013 (cartera financiera), fase 10 (US6, entrega K1). Complementa
> [contracts/interfaces.md](../../specs/013-cartera-financiera/contracts/interfaces.md) §5 (consultas y reservas de Cartera),
> [contracts/mensajes.md](../../specs/013-cartera-financiera/contracts/mensajes.md) §11, §12 y §15 (la venta, sus ajustes y las respuestas),
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §7.3, §8.2 y §16.2, y
> [quickstart.md](../../specs/013-cartera-financiera/quickstart.md) §3.7. Aquellos son el contrato; esto es la receta para quien vende a
> crédito en el almacén o el POS, para el jefe de cartera que recibe esas ventas y para quien atiende la bandeja de mensajes.

Una venta a crédito de Comercial **siempre** termina en una obligación de Cartera, pero las dos no se hablan por dentro: Comercial
**consulta** el cupo y **reserva** el número por los dos puertos de Cartera (`IConsultasDeCartera`, `IReservasDeCartera`), y Cartera
**recibe la venta por mensaje** (`VentaACreditoRegistrada` v2) y le **responde por mensaje** (`ObligacionCreada` u `ObligacionRechazada`).
Comercial guarda de esa respuesta el **número y el estado**, nunca saldos ni cuotas: para eso está Cartera.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Consultar cupo y estado al elegir un medio de crédito | la venta de oficina y el cobro del POS | `Inventory.Sales.SellOnCredit` |
| Ver el número del crédito y su estado en Cartera | pestaña «Crédito» de la venta (`/ventas/documentos/{id}`) | `Inventory.Sales.View` |
| Ver lo que llegó a Cartera, sus evaluaciones y la respuesta | `/cartera/bandeja-de-mensajes`, pestaña «Recibidos» | `Lending.Messages.Read` |
| Marcar no reprocesable un recibo rechazado | misma pestaña, «Marcar no reprocesable…» | `Lending.Messages.Reprocess` |
| Ver las corridas del proceso diario | misma pantalla, pestaña «Corridas del proceso» | `Lending.Processes.View` |
| Confirmar por excepción una validación fallida | el aviso del resumen de `/cartera/creditos/{id}` | `Lending.ValidationFailures.Confirm` (con su límite) |
| Ver las reservas de número | `/cartera/cupos`, botón «Reservas de número» | `Lending.NumberReservations.Read` |

Fuera del alcance de oficina y línea, el recibo de un crédito ajeno no existe (el mismo 404). Un recibo sin crédito —rechazado o no
incorporado— no es de ninguna oficina y lo ve quien tiene el permiso.

## 2. El cupo rotativo y la consulta

El cupo que sirve para comprar en el almacén es el **rotativo** de la persona (US2), sin línea: vale para toda línea de Cartera que admita
el **origen comercial** y el tipo de comprador (asociado o cliente). El disponible es el de siempre: cupo − saldos vivos como deudor y
codeudor − reservas vigentes − solicitudes aprobadas sin desembolsar.

Al elegir un medio de crédito, Comercial pregunta a Cartera (con el plazo de `Cartera.ConsultaSegundos`) y recibe:

- el **estado** (`Active`, `InArrears` con los días de mora, `Blocked` con el motivo en español, `NoRelationship`) y si **admite la venta**
  (`allowsSale`: un bloqueo, o una mora mayor que `Cupo.MoraMaximaParaVender`, no la admite);
- el **disponible** y el cupo total;
- las **líneas** de origen comercial con plazo y cuotas máximas y las periodicidades de Comercial que tienen equivalencia;
- una **evidencia** (`evidenceId`): la consulta queda en la auditoría (`Lending.CreditStatus.Queried`) y su identificador viaja con la
  reserva y con la venta.

La consulta verifica listas restrictivas y la autorización de datos (`CreditProcessing`) **sin escribir nada**: una coincidencia o la
falta de autorización se ven como `Blocked`, sin decir por qué (el detalle es del oficial de cumplimiento).

Con esa respuesta la venta: bloqueada o sin admitir → `Inventory.Credit.LendingBlocked`; más que el disponible →
`Inventory.Credit.QuotaExceeded` con el disponible; sin respuesta a tiempo → lo que diga `Cartera.PoliticaSinRespuesta` (bloquear, o
seguir como crédito provisional con aprobación); válida → crédito **validado**, sin la aprobación provisional de la 012.

## 3. La reserva del número y por qué puede no haberla

Al confirmar una venta validada —también si queda esperando una aprobación del documento— Comercial **reserva** el número del crédito
dentro de su propia transacción, después de bloquear sus filas y antes de numerar la factura (orden de cerrojos C24). La clave de la
reserva es `INV:{pago}:{valor}`: repetir la confirmación devuelve **el mismo número**; la misma clave con otro valor es
`Lending.Reservation.KeyReused`.

La reserva corre bajo un **punto de guardado propio** (C25): si Cartera falla por algo técnico, se deshace sólo la reserva y **la venta se
confirma sin número** (D26). El número llega igual con `ObligacionCreada`. Si Cartera rechaza la reserva (el cupo se agotó entre la
consulta y la confirmación, o el deudor quedó bloqueado), la confirmación no procede.

Una reserva vive `Reservas.DiasDeVigencia` días. Descartar el borrador la **libera**; si nadie la usa, la tarea diaria
`cartera.reservas-vencidas` la deja **vencida** y avisa (`Cartera.ReservaVencida`). **Un número reservado nunca se reasigna**, ni vencido.

## 4. Cómo nace la obligación de una venta

El despachador entrega la `VentaACreditoRegistrada` a Cartera (destino `Lending`), que en **una** transacción:

1. deja su recibo (`LND_InboundMessages`, único por mensaje: el mismo mensaje dos veces no crea otro crédito);
2. convierte la venta: la persona, la línea (la del pago, la sugerida o la por defecto del tipo de comprador), la periodicidad y la unidad
   de plazo por las **equivalencias de Comercial** (`/cartera/lineas`, pestaña de equivalencias);
3. si la venta estaba **pendiente de validar** (crédito provisional, o vendida antes de activar Cartera), la **evalúa a su fecha**: estado,
   cupo sin contarla, concentración, usura, línea comercial, listas y autorización;
4. **crea siempre la obligación** con desembolso en especie, la factura como referencia y el número reservado (o uno nuevo, si la reserva no
   existe); aplica los cobros provisionales que Comercial registró de esa factura;
5. responde `ObligacionCreada` a Comercial, que la guarda (`INV_LendingObligationResponses`) y la muestra en la venta.

## 5. Validación fallida y su confirmación por excepción

Si la evaluación del paso 3 no pasa, la obligación **nace igual** —la venta ya se hizo y la plata se debe— pero **marcada**: Comercial ve
«Validación fallida» con el motivo, el crédito muestra el aviso en su resumen y el deudor queda **bloqueado para crédito nuevo** (bloqueo
`ValidationFailed`). La marca no cambia la causación, el cobro ni la calificación. **La venta no se anula.**

Quien tiene `Lending.ValidationFailures.Confirm` la **confirma por excepción** desde el aviso, con motivo: se levanta el bloqueo, el recibo
queda «Confirmado por excepción» y Comercial ve la venta validada. Si el valor supera su atribución, el crédito pasa a decisión del nivel que
corresponda. **Listas restrictivas, usura, autorización de datos y capacidad de pago no admiten excepción**
(`Lending.Attribution.NotExceptionable`).

## 6. Notas, anulaciones y reemplazos

Comercial manda un `AjusteDeVentaACredito` por cada nota o anulación que toca el crédito; Cartera lo aplica como operación
`CommercialAdjustment` del crédito, desde su fecha:

| Ajuste | Qué hace Cartera |
|---|---|
| Nota crédito o devolución | Baja capital, reversa el interés causado sobre lo devuelto y recalcula el plan según la línea (`NoteRecalculation`); lo que exceda el saldo queda como saldo a favor |
| Anulación (incluida la del rechazo de la DIAN) | Deja el crédito sin efecto desde su origen; lo pagado queda como saldo a favor |
| Nota débito | Sube capital y regenera el plan; se evalúa como una venta |
| Reemplazo (la factura que reemplaza a otra) | Crea un crédito nuevo enlazado al anulado y responde `ObligacionCreada` con el crédito reemplazado |

Un ajuste espera a su venta: si la obligación todavía no existe, se reintenta.

## 7. Ventas hasta el corte de la carga

Las ventas fechadas **hasta el corte** de la carga activada nunca se procesan como ventas en vivo: responde lo que diga la conciliación de
la carga (FR-143) —migrada, cancelada antes del corte, aceptada sin migrar o fuera de Cartera—. Una venta hasta el corte que llega sin fila
de conciliación (emitida después de activar) queda **no incorporada**: Cartera responde `ObligacionRechazada` y avisa
(`Cartera.VentaNoIncorporada`). La conciliación la arma la carga de la migración al aplicarse y activarse (US8).

## 8. Mensajes no reprocesables

Una venta que Cartera **no puede convertir** (la persona no existe, la línea no es válida y no hay alterna, la periodicidad no tiene
equivalencia, una versión que no conoce) queda **rechazada y reprocesable**, sin respuesta a Comercial: la venta se ve «Pendiente» con el
motivo. Lo normal es **corregir la causa** (crear la equivalencia, la línea) y reprocesar desde la bandeja de integración.

Si la venta no se va a financiar nunca, alguien con `Lending.Messages.Reprocess` la **marca no reprocesable** con motivo en
`/cartera/bandeja-de-mensajes`: Cartera responde en el mismo momento `ObligacionRechazada` (motivo `NoConvertible`) y Comercial la ve
«Rechazada por Cartera». Marcar no se deshace.

## 9. Lo que Comercial guarda y muestra

En la pestaña «Crédito» de la venta, por cada pago a crédito: el **número** (el reservado o el de la respuesta) y el **estado en Cartera**
—«Validada», «Validación fallida», «Migrada», «Cancelada antes del corte», «No incorporada», «Pendiente: queda para reproceso» o
«Rechazada por Cartera»—, con el motivo cuando lo hay. Además, los mensajes a Cartera con su entrega. **Nunca saldos ni cuotas**: la
cartera de la persona se consulta en Cartera.

Un medio marcado **crédito sin Cartera** (`CreditWithoutLending`, D29: cuotas de servicios a asociados) no consulta, no reserva y no manda
nada a Cartera: sigue como el crédito provisional de la 012, con su aprobación y la cuenta por cobrar en Contabilidad.
