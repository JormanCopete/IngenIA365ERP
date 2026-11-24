# Cartera: causación diaria y liquidación a cualquier fecha

> Feature 013 (cartera financiera), fase 8 (US4, entrega K1). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §8.1 (consultas del crédito, estado de cuenta y
> liquidación), §16.4 (corridas) y §16.5 (integridad del libro),
> [contracts/mensajes.md](../../specs/013-cartera-financiera/contracts/mensajes.md) §10.3–§10.6 y §10.12 (los hechos de la causación),
> y [data-model.md](../../specs/013-cartera-financiera/data-model.md) §5, §6 y §22. Aquellos son el contrato; esto es la receta para la
> **contadora** (qué se causa, cómo y con qué tasa; cómo cuadra lo que llega a Contabilidad) y para cartera (cómo leer una
> liquidación). Lo que hace operaciones con el proceso diario está en
> [docs/operaciones/cartera-proceso-diario.md](../operaciones/cartera-proceso-diario.md).

Cartera no guarda saldos que alguien pueda editar. Cada día el proceso diario **causa** los intereses de cada
crédito vivo —corriente, mora y seguro, por cuota— y los deja en el **libro** del crédito (`LND_LoanTransactions`),
que nunca se borra ni se edita: lo que cambia se corrige con otra fila. Cualquier saldo, a cualquier fecha, sale de
**reconstruir** el crédito desde sus operaciones con las reglas con que se pactó; el libro es el registro de lo que
se causó, y una verificación compara los dos.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Buscar créditos (número o deudor, línea, oficina, estado, modalidad, mora mínima) | `/cartera/creditos` | `Lending.Loans.Read` |
| Resumen, estado de cuenta, plan con sus versiones, operaciones con su comprobante, libro, garantías y giro | `/cartera/creditos/{id}` (pestañas) | `Lending.Loans.Read` |
| Liquidación a una fecha «con la información hasta» (lectura auditada) | pestaña «Liquidación» del detalle | `Lending.Payoffs.Read` |
| Corridas del proceso: ver | pestaña «Corridas del proceso» de `/cartera/bandeja-de-mensajes` (`GET /api/lending/process-runs`) | `Lending.Processes.View` |
| Reintentar una corrida fallida o con créditos sin causar | botón «Reintentar…» de esa pestaña, con motivo (`POST …/{id}/retry`) | `Lending.Processes.Run` |
| Ordenar el ciclo de un día | `POST /api/lending/process-runs` (sin pantalla) | `Lending.Processes.Run` |
| Verificar el libro contra la reconstrucción | `POST /api/lending/integrity/verify` | `Lending.Integrity.Verify` |
| Proceso de causación: ver lo que espera, los procesos, su borrador por hecho y por crédito, su lote (§9.5) | `/cartera/causacion` | `Lending.AccrualProcesses.Read` |
| Armar, recalcular y anular el borrador | botones de esa pantalla | `Lending.AccrualProcesses.Calculate` |
| Pedir que se ponga en firme (con motivo) y aprobar, presente, el de otra persona | botones de esa pantalla | `Lending.AccrualProcesses.Confirm` |
| Reprocesar uno en firme (y pedir la firma del reproceso) | botones de esa pantalla | `Lending.AccrualProcesses.Reprocess` (y `.Confirm`) |

Fuera del alcance de oficina y línea el crédito no existe para quien consulta (el mismo 404). La lista muestra los
saldos de la **última posición diaria** (`LND_LoanDailyPositions`), con su fecha: es una foto rápida, nunca la
autoridad. La cifra exacta a una fecha es la liquidación.

## 2. Convenciones, equivalencia de tasas y tope de usura

Todo se calcula en `decimal` exacto (nunca en coma flotante) con las reglas **selladas en el crédito al
desembolsar** (`RulesVersion`, hoy `LND-2026.1`). Un cambio de reglas del programa no toca los créditos vivos: un
crédito sellado con unas reglas se calcula siempre con ellas, y si el programa no las conoce el crédito no se
calcula con otras —queda con el error `Lending.Rules.UnknownVersion` en la corrida—.

- **Convención de días** de la línea: real/365, real/360 o 30/360. Es la que cuenta los días del tramo.
- **Equivalencia**: la tasa pactada es **efectiva anual** (EA, como fracción: 0,22 = 22 %). La tasa del día es su
  equivalente diario en la convención sellada; la del período, su equivalente en la periodicidad de la cuota.
- **Tasa variable**: índice (IBR, IPC…) más el margen **en la expresión del índice**, llevado a EA, con piso y
  techo (D10). Se reajusta en las fechas pactadas con el **último índice publicado**; un índice viejo alerta
  `Cartera.IndiceDesactualizado`.
- **Tope de usura**: la tasa del día nunca pasa la usura certificada **del día** para la modalidad del crédito (la
  clave de usura sale del catálogo de modalidades vigente). La mora (1,5 veces la corriente, la de la línea o, desde la
  feature 024, la **tasa general de mora** de la empresa, §9.1) también se topa. Si un día se topa, el crédito queda marcado («Topado en la usura») y se alerta
  `Cartera.UsuraTopada` una vez por crédito y mes.
- Una tasa pactada con más de seis decimales de fracción **se rechaza** (`Lending.Rate.TooPrecise`), nunca se redondea.
- **Redondeo**: el de la línea (unidad y modo), aplicado una vez por fila del libro; el residuo queda visible en la
  última cuota, nunca escondido.

## 3. La causación del día: (d − 1, d] y la puesta al día

El ciclo del día **d** causa, para cada crédito vivo cuya marca de agua (`LastAccruedDate`) es **d − 1**, lo
devengado entre el final de d − 1 y el final de d:

- **interés corriente** sobre el capital de cada cuota no vencida, con la tasa del día;
- **interés de mora** sólo sobre **lo vencido** (el capital de la cuota vence el día siguiente a su fecha exigible),
  con la tasa de mora del día topada por la usura;
- **seguro** cuando la línea lo cobra sobre saldo (D9).

Cada concepto, cuota y día es **una fila**. Causar un día dos veces es imposible: el registro sólo avanza la marca
de d − 1 a d, y un índice único lo garantiza en la base. Repetir el ciclo no escribe nada.

**Un día sin correr** (el servidor estuvo apagado, el ciclo falló) no se pierde: el ciclo siguiente causa **cada día
pendiente por separado**, cada uno con su tasa y su usura, desde la marca + 1 hasta hoy. Lo mismo pasa cuando una
operación llega sobre un crédito atrasado (un tramo, en K1; un pago, desde US5): **primero lo pone al día en su propia
transacción** («puesta al día», partes `LoanCatchUp` con el rótulo `ALD-{operación}-n`) y después aplica la operación.

**Día no hábil** (art. 829 del Código de Comercio): una cuota que vence en domingo o festivo nacional se **exige** el
hábil siguiente; el sábado cuenta como hábil (D2). Hasta la fecha exigible no hay mora, y el interés de esos días va
en la cuota siguiente.

