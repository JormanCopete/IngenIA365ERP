# Cartera: novedades, condonaciones y eventos del deudor

> Feature 013 (cartera financiera), fase 16 (US11, entrega K3). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §11.1, §11.2, §11.4 y §17,
> [contracts/mensajes.md](../../specs/013-cartera-financiera/contracts/mensajes.md) §10.20–§10.21 y
> [contracts/contabilidad.md](../../specs/013-cartera-financiera/contracts/contabilidad.md) §2.1. Aquellos son el contrato;
> esto es la receta para **el jefe de cartera**, quien decide y quien aplica.

Una **novedad** cambia las condiciones de un crédito vivo: más plazo, otra fecha de pago, otra tasa, una novación, una
refinanciación (que puede recoger otros créditos del deudor) o el cambio de oficina. Una **condonación** perdona parte de
lo que el deudor debe. Los **eventos del deudor** (fallecimiento, retiro de la cooperativa, embargo) y la **insolvencia**
suspenden lo que deben suspender. Nada de esto escribe asientos: Cartera emite sus hechos y Contabilidad los contabiliza
por la matriz (comprobante `CN` para las novedades).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Novedades: lista, pedir, simular, consentimientos, radicar, anular | `/cartera/novedades` (y el botón «Novedad» del crédito) | `Lending.Novelties.Read` · `.Request` |
| Decidir una novedad | detalle en `/cartera/novedades` o «Mis pendientes» (`/cartera/aprobaciones`) | `Lending.Novelties.Read` + el permiso del nivel (`Lending.Decisions.*`) |
| Aprobar la excepción de cobertura (otra persona, antes de aplicar) | detalle en `/cartera/novedades` | `Lending.Novelties.Read` + el nivel de excepción (`Lending.Decisions.Exceptions`) |
| Aplicar una novedad aprobada | detalle en `/cartera/novedades` | `Lending.Novelties.Apply` |
| Cambiar el plazo de un crédito sin pagos ni causación (no es una novedad, §3a) | botón «Cambiar el plazo» de `/cartera/creditos/{id}` | `Lending.Loans.ChangeTerm` |
| Levantar el bloqueo de una insolvencia fracasada | `/cartera/cupos` | `Lending.Blocks.Manage` |
| Condonaciones: lista, pedir, anular | `/cartera/condonaciones` (y el botón «Condonar» del crédito) | `Lending.Loans.Read` · `Lending.Condonations.Request` |
| Decidir y aplicar una condonación | `/cartera/condonaciones` o «Mis pendientes» | `Lending.Loans.Read` + nivel · `Lending.Condonations.Apply` |
| Eventos del deudor y reclamaciones al seguro | pestaña «Eventos y reclamaciones» de `/cartera/creditos/{id}` | `Lending.Loans.Read` · `Lending.DebtorEvents.Manage` |
| Insolvencia | botón «Insolvencia» de la misma pestaña | `Lending.Insolvency.Manage` |
| Informe de reestructurados | `/cartera/informes?vista=restructured` | `Lending.Reports.View` + `Lending.Loans.Read` |

`Novelties.Request` lo trae `cartera.jefe`; `Novelties.Read`, también `cartera.analista` y `cartera.gerente`, que deciden
novedades (desde el 2026-10-15; antes hacía falta un rol propio).

## 2. La causa y el mecanismo

El **mecanismo** lo elige quien pide la novedad; la **causa** la calcula el sistema y queda explicada en la novedad:

- **Reestructuración** si la motiva el deterioro de la capacidad de pago, cualquiera sea el mecanismo: el crédito está en
  mora, el deudor la pide «por dificultades de pago», es el acuerdo de un proceso de insolvencia o la refinanciación
  recoge un crédito en mora.
- **Modificación** si la pide el deudor sin dificultades y su mora reciente no pasa del límite del Anexo 1 del SARC para su
  segmento (meses de observación y días, en la norma vigente). Si pasa, la petición se rechaza con
  `Lending.Novelty.ModificationNotAllowed` y los días que tuvo frente al límite.

Una reestructuración marca el crédito, suma una al conteo y nunca mejora la calificación al aplicarse (la mejora llega en
los cierres siguientes, por tramos de cuotas pagadas seguidas; si recae, vuelve).

## 3. Pedir y simular

