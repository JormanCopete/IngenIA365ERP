# Cartera: cobranza, aviso previo y reclamos sobre el reporte

> Feature 013 (cartera financiera), fase 18 (US12, entrega K4). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §14.3–§14.9,
> [contracts/adaptadores-y-archivos.md](../../specs/013-cartera-financiera/contracts/adaptadores-y-archivos.md) §4.5–§4.8 y §5 y
> [data-model.md](../../specs/013-cartera-financiera/data-model.md) §26. Aquellos son el contrato; esto es la receta para **el jefe de
> cartera, los gestores de cobranza y el oficial de cumplimiento**. El despliegue de K4 (la etapa migrada, SMS y WhatsApp) está en
> [docs/operaciones/cartera-despliegue-k4.md](../operaciones/cartera-despliegue-k4.md).

La cobranza ordena la cartera vencida en **etapas** por días de mora, la **reparte** entre gestores, deja en su **agenda** lo que toca
cada día y registra cada **gestión** (inmutable) con su **promesa**. Un **acuerdo de pago** reorganiza sólo lo vencido; el **cobro
jurídico** entrega el crédito a un abogado. Los **mensajes** al deudor —de la etapa, de una campaña, el aviso del gasto y el aviso
previo— salen siempre por la cola de mensajes al deudor, que respeta la **Ley 2300**. Ningún paso contacta a una referencia personal:
sólo al deudor, a un codeudor obligado o, para la gestión de libranza, a la pagaduría. Nada de esto escribe asientos: el gasto de
cobranza causado sale como hecho `GastoDeCobranzaCausado` (comprobante `CA`) y lo contabiliza la matriz.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Etapas vigentes (versión) y reglas de reparto | `/cartera/etapas-de-cobranza` | `Lending.Collections.Read` |
| Versionar las etapas | «Nueva versión» en la misma pantalla | `Lending.CollectionStages.Manage` |
| Crear o cerrar reglas de reparto; asignar a mano; repartir | pestaña «Reglas de reparto»; `/cartera/cobranza` («Asignaciones», «Repartir», «Asignar a mano») | `Lending.Collections.Assign` |
| Gastos por causar y causarlos | pestaña «Gastos por causar» de `/cartera/cobranza` | `Lending.Collections.Read` · `Lending.CollectionStages.Manage` |
| Agenda del día, promesas y gestiones; registrar y anular gestiones | `/cartera/agenda` | `Lending.Collections.Read` · `Lending.Collections.RecordActivity` |
| Acuerdos de pago: proponer, cancelar · aprobar, rechazar | `/cartera/acuerdos-de-pago` | `Lending.PaymentAgreements.Create` · `Lending.PaymentAgreements.Approve` |
| Cobro jurídico: abrir, actuaciones, reanudar, cerrar | `/cartera/juridico` | `Lending.Collections.Read` · `Lending.LegalProcesses.Manage` |
| Campañas | `/cartera/campanas` | `Lending.Campaigns.Manage` (también para verlas) |
| Avisos previos: ver · enviar · registrar la carta | pestaña «Avisos previos» de `/cartera/avisos-previos` | `Lending.PriorNotices.Read` · `.Send` · `.RegisterEvidence` |
| Reclamos sobre el reporte | pestaña «Reclamos sobre el reporte» de la misma pantalla | `Lending.CreditReportClaims.Read` · `.Manage` |
| Informes `collection-stages`, `promises`, `payment-agreements`, `prior-notices` | `/cartera/informes?vista=` | `Lending.Reports.View` (+ `Lending.Loans.Read` y `ExportPersonalData` para los datos personales) |

Plantillas de rol (bloque D, decisión del dueño del 2026-10-16): `cartera.gestor-cobranza` lee, registra gestiones, propone acuerdos,
lee, **envía** avisos previos y **registra la carta** (por eso sube y elige adjuntos: la guía escaneada; los soportes de novedades, castigo o
eventos del deudor le siguen vedados por permiso); `cartera.jefe` lee, asigna, reparte, versiona etapas, causa gastos y además **aprueba
acuerdos**, lleva el **cobro jurídico** y las **campañas** y envía y registra avisos previos; `cartera.oficial-cumplimiento` tramita los
reclamos; `cartera.administrador` trae todo `Lending.*`. Quien propone un acuerdo no lo aprueba: el gestor propone y el jefe aprueba, y si una
cooperativa le da los dos permisos a la misma persona, el comando rechaza aprobar lo propio (`Lending.Segregation.Conflict`). Cada
cooperativa ajusta sus roles en Seguridad › Roles. Las consultas se filtran en silencio por el alcance de oficina y línea; lo de fuera
responde el mismo 404 que lo inexistente.

