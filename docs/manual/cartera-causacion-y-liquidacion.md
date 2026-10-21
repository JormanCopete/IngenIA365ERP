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
| Corridas del proceso: ver | `GET /api/lending/process-runs` | `Lending.Processes.View` |
| Ordenar o reintentar el ciclo de un día | `POST /api/lending/process-runs` · `…/{id}/retry` | `Lending.Processes.Run` |
| Verificar el libro contra la reconstrucción | `POST /api/lending/integrity/verify` | `Lending.Integrity.Verify` |

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
  clave de usura sale del catálogo de modalidades vigente). La mora (1,5 veces la corriente, o la de la línea)
  también se topa. Si un día se topa, el crédito queda marcado («Topado en la usura») y se alerta
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
26 (crédito migrado con suspenso), 28 (seguro sobre saldo) y 30 (desembolso por tramos).
