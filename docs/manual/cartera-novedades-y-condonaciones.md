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
| Aplicar una novedad aprobada | detalle en `/cartera/novedades` | `Lending.Novelties.Apply` |
| Condonaciones: lista, pedir, anular | `/cartera/condonaciones` (y el botón «Condonar» del crédito) | `Lending.Loans.Read` · `Lending.Condonations.Request` |
| Decidir y aplicar una condonación | `/cartera/condonaciones` o «Mis pendientes» | `Lending.Loans.Read` + nivel · `Lending.Condonations.Apply` |
| Eventos del deudor y reclamaciones al seguro | pestaña «Eventos y reclamaciones» de `/cartera/creditos/{id}` | `Lending.Loans.Read` · `Lending.DebtorEvents.Manage` |
| Insolvencia | botón «Insolvencia» de la misma pestaña | `Lending.Insolvency.Manage` |
| Informe de reestructurados | `/cartera/informes?vista=restructured` | `Lending.Reports.View` + `Lending.Loans.Read` |

Las plantillas de rol traen `Novelties.Read` y `.Request` sólo en `cartera.jefe`; quien decide una novedad (el analista,
el gerente) necesita además **ver** novedades: hoy hay que darle `Lending.Novelties.Read` en un rol propio (ver §9).

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
  responde `Lending.Novelty.CoverageInsufficient` con lo exigido y lo que queda, salvo que quien aplica apruebe la
  **excepción de cobertura** con la atribución del nivel de excepción de la matriz (sin ella, `Lending.Attribution.LevelRequired`).
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
  novedad»): al aplicarla, el proceso queda con acuerdo. El **fracaso** («Registrar fracaso…») lo cierra y la calificación
  pasa a la que fija la norma desde esa fecha.

## 9. Pendiente de decidir (dueño)

- Las plantillas `cartera.analista` y `cartera.gerente` deciden en sus niveles pero no traen `Lending.Novelties.Read`,
  que la ruta de decisiones de novedades exige: sin un rol propio con ese permiso no pueden decidir una novedad.
- El soporte de eventos e insolvencia usa el dueño de adjunto `LendingDebtorEventSupport` (la persona), que no estaba en el
  contrato de adjuntos (§9 de adaptadores-y-archivos); se agregó allí.
- Los roles contables `IngresoDiferido` y `GastoDeCastigoOCondonacion` siguen sin cuenta hasta que la contadora los valide
  (T0895); mientras tanto rige `Contabilidad.FaltaDeRegla`.
