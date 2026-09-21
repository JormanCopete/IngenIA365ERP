# Liquidaciones especiales de nómina (feature 010, entrega N1)

Prima de servicios, cesantías e intereses del año, vacaciones y liquidación definitiva por
retiro. Las cuatro comparten **un ciclo**, **un motor** y **un contabilizador**; cada una
tiene su ruta, su permiso y su pantalla. Este manual dice cómo se usan y por qué se
comportan así; el runbook de lo que hay que dejar cargado antes de la primera está en
`docs/operaciones/nomina-primer-periodo.md` § «Antes de la primera liquidación especial».

Documentos que mandan: `specs/010-nomina-prestaciones-pila-dian/spec.md` (FR-001..FR-035),
`plan.md` («Decisiones de integración» D-01..D-41) y `contracts/api.md`.

---

## 1. El ciclo común

Toda liquidación especial es una **corrida** (`PAY_PayrollRuns`) con `Kind` distinto de
`Ordinary` (`ServiceBonus = 1`, `Severance = 2`, `Vacation = 3`, `Settlement = 4`), sin
período de pago, con `CutoffDate` (la fecha de corte que fija el cálculo) y `PayDate`
(cuándo se paga). Como la ordinaria, es **inmutable**: recalcular crea la versión N+1 y deja
la anterior `Superseded`; nada se edita.

| Paso | Qué pasa | Permiso |
|---|---|---|
| Calcular | El cargador arma las entradas desde corridas ordinarias **aprobadas** (bases por mes de imputación, provisiones acumuladas, ausencias), saldos iniciales, movimientos y deudas; el motor puro (`Domain/Payroll/Settlements/SettlementCalculationEngine`) devuelve líneas con **explicación paso a paso** y la corrida nace `Draft`. Quien no tiene derecho queda en `excluded` **con razón** (`SalarioIntegral`, `AprendizLectiva`, `Pasante`, `YaPagadaEnDefinitiva`, `SinDiasEnElSemestre`…). | `*.Calculate` (`Vacations.Register` para registrar el movimiento) |
| Revisar | Pestañas Resumen / Empleados (cada valor con `ExplicacionDeLinea`) / Excluidos / Relación de pago / Historial. Los avisos (`warnings`) no bloquean; los `blockers` sí. | `*.View` |
| Aprobar | Segregación como la ordinaria: quien calculó no aprueba salvo que la política `AllowSameUserApproval` lo permita y se confirme dos veces. Genera el comprobante `NM` **en la misma transacción**, con `SourceType` por tipo (`ServiceBonusRun`, `SeveranceRun`, `VacationRun`, `SettlementRun`), fechado por defecto al **corte** (D-04/D-21; se puede elegir otra fecha entre el corte y hoy) y consume el saldo inicial que usó (`ConsumedByRunId`). | `*.Approve` (sólo `CompanyAdmin` por defecto) |
| Pagar | Relación de pago propia por `runId` y comprobantes del empleado (`/api/payroll/runs/{runId}/payments`, `/payslips`), como la ordinaria. | `Payroll.Payments.Mark` |
| Reversar | Asiento espejo por el contrato contable, la corrida queda `Reversed`, el saldo inicial vuelve a estar disponible y lo propio de cada tipo se deshace (ficha reabierta, movimiento a `Registered`). Con pagos marcados no se puede (`Payroll.PaymentBlocksReversal`). | `*.Reverse` |
| Descartar | Un borrador se descarta con motivo (`Superseded` con `DiscardedAt/By/Reason`). | `*.Calculate` |

Las rutas de la ordinaria (`/api/payroll/runs/{runId}/approve|reverse|discard`) **rechazan**
una corrida especial con 422 `Payroll.Settlement.UseSettlementRoute` y la ruta correcta en
`data`; las de lectura, pagos y comprobantes sirven a cualquier `Kind`.

**Un borrador se marca desactualizado (`Stale`)** cuando cambia algo de lo que leyó: se
aprueba o reversa una ordinaria de su rango, cambia un salario, se registra un saldo inicial,
o —entre la definitiva y la ordinaria del período del retiro— se aprueba la otra (D-29,
D-35). Aprobar una `Stale` responde `Payroll.Settlement.NotDraft`: hay que recalcular.

### Contabilidad y provisiones (SC-003)

