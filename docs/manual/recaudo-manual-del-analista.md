# Recaudo: manual del analista de recaudo (R0, R1 y R2)

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

## 6. Lo que trae R1

- **Reglas de identificación** (`/recaudo/reglas-de-identificacion`): por prioridad, por referencia de pago (el número del crédito o de la
  cuenta), documento del consignante, consignación reportada o valor exacto de lo exigible. Al elegir un abono en `/recaudo/extractos`
  aparece la propuesta: «Usar la propuesta» la pone en las partes y usted confirma. Sólo la regla por referencia, marcada «aplicar al
  cargar» y con `Identificacion.AplicarAlCargar` encendido, aplica al cargar el extracto lo de referencia exacta, a su nombre. Ninguna
  tarea aplica sola.
- **Consignación reportada**: si el abono es una consignación que el asociado reportó en ventanilla, «Confirmar con la consignación
  reportada» la cruza; si ya se había aplicado de inmediato, la línea queda identificada con el mismo recibo, sin aplicar dos veces.
- **Identificación tardía**: cuando Cartera y Captaciones adoptan la versión 2 de sus contratos (`/recaudo/activacion`), el abono que se
  identifica tarde entra con la fecha valor del movimiento y cita su partida.
- **Calendario de pagadurías** (`/recaudo/pagadurias`): registre la fecha de pago de la nómina que informa cada pagaduría; el sistema
  calcula la fecha legal de giro (`Libranza.DiasHabilesGiro`, días hábiles) y marca las morosas. Es informativo.
- **Reporte de transacciones** (`/recaudo/cumplimiento`, oficial de cumplimiento): genere el del mes; con una inconsistencia bloqueante
  queda validado sin archivo; regenerar deja la versión anterior reemplazada; marque cargado con el radicado. Cada generación advierte
  que la estructura está sin cotejar hasta que el oficial la revise.
  Adjunte el comprobante del cargue con «Adjuntar comprobante» antes de marcarlo cargado (R2).

## 7. Lo que trae R2: canje, convenios y pasarelas

- **Canje de cheques** (`/recaudo/cheques-recibidos` → «Canje de cheques»): arme el lote de canje de los cheques que salen en la remesa del
  día a una cuenta propia; cuando llega el abono del extracto que los paga, «Cruzar con el extracto» los marca pagados (el lote entero o
  los elegidos, con el valor exacto). El devuelto sigue la devolución de siempre.
- **Convenios de recaudo** (`/recaudo/convenios`): un convenio por banco, corresponsal o pasarela, con su cuenta recaudadora, la estructura
  de la referencia (por producto o por persona, longitud y dígito de verificación) y el formato del archivo diario (Maestros → Formatos
  bancarios, ámbito «archivo de convenio» o «archivo de pasarela»). La comisión pactada es informativa. Si cambia la referencia, el formato
  o la comisión, nace una versión nueva desde la fecha indicada; nunca es retroactiva. En la misma pantalla se emite la **referencia de
  pago** de un producto: el número del crédito o de la cuenta con su dígito (`Referencias.DigitoDeVerificacion`), la misma que imprimen el
  plan de pagos y el extracto.
- **Archivo del convenio o de la pasarela** (`/recaudo/archivos-de-convenio`): elija el convenio, adjunte el archivo y «Revisar»: verá qué
  se aplicaría, qué iría a partidas (referencia inválida, dígito que no corresponde o referencia desconocida), qué no salió aprobado en la
  pasarela y qué pagos ya habían llegado en otro archivo. «Cargar y aplicar» aplica cada pago a su nombre con un recibo de convenio (o en
  línea) y deja lo demás como partida por identificar con su motivo. El mismo archivo no se carga dos veces. Si el aliado informa cuántos
  pagos y cuánto trae el archivo, escríbalo: si no cuadra, el archivo no se carga.
- **Conciliar el archivo** («Conciliar»): elija el abono del extracto de la cuenta recaudadora que liquidó el archivo y escriba la comisión,
  su IVA y las retenciones que descontó el aliado. Lo que quede sin explicar por encima de `Convenios.ToleranciaDeConciliacion` lo aprueba
  otra persona en «Conciliaciones por aprobar». El abono queda identificado como liquidación del convenio.
- **Pago de una aseguradora**: al identificar un abono de la reclamación de un seguro de crédito, elija a la aseguradora en
  «Aseguradora que paga»: queda como pagadora y el pago se aplica al crédito del deudor por el contrato de Cartera.
- **Soportes**: la firma de quien recibe un egreso (`/recaudo/egresos`), el soporte de una consignación reportada
  (`/recaudo/consignaciones-reportadas`) y el comprobante del cargue a la UIAF (`/recaudo/cumplimiento`) se adjuntan en su pantalla con su
  propio tipo de documento; no se borran.
- **Informes** (`/recaudo/informes`): «Recaudo por canal» (cuadra con los recibos vigentes) y «Convenios de recaudo» (cada archivo con lo
  informado, lo aplicado, lo que fue a partidas y su conciliación).
