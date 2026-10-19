# Captaciones y aportes: la integración contable

> Feature 014, US8 (transversal a K7–K11). Para la contadora. Complementa
> [cartera-integracion-contable.md](cartera-integracion-contable.md) (la matriz de la 013, que es la misma) y
> [contabilidad-contrato-de-contabilizacion.md](contabilidad-contrato-de-contabilizacion.md) §9. El contrato es
> `specs/014-ahorros-aportes-cdt/contracts/contabilidad.md` y `mensajes.md`, con las enmiendas de `research.md` §7 y §15.

## 1. Las reglas que no cambian

- **Cartera no conoce cuentas ni escribe asientos.** Cada operación de una cuenta emite su hecho (`CAR → Accounting`) en la misma transacción;
  ningún hecho lleva una cuenta contable. Contabilidad lo asienta por la **matriz de Cartera** (`/contabilidad/cartera/matriz`), por el único
  camino al libro (`AccountingPoster`).
- **Lo de Cartera no se reversa desde Comprobantes**: el comprobante de captaciones responde «se corrige en la Cartera con el reverso de la
  operación de la cuenta» y, si es un resumido, dice cuántas cuentas reúne. El reverso es otro hecho (`OperacionDeCuentaReversada`) con el
  **tipo de comprobante de su original**.
- **Un mes no se cierra** en Contabilidad si Cartera no cerró, y con captaciones activadas el aviso dice desde qué día está pendiente la
  causación de captaciones.
- La plantilla de la matriz **sugiere** cuentas del CUIF pero **no siembra ninguna regla**: las sugerencias son dato
  (`sugerencias-cuif-captaciones.json`) y la contadora las confirma o cambia.

## 2. Dimensiones y roles nuevos

La clave de la matriz suma **cinco dimensiones** al final: naturaleza de la línea (colocación, captación, aportes), clase de captación (a la vista,
contractual, permanente, CDAT), plazo de emisión (menos de 6 meses, 6 a 12, 12 a 18, 18 o más), clase de aporte (mínimo, ordinario,
extraordinario, revalorizado) y marca de la cuenta (`Standard`, `GmfExempt`, `Garnished`; manda el embargo, luego la exención). Una dimensión
que el rol exige no puede ir en `*`. La **inactividad no es una marca**: cambia el rol del capital.

Los **17 roles nuevos** (familias «Captaciones» y «Aportes y fondos» en la pantalla):

| Rol | Qué lleva | Cuenta CUIF sugerida (pendiente de validar) |
|---|---|---|
| `CapitalDeCaptacion` | el depósito (exige clase y marca; en CDAT y contractual, plazo de emisión) | a la vista 210505; CDAT 211005 a 211020 por plazo; contractual 212505 (hasta 12 meses) y 212515 (más); permanente 213005 o 213010 |
| `CapitalDeCaptacionInactiva` | el depósito de una cuenta inactiva | a la vista 210510; contractual 212510 y 212520 |
| `InteresPorPagar` | intereses causados y no abonados | 210595, 211095, 212595, 213095 por clase |
| `GastoPorIntereses` | gasto de la causación y sus ajustes | 615005, 615010, 615015, 615020 por clase |
| `RetencionPorPagar` | retención practicada | 243535 |
| `GmfPorPagar` | GMF retenido, por la clase del producto que se debita | 243005 a 243020 por clase |
| `GastoPorBonificacion` | bonificación del contractual | 615015 |
| `IngresoPorCargos` | cargos de cuenta (por clase de cargo) | sin sugerencia |
| `CargoDeCuentaPorCobrar` | cargo causado que la cuenta no alcanzó a pagar | sin sugerencia |
| `AportesSociales` | aportes, por clase | ordinarios y revalorizados 310505, extraordinarios 310510, mínimos 3110 |
| `FondoDeRevalorizacion` | fondo con que la asamblea revaloriza | 3310 |
| `FondoDeExcedentes` | fondo de excedentes del acta | 3505 |
| `DevolucionPorPagar` | lo que se gira (devolución, cancelación, CDAT, excedentes, entrega por fallecimiento, no reclamados) | aportes 246505; ahorros del retiro 246510 |
| `NoReclamadosPorPagar` | saldos no reclamados | 246595 |
| `EmbargosPorConsignar` | lo consignado al juzgado mientras se gira | sin sugerencia |
| `IngresoPorPenalidad` | penalidad del CDAT redimido o del contractual incumplido | sin sugerencia |
| `PerdidaAbsorbida` | pérdidas descontadas al retiro | 3510 |

## 3. Los hechos y su comprobante

Cinco tipos de comprobante nuevos (`MA` movimientos de cuentas de captación y aportes, `CC` causación de captaciones, `TD` CDAT, `AO` aportes
sociales y asamblea, `RA` retiro de asociados); los heredados `AH` y `CD` quedan inactivos. El mapeo hecho → comprobante es **editable** en
`/contabilidad/cartera/tipos-de-comprobante` y el modo de paso por hecho en el parámetro `Contabilidad.ModoDePaso` (D23).