1. «Nueva novedad»: el crédito (o los créditos), el mecanismo, por qué la pide el deudor y las condiciones nuevas
   (cuotas del plan nuevo, tasa, día de pago, si capitaliza el interés que se debe). En una refinanciación los créditos
   adicionales que marque se **recogen**.
2. Queda en **borrador** con su causa y un consentimiento pendiente por cada codeudor, avalista y propietario de una
   garantía del crédito.
3. «Simular» muestra el plan nuevo desde hoy, el capital (saldo + recogido + capitalizado), la calificación hoy y después,
   el conteo, la usura y la validación previa contable. No escribe nada. Los costos de la reestructuración nunca entran al
   plan del deudor.

## 3a. Cambiar el plazo de un crédito sin pagos ni causación (no es una novedad)

Desde el 2026-11-24 (pedido del dueño), el botón **«Cambiar el plazo»** del crédito (`/cartera/creditos/{id}`, permiso
`Lending.Loans.ChangeTerm`) corrige el número de cuotas de un crédito que **todavía no tiene pagos ni causación**. El caso
típico: el crédito que nació hace minutos de una factura a crédito (la afiliación) con las cuotas equivocadas. No se radica,
no se decide ni se aplica; no es modificación ni reestructuración y no suma al conteo: el crédito queda como si hubiera
nacido con las cuotas nuevas.

1. «Cambiar el plazo» → **«Cuotas nuevas»** y, si se quiere, **«Primer vencimiento»**. Sin primer vencimiento se conserva
   el del plan de hoy (el día de pago no se corre); con otro, las demás cuotas vencen ese mismo día de cada período.
2. **«Simular»** muestra el plazo y la cuota de hoy, los nuevos («Plazo actual», «Cuota actual», «Plazo nuevo», «Cuota
   nueva») y el plan nuevo completo. No escribe. Cambiar las cuotas o la fecha borra la simulación: hay que volver a
   simular.
3. Con el **«Motivo»**, **«Cambiar el plazo»** lo registra: una operación `TermsChange` numerada `CPZ-` con fecha valor = la
   del desembolso, una versión nueva de las condiciones (causa «Cambio de plazo») y una versión nueva del plan. Las
   anteriores quedan como historia (pestaña «Plan de pagos», selector «Versión del plan»; Principio XI). La auditoría
   guarda el plazo, la cuota y el primer vencimiento de antes y de después y el motivo.

Sirve para **acortar** y para **alargar**, y se puede repetir mientras el crédito siga sin pagos ni causación. Lo admite un
crédito:

- activo, no migrado, en pesos (no en UVR), sin período de gracia ni cuotas extraordinarias pactadas y que no sea de pago
  único (la regeneración no las conserva);
- cuyas únicas operaciones sean su desembolso (o la venta a crédito), cambios de plazo anteriores y cambios de la
  preferencia de abono. Un pago, un cobro provisional (el anticipo de una venta), un reverso o una resolución de saldo a
  favor lo impiden, y también un cargo, una condonación, una novedad, un ajuste de Comercial o un reajuste de tasa;
- sin causación registrada (ni el reconocimiento al inicio del ciclo de la feature 024);
- nacido en un mes que no se ha cerrado;
- dentro de los rangos de cuotas y de meses de la versión de la línea con que nació, y bajo la usura vigente a la fecha
  del desembolso.

| Mensaje (código) | Qué pasó |
|---|---|
| «El crédito ya tiene pagos aplicados (o anticipos)…» (`Lending.Loan.TermChange.HasPayments`) | Hubo un pago o un cobro provisional: use una novedad de prórroga. |
| «El crédito ya tiene causación registrada…» (`…HasAccrual`) | El ciclo diario ya causó: use una novedad. |
| «El crédito ya tiene otras operaciones (…)…» (`…HasOperations`) | Cargo, condonación, novedad, ajuste de Comercial, reajuste… |
| «El crédito se cargó en la migración…» (`…Migrated`) | Lo migrado trae su plan y su historia. |
| «El crédito tiene período de gracia pactado / cuotas extraordinarias / su capital en UVR…» (`…SpecialTerms`) | La regeneración no los conserva. |
| «El crédito nació el …, en un mes que ya se cerró…» (`…ClosedMonth`) | No se toca un mes cerrado. |
| «La línea del crédito admite de A a B cuotas…» o «El plazo (N meses) tiene que estar entre…» (`…OutOfLineRange`) | Fuera de la línea con que nació. |
| «El crédito ya tiene N cuotas con ese primer vencimiento…» (`…Unchanged`) | No hay nada que cambiar. |
| «El primer vencimiento va después del desembolso / de hoy» (`…FirstDueDateInvalid`) | Fecha imposible. |
| «La cooperativa no permite cambiar el plazo desde el crédito…» (`…Disabled`) | `Creditos.CambioDePlazo` en «No». |
| «Usted registró este crédito y la cooperativa pide que el plazo lo cambie otra persona…» (`…SameUser`) | `Segregacion.CambioDePlazoPorOtraPersona` en «Sí». |