## 2. Etapas

Una **versión de etapas** es el juego completo, vigente desde una fecha. Cada etapa tiene código (alfanumérico, hasta el largo del
catálogo), nombre, rango de días de mora (los dos extremos incluidos; la última puede ir sin tope), si es la **jurídica**, sus
**acciones sugeridas** (`Call`, `Message`, `Visit`, `PriorNotice`, `PaymentAgreement`, `LegalHandover`), sus **plantillas** de cobranza
y, opcional, su **regla de gasto** (§6). La **preventiva** admite días negativos: los que faltan para el próximo vencimiento, con signo
menos.

«Nueva versión» (con motivo) valida que las etapas cubran un rango continuo: sin cruces, sin huecos, sólo la última sin tope y el orden
siguiendo a los días (`Lending.CollectionStage.RangesInvalid`, con `data.kind` = `Overlap`, `Gap`, `OpenNotLast`, `Inverted`,
`OrderMismatch`, `DuplicateCode` o `Empty`); cada regla de gasto válida (`.FeeRuleInvalid`); cada plantilla, una de cobranza de Cartera
que exista (`Lending.MessageTemplate.NotFound`); y que empiece después de la vigente (`.ValidityInvalid`). La versión anterior se cierra
la víspera; nada se borra.

La API siembra al arrancar cuatro etapas **«pendiente de validar»** (la pantalla las marca así) que la cooperativa debe revisar (§11):

| Código | Nombre | Días | Acciones | Plantilla |
|---|---|---|---|---|
| `PREVENTIVA` | Preventiva | −5 a 0 | mensaje | `RECORDPREV` |
| `ADMIN` | Administrativa | 1 a 30 | llamar, mensaje, aviso previo, acuerdo | `MORAADMIN` |
| `PREJURID` | Prejurídica | 31 a 90 | llamar, visita, mensaje, acuerdo | `PREJURID` |
| `JURIDICA` | Jurídica (jurídica) | 91 en adelante | entrega a abogado | — |

**El ciclo diario** (paso 7, desde K4) mide cada crédito vivo y lo hace **entrar** a la etapa que le toca cuando cambia de etapa o
vuelve a la misma después de haber salido de su rango; queda una entrada en la historia (`Source = DailyCycle`, con la corrida). Al
entrar, encola el mensaje de cada plantilla de la etapa, abre el aviso previo si la etapa tiene la acción `PriorNotice` y el crédito está
en mora (§9) y propone el gasto si la etapa tiene regla (§6). No entra dos veces el mismo día. Un crédito con un **acuerdo aprobado al
día no avanza**; el día en que el acuerdo se incumple vuelve a la etapa que le corresponde (motivo «Acuerdo de pago incumplido»).
La cobranza no toca al deudor fallecido con reclamación al seguro en trámite ni al que está en negociación de insolvencia.

La etapa vigente de un crédito es **su última entrada**, salvo que sea una **salida** (bloque D, decisión del dueño del 2026-10-16):

- **Cuándo sale**: el ciclo encuentra un crédito con etapa vigente que **ya no está en mora** (días de mora en cero) y al que, por los días que
  faltan para su próximo vencimiento, **no le toca ninguna etapa** —es decir, ninguna preventiva—. Si le toca la preventiva (en la semilla,
  faltan de 0 a 5 días), no sale: pasa a la preventiva como cualquier cambio de etapa. Un crédito en la preventiva que paga a tiempo y deja de
  estar en su rango también sale.
- **Qué deja**: una entrada `Source = Exit` con la etapa de la que sale y el motivo «Salida por pago al día»; una **gestión de sistema**
  (canal «Sistema», resultado «Salió de cobranza (al día)», sin gestor ni contacto directo, con la observación «Sale de cobranza: se puso al
  día el dd/mm/aaaa»), que no cuenta en la ventana legal y no se anula (`Lending.CollectionAction.SystemRecord`); y el **cierre de la
  asignación** del gestor (una fila sin gestor).