| Hecho | Cuándo | Comprobante sembrado |
|---|---|---|
| `ConsignacionEnCuentaAplicada` | consignación en una cuenta de captación | `MA` (`DN` si consigna Nómina) |
| `AporteRecibido` | consignación en aportes (cuota o extraordinario) | `AO` (`DN` si consigna Nómina) |
| `RetiroDeCuentaAplicado` | retiro por el contrato, por giro, cancelación o débito automático | `MA` |
| `TrasladoEntreCuentasAplicado` | traslado entre cuentas | `MA` |
| `GmfRetenido` | acompaña a la operación que lo genera | el del principal |
| `InteresDeCaptacionCausado` | causación del ciclo diario, por lotes resumida por día | `CC` |
| `AjusteDeCausacionDeCaptacion` | operación fechada atrás o corrección del proceso | `CC` |
| `InteresDeCaptacionLiquidado` + `RetencionPracticada` | liquidación periódica o manual | `CC` |
| `CdatConstituido`, `InteresesDeCdatPagados`, `CdatProrrogado`, `CdatRenovado`, `CdatRedimido`, `CdatCancelado`, `PenalidadAplicada` | ciclo del CDAT | `TD` |
| `BonificacionReconocida` | bonificación del contractual (provisión, reversión o abono) | `CC` |
| `CaptacionReclasificada` | cambio de plazo de emisión, inactividad, embargo o exención | `CN` |
| `CargoDeCuentaCausado`, `CargoPendienteCobrado`, `CargoDeCuentaCondonado` | cargos | `MA` |
| `RevalorizacionDeAportesAplicada`, `ExcedentesDistribuidos`, `AportesAmortizados` | asamblea (un hecho masivo por decisión) | `AO` |
| `CruceAlRetiroAplicado`, `PerdidaAplicadaAlRetiro`, `DevolucionOrdenada` | retiro del asociado y toda devolución por giro | `RA` |
| `SaldoTrasladadoANoReclamados` | traslado a no reclamados | `MA` |
| `EmbargoConsignado` | consignación al juzgado | `MA` |
| `OperacionDeCuentaReversada` | reverso de cualquiera de los anteriores | el del original |
| `DevolucionGirada`, `CaptacionesActivadas`, `CargaDeCaptacionesAplicada` | informativos | — |

Cómo se arma cada asiento: el importe de cada concepto es el cambio de su saldo (más = Cartera le debe más al titular), cada concepto va a su rol
por su lado natural y lo que no cuadra va a la **contrapartida del hecho**: el puente del recaudador en una consignación, un aporte o un retiro por
el contrato; la devolución por pagar en un giro; el gasto por intereses en la causación; el ingreso por cargos; el puente del débito automático y
del cruce; los fondos de la asamblea. Cada línea lleva el número de la cuenta del asociado como documento cruce `CTA` cuando la cuenta contable
exige documento, y **una línea por cuenta** donde la cuenta exige tercero, cruce o base (también en el resumido).

El cruce al retiro y el pago del crédito van en **un solo comprobante `RA`** (D45); su reverso todavía sale en comprobantes separados, cada uno
cuadrado por el puente (pregunta al dueño).

## 4. Validación previa

Rige lo mismo que en créditos (A8 de la 013) con `Contabilidad.FaltaDeRegla`:

- **`Bloquear`** (el defecto): si a un hecho le falta regla, la pantalla responde que no es contabilizable y no hace nada; el contrato de cuentas
  rechaza la consignación o el retiro; el proceso diario deja la tanda sin avanzar, la cuenta en los errores de la corrida y la alerta
  `Cartera.HechoNoContabilizable`.
- **`Advertir`**: todo se aplica, se sella y alerta.
- La activación de cada naturaleza exige que **todos los hechos que emite desde el primer día** tengan regla para cada línea activa (requisito
  `LinesWithMatrix`): en aportes, el aporte, el cruce y la devolución; en el ahorro a la vista y el permanente, la consignación, el retiro por giro con
  su GMF, la causación, la liquidación y la retención; en CDAT y contractual, sus hechos en cada plazo de emisión que admite la línea.
- La **completitud** (`/contabilidad/cartera/completitud`, filtro por naturaleza) exige por línea las combinaciones de K7 y K8 y avisa lo demás
  (asamblea, no reclamados, embargos y el puente de Tesorería se sugieren pero no se exigen).
- La orden del juez no se detiene por la matriz: la reclasificación por embargo no pasa por la validación previa.

## 5. Conciliación

La vista `deposits-reconciliation` (en `/cartera/conciliacion-contable`, permiso `Lending.Reconciliation.View`, sin datos de titulares) compara
por **conjuntos de cuentas** el libro de cuentas (por fecha contable, rol, línea, naturaleza, clase, marca y oficina) contra los saldos contables,
y explica cada diferencia hecho por hecho. **La retención y el GMF por pagar no se concilian aquí**: en el libro de la cuenta sólo crecen y la
cuenta contable baja al pagar a la DIAN; se concilian con la declaración. El residuo de centavos entre la causación diaria y el interés liquidado al
peso queda en el interés por pagar contable y lo decide la contadora.

## 6. Pendiente de validar

Las cuentas sugeridas, si la cuenta exenta o la embargada llevan otra cuenta contable, el residuo de centavos y la conciliación de retención y GMF:
ver [cartera-captaciones.md](cartera-captaciones.md), «Contabilidad».
