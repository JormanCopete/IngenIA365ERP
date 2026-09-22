# Retención en la fuente: porcentaje fijo del procedimiento 2

Feature 010, entrega N2 (2026-09-21). Cómo se calcula, se revisa, se aprueba y se aplica el
porcentaje fijo semestral de los empleados en procedimiento 2 (ET art. 386), y cómo la
contadora lo reconstruye mes a mes desde la explicación (SC-006).

## 1. Qué hace

En **junio** se calcula el porcentaje que rige **julio–diciembre** (semestre 1); en **diciembre**
el que rige **enero–junio** del año siguiente (semestre 2). Para cada empleado activo con
«Procedimiento 2» en Retención y plan:

1. Suma los pagos **gravables** (`AffectsWithholdingBase`) de los **doce meses anteriores** al mes
   del cálculo desde las corridas aprobadas: ordinarias por su mes de imputación y especiales por
   el mes del corte. La **prima entra** (es el mes trece); las **cesantías y sus intereses no**
   (art. 386).
2. Resta los **aportes obligatorios reales** de esas corridas (`SALUD_EMP`, `PENSION_EMP`, `FSP`).
3. Depura con las **mismas deducciones y rentas exentas** de la nómina ordinaria
   (`DepuracionDeRetencion`: vivienda, prepagada, dependientes, AFC/AVP, 25 % con sus topes) en la
   secuencia de la política **`P2SecuenciaDepuracion`**: `DepurarLuegoDividir` (depura la
   sumatoria con topes proporcionados a los meses y luego divide) o `DividirLuegoDepurar` (divide y
   depura el promedio con topes mensuales). Las dos son admitidas; con topes que muerden dan
   porcentajes distintos (casos dorados 01 y 02: 16,35 % y 16,13 %).
4. Divide por **`RETEFTE_P2_DIVISOR`** (13) cuando hay doce meses con historia; con menos, por los
   **meses de vinculación** (caso 03: 8 meses ÷ 8).
5. Lleva la base promedio a la **tabla marginal vigente** (`RETEFTE_TABLA_UVT`) o a la **tabla del
   plan** si el plan trae tramos (caso 05), con la UVT del mes del cálculo.
6. Porcentaje = retención teórica ÷ base promedio × 100, a dos decimales (Oficio DIAN 68292/2013).

Todo queda en `PAY_WithholdingRateCalculations` (una versión por cálculo; recalcular deja la
anterior `Superseded`) y `PAY_WithholdingRateCalculationMonths` (mes a mes con las corridas
sumadas). Nada de esto tiene un número escrito en el código: divisor, UVT, tabla y topes son
parámetros con vigencia.

## 2. Pantalla: Nómina › Retención procedimiento 2

- **Calcular** (`Payroll.WithholdingRate.Calculate`): año y semestre; devuelve la lista, los
  **omitidos** con motivo (`NoHistory`: sin nómina aprobada en la ventana) y el aviso
  `SemesterIncomplete` si al último mes de la ventana le falta nómina aprobada (calcula igual con lo
  que hay; se recalcula al aprobarla).
- **Detalle**: mes a mes con sus corridas (tipo y versión), depuración paso a paso, divisor y su
  origen, tabla y tramo, porcentaje. Exportable a Excel y PDF (`retencion-p2`).
- **Aprobar** (`Payroll.WithholdingRate.Approve`), por ítem o «Aprobar todos los calculados»:
  cierra la vigencia actual de la ficha **la víspera** del semestre (nunca la borra, R8) y abre la
  nueva con **origen «calculado»** y el cálculo del que salió; la primera quincena del semestre ya
  la aplica (`WithholdingProcedure2` en el motor ordinario, sin cambios). Aprobar dos veces responde
  `AlreadyApproved`; una aprobada no se reemplaza al recalcular: se crea otra versión al lado.
- **Rechazar** con motivo deja el cálculo `Rejected`.
- La ficha (Nómina › Empleados › Retención y plan) muestra todas las vigencias y sigue admitiendo
  ajustes a mano; desde N2 una tasa nueva **cierra** la anterior en vez de reemplazar el conjunto.

## 3. Lo que se necesita antes

| Qué | Dónde | Si falta |
|---|---|---|
| Empleado en procedimiento 2 | Retención y plan de la ficha | `NoProcedure2Employees` |
| Doce meses (o los de vinculación) con nómina aprobada | Liquidación | `NoHistory` con el primer mes aprobado |
| `RETEFTE_P2_DIVISOR`, `UVT`, `RETEFTE_TABLA_UVT`, topes de renta exenta y deducciones vigentes al mes del cálculo | Nómina › Parámetros legales (`GET /api/payroll/legal-parameters/missing?process=WithholdingRates`) | `ParametersMissing` |
| Política `P2SecuenciaDepuracion` (por defecto `DepurarLuegoDividir`) y `RetefteTopesAnualesModo` | Nómina › Políticas de la empresa | se usa el defecto |
| Deducciones y rentas exentas declaradas del empleado | Retención y plan de la ficha | se depura sólo con aportes y renta exenta legal |

## 4. Cómo se prueba

Casos dorados en `tests/IngenIA365ERP.Domain.Tests/Payroll/Withholding/Casos/` con la derivación
a mano (01 depurar luego dividir, 02 dividir luego depurar, 03 menos de doce meses, 04 con prima
y sin cesantías, 05 tabla del plan); en Application `WithholdingRateHandlersTests` (loader con
prima y sin cesantías, divisor, `NoHistory`, versiones, política, aprobación que cierra la
vigencia y la nómina siguiente que la lee, cierre de vigencias desde la ficha); por HTTP
`RetencionProcedimiento2Tests` (quickstart §3.7). Confirmación 8h de la contadora: cuál de las dos
secuencias adopta la cooperativa.