- **Qué cambia**: el crédito queda **fuera de cobranza**: no tiene etapa ni gestor vigentes, sale de la agenda, de las campañas filtradas por
  etapa y del informe `collection-stages`, y no admite gestiones con el titular (`Lending.Collections.LoanNotInCollection`).
- **Si recae** en mora, entra a la etapa que le toque como **episodio nuevo** (la entrada enlaza la salida) y se reparte de nuevo por las reglas.

No sale el crédito que la cobranza no toca (fallecido con reclamación en trámite, insolvencia en negociación).

## 3. Reparto y agenda

Una **regla de reparto** (pestaña «Reglas de reparto», con motivo) tiene prioridad, criterios (oficina, etapa, línea, pagaduría, rango
de saldo; vacío = cualquiera), forma (**por número** o **por saldo**), si asigna **al deudor completo** y sus gestores con su peso
(`Lending.CollectorAssignmentRule.Invalid` si no hay gestores, se repiten, un peso no es positivo o el rango está invertido; un gestor que
no es usuario activo responde `Lending.Collections.CollectorNotFound`). Gana la primera regla que casa por prioridad; el resultado es
reproducible y quien ya tiene un gestor de la regla lo conserva. «Cerrar vigencia…» sólo cambia el fin de la regla (`.ValidityInvalid`
si ya estaba cerrada); sus asignaciones quedan.

- **El ciclo** reparte con las reglas vigentes a los créditos que entraron a una etapa ese día.
- **Repartir** (`/cartera/cobranza`): filtros, gestores y forma; «Vista previa» no escribe nada; «Aplicar el reparto» escribe una
  asignación por crédito que cambia de gestor, con el motivo.
- **Asignar a mano**: uno o varios créditos a un gestor, con motivo; la que ya es de ese gestor no se repite.

Cada asignación es un hecho con la anterior enlazada: la historia sólo crece. Cuando el crédito sale de cobranza, el ciclo escribe el
**cierre** de su asignación (una fila sin gestor, motivo «Salida por pago al día»; en «Asignaciones» se ve «Sin gestor · Cierre: salió de
cobranza»).

La **agenda** (`/cartera/agenda`, «Agenda») muestra al gestor —o a quien elija quien tiene `Collections.Read`— sobre sus créditos
asignados: promesas que vencen ese día, promesas incumplidas que esperan una gestión nueva («vuelve a la agenda»), cuotas de acuerdos
que vencen y créditos que entraron a una etapa. Es una consulta: no guarda nada.

## 4. Gestiones y la Ley 2300

«Registrar gestión» (o «Gestionar…» desde la agenda): el crédito, el instante en que ocurrió (no futuro), el canal (teléfono, en
persona, correo, SMS, WhatsApp, carta, visita), con quién (la pantalla sólo ofrece al deudor y a los codeudores obligados; nunca una
referencia), si hubo **contacto directo**, el resultado y notas. Antes de guardar muestra lo que dice la **ventana de contacto**.

- La persona contactada es el deudor, un codeudor vigente o la **pagaduría** del crédito (gestión de libranza, nunca como contacto
  directo); cualquier otra responde `Lending.CollectionAction.ContactNotObligated`.
- La gestión con el titular es de un crédito **en cobranza** (con etapa vigente): uno que nunca entró o que salió al ponerse al día responde
  `Lending.Collections.LoanNotInCollection` (bloque D). La gestión ante la pagaduría no depende de la etapa. Los canales y resultados de
  sistema no se registran a mano.
- La ventana juzga la gestión **en el instante en que ocurrió**: fuera de horario, domingo o festivo nacional, sobre el tope del día
  (`Cobranza.ContactosPorDia`) o por otro canal que el de la semana se rechaza con los códigos de los mensajes
  (`Lending.DebtorMessage.OutsideLegalWindow`, `.WeeklyChannelUsed`, `.ChannelNotAuthorized`). La decisión queda **sellada** en la
  gestión. La de la pagaduría no pasa por la ventana.
- **Un contacto directo fija el canal de la semana** del titular, igual que un mensaje de cobranza que salió: con un teléfono el lunes,
  un WhatsApp de cobranza el miércoles queda diferido a la semana siguiente (`WeeklyChannelLocked`). Una gestión sin contacto directo
  (no contestó, número errado) no cuenta.
