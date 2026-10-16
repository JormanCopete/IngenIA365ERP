# Cartera: castigo y evaluación de cartera

> Feature 013 (cartera financiera), fase 17 (US14, entrega K3). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §11.3, §12.6 y §17,
> [contracts/mensajes.md](../../specs/013-cartera-financiera/contracts/mensajes.md) §10.22 y
> [contracts/contabilidad.md](../../specs/013-cartera-financiera/contracts/contabilidad.md) §2.1, §2.2 (ejemplo 14). Aquellos son el
> contrato; esto es la receta para **el jefe de cartera**, la **secretaría del consejo** y el **comité de riesgos**.

**Castigar** es sacar del activo un crédito irrecuperable: deja de estar en el balance y pasa a cuentas de orden, pero la
deuda no se perdona —se sigue cobrando y lo que se recupere es ingreso—. La **evaluación de cartera** es la revisión
periódica en la que el comité de riesgos propone recalificar créditos y el consejo la aprueba; su calificación entra a los
cierres mensuales desde su mes de efecto. Nada de esto escribe asientos: Cartera emite sus hechos y Contabilidad los
contabiliza por la matriz (`CreditoCastigado` en un `PV`).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Ver lotes de castigo | `/cartera/castigos` | `Lending.Loans.Read` |
| Armar el lote, agregar y quitar créditos, enviarlo al consejo, anularlo | `/cartera/castigos` | `Lending.WriteOffs.Prepare` (plantilla `cartera.jefe`) |
| Subir los soportes de cada crédito | diálogo del lote | `Attachments.Upload` (plantillas `cartera.jefe`, `cartera.consejo` y `cartera.administrador`) |
| Decidir el lote | `/cartera/actas`, punto «Decisión» del consejo | miembros del consejo (votos), `Lending.Sessions.*` para la secretaría |
| Aplicar el lote y registrar el reporte al supervisor | `/cartera/castigos` | `Lending.WriteOffs.Apply` (plantilla `cartera.consejo`) |
| Cartera castigada y recuperaciones | `/cartera/informes?vista=charge-offs` | `Lending.Reports.View` |
| Ver evaluaciones | `/cartera/evaluaciones` | `Lending.Ratings.Read` |
| Preparar una evaluación y descartarla | `/cartera/evaluaciones` | `Lending.PortfolioEvaluations.Prepare` (plantilla `cartera.comite-riesgos`) |
| Recomendarla y aprobarla | `/cartera/actas`: punto «Recomendación» del comité de riesgos, punto «Decisión» del consejo | miembros de cada órgano |
| Aplicarla | `/cartera/evaluaciones` | `Lending.PortfolioEvaluations.Apply` (plantilla `cartera.consejo`) |

La plantilla `cartera.consejo` trae también `Lending.Loans.Read` y `Lending.Ratings.Read`, que piden las dos pantallas (desde
el 2026-10-15).

Todo lo que escribe lleva su clave de operación: un doble clic no arma dos lotes ni aplica dos veces.

## 2. Requisitos de un crédito para castigarlo

Cada crédito se verifica **al armar el lote, al agregarlo y otra vez al aplicarlo**. Los que no cumplen quedan
**rechazados** en el lote con lo que les falta (`Lending.WriteOff.RequirementMissing`), y el detalle dice cada requisito con
su resultado y su evidencia.

| Requisito | Origen | Qué se mira |
|---|---|---|
| Ya no es asociado | Norma | la marca de asociado de la persona en el maestro |
| Aportes cruzados | Norma | la consulta de aportes; mientras no esté disponible (D19, llega con Ahorros), **el soporte del cruce adjunto** |
| Informe motivado | Norma | adjunto al crédito |
| Concepto de irrecuperabilidad | Norma | adjunto al crédito |
| Deterioro total | Política | `Castigo.ExigeDeterioroTotal` (sí, por defecto): el deterioro individual del último cierre igual al saldo |
| Altura mínima de mora | Política | `Castigo.DiasMinimosDeMora` (vacío por defecto: no se exige) |
| Gestiones agotadas | Política | `Castigo.GestionesMinimas` (0); mientras no exista el registro de gestiones (K4), las acredita el informe motivado |