**Lo que llega a Contabilidad.** La causación no viaja crédito por crédito: el ciclo arma **partes** (`LND_AccountingFactParts`),
una por fecha, sucursal contable, tipo de hecho y tanda, cada una con su mensaje masivo `InteresCausado`,
`MoraCausada` o `SeguroCausado` (rótulo `CAU-aaaammdd-nnnn`), sus totales por dimensión contable y la lista de créditos
con su huella. Contabilidad los contabiliza en línea o por lotes según el modo de paso vigente; con el modo por
lotes, el lote de la causación se arma al terminar el ciclo (`AccrualRun:{fecha}`). Cada fila del libro guarda el
mensaje de su parte: desde el libro se llega al comprobante y desde el comprobante al libro.

## 4. Suspensión, reanudación y reajuste

- **Suspensión**: cuando un crédito alcanza la altura de mora de la categoría de suspensión de su segmento en el
  **Anexo 1 del SARC vigente** (61 días en consumo, según la propuesta sembrada), o por otra causa de la regla (dos o
  más reestructuraciones, insolvencia, castigo), lo que se causa **desde ese día** va a cuentas de suspenso
  (`CurrentInterestInSuspense`, `DefaultInterestInSuspense`). **Lo causado antes no se reclasifica.** Cada
  suspensión emite un `CausacionSuspendida` por crédito (informativo, sin comprobante).
- **Reanudación**: cuando el crédito vuelve por debajo del umbral (por la regla por defecto), lo nuevo vuelve a
  causarse en las cuentas normales y sale un `CausacionReanudada`.
- **Reajuste de tasa variable**: en su fecha, el ciclo registra una operación `RateReset` (número `RR-…`, sin
  filas de libro), una versión nueva de condiciones con la tasa sellada a seis decimales y una versión nueva del
  plan que recalcula cuota o plazo según lo pactado. La causación de ese día ya usa la tasa nueva.

## 5. La liquidación a (D, R) y por qué siempre da lo mismo

La liquidación responde **cuánto debe el crédito a la fecha D con la información que se tenía hasta el instante R**
(`date` y `recordedAsOf`, C83). Sale de **reconstruir** el crédito —no del libro— con las operaciones registradas
hasta R que no tenían un reverso registrado hasta R, las versiones de condiciones conocidas en R y los valores
nacionales conocidos en R (`VigenciaBitemporal`).

- **Reproducible**: la misma (D, R) da **el mismo resultado al centavo y la misma huella** (`fingerprint`,
  `sha256:` + la huella de la reconstrucción con el prefijo de las reglas), hoy o dentro de un año.
- **R anterior**: si hoy se registra un pago con fecha valor de la semana pasada (o un desembolso se reversa), la
  liquidación de la semana pasada **con R de ayer** no cambia; con R de ahora, sí. Así se contesta «¿qué le dijimos
  al deudor ese día?».
- **D futura**: se proyecta **sin pagos intermedios** con la última usura conocida y dice hasta cuándo vale
  (`validUntil`: el último día con usura certificada). Una D pasada o de hoy sin usura certificada responde
  `NationalValues.Missing`.
- **La explicación** trae, por concepto y cuota, cada tramo con sus días, la base, la tasa EA, la tasa diaria, el
  valor y si se topó en la usura o fue a suspenso. Es lo que se le muestra al deudor o a la revisoría.
- Consultar **no escribe** nada y queda en la auditoría (`Lending.Payoff.Viewed`, con D, R y la huella).

El **estado de cuenta** (pestaña del mismo nombre) es la misma reconstrucción vista por cuota: pagadas, vencidas,
cubiertas por anticipo y por vencer; días de mora; saldos; versiones del plan; marcas; y los no aplicados por la
pagaduría o la nómina. La calificación y la etapa de cobranza llegan con US9 y K4.

## 6. Correcciones: un valor nacional que cambia después de causar

Si el maestro corrige una usura ya replicada (la corrección llega con `SupersedesPublicId`), la réplica encola
sola una corrida **`NationalValueCorrection`** para los días ya causados que toca. La corrida verifica el libro de
cada crédito contra la reconstrucción con la usura corregida y registra **cada diferencia por día, concepto y cuota**
como `AccrualAdjustment` (rótulo `VNC-…`, hecho `AjusteDeCausacionPorFechaValor`, fecha contable del período
abierto). Si el deudor ya había pagado interés de más (desde US5), se reconoce un **saldo a favor**
(`SaldoAFavorReconocido`). La liquidación con R anterior a la corrección sigue dando lo que daba (SC-002).

## 7. El libro y su verificación

`POST /api/lending/integrity/verify` compara, crédito por crédito y **día por día** hasta su marca, Σ libro por (día
valor, concepto, cuota) contra la reconstrucción con la información de ahora. Puede pedirse para unos créditos, una
muestra (`sampleSize`) o todos, y el ciclo verifica una muestra al terminar cada día. **No corrige nada**: cada
crédito con diferencias levanta **`Cartera.LibroDescuadrado`** (a cartera y a la contadora) con el primer día
descuadrado. Un libro descuadrado no se arregla editándolo: se averigua qué operación lo causó.

## 8. Cómo leer un caso dorado

Los casos dorados del motor viven en `tests/IngenIA365ERP.Domain.Tests/Lending/Casos/*.json`, calculados a mano con
`calcular.py` (nunca con Excel) y **pendientes de validar por la contadora**. Cada uno trae:

- `credito`: monto, plazo, periodicidad, tasa EA, convención, equivalencia, redondeo y reglas;
- `plan`: las cuotas esperadas (capital, interés, seguro, saldo) al centavo;
- `series`: la usura por modalidad con su vigencia (y `registradoEn` cuando se corrige), el IBR y los festivos;
- `operaciones`: desembolsos, tramos y reajustes con su fecha valor y su `registradoEn` (R);
- `consultas`: a una fecha D y con la información hasta R, lo esperado **por día, cuota y concepto**.

Para revisar uno basta tomar un tramo de la explicación y rehacerlo: días × base × tasa diaria, topada si la tasa
del día pasa la usura de ese día. `python calcular.py --verificar` recalcula todos los casos y dice si coinciden.
Los casos de esta fase: 10 (vencimiento en festivo), 12 (suspensión y reanudación), 13 (usura baja), 14 (corrección
de usura), 15 (IBR), 22 (dos días sin causar), 23 (liquidación futura), 24 (tolerancia de saldo residual),
26 (crédito migrado con suspenso), 28 (seguro sobre saldo) y 30 (desembolso por tramos). La feature 024 suma el 35 (tasa
general de mora: baja a mitad de un período, sube por encima de la usura y se topa, vuelve a la de la línea con una vigencia
vacía, y «sin tasa general» con la información de antes del primer registro) y el 36 (la administración como rubro propio, §9.4).

## 9. Causación configurable (feature 024, fase 1, 2026-11-16)

Pedido del dueño. El **cálculo** sigue siendo día a día (§3); lo que se vuelve configurable es la tasa de mora y **cómo
pasa a contabilidad** lo causado. De fábrica nada cambia. Los tres parámetros están en `/cartera/parametros` (grupos
«Mora», «Contabilidad» y «Causacion»), con vigencia, motivo e historial como los demás.

### 9.1 Tasa general de mora (`Mora.TasaGeneral`)