- **Anular** («Anular…», con motivo) no borra: escribe otra gestión que anula a la primera. La anulada deja de contar para la ventana y
  su promesa queda anulada. No se anula dos veces (`Lending.CollectionAction.AlreadyVoided`).

El horario, el sábado y el tope son parámetros de la cooperativa que sólo pueden ser más estrictos que el techo legal
(`Cobranza.HorarioLunesAViernes`, `Cobranza.HorarioSabado`, `Cobranza.ContactosPorDia`).

## 5. Promesas y acuerdos de pago

**Promesa**: una gestión con resultado «promesa de pago» lleva fecha (no anterior a la gestión) y valor. Al registrarla se sella la
tolerancia vigente `Cobranza.ToleranciaDePromesa` (pesos que se perdonan; 0 por defecto). La promesa no guarda estado, se calcula:
**cumplida** cuando los pagos vigentes registrados después de ella con fecha valor hasta la prometida suman lo prometido menos la
tolerancia; **incumplida** si pasó la fecha sin eso (el ciclo del día siguiente avisa `Cartera.PromesaIncumplida` y vuelve a la agenda
del gestor); **pendiente** si no; anulada si su gestión se anuló. Pestaña «Promesas» de la agenda.

**Acuerdo de pago** («Nuevo acuerdo», con motivo): un calendario propio **sólo sobre lo vencido** de un crédito activo, sin cambiar sus
condiciones. Si el calendario suma más de lo vencido, o no hay nada vencido, se responde `Lending.PaymentAgreement.ChangesTerms`: cambiar
plazo, cuota o tasa es una **novedad**. El calendario empieza hoy o después, crece y cada cuota es positiva (`.ScheduleInvalid`); un
crédito tiene a lo sumo uno propuesto o vigente (`.AlreadyOpen`). Nace **propuesto** con su número.

- **Aprobar** o **rechazar** (con motivo) lo hace **otra persona** que quien lo propuso (`Lending.Segregation.Conflict`); sólo uno
  propuesto (`.NotProposed`). **Cancelar** (con motivo) uno propuesto o aprobado; uno terminado responde `.InvalidStatus`.
- Aprobado, su cumplimiento se calcula con los pagos vigentes desde la aprobación: **al día**, **cumplido** o **incumplido** cuando una
  cuota vencida no quedó cubierta a su vencimiento. Incumplir es un hecho de ese día: no se deshace aunque luego pague.

## 6. Gastos de cobranza: informar antes de causar

El gasto de cobranza nunca se cobra sin avisarle primero al deudor. Tiene dos orígenes:

1. **La regla de la etapa** (`feeRule`): base (`PastDueAmount`, todo lo vencido, o `PastDuePrincipal`, el capital vencido), tarifa
   como fracción entre 0 y 1, tope en pesos opcional y momento (`OnStageEntry`, al entrar a la etapa). El gasto es base × tarifa al
   centavo, sin pasar del tope de la regla ni del **tope de la cooperativa** (abajo).
2. **Un honorario o gasto del cobro jurídico** marcado «se cobra al deudor» (§7).

**Tope del gasto de la cooperativa** (bloque D, decisión del dueño del 2026-10-16; parámetros con vigencia en `/cartera/parametros`, sin
valor sembrado = sin tope, «pendiente de validar»):

- `Cobranza.TopeGasto.Porcentaje`: fracción de 0 a 1 **de la base del gasto** (lo vencido o el capital vencido, según la regla). Ninguna
  versión de etapas acepta una regla con una tarifa mayor (`Lending.CollectionStage.FeeAboveCap`, con la tarifa y el tope).
- `Cobranza.TopeGasto.ValorMaximo`: pesos por gasto.
- Con los dos rige **el menor**: el gasto propuesto por la etapa se recorta a él. Un honorario o gasto jurídico **a cargo del deudor** por encima
  del tope (base: lo vencido de la última posición diaria del crédito) se rechaza diciendo el máximo (`Lending.CollectionFee.AboveCap`): se
  registra a cargo del deudor a lo sumo ese valor y el resto sin cobrárselo. No se recorta en silencio porque el valor lo puso una persona.

