# Research: Nómina completa — prestaciones, retiro, procedimiento 2, PILA, nómina electrónica y dispersión

**Feature**: 010 | **Date**: 2026-09-20 | **Rama**: `010-nomina-prestaciones-pila-dian`

Fase 0 del plan. Las decisiones del dueño están en `spec.md › Clarifications` (nueve, sesión
2026-09-20); lo demás se resolvió con cuatro informes de investigación del 2026-09-20 —norma
laboral y tributaria, PILA (Resolución 2388/2016), nómina electrónica DIAN e inventario del código
de nómina en la rama— y con la norma pública. El quinto informe (dispersión bancaria) **no llegó a
este redactor**: R11 se apoya en la spec, en el inventario del código y en lo que el dueño ya dijo
(AV Villas; la estructura la aporta él). Cada punto sigue Decisión / Razón / Alternativas
consideradas / Fuentes. Cuando la norma o el formato tienen variantes, se dice y se deja como
parámetro; nada de lo que sigue es una constante del programa (FR-003, SC-008).

Convención de citas: `archivo:línea` es del repositorio en la rama; los URL son de la sesión de
investigación; «scratchpad» es
`C:/Users/Jorman/AppData/Local/Temp/claude/D--OneDrive---INGENIA-365-PSNL-AplicacionesWeb-IngenIA365ERP/14867f9b-d4c6-49bd-bee5-cd2b02e7b646/scratchpad/`
(anexo DIAN extraído, Res. 227/2025, WSDL de habilitación, Caja de Herramientas).

## Lo que la investigación cambia en la spec (corregir antes de la Fase 1)

1. **Aprendices** (Edge Cases, FR-009, FR-013, Assumptions «aprendices sin prestaciones»): la Ley
   2466 de 2025 (art. 21, vigente desde el 25-06-2025) convirtió el contrato de aprendizaje en
   contrato laboral especial a término fijo. **Etapa lectiva**: apoyo 75 % del SMMLV, salud y ARL a
   cargo del empleador, sin prestaciones, cotizante PILA **19**. **Etapa práctica**: 100 % del
   SMMLV, prima, cesantías, intereses, vacaciones, pensión y aportes completos, cotizante PILA
   **1**. La exclusión en bloque sólo vale para la etapa lectiva. Fuentes:
   https://www.funcionpublica.gov.co/eva/gestornormativo/norma.php?i=260676 ;
   https://www.garrigues.com/es_ES/noticia/colombia-asi-deben-aplicarse-nuevos-lineamientos-contratos-aprendizaje-sector-privado ;
   AT2 v30 págs. 106-109. **No encontrado**: si el aprendiz en práctica causa CCF/SENA/ICBF.
2. **Procedimiento 2** (Glosario, FR-022, Assumptions «promedio mensual»): el art. 386 ET dice
   literalmente «dividir por 13» la sumatoria de los doce meses (la prima es el mes trece), o por
   los meses de vinculación si son menos de doce. Fuente: https://actualicese.com/estatutotributario/386-2/.
3. **Plazo de la nómina electrónica** (Contexto, US6 «diez días hábiles»): la norma vigente dice
   «dentro de los diez (10) primeros días del mes siguiente» sin «hábiles» (Res. 227/2025 art.
   1.5.3.4.1.1) y el Oficio DIAN 901706/2022 lo lee como calendario; «hábiles» sólo aparece en las
   Res. 151/2021 y 028/2022 para la primera transmisión de 2022. Ver R10.
4. **Marco normativo DIAN** (US6, Assumptions): la Resolución 000013 de 2021 fue compilada y su
   texto derogado por la **Resolución Única 000227 del 23-09-2025** (arts. 1.5.3.1.1 a 1.5.3.9.x);
   citar «Res. 013/2021, compilada en la Res. 227/2025».
5. **Proveedor tecnológico** (Clarifications, FR-031a): ser PT exige ya ser PT de factura
   electrónica, patrimonio ≥ 20.000 UVT y visita de la DIAN; el servicio nace con el modo
   «software propio» de cada cooperativa y el modo PT se activa después. Ver R10.
6. **Fondo de solidaridad pensional y PILA 2027** (FR-024): la Ley 2381/2024 rige desde el
   **01-04-2027** (Sentencia C-264 del 02-09-2026); dos tablas FSP con vigencia y una bandera
   «régimen de transición» por empleado. Ver R4 y R9.
7. **Redondeo PILA**: IBC al peso superior; aportes al múltiplo de 100 superior (Decreto 780/2016
   art. 3.2.1.5, sustituido por el Decreto 1990/2016); ambos parámetros.
8. **Exoneración art. 114-1 ET**: la spec dice «COOFLOPAL sí»; el comentario sembrado en el código
   dice que una cooperativa del régimen especial «normalmente NO» (`CalculationInput.cs:39-45`,
   `CatalogSeeders.cs:100`). La norma encontrada respalda la spec: la Ley 1955/2019 art. 204
   añadió al par. 2 del art. 114-1 que «las entidades de que trata el artículo 19-4 del Estatuto
   Tributario conservan el derecho a la exoneración» (Oficio DIAN 26845/2019). Confirmar con la
   contadora antes de fijar el valor; el programa admite ambas (FR-024a).

## R1. Motor de liquidaciones especiales: motor puro nuevo que reutiliza las piezas

**Decisión**: crear `Domain/Payroll/Settlements/SettlementCalculationEngine` (puro, sin IO, todo
por parámetro) para prima, cesantías e intereses, vacaciones y definitiva, reutilizando tal cual
`Explanation`, `ParameterSet`/`ConceptSet`, `SalaryTranches`, `CalendarConventions`, `Fmt`,
`LineFactory` y un helper extraído de `RangeTableRule` (búsqueda de tramo: tabla + base + unidad →
valor + `ExplanationRange`). **No** forzar `PayrollCalculationEngine`. Entrada nueva
`SettlementInput` (empleado con historial de salarios, fecha de corte, tipo, historial de bases
prestacionales de 6/12 meses tomadas de las corridas aprobadas, saldos iniciales, movimientos de
vacaciones, provisiones acumuladas, ausencias/suspensiones, motivo de retiro, deudas propuestas) y
salida con líneas explicadas del mismo tipo `CalculationLine`. Convención de días 30/360 con el 31
tratado como 30 (`CalendarConventions.Days`); días de suspensión del contrato descontados de
vacaciones y cesantías, no de prima ni intereses (art. 53 CST); incapacidades cuentan en todo.

**Razón**: el motor ordinario ata el período a una periodicidad (`PeriodInput.DaysInPeriod =>
(int)Periodicity`, `CalculationInput.cs:11-99`), proporciona los topes UVT/SMMLV a 30 días
(`BaseBuilder.cs:45`, `WithholdingBaseBuilder.cs:190`) y corre un pipeline de once pasos de nómina
ordinaria (`PayrollCalculationEngine.cs:118-305`): un semestre o un año lo desbordan, y cualquier
parche pondría en riesgo los 22 casos dorados y el `InputsHash` de repetibilidad. Las piezas
reutilizables ya son estáticas o records independientes: `SalaryTranches.Build` sólo usa
`StartDate/EndDate` y sirve para semestre y año (`SalaryTranches.cs:90-161`); `Explanation` y
`LineFactory` no conocen el período (`Explanation.cs:9-57`, `ICalculationRule.cs:110-150`).
Fórmulas que el motor implementa (todas con valores desde `ParameterSet`):

- Prima (CST art. 306, Ley 1788/2016): `Base × DíasSemestre / 360`, `Base` = promedio
  ponderado del semestre de (salario + auxilio de los días con derecho), equivalente a Σ devengado
  del semestre / 12. Ej.: 2.000.000 + 249.095 = 2.249.095 × 180/360 = **1.124.547,50**; ingreso
  15-09-2026 con 1.750.905 hasta 31-10 y 2.000.000 desde 01-11 → 106 días → **630.404,72**.
- Cesantías (CST arts. 249 y 253; Ley 50/1990 art. 99): `Base × Días / 360`; `Base` = último
  salario + auxilio si no varió en los tres últimos meses, si no promedio del último año o del tiempo
  servido. Ej.: 2.000.000 hasta 31-10 y 2.400.000 desde 01-11 → promedio 2.066.666,67 + 249.095 →
  **2.315.761,67**.
- Intereses (Ley 52/1975 art. 1; Decreto 116/1976 art. 2): `Cesantías × Días × 12 % / 360`.
  Ej.: 666.666,67 × 120 × 0,12/360 = **26.666,67**.
- Vacaciones (CST arts. 186, 189, 192; Ley 995/2005): días hábiles causados = `DíasTrabajados ×
  15 / 360`; valor día = salario ordinario sin auxilio ni extras / 30 (promedio 12 meses si
  variable); compensación en dinero hasta la mitad durante el contrato; total al retiro. Ej.: 540
  días → **22,5 hábiles**; 11,5 pendientes × 80.000 = **920.000**.
- Indemnización (CST art. 64, Ley 789/2002 art. 28): indefinido < 10 SMMLV: 30 días el primer
  año + 20 por año adicional proporcional; ≥ 10 SMMLV: 20 + 15; término fijo/obra: tiempo
  faltante, mínimo 15 días; base sin auxilio. Ej.: 2.400.000, 810 días → 55 días → **4.400.000**.
- Salario pendiente, auxilio proporcional, deducciones de ley sobre lo salarial (4 % + 4 % + FSP),
  retención según R5.

**Alternativas consideradas**: (a) extender `PeriodInput` con `DaysOverride` y un modo
`Settlement` en el motor: menos código nuevo pero mezcla dos ciclos en un pipeline y exige revisar
cada paso (aportes, provisiones, topes) para que no corra en una liquidación; (b) calcular en el
handler de Application con acceso a BD: difícil de probar con casos dorados y rompe el patrón del
módulo; (c) reutilizar `VacationLiquidation`, `EmployeeLiquidationMaster/Detail`,
`SeveranceHistory` del legado SOLIDO: mapeadas y sin uso (`VacationLiquidation.cs:6-7`), sin
versión ni explicación; sólo sirven como referencia para migrar saldos iniciales.