- Efectiva anual como fracción (`0.30` = 30 %), sólo general. **Vacía** (de fábrica) = rige la mora de la línea.
- Desde su vigencia reemplaza la mora sellada de **todos** los créditos —también los ya desembolsados—, siempre **topada en
  la usura del día** (en vivienda, en el menor entre la usura y el tope de vivienda: las reglas de la Ley 546 del motor no fijan
  la tasa de mora, sólo que no corre sobre intereses, así que la general también rige allí). Si la empresa quiere volver a la
  de cada línea, registra una vigencia **vacía**.
- **No admite vigencias con fecha pasada ni en un día que la causación ya corrió** (`Lending.Parameter.BackdatedNotAllowed`,
  con la primera fecha admitida en `data.minimumValidFrom`): lo causado no se recalcula. Si el ciclo de hoy ya corrió, la
  vigencia empieza mañana. Como toda tasa, lleva a lo sumo seis decimales (`Lending.Rate.TooPrecise`).
- La reconstrucción la recibe como una serie con vigencia (`EntradaDeReconstruccion.MoraGeneral`, leída entera por
  `SeriesRegulatoriasDeCartera` con lo conocido en R): cada vigencia cambia la tasa de la mora desde su día, igual que una
  usura nueva; la liquidación con una R anterior al registro da lo que daba.

### 9.2 Un comprobante por concepto (`Contabilidad.CausacionSeparadaPorConcepto`)

- «No» (de fábrica): un `CA` por día con corriente, mora y seguro, como siempre.
- «Sí»: cada hecho de causación (`InteresCausado`, `MoraCausada`, `SeguroCausado`, `AdministracionCausada` —fase 2—,
  `AjusteDeCausacionPorFechaValor` y `CorreccionMonetariaUvrCausada`) va en **su propio comprobante y su propia transacción**: si uno falla (una regla que falta,
  un período cerrado) los demás se contabilizan. La vista previa del lote muestra lo mismo, con el concepto al lado del tipo.
- El tipo de cada uno lo elige la empresa en `/contabilidad/cartera/tipos-de-comprobante`. La semilla trae tres sugeridos,
  `CM` «Causación de interés de mora», `CS` «Causación de seguros» y `AD` «Causación de administración» (fase 2), **activos y
  sin mapear**: mientras nadie los elija, la mora, el seguro y la administración siguen en `CA` (separados si el parámetro está
  en «Sí»).
- Se sella al emitir el hecho (la marca `+Concepto` en la granularidad de su `ScheduleKey`, p. ej. `CAR.MCA|FinDeCausacion||PorDia+Concepto`):
  cambiar el parámetro rige para lo que se cause desde su vigencia.

### 9.3 Contabilización manual por concepto (`Causacion.Modo*`)

`Causacion.ModoInteresCorriente`, `.ModoInteresDeMora`, `.ModoSeguro`, `.ModoAdministracion` (fase 2, §9.4) y `.ModoOtrosRubros`
(hoy la corrección monetaria UVR): «Automatica» (de fábrica) o «Manual».

- En «Manual» el motor y el libro **causan igual** cada noche y el hecho se emite igual, pero queda **esperando**: sale por
  lotes —aunque el modo de paso del hecho sea «en línea»— con el disparador `CausacionManual` y el alcance
  `CausacionManual:{día causado}`. Ni el lote del fin de la causación (que sólo toma `AccrualRun:`) ni el programador de lotes
  (que sólo toma franjas diarias) lo toman. «No pasa» sigue sin pasar.
- Lo contabiliza el **proceso de causación** (§9.5, fase 3), que lo toma por concepto y rango de días por
  `CausacionPorContabilizar` (también informa lo que espera, por día y hecho). Un **lote manual** de Cartera
  (`/contabilidad/cartera/lotes`, pestaña «Lote manual») también lo toma como a cualquier entrega sin lote de su rango —sin borrador
  ni cuatro ojos—; el campo «Horario» filtra por la clave (p. ej. `CAR.MCA|CausacionManual||PorDia`). Es la salida para lo que el
  proceso deja fuera a propósito (un mes ya cerrado por Cartera, los créditos con abonos de un reproceso).
- El **cierre contable** del mes lo sigue avisando: lo que espera cuenta en `Accounting.Period.LendingPending`.
- Mientras lo causado de un concepto espera, sus **pagos** sí se contabilizan (en línea): la cuenta por cobrar de ese concepto
  puede quedar con saldo crédito hasta que se contabilice la causación. Conviene correr el lote (o el proceso) antes del cierre.

### 9.4 La administración como rubro propio (feature 024, fase 2, 2026-11-16)

Decisión del dueño: «Sí, rubro propio». La cuota de administración deja de cobrarse como un seguro más de la línea.

- **En la línea** cada cargo periódico tiene una **clase**: seguro (de fábrica) o administración (`LND_CreditLineInsurances.Kind`,
  migración `CausacionDeAdministracion`). Se configura igual —base, tarifa, periodicidad, causación al vencer o diaria— y se sella
  con su clase en las condiciones del crédito al desembolsar. Computa siempre para la usura. Ver
  `docs/manual/cartera-lineas-de-credito.md` §4.
- **En el plan y en el libro**: cada cuota lleva la administración en su propia columna (`LND_RepaymentInstallments.Administration`,
  dentro del total y de la cuota nivelada) y el libro la causa en su propio concepto, `Administration` (`LoanConcept` 11), con el
  momento del primer cargo de esa clase de la versión de la línea (al vencer o a prorrata de los días del período), independiente
  del momento del seguro. Cuenta en el saldo vencido de la cuota (la altura de mora) igual que el seguro y no causa mora; la
  posición diaria la guarda aparte (`LND_LoanDailyPositions.Administration`).
- **En la prelación** es su propio rubro, `Administracion` (`Pagos.PrelacionDeRubros`): puede ir separada del seguro. Sin el
  parámetro, la lista de la línea la pone justo después del seguro.
- **En contabilidad** es ingreso de la cooperativa: su hecho propio, `AdministracionCausada` (`ACA`), va de **cargo por cobrar**
  (`CargoPorCobrar`, concepto `Administration`) contra **ingreso** (`Ingreso`, concepto `Administration`, por modalidad), por
  lotes al fin de la causación, en `CA` o en `AD` si la empresa lo mapea; su pago sale del mismo cargo por cobrar; el reverso de
  un desembolso y el ajuste por fecha valor la mueven igual. Su contabilización manual es `Causacion.ModoAdministracion`. La
  plantilla de la matriz sugiere las filas nuevas (el cargo por cobrar y el ingreso de la administración, su deterioro) y la
  completitud las pide en cuanto un crédito tiene saldo de administración. Ver `docs/manual/cartera-integracion-contable.md`.
- **En pantalla**: el estado de cuenta (columna «Administración» que cada usuario puede elegir, resumen por concepto, cuotas
  pendientes, simulación de cancelación y su PDF), el detalle del crédito, el plan del desembolso y el detalle de la obligación en
  la caja la muestran aparte, sólo cuando la hay.
- **Los créditos ya desembolsados no cambian** (no hay migración de datos): la administración que tuvieran quedó sellada como un
  seguro más y se sigue causando, cobrando y contabilizando como seguro (`SeguroCausado`).
- Caso dorado 36 (`36-administracion-como-rubro-propio`): seguro al vencer y administración diaria en el mismo crédito, un pago
  parcial que reparte con `Seguro,Interes,Administracion,Capital` y la cancelación. Casos contables 58 (causación) y 59 (pago).