El ciclo (o la actuación) **sólo propone e informa**: encola el aviso `CollectionFeeNotice` con la plantilla `GASTOCOBR` por el primer
canal que la ley y el titular permiten. Ese aviso es de cobranza: respeta el horario, el tope y el canal de la semana. **Lo causa una
persona** en «Gastos por causar» de `/cartera/cobranza`: la lista muestra cada gasto con su valor, etapa, origen, el estado del aviso y si
ya se puede causar. «Causar» toma los avisos que ya están **enviados o entregados** (fuera de producción también el simulado); uno que no
salió responde `Lending.CollectionFee.NoticeNotSent`, uno ya causado `.AlreadyCaused`. Cada gasto pasa por la validación previa contable
(`Lending.Prevalidation.NotPostable` si falta la regla y `Contabilidad.FaltaDeRegla` está en «Bloquear»), escribe la operación `Charge`
(comprobante `GCO-`, una por aviso) y emite `GastoDeCobranzaCausado` (`CA`); el honorario jurídico va como cuenta por pagar al abogado.
El valor que se causa es el que se informó.

## 7. Cobro jurídico

«Abrir proceso»: el crédito, el abogado (una persona del maestro, distinta del demandado: `Lending.LegalProcess.LawyerIsDebtor`), el
despacho, el radicado y la fecha. Sólo un crédito vivo **en una etapa jurídica** (`Lending.LegalProcess.LoanNotInLegalStage`) y sin otro
proceso abierto (`.AlreadyOpen`). Si el deudor está en negociación de insolvencia, el proceso **nace suspendido**; una insolvencia que se
abre después suspende los procesos abiertos del deudor.

**Reanudar** (bloque D, decisión del dueño del 2026-10-16): «Reanudar…», con motivo, en un proceso suspendido
(`POST /api/lending/legal-processes/{id}/resume`, `Lending.LegalProcesses.Manage`). **Nunca se reanuda solo** al terminar la insolvencia: lo
decide una persona. Mientras la insolvencia que lo suspendió —o cualquier otra del deudor— siga en negociación responde
`Lending.LegalProcess.InsolvencyNegotiating`; cuando terminó (con acuerdo o con su fracaso registrado), el proceso vuelve **al estado que tenía
antes de suspenderse**, que es el que dicen sus actuaciones con la misma regla que lo mueve: sin radicación, **asignado**; radicado sin
actuación de fondo después, **radicado**; con alguna, **en curso**. Suspendido no recibe actuaciones que lo muevan, así que el estado que
vuelve es el que tenía. Uno que no está suspendido responde `.InvalidStatus`.

«Actuación…»: fecha, clase (radicación, auto, embargo, audiencia, sentencia, honorario, gasto, cambio de estado, otra), descripción y,
si es honorario o gasto, el valor y si se cobra al deudor. Los soportes se suben como documentos del proceso
(`LendingLegalProcessDocument`, no se borran; otro responde `.AttachmentInvalid`). La radicación pasa el proceso de **asignado** a
**radicado**; la primera actuación de fondo de un radicado lo pone **en curso**. Un honorario o gasto cobrable sigue la regla del §6.
«Cerrar…» con motivo: **concluido** o **retirado**; uno cerrado no recibe actuaciones (`.InvalidStatus`).

## 8. Campañas

«Nueva campaña»: código propio (único, `Lending.Campaign.CodeDuplicate`), nombre, la plantilla vigente del canal y su propósito
(cobranza o informativo), criterios (etapa, oficina, línea, días de mora mínimos, pagaduría) y el instante.

- Los criterios tienen que poder casar con algo: una etapa que no existe en la versión vigente el día programado, una mora mínima por encima
  del tope de la etapa elegida o una oficina, línea o pagaduría inexistentes responden `Lending.Campaign.FiltersInvalid` con el motivo.
- **Vista previa** (no escribe): los créditos del alcance que casan y, por cada deudor, si el mensaje sale, se difiere o queda
  **excluido** con su motivo (`ChannelNotAuthorized`, fallecido, insolvencia, cobro suspendido…).
- **Programar**: un mensaje por crédito al deudor, con la campaña. Cada uno nace en cola, **diferido** al primer instante permitido (un
  domingo pasa al lunes a la primera hora) o **excluido** con su evento y motivo: esa es la constancia de por qué no se le escribió. La
  campaña queda programada con los conteos; el ciclo la pasa a «en curso» cuando sale el primero y a «terminada» cuando no queda nada en
  cola.