**No mueve plata.** No deja filas en el libro (si las dejara, se rechaza con `…WouldChangeLedger`), así que no hay hecho
contable, ni comprobante, ni mensaje a Comercial. La **factura** de la venta a crédito conserva las cuotas con que se vendió
(es un documento fiscal); el plan del crédito es el que manda en el cobro, la caja, el estado de cuenta, la libranza y los
informes regulatorios, que leen las condiciones vigentes. Si la solicitud ya tenía **documentos para firma** (pagaré, carta
de instrucciones, libranza), la simulación avisa que hay que generarlos otra vez.

**Parámetros** (Parámetros de Cartera, con vigencia):

- `Creditos.CambioDePlazo` (de fábrica «Sí»; por cooperativa o por línea): con «No» el plazo sólo cambia por novedad.
- `Segregacion.CambioDePlazoPorOtraPersona` (de fábrica «No»): con «Sí», quien registró el crédito (quien desembolsó o
  vendió a crédito) no le cambia el plazo; lo hace otra persona. De fábrica no se exige porque el cambio no mueve plata y
  corrige un dato de un crédito recién nacido; la cooperativa que quiera segregar lo enciende.

El permiso `Lending.Loans.ChangeTerm` lo siembra la API al arrancar; lo traen el administrador de la empresa y las
plantillas `cartera.administrador` y `cartera.jefe`.

## 4. Consentimientos

Quien respalda la obligación tiene que aceptar el cambio. En el detalle de la novedad, «Registrar…» junto a cada parte:

- **Acepta**: sube su aceptación firmada (el otrosí o el pagaré nuevo) a la novedad y elíjala; aceptar sin documento se
  rechaza (`Lending.Novelty.ConsentAttachmentRequired`). Una persona que no es parte responde `Lending.Novelty.PartyUnknown`.
- **No acepta**: queda la constancia si la hay. Al aplicar, quien aplica decide con motivo (§6).

Decidida la novedad, sus documentos de consentimiento no se cambian ni se borran.

## 5. Radicar y decidir

«Radicar» vuelve a simular, sella la huella y la enruta por la matriz de atribuciones (sujeto `Novelty`, por monto
consolidado, días de mora y vinculado; las reglas pueden filtrar por mecanismo y causa). Si el deudor es vinculado, la
decide el consejo con acta. Quien la radicó no la decide (`Lending.Segregation.Conflict`). La decisión individual se
registra en el detalle o en «Mis pendientes»; la colegiada, en Actas. «Devolver» la regresa a borrador.

## 6. Aplicar

Con la novedad aprobada, «Aplicar» (otra persona, con `Lending.Novelties.Apply`):

- Si una parte no aceptó, elija por cada una: **no aplicar la novedad** (queda anulada y el crédito no cambia) o
  **aplicarla con la garantía extinguida**. Lo segundo exige que la cobertura que queda cumpla la línea; si no, se
  responde `Lending.Novelty.CoverageInsufficient` con lo exigido y lo que queda, salvo que haya **excepción de cobertura**
  aprobada por **otra persona** (cuatro ojos, decisión del dueño del 2026-10-15): con la novedad aprobada, quien tenga la
  atribución del nivel de excepción de la matriz (`Lending.Decisions.Exceptions`, plantilla `cartera.gerente`) la aprueba
  con motivo en el detalle («Aprobar excepción de cobertura»; sin ese nivel, `Lending.Attribution.LevelRequired`; quien la
  radicó no puede; una sola vez). Después, quien aplica marca «Usar la excepción de cobertura aprobada»; si nadie la aprobó
  responde `Lending.Novelty.CoverageExceptionMissing`, y si la aprobó él mismo, `Lending.Novelty.CoverageExceptionSameUser`.