- **Pendiente de validar por la contadora**: las cuentas CUIF del cargo por cobrar y del ingreso de la administración (la matriz
  no siembra ninguna), si la administración debe **suspenderse** con la causación como los intereses (hoy no: se causa como el
  seguro y los cargos), si su cargo por cobrar debe ir por categoría como el seguro (hoy, como los gastos de cobranza, sólo por
  concepto) y el caso 36.

### 9.5 El proceso de causación (feature 024, fase 3, 2026-11-16)

Decisión del dueño 6: «borrador libre; en firme con cuatro ojos». En `/cartera/causacion` la persona contabiliza, por rango y
concepto, lo causado de los conceptos en «Manual» (§9.3). **No recalcula intereses**: el motor ya los causó día a día y el valor
de cada hecho es el del libro que lo nombra (`LND_LoanTransactions.MessagePublicId`); el proceso decide **qué** se contabiliza y
**cuándo**, con control.

**1. Armar el borrador** («Nuevo proceso», `POST /api/lending/accrual-processes`, 201):

- **Periodicidad**: mensual (del 1 al último día), quincenal (primera quincena del 1 al 15, segunda del 16 al fin de mes), decadal
  (1–10, 11–20, 21–fin), semanal (lunes a domingo) o diaria. El rango de **días causados** se propone desde el período elegido y se
  ajusta, sin pasar del largo de su periodicidad (31, 16, 11, 7 o 1 días), dentro de un mismo mes en la mensual, la quincenal y la
  decadal (la semana puede cruzarlo) y nunca después de hoy (`Lending.AccrualProcess.RangeInvalid`; el reconocimiento de la cuota del
  período, §9.6, elige el ciclo, que puede terminar después de hoy).
- **Conceptos**: corriente, mora, seguro, administración y otros rubros (la corrección monetaria UVR). Sólo los que hoy están en
  «Manual» o todavía tienen algo esperando en el rango (`Lending.AccrualProcess.ConceptNotManual`).
- **Filtros opcionales**: oficina, línea y pagaduría (la del vínculo de libranza vigente). Quien no tiene alcance total de oficinas
  (o de líneas) tiene que elegir una suya (`ScopeFilterRequired`); un proceso se ve y se opera sólo si su oficina y su línea están en
  el alcance de quien lo abre.
- Cartera activada (`Lending.NotActivated`), **un solo proceso abierto** (borrador o esperando aprobación) por concepto en la
  cooperativa (`AnotherOpen`) y **ninguno en firme del mismo concepto** cuyo rango y filtros se crucen (`Overlaps`; dos oficinas,
  líneas o pagadurías distintas no se cruzan). Calcular, pedir la firma y ponerla se hacen uno a la vez (el cerrojo de `LND_Setup`
  compartido y la fila de numeración de los procesos, `PCA-`).

**2. Revisar el borrador**. La unidad que se contabiliza es el **hecho**: lo causado de un día, una sucursal y un concepto, un solo
mensaje con todos sus créditos (por eso la oficina filtra exacto y la línea o la pagaduría no pueden partirlo). Por hecho dice si se
contabiliza o queda fuera, con su motivo:

| Motivo | ¿Impide ponerlo en firme? | Qué hacer |
|---|---|---|
| Mes contable cerrado o inexistente; contabilidad sin iniciar | No: lo demás se contabiliza y éste sigue esperando | Reabrir o crear el mes en Contabilidad y reprocesar |
| Mes ya cerrado por Cartera | No | Reabrir el cierre de Cartera y reprocesar, o un lote manual de Cartera |
| Falta la regla contable con `Contabilidad.FaltaDeRegla = Bloquear` | **Sí** | Completar la matriz y recalcular (con «Advertir» entra con un aviso) |
| Contabilidad no respondió con `Contabilidad.PoliticaSinRespuesta = Bloquear` | **Sí** | Recalcular |
| Fuera de su alcance; comparte el hecho con créditos fuera del filtro | No | Quien tenga el alcance, o sin ese filtro |
| Con abonos posteriores (sólo en un reproceso) | No | Queda intacto (ver 5) |

La falta de regla se pregunta a Contabilidad una vez por combinación de tipo, sucursal y dimensiones (las de las partes), con el hecho
más pequeño de cada una. Además hay **bloqueos del proceso**: la causación del motor **al día hasta el último día del rango**
(`AccrualBehind`, con cuántos créditos y el más atrasado: el ciclo diario va primero) y, si falta, la **usura** de esos días por
modalidad (`UsuryMissing`). Los totales van por concepto y por sucursal, y «Por crédito» lista lo de cada crédito y concepto con su
estado y motivo. **Recalcular** (con lo mismo o con otros parámetros) reemplaza el borrador; con el mismo resultado da la misma
huella. El borrador no contabiliza nada; se anula con motivo.

**3. Poner en firme** («Pedir que se ponga en firme», `POST …/{id}/confirm`, `Lending.AccrualProcesses.Confirm`, con motivo y la
huella del borrador que se revisó): se recalcula y tiene que dar la **misma huella** (si no, `Stale`: llegó causación nueva, otro lote
tomó algo o cambió un mes o una regla), sin bloqueos (`Blocked`, dice cuáles) y con algo para contabilizar (`NothingToPost`). Queda
**esperando la aprobación** de la plataforma (sujeto `LendingAccrualProcess`, regla fija de un nivel con
`Lending.AccrualProcesses.Confirm`; si la cooperativa le pone una política que no pide aprobación, `ApprovalPolicyNotRequired`).
**Otra persona** —ni quien lo pidió, ni quien lo armó o lo recalculó por última vez, ni otro usuario de la misma persona
(`SelfApproval`)— lo aprueba **presente en el mismo equipo** con su llave o su código; desde la bandeja con su propia sesión no
(`ApproverNotPresent`). Al aprobar, en la misma transacción, se recalcula otra vez, se toma el cerrojo y se ordena un **lote manual**
(`COR_IntegrationBatches`, a nombre de quien pidió la firma) con **exactamente** los hechos que entraron; el proceso queda **en firme**
con el lote, quién aprobó y cómo, y la auditoría `Lending.AccrualProcess.Confirmed`. Si algo cambió, la aprobación no queda. Los
comprobantes salen por día (y por concepto con `Contabilidad.CausacionSeparadaPorConcepto`, §9.2) con el tipo que mapee la empresa;
la pantalla los muestra con los rechazos («Actualizar el lote»). Rechazar o retirar la solicitud, o recalcular, devuelve el proceso a
borrador.

**4. Un proceso en firme no se anula ni se borra**: se reprocesa.

**5. Reprocesar** (`POST …/{id}/reprocess`, `Lending.AccrualProcesses.Reprocess`, con motivo; su firma pide además ese permiso y
otra persona igual que en 3). Arma un borrador nuevo con lo mismo —clase, periodicidad, rango, conceptos y filtros, que no se
cambian— y lo calcula. **Qué se recalcula y qué no**:

- Entra lo que **todavía espera** en ese alcance: lo que el proceso reprocesado dejó fuera con un motivo que ya se corrigió (un mes
  reabierto, una regla que faltaba) y lo causado que llegó después al rango (la puesta al día de un crédito). Es la **diferencia**
  entre lo que el libro tiene causado en el rango y lo que ya se contabilizó.