Las tres claves `Castigo.*` están en **Parámetros** (`/cartera/parametros`), con vigencia, «pendientes de validar».

## 3. Armar el lote y llevarlo al consejo

1. «Nuevo lote»: busque el crédito, suba sus soportes (informe motivado, concepto de irrecuperabilidad y cruce de
   aportes) con «Agregar soporte», elíjalos en cada lista y escriba por qué se propone el castigo.
2. El lote nace en **borrador** con el crédito incluido o rechazado. «Agregar crédito…» suma otros (el que no cumple no
   entra y la pantalla dice qué le falta); «Quitar» saca uno incluido. Un crédito va a lo sumo en un lote abierto.
3. «Enviar al consejo» lo congela.
4. En **Actas**, la secretaría del consejo abre la sesión, registra la asistencia, agrega un punto **«Decisión»** sobre el lote,
   registra los votos y cierra el acta. Quien armó el lote no vota. Si no hay consejo de administración activo, el punto no
   se puede crear (`Lending.WriteOff.BoardMissing`).
5. Al cerrarse el acta el lote queda **aprobado** con su punto (o **negado**).

Al armarlo, la pantalla **avisa** —sin bloquear— lo de la norma y la política que sigue vacío y pendiente de validar: el plazo
del reporte de castigos al supervisor (`WriteOffReportTermPending`) y la altura mínima de mora (`WriteOffMinimumArrearsPending`),
con el enlace para corregirlo.

**Anular el lote**: mientras no se aplique (en borrador, enviado o ya aprobado por el consejo), «Anular lote» con el motivo,
que queda en la auditoría. Sus créditos quedan libres para otro lote; un lote aplicado no se anula.

## 4. Aplicar

«Aplicar el castigo» en el lote aprobado. Lo hace la secretaría del consejo, **no quien armó el lote** (segregación
«armar × aplicar»). Al aplicar:

- se vuelven a verificar los requisitos de cada crédito a hoy: el que ya no cumple (o ya no está vigente) se **excluye** con su
  motivo —queda rechazado en el lote, lo dice la pantalla y queda en la auditoría— y **el resto se castiga**; si no queda
  ninguno, el lote no se aplica (`Lending.WriteOff.NoLoansLeft`, con cada crédito y lo que le falta) y se puede anular;
- la validación previa contable: con `Contabilidad.FaltaDeRegla = Bloquear` y sin regla en la matriz, no se aplica
  (`Lending.Prevalidation.NotPostable`);
- cada crédito queda **castigado** (`WrittenOff`) con la fecha y la operación `WriteOff` (`CAS-`), referida al punto del acta;
- sale `CreditoCastigado`: lo que sale del activo por concepto, lo que se cubre con el deterioro individual del último cierre
  y el faltante, que va al gasto; el interés en suspenso pasa de la orden contingente a la de control;
- desde el **día siguiente** la causación queda en suspenso: la castigada sigue causando, pero en control, y sigue en la
  consulta de obligaciones y en el contrato de pagos.

En Contabilidad, el castigo es un `PV`: el activo sale contra el deterioro y el gasto, y entra a `CastigadaEnOrden` con su
concepto contra `ContraOrdenControl`. La conciliación de Cartera lleva el saldo de cada castigado (también el de lo migrado
ya castigado) al conjunto de cuentas de la castigada en orden.

## 5. Reporte al supervisor

Si el tipo de entidad del perfil regulatorio debe reportar los castigos (dato `writeOffReport { required, dueDays }` del
documento normativo **Tipos de organización**), al aplicar el lote queda sellado el plazo (hoy + los días) y se levanta la
alerta **«Plazo regulatorio por vencer»**. Radicado el reporte, «Registrar el reporte al supervisor…» con el radicado y, si
quiere, el soporte; se registra **una sola vez** y atiende la alerta. Sin el dato en el documento no se exige reporte: **la
semilla no lo trae, lo aporta el dueño** con una versión nueva del documento (registro central de valores nacionales).

## 6. Recuperaciones

