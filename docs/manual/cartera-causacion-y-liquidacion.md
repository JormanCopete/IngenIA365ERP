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
  decadal (la semana puede cruzarlo) y nunca después de hoy (`Lending.AccrualProcess.RangeInvalid`).
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

**Punto de extensión (fase 4)**. La cabecera, el borrador (hechos y líneas), los filtros, el alcance, el cerrojo, la huella y los cuatro
ojos son comunes (`ProcesosDeCausacion`); cada **clase** (`AccrualProcessKind`) dice cómo se calcula y qué hace al ponerse en firme
(`IClaseDeProcesoDeCausacion`). Esta fase trae `Posting` (`ContabilizacionDeLoCausado`). El reconocimiento de la cuota completa al
inicio del ciclo será otra clase (`PeriodRecognition`): su cálculo escribe en las líneas lo que reconoce de cada crédito (sin mensaje,
porque el hecho aún no existe) y su puesta en firme emite los hechos y ordena el lote en la misma transacción.

Tablas (migración aditiva `ProcesoDeCausacion`, en los dos motores): `LND_AccrualProcesses` (la cabecera, número `PCA-`),
`LND_AccrualProcessFacts` (los hechos del borrador) y `LND_AccrualProcessLines` (por crédito y concepto, `bigint`). Un recálculo da
de baja (lógica) el borrador anterior. **Pendiente de validar** con la contadora: el orden «contabilizar la causación antes de cerrar
el mes de Cartera» (un mes ya cerrado por Cartera queda fuera del proceso); y la e2e de la fase.