- **No** se vuelve a contabilizar, ni se reversa, lo que el reprocesado contabilizó, y los valores no se recalculan: el libro manda.
  Las correcciones de lo causado por una fecha valor o por un valor nacional corregido no pasan por aquí: viajan con su pago o con su
  corrida (`AjusteDeCausacionPorFechaValor`) y se contabilizan solas.
- Los créditos con **abonos** (pagos) registrados después de la puesta en firme del reprocesado quedan **intactos** y se listan
  (`LaterPayment`), aunque no tengan nada pendiente; un hecho que los lleva queda fuera entero y sus demás créditos dicen «va en un
  hecho que quedó fuera». Lo de ellos que siga esperando lo toma un lote manual de Cartera.
- Cada línea dice lo que el reprocesado contabilizó de ese crédito y concepto («Contabilizado antes»).
- Puesto en firme, el reprocesado queda **Reprocesado** y apunta a su reproceso.

**Las clases**. La cabecera, el borrador (hechos y líneas), los filtros, el alcance, el cerrojo, la huella y los cuatro ojos son
comunes (`ProcesosDeCausacion`); cada **clase** (`AccrualProcessKind`) dice cómo se calcula y qué hace al ponerse en firme
(`IClaseDeProcesoDeCausacion`). `Posting` (`ContabilizacionDeLoCausado`) es la de esta sección. La fase 4 trajo `PeriodRecognition`
(`ReconocimientoDelPeriodo`, §9.6): su cálculo escribe en las líneas lo que reconoce de cada crédito, con la cuota y el «desde» (sin
mensaje, porque el hecho aún no existe), y su puesta en firme escribe el libro, emite los hechos y ordena el lote en la misma transacción.

Tablas (migración aditiva `ProcesoDeCausacion`, en los dos motores): `LND_AccrualProcesses` (la cabecera, número `PCA-`),
`LND_AccrualProcessFacts` (los hechos del borrador) y `LND_AccrualProcessLines` (por crédito y concepto, `bigint`). Un recálculo da
de baja (lógica) el borrador anterior. **Pendiente de validar** con la contadora: el orden «contabilizar la causación antes de cerrar
el mes de Cartera» (un mes ya cerrado por Cartera queda fuera del proceso); y la e2e de la fase.

### 9.6 Reconocimiento al inicio del ciclo (feature 024, fase 4, 2026-11-16)

Decisión del dueño: la cooperativa puede reconocer **al empezar el período** de cada cuota lo que esa cuota va a causar, en vez de
reconocerlo noche a noche. **El cálculo no cambia**: el motor sigue causando día a día (§3) y la mora sigue naciendo sólo al vencer.
Lo que cambia es **cuándo y contra qué se contabiliza**. De fábrica nada cambia.

**Los dos parámetros** (`/cartera/parametros`, grupo «Causacion», con vigencia; los dos **pendientes de validar por la contadora**):

| Parámetro | Valores | Ámbito |
|---|---|---|
| `Causacion.Momento` | «Diaria» (de fábrica): lo causado se reconoce cada noche, como siempre. «InicioDelCiclo»: se reconoce la cuota del período al empezarlo | Empresa, con excepción por línea |
| `Causacion.InicioDelCiclo.Reconocimiento` | «IngresoDiferido» (de fábrica) o «IngresoDirecto» | Sólo empresa |

**Qué se reconoce y cuándo**

- El **primer día del período** de cada cuota (el día siguiente a su inicio, que es el vencimiento de la anterior o el desembolso), lo que
  esa cuota **va a causar hasta su vencimiento** en interés corriente, seguro y administración: la misma causación del motor proyectada
  con lo conocido ese día (tasa, usura, tope de vivienda y tasa general de mora de ese día; los abonos que vengan después no se suponen).
  Un seguro «al vencer» se reconoce entero. El **capital no tiene asiento** (ya está en la cartera) y la **mora nunca** se reconoce.
- Con el concepto en **«Automatica»** (`Causacion.Modo*`, §9.3) lo reconoce **el ciclo diario** ese mismo día, en la misma transacción que
  la causación del día. Con el concepto en **«Manual»** lo reconoce **el proceso de causación** con la clase «Reconocer la cuota del
  período» (abajo): borrador, cuatro ojos y en firme. Corrido a mitad del período, el proceso reconoce desde el día siguiente al último
  causado (lo de antes ya pasó por la causación diaria).
- No se reconoce: un crédito en UVR, castigado o con la causación suspendida (lo causado va a orden, no es ingreso), una cuota ya
  vencida o ya reconocida. Si el parámetro entra en vigencia a mitad de un período, ese período sigue diario hasta su vencimiento, salvo
  que el concepto se ponga en «Manual» y lo reconozca el proceso.
- El modo queda **sellado en el concepto** de lo reconocido (`RecognizedInterest`, `RecognizedInsurance`, `RecognizedAdministration`
  contra el diferido; `…Direct` contra el ingreso): cambiar `Causacion.InicioDelCiclo.Reconocimiento` rige para lo que se reconozca
  desde su vigencia, nunca para lo ya reconocido.

**En el libro** (`LND_LoanTransactions`, escrito sólo por `RegistroDeCartera`)

- El reconocimiento es una fila `PeriodRecognition` (clase 22) por cuota y rubro, con el valor positivo. Cada día, la causación de
  siempre lleva además una fila `Accrual` **negativa** en el concepto reconocido por lo mismo que causa: es el **consumo**. Al vencer, lo
  reconocido queda en cero.
- Lo reconocido **no es saldo**: no lo cobra un pago, no entra al valor para cancelar ni a la altura de mora. Es «cuota reconocida por
  causar».
- **El libro sigue siendo la reconstrucción**: el reconocimiento es una entrada (como un pago) que la reconstrucción vuelve a leer con lo
  conocido en R, y el consumo y las devoluciones se reproducen igual cada vez. La verificación del libro y la conciliación cuadran como
  antes.

**En contabilidad**: el hecho nuevo `CuotaReconocida` (`CUR`, por lotes al fin de la causación, por partes como la causación). Ningún
hecho lleva cuentas: la matriz resuelve el rol nuevo **`IngresoDiferidoDeLaCuota`** (crédito, por modalidad y concepto; tercero el
deudor, cruce el pagaré). El comprobante sugerido es **`CR` «Cuota reconocida del período»**, sembrado activo y **sin mapear**: mientras
la empresa no lo elija en `/contabilidad/cartera/tipos-de-comprobante`, va en `CA`.

| Momento | Ingreso diferido | Ingreso directo |
|---|---|---|
| Al reconocer (`CuotaReconocida`) | Débito: cuenta por cobrar del rubro (`InteresPorCobrar`, `PagoPorCuentaDelAsociado` del seguro, `CargoPorCobrar` de la administración). Crédito: `IngresoDiferidoDeLaCuota` | Débito: la misma cuenta por cobrar. Crédito: `Ingreso` del rubro (el seguro, `SeguroPorPagar` con la aseguradora) |
| Cada día (`InteresCausado`, `SeguroCausado`, `AdministracionCausada`) | La causación de siempre (cuenta por cobrar contra ingreso) y el consumo (crédito a la cuenta por cobrar contra débito al diferido): neto, el diferido pasa al ingreso (el seguro, a su cuenta por pagar) y la cuenta por cobrar no se mueve | La causación y su anulación en el mismo hecho: neto cero, no vuelve a reconocer ingreso |
| Lo que no se causó (abajo) | Crédito a la cuenta por cobrar contra débito al diferido | Crédito a la cuenta por cobrar contra débito al ingreso (el seguro, a su cuenta por pagar) |