Un pago sobre un crédito castigado entra por el contrato como cualquier otro (caja, recaudador, nómina) y sus filas son de
**recuperación**: emite `RecuperacionDeCastigada` (en el comprobante del pago: `RC`, o `DN` si recauda Nómina), que sale de la
castigada en orden y entra al ingreso por recuperación. La vista **`charge-offs`** («Cartera castigada») lista cada castigado
hasta el final del rango con la fecha, el punto del acta (o «Castigado antes de la activación» en lo migrado), el saldo
castigado y lo recuperado en el rango. Gestiones y último contacto llegan con la cobranza (K4).

## 7. Evaluación de cartera

1. **Preparar** («Nueva evaluación»): el corte (el **último día de un mes**), el mes de efecto (`AAAA-MM`, posterior al corte),
   los criterios aplicados (JSON) y, por crédito, la categoría propuesta (A a E; E1 o E2 sólo en E) con su justificación. Cada
   crédito tiene que estar vivo al corte y en su alcance; la pantalla muestra junto a la propuesta la calificación por las
   reglas en el cierre al corte. Si el documento **Tipos de organización** dice la periodicidad del tipo de entidad
   (`portfolioEvaluation { cutoffMonths, effectMonthsAfterCutoff }`, semestral o anual), el corte y el mes de efecto tienen
   que ser los suyos (`Lending.PortfolioEvaluation.PeriodInvalid`). Otra evaluación sin aplicar del mismo corte se rechaza
   (`Lending.PortfolioEvaluation.Overlaps`).
2. **Recomendar**: en Actas, un punto **«Recomendación»** del **comité de riesgos** (hace falta uno activo:
   `Lending.PortfolioEvaluation.RiskCommitteeMissing`). Un punto «Decisión» del comité también la deja recomendada.
3. **Aprobar**: un punto **«Decisión»** del consejo. Sin la recomendación cerrada del comité, el consejo no la decide
   (`Lending.PortfolioEvaluation.RecommendationRequired`). Quien la preparó no vota.
4. **Aplicar** con los dos puntos (los de otra cosa responden `Lending.Session.ItemMismatch`).

Al prepararla, la pantalla avisa —sin bloquear— si la periodicidad de la norma sigue pendiente de validar
(`PortfolioEvaluationPeriodicityPending`). **Descartar**: mientras no se aplique (borrador, recomendada o aprobada),
«Descartar evaluación» con el motivo, que queda en la auditoría; libera el corte para preparar otra.

Aplicada, la calificación propuesta es la candidata **«Evaluación de cartera»** de cada crédito en el **cierre de su mes de
efecto** y en los siguientes, hasta la próxima evaluación aplicada; el cierre se queda con la peor de las candidatas (mora,
análisis, arrastre, reestructurado, evaluación) y el reporte de cambios dice la causa. **Nunca reescribe un cierre anterior.**

## 8. Lo que falta o decide el dueño

- **El retiro del asociado no tiene ruta.** Ninguna pantalla de Core apaga la marca de asociado: llega con Ahorros y aportes
  (feature 014); hasta entonces el requisito «ya no es asociado» no se puede cumplir por la aplicación.
- Fijar `Castigo.DiasMinimosDeMora` y confirmar `Castigo.ExigeDeterioroTotal` y `Castigo.GestionesMinimas`.
- Aportar `writeOffReport` (si se reporta y en cuántos días) y `portfolioEvaluation` (meses de corte y de efecto) del tipo de
  entidad en el documento **Tipos de organización**. Decisión del dueño del 2026-10-15: mientras falten se quedan vacíos y el
  sistema **avisa** en la activación de Cartera, al armar un lote y al preparar una evaluación.
- La contadora: las cuentas de `CastigadaEnOrden`, `ContraOrdenControl` y `GastoDeCastigoOCondonacion` (motivo
  `WriteOff`) en la matriz (C7, C81). Desde el 2026-10-15 la plantilla de la matriz las trae como filas sugeridas sin cuenta.
- No hay ruta para **editar las líneas** de una evaluación en borrador (la entidad lo admite): se descarta y se prepara otra.
- Las plantillas suben soportes (`Attachments.Upload`) pero no traen `Attachments.Download`, que pide la lista de soportes del
  diálogo para elegirlos: la cooperativa lo suma en el rol si hace falta.