Cada rubro con provisión par (`PRIMA` ↔ `PROV_PRIMA`, `CESANTIAS` ↔ `PROV_CESANTIAS`,
`INT_CESANTIAS` ↔ `PROV_INT_CESANTIAS`, `VACACIONES_LIQ`/`VACACIONES_COMP` ↔ `PROV_VACACIONES`)
se contabiliza **contra la provisión acumulada** del empleado (corridas ordinarias aprobadas
por año/mes de imputación + saldo inicial − lo ya consumido), y la diferencia va al gasto
(`*_AJUSTE_PROV` positivo) o se libera (negativo, el contabilizador invierte débito y crédito).
Un empleado **sin provisión acumulada** lleva toda la liquidación al gasto: el lector de
provisiones informa siempre las cuatro, aunque valgan cero (hasta el 2026-09-21 no lo hacía y
la provisión quedaba en negativo). En un disfrute o compensación **parcial** de vacaciones el
ajuste es proporcional a los días liquidados, no a toda la provisión (D-30). El cuadre de la
corrida (`GET /api/payroll/runs/{runId}/balance-check`) trae el bloque `provision` con
`accrued`, `consumed`, `released`, `difference` por código.

Las cuentas de los 16 conceptos nuevos van en Nómina › Conceptos › Cuentas; sin ellas
aprobar responde `Payroll.Settlement.ConceptAccountsMissing` con la lista, y **no escribe
nada** en el libro.

### La explicación de cada valor

Cada línea trae `explanation` con la forma, la base, los pasos y el parámetro legal usado
(código y vigencia). Los valores no están en el código: viven en `PAY_LegalParameters` con
vigencia (`PRIMA_DIAS_ANIO`, `CESANTIAS_DIAS_ANIO`, `INT_CESANTIAS_PCT`, `VACACIONES_DIAS_ANIO`,
`VACACIONES_COMPENSABLE_PCT`, `INDEMNIZACION_TABLA`, `INDEMNIZACION_UMBRAL_SMMLV`, las tablas
y topes de retención, las fechas límite `*_FECHA_LIMITE*` con `Kind = DateInYear`…). Lo que
falte para un proceso lo dice `GET /api/payroll/legal-parameters/missing?process=Settlements`
(Nómina › Parámetros legales › selector de proceso).

---

## 2. Prima de servicios — `/nomina/prima`

Una corrida **por empresa, año y semestre** (D-02): corte 30-06 o 31-12. Proporcional a los
días del semestre y al salario base de prestaciones (promedio cuando hubo variaciones, con
auxilio de transporte si aplica). Excluye al salario integral, al aprendiz en etapa lectiva,
al pasante (FR-009) y a quien ya cobró la prima del semestre en una **definitiva**
(aprobada **o en borrador**, D-29); una segunda del mismo semestre responde `Duplicate`
mientras la anterior siga viva (FR-005). Retención propia (`RETEFTE_PRIMA`, procedimiento 1:
se depura aparte con su 25 % exento). Fecha límite legal en pantalla (`PRIMA_FECHA_LIMITE_S1/S2`).

## 3. Cesantías e intereses del año — `/nomina/cesantias-anuales`

Una corrida por empresa y año (corte 31-12 por defecto). `CESANTIAS` deja la cuenta por pagar
**al fondo** de cada empleado (la persona vinculada al fondo, FR-088) e `INT_CESANTIAS` al
empleado; la relación de pago y la marca de pago cubren **sólo lo que va al empleado**
(intereses menos retención). La **consignación por fondo** (`GET /{runId}/deposit-schedule`)
lista por fondo con NIT, empleados, días y valor, se exporta con el centro de reportes
(`consignacion-cesantias`: en Excel, una hoja por fondo) y se marca consignada por fondo con
fecha y referencia (`mark-deposited`; dos veces → `AlreadyDeposited`). Excluye integral,
aprendiz lectivo, pasante y al retirado con definitiva (aprobada o en borrador). El archivo
plano por fondo responde `FundFormatMissing` hasta N4. `/nomina/cesantias` sigue siendo el
catálogo de fondos.

## 4. Vacaciones — `/nomina/vacaciones`

- **Saldo derivado**, nunca guardado: causado (días trabajados × `VACACIONES_DIAS_ANIO` / 360,
  descontando suspensiones desde el ingreso) + saldo inicial − disfrutado − compensado ± ajustes.
  `GET /api/payroll/vacations/employees/{id}/balance` trae la explicación.
- **Vista previa obligatoria** de días hábiles antes de registrar (`working-days`): usa la
  política `SemanaLaboral` vigente (L–S por defecto, o L–V) y `PAY_Holidays` (Ley 51 sembrada
  2026–2028 + decretados/manuales). Un año sin festivos cargados avisa
  (`Payroll.Holiday.YearNotLoaded`).
- **Registrar** disfrute o compensación crea el movimiento y la corrida `Vacation` en borrador
  **en una sola acción** (una corrida por movimiento, D-32). La compensación no pasa del máximo
  legal (`VACACIONES_COMPENSABLE_PCT`; `CompensationOverMax` con `maxDays`); el disfrute exige
  saldo y avisa si es anticipado.