**Ejemplo con cifras** (caso dorado 37): 1.200.000 a 6 cuotas mensuales al 24 % EA desde el 15-01-2027, seguro de vida al vencer del
0,06 % sobre el saldo y administración diaria del 0,15 % sobre el monto. La cuota 1 se paga el 15-02. El ciclo del **16-02** reconoce la
cuota 2 (que vence el 15-03): interés 18.247,74, seguro 605,31 y administración 1.800.

- 16-02, `CuotaReconocida`: débito interés por cobrar 18.247,74, seguro por cobrar 605,31 y administración por cobrar 1.800; crédito
  ingreso diferido de la cuota 20.653,05.
- 16-02, la causación del día: interés 603,00 y administración 60. En su `InteresCausado`: débito interés por cobrar 603,00 contra
  ingreso 603,00 y, por el consumo, crédito interés por cobrar 603,00 contra débito al diferido 603,00. Igual la administración con 60.
- El 18-02 el estado de cuenta dice 1.810,09 de interés causado y 16.437,65 reconocidos por causar (1.620 de administración).
- El 15-03 el seguro se causa entero y consume sus 605,31; el diferido queda en cero.

Con **ingreso directo** (caso dorado 38), el mismo crédito con los conceptos en «Manual»: el proceso reconoce el **20-02** lo que la cuota 2
va a causar de ese día a su vencimiento —interés 15.833,57, seguro 605,31 y administración 1.560—, todo al ingreso (el seguro, a su cuenta
por pagar) ese día; lo causado del 16 al 19 ya había pasado por la causación diaria.

**Ajustes: lo que se reconoció y no se causó**

- **Al vencer**: si la cuota causó menos de lo reconocido (un abono extraordinario que bajó el saldo, una usura que bajó), la diferencia
  se devuelve el día del vencimiento con una fila `RecognitionRelease` (clase 23) que va en la causación de ese día. Caso 37,
  «abono-a-capital»: un abono de 300.000 el 01-03 que reduce la cuota hace causar 15.646,17 de interés; el 15-03 se devuelven 2.601,57.
- **Al cancelar antes del vencimiento** con `Pagos.InteresesAlPagarAntesDelVencimiento = Corrido`: lo reconocido que no se causó se
  devuelve ese día, con la operación que canceló, en el comprobante del pago (su acompañante `AjusteDeCausacionPorFechaValor`). Caso 38,
  «corrido»: la cancelación del 01-03 devuelve 8.556,33 de interés, 605,31 de seguro y 840 de administración.
- Con **`CuotaCompleta`** el pago cobra el interés de la cuota en curso entero (la fila del interés que faltaba por causar) y esa fila
  **consume** lo reconocido del interés: no se devuelve. El seguro al vencer y la administración no causada sí se devuelven.
- **Abonar a la cuota reconocida** antes de su vencimiento (pago de la cuota del ciclo) queda como anticipo y se aplica al vencer, como
  siempre: el **capital se abona al vencimiento**. Ya no sale el aviso «el anticipo no abona a capital ni reduce intereses»: con la cuota
  reconocida eso es lo esperado. Caso 37, «pago-anticipado». Un abono extraordinario a capital sí abona de inmediato, y lo que deje de
  causarse se devuelve al vencer.

**Caja y estado de cuenta**: la caja muestra en la obligación la cuota reconocida (columna «Reconocido» y la marca «reconocida» en la
cuota del ciclo) y, al elegir la persona o con **F4** (precargar lo exigible), la cuota del ciclo entra completa como exigible aunque
todavía no venza (si no hay nada vencido); la mora sigue siendo sólo lo vencido. El estado de cuenta de la persona tiene la columna elegible «Cuota reconocida por causar» y la
consulta del crédito la devuelve (`recognizedPending`). Ver `docs/manual/recaudo-manual-del-cajero.md` y
`docs/manual/cartera-estado-de-cuenta.md`.

**El proceso con la clase «Reconocer la cuota del período»** (`/cartera/causacion`, «Nuevo proceso», se elige la clase sólo si alguna
línea reconoce al inicio del ciclo; `Kind = PeriodRecognition` en la API). Lo común es el de §9.5 (borrador, huella, filtros, alcance,
cuatro ojos, numeración `PCA-`). Lo propio:

- El rango es **el ciclo**: puede terminar después de hoy, pero tiene que haber empezado. Los conceptos, sólo interés corriente, seguro y
  administración (`Lending.AccrualProcess.ConceptNotRecognizable`), en «Manual».
- Por crédito, la cuota cuyo período se cruza con el rango, **desde** cuándo se reconoce y el valor por rubro. Quedan fuera con su
  motivo: ya reconocida, el ciclo todavía no empezó, causación suspendida, castigado, crédito no activo, en UVR, la línea reconoce cada
  día, causación atrasada, mes contable cerrado o del mes cerrado por Cartera.
- Puesto en firme se vuelve a calcular (si un crédito cambió, `Stale`), se escribe el libro en una corrida de Cartera (`PeriodRecognition`)
  y se emiten los hechos `CuotaReconocida` por día y oficina, con la validación previa de la falta de regla (§9.5) y un lote manual con
  esas entregas, todo en la misma transacción. Dos procesos de esta clase que se cruzan no se arman (`Overlaps`).

**La conciliación** (`/cartera/conciliacion-contable`) suma a cada cuenta por cobrar lo reconocido por causar de su rubro y compara el
`IngresoDiferidoDeLaCuota` con lo reconocido por causar contra el diferido; de fábrica no hay nada que sumar.

**Migración** aditiva `ReconocimientoAlInicioDelCiclo` (`20261116174125` en SQL Server, `20261116174212` en PostgreSQL): la cuota y el
«desde» de cada línea del proceso (`LND_AccrualProcessLines.InstallmentNumber` y `RecognitionDate`). El hecho, el tipo de comprobante
`CR` y su mapeo a `CA` son semilla.

**Limitaciones conocidas** (para revisar con la contadora y el dueño):

- Lo reconocido que devuelve una operación que **no es un pago** (una condonación total, una novación o refinanciación, un saldo a favor
  aplicado que cancela) se escribe en el libro con esa operación, pero su hecho no lo lleva: la conciliación lo muestra como diferencia
  hasta que se ajuste a mano.
- La reversión de un desembolso con una cuota reconocida, la reclasificación por calificación y el cambio de dimensiones contables no
  trasladan lo reconocido por causar.
- Los créditos cancelados no aparecen en el proceso de reconocimiento; los castigados, suspendidos y en UVR se causan cada día como
  siempre.
- **Pendiente de validar por la contadora**: el esquema contable de las dos variantes, las cuentas CUIF del ingreso diferido de la cuota
  (la matriz no siembra ninguna; la plantilla sugiere las filas por modalidad y rubro, y la completitud las pide en cuanto alguna línea
  reconoce al inicio del ciclo contra el diferido), si el seguro debe ir a un diferido propio, y los casos dorados 37 y 38 y los
  contables 60 a 63.

