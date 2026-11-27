# Descuento por pago anticipado

Desde el 2026-11-26 (pedido del dueño, para COOFLOPAL), la caja de Recaudo puede descontar parte del interés de una cuota que el asociado paga por
anticipado. El descuento lo asume la cooperativa: el crédito recibe la cuota completa y nada cambia en Cartera.

## Cómo se configura

1. **Cartera → Parámetros**, clave `Pagos.DescuentoPorPagoAnticipado`. Es la fracción del interés de la cuota que se descuenta, de 0 a 1.
   - De fábrica vale 0, que es sin descuento.
   - Se fija por empresa o con excepción por línea de crédito, y tiene vigencia.
   - Ejemplo: la fórmula del sistema anterior «INTERES×1,4÷1,8−INTERES» es la fracción 1 − 1,4/1,8 = **0,2222222222**. Con diez decimales da lo mismo,
     al peso, que la fórmula.
2. **Contabilidad → Matriz de Recaudo**, rol **«Descuento por pago anticipado»**. La contadora le pone la cuenta donde la cooperativa asume el
   descuento: un fondo, un gasto o el ingreso por intereses al débito. En COOFLOPAL es la 41501002.

## Cómo se usa en la caja

1. En «Detalle» (F3) de la obligación, el cajero marca las cuotas que el asociado anticipa.
2. Si la línea tiene descuento, la tabla muestra por cuota el capital, el interés, el seguro y los demás rubros, y además «Descuento» y «A pagar».
3. Debajo aparece la casilla **«Aplicar descuento por pago anticipado (22,22 % del interés)»** con el total. El cajero decide si la marca; de
   entrada está sin marcar.
4. En «Qué paga», cada cuota con descuento lleva su casilla para quitarlo o volverlo a poner. El total a pagar ya viene con el descuento restado.
5. El recibo y la tirilla muestran «Total aplicado», «Descuento por pago anticipado» y «Total pagado».

## Qué cuotas lo llevan

El servidor lo vuelve a decidir al simular y al confirmar (`Receipts.Receipt.AdvanceDiscountNotAllowed`, con el motivo). Sólo lo lleva una cuota
que cumple todo esto:

- Se anticipa **completa**, con la modalidad «cuota específica».
- Es de un **período que no ha empezado**. La cuota del período en curso no lleva descuento, sea la próxima que vence o, por período calendario,
  la del mes en curso.
- **No tiene anticipo previo**.
- Su línea **tiene descuento** a la fecha valor.
- **El crédito queda al día**. Si tiene cuotas vencidas, el recibo tiene que llevar antes «Lo exigible» por lo vencido, y la cooperativa tiene que
  tener encendido `Caja.ConMoraPermiteAnticipar`.

Cómo se calcula el descuento: el interés del plan de la cuota por la fracción, al peso. Si entre la simulación y la confirmación cambia (otra
fracción, otra cuota), la confirmación se rechaza con `Receipts.Receipt.AdvanceDiscountChanged` y hay que volver a simular.

## Qué queda registrado

- **Cartera** recibe un solo pago por la cuota completa, como un anticipo de esa cuota. El libro, la causación, el estado de cuenta y el
  certificado tributario no cambian.
- **Recaudo** guarda en la aplicación del recibo el descuento y la fracción usada (`RCD_ReceiptApplications.AdvanceDiscount` y
  `AdvanceDiscountFraction`, migración `DescuentoPorPagoAnticipado`).
- **Contabilidad**, en el comprobante del recibo (o en el único comprobante, con `Accounting.CashReceiptSingleVoucher`):

  | Cuenta | Débito | Crédito |
  |---|---|---|
  | Caja | lo que pagó el asociado | |
  | Descuento por pago anticipado | el descuento | |
  | Recaudo por aplicar (puente) | | la cuota completa |

  Cartera cancela el puente contra el anticipo, como siempre.
- **Anular el recibo** reversa también el descuento: crédito al rol del descuento y débito al puente. Si Cartera reversa por su cuenta el pago que
  llevó el descuento, el descuento vuelve a la cooperativa; no queda como saldo a favor del pagador.

## Lo que se aceptó

- Si después se devuelve ese anticipo o se cancela el crédito antes del vencimiento, Cartera no conoce el descuento y el asociado lo conserva.
  Es raro, y el recibo lo deja a la vista.
- El certificado tributario muestra el interés completo como pagado.