- **Cancelar** (con motivo): lo que sigue en cola o diferido queda cancelado; lo que ya salió no se toca.

## 9. Aviso previo al reporte negativo (Leyes 1266 y 2157)

El dato negativo de un **episodio de mora** no va a las centrales sin un aviso previo con **constancia**. El episodio empieza en el
vencimiento de la primera cuota impaga y termina el primer día sin mora; **recaer es un episodio nuevo** y necesita un aviso nuevo.

- **Abrir**: lo abre el ciclo al entrar a una etapa con la acción «aviso previo» (en las semillas, la administrativa), uno por crédito,
  deudor y episodio (y uno por codeudor si `AvisoPrevio.IncluirCodeudores` lo pide, ver abajo). Sella los días mínimos (`AvisoPrevio.DiasMinimos`, 20 por defecto, nunca menos que el piso legal) y el umbral
  (`AvisoPrevio.UmbralDosComunicaciones`, en veces el SMMLV; vacío = el límite legal): **con saldo igual o menor al umbral se exigen dos
  comunicaciones en días distintos**; por encima, una.
- **Enviar**: el cierre del ciclo encola la primera comunicación y, la segunda, en otro día y cuando la primera ya salió (plantilla
  `AVISOPREV`). A mano, «Enviar…» por correo, SMS o WhatsApp es un envío **inmediato**: fuera de la ventana se rechaza con los códigos
  de los mensajes. Un aviso completo responde `Lending.PriorNotice.AlreadyEvidenced`; el de un episodio cerrado, `.EpisodeClosed`.
- **Carta**: «Registrar carta…» con el número de comunicación, el instante de envío, la transportadora, la guía y la guía escaneada
  subida como constancia del aviso (`LendingPriorNoticeEvidence`, no se borra; otra responde `.AttachmentInvalid`). Una comunicación
  ya registrada responde `.EvidenceDuplicate`.
- **Estado** (calculado): **pendiente** sin constancias, **en curso** con alguna, **válido** con las exigidas, **cerrado** si el
  episodio terminó. Fecha más temprana de reporte = día de la última constancia exigida + días mínimos. **En producción un envío por
  `SIMULADO` no es constancia.** Lo migrado cuenta: las fechas de envío de la carga son constancias y un reporte negativo vigente desde
  la migración deja el aviso válido.
- Si pasan los días mínimos desde que se abrió y sigue sin constancia, el ciclo levanta `Cartera.AvisoPrevioSinConstancia`: registre la
  carta o revise por qué no salió el mensaje (por ejemplo, el deudor no autorizó ningún canal).

### Aviso a los codeudores (decisión del dueño del 2026-10-16)

El parámetro **`AvisoPrevio.IncluirCodeudores`** (sí/no, con vigencia, `false` por defecto) decide si el aviso previo también se da a los
codeudores:

- **`false`** (el defecto): sólo el deudor recibe aviso. Los codeudores **no se reportan con dato negativo** a las centrales: en el archivo de
  la central quedan excluidos con `NoPriorNoticeEvidence`, sin alerta (no hay gestión que hacer). Al día sí se reportan, con su propia
  autorización.
- **`true`**: cuando el ciclo abre el aviso del episodio para el deudor, abre también **uno por cada codeudor vigente** ese día, con su propio
  número de comunicaciones exigidas, su propia constancia y su propio estado; la cobranza le encola su comunicación como al deudor, y la carta
  se registra igual (la guía va al aviso del codeudor). La central reporta al codeudor con dato negativo **sólo si su aviso está completo**
  desde su fecha más temprana; si no, queda excluido con `NoPriorNoticeEvidence` y la alerta `Cartera.AvisoPrevioSinConstancia`.
- Se lee vigente el día en que el ciclo abre el aviso (en la cobranza) y vigente al corte (en el archivo de la central). Un episodio que ya
  tenía el aviso del deudor cuando se activó el parámetro recibe los de sus codeudores la próxima vez que el crédito entre a una etapa con la
  acción «aviso previo»; mientras tanto, se pueden registrar a mano sus cartas sobre un aviso abierto.

### Qué del aviso previo es configurable