### 9.7 Recálculo de la carga desde el corte (feature 025, fase 3, 2026-11-24)

Una cartera cargada nace con el interés **desde el corte** (D5 de la 013): la primera cuota posterior al corte no lleva los días entre su
vencimiento anterior y el corte, y si la tasa se trajo en E.A. redondeada (23,87 %) deja de ser la mensual pactada (1,8 % MV). El proceso
de causación con la clase **«Recalcular la carga desde el corte»** (`/cartera/causacion`, `Kind = CutoverRecalculation`; se ofrece mientras
haya créditos cargados sin recalcular) lo corrige **sin borrar nada** (D1, D2):

- **Qué toma**: los créditos nacidos de la carga de migración, vivos, del alcance y los filtros. Quedan fuera con su motivo: castigado,
  cancelado, en UVR, con la causación suspendida, de pago único, ya recalculado (`Lending.Cutover.AlreadyAdjusted` si se intenta dos
  veces), sin cuota sin vencer donde cobrar o sin nada que cambiar.
- **La tasa (HU3)**: si la E.A. sellada está a menos de 0,005 puntos de una MV redonda con dos decimales, se sella una versión de
  condiciones nueva en esa MV (30/360 lineal), con la causa `CutoverCorrection` y **vigencia desde el corte**. Si no, conserva su tasa.
- **El interés del período (HU1)**: el de la primera cuota posterior al corte, del vencimiento anterior (o del desembolso de la carga si
  es la cuota 1) a su vencimiento, sobre el capital no vencido al corte, menos lo que esa cuota causa desde el corte y lo que trajo en la
  carga. Si pasa de un peso, se carga como interés corriente en la **primera cuota sin vencer** el día de la firma (fila
  `CutoverInterestAdjustment`, hecho `InteresDelCorte`, código `IDC`, comprobante `CA`, roles de la causación de la corriente: interés por
  cobrar contra ingreso). No genera mora ni cambia la altura ni la calificación: la cuota destino todavía no vence. Ejemplo (caso dorado
  39): 10.000.000 a 1,8 % MV, corte el 31-10, vencimiento el 9: faltan 21 días = **126.000,00**, cobrados en la cuota de diciembre.
- **El recálculo**: la reconstrucción desde el corte con la tasa nueva y los **mismos pagos** se compara con el libro hasta lo causado;
  cada diferencia es una fila nueva —ajuste de causación (`AjusteDeCausacionPorFechaValor`, causa `CutoverRecalculation`) o el reparto de un
  pago (`PagoRedistribuidoPorValorNacional`)—, como la corrección por valor nacional (§6).
- **El borrador** lista por crédito el capital al corte, la tasa sellada y la de desde el corte, los días que faltaron, el interés del
  período, lo ya causado y cargado, el valor y la cuota destino, con totales por hecho y sucursal. No escribe nada: es la medición exacta
  antes de decidir.
- **En firme** (cuatro ojos, `Lending.AccrualProcesses.Confirm`): se vuelve a calcular con los créditos bloqueados (si algo cambió,
  `Stale`), se escriben la versión de condiciones, el interés del corte, las diferencias, una **versión nueva del plan** (el capital no
  cambia; la cuota destino lleva el interés del corte en su interés, así la caja, el estado de cuenta y la consulta del crédito dicen lo
  mismo), las posiciones de los días cambiados y la marca `LND_LoanControls.CutoverAdjustedAt`. La validación previa de la falta de regla
  (`Contabilidad.FaltaDeRegla`) puede detenerlo: la matriz tiene que resolver el `Ingreso` y el `InteresPorCobrar` del interés corriente
  (las filas sugeridas de siempre, hecho `*`). No se reprocesa (`Lending.AccrualProcess.CutoverNotReprocessable`).
- **La noche no lo revierte**: el ajuste y la corrección son entrada de la reconstrucción (`EntradaDeReconstruccion.AjustesDelCorte` y la
  última versión con causa `CutoverCorrection`), así que la causación y la verificación del libro los conservan.
- **De paso** (arreglo): el valor de una cuota al vencer (`AplicadorDePagos.ValorDeCuota`, la cuota del ciclo de la caja y la cuota
  específica) suma el interés que la cuota trajo en la carga y su interés del corte; antes los ignoraba y la caja subestimaba la primera
  cuota de un crédito cargado.

**Migración** aditiva `AjusteDelCorte` (`20261124191735` en SQL Server, `20261124191843` en PostgreSQL):
`LND_LoanControls.CutoverAdjustedAt` y `LND_AccrualProcessLines.Detail` (el detalle por crédito del borrador). El hecho, su mapeo a `CA` y
su tipo son semilla. Casos dorados 39 (día 9, día 30, interés cargado, cuota destino vencida y quincenal) y 44 (recálculo con pagos).

**Pendiente**: la contadora revisa el borrador de producción crédito por crédito antes de la firma (SC-002). La fase 4 (§9.8) reusa el
ajuste, el hecho y la marca para que una carga nueva cause completo el período en curso.

### 9.8 La carga causa completo el período en curso (feature 025, fase 4, 2026-11-24)

Una cooperativa nueva no tiene que pasar por el recálculo de §9.7: la carga de migración (`/cartera/migracion` → «Carga nueva») trae la
opción **«El período en curso al corte se causa completo»**, marcada por defecto (D5 de la 025; `LND_MigrationLoads.CauseFullInProgressPeriod`,
`causeFullInProgressPeriod` en `POST /api/lending/migration/loads`). Con ella, al **activar**:

- **El plan**: la primera cuota que vence después del corte toma como inicio de su período el vencimiento de la cuota cargada anterior; si no
  la hay y es la cuota 1, el desembolso de la fila; si no, su vencimiento menos un período. El motor sigue causando **desde el corte**.
- **Lo anterior al corte** (el interés del período completo, menos lo que el motor causa desde el corte hasta su vencimiento, menos el
  interés corriente que la cuota trajo en la carga, que **cuenta como causado**; si pasa de un peso) entra **el día de la activación**
  (C+1), como **ingreso de ese día**, en esa misma cuota (todavía no vence: sin mora): la misma fila `CutoverInterestAdjustment` y el mismo
  hecho `InteresDelCorte` (`IDC`, comprobante `CA`) de §9.7, de una corrida `CutoverRecalculation` a nombre de quien activa, y una versión
  nueva del plan con el interés completo en esa cuota. Se calcula con la misma regla de §9.7 sobre la reconstrucción, con la firma el día
  del corte.
- **No se cuadra contra el balance al corte**: es ingreso del día de la activación, no un saldo a C. El cuadre de la carga sigue igual.
- **La marca**: cada crédito evaluado queda con `LND_LoanControls.CutoverAdjustedAt`, sume o no, así que el recálculo de §9.7 no lo vuelve a
  tomar (la HU1 y la HU4 no se suman). Quedan como hoy, sin ajuste ni marca, los castigados, los de causación suspendida al corte o que
  llegan con intereses en suspenso (lo de
  antes del corte no es ingreso de un crédito en suspenso), los de UVR, los de pago único y los que no se pudieron reconstruir; el recálculo
  puede tomarlos después.
