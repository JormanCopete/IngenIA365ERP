# Recaudo: manual del analista de recaudo (R0)

> Feature 015, entrega R0. Receta para **el analista de recaudo** (plantilla `recaudo.analista`): extractos, identificación de
> abonos, partidas por identificar, pagadurías y cheques devueltos. Historias US6, US7 y US15 de la
> [spec](../../specs/015-recaudo-tesoreria/spec.md).

Lo que entra al banco también es recaudo. El extracto lo carga Tesorería (es **uno solo** por cuenta y fecha) y Recaudo lo usa para
reconocer cada abono: lo aplica a las obligaciones de una persona, lo clasifica como partida por identificar o lo marca como de otro
origen (una remesa, un giro de pagaduría).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Los abonos de los extractos por identificar | `/recaudo/extractos` | `Receipts.BankItems.View` · `.Identify` · `.Classify` |
| Partidas por identificar: identificar, aplicar, devolver, pasar a ingreso | `/recaudo/partidas` | `Receipts.UnidentifiedItems.View` · `.Resolve` · `.MoveToIncome` |
| Pagadurías: envío del archivo de descuentos, respuesta y giro | `/recaudo/pagadurias` | `Receipts.Paymasters.*` |
| Cheques recibidos: consignación, canje, devolución | `/recaudo/cheques-recibidos` | `Receipts.Checks.View` · `.Manage` · `.Return` |

## 2. Identificar un abono

1. En `/recaudo/extractos` elija la cuenta y la fecha. Cada abono muestra su referencia y el consignante.
2. **Aplicar**: elija la persona y lo que paga, como en la caja; el recibo de banco (`RB`) sale con la fecha valor del movimiento.
3. **Clasificar como partida por identificar** cuando no se sabe de quién es: queda en `/recaudo/partidas` con su antigüedad.
4. La aplicación no interactiva (procesos) usa las reglas de modalidad del canal; si la matriz contable no tiene la regla, la línea
   queda para reproceso (`Receipts.Prevalidation.NotPostable`) en vez de bloquear.

## 3. Partidas por identificar

- Escalones de antigüedad en `Partidas.EscalonesDias` (30, 60, 90 días; **pendiente de la contadora**). La tarea alerta cada escalón.
- Una partida se identifica (total o parcial), se **devuelve al consignante** por orden de pago a Tesorería o, vencida y según
  `Partidas.DestinoVencido`, pasa a ingreso con aprobación.
- Un consignante con `Sarlaft.ConsignanteRepetido.Partidas` partidas abiertas (2, pendiente del oficial) levanta la señal al oficial de
  cumplimiento.
- La conciliación bancaria de Tesorería puede crear una partida desde un abono del extracto sin registro (origen «conciliación»).

## 4. Pagadurías (libranza)

Cartera arma cada período el archivo de descuentos de la pagaduría y lo entrega a Recaudo. El analista:

1. confirma el **envío** del archivo a la pagaduría;
2. carga la **respuesta** de la pagaduría (lo descontado y lo no aplicado con su motivo), que Recaudo devuelve a Cartera;
3. registra el **giro** cuando llega al banco y lo aplica: cada línea va al contrato con la fecha valor del descuento.

Lo descontado y no girado no causa mora al deudor mientras la pagaduría esté dentro de su umbral (regla de Cartera).

## 5. Cheques recibidos

Sólo si la cooperativa los admite (`Caja.AdmiteCheque`, por defecto **no**, P2). El cheque se consigna, pasa a canje y se marca pagado;
si el banco lo devuelve, regístrelo con su causal (`/recaudo/cheques-recibidos` → «Devolver»): las aplicaciones que pagó se reversan
y, si la causal cuenta para la lista de giradores, el girador queda marcado. El plazo de presentación (`Cheques.PlazoPresentacion*`)
se informa y no bloquea, salvo que la cooperativa active `Cheques.BloquearFueraDePlazo`.