**Fuentes**: `PayrollCalculationEngine.cs:42-54, 118-305`; `CalculationInput.cs:11-99`;
`Bases/BaseBuilder.cs:15-63`; `Bases/SalaryTranches.cs:90-161`; `CalendarConventions.cs:9-49`;
`Rules/RangeTableRule.cs:14-75`; CST arts. 53, 64, 186-192, 249, 253, 306
(https://www.cancilleria.gov.co/sites/default/files/Normograma/docs/pdf/codigo_sustantivo_trabajo_pr006.pdf ;
https://leyes.co/codigo_sustantivo_del_trabajo/253.htm ; https://leyes.co/codigo_sustantivo_del_trabajo/64.htm);
Ley 50/1990 art. 99 (https://normativa.colpensiones.gov.co/colpens/docs/ley_0050_1990_pr002.htm);
Ley 52/1975 y Decreto 116/1976 (https://www.suin-juriscol.gov.co/viewDocument.asp?id=1606193 ;
https://www.suin-juriscol.gov.co/viewDocument.asp?ruta=Decretos%2F1025941); Ley 1 de 1963 art. 7
(https://www.suin-juriscol.gov.co/viewDocument.asp?ruta=Leyes/1556008); Ley 995/2005 y C-035/2005
(https://www.corteconstitucional.gov.co/relatoria/2005/C-035-05.htm).

## R2. Modelo de corrida especial: las mismas tablas de corrida con un tipo

**Decisión**: persistir prima, cesantías/intereses, vacaciones y definitiva en
`PAY_PayrollRuns` / `PAY_PayrollRunEmployees` / `PAY_PayrollRunLines`, con `PayPeriodId`
**nullable**, columna `Kind` (`Ordinary`, `ServiceBonus`, `Severance`, `Vacation`, `Settlement`),
`CutoffDate` (fecha de corte) y `Year`/`Semester` cuando aplican, y el índice único actual
`UK_PAY_PayrollRuns_Period_Version (PayPeriodId, Version)` reemplazado por índices filtrados por
tipo: ordinaria `(PayPeriodId, Version)`; prima `(Kind, Year, Semester, Version)`; cesantías
`(Kind, Year, Version)`; vacaciones y definitiva `(Kind, EmployeeId, CutoffDate, Version)` vía la
fila de `PayrollRunEmployee`. FR-005 («segunda liquidación del mismo tipo, período y empleado
mientras la anterior no esté reversada») se hace cumplir en el comando con consulta de corridas
`Approved`/`Draft` del mismo `Kind` + empleado + corte, y con el índice como red. La definitiva es
una corrida de **un solo empleado**. `SourceType` contable por tipo (`ServiceBonusRun`,
`SeveranceRun`, `VacationRun`, `SettlementRun`).

**Razón**: relación de pago (`GetPaymentRegisterQuery`), marca de pagado (`MarkPaymentsCommand`,
`RevertPaymentMarkCommand`), comprobantes PDF y correo (`PayslipModelBuilder`,
`IPayslipEmailDispatcher`), exportación, reversión (`ReversePayrollRunCommand`) y la pantalla de
detalle trabajan por `RunPublicId` y siguen sirviendo sin reescritura (FR-001, FR-011a); las líneas
ya guardan explicación, base, factor, rango, parámetro y versión del concepto
(`PayrollRunLine.cs:141-177`). Las rutas por `runId` de `PayrollRunsEndpoints.cs:15-99` funcionan
para cualquier corrida; sólo las rutas por `periodId` no aplican. El campo `Kind` permite que las
consultas existentes filtren `Ordinary` donde hoy asumen período (historial, comparación,
`balance-check`).

**Alternativas consideradas**: tablas nuevas `PAY_Settlements*`: esquema más limpio pero duplica
pagos, comprobantes, reportes y reversión o exige abstraerlos primero; reusar sin `Kind` con un
período «ficticio»: rompe la semántica de `PayPeriod` (estado `Calculated/Approved`, `ImputationYear/Month`)
y las consultas de PILA y P2 que suman por período de imputación.

**Fuentes**: `PayrollRun.cs:14-77`; `PayrollRunEmployee.cs:88-128`; `PayrollRunLine.cs:141-177`;
`PayrollRunConfiguration.cs:38-41, 75`; `PaymentQueriesAndCommands.cs:13-41, 51-100, 167`;
`PayrollPaymentsEndpoints.cs:16-73`; `Interfaces.cs:228-278`; `ApprovePayrollRunCommand.cs:33-38,
101-147, 182-192`; `ReversePayrollRunCommand.cs:29, 56-99`; `CalculatePayrollRunCommand.cs:43-215`
(plantilla del cálculo/persistencia).

## R3. Saldos iniciales de prestaciones (FR-007)

**Decisión**: tabla `PAY_EmployeeBenefitOpeningBalances` (una fila por empleado y fecha de
arranque, auditable): `AsOfDate`, `PendingVacationDays` (decimal, hábiles), `AccruedSeverance`,
`AccruedSeveranceInterest`, `AccruedServiceBonus`, `AccruedProvision*` opcional por concepto,
`Notes`, quién/cuándo; editable sólo mientras no exista una liquidación aprobada que la haya
consumido (después, un ajuste con motivo y nueva fila con vigencia). El motor la recibe como un
«tramo inicial» que suma a los acumulados de días y valores y la explica como paso propio («Saldo
inicial al 30-11-2026 digitado por … el …»). Los movimientos de vacaciones derivan el saldo
(`causado + inicial − disfrutado − compensado`), nunca lo almacenan como verdad (Key Entities).
Advertencia obligatoria en toda liquidación cuando `Employee.JoinDate < AsOfDate de arranque de la
cooperativa` y no hay saldo inicial (Edge Cases).

**Razón**: la provisión acumulada por empleado no existe en el libro (R4/R7 de código: las
provisiones se contabilizan sin tercero, `TercerosDeNomina.cs:17-27`) y las corridas empiezan el
01-12-2026; sin el saldo digitado, la prima de diciembre y las cesantías de 2026 salen cortas. La
apertura contable trae el total de la provisión; el detalle por empleado sólo puede venir digitado
o migrado.

**Alternativas consideradas**: migrar desde las tablas legado `nom_antcesantia`/`nom_maeliqemp`
(`SeveranceHistory`, `EmployeeLiquidationMaster`) con `database/migration/`: sirve como fuente
para llenar la tabla si COOFLOPAL trae datos de SOLIDO, pero no reemplaza la digitación auditada
(la cooperativa hoy lo hace a mano o en otros programas, Contexto); guardar el saldo como
«novedad» de nómina: mezcla dos naturalezas y la novedad tiene período.

**Fuentes**: spec FR-007, Edge Cases, Assumptions «Arranque el 1 de diciembre de 2026»;
`SeveranceHistory.cs:6-7`; `EmployeeLiquidationMaster.cs:6-7`; `TercerosDeNomina.cs:17-27`.

## R4. Parámetros legales nuevos y semilla 2026

**Decisión**: todo valor nuevo va a `PAY_LegalParameters` (`PayrollLegalParameter` con `Code`,
`Kind` Amount/Percent/RangeTable, `Value`, `ValidFrom/To`, `Source`, `Ranges`,
`RangeUnitParameterCode`, `RangeIsMarginal`, `PayrollLegalParameter.cs:14-50, 242-282`) con
`Source` preciso (número de ley/decreto y artículo); los códigos nuevos **no** entran en
`LegalParameterCodes.Required` (que exige 33 códigos a toda nómina ordinaria,
`LegalParameterCodes.cs:299-312`) sino en listas por proceso (`SettlementParameterCodes.Required`,
`PilaParameterCodes.Required`, `WithholdingRateParameterCodes.Required`) comprobadas con
`ParameterSet.Missing`, para que el despliegue no niegue la nómina ordinaria de las cooperativas
ya en producción. Las políticas **por empresa** con vigencia (exoneración, semana laboral, modo de
topes anuales, ARL en vacaciones, secuencia P2, cómputo del plazo DIAN) van a una tabla nueva
`PAY_CompanyPolicies` (clave, valor, `ValidFrom/To`, auditada), leída por `PayrollPolicyReader`
con fecha, manteniendo lectura de compatibilidad de `Payroll.ApplyEmployerExemption` en
`COR_SystemSettings` (`PayrollPolicyReader.cs:66-102`) hasta migrar. El **layout PILA** y el
**calendario de festivos** son datos versionados (R6, R9), no parámetros decimales.

Semilla (vigencia 2026-01-01 salvo que se diga otra; «existe» = ya está en
`PayrollLegalParametersSeeder.cs:43-127` y sólo se precisa el `Source`):

| Código | Valor | Norma (Source) | Vigencia | Estado |
|---|---|---|---|---|
| `SMMLV` | 1.750.905 | Decreto 1469 del 29-12-2025 (suspendido provisionalmente el 13-02-2026 por el Consejo de Estado; Decreto 159/2026 mismo valor; suspensión revocada jul-2026) | 2026-01-01 | existe; precisar Source |
| `AUX_TRANSPORTE` | 249.095 | Decreto 1470 del 29-12-2025 | 2026-01-01 | existe; precisar Source |
| `AUX_TRANSPORTE_TOPE_SMMLV` | 2 | Ley 15/1959 art. 2; Ley 1/1963 | vigente | existe |
| `UVT` | 52.374 | Resolución DIAN 000238 del 15-12-2025 | 2026-01-01 | existe; precisar Source |
| `PRIMA_DIAS_ANIO` | 30 (15 por semestre) | CST art. 306 (Ley 1788/2016) | vigente | **nuevo** |
| `PRIMA_FECHAS_LIMITE` | 30-06 / 20-12 | CST art. 306 | vigente | **nuevo** (texto/fecha, aviso) |
| `CESANTIAS_DIAS_ANIO` | 30 | CST art. 249; Ley 50/1990 art. 99 | vigente | **nuevo** |
| `CESANTIAS_VENTANA_ESTABILIDAD_MESES` | 3 | CST art. 253 (Decreto 2351/1965 art. 17) | vigente | **nuevo** |
| `CESANTIAS_FECHA_LIMITE_CONSIGNACION` | 14-02 | Ley 50/1990 art. 99 num. 3 | vigente | **nuevo** (aviso) |
| `INT_CESANTIAS_PCT` | 12 | Ley 52/1975 art. 1; Ley 50/1990 art. 99 num. 2 | vigente | **nuevo** |
| `INT_CESANTIAS_FECHA_LIMITE` | 31-01 | Ley 52/1975 art. 1; Decreto 116/1976 | vigente | **nuevo** (aviso) |
| `VACACIONES_DIAS_ANIO` | 15 (hábiles) | CST art. 186 | vigente | **nuevo** |
| `VACACIONES_COMPENSABLE_PCT` | 50 | CST art. 189 num. 1 (Ley 1429/2010 art. 20) | 2010-12-29 | **nuevo** |
| `INDEMNIZACION_TABLA` (RangeTable en SMMLV, no marginal, con dos valores por tramo: días primer año / días por año adicional) | <10: 30/20; ≥10: 20/15 | CST art. 64 (Ley 789/2002 art. 28) | 2002-12-27 | **nuevo** |
| `INDEMNIZACION_UMBRAL_SMMLV` | 10 | CST art. 64 | vigente | **nuevo** |
| `INDEMNIZACION_OBRA_MINIMO_DIAS` | 15 | CST art. 64 | vigente | **nuevo** |
| `SANCION_MORA_ART65_TOPE_MESES` | 24 | CST art. 65 (Ley 789/2002 art. 29) | vigente | **nuevo** (informativo) |
| `SALARIO_INTEGRAL_MINIMO_SMMLV` / `_FACTOR_PCT` | 10 / 30 | CST art. 132 | vigente | **nuevo** (validación de ficha) |
| `SALARIO_INTEGRAL_BASE_PCT` | 70 | CST art. 132 num. 3; Ley 100/1993 art. 18 | vigente | existe |
| `APRENDIZ_LECTIVA_APOYO_PCT` / `APRENDIZ_PRACTICA_APOYO_PCT` | 75 / 100 | Ley 2466/2025 art. 21; Circular Mintrabajo 0083/2025 | 2025-06-25 | **nuevo** |
| `SALUD_APRENDIZ_PCT` | 12,5 | Ley 2466/2025 art. 21 | 2025-06-25 | existe |
| `HORAS_MES` | 210 | Ley 2101/2021 (42 h desde 15-07-2026) | 2026-07-15 | existe |
| `RETEFTE_TABLA_UVT` (marginal; umbral 95) | 0-95: 0; >95-150: 19 %; >150-360: 28 %+10; >360-640: 33 %+69; >640-945: 35 %+162; >945-2300: 37 %+268; >2300: 39 %+770 | ET art. 383 (Ley 2010/2019) | 2020 | existe |
| `RETEFTE_RENTA_EXENTA_PCT` | 25 | ET art. 206 num. 10 | vigente | existe |
| `RETEFTE_RENTA_EXENTA_TOPE_UVT` (mensual) | 65,83 (= 790/12) | ET art. 206 num. 10 (Ley 2277/2022 art. 2) | 2023 | existe |
| `RETEFTE_RENTA_EXENTA_TOPE_ANUAL_UVT` | 790 | ET art. 206 num. 10 (Ley 2277/2022 art. 2) | 2023-01-01 | **nuevo** |
| `RETEFTE_DEDUCCIONES_TOPE_PCT` / `_TOPE_UVT` (mensual) | 40 / 111,67 | ET art. 388; DUR 1.2.4.1.6 par. 3 (Decreto 2231/2023 art. 9) | 2023-12-22 | existe |
| `RETEFTE_DEDUCCIONES_TOPE_ANUAL_UVT` | 1.340 | ET art. 388; DUR 1.2.4.1.6 par. 3 | 2023-12-22 | **nuevo** |
| `RETEFTE_DEPENDIENTES_PCT` / `_TOPE_UVT` | 10 / 32 | ET art. 387 | vigente | existe |
| `RETEFTE_INT_VIVIENDA_TOPE_UVT` | 100 | ET art. 387 | vigente | existe |
| `RETEFTE_MED_PREPAGADA_TOPE_UVT` | 16 | ET art. 387 | vigente | existe |
| `RETEFTE_P2_DIVISOR` | 13 | ET art. 386 | vigente | **nuevo** |
| `RETEFTE_REDONDEO` | 1.000 | práctica DIAN (norma exacta **no verificada**) | vigente | existe |
| `CESANTIAS_EXENCION_TOPE_UVT` | 350 (ingreso mensual promedio 6 meses) | ET art. 206 num. 4 | vigente | **nuevo** |
| `CESANTIAS_GRAVADA_TABLA_UVT` (RangeTable, no marginal; % **no gravado**) | ≤350: 100; >350-410: 90; >410-470: 80; >470-530: 60; >530-590: 40; >590-650: 20; >650: 0 | ET art. 206 num. 4 | vigente | **nuevo** |
| `INDEMNIZACION_RETEFTE_PCT` / `_TOPE_UVT` | 20 / 204 (ingreso mensual del trabajador) | ET art. 401-3 (Ley 788/2002 art. 92); Concepto DIAN 30573/2015 | vigente | **nuevo** |
| `SALUD_EMPLEADO_PCT` / `SALUD_EMPLEADOR_PCT` | 4 / 8,5 | Ley 100/1993 art. 204; Ley 1122/2007 art. 10 | vigente | existe |
| `PENSION_EMPLEADO_PCT` / `PENSION_EMPLEADOR_PCT` | 4 / 12 | Ley 100/1993 art. 20; Ley 797/2003 art. 7 | vigente | existe |
| `FSP_UMBRAL_SMMLV` | 4 | Ley 100/1993 art. 27; Ley 797/2003 art. 8 | vigente | **nuevo** (hoy implícito en la tabla) |
| `FSP_TABLA` (Ley 797) | ≥4: 1,0; ≥16-17: 1,2; >17-18: 1,4; >18-19: 1,6; >19-20: 1,8; >20: 2,0 | Ley 797/2003 art. 8 | hasta **2027-03-31** | existe; poner `ValidTo` y Source |
| `FSP_TABLA` (Ley 2381, sólo sin transición) | ≥4-<7: 1,5; ≥7-<11: 1,8; ≥11-<19: 2,5; ≥19-<20: 2,8; ≥20: 3,0 | Ley 2381/2024 art. 20; Sentencia C-264 del 02-09-2026 | desde **2027-04-01** | **nuevo** |
| `ARL_CLASE_I..V_PCT` | 0,522 / 1,044 / 2,436 / 4,350 / 6,960 | Decreto 1772/1994 art. 13 (Decreto 1072/2015) | vigente | existe |
| `CAJA_PCT` / `SENA_PCT` / `ICBF_PCT` | 4 / 2 / 3 | Ley 21/1982; Ley 89/1988 | vigente | existe |
| `EXONERACION_PARAFISCALES_TOPE_SMMLV` | 10 | ET art. 114-1 (Ley 1607/2012 art. 25; Ley 1819/2016 art. 65; Ley 1955/2019 art. 204) | vigente | existe |
| `IBC_TOPE_SMMLV` | 25 | Ley 100/1993 art. 18 (Ley 797/2003 art. 5) | vigente | existe |
| `IBC_MINIMO_SMMLV` | 1 (proporcional a días para salud/pensión/ARL) | Ley 797/2003 art. 5; AT2 v30 «01 - Dependiente» | vigente | **nuevo** |
| `PILA_IBC_REDONDEO` | 1 (al peso superior) | Decreto 780/2016 art. 3.2.1.5 (Decreto 1990/2016) | vigente | **nuevo** |
| `PILA_APORTE_REDONDEO_MULTIPLO` | 100 (al múltiplo superior) | Decreto 780/2016 art. 3.2.1.5 (Decreto 1990/2016) | vigente | **nuevo** |
| `PILA_PLAZO_PAGO_POR_NIT` (tabla por dos últimos dígitos → día hábil) | 00-07: 2.º … 94-99: 16.º | Decreto 780/2016 art. 3.2.2.1 (Decreto 923/2017) | vigente | **nuevo** (dato para el aviso) |
| `DIAN_PLAZO_TRANSMISION_DIAS` | 10 | Res. 227/2025 art. 1.5.3.4.1.1 | vigente | **nuevo** |
| Recargo dominical/festivo (factor del concepto, seeder de conceptos) | 80 % → 90 % (2026-07-01) → 100 % (2027-07-01) | Ley 2466/2025 | escalonado | conceptos: revisar vigencias |
| `SMMLV`, `AUX_TRANSPORTE`, `UVT` **2027** | no encontrados (se decretan a fines de diciembre de 2026) | — | 2027-01-01 | **falta**; `Revisiones()` está vacía (`PayrollLegalParametersSeeder.cs:173-178`) |

Políticas por empresa (`PAY_CompanyPolicies`, con vigencia): `Exonerada114_1` (sí/no; COOFLOPAL:
sí según la spec, a confirmar), `SemanaLaboral` (`LunesASabado` por defecto / `LunesAViernes`),
`RetefteTopesAnualesModo` (`Acumulado` / `Mensualizado`), `CotizaArlEnVacaciones` (no por defecto),
`P2SecuenciaDepuracion` (`DepurarLuegoDividir` / `DividirLuegoDepurar`), `DianPlazoComputo`
(`Calendario` por defecto / `Habiles`), `AllowSameUserApproval` (migrada de `COR_SystemSettings`).

**Razón**: `PayrollLegalParameter` ya soporta tablas por tramos en UVT/SMMLV, marginales o planas
(`RangeTableRule.cs:14-75`), exactamente lo que necesitan las tablas del art. 64, del art. 206
num. 4 y las dos del FSP. El `Source` existe pero hoy es genérico («Decreto de salario mínimo…
2026»): la contadora tiene que poder trazar cada valor a su artículo (SC-001). La Corte fijó la
vigencia de la Ley 2381 el 01-04-2027 y el operador miplanilla lo confirma: sin la segunda tabla y
la bandera de transición, la PILA de abril de 2027 sale mal para quien no esté en transición. El
resto de valores de la tabla de la investigación laboral coincide con los ya sembrados (SMMLV,
auxilio, UVT, tarifas, tabla 383).

**Alternativas consideradas**: agregar los códigos a `Required` con una migración de datos que
los siembre en todas las bases: garantiza consistencia pero acopla el despliegue a la migración y
a valores aún no validados por la contadora; seguir en `COR_SystemSettings` sin vigencia: cero
migración, pero un cambio de política a mitad de año reescribe el pasado y no hay forma de liquidar
un semestre anterior con la regla que regía (FR-015, FR-024a exigen vigencia).

**Fuentes**: `PayrollLegalParametersSeeder.cs:33-127, 173-178`; `LegalParameterCodes.cs:240-312`;
`PayrollLegalParameter.cs:14-50, 242-282`; `PayrollPolicyReader.cs:66-102`; valores 2026:
https://www.hklaw.com/en/insights/publications/2025/12/colombia-decreta-aumento-del-salario-minimo-y-auxilio-de-transporte ;
https://actualicese.com/el-consejo-de-estado-revoco-la-suspension-del-decreto-del-salario-minimo-en-colombia-2026/ ;
https://www.dian.gov.co/normatividad/Normatividad/Resoluci%C3%B3n%20000238%20de%2015-12-2025.Pdf ;
ET arts. 206, 383, 385-388, 401-3 (https://actualicese.com/estatutotributario/206-2/ ;
https://blog.alegra.com/colombia/retencion-en-la-fuente-por-salarios-tabla/ ;
https://www.funcionpublica.gov.co/eva/gestornormativo/norma.php?i=199883 ;
https://www.alcaldiabogota.gov.co/sisjur/normas/Norma1.jsp?i=152690); FSP y reforma pensional:
AT2 v30 págs. 156-158;
https://incp.org.co/publicaciones/infoincp-publicaciones/informacion-para-empresas/2026/09/corte-constitucional-declaro-exequible-la-mayor-parte-de-la-reforma-pensional/ ;
https://ayuda.miplanilla.com/hc/es-419/articles/37892245907732 ; redondeos PILA:
https://aportesenlinea.custhelp.com/app/answers/detail/a_id/153/~/norma-redondeos-y-aproximaciones ;
https://www.funcionpublica.gov.co/eva/gestornormativo/norma.php?i=78396 ; plazos PILA:
https://normativa.colpensiones.gov.co/colpens/docs/decreto_0923_2017.htm ; exoneración:
https://normograma.dian.gov.co/dian/compilacion/docs/oficio_dian_26845_2019.htm.

## R5. Retención en la fuente de las liquidaciones especiales (FR-006a)

**Decisión**: extraer de `WithholdingBaseBuilder.Build(RuleContext)` una función pura
`DepuracionDeRetencion.Depurar(bruto, aportesObligatorios, deduccionesDeclaradas, ParameterSet,
proporción, modoTopes, acumuladoAnual)` que devuelva `Depuration(Base, Steps)` y usarla desde el
motor ordinario, el de liquidaciones y el de P2. Reglas por rubro, todas con parámetros de R4:

- **Prima**, procedimiento 1: retención **independiente** sobre la prima sola (ET art. 385:
  «el valor a retener es el que figure frente al intervalo al cual corresponda la respectiva
  prima»): sin restar aportes (no se cotiza sobre ella), restando el 25 % exento dentro del cupo,
  tabla 383, redondeo `RETEFTE_REDONDEO`. Ej.: prima 7.000.000 → 25 % 1.750.000 → 5.250.000 =
  100,24 UVT → **52.000**; prima ≤ 6.634.040 → 0. Procedimiento 2: la prima **se suma** a los
  pagos gravables del mes en que se paga y se aplica el porcentaje fijo (art. 386 inciso 1); como el
  pago es aparte (Clarifications), la liquidación de prima calcula la retención con el porcentaje
  del empleado sobre la prima depurada y la explica.
- **Cesantías e intereses** (ET art. 206 num. 4): promedio del ingreso laboral de los **seis
  últimos meses** en UVT → % no gravado según `CESANTIAS_GRAVADA_TABLA_UVT` → parte gravada
  retenida de forma **independiente** con la tabla 383 (art. 386 la excluye del P2). Las cesantías
  **consignadas al fondo** no se retienen al consignar (la realización es del trabajador); aplica al
  pago directo (retiro, intereses). Ej.: promedio 20.000.000 (381,87 UVT → 90 % exento), 22.400.000
  × 10 % = 2.240.000 = 42,8 UVT → **0**.
- **Indemnización** (ET art. 401-3; Concepto DIAN 30573/2015): si el **ingreso mensual** del
  trabajador > 204 UVT, retención = (indemnización − 25 % exento dentro del cupo anual) × 20 %;
  sin restar aportes; si ≤ 204 UVT, cero. Ej.: ingreso 12.000.000, indemnización 52.000.000 →
  **7.800.000**. Bonificación por retiro voluntario: mismo tratamiento.
- **Vacaciones** (disfrutadas o compensadas) y **salario pendiente**: entran como pago laboral
  ordinario del mes con el procedimiento del empleado (P1 tabla / P2 porcentaje); en la definitiva
  se depuran junto con el salario pendiente del período.
- **Topes anuales** (790 UVT del 25 %; 1.340 UVT / 40 % de deducciones + exentas): política
  `RetefteTopesAnualesModo` = `Acumulado` (posición DIAN, Conceptos 3966/2023 y 100208192-87/2024:
  el agente observa el límite anual por persona; el cupo consumido en el año se guarda por empleado
  y se muestra en la explicación) o `Mensualizado` (790/12 = 65,83 y 1.340/12 = 111,67, práctica
  extendida, lo que hoy hace la nómina ordinaria). **Variante sin norma que la fije**: la elige la
  contadora. No aplicar en retención la deducción de 72 UVT por dependiente (art. 336; el art. 388
  remite al 387); sí aplicar el tope 40 %/1.340 (DUR 1.2.4.1.6 par. 3 desde el Decreto 2231/2023).

**Razón**: la spec exige retención automática, con topes y tarifas parametrizados y explicación por
valor, y que la responsable no la digite. La depuración hoy vive dentro del motor ordinario atada a
`RuleContext` (`WithholdingBaseBuilder.cs:179-294`); duplicarla en la liquidación y en P2 sería
escribir la norma tres veces. `RangeTableRule` ya resuelve tramos marginales y planos en UVT.

**Alternativas consideradas**: sólo modo mensualizado (más conservador para el trabajador, pero
retiene de más cuando la prima cae en un mes y se pierde el cupo del año); llamar al motor ordinario
mes a mes con entradas sintéticas (exige todos los parámetros requeridos, afiliaciones y novedades,
y produce líneas que no se usan); retener la prima sumada al mes también en P1 (contradice el art.
385).

**Fuentes**: `WithholdingBaseBuilder.cs:179-294`; `PayrollCalculationEngine.cs:181-208, 374-404`;
`Rules/RangeTableRule.cs:14-75`; ET art. 385 (https://actualicese.com/estatutotributario/385-2/);
art. 386 (https://actualicese.com/estatutotributario/386-2/); art. 206 num. 4
(https://actualicese.com/estatutotributario/206-2/); Concepto DIAN 30573/2015
(https://normograma.dian.gov.co/dian/compilacion/docs/concepto_tributario_dian_0030573_2015.htm);
Oficio DIAN 3966/2023 (https://normograma.dian.gov.co/dian/compilacion/docs/oficio_dian_3966_2023.htm);
Concepto 100208192-87/2024 (https://www.gydconsulting.com/quienes-tienen-derecho-a-la-renta-exenta-de-trabajo-y-como-se-aplica-la-retencion-en-la-fuente-concepto-dian-100208192-87/);
Decreto 2231/2023 (https://www.alcaldiabogota.gov.co/sisjur/normas/Norma1.jsp?i=152690);
https://www.gerencie.com/retencion-en-la-fuente-en-la-prima-de-servicios.html ;
https://www.gerencie.com/retencion-en-la-fuente-por-indemnizaciones-laborales.html.

## R6. Vacaciones: calendario, días hábiles y novedad (FR-014 a FR-017)

**Decisión**:

- **Semana laboral** como política por empresa con vigencia (`PAY_CompanyPolicies.SemanaLaboral`):
  `LunesASabado` por defecto, `LunesAViernes` opcional. Es la decisión del dueño («A, pero
  parametrizables, porque hay empresas que trabajan de lunes a viernes y otras de lunes a sábado»).
  Un override por ficha queda para una fase posterior.
- **Festivos**: tabla `PAY_Holidays` (fecha, nombre, origen `Ley51/Decretado/Manual`, vigencia)
  sembrada para 2026-2028 con la regla de la Ley 51 de 1983 (fijos, trasladados al lunes y los que
  dependen de Pascua) y editable, para que un puente decretado tenga dónde registrarse.
- Función pura en Domain `DiasHabiles.Contar(desde, hasta, semanaLaboral, festivos)` →
  `(hábiles, calendario, saltados: [(fecha, motivo)])`, con explicación de cada día saltado.
  Caso fijo de la spec: 28-10-2026 a 10-11-2026 → **11** hábiles L–S, **9** L–V, 14 calendario, con
  el 01-11 domingo y el 02-11 festivo excluidos.
- **Movimientos de vacaciones** `PAY_VacationMovements` (empleado, tipo `Disfrute/Compensacion/
  Ajuste`, fechas, días hábiles, días calendario, corrida especial asociada, estado); el saldo se
  deriva: causado (`DíasTrabajados × VACACIONES_DIAS_ANIO / 360`, descontando suspensiones, art. 53
  CST) + saldo inicial − disfrutado − compensado. La pantalla muestra hábiles y calendario **antes**
  de guardar (FR-015).
- **Compensación en dinero** limitada a `VACACIONES_COMPENSABLE_PCT` (50 %) de lo causado (CST
  art. 189 num. 1); al retiro, el total pendiente (art. 189 num. 2-3; Ley 995/2005).
- **Base**: salario ordinario del día en que inicia el disfrute, sin auxilio de transporte, sin
  extras ni dominicales; promedio del último año si el salario es variable (art. 192). El auxilio se
  modela con **dos banderas por concepto**: «entra a prestaciones» (prima y cesantías, Ley 1/1963
  art. 7; hoy `AffectsBenefitsBase`) y una nueva «entra a vacaciones/indemnización» (no), en lugar
  de excepciones en el código.
- **Novedad de ausencia**: reutilizar el concepto sembrado `VACACIONES` (Earning,
  QuantityTimesUnit/Day, `RequiresDates`, `ReducesWorkedDays`, afecta las cuatro bases,
  `PayrollConceptDefinitionsSeeder.cs:118-122`) generando una novedad por cada período que cubren
  las fechas, con un `NoveltyOrigin` nuevo (`VacationLeave`) para distinguirla y aplicarle la regla
  de anulación/regeneración de las recurrentes; si un período está aprobado, aplica
  `NoveltyRules.EnsureEditableAsync` y el ajuste retroactivo (`RetroactiveOfPeriodPublicId`), como
  pide el edge case. La nómina ordinaria paga esos días calendario como «vacaciones» y no como
  salario (práctica de nómina; confirmar con la contadora si paga los calendario o sólo los
  hábiles).
- **Aportes durante vacaciones**: se causan completos sobre el último IBC anterior al disfrute
  (Decreto 780/2016 art. 3.2.5.1, del Decreto 806/1998 art. 70); novedad PILA `VAC`. **ARL**:
  variante —Decreto 1772/1994 art. 19 y la práctica de los operadores (no cotizar) frente a un
  concepto de Mintrabajo (cotizar)—: política `CotizaArlEnVacaciones` (no por defecto), leída por
  la PILA y por los aportes del empleador de la ordinaria.
- La contabilización de la liquidación de vacaciones cancela `PROV_VACACIONES` (R7 contable en R12/
  FR-004) y la definitiva paga los días pendientes sobre el salario base del retiro.

**Razón**: no existe en el código calendario de festivos, semana laboral ni contador de hábiles
(`grep festivo|holiday` sólo halla `Employee.HolidayDays` legado; `CalendarConventions` es
comercial de 30 días); la US4 lo exige y el dueño confirmó el defecto y la parametrización. La ley
no define el sábado; el art. 172 CST sólo hace obligatorio el domingo, y los conceptos Mintrabajo
132494/2011 y 43141/2014 lo hacen depender de la jornada pactada/RIT; la Ley 2101/2021 permite
distribuir las 42 h en 5 o 6 días.

**Alternativas consideradas**: fijar L–S para todos (incorrecto para empresas L–V); leerlo de la
jornada de cada empleado (más fino; la spec lo define por empresa); calcular festivos en código sin
tabla (un puente decretado no tendría dónde registrarse); descontar sólo hábiles en la nómina
ordinaria (rompe la convención 30/360 del período).

**Fuentes**: spec Clarifications (sábado), FR-015; `CalendarConventions.cs:3-11`;
`PayrollConceptDefinitionsSeeder.cs:118-122`; `NoveltyRules.cs:31-50`;
`RegisterNoveltyCommand.cs:14-30`; `Enums/Payroll/NoveltyOrigin.cs`; CST arts. 172, 186-192
(https://www.cancilleria.gov.co/sites/default/files/Normograma/docs/pdf/codigo_sustantivo_trabajo_pr006.pdf ;
https://leyes.co/codigo_sustantivo_del_trabajo/192.htm); sábado hábil:
https://www.gerencie.com/el-sabado-como-dia-habil-para-el-computo-de-las-vacaciones.html ;
https://www.ambitojuridico.com/noticias/laboral/laboral-y-seguridad-social/reglamento-interno-de-trabajo-determina-si-el-sabado-es ;
Ley 995/2005 (https://www.suin-juriscol.gov.co/viewDocument.asp?id=1672390); aportes en
vacaciones: https://www.gerencie.com/seguridad-social-del-trabajador-en-vacaciones.html ;
https://siigonube.portaldeclientes.siigo.com/calcular-el-ibc-cuando-hay-novedades-de-vacaciones-en-nomina-pro-plus/ ;
Ley 51/1983 (festivos; calendario 2026-2027 **no descargado**, es determinista).

## R7. Liquidación definitiva y descuentos de Cartera (FR-018 a FR-021)

**Decisión**:

- **Terminación**: extender `TerminateEmployeeCommand` (hoy sólo pone `Status = -1`,
  `TerminationDate`, `TerminationCause` texto libre y `Person.IsEmployee = false`,
  `TerminateEmployeeCommand.cs:14-72`) a `TerminateEmployeeCommand(EmployeePublicId,
  TerminationDate, TerminationReasonId, ContractType)` con: catálogo `PAY_TerminationReasons`
  sembrado por el programa (renuncia, despido sin justa causa, despido con justa causa art. 62,
  vencimiento del término con preaviso art. 46, mutuo acuerdo, terminación de la obra, período de
  prueba art. 78, muerte, pensión) con la marca `GeneratesSeverancePay`; validación FR-021 (la fecha
  cae en un período `Open`; si cae en uno `Approved` → `Payroll.PeriodApproved` con la indicación
  de reversar o liquidar en el abierto); creación de la corrida `Settlement` en borrador con la
  misma acción; auditoría `Payroll.Employee.Terminated`. La ficha se cierra **al aprobar** la
  definitiva (FR-020), no al registrar; la reversión de la definitiva reabre la ficha
  (`Status`, `TerminationDate = MaxValue`, `Person.IsEmployee = true`) en la misma transacción.
- **Contenido** (un solo documento): salario de los días pendientes del período (el loader ya
  excluye al retirado antes del período y liquida por días al retirado dentro,
  `CalculationInputLoader.cs:71-80, 194-195`), auxilio proporcional, extras/recargos causados,
  cesantías del año en curso (base art. 253) e intereses proporcionales (Decreto 116/1976 art. 2),
  prima proporcional del semestre, vacaciones pendientes (R6), indemnización según
  `INDEMNIZACION_TABLA` y el motivo, deducciones de ley sobre lo salarial, retención (R5),
  deducciones de Cartera y libranzas. La prima ya pagada en una definitiva se descuenta en la prima
  del semestre (FR-009) buscando corridas `Settlement` aprobadas del empleado en el semestre.
- **Descuento de préstamos de la cooperativa** (D-08 existente): al calcular, leer
  `LoanPortfolio` por `PersonId` (`CurrentBalance`, `CapitalBalanceCurrent`,
  `InterestBalanceCurrent`, `DefaultBalanceCurrent`, `PendingInstallmentCount`, cuotas vía
  `GetLoanPortfolioByIdQuery`) y proponer el **saldo total** de cada obligación hasta donde alcance
  el neto, en una tabla `PAY_SettlementDeductionAdjustments` (obligación, propuesto, aplicado,
  motivo, quién, cuándo); la responsable valida y sólo puede **bajar** con motivo; al aprobar, cada
  descuento se aplica con `ProcessPaymentCommand` por obligación (`PaymentMethod` nuevo «NM»
  —descuento por liquidación de nómina— o «TR», `Reference` = número de la liquidación) y la línea
  de nómina queda como `DESC_CARTERA` con `AffectsAccounting = false` (Cartera contabiliza el
  recaudo, la nómina no lo duplica); la pantalla muestra el saldo que queda
  (`PaymentResultDto.Remaining`) tras el descuento (FR-018a). Todo va a auditoría
  (`Payroll.Settlement.DeductionAdjusted`).
- **Libranzas con terceros**: no están en Cartera; son recurrentes `LIBRANZA`
  (`PayrollRecurringNovelty.InstallmentsIssued`); se descuentan sólo las **cuotas causadas y no
  descontadas** derivadas de la recurrente y los períodos aprobados; el resto lo cobra el tercero.
- **Sanción moratoria** (CST art. 65; Ley 50 art. 99 num. 3; Ley 52/1975) como cálculo
  **informativo** bajo demanda («si paga hoy, la sanción sería…»), nunca línea automática: su
  procedencia depende de la mala fe y de un juez (CSJ SL194-2019, SL2338-2023).
- **Documento para firma** (FR-020): modelo y renderizador QuestPDF propios
  (`SettlementDocumentModel`: empresa/NIT, empleado/documento, cargo, fechas de ingreso y retiro,
  motivo, tipo de contrato, salario base, cada rubro con base y días, deducciones con propuesto/
  aplicado, neto, espacio de firmas), distinto del comprobante de nómina.

**Razón**: FR-018a exige propuesta automática desde Cartera, validación, modificación hacia abajo
con motivo y auditoría; el patrón D-08 (Cartera contabiliza, la nómina muestra) ya existe en la
ordinaria (`CalculationInputLoader.cs:114-121, 178-185`) y evita el doble asiento. El
`TerminationCause` de 120 caracteres no permite saber si el motivo genera indemnización; la spec
pide catálogo con marca. `PayrollCalculationBatch`/`CalculatePayrollRunCommand` son la plantilla
directa del cálculo y la persistencia.

**Alternativas consideradas**: registrar novedades `PRESTAMO_EMP` manuales con el valor (Cartera no
se entera, el saldo no baja, se pierde la trazabilidad); cerrar la ficha al registrar la
terminación (un error obligaría a reabrir a mano antes de recalcular); catálogo de motivos cargado
por la cooperativa (las marcas legales son fijas; el catálogo es semilla del programa y la
cooperativa puede agregar motivos propios sin indemnización).

**Fuentes**: `TerminateEmployeeCommand.cs:14-72`; `Employee.cs:23-153`;
`EmpleadoDetalle.razor:321-336`; `CalculationInputLoader.cs:71-80, 114-121, 178-185, 194-195`;
`LoanPortfolio.cs:8-80`; `PersonPortfolioReportQuery.cs:10-86`; `LoanPortfolioQueries.cs:78-291`;
`ProcessPaymentCommand.cs:13-40`; `ProcessPayrollDeductionCommand.cs:19`;
`PayrollDeductionEntry.cs:7-26`; `NoveltyRules.cs:31-50`; CST arts. 46, 62, 64, 65, 78
(https://www.cancilleria.gov.co/sites/default/files/Normograma/docs/pdf/codigo_sustantivo_trabajo_pr002.pdf ;
https://leyes.co/codigo_sustantivo_del_trabajo/65.htm);
https://cortesuprema.gov.co/rl_dl_sl2338-2023/ ;
https://www.ambitojuridico.com/noticias/laboral/laboral-y-seguridad-social/mintrabajo-recuerda-como-aplica-la-indemnizacion-por-no.

## R8. Procedimiento 2: porcentaje fijo semestral (FR-022, FR-023)

**Decisión**: comando `CalculateWithholdingRatesCommand(Semester)` que, para cada empleado con
`WithholdingProcedure == 2`: (1) suma los pagos gravables de los **doce meses anteriores** al mes
del cálculo (junio o diciembre) desde `PAY_PayrollRunLines` (Nature Earning con
`AffectsWithholdingBase`, corridas `Approved` no reversadas, ordinarias **y especiales**
—prima sí; cesantías e intereses **excluidas** por el art. 386—) agrupadas por
`PayPeriod.ImputationYear/ImputationMonth` (`PayPeriod.cs:47-51`) o por mes de la fecha de corte de
la especial; (2) resta los aportes obligatorios reales (`SALUD_EMP`, `PENSION_EMP`, `FSP`) de esas
corridas; (3) depura con la función pura de R5 (deducciones declaradas con tope mensual × meses, 25 %
con tope anual, tope 40 %/1.340); (4) divide por `RETEFTE_P2_DIVISOR` = 13 (o por los meses de
vinculación si son menos de doce; empleado nuevo: la totalidad del mes hasta el primer cálculo);
(5) busca la retención teórica con la tabla vigente (`RETEFTE_TABLA_UVT` o la del plan vía
`TablaDeRetencionDelPlan.Aplicar`, `TablaDeRetencionDelPlan.cs:118-165`) usando el helper de tramos
de R1; (6) porcentaje = retención teórica ÷ base depurada promedio × 100 (Oficio DIAN 68292/2013;
la tabla ya no es por intervalos sino marginal). Resultado en `PAY_WithholdingRateCalculations`
(empleado, semestre, doce meses con ingreso/aportes/depuración, promedio, retención teórica,
porcentaje, estado `Calculated/Approved`, explicación mes a mes). Al aprobar: cierra la vigencia
actual en `PAY_EmployeeWithholdingRates` y abre la nueva (julio–diciembre o enero–junio) con la
misma auditoría `Payroll.EmployeeWithholding.Changed`; la ordinaria la aplica sin cambios
(`WithholdingProcedure2`, `PayrollCalculationEngine.cs:181-208`). Secuencia de depuración como
política con vigencia (`P2SecuenciaDepuracion`): (i) depurar la sumatoria y luego ÷ 13
(consultorcontable) o (ii) ÷ 13 y depurar el promedio con topes mensuales; con el ejemplo de la
investigación dan **3,71 %** y **3,44 %**.

**Razón**: FR-022 exige «las mismas deducciones y rentas exentas que la nómina ordinaria aplica»
y la misma tabla; reutilizar la depuración extraída y `TablaDeRetencionDelPlan` lo garantiza. El
art. 386 dice «dividir por 13» (la prima entra en la sumatoria), no «promedio mensual». Hoy
`SetEmployeeWithholdingCommand` reemplaza todas las vigencias (`EmployeeWithholding.cs:60-65,
100-137`): la aprobación debe **cerrar** la anterior, no borrarla.

**Alternativas consideradas**: fijar una sola secuencia (más simple; que la contadora la elija y se
documente); llamar al motor ordinario con entradas sintéticas (ver R5); calcular desde
`BasesJson` (etiquetas serializadas, frágil) en vez de las líneas.

**Fuentes**: `EmployeeWithholdingRate.cs:186-197`; `EmployeeWithholding.cs:60-65, 100-137`;
`PayrollCalculationEngine.cs:181-208, 374-404`; `TablaDeRetencionDelPlan.cs:118-165`;
`PayPeriod.cs:47-51`; ET art. 386 (https://actualicese.com/estatutotributario/386-2/); Oficio
DIAN 68292/2013 (https://normograma.dian.gov.co/dian/compilacion/docs/oficio_dian_68292_2013.htm);
https://www.consultorcontable.com/retenci%C3%B3n-salarios-proc2/ ;
https://actualicese.com/archivo/procedimiento-2-de-retencion-en-la-fuente-sobre-pagos-laborales/.

## R9. PILA: formato 2388, Aportes en Línea, versiones e inconsistencias (FR-024 a FR-027)

**Decisión**:

- **Norma y versión**: Resolución 2388 de 2016 (MinSalud) y su Anexo Técnico 2 «Aportes a
  Seguridad Social de Activos», consolidado **v30 del 24-07-2026** (incorpora hasta la Resolución
  1529 de 2026). Cadena de modificaciones verificada: 5858/2016; 980, 1608, 3016/2017; 3559,
  5306/2018; 736, 1740, 2514/2019; 454, 686, 1438, 1844, 2421/2020; 014, 638, 1365, 1697/2021; 261,
  939, 2012/2022; 728, 1271/2023; 221, 738, 2520/2024; 467, 2064/2025; 010, 1529/2026 (no existe
  una «1126/2023»; la 2421 es de 2020).
- **Layout en datos**: `Application/Payroll/Pila/Layouts/at2-v30-2026-07-24.json` embebido (como
  la semilla del PUC), con cada campo: número, nombre, posición inicial, longitud, tipo A/N,
  alineación (N a la derecha con ceros; A a la izquierda con espacios), obligatoriedad y regla de
  origen (constante / ficha / cálculo / blanco). `PilaWriter` escribe ancho fijo, mayúsculas, sin
  tildes, CRLF, `.txt`, ASCII. Estructura del **archivo tipo 2**: un registro tipo 1 (encabezado,
  **22 campos, 359 posiciones**) + N registros tipo 2 (**98 campos, 693 posiciones**; la Res.
  2520/2024 sumó el campo 98 «Actividad económica ARL», Decreto 768/2022). Una versión nueva del
  anexo es un JSON nuevo con `ValidFrom`; las generaciones históricas conservan el suyo. La Res.
  1529/2026 estandariza longitudes de identificación (CC ≤ 10, TI ≤ 11, CE ≤ 7, PA ≤ 16
  alfanumérico, PE 15 exactos, PT ≤ 8…) exigibles para pagos desde el **01-10-2026**.
- **Entidades**: `PAY_PilaGenerations` (período aaaa-mm, versión, estado
  `Validated/Generated/Uploaded/Superseded`, layout usado, quién/cuándo, totales por subsistema,
  hash y nombre del archivo, número/fecha de radicación en el operador digitados), `PAY_PilaLines`
  (una fila por registro tipo 2 con los 98 campos tipados + `EmployeeId`, líneas de corrida de
  origen y explicación por valor), `PAY_PilaIssues` (severidad `Blocking/Warning`, campo, mensaje,
  enlace a ficha); archivo como adjunto cifrado. Regenerar crea versión N+1 y deja `Superseded`.
- **Motor puro** `Domain/Payroll/Pila/PilaBuilder` (layout, parámetros, política de exoneración,
  fichas y líneas de las corridas aprobadas del mes → líneas + inconsistencias). Reglas: una línea
  base por cotizante y una adicional por cada novedad con IBC distinto (IGE, LMA, VAC, SLN, IRL) y
  por cada ING/RET adicional; ING y RET del mismo mes en la **misma** línea; días por subsistema que
  suman 30 (28/30/31 → 30) salvo ING/RET; ARL tarifa 0 en IGE/LMA; SLN sólo tarifas del empleador
  y FSP 0; IBC salud/pensión/ARL ≥ 1 SMMLV proporcional, tope 25 SMMLV, integral 70 %, auxilio
  excluido; IBC al peso superior; aportes al múltiplo de 100 superior; campo 20 = Σ campo 45; campo
  19 = cotizantes únicos; campo 15 = mes anterior al 16 en planilla E; campo 76 coherente con el
  campo 33 del aportante y con IBC ≥ 10 SMMLV; exoneración: campo 54 = tarifa del trabajador
  (0,04), SENA/ICBF en 0, CCF completa; campos 51/52 (FSP) calculados para el descuento al empleado
  pero marcados «los liquida el operador» (v30): una diferencia con el operador es alerta.
- **Tipos de cotizante** (planilla **E**): 1 dependiente; 19 aprendiz lectiva (Ley 2466); 12 sólo
  períodos anteriores a ago-2025; 51 tiempo parcial dependiente; pensionado activo = tipo 1 con
  **subtipo 1** y tarifa de pensión 0; 31 cooperado de CTA **no** aplica a una cooperativa
  multiactiva. Derivación por regla paramétrica (`EmployeeClass` + etapa → tipo/subtipo) con
  posibilidad de fijarlos a mano en la ficha. Planillas N (correcciones) y A (ingresos omitidos)
  quedan fuera (decisión del dueño: se digitan en el operador).
- **Datos nuevos**: `PilaCode` (6) en EPS, AFP/ACCAI, ARL y CCF (distinto del `Code` de la
  cooperativa que sigue `CodigoDeCatalogo`); en la ficha: tipo/subtipo de cotizante, DIVIPOLA
  departamento (2) y municipio (3) laboral (default de la sucursal), actividad económica (7, Decreto
  768/2022, default de la empresa), centro de trabajo (campo 62), tipo de salario F/V/X derivado de
  `SalaryType`, extranjero no obligado a pensión, colombiano en el exterior, régimen de transición
  Ley 2381 (S/N/desconocido), indicador de alto riesgo (campo 79); datos del aportante: tipo (1),
  clase (B < 200), forma de presentación (U/S), código ARL, sucursal, exonerada S/N con vigencia
  (compartida con la ordinaria, FR-024a), dos últimos dígitos del NIT para proponer la fecha límite.
- **Validación previa** con la taxonomía de Aportes en Línea: **Bloqueante** (= Error del
  operador: sin EPS/AFP/ARL/CCF o sin `PilaCode`, sin DIVIPOLA, sin actividad económica, documento
  de longitud inválida, días que no suman 30 sin novedad, tarifa sin vigencia, layout sin versión
  vigente) y **Alerta** (diferencias de IBC entre subsistemas, IBC CCF ≠ salario + novedades, FSP
  recalculable por el operador, cotizante del mes anterior sin RET). Cuadre obligatorio antes de
  descargar: Σ campos 47/55/63/65/67/69 vs. suma de los conceptos de aportes en los comprobantes
  `NM` del mes por subsistema, mostrando la diferencia (FR-027).
- **Operador**: Aportes en Línea **acepta el archivo estándar de la resolución en .txt**
  (Liquidaciones → Adicionar liquidación → Cargar archivo → Validar) y clasifica en Error (bloquea),
  Alerta (deja cargar) y Posible corrección (autocorrige); no exige importador propio; no hay
  validador ni archivo de ejemplo público: la prueba SC-004 requiere la cuenta del aportante.

**Razón**: el anexo cambia varias veces al año (97 campos/686 posiciones en 2019; 98/693 en 2026;
longitudes nuevas desde oct-2026): con layout en datos una versión nueva no es un despliegue y las
generaciones anteriores conservan la suya (Principio XI). Los campos 31/33/35/77, 9/10, 98, 62, 41
y 76 son obligatorios y hoy no existen en catálogos ni ficha (`HealthInsuranceProvider.cs:12-27`,
`Employee.cs:38-101`); sin ellos el operador devuelve Error. Replicar sus validaciones y su lenguaje
reduce rechazos (SC-004). Mismo patrón que `PayrollCalculationEngine`: puro y probado con casos
dorados.

**Alternativas consideradas**: clase C# con 98 propiedades y atributos `[Posicion]` (cada cambio
del anexo es un despliegue); guardar sólo el archivo y los totales (pierde la trazabilidad por
cotizante); pedir los datos faltantes al generar cada mes (se digitarían once veces al mes).

**Fuentes**: AT2 v30
(https://www.minsalud.gov.co/sites/rid/Lists/BibliotecaDigital/RIDE/DE/OT/anexo-tecnico2-cotizantes.pdf):
numeral 1.1 págs. 8-16; 2.1.1.1 págs. 26-30; 2.1.2.1 págs. 81-102; 2.1.2.2 págs. 102-104;
2.1.2.3.1 págs. 104-118; aclaraciones págs. 142-166; cap. 5 págs. 329-335; Res. 1529/2026
(https://consultorsalud.com/wp-content/uploads/2026/07/Resolucion-1529-de-2026.pdf.pdf, OCR
defectuoso); https://incp.org.co/publicaciones/infoincp-publicaciones/informacion-para-empresas/2026/08/minsalud-incorporo-nuevos-tipos-de-cotizante-y-modifico-las-reglas-de-reporte-de-la-pila/ ;
Aportes en Línea: https://aportesenlinea.custhelp.com/app/answers/detail/a_id/363 ;
https://aportesenlinea.custhelp.com/ci/fattach/get/393/0/filename/Anexo+8.pdf ;
https://www.aportesenlinea.com/Documents/Instructivo%20Corrector%20por%20Pantalla%20v.1.3.pdf ;
https://ayuda.aleluya.com/es/articles/15061128-paso-a-paso-para-cargar-el-archivo-de-seguridad-social-en-aportes-en-linea ;
aprendices: https://derlaboral.uexternado.edu.co/analisis-y-opinion/la-regulacion-de-los-aprendices-en-la-reforma-laboral-ley-2466-de-2025/ ;
código: `Employee.cs:38-101`; `HealthInsuranceProvider.cs:12-27`; `PayrollLegalParameter.cs:14-50`;
`PayrollLegalParametersSeeder.cs:47-63, 96-106`.

## R10. Nómina electrónica DIAN: anexo, CUNE, firma, servicio web, servicio central, librerías, secretos (FR-028 a FR-031b)

**Decisión**:

- **Norma y anexo**: Res. 013/2021 compilada en la **Res. Única 000227 del 23-09-2025** (Parte 1,
  Título 5, Cap. 3: definiciones 1.5.3.1.1, documento 1.5.3.2.1, periodicidad 1.5.3.2.2, contenido
  1.5.3.3.1, plazo 1.5.3.4.1.1, habilitación 1.5.3.5.1.1, CUNE 1.5.3.5.1.3, transmisión 1.5.3.5.1.4,
  inconvenientes 1.5.3.5.1.5-6, validación 1.5.3.5.1.7, notas 1.5.3.5.1.9, anexo 1.5.3.7.1, terceros
  1.5.3.8.1, conservación 1.5.3.8.2, gráfica/QR 1.5.3.8.3; PT en 1.5.1.8.1.1). Único anexo vigente:
  **Anexo Técnico Documento Soporte de Pago de Nómina Electrónica v1.0** (269 págs.; no existe v1.1
  ni v3.0 en dian.gov.co). Caja de Herramientas V1-0: XSD **v1.0.6** (`NominaIndividualElectronicaXSDV1.0.6.xsd`,
  `NominaIndividualDeAjusteElectronicaXSDV1.0.6.xsd`), XML de ejemplo v1.0.2, esquemas UBL 2.1 y
  XAdES 1.3.2/1.4.1. Los XSD se embeben como recursos y se valida en local antes de firmar.
- **XML**: propio de la DIAN (no UBL) con raíces `NominaIndividual` (TipoXML **102**) y
  `NominaIndividualDeAjuste` (TipoXML **103**, `TipoNota` 1 Reemplazar / 2 Eliminar), namespaces
  `dian:gov:co:facturaelectronica:NominaIndividual[DeAjuste]`; sólo la firma usa UBL
  (`ext:UBLExtensions`). `InformacionGeneral/@Version` = literal «V1.0: Documento Soporte de Pago de
  Nómina Electrónica» (NIE022). Orden y atributos según anexo 3.1 (Periodo con `TiempoLaborado`
  360/30, `NumeroSecuenciaXML` = Prefijo + Consecutivo sin espacios ni guiones, `LugarGeneracionXML`
  con DANE 2/5 dígitos, `ProveedorXML`, `CodigoQR`, `InformacionGeneral` con `PeriodoNomina` 4
  quincenal, `Empleador`, `Trabajador` con TipoTrabajador PILA y TipoContrato 1-5, `Pago` con método
  tabla 5.3.3.2, `FechasPagos` 1-N —las dos quincenas—, `Devengados` (Basico obligatorio; Primas,
  Cesantias/@PagoIntereses, Vacaciones, Indemnizacion, BonifRetiro…), `Deducciones` (Salud y
  FondoPension obligatorias; Libranzas, Cooperativa, RetencionFuente…), totales). Valores con punto
  y dos decimales, sin negativos. **Documento mensual por empleado** acumulando las quincenas y las
  liquidaciones especiales pagadas en el mes (art. 1.5.3.2.2; FR-011a); `FechaRetiro` sólo en el mes
  del retiro; un empleado sin pago en el mes no genera documento. Nombres de archivo `nie` / `niae`
  + NIT 10 dígitos + aa + 8 hex; ZIP `z…` con un solo documento (anexo 3.3-3.5).
- **CUNE** (anexo 8.1) = SHA-384(Numero + FechaGen + HoraGen con GMT + DevengadosTotal +
  DeduccionesTotal + ComprobanteTotal + NIT empleador sin DV + documento trabajador + TipoXML + PIN +
  Ambiente), hexadecimal minúscula (96); en Eliminar los tres totales «0.00» y DocEmp «0».
  **SoftwareSC** = SHA-384(SoftwareID + PIN + Numero). Ambiente 1 producción / 2 pruebas.
  **VERIFICADO**: el ejemplo del anexo (pág. 245-249) **no reproduce su propio hash**; el caso
  dorado real es un documento **aceptado en habilitación** (`XmlDocumentKey` = CUNE).
- **Firma**: XAdES-EPES enveloped en `ext:UBLExtensions/…/ds:Signature`; C14N inclusiva
  `http://www.w3.org/TR/2001/REC-xml-c14n-20010315`; `rsa-sha256`; tres `ds:Reference` (documento
  con enveloped-signature, KeyInfo, SignedProperties); `SigningTime` con zona -05:00;
  `SignaturePolicyIdentifier` = `https://facturaelectronica.dian.gov.co/politicadefirma/v2/politicadefirmav2.pdf`,
  descripción «Política de firma para nóminas electrónicas de la República de Colombia»,
  `SigPolicyHash` SHA-256 base64 **`dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=`** (recalculado sobre
  el PDF descargado, 1.272.898 bytes); `SignerRole` «supplier» (firma el empleador) o «third party»
  (firma un PT); certificado de ECD acreditada por ONAC (Certicámara, GSE, Andes SCD, Camerfirma) con
  Digital Signature + Non Repudiation, sha256WithRSA.
- **Servicio web**: `https://vpfe-hab.dian.gov.co/WcfDianCustomerServices.svc` (habilitación) y
  `https://vpfe.dian.gov.co/WcfDianCustomerServices.svc` (producción); SOAP 1.2 document/literal
  sobre TLS 1.2, WS-Addressing (`wsa:Action` = `http://wcf.dian.colombia/IWcfDianCustomerServices/<Op>`),
  WS-Security X.509 Token Profile: `wsu:Timestamp` (~1 min), `wsse:BinarySecurityToken`, firma
  exc-c14n + rsa-sha256 del header `wsa:To` con `SecurityTokenReference`. Operaciones:
  `SendNominaSync(contentFile zip base64)` → `DianResponse`; `SendTestSetAsync(fileName, contentFile,
  testSetId)` → `UploadDocumentResponse{ZipKey, ErrorMessageList}`; `GetStatus(trackId = CUNE)`,
  `GetStatusZip(trackId = ZipKey)`. `DianResponse{IsValid, StatusCode 00/99, StatusDescription,
  StatusMessage, ErrorMessage[] «Regla: NIExxx, Rechazo|Notificación: …», XmlBase64Bytes =
  ApplicationResponse firmado (ResponseCode 02 validado / 04 rechazado), XmlDocumentKey = CUNE}`.
  Códigos 66 (ya procesado) y 90 (trackId no encontrado) reportados por la comunidad, **no
  verificados**: observar en habilitación.
- **Arquitectura del servicio central de Ingenia365** (FR-031a/b): nuevo proyecto
  `src/Servicios/IngenIA365.NominaElectronica` (.NET 10 Minimal API), multi-tenant por `tenantId`
  con filtro global y credencial ligada al tenant, base PostgreSQL **propia** (no las de
  cooperativa): `Habilitacion` (tenant, NIT, DV, razón social, **modo** `SoftwarePropio|ProveedorTecnologico`,
  ambiente, SoftwareID, TestSetId, estado del set, referencia al secreto), `Certificado` (tenant,
  thumbprint, emisor, vigencia, .p12 y contraseña cifrados), `Documento` (tenant, tipo 102/103,
  número, CUNE, documento del empleado, mes, estado `Generado/Firmado/Transmitido/Aceptado/Rechazado/EnProceso`,
  intentos, fechaGen/horaGen, totales), `Envio` (documento, operación, hash del request,
  StatusCode, IsValid, errores, XmlDocumentKey, duración); blobs inmutables (XML firmado, ZIP,
  ApplicationResponse, representación gráfica) en el almacenamiento de objetos ya usado por
  `IngenIA365ERP.Storage`, retención ≥ 5 años (ET art. 632 vía art. 1.5.3.8.2).
  **Dos modos de firma por cooperativa**: «software propio» (la cooperativa registra el ERP como
  su software —fabricante Ingenia365—, obtiene SoftwareID, fija PIN, pasa su set, y el servicio
  custodia **su** certificado y firma como «supplier»; `ProveedorXML` lleva NIT/DV de la
  cooperativa) y «proveedor tecnológico» (certificado y software de Ingenia365, «third party»,
  `ProveedorXML` con NIT de Ingenia365). Se **arranca con el primero**; el segundo se activa por
  configuración cuando exista la resolución de habilitación de Ingenia365 como PT, sin cambiar el
  contrato ERP↔servicio.
- **Contrato ERP ↔ servicio**: REST propio, autenticado por cooperativa (client-credentials o JWT
  RS256 de la identidad central con claim tenant): `POST /v1/tenants/{t}/documentos` (XML **sin
  firmar** con CUNE/SoftwareSC vacíos + idempotency-key tenant+tipo+número), `POST …/{id}/transmitir`,
  `GET …/{id}` (estado, CUNE, StatusCode, errores traducidos), `GET …/{id}/xml` y
  `/application-response`, `POST …/set-pruebas`, `GET/PUT …/habilitacion` (sin exponer PIN ni
  clave). El servicio completa CUNE y SoftwareSC (necesitan el PIN), valida XSD, firma y transmite;
  el ERP construye el XML y muestra estado, CUNE, ambiente y errores en lenguaje llano (diccionario
  NIExxx mantenido en el servicio; «Notificación» = advertencia).
- **Máquina de estados y reintentos**: `SendNominaSync` con timeout 60 s; `IsValid=true` →
  Aceptado (CUNE = XmlDocumentKey + ApplicationResponse guardado); `99` → Rechazado con reglas, sin
  reintento automático; timeout/5xx/red → EnProceso y **consulta** `GetStatus(CUNE)` con backoff
  (1, 5, 15, 60 min, máx. 24 h) antes de reenviar; reenvío del mismo ZIP sólo si GetStatus no lo
  encuentra; nunca Aceptado sin CUNE ni ApplicationResponse (Edge Cases). Notas de ajuste: reversión
  de una nómina ya transmitida → **Reemplazar** con los nuevos totales del mes (si el mes queda en
  cero para el empleado, **Eliminar**); el número reemplazado no se reutiliza; una nota puede
  ajustar a otra nota.
- **Numeración** (art. 1.5.3.3.1 num. 5): consecutivo **interno** del empleador, prefijo opcional,
  **sin rango autorizado por la DIAN** ni `GetNumberingRange` (es de factura). Los «rangos» de
  FR-028/FR-031 son internos: prefijo, desde, hasta, vigencia, **por tipo de documento y por
  ambiente** (los de habilitación no valen en producción). La propiedad del consecutivo **no** se
  llama `NextNumber` (dispararía `NingunModuloEscribeMovimientosFueraDelContrato`, que vigila
  `\.NextNumber\s*(=|\+\+|\+=)` en todo el fuente).
- **Plazo**: `DIAN_PLAZO_TRANSMISION_DIAS` = 10 con política `DianPlazoComputo` `Calendario` por
  defecto (texto vigente sin «hábiles»; Oficio 901706/2022) u `Habiles` (spec, doctrina privada por
  la Ley 4/1913 art. 62), con el calendario de festivos de R6; recordatorios desde el día 1 del mes
  siguiente; inconvenientes tecnológicos: 48 h desde el restablecimiento con soportes.
- **Representación gráfica y QR**: QuestPDF (ya en el stack) + generador QR (QRCoder, MIT);
  `CodigoQR` = `https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=CUNE`
  (`catalogo-vpfe-hab` en habilitación); contenido mínimo de los 13 datos del art. 1.5.3.3.1; el
  documento «no es un desprendible de pago» (Concepto 0106/2022): el comprobante del empleado sigue
  aparte.
- **Librerías**: firma XAdES-EPES con implementación propia sobre
  `System.Security.Cryptography.Xml.SignedXml` (BCL, MIT; `ds:Object` con
  `xades:QualifyingProperties` a mano y `GetIdElement` sobrescrito) —o `FirmaXadesNetCore` (LGPL v3,
  forks) si se acepta la dependencia—; cliente SOAP **a mano** (XDocument + SignedXml exc-c14n para el
  header + HttpClient `application/soap+xml`) porque el binding que publica el WSDL
  (TransportBinding + EndorsingSupportingTokens X.509 con thumbprint y SignedParts `To`) no lo
  reproduce el cliente WCF de .NET Core (dotnet/wcf #4828). Prueba de arquitectura: sólo el servicio
  referencia criptografía de firma y ningún proyecto del ERP conoce PIN ni certificado.
- **Secretos**: .p12, contraseña y PIN cifrados en la base del servicio con DataProtection (llavero
  propio, propósito «NominaElectronica.Certificado», patrón de `ADM_DataProtectionKeys`,
  `docs/operaciones/llavero-dataprotection.md`); llaves maestras y credenciales de base en el
  secreto de Kubernetes (o Vault/SOPS del GitOps) del pod del servicio, nunca en `appsettings.json`
  ni en el repo; el ERP sólo guarda el identificador del servicio y su credencial de cliente por
  ambiente. Rotación por vigencia y alerta 30 días antes del vencimiento.
- **Datos que faltan en el ERP** para armar el XML: segundo apellido y otros nombres separados
  (`Person` sólo tiene `FirstName/LastName`), DANE departamento (2) y municipio (5) del lugar de
  trabajo y del empleador (`Person.DaneCityCode` existe), TipoTrabajador/SubTipo, AltoRiesgoPension,
  TipoContrato DIAN 1-5 (el `ContractType` actual es del legado), medio de pago DIAN (tabla 5.3.3.2)
  por `PaymentMethod`, mapeo concepto → ruta del XML por plan de nómina; `Company.DianCode/
  DianResolutionNumber/DianInvoiceStart/End` son de factura y **no** sirven. No hay ningún código
  DIAN de nómina en el repositorio (`grep NominaIndividual|CUNE` sin resultados).

**Razón**: FR-031a exige que certificado y credenciales vivan sólo en el servicio, y el PIN entra
en el CUNE y en el SoftwareSC, así que el ERP no puede calcularlos: el XML se completa y se firma en
el servicio. La spec asume que Ingenia365 será PT, pero ser PT exige ser facturador electrónico
habilitado y PT de factura electrónica, patrimonio ≥ 20.000 UVT (≈ COP 1.047 millones con UVT
2026) con PPyE ≥ 10.000 UVT, ISO 27001 o compromiso a 18 meses, visita de la DIAN y hasta 2 meses
de trámite (art. 1.5.1.8.1.1), y el art. 1.5.3.8.1 exige que un tercero que transmita esté
«previamente habilitado»; COOFLOPAL debe transmitir noviembre/diciembre de 2026 en los primeros
días del mes siguiente: sin PT habilitado, el único camino legal es «software propio», y nada exige
un PT para operar un software de terceros (el fabricante puede ser Ingenia365). El XML sólo cambia
en `ProveedorXML`, el certificado y el `SignerRole`. La consulta de estado por CUNE (calculado
antes de enviar) permite saber si un documento quedó registrado sin arriesgar un duplicado.

**Alternativas consideradas**: sólo PT (bloquea la salida en vivo y obliga a asumir 20.000 UVT);
sólo software propio (funciona siempre; cada cooperativa compra y renueva certificado y hace su set
de 4 + 4); contratar un PT existente por API (Alegra, Loggro, Siigo, The Factory HKA: más rápido,
contradice el servicio propio y agrega costo por documento); biblioteca embebida en la API del ERP
(pone certificado y PIN en el mismo proceso, contra FR-031a); JSON canónico en vez de XML (centraliza
el anexo pero acopla el servicio a las reglas de nómina); gRPC/cola de mensajes (con 11-100
documentos al mes no compensa); MongoDB para Documento/Envio (rompe la homogeneidad EF/PostgreSQL);
HSM/Key Vault (mayor seguridad, más costo y latencia; posible después); reintento automático del
envío tras timeout (riesgo de duplicado); FirmaXadesNet/Microsoft.Xades (.NET Framework);
servicio en otro lenguaje (rompe el stack y las pruebas de arquitectura).

**Fuentes**: Res. 227/2025
(https://www.dian.gov.co/normatividad/Normatividad/Resoluci%C3%B3n%20000227%20de%2023-09-2025.pdf,
págs. 242-260 y 449; scratchpad `res227.txt` líneas 19199-19420, 21203-22280);
https://normograma.dian.gov.co/dian/compilacion/docs/resolucion_dian_0227_2025.htm ; Caja de
Herramientas (https://www.dian.gov.co/impuestos/factura-electronica/Documents/Caja-de-Herramientas-Nomina-Electronica-V1-0.zip);
https://micrositios.dian.gov.co/sistema-de-facturacion-electronica/documentacion-tecnica-soporte-de-pago-nomina-electronica/ ;
anexo (scratchpad `anexo.txt`) págs. 14-53, 96-118, 120-130, 178-187, 221-260; política de firma
(https://facturaelectronica.dian.gov.co/politicadefirma/v2/politicadefirmav2.pdf); WSDL
(https://vpfe-hab.dian.gov.co/WcfDianCustomerServices.svc?singleWsdl; scratchpad `wsdl-hab.xml`);
Oficio 901706/2022 (https://normograma.dian.gov.co/dian/compilacion/docs/oficio_dian_901706_2022.htm);
Res. 028/2022 (https://normograma.dian.gov.co/dian/compilacion/docs/resolucion_dian_0028_2022.htm);
guía de registro (https://www.dian.gov.co/impuestos/factura-electronica/Documents/Registro-y-Seleccion-del-Modo-de-Operacion-Nomina-Electronica.pdf);
PT (https://www.dian.gov.co/impuestos/factura-electronica/Documents/Preguntas_y_Respuestas_PT_2021.pdf ;
https://www.dian.gov.co/impuestos/factura-electronica/proveedores-tecnologicos/Paginas/Proveedores-tecnologicos-autorizados-DIAN.aspx);
https://actualicese.com/nomina-electronica-en-2026-cambios-normativos-y-puntos-clave-que-debes-revisar/ ;
https://felcowiki.thefactoryhka.com.co/index.php/N%C3%B3mina_Electr%C3%B3nica_seg%C3%BAn_Concepto_Unificado_DIAN_0106_%E2%80%93_19_agosto_2022 ;
librerías: https://www.nuget.org/packages/FirmaXadesNetCore/ ; https://github.com/icesasoftcorp/FirmaXadesNetCore ;
https://github.com/miguelhuertas/eFacturacionColombia_V2.Firma ; https://github.com/dotnet/wcf/issues/4828 ;
https://github.com/Stenfrank/soap-dian/blob/master/src/SOAPDIAN21.php ; https://github.com/wariox3/nobelio ;
código: `Employee.cs:58-111`; `Person.cs:35-99`; `Company.cs:46-59`;
`NingunModuloEscribeMovimientosFueraDelContrato.cs:16-60`; `docs/operaciones/llavero-dataprotection.md`.

## R11. Dispersión bancaria: formato parametrizable y AV Villas (FR-032, FR-033)

**Decisión**: formato en datos con vigencia, `PAY_BankFileFormats` (banco `COR_Banks`, nombre,
`ValidFrom/To`, tipo `AnchoFijo|Delimitado`, separador, codificación, fin de línea, decimales,
registros `Encabezado/Detalle/Totales` opcionales, y una lista ordenada de campos: origen
—constante, cuenta origen de la empresa, datos del empleado (`Person.TaxId`, tipo de documento,
nombre), `Employee.PayrollBankId → COR_Banks` (código de banco destino: **verificar** que
`Bank.TransferCode` (20) traiga el código ACH/Superintendencia; el legado guarda también
`FileStructure` (4)), `PayrollBankAccountType` 1 ahorros / 2 corriente, `PayrollBankAccountNumber`
(25), neto, referencia/concepto—, longitud, alineación, relleno, formato). Tabla
`PAY_DispersionFiles` (relación de pago/corrida, banco, formato usado, líneas con empleado y valor,
total, estado `Generado/Enviado`, referencia, nombre y hash del archivo, quién/cuándo; adjunto
cifrado). Empleados sin cuenta quedan **fuera** del archivo y en «pendientes» para otro medio.
«Marcar como enviado» llama `MarkPaymentsCommand(RunPublicId, empleadosDelArchivo, PaidAt,
Transfer, Reference = referencia del archivo)` (cabe en `PayrollPayment.Reference` de 60) y bloquea
la reversión como la marca manual (`Payroll.PaymentBlocksReversal`). Toda liquidación especial
genera su propio archivo con fecha propia (FR-011a). El **primer formato** es el del archivo plano
de pagos de **Banco AV Villas Empresas**: **no encontrado** en fuentes públicas ni en el repo; la
estructura la aporta el dueño, y el formato se carga como fila de datos, no como código. Reporte
`ArchivoDeDispersionReportQuery` → `TablaExportable` (FR-034).

**Razón**: la spec exige formato parametrizable (columnas, orden, separador, longitudes, cuenta
origen, vigencia) y que un cambio de banco a mitad de año conserve el formato de los archivos
anteriores (Edge Cases); un layout en datos —el mismo enfoque que la PILA— lo cumple sin
despliegue. La relación de pago y la marca por referencia ya existen y se reutilizan tal cual
(`PaymentQueriesAndCommands.cs:13-41, 51-100`; `PayrollPayment.cs:120-141`). La ficha ya tiene
banco, tipo y número de cuenta y la relación de pago ya los muestra (`Employee.cs`,
`EmpleadoDetalle.razor:321-336`); `Person` no tiene la cuenta del empleado (sólo `SupplierBank*`).

**Alternativas consideradas**: una clase por banco (cada banco nuevo es un despliegue); generar el
archivo desde Excel a mano (lo que hace hoy la cooperativa; sin marca masiva ni trazabilidad);
integrar con el portal del banco (fuera de alcance: la respuesta del banco no se procesa).

**Fuentes**: spec Clarifications (AV Villas), FR-032, FR-033, Edge Cases;
`PaymentQueriesAndCommands.cs:13-41, 51-100, 167`; `PayrollPayment.cs:120-141`;
`PayrollPaymentsEndpoints.cs:16-73`; `Employee.cs:23-153`; `Person.cs:165-177`;
`Entities/Core/Bank.cs:9-66` (`TransferCode`, `FileStructure`). Informe de dispersión: **no
recibido** por este redactor.

## R12. Permisos y auditoría (FR-006, FR-035)

**Decisión**: recursos nuevos en `PayrollPermissionCatalogSeeder` —`Payroll.Settlements`
(View/Calculate/Approve/Reverse/Export; cubre prima, cesantías, vacaciones y definitiva, con
`Payroll.Settlements.AdjustDeduction` para bajar el descuento de Cartera), `Payroll.Vacations`
(View/Register/Cancel), `Payroll.OpeningBalances` (View/Manage), `Payroll.TerminationReasons`
(View/Manage), `Payroll.WithholdingRates` (View/Calculate/Approve), `Payroll.Pila`
(View/Generate/MarkUploaded), `Payroll.ElectronicPayroll` (View/Generate/Transmit/ManageEnablement),
`Payroll.Dispersion` (View/Generate/MarkSent), `Payroll.BankFileFormats` (View/Manage),
`Payroll.Holidays` (View/Manage), `Payroll.CompanyPolicies` (View/Manage)— y patrones de roles:
`CompanyAdmin *`; `Operator`: ver, registrar, calcular y generar; **aprobar, reversar, transmitir,
marcar enviado/cargado, ajustar descuentos y administrar políticas sólo CompanyAdmin**; `Auditor`
y `ReadOnly` `*.View` (más `Payroll.Settlements.Export` para Auditor, como `Payroll.Runs.Export`).
Segregación como la ordinaria (`Payroll.AllowSameUserApproval`), comparando aprobador con
`CalculatedBy` (no hay novedades de período en una especial). Eventos nuevos en
`AuditEventTypes` emitidos con `PayrollAuditEmitter`: `Payroll.Settlement.Calculated/Approved/
Reversed/DeductionAdjusted`, `Payroll.Employee.Terminated/Reinstated`,
`Payroll.OpeningBalance.Changed`, `Payroll.Vacation.Registered/Cancelled`,
`Payroll.WithholdingRate.Calculated/Approved`, `Payroll.Pila.Generated/Uploaded`,
`Payroll.ElectronicPayroll.Generated/Transmitted/StatusChanged/EnablementChanged`,
`Payroll.Dispersion.Generated/Sent`, `Payroll.CompanyPolicy.Changed`, `Payroll.Holiday.Changed`.
Namespaces nuevos (`Payroll.Settlements`, `Payroll.Vacations`, `Payroll.Terminations`,
`Payroll.WithholdingRates`, `Payroll.Pila`, `Payroll.ElectronicPayroll`, `Payroll.Dispersion`,
`Payroll.Policies`, `Payroll.OpeningBalances`) agregados a
`PrincipioVIII_DualValidation.InScopeNamespacePrefixes` **en el primer commit**, con
`AbstractValidator<T>` para cada comando/consulta. La navegación queda auditada sola
(`RegistroDeAccesos`).

**Razón**: FR-035 exige permisos propios por proceso (ver, registrar, aprobar, reversar, generar,
transmitir) y auditoría de toda escritura, generación y transmisión; el molde existe
(`PayrollPermissionCatalogSeeder.cs:27-59`, `BuiltInRolesSeeder.cs:81-99`,
`AuditEventTypes.cs:102-110`). La prueba de validación dual no cubre namespaces que no estén
listados (`PrincipioVIII_DualValidation.cs:23-51`): un comando sin validador pasaría en verde.

**Alternativas consideradas**: un solo recurso `Payroll.Settlements` para todo (impide dar a un
operador PILA sin darle definitivas); agregar namespaces al cierre de la feature (riesgo de
comandos sin validador en QA).

**Fuentes**: `PayrollPermissionCatalogSeeder.cs:27-59`; `BuiltInRolesSeeder.cs:81-99`;
`AuditEventTypes.cs:102-110`; `PrincipioVIII_DualValidation.cs:23-51, 58-87`;
`ApprovePayrollRunCommand.cs:101-147` (segregación).

## R13. Pantallas y reportes (FR-034)

**Decisión**: pantallas nuevas bajo `Pages/Nomina` copiando la estructura de `Liquidacion.razor`
(`.page-header` con acciones por estado, **un** `<IndicadorDeCarga>` por zona con `EstadoDeCarga`,
`.info-card` de estado, `SfTab` Resumen/Empleados/Relación de pago/Historial, `SfDialog` de
detalle con explicación paso a paso, diálogos con su propio indicador, `PanelDeAprobacion`) y
partials nuevos de `NominaClient` (`Prima`, `CesantiasAnuales`, `Vacaciones`, `Definitiva`,
`Retencion2`, `Pila`, `NominaElectronica`, `Dispersion`, `SaldosIniciales`, `Festivos`,
`Politicas`). Rutas: `/nomina/prima`, `/nomina/cesantias-anuales` (**`/nomina/cesantias` ya es el
catálogo de fondos**, `Cesantias.razor:1-12`), `/nomina/vacaciones`, `/nomina/liquidacion-definitiva`
(desde la ficha y desde el menú), `/nomina/retencion-procedimiento-2`, `/nomina/pila`,
`/nomina/nomina-electronica`, `/nomina/dispersion` (también como acción en la relación de pago),
`/nomina/saldos-iniciales`, `/nomina/festivos`, `/nomina/politicas`. Enlace del menú sólo cuando
la página existe (`TodoEnlaceDelMenuTieneSuPagina`). Reportes: consultas nuevas
`IRequest<Result<TablaExportable>>` en `ReportesDeNomina` —liquidación especial por tipo
(resumen y detalle), consignación de cesantías por fondo, PILA (líneas y cuadre), nómina electrónica
(estado por documento), dispersión (líneas del archivo), cálculo de porcentaje P2 mes a mes—,
exportadas por `/api/reports/payroll/{vista}?format=` con `ExportadorDeTablas` (Excel/Word/PDF).
PDF propios con QuestPDF: documento de liquidación definitiva para firma (FR-020) y representación
gráfica de la nómina electrónica con QR (R10); el comprobante del empleado de prima, intereses y
vacaciones reutiliza `PayslipModelBuilder`/`IPayslipPdfRenderer` con la etiqueta del tipo. La ficha
del empleado muestra saldo de vacaciones, saldos iniciales, porcentaje P2 vigente y los campos
nuevos de PILA/DIAN.

**Razón**: `LasPantallasDicenQueEstanCargando` exige `IndicadorDeCarga` en toda `.razor` bajo
`Pages/Nomina` con `SfGrid` o `DialogTemplates` (`LasPantallasDicenQueEstanCargando.cs:23, 37-71`);
el sistema de diseño (`tokens.css`, `componentes.css`) y la estructura de `Liquidacion.razor`
(`Liquidacion.razor:1-8, 82-325`) ya lo satisfacen; el centro de reportes acepta vistas nuevas
agregando consultas del mismo tipo (`ReportesDeNomina.cs:17-202`, `ExportadorDeTablas.cs`).

**Alternativas consideradas**: una sola pantalla «Liquidaciones especiales» con selector de tipo
(menos archivos, pero cada tipo tiene columnas y acciones distintas —consignación por fondo,
documento para firma, saldo de días— y el manual las nombra por separado).

**Fuentes**: `Liquidacion.razor:1-8, 12-45, 82-325, 331-507, 510-560`; `Cesantias.razor:1-12`;
`NominaClient.Liquidacion.cs:9-34`; `NominaClient.Pagos.cs:8-26`; `ReportesDeNomina.cs:17-202`;
`Application/Common/Reports/TablaExportable.cs:36`; `API/Reports/Exportadores/ExportadorDeTablas.cs`;
`Interfaces.cs:228-278`; `PayslipModelBuilder.cs:13-23`; `LasPantallasDicenQueEstanCargando.cs:23, 37-71`;
`docs/manual/indicador-de-carga.md`.

## R14. Pruebas: casos dorados, e2e y validadores externos

**Decisión**:

- **Casos dorados de liquidaciones** en `Domain.Tests/Payroll/Settlements/Casos/*.json` con un
  esquema hermano de `CasoDorado` (tipo, fecha de corte, empleado con historial de salarios, bases
  prestacionales por mes de 6/12 meses, suspensiones, saldos iniciales, movimientos de vacaciones,
  provisiones acumuladas, motivo de retiro, deudas propuestas, `parametros` override, `esperado`
  por línea y totales) con los ejemplos calculados a mano de esta investigación como mínimo: prima
  fija (1.124.547,50), prima con ingreso a mitad y cambio de salario (630.404,72), cesantías estable
  (2.749.095), ingreso en septiembre (666.666,67 / 26.666,67), cambio de salario en los últimos tres
  meses (2.315.761,67 / 277.891,40), vacaciones 18 meses (22,5) y disfrute 28-10 a 10-11 (11 L–S /
  9 L–V), indemnización indefinida 810 días (4.400.000), término fijo (15.600.000), ≥ 10 SMMLV
  (30.000.000), definitiva integrada (retiro 15-12-2026), retención de prima (52.000), cesantías
  gravadas (0 y 1.124.000), indemnización con 401-3 (7.800.000), P2 con las dos secuencias
  (3,71 % / 3,44 %), salario integral y aprendices por etapa excluidos/incluidos.
- **Casos dorados de PILA** (`Domain.Tests/Payroll/Pila/Casos/`): mes con once empleados, uno con
  ingreso el 10, uno con IGE de 3 días, uno retirado el 20, uno integral, uno pensionado activo
  subtipo 1, uno con vacaciones; archivo esperado byte a byte con layout v30; un caso **abril 2027**
  con la tabla FSP de la Ley 2381 y un empleado en transición; empresa exonerada y no exonerada.
- **Casos dorados DIAN**: construcción del XML contra los XSD v1.0.6 embebidos; CUNE y SoftwareSC
  con un **documento aceptado en habilitación** (el ejemplo del anexo no cuadra: **falta**); firma
  verificada con `SignedXml.CheckSignature` y `SigPolicyHash` fijo; nota Reemplazar y Eliminar.
- **Pruebas de Application** con `NominaTestData` (InMemory, reloj fijo, servicios reales): cálculo/
  persistencia por tipo, FR-005 (segunda liquidación rechazada), FR-021 (retiro en período aprobado),
  aprobación que contabiliza en una transacción, reversión que reabre la ficha, descuento de Cartera
  aplicado con `ProcessPaymentCommand`, cierre de vigencia P2, cuadre PILA vs `NM`.
- **e2e** (`API.IntegrationTests`, colección «Nomina e2e», PostgreSQL/Mongo/Redis reales): ciclo
  completo prima → pago → dispersión → nómina electrónica generada (sin transmitir); definitiva con
  préstamo; PILA del mes con archivo descargado; SC-003 comprobado tras cada operación (provisión en
  el libro = suma de liquidaciones pendientes, por cuenta/total).
- **Arquitectura**: `LaNominaNoTieneValoresLegalesFijos` sigue verde (literales prohibidos: `180m`,
  `6m`, `3m`, `20m`, `10m`, `70m`, `12.0m`, subcadenas `0.12`, `200000`… también en strings y
  comentarios de fin de línea, `LaNominaNoTieneValoresLegalesFijos.cs:24-35, 43-79`);
  `NingunModuloEscribeMovimientosFueraDelContrato` (ningún `NextNumber` incrementado);
  `PrincipioVIII_DualValidation` con los namespaces nuevos; `LasPantallasDicenQueEstanCargando`;
  prueba nueva «sólo el servicio de nómina electrónica referencia criptografía de firma».
- **Validadores externos** (no automatizables desde el repo): botón **Validar** de Aportes en Línea
  con la cuenta de COOFLOPAL (SC-004); set de pruebas en `catalogo-vpfe-hab` (4 nóminas + 4 notas
  aceptadas para NO OFE; SC-005); portal AV Villas Empresas (SC-007). Se documentan como pasos del
  quickstart con evidencia (captura/ZipKey/CUNE) y **segundo revisor** para la primera transmisión
  en producción (Principio XII).

**Razón**: la nómina ordinaria ya se prueba así (22 JSON, `CasoDorado.ConstruirEntrada`,
`NominaTestData.cs:29-130`) y `CalculationInput` no cubre las liquidaciones; la spec exige
coincidencia «al peso» con la contadora (SC-001) y verde permanente de la prueba de literales
(SC-008). El caso 20 (quincena, 3.000.000 → provisión de prima 135.325) es la referencia para
cuadrar provisión acumulada vs. prima liquidada.

**Alternativas consideradas**: probar sólo en e2e (lento, sin explicación por valor); omitir el caso
2027 (deja una bomba conocida).

**Fuentes**: `NominaTestData.cs:29-130`; `Domain.Tests/Payroll/Calculation/CasoDorado.cs`;
`Casos/20-quincena-con-provision-de-prima.json`; `LaNominaNoTieneValoresLegalesFijos.cs:24-35, 43-79`;
`NingunModuloEscribeMovimientosFueraDelContrato.cs:16-60`; `PrincipioVIII_DualValidation.cs:23-51`;
`LasPantallasDicenQueEstanCargando.cs:23, 37-71`; CLAUDE.md «Totales» (fixture única
`CentralIdentityApiFixture`, colección «Nomina e2e»).

## Contabilización de las liquidaciones (transversal a R1, R2, R7; FR-004)

**Decisión**: sobrecarga de `PayrollAccountingPoster.PostAsync(AccountingOrigin origen,
IReadOnlyList<(Employee, PayrollRunLine[])>, DateOnly fecha, string detalle)` con `SourceType` por
tipo de liquidación, manteniendo `AccountingPoster.PrepareAsync/PrepareReversalAsync` como único
camino al libro. La cancelación de la provisión se parametriza con **conceptos pares** en
`PAY_ConceptDefinitionAccounts`: p. ej. `PRIMA` → débito provisión / crédito CxP empleado;
`PRIMA_AJUSTE_PROV` → débito gasto / crédito provisión, con signo negativo cuando la provisión
supera lo liquidado (el poster invierte débito/crédito con valor negativo). Igual para
`CESANTIAS` (CxP al fondo), `INT_CESANTIAS`, `VACACIONES`, `INDEMNIZACION` (gasto directo),
`SALARIO_PENDIENTE`, y las retenciones de cada liquidación (`RETEFTE_PRIMA`,
`RETEFTE_CESANTIAS`, `RETEFTE_INDEMNIZACION`). La **provisión acumulada por empleado** se calcula
sumando `PAY_PayrollRunLines` (Nature `Provision`, corridas `Approved` no reversadas) por
`EmployeeId` y concepto, más el saldo inicial, menos lo consumido por liquidaciones aprobadas; el
cuadre SC-003 se hace por cuenta/total. **No** se cambia el tercero de las provisiones en esta
feature. `PAY_ConceptDefinitionAccounts` quedó vacía tras `ContabilidadNiif`: la contadora debe
parametrizar también los conceptos nuevos (`docs/operaciones/nomina-primer-periodo.md`).

**Razón**: las provisiones se contabilizan **sin tercero** (`TercerosDeNomina.EntidadDe` devuelve
`Ninguna` para `PROV_*`, `TercerosDeNomina.cs:17-27`; `PayrollAccountingPoster.cs:51-65`), así que
el libro no tiene saldo por empleado; cambiarlo ahora alteraría sólo los asientos futuros y dejaría
la producción con dos criterios (decisión contable de la contadora). Una pareja de cuentas por
concepto es el modelo actual (`PayrollConceptDefinitionAccount.cs:130-144`): dos conceptos
resuelven «cancelar provisión + diferencia al gasto» sin nuevo mapa ni nuevo escritor.

**Alternativas consideradas**: poner al empleado como tercero de las provisiones desde la 010 con
un asiento de reclasificación de los saldos anteriores (más limpio a largo plazo; afecta comprobantes
ya aprobados en PDN); un mapa de cuentas propio de liquidaciones (otro sitio que parametrizar y otro
escritor que vigilar).

**Fuentes**: `PayrollAccountingPoster.cs:25-124`; `TercerosDeNomina.cs:17-27`;
`Accounting/Posting/PostingRequest.cs:7-60`; `PayrollConceptDefinitionAccount.cs:130-144`;
`ApprovePayrollRunCommand.cs:101-147`; CLAUDE.md «Contabilidad (feature 009)».

## Lo que falta del dueño

1. **Estructura del archivo plano de pagos de Banco AV Villas Empresas** (columnas, separador o
   ancho fijo, longitudes, cuenta origen, tipo de identificación del titular, códigos de banco
   destino, totales): no encontrada en fuentes públicas ni en el repo; sin ella el primer formato de
   R11 no se puede cargar ni probar (SC-007). Verificar si `COR_Banks.TransferCode` trae el código
   ACH de cada banco.
2. **Registro DIAN y modo de operación**: decidir si Ingenia365 tramita la habilitación como
   **proveedor tecnológico** (exige ser facturador electrónico habilitado y PT de factura,
   patrimonio ≥ 20.000 UVT ≈ COP 1.047 millones con PPyE ≥ 10.000 UVT, ISO 27001 o compromiso a 18
   meses, visita DIAN, ~2 meses) o se arranca con **software propio** por cooperativa (R10). No se
   verificó que Ingenia365 figure en la lista de PT autorizados.
3. **Cuenta de COOFLOPAL en el catálogo DIAN** (`catalogo-vpfe-hab.dian.gov.co`): registro como
   NO OFE u OFE (no se sabe si factura electrónicamente), «Nómina Electrónica → Emisor», modo,
   registro/asociación del software, y entrega de **SoftwareID, PIN (si software propio) y
   TestSetId**; sin eso no hay set de pruebas (SC-005).
4. **Certificado(s) de firma digital**: quién compra, a qué ECD acreditada por ONAC (Certicámara,
   GSE, Andes SCD, Camerfirma), de persona jurídica —de Ingenia365 si PT, de cada cooperativa si
   software propio—, .p12 con contraseña, vigencia y precio (no encontrado en fuente oficial).
5. **Set de pruebas DIAN**: un documento **aceptado** en habilitación (XML firmado +
   ApplicationResponse) para caso dorado de CUNE, SoftwareSC y firma (el ejemplo oficial no cuadra);
   y observar/registrar los `StatusCode` distintos de 00/99.
6. **Validador de Aportes en Línea**: acceso a la cuenta de COOFLOPAL (perfil Nómina) para el botón
   Validar (no hay validador ni archivo de ejemplo público); ideal una planilla ya pagada
   («Planilla Integrada» descargable) como patrón dorado de posiciones, formato de tarifas (7
   posiciones, decimales no fijados por el anexo) y códigos PILA de administradoras.
7. **Datos del aportante para la PILA**: tipo (1), clase (B), forma de presentación (U/S), código
   PILA de la ARL y centros de trabajo, actividad económica Decreto 768/2022, DIVIPOLA de la sede, si
   COOFLOPAL está marcada exonerada (campo 33) ante el operador; y **por empleado**: códigos PILA de
   EPS/AFP/ARL/CCF, régimen de transición Ley 2381 (S/N), tipo de salario F/V/X, subtipo (¿algún
   pensionado activo?), aprendices y su etapa, extranjeros; tarifa ARL efectiva fijada por la ARL.
8. **Confirmaciones de la contadora (Rafaela Lastra España)**: (a) exoneración art. 114-1 para
   COOFLOPAL (spec sí; comentario del código no; norma encontrada sí); (b) modo de control de los
   topes 790/1.340 UVT (acumulado vs mensualizado); (c) secuencia de depuración del P2; (d) ARL en
   vacaciones; (e) si la nómina paga los días calendario del disfrute o sólo los hábiles; (f) plazo
   DIAN calendario vs hábiles; (g) tratamiento de prestaciones y conceptos no salariales en el
   documento DIAN del mes; (h) aprendices SENA y su etapa tras la Ley 2466/2025; (i) cuentas
   contables de los conceptos nuevos en `PAY_ConceptDefinitionAccounts`; (j) validar los valores de
   la tabla de R4 antes de sembrar; (k) convención colectiva, pacto o RIT con jornada L–V o
   prestaciones extralegales.
9. **Vigencias 2027** de SMMLV, auxilio de transporte y UVT (se conocen a fines de diciembre de
   2026): decidir si entran por semilla (`Revisiones()`) o por la pantalla de parámetros antes de
   los intereses de enero y la consignación de febrero.
10. **Infraestructura del servicio de nómina electrónica**: mismo clúster/namespace que la API con
    NetworkPolicy propia o namespace aparte; llaves maestras en secreto Kubernetes vs Vault/SOPS en
    GitOps; si se quiere HSM/Key Vault; y **segundo revisor** de la primera transmisión en
    producción (Principio XII).
11. **Textos oficiales limpios**: Res. 1529/2026 y Res. 010/2026 de MinSalud (los PDF públicos son
    escaneos con OCR defectuoso; el consolidado v30 basta para construir, no para citar); artículos
    190-191 CST (acumulación de vacaciones); numeración exacta en el Decreto 780/2016 del art. 71 del
    Decreto 806/1998 (licencia no remunerada); norma del redondeo de la retención al múltiplo de mil;
    número del decreto transitorio de 2026 del SMMLV.

## Riesgos

- **Calendario legal ajustado**: salida en vivo 01-12-2026; prima antes del 20-12-2026; nómina
  electrónica de diciembre en los primeros 10 días de enero (calendario, lectura estricta);
  intereses antes del 31-01-2027; cesantías antes del 15-02-2027. La habilitación DIAN (cuenta,
  certificado, set 4 + 4) depende de trámites externos del dueño y puede no estar lista: sin
  transmisión en producción se incumple la Res. 227/2025 (sanción art. 651 ET, según doctrina no
  verificada en fuente DIAN). Mitigación: modo software propio desde el inicio y sets de prueba en
  noviembre.
- **Ser PT no es viable a corto plazo** (patrimonio, ISO 27001, visita, 2 meses): si el dueño insiste
  en el modo PT antes de la habilitación, no hay camino legal para transmitir. La spec debe aceptar
  el modo dual.
- **Spec desactualizada frente a la norma** (aprendices Ley 2466/2025, ÷ 13, plazo DIAN, FSP y
  reforma pensional 2027, redondeos PILA): planificar sobre el texto actual produce un generador que
  el operador rechaza o que descuenta mal; corregir la spec antes de la Fase 1.
- **Reforma pensional desde el 01-04-2027** (Ley 2381/2024; C-264/2026): la PILA cambia (segunda
  tabla FSP, bandera de transición, reparto Colpensiones/ACCAI que hace el operador); la
  reglamentación no está expedida y tres artículos volvieron al Congreso; los casos dorados de 2027
  pueden requerir ajuste.
- **Anexo PILA que cambia varias veces al año** (v30 en julio 2026; longitudes nuevas desde
  oct-2026): mitigado con layout en datos versionado, pero cada versión exige alguien que la cargue y
  la pruebe contra el validador.
- **Variantes doctrinales sin norma que las cierre** (topes anuales de retención, secuencia P2, ARL
  en vacaciones, días calendario vs hábiles del disfrute, exoneración de la cooperativa): si la
  contadora no decide antes de sembrar, los valores «al peso» de SC-001 no coinciden. Mitigación:
  políticas por empresa con vigencia y explicación que nombra el modo elegido.
- **Provisiones sin tercero**: la provisión acumulada por empleado se reconstruye desde las líneas de
  corrida; sin saldos iniciales digitados (FR-007) las primeras liquidaciones salen cortas y la
  cancelación contable no cuadra por empleado; el cuadre SC-003 sólo es por cuenta/total.
- **Cambio de esquema en tablas de producción** (`PAY_PayrollRuns.PayPeriodId` nullable, índice
  único nuevo, `Kind`): migración par PostgreSQL/SQL Server con datos vivos; las consultas actuales
  que asumen período deben filtrar `Ordinary`. Exige respaldo y prueba `DosContextosUnaTablaTests`
  para el default de `Kind`.
- **Pruebas de arquitectura sensibles**: `LaNominaNoTieneValoresLegalesFijos` cae con `180m`,
  `6m`, `20m`, `10m`, `70m`, `0.12` (incluso en GUIDs y comentarios) bajo `Domain/Payroll` y
  `Application/Payroll`; `NingunModuloEscribeMovimientosFueraDelContrato` cae con cualquier
  `NextNumber++`; `PrincipioVIII` no cubre namespaces no listados. Cualquier módulo nuevo colocado
  bajo `Application/Payroll/` queda sujeto a todas.
- **Nuevos códigos en `Required`** negarían la nómina ordinaria de las cooperativas desplegadas hasta
  que el seeder corra; mitigado con listas por proceso.
- **Certificados y PIN**: un certificado vencido o revocado hace fallar la firma y la DIAN rechaza;
  el PIN filtrado permite fabricar CUNEs válidos; renovación por cooperativa en modo software propio.
  Mitigación: cifrado con DataProtection en el servicio, alerta 30 días antes, rotación por vigencia.
- **Servicio SOAP de la DIAN inestable**: sin reintento automático de envío (riesgo de duplicado), los
  documentos pueden quedar «en proceso» hasta que alguien mire; mitigado con consulta automática
  `GetStatus(CUNE)` con backoff (no reenvía).
- **Formato AV Villas desconocido**: hasta que el dueño lo entregue, la US8 sólo puede probarse con un
  formato ficticio; SC-007 depende del portal del banco.
- **Aportes en Línea sin validador público**: SC-004 sólo se comprueba con la cuenta del aportante;
  las validaciones replicadas (Bloqueante/Alerta) pueden divergir de las del operador (campos 51/52,
  15) y hay que registrar las diferencias observadas.
- **Datos maestros incompletos** (segundo apellido, DANE 2/5, DIVIPOLA, códigos PILA, tipo de
  cotizante, actividad económica, motivo de retiro): la migración de datos y la digitación previa al
  01-12-2026 son trabajo de la cooperativa; sin ellos PILA y DIAN se bloquean en la validación previa.