- **Prórroga, fecha o tasa**: el crédito conserva su número y su historial; el plan anterior queda como versión.
- **Novación o refinanciación**: nace un **crédito enlazado** con otro número que hereda marca, conteo, calificación e
  historial de mora; el anterior queda `Novado`. Sus garantías se constituyen aparte.
- Emite `CreditoReestructurado`, `CreditoModificado` o `CreditoNovado` (comprobante `CN`) y, si cambia una dimensión
  contable, `DimensionContableCambiada`. Con `Contabilidad.FaltaDeRegla = Bloquear` y una regla faltante, no se aplica
  (`Lending.Prevalidation.NotPostable`). Una prórroga sin capitalización no mueve saldos: su hecho se procesa «sin
  comprobante (valor cero)».

## 7. Condonaciones

«Nueva condonación»: el crédito, los conceptos y el valor de cada uno, y el motivo. Cada concepto no pasa de su saldo
(`Lending.Waiver.ExceedsBalance` dice el saldo). Condonar **capital** sube al nivel de excepción. Los días de mora y si el
deudor es vinculado se sellan al pedirla y la enrutan; la matriz puede llevar un concepto o una altura de mora a un nivel
más alto que el del gerente. Toda decisión lleva motivo y quien la pidió no la decide. Aplicada, emite
`CondonacionAprobada`: lo condonado sale primero contra el deterioro individual del último cierre y el resto va al gasto.

## 8. Fallecimiento, retiro, embargo e insolvencia

En la pestaña «Eventos y reclamaciones» del crédito:

- **Registrar evento del deudor**: fallecimiento, retiro de la cooperativa o embargo, con su fecha y el soporte (se sube a
  la persona y no se borra). Todos bloquean el cupo. El **fallecimiento** además suspende la cobranza, los mensajes de
  cobro y el reporte negativo y abre una **reclamación en trámite** por cada crédito con seguro de vida deudores, ante la
  aseguradora de la línea. La causación sigue salvo que el parámetro `Causacion.ContinuaTrasFallecimiento` de la línea
  diga lo contrario. El mismo evento dos veces responde `Lending.DebtorEvent.Duplicate`.
- **Reclamación**: cuando la aseguradora paga por el recaudador, como pagadora, la reclamación se cierra **pagada** con la
  operación en la misma transacción; aquí sólo se cierra como negada o desistida (con motivo), lo que levanta la suspensión.
- **Insolvencia**: abre el proceso en negociación (fecha, régimen, autoridad, radicado y el auto como soporte): suspende la
  causación y el cobro de todos los créditos del deudor, bloquea crédito nuevo y mantiene la calificación del último
  cierre anterior. El **acuerdo** se tramita como una novedad de reestructuración ligada al proceso («Acuerdo como
  novedad»): al aplicarla, el proceso queda con acuerdo y **se levanta solo** el bloqueo de crédito nuevo. El **fracaso**
  («Registrar fracaso…») lo cierra y la calificación pasa a la que fija la norma desde esa fecha, pero **el bloqueo se
  mantiene** (decisión del dueño del 2026-10-15): lo levanta a mano, con motivo, quien tenga `Lending.Blocks.Manage` en
  **Cupos** (`/cartera/cupos`, «Levantar…» sobre el bloqueo de insolvencia).

## 9. Pendiente de decidir (dueño)

- Desde el 2026-10-15 las plantillas `cartera.analista` y `cartera.gerente` traen `Lending.Novelties.Read` (deciden
  novedades) y `cartera.jefe` trae `Attachments.Upload` y `Attachments.Download` (sube los consentimientos y lista los
  soportes para elegirlos).
- El soporte de eventos e insolvencia usa el dueño de adjunto `LendingDebtorEventSupport` (la persona), que no estaba en el
  contrato de adjuntos (§9 de adaptadores-y-archivos); se agregó allí.
- Los roles contables `IngresoDiferido` y `GastoDeCastigoOCondonacion` siguen sin cuenta hasta que la contadora los valide
  (T0895); mientras tanto rige `Contabilidad.FaltaDeRegla`.