- **Quién paga los días** (D-01, D-31): con `VacacionesPagoAnticipado = sí` la liquidación paga
  los días del descanso contados por el **calendario comercial** (los mismos que la ordinaria
  descuenta), y la nómina ordinaria de los períodos cubiertos recibe la novedad informativa
  `AUSENCIA_VACACIONES` (origen `VacationLeave`) que reduce los días de salario sin pagar
  nada; los aportes de esos días los sigue causando la ordinaria. Sin período de nómina que
  cubra el disfrute, aprobar responde `Payroll.Vacation.PeriodMissing` (D-33): hay que
  crear el período primero. Un disfrute sobre un período ya aprobado responde
  `PeriodApproved` con el período retroactivo; se acepta con `acceptRetroactive`.
- **Reversar** una liquidación cuya ausencia ya descontó una ordinaria aprobada se rechaza
  (`NoveltyAlreadyPaid`, D-34): primero se reversa esa nómina.
- Ajustes manuales del saldo con motivo y cancelación de movimientos, todo auditado.

## 5. Liquidación definitiva por retiro — `/nomina/liquidacion-definitiva`

- **Terminar contrato** se hace desde la ficha del empleado o desde esta pantalla: registra la
  terminación (fecha, motivo del catálogo `PAY_TerminationReasons`, tipo de contrato DIAN,
  fin de contrato, notas) y crea la corrida `Settlement` en borrador en la misma acción. El
  retiro debe caer en un período abierto o calculado (FR-021: `Payroll.Termination.PeriodApproved`).
  La ruta vieja `POST /api/payroll/employees/{id}/terminate` **se retiró sin alias**.
- Rubros: `SALARIO_PENDIENTE` (los días del período del retiro **más las novedades activas de
  ese período** con su concepto, D-29), `PRIMA` proporcional, `CESANTIAS` e `INT_CESANTIAS`
  del período, `VACACIONES_COMP` por los días pendientes, `INDEMNIZACION` según tipo de
  contrato y motivo (tabla `INDEMNIZACION_TABLA` por tramos, D-08; sólo los motivos sembrados
  la generan), `BONIF_RETIRO` si hay novedad, deducciones de ley, retención (`RETEFTE`,
  `RETEFTE_PRIMA`, `RETEFTE_CESANTIAS`, `RETEFTE_INDEMNIZACION`) y **descuentos de Cartera**.
- **Un solo pagador del último tramo** (D-29): el empleado con definitiva aprobada dentro del
  período **no entra** a la nómina ordinaria de ese período; si la ordinaria se aprobó antes,
  la definitiva en borrador queda `Stale` y al recalcular sale sin el tramo. La prima y las
  cesantías ya pagadas por una corrida semestral/anual aprobada se omiten (`YaPagadaEnCorridaSemestral`,
  `YaPagadaEnCorridaAnual`).
- **Descuentos** (`GET/PUT /{runId}/deductions`): créditos de Cartera y libranzas del
  empleado, propuestos según la política `DeduccionAlRetiroModo` (`SaldoTotal`,
  `SoloCuotasCausadas` —contadas con la misma regla `ApplyOn` de la recurrente—, `NoProponer`);
  la responsable sólo puede **bajar** lo aplicado, con motivo, y queda auditado
  (`Payroll.Settlement.DeductionAdjusted`). Si Cartera no responde, aviso, no bloqueo.
- **Aprobar** cierra la ficha (`Status = -1`, `TerminationId`), recauda en Cartera por cada
  obligación con lo aplicado —dentro de la misma unidad de trabajo: si el recaudo falla, no se
  aprueba nada— y genera el **documento para firma** (PDF, `GET /{runId}/document`; el borrador
  sale marcado «BORRADOR»). **Reversar** reabre la ficha y lista los recaudos a deshacer; si la
  persona ya tiene una ficha nueva por reingreso responde `Payroll.Settlement.EmployeeRehired`.
- Sanción moratoria informativa (`GET /{runId}/late-payment-penalty`): no se contabiliza.

---

## 6. Lo que se parametriza una vez