| Qué | Dónde | Defecto |
|---|---|---|
| Días mínimos entre la constancia y el reporte | `AvisoPrevio.DiasMinimos` (nunca menos que el límite legal `AVISO_PREVIO_DIAS`) | 20 |
| Saldo hasta el cual se exigen dos comunicaciones | `AvisoPrevio.UmbralDosComunicaciones`, en veces el SMMLV (vacío = el límite legal) | el límite legal |
| Avisar también a los codeudores | `AvisoPrevio.IncluirCodeudores` | `false` |
| En qué etapa se abre el aviso | la acción «aviso previo» de la etapa (`/cartera/etapas-de-cobranza`) | la semilla la pone en una etapa |
| Texto de la comunicación | la plantilla `AVISOPREV` por canal | la de la semilla |
| Canal | el que el titular autorizó y la ventana de la Ley 2300 permite (horario y contactos por día: `Cobranza.Horario*`, `Cobranza.ContactosPorDia`) | — |
| Desde qué mora el dato es negativo (y por tanto exige aviso) en el archivo de la central | `Centrales.RangosDeMora`, por tipo de cartera (runbook del SICSES y las centrales) | toda mora |

Lo que **no** es configurable: el aviso es por episodio de mora (recaer es otro aviso), en producción un envío `SIMULADO` no es constancia, y
la constancia de una carta exige la guía adjunta.

## 10. Reclamos sobre el reporte

Pestaña «Reclamos sobre el reporte»: «Registrar reclamo» de una persona (con o sin crédito; sin crédito sólo con todas las oficinas), la
clase (reclamo o suplantación), la fecha de recibo, el medio, la descripción y los soportes (`LendingCreditReportClaimSupport` de la
persona). Vence en los días hábiles nacionales del reclamo contados desde el día siguiente al recibo (límites `TITULAR_*`; ningún plazo
está en el código) y nace **en trámite**: desde ahí el reporte lleva la marca «reclamo en trámite».

- **Prorrogar** (con motivo) **una sola vez** (`Lending.CreditReportClaim.AlreadyExtended`); **responder** con el resumen, si se le da
  la razón y el soporte; **desistir** (con motivo) a pedido del titular. Fuera de trámite responde `.NotInProcess`.
- **Aceptación tácita**: al cerrar el ciclo, un reclamo vencido (el vencimiento prorrogado, si lo hay) sin respuesta pasa a
  **aceptado tácitamente** y levanta `Cartera.PlazoRegulatorioPorVencer`: corrija el reporte a favor del titular.

## 11. Pendiente de validar (dueño y contadora)

- **Etapas por defecto** (T0962): Preventiva −5 a 0, Administrativa 1 a 30, Prejurídica 31 a 90 y Jurídica 91 en adelante, con sus
  acciones y plantillas, sembradas «pendiente de validar». La cooperativa las confirma o versiona en `/cartera/etapas-de-cobranza`.
- **Reglas de gasto** (T0962): la semilla no trae ninguna; cada cooperativa define base, tarifa, tope y momento por etapa.
- **`Cobranza.ToleranciaDePromesa`** (T0962): 0 pesos por defecto, sembrado «pendiente de validar».
- **Tope del gasto de cobranza** (bloque D): ya es configurable por la cooperativa (`Cobranza.TopeGasto.Porcentaje` y `.ValorMaximo`, §6),
  sin valor sembrado. Falta que el dueño y la contadora fijen el valor de cada cooperativa; el catálogo de límites legales sigue sin un techo
  nacional y no se inventa.
- **Aviso a los codeudores** (2026-10-16): `AvisoPrevio.IncluirCodeudores` arranca en `false`; el dueño confirma si su política es avisarles
  (y reportarlos) o no.
- **SMS y WhatsApp reales**: pendientes de proveedor. Hoy esos canales salen por `SIMULADO`, que en producción **no** es constancia del
  aviso previo; mientras tanto, el aviso previo con efecto legal en producción sale por correo o por carta con guía. Ver el runbook.
- **Permisos de las plantillas de rol** (resuelto en el bloque D): el jefe trae aprobar acuerdos, el cobro jurídico, las campañas y los avisos
  previos; el gestor, enviar y registrar avisos previos (y subir la guía). La alerta `Cartera.AvisoPrevioSinConstancia` va a quien registra la
  constancia. Una cooperativa que ya creó sus roles desde las plantillas **no** los recibe sola: los agrega en Seguridad › Roles.