- **La noche no lo revierte**: el ajuste es entrada de la reconstrucción, como en §9.7.
- **La vista previa**: en la pestaña «Partes» de la carga, «Período en curso al corte» lista por crédito la cuota, el inicio del período,
  los días antes del corte, el interés del período completo, lo que se causa desde el corte, lo cargado y lo que suma, con el total
  (`GET /api/lending/migration/loads/{id}/full-period-interest`, permiso `Lending.Migration.Load`). Es una estimación sin reconstrucción:
  deja fuera lo que llega con intereses en suspenso; la activación responde lo que de verdad sumó (`fullPeriodInterest`) y lo sella en los
  conteos de la activación (`fullPeriod`).
- **Sin la opción**, el comportamiento de siempre (D5 de la 013): el interés nace desde el corte. Una carga creada antes de la migración
  `CargaConPeriodoCompleto` queda sin la opción; para usarla, se crea una carga nueva (reemplaza al borrador).

Ejemplo (caso dorado `26-credito-migrado-periodo-completo`): 400.000 a 25 % E.A. compuesta, corte el 30-06, cuota 5 que vence el 10-07 con
4.000 cargados: período completo del 10-06 al 10-07 = 7.507,71; desde el corte el motor causa 2.487,07; la activación suma **1.020,64** el
01-07. Si la matriz no resuelve el `InteresDelCorte` y `Contabilidad.FaltaDeRegla = Bloquear`, la activación responde
`Lending.Activation.FullPeriodNotPostable`: se completa la matriz o se carga sin la opción.

### 9.9 Causación por período calendario (feature 025, fase 5, 2026-11-24)

Casi todas las herramientas de origen causan **por período del calendario**: el día 1 queda causada la cuota que vence en el mes y la caja la
cobra completa. El parámetro **`Causacion.Periodo`** (Parámetros de Cartera → Causación; general con excepción por línea, como
`Causacion.Momento`) lo permite:

- **`CicloDeLaCuota`** (de fábrica): lo de siempre. El período de una cuota va del vencimiento anterior al suyo.
- **`PeriodoCalendario`**: el período de una cuota es el **tramo del calendario que contiene su vencimiento** —mes del 1 al último día;
  quincena del 1 al 15 y del 16 al fin; década del 1 al 10, del 11 al 20 y del 21 al fin; semana de lunes a domingo; las periodicidades de más de
  un mes y el pago único, el mes—. Los límites salen de la periodicidad, nunca de un número escrito (`PeriodoDeCausacion.TramoQueContiene`).

El cálculo sigue siendo día a día y **el monto de la cuota no cambia**. Lo que cambia con `PeriodoCalendario`:

- **Reconocimiento** (con `Causacion.Momento = InicioDelCiclo`, §9.6): el ciclo diario reconoce la cuota el **primer día de su tramo**
  (o el primero de su ciclo, si el desembolso fue a mitad del tramo), no el día siguiente al vencimiento anterior. Lo reconocido es lo que la
  cuota va a causar desde ese día hasta su vencimiento (lo anterior ya está causado).
- **Proceso de causación** (§9.5, clase «Reconocer la cuota del período»): con rango del 1 al 30 de noviembre toma las cuotas que **vencen**
  en el rango, no las cuyo ciclo se cruza con él. Si el tramo de la cuota empieza después de hoy, queda fuera con `PeriodNotStarted` («Su
  período empieza después de hoy») en vez de `CycleNotStarted`.
- **Filtros de la herramienta anterior** (FR-018; migración `FiltrosDelProcesoDeCausacion`, columnas `PaymentForm`, `InstallmentScope` y
  `DiscountCycle` de `LND_AccrualProcesses`): al reconocer se puede filtrar por **forma de pago** (nómina = nómina propia y libranza; caja = lo
  demás, por el vínculo vigente), **qué causa** (todo, sin extras o sólo extras: las cuotas con prima) y **ciclo de descuento** (primera o segunda
  quincena, una de las tres décadas o una semana del mes, por el día en que vence la cuota). Son exactos porque cada línea del reconocimiento es
  de un crédito y una cuota; al contabilizar lo causado no se admiten (`Lending.AccrualProcess.FiltersOnlyForRecognition`): un hecho junta créditos
  y no se parte. La pantalla y el servidor validan los valores.
- **Caja y estado de cuenta**: la «cuota del período» es la que vence en el **tramo en curso**. El 24-11, en un crédito que vence el 9 y ya
  pagó la de noviembre, no hay cuota del período por cobrar: pagar la de diciembre ese día es un **anticipo** (modalidad anticipo o cuota
  específica), y la cuota del ciclo devuelve el valor como excedente. El detalle de la obligación lo avisa.
- **Pagos dentro del período (D3)**:
  - Un **abono** (todo pago que no cancela: cuota, anticipo, abono a capital, valor libre) cubre **primero lo causado de la cuota del tramo en
    curso**, como si fuera exigible, y después lo que diga su modalidad. La cuota del ciclo no vuelve a anticipar lo que ya cubrió.
  - Una **cancelación** la decide `Pagos.InteresesAlPagarAntesDelVencimiento`, que en pantalla se lee «¿Devuelve los días no corridos al
    cancelar?»: **Sí** (`Corrido`, de fábrica) cobra lo causado hasta la fecha de pago y devuelve lo reconocido que no corrió (la devolución de la
    024); **No** (`CuotaCompleta`) cobra completo el interés de la cuota del tramo causado. Como hoy, en vivienda y dentro del tope legal del
    prepago (Ley 1555) siempre se devuelven.
  - Lo causado día a día de una cuota cuyo **tramo no ha empezado** (el 24-11, lo de diciembre causado desde el 10-11) **nunca se cobra**: la
    cancelación lo devuelve en su fecha valor (`RecognitionRelease` negativo en el interés corriente de esa cuota, de la operación que cancela;
    pasa a Contabilidad con el ajuste de causación del pago). Si los fondos no alcanzan para cancelar, es la cancelación parcial de siempre.
  - Lo sella cada pago en su decisión (`AccrualPeriod = "PeriodoCalendario"`; nulo por el ciclo de la cuota) para que la reconstrucción lo repita
    aunque el parámetro cambie después. Un crédito en UVR va siempre por el ciclo de su cuota. El saldo a favor aplicado como abono no lo usa.

Casos dorados (calculados con `calcular.py`): `45-periodo-calendario-mensual` (1,8 % MV sobre 10.000.000, días 9: la cuota 2 se reconoce el
01-03 por 49.928,29 —9 días— y la caja del 03-03 cubre 133.142,12 de causado y deja 800.877,65 de anticipo), `46-periodo-calendario-quincenal`
(reconocimientos el 01-02 y el 16-02), `47-cancelacion-periodo-causado` (el 05-03, corrido 144.237,29 o cuota completa 166.427,64; el 24-02,
83.213,82 no cobrados con las dos opciones), `48-abono-a-mitad-de-periodo` (de 1.000.000 el 05-03, 144.237,29 al interés y 855.762,71 a capital) y
`49-pago-el-24-es-anticipo`.

Pendiente de validar por la contadora: que los abonos cubran primero lo causado del tramo (no el reconocido todavía por causar, que se
cobra como anticipo de la cuota) y que lo no cobrado de un tramo sin empezar se lleve como ajuste de causación del pago.