| Qué | Dónde | Notas |
|---|---|---|
| **Políticas de la empresa** (11 claves con vigencia) | `/nomina/politicas` (`PAY_CompanyPolicies`) | `Exonerada114_1`, `SemanaLaboral`, `VacacionesPagoAnticipado`, `CotizaArlEnVacaciones`, `RetefteTopesAnualesModo`, `P2SecuenciaDepuracion`, `DianPlazoComputo`, `DianMedioPagoMapa`, `DeduccionAlRetiroModo`, `ArranqueNominaFecha`, `AllowSameUserApproval`. La vigencia que cubre la fecha del proceso manda; una retroactiva avisa, y en `Exonerada114_1` se bloquea si ya hay corridas aprobadas desde esa fecha. `PayrollPolicyReader` es el único lector. |
| **Festivos** | `/nomina/festivos` (`PAY_Holidays`) | Ley 51 sembrada 2026–2028 (`FestivosLey51`); decretados y manuales a mano; los sembrados no se borran. |
| **Saldos iniciales de prestaciones** | `/nomina/saldos-iniciales` (`PAY_EmployeeBenefitOpeningBalances`) | Para quien ingresó antes de `ArranqueNominaFecha`: días de vacaciones pendientes, cesantías, intereses y prima acumulados a una fecha. La **fila vigente** (corte más reciente, y a igual corte la última registrada) es el saldo completo; un ajuste se digita como saldo completo con motivo. Se consume al aprobar la primera liquidación que lo usa y se libera al reversarla. |
| **Motivos de retiro** | `/nomina/liquidacion-definitiva` › motivos (`PAY_TerminationReasons`) | 9 sembrados (`RENUNCIA`, `DESP_SINJC`, `DESP_JC`, `VENC_TERM`, `MUTUO_ACDO`, `FIN_OBRA`, `PER_PRUEBA`, `MUERTE`, `PENSION`); los propios no generan indemnización. |
| **Ficha del empleado** | `/nomina/empleados/{id}` | Bloques PILA y DIAN (tipo/subtipo de cotizante, DIVIPOLA, tipo de contrato DIAN, medio de pago), etapa del aprendiz, banco de dispersión, saldo inicial, saldo de vacaciones, porcentaje P2 vigente. La persona tiene segundo apellido y otros nombres. |
| **Cuentas por concepto** | Nómina › Conceptos › Cuentas | Los 16 conceptos nuevos (§1). |

---

## 7. Permisos

| Recurso | Acciones | Operador | Auditor / Sólo lectura |
|---|---|---|---|
| `Payroll.ServiceBonus` | View, Calculate, Approve, Reverse | View, Calculate | View |
| `Payroll.Severance` | View, Calculate, Approve, Reverse, MarkDeposited | View, Calculate | View |
| `Payroll.Vacations` | View, Register, Calculate, Approve, Reverse | View, Register, Calculate | View |
| `Payroll.Settlements` | View, Calculate, Approve, Reverse, AdjustDeduction, Manage | View, Calculate | View |
| `Payroll.BenefitBalances` | View, Manage | View, Manage | View |
| `Payroll.CompanyPolicies` / `Payroll.Holidays` | View, Manage | View | View |

Sin permiso el servidor responde 404 `Generic.NotFound`, como en el resto del ERP. Los
reportes del centro de reportes (`liquidacion-especial-resumen/detalle`, `consignacion-cesantias`,
`saldos-vacaciones`, `movimientos-vacaciones`, `terminaciones`, `saldos-iniciales-prestaciones`)
exigen el `View` del tipo de la corrida.

---

## 8. Cómo se prueba

- **Casos dorados** en `tests/IngenIA365ERP.Domain.Tests/Payroll/Settlements/Casos/` (JSON
  con la derivación a mano; el motor tiene que coincidir al peso o al centavo según la
  política de redondeo). Agregar uno nuevo es agregar un archivo.
- **Application.Tests** por comando con `NominaTestData` (corridas aprobadas de meses
  anteriores, saldos iniciales, políticas con vigencia, fondos con persona).
- **e2e** con Docker: `PrimaDeServiciosTests`, `CesantiasAnualesTests`, `VacacionesTests`,
  `LiquidacionDefinitivaTests` en la colección «Nomina e2e» (`NominaE2E.PrepararAsync` abre la
  vigencia `AllowSameUserApproval = true` en `PAY_CompanyPolicies`, porque la fila sembrada en
  `false` manda sobre el ajuste heredado de `COR_SystemSettings`).
- Reglas de arquitectura: `LaNominaNoTieneValoresLegalesFijos`, `LosCodigosDeNominaEstanEnElContrato`,
  `LosEnumsDelContratoNumeranComoElDominio`, `LasPantallasDeNominaSiguenAlContrato`,
  `LaAuditoriaSeNombraPorElPublicIdDelTenant`.

## 9. Lo que N1 deja para después

- Aportes patronales, ARL, parafiscales y provisiones de los días del último tramo de una
  definitiva: no los calcula ninguna corrida; N2 (PILA) los toma de la corrida `Settlement`.
- Los devengos variables liquidados en la definitiva no entran al promedio de la prima ni de
  las cesantías de esa misma definitiva.
- Archivo plano de consignación por fondo (N4), `pilaCode` del fondo (N2), recaudo en Cartera
  sólo por Application.Tests hasta que exista desembolso por HTTP (E3 de la 009).
- Confirmaciones de la contadora: días del disfrute por calendario comercial (D-31),
  exoneración, `AffectsVacationBase`, aprendiz en práctica.
